// Renders the live sequencing timeline projection in the Unity Editor.
#nullable enable

using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Draws responsive live sequencing timelines without interpreting runtime scheduling policy.</summary>
internal static class LiveTimelineRenderer
{
    /// <summary>Cached centered style for ordinary beat cells.</summary>
    private static GUIStyle? cellStyle;

    /// <summary>Cached centered style for Cue Mark and Impact Point beat cells.</summary>
    private static GUIStyle? markedCellStyle;

    /// <summary>Neutral Grid-cell fill.</summary>
    private static readonly Color BaseFill = new(0.12f, 0.13f, 0.16f);

    /// <summary>Runway fill and top-stripe color.</summary>
    private static readonly Color RunwayFill = new(0.14f, 0.31f, 0.50f);

    /// <summary>Tail fill and bottom-stripe color.</summary>
    private static readonly Color TailFill = new(0.38f, 0.18f, 0.46f);

    /// <summary>Current-beat fill and outline color.</summary>
    private static readonly Color CurrentFill = new(0.92f, 0.62f, 0.12f);

    /// <summary>High-contrast outline used to keep the current beat visible over its fill.</summary>
    private static readonly Color CurrentOutline = new(1f, 1f, 1f);

    /// <summary>High-contrast mark color for Cue and execution glyphs.</summary>
    private static readonly Color MarkColor = new(0.95f, 0.95f, 0.95f);

    /// <summary>Draws current and next Phrase plans with a shared legend and optional execution progress.</summary>
    public static void Draw(LiveTimelineModel model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        EnsureStyles();
        DrawLegend();

        if (!model.IsSynced)
        {
            EditorGUILayout.HelpBox(
                "Standalone Mode · live Grid and Transition timing unavailable.",
                MessageType.None);
        }
        else if (!model.CurrentPositionAvailable)
        {
            EditorGUILayout.HelpBox(
                "Current Grid/Phrase position unavailable.",
                MessageType.None);
        }

        if (model.HasLoadedCue && !model.LoadedCueTimingAvailable)
        {
            EditorGUILayout.HelpBox("Loaded Cue timing unavailable.", MessageType.None);
        }

        DrawPhrase("CURRENT CUE SHEET", model.Current);
        EditorGUILayout.Space(8f);
        DrawPhrase("NEXT CUE SHEET", model.Next);

        if (model.ExecutionProgress is { } progress)
        {
            EditorGUILayout.Space(8f);
            var rect = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.ProgressBar(rect, progress, $"Active Transition  {progress:P0}");
            EditorGUILayout.HelpBox(
                "Active Transition beat placement unavailable · Switcher reports progress only.",
                MessageType.None);
        }
    }

