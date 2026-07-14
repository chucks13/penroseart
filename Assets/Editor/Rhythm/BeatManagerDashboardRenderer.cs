using UnityEditor;
using UnityEngine;

/// <summary>
/// Responsive IMGUI renderer for the BeatManager Rhythm dashboard.
/// </summary>
/// <remarks>
/// This module owns layout, colors, styles, and widgets. It consumes <see cref="BeatManagerDashboardModel"/>
/// instead of reading runtime state directly, keeping Unity drawing separate from the dashboard's display
/// decisions. The Tuning Window and legacy drawer share this renderer; user actions return to the drawer adapter
/// so editor-only preview selection stays outside the runtime.
/// </remarks>
internal static class BeatManagerDashboardRenderer
{
    private const float PanelPadding = 12f;
    private const float HeaderHeight = 22f;
    private const float HeaderGap = 12f;
    private const float BodyHeight = 78f;
    private const float BodyGap = 12f;
    private const float ChipHeight = 24f;
    /// <summary>Height of one semantic dashboard group heading.</summary>
    private const float GroupHeaderHeight = 18f;
    /// <summary>Space between one group heading and its first fact.</summary>
    private const float GroupHeaderGap = 8f;
    /// <summary>Space between adjacent semantic groups.</summary>
    private const float GroupGap = 14f;
    private const float QueryRowHeight = 22f;
    private const float QueryRowGap = 6f;

    /// <summary>The conservative stacked height used by narrow property-drawer callers.</summary>
    public const float DashboardHeight = 708f;

    private const float BarHeight = 8f;

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

    private const float MeterRowHeight = 22f;
    private const float MeterRowGap = 6f;
    private const float MeterLabelWidth = 40f;
    /// <summary>Width reserved for a numeric pulse or the full unavailable label.</summary>
    private const float MeterValueWidth = 76f;

    private const float ChipGap = 8f;

    private const float WaveformSelectorHeight = 20f;
    private const float WaveformSelectorGap = 6f;
    private const float WaveformStripHeight = 46f;
    private const float WaveformValueWidth = 46f;

    /// <summary>Width reserved for each semantic query-row label.</summary>
    private const float QueryRowLabelWidth = 84f;
    private const float RightTextWidth = 150f;
    private const float StatusChipWidth = 46f;
    private const float PhraseLabelWidth = 90f;
    private const float SegmentGap = 6f;

    /// <summary>Canonical whole-row text for an unavailable display fact.</summary>
    private const string NullValueText = BeatManagerDashboardModel.UnavailableText;
    private const string OffBeatActive = "◆";
    private const string OffBeatInactive = "◇";

    /// <summary>Explains that the waveform strip is a downstream, editor-only preview.</summary>
    private const string EnvelopeTooltip =
        "Waveform.Envelope: the selected Pool shape evaluated against the current Bar Phase. " +
        "Preview only; selection does not mutate runtime state.";
    /// <summary>Explains the canonical Fill phrase-event presentation.</summary>
    private const string FillTooltip =
        "Fill: a short build-up flourish. IN n = counting down (the bar fills as it approaches over the last " +
        "32 beats); NOW = riding it (the bar sweeps its progress). next — · ×0 = the track's Fills are all " +
        "behind the playhead. Unavailable: no Fill data on the wire right now.";
    /// <summary>Explains the canonical Drop phrase-event presentation.</summary>
    private const string DropTooltip =
        "Drop: the payoff section, same anatomy as Fill — the bar fills toward the start (land transitions " +
        "on it), then sweeps its progress while it plays. next — · ×0 = no Drops left ahead. Unavailable: no Drop " +
        "data on the wire right now.";
    /// <summary>Explains current and announced next Energy presentation.</summary>
    private const string EnergyTooltip =
        "Energy: the track's intensity tier in the closed Low/Mid/High vocabulary. Current progress and the " +
        "announced next run stay separate. Unavailable means the wire lane is absent or unrecognized.";
    /// <summary>Explains current and announced next Phrase presentation.</summary>
    private const string PhraseTooltip =
        "Phrase: the track's open-vocabulary section label. Current progress and the announced next Phrase stay " +
        "separate. Unavailable means the corresponding wire lane is absent.";
    /// <summary>Explains the live Levels follower and Standalone availability contract.</summary>
    private const string LevelsTooltip =
        "Levels: low/mid/high band energy with BeatManager's attack/release smoothing already applied " +
        "(fast up, slow down — anti-flicker). Synced missing levels follow the runtime's zero contract; " +
        "Standalone Mode is unavailable.";

