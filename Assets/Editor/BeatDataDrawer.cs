using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows BeatData's live Rave OSC values with a read-only musical status panel in the Inspector.
/// </summary>
/// <remarks>
/// The drawer keeps the raw serialized fields visible for debugging, but presents the values that matter
/// during live shows first: active/offline state, current musical beat, offbeat gates, pulse strength, and
/// nearest countdowns. All displayed values are read-only because Rave OSC is the source of truth at runtime.
/// </remarks>
[CustomPropertyDrawer(typeof(BeatData))]
public sealed class BeatDataDrawer : PropertyDrawer
{
    private const int BeatSlotCount = 4;

    private const string DotFilled = "●";
    private const string DotEmpty = "○";
    private const string OffBeatActive = "◆";
    private const string OffBeatInactive = "◇";

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

    private const float PanelPadding = 12f;
    private const float VisualPanelHeight = 178f;
    private const float HeaderHeight = 22f;
    private const float BeatRowHeight = 34f;
    private const float OffBeatRowHeight = 30f;
    private const float PulseRowHeight = 28f;
    private const float BarHeight = 8f;
    private const float ChipHeight = 24f;
    private const float MarkerSpacing = 42f;
    private const float MarkerWidth = 34f;
    private const float RowLabelWidth = 64f;
    private const float MarkerStartX = 76f;
    private const float PulseTextStartX = 256f;

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
        "beatAverageMs",
        BeatPulseField,
        "levels",
        "phaseState",
        "dropState",
        "fillState",
        "energyState",
        "beatsPerMeasure",
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
    private static readonly Color PanelOfflineAccentColor = new Color(0.45f, 0.12f, 0.16f);
    private static readonly Color LiveBadgeColor = new Color(0.02f, 0.38f, 0.30f);
    private static readonly Color OfflineBadgeColor = new Color(0.28f, 0.08f, 0.08f);
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

    private static GUIStyle titleStyle;
    private static GUIStyle smallLabelStyle;
    private static GUIStyle valueStyle;
    private static GUIStyle badgeStyle;
    private static GUIStyle dotStyle;
    private static GUIStyle markerLabelStyle;
    private static GUIStyle chipLabelStyle;

    /// <summary>Draws the foldout, compact live-status panel, and disabled raw fields for a BeatData value.</summary>
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

                DrawRawFields(line, property);
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

    /// <summary>Draws the compact live Rave OSC dashboard above BeatData's raw serialized fields.</summary>
    private static void DrawVisualPanel(Rect rect, BeatPanelState state)
    {
        DrawPanelBackground(rect, state.Active);

        var content = new Rect(rect.x + PanelPadding, rect.y + PanelPadding, Mathf.Max(0f, rect.width - (PanelPadding * 2f)), rect.height - (PanelPadding * 2f));
        var header = new Rect(content.x, content.y, content.width, HeaderHeight);
        DrawHeader(header, state);

        var rowY = header.yMax + 12f;
        DrawBeatDotsRow(new Rect(content.x, rowY, content.width, BeatRowHeight), state);

        rowY += 38f;
        DrawOffBeatRow(new Rect(content.x, rowY, content.width, OffBeatRowHeight), state);

        rowY += 34f;
        DrawPulseMeters(new Rect(content.x, rowY, content.width, PulseRowHeight), state);

        rowY += 36f;
        DrawCountdownChips(new Rect(content.x, rowY, content.width, ChipHeight), state);
    }

    /// <summary>Draws the LIVE/OFFLINE badge, current track text, players, and BPM.</summary>
    private static void DrawHeader(Rect rect, BeatPanelState state)
    {
        var badgeRect = new Rect(rect.x, rect.y, 74f, rect.height);
        EditorGUI.DrawRect(badgeRect, state.Active ? LiveBadgeColor : OfflineBadgeColor);
        GUI.Label(badgeRect, state.Active ? "LIVE" : "OFFLINE", badgeStyle);

        var titleWidth = Mathf.Max(0f, rect.width - badgeRect.width - 128f);
        var titleRect = new Rect(badgeRect.xMax + 8f, rect.y, titleWidth, rect.height);
        var title = string.IsNullOrWhiteSpace(state.Track) ? "No Rave OSC track" : state.Track;
        GUI.Label(titleRect, title, titleStyle);

        var bpmText = state.Active && state.Bpm > 0f ? $"{state.Bpm:0.##} BPM" : "-- BPM";
        var rightRect = new Rect(rect.xMax - 110f, rect.y, 110f, rect.height);
        GUI.Label(rightRect, string.IsNullOrWhiteSpace(state.PlayersLive) ? bpmText : $"{state.PlayersLive}  {bpmText}", valueStyle);
    }

