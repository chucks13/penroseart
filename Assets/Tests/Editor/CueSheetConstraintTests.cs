using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Seam-1 property tests for the pure Cue Sheet builder (ADR-0011). They assert the construction
/// constraints across many Phrase lengths — Grid-multiple and irregular alike — and many seeds, and
/// never pin a specific random layout: the layout is a seeded roll, so only its guarantees — interior
/// marks on Grid Boundaries, a mandatory Phrase-end mark, bounded gaps, and determinism — are behavior
/// worth testing.
/// </summary>
public sealed class CueSheetConstraintTests
{
    private const int GridBeats = 16;
    private const int MinimumGapBeats = 16;
    private const int MaximumGapBeats = 64;

    // Grid-multiple lengths plus irregular ones: 8 is sub-Grid, 40 a legal 8-multiple tail, 41 the truly
    // odd one, and 24/56/120 span the run-out range.
    private static readonly int[] PhraseLengths = { 16, 32, 48, 64, 80, 128, 192, 256, 512, 8, 24, 40, 41, 56, 120 };
    private static readonly int[] IrregularPhraseLengths = { 8, 24, 40, 41, 56, 120 };
    private static readonly int[] Seeds = { 0, 1, 2, 7, 42, 100, 9999, -1, int.MaxValue };

    [Test]
    public void EveryInteriorCueMarkSitsOnAGridBoundary()
    {
        // The Phrase end (final mark) is a Grid Boundary by wire re-anchor even when the length is not a
        // Grid multiple; every mark before it sits on a Grid multiple. ThePhraseEndAlwaysCarriesTheFinalCueMark
        // pins the end mark itself.
        ForEachAnnouncement((length, seed, sheet) =>
        {
            for (var i = 0; i < sheet.CueMarkOffsets.Length - 1; i++)
            {
                Assert.That(sheet.CueMarkOffsets[i] % GridBeats, Is.EqualTo(0),
                    $"length={length} seed={seed}: interior mark {sheet.CueMarkOffsets[i]} is off the Grid");
            }
        });
    }

    [Test]
    public void ConsecutiveGapsIncludingTheRunInStayWithinCadence()
    {
        ForEachAnnouncement((length, seed, sheet) =>
        {
            // The one min-gap exception: a Phrase shorter than one Grid carries only its end mark, so its
            // run-in is unavoidably below one Grid.
            var subGridPhrase = length < GridBeats;
            var previousMark = 0; // The run-in origin is the Phrase start.
            foreach (var mark in sheet.CueMarkOffsets)
            {
                var gap = mark - previousMark;
                if (!subGridPhrase)
                {
                    Assert.That(gap, Is.GreaterThanOrEqualTo(MinimumGapBeats),
                        $"length={length} seed={seed}: gap {gap} below minimum cadence");
                }

                Assert.That(gap, Is.LessThanOrEqualTo(MaximumGapBeats),
                    $"length={length} seed={seed}: gap {gap} above maximum cadence");
                previousMark = mark;
            }
        });
    }

    [Test]
    public void ThePhraseEndAlwaysCarriesTheFinalCueMark()
    {
        ForEachAnnouncement((length, seed, sheet) =>
        {
            Assert.That(sheet.CueMarkOffsets, Is.Not.Empty,
                $"length={length} seed={seed}: sheet has no marks");
            Assert.That(sheet.CueMarkOffsets[sheet.CueMarkOffsets.Length - 1], Is.EqualTo(length),
                $"length={length} seed={seed}: final mark is not the Phrase end");
            Assert.That(sheet.PhraseLengthBeats, Is.EqualTo(length));
        });
    }

    [Test]
    public void CueMarksAreStrictlyAscending()
    {
        ForEachAnnouncement((length, seed, sheet) =>
        {
            for (var i = 1; i < sheet.CueMarkOffsets.Length; i++)
            {
                Assert.That(sheet.CueMarkOffsets[i], Is.GreaterThan(sheet.CueMarkOffsets[i - 1]),
                    $"length={length} seed={seed}: marks are not strictly ascending");
            }
        });
    }

    [Test]
    public void TheRunOutAbsorbsTheIrregularTail()
    {
        // For an irregular Phrase of at least one Grid, one random run-out Grid carries the odd tail: the
        // final gap stays in (16, 64] and no interior mark reaches into the last Grid (above length - 16).
        // A sub-Grid Phrase is the sole exception — it carries just its end mark.
        foreach (var length in IrregularPhraseLengths)
        {
            foreach (var seed in Seeds)
            {
                var sheet = CueSheet.Build(length, seed);
                if (length < GridBeats)
                {
                    Assert.That(sheet.CueMarkOffsets, Is.EqualTo(new[] { length }),
                        $"length={length} seed={seed}: sub-Grid Phrase is not the lone end mark");
                    continue;
                }

                var marks = sheet.CueMarkOffsets;
                var finalGap = marks.Length == 1 ? marks[0] : marks[marks.Length - 1] - marks[marks.Length - 2];
                Assert.That(finalGap, Is.GreaterThan(MinimumGapBeats),
                    $"length={length} seed={seed}: run-out gap {finalGap} not above one Grid");
                Assert.That(finalGap, Is.LessThanOrEqualTo(MaximumGapBeats),
                    $"length={length} seed={seed}: run-out gap {finalGap} above maximum cadence");
                for (var i = 0; i < marks.Length - 1; i++)
                {
                    Assert.That(marks[i], Is.LessThanOrEqualTo(length - GridBeats),
                        $"length={length} seed={seed}: interior mark {marks[i]} reaches into the final Grid");
                }
            }
        }
    }

    [Test]
    public void SameAnnouncementRollsAnIdenticalSheet()
    {
        ForEachAnnouncement((length, seed, sheet) =>
        {
            var rebuilt = CueSheet.Build(length, seed);
            Assert.That(rebuilt.CueMarkOffsets, Is.EqualTo(sheet.CueMarkOffsets),
                $"length={length} seed={seed}: same announcement re-rolled a different sheet");
        });
    }

    [Test]
    public void DifferentSeedsVaryLayoutWithinTheConstraints()
    {
        // A long Phrase has interior freedom, so varying only the seed must reach more than one layout.
        var layouts = new HashSet<string>();
        foreach (var seed in Seeds)
        {
            layouts.Add(LayoutKey(CueSheet.Build(256, seed)));
        }

        Assert.That(layouts.Count, Is.GreaterThan(1),
            "varying the seed never changed the layout of a long Phrase");
    }

    [Test]
    public void ShortestPhraseCarriesOnlyTheMandatoryEndMark()
    {
        // One Grid long: no interior room exists, so every seed yields exactly the Phrase-end mark.
        foreach (var seed in Seeds)
        {
            Assert.That(CueSheet.Build(GridBeats, seed).CueMarkOffsets, Is.EqualTo(new[] { GridBeats }));
        }
    }

    [Test]
    public void OnlyNonPositiveLengthIsRejected()
    {
        // Irregular lengths are first-class now; only non-positive lengths throw.
        Assert.That(() => CueSheet.Build(24, 0), Throws.Nothing);
        Assert.That(() => CueSheet.Build(41, 0), Throws.Nothing);
        Assert.That(() => CueSheet.Build(0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => CueSheet.Build(-16, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static void ForEachAnnouncement(Action<int, int, CueSheet> assertion)
    {
        foreach (var length in PhraseLengths)
        {
            foreach (var seed in Seeds)
            {
                assertion(length, seed, CueSheet.Build(length, seed));
            }
        }
    }

    private static string LayoutKey(CueSheet sheet)
    {
        return string.Join(",", sheet.CueMarkOffsets);
    }
}