    private static readonly GUIContent EnvelopeLabel = new("ENVELOPE", EnvelopeTooltip);
    private static readonly GUIContent FillLabel = new("FILL", FillTooltip);
    private static readonly GUIContent DropLabel = new("DROP", DropTooltip);
    private static readonly GUIContent EnergyLabel = new("ENERGY", EnergyTooltip);
    private static readonly GUIContent PhraseLabel = new("PHRASE", PhraseTooltip);
    /// <summary>Label for announced next Energy state.</summary>
    private static readonly GUIContent NextEnergyLabel = new("NEXT ENERGY", EnergyTooltip);
    /// <summary>Label for announced next Phrase state.</summary>
    private static readonly GUIContent NextPhraseLabel = new("NEXT PHRASE", PhraseTooltip);
    /// <summary>Label and availability guidance for sender-provided Grid placement.</summary>
    private static readonly GUIContent GridLabel = new(
        "GRID",
        "Grid: the sender-provided one-based Bar and Beat placement. Unavailable means no complete Grid fact was broadcast.");
    private static readonly GUIContent LevelsLabel = new("LEVELS", LevelsTooltip);

    private static readonly Color PanelBackgroundColor = new(0.035f, 0.04f, 0.055f);
    private static readonly Color PanelLiveAccentColor = new(0.10f, 0.85f, 0.95f);
    private static readonly Color PanelOfflineAccentColor = new(0.45f, 0.12f, 0.16f);
    private static readonly Color LiveBadgeColor = new(0.02f, 0.38f, 0.30f);
    private static readonly Color OfflineBadgeColor = new(0.28f, 0.08f, 0.08f);
    private static readonly Color DividerColor = new(1f, 1f, 1f, 0.08f);
    private static readonly Color MeterTrackColor = new(0.07f, 0.08f, 0.10f);
    private static readonly Color BeatMeterColor = new(0.12f, 0.92f, 1f);
    private static readonly Color OffBeatMeterColor = new(1f, 0.42f, 0.92f);
    private static readonly Color EighthMeterColor = new(0.62f, 1f, 0.25f);
    private static readonly Color BeatChipColor = new(0.08f, 0.28f, 0.34f);
    private static readonly Color OnBeatChipColor = new(0.10f, 0.32f, 0.18f);
    private static readonly Color OffBeatChipColor = new(0.26f, 0.10f, 0.32f);
    private static readonly Color OffBeatGateChipColor = new(0.24f, 0.10f, 0.25f);
    private static readonly Color DisabledDotColor = new(0.32f, 0.34f, 0.38f);
    private static readonly Color PastBeatDotColor = new(0.16f, 0.42f, 0.45f);
    private static readonly Color FutureBeatDotColor = new(0.30f, 0.33f, 0.38f);
    private static readonly Color CurrentBeatSteadyColor = new(0.10f, 0.70f, 0.78f);
    private static readonly Color CurrentBeatFlashColor = new(0.28f, 1f, 0.98f);
    private static readonly Color OffBeatSteadyColor = new(0.45f, 0.08f, 0.42f);
    private static readonly Color OffBeatFlashColor = new(1f, 0.42f, 0.92f);
    private static readonly Color OffBeatInactiveColor = new(0.34f, 0.24f, 0.38f, 1f);
    private static readonly Color OffBeatDisabledColor = new(0.34f, 0.24f, 0.38f, 0.55f);
    private static readonly Color WaveformCurveIdleColor = new(0.34f, 0.42f, 0.47f);
    private static readonly Color EnvelopeMeterColor = new(0.12f, 0.92f, 1f);
    private static readonly Color FillNowChipColor = new(0.10f, 0.42f, 0.22f);
    private static readonly Color FillSoonChipColor = new(0.08f, 0.28f, 0.20f);
    private static readonly Color FillMeterColor = new(0.35f, 0.95f, 0.55f);
    private static readonly Color DropNowChipColor = new(0.44f, 0.12f, 0.30f);
    private static readonly Color DropSoonChipColor = new(0.24f, 0.10f, 0.22f);
    private static readonly Color DropMeterColor = new(1f, 0.42f, 0.62f);
    private static readonly Color PhraseEventIdleChipColor = new(0.20f, 0.21f, 0.24f);
    private static readonly Color EnergyLowChipColor = new(0.10f, 0.30f, 0.45f);
    private static readonly Color EnergyMidChipColor = new(0.46f, 0.34f, 0.06f);
    private static readonly Color EnergyHighChipColor = new(0.50f, 0.12f, 0.16f);
    private static readonly Color EnergyMeterColor = new(1f, 0.72f, 0.25f);
    private static readonly Color PhraseMeterColor = new(0.62f, 0.55f, 1f);
    private static readonly Color LowBandColor = new(0.95f, 0.40f, 0.30f);
    private static readonly Color MidBandColor = new(0.40f, 0.90f, 0.45f);
    private static readonly Color HighBandColor = new(0.40f, 0.60f, 1f);

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
    private static GUIStyle nullStyle;
    private static GUIStyle statusChipStyle;
    private static GUIStyle phraseTextStyle;
    private static GUIStyle bandLabelStyle;

