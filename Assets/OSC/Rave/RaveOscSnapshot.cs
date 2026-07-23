// Copyright © 2026 Hunter Luisi. All rights reserved.
// RaveSystem OSC client state for PenroseArt.

#nullable enable

using System;

namespace PenroseArt.RaveOsc {

/// <summary>Focused on-air beat position from <c>/rave/onair/beat</c> and <c>/rave/onair/total_beats</c>.</summary>
[Serializable]
public struct BeatPosition {
    public int current;
    public int total;
}

/// <summary>Focused on-air bar position from <c>/rave/onair/bar</c> and <c>/rave/onair/next_bar_ms</c>.</summary>
[Serializable]
public struct BarPosition {
    public int current;
    public int nextMs;
}

/// <summary>Low/mid/high normalized waveform energy values from <c>/rave/onair/levels</c>.</summary>
[Serializable]
public struct Levels {
    public float low;
    public float mid;
    public float high;

    /// <summary>Levels value whose bands are all unavailable (-1 sentinels).</summary>
    public static Levels Unavailable => new Levels { low = -1f, mid = -1f, high = -1f };
}

/// <summary>
/// Current phrase of the on-air focus from <c>/rave/onair/phrase_state</c> (<c>siii</c>).
/// <c>countBeats</c> is beats remaining in the phrase; <c>lengthBeats</c> its total length.
/// </summary>
[Serializable]
public struct PhraseState {
    /// <summary>Phrase name; empty/null = unavailable.</summary>
    public string? label;

    public int countBeats;
    public int lengthBeats;

    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = phrase length not divisible by 16, <c>0</c> = regular,
    /// <c>-1</c> = unavailable. Do not collapse to a bool.
    /// </summary>
    public int irregular;

    /// <summary>Phrase state whose fields are all unavailable (-1 sentinels, empty label).</summary>
    public static PhraseState Unavailable =>
        new PhraseState { label = null, countBeats = -1, lengthBeats = -1, irregular = -1 };
}

/// <summary>
/// Labeled beat countdown shared by <c>/rave/onair/next_phrase_state</c>, <c>energy_state</c>, and
/// <c>next_energy_state</c> (<c>sii</c>). For "next" lanes <c>countBeats</c> counts down to the change and
/// <c>lengthBeats</c> is the upcoming run's own total length; for the current energy lane they describe the
/// current run.
/// </summary>
[Serializable]
public struct LabeledCountdown {
    /// <summary>Phrase name or energy level; empty/null = unavailable.</summary>
    public string? label;

    public int countBeats;
    public int lengthBeats;

    /// <summary>Labeled countdown whose fields are all unavailable (-1 sentinels, empty label).</summary>
    public static LabeledCountdown Unavailable =>
        new LabeledCountdown { label = null, countBeats = -1, lengthBeats = -1 };
}

/// <summary>
/// Loop state of the on-air focus from <c>/rave/onair/loop_state</c> (<c>iifiii</c>).
/// </summary>
[Serializable]
public struct LoopState {
    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = looping audio is rolling (looping and playing), <c>0</c> = not rolling,
    /// <c>-1</c> = unavailable. Do not collapse to a bool.
    /// </summary>
    public int active;

    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = a loop region exists on the deck (persists while paused),
    /// <c>0</c> = no region, <c>-1</c> = unavailable. Do not collapse to a bool.
    /// </summary>
    public int set;

    /// <summary>Loop region length in beats; fractional loops are real (a 1/2-beat loop is 0.5).</summary>
    public float lengthBeats;

    public int lengthMs;
    public int sizeNumerator;
    public int sizeDenominator;

    /// <summary>Loop state whose fields are all unavailable (-1 sentinels).</summary>
    public static LoopState Unavailable => new LoopState {
        active = -1, set = -1, lengthBeats = -1f, lengthMs = -1, sizeNumerator = -1, sizeDenominator = -1,
    };
}

/// <summary>
/// Source-computed cyclic timing grid of the on-air focus from <c>/rave/onair/timing_grid</c> (<c>iis</c>).
/// <c>beat</c> counts 1..16 with 1 as the One; <c>bar</c> is the 1..4 four-beat subdivision. Beat/bar can be
/// -1 with a live <c>state</c> when no beat is placeable yet; the full unavailable shape is -1 -1 "".
/// </summary>
[Serializable]
public struct TimingGrid {
    public int beat;
    public int bar;

    /// <summary>Grid-confidence vocabulary: "locked" / "coasting" / "disputed"; empty/null = unavailable.</summary>
    public string? state;

