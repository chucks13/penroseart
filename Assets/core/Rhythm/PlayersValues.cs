// Captures the six physical players' wire lanes: clock, transport, loop, grid, structure, cursor.
#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PenroseArt.RaveOsc;

/// <summary>Where a player's held song structure came from.</summary>
/// <remarks>
/// Mirrors the wire's closed source vocabulary. The wire's <c>unavailable</c> value (and any
/// unrecognized value) is exposed as a null <see cref="StructureValues.Source"/> rather than a member.
/// </remarks>
public enum StructureSource
{
    /// <summary>Structure read from rekordbox's own phrase analysis.</summary>
    Analyzed,
    /// <summary>Structure synthesized from the waveform.</summary>
    Synthesized,
    /// <summary>Analyzed structure fused with PSSI refinement.</summary>
    Fused,
}

/// <summary>Kind of one analyzed phrase in a player's song structure.</summary>
/// <remarks>
/// Mirrors the wire's closed lowercase phrase-type vocabulary. This is a different set and a
/// different case from the open on-air phrase labels; the two surfaces share no phrase vocabulary.
/// <see cref="Unknown"/> is a legal opaque kind and the safe fallback for any unrecognized value.
/// </remarks>
public enum PhraseType
{
    /// <summary>Phrase kind not in the published vocabulary; also the fallback for an unrecognized value.</summary>
    Unknown,
    /// <summary>Opening section before the main body.</summary>
    Intro,
    /// <summary>Rising or building section.</summary>
    Up,
    /// <summary>Lower-energy section, break, or transition.</summary>
    Down,
    /// <summary>Verse section.</summary>
    Verse,
    /// <summary>Bridge section.</summary>
    Bridge,
    /// <summary>Sustained full-rhythm section.</summary>
    Chorus,
    /// <summary>Closing section shaped for mixing out.</summary>
    Outro,
    /// <summary>Peak-impact entry into a high-energy section.</summary>
    Drop,
}

/// <summary>Immutable mirror of one analyzed phrase in a player's assembled song structure.</summary>
/// <remarks>
/// A phrase's identity is its position in <see cref="StructureValues.Phrases"/> (its one-based
/// ordinal is index + 1), never its <see cref="Type"/>; adjacent identical types are distinct phrases.
/// </remarks>
public readonly struct StructurePhraseValues
{
    /// <summary>Captures one translated wire phrase tuple.</summary>
    internal StructurePhraseValues(int startBeat, int endBeat, PhraseType type, int variant,
        int? fillStartBeat, int? dropLandingBeat)
    {
        StartBeat = startBeat;
        EndBeat = endBeat;
        Type = type;
        Variant = variant;
        FillStartBeat = fillStartBeat;
        DropLandingBeat = dropLandingBeat;
    }

    /// <summary>Inclusive one-based phrase start beat.</summary>
    public int StartBeat { get; }
    /// <summary>Inclusive phrase end beat.</summary>
    public int EndBeat { get; }
    /// <summary>Phrase kind from the closed per-player vocabulary.</summary>
    public PhraseType Type { get; }
    /// <summary>Rekordbox phrase variant; zero when variantless.</summary>
    public int Variant { get; }
    /// <summary>One-based fill start beat within the phrase; null when the phrase has no fill.</summary>
    public int? FillStartBeat { get; }
    /// <summary>Pinned drop landing beat; null when none.</summary>
    public int? DropLandingBeat { get; }
}

/// <summary>Immutable assembled song structure of one physical player.</summary>
public readonly struct StructureValues
{
    /// <summary>Translated ordered phrase list; null only in a default value.</summary>
    private readonly IReadOnlyList<StructurePhraseValues>? phrases;

    /// <summary>Captures the translated structure header and its ordered phrase list.</summary>
    internal StructureValues(int generation, string? trackId, StructureSource? source, int? totalBeats,
        int phraseCount, IReadOnlyList<StructurePhraseValues> phrases)
    {
        Generation = generation;
        TrackId = trackId;
        Source = source;
        TotalBeats = totalBeats;
        PhraseCount = phraseCount;
        this.phrases = phrases;
    }

    /// <summary>Structure generation, the sole change detector; zero when no structure was ever loaded.</summary>
    public int Generation { get; }
    /// <summary>Opaque decimal rekordbox track id, a recognition hint only; null when no track is loaded.</summary>
    public string? TrackId { get; }
    /// <summary>Where the structure came from; null when unavailable.</summary>
    public StructureSource? Source { get; }
    /// <summary>Total musical beats in the loaded track.</summary>
    public int? TotalBeats { get; }
    /// <summary>Full-track phrase count announced by the sender; zero when there are no phrases.</summary>
    public int PhraseCount { get; }

    /// <summary>
    /// Ordered phrase list; a phrase's one-based ordinal is its index + 1. May hold fewer entries
    /// than <see cref="PhraseCount"/> while structure chunks are still converging. Never null.
    /// Only when the list is complete (<c>Phrases.Count == PhraseCount</c>) do cursor ordinals
    /// index it reliably; see <see cref="StructureCursorValues"/>.
    /// </summary>
    public IReadOnlyList<StructurePhraseValues> Phrases => phrases ?? Array.Empty<StructurePhraseValues>();
}

