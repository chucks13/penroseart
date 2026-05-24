// Copyright © 2026 Hunter Luisi. All rights reserved.
// RaveSystem OSC client packet parser for PenroseArt.

#nullable enable

using System;
using System.Text;
using RaveSystem.Osc;

namespace PenroseArt.RaveOsc {

/// <summary>
/// Decodes the RaveSystem on-air OSC broadcast schema into a thread-safe snapshot.
/// </summary>
public sealed class RaveOscPacketParser : IDisposable {
    private readonly OscDispatcher _dispatcher = new OscDispatcher();
    private readonly object _lock = new object();
    private RaveOscSnapshot _snapshot;
    private bool _hasUpdate;

    public RaveOscPacketParser() {
        RegisterFloat("/rave/onair/bpm", (ref RaveOscSnapshot snapshot, float value) => {
            snapshot.HasBpm = true;
            snapshot.Bpm = value;
        });
        RegisterInt("/rave/onair/beat", (ref RaveOscSnapshot snapshot, int value) => {
            snapshot.HasBeat = true;
            snapshot.Beat = value;
        });
        RegisterInt("/rave/onair/bar", (ref RaveOscSnapshot snapshot, int value) => {
            snapshot.HasBar = true;
            snapshot.Bar = value;
        });
        RegisterInt("/rave/onair/beat_in_bar", (ref RaveOscSnapshot snapshot, int value) => {
            snapshot.HasBeatInBar = true;
            snapshot.BeatInBar = value;
        });
        RegisterInt("/rave/onair/next_beat_ms", (ref RaveOscSnapshot snapshot, int value) => {
            snapshot.HasNextBeatMs = true;
            snapshot.NextBeatMs = value;
        });
        RegisterInt("/rave/onair/on_beat", (ref RaveOscSnapshot snapshot, int value) => {
            snapshot.HasOnBeat = true;
            snapshot.OnBeat = value != 0;
        });
        RegisterFloat("/rave/onair/low", (ref RaveOscSnapshot snapshot, float value) => {
            snapshot.HasLow = true;
            snapshot.Low = value;
        });
        RegisterFloat("/rave/onair/mid", (ref RaveOscSnapshot snapshot, float value) => {
            snapshot.HasMid = true;
            snapshot.Mid = value;
        });
        RegisterFloat("/rave/onair/high", (ref RaveOscSnapshot snapshot, float value) => {
            snapshot.HasHigh = true;
            snapshot.High = value;
        });
        RegisterInt("/rave/onair/drop_in", (ref RaveOscSnapshot snapshot, int value) => {
            snapshot.HasDropIn = true;
            snapshot.DropIn = value != 0;
        });
        RegisterString("/rave/onair/phase", (ref RaveOscSnapshot snapshot, string value) => {
            snapshot.HasPhase = true;
            snapshot.Phase = value;
        });
    }

    /// <summary>Dispatches one raw OSC packet. Bundles are decoded recursively by <see cref="OscDispatcher" />.</summary>
    public int Dispatch(ReadOnlySpan<byte> packet) => _dispatcher.Dispatch(packet);

    /// <summary>Returns the latest snapshot without clearing the pending-update flag.</summary>
    public RaveOscSnapshot Snapshot {
        get {
            lock (_lock) {
                return _snapshot;
            }
        }
    }

    /// <summary>Returns and clears the latest snapshot when at least one registered value changed.</summary>
    public bool TryTakeSnapshot(out RaveOscSnapshot snapshot) {
        lock (_lock) {
            snapshot = _snapshot;
            if (!_hasUpdate) {
                return false;
            }
            _hasUpdate = false;
            return true;
        }
    }

    public void Dispose() => _dispatcher.Dispose();

    private delegate void SnapshotFloatSetter(ref RaveOscSnapshot snapshot, float value);

    private delegate void SnapshotIntSetter(ref RaveOscSnapshot snapshot, int value);

    private delegate void SnapshotStringSetter(ref RaveOscSnapshot snapshot, string value);

    private void RegisterFloat(string address, SnapshotFloatSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = ReadSingleFloat(address, ref reader);
            UpdateSnapshot((ref RaveOscSnapshot snapshot) => setter(ref snapshot, value));
        });
    }

    private void RegisterInt(string address, SnapshotIntSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = ReadSingleInt(address, ref reader);
            UpdateSnapshot((ref RaveOscSnapshot snapshot) => setter(ref snapshot, value));
        });
    }

    private void RegisterString(string address, SnapshotStringSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = ReadSingleString(address, ref reader);
            UpdateSnapshot((ref RaveOscSnapshot snapshot) => setter(ref snapshot, value));
        });
    }

    private delegate void SnapshotUpdater(ref RaveOscSnapshot snapshot);

    private void UpdateSnapshot(SnapshotUpdater updater) {
        lock (_lock) {
            updater(ref _snapshot);
            _hasUpdate = true;
        }
    }

    private static float ReadSingleFloat(string address, ref OscReader reader) {
        if (!reader.MoveNext() || reader.CurrentTag != OscToken.F32) {
            throw UnexpectedType(address, "float32", reader.CurrentTag);
        }
        return reader.ReadFloat32();
    }

    private static int ReadSingleInt(string address, ref OscReader reader) {
        if (!reader.MoveNext() || reader.CurrentTag != OscToken.I32) {
            throw UnexpectedType(address, "int32", reader.CurrentTag);
        }
        return reader.ReadInt32();
    }

    private static string ReadSingleString(string address, ref OscReader reader) {
        if (!reader.MoveNext() || reader.CurrentTag != OscToken.Str) {
            throw UnexpectedType(address, "string", reader.CurrentTag);
        }
        return reader.ReadStringAlloc();
    }

    private static OscFormatException UnexpectedType(string address, string expected, OscToken actual) =>
        new OscFormatException($"Rave OSC address {address} expected one {expected} argument, received tag '{Encoding.ASCII.GetString(new[] { (byte)actual })}'");
}

}