    /// <summary>Timing grid whose fields are all unavailable (-1 sentinels, empty state).</summary>
    public static TimingGrid Unavailable => new TimingGrid { beat = -1, bar = -1, state = null };
}

/// <summary>
/// Countdown beat state used by drop and fill projections.
/// Mirrors RaveSystem's track countdown state shape: tri-state active value, beat count, length, and remaining count.
/// </summary>
[Serializable]
public struct CountdownState {
    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = active now, <c>0</c> = counting to the next occurrence,
    /// <c>-1</c> = unavailable. Mirrors <c>TrackCountdownState.Active</c>; do not collapse to a bool.
    /// </summary>
    public int active;

    public int countBeats;
    public int lengthBeats;
    public int remaining;

    /// <summary>Countdown state whose fields are all unavailable (-1 sentinels).</summary>
    public static CountdownState Unavailable =>
        new CountdownState { active = -1, countBeats = -1, lengthBeats = -1, remaining = -1 };
}

/// <summary>
/// 60 Hz clock of one physical player from <c>/rave/player/{N}/clock</c> (<c>fiiif</c>).
/// Fields follow the same meaning and formulas as the on-air bpm/beat/bar/beat-in-bar/pulse lanes.
/// </summary>
[Serializable]
public struct PlayerClock {
    public float bpm;
    public int beat;
    public int bar;
    public int beatInBar;

    /// <summary>
    /// Triangle wave: 1.0 on each beat, 0.0 halfway between. On the wire 0.0 is both the mid-beat
    /// trough and the at-rest/unavailable value, so it alone cannot signal a missing clock.
    /// </summary>
    public float beatPulse;

    /// <summary>Player clock whose fields are all unavailable (-1 sentinels, at-rest pulse).</summary>
    public static PlayerClock Unavailable =>
        new PlayerClock { bpm = -1f, beat = -1, bar = -1, beatInBar = -1, beatPulse = 0f };
}

/// <summary>
/// Capture-proven transport predicates of one physical player from
/// <c>/rave/player/{N}/transport</c> (<c>iiiii</c>). Every field is a RaveSystem tri-state:
/// <c>1</c> = yes, <c>0</c> = no, <c>-1</c> = unavailable. Do not collapse any of them to a bool.
/// "Live" is not transmitted; clients derive it as <c>playing == 1 &amp;&amp; onAir == 1</c>.
/// </summary>
[Serializable]
public struct PlayerTransport {
    public int playing;
    public int cued;
    public int onAir;
    public int master;
    public int synced;

    /// <summary>Player transport whose predicates are all unavailable (-1 sentinels).</summary>
    public static PlayerTransport Unavailable => new PlayerTransport {
        playing = -1, cued = -1, onAir = -1, master = -1, synced = -1,
    };
}

/// <summary>
/// One analyzed phrase from <c>/rave/player/{N}/structure</c> (one <c>iisiii</c> repeating tuple).
/// A phrase's identity is its position in the assembled list (ordinal), never its type or name;
/// repeated and immediately adjacent identical types are distinct phrases.
/// </summary>
[Serializable]
public struct StructurePhrase {
    /// <summary>Inclusive one-based phrase start beat.</summary>
    public int startBeat;

    /// <summary>Inclusive phrase end beat.</summary>
    public int endBeat;

    /// <summary>Lowercase phrase kind from the per-player vocabulary; <c>unknown</c> is a legal opaque kind.</summary>
    public string? type;

    /// <summary>Rekordbox phrase variant; <c>0</c> when variantless.</summary>
    public int variant;

    /// <summary>One-based fill start beat within the phrase; <c>0</c> when the phrase has no fill.</summary>
    public int fillStartBeat;

    /// <summary>Pinned drop landing beat; <c>0</c> when none.</summary>
    public int dropLandingBeat;
}

/// <summary>
/// Assembled song structure of one physical player from chunked <c>/rave/player/{N}/structure</c>
/// datagrams (header <c>sisiiii</c>, then one <c>iisiii</c> tuple per phrase). <c>generation</c> is the
/// sole change detector; <c>trackId</c> is an opaque recognition hint only. <c>phrases</c> may hold
/// fewer entries than <c>phraseCount</c> while chunks are still converging — partial structures are
/// visible by contract.
/// </summary>
[Serializable]
public struct PlayerStructure {
    /// <summary>Structure generation this snapshot belongs to; <c>0</c> = never loaded (sentinel, never on the wire).</summary>
    public int generation;