    /// <summary>Draws the full dashboard and returns user actions for the drawer adapter to apply.</summary>
    /// <param name="rect">The complete dashboard bounds.</param>
    /// <param name="model">The immutable live rhythm display facts.</param>
    /// <param name="selector">The valid preview choices or required-Pool failure.</param>
    /// <param name="waveform">The selected runtime Waveform, or null when preview is unavailable.</param>
    /// <param name="layoutWidth">The available workspace width used to select one responsive flow.</param>
    public static BeatManagerDashboardActions Draw(Rect rect, BeatManagerDashboardModel model,
        WaveformSelectorView selector, Waveform? waveform, float layoutWidth)
    {
        EnsureStyles();

        var accent = model.Synced ? PanelLiveAccentColor : PanelOfflineAccentColor;
        DrawPanelBackground(rect, accent);

        var content = new Rect(
            rect.x + PanelPadding,
            rect.y + PanelPadding,
            Mathf.Max(0f, rect.width - (PanelPadding * 2f)),
            rect.height - (PanelPadding * 2f));

        var flow = BeatManagerDashboardModel.FlowForWidth(layoutWidth);
        var y = content.y;
        DrawHeader(new Rect(content.x, y, content.width, HeaderHeight), model);
        y += HeaderHeight + HeaderGap;

        DrawGroupHeader(new Rect(content.x, y, content.width, GroupHeaderHeight), "TIMING");
        y += GroupHeaderHeight + GroupHeaderGap;

        var body = new Rect(content.x, y, content.width, BodyHeight);
        var leftWidth = Mathf.Min(LeftZoneWidth, body.width * LeftZoneMaxFraction);
        var leftZone = new Rect(body.x, body.y, leftWidth, body.height);
        DrawPositionZone(leftZone, model);

        var dividerX = leftZone.xMax + (ZoneDividerGap * 0.5f);
        EditorGUI.DrawRect(new Rect(dividerX, body.y + 2f, 1f, body.height - 4f), DividerColor);

        var rightX = leftZone.xMax + ZoneDividerGap;
        DrawPulseZone(new Rect(rightX, body.y, Mathf.Max(0f, body.xMax - rightX), body.height), model);
        y = body.yMax + BodyGap;

        var chipAreaHeight = flow == RhythmDashboardFlow.Stacked ? (ChipHeight * 2f) + ChipGap : ChipHeight;
        var chipsRect = new Rect(content.x, y, content.width, chipAreaHeight);
        DrawCountdownChips(chipsRect, model, flow);
        y = chipsRect.yMax + QueryRowGap;

        DrawGridRow(new Rect(content.x, y, content.width, QueryRowHeight), model.Grid);
        y += QueryRowHeight + GroupGap;

        DrawGroupHeader(new Rect(content.x, y, content.width, GroupHeaderHeight), "WAVEFORM PREVIEW");
        y += GroupHeaderHeight + GroupHeaderGap;

        var selectorRect = new Rect(content.x, y, content.width, WaveformSelectorHeight);
        var actions = DrawWaveformSelector(selectorRect, selector);

        var stripRect = new Rect(content.x, selectorRect.yMax + WaveformSelectorGap, content.width, WaveformStripHeight);
        DrawWaveformStrip(stripRect, model, model.PoolHealthy ? waveform : null, model.PoolError);
        y = stripRect.yMax + QueryRowGap;
        DrawEnvelopeRow(new Rect(content.x, y, content.width, QueryRowHeight), model.Envelope);
        y += QueryRowHeight + GroupGap;

        if (flow == RhythmDashboardFlow.Split)
        {
            var columnWidth = Mathf.Max(0f, (content.width - GroupGap) * 0.5f);
            var currentBottom = DrawCurrentGroup(new Rect(content.x, y, columnWidth, 0f), model);
            var nextBottom = DrawNextGroup(
                new Rect(content.x + columnWidth + GroupGap, y, columnWidth, 0f),
                model);
            y = Mathf.Max(currentBottom, nextBottom) + GroupGap;
        }
        else
        {
            y = DrawCurrentGroup(new Rect(content.x, y, content.width, 0f), model) + GroupGap;
            y = DrawNextGroup(new Rect(content.x, y, content.width, 0f), model) + GroupGap;
        }

        DrawGroupHeader(new Rect(content.x, y, content.width, GroupHeaderHeight), "LEVELS");
        y += GroupHeaderHeight + GroupHeaderGap;
        DrawLevelsRow(new Rect(content.x, y, content.width, QueryRowHeight), model.Levels);
        return actions;
    }