    /// <summary>Draws the visual vocabulary once, using both colors and text shapes.</summary>
    internal static void DrawLegend()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("▰ Runway", EditorStyles.miniLabel);
            GUILayout.Label("▱ Tail", EditorStyles.miniLabel);
            GUILayout.Label("◆ Cue Mark", EditorStyles.miniLabel);
            GUILayout.Label("● Loaded Cue", EditorStyles.miniLabel);
            GUILayout.Label("▣ Locked", EditorStyles.miniLabel);
            GUILayout.Label("□ Current", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
        }
    }

    /// <summary>Draws one available Phrase as consecutive full or partial Grid rows.</summary>
    internal static void DrawPhrase(string heading, LiveTimelinePhrase phrase)
    {
        EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        if (!phrase.IsAvailable)
        {
            EditorGUILayout.HelpBox("Cue Sheet unavailable.", MessageType.None);
            return;
        }

        var identity = string.IsNullOrWhiteSpace(phrase.Label)
            ? $"{phrase.LengthBeats} beats"
            : $"{phrase.Label}  ·  {phrase.LengthBeats} beats";
        EditorGUILayout.LabelField(identity, EditorStyles.miniBoldLabel);

        for (var blockIndex = 0; blockIndex < phrase.Blocks.Count; blockIndex++)
        {
            DrawBlock(blockIndex, phrase.Blocks[blockIndex]);
        }
    }

    /// <summary>Draws one responsive Grid row; a partial final row uses only its real cells.</summary>
    private static void DrawBlock(int blockIndex, LiveTimelineBlock block)
    {
        var endBeat = block.StartPhraseBeat + block.Cells.Count - 1;
        EditorGUILayout.LabelField(
            $"Grid {blockIndex + 1}  ·  beats {block.StartPhraseBeat}–{endBeat}",
            EditorStyles.miniLabel);

        var row = GUILayoutUtility.GetRect(160f, 34f, GUILayout.ExpandWidth(true));
        const float gap = 2f;
        var totalGap = gap * Math.Max(0, block.Cells.Count - 1);
        var cellWidth = Math.Max(8f, (row.width - totalGap) / block.Cells.Count);

        for (var index = 0; index < block.Cells.Count; index++)
        {
            var cellRect = new Rect(row.x + index * (cellWidth + gap), row.y, cellWidth, row.height);
            DrawCell(cellRect, block.Cells[index]);
        }
    }

    /// <summary>Draws one cell from resolved fill plus independent Cue, lock, execution, and current-beat marks.</summary>
    private static void DrawCell(Rect rect, LiveTimelineCell cell)
    {
        EditorGUI.DrawRect(rect, FillColor(cell.Fill));

        if (cell.IsRunway)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 3f), RunwayFill * 1.35f);
        }

        if (cell.IsTail)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), TailFill * 1.35f);
        }

        if (cell.IsExecuting)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, rect.width, 1f), MarkColor);
        }

        if (cell.IsCurrentBeat)
        {
            DrawOutline(rect, CurrentOutline, 2f);
        }

        var previousColor = GUI.contentColor;
        GUI.contentColor = cell.IsCurrentBeat ? Color.black : MarkColor;
        GUI.Label(rect, new GUIContent(CellText(cell), Tooltip(cell)), CellStyle(cell));
        GUI.contentColor = previousColor;
    }

    /// <summary>Resolves only the already-decided semantic fill.</summary>
    private static Color FillColor(LiveTimelineFill fill)
    {
        return fill switch
        {
            LiveTimelineFill.Runway => RunwayFill,
            LiveTimelineFill.Tail => TailFill,
            LiveTimelineFill.CurrentBeat => CurrentFill,
            _ => BaseFill,
        };
    }

    /// <summary>Returns a compact shape-plus-count label for a cell.</summary>
    private static string CellText(LiveTimelineCell cell)
    {
        var glyph = cell.IsLocked
            ? "▣"
            : cell.IsLoadedCue
                ? "●"
                : cell.IsImpactPoint || cell.IsCueMark
                    ? "◆"
                    : string.Empty;
        return $"{glyph}{cell.PhraseBeat}";
    }

    /// <summary>Builds an accessibility tooltip from every independent semantic attribute.</summary>
    private static string Tooltip(LiveTimelineCell cell)
    {
        var text = new StringBuilder($"Phrase beat {cell.PhraseBeat}");
        if (cell.IsCueMark) text.Append(" · Cue Mark");
        if (cell.IsLoadedCue) text.Append(" · Loaded Cue");
        if (cell.IsLocked) text.Append(" · Locked");
        if (cell.IsImpactPoint) text.Append(" · Impact Point");
        if (cell.IsRunway) text.Append(" · Runway");
        if (cell.IsTail) text.Append(" · Tail");
        if (cell.IsExecuting) text.Append(" · Active Transition");
        if (cell.IsCurrentBeat) text.Append(" · Current beat");
        return text.ToString();
    }

    /// <summary>Returns the cached centered editor-native label style for a cell.</summary>
    private static GUIStyle CellStyle(LiveTimelineCell cell)
    {
        return cell.IsCueMark || cell.IsImpactPoint
            ? markedCellStyle!
            : cellStyle!;
    }

    /// <summary>Creates the two cell styles once, after Unity's editor styles are available.</summary>
    private static void EnsureStyles()
    {
        if (cellStyle != null)
        {
            return;
        }

        cellStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
        };
        markedCellStyle = new GUIStyle(cellStyle)
        {
            fontStyle = FontStyle.Bold,
        };
    }

    /// <summary>Draws a rectangular current-beat outline without replacing interior marks.</summary>
    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
