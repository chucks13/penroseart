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
    set => v = Mathf.Clamp01(value);
  }

  public float D => 1f - v;

  // Used for UI display and gets called every frame
  public virtual string DebugText() => $"{controller.effects[a].Name} ({D:0.00}) => {controller.effects[b].Name} ({v:0.00})";

  // Should be called after creation
  public virtual void Init() {
    controller = Controller.Instance;
    buffer = new Color[Penrose.Total];
  }

  // Should be called every time an effect is turned on
  public abstract void OnStart();

  // Should be called every time an effect is turned off
  public abstract void OnEnd();

  // Should be called every frame
  public abstract void Draw();

}