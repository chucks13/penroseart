using System;
using Random = UnityEngine.Random;

[System.Serializable]
public abstract class Mixer : EffectBase {

  protected int numberOfEffectTypes;

  protected EffectBase[] effects;

  public override void Init() {
    
    // effect init
    base.Init();

    // count the number of effect types
    numberOfEffectTypes = Enum.GetValues(typeof(Controller.EffectTypes)).Length;

  }

}