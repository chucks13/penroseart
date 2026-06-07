// Receives RaveSystem OSC broadcasts through the new RaveSystem.Osc stack.

using System;
using System.Net;
using System.Threading;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch; // aliased: a plain using would make Debug ambiguous with UnityEngine.Debug

/// <summary>
/// Unity component that listens for RaveSystem on-air OSC broadcasts and exposes the latest snapshot.
/// </summary>
public sealed class RaveOscReceiver : MonoBehaviour
{
    private const int RaveBroadcastPort = 7000;
    private const float RaveBroadcastRateHz = 60f;

    // Liveness grace window: 15 broadcast intervals (0.25 s). Wide enough that UDP burst loss and
    // editor/runtime frame hitches cannot flap the beat source (each flap wipes the cooked phrase and
    // Levels state downstream), while a real RaveSystem disconnect still falls back within a quarter
    // second. The original 3-interval (50 ms) window flapped on any frame hitch longer than 50 ms.
    private const float BroadcastSilenceTimeoutSeconds = 15f / RaveBroadcastRateHz;

    /// <summary>Sentinel for "no recognized packet yet"; kept out of arithmetic to avoid tick overflow.</summary>
    private const long NoPacketTimestamp = long.MinValue;

    private readonly object errorLock = new object();
    private OscUdpSocket socket;
    private RaveOscPacketParser parser;
    private RaveOnAirSnapshot latest = new RaveOnAirSnapshot();
    private Exception pendingError;
    private bool hasPendingError;
    private bool hasSnapshot;

    // Stopwatch timestamp of the last recognized Rave packet, written on the SOCKET thread at true
    // arrival time (Unity's Time.* clocks are main-thread-only). Stamping at arrival instead of at
    // main-thread snapshot consumption keeps liveness frame-rate independent: packets that land
    // during a long frame still count, so an editor hitch can no longer read as broadcast silence.
    private long lastRecognizedPacketTimestamp = NoPacketTimestamp;

    // Flap diagnostics: socket-thread packet counters and the last counts seen by ApplyTo. When
    // liveness drops, the deltas name the failing leg: raw stalled = nothing reached the socket
    // (network/OS); raw advancing but recognized stalled = packets arrive but stop dispatching.
    private long rawPacketCount;
    private long recognizedPacketCount;
    private long lastSeenRawCount;
    private long lastSeenRecognizedCount;
    private bool wasBroadcasting;

    /// <summary>True after at least one recognized Rave OSC value has been received.</summary>
    public bool HasSnapshot => hasSnapshot;

    /// <summary>The latest decoded Rave on-air values.</summary>
    public RaveOnAirSnapshot Latest => latest;

    /// <summary>True while recognized RaveSystem OSC is arriving on UDP 7000.</summary>
    public bool IsBroadcasting => IsBroadcastingAt(hasSnapshot, SecondsSinceLastRecognizedPacket());

    private void Awake()
    {
        StartListening();
    }

    private void Update()
    {
        if (parser != null && parser.TryTakeSnapshot(out var snapshot))
        {
            latest = snapshot;
            hasSnapshot = true;
        }

        Exception error = null;
        lock (errorLock)
        {
            if (hasPendingError)
            {
                error = pendingError;
                pendingError = null;
                hasPendingError = false;
            }
        }

        if (error != null)
        {
            Debug.LogError($"[RaveOscReceiver] Failed to decode Rave OSC packet: {error}");
        }
    }

    private void OnDestroy()
    {
        StopListening();
    }

