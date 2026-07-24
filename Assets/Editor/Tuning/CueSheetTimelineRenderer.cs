// IMGUI drawing for the Cue Sheet tracker view. Thin: every decision lives in the
// CueSheetTimeline builder or the owning window; this file only turns rows into pixels.
#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws the tracker rows built by <see cref="CueSheetTimeline"/>. Two colour channels that never
/// compete: structure owns hue at low chroma (the phrase wash), cue mechanics own saturation and
/// always paint on top, and the playhead is the single brightest thing on screen. A pending mark
/// draws hollow and a fired mark solid, so a hollow cell behind the playhead reads as a missed fire.
/// </summary>
internal static class CueSheetTimelineRenderer
{
    /// <summary>Height of one Grid row, shared with the auto-scroll math.</summary>
    public const float RowHeight = 18f;

    private const int Columns = TrackCueSheet.GridBeats;

    /// <summary>Right-aligned gutter showing each Grid's first beat number.</summary>
    private const float BeatGutterWidth = 44f;

    /// <summary>Continuous phrase ribbon down the left edge of the cells.</summary>
    private const float PhraseBarWidth = 6f;

    /// <summary>Right column carrying the row's cue identity.</summary>
    private const float CueColumnWidth = 180f;

    private const float CellGap = 1f;

    /// <summary>Extra seam every four cells so bars read.</summary>
    private const float BarSeamWidth = 3f;

    /// <summary>Cells expand with the window but never shrink past this; the scroll view takes over.</summary>
    private const float MinCellWidth = 14f;

    // Phrase field: one wash per PhraseType member, so the vocabulary is covered exhaustively and
    // Unknown means genuinely unknown rather than "unmapped". These stay low-chroma on purpose —
    // structure owns hue, cue mechanics own saturation, and the mechanics must always win on top.
    // Hues are chosen so the adjacencies a track actually produces stay distinct: Verse (teal) into
    // Up (olive, climbing) into Drop (red, peak) into Down (violet, suspended).
    private static readonly Color IntroPhrase = new(0.13f, 0.16f, 0.22f);
    private static readonly Color VersePhrase = new(0.13f, 0.20f, 0.21f);
    private static readonly Color UpPhrase = new(0.20f, 0.22f, 0.12f);
    private static readonly Color DownPhrase = new(0.19f, 0.14f, 0.23f);
    private static readonly Color ChorusPhrase = new(0.22f, 0.19f, 0.13f);
    private static readonly Color BridgePhrase = new(0.15f, 0.15f, 0.24f);
    private static readonly Color DropPhrase = new(0.26f, 0.13f, 0.15f);
    private static readonly Color OutroPhrase = new(0.16f, 0.17f, 0.19f);
    private static readonly Color UnknownPhrase = new(0.12f, 0.13f, 0.16f);

    // Cue mechanics: the retired LiveTimelineRenderer's exact identities, kept so muscle memory
    // survives. Anchor landings take the orange freed by the retired Lock Point.
    private static readonly Color RunwayFill = new(1f, 0.35f, 0.85f);
    private static readonly Color ImpactFill = new(0.25f, 0.95f, 1f);
    private static readonly Color AnchorRing = new(1f, 0.55f, 0.2f);
    private static readonly Color TailFill = new(0.25f, 0.8f, 0.45f);
    private static readonly Color PlayheadFill = new(1f, 0.9f, 0.35f);
    private static readonly Color PlayheadOutline = Color.white;
    private static readonly Color CellText = new(0.95f, 0.95f, 0.95f);

    /// <summary>Cached right-aligned style for the beat gutter.</summary>
    private static GUIStyle? gutterStyle;

    /// <summary>Cached centered style for the column header numbers.</summary>
    private static GUIStyle? headerStyle;

    /// <summary>Cached left-aligned style for the cue column.</summary>
    private static GUIStyle? cueStyle;

    /// <summary>Column header labels 1..16, cached so repaints allocate nothing.</summary>
    private static readonly string[] ColumnLabels = BuildColumnLabels();

    /// <summary>
    /// Draws the 16-column beat header. Drawn by the window outside the row scroll view so it
    /// stays visible, using the same width math as the rows so the numbers sit over their cells.
    /// </summary>
    public static void DrawHeader(float viewWidth)
    {
        EnsureStyles();
        var cellWidth = CellWidthFor(viewWidth);
        var rect = GUILayoutUtility.GetRect(TotalWidthFor(cellWidth), RowHeight);
        for (var column = 0; column < Columns; column++)
        {
            GUI.Label(CellRect(rect, cellWidth, column), ColumnLabels[column], headerStyle);
        }
    }

