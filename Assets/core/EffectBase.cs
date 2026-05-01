﻿using UnityEngine;
using Random = UnityEngine.Random;

[System.Serializable]
public abstract class EffectBase {

  [HideInInspector]
  public Color[] buffer;
 // public int initialIndex;
  public Controller controller;
  private Factory<EffectBase> factory;

  public float effectTime;
  public float effectDelta;
    [HideInInspector]
 // public int sortIndex;

  protected Penrose penrose;
  protected Penrose.TileData[] tiles;
  public static AnimPalette APalette=new AnimPalette();

  public string Name => GetType().ToString();
   
  // Used for UI display and gets called every frame
  public abstract string DebugText();

  // Should be called after creation
  public virtual void Init() {
    factory = new Factory<EffectBase>();
    controller = Controller.Instance;
    penrose = controller.penrose;
    tiles = penrose.Tiles;
    buffer     = new Color[Penrose.Total];
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

  // Should be called every time an effect is turned on
  public abstract void OnStart();

  // Should be called every time an effect is turned off
  public abstract void OnEnd();

  // Should be called every frame
  public abstract void Draw();
    public virtual EffectBase GetRandomEffect()
    {
        EffectBase effect;
        while (true)
        {
            effect = factory.Create(factory.Types[Random.Range(0, factory.Count)]);
            if (effect.Name == Name)
                continue;
            break;
        }
        return effect;
    }

}