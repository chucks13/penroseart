using System;
using System.Collections.Generic;

/// <summary>
/// One performer catalog entry as it enters the sheet builder: a stable catalog index paired with the
/// musical <see cref="Repertoire"/> that index advertises. The builder deals indices, never deck slots
/// or effect instances, so the plan it returns stays a pure value with no engine references.
/// </summary>
public readonly struct EffectDescriptor
{
    /// <summary>Captures one effect catalog entry.</summary>
    /// <param name="index">Stable catalog index this descriptor stands for.</param>
    /// <param name="repertoire">Musical capabilities and energy tags the index advertises.</param>
    public EffectDescriptor(int index, Repertoire repertoire)
    {
        Index = index;
        Repertoire = repertoire;
    }

    /// <summary>Stable catalog index the plan bakes into a Cue Mark.</summary>
    public int Index { get; }

    /// <summary>Musical capabilities and energy tags the index advertises.</summary>
    public Repertoire Repertoire { get; }
}

/// <summary>
/// One transition catalog entry as it enters the sheet builder: a stable catalog index paired with the
/// <see cref="TransitionRepertoire"/> timing/use contract that index advertises.
/// </summary>
public readonly struct TransitionDescriptor
{
    /// <summary>Captures one transition catalog entry.</summary>
    /// <param name="index">Stable catalog index this descriptor stands for.</param>
    /// <param name="repertoire">Timing and musical-use contract the index advertises.</param>
    public TransitionDescriptor(int index, TransitionRepertoire repertoire)
    {
        Index = index;
        Repertoire = repertoire;
    }

    /// <summary>Stable catalog index the plan bakes into a Cue Mark.</summary>
    public int Index { get; }

    /// <summary>Timing and musical-use contract the index advertises.</summary>
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
/// </summary>
public readonly struct CuePlanMark
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
/// it holds no notion of "now" and no engine references, so the Director can run it by position lookup and
/// the same load always rebuilds the identical sheet.
/// </summary>
/// <remarks>
/// This is the track-scoped "Cue Sheet" of the track-cue-sheets spec. It is a distinct type from the
/// phrase-scoped <see cref="CueSheet"/> (an index of empty marks over one Phrase) while both coexist; the
/// spec supersedes the phrase-scoped definition in a later hard cut.
///
/// The mark placement absorbs the phrase-scoped builder's Grid walk — interior marks on Grid Boundaries,
/// bounded 16-to-64-beat gaps, a mandatory Phrase-end mark, one run-out Grid absorbing an irregular tail —
/// and runs it per Phrase against absolute track beats. Every consecutive gap in <see cref="Marks"/> stays
/// within one to four Grids by construction, including across Anchor suppression.
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
    /// Minimum beats a drop-landing Effect holds before the next Cue Mark — a named knob, one Grid for now.
    /// No Cue Mark is placed within this window after a drop landing.
    /// </summary>
    public const int PostDropHoldBeats = GridBeats;

    private static readonly IReadOnlyList<CuePlanMark> NoMarks = Array.Empty<CuePlanMark>();
    private static readonly IReadOnlyList<AnchorResolution> NoAnchors = Array.Empty<AnchorResolution>();

    private TrackCueSheet(IReadOnlyList<CuePlanMark> marks, IReadOnlyList<AnchorResolution> anchors)
    {
        Marks = marks;
        Anchors = anchors;
    }

    /// <summary>Every placed Cue Mark, ascending by beat; the complete fire schedule the Director runs.</summary>
    public IReadOnlyList<CuePlanMark> Marks { get; }

    /// <summary>Every owned drop or fill Anchor, ascending by landing beat; how each protected moment is performed.</summary>
    public IReadOnlyList<AnchorResolution> Anchors { get; }

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
    /// <param name="effects">Effect catalog as descriptors (index + repertoire). Must be non-empty.</param>
    /// <param name="transitions">Transition catalog as descriptors (index + repertoire). Must be non-empty.</param>
    /// <param name="structureGeneration">The structure's generation — the first half of the seed.</param>
    /// <param name="playerNumber">The physical player number — the second half of the seed.</param>
    /// <returns>The complete track-scoped Cue Sheet.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="effects"/> or <paramref name="transitions"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="effects"/> or <paramref name="transitions"/> is empty.</exception>
    public static TrackCueSheet Build(
        StructureValues structure,
        IReadOnlyList<EffectDescriptor> effects,
        IReadOnlyList<TransitionDescriptor> transitions,
        int structureGeneration,
        int playerNumber)
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
            return new TrackCueSheet(NoMarks, NoAnchors);
        }

        var rng = new Rng(structureGeneration, playerNumber);
        var effectBag = new Bag(effects.Count, rng);
        var transitionBag = new Bag(transitions.Count, rng);

        var baseMarks = WalkTrack(phrases, rng);
        var anchors = CollectAnchors(phrases);
        var plan = ResolveAndDeal(phrases, baseMarks, anchors, effects, transitions, effectBag, transitionBag, rng);
        return plan;
    }

    /// <summary>
    /// Walks every Phrase in order, absorbing the phrase-scoped Grid walk against absolute track beats.
    /// Interior marks land on Grid Boundaries, one run-out Grid absorbs an irregular tail, and each Phrase's
    /// end carries a mark on the next Phrase's downbeat. Adjacent Phrases share that boundary beat, so the
    /// concatenated result holds one mark per boundary with every gap in one to four Grids.
    /// </summary>
    private static List<int> WalkTrack(IReadOnlyList<StructurePhraseValues> phrases, Rng rng)
    {
        var marks = new List<int>();
        foreach (var phrase in phrases)
        {
            WalkPhrase(phrase.StartBeat, PhraseLength(phrase), rng, marks);
        }

        return marks;
    }

    /// <summary>Appends one Phrase's Cue Marks (absolute beats) using the absorbed Grid walk.</summary>
    private static void WalkPhrase(int startBeat, int lengthBeats, Rng rng, List<int> marks)
    {
        if (lengthBeats <= 0)
        {
            return;
        }

        var tailBeats = lengthBeats % GridBeats;
        var walkBeats = lengthBeats;
        if (tailBeats != 0)
        {
            var interiorBeats = lengthBeats - tailBeats;
            var gridsAvailable = interiorBeats / GridBeats;
            if (gridsAvailable == 0)
            {
                // Shorter than one Grid: only the mandatory end mark, with an unavoidably short run-in.
                marks.Add(startBeat + lengthBeats);
                return;
            }

            var maxRunOutGrids = (MaximumGapBeats - tailBeats) / GridBeats;
            var runOutCap = maxRunOutGrids < gridsAvailable ? maxRunOutGrids : gridsAvailable;
            var runOutGrids = 1 + rng.Bounded(runOutCap);
            walkBeats = interiorBeats - runOutGrids * GridBeats;
        }

        var offset = 0;
        while (offset < walkBeats)
        {
            var gridsRemaining = (walkBeats - offset) / GridBeats;
            var maxGapGrids = gridsRemaining < MaximumGapBeats / GridBeats ? gridsRemaining : MaximumGapBeats / GridBeats;
            var gapGrids = 1 + rng.Bounded(maxGapGrids);
            offset += gapGrids * GridBeats;
            marks.Add(startBeat + offset);
        }

        if (tailBeats != 0)
        {
            marks.Add(startBeat + lengthBeats);
        }
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
    /// </summary>
    private static TrackCueSheet ResolveAndDeal(
        IReadOnlyList<StructurePhraseValues> phrases,
        List<int> baseMarks,
        List<Anchor> anchors,
        IReadOnlyList<EffectDescriptor> effects,
        IReadOnlyList<TransitionDescriptor> transitions,
        Bag effectBag,
        Bag transitionBag,
        Rng rng)
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
            // One flip per Anchor, always consumed, so the roll stream never depends on catalog contents.
            var prefersRideThrough = rng.Flip();

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

        var phraseStarts = PhraseStarts(phrases);
        var marks = new List<CuePlanMark>();
        foreach (var beat in baseMarks)
        {
            if (suppressed.Contains(beat))
            {
                continue;
            }

            var energyFlag = EnergyFlagFor(PhraseTypeCovering(phrases, phraseStarts, beat));

            if (performedMarks.TryGetValue(beat, out var performed))
            {
                var transitionIndex = transitionBag.DealCapable(i => IsTransitionCapable(transitions, i, performed.Capability), out _);
                var effectIndex = DealEffect(effectBag, effects, energyFlag);
                marks.Add(new CuePlanMark(beat, effects[effectIndex].Index, transitions[transitionIndex].Index));
                resolutions[performed.LandingBeat] = new AnchorResolution(
                    performed.LandingBeat, performed.Kind, AnchorTreatment.PerformedTransition, transitions[transitionIndex].Index);
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
                marks.Add(new CuePlanMark(beat, effects[effectIndex].Index, transitions[transitionIndex].Index));
                foreach (var carried in carriedAnchors)
                {
                    resolutions[carried.LandingBeat] = new AnchorResolution(
                        carried.LandingBeat, carried.Kind, AnchorTreatment.RideThrough, effects[effectIndex].Index);
                }

                continue;
            }

            var regularEffect = DealEffect(effectBag, effects, energyFlag);
            var regularTransition = transitionBag.DealTop();
            marks.Add(new CuePlanMark(beat, effects[regularEffect].Index, transitions[regularTransition].Index));
        }

        var anchorList = new AnchorResolution[resolutions.Count];
        resolutions.Values.CopyTo(anchorList, 0);
        return new TrackCueSheet(marks, anchorList);
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

    /// <summary>Deals one Effect with a soft energy-fit scan: the first energy-matching card, else the top card.</summary>
    private static int DealEffect(Bag effectBag, IReadOnlyList<EffectDescriptor> effects, Repertoire energyFlag)
    {
        return effectBag.DealPreferred(i => (effects[i].Repertoire & energyFlag) != 0);
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

    private static int[] PhraseStarts(IReadOnlyList<StructurePhraseValues> phrases)
    {
        var starts = new int[phrases.Count];
        for (var i = 0; i < phrases.Count; i++)
        {
            starts[i] = phrases[i].StartBeat;
        }

        return starts;
    }

    /// <summary>The Phrase kind covering an absolute beat: the last Phrase whose start is at or below it.</summary>
    private static PhraseType PhraseTypeCovering(IReadOnlyList<StructurePhraseValues> phrases, int[] phraseStarts, int beat)
    {
        var covering = 0;
        for (var i = 0; i < phraseStarts.Length; i++)
        {
            if (phraseStarts[i] <= beat)
            {
                covering = i;
            }
            else
            {
                break;
            }
        }

        return phrases[covering].Type;
    }

    /// <summary>
    /// The soft energy class a Phrase kind prefers: Drop/Chorus lean High, Up/Verse lean Mid, and the
    /// quieter and unknown kinds lean Low. A tunable mapping, deliberately coarse.
    /// </summary>
    private static Repertoire EnergyFlagFor(PhraseType type)
    {
        return type switch
        {
            PhraseType.Drop => Repertoire.EnergyHigh,
            PhraseType.Chorus => Repertoire.EnergyHigh,
            PhraseType.Up => Repertoire.EnergyMid,
            PhraseType.Verse => Repertoire.EnergyMid,
            _ => Repertoire.EnergyLow,
        };
    }

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

    private static bool IsEffectCapable(IReadOnlyList<EffectDescriptor> effects, int index, Repertoire capability)
    {
        return (effects[index].Repertoire & capability) != 0;
    }

    private static bool IsTransitionCapable(IReadOnlyList<TransitionDescriptor> transitions, int index, Repertoire capability)
    {
        return (transitions[index].Repertoire.Tags & capability) != 0;
    }

    /// <summary>A drop or fill window read from the Phrase map, before a treatment is chosen for it.</summary>
    private readonly struct Anchor
    {
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
        private uint state;

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

        /// <summary>A single fair coin flip.</summary>
        public bool Flip()
        {
            return (Next() & 1u) == 0u;
        }
    }

    /// <summary>
    /// A seeded shuffled Bag over one catalog's indices, dealt top-down and reshuffled when empty so the
    /// whole catalog is shown before any card repeats. Anchors scan the Bag for a capable card and encore
    /// the least-recently-dealt capable card from the discard pile only when the remaining cards have none.
    /// </summary>
    private sealed class Bag
    {
        private readonly int cardCount;
        private readonly Rng rng;
        private readonly List<int> remaining;
        private readonly List<int> discard;

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
        /// Deals the first card matching <paramref name="preferred"/>, else the top card. Used for the soft
        /// energy-fit scan, where no match simply falls back to the top card and never encores.
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

        private void EnsureCards()
        {
            if (remaining.Count == 0)
            {
                Reshuffle();
            }
        }

        private int Take(int index)
        {
            var card = remaining[index];
            remaining.RemoveAt(index);
            discard.Add(card);
            return card;
        }

        /// <summary>Refills the Bag with a fresh Fisher-Yates permutation and clears the discard pile.</summary>
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

            discard.Clear();
        }
    }
}
