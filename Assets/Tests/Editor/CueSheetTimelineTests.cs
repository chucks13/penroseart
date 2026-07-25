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
        var structure = Structure(32, Phrase(1, 16, PhraseType.Intro));
        var transition = Transition(runwayBeats: 3, tailBeats: 2);
        var sheet = Sheet(structure, transition);

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
        var structure = Structure(32, Phrase(1, 19, PhraseType.Intro));
        var transition = Transition(runwayBeats: 5, tailBeats: 0);
        var sheet = Sheet(structure, transition);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(rows[0].Cells[14], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[0].Cells[15], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[1].Cells[2], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[1].Cells[3], Is.EqualTo(CueSheetBeatMark.Impact));
    }

    /// <summary>Pins beat-in-Grid column placement at both ends of a row.</summary>
    [Test]
    public void MarkColumnsUseBeatInGridPosition()
    {
        var structure = Structure(
            32,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 31, PhraseType.Verse));
        var transition = Transition(0, 0);
        var sheet = Sheet(structure, transition);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(CueSheetTimeline.RowContaining(rows, 17), Is.EqualTo(1));
        Assert.That(CueSheetTimeline.RowContaining(rows, 32), Is.EqualTo(1));
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
        Assert.That(rows[1].Cells[15], Is.EqualTo(CueSheetBeatMark.Impact));
    }

    /// <summary>Pins flag composition where one mark's Tail meets the next mark's Runway.</summary>
    [Test]
    public void TailAndRunwayCanShareOneCell()
    {
        var structure = Structure(
            32,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 24, PhraseType.Verse));
        var transition = Transition(runwayBeats: 4, tailBeats: 4);
        var sheet = Sheet(structure, transition);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(
            rows[1].Cells[4],
            Is.EqualTo(CueSheetBeatMark.Tail | CueSheetBeatMark.Runway));
    }

    /// <summary>Pins fired state as read off the cue itself, so the row shows what that cue actually did.</summary>
    [Test]
    public void CueFiredComesFromTheMarkItself()
    {
        var structure = Structure(
            48,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Verse));
        var transition = Transition(0, 0);
        var sheet = Sheet(structure, transition);

        var pendingRows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);
        Assert.That(pendingRows[1].CueFired, Is.False, "An unfired cue reads pending.");

        sheet.Marks[0].FiredAtBeat = sheet.Marks[0].Beat;
        var firedRows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(firedRows[1].CueFired, Is.True);
        Assert.That(firedRows[2].CueFired, Is.False, "Only the cue that fired reads fired.");
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

    /// <summary>Pins ride-through cue identity and a real Cue Mark's priority in the same row.</summary>
    [Test]
    public void RideThroughUsesTheRidingEffectUnlessARealMarkSharesTheRow()
    {
        var transition = Transition(0, 0);
        var rideStructure = Structure(
            48,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Up, fillStartBeat: 1));
        var rideSheet = Sheet(rideStructure, transition, Repertoire.HandlesFill);

        var rideRows = CueSheetTimeline.Build(rideSheet, rideStructure, new[] { transition }, null);

        Assert.That(
            rideRows[2].Cells[0] & CueSheetBeatMark.AnchorLanding,
            Is.EqualTo(CueSheetBeatMark.AnchorLanding));
        Assert.That(rideRows[2].CueEffectIndex, Is.EqualTo(0));
        Assert.That(rideRows[2].CueTransitionIndex, Is.Null);
        Assert.That(rideRows[2].CueIsRideThrough, Is.True);

        var priorityStructure = Structure(
            32,
            Phrase(1, 8, PhraseType.Intro),
            Phrase(9, 16, PhraseType.Up, fillStartBeat: 1),
            Phrase(17, 24, PhraseType.Chorus));
        var prioritySheet = Sheet(priorityStructure, transition, Repertoire.HandlesFill);

        var priorityRows = CueSheetTimeline.Build(
            prioritySheet,
            priorityStructure,
            new[] { transition },
            null);

        // Two eight-beat phrases take a short row each, so the third phrase's row is index 2.
        Assert.That(priorityRows[2].CueEffectIndex, Is.EqualTo(0));
        Assert.That(priorityRows[2].CueTransitionIndex, Is.EqualTo(0));
        Assert.That(priorityRows[2].CueIsRideThrough, Is.False);
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

        var markStructure = Structure(16, Phrase(1, 16, PhraseType.Intro));
        var markSheet = Sheet(markStructure, hardCut);
        Assert.That(
            CueSheetTimeline.Build(markSheet, markStructure, new[] { hardCut }, null).Count,
            Is.EqualTo(2));

        var tailStructure = Structure(16, Phrase(1, 15, PhraseType.Intro));
        var tailedTransition = Transition(0, 4);
        var tailSheet = Sheet(tailStructure, tailedTransition);
        Assert.That(
            CueSheetTimeline.Build(tailSheet, tailStructure, new[] { tailedTransition }, null).Count,
            Is.EqualTo(2));

        var anchorStructure = Structure(
            32,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Up, fillStartBeat: 1));
        var anchorSheet = Sheet(anchorStructure, hardCut, Repertoire.HandlesFill);
        Assert.That(
            CueSheetTimeline.Build(anchorSheet, anchorStructure, new[] { hardCut }, null).Count,
            Is.EqualTo(3));

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
        var structure = Structure(32, Phrase(1, 16, PhraseType.Intro));
        var transition = Transition(4, 4);
        var sheet = Sheet(structure, transition);

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
        var structure = Structure(32, Phrase(1, 16, PhraseType.Intro));
        var transition = Transition(3, 2);
        var sheet = Sheet(structure, transition);

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
            Repertoire.None,
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