    /// <summary>
    /// Chooses the shared beat source from RaveSystem transport liveness and pushes the latest decoded
    /// snapshot into the shared beat data while recognized packets are arriving.
    /// </summary>
    /// <remarks>
    /// Source selection is transport-only: any recognized Rave on-air OSC value on UDP 7000 makes the
    /// BeatManager live immediately. Payload sentinels such as <c>bpm = -1</c> are valid data and are
    /// applied exactly as received; cooked queries expose unavailable values as null instead of letting
    /// the simulator overwrite an active OSC stream.
    /// </remarks>
    public void ApplyTo(BeatManager beatManager)
    {
        if (beatManager == null)
        {
            return;
        }

        var broadcasting = IsBroadcasting;
        LogLivenessTransitionDiagnostics(broadcasting);

        beatManager.SetLiveBeatSource(broadcasting);
        if (!broadcasting || !hasSnapshot)
        {
            return;
        }

        ApplySnapshotToBeatData(latest, beatManager.beatData);
    }

    /// <summary>Returns true while the last recognized Rave OSC value is within the liveness grace window.</summary>
    public static bool IsBroadcastingAt(bool hasSnapshot, float secondsSinceLastRecognizedPacket)
    {
        return hasSnapshot && secondsSinceLastRecognizedPacket <= BroadcastSilenceTimeoutSeconds;
    }

    /// <summary>
    /// Logs one line with hard numbers at every liveness edge, so a flap names its own cause instead of
    /// requiring another round of theory: seconds since the last recognized packet plus the raw/recognized
    /// packet deltas since the previous main-thread check.
    /// </summary>
    private void LogLivenessTransitionDiagnostics(bool broadcasting)
    {
        if (broadcasting == wasBroadcasting)
        {
            return;
        }

        var raw = Interlocked.Read(ref rawPacketCount);
        var recognized = Interlocked.Read(ref recognizedPacketCount);
        var rawDelta = raw - lastSeenRawCount;
        var recognizedDelta = recognized - lastSeenRecognizedCount;
        lastSeenRawCount = raw;
        lastSeenRecognizedCount = recognized;
        wasBroadcasting = broadcasting;

        if (broadcasting)
        {
            Debug.Log($"[RaveOscReceiver] Liveness UP: raw +{rawDelta} / recognized +{recognizedDelta} packets since drop (totals {raw}/{recognized}).");
        }
        else
        {
            Debug.LogWarning(
                $"[RaveOscReceiver] Liveness DROP: {SecondsSinceLastRecognizedPacket():0.000}s since last recognized packet; " +
                $"raw +{rawDelta} / recognized +{recognizedDelta} packets since liveness came up (totals {raw}/{recognized}). " +
                "raw stalled = nothing reached the socket (network/OS); raw moving but recognized stalled = dispatch is rejecting packets.");
        }
    }

    /// <summary>Seconds since the socket thread stamped a recognized packet; infinity before the first one.</summary>
    private float SecondsSinceLastRecognizedPacket()
    {
        var last = Volatile.Read(ref lastRecognizedPacketTimestamp);
        if (last == NoPacketTimestamp)
        {
            return float.PositiveInfinity;
        }

        return (float)((Stopwatch.GetTimestamp() - last) / (double)Stopwatch.Frequency);
    }

    /// <summary>Stores raw Rave OSC values and derives the compatibility beat fields Penrose effects already use.</summary>
    public static void ApplySnapshotToBeatData(RaveOnAirSnapshot snapshot, BeatData beatData)
    {
        if (snapshot == null || beatData == null)
        {
            return;
        }

        beatData.CopyFrom(snapshot);

        var hasUsableBeat = beatData.bpm > 0f;
        beatData.active = hasUsableBeat;
        beatData.currentBeat = beatData.beatInBar >= 1 && beatData.beatInBar <= beatData.beatsPerMeasure
            ? beatData.beatInBar - 1
            : 0;
        DeriveOffBeats(beatData, hasUsableBeat);
    }

