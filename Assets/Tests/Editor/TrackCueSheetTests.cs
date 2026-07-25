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
    public void DealOffPlanCueAtIsDeterministicForTheSameSeedBoundaryAndAsk()
    {
        // The off-plan deal is a pure function of (sheet seed, boundary beat, ask): the same situation deals the
        // identical card, both on a repeat call and on a fresh rebuild of the same sheet.
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2);
        var first = sheet.DealOffPlanCueAt(200, gapGrids: 1, ask: 1, onWallEffectIndex: -1);
        var again = sheet.DealOffPlanCueAt(200, gapGrids: 1, ask: 1, onWallEffectIndex: -1);
        var rebuilt = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2)
            .DealOffPlanCueAt(200, gapGrids: 1, ask: 1, onWallEffectIndex: -1);

        Assert.That(again, Is.EqualTo(first));
        Assert.That(rebuilt, Is.EqualTo(first), "A rebuilt sheet deals identically.");
    }

    [Test]
    public void AnOffPlanDealIsAlwaysTakenAtTheCeiling()
    {
        // The whole 64-beat rule reduces to this: however the earlier boundaries fell, the deal at the fourth
        // is certain, so the wall cannot hold still past MaximumGapBeats.
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2);
        for (var ask = 1; ask <= 256; ask++)
        {
            Assert.That(sheet.DealOffPlanCueAt(81, TrackCueSheet.MaximumGapGrids, ask, -1).Take, Is.True,
                $"ask {ask} would hold the wall past {TrackCueSheet.MaximumGapBeats} beats");
            Assert.That(sheet.DealOffPlanCueAt(81, TrackCueSheet.MaximumGapGrids + 1, ask, -1).Take, Is.True,
                "An overshot count must not wrap back into riding.");
        }
    }

    [Test]
    public void BelowTheCeilingAnOffPlanDealBothRidesAndIsTaken()
    {
        // Below the ceiling the choice is real, which is what spreads changes over one to four Grids instead of
        // pinning every one of them to the fourth boundary.
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2);
        for (var boundaries = 1; boundaries < TrackCueSheet.MaximumGapGrids; boundaries++)
        {
            var taken = 0;
            var ridden = 0;
            for (var ask = 1; ask <= 256; ask++)
            {
                if (sheet.DealOffPlanCueAt(81, boundaries, ask, -1).Take) { taken++; } else { ridden++; }
            }

            Assert.That(taken, Is.GreaterThan(0), $"boundary {boundaries} never changes the wall");
            Assert.That(ridden, Is.GreaterThan(0), $"boundary {boundaries} always changes the wall");
        }
    }

    [Test]
    public void OffPlanCardsCoverTheWholeCatalogAcrossAsks()
    {
        // The live defect behind this: a rolling loop was dealt the same card every pass, so the second cue
        // transitioned the on-air Effect to itself and moved nothing. Consecutive asks can still repeat a card
        // by chance — excluding what is already on the wall is the Director's job, not the deal's — but the
        // deal must draw from the whole catalog rather than collapsing onto one card.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var sheet = TrackCueSheet.Build(MixedTrack(), effects, transitions, 7, 2);
        var dealtEffects = new HashSet<int>();
        var dealtTransitions = new HashSet<int>();
        for (var ask = 1; ask <= 256; ask++)
        {
            var dealt = sheet.DealOffPlanCueAt(81, gapGrids: 1, ask: ask, onWallEffectIndex: -1);
            dealtEffects.Add(dealt.EffectIndex);
            dealtTransitions.Add(dealt.TransitionIndex);
        }

        Assert.That(dealtEffects, Is.EquivalentTo(effects.Select(e => e.Index)));
        Assert.That(dealtTransitions, Is.EquivalentTo(transitions.Select(t => t.Index)));
    }

    [Test]
    public void AnOffPlanDealNeverHandsBackWhatIsAlreadyOnTheWall()
    {
        // The freeze this closes: the ceiling fired on time and dealt the Effect already showing, so the wall
        // transitioned to itself and the 64-beat rule was met on paper while nothing moved.
        var effects = MixedEffects();
        var sheet = TrackCueSheet.Build(MixedTrack(), effects, MixedTransitions(), 7, 2);
        foreach (var descriptor in effects)
        {
            for (var ask = 1; ask <= 64; ask++)
            {
                var dealt = sheet.DealOffPlanCueAt(81, TrackCueSheet.MaximumGapGrids, ask, descriptor.Index);
                Assert.That(dealt.EffectIndex, Is.Not.EqualTo(descriptor.Index),
                    $"ask {ask} would transition Effect {descriptor.Index} to itself.");
            }
        }
    }

    [Test]
    public void TheOffPlanCardDoesNotDependOnHowLongTheWallHasHeld()
    {
        // Only whether the deal is taken reads the count; the card itself is drawn first, so one boundary's card
        // is the same whether it is the first ask after a change or the last before the ceiling.
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2);
        var atFirstBoundary = sheet.DealOffPlanCueAt(97, gapGrids: 1, ask: 3, onWallEffectIndex: -1);
        for (var boundaries = 2; boundaries <= TrackCueSheet.MaximumGapGrids; boundaries++)
        {
            var later = sheet.DealOffPlanCueAt(97, boundaries, ask: 3, onWallEffectIndex: -1);
            Assert.That(
                (later.EffectIndex, later.TransitionIndex),
                Is.EqualTo((atFirstBoundary.EffectIndex, atFirstBoundary.TransitionIndex)));
        }
    }

    [Test]
    public void DealOffPlanCueAtNeverMutatesThePlan()
    {
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 7, 2);
        var before = sheet.Marks.Select(m => (m.Beat, m.EffectIndex, m.TransitionIndex)).ToArray();

        sheet.DealOffPlanCueAt(200, gapGrids: 1, ask: 1, onWallEffectIndex: -1);
        sheet.DealOffPlanCueAt(48, gapGrids: 2, ask: 2, onWallEffectIndex: -1);
        sheet.DealOffPlanCueAt(415, gapGrids: 4, ask: 3, onWallEffectIndex: -1);

        var after = sheet.Marks.Select(m => (m.Beat, m.EffectIndex, m.TransitionIndex)).ToArray();
        Assert.That(after, Is.EqualTo(before), "The deal comes from fresh local bags and never touches the sheet's plan.");
    }

    [Test]
    public void DealOffPlanCueAtYieldsIndicesInsideBothCatalogs()
    {
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var sheet = TrackCueSheet.Build(MixedTrack(), effects, transitions, 7, 2);
        foreach (var boundaryBeat in new[] { 16, 97, 200, 370, 456 })
        {
            var dealt = sheet.DealOffPlanCueAt(boundaryBeat, gapGrids: 2, ask: boundaryBeat, onWallEffectIndex: -1);
            Assert.That(dealt.EffectIndex, Is.InRange(0, effects.Count - 1));
            Assert.That(dealt.TransitionIndex, Is.InRange(0, transitions.Count - 1));
        }
    }

    [Test]
    public void EveryMarkIsGridSpacedFromItsPhraseStartAndStrictlyAscending()
    {
        // "A cue is a marker at a Grid Boundary" (CONTEXT.md:184), and the Grid is a wire lane that restarts on
        // every Phrase: a Phrase shorter than sixteen beats simply makes a short Grid. So a Boundary is a Grid
        // multiple *from the Phrase start*, or the Phrase end itself — never a multiple of sixteen counted from
        // track beat one. Marks must also be strictly ascending.
        var sheet = TrackCueSheet.Build(MixedTrack(), MixedEffects(), MixedTransitions(), 42, 1);
        var phraseStarts = MixedTrack().Phrases.Select(p => p.StartBeat).ToHashSet();
        foreach (var mark in sheet.Marks)
        {
            Assert.That(
                phraseStarts.Any(start => mark.Beat >= start && (mark.Beat - start) % TrackCueSheet.GridBeats == 0)
                    || phraseStarts.Contains(mark.Beat),
                Is.True,
                $"the mark at beat {mark.Beat} is not Grid-spaced from any Phrase start");
        }

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
                        Assert.That(landing!.TransitionIndex, Is.EqualTo(anchor.PerformerIndex),
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
                        Assert.That(carrier!.EffectIndex, Is.EqualTo(anchor.PerformerIndex),
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
        // catalog is shown before any effect repeats. Untagged effects and a plain track keep capability out
        // of it too, isolating the top-deal cadence.
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
    public void NoTwoConsecutiveMarksDealTheSameEffect()
    {
        // The cycle test above checks aligned windows, which is true by construction and so blind to the one
        // place a fair bag repeats: the seam between two passes. A repeat there bakes a Transition from an
        // Effect to itself, which restarts it in place and moves nothing — a plan that skips a change. Several
        // seeds, because whether a seam lands on a repeat at all is a property of the shuffle.
        const int catalogSize = 4;
        var effects = UntaggedEffects(catalogSize);
        var transitions = PlainTransitions(2);

        foreach (var generation in Generations)
        {
            var sheet = TrackCueSheet.Build(PlainTrack(catalogSize * 6), effects, transitions, generation, 1);
            Assert.That(sheet.Marks.Count, Is.GreaterThan(catalogSize * 2),
                "not enough marks to cross a reshuffle seam");
            for (var i = 1; i < sheet.Marks.Count; i++)
            {
                Assert.That(sheet.Marks[i].EffectIndex, Is.Not.EqualTo(sheet.Marks[i - 1].EffectIndex),
                    $"generation {generation} dealt effect {sheet.Marks[i].EffectIndex} to marks {i - 1} and {i}");
            }
        }
    }

    [Test]
    public void EnergyAffinityDoesNotInfluenceThePlan()
    {
        // ADR-0011 took energy out of casting: it is a Performer input read from BeatManager, not a Director
        // one. The bag deals Effects freely and capability is asked only of a ride-through carrier, which has
        // to play the moment itself. So two catalogs identical in capability and differing only in energy
        // affinity must plan the same show from the same seed — mark for mark, anchor for anchor.
        var capabilities = new[]
        {
            Repertoire.HandlesFill | Repertoire.HandlesDrop,
            Repertoire.HandlesFill | Repertoire.HandlesDrop,
            Repertoire.HandlesDrop,
            Repertoire.HandlesFill,
            Repertoire.None,
            Repertoire.None,
        };
        var affinities = new[]
        {
            Repertoire.EnergyLow,
            Repertoire.EnergyHigh,
            Repertoire.EnergyHigh,
            Repertoire.EnergyMid,
            Repertoire.EnergyHigh,
            Repertoire.EnergyLow,
        };
        var untagged = Effects(capabilities);
        var tagged = Effects(capabilities.Select((c, i) => c | affinities[i]).ToArray());
        var transitions = MixedTransitions();

        foreach (var generation in Generations)
        {
            var withoutAffinity = TrackCueSheet.Build(MixedTrack(), untagged, transitions, generation, 1);
            var withAffinity = TrackCueSheet.Build(MixedTrack(), tagged, transitions, generation, 1);
            Assert.That(Serialize(withAffinity), Is.EqualTo(Serialize(withoutAffinity)),
                $"generation {generation}: energy affinity changed the plan");
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
            if (mark.Beat < beat && (best is null || mark.Beat > best.Beat))
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
