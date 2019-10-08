using System;
using Random = UnityEngine.Random;

[System.Serializable]
public abstract class Mixer : Effect {

  protected int numberOfEffectTypes;

  protected Effect[] effects;

  public override void Init() {
    
    // effect init
    base.Init();

    // count the number of effect types
    numberOfEffectTypes = Enum.GetValues(typeof(Controller.EffectTypes)).Length;

  }

}