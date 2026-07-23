// Copyright © 2026 Hunter Luisi. All rights reserved.
// RaveSystem OSC client packet parser for PenroseArt.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using RaveSystem.Osc;

namespace PenroseArt.RaveOsc {

/// <summary>
/// Decodes the RaveSystem OSC broadcast schema — the on-air surface and the keyed
/// per-player surface — into a thread-safe snapshot.
/// </summary>
public sealed class RaveOscPacketParser : IDisposable {
    private readonly OscDispatcher _dispatcher = new OscDispatcher();
    private readonly object _lock = new object();
    private RaveWireSnapshot _snapshot = new RaveWireSnapshot();
    private bool _hasUpdate;

    /// <summary>
    /// Per-player structure chunk-slot buffers, indexed by device number minus one. A null entry
    /// means no generation is held for that player; inside a buffer, a slot is null until its
    /// chunk arrives. Every access runs inside <see cref="UpdateSnapshot"/> and is therefore
    /// guarded by <see cref="_lock"/>; the buffers are never exposed outside the parser.
    /// </summary>
    private readonly StructurePhrase[]?[]?[] _structureChunks = new StructurePhrase[RaveWireSnapshot.PlayerCount][][];

    public RaveOscPacketParser() {
        RegisterString("/rave/onair/players_live", (snapshot, value) => snapshot.playersLive = value);
        RegisterString("/rave/onair/track", (snapshot, value) => snapshot.track = value);
        RegisterFloat("/rave/onair/bpm", (snapshot, value) => snapshot.bpm = value);
        RegisterInt("/rave/onair/beat", (snapshot, value) => {
            var beat = snapshot.beat;
            beat.current = value;
            snapshot.beat = beat;
        });
        RegisterInt("/rave/onair/total_beats", (snapshot, value) => {
            var beat = snapshot.beat;
            beat.total = value;
            snapshot.beat = beat;
        });
        RegisterInt("/rave/onair/bar", (snapshot, value) => {
            var bar = snapshot.bar;
            bar.current = value;
            snapshot.bar = bar;
        });
        RegisterInt("/rave/onair/next_bar_ms", (snapshot, value) => {
            var bar = snapshot.bar;
            bar.nextMs = value;
            snapshot.bar = bar;
        });
        RegisterInt("/rave/onair/beat_in_bar", (snapshot, value) => snapshot.beatInBar = value);
        RegisterFourInts("/rave/onair/beats_count_ms", (snapshot, value) => snapshot.beatsCountMs = value);
        RegisterFourBools("/rave/onair/on_beats", (snapshot, value) => snapshot.onBeats = value);
        RegisterInt("/rave/onair/beat_avg_ms", (snapshot, value) => snapshot.beatAverageMs = value);
        RegisterFloat("/rave/onair/beat_pulse", (snapshot, value) => snapshot.beatPulse = value);
        RegisterLevels("/rave/onair/levels", (snapshot, value) => snapshot.levels = value);
        RegisterPhraseState("/rave/onair/phrase_state", (snapshot, value) => snapshot.phraseState = value);
        RegisterLabeledCountdown("/rave/onair/next_phrase_state", (snapshot, value) => snapshot.nextPhraseState = value);
        RegisterCountdownState("/rave/onair/drop_state", (snapshot, value) => snapshot.dropState = value);
        RegisterCountdownState("/rave/onair/fill_state", (snapshot, value) => snapshot.fillState = value);
        RegisterLabeledCountdown("/rave/onair/energy_state", (snapshot, value) => snapshot.energyState = value);
        RegisterLabeledCountdown("/rave/onair/next_energy_state", (snapshot, value) => snapshot.nextEnergyState = value);
        RegisterLoopState("/rave/onair/loop_state", (snapshot, value) => snapshot.loopState = value);
        RegisterTimingGrid("/rave/onair/timing_grid", (snapshot, value) => snapshot.timingGrid = value);
        RegisterInt("/rave/onair/track_id", (snapshot, value) => snapshot.trackId = value);
        for (var playerNumber = 1; playerNumber <= RaveWireSnapshot.PlayerCount; playerNumber++) {
            RegisterPlayerLanes(playerNumber);
        }
    }