    /// <summary>Draws the four filled/empty quarter-note beat markers and the current beat pulse value.</summary>
    private static void DrawBeatDotsRow(Rect rect, BeatPanelState state)
    {
        GUI.Label(new Rect(rect.x, rect.y + 7f, RowLabelWidth, rect.height), "BEAT", smallLabelStyle);

        var dotX = rect.x + MarkerStartX;
        for (var i = 0; i < BeatSlotCount; i++)
        {
            var beatLabel = i + 1;
            var markerRect = new Rect(dotX + (i * MarkerSpacing), rect.y, MarkerWidth, rect.height);
            var glyph = BuildBeatDotGlyph(state.Active, state.BeatInBar, beatLabel);
            var color = GetBeatDotColor(state.Active, beatLabel, state.BeatInBar, state.OnBeat, state.BeatPulse);
            DrawDot(markerRect, glyph, color, beatLabel.ToString());
        }

        GUI.Label(new Rect(rect.x + PulseTextStartX, rect.y + 7f, Mathf.Max(0f, rect.width - PulseTextStartX), rect.height), $"pulse {state.BeatPulse:0.00}", valueStyle);
    }

    /// <summary>Draws four midpoint/offbeat markers and the derived offbeat gate/pulse summary.</summary>
    private static void DrawOffBeatRow(Rect rect, BeatPanelState state)
    {
        GUI.Label(new Rect(rect.x, rect.y + 5f, RowLabelWidth, rect.height), "OFFBEAT", smallLabelStyle);

        var dotX = rect.x + MarkerStartX;
        for (var i = 0; i < BeatSlotCount; i++)
        {
            var enabled = state.IsOffBeatGateActive(i);
            var markerRect = new Rect(dotX + (i * MarkerSpacing), rect.y, MarkerWidth, rect.height);
            var color = enabled
                ? Color.Lerp(OffBeatSteadyColor, OffBeatFlashColor, state.OffBeatPulse)
                : state.Active ? OffBeatInactiveColor : OffBeatDisabledColor;
            DrawDot(markerRect, enabled ? OffBeatActive : OffBeatInactive, color, (i + 1).ToString());
        }

        GUI.Label(new Rect(rect.x + PulseTextStartX, rect.y + 5f, Mathf.Max(0f, rect.width - PulseTextStartX), rect.height), $"{(state.OffBeat ? "gate" : "next")}  pulse {state.OffBeatPulse:0.00}", valueStyle);
    }

    /// <summary>Draws beat, offbeat, and combined eighth-note pulse meters across the available width.</summary>
    private static void DrawPulseMeters(Rect rect, BeatPanelState state)
    {
        var barWidth = Mathf.Max(0f, (rect.width - 12f) / 3f);
        DrawPulseMeter(new Rect(rect.x, rect.y, barWidth, rect.height), "BEAT", state.BeatPulse, BeatMeterColor, state.Active);
        DrawPulseMeter(new Rect(rect.x + barWidth + 6f, rect.y, barWidth, rect.height), "OFF", state.OffBeatPulse, OffBeatMeterColor, state.Active);
        DrawPulseMeter(new Rect(rect.x + (barWidth + 6f) * 2f, rect.y, barWidth, rect.height), "8TH", state.EighthPulse, EighthMeterColor, state.Active);
    }

