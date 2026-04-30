using UnityEngine;

public abstract class MixerBase : EffectBase {

    public override void Init() {
        base.Init();
    }

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