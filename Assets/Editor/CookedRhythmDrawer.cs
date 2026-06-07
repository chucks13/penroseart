using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows BeatManager's cooked rhythm queries (ADR-0002) as a live read-only dashboard in the Inspector,
/// above the raw serialized BeatManager fields.
/// </summary>
/// <remarks>
/// Each row renders one nullable query exactly as effects, transitions, and blenders consume it: a
/// meter/value when the query is non-null (Synced Mode), and a dimmed null marker when it is null (the
/// caller's Default Mode). Hover any row label for what that query represents. Unlike
/// <see cref="BeatDataDrawer"/>, which must reconstruct values from the SerializedProperty and mirror the
/// BarPhase formula by hand, this drawer resolves the live BeatManager instance through
/// <see cref="PropertyDrawer.fieldInfo"/> and calls the real query properties — so the panel cannot drift
/// from what the runtime actually computes. Smooth Play Mode animation comes from
/// <see cref="ControllerEditor.RequiresConstantRepaint"/>; in Edit Mode the queries are honestly null
/// (no clock, no smoothed Levels), which is itself a correct picture of the contract.
/// </remarks>
[CustomPropertyDrawer(typeof(BeatManager))]
public sealed class CookedRhythmDrawer : PropertyDrawer
{
    // Vertical budget. PanelHeight is derived so the layout, the panel rect, and GetPropertyHeight stay in sync.
    private const float PanelPadding = 12f;
    private const float HeaderHeight = 18f;
    private const float HeaderGap = 10f;
    private const float RowHeight = 22f;
    private const float RowGap = 6f;
    private const float SwatchRowHeight = 26f;
    private const int StandardRowCount = 6; // envelope, fill, drop, energy, phase, levels
    private const float PanelHeight =
        PanelPadding + HeaderHeight + HeaderGap
        + (StandardRowCount * (RowHeight + RowGap))
        + SwatchRowHeight + PanelPadding;

    // Horizontal anatomy of a query row: label column, optional status chip, meter, right-aligned readout.
    private const float RowLabelWidth = 70f;
    private const float RightTextWidth = 150f;
    private const float ChipWidth = 46f;
    private const float PhaseLabelWidth = 90f;
    private const float BarHeight = 8f;
    private const float SegmentGap = 6f;

    private const string NullValueText = "—  null → Default Mode";

    private const string EnvelopeTooltip =
        "Envelope(variant): the Waveform Pool envelope evaluated at the current Bar Phase — the primitive under " +
        "BeatBrightness/BeatTime. Shows the wall's effective variant (the lock, or the on-screen effect's). " +
        "Null: no beat clock is running.";
    private const string FillTooltip =
        "Fill: a short build-up flourish. SOON = counting down to the next one (anticipate it); NOW = riding it " +
        "(progress sweeps 0→1). Null: no Fill data on the wire right now.";
    private const string DropTooltip =
        "Drop: the payoff section, same two-phase shape as Fill — land transitions on its start, ride its " +
        "progress while it plays. Null: no Drop data on the wire right now.";
    private const string EnergyTooltip =
        "Energy: the track's intensity tier in the closed Low/Mid/High vocabulary, with where it is heading. " +
        "Meter shows the normalized tier (0 / 0.5 / 1). Null: unavailable, or the wire label was unrecognized.";
    private const string PhaseTooltip =
        "Phase: the track's current section label (open vocabulary — display it, don't keyword-parse it), with " +
        "cooked progress and the upcoming section. Null: no phase data on the wire right now.";
    private const string LevelsTooltip =
        "Levels: low/mid/high band energy with BeatManager's attack/release smoothing already applied " +
        "(fast up, slow down — anti-flicker). Null: no live Levels; the local simulator never supplies them.";
    private const string ColorTooltip =
        "Color Bank, the three cooked Levels colors — RGB: bands mapped straight onto red/green/blue channels; " +
        "HUE: spectral-centroid hue, dominance saturation, strongest-band value; PAL: the live AnimPalette read " +
        "at the bands' centroid, scaled by the strongest band.";

