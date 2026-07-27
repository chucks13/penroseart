using System;
using System.Collections.Generic;

/// <summary>
/// One performer catalog entry as it enters the sheet builder: the musical <see cref="Repertoire"/> the
/// catalog advertises at this position. Position is identity — the builder deals list positions, never deck
/// slots or effect instances, so the plan it returns stays a pure value with no engine references.
/// </summary>
public readonly struct EffectDescriptor
{
    /// <summary>Captures one effect catalog entry.</summary>
    /// <param name="repertoire">Musical capabilities and energy tags this catalog position advertises.</param>
    public EffectDescriptor(Repertoire repertoire)
    {
        Repertoire = repertoire;
    }

    /// <summary>Musical capabilities and energy tags this catalog position advertises.</summary>
    public Repertoire Repertoire { get; }
}

/// <summary>
/// One transition catalog entry as it enters the sheet builder: the <see cref="TransitionRepertoire"/>
/// timing/use contract the catalog advertises at this position. Position is identity, as for
/// <see cref="EffectDescriptor"/>.
/// </summary>
public readonly struct TransitionDescriptor
{
    /// <summary>Captures one transition catalog entry.</summary>
    /// <param name="repertoire">Timing and musical-use contract this catalog position advertises.</param>
    public TransitionDescriptor(TransitionRepertoire repertoire)
    {
        Repertoire = repertoire;
    }

    /// <summary>Timing and musical-use contract this catalog position advertises.</summary>
    public TransitionRepertoire Repertoire { get; }
}

/// <summary>Which protected musical moment an <see cref="AnchorResolution"/> owns.</summary>
public enum AnchorKind
{
    /// <summary>A drop landing — the peak-impact beat a high-energy section enters on.</summary>
    Drop,

    /// <summary>A fill window with no drop behind it — a build-up that still deserves a capable performer.</summary>
    Fill,
}

/// <summary>How an Anchor is performed on the wall.</summary>
public enum AnchorTreatment
{
    /// <summary>
    /// The incumbent capable Effect enters at the prior Cue Mark and plays through the boundary with no
    /// transition; the boundary Cue Mark is suppressed so nothing crossfades over the moment.
    /// </summary>
    RideThrough,

    /// <summary>
    /// A capable Transition owns the boundary Cue Mark, its Impact Point landing on the landing beat, and
    /// deals into a normally selected Effect.
    /// </summary>
    PerformedTransition,
}

/// <summary>
/// One placed Cue Mark in a track-scoped plan: the absolute beat the change lands on and the baked-in
/// Effect and Transition catalog indices selected for it. A mark's Effect plays the segment beginning at
/// <see cref="Beat"/> until the next mark; its Transition performs the change into that segment.
/// A reference type so <see cref="Fired"/> can be set on the mark itself rather than tracked beside it.
/// </summary>
public sealed class CuePlanMark
{
    /// <summary>Captures one placed Cue Mark.</summary>
    /// <param name="beat">Absolute one-based track beat the change lands on (a Grid Boundary).</param>
    /// <param name="effectIndex">Effect catalog index that plays from this mark until the next.</param>
    /// <param name="transitionIndex">Transition catalog index that performs the change into this mark.</param>
    public CuePlanMark(int beat, int effectIndex, int transitionIndex)
    {
        Beat = beat;
        EffectIndex = effectIndex;
        TransitionIndex = transitionIndex;
    }

    /// <summary>Absolute one-based track beat the change lands on; always a Grid Boundary.</summary>
    public int Beat { get; }

    /// <summary>Effect catalog index that plays from this mark until the next mark.</summary>
    public int EffectIndex { get; }

    /// <summary>Transition catalog index that performs the change into this mark.</summary>
    public int TransitionIndex { get; }

    /// <summary>
    /// The beat this cue's Transition left on, or -1 while the cue is still pending. Set once by the Switcher
    /// when it fires. The beat is recorded rather than a bare flag because a cue's Runway start is not fixed by
    /// the plan alone — an override Transition with a different Runway leaves on a different beat — and only the
    /// beat it actually left on identifies the one place a loop can bring the playhead back over it.
    /// </summary>
    public int FiredAtBeat { get; set; } = -1;

    /// <summary>
    /// Whether this cue has been performed. A DJ looping brings the playhead back over a fired mark, which is
    /// how the Switcher knows to ask for a fresh cue instead of playing the same one twice.
    /// </summary>
    public bool Fired => FiredAtBeat >= 0;
}

/// <summary>
/// How one drop or fill Anchor was owned by the plan. A capable performer always owns each Anchor: a
/// <see cref="AnchorTreatment.RideThrough"/> resolution names the Effect that rides the boundary and there
/// is no Cue Mark at <see cref="LandingBeat"/>; a <see cref="AnchorTreatment.PerformedTransition"/>
/// resolution names the Transition carried by the Cue Mark at <see cref="LandingBeat"/>.
/// </summary>
public readonly struct AnchorResolution
{
    /// <summary>Captures one Anchor resolution.</summary>
    /// <param name="landingBeat">Absolute one-based Grid Boundary beat the Anchor lands on.</param>
    /// <param name="kind">Whether the Anchor is a drop landing or a fill window.</param>
    /// <param name="treatment">Whether the Anchor is ridden through or performed by a transition.</param>
    /// <param name="performerIndex">
    /// Effect catalog index for <see cref="AnchorTreatment.RideThrough"/>; Transition catalog index for
    /// <see cref="AnchorTreatment.PerformedTransition"/>.
    /// </param>
    public AnchorResolution(int landingBeat, AnchorKind kind, AnchorTreatment treatment, int performerIndex)
    {
        LandingBeat = landingBeat;
        Kind = kind;
        Treatment = treatment;
        PerformerIndex = performerIndex;
    }

    /// <summary>Absolute one-based Grid Boundary beat the Anchor lands on.</summary>
    public int LandingBeat { get; }

    /// <summary>Whether the Anchor is a drop landing or a fill window.</summary>
    public AnchorKind Kind { get; }

    /// <summary>Whether the Anchor is ridden through or performed by a transition.</summary>
    public AnchorTreatment Treatment { get; }

    /// <summary>
    /// The capable performer that owns the moment: an Effect catalog index when the treatment is
    /// <see cref="AnchorTreatment.RideThrough"/>, a Transition catalog index when it is
    /// <see cref="AnchorTreatment.PerformedTransition"/>.
    /// </summary>
    public int PerformerIndex { get; }
}

