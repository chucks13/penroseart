using UnityEngine;

/// <summary>
/// Marks an effect-index field so the inspector draws it as a catalog dropdown:
/// row 0 is "Random" (the <c>-1</c> sentinel = deck rotation) and the remaining
/// rows are the reflection-built effect catalog, in the same order as the runtime
/// <c>effects</c> array. A non-negative value holds that effect; <c>-1</c> lets
/// the deck rotate normally. Pure marker — see <c>EffectSelectorDrawer</c>.
/// </summary>
public sealed class EffectSelectorAttribute : PropertyAttribute
{
}
