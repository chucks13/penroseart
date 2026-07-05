using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Seam-1 property tests for the pure Cue Sheet builder (ADR-0011). They assert the construction
/// constraints across many Phrase lengths, offsets, and seeds and never pin a specific random layout:
/// the layout is a seeded roll, so only its guarantees — Grid Boundaries, bounded gaps, a mandatory
/// end mark, and determinism — are behavior worth testing.
/// </summary>
public sealed class CueSheetConstraintTests
{
    private const int GridBeats = 16;
    private const int MinimumGapBeats = 16;
    private const int MaximumGapBeats = 64;

    private static readonly int[] PhraseLengths = { 16, 32, 48, 64, 80, 128, 192, 256, 512 };
    private static readonly int[] Offsets = { 0, 1, 17, 64, 577, -33, 1024 };
    private static readonly int[] Seeds = { 0, 1, 2, 7, 42, 100, 9999, -1, int.MaxValue };

    [Test]
    public void EveryCueMarkSitsOnAGridBoundary()
    {
        ForEachAnnouncement((length, offset, seed, sheet) =>
        {
            foreach (var mark in sheet.CueMarkOffsets)
            {
                Assert.That(mark % GridBeats, Is.EqualTo(0),
                    $"length={length} offset={offset} seed={seed}: mark {mark} is off the Grid");
            }
        });
    }

    [Test]
    public void ConsecutiveGapsIncludingTheRunInStayWithinCadence()
    {
        ForEachAnnouncement((length, offset, seed, sheet) =>
        {
            var previousMark = 0; // The run-in origin is the Phrase start.
            foreach (var mark in sheet.CueMarkOffsets)
            {
                var gap = mark - previousMark;
                Assert.That(gap, Is.GreaterThanOrEqualTo(MinimumGapBeats),
                    $"length={length} offset={offset} seed={seed}: gap {gap} below minimum cadence");
                Assert.That(gap, Is.LessThanOrEqualTo(MaximumGapBeats),
                    $"length={length} offset={offset} seed={seed}: gap {gap} above maximum cadence");
                previousMark = mark;
            }
        });
    }

    [Test]
    public void ThePhraseEndAlwaysCarriesTheFinalCueMark()
    {
        ForEachAnnouncement((length, offset, seed, sheet) =>
        {
            Assert.That(sheet.CueMarkOffsets, Is.Not.Empty,
                $"length={length} offset={offset} seed={seed}: sheet has no marks");
            Assert.That(sheet.CueMarkOffsets[sheet.CueMarkOffsets.Length - 1], Is.EqualTo(length),
                $"length={length} offset={offset} seed={seed}: final mark is not the Phrase end");
            Assert.That(sheet.PhraseLengthBeats, Is.EqualTo(length));
        });
    }

    [Test]
    public void CueMarksAreStrictlyAscending()
    {
        ForEachAnnouncement((length, offset, seed, sheet) =>
        {
            for (var i = 1; i < sheet.CueMarkOffsets.Length; i++)
            {
                Assert.That(sheet.CueMarkOffsets[i], Is.GreaterThan(sheet.CueMarkOffsets[i - 1]),
                    $"length={length} offset={offset} seed={seed}: marks are not strictly ascending");
            }
        });
    }

    [Test]
    public void SameAnnouncementRollsAnIdenticalSheet()
    {
        ForEachAnnouncement((length, offset, seed, sheet) =>
        {
            var rebuilt = CueSheet.Build(length, offset, seed);
            Assert.That(rebuilt.CueMarkOffsets, Is.EqualTo(sheet.CueMarkOffsets),
                $"length={length} offset={offset} seed={seed}: same announcement re-rolled a different sheet");
        });
    }

    [Test]
    public void DifferentSeedsVaryLayoutWithinTheConstraints()
    {
        // A long Phrase has interior freedom, so varying only the seed must reach more than one layout.
        var layouts = new HashSet<string>();
        foreach (var seed in Seeds)
        {
            layouts.Add(LayoutKey(CueSheet.Build(256, 0, seed)));
        }

        Assert.That(layouts.Count, Is.GreaterThan(1),
            "varying the seed never changed the layout of a long Phrase");
    }

    [Test]
    public void DifferentOffsetsVaryLayoutWithinTheConstraints()
    {
        // The offset is a roll dimension too: two same-length Phrases at different positions differ.
        var layouts = new HashSet<string>();
        foreach (var offset in Offsets)
        {
            layouts.Add(LayoutKey(CueSheet.Build(256, offset, 0)));
        }

        Assert.That(layouts.Count, Is.GreaterThan(1),
            "varying the offset never changed the layout of a long Phrase");
    }

    [Test]
    public void ShortestPhraseCarriesOnlyTheMandatoryEndMark()
    {
        // One Grid long: no interior room exists, so every seed yields exactly the Phrase-end mark.
        foreach (var seed in Seeds)
        {
            Assert.That(CueSheet.Build(GridBeats, 0, seed).CueMarkOffsets, Is.EqualTo(new[] { GridBeats }));
        }
    }

    [Test]
    public void NonGridPhraseLengthIsRejected()
    {
        Assert.That(() => CueSheet.Build(24, 0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => CueSheet.Build(0, 0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => CueSheet.Build(-16, 0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static void ForEachAnnouncement(Action<int, int, int, CueSheet> assertion)
    {
        foreach (var length in PhraseLengths)
        {
            foreach (var offset in Offsets)
            {
                foreach (var seed in Seeds)
                {
                    assertion(length, offset, seed, CueSheet.Build(length, offset, seed));
                }
            }
        }
    }

    private static string LayoutKey(CueSheet sheet)
    {
        return string.Join(",", sheet.CueMarkOffsets);
    }
}
