// Pulldown drawer for string settings marked as Waveform Pool entry names.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders a <see cref="WaveformNameAttribute"/> string field as a pulldown over the current
/// Waveform Pool entry names, so a setting selects an existing Preset instead of retyping one.
/// A saved name missing from the Pool stays shown and marked — the defect is the maintainer's to
/// see, and the runtime fails visibly on it — until the pulldown repoints to a real entry.
/// </summary>
[CustomPropertyDrawer(typeof(WaveformNameAttribute))]
public sealed class WaveformNameDrawer : PropertyDrawer
{
    /// <summary>Pool entry names in document order, cached against the Pool file write time.</summary>
    private static string[] cachedNames;

    /// <summary>Pool file write time the cached names were read at.</summary>
    private static DateTime cachedWriteTimeUtc;

    /// <inheritdoc/>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string[] names = PoolNames();
        string current = property.stringValue;
        int currentIndex = Array.IndexOf(names, current);

        EditorGUI.BeginProperty(position, label, property);
        if (currentIndex < 0)
        {
            var withMissing = new string[names.Length + 1];
            withMissing[0] = $"{current} (missing from Pool)";
            Array.Copy(names, 0, withMissing, 1, names.Length);
            int picked = EditorGUI.Popup(position, label.text, 0, withMissing);
            if (picked > 0)
            {
                property.stringValue = names[picked - 1];
            }
        }
        else
        {
            int picked = EditorGUI.Popup(position, label.text, currentIndex, names);
            if (picked != currentIndex)
            {
                property.stringValue = names[picked];
            }
        }

        EditorGUI.EndProperty();
    }

    /// <summary>
    /// Reads the Pool entry names, re-parsing only when the Pool file's write time moves, so a
    /// repainting tuning window costs one file stat per repaint instead of a parse.
    /// </summary>
    private static string[] PoolNames()
    {
        DateTime writeTimeUtc = File.GetLastWriteTimeUtc(WaveformPool.FilePath);
        if (cachedNames == null || writeTimeUtc != cachedWriteTimeUtc)
        {
            var entries = WaveformPool.Parse(WaveformPool.ReadFileOrEmpty());
            cachedNames = new string[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                cachedNames[i] = entries[i].name;
            }

            cachedWriteTimeUtc = writeTimeUtc;
        }

        return cachedNames;
    }
}
