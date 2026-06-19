using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The unified BeatManager dashboard: one Inspector panel for every rhythm value, raw and contrived.
/// </summary>
/// <remarks>
/// Top to bottom: a header with the SIM/LIVE/OFFLINE badge, track, players, and BPM; the beat clock zone
/// (quarter-note beat dots and offbeat markers on the left, beat/offbeat/eighth pulse meters on the right,
/// countdown chips below); the Waveform strip with its two-way wall selector; and the contrived rhythm
/// query rows (ADR-0002), each rendered exactly as effects consume it — a meter/value when non-null
/// (Synced Mode) and a dimmed null marker when null (the caller's Standalone response). The raw serialized
/// fields, including <c>beatData</c> via Unity's plain default foldout, follow underneath as the
/// raw-values debug view.
///
/// Every displayed value is read from the LIVE BeatManager instance, resolved through
/// <see cref="PropertyDrawer.fieldInfo"/> — no SerializedProperty reconstruction and no hand-mirrored
/// runtime formulas, so the panel cannot drift from what the runtime actually computes. Smooth Play Mode
/// animation comes from <see cref="ControllerEditor.RequiresConstantRepaint"/>; in Edit Mode the queries
/// are honestly null (no clock, no smoothed Levels), which is itself a correct picture of the contract.
/// </remarks>
[CustomPropertyDrawer(typeof(BeatManager))]
public sealed class BeatManagerDrawer : PropertyDrawer
{
    private const int BeatSlotCount = 4;

    private const string DotFilled = "●";
    private const string DotEmpty = "○";
    private const string OffBeatActive = "◆";
    private const string OffBeatInactive = "◇";

    // Vertical budget. PanelHeight is derived so the layout, the panel rect, and GetPropertyHeight stay in sync.
    // PanelPadding is added at both the top and the bottom so the swatch row keeps a margin instead of sitting
    // flush against the edge.
    private const float PanelPadding = 12f;
    private const float HeaderHeight = 22f;
    private const float HeaderGap = 12f;
    private const float BodyHeight = 78f;
    private const float BodyGap = 12f;
    private const float ChipHeight = 24f;
    private const float SectionGap = 12f;
    private const float QueriesHeaderHeight = 18f;
    private const float QueriesHeaderGap = 10f;
    private const float QueryRowHeight = 22f;
    private const float QueryRowGap = 6f;
    private const float SwatchRowHeight = 26f;
    private const int QueryRowCount = 6; // envelope, fill, drop, energy, phase, levels
    private const float PanelHeight =
        PanelPadding + HeaderHeight + HeaderGap + BodyHeight + BodyGap + ChipHeight
        + WaveformStripGap + WaveformSelectorHeight + WaveformSelectorGap + WaveformStripHeight
        + SectionGap + QueriesHeaderHeight + QueriesHeaderGap
        + (QueryRowCount * (QueryRowHeight + QueryRowGap))
        + SwatchRowHeight + PanelPadding;

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

    // Waveform strip: a Preset-picker row, then a row plotting the chosen envelope with a live Bar Phase playhead.
    private const float WaveformStripGap = 12f;
    private const float WaveformSelectorHeight = 20f;
    private const float WaveformSelectorGap = 6f;
    private const float WaveformStripHeight = 46f;
    private const float WaveformValueWidth = 46f;

    // Horizontal anatomy of a query row: label column, optional status chip, meter, right-aligned readout.
    private const float QueryRowLabelWidth = 70f;
    private const float RightTextWidth = 150f;
    private const float StatusChipWidth = 46f;
    private const float PhaseLabelWidth = 90f;
    private const float SegmentGap = 6f;

    private const string NullValueText = "—  null → Standalone";

