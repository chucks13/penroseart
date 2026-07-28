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
        // A drop landing pins a Cue Mark on that Grid Boundary whatever the cadence rolls, which is how these
        // paint tests get a mark at a known beat now that no mark is forced onto a Phrase end.
        var structure = Structure(
            32,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Drop, dropLandingBeat: 17));
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
        var structure = Structure(
            32,
            Phrase(1, 19, PhraseType.Intro),
            Phrase(20, 32, PhraseType.Drop, dropLandingBeat: 20));
        var transition = Transition(runwayBeats: 5, tailBeats: 0);
        var sheet = Sheet(structure, transition);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        // The 19-beat Intro lays a full row (1-16) and a short row (17-19); the pinned mark at 20 opens the
        // Drop row. Its five-beat Runway (15-19) crosses both Intro rows on the way in.
        Assert.That(rows[0].Cells[14], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[0].Cells[15], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[1].Cells[2], Is.EqualTo(CueSheetBeatMark.Runway));
        Assert.That(rows[2].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
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
        // Pins one Grid apart — the cadence floor — so both survive; anything closer drops the later pin.
        var structure = Structure(
            48,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Verse, dropLandingBeat: 17),
            Phrase(33, 48, PhraseType.Chorus, dropLandingBeat: 33));
        var transition = Transition(0, 0);
        var sheet = Sheet(structure, transition);

        var rows = CueSheetTimeline.Build(sheet, structure, new[] { transition }, null);

        Assert.That(CueSheetTimeline.RowContaining(rows, 17), Is.EqualTo(1));
        Assert.That(CueSheetTimeline.RowContaining(rows, 33), Is.EqualTo(2));
        Assert.That(rows[1].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
        Assert.That(rows[2].Cells[0], Is.EqualTo(CueSheetBeatMark.Impact));
    }

    /// <summary>
    /// Pins that cell flags compose rather than overwrite. The Playhead is the one layer that can land on
    /// any painted beat; Tail-meets-Runway is no longer reachable, because consecutive marks sit at least
    /// one Grid apart while a Transition's whole Duration is capped at twelve beats.
    /// </summary>
    [Test]
    public void ImpactAndPlayheadCanShareOneCell()
    {
        var structure = Structure(
            32,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Drop, dropLandingBeat: 17));
        var transition = Transition(runwayBeats: 4, tailBeats: 4);
        var sheet = Sheet(structure, transition);

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
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Verse, dropLandingBeat: 17),
            Phrase(33, 48, PhraseType.Chorus, dropLandingBeat: 33));
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
        // The Intro runs a full four Grids so the cadence ceiling guarantees a carrier mark ahead of the fill
        // Anchor at beat 81; without an incumbent there is nothing to ride through. The Anchor's row is index
        // five: four Intro rows, then the Up row, then the Chorus row it lands on.
        var rideStructure = Structure(
            96,
            Phrase(1, 64, PhraseType.Intro),
            Phrase(65, 80, PhraseType.Up, fillStartBeat: 1),
            Phrase(81, 96, PhraseType.Chorus));
        var rideSheet = Sheet(rideStructure, transition, Repertoire.HandlesFill);

        var rideRows = CueSheetTimeline.Build(rideSheet, rideStructure, new[] { transition }, null);

        Assert.That(
            rideRows[5].Cells[0] & CueSheetBeatMark.AnchorLanding,
            Is.EqualTo(CueSheetBeatMark.AnchorLanding));
        Assert.That(rideRows[5].CueEffectIndex, Is.EqualTo(0));
        Assert.That(rideRows[5].CueTransitionIndex, Is.Null);
        Assert.That(rideRows[5].CueIsRideThrough, Is.True);

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

        // Drop landings pin the marks these layers hang off, since no mark is forced onto a Phrase end any
        // more; each Phrase map deliberately runs past its announced total_beats so the extension
        // is what the row count is measuring.
        var markStructure = Structure(
            16,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Drop, dropLandingBeat: 17));
        var markSheet = Sheet(markStructure, hardCut);
        Assert.That(
            CueSheetTimeline.Build(markSheet, markStructure, new[] { hardCut }, null).Count,
            Is.EqualTo(2));

        // The pinned mark at 17 sits on the short final Phrase, so its four-beat Tail (18-21) runs past both
        // the Phrase end and total_beats; the last row stretches to cover it.
        var tailStructure = Structure(
            20,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 20, PhraseType.Drop, dropLandingBeat: 17));
        var tailedTransition = Transition(0, 4);
        var tailSheet = Sheet(tailStructure, tailedTransition);
        Assert.That(
            CueSheetTimeline.Build(tailSheet, tailStructure, new[] { tailedTransition }, null).Count,
            Is.EqualTo(2));

        // Four Intro Grids so the cadence ceiling guarantees the carrier the ride-through Anchor needs; the
        // fill Anchor lands on beat 81 — one past the final Phrase — so the rows extend one cell to show it.
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
            32,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Drop, dropLandingBeat: 17));
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
        var structure = Structure(
            32,
            Phrase(1, 16, PhraseType.Intro),
            Phrase(17, 32, PhraseType.Drop, dropLandingBeat: 17));
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
