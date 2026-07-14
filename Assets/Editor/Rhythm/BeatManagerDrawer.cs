using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity adapter for the BeatManager Inspector dashboard.
/// </summary>
/// <remarks>
/// This drawer owns the Unity <see cref="SerializedProperty"/> lifecycle, resolves the live runtime object behind
/// the serialized field, keeps editor-only Waveform Pool selector state, and applies returned dashboard actions.
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
    // BeatManager and this preview parse the same file through the same codec.
    private static WaveformPool.Entry[] waveformPoolEntries;
    private static string[] waveformPoolNames;

    /// <summary>The required-Pool configuration failure shown instead of a synthetic preview.</summary>
    private static string waveformPoolError;

    // Editor-only Pool preview. It never writes musical runtime state.
    private static int selectedWaveformIndex;
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
                DrawDashboard(panelRect, ResolveBeatManager(property), panelRect.width);
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
    /// Resolves the live BeatManager instance behind this property so the panel can call the real rhythm
    /// queries instead of reconstructing them from serialized fields. Null during multi-object editing.
    /// </summary>
    private BeatManager ResolveBeatManager(SerializedProperty property)
    {
        var serializedObject = property.serializedObject;
        if (serializedObject.isEditingMultipleObjects)
        {
            return null;
        }

        var target = serializedObject.targetObject;
        return target != null ? fieldInfo?.GetValue(target) as BeatManager : null;
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
    /// <param name="beatManager">The live runtime source, or null when it cannot be resolved.</param>
    /// <param name="layoutWidth">The available workspace width that selects the responsive flow.</param>
    internal static void DrawDashboard(Rect rect, BeatManager beatManager, float layoutWidth)
    {
        EnsureWaveformPool();

        var selectedWaveform = SelectedWaveform();
        var model = BeatManagerDashboardModel.From(beatManager, selectedWaveform, waveformPoolError);
        var selector = BuildWaveformSelector();
        var actions = BeatManagerDashboardRenderer.Draw(rect, model, selector, selectedWaveform, layoutWidth);
        ApplyDashboardActions(actions);
    }

    /// <summary>(Re)loads the Waveform Pool for the preview dropdown, keyed on the file's last-write time so an
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

        selectedWaveformIndex = waveformPoolEntries.Length > 0
            ? Mathf.Clamp(selectedWaveformIndex, 0, waveformPoolEntries.Length - 1)
            : -1;
        waveformPoolStampTicks = stamp;
    }

    /// <summary>The runtime Waveform the strip plots, or null when required Pool configuration is unusable.</summary>
    private static Waveform? SelectedWaveform()
    {
        if (waveformPoolEntries == null || waveformPoolEntries.Length == 0)
        {
            return null;
        }

        var index = Mathf.Clamp(selectedWaveformIndex, 0, waveformPoolEntries.Length - 1);
        return waveformPoolEntries[index].waveform;
    }

    /// <summary>Builds the preview selector from the cached Pool without exposing runtime control.</summary>
    private static WaveformSelectorView BuildWaveformSelector()
    {
        var shownIndex = waveformPoolNames.Length > 0
            ? Mathf.Clamp(selectedWaveformIndex, 0, waveformPoolNames.Length - 1)
            : -1;
        return new WaveformSelectorView(shownIndex, waveformPoolNames, waveformPoolError);
    }

    /// <summary>Applies preview and Pool-editor actions without writing musical runtime state.</summary>
    private static void ApplyDashboardActions(BeatManagerDashboardActions actions)
    {
        if (actions.HasWaveformSelection)
        {
            selectedWaveformIndex = actions.SelectedWaveformIndex;
        }

        if (actions.OpenWaveformPoolEditor)
        {
            WaveformPoolEditor.Open();
        }
    }
}