    private const string EnvelopeTooltip =
        "Envelope(variant): the Waveform Pool envelope evaluated at the current Bar Phase — the primitive under " +
        "BeatBrightness/BeatTime. Shows the wall's effective variant (the lock, or the on-screen effect's). " +
        "Null: no beat clock is running.";
    private const string FillTooltip =
        "Fill: a short build-up flourish. IN n = counting down (the bar fills as it approaches over the last " +
        "32 beats); NOW = riding it (the bar sweeps its progress). next — · ×0 = the track's Fills are all " +
        "behind the playhead. Null: no Fill data on the wire right now.";
    private const string DropTooltip =
        "Drop: the payoff section, same anatomy as Fill — the bar fills toward the start (land transitions " +
        "on it), then sweeps its progress while it plays. next — · ×0 = no Drops left ahead. Null: no Drop " +
        "data on the wire right now.";
    private const string EnergyTooltip =
        "Energy: the track's intensity tier in the closed Low/Mid/High vocabulary, with where it is heading " +
        "and the changes still ahead. The bar sweeps the current same-energy run. Null: unavailable, or the " +
        "wire label was unrecognized.";
    private const string PhaseTooltip =
        "Phase: the track's current section label (open vocabulary — display it, don't keyword-parse it), with " +
        "contrived progress and the upcoming section. Null: no phase data on the wire right now.";
    private const string LevelsTooltip =
        "Levels: low/mid/high band energy with BeatManager's attack/release smoothing already applied " +
        "(fast up, slow down — anti-flicker). Null: no live Levels; the local simulator never supplies them.";
    private const string ColorTooltip =
        "Color Bank, the three contrived Levels colors — RGB: bands mapped straight onto red/green/blue channels; " +
        "HUE: spectral-centroid hue, dominance saturation, strongest-band value; PAL: the live AnimPalette read " +
        "at the bands' centroid, scaled by the strongest band.";

    /// <summary>Bottom chip labels in draw order: beat countdown, beat gate, offbeat countdown, offbeat gate.</summary>
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
    private static readonly Color WaveformCurveIdleColor = new Color(0.34f, 0.42f, 0.47f);
    private static readonly Color EnvelopeMeterColor = new Color(0.12f, 0.92f, 1f);
    private static readonly Color FillNowChipColor = new Color(0.10f, 0.42f, 0.22f);
    private static readonly Color FillSoonChipColor = new Color(0.08f, 0.28f, 0.20f);
    private static readonly Color FillMeterColor = new Color(0.35f, 0.95f, 0.55f);
    private static readonly Color DropNowChipColor = new Color(0.44f, 0.12f, 0.30f);
    private static readonly Color DropSoonChipColor = new Color(0.24f, 0.10f, 0.22f);
    private static readonly Color DropMeterColor = new Color(1f, 0.42f, 0.62f);
    private static readonly Color PhraseEventIdleChipColor = new Color(0.20f, 0.21f, 0.24f);
    private static readonly Color EnergyLowChipColor = new Color(0.10f, 0.30f, 0.45f);
    private static readonly Color EnergyMidChipColor = new Color(0.46f, 0.34f, 0.06f);
    private static readonly Color EnergyHighChipColor = new Color(0.50f, 0.12f, 0.16f);
    private static readonly Color EnergyMeterColor = new Color(1f, 0.72f, 0.25f);
    private static readonly Color PhaseMeterColor = new Color(0.62f, 0.55f, 1f);
    private static readonly Color LowBandColor = new Color(0.95f, 0.40f, 0.30f);
    private static readonly Color MidBandColor = new Color(0.40f, 0.90f, 0.45f);
    private static readonly Color HighBandColor = new Color(0.40f, 0.60f, 1f);
    private static readonly Color SwatchBorderColor = new Color(1f, 1f, 1f, 0.18f);

    private static GUIStyle titleStyle;
    private static GUIStyle rowLabelStyle;
    private static GUIStyle valueStyle;
    private static GUIStyle badgeStyle;
    private static GUIStyle dotStyle;
    private static GUIStyle markerLabelStyle;
    private static GUIStyle chipLabelStyle;
    private static GUIStyle chipValueStyle;
    private static GUIStyle chipValueRightStyle;
    private static GUIStyle queriesHeaderStyle;
    private static GUIStyle hintStyle;
    private static GUIStyle nullStyle;
    private static GUIStyle nullCenterStyle;
    private static GUIStyle statusChipStyle;
    private static GUIStyle phaseTextStyle;
    private static GUIStyle bandLabelStyle;

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

    /// <summary>Draws the foldout, the unified rhythm dashboard, and the regular serialized child fields.</summary>
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

                var panelRect = EditorGUI.IndentedRect(new Rect(line.x, line.y, line.width, PanelHeight));
                DrawDashboard(panelRect, ResolveBeatManager(property), property.serializedObject.targetObject);
                line.y += PanelHeight + EditorGUIUtility.standardVerticalSpacing;

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

