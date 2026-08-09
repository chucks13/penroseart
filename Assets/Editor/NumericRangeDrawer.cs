// Draws serialized FloatRange and IntRange values with editable slider calibration rails.

using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws both numeric range types as editable rails, exact endpoints, and one two-thumb slider.
/// </summary>
[CustomPropertyDrawer(typeof(FloatRange))]
[CustomPropertyDrawer(typeof(IntRange))]
public sealed class NumericRangeDrawer : PropertyDrawer
{
    /// <summary>
    /// Draws one range row for recursive IMGUI property fields, including the Tuning Window's saved
    /// Standalone and Sync Settings editors.
    /// </summary>
    /// <param name="position">The rectangle Unity allocated for this serialized range.</param>
    /// <param name="property">The serialized FloatRange or IntRange value.</param>
    /// <param name="label">The owning settings field's label.</param>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var previousIndentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        var contentPosition = EditorGUI.PrefixLabel(
            position,
            GUIUtility.GetControlID(FocusType.Passive),
            label);

        const float spacing = 2f;
        const float railFieldWidth = 52f;
        const float endpointFieldWidth = 46f;

        var lowRailPosition = new Rect(
            contentPosition.x,
            contentPosition.y,
            railFieldWidth,
            contentPosition.height);
        var minimumPosition = new Rect(
            lowRailPosition.xMax + spacing,
            contentPosition.y,
            endpointFieldWidth,
            contentPosition.height);
        var highRailPosition = new Rect(
            contentPosition.xMax - railFieldWidth,
            contentPosition.y,
            railFieldWidth,
            contentPosition.height);
        var maximumPosition = new Rect(
            highRailPosition.x - spacing - endpointFieldWidth,
            contentPosition.y,
            endpointFieldWidth,
            contentPosition.height);
        var sliderPosition = new Rect(
            minimumPosition.xMax + spacing,
            contentPosition.y,
            maximumPosition.x - spacing - minimumPosition.xMax - spacing,
            contentPosition.height);

        var lowRailProperty = property.FindPropertyRelative(nameof(FloatRange.LowRail));
        var highRailProperty = property.FindPropertyRelative(nameof(FloatRange.HighRail));
        var minimumInclusiveProperty = property.FindPropertyRelative(nameof(IntRange.MinInclusive));

        if (minimumInclusiveProperty != null)
        {
            var maximumExclusiveProperty = property.FindPropertyRelative(nameof(IntRange.MaxExclusive));
            var lowRail = EditorGUI.IntField(lowRailPosition, lowRailProperty.intValue);
            var minimumInclusive = EditorGUI.IntField(
                minimumPosition,
                minimumInclusiveProperty.intValue);
            var maximumExclusive = EditorGUI.IntField(
                maximumPosition,
                maximumExclusiveProperty.intValue);
            var highRail = EditorGUI.IntField(highRailPosition, highRailProperty.intValue);

            var sliderMinimum = (float)minimumInclusive;
            var sliderMaximum = (float)maximumExclusive;
            EditorGUI.BeginChangeCheck();
            DrawGrippableMinMaxSlider(
                sliderPosition,
                ref sliderMinimum,
                ref sliderMaximum,
                lowRail,
                highRail);
            if (EditorGUI.EndChangeCheck())
            {
                minimumInclusive = Mathf.RoundToInt(sliderMinimum);
                maximumExclusive = Mathf.RoundToInt(sliderMaximum);
                if (minimumInclusive == maximumExclusive)
                {
                    if (maximumExclusive < highRail)
                    {
                        maximumExclusive++;
                    }
                    else
                    {
                        minimumInclusive--;
                    }
                }
            }

            lowRailProperty.intValue = lowRail;
            minimumInclusiveProperty.intValue = minimumInclusive;
            maximumExclusiveProperty.intValue = maximumExclusive;
            highRailProperty.intValue = highRail;
        }
        else
        {
            var minimumProperty = property.FindPropertyRelative(nameof(FloatRange.Min));
            var maximumProperty = property.FindPropertyRelative(nameof(FloatRange.Max));
            var lowRail = EditorGUI.FloatField(lowRailPosition, lowRailProperty.floatValue);
            var minimum = EditorGUI.FloatField(minimumPosition, minimumProperty.floatValue);
            var maximum = EditorGUI.FloatField(maximumPosition, maximumProperty.floatValue);
            var highRail = EditorGUI.FloatField(highRailPosition, highRailProperty.floatValue);

            var sliderMinimum = minimum;
            var sliderMaximum = maximum;
            EditorGUI.BeginChangeCheck();
            DrawGrippableMinMaxSlider(
                sliderPosition,
                ref sliderMinimum,
                ref sliderMaximum,
                lowRail,
                highRail);
            if (EditorGUI.EndChangeCheck())
            {
                minimum = sliderMinimum;
                maximum = sliderMaximum;
            }

            lowRailProperty.floatValue = lowRail;
            minimumProperty.floatValue = minimum;
            maximumProperty.floatValue = maximum;
            highRailProperty.floatValue = highRail;
        }

        EditorGUI.indentLevel = previousIndentLevel;
        EditorGUI.EndProperty();
    }

    /// <summary>Width in pixels of each thumb's grip hit zone and painted marker.</summary>
    private const int ThumbGripWidth = 5;

    /// <summary>
    /// Draws Unity's two-thumb slider with widened, painted thumb grips. The stock editor thumb
    /// style ("MinMaxHorizontalSliderThumb") makes each grip only its padding wide — a few pixels,
    /// too thin to see or grab comfortably in the Tuning Window.
    /// </summary>
    /// <param name="position">The slider rectangle between the exact endpoint fields.</param>
    /// <param name="minimum">The chosen lower endpoint moved by the left thumb.</param>
    /// <param name="maximum">The chosen upper endpoint moved by the right thumb.</param>
    /// <param name="lowRail">The lowest value the slider spans.</param>
    /// <param name="highRail">The highest value the slider spans.</param>
    private static void DrawGrippableMinMaxSlider(
        Rect position,
        ref float minimum,
        ref float maximum,
        float lowRail,
        float highRail)
    {
        var thumbStyle = GUI.skin.FindStyle("MinMaxHorizontalSliderThumb");
        if (thumbStyle == null)
        {
            EditorGUI.MinMaxSlider(position, ref minimum, ref maximum, lowRail, highRail);
            return;
        }

        var originalLeftPadding = thumbStyle.padding.left;
        var originalRightPadding = thumbStyle.padding.right;
        thumbStyle.padding.left = ThumbGripWidth;
        thumbStyle.padding.right = ThumbGripWidth;
        try
        {
            EditorGUI.MinMaxSlider(position, ref minimum, ref maximum, lowRail, highRail);
            PaintThumbGrips(position, minimum, maximum, lowRail, highRail, thumbStyle);
        }
        finally
        {
            thumbStyle.padding.left = originalLeftPadding;
            thumbStyle.padding.right = originalRightPadding;
        }
    }

    /// <summary>
    /// Paints visible grip bars over the slider's two thumb hit zones. The rect math mirrors
    /// Unity's internal EditorGUIExt.DoMinMaxSlider so the paint lands exactly on the grabbable
    /// area: grips are the thumb style's left/right padding at the ends of the selected span.
    /// </summary>
    /// <param name="position">The slider rectangle the slider was drawn into.</param>
    /// <param name="minimum">The chosen lower endpoint.</param>
    /// <param name="maximum">The chosen upper endpoint.</param>
    /// <param name="lowRail">The lowest value the slider spans.</param>
    /// <param name="highRail">The highest value the slider spans.</param>
    /// <param name="thumbStyle">The live thumb style, still carrying the widened padding.</param>
    private static void PaintThumbGrips(
        Rect position,
        float minimum,
        float maximum,
        float lowRail,
        float highRail,
        GUIStyle thumbStyle)
    {
        if (Event.current.type != EventType.Repaint || highRail <= lowRail)
        {
            return;
        }

        var sliderStyle = GUI.skin.horizontalSlider;
        var contentRect = thumbStyle.margin.Remove(sliderStyle.padding.Remove(position));
        var thumbSize = thumbStyle.fixedWidth != 0f
            ? thumbStyle.fixedWidth
            : thumbStyle.padding.horizontal;
        var pixelsPerValue =
            (position.width - sliderStyle.padding.horizontal - thumbSize) / (highRail - lowRail);
        var clampedMinimum = Mathf.Clamp(minimum, lowRail, highRail);
        var clampedMaximum = Mathf.Clamp(maximum, clampedMinimum, highRail);
        var thumbRect = new Rect(
            (clampedMinimum - lowRail) * pixelsPerValue + contentRect.x,
            contentRect.y,
            (clampedMaximum - clampedMinimum) * pixelsPerValue + thumbSize,
            contentRect.height);
        var gripColor = EditorGUIUtility.isProSkin
            ? new Color(0.75f, 0.75f, 0.75f)
            : new Color(0.3f, 0.3f, 0.3f);
        EditorGUI.DrawRect(
            new Rect(thumbRect.x, thumbRect.y, thumbStyle.padding.left, thumbRect.height),
            gripColor);
        EditorGUI.DrawRect(
            new Rect(
                thumbRect.xMax - thumbStyle.padding.right,
                thumbRect.y,
                thumbStyle.padding.right,
                thumbRect.height),
            gripColor);
    }
}
