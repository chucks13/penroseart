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
/// One Grid row of the tracker. Column position is beat-in-Grid, so Grid adherence is structural:
/// downbeats align vertically and a boundary reads as a column. A row never straddles a phrase —
/// the Grid restarts at every phrase — so the row carries one phrase and holds at most
/// <see cref="TrackCueSheet.GridBeats"/> cells, fewer on a phrase's short final Grid. Cue Marks sit
/// only on Grid Boundaries and every row begins on one, so at most one Cue Mark falls in a row and
/// the cue identity is row-level data.
/// </summary>
public sealed class CueSheetGridRow
{
    /// <summary>The row's own cells, mutable for the builder's painting passes.</summary>
    private readonly CueSheetBeatMark[] cells;

    /// <summary>Opens an unpainted row; only <see cref="CueSheetTimeline"/> lays these out.</summary>
    internal CueSheetGridRow(int firstBeat, int length, PhraseType phrase, PhraseType? phraseStart)
    {
        FirstBeat = firstBeat;
        cells = new CueSheetBeatMark[length];
        Phrase = phrase;
        PhraseStart = phraseStart;
    }

    /// <summary>Absolute one-based track beat of this row's first column.</summary>
    public int FirstBeat { get; }

    /// <summary>
    /// What each of the row's cells presents. One entry per beat the row holds: a full Grid, or the
    /// remainder on a phrase's last row.
    /// </summary>
    public IReadOnlyList<CueSheetBeatMark> Cells => cells;

    /// <summary>The phrase this row's beats belong to; rows never cross a phrase boundary.</summary>
    public PhraseType Phrase { get; }

    /// <summary>
    /// Set only on a phrase's first row, so its name is drawn once and never repeats down a long
    /// phrase; null everywhere else, including rows running past the last phrase.
    /// </summary>
    public PhraseType? PhraseStart { get; }

    /// <summary>
    /// Effect catalog index for the row's cue identity: the mark's Effect, or on an Anchor-landing
    /// row the capable Effect already on the wall for the moment. Null when the row presents no cue.
    /// </summary>
    public int? CueEffectIndex { get; internal set; }

    /// <summary>Transition catalog index of the row's Cue Mark; null on Anchor-landing and empty rows.</summary>
    public int? CueTransitionIndex { get; internal set; }

    /// <summary>Whether the row's cue identity is an Anchor's riding Effect rather than a Cue Mark.</summary>
    public bool CueIsRideThrough { get; internal set; }

    /// <summary>
    /// Whether the row's Cue Mark has been checked off by the Switcher. A pending mark draws
    /// hollow; a pending mark behind the Playhead is the missed fire the view exists to expose.
    /// </summary>
    public bool CueFired { get; internal set; }

    /// <summary>Whether an absolute one-based track beat falls in this row.</summary>
    public bool Contains(int beat)
    {
        return beat >= FirstBeat && beat < FirstBeat + cells.Length;
    }

    /// <summary>Ors a flag into the cell holding a beat this row contains.</summary>
    internal void Paint(int beat, CueSheetBeatMark flag)
    {
        cells[beat - FirstBeat] |= flag;
    }
}

/// <summary>
/// Builds the Cue Sheet tracker rows: the track's Grid rows with the sheet's Cue Marks, Anchor
/// landings, transition Runways/Tails and the playhead painted into whichever row each beat falls
/// in. Rows follow the Grid the runtime actually delivers, which restarts at every phrase, so a
/// phrase that is not a whole number of Grids ends on a short row instead of pushing every later
/// row out of step with the wall. Pure and deterministic; degrades quietly on missing or
/// inconsistent inputs rather than throwing, because it runs during live performance debugging and
/// must never take the window down.
/// </summary>
public static class CueSheetTimeline
{
    /// <summary>
    /// Hard row ceiling protecting the view from a garbage total-beats value; generous next to a
    /// real track's few hundred Grids.
    /// </summary>
    private const int MaxRows = 4096;

    private static readonly IReadOnlyList<CueSheetGridRow> NoRows = Array.Empty<CueSheetGridRow>();

