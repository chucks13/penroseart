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
    private const string WaveformAutoLabel = "Auto (wall picks)";

    // The canonical Beat Pulse, parsed once and reused: the origin envelope and the strip's fallback when the Pool
    // is empty/missing. A struct, so caching just avoids re-parsing (and re-allocating its Hump[]) on every repaint.
    private static Waveform beatPulseWaveform;
    private static bool beatPulseBuilt;

    // The Waveform Pool, cached for the strip's selector dropdown. Parsing happens once per change, not per repaint:
    // re-reading every frame would re-run Waveform.Parse (which logs malformations) on a constantly-repainting
    // Play Mode inspector. The file's last-write time is the cache key, so an external save (the Pool editor's
    // AssetDatabase.Refresh) is picked up automatically without parsing — and therefore without log spam — in between.
    // Index alignment is what makes the two-way wall binding work: BeatManager.LoadWaveformPool parses the SAME file
    // through the SAME codec in the SAME order, so a Pool index here equals the runtime's beatVariant for that entry.
    private static WaveformPool.Entry[] waveformPoolEntries;
    private static string[] waveformPoolNames;
    private static string[] waveformPopupOptions;
    private static int waveformPopupAutoVariant = int.MinValue;

    // The dropdown's selected row, NOT a raw Pool index: row 0 is the "Auto" sentinel, row k+1 is Pool index k.
    // In Play Mode it is recomputed from BeatManager.activeVariant each repaint (read-back); in Edit Mode it is the
    // local preview choice the user is browsing.
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
                DrawDashboard(panelRect, ResolveBeatManager(property), property.serializedObject.targetObject);
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

    /// <summary>
    /// The on-screen effect's live variant, or <c>-1</c> outside safe Play Mode Controller access.
    /// </summary>
    private static int ResolveOnScreenVariant(Object owner)
    {
        return WallVariantControl.ResolveOnScreenVariant(owner);
    }

    /// <summary>Draws the serialized BeatManager fields (beatData, simulator and smoothing tunables) normally.</summary>
    /// <remarks>Children are enumerated rather than listed by name so future fields appear without touching
    /// this drawer; beatData renders through Unity's plain default foldout — that is the raw-values debug view,
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

    private static void DrawDashboard(Rect rect, BeatManager beatManager, Object owner)
    {
        EnsureWaveformPool();

        var model = BeatManagerDashboardModel.From(beatManager, ResolveOnScreenVariant(owner));
        var selector = BuildWaveformSelector();
        var actions = BeatManagerDashboardRenderer.Draw(rect, model, selector, SelectedWaveform());
        ApplyDashboardActions(actions, selector.Live);
    }

    /// <summary>Returns the canonical Beat Pulse Waveform (<c>QQQQ</c> / <c>8888</c>), parsed once and cached.</summary>
    private static Waveform GetBeatPulseWaveform()
    {
        if (!beatPulseBuilt)
        {
            // Default shaping: Beat Pulse rounding, zero offset — the origin point the strip visualizes.
            beatPulseWaveform = Waveform.Parse("QQQQ", "8888");
            beatPulseBuilt = true;
        }

        return beatPulseWaveform;
    }

    /// <summary>(Re)loads the Waveform Pool for the preview dropdown, keyed on the file's last-write time so an
    /// external save is reflected without re-parsing — and so without re-running <see cref="Waveform.Parse"/>'s
    /// malformation logging — on every repaint. Falls back to a single Beat Pulse entry when the file is missing,
    /// empty, or has no parseable Presets.</summary>
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

        var parsed = WaveformPool.Parse(WaveformPool.ReadFileOrEmpty());
        if (parsed.Count == 0)
        {
            waveformPoolEntries = new[] { new WaveformPool.Entry("beat pulse", GetBeatPulseWaveform()) };
        }
        else
        {
            waveformPoolEntries = new WaveformPool.Entry[parsed.Count];
            for (var i = 0; i < parsed.Count; i++)
            {
                waveformPoolEntries[i] = parsed[i];
            }
        }

        waveformPoolNames = new string[waveformPoolEntries.Length];
        for (var i = 0; i < waveformPoolEntries.Length; i++)
        {
            waveformPoolNames[i] = waveformPoolEntries[i].name;
        }

        waveformPopupOptions = null;
        waveformPopupAutoVariant = int.MinValue;

        // Clamp to the dropdown range, which has one extra row (index 0 = "Auto") on top of the Pool entries.
        selectedWaveformIndex = Mathf.Clamp(selectedWaveformIndex, 0, waveformPoolEntries.Length);
        waveformPoolStampTicks = stamp;
    }

    /// <summary>
    /// The Waveform the strip plots: when the wall is live, the locked Waveform if one is set, otherwise the one the
    /// on-screen effect is actually using right now (the read-back); in Edit Mode, the local preview choice. Falls
    /// back to the first Pool entry / Beat Pulse during the brief startup or transition gap, or when the Pool is empty.
    /// </summary>
    private static Waveform SelectedWaveform()
    {
        if (waveformPoolEntries == null || waveformPoolEntries.Length == 0)
        {
            return GetBeatPulseWaveform();
        }

        int index;
        if (WallVariantControl.TryGetState(out var wall))
        {
            // Locked: plot the lock. Auto: plot whatever the on-screen effect uses, falling back to index 0 in the gap.
            index = wall.ActiveVariant >= 0 ? wall.ActiveVariant : (wall.CurrentVariant >= 0 ? wall.CurrentVariant : 0);
        }
        else
        {
            // Edit Mode: dropdown row 0 ("Auto") has no live effect to mirror, so it previews the first Pool entry.
            index = selectedWaveformIndex <= 0 ? 0 : selectedWaveformIndex - 1;
        }

        index = Mathf.Clamp(index, 0, waveformPoolEntries.Length - 1);
        return waveformPoolEntries[index].waveform;
    }

    private static WaveformSelectorView BuildWaveformSelector()
    {
        var live = WallVariantControl.TryGetState(out var wall);
        var options = GetWaveformPopupOptions(live, wall.ActiveVariant, wall.CurrentVariant);
        var shownIndex = live
            ? (wall.ActiveVariant < 0 ? 0 : Mathf.Clamp(wall.ActiveVariant + 1, 0, options.Length - 1))
            : Mathf.Clamp(selectedWaveformIndex, 0, options.Length - 1);
        return new WaveformSelectorView(live, shownIndex, options);
    }

    private static string[] GetWaveformPopupOptions(bool live, int activeVariant, int currentVariant)
    {
        if (waveformPopupOptions == null || waveformPopupOptions.Length != waveformPoolNames.Length + 1)
        {
            waveformPopupOptions = new string[waveformPoolNames.Length + 1];
            for (var i = 0; i < waveformPoolNames.Length; i++)
            {
                waveformPopupOptions[i + 1] = waveformPoolNames[i];
            }

            waveformPopupAutoVariant = int.MinValue;
        }

        var autoVariant = live && activeVariant < 0 && currentVariant >= 0 && currentVariant < waveformPoolNames.Length
            ? currentVariant
            : -1;
        if (waveformPopupAutoVariant != autoVariant)
        {
            waveformPopupOptions[0] = autoVariant >= 0
                ? WaveformAutoLabel + " → " + waveformPoolNames[autoVariant]
                : WaveformAutoLabel;
            waveformPopupAutoVariant = autoVariant;
        }

        return waveformPopupOptions;
    }

    private static void ApplyDashboardActions(BeatManagerDashboardActions actions, bool live)
    {
        if (actions.HasWaveformSelection)
        {
            selectedWaveformIndex = actions.SelectedWaveformIndex; // remember for Edit-Mode preview and the next repaint
            if (live && WallVariantControl.TryGetState(out var wall))
            {
                WallVariantControl.ApplySelection(wall.BeatManager, actions.SelectedWaveformIndex);
            }
        }

        if (actions.OpenWaveformPoolEditor)
        {
            WaveformPoolEditor.Open();
        }
    }
}
