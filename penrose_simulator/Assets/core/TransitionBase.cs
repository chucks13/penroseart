using UnityEngine;

[System.Serializable]
public abstract class TransitionBase {

  [HideInInspector]
  public Color[] buffer;

  protected Controller controller;

  public string Name => GetType().ToString();

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

  public float D => 1f - V;


  public virtual void Init() {
    controller = Controller.Instance;
    buffer = new Color[Penrose.Total];
  }

  public abstract void Draw();

}