    /// <summary>
    /// Zero-based row holding an absolute one-based track beat, or -1 when no row does. Row spans
    /// are layout rather than arithmetic — the Grid restarts at every phrase — so this searches the
    /// built rows instead of dividing the beat.
    /// </summary>
    public static int RowContaining(IReadOnlyList<CueSheetGridRow> rows, int beat)
    {
        for (var row = 0; row < rows.Count; row++)
        {
            if (rows[row].Contains(beat))
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>
    /// Builds the full track's rows for one sheet. Fired state is read off each
    /// <see cref="CuePlanMark.Fired"/>. <paramref name="transitions"/> is the Transition catalog's
    /// repertoires by catalog index, used only for Runway and Tail lengths. A default sheet, an empty
    /// structure, or a null <paramref name="currentBeat"/> each just leave their layer out of the rows.
    /// </summary>
    public static IReadOnlyList<CueSheetGridRow> Build(
        TrackCueSheet sheet,
        StructureValues structure,
        IReadOnlyList<TransitionRepertoire>? transitions,
        int? currentBeat)
    {
        // A default (no-plan) sheet exposes null lists; the view treats that as an empty plan.
        var marks = sheet.Marks ?? Array.Empty<CuePlanMark>();
        var anchors = sheet.Anchors ?? Array.Empty<AnchorResolution>();
        transitions ??= Array.Empty<TransitionRepertoire>();

        var rows = Layout(
            LastBeat(structure, marks, anchors, transitions, currentBeat),
            structure.Phrases);
        if (rows.Count == 0)
        {
            return NoRows;
        }

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
                Paint(rows, beat, CueSheetBeatMark.Runway);
            }

            Paint(rows, mark.Beat, CueSheetBeatMark.Impact);
            for (var beat = mark.Beat + 1; beat <= mark.Beat + tail; beat++)
            {
                Paint(rows, beat, CueSheetBeatMark.Tail);
            }

            var row = RowContaining(rows, mark.Beat);
            if (row >= 0)
            {
                rows[row].CueEffectIndex = mark.EffectIndex;
                rows[row].CueTransitionIndex = mark.TransitionIndex;
                rows[row].CueFired = mark.Fired;
            }
        }

        for (var i = 0; i < anchors.Count; i++)
        {
            var anchor = anchors[i];
            Paint(rows, anchor.LandingBeat, CueSheetBeatMark.AnchorLanding);

            // An owned landing carries no Cue Mark, so the Effect on the wall for the moment is the
            // row's cue identity; a real mark keeps priority if inconsistent inputs put one there.
            var row = RowContaining(rows, anchor.LandingBeat);
            if (row >= 0 && rows[row].CueEffectIndex is null)
            {
                rows[row].CueEffectIndex = anchor.EffectIndex;
                rows[row].CueIsRideThrough = true;
            }
        }

        if (currentBeat is { } playhead)
        {
            Paint(rows, playhead, CueSheetBeatMark.Playhead);
        }

        return rows;
    }

    /// <summary>
    /// Lays out the track's rows phrase by phrase, because the Grid restarts at every phrase. A
    /// phrase owns beats from its start until the next phrase starts, so a phrase shorter than a
    /// Grid gets one short row and the next phrase still begins on column one. Out-of-order or
    /// duplicate phrase starts are absorbed rather than trusted; beats before the first phrase or
    /// past the last are laid out as unnamed rows.
    /// </summary>
    private static List<CueSheetGridRow> Layout(int lastBeat, IReadOnlyList<StructurePhraseValues> phrases)
    {
        var rows = new List<CueSheetGridRow>();
        var beat = 1;
        if (phrases.Count > 0 && phrases[0].StartBeat > beat)
        {
            AddRows(rows, beat, Math.Min(phrases[0].StartBeat - 1, lastBeat), phrases[0].Type, named: false);
            beat = phrases[0].StartBeat;
        }

        for (var i = 0; i < phrases.Count && beat <= lastBeat; i++)
        {
            // The last phrase absorbs anything running past the structure, so an overrunning mark or
            // playhead keeps marching in Grids from that phrase's start rather than starting over.
            var end = i + 1 < phrases.Count ? Math.Min(phrases[i + 1].StartBeat - 1, lastBeat) : lastBeat;
            if (end < beat)
            {
                continue;
            }

            AddRows(rows, beat, end, phrases[i].Type, named: true);
            beat = end + 1;
        }

        if (beat <= lastBeat)
        {
            AddRows(rows, beat, lastBeat, PhraseType.Unknown, named: false);
        }

        return rows;
    }

    /// <summary>
    /// Breaks one phrase's beat span into Grid rows, the last one short when the span is not a whole
    /// number of Grids. Names the first row when the span is a phrase's own. Stops at
    /// <see cref="MaxRows"/>.
    /// </summary>
    private static void AddRows(
        List<CueSheetGridRow> rows,
        int firstBeat,
        int lastBeat,
        PhraseType phrase,
        bool named)
    {
        for (var beat = firstBeat; beat <= lastBeat && rows.Count < MaxRows; beat += TrackCueSheet.GridBeats)
        {
            var length = Math.Min(TrackCueSheet.GridBeats, lastBeat - beat + 1);
            rows.Add(new CueSheetGridRow(beat, length, phrase, named && beat == firstBeat ? phrase : null));
        }
    }

    /// <summary>
    /// The last beat the rows must reach: the structure's full length plus any mark, Tail, Anchor,
    /// or playhead running past it. Zero or less means there is nothing to present.
    /// </summary>
    private static int LastBeat(
        StructureValues structure,
        IReadOnlyList<CuePlanMark> marks,
        IReadOnlyList<AnchorResolution> anchors,
        IReadOnlyList<TransitionRepertoire> transitions,
        int? currentBeat)
    {
        var phrases = structure.Phrases;
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

        return lastBeat;
    }

    /// <summary>Ors a flag into the cell holding an absolute beat; out-of-range beats are ignored.</summary>
    private static void Paint(IReadOnlyList<CueSheetGridRow> rows, int beat, CueSheetBeatMark flag)
    {
        var row = RowContaining(rows, beat);
        if (row >= 0)
        {
            rows[row].Paint(beat, flag);
        }
    }
}
