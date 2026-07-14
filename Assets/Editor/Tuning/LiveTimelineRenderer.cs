// Renders the rolling live Transition projection in the Unity Editor.
#nullable enable

using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Draws two compact live Grid rows without interpreting runtime scheduling policy.</summary>
internal static class LiveTimelineRenderer
{
    /// <summary>Cached centered style for beat cells.</summary>
    private static GUIStyle? cellStyle;

    /// <summary>Cached bold style for Transition boundary cells.</summary>
    private static GUIStyle? boundaryCellStyle;

    /// <summary>Neutral Grid-cell fill.</summary>
    private static readonly Color BaseFill = new(0.12f, 0.13f, 0.16f);

    /// <summary>Runway fill restored from the original live phase strip.</summary>
    private static readonly Color RunwayFill = new(1f, 0.35f, 0.85f);

    /// <summary>Tail fill restored from the original live phase strip.</summary>
    private static readonly Color TailFill = new(0.25f, 0.8f, 0.45f);

    /// <summary>Lock Point fill restored from the original live phase strip.</summary>
    private static readonly Color LockFill = new(1f, 0.55f, 0.2f);

    /// <summary>Impact Point fill restored from the original live phase strip.</summary>
    private static readonly Color ImpactFill = new(0.25f, 0.95f, 1f);

    /// <summary>Current-beat fill restored from the original live phase strip.</summary>
    private static readonly Color CurrentFill = new(1f, 0.9f, 0.35f);

    /// <summary>High-contrast outline used to keep the current beat trackable.</summary>
    private static readonly Color CurrentOutline = Color.white;

    /// <summary>Ordinary cell content color.</summary>
    private static readonly Color CellContent = new(0.95f, 0.95f, 0.95f);

    /// <summary>Draws pending-or-active Cue identity, timing state, legend, and two rolling Grid rows.</summary>
    public static void Draw(LiveTimelineModel model, string cueLabel)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        EnsureStyles();
        EditorGUILayout.LabelField(
            model.IsActive ? "ACTIVE" : "NEXT",
            string.IsNullOrWhiteSpace(cueLabel) ? "—" : cueLabel);
        EditorGUILayout.LabelField(FormatTimingStatus(model), EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "L Lock  ·  S Start / Runway  ·  X Impact  ·  T Tail / E End  ·  Yellow Current",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);

        if (!model.IsSynced)
        {
            EditorGUILayout.HelpBox("Standalone Mode · live Transition timing unavailable.", MessageType.None);
            return;
        }

        if (!model.CurrentPositionAvailable)
        {
            EditorGUILayout.HelpBox("Current Grid position unavailable.", MessageType.None);
            return;
        }

        if (model.HasCue && !model.CueTimingAvailable)
        {
            EditorGUILayout.HelpBox("Cue timing unavailable.", MessageType.None);
        }

        if (model.Grids.Count != 2)
        {
            EditorGUILayout.HelpBox("Rolling Grid window unavailable.", MessageType.None);
            return;
        }

