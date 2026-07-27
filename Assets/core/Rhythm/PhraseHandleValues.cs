// Serves the seven typed Phrase handles of the Focus player's Song Structure.
#nullable enable

using System;

/// <summary>
/// One typed Phrase handle of the Song Structure: the <see cref="Before"/> and <see cref="In"/> spans
/// of a single phrase type, read against the Focus player's structure cursor.
/// </summary>
/// <remarks>
/// A handle carries envelopes and nothing else. <see cref="Before"/> targets the <em>next ordinal
/// occurrence</em> of the type after the current position, so during a chorus <c>Chorus.Before</c> means
/// the following chorus and both spans of one handle can be live in the same frame. A default value —
/// no structure held, no generation-matched cursor, no phrase covering the current beat, or Standalone
/// Mode — rests every envelope at its nothing-happening value.
/// </remarks>
public readonly struct PhraseHandleValues
{
    /// <summary>Creates a handle from the two spans read for its phrase type.</summary>
    internal PhraseHandleValues(BeforeSpan before, InSpan within)
    {
        Before = before;
        In = within;
    }

    /// <summary>
    /// Envelopes approaching the next occurrence of this phrase type across a caller-named window of
    /// whole beats.
    /// </summary>
    public BeforeSpan Before { get; }

    /// <summary>Envelopes running through this phrase type while the cursor sits inside one.</summary>
    public InSpan In { get; }
}

/// <summary>
/// One frame's positional reading of the Focus player's Song Structure: where its generation-matched
/// cursor sits, and where each phrase type next begins after that point.
/// </summary>
/// <remarks>
/// Every value is positional — absolute track beats taken from the held phrase list — never time
/// accumulated across frames, so a Loop rewinding into a phrase re-enters its In span and a needle-drop
/// reads correctly by construction. A default reading covers every rest case at once: it holds no
/// next-occurrence table and no covering type, so all seven handles read their nothing-happening values.
/// </remarks>
internal readonly struct FocusStructureReading
{
    /// <summary>
    /// Start beat of the next occurrence of each phrase type, indexed by <see cref="PhraseType"/>; zero
    /// where no occurrence follows. Borrowed from the capturing <see cref="BeatManager"/> and valid only
    /// for that capture, which reads every handle out of it before the next frame overwrites it.
    /// </summary>
    private readonly int[]? nextStartBeats;

    /// <summary>Type of the phrase covering the current beat; <see cref="PhraseType.Unknown"/> when none does.</summary>
    private readonly PhraseType coveringType;

    /// <summary>Continuous beats elapsed through the covering phrase; null when none covers the beat.</summary>
    private readonly float? elapsedBeats;

    /// <summary>Length of the covering phrase in whole beats; null when it is unusable.</summary>
    private readonly int? coveringLength;

    /// <summary>Absolute one-based track beat the cursor sits on.</summary>
    private readonly int currentBeat;

    /// <summary>Continuous fraction elapsed through <see cref="currentBeat"/>.</summary>
    private readonly float intraBeat;

    /// <summary>Captures one frame's cursor position and its next-occurrence table.</summary>
    internal FocusStructureReading(int[] nextStartBeats, PhraseType coveringType, float elapsedBeats,
        int? coveringLength, int currentBeat, float intraBeat)
    {
        this.nextStartBeats = nextStartBeats;
        this.coveringType = coveringType;
        this.elapsedBeats = elapsedBeats;
        this.coveringLength = coveringLength;
        this.currentBeat = currentBeat;
        this.intraBeat = intraBeat;
    }

    /// <summary>Reads both spans of one phrase type from this position.</summary>
    /// <param name="type">The phrase type whose handle is being served.</param>
    internal PhraseHandleValues Handle(PhraseType type)
    {
        var nextStartBeat = nextStartBeats is { } starts ? starts[(int)type] : 0;
        return new PhraseHandleValues(
            new BeforeSpan(nextStartBeat > 0 ? nextStartBeat - currentBeat - intraBeat : null),
            coveringType == type ? new InSpan(elapsedBeats, coveringLength) : default);
    }
}

public partial class BeatManager
{
    /// <summary>Slots needed to index a per-phrase-type table by <see cref="PhraseType"/>.</summary>
    private static readonly int PhraseTypeSlots = Enum.GetValues(typeof(PhraseType)).Length;

    /// <summary>
    /// Start beat of each phrase type's next occurrence, rebuilt in place every capture so the
    /// per-frame structure scan allocates nothing.
    /// </summary>
    private readonly int[] nextPhraseStartBeats = new int[PhraseTypeSlots];