    /// <summary>Opaque decimal rekordbox track id string; recognition hint only, never a change signal. Empty/null = no track.</summary>
    public string? trackId;

    /// <summary>Structure source: "analyzed" / "synthesized" / "fused" / "unavailable"; empty/null = unavailable.</summary>
    public string? source;

    /// <summary>Total musical beats in the loaded track; <c>-1</c> when unavailable.</summary>
    public int totalBeats;

    /// <summary>Full-track phrase count across all chunks of this generation; <c>0</c> when there are no phrases.</summary>
    public int phraseCount;

    /// <summary>
    /// Ordered phrase list; a phrase's one-based ordinal is its position + 1. Null when no structure
    /// is held. IMMUTABLE ONCE PUBLISHED: the parser's assembler assigns a freshly built array
    /// whenever the visible list changes and never mutates a published array, so the per-element
    /// player copy in <see cref="RaveWireSnapshot.Clone"/> stays a deep copy in effect even though
    /// clones share this reference.
    /// </summary>
    public StructurePhrase[]? phrases;

    /// <summary>Structure whose fields are all unavailable (never-loaded generation <c>0</c>, no phrases).</summary>
    public static PlayerStructure Unavailable => new PlayerStructure {
        generation = 0, trackId = null, source = null, totalBeats = -1, phraseCount = 0, phrases = null,
    };
}

/// <summary>
/// Live structure cursor of one physical player from <c>/rave/player/{N}/structure_state</c>
/// (<c>iiii</c>). The parser applies a cursor only when <c>generation</c> equals the held
/// <see cref="PlayerStructure.generation"/>; a mismatched cursor is dropped silently and the
/// prior cursor holds, so <c>currentPhrase</c> is always an ordinal into the held phrase list.
/// </summary>
[Serializable]
public struct StructureCursor {
    /// <summary>Generation of the structure this cursor belongs to; <c>0</c> when no structure is held.</summary>
    public int generation;

    /// <summary>One-based ordinal (position + 1) of the phrase covering the current beat; <c>-1</c> when none covers it.</summary>
    public int currentPhrase;

    /// <summary>One-based beat position within that phrase; <c>-1</c> in the same no-coverage case.</summary>
    public int beatInPhrase;

    /// <summary>Beats remaining to the next phrase boundary; <c>-1</c> in the same no-coverage case.</summary>
    public int beatsToNextPhrase;

    /// <summary>Cursor bound to no structure (generation <c>0</c>, no-coverage <c>-1</c> sentinels).</summary>
    public static StructureCursor Unavailable => new StructureCursor {
        generation = 0, currentPhrase = -1, beatInPhrase = -1, beatsToNextPhrase = -1,
    };
}

/// <summary>
/// Per-player wire lanes of one physical player (<c>/rave/player/{N}/…</c>): the four bundled
/// lanes plus the assembled structure and its generation-bound cursor. Loop and timing grid
/// reuse the on-air wire shapes verbatim; per the contract the argument meanings and sentinels
/// are identical, only the scope differs.
/// </summary>
[Serializable]
public struct PlayerState {
    public PlayerClock clock;
    public PlayerTransport transport;
    public LoopState loopState;
    public TimingGrid timingGrid;

    /// <summary>Assembled song structure; exposed even while chunks are still converging.</summary>
    public PlayerStructure structure;

    /// <summary>Structure cursor; either unavailable or generation-matched to <see cref="structure"/>.</summary>
    public StructureCursor cursor;

    /// <summary>Player state whose lanes are all unavailable.</summary>
    public static PlayerState Unavailable => new PlayerState {
        clock = PlayerClock.Unavailable,
        transport = PlayerTransport.Unavailable,
        loopState = LoopState.Unavailable,
        timingGrid = TimingGrid.Unavailable,
        structure = PlayerStructure.Unavailable,
        cursor = StructureCursor.Unavailable,
    };
}

/// <summary>
/// Latest known RaveSystem OSC wire values decoded from UDP broadcasts.
/// The fields intentionally mirror the compact OSC payload groups.
/// </summary>
[Serializable]
public sealed class RaveWireSnapshot {
    /// <summary>Number of per-player slots: ProLink device numbers 1..6.</summary>
    public const int PlayerCount = 6;