    /// <summary>Draws every Grid row; catalog names resolve the cue column's indexes.</summary>
    public static void DrawRows(
        IReadOnlyList<CueSheetGridRow> rows,
        IReadOnlyList<string> effectNames,
        IReadOnlyList<string> transitionNames,
        float viewWidth)
    {
        EnsureStyles();
        var cellWidth = CellWidthFor(viewWidth);
        var totalWidth = TotalWidthFor(cellWidth);
        for (var i = 0; i < rows.Count; i++)
        {
            // Bare GetRect calls stack with no spacing, so the phrase ribbon runs unbroken.
            var rect = GUILayoutUtility.GetRect(totalWidth, RowHeight);
            DrawRow(rect, rows[i], cellWidth, effectNames, transitionNames);
        }
    }

    /// <summary>
    /// Keeps the playhead row visible without fighting the user: the scroll moves only when the
    /// playhead has just entered a different row and that row sits outside the visible span, so a
    /// reader parked elsewhere is never yanked back mid-thought.
    /// </summary>
    public static Vector2 AutoScroll(Vector2 scroll, int? playheadRow, int? previousPlayheadRow, float viewHeight)
    {
        if (playheadRow is not { } row || row == previousPlayheadRow || viewHeight <= 0f)
        {
            return scroll;
        }

        var top = row * RowHeight;
        if (top >= scroll.y && top + RowHeight <= scroll.y + viewHeight)
        {
            return scroll;
        }

        scroll.y = Mathf.Max(0f, top - ((viewHeight - RowHeight) * 0.5f));
        return scroll;
    }

    /// <summary>Draws one row: gutter, phrase ribbon, sixteen cells, and the cue column.</summary>
    private static void DrawRow(
        Rect rect,
        CueSheetGridRow row,
        float cellWidth,
        IReadOnlyList<string> effectNames,
        IReadOnlyList<string> transitionNames)
    {
        GUI.Label(new Rect(rect.x, rect.y, BeatGutterWidth - 4f, rect.height), row.FirstBeat.ToString(), gutterStyle);

        // Full row height with no vertical inset, so consecutive rows read as one continuous ribbon.
        var bar = new Rect(rect.x + BeatGutterWidth, rect.y, PhraseBarWidth, rect.height);
        EditorGUI.DrawRect(bar, PhraseColor(row.CellPhrases[0]));

        for (var column = 0; column < Columns; column++)
        {
            DrawCell(CellRect(rect, cellWidth, column), row.Cells[column], PhraseColor(row.CellPhrases[column]), row.CueFired);
        }

        var cue = new Rect(rect.xMax - CueColumnWidth, rect.y, CueColumnWidth, rect.height);
        GUI.Label(cue, CueLabel(row, effectNames, transitionNames), cueStyle);
    }

    /// <summary>
    /// Draws one cell in the settled order: phrase field, Runway fill, Impact (solid when fired,
    /// a hollow ring while pending), Anchor ring, Tail underline — an underline rather than a
    /// fill so a Tail running into the next mark's Runway still reads as two things — then the
    /// playhead on top of everything.
    /// </summary>
    private static void DrawCell(Rect cell, CueSheetBeatMark marks, Color phrase, bool fired)
    {
        EditorGUI.DrawRect(cell, phrase);
        if ((marks & CueSheetBeatMark.Runway) != 0)
        {
            EditorGUI.DrawRect(cell, RunwayFill);
        }

        if ((marks & CueSheetBeatMark.Impact) != 0)
        {
            if (fired)
            {
                EditorGUI.DrawRect(cell, ImpactFill);
            }
            else
            {
                DrawRing(cell, 2f, ImpactFill);
            }
        }

        if ((marks & CueSheetBeatMark.AnchorLanding) != 0)
        {
            DrawRing(cell, 2f, AnchorRing);
        }

        if ((marks & CueSheetBeatMark.Tail) != 0)
        {
            EditorGUI.DrawRect(new Rect(cell.x, cell.yMax - 4f, cell.width, 4f), TailFill);
        }

        if ((marks & CueSheetBeatMark.Playhead) != 0)
        {
            EditorGUI.DrawRect(cell, PlayheadFill);
            DrawRing(cell, 1f, PlayheadOutline);
        }
    }

