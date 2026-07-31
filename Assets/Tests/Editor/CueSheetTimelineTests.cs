// Seam tests for the pure Cue Sheet timeline builder. Wire-translated structures and deterministic
// TrackCueSheet plans go in; Grid rows, beat flags, phrase coverage, cue identity, and degradation
// behavior come out. Tests use only CueSheetTimeline.Build and RowContaining.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>
/// Behavioral coverage for <see cref="CueSheetTimeline"/> through its two public entry points.
/// Inputs use the production structure translation and Cue Sheet builder seams.
/// </summary>
public sealed class CueSheetTimelineTests
{
    /// <summary>Pins Runway, Impact, and Tail placement around a Cue Mark.</summary>
    [Test]
    public void RunwayImpactAndTailOccupyTheBeatsAroundTheirMark()
    {
        // An owned drop Anchor forces a carrier mark onto the last candidate boundary before its landing,
        // which is how these paint tests get a mark at a known beat (17 here, ahead of the drop at 33) now
        // that nothing pins a mark onto the landing itself.
        var structure = Structure(
            48,
            Phrase(1, 32, PhraseType.Intro),
            Phrase(33, 48, PhraseType.Drop, dropLandingBeat: 33));
        var transition = Transition(runwayBeats: 3, tailBeats: 2);
        var sheet = Sheet(structure, transition, Repertoire.HandlesDrop);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(rows[0].Cells[12], Is.EqualTo(CueSheetBeatMark.None));
        Assert.That(rows[0].Cells[13], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[0].Cells[15], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
        Assert.That(rows[1].Cells[1], Is.EqualTo(CueSheetBeatMark.Tail));
        Assert.That(rows[1].Cells[2], Is.EqualTo(CueSheetBeatMark.Tail));
        Assert.That(rows[1].Cells[3], Is.EqualTo(CueSheetBeatMark.None));
    }

    /// <summary>Pins a Runway straddling a Grid boundary into both affected rows.</summary>
    [Test]
    public void RunwayStraddlingAGridBoundaryPaintsBothRows()
    {
        var structure = Structure(
            42,
            Phrase(1, 10, PhraseType.Intro),
            Phrase(11, 26, PhraseType.Up),
            Phrase(27, 42, PhraseType.Drop, dropLandingBeat: 27));
        var transition = Transition(runwayBeats: 5, tailBeats: 0);
        var sheet = Sheet(structure, transition, Repertoire.HandlesDrop);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        // The ten-beat Intro lays one short row (1-10); the forced carrier mark at 11 opens the Up row.
        // Its five-beat Runway (6-10) reaches back across the row boundary into the short Intro row.
        Assert.That(rows[0].Cells[4], Is.EqualTo(CueSheetBeatMark.None));
        Assert.That(rows[0].Cells[5], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[0].Cells[9], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
    }

    /// <summary>
    /// Pins beat-in-Grid column placement. Every Cue Mark is a Grid Boundary and every row begins on one, so
    /// a mark always paints column zero of its row — including where a short Phrase makes the preceding row
    /// end early. Column 15 is unreachable for a mark: the old mandatory Phrase-end mark could land mid-Grid
    /// (and even one beat past the track), and nothing places a mark off a Boundary any more.
    /// </summary>
    [Test]
    public void MarkColumnsUseBeatInGridPosition()
    {
        var transition = Transition(0, 0);

        // Regular Grids: the forced carrier mark at 17 opens its own row on column zero.
        var structure = Structure(
            48,
            Phrase(1, 32, PhraseType.Intro),
            Phrase(33, 48, PhraseType.Drop, dropLandingBeat: 33));
        var rows = CueSheetTimeline.Build(
            Sheet(structure, transition, Repertoire.HandlesDrop), structure, new[] { transition }, null);

        Assert.That(CueSheetTimeline.RowContaining(rows, 17), Is.EqualTo(1));
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));

        // A short Intro Phrase ends its row early; the carrier mark at 11 still opens column zero.
        var shortStructure = Structure(
            42,
            Phrase(1, 10, PhraseType.Intro),
            Phrase(11, 26, PhraseType.Up),
            Phrase(27, 42, PhraseType.Chorus, dropLandingBeat: 27));
        var shortRows = CueSheetTimeline.Build(
            Sheet(shortStructure, transition, Repertoire.HandlesDrop), shortStructure, new[] { transition }, null);

        Assert.That(CueSheetTimeline.RowContaining(shortRows, 11), Is.EqualTo(1));
        Assert.That(shortRows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
    }

    /// <summary>
    /// Pins that cell flags compose rather than overwrite. The Playhead is the one layer that can land on
    /// any painted beat; Tail-meets-Runway is no longer reachable, because every Transition is dealt to fit
    /// the space it is given and no two blends ever share a beat.
    /// </summary>
    [Test]
    public void ImpactAndPlayheadCanShareOneCell()
    {
        var structure = Structure(
            48,
            Phrase(1, 32, PhraseType.Intro),
            Phrase(33, 48, PhraseType.Drop, dropLandingBeat: 33));
        var transition = Transition(runwayBeats: 4, tailBeats: 4);
        var sheet = Sheet(structure, transition, Repertoire.HandlesDrop);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, currentBeat: 17);

        Assert.That(
            rows[1].Cells[0],
            Is.EqualTo(CueSheetBeatMark.Impact | CueSheetBeatMark.Playhead));
    }