    private static readonly Color PanelBackgroundColor = new Color(0.035f, 0.04f, 0.055f);
    private static readonly Color PanelAccentColor = new Color(0.45f, 0.90f, 0.45f);
    private static readonly Color MeterTrackColor = new Color(0.07f, 0.08f, 0.10f);
    private static readonly Color EnvelopeMeterColor = new Color(0.12f, 0.92f, 1f);
    private static readonly Color FillNowChipColor = new Color(0.10f, 0.42f, 0.22f);
    private static readonly Color FillSoonChipColor = new Color(0.08f, 0.28f, 0.20f);
    private static readonly Color FillMeterColor = new Color(0.35f, 0.95f, 0.55f);
    private static readonly Color DropNowChipColor = new Color(0.44f, 0.12f, 0.30f);
    private static readonly Color DropSoonChipColor = new Color(0.24f, 0.10f, 0.22f);
    private static readonly Color DropMeterColor = new Color(1f, 0.42f, 0.62f);
    private static readonly Color EnergyLowChipColor = new Color(0.10f, 0.30f, 0.45f);
    private static readonly Color EnergyMidChipColor = new Color(0.46f, 0.34f, 0.06f);
    private static readonly Color EnergyHighChipColor = new Color(0.50f, 0.12f, 0.16f);
    private static readonly Color EnergyMeterColor = new Color(1f, 0.72f, 0.25f);
    private static readonly Color PhaseMeterColor = new Color(0.62f, 0.55f, 1f);
    private static readonly Color LowBandColor = new Color(0.95f, 0.40f, 0.30f);
    private static readonly Color MidBandColor = new Color(0.40f, 0.90f, 0.45f);
    private static readonly Color HighBandColor = new Color(0.40f, 0.60f, 1f);
    private static readonly Color SwatchBorderColor = new Color(1f, 1f, 1f, 0.18f);

    private static GUIStyle headerStyle;
    private static GUIStyle hintStyle;
    private static GUIStyle rowLabelStyle;
    private static GUIStyle valueStyle;
    private static GUIStyle nullStyle;
    private static GUIStyle nullCenterStyle;
    private static GUIStyle chipStyle;
    private static GUIStyle phaseTextStyle;
    private static GUIStyle bandLabelStyle;

    /// <summary>Draws the foldout, the cooked-query dashboard, and the regular serialized child fields.</summary>
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
                DrawCookedPanel(panelRect, ResolveBeatManager(property), property.serializedObject.targetObject);
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

    /// <summary>Returns the exact IMGUI height for the foldout, the cooked panel, and the child fields.</summary>
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
    /// Resolves the live BeatManager instance behind this property so the panel can call the real cooked
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
        if (beatManager.activeVariant >= 0)
        {
            return beatManager.activeVariant;
        }

        if (Application.isPlaying && owner is Controller controller)
        {
            var current = controller.CurrentBeatVariant;
            if (current >= 0)
            {
                return current;
            }
        }

