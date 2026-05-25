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
    private RaveOnAirSnapshot _snapshot = new RaveOnAirSnapshot();
    private bool _hasUpdate;

    public RaveOscPacketParser() {
        RegisterString("/rave/onair/players_live", (snapshot, value) => snapshot.playersLive = value);
        RegisterString("/rave/onair/track", (snapshot, value) => snapshot.track = value);
        RegisterFloat("/rave/onair/bpm", (snapshot, value) => snapshot.bpm = value);
        RegisterBeatPosition("/rave/onair/beat", (snapshot, value) => snapshot.beat = value);
        RegisterBarPosition("/rave/onair/bar", (snapshot, value) => snapshot.bar = value);
        RegisterInt("/rave/onair/beat_in_bar", (snapshot, value) => snapshot.beatInBar = value);
        RegisterFourInts("/rave/onair/beats_count_ms", (snapshot, value) => snapshot.beatsCountMs = value);
        RegisterFourBools("/rave/onair/on_beats", (snapshot, value) => snapshot.onBeats = value);
        RegisterInt("/rave/onair/beat_avg_ms", (snapshot, value) => snapshot.beatAverageMs = value);
        RegisterFloat("/rave/onair/beat_pulse", (snapshot, value) => snapshot.beatPulse = value);
        RegisterLevels("/rave/onair/levels", (snapshot, value) => snapshot.levels = value);
        RegisterNamedState("/rave/onair/phase_state", (snapshot, value) => snapshot.phaseState = value);
        RegisterCountdownState("/rave/onair/drop_state", (snapshot, value) => snapshot.dropState = value);
        RegisterCountdownState("/rave/onair/fill_state", (snapshot, value) => snapshot.fillState = value);
        RegisterNamedState("/rave/onair/energy_state", (snapshot, value) => snapshot.energyState = value);
    }

    /// <summary>Dispatches one raw OSC packet. Bundles are decoded recursively by <see cref="OscDispatcher" />.</summary>
    public int Dispatch(ReadOnlySpan<byte> packet) => _dispatcher.Dispatch(packet);

    /// <summary>Returns the latest snapshot without clearing the pending-update flag.</summary>
    public RaveOnAirSnapshot Snapshot {
        get {
            lock (_lock) {
                return _snapshot.Clone();
            }
        }
    }

    /// <summary>Returns and clears the latest snapshot when at least one registered value changed.</summary>
    public bool TryTakeSnapshot(out RaveOnAirSnapshot snapshot) {
        lock (_lock) {
            snapshot = _snapshot.Clone();
            if (!_hasUpdate) {
                return false;
            }
            _hasUpdate = false;
            return true;
        }
    }

    public void Dispose() => _dispatcher.Dispose();

    private delegate void SnapshotFloatSetter(RaveOnAirSnapshot snapshot, float value);

    private delegate void SnapshotIntSetter(RaveOnAirSnapshot snapshot, int value);

    private delegate void SnapshotStringSetter(RaveOnAirSnapshot snapshot, string value);

    private delegate void SnapshotBeatPositionSetter(RaveOnAirSnapshot snapshot, RaveBeatPosition value);

    private delegate void SnapshotBarPositionSetter(RaveOnAirSnapshot snapshot, RaveBarPosition value);

    private delegate void SnapshotIntArraySetter(RaveOnAirSnapshot snapshot, int[] value);

    private delegate void SnapshotBoolArraySetter(RaveOnAirSnapshot snapshot, bool[] value);

    private delegate void SnapshotLevelsSetter(RaveOnAirSnapshot snapshot, RaveLevels value);

    private delegate void SnapshotNamedStateSetter(RaveOnAirSnapshot snapshot, RaveNamedState value);

    private delegate void SnapshotCountdownStateSetter(RaveOnAirSnapshot snapshot, RaveCountdownState value);

    private delegate void SnapshotUpdater(RaveOnAirSnapshot snapshot);

    private void RegisterFloat(string address, SnapshotFloatSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = ReadSingleFloat(address, ref reader);
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterInt(string address, SnapshotIntSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = ReadSingleInt(address, ref reader);
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterString(string address, SnapshotStringSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = ReadSingleString(address, ref reader);
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterBeatPosition(string address, SnapshotBeatPositionSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new RaveBeatPosition {
                current = ReadNextInt(address, ref reader),
                total = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterBarPosition(string address, SnapshotBarPositionSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new RaveBarPosition {
                current = ReadNextInt(address, ref reader),
                nextMs = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterFourInts(string address, SnapshotIntArraySetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new[] {
                ReadNextInt(address, ref reader),
                ReadNextInt(address, ref reader),
                ReadNextInt(address, ref reader),
                ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterFourBools(string address, SnapshotBoolArraySetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new[] {
                ReadNextInt(address, ref reader) != 0,
                ReadNextInt(address, ref reader) != 0,
                ReadNextInt(address, ref reader) != 0,
                ReadNextInt(address, ref reader) != 0,
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterLevels(string address, SnapshotLevelsSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new RaveLevels {
                low = ReadNextFloat(address, ref reader),
                mid = ReadNextFloat(address, ref reader),
                high = ReadNextFloat(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterNamedState(string address, SnapshotNamedStateSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new RaveNamedState {
                current = ReadNextString(address, ref reader),
                next = ReadNextString(address, ref reader),
                active = ReadNextInt(address, ref reader) != 0,
                countBeats = ReadNextInt(address, ref reader),
                lengthBeats = ReadNextInt(address, ref reader),
                remaining = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterCountdownState(string address, SnapshotCountdownStateSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new RaveCountdownState {
                active = ReadNextInt(address, ref reader) != 0,
                countBeats = ReadNextInt(address, ref reader),
                lengthBeats = ReadNextInt(address, ref reader),
                remaining = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void UpdateSnapshot(SnapshotUpdater updater) {
        lock (_lock) {
            updater(_snapshot);
            _hasUpdate = true;
        }
    }

    private static float ReadSingleFloat(string address, ref OscReader reader) => ReadNextFloat(address, ref reader);

    private static int ReadSingleInt(string address, ref OscReader reader) => ReadNextInt(address, ref reader);

    private static string ReadSingleString(string address, ref OscReader reader) => ReadNextString(address, ref reader);

    private static float ReadNextFloat(string address, ref OscReader reader) {
        if (!reader.MoveNext() || reader.CurrentTag != OscToken.F32) {
            throw UnexpectedType(address, "float32", reader.CurrentTag);
        }
        return reader.ReadFloat32();
    }

    private static int ReadNextInt(string address, ref OscReader reader) {
        if (!reader.MoveNext() || reader.CurrentTag != OscToken.I32) {
            throw UnexpectedType(address, "int32", reader.CurrentTag);
        }
        return reader.ReadInt32();
    }

    private static string ReadNextString(string address, ref OscReader reader) {
        if (!reader.MoveNext() || reader.CurrentTag != OscToken.Str) {
            throw UnexpectedType(address, "string", reader.CurrentTag);
        }
        return reader.ReadStringAlloc();
    }

    private static OscFormatException UnexpectedType(string address, string expected, OscToken actual) =>
        new OscFormatException($"Rave OSC address {address} expected {expected} argument, received tag '{Encoding.ASCII.GetString(new[] { (byte)actual })}'");
}

}
