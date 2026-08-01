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
/// timing contract the catalog advertises at this position. Position is identity, as for
/// <see cref="EffectDescriptor"/>.
/// </summary>
public readonly struct TransitionDescriptor
{
    /// <summary>Captures one transition catalog entry.</summary>
    /// <param name="repertoire">Timing contract this catalog position advertises.</param>
    public TransitionDescriptor(TransitionRepertoire repertoire)
    {
        Repertoire = repertoire;
    }

    /// <summary>Timing contract this catalog position advertises.</summary>
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
    /// The beat this cue's Transition left on, or -1 while the cue is still pending. Recorded by the Switcher
    /// when it fires. The beat is recorded rather than a bare flag because a cue's Runway start is not fixed by
    /// the plan alone — an override Transition with a different Runway leaves on a different beat — and the
    /// recorded beat identifies exactly when the permanent check-off was set.
    /// </summary>
    public int FiredAtBeat { get; set; } = -1;

    /// <summary>
    /// Whether this cue has been performed. Once set, this check-off is permanent for the life of the sheet:
    /// re-crossing the mark never replays it, and any Off-Plan Cue lives outside the sheet.
    /// </summary>
    public bool Fired => FiredAtBeat >= 0;
}

/// <summary>
/// How one drop or fill Anchor is owned by the plan — there is one way an Anchor is performed: a
/// repertoire-capable Effect is already on the wall when the moment hits and rides through it, no Cue
/// Mark sits on <see cref="LandingBeat"/>, and no transition's Runway or Tail crosses it.
/// <see cref="EffectIndex"/> names the capable Effect, cast at the last Cue Mark before the landing.
/// </summary>
public readonly struct AnchorResolution
{
    /// <summary>Captures one Anchor resolution.</summary>
    /// <param name="landingBeat">Absolute one-based Grid Boundary beat the Anchor lands on.</param>
    /// <param name="kind">Whether the Anchor is a drop landing or a fill window.</param>
    /// <param name="effectIndex">Effect catalog index of the capable Effect on the wall at the moment.</param>
    public AnchorResolution(int landingBeat, AnchorKind kind, int effectIndex)
    {
        LandingBeat = landingBeat;
        Kind = kind;
        EffectIndex = effectIndex;
    }

    /// <summary>Absolute one-based Grid Boundary beat the Anchor lands on.</summary>
    public int LandingBeat { get; }

    /// <summary>Whether the Anchor is a drop landing or a fill window.</summary>
    public AnchorKind Kind { get; }

    /// <summary>
    /// Effect catalog index of the capable Effect that owns the moment: dealt to the last Cue Mark before
    /// <see cref="LandingBeat"/>, so it is already on the wall — showing off on its own — when the moment hits.
    /// </summary>
    public int EffectIndex { get; }
}