/// <summary>Immutable live structure cursor of one physical player.</summary>
/// <remarks>
/// The parser applies a cursor only when its generation matches the held structure's, so the
/// cursor always refers to the same structure as <see cref="StructureValues.Phrases"/>. While
/// chunks are still converging the visible list can be partial, so <see cref="CurrentPhrase"/>
/// may point past it or land on a different phrase than the same ordinal in the complete list —
/// treat an ordinal beyond <c>Phrases.Count</c> as not-yet-converged and bounds-check before indexing.
/// </remarks>
public readonly struct StructureCursorValues
{
    /// <summary>Captures the translated generation-matched cursor.</summary>
    internal StructureCursorValues(int generation, int? currentPhrase, int? beatInPhrase, int? beatsToNextPhrase)
    {
        Generation = generation;
        CurrentPhrase = currentPhrase;
        BeatInPhrase = beatInPhrase;
        BeatsToNextPhrase = beatsToNextPhrase;
    }

    /// <summary>Generation of the structure this cursor belongs to; zero when no structure is held.</summary>
    public int Generation { get; }
    /// <summary>One-based ordinal of the phrase covering the current beat; null when none covers it.</summary>
    public int? CurrentPhrase { get; }
    /// <summary>One-based beat position within that phrase; null in the same no-coverage case.</summary>
    public int? BeatInPhrase { get; }
    /// <summary>Beats remaining to the next phrase boundary; null in the same no-coverage case.</summary>
    public int? BeatsToNextPhrase { get; }
}

/// <summary>Immutable per-frame values of one physical player (ProLink device number 1..6).</summary>
public readonly struct PlayerValues
{
    /// <summary>Captures one player's translated clock, transport, loop, grid, structure, and cursor.</summary>
    internal PlayerValues(int playerNumber,
        float? bpm, int? beat, int? bar, int? beatInBar, float beatPulse,
        bool? playing, bool? cued, bool? onAir, bool? master, bool? synced, bool live,
        LoopValues loop, GridState? gridState, int? gridBeat, int? gridBar,
        StructureValues structure, StructureCursorValues cursor)
    {
        PlayerNumber = playerNumber;
        Bpm = bpm;
        Beat = beat;
        Bar = bar;
        BeatInBar = beatInBar;
        BeatPulse = beatPulse;
        Playing = playing;
        Cued = cued;
        OnAir = onAir;
        Master = master;
        Synced = synced;
        Live = live;
        Loop = loop;
        GridState = gridState;
        GridBeat = gridBeat;
        GridBar = gridBar;
        Structure = structure;
        Cursor = cursor;
    }

    /// <summary>ProLink device number of this player, 1 through 6.</summary>
    public int PlayerNumber { get; }

    /// <summary>Pitch-adjusted playing tempo of this player's clock.</summary>
    public float? Bpm { get; }
    /// <summary>One-based absolute track beat of this player's clock.</summary>
    public int? Beat { get; }
    /// <summary>One-based absolute four-beat bar of this player's clock.</summary>
    public int? Bar { get; }
    /// <summary>This player's musical count, from 1 through 4.</summary>
    public int? BeatInBar { get; }
    /// <summary>
    /// This player's beat triangle wave: 1 on each beat, 0 halfway between. Zero is both the
    /// mid-beat trough and the at-rest value, so it alone cannot signal a missing clock.
    /// </summary>
    public float BeatPulse { get; }

    /// <summary>Whether this player's audio is rolling; null when unavailable.</summary>
    public bool? Playing { get; }
    /// <summary>Whether this player is engaged with a cue point; null when unavailable.</summary>
    public bool? Cued { get; }
    /// <summary>Whether this player's mixer channel is on air; null when unavailable.</summary>
    public bool? OnAir { get; }
    /// <summary>Whether this player is tempo master; null when unavailable.</summary>
    public bool? Master { get; }
    /// <summary>Whether this player is fully beat-synced; null when unavailable.</summary>
    public bool? Synced { get; }
    /// <summary>Whether the audience hears this player: playing and on air. Rests at false.</summary>
    public bool Live { get; }

    /// <summary>This player's loop wire values, in the same shape as the on-air <see cref="BeatManager.Loop"/>.</summary>
    public LoopValues Loop { get; }

    /// <summary>This player's sixteen-beat grid trust state; null when unavailable.</summary>
    public GridState? GridState { get; }
    /// <summary>One-based beat within this player's sixteen-beat grid.</summary>
    public int? GridBeat { get; }
    /// <summary>One-based bar within this player's four-bar grid.</summary>
    public int? GridBar { get; }

    /// <summary>This player's assembled song structure.</summary>
    public StructureValues Structure { get; }
    /// <summary>This player's live structure cursor, generation-matched to <see cref="Structure"/>.</summary>
    public StructureCursorValues Cursor { get; }
}

