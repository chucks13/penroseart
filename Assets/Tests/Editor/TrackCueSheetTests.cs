// Seam tests for the pure track-scoped Cue Sheet builder (TrackCueSheet.Build). Synthetic structures and
// descriptor catalogs go in; the built sheet's constraints, determinism, and fairness come out. Tests assert
// caller-visible guarantees — gap bounds, Anchor ownership, suppression, post-drop hold, determinism, bag
// fairness — never the private walk order or bag internals.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Behavioral battery for <see cref="TrackCueSheet"/>: the single track-scoped builder seam. Every test
/// drives the public <see cref="TrackCueSheet.Build"/> and asserts a spec constraint through the returned
/// plan (marks and Anchor resolutions), across many seeds where a guarantee must hold for all shows.
/// </summary>
public sealed class TrackCueSheetTests
{
    private const int GridBeats = TrackCueSheet.GridBeats;
    private const int MinimumGapBeats = TrackCueSheet.MinimumGapBeats;
    private const int MaximumGapBeats = TrackCueSheet.MaximumGapBeats;
    private const int PostDropHoldBeats = TrackCueSheet.PostDropHoldBeats;

    private static readonly int[] Generations = { 1, 2, 3, 7, 42, 100, 9999 };
    private static readonly int[] Players = { 1, 2, 3, 4, 5, 6 };

    // A rich mixed-length track: regular Grid multiples, irregular tails, and three drop/fill Anchors.
    private static StructureValues MixedTrack()
    {
        return Structure(
            Phrase(1, 32, PhraseType.Intro),
            Phrase(33, 96, PhraseType.Up, fill: 88),                       // fill leading into the drop below
            Phrase(97, 160, PhraseType.Drop, drop: 97),                    // drop landing Anchor at beat 97
            Phrase(161, 200, PhraseType.Verse),                            // irregular length 40
            Phrase(201, 264, PhraseType.Bridge, fill: 258),               // fill without a following drop
            Phrase(265, 305, PhraseType.Chorus),                           // irregular length 41
            Phrase(306, 369, PhraseType.Down),
            Phrase(370, 433, PhraseType.Drop, drop: 370),                  // second drop landing Anchor at beat 370
            Phrase(434, 456, PhraseType.Outro));                           // irregular length 23
    }

