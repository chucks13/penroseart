using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fluid : EffectBase {

  private Data fluidData;

  public override string DebugText() { return $""; }

  public override void Init() {
    base.Init();
    fluidData = new Data(44, 0, 0);
  }

  public override void OnStart() { buffer.Clear(Color.green); }
  public override void OnEnd() { }
  public override void Draw() { }

  public class Data {

    public int size;
    public float deltaTime;
    public float diffusion;
    public float viscosity;

    public float[] s;
    public float[] density;

    public Vector2[] v;
    public Vector2[] v0;

    private int width;
    private int height;
    private int length;

    public Data(int size, float diffusion, float viscosity) {
      this.size = size;
      this.diffusion = diffusion;
      this.viscosity = viscosity;

      width = size;
      height = size / 2;
      length = width * height;

      s = new float[length];
      density = new float[length];
      v = new Vector2[length];
      v0 = new Vector2[length];
    }

  }

}