    /// <summary>Returns the dashboard height required by the responsive flow at a given width.</summary>
    public static float DashboardHeightForWidth(float width)
    {
        return BeatManagerDashboardModel.FlowForWidth(width) == RhythmDashboardFlow.Split ? 606f : DashboardHeight;
    }

    /// <summary>Draws the current Phrase, Fill, Drop, and Energy group and returns its lower edge.</summary>
    private static float DrawCurrentGroup(Rect rect, BeatManagerDashboardModel model)
    {
        var y = rect.y;
        DrawGroupHeader(new Rect(rect.x, y, rect.width, GroupHeaderHeight), "CURRENT");
        y += GroupHeaderHeight + GroupHeaderGap;
        DrawPhraseRow(new Rect(rect.x, y, rect.width, QueryRowHeight), PhraseLabel, model.CurrentPhrase, showMeter: true);
        y += QueryRowHeight + QueryRowGap;
        DrawPhraseEventRow(new Rect(rect.x, y, rect.width, QueryRowHeight), FillLabel, model.Fill,
            FillNowChipColor, FillSoonChipColor, FillMeterColor);
        y += QueryRowHeight + QueryRowGap;
        DrawPhraseEventRow(new Rect(rect.x, y, rect.width, QueryRowHeight), DropLabel, model.Drop,
            DropNowChipColor, DropSoonChipColor, DropMeterColor);
        y += QueryRowHeight + QueryRowGap;
        DrawEnergyRow(new Rect(rect.x, y, rect.width, QueryRowHeight), EnergyLabel, model.CurrentEnergy, showMeter: true);
        return y + QueryRowHeight;
    }

    /// <summary>Draws announced next Phrase and Energy facts and returns its lower edge.</summary>
    private static float DrawNextGroup(Rect rect, BeatManagerDashboardModel model)
    {
        var y = rect.y;
        DrawGroupHeader(new Rect(rect.x, y, rect.width, GroupHeaderHeight), "NEXT");
        y += GroupHeaderHeight + GroupHeaderGap;
        DrawPhraseRow(new Rect(rect.x, y, rect.width, QueryRowHeight), NextPhraseLabel, model.NextPhrase, showMeter: false);
        y += QueryRowHeight + QueryRowGap;
        DrawEnergyRow(new Rect(rect.x, y, rect.width, QueryRowHeight), NextEnergyLabel, model.NextEnergy, showMeter: false);
        return y + QueryRowHeight;
    }

    /// <summary>Draws one stable semantic group heading.</summary>
    private static void DrawGroupHeader(Rect rect, string text)
    {
        GUI.Label(rect, text, queriesHeaderStyle);
    }

    /// <summary>Draws synchronized/Standalone status, track identity, players, and tempo.</summary>
    private static void DrawHeader(Rect rect, BeatManagerDashboardModel model)
    {
        var badgeColor = model.Synced ? LiveBadgeColor : OfflineBadgeColor;
        var badgeRect = new Rect(rect.x, rect.y, 112f, rect.height);
        EditorGUI.DrawRect(badgeRect, badgeColor);
        GUI.Label(badgeRect, model.BadgeText, badgeStyle);

        const float rightWidth = 132f;
        var titleWidth = Mathf.Max(0f, rect.width - badgeRect.width - rightWidth - 16f);
        var titleRect = new Rect(badgeRect.xMax + 8f, rect.y, titleWidth, rect.height);
        GUI.Label(titleRect, model.TrackText, titleStyle);

        var rightRect = new Rect(rect.xMax - rightWidth, rect.y, rightWidth, rect.height);
        GUI.Label(rightRect, model.HeaderRightText, valueStyle);
    }