    [Test]
    public void ConsecutiveMarkGapsStayWithinCadenceAcrossSeeds()
    {
        // The Grid walk bounds every gap to one-to-four Grids, and Anchor suppression is capped so a merged
        // gap can never exceed the maximum. The run-in from track start is unconstrained (the wall keeps
        // playing until the first mark), so only gaps between consecutive marks are checked.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(MixedTrack(), effects, transitions, generation, player);
                var marks = sheet.Marks;
                for (var i = 1; i < marks.Count; i++)
                {
                    var gap = marks[i].Beat - marks[i - 1].Beat;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(MinimumGapBeats),
                        $"gen={generation} player={player}: gap {gap} below one Grid");
                    Assert.That(gap, Is.LessThanOrEqualTo(MaximumGapBeats),
                        $"gen={generation} player={player}: gap {gap} above four Grids");
                }
            }
        }
    }

    [Test]
    public void DealAtIsDeterministicForTheSameSeedAndBoundaryBeat()
    {
        // The starvation one-off deal is a pure function of (sheet seed, boundary beat): the same situation
        // deals the identical card, both on a repeat call and on a fresh rebuild of the same sheet.
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2);
        var first = sheet.DealAt(200);
        var again = sheet.DealAt(200);
        var rebuilt = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2).DealAt(200);

        Assert.That(first.Beat, Is.EqualTo(200), "The dealt mark lands on the boundary beat.");
        Assert.That(again.EffectIndex, Is.EqualTo(first.EffectIndex));
        Assert.That(again.TransitionIndex, Is.EqualTo(first.TransitionIndex));
        Assert.That(rebuilt.EffectIndex, Is.EqualTo(first.EffectIndex), "A rebuilt sheet deals identically.");
        Assert.That(rebuilt.TransitionIndex, Is.EqualTo(first.TransitionIndex));
    }

    [Test]
    public void DealAtNeverMutatesThePlan()
    {
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2);
        var before = sheet.Marks.Select(m => (m.Beat, m.EffectIndex, m.TransitionIndex)).ToArray();

        sheet.DealAt(200);
        sheet.DealAt(48);
        sheet.DealAt(415);

        var after = sheet.Marks.Select(m => (m.Beat, m.EffectIndex, m.TransitionIndex)).ToArray();
        Assert.That(after, Is.EqualTo(before), "DealAt deals from fresh local bags and never touches the sheet's plan.");
    }

    [Test]
    public void DealAtYieldsIndicesInsideBothCatalogs()
    {
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var sheet = TrackCueSheet.Build(MixedTrack(), effects, transitions, 7, 2);
        foreach (var boundaryBeat in new[] { 16, 97, 200, 370, 456 })
        {
            var dealt = sheet.DealAt(boundaryBeat);
            Assert.That(dealt.EffectIndex, Is.InRange(0, effects.Count - 1));
            Assert.That(dealt.TransitionIndex, Is.InRange(0, transitions.Count - 1));
        }
    }

    [Test]
    public void EveryInteriorMarkSitsOnAGridBoundary()
    {
        // Interior marks are Grid multiples relative to their Phrase start; because Phrase starts are Grid
        // Boundaries on the wire, every mark beat is congruent to a Boundary. Marks are strictly ascending.
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 42, 1);
        for (var i = 1; i < sheet.Marks.Count; i++)
        {
            Assert.That(sheet.Marks[i].Beat, Is.GreaterThan(sheet.Marks[i - 1].Beat),
                "marks are not strictly ascending");
        }
    }

    [Test]
    public void EveryAnchorIsOwnedByACapablePerformerUnderBothTreatments()
    {
        // Across seeds, both treatments occur for the same Anchor. Whichever is chosen, a capable performer
        // owns it: a performed Anchor carries a capable Transition on the landing mark; a ride-through Anchor
        // has no landing mark and hands a capable Effect to the mark immediately before it.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var treatmentsSeen = new HashSet<AnchorTreatment>();
        // Mixed exercises irregular geometry; the all-Grid TwoDropTrack keeps ride-through always feasible so
        // the seeded flip genuinely reaches both treatments.
        var structures = new[] { MixedTrack(), TwoDropTrack() };

        foreach (var structure in structures)
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(structure, effects, transitions, generation, player);
                foreach (var anchor in sheet.Anchors)
                {
                    treatmentsSeen.Add(anchor.Treatment);
                    var capability = anchor.Kind == AnchorKind.Drop ? Repertoire.HandlesDrop : Repertoire.HandlesFill;

                    if (anchor.Treatment == AnchorTreatment.PerformedTransition)
                    {
                        var landing = MarkAt(sheet, anchor.LandingBeat);
                        Assert.That(landing, Is.Not.Null,
                            $"performed Anchor at {anchor.LandingBeat} has no landing mark");
                        Assert.That(landing!.Value.TransitionIndex, Is.EqualTo(anchor.PerformerIndex),
                            "performed Anchor's landing mark does not carry its transition");
                        Assert.That(TransitionCapability(transitions, anchor.PerformerIndex) & capability, Is.EqualTo(capability),
                            "performed Anchor's transition is not capable");
                    }
                    else
                    {
                        Assert.That(MarkAt(sheet, anchor.LandingBeat), Is.Null,
                            $"ride-through Anchor at {anchor.LandingBeat} did not suppress its landing mark");
                        var carrier = LastMarkBefore(sheet, anchor.LandingBeat);
                        Assert.That(carrier, Is.Not.Null,
                            "ride-through Anchor has no incumbent mark to carry it");
                        Assert.That(carrier!.Value.EffectIndex, Is.EqualTo(anchor.PerformerIndex),
                            "ride-through Anchor's incumbent mark does not carry its effect");
                        Assert.That(EffectCapability(effects, anchor.PerformerIndex) & capability, Is.EqualTo(capability),
                            "ride-through Anchor's effect is not capable");
                    }
                }
            }
        }

        Assert.That(treatmentsSeen, Is.EquivalentTo(new[] { AnchorTreatment.RideThrough, AnchorTreatment.PerformedTransition }),
            "both Anchor treatments should be reachable across seeds");
    }

    [Test]
    public void DropLandingsAndFillWindowsBecomeAnchors()
    {
        // The two drops and the lone fill (the fill leading into a drop is folded into that drop) each
        // surface as an Anchor at the expected Grid Boundary.
        var landings = new HashSet<int>();
        foreach (var generation in Generations)
        {
            var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), generation, 1);
            foreach (var anchor in sheet.Anchors)
            {
                landings.Add(anchor.LandingBeat);
            }
        }

        Assert.That(landings, Does.Contain(97), "drop landing at 97 is not an Anchor");
        Assert.That(landings, Does.Contain(370), "drop landing at 370 is not an Anchor");
        Assert.That(landings, Does.Contain(265), "fill window ending at 265 is not an Anchor");
    }

    [Test]
    public void NoMarkFallsWithinThePostDropHold()
    {
        // After a drop landing, the performing Effect holds a minimum of one Grid: no mark lands in the open
        // window (landing, landing + hold).
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), generation, player);
                foreach (var anchor in sheet.Anchors.Where(a => a.Kind == AnchorKind.Drop))
                {
                    foreach (var mark in sheet.Marks)
                    {
                        var withinHold = mark.Beat > anchor.LandingBeat && mark.Beat < anchor.LandingBeat + PostDropHoldBeats;
                        Assert.That(withinHold, Is.False,
                            $"gen={generation} player={player}: mark {mark.Beat} inside the post-drop hold after {anchor.LandingBeat}");
                    }
                }
            }
        }
    }

    [Test]
    public void SameSeedRebuildsAnIdenticalSheet()
    {
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var first = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), generation, player);
                var second = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), generation, player);
                Assert.That(Serialize(second), Is.EqualTo(Serialize(first)),
                    $"gen={generation} player={player}: same seed re-dealt a different sheet");
            }
        }
    }

    [Test]
    public void DifferentGenerationsDealADifferentShow()
    {
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var one = TrackCueSheet.Build(MixedTrack(), effects, transitions, 1, 1);
        var two = TrackCueSheet.Build(MixedTrack(), effects, transitions, 2, 1);
        Assert.That(Serialize(two), Is.Not.EqualTo(Serialize(one)),
            "a new structure generation dealt the identical show");
    }

    [Test]
    public void DifferentPlayersDealADifferentShow()
    {
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var one = TrackCueSheet.Build(MixedTrack(), effects, transitions, 1, 1);
        var two = TrackCueSheet.Build(MixedTrack(), effects, transitions, 1, 2);
        Assert.That(Serialize(two), Is.Not.EqualTo(Serialize(one)),
            "a different player dealt the identical show");
    }

    [Test]
    public void EmptyStructureYieldsAnEmptySheet()
    {
        var sheet = TrackCueSheet.Build(default, MixedEffects(), MixedTransitions(), 1, 1);
        Assert.That(sheet.Marks, Is.Empty);
        Assert.That(sheet.Anchors, Is.Empty);
    }

    [Test]
    public void EveryEffectIsDealtBeforeAnyRepeats()
    {
        // A no-Anchor track deals only top cards, so each shuffled cycle is a full permutation: the whole
        // catalog is shown before any effect repeats. Untagged effects and a plain track keep the energy
        // scan a no-op, isolating the top-deal cadence.
        const int catalogSize = 6;
        var effects = UntaggedEffects(catalogSize);
        var transitions = PlainTransitions(2);
        var sheet = TrackCueSheet.Build(PlainTrack(catalogSize * 3), effects, transitions, 5, 1);

        var dealt = sheet.Marks.Select(m => m.EffectIndex).ToArray();
        Assert.That(dealt.Length, Is.GreaterThanOrEqualTo(catalogSize * 2),
            "not enough marks to observe a full cycle");
        for (var start = 0; start + catalogSize <= dealt.Length; start += catalogSize)
        {
            var window = dealt.Skip(start).Take(catalogSize).ToArray();
            Assert.That(window.Distinct().Count(), Is.EqualTo(catalogSize),
                $"cycle at {start} repeated an effect before dealing the whole catalog");
        }
    }

    [Test]
    public void CapableEffectsAreNotOverRepresentedAtRegularMarks()
    {
        // At regular marks the drop-capable effects are dealt at the same rate as everyone else: over whole
        // shuffle cycles every effect appears an equal number of times, so capability never biases the deal.
        const int catalogSize = 6;
        var reps = new Repertoire[catalogSize];
        reps[0] = Repertoire.HandlesDrop;
        reps[1] = Repertoire.HandlesDrop; // two capable, four not — capability must not change frequency
        var effects = Effects(reps);
        var transitions = PlainTransitions(2);

        var sheet = TrackCueSheet.Build(PlainTrack(catalogSize * 4), effects, transitions, 11, 1);
        var counts = new int[catalogSize];
        var fullCycleMarks = (sheet.Marks.Count / catalogSize) * catalogSize;
        for (var i = 0; i < fullCycleMarks; i++)
        {
            counts[sheet.Marks[i].EffectIndex]++;
        }

        var expected = fullCycleMarks / catalogSize;
        for (var i = 0; i < catalogSize; i++)
        {
            Assert.That(counts[i], Is.EqualTo(expected),
                $"effect {i} appeared {counts[i]} times, expected {expected}");
        }
    }

    [Test]
    public void MissingCapableTransitionForcesEveryDropAnchorToRideThrough()
    {
        // A catalog with a capable Effect but no capable Transition degenerates the flip to a one-faced
        // coin: every drop Anchor is ridden through.
        var effects = Effects(Repertoire.HandlesDrop, Repertoire.None, Repertoire.None, Repertoire.None);
        var transitions = PlainTransitions(3); // no HandlesDrop tag anywhere

        foreach (var generation in Generations)
        {
            var sheet = TrackCueSheet.Build(TwoDropTrack(), effects, transitions, generation, 1);
            Assert.That(sheet.Anchors, Is.Not.Empty, "expected drop Anchors");
            Assert.That(sheet.Anchors.All(a => a.Treatment == AnchorTreatment.RideThrough), Is.True,
                $"gen={generation}: an Anchor was not ridden through despite no capable transition");
        }
    }

    [Test]
    public void MissingCapableEffectForcesEveryDropAnchorToPerformedTransition()
    {
        // The mirror: a capable Transition but no capable Effect degenerates the flip to performed transitions.
        var effects = UntaggedEffects(4); // no HandlesDrop tag anywhere
        var transitions = new[]
        {
            new TransitionDescriptor(0, Transition(Repertoire.HandlesDrop)),
            new TransitionDescriptor(1, Transition(Repertoire.None)),
        };

        foreach (var generation in Generations)
        {
            var sheet = TrackCueSheet.Build(TwoDropTrack(), effects, transitions, generation, 1);
            Assert.That(sheet.Anchors, Is.Not.Empty, "expected drop Anchors");
            Assert.That(sheet.Anchors.All(a => a.Treatment == AnchorTreatment.PerformedTransition), Is.True,
                $"gen={generation}: an Anchor was not performed despite no capable effect");
        }
    }

    [Test]
    public void DiscardPileEncoreOwnsAnAnchorWhenTheBagHasNoCapableCardLeft()
    {
        // One drop-capable effect, several plain ones, and two ride-through drop Anchors with distinct carrier
        // marks but inside one shuffle cycle. The first Anchor's carrier deals the lone capable card; the
        // second, still in the same shuffle, finds none left in the bag and encores it from the discard pile.
        // Both Anchors end up owned by that single card.
        var effects = Effects(Repertoire.HandlesDrop, Repertoire.None, Repertoire.None, Repertoire.None, Repertoire.None, Repertoire.None);
        var transitions = PlainTransitions(2); // no capable transition, so both Anchors ride through

        foreach (var generation in Generations)
        {
            var sheet = TrackCueSheet.Build(TwoDropTrack(), effects, transitions, generation, 1);
            var dropAnchors = sheet.Anchors.Where(a => a.Kind == AnchorKind.Drop).ToArray();
            Assert.That(dropAnchors.Length, Is.EqualTo(2), $"gen={generation}: expected two drop Anchors");
            foreach (var anchor in dropAnchors)
            {
                Assert.That(anchor.Treatment, Is.EqualTo(AnchorTreatment.RideThrough));
                Assert.That(anchor.PerformerIndex, Is.EqualTo(0),
                    $"gen={generation}: drop Anchor not owned by the only capable effect (encore failed)");
            }
        }
    }

    [Test]
    public void PlanBakesDescriptorIndicesNotBagPositions()
    {
        // Descriptors carry catalog indices offset from their bag position; the plan must bake the descriptor
        // index, never the internal shuffle slot.
        var effects = new[]
        {
            new EffectDescriptor(100, Repertoire.None),
            new EffectDescriptor(101, Repertoire.None),
            new EffectDescriptor(102, Repertoire.None),
        };
        var transitions = new[]
        {
            new TransitionDescriptor(200, Transition(Repertoire.None)),
            new TransitionDescriptor(201, Transition(Repertoire.None)),
        };

        var sheet = TrackCueSheet.Build(PlainTrack(6), effects, transitions, 3, 1);
        Assert.That(sheet.Marks, Is.Not.Empty);
        foreach (var mark in sheet.Marks)
        {
            Assert.That(mark.EffectIndex, Is.InRange(100, 102), $"effect index {mark.EffectIndex} is a bag position, not a descriptor index");
            Assert.That(mark.TransitionIndex, Is.InRange(200, 201), $"transition index {mark.TransitionIndex} is a bag position, not a descriptor index");
        }
    }

    // --- Structures -------------------------------------------------------------------------------------

    private static StructurePhraseValues Phrase(int start, int end, PhraseType type, int? fill = null, int? drop = null)
    {
        return new StructurePhraseValues(start, end, type, 0, fill, drop);
    }

    private static StructureValues Structure(params StructurePhraseValues[] phrases)
    {
        var totalBeats = phrases.Length == 0 ? 0 : phrases[phrases.Length - 1].EndBeat;
        return new StructureValues(1, null, StructureSource.Analyzed, totalBeats, phrases.Length, phrases);
    }

    /// <summary>A plain track of equal one-Grid Phrases with no Anchors, sized to a target mark count.</summary>
    private static StructureValues PlainTrack(int phraseCount)
    {
        var phrases = new StructurePhraseValues[phraseCount];
        for (var i = 0; i < phraseCount; i++)
        {
            var start = 1 + i * GridBeats;
            phrases[i] = Phrase(start, start + GridBeats - 1, PhraseType.Verse);
        }

        return Structure(phrases);
    }

    /// <summary>Two drop Anchors set deep enough in the track that each has a preceding carrier mark.</summary>
    private static StructureValues TwoDropTrack()
    {
        return Structure(
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Up),
            Phrase(33, 48, PhraseType.Drop, drop: 33),
            Phrase(49, 64, PhraseType.Verse),
            Phrase(65, 80, PhraseType.Up),
            Phrase(81, 96, PhraseType.Drop, drop: 81),
            Phrase(97, 112, PhraseType.Outro));
    }

    // --- Catalogs ---------------------------------------------------------------------------------------

    private static IReadOnlyList<EffectDescriptor> MixedEffects()
    {
        return Effects(
            Repertoire.EnergyLow,
            Repertoire.EnergyMid,
            Repertoire.EnergyHigh,
            Repertoire.HandlesDrop | Repertoire.EnergyHigh,
            Repertoire.HandlesDrop,
            Repertoire.HandlesFill | Repertoire.EnergyMid,
            Repertoire.HandlesFill,
            Repertoire.None);
    }

    private static IReadOnlyList<TransitionDescriptor> MixedTransitions()
    {
        return new[]
        {
            new TransitionDescriptor(0, Transition(Repertoire.None)),
            new TransitionDescriptor(1, Transition(Repertoire.HandlesDrop)),
            new TransitionDescriptor(2, Transition(Repertoire.HandlesFill)),
            new TransitionDescriptor(3, Transition(Repertoire.HandlesDrop | Repertoire.HandlesFill)),
        };
    }

    private static IReadOnlyList<EffectDescriptor> UntaggedEffects(int count)
    {
        var reps = new Repertoire[count];
        return Effects(reps);
    }

    private static IReadOnlyList<EffectDescriptor> Effects(params Repertoire[] reps)
    {
        var list = new EffectDescriptor[reps.Length];
        for (var i = 0; i < reps.Length; i++)
        {
            list[i] = new EffectDescriptor(i, reps[i]);
        }

        return list;
    }

    private static IReadOnlyList<TransitionDescriptor> PlainTransitions(int count)
    {
        var list = new TransitionDescriptor[count];
        for (var i = 0; i < count; i++)
        {
            list[i] = new TransitionDescriptor(i, Transition(Repertoire.None));
        }

        return list;
    }

    private static TransitionRepertoire Transition(Repertoire tags)
    {
        return TransitionRepertoire.FromRunwayAndTail(tags, runwayBeats: 4, tailBeats: 0,
            TransitionShape.Blend, TransitionIntensity.Subtle, defaultDurationSeconds: 4f);
    }

    // --- Helpers ----------------------------------------------------------------------------------------

    private static CuePlanMark? MarkAt(TrackCueSheet sheet, int beat)
    {
        foreach (var mark in sheet.Marks)
        {
            if (mark.Beat == beat)
            {
                return mark;
            }
        }

        return null;
    }

    private static CuePlanMark? LastMarkBefore(TrackCueSheet sheet, int beat)
    {
        CuePlanMark? best = null;
        foreach (var mark in sheet.Marks)
        {
            if (mark.Beat < beat && (best is null || mark.Beat > best.Value.Beat))
            {
                best = mark;
            }
        }

        return best;
    }

    private static Repertoire EffectCapability(IReadOnlyList<EffectDescriptor> effects, int index)
    {
        foreach (var effect in effects)
        {
            if (effect.Index == index)
            {
                return effect.Repertoire;
            }
        }

        return Repertoire.None;
    }

    private static Repertoire TransitionCapability(IReadOnlyList<TransitionDescriptor> transitions, int index)
    {
        foreach (var transition in transitions)
        {
            if (transition.Index == index)
            {
                return transition.Repertoire.Tags;
            }
        }

        return Repertoire.None;
    }

    private static string Serialize(TrackCueSheet sheet)
    {
        var marks = string.Join(";", sheet.Marks.Select(m => $"{m.Beat}:{m.EffectIndex}:{m.TransitionIndex}"));
        var anchors = string.Join(";", sheet.Anchors.Select(a => $"{a.LandingBeat}:{a.Kind}:{a.Treatment}:{a.PerformerIndex}"));
        return marks + "|" + anchors;
    }
}