    /// <summary>The Focus deck's opening sections, as Before and In spans.</summary>
    public PhraseHandleValues Intro { get; private set; }

    /// <summary>The Focus deck's rising or building sections, as Before and In spans.</summary>
    public PhraseHandleValues Up { get; private set; }

    /// <summary>The Focus deck's lower-energy sections, breaks, and transitions, as Before and In spans.</summary>
    public PhraseHandleValues Down { get; private set; }

    /// <summary>The Focus deck's verse sections, as Before and In spans.</summary>
    public PhraseHandleValues Verse { get; private set; }

    /// <summary>The Focus deck's bridge sections, as Before and In spans.</summary>
    public PhraseHandleValues Bridge { get; private set; }

    /// <summary>The Focus deck's sustained full-rhythm sections, as Before and In spans.</summary>
    public PhraseHandleValues Chorus { get; private set; }

    /// <summary>The Focus deck's closing sections shaped for mixing out, as Before and In spans.</summary>
    public PhraseHandleValues Outro { get; private set; }

    /// <summary>
    /// Captures the seven typed Phrase handles from the Focus player's Song Structure.
    /// </summary>
    /// <remarks>
    /// The wire's <c>unknown</c> type gets no handle, and the structure's own <c>drop</c> phrases feed no
    /// handle either: <see cref="Drop"/> and <see cref="Fill"/> keep their single source in the on-air
    /// event lanes.
    /// </remarks>
    private void CapturePhraseHandles()
    {
        var reading = ReadFocusStructure();
        Intro = reading.Handle(PhraseType.Intro);
        Up = reading.Handle(PhraseType.Up);
        Down = reading.Handle(PhraseType.Down);
        Verse = reading.Handle(PhraseType.Verse);
        Bridge = reading.Handle(PhraseType.Bridge);
        Chorus = reading.Handle(PhraseType.Chorus);
        Outro = reading.Handle(PhraseType.Outro);
    }

    /// <summary>
    /// Reads where the Focus player's cursor sits in its held Song Structure, and where each phrase
    /// type next begins after that beat.
    /// </summary>
    /// <remarks>
    /// Focus is followed without damping — a live-order change re-reads the new deck's structure the
    /// same frame — and the cursor is honored only when its generation equals the held structure's, the
    /// wire contract's mandatory gate: an ordinal is meaningless against a different structure. Anything
    /// missing (no clock, no focus, no structure, a mismatched generation, a phrase list still
    /// assembling, or no phrase covering the beat) reads as the default resting position.
    /// </remarks>
    private FocusStructureReading ReadFocusStructure()
    {
        // No running beat count is no clock at all: without one the handles rest.
        if (!IsSynced || LiveOrder.Focus is not { } focus)
        {
            return default;
        }

        var deck = Players[focus - 1];
        var structure = deck.Structure;
        var cursor = deck.Cursor;
        if (structure.Generation == 0 || cursor.Generation != structure.Generation)
        {
            return default;
        }

        // A cursor ordinal only indexes a fully assembled list. While structure chunks converge the
        // visible phrases are a partial slice, so the tuple at an ordinal can be a different phrase
        // than the one the sender named — the length gate, not a bounds check, is what makes it safe.
        var phrases = structure.Phrases;
        if (phrases.Count != structure.PhraseCount || cursor.CurrentPhrase is not { } ordinal
            || ordinal > phrases.Count || cursor.BeatInPhrase is not { } beatInPhrase)
        {
            return default;
        }

        var covering = phrases[ordinal - 1];
        var currentBeat = covering.StartBeat + beatInPhrase - 1;
        var coveringLength = covering.EndBeat - covering.StartBeat + 1;
        var intraBeat = IntraBeatFraction();

        Array.Clear(nextPhraseStartBeats, 0, nextPhraseStartBeats.Length);
        for (var i = ordinal; i < phrases.Count; i++)
        {
            // Identity is ordinal, so the first later phrase of a type is that type's next occurrence
            // even when it immediately follows an identical one.
            var phrase = phrases[i];
            if (nextPhraseStartBeats[(int)phrase.Type] == 0 && phrase.StartBeat > currentBeat)
            {
                nextPhraseStartBeats[(int)phrase.Type] = phrase.StartBeat;
            }
        }

        return new FocusStructureReading(
            nextPhraseStartBeats,
            covering.Type,
            beatInPhrase - 1 + intraBeat,
            coveringLength > 0 ? coveringLength : null,
            currentBeat,
            intraBeat);
    }
}
