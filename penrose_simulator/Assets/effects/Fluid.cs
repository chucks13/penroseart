using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fluid : EffectBase {
  private Setting setting;
  private Data fluidData;

  public override string DebugText() { return$""; }

  public override void Init() {
    base.Init();
    fluidData = new Data(setting.size, 0, 0);
  }

  public override void OnStart() { buffer.Clear(Color.green); }

  public override void OnEnd() { }

  public override void Draw() { }

  public class Setting {
    public readonly Vector2Int size = new Vector2Int(44, 22);
  }

  public class Data {
    public Vector2Int size;
    public float deltaTime;
    public float diffusion;
    public float viscosity;
    public float[] s;
    public float[] density;
    public Vector2[] v;
    public Vector2[] v0;
    private int length;
    private int iterations;

    public Data(Vector2Int size, float diffusion, float viscosity) {
      this.size = size;
      this.diffusion = diffusion;
      this.viscosity = viscosity;

      length = size.x * size.y;

      s = new float[length];
      density = new float[length];
      v = new Vector2[length];
      v0 = new Vector2[length];
    }

    private int GetIndex(int x, int y) => x + (y * size.x);

    private void Diffuse(
      int b, Vector2[] v, Vector2[] v0, float diff, float deltaTime, int inter, int size
    ) {
      var a = deltaTime * diff * (size - 2) * (size - 2);
    }

    public void AddDensity(int x, int y, float amount) { density[GetIndex(x, y)] += amount; }

    public void AddVelocity(int x, int y, Vector2 amount) { v[GetIndex(x, y)] += amount; }

    private void LinSolve(int b, float a, float c) {
      float cRecip = 1.0f / c;
      for(int i = 0; i < iterations; i++) {
        for(int y = 1; y < size.y - 1; y++) {
          for(int x = 1; x < size.x - 1; x++) {
            v[GetIndex(x, y)] =
              (v0[GetIndex(x, y)] + a * (v[GetIndex(x + 1, y)] + v[GetIndex(x - 1, y)] +
                                         v[GetIndex(x, y + 1)] + v[GetIndex(x, y - 1)])) * cRecip;
          }
        }

        //set_bnd(b, x);
      }
    }
  }
}