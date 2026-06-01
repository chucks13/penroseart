// Receives RaveSystem OSC broadcasts through the new RaveSystem.Osc stack.

using System;
using System.Net;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;
using UnityEngine;

/// <summary>
/// Unity component that listens for RaveSystem on-air OSC broadcasts and exposes the latest snapshot.
/// </summary>
public sealed class RaveOscReceiver : MonoBehaviour
{
    private const int RaveBroadcastPort = 7000;
    private const float RaveBroadcastRateHz = 60f;
    private const float BroadcastSilenceTimeoutSeconds = 3f / RaveBroadcastRateHz;

    private readonly object errorLock = new object();
    private OscUdpSocket socket;
    private RaveOscPacketParser parser;
    private RaveOnAirSnapshot latest = new RaveOnAirSnapshot();
    private Exception pendingError;
    private bool hasPendingError;
    private bool hasSnapshot;
    private float lastRecognizedPacketTime = float.NegativeInfinity;

    /// <summary>True after at least one recognized Rave OSC value has been received.</summary>
    public bool HasSnapshot => hasSnapshot;

    /// <summary>The latest decoded Rave on-air values.</summary>
    public RaveOnAirSnapshot Latest => latest;

    /// <summary>True while recognized RaveSystem OSC is arriving on UDP 7000.</summary>
    public bool IsBroadcasting => HasRecentRecognizedPacket(Time.realtimeSinceStartup);

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
            lastRecognizedPacketTime = Time.realtimeSinceStartup;
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
    /// Chooses the shared beat source from RaveSystem transport liveness and, while broadcasting,
    /// pushes the latest decoded snapshot into the shared beat data every frame.
    /// </summary>
    /// <remarks>
    /// Any recognized Rave on-air OSC value on UDP 7000 makes the BeatManager live immediately. When
    /// recognized values stop arriving for three 60 Hz broadcast intervals, the BeatManager returns to its
    /// local fallback/no-beat path. Beat activity itself is still data-driven: an idle RaveSystem broadcast
    /// keeps the source live while <see cref="ApplySnapshotToBeatData"/> marks <see cref="BeatData.active"/>
    /// false from the sentinel payload.
    /// </remarks>
    public void ApplyTo(BeatManager beatManager)
    {
        if (beatManager == null)
        {
            return;
        }

        var broadcasting = IsBroadcasting;
        beatManager.SetLiveBeatSource(broadcasting);
        if (!broadcasting || !hasSnapshot)
        {
            return;
        }

        ApplySnapshotToBeatData(latest, beatManager.beatData);
    }

    /// <summary>Returns true while the last recognized Rave OSC value is still within the UDP 7000 broadcast cadence.</summary>
    public static bool IsBroadcastingAt(bool hasSnapshot, float lastRecognizedPacketTime, float now)
    {
        return hasSnapshot && now - lastRecognizedPacketTime <= BroadcastSilenceTimeoutSeconds;
    }

    private bool HasRecentRecognizedPacket(float now)
    {
        return IsBroadcastingAt(hasSnapshot, lastRecognizedPacketTime, now);
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
            parser.Dispatch(packet);
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
