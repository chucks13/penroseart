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

    private readonly object errorLock = new object();
    private OscUdpSocket socket;
    private RaveOscPacketParser parser;
    private RaveOnAirSnapshot latest = new RaveOnAirSnapshot();
    private Exception pendingError;
    private bool hasPendingError;
    private bool hasSnapshot;

    /// <summary>True after at least one recognized Rave OSC value has been received.</summary>
    public bool HasSnapshot => hasSnapshot;

    /// <summary>The latest decoded Rave on-air values.</summary>
    public RaveOnAirSnapshot Latest => latest;

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

    /// <summary>Applies the latest Rave beat data to PenroseArt's shared beat manager.</summary>
    public void ApplyTo(BeatManager beatManager)
    {
        if (!hasSnapshot || beatManager == null)
        {
            return;
        }

        ApplySnapshotToBeatData(latest, beatManager.beatData);
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
