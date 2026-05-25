using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows BeatData's computed array-backed properties in the Inspector without making them editable.
/// </summary>
[CustomPropertyDrawer(typeof(BeatData))]
public sealed class BeatDataDrawer : PropertyDrawer
{
    private static readonly string[] ChildFields =
    {
        "active",
        "playersLive",
        "track",
        "bpm",
        "beat",
        "bar",
        "beatInBar",
        "beatsCountMs",
        "onBeats",
        "offBeatsCountMs",
        "offBeats",
        "offBeatPulse",
        "beatAverageMs",
        "beatPulse",
        "levels",
        "phaseState",
        "dropState",
        "fillState",
        "energyState",
        "beatsPerMeasure",
        "currentBeat",
    };

    private const int ComputedLineCount = 4;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        DrawComputedBeatValues(line, property);
        line.y += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * ComputedLineCount;

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

        EditorGUI.indentLevel--;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing;
        height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * ComputedLineCount;

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

    private static void DrawComputedBeatValues(Rect line, SerializedProperty property)
    {
        var nextBeatIndex = IndexOfSmallestNonNegative(property, "beatsCountMs");
        var nextOffBeatIndex = IndexOfSmallestNonNegative(property, "offBeatsCountMs");

        DrawReadOnlyLabel(line, "Next Beat Ms", ReadIntArray(property, "beatsCountMs", nextBeatIndex).ToString());
        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        DrawReadOnlyLabel(line, "On Beat", ReadBoolArray(property, "onBeats", nextBeatIndex).ToString());
        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        DrawReadOnlyLabel(line, "Next Off Beat Ms", ReadIntArray(property, "offBeatsCountMs", nextOffBeatIndex).ToString());
        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        DrawReadOnlyLabel(line, "Off Beat", ReadBoolArray(property, "offBeats", nextOffBeatIndex).ToString());
    }

    private static void DrawReadOnlyLabel(Rect position, string label, string value)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUI.TextField(position, label, value);
        }
    }

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

    private static int ReadIntArray(SerializedProperty property, string fieldName, int index)
    {
        var array = property.FindPropertyRelative(fieldName);
        if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
        {
            return -1;
        }

        return array.GetArrayElementAtIndex(index).intValue;
    }

    private static bool ReadBoolArray(SerializedProperty property, string fieldName, int index)
    {
        var array = property.FindPropertyRelative(fieldName);
        if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
        {
            return false;
        }

        return array.GetArrayElementAtIndex(index).boolValue;
    }
}
