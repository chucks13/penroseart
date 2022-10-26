using UnityEngine;

[System.Serializable]
public abstract class EffectBase {

  [HideInInspector]
  public Color[] buffer;
  public int initialIndex;
  public Controller controller;

    [HideInInspector]
  public int sortIndex;

  protected Penrose penrose;
  protected Penrose.TileData[] tiles;
  public static AnimPalette APalette=new AnimPalette();

  public string Name => GetType().ToString();
   
  // Used for UI display and gets called every frame
  public abstract string DebugText();

  // Should be called after creation
  public virtual void Init() {
    controller = Controller.Instance;
    penrose = controller.penrose;
    tiles = penrose.Tiles;
    buffer     = new Color[Penrose.Total];
  }

  // Should be called every time an effect is turned on
  public abstract void OnStart();

  // Should be called every time an effect is turned off
  public abstract void OnEnd();

  // Should be called every frame
  public abstract void Draw();

}