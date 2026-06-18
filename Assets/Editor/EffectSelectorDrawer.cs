using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws an effect-index field as a catalog dropdown. Row 0 is "Random" (the
/// <c>-1</c> sentinel = deck rotation); the remaining rows are the reflection-built
/// effect catalog in the same order as the runtime <c>effects</c> array, so a popup
/// row maps straight to the held effect index. Picking an effect holds it; picking
/// Random releases the wall back to deck rotation.
/// </summary>
/// <remarks>
/// The catalog comes from the same <see cref="Factory{T}"/> the runtime uses, so the
/// blank <c>EmptyEffect</c> template (carrying <c>[RuntimeCatalogIgnore]</c>) is absent
/// here exactly as it is absent from <c>effects</c>, and a dropdown index equals the
/// runtime effect index. Changing the value in Play Mode is picked up by the
/// Controller's per-frame held-effect override on the next Update; the change is logged
/// because a silent change to what the wall is playing is the kind of invisible state
/// the project forbids.
/// </remarks>
[CustomPropertyDrawer(typeof(EffectSelectorAttribute))]
public sealed class EffectSelectorDrawer : PropertyDrawer
{
    private const string RandomLabel = "Random (deck rotation)";

    // Built once per domain load: the catalog only changes on recompile, which resets
    // this static. Index k here is runtime effects[k]; row 0 is the Random sentinel.
    private static string[] cachedOptions;

    /// <summary>Renders the held-effect dropdown, mapping row 0 ↔ -1 and row k+1 ↔ effect index k.</summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var options = GetOptions();

        EditorGUI.BeginProperty(position, label, property);

        // heldEffect -1 => row 0 (Random); held index k => row k + 1. A stale index past
        // the current catalog (e.g. an effect was removed) falls back to Random.
        var current = property.intValue;
        var shown = (current >= 0 && current < options.Length - 1) ? current + 1 : 0;

        EditorGUI.BeginChangeCheck();
        var chosen = EditorGUI.Popup(position, label.text, shown, options);
        if (EditorGUI.EndChangeCheck())
        {
            var next = chosen <= 0 ? -1 : chosen - 1;
            if (next != current)
            {
                property.intValue = next;
                if (Application.isPlaying)
                {
                    Debug.Log(next < 0
                        ? "[Controller] Effect lock released — back to Random (deck rotation)."
                        : $"[Controller] Effect held: {options[chosen]} (index {next}).");
                }
            }
        }

        EditorGUI.EndProperty();
    }

    /// <summary>Builds (once) the dropdown rows: the Random sentinel followed by the effect catalog names.</summary>
    private static string[] GetOptions()
    {
        if (cachedOptions != null)
        {
            return cachedOptions;
        }

        var names = new Factory<EffectBase>().Names;
        cachedOptions = new string[names.Length + 1];
        cachedOptions[0] = RandomLabel;
        for (var i = 0; i < names.Length; i++)
        {
            cachedOptions[i + 1] = names[i];
        }

        return cachedOptions;
    }
}
