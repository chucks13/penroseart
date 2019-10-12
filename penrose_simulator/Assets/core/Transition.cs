[System.Serializable]
public abstract class Transition : EffectBase {

  private EffectBase[] effects;
  private float v;

  public EffectBase A {
    get => effects[0];
    set {
      if(value != null) effects[0] = value;
    }
  }

  public EffectBase B {
    get => effects[1];
    set {
      if(value != null) effects[1] = value;
    }
  }

  public float V {
    get => v;
    set {
      if(value > 0f && value <= 1f) v = value;
    }
  }

  public override void Init() {
    base.Init();
    effects = new EffectBase[2];
  }

  public void SetEffects(EffectBase a, EffectBase b, float mv = 0f) {
    effects[0] = a;
    effects[1] = b;
    v          = mv;
  }

}