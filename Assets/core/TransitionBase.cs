using UnityEngine;
using System;
using Random = UnityEngine.Random;

[System.Serializable]
public abstract class TransitionBase
{

  [HideInInspector]
  public Color[] buffer;

  protected Controller controller;

  public string Name => GetType().ToString();

  private int a;
  private int b;
  private float v;
  public float effectTime;
  public float effectDelta;


  // Defaults to no fader arguments so Blend() implementations can safely use
  // settings.Length before telnet or other controls provide values.
  public float[] settings = Array.Empty<float>();
  public void setFaders(string[] stringArray)
  {
    settings = Array.ConvertAll(stringArray, float.Parse);
  }

  public virtual void Blend(Color[] dest, Color[] src1, Color[] src2)
  {

  }
  public virtual string Usage()
  {
    return "(not implemented yet)";
  }


  public int A
  {
    get => a;
    set
    {
      if (value >= 0 && value < controller.effects.Length) a = value;
    }
  }

  public int B
  {
    get => b;
    set
    {
      if (value >= 0 && value < controller.effects.Length) b = value;
    }
  }

  public float V
  {
    get => v;
    set => v = Mathf.Clamp01(value);
  }

  public float D => 1f - v;

  // Used for UI display and gets called every frame
  public virtual string DebugText() => $"{controller.effects[a].Name} ({D:0.00}) => {controller.effects[b].Name} ({v:0.00})";

  /// <summary>
  /// Called once after reflection creates the transition instance.
  /// Put reusable setup here so Blend() is safe before the transition has
  /// been selected for a real effect-to-effect transition.
  /// </summary>
  public virtual void Init()
  {
    controller = Controller.Instance;
    buffer = new Color[Penrose.Total];
  }

  public void RandomizeTime()
  {
      // Seed with 0 to 4 hours (14400 seconds)
      effectTime = Random.Range(0f, 14400f);
  }

  public void UpdateTime()
  {
      effectDelta = Time.deltaTime;
      effectTime += effectDelta;
  }

  /// <summary>
  /// Called each time this transition becomes the active effect-to-effect
  /// transition. Use it for per-run state such as random direction/color.
  /// </summary>
  public abstract void OnStart();

  /// <summary>
  /// Reserved for future transition deactivation cleanup. The current
  /// controller does not call OnEnd(); transitions should not rely on it yet.
  /// </summary>
  public abstract void OnEnd();

  // Should be called every frame
  public abstract void Draw();

}