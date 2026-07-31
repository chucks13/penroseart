// Seam tests for the pure track-scoped Cue Sheet builder (TrackCueSheet.Build). Synthetic structures and
// descriptor catalogs go in; the built sheet's constraints, determinism, and fairness come out. Tests assert
// caller-visible guarantees — the gap ceiling, Anchor clearance, blend fit, determinism, bag fairness —
// never the private walk order or bag internals.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Behavioral battery for <see cref="TrackCueSheet"/>: the single track-scoped builder seam. Every test
/// drives the public <see cref="TrackCueSheet.Build"/> and asserts a model rule through the returned
/// plan (marks and Anchor resolutions), across many seeds where a guarantee must hold for all shows.
/// </summary>
public sealed class TrackCueSheetTests
{
    private const int GridBeats = TrackCueSheet.GridBeats;
    private const int MaximumGapBeats = TrackCueSheet.MaximumGapBeats;

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
    public void ConsecutiveMarkGapsNeverExceedTheCeiling()
    {
        // Never more than 64 beats without a transition, whatever the seed and however the Anchors fell.
        // There is deliberately no lower-bound assertion: the model has no fixed spacing floor — small gaps
        // are rare by cadence judgment, not forbidden by constant.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var structures = new[] { MixedTrack(), ShortGridTrack(), TwoDropTrack() };
        foreach (var structure in structures)
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(structure, effects, transitions, generation, player);
                var marks = sheet.Marks;
                for (var i = 1; i < marks.Count; i++)
                {
                    var gap = marks[i].Beat - marks[i - 1].Beat;
                    Assert.That(gap, Is.LessThanOrEqualTo(MaximumGapBeats),
                        $"gen={generation} player={player}: gap {gap} above four Grids");
                }
            }
        }
    }

    [Test]
    public void MarksAreIrregularlySpacedNeverClumpedNeverMetronomic()
    {
        // "Never clumped, never metronomic" is plan-time judgment, asserted statistically: one-Grid-or-closer
        // gaps stay rare, never run back to back, and the mean spacing stays in the intended band. A
        // metronomic plan would collapse the gap distribution onto one value; a clumped one would pile
        // gaps at the bottom.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var tightGaps = 0;
        var totalGaps = 0;
        var totalBeats = 0;
        var longestTightRun = 0;
        var distinctGaps = new HashSet<int>();

        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var marks = TrackCueSheet.Build(MixedTrack(), effects, transitions, generation, player).Marks;
                var run = 0;
                for (var i = 1; i < marks.Count; i++)
                {
                    var gap = marks[i].Beat - marks[i - 1].Beat;
                    totalGaps++;
                    totalBeats += gap;
                    distinctGaps.Add(gap);
                    if (gap <= GridBeats)
                    {
                        tightGaps++;
                        run++;
                        longestTightRun = run > longestTightRun ? run : longestTightRun;
                    }
                    else
                    {
                        run = 0;
                    }
                }
            }
        }

        Assert.That(totalGaps, Is.GreaterThan(100), "not enough gaps to judge the distribution");
        Assert.That(tightGaps / (double)totalGaps, Is.LessThan(0.15),
            $"{tightGaps} of {totalGaps} gaps sat at one Grid or less — changes are clustering");
        Assert.That(longestTightRun, Is.LessThanOrEqualTo(2),
            $"{longestTightRun} tight gaps ran back to back — that is the clump this rule exists to stop");
        Assert.That(totalBeats / (double)totalGaps, Is.InRange(36.0, 50.0),
            "mean spacing drifted away from the intended cadence");
        Assert.That(distinctGaps.Count, Is.GreaterThan(3),
            "the gap distribution collapsed onto a few values — the plan turned metronomic");
    }

    [Test]
    public void APhraseEndIsAnOrdinaryCandidateNotAMandatoryMark()
    {
        // The old walk placed a mark on every Phrase end, which is what forced the short gaps at every seam.
        // Phrase boundaries are preferred positions, not mandates (ADR-0010, CONTEXT.md "Cue Mark"), so across
        // seeds plenty of Phrase ends must go unmarked.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var phraseEnds = MixedTrack().Phrases.Select(p => p.StartBeat).Skip(1).ToHashSet();
        var unmarked = 0;

        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(MixedTrack(), effects, transitions, generation, player);
                var marked = sheet.Marks.Select(m => m.Beat).ToHashSet();
                unmarked += phraseEnds.Count(end => !marked.Contains(end));
            }
        }

        Assert.That(unmarked, Is.GreaterThan(0), "every Phrase end carried a mark — the mandate is still in force");
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

        Assert.That(dealtEffects, Is.EquivalentTo(Enumerable.Range(0, effects.Count)));
        Assert.That(dealtTransitions, Is.EquivalentTo(Enumerable.Range(0, transitions.Count)));
    }

    [Test]
    public void AnOffPlanDealNeverHandsBackWhatIsAlreadyOnTheWall()
    {
        // The freeze this closes: the ceiling fired on time and dealt the Effect already showing, so the wall
        // transitioned to itself and the 64-beat rule was met on paper while nothing moved.
        var effects = MixedEffects();
        var sheet = TrackCueSheet.Build(MixedTrack(), effects, MixedTransitions(), 7, 2);
        for (var onWall = 0; onWall < effects.Count; onWall++)
        {
            for (var ask = 1; ask <= 64; ask++)
            {
                var dealt = sheet.DealOffPlanCueAt(81, TrackCueSheet.MaximumGapGrids, ask, onWall);
                Assert.That(dealt.EffectIndex, Is.Not.EqualTo(onWall),
                    $"ask {ask} would transition Effect {onWall} to itself.");
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
    public void EveryMarkSitsOnAGridBoundaryOfItsOwnPhraseAndMarksAscend()
    {
        // Marks sit on Grid Boundaries and nowhere else, and the Grid restarts on every Phrase: a Boundary
        // is a Grid multiple from the start of the Phrase *containing* the mark — never a multiple of
        // sixteen counted from track beat one, and a Phrase shorter than sixteen beats simply makes a short
        // Grid. The lattice is derived independently here so alignment to some other Phrase cannot pass.
        // Marks must also be strictly ascending.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var structures = new[] { MixedTrack(), ShortGridTrack(), TwoDropTrack() };
        foreach (var structure in structures)
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(structure, effects, transitions, generation, player);
                var lattice = GridBoundaryLattice(structure);
                foreach (var mark in sheet.Marks)
                {
                    Assert.That(lattice.Contains(mark.Beat), Is.True,
                        $"gen={generation} player={player}: the mark at beat {mark.Beat} "
                        + "is not a Grid Boundary of the Phrase containing it");
                }

                for (var i = 1; i < sheet.Marks.Count; i++)
                {
                    Assert.That(sheet.Marks[i].Beat, Is.GreaterThan(sheet.Marks[i - 1].Beat),
                        "marks are not strictly ascending");
                }
            }
        }
    }

    [Test]
    public void EveryAnchorIsClearedAndOwnedByACapableEffectAlreadyOnTheWall()
    {
        // An Anchor is performed one way: no Cue Mark sits on the landing boundary, the last mark before
        // the landing casts a repertoire-capable Effect (so it is already on the wall when the moment
        // hits), and the resolution names that Effect.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var structures = new[] { MixedTrack(), TwoDropTrack() };

        foreach (var structure in structures)
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(structure, effects, transitions, generation, player);
                Assert.That(sheet.Anchors, Is.Not.Empty,
                    $"gen={generation} player={player}: a capable catalog left every Anchor unowned");
                foreach (var anchor in sheet.Anchors)
                {
                    var capability = anchor.Kind == AnchorKind.Drop ? Repertoire.HandlesDrop : Repertoire.HandlesFill;

                    Assert.That(MarkAt(sheet, anchor.LandingBeat), Is.Null,
                        $"gen={generation} player={player}: a Cue Mark sits on the Anchor landing at {anchor.LandingBeat}");

                    var carrier = LastMarkBefore(sheet, anchor.LandingBeat);
                    Assert.That(carrier, Is.Not.Null,
                        $"gen={generation} player={player}: no Effect was cast ahead of the Anchor at {anchor.LandingBeat}");
                    Assert.That(carrier!.EffectIndex, Is.EqualTo(anchor.EffectIndex),
                        "the Anchor resolution does not name the Effect on the wall at the moment");
                    Assert.That(EffectCapability(effects, anchor.EffectIndex) & capability, Is.EqualTo(capability),
                        "the Effect on the wall at the Anchor is not capable");
                }
            }
        }
    }

    [Test]
    public void NoRunwayOrTailCrossesAnAnchorLanding()
    {
        // The moment is cleared of Runways and Tails: no transition's blend interval — Runway before its mark,
        // Tail after — may contain an owned Anchor's landing beat.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(MixedTrack(), effects, transitions, generation, player);
                foreach (var anchor in sheet.Anchors)
                {
                    foreach (var mark in sheet.Marks)
                    {
                        var repertoire = transitions[mark.TransitionIndex].Repertoire;
                        var blendStart = mark.Beat - repertoire.RunwayBeats;
                        var blendEnd = mark.Beat + repertoire.TailBeats;
                        Assert.That(
                            anchor.LandingBeat >= blendStart && anchor.LandingBeat <= blendEnd, Is.False,
                            $"gen={generation} player={player}: the blend of the mark at {mark.Beat} "
                            + $"([{blendStart}, {blendEnd}]) crosses the Anchor landing at {anchor.LandingBeat}");
                    }
                }
            }
        }
    }

    [Test]
    public void NoTwoBlendsOverlapEvenAcrossShortGrids()
    {
        // The real spacing invariant: transitions are dealt to fit the space they are given, so consecutive
        // blends never share a beat and the first blend never starts before the track's opening downbeat.
        // The short-Grid structure and the wide-runway catalog are the stress: a 12-beat Runway card must
        // never be dealt into a slot it cannot fit.
        var effects = MixedEffects();
        var transitions = WideTimingTransitions();
        var structures = new[] { MixedTrack(), ShortGridTrack(), TwoDropTrack() };

        foreach (var structure in structures)
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(structure, effects, transitions, generation, player);
                var startBeat = structure.Phrases[0].StartBeat;
                var previousBlendEnd = startBeat - 1;
                foreach (var mark in sheet.Marks)
                {
                    var repertoire = transitions[mark.TransitionIndex].Repertoire;
                    var blendStart = mark.Beat - repertoire.RunwayBeats;
                    Assert.That(blendStart, Is.GreaterThan(previousBlendEnd),
                        $"gen={generation} player={player}: the blend into the mark at {mark.Beat} starts at "
                        + $"{blendStart}, inside the previous blend (ends {previousBlendEnd})");
                    previousBlendEnd = mark.Beat + repertoire.TailBeats;
                }
            }
        }
    }

    [Test]
    public void NoTransitionEverLeadsFromAnEffectIntoItself()
    {
        // A Transition from an Effect into itself restarts it in place and moves nothing — a plan that skips
        // a change. Consecutive marks must always deal different Effects, including across the bag's
        // reshuffle seam and through Anchor carrier deals.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var structures = new[] { MixedTrack(), TwoDropTrack(), PlainTrack(4 * 18) };

        foreach (var structure in structures)
        foreach (var generation in Generations)
        {
            foreach (var player in Players)
            {
                var sheet = TrackCueSheet.Build(structure, effects, transitions, generation, player);
                for (var i = 1; i < sheet.Marks.Count; i++)
                {
                    Assert.That(sheet.Marks[i].EffectIndex, Is.Not.EqualTo(sheet.Marks[i - 1].EffectIndex),
                        $"gen={generation} player={player}: marks {i - 1} and {i} deal the same Effect");
                }
            }
        }
    }

    [Test]
    public void DropLandingsAndFillWindowsBecomeAnchors()
    {
        // The two drops and the lone fill (the fill leading into a drop is folded into that drop) each
        // surface as an owned Anchor at the expected Grid Boundary.
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
    public void DifferentSaltsDealADifferentShowAndTheSameSaltRebuildsIt()
    {
        // The per-run salt (ADR-0008) is what stops every session opening with the identical show when the
        // wire's generation counters restart. Same salt, same show; different salt, fresh show.
        var effects = MixedEffects();
        var transitions = MixedTransitions();
        var one = TrackCueSheet.Build(MixedTrack(), effects, transitions, 1, 1, salt: 12345);
        var same = TrackCueSheet.Build(MixedTrack(), effects, transitions, 1, 1, salt: 12345);
        var other = TrackCueSheet.Build(MixedTrack(), effects, transitions, 1, 1, salt: 54321);

        Assert.That(Serialize(same), Is.EqualTo(Serialize(one)), "the same salt re-dealt a different show");
        Assert.That(Serialize(other), Is.Not.EqualTo(Serialize(one)), "a different salt dealt the identical show");
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
        var sheet = TrackCueSheet.Build(PlainTrack(catalogSize * 9), effects, transitions, 5, 1);

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
    public void EnergyAffinityDoesNotInfluenceThePlan()
    {
        // Energy is out of casting: it is a Performer input read from BeatManager, not a Director
        // one. The bag deals Effects freely and capability is asked only of an Anchor carrier, which has
        // to be on the wall for the moment. So two catalogs identical in capability and differing only in
        // energy affinity must plan the same show from the same seed — mark for mark, anchor for anchor.
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

        var sheet = TrackCueSheet.Build(PlainTrack(catalogSize * 12), effects, transitions, 11, 1);
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
    public void MissingCapableEffectLeavesAnchorsUnownedAndLandingsOrdinary()
    {
        // Without a capable Effect anywhere in the catalog there is no way to own the moment: no resolution
        // is recorded and the landing boundary goes back to being an ordinary candidate.
        var effects = UntaggedEffects(4); // no HandlesDrop tag anywhere
        var transitions = MixedTransitions();

        var landingMarked = 0;
        foreach (var generation in Generations)
        {
            var sheet = TrackCueSheet.Build(TwoDropTrack(), effects, transitions, generation, 1);
            Assert.That(sheet.Anchors, Is.Empty,
                $"gen={generation}: an Anchor was owned despite no capable effect");
            landingMarked += sheet.Marks.Count(m => m.Beat == 193 || m.Beat == 385);
        }

        Assert.That(landingMarked, Is.GreaterThan(0),
            "unowned landings never took an ordinary mark across seeds — they are still being suppressed");
    }

    [Test]
    public void DiscardPileEncoreOwnsAnAnchorWhenTheBagHasNoCapableCardLeft()
    {
        // One drop-capable effect, several plain ones, and two drop Anchors with distinct carrier marks
        // inside one shuffle cycle. The first Anchor's carrier deals the lone capable card; the second,
        // still in the same shuffle, finds none left in the bag and encores it from the discard pile.
        // Both Anchors end up owned by that single card.
        var effects = Effects(Repertoire.HandlesDrop, Repertoire.None, Repertoire.None, Repertoire.None, Repertoire.None, Repertoire.None);
        var transitions = PlainTransitions(2);

        foreach (var generation in Generations)
        {
            var sheet = TrackCueSheet.Build(TwoDropTrack(), effects, transitions, generation, 1);
            var dropAnchors = sheet.Anchors.Where(a => a.Kind == AnchorKind.Drop).ToArray();
            Assert.That(dropAnchors.Length, Is.EqualTo(2), $"gen={generation}: expected two drop Anchors");
            foreach (var anchor in dropAnchors)
            {
                Assert.That(anchor.EffectIndex, Is.EqualTo(0),
                    $"gen={generation}: drop Anchor not owned by the only capable effect (encore failed)");
            }
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

    /// <summary>
    /// A plain track of equal one-Grid Phrases with no Anchors. Marks land roughly every 43 beats,
    /// so budget about three Phrases per mark when sizing one of these.
    /// </summary>
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

    /// <summary>
    /// A run of short irregular Phrases, so the boundary lattice carries many short Grids and consecutive
    /// candidates sit well under sixteen beats apart. The stress fixture for blend fit: with no fixed
    /// spacing floor, marks may land close together and every dealt transition still has to fit its slot.
    /// </summary>
    private static StructureValues ShortGridTrack()
    {
        var phrases = new List<StructurePhraseValues>();
        var lengths = new[] { 24, 9, 21, 7, 40, 11, 23, 8, 25, 39, 10, 22, 41, 9, 24 };
        var start = 1;
        foreach (var length in lengths)
        {
            phrases.Add(Phrase(start, start + length - 1, PhraseType.Verse));
            start += length;
        }

        return Structure(phrases.ToArray());
    }

    /// <summary>
    /// Two drop Anchors set deep enough in the track that each has a preceding carrier mark: at least
    /// <see cref="MaximumGapBeats"/> of music ahead of the landing, where the cadence ceiling guarantees a
    /// mark — the Effect that will be on the wall when the moment hits.
    /// </summary>
    private static StructureValues TwoDropTrack()
    {
        return Structure(
            Phrase(1, 64, PhraseType.Intro),
            Phrase(65, 128, PhraseType.Up),
            Phrase(129, 192, PhraseType.Up),
            Phrase(193, 256, PhraseType.Drop, drop: 193),
            Phrase(257, 320, PhraseType.Verse),
            Phrase(321, 384, PhraseType.Up),
            Phrase(385, 448, PhraseType.Drop, drop: 385),
            Phrase(449, 512, PhraseType.Outro));
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
            new TransitionDescriptor(Transition(runway: 4, tail: 0)),
            new TransitionDescriptor(Transition(runway: 8, tail: 4)),
            new TransitionDescriptor(Transition(runway: 2, tail: 2)),
            new TransitionDescriptor(Transition(runway: 12, tail: 0)),
        };
    }

    /// <summary>A catalog spanning the timing extremes: long Runways, long Tails, and one small card.</summary>
    private static IReadOnlyList<TransitionDescriptor> WideTimingTransitions()
    {
        return new[]
        {
            new TransitionDescriptor(Transition(runway: 12, tail: 0)),
            new TransitionDescriptor(Transition(runway: 0, tail: 8)),
            new TransitionDescriptor(Transition(runway: 2, tail: 1)),
            new TransitionDescriptor(Transition(runway: 6, tail: 6)),
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
            list[i] = new EffectDescriptor(reps[i]);
        }

        return list;
    }

    private static IReadOnlyList<TransitionDescriptor> PlainTransitions(int count)
    {
        var list = new TransitionDescriptor[count];
        for (var i = 0; i < count; i++)
        {
            list[i] = new TransitionDescriptor(Transition(runway: 4, tail: 0));
        }

        return list;
    }

    private static TransitionRepertoire Transition(int runway, int tail)
    {
        return TransitionRepertoire.FromRunwayAndTail(Repertoire.None, runway, tail,
            TransitionShape.Blend, TransitionIntensity.Subtle, defaultDurationSeconds: 4f);
    }

    // --- Helpers ----------------------------------------------------------------------------------------

    /// <summary>
    /// Every Grid Boundary of a structure, derived independently of the builder: each Phrase contributes
    /// its start and every sixteenth beat after that still falls inside the Phrase.
    /// </summary>
    private static HashSet<int> GridBoundaryLattice(StructureValues structure)
    {
        var boundaries = new HashSet<int>();
        foreach (var phrase in structure.Phrases)
        {
            var length = phrase.EndBeat - phrase.StartBeat + 1;
            for (var offset = 0; offset < length; offset += GridBeats)
            {
                boundaries.Add(phrase.StartBeat + offset);
            }
        }

        return boundaries;
    }

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
        return index >= 0 && index < effects.Count ? effects[index].Repertoire : Repertoire.None;
    }

    private static string Serialize(TrackCueSheet sheet)
    {
        var marks = string.Join(";", sheet.Marks.Select(m => $"{m.Beat}:{m.EffectIndex}:{m.TransitionIndex}"));
        var anchors = string.Join(";", sheet.Anchors.Select(a => $"{a.LandingBeat}:{a.Kind}:{a.EffectIndex}"));
        return marks + "|" + anchors;
    }
}