/// <summary>Immutable frame-coherent group of all six physical players, ordered by player number.</summary>
public readonly struct PlayersValues
{
    /// <summary>Captured entries indexed by player number minus one; null only in a default value.</summary>
    private readonly PlayerValues[]? players;

    /// <summary>Captures the six translated player entries.</summary>
    internal PlayersValues(PlayerValues[] players)
    {
        this.players = players;
    }

    /// <summary>Number of player entries; always six.</summary>
    public int Count => RaveWireSnapshot.PlayerCount;

    /// <summary>
    /// The entry at <paramref name="index"/> 0..5; the entry at index N-1 is player number N.
    /// In a default <see cref="PlayersValues"/> (before the first capture) entries read fully
    /// unavailable with <see cref="PlayerValues.PlayerNumber"/> 0.
    /// </summary>
    public PlayerValues this[int index] => players is null ? default : players[index];
}

public partial class BeatManager
{
    /// <summary>All six physical players' wire values, ordered by player number.</summary>
    public PlayersValues Players { get; private set; }

    /// <summary>Held structure generation each cached phrase translation belongs to, per player slot.</summary>
    private readonly int[] playerPhrasesGeneration = new int[RaveWireSnapshot.PlayerCount];

    /// <summary>Cached translated phrase lists, per player slot, behind read-only wrappers.</summary>
    private readonly ReadOnlyCollection<StructurePhraseValues>?[] playerPhrasesCache =
        new ReadOnlyCollection<StructurePhraseValues>?[RaveWireSnapshot.PlayerCount];

    /// <summary>Captures all six players from the settled wire snapshot.</summary>
    /// <remarks>
    /// Missing wire slots (a snapshot serialized before players existed) capture as unavailable
    /// entries so the group always presents exactly six players.
    /// </remarks>
    private PlayersValues CapturePlayers()
    {
        var wire = wireSnapshot.players;
        var captured = new PlayerValues[RaveWireSnapshot.PlayerCount];
        for (var i = 0; i < captured.Length; i++)
        {
            var state = wire != null && i < wire.Length ? wire[i] : PlayerState.Unavailable;
            captured[i] = CapturePlayer(i, state);
        }
        return new PlayersValues(captured);
    }