    public string playersLive = "";
    public string track = "";
    public float bpm = -1f;
    public BeatPosition beat = new BeatPosition { current = -1, total = -1 };
    public BarPosition bar = new BarPosition { current = -1, nextMs = -1 };
    public int beatInBar = -1;
    public int[] beatsCountMs = new[] { -1, -1, -1, -1 };
    public bool[] onBeats = new bool[4];
    public int beatAverageMs = -1;
    public float beatPulse;
    public Levels levels = Levels.Unavailable;
    public PhraseState phraseState = PhraseState.Unavailable;
    public LabeledCountdown nextPhraseState = LabeledCountdown.Unavailable;
    public CountdownState dropState = CountdownState.Unavailable;
    public CountdownState fillState = CountdownState.Unavailable;
    public LabeledCountdown energyState = LabeledCountdown.Unavailable;
    public LabeledCountdown nextEnergyState = LabeledCountdown.Unavailable;
    public LoopState loopState = LoopState.Unavailable;
    public TimingGrid timingGrid = TimingGrid.Unavailable;
    public int trackId = -1;

    /// <summary>Per-player wire state indexed by ProLink device number minus one (players 1..6).</summary>
    public PlayerState[] players = NewUnavailablePlayers();

    /// <summary>Creates a deep copy so background OSC updates cannot mutate a returned snapshot.</summary>
    /// <remarks>
    /// All scalar/struct fields copy via <see cref="object.MemberwiseClone"/>; only the array fields are
    /// then re-copied so the clone is independent for thread-safety. Keeping the per-field list out of here
    /// is deliberate — it stops Clone from silently dropping a newly added scalar field. <see cref="players"/>
    /// holds structs whose reference-typed content is immutable strings and the
    /// <see cref="PlayerStructure.phrases"/> array, which is immutable once published (the parser's
    /// assembler always assigns a fresh array, never mutating a published one), so an element copy
    /// is a deep copy in effect.
    /// </remarks>
    public RaveWireSnapshot Clone() {
        var copy = (RaveWireSnapshot)MemberwiseClone();
        copy.beatsCountMs = CopyFour(beatsCountMs);
        copy.onBeats = CopyFour(onBeats);
        copy.players = CopyPlayers(players);
        return copy;
    }

    /// <summary>Returns the smallest non-negative beat countdown, or <paramref name="fallback" /> when unavailable.</summary>
    public int NextBeatMs(int fallback = -1) {
        var result = int.MaxValue;
        for (var i = 0; i < beatsCountMs.Length; i++) {
            var value = beatsCountMs[i];
            if (value >= 0 && value < result) {
                result = value;
            }
        }
        return result == int.MaxValue ? fallback : result;
    }

    /// <summary>Returns the on-beat gate for a 1-based beat-in-bar label.</summary>
    public bool IsOnBeat(int beatInBar) {
        var index = beatInBar - 1;
        return index >= 0 && index < onBeats.Length && onBeats[index];
    }

    public override string ToString() {
        var bpmText = bpm > 0f ? bpm.ToString("0.##") : "?";
        var beatText = beat.current >= 0 ? beat.current.ToString() : "?";
        var barText = bar.current >= 0 ? bar.current.ToString() : "?";
        var phraseText = string.IsNullOrEmpty(phraseState.label) ? "?" : phraseState.label;
        return $"Rave OSC bpm={bpmText} beat={beatText} bar={barText} phrase={phraseText}";
    }

    private static int[] CopyFour(int[] source) {
        var copy = new int[4];
        if (source != null) {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }

    private static bool[] CopyFour(bool[] source) {
        var copy = new bool[4];
        if (source != null) {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }

    /// <summary>Returns a fixed six-slot player array with every lane at its unavailable sentinel.</summary>
    private static PlayerState[] NewUnavailablePlayers() {
        var players = new PlayerState[PlayerCount];
        for (var i = 0; i < players.Length; i++) {
            players[i] = PlayerState.Unavailable;
        }
        return players;
    }

    /// <summary>Copies the per-player slots into a fresh fixed-size array for <see cref="Clone"/>.</summary>
    /// <remarks>
    /// Phrase arrays are also cloned so the deep-copy contract holds unconditionally — a caller
    /// writing into a taken snapshot's phrases can never reach the parser's held state.
    /// </remarks>
    private static PlayerState[] CopyPlayers(PlayerState[] source) {
        var copy = NewUnavailablePlayers();
        if (source != null) {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            for (var i = 0; i < copy.Length; i++) {
                var phrases = copy[i].structure.phrases;
                if (phrases != null) {
                    copy[i].structure.phrases = (StructurePhrase[])phrases.Clone();
                }
            }
        }
        return copy;
    }
}

}