    /// <summary>
    /// The cue column's text: <c>Effect ▸ Transition</c> for the row's mark with a tick once
    /// fired, <c>Effect ▸ ride-through</c> for a ride-through Anchor, and on cue-less rows the
    /// phrase name where a phrase starts — drawn once, never repeated down the phrase.
    /// </summary>
    private static string CueLabel(
        CueSheetGridRow row,
        IReadOnlyList<string> effectNames,
        IReadOnlyList<string> transitionNames)
    {
        if (row.CueEffectIndex is not { } effectIndex)
        {
            return row.PhraseStart is { } start ? PhraseName(start) : string.Empty;
        }

        var effect = NameOf(effectNames, effectIndex);
        var transition = row.CueIsRideThrough
            ? "ride-through"
            : NameOf(transitionNames, row.CueTransitionIndex ?? -1);
        return row.CueFired ? $"{effect} ▸ {transition}  ✓" : $"{effect} ▸ {transition}";
    }

    /// <summary>Resolves a catalog index against the given names, degrading to the raw index.</summary>
    private static string NameOf(IReadOnlyList<string> names, int index)
    {
        return index >= 0 && index < names.Count ? names[index] : $"#{index}";
    }

    /// <summary>The phrase-type name as the wire spells it: lowercase.</summary>
    private static string PhraseName(PhraseType type)
    {
        return type switch
        {
            PhraseType.Intro => "intro",
            PhraseType.Up => "up",
            PhraseType.Down => "down",
            PhraseType.Verse => "verse",
            PhraseType.Bridge => "bridge",
            PhraseType.Chorus => "chorus",
            PhraseType.Outro => "outro",
            PhraseType.Drop => "drop",
            _ => "unknown",
        };
    }

    /// <summary>The phrase field wash for one covering phrase type; see the table note above.</summary>
    private static Color PhraseColor(PhraseType type)
    {
        return type switch
        {
            PhraseType.Intro => IntroPhrase,
            PhraseType.Verse => VersePhrase,
            PhraseType.Up => UpPhrase,
            PhraseType.Down => DownPhrase,
            PhraseType.Chorus => ChorusPhrase,
            PhraseType.Bridge => BridgePhrase,
            PhraseType.Drop => DropPhrase,
            PhraseType.Outro => OutroPhrase,
            _ => UnknownPhrase,
        };
    }

    /// <summary>Hollow rectangle drawn as four edge strips.</summary>
    private static void DrawRing(Rect rect, float thickness, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + thickness, thickness, rect.height - (2f * thickness)), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y + thickness, thickness, rect.height - (2f * thickness)), color);
    }

    /// <summary>Cell width for the current view width: expands to fill, clamped to the minimum.</summary>
    private static float CellWidthFor(float viewWidth)
    {
        var fixedWidth = BeatGutterWidth + PhraseBarWidth + CueColumnWidth
            + ((Columns - 1) * CellGap) + (3f * BarSeamWidth);
        return Mathf.Max(MinCellWidth, (viewWidth - fixedWidth) / Columns);
    }

    /// <summary>Total row width for a cell width; rows wider than the view horizontally scroll.</summary>
    private static float TotalWidthFor(float cellWidth)
    {
        return BeatGutterWidth + PhraseBarWidth + (Columns * cellWidth)
            + ((Columns - 1) * CellGap) + (3f * BarSeamWidth) + CueColumnWidth;
    }

    /// <summary>One cell's rect: 1px gap between cells, a 3px seam every four, 1px row inset.</summary>
    private static Rect CellRect(Rect row, float cellWidth, int column)
    {
        var x = row.x + BeatGutterWidth + PhraseBarWidth
            + (column * (cellWidth + CellGap))
            + (column / 4 * BarSeamWidth);
        return new Rect(x, row.y + 1f, cellWidth, row.height - 2f);
    }

    /// <summary>Creates the cached styles once per Editor session.</summary>
    private static void EnsureStyles()
    {
        gutterStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = CellText },
        };
        headerStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = CellText },
        };
        cueStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = { left = 6 },
            normal = { textColor = CellText },
        };
    }

    /// <summary>Builds the cached 1..16 header labels.</summary>
    private static string[] BuildColumnLabels()
    {
        var labels = new string[Columns];
        for (var column = 0; column < Columns; column++)
        {
            labels[column] = (column + 1).ToString();
        }

        return labels;
    }
}