    /// <summary>Translates one player's wire lanes into an immutable entry for slot <paramref name="slot"/>.</summary>
    private PlayerValues CapturePlayer(int slot, in PlayerState state)
    {
        var clock = state.clock;
        var transport = state.transport;
        var grid = state.timingGrid;
        var structure = state.structure;
        var cursor = state.cursor;
        // Match the on-air grid capture: an unparseable state label makes the whole grid
        // unavailable — the state is the trust gate for beat and bar.
        var hasGrid = TryParseGridState(grid.state, out var gridState);
        return new PlayerValues(
            slot + 1,
            clock.bpm > 0f ? clock.bpm : null,
            clock.beat >= 1 ? clock.beat : null,
            clock.bar >= 1 ? clock.bar : null,
            clock.beatInBar is >= 1 and <= BeatSlotCount ? clock.beatInBar : null,
            clock.beatPulse,
            TriStateOrNull(transport.playing),
            TriStateOrNull(transport.cued),
            TriStateOrNull(transport.onAir),
            TriStateOrNull(transport.master),
            TriStateOrNull(transport.synced),
            TriStateTrue(transport.playing) && TriStateTrue(transport.onAir),
            TranslateLoop(state.loopState),
            hasGrid ? gridState : null,
            hasGrid && grid.beat >= 1 ? grid.beat : null,
            hasGrid && grid.bar >= 1 ? grid.bar : null,
            new StructureValues(
                structure.generation,
                string.IsNullOrEmpty(structure.trackId) ? null : structure.trackId,
                ParseStructureSource(structure.source),
                NonNegativeOrNull(structure.totalBeats),
                structure.phraseCount,
                TranslatePhrases(slot, structure)),
            new StructureCursorValues(
                cursor.generation,
                cursor.currentPhrase >= 1 ? cursor.currentPhrase : null,
                cursor.beatInPhrase >= 1 ? cursor.beatInPhrase : null,
                NonNegativeOrNull(cursor.beatsToNextPhrase)));
    }

    /// <summary>Translates one player's wire phrase list, reusing the previous frame's translation when unchanged.</summary>
    /// <remarks>
    /// The generation is the sole change detector and within one generation the visible list only
    /// grows as chunks land, so an unchanged (generation, length) pair means identical content and
    /// the cached list can be handed out again without per-frame retranslation garbage. The cache
    /// is shared across frames, so it is exposed only behind a read-only wrapper — a caller cannot
    /// cast its way back to the mutable array.
    /// </remarks>
    private IReadOnlyList<StructurePhraseValues> TranslatePhrases(int slot, in PlayerStructure structure)
    {
        var wirePhrases = structure.phrases;
        if (wirePhrases == null || wirePhrases.Length == 0)
        {
            playerPhrasesGeneration[slot] = structure.generation;
            playerPhrasesCache[slot] = null;
            return Array.Empty<StructurePhraseValues>();
        }

        var cached = playerPhrasesCache[slot];
        if (cached != null && playerPhrasesGeneration[slot] == structure.generation
            && cached.Count == wirePhrases.Length)
        {
            return cached;
        }

        var translated = new StructurePhraseValues[wirePhrases.Length];
        for (var i = 0; i < translated.Length; i++)
        {
            var phrase = wirePhrases[i];
            translated[i] = new StructurePhraseValues(
                phrase.startBeat,
                phrase.endBeat,
                ParsePhraseType(phrase.type),
                phrase.variant,
                phrase.fillStartBeat >= 1 ? phrase.fillStartBeat : null,
                phrase.dropLandingBeat >= 1 ? phrase.dropLandingBeat : null);
        }
        var wrapped = new ReadOnlyCollection<StructurePhraseValues>(translated);
        playerPhrasesGeneration[slot] = structure.generation;
        playerPhrasesCache[slot] = wrapped;
        return wrapped;
    }

    /// <summary>Translates the wire's closed lowercase phrase-type labels; anything else is Unknown.</summary>
    private static PhraseType ParsePhraseType(string? type)
    {
        return type switch
        {
            "intro" => PhraseType.Intro,
            "up" => PhraseType.Up,
            "down" => PhraseType.Down,
            "verse" => PhraseType.Verse,
            "bridge" => PhraseType.Bridge,
            "chorus" => PhraseType.Chorus,
            "outro" => PhraseType.Outro,
            "drop" => PhraseType.Drop,
            _ => PhraseType.Unknown,
        };
    }

    /// <summary>Translates the wire's closed structure-source labels; unavailable and unrecognized values are null.</summary>
    private static StructureSource? ParseStructureSource(string? source)
    {
        return source switch
        {
            "analyzed" => StructureSource.Analyzed,
            "synthesized" => StructureSource.Synthesized,
            "fused" => StructureSource.Fused,
            _ => null,
        };
    }

    /// <summary>Resets every wire player slot to unavailable as part of the standard no-beat clear.</summary>
    /// <remarks>
    /// Last-known player values persist while the OSC source is live; only a stopped broadcast
    /// clears them, so stale player data never drives visuals in Standalone Mode.
    /// </remarks>
    private void ClearPlayersToNoBeat()
    {
        var players = wireSnapshot.players;
        if (players == null)
        {
            return;
        }

        for (var i = 0; i < players.Length; i++)
        {
            players[i] = PlayerState.Unavailable;
        }
    }
}