    /// <summary>Draws one horizontal pulse meter with a small highlight strip over the filled portion.</summary>
    private static void DrawPulseMeter(Rect rect, string label, float pulse, Color color, bool active)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 14f), $"{label} {pulse:0.00}", smallLabelStyle);

        var barRect = new Rect(rect.x, rect.y + 17f, rect.width, BarHeight);
        EditorGUI.DrawRect(barRect, new Color(0.07f, 0.08f, 0.10f));

        var fill = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(pulse), barRect.height);
        var fillColor = active ? color : new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.8f);
        EditorGUI.DrawRect(fill, fillColor);

        var shine = new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f));
        EditorGUI.DrawRect(shine, new Color(1f, 1f, 1f, active ? 0.22f : 0.08f));
    }

    /// <summary>Draws the four bottom chips for beat/offbeat countdowns and gate state.</summary>
    private static void DrawCountdownChips(Rect rect, BeatPanelState state)
    {
        var chipWidth = Mathf.Max(0f, (rect.width - 24f) / 4f);
        var labels = BuildCountdownChipLabels();

        DrawChip(new Rect(rect.x, rect.y, chipWidth, rect.height), labels[0], FormatMs(state.NextBeatMs), BeatChipColor);
        DrawChip(new Rect(rect.x + chipWidth + 8f, rect.y, chipWidth, rect.height), labels[1], state.OnBeat ? "YES" : "NO", OnBeatChipColor);
        DrawChip(new Rect(rect.x + (chipWidth + 8f) * 2f, rect.y, chipWidth, rect.height), labels[2], FormatMs(state.NextOffBeatMs), OffBeatChipColor);
        DrawChip(new Rect(rect.x + (chipWidth + 8f) * 3f, rect.y, chipWidth, rect.height), labels[3], state.OffBeat ? "YES" : "NO", OffBeatGateChipColor);
    }

    /// <summary>Draws the panel background, live/offline accent bar, and subtle top/bottom bevels.</summary>
    private static void DrawPanelBackground(Rect rect, bool active)
    {
        EditorGUI.DrawRect(rect, PanelBackgroundColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), active ? PanelLiveAccentColor : PanelOfflineAccentColor);

        var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
        EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width, 1f), new Color(1f, 1f, 1f, 0.08f));
        EditorGUI.DrawRect(new Rect(inner.x, inner.yMax - 1f, inner.width, 1f), new Color(0f, 0f, 0f, 0.5f));
    }

    /// <summary>Draws one beat/offbeat marker glyph with a faint colored glow and a numeric musical label.</summary>
    private static void DrawDot(Rect rect, string glyph, Color color, string label)
    {
        var glow = new Rect(rect.x + 5f, rect.y + 1f, rect.width - 10f, 22f);
        EditorGUI.DrawRect(glow, new Color(color.r, color.g, color.b, 0.10f));

        var dotRect = new Rect(rect.x, rect.y - 2f, rect.width, 25f);
        var previous = GUI.color;
        GUI.color = color;
        GUI.Label(dotRect, glyph, dotStyle);
        GUI.color = previous;

        GUI.Label(new Rect(rect.x, rect.y + 23f, rect.width, 11f), label, markerLabelStyle);
    }

    /// <summary>Draws one fixed-height status chip with a left label and right-aligned value.</summary>
    private static void DrawChip(Rect rect, string label, string value, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));

        var labelRect = new Rect(rect.x + 8f, rect.y + 2f, Mathf.Max(0f, rect.width - 66f), rect.height - 4f);
        var valueRect = new Rect(rect.x + Mathf.Max(0f, rect.width - 58f), rect.y + 2f, Mathf.Min(52f, rect.width), rect.height - 4f);
        GUI.Label(labelRect, label, chipLabelStyle);
        GUI.Label(valueRect, value, valueStyle);
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
            normal = { textColor = new Color(0.80f, 0.90f, 0.92f) },
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
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

        /// <summary>Builds the panel snapshot from Unity's serialized BeatData object.</summary>
        public static BeatPanelState FromSerializedProperty(SerializedProperty property)
        {
            var active = ReadBool(property, ActiveField);
            var beatInBar = ReadInt(property, BeatInBarField);
            var beatPulse = active ? Mathf.Clamp01(ReadFloat(property, BeatPulseField)) : 0f;
            var offBeatPulse = active ? Mathf.Clamp01(ReadFloat(property, OffBeatPulseField)) : 0f;
            var beatIndex = active ? IndexOfSmallestNonNegative(property, BeatsCountMsField) : -1;
            var offBeatIndex = active ? IndexOfSmallestNonNegative(property, OffBeatsCountMsField) : -1;
            var currentBeatIndex = beatInBar - 1;

            return new BeatPanelState
            {
                Active = active,
                BeatInBar = beatInBar,
                Bpm = ReadFloat(property, BpmField),
                Track = ReadString(property, TrackField),
                PlayersLive = ReadString(property, PlayersLiveField),
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
            };
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
