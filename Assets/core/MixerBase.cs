using UnityEngine;

/// <summary>
/// Base class for effects that own child effects and combine or transform their buffers.
/// </summary>
/// <remarks>
/// Child effects are created and driven manually by the mixer, not by the top-level Controller catalog.
/// </remarks>
public abstract class MixerBase : EffectBase {

    /// <summary>
    /// Runs the normal EffectBase setup. Present as an explicit lifecycle marker for mixer subclasses.
    /// </summary>
    public override void Init() {
        base.Init();
    }

    /// <summary>
    /// Creates a random non-mixer child effect to avoid recursive mixer trees.
    /// </summary>
    public override EffectBase GetRandomEffect()
    {
        EffectBase effect;
        while (true)
        {
            effect = base.GetRandomEffect();
            if (!(effect is MixerBase))
                break;
        }
        return effect;
    }
}