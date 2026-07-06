using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Seam-1 property tests for the pure Cue Sheet builder (ADR-0011). They assert the construction
/// constraints across many Phrase lengths and seeds and never pin a specific random layout:
/// the layout is a seeded roll, so only its guarantees — Grid Boundaries, bounded gaps, a mandatory
/// end mark, and determinism — are behavior worth testing.
/// </summary>
public sealed class CueSheetConstraintTests
{
    private const int GridBeats = 16;
    private const int MinimumGapBeats = 16;
    private const int MaximumGapBeats = 64;

    private static readonly int[] PhraseLengths = { 16, 32, 48, 64, 80, 128, 192, 256, 512 };
    private static readonly int[] Seeds = { 0, 1, 2, 7, 42, 100, 9999, -1, int.MaxValue };

    [Test]
    public void EveryCueMarkSitsOnAGridBoundary()
    {
        ForEachAnnouncement((length, seed, sheet) =>
        {
            foreach (var mark in sheet.CueMarkOffsets)
            {
                Assert.That(mark % GridBeats, Is.EqualTo(0),
                    $"length={length} seed={seed}: mark {mark} is off the Grid");
            }
        });
    }

    [Test]
    public void ConsecutiveGapsIncludingTheRunInStayWithinCadence()
    {
        ForEachAnnouncement((length, seed, sheet) =>
        {
            var previousMark = 0; // The run-in origin is the Phrase start.
            foreach (var mark in sheet.CueMarkOffsets)
            {
                var gap = mark - previousMark;
                Assert.That(gap, Is.GreaterThanOrEqualTo(MinimumGapBeats),
                    $"length={length} seed={seed}: gap {gap} below minimum cadence");
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
    public void NonGridPhraseLengthIsRejected()
    {
        Assert.That(() => CueSheet.Build(24, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
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