/// <summary>
/// A track-scoped, full-length show plan: every Cue Mark placed against a player's real Phrase map with
/// its Effect and Transition assignment baked in, plus how each drop or fill Anchor was owned. Built once
/// per track load by <see cref="Build"/> as a pure, deterministic function of (structure, seed, catalogs);
/// it holds no notion of "now" and no engine references. The Director builds it and hands it over; the
/// Switcher performs it against the sheet player's own beat, and the same load always rebuilds the identical
/// sheet.
/// </summary>
/// <remarks>
/// This is the track-scoped "Cue Sheet" of the track-cue-sheets spec (ADR-0019): it superseded and replaced
/// the phrase-scoped index of empty Cue Marks over a single Phrase.
///
/// Mark placement is one walk over the whole track's Grid Boundaries, counted in beats (ADR-0023). Each
/// candidate boundary is taken with a probability that rises with the gap behind it, so changes spread
/// instead of clustering; a mark is never forced onto a boundary to make the walk land somewhere. Phrase
/// ends carry no special status — a Phrase boundary begins a Grid, so it is simply one more candidate.
/// Every consecutive gap in <see cref="Marks"/> stays within <see cref="MinimumGapBeats"/> and
/// <see cref="MaximumGapBeats"/> by construction, including across Anchor suppression.
/// </remarks>
public readonly struct TrackCueSheet
{
    /// <summary>Beats in one Grid — the 16-beat cycle every Cue Mark lands on.</summary>
    public const int GridBeats = 16;

    /// <summary>Smallest legal gap between consecutive Cue Marks, in beats (one Grid).</summary>
    public const int MinimumGapBeats = GridBeats;

    /// <summary>Largest legal gap between consecutive Cue Marks, in beats (four Grids).</summary>
    public const int MaximumGapBeats = 64;

    /// <summary>
    /// The largest legal gap counted in Grid Boundaries rather than beats — how the Switcher measures it,
    /// because a loop re-crosses the same beat numbers and only boundary crossings measure elapsed music.
    /// </summary>
    public const int MaximumGapGrids = MaximumGapBeats / GridBeats;

    /// <summary>
    /// Minimum beats a drop-landing Effect holds before the next Cue Mark — a named knob, one Grid for now.
    /// No Cue Mark is placed within this window after a drop landing.
    /// </summary>
    public const int PostDropHoldBeats = GridBeats;

    /// <summary>
    /// How often an Anchor is ridden through rather than performed by a Transition, as a percentage. The
    /// incumbent playing the moment itself is the preferred reading of a drop or fill, but not the only one
    /// (ADR-0023); a fair coin here made the wall cut into every second drop.
    /// </summary>
    public const int RideThroughPreferencePercent = 75;

    /// <summary>
    /// Largest gap the walk allows on each side of a pinned Anchor landing: half of
    /// <see cref="MaximumGapBeats"/>, so that when a Ride-through suppresses the landing mark, its two
    /// neighbours — at most one flank apart on each side — are never left more than the full ceiling apart.
    /// Without this the ~43-beat mean spacing made suppression illegal almost everywhere and Ride-through
    /// silently degraded to a Performed Transition.
    /// </summary>
    public const int AnchorFlankBeats = MaximumGapBeats / 2;

    /// <summary>
    /// The chance, as a percentage, that a candidate Grid Boundary becomes a Cue Mark, indexed by how many
    /// whole Grids of music sit behind it (one Grid at index zero, four at index three). Rising rather than
    /// uniform is the whole anti-clustering rule: a boundary one Grid after the last change is nearly always
    /// let past, while the fourth is certain, which bounds every gap to
    /// <see cref="MinimumGapBeats"/>..<see cref="MaximumGapBeats"/> and puts the mean near 43 beats.
    /// </summary>
    private static readonly int[] TakeChancePercent = { 8, 35, 65, 100 };

    /// <summary>The empty mark list a structure-less sheet returns, shared so no-plan sheets allocate nothing.</summary>
    private static readonly IReadOnlyList<CuePlanMark> NoMarks = Array.Empty<CuePlanMark>();

    /// <summary>The empty Anchor list a structure-less sheet returns, shared for the same reason as <see cref="NoMarks"/>.</summary>
    private static readonly IReadOnlyList<AnchorResolution> NoAnchors = Array.Empty<AnchorResolution>();

    /// <summary>
    /// The Effect catalog this sheet was dealt from, retained so <see cref="DealOffPlanCueAt"/> can deal one
    /// more card without the live build-time bags. Descriptors are pure repertoire values with no engine
    /// references, so keeping them leaves the sheet a value.
    /// </summary>
    private readonly IReadOnlyList<EffectDescriptor> effects;

    /// <summary>The Transition catalog this sheet was dealt from, retained for the same reason as <see cref="effects"/>.</summary>
    private readonly IReadOnlyList<TransitionDescriptor> transitions;

    /// <summary>
    /// The run-scoped seed salt this sheet was dealt under (ADR-0024), retained so
    /// <see cref="DealOffPlanCueAt"/> draws from the same salted stream as the plan. Not part of the sheet's
    /// identity: the salt is constant within a run, so (generation, player) still identifies the sheet.
    /// </summary>
    private readonly int salt;

    /// <summary>Captures one finished plan. Private because <see cref="Build"/> is the only way to make a real sheet.</summary>
    /// <param name="marks">Every placed Cue Mark, ascending by beat.</param>
    /// <param name="anchors">Every owned Anchor resolution, ascending by landing beat.</param>
    /// <param name="effects">The Effect catalog this plan was dealt from.</param>
    /// <param name="transitions">The Transition catalog this plan was dealt from.</param>
    /// <param name="structureGeneration">First half of the deal seed and of the sheet's identity.</param>
    /// <param name="playerNumber">Second half of the deal seed and of the sheet's identity.</param>
    /// <param name="salt">Run-scoped seed salt the deal was drawn under (ADR-0024).</param>
    private TrackCueSheet(
        IReadOnlyList<CuePlanMark> marks,
        IReadOnlyList<AnchorResolution> anchors,
        IReadOnlyList<EffectDescriptor> effects,
        IReadOnlyList<TransitionDescriptor> transitions,
        int structureGeneration,
        int playerNumber,
        int salt)
    {
        Marks = marks;
        Anchors = anchors;
        this.effects = effects;
        this.transitions = transitions;
        StructureGeneration = structureGeneration;
        PlayerNumber = playerNumber;
        this.salt = salt;
    }

    /// <summary>Every placed Cue Mark, ascending by beat; the complete fire schedule the Switcher performs.</summary>
    public IReadOnlyList<CuePlanMark> Marks { get; }

    /// <summary>Every owned drop or fill Anchor, ascending by landing beat; how each protected moment is performed.</summary>
    public IReadOnlyList<AnchorResolution> Anchors { get; }

    /// <summary>
    /// Structure generation this sheet was built from — the first half of its deal seed. With
    /// <see cref="PlayerNumber"/> it is the sheet's identity: the Switcher compares the pair to make the
    /// handover idempotent. Zero in a default (no-plan) sheet.
    /// </summary>
    public int StructureGeneration { get; }

    /// <summary>
    /// One-based physical player whose track this sheet plans — the second half of its deal seed and of
    /// the sheet's identity. Zero in a default (no-plan) sheet.
    /// </summary>
    public int PlayerNumber { get; }

    /// <summary>
    /// Deals what to do at a Grid Boundary the plan cannot cover: a fresh Effect and Transition, and whether
    /// to take them here or ride through to the next boundary. Wanted whenever the plan has nothing left to
    /// give at the playhead — the DJ has looped back over a cue already performed, or an inspection freeze has
    /// just ended. Taking is dealt so changes land evenly one to four Grids apart, and is certain once
    /// <paramref name="gapGrids"/> reaches <see cref="MaximumGapGrids"/>, which is what makes holding the wall
    /// still past <see cref="MaximumGapBeats"/> beats impossible. The card is seeded from the sheet's own seed
    /// pair, the boundary, and <paramref name="ask"/> only, so it is reproducible and independent of how long
    /// the wall has held. Leaves the sheet untouched. Valid on any <see cref="Build"/> sheet.
    /// </summary>
    /// <param name="boundaryBeat">Absolute Grid Boundary beat being asked about.</param>
    /// <param name="gapGrids">
    /// The gap in Grids that taking this deal would produce — how far the new Impact Point would sit from the
    /// last one. At <see cref="MaximumGapGrids"/> or beyond the deal is always taken.
    /// </param>
    /// <param name="ask">Which ask this is, counting up; the only thing separating one ask from the next.</param>
    /// <param name="onWallEffectIndex">
    /// Effect catalog index already on the wall, which the deal will not hand back — an off-plan cue exists to
    /// move the wall, and a Transition from an Effect to itself moves nothing. Pass a negative index when
    /// nothing is showing. Honoured unless the catalog has no other card to give.
    /// </param>
    /// <returns>The dealt Effect and Transition catalog indices, and whether to take them at this boundary.</returns>
    public (int EffectIndex, int TransitionIndex, bool Take) DealOffPlanCueAt(
        int boundaryBeat,
        int gapGrids,
        int ask,
        int onWallEffectIndex)
    {
        var rng = new Rng(StructureGeneration ^ salt, PlayerNumber, boundaryBeat, ask);
        var effectBag = new Bag(effects.Count, rng);
        var transitionBag = new Bag(transitions.Count, rng);
        var effectIndex = effectBag.DealPreferred(card => card != onWallEffectIndex);
        var transitionIndex = transitionBag.DealTop();

        // The same rising cadence the plan walk uses (ADR-0023), so an off-plan cue spreads exactly like a
        // planned one instead of carrying its own rule. Drawn after the cards so the card never depends on
        // how long the wall has held.
        var take = TakeBoundary(gapGrids * GridBeats, rng);
        return (effectIndex, transitionIndex, take);
    }

    /// <summary>
    /// Builds a track's complete Cue Sheet as a pure function of its structure, a seed, and the two
    /// performer catalogs. Determinism is total: identical (<paramref name="structure"/>,
    /// <paramref name="structureGeneration"/>, <paramref name="playerNumber"/>) always produce a
    /// byte-identical sheet, and a different generation deals a fresh show. The seed pair is the caller's
    /// (structure generation, player number); the builder folds it into one deterministic roll stream that
    /// drives the Grid walk, both bags, and every Anchor flip.
    /// </summary>
    /// <param name="structure">
    /// The player's assembled song structure. Marks are laid against <see cref="StructureValues.Phrases"/>;
    /// the caller supplies a complete structure (<c>Phrases.Count == PhraseCount</c>). An empty phrase list
    /// yields an empty sheet.
    /// </param>
    /// <param name="effects">Effect catalog as descriptors, one per catalog position. Must be non-empty.</param>
    /// <param name="transitions">Transition catalog as descriptors, one per catalog position. Must be non-empty.</param>
    /// <param name="structureGeneration">The structure's generation — the first half of the seed.</param>
    /// <param name="playerNumber">The physical player number — the second half of the seed.</param>
    /// <param name="salt">
    /// Run-scoped seed salt (ADR-0024), folded into the roll stream so each run deals a fresh show even when
    /// the wire's generation counters restart identically. Constant within a run — the Director draws one at
    /// startup — so rebuilds and handover identity are unaffected. Zero (the default) means unsalted, which
    /// keeps every existing deterministic expectation byte-identical.
    /// </param>
    /// <returns>The complete track-scoped Cue Sheet.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="effects"/> or <paramref name="transitions"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="effects"/> or <paramref name="transitions"/> is empty.</exception>
    public static TrackCueSheet Build(
        StructureValues structure,
        IReadOnlyList<EffectDescriptor> effects,
        IReadOnlyList<TransitionDescriptor> transitions,
        int structureGeneration,
        int playerNumber,
        int salt = 0)
    {
        if (effects == null)
        {
            throw new ArgumentNullException(nameof(effects));
        }

        if (transitions == null)
        {
            throw new ArgumentNullException(nameof(transitions));
        }

        if (effects.Count == 0)
        {
            throw new ArgumentException("Effect catalog must not be empty.", nameof(effects));
        }

        if (transitions.Count == 0)
        {
            throw new ArgumentException("Transition catalog must not be empty.", nameof(transitions));
        }

        var phrases = structure.Phrases;
        if (phrases.Count == 0)
        {
            return new TrackCueSheet(NoMarks, NoAnchors, effects, transitions, structureGeneration, playerNumber, salt);
        }

        var rng = new Rng(structureGeneration ^ salt, playerNumber);
        var effectBag = new Bag(effects.Count, rng);
        var transitionBag = new Bag(transitions.Count, rng);

        // Anchors are read first because their landing beats are pinned into the walk: a drop or fill is the
        // reason a capable performer exists, so those boundaries are marks regardless of the cadence roll.
        var anchors = CollectAnchors(phrases);
        var pinned = new HashSet<int>();
        foreach (var anchor in anchors)
        {
            pinned.Add(anchor.LandingBeat);
        }

        var baseMarks = WalkTrack(phrases, pinned, rng);
        var plan = ResolveAndDeal(baseMarks, anchors, effects, transitions, effectBag, transitionBag, rng, structureGeneration, playerNumber, salt);
        return plan;
    }

    /// <summary>
    /// Every Grid Boundary in the track, ascending. The Grid count is phrase-relative — the wire restarts it
    /// at each Phrase's downbeat (the One) and runs `1..16` for as long as that Phrase lasts — so a Phrase
    /// contributes a boundary at its start and every Grid thereafter that still falls inside it, and the
    /// final Grid of a Phrase whose length is not a Grid multiple is simply short. This is the candidate set
    /// the walk chooses from and the only thing the Phrase map contributes to placement.
    /// </summary>
    private static List<int> GridBoundaries(IReadOnlyList<StructurePhraseValues> phrases)
    {
        var boundaries = new List<int>();
        foreach (var phrase in phrases)
        {
            var length = PhraseLength(phrase);
            for (var offset = 0; offset < length; offset += GridBeats)
            {
                boundaries.Add(phrase.StartBeat + offset);
            }
        }

        return boundaries;
    }

    /// <summary>
    /// Walks the track's Grid Boundaries once, in beats, taking each candidate with a chance that rises with
    /// the gap behind it (ADR-0023). Nothing resets at a Phrase seam and no boundary is ever forced, which is
    /// what stops the clustering the per-Phrase walk produced: that walk had to land exactly on each Phrase
    /// end, so it truncated its own gap draw and jammed changes together at every seam.
    /// </summary>
    /// <param name="phrases">The track's Phrase map, supplying the boundary lattice.</param>
    /// <param name="pinned">Anchor landing beats, which become marks regardless of the cadence roll.</param>
    /// <param name="rng">The sheet's single roll stream.</param>
    /// <returns>Every placed mark beat, ascending, with all gaps inside the cadence bounds.</returns>
    private static List<int> WalkTrack(IReadOnlyList<StructurePhraseValues> phrases, HashSet<int> pinned, Rng rng)
    {
        var marks = new List<int>();
        var boundaries = GridBoundaries(phrases);

        // The run-in from track start is unconstrained — the wall keeps playing whatever it holds until the
        // first mark — so the first gap is measured from the opening downbeat and never forces a mark onto it.
        var lastMark = phrases[0].StartBeat;
        var lastWasPinned = false;

        for (var i = 0; i < boundaries.Count; i++)
        {
            var boundary = boundaries[i];
            if (boundary <= lastMark)
            {
                continue;
            }

            var gap = boundary - lastMark;
            if (pinned.Contains(boundary))
            {
                // A pin outranks the cadence, but not the floor: rather than place two marks inside one Grid,
                // the ordinary mark that crowds it gives way. Two pins that close cannot both be honoured, so
                // the later one is dropped and its Anchor degrades through the usual capability path.
                if (gap < MinimumGapBeats)
                {
                    if (lastWasPinned || marks.Count == 0)
                    {
                        continue;
                    }

                    marks.RemoveAt(marks.Count - 1);
                    lastMark = marks.Count > 0 ? marks[marks.Count - 1] : phrases[0].StartBeat;
                }

                marks.Add(boundary);
                lastMark = boundary;
                lastWasPinned = true;
                continue;
            }

            // The ceiling is enforced against the *next* candidate, not this one. A Phrase whose length is not
            // a Grid multiple leaves a short final Grid, so boundaries are not evenly spaced and "take it once
            // the gap reaches four Grids" can overshoot: the last boundary under the ceiling has to be taken
            // while it is still under it. Beside a pinned Anchor the ceiling halves to
            // <see cref="AnchorFlankBeats"/> — into the pin ahead and out of the pin behind — which is what
            // keeps a Ride-through's mark suppression legal (see the constant's remarks).
            var next = i + 1 < boundaries.Count ? boundaries[i + 1] : int.MaxValue;
            var cap = lastWasPinned || (next != int.MaxValue && pinned.Contains(next))
                ? AnchorFlankBeats
                : MaximumGapBeats;
            var lastChance = next != int.MaxValue && next - lastMark > cap;
            if (!lastChance && !TakeBoundary(gap, rng))
            {
                continue;
            }

            marks.Add(boundary);
            lastMark = boundary;
            lastWasPinned = false;
        }

        return marks;
    }

    /// <summary>
    /// Whether a candidate Grid Boundary becomes a Cue Mark, given the beats of music behind it. Below
    /// <see cref="MinimumGapBeats"/> the answer is always no and below <see cref="MaximumGapBeats"/> always
    /// yes, so the cadence bounds hold by construction; between them the chance rises with the gap
    /// (<see cref="TakeChancePercent"/>). Consumes a roll only in that middle band, where the answer is
    /// genuinely open.
    /// </summary>
    /// <param name="gapBeats">Beats between the last placed mark and this boundary.</param>
    /// <param name="rng">The roll stream to draw from.</param>
    private static bool TakeBoundary(int gapBeats, Rng rng)
    {
        if (gapBeats < MinimumGapBeats)
        {
            return false;
        }

        if (gapBeats >= MaximumGapBeats)
        {
            return true;
        }

        var index = gapBeats / GridBeats - 1;
        if (index >= TakeChancePercent.Length)
        {
            index = TakeChancePercent.Length - 1;
        }

        return rng.Bounded(100) < TakeChancePercent[index];
    }

    /// <summary>
    /// Reads the drop and fill Anchors out of the Phrase map. A Phrase carrying a drop landing is a Drop
    /// Anchor on its own downbeat; a Phrase carrying a fill with no drop on the following Phrase is a Fill
    /// Anchor on its end boundary. A fill leading into a drop is folded into that drop Anchor.
    /// </summary>
    private static List<Anchor> CollectAnchors(IReadOnlyList<StructurePhraseValues> phrases)
    {
        var anchors = new List<Anchor>();
        for (var i = 0; i < phrases.Count; i++)
        {
            var phrase = phrases[i];
            if (phrase.DropLandingBeat is not null)
            {
                // The drop lands on this Phrase's first beat (a Grid Boundary carried by the prior Phrase's
                // end mark). A Phrase-zero drop has no boundary mark to own and is left as a normal opening.
                if (i > 0)
                {
                    anchors.Add(new Anchor(phrase.StartBeat, AnchorKind.Drop, Repertoire.HandlesDrop));
                }

                continue;
            }

            if (phrase.FillStartBeat is not null)
            {
                var followedByDrop = i + 1 < phrases.Count && phrases[i + 1].DropLandingBeat is not null;
                if (!followedByDrop)
                {
                    anchors.Add(new Anchor(phrase.StartBeat + PhraseLength(phrase), AnchorKind.Fill, Repertoire.HandlesFill));
                }
            }
        }

        anchors.Sort(static (a, b) => a.LandingBeat.CompareTo(b.LandingBeat));
        return anchors;
    }

    /// <summary>
    /// Resolves each Anchor by seeded flip, applies suppression, then deals Effects and Transitions to the
    /// surviving marks in beat order. The flip is consumed once per Anchor for stable determinism; the
    /// chosen treatment then degenerates to whichever side the catalogs and geometry actually support.
    /// Effects are dealt from the bag's own order at every mark: capability is asked of a ride-through
    /// carrier, which has to play the moment itself, and of nothing else. Nothing else filters the deal
    /// (ADR-0011), so the plan shows the whole catalog before it repeats anything.
    /// </summary>
    private static TrackCueSheet ResolveAndDeal(
        List<int> baseMarks,
        List<Anchor> anchors,
        IReadOnlyList<EffectDescriptor> effects,
        IReadOnlyList<TransitionDescriptor> transitions,
        Bag effectBag,
        Bag transitionBag,
        Rng rng,
        int structureGeneration,
        int playerNumber,
        int salt)
    {
        var suppressed = new HashSet<int>();
        var rideCarriers = new Dictionary<int, List<Anchor>>();
        var performedMarks = new Dictionary<int, Anchor>();
        var resolutions = new SortedDictionary<int, AnchorResolution>();

        var hasCapableEffectForDrop = AnyEffect(effects, Repertoire.HandlesDrop);
        var hasCapableEffectForFill = AnyEffect(effects, Repertoire.HandlesFill);
        var hasCapableTransitionForDrop = AnyTransition(transitions, Repertoire.HandlesDrop);
        var hasCapableTransitionForFill = AnyTransition(transitions, Repertoire.HandlesFill);

        foreach (var anchor in anchors)
        {
            // One roll per Anchor, always consumed, so the roll stream never depends on catalog contents.
            // Weighted, not fair: the incumbent playing the moment through is the preferred reading.
            var prefersRideThrough = rng.Chance(RideThroughPreferencePercent);

            var capable = anchor.Capability;
            var hasCapableEffect = capable == Repertoire.HandlesDrop ? hasCapableEffectForDrop : hasCapableEffectForFill;
            var hasCapableTransition = capable == Repertoire.HandlesDrop ? hasCapableTransitionForDrop : hasCapableTransitionForFill;

            // Ride-through needs a prior surviving mark and a merged gap that stays within one to four Grids
            // once the boundary mark is removed; otherwise it is not a legal treatment.
            var carrier = LastSurvivingBefore(baseMarks, suppressed, anchor.LandingBeat);
            var canRideThrough = hasCapableEffect && carrier >= 0
                && MergedGapWithin(baseMarks, suppressed, carrier, anchor.LandingBeat);
            var canPerform = hasCapableTransition && baseMarks.Contains(anchor.LandingBeat);

            AnchorTreatment treatment;
            if (canRideThrough && canPerform)
            {
                treatment = prefersRideThrough ? AnchorTreatment.RideThrough : AnchorTreatment.PerformedTransition;
            }
            else if (canRideThrough)
            {
                treatment = AnchorTreatment.RideThrough;
            }
            else if (canPerform)
            {
                treatment = AnchorTreatment.PerformedTransition;
            }
            else
            {
                // No capable performer on either side: leave the boundary as a normal mark, unrecorded.
                continue;
            }

            if (treatment == AnchorTreatment.RideThrough)
            {
                // Adjacent ride-through Anchors can share one incumbent carrier mark; the incumbent rides
                // through every one of them, so a carrier keeps a list rather than a single Anchor.
                suppressed.Add(anchor.LandingBeat);
                if (!rideCarriers.TryGetValue(carrier, out var carried))
                {
                    carried = new List<Anchor>();
                    rideCarriers[carrier] = carried;
                }

                carried.Add(anchor);
            }
            else
            {
                performedMarks[anchor.LandingBeat] = anchor;
            }

            if (anchor.Kind == AnchorKind.Drop)
            {
                SuppressPostDropHold(baseMarks, suppressed, anchor.LandingBeat, performedMarks, rideCarriers);
            }
        }

        var marks = new List<CuePlanMark>();
        foreach (var beat in baseMarks)
        {
            if (suppressed.Contains(beat))
            {
                continue;
            }

            if (performedMarks.TryGetValue(beat, out var performed))
            {
                // Only the Transition has to be capable: it carries the hit, so the Effect it moves toward is
                // dealt like any other — the bag's own order, unfiltered.
                var transitionIndex = transitionBag.DealCapable(i => IsTransitionCapable(transitions, i, performed.Capability), out _);
                var effectIndex = effectBag.DealTop();
                marks.Add(new CuePlanMark(beat, effectIndex, transitionIndex));
                resolutions[performed.LandingBeat] = new AnchorResolution(
                    performed.LandingBeat, performed.Kind, AnchorTreatment.PerformedTransition, transitionIndex);
                continue;
            }

            if (rideCarriers.TryGetValue(beat, out var carriedAnchors))
            {
                // One capable incumbent enters here and rides through every Anchor keyed to this carrier. It
                // must satisfy all of their capabilities; if none does, fall back to the first Anchor's need.
                var combined = Repertoire.None;
                foreach (var carried in carriedAnchors)
                {
                    combined |= carried.Capability;
                }

                var effectIndex = effectBag.DealCapable(i => (effects[i].Repertoire & combined) == combined, out var any);
                if (!any)
                {
                    effectIndex = effectBag.DealCapable(i => IsEffectCapable(effects, i, carriedAnchors[0].Capability), out _);
                }

                var transitionIndex = transitionBag.DealTop();
                marks.Add(new CuePlanMark(beat, effectIndex, transitionIndex));
                foreach (var carried in carriedAnchors)
                {
                    resolutions[carried.LandingBeat] = new AnchorResolution(
                        carried.LandingBeat, carried.Kind, AnchorTreatment.RideThrough, effectIndex);
                }

                continue;
            }

            marks.Add(new CuePlanMark(beat, effectBag.DealTop(), transitionBag.DealTop()));
        }

        var anchorList = new AnchorResolution[resolutions.Count];
        resolutions.Values.CopyTo(anchorList, 0);
        return new TrackCueSheet(marks, anchorList, effects, transitions, structureGeneration, playerNumber, salt);
    }

    /// <summary>Suppresses any base mark inside the post-drop hold window, keeping an owned mark intact.</summary>
    private static void SuppressPostDropHold(
        List<int> baseMarks,
        HashSet<int> suppressed,
        int landingBeat,
        Dictionary<int, Anchor> performedMarks,
        Dictionary<int, List<Anchor>> rideCarriers)
    {
        foreach (var beat in baseMarks)
        {
            if (beat > landingBeat && beat < landingBeat + PostDropHoldBeats
                && !performedMarks.ContainsKey(beat) && !rideCarriers.ContainsKey(beat))
            {
                suppressed.Add(beat);
            }
        }
    }

    /// <summary>The greatest surviving base-mark beat strictly below <paramref name="landingBeat"/>, or -1.</summary>
    private static int LastSurvivingBefore(List<int> baseMarks, HashSet<int> suppressed, int landingBeat)
    {
        var best = -1;
        foreach (var beat in baseMarks)
        {
            if (beat < landingBeat && beat > best && !suppressed.Contains(beat))
            {
                best = beat;
            }
        }

        return best;
    }

    /// <summary>
    /// Whether removing the boundary mark keeps the merged carrier-to-next gap within the maximum cadence.
    /// The next surviving mark above the boundary is used, or the boundary itself when none exists.
    /// </summary>
    private static bool MergedGapWithin(List<int> baseMarks, HashSet<int> suppressed, int carrier, int landingBeat)
    {
        var next = int.MaxValue;
        foreach (var beat in baseMarks)
        {
            if (beat > landingBeat && beat < next && !suppressed.Contains(beat))
            {
                next = beat;
            }
        }

        var mergedTo = next == int.MaxValue ? landingBeat : next;
        return mergedTo - carrier <= MaximumGapBeats;
    }

    /// <summary>Phrase length in beats: an inclusive one-based span, so its end mark is the next downbeat.</summary>
    private static int PhraseLength(StructurePhraseValues phrase)
    {
        return phrase.EndBeat - phrase.StartBeat + 1;
    }

    /// <summary>Whether any Effect in the catalog carries <paramref name="capability"/>.</summary>
    private static bool AnyEffect(IReadOnlyList<EffectDescriptor> effects, Repertoire capability)
    {
        for (var i = 0; i < effects.Count; i++)
        {
            if ((effects[i].Repertoire & capability) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether any Transition in the catalog carries <paramref name="capability"/>.</summary>
    private static bool AnyTransition(IReadOnlyList<TransitionDescriptor> transitions, Repertoire capability)
    {
        for (var i = 0; i < transitions.Count; i++)
        {
            if ((transitions[i].Repertoire.Tags & capability) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the Effect at <paramref name="index"/> carries <paramref name="capability"/>.</summary>
    private static bool IsEffectCapable(IReadOnlyList<EffectDescriptor> effects, int index, Repertoire capability)
    {
        return (effects[index].Repertoire & capability) != 0;
    }

    /// <summary>Whether the Transition at <paramref name="index"/> carries <paramref name="capability"/>.</summary>
    private static bool IsTransitionCapable(IReadOnlyList<TransitionDescriptor> transitions, int index, Repertoire capability)
    {
        return (transitions[index].Repertoire.Tags & capability) != 0;
    }

    /// <summary>A drop or fill window read from the Phrase map, before a treatment is chosen for it.</summary>
    private readonly struct Anchor
    {
        /// <summary>Captures one Anchor read from the Phrase map.</summary>
        /// <param name="landingBeat">Absolute Grid Boundary beat the Anchor lands on.</param>
        /// <param name="kind">Whether this is a drop landing or a fill window.</param>
        /// <param name="capability">The single Repertoire flag a performer must carry to own it.</param>
        public Anchor(int landingBeat, AnchorKind kind, Repertoire capability)
        {
            LandingBeat = landingBeat;
            Kind = kind;
            Capability = capability;
        }

        /// <summary>Absolute Grid Boundary beat the Anchor lands on.</summary>
        public int LandingBeat { get; }

        /// <summary>Whether this is a drop landing or a fill window.</summary>
        public AnchorKind Kind { get; }

        /// <summary>The single Repertoire flag a performer must carry to own this Anchor.</summary>
        public Repertoire Capability { get; }
    }

    /// <summary>
    /// The sheet's single deterministic roll stream: an FNV-1a fold of the seed pair advanced by xorshift32.
    /// The Grid walk, both <see cref="Bag"/>s, and every Anchor flip draw from this one stream, so the whole
    /// sheet is a byte-identical function of the seed pair.
    /// </summary>
    private sealed class Rng
    {
        /// <summary>The xorshift32 register; every draw advances it.</summary>
        private uint state;

        /// <summary>Folds the sheet's seed pair — Structure Generation and player number — into the stream.</summary>
        public Rng(int first, int second)
        {
            unchecked
            {
                var folded = 2166136261u;
                folded = (folded ^ (uint)first) * 16777619u;
                folded = (folded ^ (uint)second) * 16777619u;
                state = folded == 0u ? 0x9E3779B9u : folded;
            }
        }

        /// <summary>
        /// Folds two further dimensions into the seed for an off-plan deterministic deal (the boundary beat
        /// and which ask it is). A distinct stream from the two-argument build seed, so the
        /// off-plan deal never disturbs the sheet's own roll, and distinct per ask, so a loop asking again at
        /// the same boundary is not handed the same answer.
        /// </summary>
        public Rng(int first, int second, int third, int fourth)
        {
            unchecked
            {
                var folded = 2166136261u;
                folded = (folded ^ (uint)first) * 16777619u;
                folded = (folded ^ (uint)second) * 16777619u;
                folded = (folded ^ (uint)third) * 16777619u;
                folded = (folded ^ (uint)fourth) * 16777619u;
                state = folded == 0u ? 0x9E3779B9u : folded;
            }
        }

        /// <summary>Advances the stream and returns the next value.</summary>
        public uint Next()
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }
        }

        /// <summary>A uniform roll in [0, <paramref name="exclusiveBound"/>); zero when the bound is one or less.</summary>
        public int Bounded(int exclusiveBound)
        {
            return exclusiveBound <= 1 ? 0 : (int)(Next() % (uint)exclusiveBound);
        }

        /// <summary>A weighted yes/no draw: true with <paramref name="percent"/> chance in a hundred.</summary>
        /// <param name="percent">Chance of true, 0..100.</param>
        public bool Chance(int percent)
        {
            return Bounded(100) < percent;
        }
    }

    /// <summary>
    /// A seeded shuffled Bag over one catalog's indices, dealt top-down and reshuffled when empty so the
    /// whole catalog is shown before any card repeats — including across the seam between two passes, where
    /// the card just dealt is kept off the top of the new permutation. Anchors scan the Bag for a capable
    /// card and encore the least-recently-dealt capable card from the discard pile only when the remaining
    /// cards have none; that encore is the one path that can still repeat a card back to back.
    /// </summary>
    private sealed class Bag
    {
        /// <summary>How many cards the catalog holds; every pass deals a permutation of exactly these.</summary>
        private readonly int cardCount;

        /// <summary>The sheet's roll stream, shared with the Grid walk and the Anchor flips.</summary>
        private readonly Rng rng;

        /// <summary>Cards not yet dealt in this pass, in the order they will come off the top.</summary>
        private readonly List<int> remaining;

        /// <summary>Cards already dealt in this pass, oldest first — the encore pile for capability scans.</summary>
        private readonly List<int> discard;

        /// <summary>Creates a Bag over <paramref name="cardCount"/> catalog positions and shuffles the first pass.</summary>
        /// <param name="cardCount">Number of cards in the catalog this Bag deals.</param>
        /// <param name="rng">The sheet's single roll stream.</param>
        public Bag(int cardCount, Rng rng)
        {
            this.cardCount = cardCount;
            this.rng = rng;
            remaining = new List<int>(cardCount);
            discard = new List<int>(cardCount);
            Reshuffle();
        }

        /// <summary>Deals the top card, reshuffling first if the Bag is empty.</summary>
        public int DealTop()
        {
            EnsureCards();
            return Take(0);
        }

        /// <summary>
        /// Deals the first card matching <paramref name="preferred"/>, else the top card. A soft scan: no
        /// match simply falls back to the top card and never encores. Used for the off-plan deal's exclusion
        /// of whatever is already on the wall.
        /// </summary>
        public int DealPreferred(Func<int, bool> preferred)
        {
            EnsureCards();
            for (var i = 0; i < remaining.Count; i++)
            {
                if (preferred(remaining[i]))
                {
                    return Take(i);
                }
            }

            return Take(0);
        }

        /// <summary>
        /// Deals the first remaining card matching <paramref name="capable"/>. If no remaining card matches,
        /// encores the least-recently-dealt capable card from the discard pile without consuming a card.
        /// <paramref name="any"/> is false only when the whole catalog holds no capable card.
        /// </summary>
        public int DealCapable(Func<int, bool> capable, out bool any)
        {
            EnsureCards();
            for (var i = 0; i < remaining.Count; i++)
            {
                if (capable(remaining[i]))
                {
                    any = true;
                    return Take(i);
                }
            }

            for (var i = 0; i < discard.Count; i++)
            {
                if (capable(discard[i]))
                {
                    any = true;
                    return discard[i];
                }
            }

            any = false;
            return -1;
        }

        /// <summary>Refills the Bag when the current pass is spent, so a deal never comes up empty.</summary>
        private void EnsureCards()
        {
            if (remaining.Count == 0)
            {
                Reshuffle();
            }
        }

        /// <summary>Removes the card at <paramref name="index"/> from the pass and discards it.</summary>
        /// <param name="index">Position in <see cref="remaining"/> to deal.</param>
        /// <returns>The dealt catalog position.</returns>
        private int Take(int index)
        {
            var card = remaining[index];
            remaining.RemoveAt(index);
            discard.Add(card);
            return card;
        }

        /// <summary>
        /// Refills the Bag with a fresh Fisher-Yates permutation and clears the discard pile. The card just
        /// dealt is kept off the top of the new permutation, because the seam between two passes is the one
        /// place a fair bag can otherwise deal the same card twice running — and for the Effect catalog that
        /// deals a Transition from a card to itself, which restarts the Effect in place and moves nothing.
        /// </summary>
        private void Reshuffle()
        {
            var lastDealt = discard.Count > 0 ? discard[^1] : -1;

            remaining.Clear();
            for (var i = 0; i < cardCount; i++)
            {
                remaining.Add(i);
            }

            for (var i = remaining.Count - 1; i > 0; i--)
            {
                var j = rng.Bounded(i + 1);
                (remaining[i], remaining[j]) = (remaining[j], remaining[i]);
            }

            // Swapped with a rolled position rather than a fixed one, so avoiding the repeat does not itself
            // bias which card follows it. A one-card catalog has no alternative and keeps the repeat.
            if (cardCount > 1 && remaining[0] == lastDealt)
            {
                var swapWith = 1 + rng.Bounded(cardCount - 1);
                (remaining[0], remaining[swapWith]) = (remaining[swapWith], remaining[0]);
            }

            discard.Clear();
        }
    }
}