/// <summary>
/// A track-scoped, full-length show plan: every Cue Mark placed against a player's real Phrase map with
/// its Effect and Transition assignment baked in, plus how each drop or fill Anchor is owned. Built once
/// per track load by <see cref="Build"/> as a pure, deterministic function of (structure, seed, catalogs);
/// it holds no notion of "now" and no engine references. The Director builds it and hands it over; the
/// Switcher performs it, and the same load always rebuilds the identical sheet.
/// </summary>
/// <remarks>
/// This is the track-scoped "Cue Sheet" of the track-cue-sheets spec (ADR-0010): it superseded and replaced
/// the phrase-scoped index of empty Cue Marks over a single Phrase.
///
/// Mark placement is one walk over the whole track's Grid Boundaries. Each
/// candidate boundary is taken with a probability that rises with the actual Grid count behind it, so
/// changes spread irregularly instead of clustering; a mark is never forced onto a boundary except to keep
/// the <see cref="MaximumGapGrids"/> limit or to put a capable Effect on the wall ahead of an Anchor. There
/// is no fixed spacing floor: the only lower bound on a gap is that the catalog's transitions must fit the
/// space they are given, so no two blends can ever overlap. Phrase ends carry no special status — a Phrase
/// boundary begins a Grid, so it is simply one more candidate. Around each owned Anchor the landing
/// boundary and its immediate flanks are withheld from the walk, which is what clears the moment of
/// Runways and Tails by construction.
/// </remarks>
public readonly struct TrackCueSheet
{
    /// <summary>Beats in one Grid — the 16-beat cycle every Cue Mark lands on.</summary>
    public const int GridBeats = 16;

    /// <summary>Largest legal gap between consecutive Cue Marks, measured in actual Grids.</summary>
    public const int MaximumGapGrids = 4;

    /// <summary>
    /// The chance, as a percentage, that a candidate Grid Boundary becomes a Cue Mark, indexed by how many
    /// actual Grids of music sit behind it (one Grid at index zero, four at index three). Rising rather
    /// than uniform is the anti-clustering rule: a boundary shortly after the last change is nearly always
    /// let past, while the fourth Grid is certain, which caps every consecutive planned gap at
    /// <see cref="MaximumGapGrids"/> actual Grids.
    /// </summary>
    private static readonly int[] TakeChancePercent = { 8, 35, 65, 100 };

    /// <summary>The empty mark list a structure-less sheet returns, shared so no-plan sheets allocate nothing.</summary>
    private static readonly IReadOnlyList<CuePlanMark> NoMarks = Array.Empty<CuePlanMark>();

    /// <summary>The empty Anchor list a structure-less sheet returns, shared for the same reason as <see cref="NoMarks"/>.</summary>
    private static readonly IReadOnlyList<AnchorResolution> NoAnchors = Array.Empty<AnchorResolution>();

    /// <summary>The empty boundary list a structure-less sheet returns, shared for the same reason as <see cref="NoMarks"/>.</summary>
    private static readonly IReadOnlyList<int> NoBoundaries = Array.Empty<int>();

    /// <summary>
    /// The Effect catalog this sheet was dealt from, retained so <see cref="DealOffPlanCueAt"/> can deal one
    /// more card without the live build-time bags. Descriptors are pure repertoire values with no engine
    /// references, so keeping them leaves the sheet a value.
    /// </summary>
    private readonly IReadOnlyList<EffectDescriptor> effects;

    /// <summary>The Transition catalog this sheet was dealt from, retained for the same reason as <see cref="effects"/>.</summary>
    private readonly IReadOnlyList<TransitionDescriptor> transitions;

    /// <summary>
    /// The run-scoped seed salt this sheet was dealt under (ADR-0008), retained so
    /// <see cref="DealOffPlanCueAt"/> draws from the same salted stream as the plan. Not part of the sheet's
    /// identity: the salt is constant within a run, so (generation, player) still identifies the sheet.
    /// </summary>
    private readonly int salt;

    /// <summary>
    /// Every Grid Boundary of the built structure, ascending — the phrase-relative lattice the walk placed
    /// marks on, retained so <see cref="ClosingBoundaryAfter"/> can answer from the plan's own knowledge.
    /// </summary>
    private readonly IReadOnlyList<int> boundaries;

    /// <summary>Captures one finished plan. Private because <see cref="Build"/> is the only way to make a real sheet.</summary>
    /// <param name="marks">Every placed Cue Mark, ascending by beat.</param>
    /// <param name="anchors">Every owned Anchor resolution, ascending by landing beat.</param>
    /// <param name="effects">The Effect catalog this plan was dealt from.</param>
    /// <param name="transitions">The Transition catalog this plan was dealt from.</param>
    /// <param name="structureGeneration">First half of the deal seed and of the sheet's identity.</param>
    /// <param name="playerNumber">Second half of the deal seed and of the sheet's identity.</param>
    /// <param name="salt">Run-scoped seed salt the deal was drawn under (ADR-0008).</param>
    /// <param name="boundaries">Every Grid Boundary of the built structure, ascending.</param>
    private TrackCueSheet(
        IReadOnlyList<CuePlanMark> marks,
        IReadOnlyList<AnchorResolution> anchors,
        IReadOnlyList<EffectDescriptor> effects,
        IReadOnlyList<TransitionDescriptor> transitions,
        int structureGeneration,
        int playerNumber,
        int salt,
        IReadOnlyList<int> boundaries)
    {
        Marks = marks;
        Anchors = anchors;
        this.effects = effects;
        this.transitions = transitions;
        StructureGeneration = structureGeneration;
        PlayerNumber = playerNumber;
        this.salt = salt;
        this.boundaries = boundaries;
    }

    /// <summary>Every placed Cue Mark, ascending by beat; the complete fire schedule the Switcher performs.</summary>
    public IReadOnlyList<CuePlanMark> Marks { get; }

    /// <summary>Every owned drop or fill Anchor, ascending by landing beat; how each protected moment is owned.</summary>
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
    /// Deals a fresh Effect and Transition for an anomaly at a Grid Boundary, and whether to take them here or
    /// ride through to the next boundary. Taking follows the same one-to-four-Grid cadence as planning and is
    /// certain once <paramref name="gapGrids"/> reaches <see cref="MaximumGapGrids"/>. This Off-Plan Cue does
    /// not spend a Cue Mark or change any fired check-off. The card is seeded from the sheet's own seed pair,
    /// the boundary, and
    /// <paramref name="ask"/> only, so it is reproducible and independent of prior asks. Leaves the sheet
    /// untouched. Valid on any <see cref="Build"/> sheet.
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
    /// <param name="movingTowardEffectIndex">
    /// Effect catalog index a mid-flight Transition is carrying the wall toward, excluded for the same
    /// reason: a doorway answer must move the wall somewhere new, never where it is already headed. Equal to
    /// <paramref name="onWallEffectIndex"/> when no blend is in flight; negative when nothing is staged.
    /// Honoured unless the catalog has no other card to give.
    /// </param>
    /// <returns>The dealt Effect and Transition catalog indices, and whether to take them at this boundary.</returns>
    /// <summary>
    /// The Grid Boundary closing the Grid that contains <paramref name="beat"/> — the first boundary of the
    /// plan's phrase-relative lattice strictly after it, short Grids included, known at track load. Null when
    /// the plan cannot answer: a structure-less sheet, or a beat at or past the track's last boundary — the
    /// caller falls back to nominal Grid math there, and a wrong nominal guess self-heals at the next
    /// Grid-start think.
    /// </summary>
    /// <param name="beat">The on-air beat to look ahead from, normally a Grid's opening beat.</param>
    public int? ClosingBoundaryAfter(int beat)
    {
        if (boundaries == null)
        {
            return null;
        }

        foreach (var boundary in boundaries)
        {
            if (boundary > beat)
            {
                return boundary;
            }
        }

        return null;
    }

    public (int EffectIndex, int TransitionIndex, bool Take) DealOffPlanCueAt(
        int boundaryBeat,
        int gapGrids,
        int ask,
        int onWallEffectIndex,
        int movingTowardEffectIndex)
    {
        var rng = new Rng(StructureGeneration ^ salt, PlayerNumber, boundaryBeat, ask);
        var effectBag = new Bag(effects.Count, rng);
        var transitionBag = new Bag(transitions.Count, rng);
        var effectIndex = effectBag.DealPreferred(card => card != onWallEffectIndex && card != movingTowardEffectIndex);
        var transitionIndex = transitionBag.DealTop();

        // The same Grid-counted cadence the plan walk uses. Drawn after the cards so the card never
        // depends on how long the wall has held.
        var take = TakeBoundary(gapGrids, rng);
        return (effectIndex, transitionIndex, take);
    }

    /// <summary>
    /// Builds a track's complete Cue Sheet as a pure function of its structure, a seed, and the two
    /// performer catalogs. Determinism is total: identical (<paramref name="structure"/>,
    /// <paramref name="structureGeneration"/>, <paramref name="playerNumber"/>) always produce a
    /// byte-identical sheet, and a different generation deals a fresh show. The seed pair is the caller's
    /// (structure generation, player number); the builder folds it into one deterministic roll stream that
    /// drives the Grid walk and both bags.
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
    /// Run-scoped seed salt (ADR-0008), folded into the roll stream so each run deals a fresh show even when
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
            return new TrackCueSheet(NoMarks, NoAnchors, effects, transitions, structureGeneration, playerNumber, salt, NoBoundaries);
        }

        var rng = new Rng(structureGeneration ^ salt, playerNumber);
        var effectBag = new Bag(effects.Count, rng);
        var transitionBag = new Bag(transitions.Count, rng);

        // The fit guarantee card: the transition whose whole blend is the shortest. Every gap the walk
        // accepts and every clearance zone it withholds is sized so this card always fits, which is what
        // makes "no two blends overlap" and "the moment is cleared" hold by construction.
        SmallestBlend(transitions, out var smallRunway, out var smallTail);

        var startBeat = phrases[0].StartBeat;
        var boundaries = GridBoundaries(phrases);
        var anchors = CollectAnchors(phrases);
        var owned = OwnableAnchors(anchors, boundaries, startBeat, effects, smallRunway, smallTail);
        var candidates = WithoutClearanceZones(boundaries, owned, smallRunway, smallTail);
        var markBeats = WalkTrack(candidates, boundaries, owned, startBeat, smallRunway, smallTail, rng);
        return Deal(
            markBeats, owned, effects, transitions, effectBag, transitionBag,
            startBeat, smallRunway, structureGeneration, playerNumber, salt, boundaries);
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
    /// Finds the transition catalog's shortest whole blend: the card with the smallest Runway + Tail,
    /// earliest catalog position on a tie. Its Runway and Tail are the fit floor the walk guarantees
    /// everywhere, so the deal can never come up empty.
    /// </summary>
    private static void SmallestBlend(IReadOnlyList<TransitionDescriptor> transitions, out int runway, out int tail)
    {
        var best = transitions[0].Repertoire;
        for (var i = 1; i < transitions.Count; i++)
        {
            if (transitions[i].Repertoire.DurationBeats < best.DurationBeats)
            {
                best = transitions[i].Repertoire;
            }
        }

        runway = best.RunwayBeats;
        tail = best.TailBeats;
    }

    /// <summary>
    /// Decides which Anchors the plan can own under the single Anchor treatment: the catalog holds a capable
    /// Effect, and a carrier Cue Mark can exist before the landing — either because an earlier owned Anchor
    /// already forces one, or because a candidate boundary can legally be the first mark and
    /// still clears the moment. An Anchor that cannot be owned is dropped here: no resolution is recorded and
    /// its landing boundary goes back to being an ordinary candidate.
    /// </summary>
    private static List<Anchor> OwnableAnchors(
        List<Anchor> anchors,
        List<int> boundaries,
        int startBeat,
        IReadOnlyList<EffectDescriptor> effects,
        int smallRunway,
        int smallTail)
    {
        var owned = new List<Anchor>();
        foreach (var anchor in anchors)
        {
            if (!AnyEffect(effects, anchor.Capability))
            {
                continue;
            }

            // An earlier owned Anchor already guarantees a mark before its own (earlier) landing, and any
            // mark before the earlier landing is also before this one.
            var carrierPossible = owned.Count > 0;
            if (!carrierPossible)
            {
                foreach (var boundary in boundaries)
                {
                    if (boundary >= anchor.LandingBeat - smallTail)
                    {
                        break;
                    }

                    if (boundary > startBeat && boundary - startBeat >= smallRunway)
                    {
                        carrierPossible = true;
                        break;
                    }
                }
            }

            if (carrierPossible)
            {
                owned.Add(anchor);
            }
        }

        return owned;
    }

    /// <summary>
    /// Withholds each owned Anchor's clearance zone from the candidate Grid Boundaries: the landing boundary
    /// itself (no Cue Mark sits on it) and any boundary so close that even the shortest card's Tail or Runway
    /// would cross the moment. What remains is every boundary a mark may legally land on.
    /// </summary>
    private static List<int> WithoutClearanceZones(
        List<int> boundaries,
        List<Anchor> owned,
        int smallRunway,
        int smallTail)
    {
        if (owned.Count == 0)
        {
            return boundaries;
        }

        var candidates = new List<int>(boundaries.Count);
        foreach (var boundary in boundaries)
        {
            var excluded = false;
            foreach (var anchor in owned)
            {
                if (boundary >= anchor.LandingBeat - smallTail && boundary <= anchor.LandingBeat + smallRunway)
                {
                    excluded = true;
                    break;
                }
            }

            if (!excluded)
            {
                candidates.Add(boundary);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Walks the candidate boundaries once, taking each with a chance that rises with the actual Grid count
    /// behind it. Nothing resets at a Phrase seam, and a boundary is forced only twice over: when skipping it
    /// would leave the next candidate past <see cref="MaximumGapGrids"/>, and when it is the last chance to put
    /// the Effect that will be on the wall ahead of the earliest owned Anchor. A boundary whose beat gap is
    /// too small for the shortest card to fit is never taken; fit is the only lower bound on spacing.
    /// </summary>
    /// <param name="candidates">The candidate Grid Boundaries with every clearance zone already withheld.</param>
    /// <param name="boundaries">Every actual phrase-relative Grid Boundary, ascending.</param>
    /// <param name="owned">The owned Anchors, ascending by landing beat.</param>
    /// <param name="startBeat">The track's opening downbeat; the first blend may not start before it.</param>
    /// <param name="smallRunway">Runway of the catalog's shortest blend.</param>
    /// <param name="smallTail">Tail of the catalog's shortest blend.</param>
    /// <param name="rng">The sheet's single roll stream.</param>
    /// <returns>Every placed mark beat, ascending.</returns>
    private static List<int> WalkTrack(
        List<int> candidates,
        List<int> boundaries,
        List<Anchor> owned,
        int startBeat,
        int smallRunway,
        int smallTail,
        Rng rng)
    {
        var marks = new List<int>();
        var earliestLanding = owned.Count > 0 ? owned[0].LandingBeat : int.MaxValue;

        // The run-in from track start is unconstrained — the wall keeps playing whatever it holds until the
        // first mark — so the first gap is measured from the opening downbeat and never forces a mark onto it.
        var lastMark = startBeat;

        for (var i = 0; i < candidates.Count; i++)
        {
            var boundary = candidates[i];
            if (boundary <= lastMark)
            {
                continue;
            }

            // Fit legality: the shortest card must fit here. The first mark only needs its Runway to clear
            // the opening downbeat; a later mark also needs to clear the previous mark's Tail.
            var gapBeats = boundary - lastMark;
            var fits = marks.Count == 0 ? gapBeats >= smallRunway : gapBeats >= smallRunway + smallTail + 1;
            if (!fits)
            {
                continue;
            }

            var next = i + 1 < candidates.Count ? candidates[i + 1] : int.MaxValue;
            var lastMarkGrid = boundaries.IndexOf(lastMark);
            var boundaryGrid = boundaries.IndexOf(boundary);
            var gapGrids = boundaryGrid - lastMarkGrid;

            // The last chance to cast the Effect that will be on the wall at the earliest Anchor: with no
            // mark yet and no further candidate before the landing, this boundary carries the moment.
            var mustCarry = marks.Count == 0 && boundary < earliestLanding && next > earliestLanding;

            // Candidates may skip actual Grids around Anchors, so take the last available boundary before
            // the next candidate would cross the four-Grid limit.
            var lastChance = next != int.MaxValue
                && boundaries.IndexOf(next) - lastMarkGrid > MaximumGapGrids;

            if (!mustCarry && !lastChance && !TakeBoundary(gapGrids, rng))
            {
                continue;
            }

            marks.Add(boundary);
            lastMark = boundary;
        }

        return marks;
    }

    /// <summary>
    /// Whether a candidate Grid Boundary becomes a Cue Mark, given the actual Grids behind it. At
    /// <see cref="MaximumGapGrids"/> the answer is always yes; below it the chance rises with the gap
    /// (<see cref="TakeChancePercent"/>). Small gaps are rare by judgment, not forbidden by floor.
    /// Consumes a roll only when the answer is open.
    /// </summary>
    /// <param name="gapGrids">Actual Grids between the last placed mark and this boundary.</param>
    /// <param name="rng">The roll stream to draw from.</param>
    private static bool TakeBoundary(int gapGrids, Rng rng)
    {
        if (gapGrids >= MaximumGapGrids)
        {
            return true;
        }

        return rng.Bounded(100) < TakeChancePercent[gapGrids - 1];
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
    /// Deals Effects and Transitions to the placed marks in beat order and records each owned Anchor's
    /// resolution. Every Transition is dealt to fit the space it is given — its Runway inside the free
    /// interval behind the mark, its Tail short of the next Anchor moment and of the next mark's smallest
    /// possible Runway — so no two blends overlap and no blend crosses a landing, by construction. The mark
    /// standing last before an owned landing is that Anchor's carrier: its Effect is dealt capable, because
    /// it is the Effect on the wall when the moment hits. Every other mark deals the bag's own order,
    /// unfiltered, so the plan shows the whole catalog before it repeats anything.
    /// </summary>
    private static TrackCueSheet Deal(
        List<int> markBeats,
        List<Anchor> owned,
        IReadOnlyList<EffectDescriptor> effects,
        IReadOnlyList<TransitionDescriptor> transitions,
        Bag effectBag,
        Bag transitionBag,
        int startBeat,
        int smallRunway,
        int structureGeneration,
        int playerNumber,
        int salt,
        IReadOnlyList<int> boundaries)
    {
        // Carrier map: each owned Anchor keys to the last mark before its landing. The walk guarantees one
        // exists. Adjacent Anchors can share a carrier; the one incumbent then rides every one of them.
        var carriers = new Dictionary<int, List<Anchor>>();
        foreach (var anchor in owned)
        {
            var carrier = -1;
            foreach (var beat in markBeats)
            {
                if (beat < anchor.LandingBeat && beat > carrier)
                {
                    carrier = beat;
                }
            }

            if (!carriers.TryGetValue(carrier, out var carried))
            {
                carried = new List<Anchor>();
                carriers[carrier] = carried;
            }

            carried.Add(anchor);
        }

        var marks = new List<CuePlanMark>(markBeats.Count);
        var resolutions = new List<AnchorResolution>(owned.Count);
        var previousBlendEnd = startBeat - 1;
        for (var i = 0; i < markBeats.Count; i++)
        {
            var beat = markBeats[i];
            var next = i + 1 < markBeats.Count ? markBeats[i + 1] : int.MaxValue;

            // The Runway may reach back to the end of the previous blend and never across a landing; the
            // Tail may reach forward short of the next landing and must leave the next mark room for the
            // catalog's smallest Runway. The walk's fit rules keep both budgets at least the shortest
            // card's size, so this deal always finds a card.
            var runwayBudget = beat - previousBlendEnd - 1;
            var tailBudget = next == int.MaxValue ? int.MaxValue : next - beat - 1 - smallRunway;
            foreach (var anchor in owned)
            {
                if (anchor.LandingBeat < beat)
                {
                    runwayBudget = Math.Min(runwayBudget, beat - anchor.LandingBeat - 1);
                }
                else if (anchor.LandingBeat > beat && anchor.LandingBeat < next)
                {
                    tailBudget = Math.Min(tailBudget, anchor.LandingBeat - beat - 1);
                }
            }

            var transitionIndex = transitionBag.DealCapable(
                card => transitions[card].Repertoire.RunwayBeats <= runwayBudget
                    && transitions[card].Repertoire.TailBeats <= tailBudget,
                out _);

            int effectIndex;
            if (carriers.TryGetValue(beat, out var carriedAnchors))
            {
                // One capable incumbent enters here and is on the wall for every Anchor keyed to this
                // carrier. It must satisfy all of their capabilities; if none does, fall back to the first
                // Anchor's need.
                var combined = Repertoire.None;
                foreach (var carried in carriedAnchors)
                {
                    combined |= carried.Capability;
                }

                effectIndex = effectBag.DealCapable(card => (effects[card].Repertoire & combined) == combined, out var any);
                if (!any)
                {
                    effectIndex = effectBag.DealCapable(
                        card => (effects[card].Repertoire & carriedAnchors[0].Capability) != 0, out _);
                }

                // An Anchor the dealt Effect cannot actually serve — a shared carrier needing both flags
                // from a catalog that holds no dual-capable card — is not reported owned. Its landing
                // stays cleared (the walk already withheld it), but no resolution claims capability.
                foreach (var carried in carriedAnchors)
                {
                    if ((effects[effectIndex].Repertoire & carried.Capability) != 0)
                    {
                        resolutions.Add(new AnchorResolution(carried.LandingBeat, carried.Kind, effectIndex));
                    }
                }
            }
            else
            {
                effectIndex = effectBag.DealTop();
            }

            marks.Add(new CuePlanMark(beat, effectIndex, transitionIndex));
            previousBlendEnd = beat + transitions[transitionIndex].Repertoire.TailBeats;
        }

        resolutions.Sort(static (a, b) => a.LandingBeat.CompareTo(b.LandingBeat));
        return new TrackCueSheet(marks, resolutions, effects, transitions, structureGeneration, playerNumber, salt, boundaries);
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

    /// <summary>A drop or fill window read from the Phrase map, before the plan decides it can own it.</summary>
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
    /// The Grid walk and both <see cref="Bag"/>s draw from this one stream, so the whole sheet is a
    /// byte-identical function of the seed pair.
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
        /// off-plan deal never disturbs the sheet's own roll, and distinct per ask, so the doorway asking again at
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
    }

    /// <summary>
    /// A seeded shuffled Bag over one catalog's indices, dealt top-down and reshuffled when empty so the
    /// whole catalog is shown before any card repeats. The card most recently handed out is remembered and
    /// kept away from the next deal wherever a choice exists — off the top of a fresh permutation, and
    /// skipped by a filtered scan when another match remains — because for the Effect catalog a back-to-back
    /// repeat deals a Transition from a card to itself, which restarts the Effect in place and moves
    /// nothing. Filtered deals that find no match in the remaining cards encore the least-recently-dealt
    /// matching card from the discard pile without consuming a card.
    /// </summary>
    private sealed class Bag
    {
        /// <summary>How many cards the catalog holds; every pass deals a permutation of exactly these.</summary>
        private readonly int cardCount;

        /// <summary>The sheet's roll stream, shared with the Grid walk.</summary>
        private readonly Rng rng;

        /// <summary>Cards not yet dealt in this pass, in the order they will come off the top.</summary>
        private readonly List<int> remaining;

        /// <summary>Cards already dealt in this pass, oldest first — the encore pile for filtered scans.</summary>
        private readonly List<int> discard;

        /// <summary>The card most recently handed out by any deal, including encores; -1 before the first.</summary>
        private int lastDealt = -1;

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
        /// Deals the first card matching <paramref name="capable"/>, preferring any match that is not the
        /// card just dealt: the remaining cards are scanned first, then the discard pile as an encore that
        /// consumes nothing, and only when the just-dealt card is the sole match anywhere is it repeated.
        /// <paramref name="any"/> is false only when the whole catalog holds no matching card.
        /// </summary>
        public int DealCapable(Func<int, bool> capable, out bool any)
        {
            EnsureCards();
            var card = ScanFor(capable, excluded: lastDealt);
            if (card < 0)
            {
                card = ScanFor(capable, excluded: -1);
            }

            any = card >= 0;
            return card;
        }

        /// <summary>
        /// One matching scan: the remaining cards first (a real deal), then the discard pile oldest-first
        /// (an encore), skipping <paramref name="excluded"/> in both. Returns -1 when nothing matches.
        /// </summary>
        private int ScanFor(Func<int, bool> capable, int excluded)
        {
            for (var i = 0; i < remaining.Count; i++)
            {
                if (capable(remaining[i]) && remaining[i] != excluded)
                {
                    return Take(i);
                }
            }

            for (var i = 0; i < discard.Count; i++)
            {
                if (capable(discard[i]) && discard[i] != excluded)
                {
                    return Encore(discard[i]);
                }
            }

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
            lastDealt = card;
            return card;
        }

        /// <summary>Hands out an already-discarded card again without consuming a card from the pass.</summary>
        private int Encore(int card)
        {
            lastDealt = card;
            return card;
        }

        /// <summary>
        /// Refills the Bag with a fresh Fisher-Yates permutation and clears the discard pile. The card just
        /// dealt is kept off the top of the new permutation, because the seam between two passes is the one
        /// place a fair bag can otherwise deal the same card twice running.
        /// </summary>
        private void Reshuffle()
        {
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
