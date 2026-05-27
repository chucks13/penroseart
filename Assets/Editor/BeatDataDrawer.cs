using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows BeatData's live Rave OSC values with a read-only musical status panel in the Inspector.
/// </summary>
/// <remarks>
/// The panel uses a two-zone layout: the left zone shows musical position (quarter-note beat dots and
/// offbeat markers sharing one column-label row), the right zone shows beat/offbeat/eighth pulse meters,
/// and a row of countdown chips runs along the bottom. The header badge distinguishes SIM (local fallback
/// simulator), LIVE (real Rave OSC), and OFFLINE. The raw serialized fields stay available under a
/// collapsible sub-foldout for debugging. All displayed values are read-only because Rave OSC (or the
/// simulator) is the source of truth at runtime.
/// </remarks>
[CustomPropertyDrawer(typeof(BeatData))]
public sealed class BeatDataDrawer : PropertyDrawer
{
    private const int BeatSlotCount = 4;

    private const string DotFilled = "●";
    private const string DotEmpty = "○";
    private const string OffBeatActive = "◆";
    private const string OffBeatInactive = "◇";

    private const string SimPlayersToken = "SIM";

    private const string ActiveField = "active";
    private const string PlayersLiveField = "playersLive";
    private const string TrackField = "track";
    private const string BpmField = "bpm";
    private const string BeatInBarField = "beatInBar";
    private const string BeatsCountMsField = "beatsCountMs";
    private const string OnBeatsField = "onBeats";
    private const string OffBeatsCountMsField = "offBeatsCountMs";
    private const string OffBeatsField = "offBeats";
    private const string OffBeatPulseField = "offBeatPulse";
    private const string BeatPulseField = "beatPulse";
    private const string BeatAverageMsField = "beatAverageMs";
    private const string BeatsPerMeasureField = "beatsPerMeasure";

    // Vertical budget. VisualPanelHeight is derived so the layout, the panel rect, and GetPropertyHeight stay in sync.
    // PanelPadding is added at both the top and the bottom so the chip row keeps a margin instead of sitting flush against the edge.
    private const float PanelPadding = 12f;
    private const float HeaderHeight = 22f;
    private const float HeaderGap = 12f;
    private const float BodyHeight = 78f;
    private const float BodyGap = 12f;
    private const float ChipHeight = 24f;
    private const float VisualPanelHeight =
        PanelPadding + HeaderHeight + HeaderGap + BodyHeight + BodyGap + ChipHeight
        + WaveformStripGap + WaveformSelectorHeight + WaveformSelectorGap + WaveformStripHeight + PanelPadding;

    private const float BarHeight = 8f;

    // Position zone (left): row label, then four evenly spaced markers, then a shared column-number row.
    private const float LeftZoneWidth = 232f;
    private const float LeftZoneMaxFraction = 0.55f;
    private const float ZoneDividerGap = 16f;
    private const float RowLabelWidth = 64f;
    private const float MarkerStartX = 72f;
    private const float MarkerSpacing = 40f;
    private const float MarkerWidth = 28f;
    private const float DotRowHeight = 26f;
    private const float DotRowGap = 4f;
    private const float ColumnLabelHeight = 12f;

    // Pulse zone (right): three stacked meters, each a label + bar + value.
    private const float MeterRowHeight = 22f;
    private const float MeterRowGap = 6f;
    private const float MeterLabelWidth = 40f;
    private const float MeterValueWidth = 46f;

    private const float ChipGap = 8f;

    // Waveform strip (bottom): a Preset-picker row, then a row plotting the chosen envelope with a live Bar Phase playhead.
    private const float WaveformStripGap = 12f;
    private const float WaveformSelectorHeight = 20f;
    private const float WaveformSelectorGap = 6f;
    private const float WaveformStripHeight = 46f;
    private const float WaveformValueWidth = 46f;
    private const int WaveformPlotSamples = 96;

    private static readonly string[] ChildFields =
    {
        ActiveField,
        PlayersLiveField,
        TrackField,
        BpmField,
        "beat",
        "bar",
        BeatInBarField,
        BeatsCountMsField,
        OnBeatsField,
        OffBeatsCountMsField,
        OffBeatsField,
        OffBeatPulseField,
        BeatAverageMsField,
        BeatPulseField,
        "levels",
        "phaseState",
        "dropState",
        "fillState",
        "energyState",
        BeatsPerMeasureField,
    };

    private static readonly string[] CountdownChipLabels =
    {
        "NEXT BEAT",
        "ON BEAT",
        "NEXT OFF BEAT",
        "OFF BEAT",
    };

