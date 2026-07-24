// Pure row model and builder for the Cue Sheet tracker view. No UnityEngine or UnityEditor
// dependency: everything here is plain data, deterministic, and testable without an Editor.
#nullable enable

using System;
using System.Collections.Generic;

/// <summary>
/// What one tracker cell presents. Flags, because a beat genuinely can be several things at once —
/// a Tail underline can run under the next mark's Runway, and the Playhead can sit on any of them.
/// </summary>
[Flags]
public enum CueSheetBeatMark
{
    /// <summary>Nothing scheduled on this beat; only the phrase field shows.</summary>
    None = 0,

    /// <summary>Beat inside a transition's Runway — the approach between fire and Impact Point.</summary>
    Runway = 1 << 0,

    /// <summary>A Cue Mark's landing beat, where the transition's Impact Point falls.</summary>
    Impact = 1 << 1,

    /// <summary>An Anchor's landing beat — a protected drop or fill moment the plan owns.</summary>
    AnchorLanding = 1 << 2,

    /// <summary>Beat inside a transition's Tail — intentional continuation after the Impact Point.</summary>
    Tail = 1 << 3,

    /// <summary>The sheet player's current beat.</summary>
    Playhead = 1 << 4,
}

/// <summary>
/// One 16-beat Grid row of the tracker. Column position is beat-in-Grid, so Grid adherence is
/// structural: downbeats align vertically down the whole track and an off-Grid mark reads as a
/// horizontal offset from its neighbours. At most one Cue Mark falls in a row
/// (<see cref="TrackCueSheet.MinimumGapBeats"/> equals <see cref="TrackCueSheet.GridBeats"/>),
/// so the cue identity is row-level data.
/// </summary>
public sealed class CueSheetGridRow
{
    /// <summary>Captures one built row; only <see cref="CueSheetTimeline.Build"/> assembles these.</summary>
    internal CueSheetGridRow(
        int firstBeat,
        CueSheetBeatMark[] cells,
        PhraseType[] cellPhrases,
        PhraseType? phraseStart,
        int? cueEffectIndex,
        int? cueTransitionIndex,
        bool cueIsRideThrough,
        bool cueFired)
    {
        FirstBeat = firstBeat;
        Cells = cells;
        CellPhrases = cellPhrases;
        PhraseStart = phraseStart;
        CueEffectIndex = cueEffectIndex;
        CueTransitionIndex = cueTransitionIndex;
        CueIsRideThrough = cueIsRideThrough;
        CueFired = cueFired;
    }

    /// <summary>Absolute one-based track beat of this row's first column.</summary>
    public int FirstBeat { get; }

    /// <summary>What each of the row's <see cref="TrackCueSheet.GridBeats"/> cells presents.</summary>
    public IReadOnlyList<CueSheetBeatMark> Cells { get; }

    /// <summary>
    /// The phrase type covering each cell's beat, so a phrase boundary inside a row changes the
    /// wash mid-row instead of lying until the next row.
    /// </summary>
    public IReadOnlyList<PhraseType> CellPhrases { get; }

    /// <summary>
    /// Set only on the row where a phrase starts, so its name is drawn once and never repeats
    /// down a long phrase; null everywhere else.
    /// </summary>
    public PhraseType? PhraseStart { get; }

    /// <summary>
    /// Effect catalog index for the row's cue identity: the mark's Effect, or the riding Effect
    /// on a ride-through Anchor row. Null when the row presents no cue.
    /// </summary>
    public int? CueEffectIndex { get; }

    /// <summary>Transition catalog index of the row's Cue Mark; null on ride-through and empty rows.</summary>
    public int? CueTransitionIndex { get; }

    /// <summary>Whether the row's cue identity is a ride-through Anchor rather than a Cue Mark.</summary>
    public bool CueIsRideThrough { get; }

    /// <summary>
    /// Whether the row's Cue Mark has been checked off by the Switcher. A pending mark draws
    /// hollow; a pending mark behind the Playhead is the missed fire the view exists to expose.
    /// </summary>
    public bool CueFired { get; }
}

