// Copyright © 2026 Hunter Luisi. All rights reserved.
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
    private RaveOscSnapshot latest;
    private Exception pendingError;
    private bool hasPendingError;
    private bool hasSnapshot;

    /// <summary>True after at least one recognized Rave OSC value has been received.</summary>
    public bool HasSnapshot => hasSnapshot;

    /// <summary>The latest decoded Rave on-air values.</summary>
    public RaveOscSnapshot Latest => latest;

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

    /// <summary>Copies beat-relevant Rave OSC values into PenroseArt's shared beat data.</summary>
    public static void ApplySnapshotToBeatData(RaveOscSnapshot snapshot, BeatData beatData)
    {
        if (beatData == null)
        {
            return;
        }

        var hasUsableBeat = snapshot.Bpm > 0f;
        beatData.active = hasUsableBeat;
        beatData.bpm = hasUsableBeat ? snapshot.Bpm : 120f;
        beatData.currentBeat = snapshot.BeatInBar >= 1 && snapshot.BeatInBar <= beatData.beatsPerMeasure
            ? snapshot.BeatInBar - 1
            : 0;
        beatData.onBeat = snapshot.OnBeat;
        beatData.beatPulse = Mathf.Clamp01(snapshot.BeatPulse);
        beatData.timeEvent = snapshot.OnBeat
            ? 0
            : snapshot.NextBeatMs >= 0 ? -snapshot.NextBeatMs : 0;
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