    /// <summary>
    /// Registers the wire lanes of physical player <paramref name="playerNumber"/> (ProLink
    /// device number 1..6), each routed to that player's snapshot slot only: the four bundled
    /// lanes plus the bare structure datagram and its cursor. Loop and timing grid reuse the
    /// on-air readers because the contract declares the shapes identical.
    /// </summary>
    private void RegisterPlayerLanes(int playerNumber) {
        var index = playerNumber - 1;
        var prefix = "/rave/player/" + playerNumber;
        RegisterPlayerClock(prefix + "/clock", (snapshot, value) => snapshot.players[index].clock = value);
        RegisterPlayerTransport(prefix + "/transport", (snapshot, value) => snapshot.players[index].transport = value);
        RegisterLoopState(prefix + "/loop_state", (snapshot, value) => snapshot.players[index].loopState = value);
        RegisterTimingGrid(prefix + "/timing_grid", (snapshot, value) => snapshot.players[index].timingGrid = value);
        RegisterPlayerStructure(prefix + "/structure", index);
        RegisterStructureCursor(prefix + "/structure_state", index);
    }

    /// <summary>
    /// Dispatches one raw Rave on-air OSC packet using local receive-time delivery.
    /// </summary>
    /// <remarks>
    /// RaveSystem on-air packets are live telemetry. Bundle timetags document sender time, but they
    /// must not delay delivery: across hosts, a small clock skew makes every <c>OscTimeTag.Now</c>
    /// packet look "future" to the receiver. The generic OSC dispatcher keeps standard scheduling
    /// behavior; this adapter unwraps bundles and dispatches their message elements immediately.
    /// </remarks>
    public int Dispatch(ReadOnlySpan<byte> packet) => DispatchLivePacket(packet);

    private int DispatchLivePacket(ReadOnlySpan<byte> packet) {
        var kind = OscPacket.Classify(packet);
        return kind == OscPacketKind.Bundle
            ? DispatchLiveBundle(packet)
            : _dispatcher.Dispatch(packet);
    }

    private int DispatchLiveBundle(ReadOnlySpan<byte> bundle) {
        var bundleReader = new OscBundleReader(bundle);
        var dispatched = 0;
        while (bundleReader.HasMoreElements) {
            dispatched += DispatchLivePacket(bundleReader.ReadNextElement());
        }
        return dispatched;
    }

    /// <summary>Returns the latest snapshot without clearing the pending-update flag.</summary>
    public RaveWireSnapshot Snapshot {
        get {
            lock (_lock) {
                return _snapshot.Clone();
            }
        }
    }