        DrawGrid("CURRENT", model.Grids[0]);
        EditorGUILayout.Space(3f);
        DrawGrid("NEXT", model.Grids[1]);
    }

    /// <summary>Formats the live Lock, Start, active, and End state without targeting the Impact Point.</summary>
    internal static string FormatTimingStatus(LiveTimelineModel model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (!model.IsSynced)
        {
            return "TIMING UNAVAILABLE";
        }

        if (!model.HasCue)
        {
            return "NO CUE";
        }

        if (!model.CueTimingAvailable)
        {
            return "CUE TIMING UNAVAILABLE";
        }

        var lockStatus = model.IsCueLocked
            ? "LOCKED"
            : FormatEvent("LOCK", model.LockBeatsUntil);
        if (model.StartBeatsUntil == 0)
        {
            return $"{lockStatus} · START NOW · {FormatEvent("END", model.EndBeatsUntil)}";
        }

        if (model.IsActive)
        {
            return $"{lockStatus} · ACTIVE · {FormatEvent("END", model.EndBeatsUntil)}";
        }

        if (model.EndBeatsUntil is < 0)
        {
            return $"{lockStatus} · COMPLETE";
        }

        return $"{lockStatus} · {FormatEvent("START", model.StartBeatsUntil)} · {FormatEvent("END", model.EndBeatsUntil)}";
    }

    /// <summary>Formats one signed event delta as an operational countdown.</summary>
    private static string FormatEvent(string label, int? beatsUntil)
    {
        return beatsUntil switch
        {
            null => $"{label} —",
            > 0 => $"{label} IN {beatsUntil.Value}",
            0 => $"{label} NOW",
            _ => $"{label} PASSED",
        };
    }

    /// <summary>Draws one complete Grid row with a fixed label and 16 responsive cells.</summary>
    private static void DrawGrid(string label, LiveTimelineGrid grid)
    {
        var row = GUILayoutUtility.GetRect(220f, 34f, GUILayout.ExpandWidth(true));
        const float labelWidth = 54f;
        const float gap = 2f;
        var labelRect = new Rect(row.x, row.y, labelWidth - gap, row.height);
        GUI.Label(labelRect, label, EditorStyles.miniBoldLabel);

        var cellsRect = new Rect(row.x + labelWidth, row.y, row.width - labelWidth, row.height);
        var totalGap = gap * Math.Max(0, grid.Cells.Count - 1);
        var cellWidth = Math.Max(8f, (cellsRect.width - totalGap) / grid.Cells.Count);
        for (var index = 0; index < grid.Cells.Count; index++)
        {
            var cellRect = new Rect(
                cellsRect.x + index * (cellWidth + gap),
                cellsRect.y,
                cellWidth,
                cellsRect.height);
            DrawCell(cellRect, grid.Cells[index]);
        }
    }

    /// <summary>Draws one beat from its resolved fill and independent boundary markers.</summary>
    private static void DrawCell(Rect rect, LiveTimelineCell cell)
    {
        EditorGUI.DrawRect(rect, FillColor(cell.Fill));
        if (cell.IsCurrentBeat)
        {
            DrawOutline(rect, CurrentOutline, 2f);
        }

        var previousColor = GUI.contentColor;
        GUI.contentColor = cell.Fill == LiveTimelineFill.Base ? CellContent : Color.black;
        GUI.Label(rect, new GUIContent(CellText(cell), Tooltip(cell)), CellStyle(cell));
        GUI.contentColor = previousColor;
    }

    /// <summary>Maps each semantic fill to the approved live color vocabulary.</summary>
    private static Color FillColor(LiveTimelineFill fill)
    {
        return fill switch
        {
            LiveTimelineFill.Runway => RunwayFill,
            LiveTimelineFill.Tail => TailFill,
            LiveTimelineFill.LockPoint => LockFill,
            LiveTimelineFill.ImpactPoint => ImpactFill,
            LiveTimelineFill.CurrentBeat => CurrentFill,
            _ => BaseFill,
        };
    }

    /// <summary>Returns compact boundary letters, falling back to the one-based Grid beat.</summary>
    private static string CellText(LiveTimelineCell cell)
    {
        var markers = new StringBuilder(3);
        if (cell.IsLockPoint) markers.Append('L');
        if (cell.IsStart) markers.Append('S');
        if (cell.IsImpactPoint) markers.Append('X');
        if (cell.IsEnd) markers.Append('E');
        return markers.Length > 0
            ? markers.ToString()
            : cell.GridBeat.ToString("00");
    }

    /// <summary>Builds an accessibility tooltip from every independent timing attribute.</summary>
    private static string Tooltip(LiveTimelineCell cell)
    {
        var text = new StringBuilder($"Grid beat {cell.GridBeat} · Absolute beat {cell.AbsoluteBeat}");
        if (cell.IsLockPoint) text.Append(" · Lock Point");
        if (cell.IsStart) text.Append(" · Start");
        if (cell.IsRunway) text.Append(" · Runway");
        if (cell.IsImpactPoint) text.Append(" · Impact Point");
        if (cell.IsTail) text.Append(" · Tail");
        if (cell.IsEnd) text.Append(" · End");
        if (cell.IsCurrentBeat) text.Append(" · Current beat");
        return text.ToString();
    }

    /// <summary>Returns the cached ordinary or boundary cell style.</summary>
    private static GUIStyle CellStyle(LiveTimelineCell cell)
    {
        return cell.IsLockPoint || cell.IsStart || cell.IsImpactPoint || cell.IsEnd
            ? boundaryCellStyle!
            : cellStyle!;
    }

    /// <summary>Creates cell styles once after Unity's editor styles are available.</summary>
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
        boundaryCellStyle = new GUIStyle(cellStyle)
        {
            fontStyle = FontStyle.Bold,
        };
    }

    /// <summary>Draws a rectangular outline inside the supplied cell.</summary>
    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