        return 0;
    }

    /// <summary>Draws the serialized BeatManager fields (beatData, simulator and smoothing tunables) normally.</summary>
    /// <remarks>Children are enumerated rather than listed by name so future fields appear without touching
    /// this drawer; beatData still renders through <see cref="BeatDataDrawer"/> via the nested PropertyField.</remarks>
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

    /// <summary>Draws the dashboard: header, the six query rows, and the Color Bank swatch row.</summary>
    private static void DrawCookedPanel(Rect rect, BeatManager beatManager, Object owner)
    {
        DrawPanelBackground(rect);

        var content = new Rect(
            rect.x + PanelPadding,
            rect.y + PanelPadding,
            Mathf.Max(0f, rect.width - (PanelPadding * 2f)),
            rect.height - (PanelPadding * 2f));

        GUI.Label(new Rect(content.x, content.y, content.width * 0.5f, HeaderHeight), "COOKED RHYTHM QUERIES", headerStyle);
        GUI.Label(new Rect(content.x + (content.width * 0.5f), content.y, content.width * 0.5f, HeaderHeight),
            "— = null → Default Mode", hintStyle);

        var y = content.y + HeaderHeight + HeaderGap;
        DrawEnvelopeRow(new Rect(content.x, y, content.width, RowHeight), beatManager, owner);
        y += RowHeight + RowGap;

        DrawFillRow(new Rect(content.x, y, content.width, RowHeight), beatManager);
        y += RowHeight + RowGap;

        DrawDropRow(new Rect(content.x, y, content.width, RowHeight), beatManager);
        y += RowHeight + RowGap;

        DrawEnergyRow(new Rect(content.x, y, content.width, RowHeight), beatManager);
        y += RowHeight + RowGap;

        DrawPhaseRow(new Rect(content.x, y, content.width, RowHeight), beatManager);
        y += RowHeight + RowGap;

        DrawLevelsRow(new Rect(content.x, y, content.width, RowHeight), beatManager);
        y += RowHeight + RowGap;

        DrawColorRow(new Rect(content.x, y, content.width, SwatchRowHeight), beatManager);
    }

    /// <summary>Draws the Envelope row: the variant's Waveform value at the current Bar Phase.</summary>
    private static void DrawEnvelopeRow(Rect row, BeatManager beatManager, Object owner)
    {
        var content = DrawRowLabel(row, "ENVELOPE", EnvelopeTooltip);
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

    /// <summary>Draws the Fill row from the cooked FillInfo.</summary>
    private static void DrawFillRow(Rect row, BeatManager beatManager)
    {
        var content = DrawRowLabel(row, "FILL", FillTooltip);
        if (beatManager?.Fill is { } fill)
        {
            DrawPhraseEventContent(content, fill.inProgress, fill.beatsUntilStart, fill.progress,
                fill.lengthBeats, fill.remaining, FillNowChipColor, FillSoonChipColor, FillMeterColor);
        }
        else
        {
            DrawNullValue(content);
        }
    }

    /// <summary>Draws the Drop row from the cooked DropInfo.</summary>
    private static void DrawDropRow(Rect row, BeatManager beatManager)
    {
        var content = DrawRowLabel(row, "DROP", DropTooltip);
        if (beatManager?.Drop is { } drop)
        {
            DrawPhraseEventContent(content, drop.inProgress, drop.beatsUntilStart, drop.progress,
                drop.lengthBeats, drop.remaining, DropNowChipColor, DropSoonChipColor, DropMeterColor);
        }
        else
        {
            DrawNullValue(content);
        }
    }

    /// <summary>
    /// Draws the shared two-phase Fill/Drop anatomy: a NOW/SOON chip, the progress meter (0 while
    /// upcoming), and a readout with countdown-or-progress plus length and remaining occurrences.
    /// </summary>
    private static void DrawPhraseEventContent(Rect content, bool inProgress, int? beatsUntilStart,
        float progress, int? lengthBeats, int? remaining, Color nowColor, Color soonColor, Color meterColor)
    {
        var chip = TakeLeft(ref content, ChipWidth);
        DrawChip(chip, inProgress ? "NOW" : "SOON", inProgress ? nowColor : soonColor);

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, progress, meterColor);

        var head = inProgress ? $"{progress:0.00}" : $"in {FormatBeats(beatsUntilStart)}";
        GUI.Label(right, $"{head} · len {FormatCount(lengthBeats)} · ×{FormatCount(remaining)}", valueStyle);
    }

    /// <summary>Draws the Energy row: tier chip, normalized meter, and where the energy is heading.</summary>
    private static void DrawEnergyRow(Rect row, BeatManager beatManager)
    {
        var content = DrawRowLabel(row, "ENERGY", EnergyTooltip);
        if (!(beatManager?.Energy is { } energy))
        {
            DrawNullValue(content);
            return;
        }

        var chip = TakeLeft(ref content, ChipWidth);
        DrawChip(chip, energy.level.ToString().ToUpperInvariant(), GetEnergyChipColor(energy.level));

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, energy.normalized, EnergyMeterColor);

        var arrow = energy.direction > 0 ? "↗" : energy.direction < 0 ? "↘" : "→";
        var heading = energy.next is { } next
            ? $"{arrow} {next.ToString().ToUpperInvariant()} in {FormatBeats(energy.beatsUntilChange)}"
            : "steady";
        GUI.Label(right, heading, valueStyle);
    }

    /// <summary>Draws the Phase row: the open-vocabulary section label, progress, and the upcoming section.</summary>
    private static void DrawPhaseRow(Rect row, BeatManager beatManager)
    {
        var content = DrawRowLabel(row, "PHASE", PhaseTooltip);
        if (!(beatManager?.Phase is { } phase))
        {
            DrawNullValue(content);
            return;
        }

        var labelRect = TakeLeft(ref content, PhaseLabelWidth);
        GUI.Label(labelRect, phase.label, phaseTextStyle);

        var right = TakeRight(ref content, RightTextWidth);
        DrawMeter(content, phase.progress, PhaseMeterColor);

        var heading = phase.next != null
            ? $"→ {phase.next} in {FormatBeats(phase.beatsUntilNext)}"
            : $"len {FormatCount(phase.lengthBeats)}";
        GUI.Label(right, heading, valueStyle);
    }

    /// <summary>Draws the Levels row: three mini meters for the smoothed low/mid/high bands.</summary>
    private static void DrawLevelsRow(Rect row, BeatManager beatManager)
    {
        var content = DrawRowLabel(row, "LEVELS", LevelsTooltip);
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
        var content = DrawRowLabel(row, "COLOR", ColorTooltip);
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

    /// <summary>Draws a row's query-name label (with its explanatory tooltip) and returns the content area.</summary>
    private static Rect DrawRowLabel(Rect row, string text, string tooltip)
    {
        GUI.Label(new Rect(row.x, row.y, RowLabelWidth, row.height), new GUIContent(text, tooltip), rowLabelStyle);
        return new Rect(row.x + RowLabelWidth + 4f, row.y, Mathf.Max(0f, row.width - RowLabelWidth - 4f), row.height);
    }

    /// <summary>Draws the dimmed marker for a query that is currently null — the consumer's Default Mode.</summary>
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

    /// <summary>Draws one small status chip (NOW/SOON, LOW/MID/HIGH).</summary>
    private static void DrawChip(Rect rect, string text, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));
        GUI.Label(rect, text, chipStyle);
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

    /// <summary>Formats a nullable beat count ("16b"), using — when the cooked value is null.</summary>
    private static string FormatBeats(int? value)
    {
        return value is { } beats ? $"{beats}b" : "—";
    }

    /// <summary>Formats a nullable count, using — when the cooked value is null.</summary>
    private static string FormatCount(int? value)
    {
        return value is { } count ? count.ToString() : "—";
    }

    /// <summary>Draws the panel background with the cooked-layer green accent bar and subtle bevels.</summary>
    private static void DrawPanelBackground(Rect rect)
    {
        EditorGUI.DrawRect(rect, PanelBackgroundColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), PanelAccentColor);

        var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
        EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width, 1f), new Color(1f, 1f, 1f, 0.08f));
        EditorGUI.DrawRect(new Rect(inner.x, inner.yMax - 1f, inner.width, 1f), new Color(0f, 0f, 0f, 0.5f));
    }

    /// <summary>Creates and caches GUI styles once per domain reload so repaints do not allocate styles repeatedly.</summary>
    private static void EnsureStyles()
    {
        if (headerStyle != null)
        {
            return;
        }

        headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.70f, 0.95f, 0.72f) },
            alignment = TextAnchor.MiddleLeft,
        };
        hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.45f, 0.50f, 0.55f) },
            alignment = TextAnchor.MiddleRight,
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
        nullStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.45f, 0.50f, 0.55f) },
            alignment = TextAnchor.MiddleLeft,
        };
        nullCenterStyle = new GUIStyle(nullStyle)
        {
            alignment = TextAnchor.MiddleCenter,
        };
        chipStyle = new GUIStyle(EditorStyles.miniBoldLabel)
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