/// <summary>
/// Builds the Cue Sheet tracker rows: one row per 16-beat Grid over the whole track, with the
/// sheet's Cue Marks, Anchor landings, transition Runways/Tails, phrase coverage, and the
/// playhead painted into whichever row each beat falls in. Pure and deterministic; degrades
/// quietly on missing or inconsistent inputs rather than throwing, because it runs during live
/// performance debugging and must never take the window down.
/// </summary>
public static class CueSheetTimeline
{
    /// <summary>
    /// Hard row ceiling protecting the view from a garbage total-beats value; generous next to a
    /// real track's few hundred Grids.
    /// </summary>
    private const int MaxRows = 4096;

    private static readonly IReadOnlyList<CueSheetGridRow> NoRows = Array.Empty<CueSheetGridRow>();

    /// <summary>Zero-based Grid row containing an absolute one-based track beat.</summary>
    public static int RowContaining(int beat)
    {
        return (beat - 1) / TrackCueSheet.GridBeats;
    }

    /// <summary>
    /// Builds the full track's rows for one sheet. <paramref name="firedMarks"/> parallels
    /// <see cref="TrackCueSheet.Marks"/> by index; missing or short lists read as not fired.
    /// <paramref name="transitions"/> is the Transition catalog's repertoires by catalog index,
    /// used only for Runway and Tail lengths. A default sheet, an empty structure, or a null
    /// <paramref name="currentBeat"/> each just leave their layer out of the rows.
    /// </summary>
    public static IReadOnlyList<CueSheetGridRow> Build(
        TrackCueSheet sheet,
        IReadOnlyList<bool>? firedMarks,
        StructureValues structure,
        IReadOnlyList<TransitionRepertoire>? transitions,
        int? currentBeat)
    {
        // A default (no-plan) sheet exposes null lists; the view treats that as an empty plan.
        var marks = sheet.Marks ?? Array.Empty<CuePlanMark>();
        var anchors = sheet.Anchors ?? Array.Empty<AnchorResolution>();
        var phrases = structure.Phrases;
        transitions ??= Array.Empty<TransitionRepertoire>();

        var rowCount = RowCount(structure, phrases, marks, anchors, transitions, currentBeat);
        if (rowCount <= 0)
        {
            return NoRows;
        }

        const int columns = TrackCueSheet.GridBeats;
        var cells = new CueSheetBeatMark[rowCount][];
        var cellPhrases = new PhraseType[rowCount][];
        for (var row = 0; row < rowCount; row++)
        {
            cells[row] = new CueSheetBeatMark[columns];
            cellPhrases[row] = new PhraseType[columns];
        }

        PaintPhrases(cellPhrases, phrases);

        var phraseStart = new PhraseType?[rowCount];
        for (var i = 0; i < phrases.Count; i++)
        {
            var row = RowContaining(phrases[i].StartBeat);
            if (row >= 0 && row < rowCount && phraseStart[row] is null)
            {
                phraseStart[row] = phrases[i].Type;
            }
        }

        var cueEffect = new int?[rowCount];
        var cueTransition = new int?[rowCount];
        var cueRideThrough = new bool[rowCount];
        var cueFired = new bool[rowCount];

        // Paint each mark's Runway, Impact, and Tail into whichever row each beat falls in, so a
        // Runway straddling a Grid boundary paints into both rows without special cases.
        for (var i = 0; i < marks.Count; i++)
        {
            var mark = marks[i];
            var runway = 0;
            var tail = 0;
            if (mark.TransitionIndex >= 0 && mark.TransitionIndex < transitions.Count)
            {
                var repertoire = transitions[mark.TransitionIndex];
                runway = repertoire.RunwayBeats;
                tail = repertoire.TailBeats;
            }

            for (var beat = mark.Beat - runway; beat < mark.Beat; beat++)
            {
                Paint(cells, beat, CueSheetBeatMark.Runway);
            }

            Paint(cells, mark.Beat, CueSheetBeatMark.Impact);
            for (var beat = mark.Beat + 1; beat <= mark.Beat + tail; beat++)
            {
                Paint(cells, beat, CueSheetBeatMark.Tail);
            }

            var row = RowContaining(mark.Beat);
            if (row >= 0 && row < rowCount)
            {
                cueEffect[row] = mark.EffectIndex;
                cueTransition[row] = mark.TransitionIndex;
                cueFired[row] = firedMarks != null && i < firedMarks.Count && firedMarks[i];
            }
        }

        for (var i = 0; i < anchors.Count; i++)
        {
            var anchor = anchors[i];
            Paint(cells, anchor.LandingBeat, CueSheetBeatMark.AnchorLanding);
            if (anchor.Treatment != AnchorTreatment.RideThrough)
            {
                continue;
            }

            // A ride-through suppresses its boundary Cue Mark, so the riding Effect is the row's
            // cue identity; a real mark elsewhere in the row keeps priority.
            var row = RowContaining(anchor.LandingBeat);
            if (row >= 0 && row < rowCount && cueEffect[row] is null)
            {
                cueEffect[row] = anchor.PerformerIndex;
                cueRideThrough[row] = true;
            }
        }

        if (currentBeat is { } playhead)
        {
            Paint(cells, playhead, CueSheetBeatMark.Playhead);
        }

        var rows = new CueSheetGridRow[rowCount];
        for (var row = 0; row < rowCount; row++)
        {
            rows[row] = new CueSheetGridRow(
                row * columns + 1,
                cells[row],
                cellPhrases[row],
                phraseStart[row],
                cueEffect[row],
                cueTransition[row],
                cueRideThrough[row],
                cueFired[row]);
        }

        return rows;
    }

