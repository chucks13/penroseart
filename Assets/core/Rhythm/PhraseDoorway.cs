// The Phrase doorway: the current musical section as a Span and a labeled state (beat-data spec).

#nullable enable

/// <summary>
/// The current musical section — whatever name it carries — one Data Surface doorway. A Phrase is
/// a Span <em>and</em> a labeled state, so it serves Started/Ended and Changed (the one doorway
/// with both). Facts are nullable; the edges are never null and rest at false.
/// </summary>
public readonly struct PhraseView
{
    /// <summary>
    /// The phrase as a Span. "Act differently in an Intro" needs no per-name offering — the name
    /// is a fact on it. A boundary into a new phrase fires Ended and Started on the same frame,
    /// since the facts never go null between back-to-back phrases.
    /// </summary>
    public SpanView<PhraseFacts> Span { get; }

    /// <summary>Edge: the phrase name changed this frame — including appearing from and vanishing to unavailable. Never null.</summary>
    public bool Changed { get; }

    /// <summary>
    /// Name of the next phrase; null when no next phrase is known (including while the final
    /// phrase plays). Canonical vocabulary Intro/Up/Chorus/Drop/Down/Outro; unknown labels are
    /// opaque phrase names, never rejected or mapped.
    /// </summary>
    public string? NextName { get; }

    /// <summary>Beats until the next phrase begins, including the current beat. The pre-arm lane: build toward the boundary.</summary>
    public int? NextInBeats { get; }

    /// <summary>The next phrase's own total length in beats.</summary>
    public int? NextLengthBeats { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated and edges evaluated.</summary>
    internal PhraseView(SpanView<PhraseFacts> span, bool changed, string? nextName, int? nextInBeats,
        int? nextLengthBeats)
    {
        Span = span;
        Changed = changed;
        NextName = nextName;
        NextInBeats = nextInBeats;
        NextLengthBeats = nextLengthBeats;
    }
}

/// <summary>Facts of the current phrase, present while the wire names one.</summary>
public readonly struct PhraseFacts
{
    /// <summary>
    /// The phrase name as broadcast. Canonical names are Intro/Up/Chorus/Drop/Down/Outro; any
    /// other non-empty label is an opaque phrase name served untouched (the empty string is the
    /// wire's unavailable sentinel, not a name — it never appears here).
    /// </summary>
    public string Name { get; }

    /// <summary>Beats remaining in the phrase, including the current beat.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>The phrase's total length in beats. Not necessarily divisible by 16.</summary>
    public int? LengthBeats { get; }

    /// <summary>
    /// Whether the phrase length breaks the ÷16 grid. Informational only — never used to reject,
    /// repair, or reinterpret phrase boundaries (wire contract rule).
    /// </summary>
    public bool? Irregular { get; }

    /// <summary>Built only by the hub's per-update capture.</summary>
    internal PhraseFacts(string name, int? beatsRemaining, int? lengthBeats, bool? irregular)
    {
        Name = name;
        BeatsRemaining = beatsRemaining;
        LengthBeats = lengthBeats;
        Irregular = irregular;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Phrase doorway, captured once per hub update ahead of effect Draw — identical for
    /// every reader within a frame.
    /// </summary>
    public PhraseView Phrase { get; private set; }

    /// <summary>
    /// Prior observed phrase name (null = unavailable), retained between hub updates so the
    /// Changed edge and the span's boundary edges genuinely witness each phrase change
    /// (ADR-0015: the hub owns musical moment identity).
    /// </summary>
    private string? previousPhraseName;

    /// <summary>
    /// Captures the Phrase doorway from the settled transport state. A phrase is inside its span
    /// exactly while the wire names one; a name change during continuous presence is a boundary —
    /// the old span's Ended and the new span's Started fire together. The wire has no "known no
    /// phrase" state (empty is the unavailable sentinel), so the span can never be known-outside;
    /// a phrase first appearing fires Changed but never Started.
    /// </summary>
    private PhraseView CapturePhrase()
    {
        var snapshot = beatData.snapshot;
        var state = snapshot.phraseState;
        var name = string.IsNullOrEmpty(state.label) ? null : state.label;

        var changed = Edges.Changed(previousPhraseName, name);
        var previousInside = previousPhraseName != null ? true : (bool?)null;
        var started = Edges.SpanStarted(previousInside, name != null, changed);
        var ended = Edges.SpanEnded(previousInside, name != null, changed);
        previousPhraseName = name;

        var elapsed = name != null ? ElapsedInSpan(state.countBeats, state.lengthBeats) : null;
        var facts = name != null
            ? new PhraseFacts(name, NonNegativeOrNull(state.countBeats), NonNegativeOrNull(state.lengthBeats),
                TriStateOrNull(state.irregular))
            : (PhraseFacts?)null;
        var span = new SpanView<PhraseFacts>(facts, ProgressOverLength(elapsed, state.lengthBeats),
            started, ended, elapsed, LengthOrNull(state.lengthBeats));

        var next = snapshot.nextPhraseState;
        var nextName = string.IsNullOrEmpty(next.label) ? null : next.label;
        return new PhraseView(span, changed, nextName, NonNegativeOrNull(next.countBeats),
            NonNegativeOrNull(next.lengthBeats));
    }
}
