using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity adapter for the BeatManager Inspector dashboard.
/// </summary>
/// <remarks>
/// This drawer owns the Unity <see cref="SerializedProperty"/> lifecycle, resolves the live Controller behind
/// the serialized field, and mirrors the staged Effect's Waveform or Routine selection.
/// Display decisions live in <see cref="BeatManagerDashboardModel"/>; IMGUI layout and widgets live in
/// <see cref="BeatManagerDashboardRenderer"/>. That leaves the property drawer as a small adapter over one deep
/// dashboard module instead of a monolith that mixes runtime reads, display rules, and drawing.
/// </remarks>
[CustomPropertyDrawer(typeof(BeatManager))]
public sealed class BeatManagerDrawer : PropertyDrawer
{
    // The Waveform Pool, cached for the strip's selector dropdown. Parsing happens once per change, not per repaint:
    // re-reading every frame would re-run Waveform.Parse (which logs malformations) on a constantly-repainting
    // Play Mode inspector. The file's last-write time is the cache key, so an external save (the Pool editor's
    // AssetDatabase.Refresh) is picked up automatically without parsing — and therefore without log spam — in between.
    // Runtime acquisition and this read-only label cache parse the same file through the same codec.
    private static WaveformPool.Entry[] waveformPoolEntries;
    private static string[] waveformPoolNames;

    /// <summary>The required-Pool configuration failure shown instead of synthetic selection labels.</summary>
    private static string waveformPoolError;

    private static long waveformPoolStampTicks = long.MinValue; // sentinel: nothing loaded yet

    /// <summary>Draws the foldout, the unified rhythm dashboard, and the regular serialized child fields.</summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        try
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            try
            {
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var panelRect = EditorGUI.IndentedRect(new Rect(line.x, line.y, line.width, BeatManagerDashboardRenderer.DashboardHeight));
                DrawDashboard(panelRect, ResolveController(property), panelRect.width);
                line.y += BeatManagerDashboardRenderer.DashboardHeight + EditorGUIUtility.standardVerticalSpacing;

                DrawChildFields(line, property);
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Returns the exact IMGUI height for the foldout, the dashboard panel, and the child fields.</summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing
            + BeatManagerDashboardRenderer.DashboardHeight
            + EditorGUIUtility.standardVerticalSpacing;

        var child = property.Copy();
        var end = property.GetEndProperty();
        var enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            enterChildren = false;
            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    /// <summary>
    /// Resolves the live Controller that owns this BeatManager so the panel can follow its staged Effect.
    /// Returns null during multi-object editing or when the property belongs to another host type.
    /// </summary>
    private static Controller ResolveController(SerializedProperty property)
    {
        var serializedObject = property.serializedObject;
        if (serializedObject.isEditingMultipleObjects)
        {
            return null;
        }

        return serializedObject.targetObject as Controller;
    }

    /// <summary>Draws the serialized BeatManager wire snapshot and smoothing tunables normally.</summary>
    /// <remarks>Children are enumerated rather than listed by name so future fields appear without touching
    /// this drawer; wireSnapshot renders through Unity's plain default foldout — that is the raw-values debug view,
    /// intentionally plain.</remarks>
    private static void DrawChildFields(Rect line, SerializedProperty property)
    {
        var child = property.Copy();
        var end = property.GetEndProperty();
        var enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            enterChildren = false;
            var height = EditorGUI.GetPropertyHeight(child, true);
            line.height = height;
            EditorGUI.PropertyField(line, child, true);
            line.y += height + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    /// <summary>Draws the read-only BeatManager dashboard without exposing serialized transport fields.</summary>
    /// <param name="rect">The dashboard rectangle allocated by the property drawer.</param>
    /// <param name="controller">The live runtime source, or null when it cannot be resolved.</param>
    /// <param name="layoutWidth">The available workspace width that selects the responsive flow.</param>
    internal static void DrawDashboard(Rect rect, Controller controller, float layoutWidth)
    {
        EnsureWaveformPool();

        var beatManager = controller != null ? controller.beatManager : null;
        var grid = beatManager != null ? beatManager.Grid : default;
        var selection = EffectRhythmSelectionView.From(
            controller,
            waveformPoolEntries,
            waveformPoolNames,
            waveformPoolError,
            grid.Bar,
            grid.Progress);
        var model = BeatManagerDashboardModel.From(
            beatManager,
            selection.Waveform,
            selection.WaveformSelector.Error);
        var actions = BeatManagerDashboardRenderer.Draw(
            rect,
            model,
            selection.WaveformSelector,
            selection.Waveform,
            selection.Routine,
            waveformPoolNames,
            layoutWidth);
        ApplyDashboardActions(actions);
    }

    /// <summary>(Re)loads the Waveform Pool for read-only Effect selection labels, keyed on the file's last-write time so an
    /// external save is reflected without re-parsing — and so without re-running <see cref="Waveform.Parse"/>'s
    /// malformation logging — on every repaint. Required-configuration failures remain unavailable and visible.</summary>
    private static void EnsureWaveformPool()
    {
        long stamp;
        try
        {
            var path = WaveformPool.FilePath;
            stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
        }
        catch
        {
            stamp = 0L; // an unreadable stat: load once, then treat as stable rather than thrashing the parser
        }

        if (waveformPoolEntries != null && stamp == waveformPoolStampTicks)
        {
            return;
        }

        WaveformPoolPreview preview;
        try
        {
            var path = WaveformPool.FilePath;
            var exists = File.Exists(path);
            var text = exists ? File.ReadAllText(path) : "";
            preview = WaveformPoolPreview.FromText(text, exists);
        }
        catch (System.Exception exception)
        {
            preview = WaveformPoolPreview.Unreadable(
                $"Required Waveform Pool '{WaveformPool.FileName}' could not be read: {exception.Message}");
        }

        waveformPoolEntries = preview.Entries;
        waveformPoolError = preview.Error;
        waveformPoolNames = new string[waveformPoolEntries.Length];
        for (var i = 0; i < waveformPoolEntries.Length; i++)
        {
            waveformPoolNames[i] = waveformPoolEntries[i].name;
        }

        waveformPoolStampTicks = stamp;
    }

    /// <summary>Applies the explicit Pool-editor action without writing musical runtime state.</summary>
    /// <param name="actions">The explicit Pool-editor request from this draw.</param>
    private static void ApplyDashboardActions(BeatManagerDashboardActions actions)
    {
        if (actions.OpenWaveformPoolEditor)
        {
            WaveformPoolEditor.Open();
        }
    }
}
