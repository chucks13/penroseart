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

        var beatData = beatManager.beatData;
        beatData.active = true;

        if (latest.HasBpm && latest.Bpm > 0f)
        {
            beatData.bpm = latest.Bpm;
        }

        if (latest.HasBeatInBar && latest.BeatInBar >= 1 && latest.BeatInBar <= beatData.beatsPerMeasure)
        {
            beatData.currentBeat = latest.BeatInBar - 1;
        }

        if (latest.HasOnBeat && latest.OnBeat)
        {
            beatData.timeEvent = 0;
        }
        else if (latest.HasNextBeatMs && latest.NextBeatMs >= 0)
        {
            beatData.timeEvent = -latest.NextBeatMs;
        }
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