    /// <summary>
    /// Rows needed to present everything the inputs mention: the structure's full length plus any
    /// mark, Tail, Anchor, or playhead running past it; clamped to <see cref="MaxRows"/>.
    /// </summary>
    private static int RowCount(
        StructureValues structure,
        IReadOnlyList<StructurePhraseValues> phrases,
        IReadOnlyList<CuePlanMark> marks,
        IReadOnlyList<AnchorResolution> anchors,
        IReadOnlyList<TransitionRepertoire> transitions,
        int? currentBeat)
    {
        var lastBeat = structure.TotalBeats ?? 0;
        for (var i = 0; i < phrases.Count; i++)
        {
            lastBeat = Math.Max(lastBeat, phrases[i].EndBeat);
        }

        for (var i = 0; i < marks.Count; i++)
        {
            var mark = marks[i];
            var tail = mark.TransitionIndex >= 0 && mark.TransitionIndex < transitions.Count
                ? transitions[mark.TransitionIndex].TailBeats
                : 0;
            lastBeat = Math.Max(lastBeat, mark.Beat + tail);
        }

        for (var i = 0; i < anchors.Count; i++)
        {
            lastBeat = Math.Max(lastBeat, anchors[i].LandingBeat);
        }

        if (currentBeat is { } beat)
        {
            lastBeat = Math.Max(lastBeat, beat);
        }

        if (lastBeat <= 0)
        {
            return 0;
        }

        var rowCount = (lastBeat + TrackCueSheet.GridBeats - 1) / TrackCueSheet.GridBeats;
        return Math.Min(rowCount, MaxRows);
    }

    /// <summary>
    /// Fills every cell's covering phrase type with <c>TrackCueSheet.PhraseTypeCovering</c>
    /// semantics: the last phrase starting at or before the beat covers it, beats before the
    /// first phrase take the first phrase, and beats past the last take the last. No phrases at
    /// all leaves every cell <see cref="PhraseType.Unknown"/>.
    /// </summary>
    private static void PaintPhrases(PhraseType[][] cellPhrases, IReadOnlyList<StructurePhraseValues> phrases)
    {
        if (phrases.Count == 0)
        {
            return;
        }

        var covering = 0;
        var beat = 1;
        for (var row = 0; row < cellPhrases.Length; row++)
        {
            var rowPhrases = cellPhrases[row];
            for (var column = 0; column < rowPhrases.Length; column++, beat++)
            {
                while (covering + 1 < phrases.Count && phrases[covering + 1].StartBeat <= beat)
                {
                    covering++;
                }

                rowPhrases[column] = phrases[covering].Type;
            }
        }
    }

    /// <summary>Ors a flag into the cell holding an absolute beat; out-of-range beats are ignored.</summary>
    private static void Paint(CueSheetBeatMark[][] cells, int beat, CueSheetBeatMark flag)
    {
        if (beat < 1)
        {
            return;
        }

        var row = RowContaining(beat);
        if (row >= cells.Length)
        {
            return;
        }

        cells[row][(beat - 1) % TrackCueSheet.GridBeats] |= flag;
    }
}