    private static void DrawPositionZone(Rect zone, BeatManagerDashboardModel model)
    {
        var beatRow = new Rect(zone.x, zone.y + 2f, zone.width, DotRowHeight);
        DrawBeatDotsRow(beatRow, model);

        var offRow = new Rect(zone.x, beatRow.yMax + DotRowGap, zone.width, DotRowHeight);
        DrawOffBeatRow(offRow, model);

        DrawColumnLabels(new Rect(zone.x, offRow.yMax, zone.width, ColumnLabelHeight));
    }

    private static void DrawBeatDotsRow(Rect rect, BeatManagerDashboardModel model)
    {
        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), "BEAT", rowLabelStyle);

        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatManagerDashboardModel.BeatSlotCount; i++)
        {
            var beatLabel = i + 1;
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            DrawMarker(markerRect, model.GetBeatGlyph(beatLabel), GetBeatDotColor(model, beatLabel));
        }
    }

    /// <summary>Draws the four canonical OffBeats gates.</summary>
    private static void DrawOffBeatRow(Rect rect, BeatManagerDashboardModel model)
    {
        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), "OFFBEAT", rowLabelStyle);

        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatManagerDashboardModel.BeatSlotCount; i++)
        {
            var enabled = model.OffBeatGateAt(i);
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            if (!enabled.HasValue)
            {
                DrawMarker(markerRect, "—", OffBeatDisabledColor);
                continue;
            }

            var color = enabled.Value
                ? Color.Lerp(OffBeatSteadyColor, OffBeatFlashColor, model.OffBeatPulse ?? 0f)
                : OffBeatInactiveColor;
            DrawMarker(markerRect, enabled.Value ? OffBeatActive : OffBeatInactive, color);
        }
    }

    private static void DrawColumnLabels(Rect rect)
    {
        var spacing = ComputeMarkerSpacing(rect);
        var markerWidth = Mathf.Min(MarkerWidth, spacing);
        for (var i = 0; i < BeatManagerDashboardModel.BeatSlotCount; i++)
        {
            var markerRect = new Rect(rect.x + MarkerStartX + (i * spacing), rect.y, markerWidth, rect.height);
            GUI.Label(markerRect, (i + 1).ToString(), markerLabelStyle);
        }
    }

    private static float ComputeMarkerSpacing(Rect zone)
    {
        var available = Mathf.Max(0f, zone.width - MarkerStartX - MarkerWidth);
        return Mathf.Min(MarkerSpacing, available / (BeatManagerDashboardModel.BeatSlotCount - 1));
    }

    /// <summary>Draws the Beat, OffBeat, and combined eighth pulse meters.</summary>
    private static void DrawPulseZone(Rect rect, BeatManagerDashboardModel model)
    {
        var step = MeterRowHeight + MeterRowGap;
        DrawPulseMeterRow(new Rect(rect.x, rect.y, rect.width, MeterRowHeight), "BEAT", model.BeatPulse, BeatMeterColor);
        DrawPulseMeterRow(new Rect(rect.x, rect.y + step, rect.width, MeterRowHeight), "OFF", model.OffBeatPulse, OffBeatMeterColor);
        DrawPulseMeterRow(new Rect(rect.x, rect.y + (step * 2f), rect.width, MeterRowHeight), "8TH", model.EighthPulse, EighthMeterColor);
    }

    /// <summary>Draws one nullable pulse meter without manufacturing a zero value.</summary>
    private static void DrawPulseMeterRow(Rect rect, string label, float? pulse, Color color)
    {
        GUI.Label(new Rect(rect.x, rect.y, MeterLabelWidth, rect.height), label, rowLabelStyle);

        var barX = rect.x + MeterLabelWidth + 4f;
        var barRight = rect.xMax - MeterValueWidth - 6f;
        var barRect = new Rect(barX, rect.y + ((rect.height - BarHeight) * 0.5f), Mathf.Max(0f, barRight - barX), BarHeight);
        EditorGUI.DrawRect(barRect, MeterTrackColor);

        if (pulse is not { } value)
        {
            GUI.Label(new Rect(rect.xMax - MeterValueWidth, rect.y, MeterValueWidth, rect.height),
                NullValueText, nullStyle);
            return;
        }

        var fill = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(value), barRect.height);
        EditorGUI.DrawRect(fill, color);

        var shine = new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f));
        EditorGUI.DrawRect(shine, new Color(1f, 1f, 1f, 0.22f));

        GUI.Label(new Rect(rect.xMax - MeterValueWidth, rect.y, MeterValueWidth, rect.height), $"{value:0.00}", valueStyle);
    }

    /// <summary>Draws timing chips in one wide row or two readable narrow rows.</summary>
    private static void DrawCountdownChips(
        Rect rect,
        BeatManagerDashboardModel model,
        RhythmDashboardFlow flow)
    {
        if (flow == RhythmDashboardFlow.Split)
        {
            var chipWidth = Mathf.Max(0f, (rect.width - (ChipGap * 3f)) / 4f);
            var step = chipWidth + ChipGap;

            DrawCountdownChip(new Rect(rect.x, rect.y, chipWidth, ChipHeight), model.NextBeat, BeatChipColor);
            DrawCountdownChip(new Rect(rect.x + step, rect.y, chipWidth, ChipHeight), model.OnBeatGate, OnBeatChipColor);
            DrawCountdownChip(new Rect(rect.x + (step * 2f), rect.y, chipWidth, ChipHeight), model.NextOffBeat, OffBeatChipColor);
            DrawCountdownChip(new Rect(rect.x + (step * 3f), rect.y, chipWidth, ChipHeight), model.OffBeatGate, OffBeatGateChipColor);
            return;
        }

        var narrowWidth = Mathf.Max(0f, (rect.width - ChipGap) * 0.5f);
        var secondColumn = rect.x + narrowWidth + ChipGap;
        var secondRow = rect.y + ChipHeight + ChipGap;
        DrawCountdownChip(new Rect(rect.x, rect.y, narrowWidth, ChipHeight), model.NextBeat, BeatChipColor);
        DrawCountdownChip(new Rect(secondColumn, rect.y, narrowWidth, ChipHeight), model.OnBeatGate, OnBeatChipColor);
        DrawCountdownChip(new Rect(rect.x, secondRow, narrowWidth, ChipHeight), model.NextOffBeat, OffBeatChipColor);
        DrawCountdownChip(new Rect(secondColumn, secondRow, narrowWidth, ChipHeight), model.OffBeatGate, OffBeatGateChipColor);
    }

    /// <summary>Draws the editor-only Waveform preview selector and reports any chosen action.</summary>
    /// <param name="rect">The selector row bounds.</param>
    /// <param name="selector">The valid preview choices or required-Pool failure.</param>
    private static BeatManagerDashboardActions DrawWaveformSelector(Rect rect, WaveformSelectorView selector)
    {
        const float editButtonWidth = 90f;
        const float gap = 6f;

        GUI.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), "PREVIEW", rowLabelStyle);

        var popupX = rect.x + RowLabelWidth;
        var popupRight = rect.xMax - editButtonWidth - gap;
        var popupRect = new Rect(popupX, rect.y, Mathf.Max(0f, popupRight - popupX), rect.height);

        var selectedIndex = -1;
        if (selector.Options.Length > 0)
        {
            var chosen = EditorGUI.Popup(popupRect, selector.ShownIndex, selector.Options);
            selectedIndex = chosen != selector.ShownIndex ? chosen : -1;
        }
        else
        {
            EditorGUI.HelpBox(popupRect, selector.Error, MessageType.Error);
        }

        var buttonRect = new Rect(rect.xMax - editButtonWidth, rect.y, editButtonWidth, rect.height);
        var openPoolEditor = GUI.Button(buttonRect, "Edit Pool…");
        return selectedIndex >= 0 || openPoolEditor
            ? new BeatManagerDashboardActions(selectedIndex, openPoolEditor)
            : BeatManagerDashboardActions.None;
    }

    /// <summary>Draws the editor-selected Waveform and live playhead, or the exact required-Pool failure.</summary>
    /// <param name="rect">The preview strip bounds.</param>
    /// <param name="model">The immutable live rhythm display facts.</param>
    /// <param name="waveform">The selected runtime Waveform, or null when unavailable.</param>
    /// <param name="configurationError">The exact failure drawn when <paramref name="waveform"/> is null.</param>
    private static void DrawWaveformStrip(
        Rect rect,
        BeatManagerDashboardModel model,
        Waveform? waveform,
        string configurationError)
    {
        if (waveform is not { } availableWaveform)
        {
            EditorGUI.HelpBox(rect, configurationError, MessageType.Error);
            return;
        }

        var plotRight = rect.xMax - WaveformValueWidth - 6f;
        var plot = new Rect(rect.x, rect.y + 2f, Mathf.Max(0f, plotRight - rect.x), rect.height - 4f);

        WaveformPlot.Draw(plot, availableWaveform, model.Synced ? WaveformPlot.Curve : WaveformCurveIdleColor,
            model.BarPhase);

        var readout = new Rect(rect.xMax - WaveformValueWidth, rect.y, WaveformValueWidth, rect.height);
        if (model.BarPhase is { } barPhase)
        {
            var emitted = Mathf.Clamp01(availableWaveform.Sample(barPhase));
            GUI.Label(readout, $"{emitted:0.00}", valueStyle);
        }
        else
        {
            GUI.Label(readout, "—", valueStyle);
        }
    }

    private static void DrawEnvelopeRow(Rect row, EnvelopeRowView envelope)
    {
        var content = DrawQueryRowLabel(row, EnvelopeLabel);
        if (!envelope.HasValue)
        {
            DrawNullValue(content);
            return;
        }

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, envelope.Meter, EnvelopeMeterColor);
        GUI.Label(right, envelope.Readout, valueStyle);
    }

    private static void DrawPhraseEventRow(Rect row, GUIContent label, PhraseEventRowView rowView, Color nowColor,
        Color soonColor, Color meterColor)
    {
        var content = DrawQueryRowLabel(row, label);
        if (!rowView.HasValue)
        {
            DrawNullValue(content);
            return;
        }

        var view = rowView.View;
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

    /// <summary>Draws one current or next Energy row with optional progress.</summary>
    private static void DrawEnergyRow(Rect row, GUIContent label, EnergyRowView energy, bool showMeter)
    {
        var content = DrawQueryRowLabel(row, label);
        if (!energy.HasValue)
        {
            DrawNullValue(content);
            return;
        }

        var chip = TakeLeft(ref content, StatusChipWidth);
        DrawStatusChip(chip, energy.Chip, GetEnergyChipColor(energy.Level));

        var right = TakeRight(ref content, RightTextWidth);
        if (showMeter)
        {
            DrawMeter(content, energy.Meter, EnergyMeterColor);
        }
        GUI.Label(right, energy.Readout, valueStyle);
    }

    /// <summary>Draws one current or next Phrase row with optional progress.</summary>
    private static void DrawPhraseRow(Rect row, GUIContent label, PhraseRowView phrase, bool showMeter)
    {
        var content = DrawQueryRowLabel(row, label);
        if (!phrase.HasValue)
        {
            DrawNullValue(content);
            return;
        }

        var labelRect = TakeLeft(ref content, PhraseLabelWidth);
        GUI.Label(labelRect, phrase.Label, phraseTextStyle);

        var right = TakeRight(ref content, RightTextWidth);
        if (showMeter)
        {
            DrawMeter(content, phrase.Meter, PhraseMeterColor);
        }
        GUI.Label(right, phrase.Readout, valueStyle);
    }

    /// <summary>Draws the sender-provided one-based Grid placement.</summary>
    private static void DrawGridRow(Rect row, GridRowView grid)
    {
        var content = DrawQueryRowLabel(row, GridLabel);
        if (!grid.HasValue)
        {
            DrawNullValue(content);
            return;
        }

        var chip = TakeLeft(ref content, StatusChipWidth + 18f);
        DrawStatusChip(chip, grid.State, BeatChipColor);
        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, grid.Meter, PhraseMeterColor);
        GUI.Label(right, grid.Readout, valueStyle);
    }

    private static void DrawLevelsRow(Rect row, LevelsRowView levels)
    {
        var content = DrawQueryRowLabel(row, LevelsLabel);
        if (!levels.HasValue)
        {
            DrawNullValue(content);
            return;
        }

        var segmentWidth = Mathf.Max(0f, (content.width - (SegmentGap * 2f)) / 3f);
        DrawBandSegment(new Rect(content.x, content.y, segmentWidth, content.height), "L", levels.Low, LowBandColor);
        DrawBandSegment(new Rect(content.x + segmentWidth + SegmentGap, content.y, segmentWidth, content.height), "M", levels.Mid, MidBandColor);
        DrawBandSegment(new Rect(content.x + ((segmentWidth + SegmentGap) * 2f), content.y, segmentWidth, content.height), "H", levels.High, HighBandColor);
    }

    private static void DrawBandSegment(Rect rect, string label, float value, Color color)
    {
        GUI.Label(new Rect(rect.x, rect.y, 14f, rect.height), label, bandLabelStyle);
        var valueRect = new Rect(rect.xMax - 36f, rect.y, 36f, rect.height);
        var meter = new Rect(rect.x + 16f, rect.y, Mathf.Max(0f, rect.width - 16f - 38f), rect.height);
        DrawMeter(meter, value, color);
        GUI.Label(valueRect, $"{value:0.00}", valueStyle);
    }

    private static Rect DrawQueryRowLabel(Rect row, GUIContent label)
    {
        GUI.Label(new Rect(row.x, row.y, QueryRowLabelWidth, row.height), label, rowLabelStyle);
        return new Rect(row.x + QueryRowLabelWidth + 4f, row.y, Mathf.Max(0f, row.width - QueryRowLabelWidth - 4f), row.height);
    }

    private static void DrawNullValue(Rect content)
    {
        GUI.Label(content, NullValueText, nullStyle);
    }

    private static void DrawMeter(Rect rect, float value, Color color)
    {
        var bar = new Rect(rect.x, rect.y + ((rect.height - BarHeight) * 0.5f), rect.width, BarHeight);
        EditorGUI.DrawRect(bar, MeterTrackColor);

        var fill = new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value), bar.height);
        EditorGUI.DrawRect(fill, color);
        EditorGUI.DrawRect(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f)), new Color(1f, 1f, 1f, 0.22f));
    }

    private static void DrawStatusChip(Rect rect, string text, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));
        GUI.Label(rect, text, statusChipStyle);
    }

    private static void DrawCountdownChip(Rect rect, CountdownChipView chip, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));

        var textWidth = Mathf.Max(0f, rect.width - 16f);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, textWidth, 10f), chip.Label, chipLabelStyle);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 11f, textWidth, rect.height - 12f), chip.Value,
            chip.AlignValueRight ? chipValueRightStyle : chipValueStyle);
    }

    /// <summary>Maps the canonical Energy ladder onto dashboard chip colors.</summary>
    private static Color GetEnergyChipColor(Energy level) => level switch
    {
        Energy.Low => EnergyLowChipColor,
        Energy.Mid => EnergyMidChipColor,
        _ => EnergyHighChipColor,
    };

    private static Rect TakeLeft(ref Rect rect, float width)
    {
        var left = new Rect(rect.x, rect.y, width, rect.height);
        rect = new Rect(rect.x + width + SegmentGap, rect.y, Mathf.Max(0f, rect.width - width - SegmentGap), rect.height);
        return left;
    }

    private static Rect TakeRight(ref Rect rect, float width)
    {
        var right = new Rect(rect.xMax - width, rect.y, width, rect.height);
        rect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - width - SegmentGap), rect.height);
        return right;
    }

    private static void DrawPanelBackground(Rect rect, Color accent)
    {
        EditorGUI.DrawRect(rect, PanelBackgroundColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), accent);

        var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
        EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width, 1f), new Color(1f, 1f, 1f, 0.08f));
        EditorGUI.DrawRect(new Rect(inner.x, inner.yMax - 1f, inner.width, 1f), new Color(0f, 0f, 0f, 0.5f));
    }

    private static void DrawMarker(Rect rect, string glyph, Color color)
    {
        var glow = new Rect(rect.x + 4f, rect.y + 2f, Mathf.Max(0f, rect.width - 8f), Mathf.Min(20f, rect.height));
        EditorGUI.DrawRect(glow, new Color(color.r, color.g, color.b, 0.10f));

        var previous = GUI.color;
        GUI.color = color;
        GUI.Label(rect, glyph, dotStyle);
        GUI.color = previous;
    }

    /// <summary>Chooses the dashboard color for one musical beat marker.</summary>
    private static Color GetBeatDotColor(BeatManagerDashboardModel model, int beatLabel) =>
        model.GetBeatMarkerState(beatLabel) switch
        {
            BeatMarkerState.Past => PastBeatDotColor,
            BeatMarkerState.Current => Color.Lerp(
                CurrentBeatSteadyColor,
                CurrentBeatFlashColor,
                model.OnBeat == true
                    ? Mathf.Max(0.45f, model.BeatPulse ?? 0f)
                    : (model.BeatPulse ?? 0f) * 0.35f),
            BeatMarkerState.Future => FutureBeatDotColor,
            _ => DisabledDotColor,
        };

    /// <summary>Creates the shared IMGUI styles once for the active editor domain.</summary>
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
        nullStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.45f, 0.50f, 0.55f) },
            alignment = TextAnchor.MiddleLeft,
        };
        statusChipStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
        };
        phraseTextStyle = new GUIStyle(EditorStyles.boldLabel)
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