        height += EditorGUIUtility.standardVerticalSpacing + PanelHeight + EditorGUIUtility.standardVerticalSpacing;

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
    /// The Waveform Pool variant the Envelope row displays: the wall-wide lock when one is set, otherwise the
    /// on-screen effect's live variant (Play Mode only — <see cref="Controller.CurrentBeatVariant"/> reads the
    /// effects array, which does not exist in Edit Mode), falling back to Pool index 0.
    /// </summary>
    private static int ResolveDisplayVariant(BeatManager beatManager, Object owner)
    {
        // Editor-context step only: figure out the on-screen effect's variant (Play Mode only — the effects
        // array does not exist in Edit Mode), then let the runtime decide lock vs on-screen vs fallback.
        var onScreenVariant = (Application.isPlaying && owner is Controller controller) ? controller.CurrentBeatVariant : -1;
        return beatManager.ResolveDisplayVariant(onScreenVariant);
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

    /// <summary>
    /// Draws the dashboard: header, beat clock zone, countdown chips, Waveform strip + selector, and the
    /// contrived rhythm query rows with the Color Bank swatches.
    /// </summary>
    private static void DrawDashboard(Rect rect, BeatManager beatManager, Object owner)
    {
        var active = beatManager != null && beatManager.IsActive;
        var live = beatManager != null && beatManager.IsLiveSource;

        var accent = !active ? PanelOfflineAccentColor : live ? PanelLiveAccentColor : PanelSimAccentColor;
        DrawPanelBackground(rect, accent);

        var content = new Rect(
            rect.x + PanelPadding,
            rect.y + PanelPadding,
            Mathf.Max(0f, rect.width - (PanelPadding * 2f)),
            rect.height - (PanelPadding * 2f));

        DrawHeader(new Rect(content.x, content.y, content.width, HeaderHeight), beatManager, active, live);

        var body = new Rect(content.x, content.y + HeaderHeight + HeaderGap, content.width, BodyHeight);
        var leftWidth = Mathf.Min(LeftZoneWidth, body.width * LeftZoneMaxFraction);
        var leftZone = new Rect(body.x, body.y, leftWidth, body.height);
        DrawPositionZone(leftZone, beatManager, active);

        var dividerX = leftZone.xMax + (ZoneDividerGap * 0.5f);
        EditorGUI.DrawRect(new Rect(dividerX, body.y + 2f, 1f, body.height - 4f), DividerColor);

        var rightX = leftZone.xMax + ZoneDividerGap;
        DrawPulseZone(new Rect(rightX, body.y, Mathf.Max(0f, body.xMax - rightX), body.height), beatManager, active);

        var chipsRect = new Rect(content.x, body.yMax + BodyGap, content.width, ChipHeight);
        DrawCountdownChips(chipsRect, beatManager);

        var selectorRect = new Rect(content.x, chipsRect.yMax + WaveformStripGap, content.width, WaveformSelectorHeight);
        DrawWaveformSelector(selectorRect);

        var stripRect = new Rect(content.x, selectorRect.yMax + WaveformSelectorGap, content.width, WaveformStripHeight);
        DrawWaveformStrip(stripRect, active, active ? beatManager.BarPhase : 0f);

        var queriesY = stripRect.yMax + SectionGap;
        GUI.Label(new Rect(content.x, queriesY, content.width * 0.5f, QueriesHeaderHeight), "RHYTHM QUERIES", queriesHeaderStyle);
        GUI.Label(new Rect(content.x + (content.width * 0.5f), queriesY, content.width * 0.5f, QueriesHeaderHeight),
            "— = null → Standalone", hintStyle);

        var y = queriesY + QueriesHeaderHeight + QueriesHeaderGap;
        DrawEnvelopeRow(new Rect(content.x, y, content.width, QueryRowHeight), beatManager, owner);
        y += QueryRowHeight + QueryRowGap;

        DrawFillRow(new Rect(content.x, y, content.width, QueryRowHeight), beatManager);
        y += QueryRowHeight + QueryRowGap;

        DrawDropRow(new Rect(content.x, y, content.width, QueryRowHeight), beatManager);
        y += QueryRowHeight + QueryRowGap;

        DrawEnergyRow(new Rect(content.x, y, content.width, QueryRowHeight), beatManager);
        y += QueryRowHeight + QueryRowGap;

        DrawPhaseRow(new Rect(content.x, y, content.width, QueryRowHeight), beatManager);
        y += QueryRowHeight + QueryRowGap;

        DrawLevelsRow(new Rect(content.x, y, content.width, QueryRowHeight), beatManager);
        y += QueryRowHeight + QueryRowGap;

        DrawColorRow(new Rect(content.x, y, content.width, SwatchRowHeight), beatManager);
    }

    /// <summary>Draws the SIM/LIVE/OFFLINE badge, current track text, and players + BPM.</summary>
    private static void DrawHeader(Rect rect, BeatManager beatManager, bool active, bool live)
    {
        var badgeColor = !active ? OfflineBadgeColor : live ? LiveBadgeColor : SimBadgeColor;
        var badgeText = !active ? "OFFLINE" : live ? "LIVE" : "SIM";
        var badgeRect = new Rect(rect.x, rect.y, 74f, rect.height);
        EditorGUI.DrawRect(badgeRect, badgeColor);
        GUI.Label(badgeRect, badgeText, badgeStyle);

        const float rightWidth = 132f;
        var titleWidth = Mathf.Max(0f, rect.width - badgeRect.width - rightWidth - 16f);
        var titleRect = new Rect(badgeRect.xMax + 8f, rect.y, titleWidth, rect.height);
        GUI.Label(titleRect, beatManager?.Track ?? "—", titleStyle);

        var bpmText = beatManager?.Bpm is { } bpm ? $"{bpm:0.##} BPM" : "-- BPM";
        var rightText = beatManager?.PlayersLive is { } players && live ? $"{players} · {bpmText}" : bpmText;
        var rightRect = new Rect(rect.xMax - rightWidth, rect.y, rightWidth, rect.height);
        GUI.Label(rightRect, rightText, valueStyle);
    }

    /// <summary>Draws the left position zone: quarter-note beat dots, offbeat markers, and shared column labels.</summary>
    private static void DrawPositionZone(Rect zone, BeatManager beatManager, bool active)
    {
        var beatRow = new Rect(zone.x, zone.y + 2f, zone.width, DotRowHeight);
        DrawBeatDotsRow(beatRow, beatManager, active);

        var offRow = new Rect(zone.x, beatRow.yMax + DotRowGap, zone.width, DotRowHeight);
        DrawOffBeatRow(offRow, beatManager, active);

        DrawColumnLabels(new Rect(zone.x, offRow.yMax, zone.width, ColumnLabelHeight));
    }

    /// <summary>Draws the four quarter-note beat markers in the position zone from the live raw queries.</summary>
    private static void DrawBeatDotsRow(Rect rect, BeatManager beatManager, bool active)
    {
        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), "BEAT", rowLabelStyle);