    /// <summary>Returns and clears the latest snapshot when at least one registered value changed.</summary>
    public bool TryTakeSnapshot(out RaveWireSnapshot snapshot) {
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

    private delegate void SnapshotFloatSetter(RaveWireSnapshot snapshot, float value);

    private delegate void SnapshotIntSetter(RaveWireSnapshot snapshot, int value);

    private delegate void SnapshotStringSetter(RaveWireSnapshot snapshot, string value);

    private delegate void SnapshotIntArraySetter(RaveWireSnapshot snapshot, int[] value);

    private delegate void SnapshotBoolArraySetter(RaveWireSnapshot snapshot, bool[] value);

    private delegate void SnapshotLevelsSetter(RaveWireSnapshot snapshot, Levels value);

    private delegate void SnapshotPhraseStateSetter(RaveWireSnapshot snapshot, PhraseState value);

    private delegate void SnapshotLabeledCountdownSetter(RaveWireSnapshot snapshot, LabeledCountdown value);

    private delegate void SnapshotLoopStateSetter(RaveWireSnapshot snapshot, LoopState value);

    private delegate void SnapshotTimingGridSetter(RaveWireSnapshot snapshot, TimingGrid value);

    private delegate void SnapshotCountdownStateSetter(RaveWireSnapshot snapshot, CountdownState value);

    private delegate void SnapshotPlayerClockSetter(RaveWireSnapshot snapshot, PlayerClock value);

    private delegate void SnapshotPlayerTransportSetter(RaveWireSnapshot snapshot, PlayerTransport value);

    private delegate void SnapshotUpdater(RaveWireSnapshot snapshot);

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

    /// <summary>
    /// Registers a four-lane gate message. Only positive wire values open gates; the unavailable <c>-1</c> sentinel must remain closed.
    /// </summary>
    /// <param name="address">OSC address carrying the four integer gate lanes.</param>
    /// <param name="setter">Snapshot mutation that receives the parsed gate lanes.</param>
    private void RegisterFourBools(string address, SnapshotBoolArraySetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new[] {
                ReadNextInt(address, ref reader) > 0,
                ReadNextInt(address, ref reader) > 0,
                ReadNextInt(address, ref reader) > 0,
                ReadNextInt(address, ref reader) > 0,
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterLevels(string address, SnapshotLevelsSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new Levels {
                low = ReadNextFloat(address, ref reader),
                mid = ReadNextFloat(address, ref reader),
                high = ReadNextFloat(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterPhraseState(string address, SnapshotPhraseStateSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new PhraseState {
                label = ReadNextString(address, ref reader),
                countBeats = ReadNextInt(address, ref reader),
                lengthBeats = ReadNextInt(address, ref reader),
                // Tri-state passthrough: -1 unavailable / 0 regular / 1 irregular. A != 0 collapse here
                // would turn "unavailable" into "irregular".
                irregular = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterLabeledCountdown(string address, SnapshotLabeledCountdownSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new LabeledCountdown {
                label = ReadNextString(address, ref reader),
                countBeats = ReadNextInt(address, ref reader),
                lengthBeats = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterLoopState(string address, SnapshotLoopStateSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new LoopState {
                // Tri-state passthrough: -1 unavailable / 0 no / 1 yes. A != 0 collapse here
                // would turn "unavailable" into "active now".
                active = ReadNextInt(address, ref reader),
                set = ReadNextInt(address, ref reader),
                lengthBeats = ReadNextFloat(address, ref reader),
                lengthMs = ReadNextInt(address, ref reader),
                sizeNumerator = ReadNextInt(address, ref reader),
                sizeDenominator = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterTimingGrid(string address, SnapshotTimingGridSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new TimingGrid {
                beat = ReadNextInt(address, ref reader),
                bar = ReadNextInt(address, ref reader),
                state = ReadNextString(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterCountdownState(string address, SnapshotCountdownStateSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new CountdownState {
                // Tri-state passthrough: -1 unavailable / 0 upcoming / 1 active. A != 0 collapse here
                // would turn "unavailable" into "active now".
                active = ReadNextInt(address, ref reader),
                countBeats = ReadNextInt(address, ref reader),
                lengthBeats = ReadNextInt(address, ref reader),
                remaining = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterPlayerClock(string address, SnapshotPlayerClockSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new PlayerClock {
                bpm = ReadNextFloat(address, ref reader),
                beat = ReadNextInt(address, ref reader),
                bar = ReadNextInt(address, ref reader),
                beatInBar = ReadNextInt(address, ref reader),
                beatPulse = ReadNextFloat(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    private void RegisterPlayerTransport(string address, SnapshotPlayerTransportSetter setter) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new PlayerTransport {
                // Tri-state passthrough: -1 unavailable / 0 no / 1 yes. A != 0 collapse here
                // would turn "unavailable" into "yes".
                playing = ReadNextInt(address, ref reader),
                cued = ReadNextInt(address, ref reader),
                onAir = ReadNextInt(address, ref reader),
                master = ReadNextInt(address, ref reader),
                synced = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => setter(snapshot, value));
        });
    }

    /// <summary>
    /// Registers the bare <c>/rave/player/{N}/structure</c> datagram for one player slot: reads
    /// the <c>sisiiii</c> header (track_id, structure_generation, source, total_beats,
    /// phrase_count, chunk_index, chunk_count) plus the repeating <c>iisiii</c> phrase tuples,
    /// then applies chunk assembly under the snapshot lock.
    /// </summary>
    private void RegisterPlayerStructure(string address, int index) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var header = new PlayerStructure {
                trackId = ReadNextString(address, ref reader),
                generation = ReadNextInt(address, ref reader),
                source = ReadNextString(address, ref reader),
                totalBeats = ReadNextInt(address, ref reader),
                phraseCount = ReadNextInt(address, ref reader),
            };
            var chunkIndex = ReadNextInt(address, ref reader);
            var chunkCount = ReadNextInt(address, ref reader);
            if (chunkCount < 1 || chunkIndex < 0 || chunkIndex >= chunkCount) {
                throw new OscFormatException($"Rave OSC address {address} structure chunk must identify an index within a positive chunk count, received chunk {chunkIndex} of {chunkCount}");
            }
            var chunk = ReadPhraseTuples(address, ref reader);
            UpdateSnapshot(snapshot => ApplyStructureChunk(snapshot, index, header, chunkIndex, chunkCount, chunk));
        });
    }

    /// <summary>
    /// Reads the datagram's remaining <c>iisiii</c> phrase tuples in wire order. Tuple order is
    /// phrase identity (ordinal); nothing is keyed or deduplicated by type. A truncated tuple
    /// throws <see cref="OscFormatException"/> like every other typed lane.
    /// </summary>
    private static StructurePhrase[] ReadPhraseTuples(string address, ref OscReader reader) {
        var phrases = new List<StructurePhrase>();
        while (reader.MoveNext()) {
            if (reader.CurrentTag != OscToken.I32) {
                throw UnexpectedType(address, "int32", reader.CurrentTag);
            }
            phrases.Add(new StructurePhrase {
                startBeat = reader.ReadInt32(),
                endBeat = ReadNextInt(address, ref reader),
                type = ReadNextString(address, ref reader),
                variant = ReadNextInt(address, ref reader),
                fillStartBeat = ReadNextInt(address, ref reader),
                dropLandingBeat = ReadNextInt(address, ref reader),
            });
        }
        return phrases.ToArray();
    }

    /// <summary>
    /// Applies one parsed structure chunk to a player slot, mirroring RaveSystem's reference
    /// client: a header-only zero-phrase chunk 0-of-1 clears structure, cursor, and buffer; a
    /// datagram whose generation differs from the held one (inequality, never ordering) replaces
    /// the whole chunk buffer and clears the cursor; each chunk overwrites its indexed slot; the
    /// visible phrase list is the filled slots concatenated in ascending chunk order, exposed
    /// even while slots are still missing. Runs inside <see cref="UpdateSnapshot"/>, whose lock
    /// also guards <see cref="_structureChunks"/>. The rebuilt phrase array is freshly allocated
    /// on every visible change and never mutated after being assigned to the slot, which keeps
    /// cloned snapshots deep copies in effect.
    /// </summary>
    private void ApplyStructureChunk(RaveWireSnapshot snapshot, int index, PlayerStructure header, int chunkIndex, int chunkCount, StructurePhrase[] chunk) {
        if (chunk.Length == 0 && chunkIndex == 0 && chunkCount == 1) {
            _structureChunks[index] = null;
            snapshot.players[index].structure = PlayerStructure.Unavailable;
            snapshot.players[index].cursor = StructureCursor.Unavailable;
            return;
        }

        var buffer = _structureChunks[index];
        var newGeneration = buffer is null || snapshot.players[index].structure.generation != header.generation;
        if (!newGeneration && buffer!.Length != chunkCount) {
            throw new OscFormatException($"Rave OSC structure generation {header.generation} chunks must share one chunk count, held {buffer!.Length} received {chunkCount}");
        }
        if (newGeneration) {
            buffer = new StructurePhrase[chunkCount][];
            _structureChunks[index] = buffer;
            snapshot.players[index].cursor = StructureCursor.Unavailable;
        }
        buffer![chunkIndex] = chunk;

        var assembledCount = 0;
        foreach (var slot in buffer) {
            if (slot is not null) {
                assembledCount += slot.Length;
            }
        }
        var phrases = new StructurePhrase[assembledCount];
        var offset = 0;
        foreach (var slot in buffer) {
            if (slot is not null) {
                Array.Copy(slot, 0, phrases, offset, slot.Length);
                offset += slot.Length;
            }
        }
        header.phrases = phrases;
        snapshot.players[index].structure = header;
    }

    /// <summary>
    /// Registers the <c>/rave/player/{N}/structure_state</c> cursor (<c>iiii</c>: generation,
    /// current_phrase, beat_in_phrase, beats_to_next_phrase) for one player slot. The cursor is
    /// applied only when its generation equals the held structure's generation; a mismatched
    /// cursor is dropped silently and the prior cursor holds, so an ordinal never lands on the
    /// wrong phrase list.
    /// </summary>
    private void RegisterStructureCursor(string address, int index) {
        _dispatcher.Register(address, (ReadOnlySpan<byte> _, ref OscReader reader, OscTimeTag __) => {
            var value = new StructureCursor {
                generation = ReadNextInt(address, ref reader),
                currentPhrase = ReadNextInt(address, ref reader),
                beatInPhrase = ReadNextInt(address, ref reader),
                beatsToNextPhrase = ReadNextInt(address, ref reader),
            };
            UpdateSnapshot(snapshot => {
                if (value.generation == snapshot.players[index].structure.generation) {
                    snapshot.players[index].cursor = value;
                }
            });
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