    private static void DeriveOffBeats(BeatData beatData, bool hasUsableBeat)
    {
        var offBeatCounts = new[] { -1, -1, -1, -1 };
        var offBeatGates = new bool[4];
        beatData.offBeatPulse = 0f;
        if (!hasUsableBeat || beatData.beatAverageMs <= 0 || beatData.beatsCountMs == null || beatData.beatsCountMs.Length < 4)
        {
            beatData.offBeatsCountMs = offBeatCounts;
            beatData.offBeats = offBeatGates;
            return;
        }

        var activeWindowMs = beatData.beatAverageMs * 0.25f;
        var measureMs = beatData.beatAverageMs * 4f;
        var nearestOffBeatMs = float.MaxValue;

        for (var i = 0; i < offBeatCounts.Length; i++)
        {
            var nextBeatIndex = (i + 1) % offBeatCounts.Length;
            var startBeatMs = beatData.beatsCountMs[i];
            var nextBeatMs = beatData.beatsCountMs[nextBeatIndex];
            if (startBeatMs < 0 || nextBeatMs < 0)
            {
                continue;
            }

            var beatGapMs = (float)(nextBeatMs - startBeatMs);
            if (beatGapMs <= 0f)
            {
                beatGapMs += measureMs;
            }

            var halfGapMs = beatGapMs * 0.5f;
            var offBeatMs = nextBeatMs - halfGapMs;
            if (offBeatMs < 0f)
            {
                offBeatMs += measureMs;
            }
            nearestOffBeatMs = Mathf.Min(nearestOffBeatMs, offBeatMs);

            if (nextBeatMs > halfGapMs)
            {
                offBeatCounts[i] = Mathf.RoundToInt(offBeatMs);
                continue;
            }

            var elapsedSinceOffBeatMs = halfGapMs - nextBeatMs;
            if (elapsedSinceOffBeatMs <= activeWindowMs)
            {
                offBeatCounts[i] = 0;
                offBeatGates[i] = true;
                continue;
            }

            offBeatCounts[i] = Mathf.RoundToInt(measureMs - elapsedSinceOffBeatMs);
        }

        if (nearestOffBeatMs != float.MaxValue)
        {
            var nextOffBeatInCycleMs = nearestOffBeatMs % beatData.beatAverageMs;
            var elapsedSinceNearestOffBeatMs = nextOffBeatInCycleMs <= 0f ? 0f : beatData.beatAverageMs - nextOffBeatInCycleMs;
            beatData.offBeatPulse = GetPulse(elapsedSinceNearestOffBeatMs, beatData.beatAverageMs);
        }

        beatData.offBeatsCountMs = offBeatCounts;
        beatData.offBeats = offBeatGates;
    }

    private static float GetPulse(float elapsedMs, float durationMs)
    {
        if (durationMs <= 0f)
        {
            return 0f;
        }

        var phase = Mathf.Clamp01(elapsedMs / durationMs);
        var smoothStep = phase * phase * (3f - (2f * phase));
        return 1f - smoothStep;
    }

    private void StartListening()
    {
        if (socket != null)
        {
            return;
        }

        parser = new RaveOscPacketParser();
        socket = new OscUdpSocket(new IPEndPoint(IPAddress.Any, RaveBroadcastPort));
        socket.PacketReceived += OnPacketReceived;
        socket.Start();
        Debug.Log($"[RaveOscReceiver] Listening for RaveSystem OSC on UDP {RaveBroadcastPort}.");
    }

    private void StopListening()
    {
        if (socket == null)
        {
            return;
        }

        socket.PacketReceived -= OnPacketReceived;
        socket.Dispose();
        socket = null;
        parser.Dispose();
        parser = null;
    }

    private void OnPacketReceived(ReadOnlySpan<byte> packet, SocketAddress sender)
    {
        try
        {
            Interlocked.Increment(ref rawPacketCount);

            // Dispatch returns the recognized-message count: only packets that actually matched a
            // registered /rave/onair/* address refresh liveness, so unrelated UDP 7000 traffic cannot
            // keep the beat source alive.
            if (parser.Dispatch(packet) > 0)
            {
                Interlocked.Increment(ref recognizedPacketCount);
                Volatile.Write(ref lastRecognizedPacketTimestamp, Stopwatch.GetTimestamp());
            }
        }
        catch (Exception ex)
        {
            lock (errorLock)
            {
                pendingError = ex;
                hasPendingError = true;
            }
        }
    }
}