    private static readonly Color PanelBackgroundColor = new Color(0.035f, 0.04f, 0.055f);
    private static readonly Color PanelLiveAccentColor = new Color(0.10f, 0.85f, 0.95f);
    private static readonly Color PanelSimAccentColor = new Color(0.95f, 0.72f, 0.18f);
    private static readonly Color PanelOfflineAccentColor = new Color(0.45f, 0.12f, 0.16f);
    private static readonly Color LiveBadgeColor = new Color(0.02f, 0.38f, 0.30f);
    private static readonly Color SimBadgeColor = new Color(0.46f, 0.34f, 0.06f);
    private static readonly Color OfflineBadgeColor = new Color(0.28f, 0.08f, 0.08f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color MeterTrackColor = new Color(0.07f, 0.08f, 0.10f);
    private static readonly Color BeatMeterColor = new Color(0.12f, 0.92f, 1f);
    private static readonly Color OffBeatMeterColor = new Color(1f, 0.42f, 0.92f);
    private static readonly Color EighthMeterColor = new Color(0.62f, 1f, 0.25f);
    private static readonly Color BeatChipColor = new Color(0.08f, 0.28f, 0.34f);
    private static readonly Color OnBeatChipColor = new Color(0.10f, 0.32f, 0.18f);
    private static readonly Color OffBeatChipColor = new Color(0.26f, 0.10f, 0.32f);
    private static readonly Color OffBeatGateChipColor = new Color(0.24f, 0.10f, 0.25f);
    private static readonly Color DisabledDotColor = new Color(0.32f, 0.34f, 0.38f);
    private static readonly Color PastBeatDotColor = new Color(0.16f, 0.42f, 0.45f);
    private static readonly Color FutureBeatDotColor = new Color(0.30f, 0.33f, 0.38f);
    private static readonly Color CurrentBeatSteadyColor = new Color(0.10f, 0.70f, 0.78f);
    private static readonly Color CurrentBeatFlashColor = new Color(0.28f, 1f, 0.98f);
    private static readonly Color OffBeatSteadyColor = new Color(0.45f, 0.08f, 0.42f);
    private static readonly Color OffBeatFlashColor = new Color(1f, 0.42f, 0.92f);
    private static readonly Color OffBeatInactiveColor = new Color(0.34f, 0.24f, 0.38f, 1f);
    private static readonly Color OffBeatDisabledColor = new Color(0.34f, 0.24f, 0.38f, 0.55f);
    private static readonly Color WaveformTrackColor = new Color(0.055f, 0.065f, 0.085f);
    private static readonly Color WaveformGridColor = new Color(1f, 1f, 1f, 0.07f);
    private static readonly Color WaveformCurveColor = new Color(0.12f, 0.92f, 1f);
    private static readonly Color WaveformCurveIdleColor = new Color(0.34f, 0.42f, 0.47f);
    private static readonly Color WaveformPlayheadColor = new Color(1f, 0.92f, 0.35f);
    private static readonly Color WaveformPlayheadLineColor = new Color(1f, 0.92f, 0.35f, 0.55f);

    private static GUIStyle titleStyle;
    private static GUIStyle smallLabelStyle;
    private static GUIStyle valueStyle;
    private static GUIStyle badgeStyle;
    private static GUIStyle dotStyle;
    private static GUIStyle markerLabelStyle;
    private static GUIStyle chipLabelStyle;
    private static GUIStyle chipValueStyle;
    private static GUIStyle chipValueRightStyle;

    private static bool rawFieldsExpanded;

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
    // The dropdown's selected row, NOT a raw Pool index: row 0 is the "Auto" sentinel, row k+1 is Pool index k.
    // In Play Mode it is recomputed from BeatManager.activeVariant each repaint (read-back); in Edit Mode it is the
    // local preview choice the user is browsing.
    private static int selectedWaveformIndex;
    private static long waveformPoolStampTicks = long.MinValue; // sentinel: nothing loaded yet

    /// <summary>Draws the foldout, two-zone live-status panel, and the collapsible raw fields for a BeatData value.</summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureStyles();
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

                var panelRect = EditorGUI.IndentedRect(new Rect(line.x, line.y, line.width, VisualPanelHeight));
                DrawVisualPanel(panelRect, BeatPanelState.FromSerializedProperty(property));
                line.y += VisualPanelHeight + EditorGUIUtility.standardVerticalSpacing;

                var rawHeader = new Rect(line.x, line.y, line.width, EditorGUIUtility.singleLineHeight);
                rawFieldsExpanded = EditorGUI.Foldout(rawHeader, rawFieldsExpanded, "Raw OSC values", true);
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                if (rawFieldsExpanded)
                {
                    EditorGUI.indentLevel++;
                    try
                    {
                        DrawRawFields(line, property);
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }
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

    /// <summary>Returns the exact IMGUI height Unity needs for the foldout, visual panel, and raw child fields.</summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing + VisualPanelHeight + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (!rawFieldsExpanded)
        {
            return height;
        }

        foreach (var fieldName in ChildFields)
        {
            var child = property.FindPropertyRelative(fieldName);
            if (child == null)
            {
                continue;
            }

            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    /// <summary>Draws the raw serialized fields as disabled Inspector controls underneath the custom dashboard.</summary>
    private static void DrawRawFields(Rect line, SerializedProperty property)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            foreach (var fieldName in ChildFields)
            {
                var child = property.FindPropertyRelative(fieldName);
                if (child == null)
                {
                    continue;
                }

                var height = EditorGUI.GetPropertyHeight(child, true);
                line.height = height;
                EditorGUI.PropertyField(line, child, true);
                line.y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    /// <summary>Draws the two-zone live dashboard: header, position zone, pulse zone, and countdown chips.</summary>
    private static void DrawVisualPanel(Rect rect, BeatPanelState state)
    {
        var accent = state.Simulated ? PanelSimAccentColor : state.Active ? PanelLiveAccentColor : PanelOfflineAccentColor;
        DrawPanelBackground(rect, accent);

        var content = new Rect(rect.x + PanelPadding, rect.y + PanelPadding, Mathf.Max(0f, rect.width - (PanelPadding * 2f)), rect.height - (PanelPadding * 2f));

        DrawHeader(new Rect(content.x, content.y, content.width, HeaderHeight), state);

        var body = new Rect(content.x, content.y + HeaderHeight + HeaderGap, content.width, BodyHeight);
        var leftWidth = Mathf.Min(LeftZoneWidth, body.width * LeftZoneMaxFraction);
        var leftZone = new Rect(body.x, body.y, leftWidth, body.height);
        DrawPositionZone(leftZone, state);

        var dividerX = leftZone.xMax + (ZoneDividerGap * 0.5f);
        EditorGUI.DrawRect(new Rect(dividerX, body.y + 2f, 1f, body.height - 4f), DividerColor);

        var rightX = leftZone.xMax + ZoneDividerGap;
        DrawPulseZone(new Rect(rightX, body.y, Mathf.Max(0f, body.xMax - rightX), body.height), state);

        var chipsRect = new Rect(content.x, body.yMax + BodyGap, content.width, ChipHeight);
        DrawCountdownChips(chipsRect, state);

        var selectorRect = new Rect(content.x, chipsRect.yMax + WaveformStripGap, content.width, WaveformSelectorHeight);
        DrawWaveformSelector(selectorRect);
        DrawWaveformStrip(new Rect(content.x, selectorRect.yMax + WaveformSelectorGap, content.width, WaveformStripHeight), state);
    }

    /// <summary>Draws the SIM/LIVE/OFFLINE badge, current track text, and players + BPM.</summary>
    private static void DrawHeader(Rect rect, BeatPanelState state)
    {
        var badgeColor = state.Simulated ? SimBadgeColor : state.Active ? LiveBadgeColor : OfflineBadgeColor;
        var badgeText = state.Simulated ? "SIM" : state.Active ? "LIVE" : "OFFLINE";
        var badgeRect = new Rect(rect.x, rect.y, 74f, rect.height);
        EditorGUI.DrawRect(badgeRect, badgeColor);
        GUI.Label(badgeRect, badgeText, badgeStyle);

        const float rightWidth = 132f;
        var titleWidth = Mathf.Max(0f, rect.width - badgeRect.width - rightWidth - 16f);
        var titleRect = new Rect(badgeRect.xMax + 8f, rect.y, titleWidth, rect.height);
        var title = string.IsNullOrWhiteSpace(state.Track) ? (state.Active ? "Untitled track" : "No Rave OSC track") : state.Track;
        GUI.Label(titleRect, title, titleStyle);

        var bpmText = state.Active && state.Bpm > 0f ? $"{state.Bpm:0.##} BPM" : "-- BPM";
        var rightText = !state.Simulated && !string.IsNullOrWhiteSpace(state.PlayersLive) ? $"{state.PlayersLive} · {bpmText}" : bpmText;
        var rightRect = new Rect(rect.xMax - rightWidth, rect.y, rightWidth, rect.height);
        GUI.Label(rightRect, rightText, valueStyle);
    }

    /// <summary>Draws the left position zone: quarter-note beat dots, offbeat markers, and shared column labels.</summary>
    private static void DrawPositionZone(Rect zone, BeatPanelState state)
    {
        var beatRow = new Rect(zone.x, zone.y + 2f, zone.width, DotRowHeight);
        DrawBeatDotsRow(beatRow, state);

        var offRow = new Rect(zone.x, beatRow.yMax + DotRowGap, zone.width, DotRowHeight);
        DrawOffBeatRow(offRow, state);

        DrawColumnLabels(new Rect(zone.x, offRow.yMax, zone.width, ColumnLabelHeight));
    }

    /// <summary>Draws the four quarter-note beat markers in the position zone.</summary>
    private static void DrawBeatDotsRow(Rect rect, BeatPanelState state)
    {
        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), "BEAT", smallLabelStyle);

        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatSlotCount; i++)
        {
            var beatLabel = i + 1;
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            var glyph = BuildBeatDotGlyph(state.Active, state.BeatInBar, beatLabel);
            var color = GetBeatDotColor(state.Active, beatLabel, state.BeatInBar, state.OnBeat, state.BeatPulse);
            DrawMarker(markerRect, glyph, color);
        }
    }

    /// <summary>Draws the four offbeat markers, lit when their derived offbeat gate is active.</summary>
    private static void DrawOffBeatRow(Rect rect, BeatPanelState state)
    {
        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), "OFFBEAT", smallLabelStyle);

        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatSlotCount; i++)
        {
            var enabled = state.IsOffBeatGateActive(i);
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            var color = enabled
                ? Color.Lerp(OffBeatSteadyColor, OffBeatFlashColor, state.OffBeatPulse)
                : state.Active ? OffBeatInactiveColor : OffBeatDisabledColor;
            DrawMarker(markerRect, enabled ? OffBeatActive : OffBeatInactive, color);
        }
    }

    /// <summary>Draws the shared 1..4 column numbers under the beat and offbeat marker columns.</summary>
    private static void DrawColumnLabels(Rect rect)
    {
        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatSlotCount; i++)
        {
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            GUI.Label(markerRect, (i + 1).ToString(), markerLabelStyle);
        }
    }

    /// <summary>Returns the marker spacing that fits four columns inside the position zone, capped at the design spacing.</summary>
    private static float ComputeMarkerSpacing(Rect zone)
    {
        var available = Mathf.Max(0f, zone.width - MarkerStartX - MarkerWidth);
        return Mathf.Min(MarkerSpacing, available / (BeatSlotCount - 1));
    }

    /// <summary>Draws the right pulse zone: stacked beat, offbeat, and combined eighth-note meters.</summary>
    private static void DrawPulseZone(Rect rect, BeatPanelState state)
    {
        var step = MeterRowHeight + MeterRowGap;
        DrawPulseMeterRow(new Rect(rect.x, rect.y, rect.width, MeterRowHeight), "BEAT", state.BeatPulse, BeatMeterColor, state.Active);
        DrawPulseMeterRow(new Rect(rect.x, rect.y + step, rect.width, MeterRowHeight), "OFF", state.OffBeatPulse, OffBeatMeterColor, state.Active);
        DrawPulseMeterRow(new Rect(rect.x, rect.y + (step * 2f), rect.width, MeterRowHeight), "8TH", state.EighthPulse, EighthMeterColor, state.Active);
    }

    /// <summary>Draws one labelled pulse meter row with a bar and right-aligned value.</summary>
    private static void DrawPulseMeterRow(Rect rect, string label, float pulse, Color color, bool active)
    {
        GUI.Label(new Rect(rect.x, rect.y, MeterLabelWidth, rect.height), label, smallLabelStyle);

        var barX = rect.x + MeterLabelWidth + 4f;
        var barRight = rect.xMax - MeterValueWidth - 6f;
        var barRect = new Rect(barX, rect.y + ((rect.height - BarHeight) * 0.5f), Mathf.Max(0f, barRight - barX), BarHeight);
        EditorGUI.DrawRect(barRect, MeterTrackColor);

        var fill = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(pulse), barRect.height);
        var fillColor = active ? color : new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.8f);
        EditorGUI.DrawRect(fill, fillColor);

        var shine = new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f));
        EditorGUI.DrawRect(shine, new Color(1f, 1f, 1f, active ? 0.22f : 0.08f));

        GUI.Label(new Rect(rect.xMax - MeterValueWidth, rect.y, MeterValueWidth, rect.height), $"{pulse:0.00}", valueStyle);
    }

    /// <summary>Draws the four bottom chips for beat/offbeat countdowns and gate state.</summary>
    private static void DrawCountdownChips(Rect rect, BeatPanelState state)
    {
        var chipWidth = Mathf.Max(0f, (rect.width - (ChipGap * 3f)) / 4f);
        var labels = BuildCountdownChipLabels();
        var step = chipWidth + ChipGap;

        DrawChip(new Rect(rect.x, rect.y, chipWidth, rect.height), labels[0], FormatMs(state.NextBeatMs), BeatChipColor, alignValueRight: true);
        DrawChip(new Rect(rect.x + step, rect.y, chipWidth, rect.height), labels[1], state.OnBeat ? "YES" : "NO", OnBeatChipColor);
        DrawChip(new Rect(rect.x + (step * 2f), rect.y, chipWidth, rect.height), labels[2], FormatMs(state.NextOffBeatMs), OffBeatChipColor, alignValueRight: true);
        DrawChip(new Rect(rect.x + (step * 3f), rect.y, chipWidth, rect.height), labels[3], state.OffBeat ? "YES" : "NO", OffBeatGateChipColor);
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

        // Clamp to the dropdown range, which has one extra row (index 0 = "Auto") on top of the Pool entries.
        selectedWaveformIndex = Mathf.Clamp(selectedWaveformIndex, 0, waveformPoolEntries.Length);
        waveformPoolStampTicks = stamp;
    }

    /// <summary>
    /// Resolves the live wall so the selector can drive and mirror it. Returns false (and leaves outputs at their
    /// "no wall" defaults) outside Play Mode or before a Controller exists — guarding on <see cref="Singleton{T}.HasInstance"/>
    /// is essential because touching <c>Controller.Instance</c> with no instance would SPAWN a Controller GameObject.
    /// </summary>
    /// <param name="beatManager">The live BeatManager whose <c>activeVariant</c> the selector reads and writes.</param>
    /// <param name="activeVariant">The wall-wide lock: -1 == Auto, otherwise the locked Pool index.</param>
    /// <param name="currentVariant">The on-screen effect's variant, or -1 mid-transition/startup.</param>
    private static bool TryGetLiveWall(out BeatManager beatManager, out int activeVariant, out int currentVariant)
    {
        beatManager = null;
        activeVariant = -1;
        currentVariant = -1;
        if (!Application.isPlaying || !Controller.HasInstance)
        {
            return false;
        }

        var controller = Controller.Instance;
        beatManager = controller.beatManager;
        activeVariant = beatManager.activeVariant;
        currentVariant = controller.CurrentBeatVariant;
        return true;
    }

    /// <summary>
    /// The Waveform the strip plots: when the wall is live, the locked Waveform if one is set, otherwise the one the
    /// on-screen effect is actually using right now (the read-back); in Edit Mode, the local preview choice. Falls
    /// back to the first Pool entry / Beat Pulse during the brief startup or transition gap, or when the Pool is empty.
    /// </summary>
    private static Waveform SelectedWaveform()
    {
        EnsureWaveformPool();
        if (waveformPoolEntries == null || waveformPoolEntries.Length == 0)
        {
            return GetBeatPulseWaveform();
        }

        int index;
        if (TryGetLiveWall(out _, out var activeVariant, out var currentVariant))
        {
            // Locked: plot the lock. Auto: plot whatever the on-screen effect uses, falling back to index 0 in the gap.
            index = activeVariant >= 0 ? activeVariant : (currentVariant >= 0 ? currentVariant : 0);
        }
        else
        {
            // Edit Mode: dropdown row 0 ("Auto") has no live effect to mirror, so it previews the first Pool entry.
            index = selectedWaveformIndex <= 0 ? 0 : selectedWaveformIndex - 1;
        }

        index = Mathf.Clamp(index, 0, waveformPoolEntries.Length - 1);
        return waveformPoolEntries[index].waveform;
    }

    /// <summary>
    /// Applies a dropdown selection to the live wall. Row 0 releases it to Auto (each effect rolls its own variant
    /// again); any other row locks every effect to that Pool index AND retargets the on-screen effect immediately,
    /// so the pulse changes the instant you pick it instead of waiting for the next effect to start. Always logs the
    /// transition — a silent change to what the whole wall is playing would be exactly the kind of invisible state
    /// the project forbids.
    /// </summary>
    private static void ApplyWallSelection(BeatManager beatManager, int dropdownIndex)
    {
        if (dropdownIndex <= 0)
        {
            beatManager.activeVariant = -1;
            Debug.Log("[Waveform] Wall released to Auto — effects roll their own beat variant again.");
            return;
        }

        var variant = dropdownIndex - 1;
        beatManager.activeVariant = variant;
        if (Controller.HasInstance)
        {
            Controller.Instance.CurrentBeatVariant = variant; // immediate: retarget the effect already on screen
        }

        var name = (variant >= 0 && variant < waveformPoolNames.Length) ? waveformPoolNames[variant] : variant.ToString();
        Debug.Log($"[Waveform] Wall locked to '{name}' (variant {variant}) — every effect uses this until set to Auto.");
    }

    /// <summary>
    /// Draws the wall-Waveform picker above the strip: a state label, the dropdown, and the "Edit Pool…" button into
    /// <see cref="WaveformPoolEditor"/>. Row 0 is an "Auto (wall picks)" sentinel; the remaining rows are Pool entries.
    /// </summary>
    /// <remarks>
    /// In Play Mode this is a <b>two-way</b> control over what the wall plays, not a preview. Picking a Waveform writes
    /// <see cref="BeatManager.activeVariant"/> (locking every effect to it) and pokes the on-screen effect for an
    /// instant change; picking "Auto" releases the lock so effects roll their own variant again. The shown selection
    /// is recomputed from <c>activeVariant</c> each repaint, so when the wall changes the live rhythm itself (deck
    /// rotation in Auto), the dropdown follows. In Auto + live, row 0's label appends the Waveform the wall is using
    /// right now, so the closed popup answers "what is the wall pulsing to?" at a glance. Outside Play Mode there is
    /// no wall to drive, so the dropdown is a local preview browser and the label reads "PREVIEW".
    /// </remarks>
    private static void DrawWaveformSelector(Rect rect)
    {
        EnsureWaveformPool();

        const float editButtonWidth = 90f;
        const float gap = 6f;

        var live = TryGetLiveWall(out var beatManager, out var activeVariant, out var currentVariant);

        // Build dropdown options: row 0 is the "Auto" sentinel, rows 1.. are Pool entries. In Auto + live, append the
        // currently-playing Waveform's name to row 0 so the collapsed popup shows what the wall is actually using.
        var options = new string[waveformPoolNames.Length + 1];
        var autoSuffix = (live && activeVariant < 0 && currentVariant >= 0 && currentVariant < waveformPoolNames.Length)
            ? $" → {waveformPoolNames[currentVariant]}"
            : string.Empty;
        options[0] = "Auto (wall picks)" + autoSuffix;
        for (var i = 0; i < waveformPoolNames.Length; i++)
        {
            options[i + 1] = waveformPoolNames[i];
        }

        // Shown selection mirrors the live lock when running (read-back); otherwise it is the local preview choice.
        // Dropdown row 0 == Auto / activeVariant -1; row k+1 == Pool index k.
        var shownIndex = live
            ? (activeVariant < 0 ? 0 : Mathf.Clamp(activeVariant + 1, 0, options.Length - 1))
            : Mathf.Clamp(selectedWaveformIndex, 0, options.Length - 1);

        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), live ? "WALL" : "PREVIEW", smallLabelStyle);

        var popupX = rect.x + RowLabelWidth;
        var popupRight = rect.xMax - editButtonWidth - gap;
        var popupRect = new Rect(popupX, rect.y, Mathf.Max(0f, popupRight - popupX), rect.height);

        var chosen = EditorGUI.Popup(popupRect, shownIndex, options);
        if (chosen != shownIndex)
        {
            selectedWaveformIndex = chosen; // remember for Edit-Mode preview and the next repaint
            if (live)
            {
                ApplyWallSelection(beatManager, chosen);
            }
        }

        var buttonRect = new Rect(rect.xMax - editButtonWidth, rect.y, editButtonWidth, rect.height);
        if (GUI.Button(buttonRect, "Edit Pool…"))
        {
            WaveformPoolEditor.Open();
        }
    }

    /// <summary>
    /// Draws the bottom Waveform strip: the wall's effective envelope (per <see cref="SelectedWaveform"/>), with beat
    /// gridlines behind it and, when live, a playhead at the current Bar Phase marking where "now" sits and the
    /// brightness being emitted.
    /// </summary>
    /// <remarks>
    /// BeatData is the shared bar-phase clock; the Waveform plotted on top is whatever the wall is using — the locked
    /// Waveform when one is set, otherwise the on-screen effect's live variant (the read-back). The envelope is
    /// sampled from the same <see cref="Waveform.Evaluate"/> the runtime uses, so the plotted shape cannot drift from
    /// runtime shaping; only the playhead position depends on the reconstructed Bar Phase. Together with the selector
    /// above, this makes both the live clock and the rhythm it is driving visible — and editable — in the Inspector.
    /// </remarks>
    private static void DrawWaveformStrip(Rect rect, BeatPanelState state)
    {
        var plotRight = rect.xMax - WaveformValueWidth - 6f;
        var plot = new Rect(rect.x, rect.y + 2f, Mathf.Max(0f, plotRight - rect.x), rect.height - 4f);

        EditorGUI.DrawRect(plot, WaveformTrackColor);

        // Beat gridlines at quarter-note fractions (0, ¼, ½, ¾, and the closing bar edge).
        for (var i = 0; i <= BeatSlotCount; i++)
        {
            var gx = Mathf.Floor(plot.x + (plot.width * (i / (float)BeatSlotCount)));
            EditorGUI.DrawRect(new Rect(gx, plot.y, 1f, plot.height), WaveformGridColor);
        }

        var wf = SelectedWaveform();
        const float vPad = 3f; // keep the peak (1) and trough (0) off the very edges of the track
        var top = plot.y + vPad;
        var bottom = plot.yMax - vPad;

        // The smooth envelope: Handles anti-aliased polyline, which only renders during a Repaint event.
        if (Event.current.type == EventType.Repaint && plot.width > 1f)
        {
            var points = new Vector3[WaveformPlotSamples + 1];
            for (var i = 0; i <= WaveformPlotSamples; i++)
            {
                var p = i / (float)WaveformPlotSamples;
                var y = Mathf.Lerp(bottom, top, Mathf.Clamp01(wf.Evaluate(p)));
                points[i] = new Vector3(plot.x + (plot.width * p), y, 0f);
            }

            Handles.color = state.Active ? WaveformCurveColor : WaveformCurveIdleColor;
            Handles.DrawAAPolyLine(2.5f, points);
        }

        // Live playhead: vertical line + dot at the emitted brightness, with a numeric readout to the right.
        if (state.Active)
        {
            var phase = Mathf.Repeat(state.BarPhase, 1f);
            var px = plot.x + (plot.width * phase);
            EditorGUI.DrawRect(new Rect(Mathf.Floor(px), plot.y, 1f, plot.height), WaveformPlayheadLineColor);

            var emitted = Mathf.Clamp01(wf.Evaluate(state.BarPhase));
            var dotY = Mathf.Lerp(bottom, top, emitted);
            EditorGUI.DrawRect(new Rect(px - 3f, dotY - 3f, 6f, 6f), WaveformPlayheadColor);

            GUI.Label(new Rect(rect.xMax - WaveformValueWidth, rect.y, WaveformValueWidth, rect.height), $"{emitted:0.00}", valueStyle);
        }
        else
        {
            GUI.Label(new Rect(rect.xMax - WaveformValueWidth, rect.y, WaveformValueWidth, rect.height), "--", valueStyle);
        }
    }

    /// <summary>Draws the panel background, the SIM/LIVE/OFFLINE accent bar, and subtle top/bottom bevels.</summary>
    private static void DrawPanelBackground(Rect rect, Color accent)
    {
        EditorGUI.DrawRect(rect, PanelBackgroundColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), accent);

        var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
        EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width, 1f), new Color(1f, 1f, 1f, 0.08f));
        EditorGUI.DrawRect(new Rect(inner.x, inner.yMax - 1f, inner.width, 1f), new Color(0f, 0f, 0f, 0.5f));
    }

    /// <summary>Draws one beat/offbeat marker glyph with a faint colored glow.</summary>
    private static void DrawMarker(Rect rect, string glyph, Color color)
    {
        var glow = new Rect(rect.x + 4f, rect.y + 2f, Mathf.Max(0f, rect.width - 8f), Mathf.Min(20f, rect.height));
        EditorGUI.DrawRect(glow, new Color(color.r, color.g, color.b, 0.10f));

        var previous = GUI.color;
        GUI.color = color;
        GUI.Label(rect, glyph, dotStyle);
        GUI.color = previous;
    }

    /// <summary>Draws one two-line status chip with a label over a bold value so labels never truncate.</summary>
    /// <remarks>Right-aligned values (countdowns) keep their trailing unit pinned so they do not reflow as digits change.</remarks>
    private static void DrawChip(Rect rect, string label, string value, Color color, bool alignValueRight = false)
    {
        EditorGUI.DrawRect(rect, color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));

        var textWidth = Mathf.Max(0f, rect.width - 16f);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, textWidth, 10f), label, chipLabelStyle);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 11f, textWidth, rect.height - 12f), value, alignValueRight ? chipValueRightStyle : chipValueStyle);
    }

    /// <summary>Chooses the Inspector color for a beat marker based on activity, past/current/future position, and pulse.</summary>
    private static Color GetBeatDotColor(bool active, int beat, int beatInBar, bool onBeat, float beatPulse)
    {
        if (!active || beatInBar < 1 || beatInBar > BeatSlotCount)
        {
            return DisabledDotColor;
        }

        if (beat < beatInBar)
        {
            return PastBeatDotColor;
        }

        if (beat == beatInBar)
        {
            return Color.Lerp(CurrentBeatSteadyColor, CurrentBeatFlashColor, onBeat ? Mathf.Max(0.45f, beatPulse) : beatPulse * 0.35f);
        }

        return FutureBeatDotColor;
    }

    /// <summary>Builds four dot glyphs for tests and other visual-model callers that need the full row at once.</summary>
    private static string[] BuildBeatDotGlyphs(BeatData beatData)
    {
        if (beatData == null)
        {
            return new[] { DotEmpty, DotEmpty, DotEmpty, DotEmpty };
        }

        return BuildBeatDotGlyphs(beatData.active, beatData.beatInBar);
    }

    /// <summary>Builds four dot glyphs where beat labels up to the current musical beat are filled.</summary>
    private static string[] BuildBeatDotGlyphs(bool active, int beatInBar)
    {
        var glyphs = new[] { DotEmpty, DotEmpty, DotEmpty, DotEmpty };
        if (!active || beatInBar < 1 || beatInBar > glyphs.Length)
        {
            return glyphs;
        }

        for (var i = 0; i < beatInBar; i++)
        {
            glyphs[i] = DotFilled;
        }

        return glyphs;
    }

    /// <summary>Returns the single glyph for one musical beat label without allocating the full glyph row.</summary>
    private static string BuildBeatDotGlyph(bool active, int beatInBar, int beatLabel)
    {
        return active && beatLabel >= 1 && beatLabel <= beatInBar && beatInBar <= BeatSlotCount ? DotFilled : DotEmpty;
    }

    /// <summary>Returns the stronger beat/offbeat pulse after clamping each pulse into the display range.</summary>
    private static float GetClampedEighthPulse(BeatData beatData)
    {
        if (beatData == null)
        {
            return 0f;
        }

        return GetClampedEighthPulseValue(beatData.beatPulse, beatData.offBeatPulse);
    }

    /// <summary>Returns the fixed chip labels used by the visual model.</summary>
    private static string[] BuildCountdownChipLabels()
    {
        return CountdownChipLabels;
    }

    /// <summary>Returns the stronger beat/offbeat pulse after clamping both inputs to the 0..1 Inspector meter range.</summary>
    private static float GetClampedEighthPulseValue(float beatPulse, float offBeatPulse)
    {
        return Mathf.Max(Mathf.Clamp01(beatPulse), Mathf.Clamp01(offBeatPulse));
    }

    /// <summary>Formats a millisecond countdown, using -- when the countdown is unavailable.</summary>
    /// <remarks>The chip draws this right-aligned so the "ms" stays pinned and digits grow leftward as the value changes.</remarks>
    private static string FormatMs(int value)
    {
        return value >= 0 ? $"{value}ms" : "--";
    }

    /// <summary>Creates and caches GUI styles once per domain reload so repaints do not allocate styles repeatedly.</summary>
    private static void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.86f, 0.96f, 1f) },
            clipping = TextClipping.Clip,
        };
        smallLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.58f, 0.72f, 0.78f) },
            alignment = TextAnchor.MiddleLeft,
        };
        valueStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.82f, 0.90f, 0.92f) },
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip,
        };
        badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
        };
        dotStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 21,
            alignment = TextAnchor.MiddleCenter,
        };
        markerLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.76f, 0.82f, 0.84f) },
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
        };
        chipLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.74f, 0.85f, 0.88f) },
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
        };
        chipValueStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.93f, 0.98f, 1f) },
            alignment = TextAnchor.MiddleLeft,
            fontSize = 12,
            clipping = TextClipping.Clip,
        };
        chipValueRightStyle = new GUIStyle(chipValueStyle)
        {
            alignment = TextAnchor.MiddleRight,
        };
    }

    /// <summary>Reads a bool child field, returning false when the field is absent.</summary>
    private static bool ReadBool(SerializedProperty property, string fieldName)
    {
        return property.FindPropertyRelative(fieldName)?.boolValue ?? false;
    }

    /// <summary>Reads an int child field, returning -1 when the field is absent.</summary>
    private static int ReadInt(SerializedProperty property, string fieldName)
    {
        return property.FindPropertyRelative(fieldName)?.intValue ?? -1;
    }

    /// <summary>Reads a float child field, returning 0 when the field is absent.</summary>
    private static float ReadFloat(SerializedProperty property, string fieldName)
    {
        return property.FindPropertyRelative(fieldName)?.floatValue ?? 0f;
    }

    /// <summary>Reads a string child field, returning the empty string when the field is absent.</summary>
    private static string ReadString(SerializedProperty property, string fieldName)
    {
        return property.FindPropertyRelative(fieldName)?.stringValue ?? string.Empty;
    }

    /// <summary>Finds the first array slot with the smallest non-negative countdown value.</summary>
    private static int IndexOfSmallestNonNegative(SerializedProperty property, string fieldName)
    {
        var array = property.FindPropertyRelative(fieldName);
        if (array == null || !array.isArray)
        {
            return -1;
        }

        var resultIndex = -1;
        var resultValue = int.MaxValue;
        for (var i = 0; i < array.arraySize; i++)
        {
            var value = array.GetArrayElementAtIndex(i).intValue;
            if (value >= 0 && value < resultValue)
            {
                resultIndex = i;
                resultValue = value;
            }
        }
        return resultIndex;
    }

    /// <summary>Reads an int array element, returning -1 when the array or index is unavailable.</summary>
    private static int ReadIntArray(SerializedProperty property, string fieldName, int index)
    {
        var array = property.FindPropertyRelative(fieldName);
        if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
        {
            return -1;
        }

        return array.GetArrayElementAtIndex(index).intValue;
    }

    /// <summary>Reads a bool array element, returning false when the array or index is unavailable.</summary>
    private static bool ReadBoolArray(SerializedProperty property, string fieldName, int index)
    {
        var array = property.FindPropertyRelative(fieldName);
        if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
        {
            return false;
        }

        return array.GetArrayElementAtIndex(index).boolValue;
    }

    /// <summary>
    /// Immutable snapshot of the serialized BeatData values needed by the custom visual panel.
    /// </summary>
    /// <remarks>
    /// Pulling values from SerializedProperty once keeps the drawing methods simple, reduces repeated property-path
    /// lookups during IMGUI repaints, and gives tests a clearer visual model seam without making new fields editable.
    /// </remarks>
    private struct BeatPanelState
    {
        public bool Active;
        public bool Simulated;
        public int BeatInBar;
        public float Bpm;
        public string Track;
        public string PlayersLive;
        public float BeatPulse;
        public float OffBeatPulse;
        public float EighthPulse;
        public int NextBeatMs;
        public int NextOffBeatMs;
        public bool OnBeat;
        public bool OffBeat;
        public bool OffBeat1;
        public bool OffBeat2;
        public bool OffBeat3;
        public bool OffBeat4;
        public float BarPhase;

        /// <summary>Builds the panel snapshot from Unity's serialized BeatData object.</summary>
        public static BeatPanelState FromSerializedProperty(SerializedProperty property)
        {
            var active = ReadBool(property, ActiveField);
            var playersLive = ReadString(property, PlayersLiveField);
            var beatInBar = ReadInt(property, BeatInBarField);
            var beatPulse = active ? Mathf.Clamp01(ReadFloat(property, BeatPulseField)) : 0f;
            var offBeatPulse = active ? Mathf.Clamp01(ReadFloat(property, OffBeatPulseField)) : 0f;
            var beatIndex = active ? IndexOfSmallestNonNegative(property, BeatsCountMsField) : -1;
            var offBeatIndex = active ? IndexOfSmallestNonNegative(property, OffBeatsCountMsField) : -1;
            var currentBeatIndex = beatInBar - 1;

            return new BeatPanelState
            {
                Active = active,
                Simulated = active && playersLive == SimPlayersToken,
                BeatInBar = beatInBar,
                Bpm = ReadFloat(property, BpmField),
                Track = ReadString(property, TrackField),
                PlayersLive = playersLive,
                BeatPulse = beatPulse,
                OffBeatPulse = offBeatPulse,
                EighthPulse = GetClampedEighthPulseValue(beatPulse, offBeatPulse),
                NextBeatMs = active ? ReadIntArray(property, BeatsCountMsField, beatIndex) : -1,
                NextOffBeatMs = active ? ReadIntArray(property, OffBeatsCountMsField, offBeatIndex) : -1,
                OnBeat = active && ReadBoolArray(property, OnBeatsField, currentBeatIndex),
                OffBeat = active && ReadBoolArray(property, OffBeatsField, offBeatIndex),
                OffBeat1 = active && ReadBoolArray(property, OffBeatsField, 0),
                OffBeat2 = active && ReadBoolArray(property, OffBeatsField, 1),
                OffBeat3 = active && ReadBoolArray(property, OffBeatsField, 2),
                OffBeat4 = active && ReadBoolArray(property, OffBeatsField, 3),
                BarPhase = ComputeBarPhase(property, active, beatInBar),
            };
        }

        /// <summary>
        /// Reconstructs the live Bar Phase (0 on the downbeat → 1 at the next downbeat) from the serialized
        /// BeatData fields.
        /// </summary>
        /// <remarks>
        /// This MIRRORS <see cref="BeatManager.BarPhase"/> line-for-line — same formula, same field reads —
        /// because the drawer only has the SerializedProperty, not the live BeatManager instance, so it cannot
        /// call the runtime property directly. Keep the two in lock-step: the strip's envelope shape comes from
        /// the same <see cref="Waveform.Evaluate"/> as runtime (so it cannot drift), and this is the one place
        /// the playhead position could. WaveformTests pins the runtime BarPhase formula.
        /// </remarks>
        private static float ComputeBarPhase(SerializedProperty property, bool active, int beatInBar)
        {
            if (!active)
            {
                return 0f;
            }

            var beatsPerMeasure = ReadInt(property, BeatsPerMeasureField);
            if (beatsPerMeasure <= 0)
            {
                beatsPerMeasure = BeatSlotCount;
            }

            var label = beatInBar;
            if (label < 1 || label > beatsPerMeasure)
            {
                return 0f; // beat label not yet known / inert
            }

            // Read the countdown to the *next* beat label so the fraction grows 0 -> 1 across this beat.
            var nextSlot = label % beatsPerMeasure; // 0-based slot of the next label (wraps last -> 0)
            var msToNext = ReadIntArray(property, BeatsCountMsField, nextSlot);
            var beatAverageMs = ReadInt(property, BeatAverageMsField);

            var intraBeatPhase = 0f;
            if (beatAverageMs > 0 && msToNext >= 0)
            {
                intraBeatPhase = Mathf.Clamp01(1f - ((float)msToNext / beatAverageMs));
            }

            return ((label - 1) + intraBeatPhase) / beatsPerMeasure;
        }

        /// <summary>Returns the offbeat gate for a zero-based offbeat marker slot.</summary>
        public bool IsOffBeatGateActive(int index)
        {
            switch (index)
            {
                case 0:
                    return OffBeat1;
                case 1:
                    return OffBeat2;
                case 2:
                    return OffBeat3;
                case 3:
                    return OffBeat4;
                default:
                    return false;
            }
        }
    }
}
