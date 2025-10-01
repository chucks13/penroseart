[System.Serializable]
public abstract class Transition : EffectBase {

  private int a;
  private int b;
  private float v;

  public int A {
    get => a;
    set {
      if(value >= 0 && value < controller.effects.Length) a = value;
    }
  }

  public int B {
    get => b;
    set {
      if(value >= 0 && value < controller.effects.Length) b = value;
    }
  }

  public float V {
    get => v;
    set {
      if(value >= 0f && value <= 1f) v = value;
    }
  }

  public float Delta => 1f - V;
  
  
}