    /// <summary>Pins fired state as read off the cue itself, so the row shows what that cue actually did.</summary>
    [Test]
    public void CueFiredComesFromTheMarkItself()
    {
        var structure = Structure(
            48,
            Phrase(1, 32, PhraseType.Intro),
            Phrase(33, 48, PhraseType.Drop, dropLandingBeat: 33));
        var transition = Transition(0, 0);
        var sheet = Sheet(structure, transition, Repertoire.HandlesDrop);

        var pendingRows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);
        Assert.That(pendingRows[1].CueFired, Is.False, "An unfired cue reads pending.");

        sheet.Marks[0].FiredAtBeat = sheet.Marks[0].Beat;
        var firedRows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(firedRows[1].CueFired, Is.True);
        Assert.That(firedRows[2].CueFired, Is.False, "The Anchor row carries no mark and stays pending.");
    }

    /// <summary>Pins a phrase label to its start row without repeating it on later rows.</summary>
    [Test]
    public void PhraseStartAppearsOnlyOnTheRowWhereThePhraseBegins()
    {
        var structure = Structure(48, Phrase(1, 48, PhraseType.Chorus));

        var rows = CueSheetTimeline.Build(default, structure, null, null);

        Assert.That(rows[0].PhraseStart, Is.EqualTo(PhraseType.Chorus));
        Assert.That(rows[1].PhraseStart, Is.Null);
        Assert.That(rows[2].PhraseStart, Is.Null);
    }

    /// <summary>
    /// Pins the Grid restarting at every phrase: a phrase shorter than a Grid ends on a short row
    /// and the next phrase begins on column one, so rows stay in step with the Grid the runtime
    /// delivers instead of sliding onto an absolute 16-beat lattice.
    /// </summary>
    [Test]
    public void AShortPhraseEndsItsRowAndTheNextPhraseRestartsTheGrid()
    {
        var structure = Structure(
            32,
            Phrase(1, 8, PhraseType.Intro),
            Phrase(9, 32, PhraseType.Drop));

        var rows = CueSheetTimeline.Build(default, structure, null, null);

        Assert.That(rows.Count, Is.EqualTo(3));
        Assert.That(rows[0].FirstBeat, Is.EqualTo(1));
        Assert.That(rows[0].Cells.Count, Is.EqualTo(8), "The eight-beat phrase ends its row.");
        Assert.That(rows[0].Phrase, Is.EqualTo(PhraseType.Intro));
        Assert.That(rows[1].FirstBeat, Is.EqualTo(9), "The next phrase restarts the Grid.");
        Assert.That(rows[1].Cells.Count, Is.EqualTo(TrackCueSheet.GridBeats));
        Assert.That(rows[1].Phrase, Is.EqualTo(PhraseType.Drop));
        Assert.That(rows[1].PhraseStart, Is.EqualTo(PhraseType.Drop));
        Assert.That(rows[2].FirstBeat, Is.EqualTo(25));
        Assert.That(rows[2].Cells.Count, Is.EqualTo(8), "The phrase's own last Grid is short.");
        Assert.That(rows[2].PhraseStart, Is.Null);
    }

    /// <summary>Pins an Anchor-landing row's cue identity to the Effect on the wall for the moment.</summary>
    [Test]
    public void AnAnchorRowShowsTheEffectOnTheWallForTheMoment()
    {
        var transition = Transition(0, 0);
        // The fill Anchor lands at beat 81; the plan casts its capable Effect at a carrier mark somewhere
        // ahead of it, and the landing boundary itself carries no mark. The Anchor's row is index five: four
        // Intro rows, then the Up row, then the Chorus row it lands on.
        var structure = Structure(
            96,
            Phrase(1, 64, PhraseType.Intro),
            Phrase(65, 80, PhraseType.Up, fillStartBeat: 1),
            Phrase(81, 96, PhraseType.Chorus));
        var sheet = Sheet(structure, transition, Repertoire.HandlesFill);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(
            rows[5].Cells[0] & CueSheetBeatMark.AnchorLanding,
            Is.EqualTo(CueSheetBeatMark.AnchorLanding));
        Assert.That(rows[5].CueEffectIndex, Is.EqualTo(0));
        Assert.That(rows[5].CueTransitionIndex, Is.Null);
        Assert.That(rows[5].CueIsRideThrough, Is.True);
        Assert.That(sheet.Marks.Any(m => m.Beat == 81), Is.False,
            "an owned landing must not carry a Cue Mark");
    }

    /// <summary>Pins the playhead to exactly one cell and leaves it absent for a null beat.</summary>
    [Test]
    public void PlayheadPaintsExactlyOneCellOrNone()
    {
        var structure = Structure(32);

        var activeRows = CueSheetTimeline.Build(default, structure, null, 18);
        var idleRows = CueSheetTimeline.Build(default, structure, null, null);

        Assert.That(CountFlags(activeRows, CueSheetBeatMark.Playhead), Is.EqualTo(1));
        Assert.That(activeRows[1].Cells[1], Is.EqualTo(CueSheetBeatMark.Playhead));
        Assert.That(CountFlags(idleRows, CueSheetBeatMark.Playhead), Is.Zero);
    }

    /// <summary>Pins row coverage to structure length and every supported past-end timeline layer.</summary>
    [Test]
    public void RowsCoverTheStructureAndExtendForPastEndContent()
    {
        var hardCut = Transition(0, 0);
        var wholeStructure = Structure(33);
        Assert.That(
            CueSheetTimeline.Build(default, wholeStructure, null, null).Count,
            Is.EqualTo(3));

        // The Phrase map deliberately runs past its announced total_beats; the rows follow the Phrase map,
        // so the extension is what the row count is measuring. (A dealt mark's Tail can also stretch the
        // last row, but mark positions are cadence-dealt and so not pinnable here.)
        var markStructure = Structure(
            16,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Drop, dropLandingBeat: 17));
        var markSheet = Sheet(markStructure, hardCut);
        Assert.That(
            CueSheetTimeline.Build(markSheet, markStructure, new[] { hardCut }, null).Count,
            Is.EqualTo(2));

        // Four Intro Grids so a carrier mark exists ahead of the owned fill Anchor; the Anchor lands on
        // beat 81 — one past the final Phrase — so the rows extend one cell to show it.
        var anchorStructure = Structure(
            80,
            Phrase(1, 64, PhraseType.Intro),
            Phrase(65, 80, PhraseType.Up, fillStartBeat: 1));
        var anchorSheet = Sheet(anchorStructure, hardCut, Repertoire.HandlesFill);
        Assert.That(
            CueSheetTimeline.Build(anchorSheet, anchorStructure, new[] { hardCut }, null).Count,
            Is.EqualTo(6));

        var playheadStructure = Structure(16);
        Assert.That(
            CueSheetTimeline.Build(default, playheadStructure, null, 17).Count,
            Is.EqualTo(2));
    }

    /// <summary>Pins a default Cue Sheet's null collections to an ordinary empty-plan row.</summary>
    [Test]
    public void DefaultCueSheetDegradesToAnEmptyPlan()
    {
        var structure = Structure(16);

        var rows = CueSheetTimeline.Build(default, structure, null, null);

        Assert.That(rows.Count, Is.EqualTo(1));
        Assert.That(rows[0].Cells, Is.All.EqualTo(CueSheetBeatMark.None));
        Assert.That(rows[0].CueEffectIndex, Is.Null);
    }

    /// <summary>Pins empty and phrase-less structures to safe empty or Unknown coverage.</summary>
    [Test]
    public void EmptyAndPhraselessStructuresReturnSaneRows()
    {
        var emptyRows = CueSheetTimeline.Build(default, default, null, null);
        var phraselessRows = CueSheetTimeline.Build(default, Structure(16), null, null);

        Assert.That(emptyRows, Is.Empty);
        Assert.That(phraselessRows.Count, Is.EqualTo(1));
        Assert.That(phraselessRows[0].Phrase, Is.EqualTo(PhraseType.Unknown));
        Assert.That(phraselessRows[0].PhraseStart, Is.Null);
    }

    /// <summary>Pins an out-of-range transition index to zero Runway and Tail.</summary>
    [Test]
    public void OutOfRangeTransitionIndexUsesNoRunwayOrTail()
    {
        var structure = Structure(
            48,
            Phrase(1, 32, PhraseType.Intro),
            Phrase(33, 48, PhraseType.Drop, dropLandingBeat: 33));
        var transition = Transition(4, 4);
        var sheet = Sheet(structure, transition, Repertoire.HandlesDrop);

        // An empty catalog puts every baked index out of range while still being a supplied catalog, which is
        // what tells this apart from the null case NullTransitionsAndFiredMarksLeaveAValidPendingImpact covers.
        var rows = CueSheetTimeline.Build(sheet, structure, Array.Empty<TransitionRepertoire>(), null);

        Assert.That(CountFlags(rows, CueSheetBeatMark.Runway), Is.Zero);
        Assert.That(CountFlags(rows, CueSheetBeatMark.Tail), Is.Zero);
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
    }

    /// <summary>Pins null optional collections to pending, hard-cut presentation without exceptions.</summary>
    [Test]
    public void NullTransitionsAndFiredMarksLeaveAValidPendingImpact()
    {
        var structure = Structure(
            48,
            Phrase(1, 32, PhraseType.Intro),
            Phrase(33, 48, PhraseType.Drop, dropLandingBeat: 33));
        var transition = Transition(3, 2);
        var sheet = Sheet(structure, transition, Repertoire.HandlesDrop);

        var rows = CueSheetTimeline.Build(sheet, structure, null, null);

        Assert.That(CountFlags(rows, CueSheetBeatMark.Runway), Is.Zero);
        Assert.That(CountFlags(rows, CueSheetBeatMark.Tail), Is.Zero);
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
        Assert.That(rows[1].CueFired, Is.False);
    }

    /// <summary>Builds a translated structure through the same wire ingress used by runtime data.</summary>
    private static StructureValues Structure(int totalBeats, params StructurePhrase[] phrases)
    {
        var beatManager = new BeatManager();
        BeatManagerWireFixture.Feed(beatManager, snapshot =>
        {
            snapshot.players ??= new PlayerState[RaveWireSnapshot.PlayerCount];
            var player = PlayerState.Unavailable;
            player.structure = new PlayerStructure
            {
                generation = 1,
                trackId = "timeline-test",
                source = "analyzed",
                totalBeats = totalBeats,
                phraseCount = phrases.Length,
                phrases = phrases,
            };
            snapshot.players[0] = player;
        });
        beatManager.Update(0f);
        return beatManager.Players[0].Structure;
    }

    /// <summary>Creates one explicit wire phrase for structure translation.</summary>
    private static StructurePhrase Phrase(
        int startBeat,
        int endBeat,
        PhraseType type,
        int fillStartBeat = 0,
        int dropLandingBeat = 0)
    {
        return new StructurePhrase
        {
            startBeat = startBeat,
            endBeat = endBeat,
            type = type.ToString().ToLowerInvariant(),
            variant = 0,
            fillStartBeat = fillStartBeat,
            dropLandingBeat = dropLandingBeat,
        };
    }

    /// <summary>Builds a deterministic one-effect, one-transition Cue Sheet through its public seam.</summary>
    private static TrackCueSheet Sheet(
        StructureValues structure,
        TransitionRepertoire transition,
        Repertoire effectRepertoire = Repertoire.None)
    {
        return TrackCueSheet.Build(
            structure,
            new[] { new EffectDescriptor(effectRepertoire) },
            new[] { new TransitionDescriptor(transition) },
            structure.Generation,
            playerNumber: 1);
    }

    /// <summary>Creates explicit transition timing for timeline paint tests.</summary>
    private static TransitionRepertoire Transition(int runwayBeats, int tailBeats)
    {
        return TransitionRepertoire.FromRunwayAndTail(
            runwayBeats,
            tailBeats,
            TransitionShape.Blend,
            TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
    }

    /// <summary>Counts cells carrying one timeline flag across every row.</summary>
    private static int CountFlags(
        IReadOnlyList<CueSheetGridRow> rows,
        CueSheetBeatMark flag)
    {
        return rows.SelectMany(row => row.Cells).Count(cell => (cell & flag) != 0);
    }
}