        // BeatInBar is null when the label is unknown — same disabled rendering as no clock at all.
        var beatInBar = beatManager?.BeatInBar ?? -1;
        var onBeat = beatManager?.OnBeat ?? false;
        var beatPulse = Mathf.Clamp01(beatManager?.Pulse ?? 0f);

        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatSlotCount; i++)
        {
            var beatLabel = i + 1;
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            var glyph = BuildBeatDotGlyph(active, beatInBar, beatLabel);
            var color = GetBeatDotColor(active, beatLabel, beatInBar, onBeat, beatPulse);
            DrawMarker(markerRect, glyph, color);
        }
    }

    /// <summary>Draws the four offbeat markers, lit when their derived offbeat gate is active.</summary>
    private static void DrawOffBeatRow(Rect rect, BeatManager beatManager, bool active)
    {
        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), "OFFBEAT", rowLabelStyle);

        var gates = beatManager?.OffBeats;
        var offBeatPulse = Mathf.Clamp01(beatManager?.OffBeatPulse ?? 0f);

        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatSlotCount; i++)
        {
            var enabled = active && gates != null && i < gates.Length && gates[i];
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            var color = enabled
                ? Color.Lerp(OffBeatSteadyColor, OffBeatFlashColor, offBeatPulse)
                : active ? OffBeatInactiveColor : OffBeatDisabledColor;
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
    private static void DrawPulseZone(Rect rect, BeatManager beatManager, bool active)
    {
        var beatPulse = Mathf.Clamp01(beatManager?.Pulse ?? 0f);
        var offBeatPulse = Mathf.Clamp01(beatManager?.OffBeatPulse ?? 0f);
        var eighthPulse = GetClampedEighthPulseValue(beatPulse, offBeatPulse);

        var step = MeterRowHeight + MeterRowGap;
        DrawPulseMeterRow(new Rect(rect.x, rect.y, rect.width, MeterRowHeight), "BEAT", beatPulse, BeatMeterColor, active);
        DrawPulseMeterRow(new Rect(rect.x, rect.y + step, rect.width, MeterRowHeight), "OFF", offBeatPulse, OffBeatMeterColor, active);
        DrawPulseMeterRow(new Rect(rect.x, rect.y + (step * 2f), rect.width, MeterRowHeight), "8TH", eighthPulse, EighthMeterColor, active);
    }

    /// <summary>Draws one labelled pulse meter row with a bar and right-aligned value.</summary>
    private static void DrawPulseMeterRow(Rect rect, string label, float pulse, Color color, bool active)
    {
        GUI.Label(new Rect(rect.x, rect.y, MeterLabelWidth, rect.height), label, rowLabelStyle);

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

    /// <summary>Draws the four bottom chips for beat/offbeat countdowns and gate state from the raw queries.</summary>
    private static void DrawCountdownChips(Rect rect, BeatManager beatManager)
    {
        var chipWidth = Mathf.Max(0f, (rect.width - (ChipGap * 3f)) / 4f);
        var step = chipWidth + ChipGap;

        DrawCountdownChip(new Rect(rect.x, rect.y, chipWidth, rect.height),
            CountdownChipLabels[0], FormatMs(beatManager?.NextBeatMs), BeatChipColor, alignValueRight: true);
        DrawCountdownChip(new Rect(rect.x + step, rect.y, chipWidth, rect.height),
            CountdownChipLabels[1], beatManager?.OnBeat == true ? "YES" : "NO", OnBeatChipColor);
        DrawCountdownChip(new Rect(rect.x + (step * 2f), rect.y, chipWidth, rect.height),
            CountdownChipLabels[2], FormatMs(beatManager?.NextOffBeatMs), OffBeatChipColor, alignValueRight: true);
        DrawCountdownChip(new Rect(rect.x + (step * 3f), rect.y, chipWidth, rect.height),
            CountdownChipLabels[3], beatManager?.OffBeat == true ? "YES" : "NO", OffBeatGateChipColor);
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
        // Editor instance-resolution guard, one consumer today. If a second consumer needs to drive the wall
        // safely from the editor, this is the extraction point for a shared LiveWall adapter.
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
            beatManager.ReleaseToAuto();
            return;
        }

        // Lock semantics (clamp, log) live on BeatManager so they are testable; the editor only supplies the
        // chosen index and the one-line on-screen retarget that needs the effects array.
        beatManager.LockVariant(dropdownIndex - 1);
        if (Controller.HasInstance)
        {
            Controller.Instance.CurrentBeatVariant = beatManager.activeVariant; // immediate: retarget the effect on screen
        }
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

        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), live ? "WALL" : "PREVIEW", rowLabelStyle);

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
    /// Draws the Waveform strip: the wall's effective envelope (per <see cref="SelectedWaveform"/>), with beat
    /// gridlines behind it and, when live, a playhead at the current Bar Phase marking where "now" sits and the
    /// brightness being emitted.
    /// </summary>
    /// <remarks>
    /// Both the envelope shape and the playhead position come straight from the runtime: the curve is sampled
    /// from the same <see cref="Waveform.Evaluate"/> the runtime uses, and <paramref name="barPhase"/> is the
    /// live <see cref="BeatManager.BarPhase"/> itself — nothing here is reconstructed, so the strip cannot
    /// drift from runtime shaping. Together with the selector above, this makes both the live clock and the
    /// rhythm it is driving visible — and editable — in the Inspector.
    /// </remarks>
    private static void DrawWaveformStrip(Rect rect, bool active, float barPhase)
    {
        var plotRight = rect.xMax - WaveformValueWidth - 6f;
        var plot = new Rect(rect.x, rect.y + 2f, Mathf.Max(0f, plotRight - rect.x), rect.height - 4f);

        var wf = SelectedWaveform();

        // Shared plot draws track/grid/curve and, while live, the aligned playhead; the curve color carries
        // this view's active/idle state.
        WaveformPlot.Draw(plot, wf, active ? WaveformPlot.Curve : WaveformCurveIdleColor, active ? barPhase : (float?)null);

        // Numeric readout to the right of the plot — dashboard-only, laid out outside the shared plot rect.
        var readout = new Rect(rect.xMax - WaveformValueWidth, rect.y, WaveformValueWidth, rect.height);
        if (active)
        {
            var emitted = Mathf.Clamp01(wf.Evaluate(barPhase));
            GUI.Label(readout, $"{emitted:0.00}", valueStyle);
        }
        else
        {
            GUI.Label(readout, "--", valueStyle);
        }
    }

    /// <summary>Draws the Envelope row: the variant's Waveform value at the current Bar Phase.</summary>
    private static void DrawEnvelopeRow(Rect row, BeatManager beatManager, Object owner)
    {
        var content = DrawQueryRowLabel(row, "ENVELOPE", EnvelopeTooltip);
        if (beatManager == null)
        {
            DrawNullValue(content);
            return;
        }

        var variant = ResolveDisplayVariant(beatManager, owner);
        if (!(beatManager.Envelope(variant) is { } envelope))
        {
            DrawNullValue(content);
            return;
        }

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, envelope, EnvelopeMeterColor);
        GUI.Label(right, $"{envelope:0.00} · var {variant}", valueStyle);
    }

    /// <summary>Draws the Fill row from the contrived PhraseEventInfo.</summary>
    private static void DrawFillRow(Rect row, BeatManager beatManager)
    {
        var content = DrawQueryRowLabel(row, "FILL", FillTooltip);
        if (beatManager?.Fill is { } fill)
        {
            DrawPhraseEventContent(content, fill, FillNowChipColor, FillSoonChipColor, FillMeterColor);
        }
        else
        {
            DrawNullValue(content);
        }
    }

    /// <summary>Draws the Drop row from the contrived PhraseEventInfo.</summary>
    private static void DrawDropRow(Rect row, BeatManager beatManager)
    {
        var content = DrawQueryRowLabel(row, "DROP", DropTooltip);
        if (beatManager?.Drop is { } drop)
        {
            DrawPhraseEventContent(content, drop, DropNowChipColor, DropSoonChipColor, DropMeterColor);
        }
        else
        {
            DrawNullValue(content);
        }
    }

    /// <summary>
    /// Draws the shared Fill/Drop anatomy from the live <see cref="PhraseEventInfo"/>: a NOW / "IN n" / —
    /// chip, one bar that fills with anticipation while counting down and with progress while in progress,
    /// and a length/remaining readout. Null fields render as — and zero renders as zero — the row asserts
    /// nothing the data does not say.
    /// </summary>
    private static void DrawPhraseEventContent(Rect content, PhraseEventInfo info, Color nowColor,
        Color soonColor, Color meterColor)
    {
        var view = PhraseEventView.Of(info);

        var chip = TakeLeft(ref content, StatusChipWidth);
        var chipColor = view.State switch
        {
            PhraseEventState.Now => nowColor,
            PhraseEventState.Soon => soonColor,
            _ => PhraseEventIdleChipColor,
        };
        DrawStatusChip(chip, view.Chip, chipColor);

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, view.Meter, meterColor);
        GUI.Label(right, view.Readout, valueStyle);
    }

    /// <summary>Draws the Energy row: tier chip, normalized meter, and where the energy is heading.</summary>
    private static void DrawEnergyRow(Rect row, BeatManager beatManager)
    {
        var content = DrawQueryRowLabel(row, "ENERGY", EnergyTooltip);
        if (!(beatManager?.Energy is { } energy))
        {
            DrawNullValue(content);
            return;
        }

        var chip = TakeLeft(ref content, StatusChipWidth);
        DrawStatusChip(chip, energy.level.ToString().ToUpperInvariant(), GetEnergyChipColor(energy.level));

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, energy.runProgress ?? 0f, EnergyMeterColor);

        var arrow = energy.direction > 0 ? "↗" : energy.direction < 0 ? "↘" : "→";
        var heading = energy.next is { } next
            ? $"{arrow} {next.ToString().ToUpperInvariant()} in {RhythmText.Beats(energy.beatsUntilChange)}"
            : "steady";
        GUI.Label(right, $"{heading} · ×{RhythmText.Count(energy.changesRemaining)}", valueStyle);
    }

    /// <summary>Draws the Phase row: the open-vocabulary section label, progress, and the upcoming section.</summary>
    private static void DrawPhaseRow(Rect row, BeatManager beatManager)
    {
        var content = DrawQueryRowLabel(row, "PHASE", PhaseTooltip);
        if (!(beatManager?.Phase is { } phase))
        {
            DrawNullValue(content);
            return;
        }

        var labelRect = TakeLeft(ref content, PhaseLabelWidth);
        GUI.Label(labelRect, phase.label, phaseTextStyle);

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, phase.progress ?? 0f, PhaseMeterColor);

        var heading = phase.next != null
            ? $"→ {phase.next} in {RhythmText.Beats(phase.beatsUntilNext)}"
            : $"len {RhythmText.Count(phase.lengthBeats)}";
        GUI.Label(right, heading, valueStyle);
    }

    /// <summary>Draws the Levels row: three mini meters for the smoothed low/mid/high bands.</summary>
    private static void DrawLevelsRow(Rect row, BeatManager beatManager)
    {
        var content = DrawQueryRowLabel(row, "LEVELS", LevelsTooltip);
        if (!(beatManager?.Levels is { } levels))
        {
            DrawNullValue(content);
            return;
        }

        var segmentWidth = Mathf.Max(0f, (content.width - (SegmentGap * 2f)) / 3f);
        DrawBandSegment(new Rect(content.x, content.y, segmentWidth, content.height), "L", levels.low, LowBandColor);
        DrawBandSegment(new Rect(content.x + segmentWidth + SegmentGap, content.y, segmentWidth, content.height), "M", levels.mid, MidBandColor);
        DrawBandSegment(new Rect(content.x + ((segmentWidth + SegmentGap) * 2f), content.y, segmentWidth, content.height), "H", levels.high, HighBandColor);
    }

    /// <summary>Draws one band's mini label + meter + value inside the Levels row.</summary>
    private static void DrawBandSegment(Rect rect, string label, float value, Color color)
    {
        GUI.Label(new Rect(rect.x, rect.y, 14f, rect.height), label, bandLabelStyle);
        var valueRect = new Rect(rect.xMax - 36f, rect.y, 36f, rect.height);
        var meter = new Rect(rect.x + 16f, rect.y, Mathf.Max(0f, rect.width - 16f - 38f), rect.height);
        DrawMeter(meter, value, color);
        GUI.Label(valueRect, $"{value:0.00}", valueStyle);
    }

    /// <summary>Draws the Color Bank row: the RGB, HUE, and PAL swatches, each going dark when null.</summary>
    private static void DrawColorRow(Rect row, BeatManager beatManager)
    {
        var content = DrawQueryRowLabel(row, "COLOR", ColorTooltip);
        var segmentWidth = Mathf.Max(0f, (content.width - (SegmentGap * 2f)) / 3f);
        DrawSwatch(new Rect(content.x, content.y, segmentWidth, content.height), "RGB", beatManager?.LevelsRgb);
        DrawSwatch(new Rect(content.x + segmentWidth + SegmentGap, content.y, segmentWidth, content.height), "HUE", beatManager?.LevelsHue);
        DrawSwatch(new Rect(content.x + ((segmentWidth + SegmentGap) * 2f), content.y, segmentWidth, content.height), "PAL", beatManager?.LevelsPalette);
    }

    /// <summary>Draws one labelled Color Bank swatch, or a dark null box when the query returned null.</summary>
    private static void DrawSwatch(Rect rect, string label, Color? color)
    {
        GUI.Label(new Rect(rect.x, rect.y, 30f, rect.height), label, bandLabelStyle);

        var box = new Rect(rect.x + 32f, rect.y + 3f, Mathf.Max(0f, rect.width - 32f), rect.height - 6f);
        EditorGUI.DrawRect(box, SwatchBorderColor);

        var inner = new Rect(box.x + 1f, box.y + 1f, Mathf.Max(0f, box.width - 2f), Mathf.Max(0f, box.height - 2f));
        if (color is { } value)
        {
            // The wall has no alpha channel; show the color opaque so a low palette alpha cannot read as "off".
            EditorGUI.DrawRect(inner, new Color(value.r, value.g, value.b, 1f));
        }
        else
        {
            EditorGUI.DrawRect(inner, MeterTrackColor);
            GUI.Label(inner, "—", nullCenterStyle);
        }
    }

    /// <summary>Draws a query row's name label (with its explanatory tooltip) and returns the content area.</summary>
    private static Rect DrawQueryRowLabel(Rect row, string text, string tooltip)
    {
        GUI.Label(new Rect(row.x, row.y, QueryRowLabelWidth, row.height), new GUIContent(text, tooltip), rowLabelStyle);
        return new Rect(row.x + QueryRowLabelWidth + 4f, row.y, Mathf.Max(0f, row.width - QueryRowLabelWidth - 4f), row.height);
    }

    /// <summary>Draws the dimmed marker for a query that is currently null — the consumer's Standalone response.</summary>
    private static void DrawNullValue(Rect content)
    {
        GUI.Label(content, NullValueText, nullStyle);
    }

    /// <summary>Draws one horizontal 0..1 meter with the shared track/shine treatment.</summary>
    private static void DrawMeter(Rect rect, float value, Color color)
    {
        var bar = new Rect(rect.x, rect.y + ((rect.height - BarHeight) * 0.5f), rect.width, BarHeight);
        EditorGUI.DrawRect(bar, MeterTrackColor);

        var fill = new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value), bar.height);
        EditorGUI.DrawRect(fill, color);
        EditorGUI.DrawRect(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f)), new Color(1f, 1f, 1f, 0.22f));
    }

    /// <summary>Draws one small single-line status chip (NOW/SOON, LOW/MID/HIGH).</summary>
    private static void DrawStatusChip(Rect rect, string text, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));
        GUI.Label(rect, text, statusChipStyle);
    }

    /// <summary>Draws one two-line countdown chip with a label over a bold value so labels never truncate.</summary>
    /// <remarks>Right-aligned values (countdowns) keep their trailing unit pinned so they do not reflow as digits change.</remarks>
    private static void DrawCountdownChip(Rect rect, string label, string value, Color color, bool alignValueRight = false)
    {
        EditorGUI.DrawRect(rect, color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));

        var textWidth = Mathf.Max(0f, rect.width - 16f);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, textWidth, 10f), label, chipLabelStyle);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 11f, textWidth, rect.height - 12f), value, alignValueRight ? chipValueRightStyle : chipValueStyle);
    }

    /// <summary>Maps an EnergyLevel tier to its chip color.</summary>
    private static Color GetEnergyChipColor(EnergyLevel level)
    {
        switch (level)
        {
            case EnergyLevel.Low:
                return EnergyLowChipColor;
            case EnergyLevel.Mid:
                return EnergyMidChipColor;
            default:
                return EnergyHighChipColor;
        }
    }

    /// <summary>Splits a fixed-width area off the left of <paramref name="rect"/> and returns it.</summary>
    private static Rect TakeLeft(ref Rect rect, float width)
    {
        var left = new Rect(rect.x, rect.y, width, rect.height);
        rect = new Rect(rect.x + width + SegmentGap, rect.y, Mathf.Max(0f, rect.width - width - SegmentGap), rect.height);
        return left;
    }

    /// <summary>Splits a fixed-width area off the right of <paramref name="rect"/> and returns it.</summary>
    private static Rect TakeRight(ref Rect rect, float width)
    {
        var right = new Rect(rect.xMax - width, rect.y, width, rect.height);
        rect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - width - SegmentGap), rect.height);
        return right;
    }

    /// <summary>Formats a nullable millisecond countdown, using -- when the countdown is unavailable.</summary>
    /// <remarks>The chip draws this right-aligned so the "ms" stays pinned and digits grow leftward as the value changes.</remarks>
    private static string FormatMs(int? value)
    {
        return value is { } ms ? $"{ms}ms" : "--";
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

    /// <summary>
    /// Builds the four-dot beat row as one glyph string: RaveSystem-style filled dots up to the current
    /// musical beat, empty-dot placeholders when the clock is inactive or the beat label is unknown.
    /// Pure visual-model seam shared with tests.
    /// </summary>
    internal static string BuildBeatDotGlyphs(bool active, int beatInBar)
    {
        var glyphs = string.Empty;
        for (var beatLabel = 1; beatLabel <= BeatSlotCount; beatLabel++)
        {
            glyphs += BuildBeatDotGlyph(active, beatInBar, beatLabel);
        }

        return glyphs;
    }

    /// <summary>Returns the single glyph for one musical beat label.</summary>
    private static string BuildBeatDotGlyph(bool active, int beatInBar, int beatLabel)
    {
        return active && beatLabel >= 1 && beatLabel <= beatInBar && beatInBar <= BeatSlotCount ? DotFilled : DotEmpty;
    }

    /// <summary>Returns the stronger beat/offbeat pulse after clamping both inputs to the 0..1 Inspector meter range.
    /// Pure visual-model seam shared with tests.</summary>
    internal static float GetClampedEighthPulseValue(float beatPulse, float offBeatPulse)
    {
        return Mathf.Max(Mathf.Clamp01(beatPulse), Mathf.Clamp01(offBeatPulse));
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
        rowLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
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
        queriesHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.70f, 0.95f, 0.72f) },
            alignment = TextAnchor.MiddleLeft,
        };
        hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.45f, 0.50f, 0.55f) },
            alignment = TextAnchor.MiddleRight,
        };
        nullStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.45f, 0.50f, 0.55f) },
            alignment = TextAnchor.MiddleLeft,
        };
        nullCenterStyle = new GUIStyle(nullStyle)
        {
            alignment = TextAnchor.MiddleCenter,
        };
        statusChipStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
        };
        phaseTextStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.86f, 0.96f, 1f) },
            alignment = TextAnchor.MiddleLeft,
            fontSize = 11,
            clipping = TextClipping.Clip,
        };
        bandLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.76f, 0.82f, 0.84f) },
            alignment = TextAnchor.MiddleLeft,
        };
    }
}
