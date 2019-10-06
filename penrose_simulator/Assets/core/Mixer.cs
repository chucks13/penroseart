using UnityEngine;

[System.Serializable]
public abstract class Mixer : Effect {

  public int effectTotal = 3;

  protected Effect[] effects;

  public override void Init() {
    base.Init();
    effects = new Effect[effectTotal];
  }

}