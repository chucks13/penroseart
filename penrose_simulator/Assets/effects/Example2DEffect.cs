using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Example2DEffect : EffectBase {

  private const int count = 3;
  private const int width = 44;
  private const int height = 22;

  private struct Neighbor {

    public int x;
    public int y;
    public float distance;
    public float weight;

  };

  private Settings setting;
  private Neighbor[,] neighbors;
  private bool initialized = false;

  [HideInInspector]
  public Color[,] rectBuffer;

  public void InitWeights() {
    // find extents of tiles;
    float maxx = -100000;
    float maxy = -1000000;
    float minx = 1000000;
    float miny = 1000000;

    for(int i = 0; i < buffer.Length; i++) {
      float x           = controller.penrose.tiles[i].center.x;
      float y           = controller.penrose.tiles[i].center.y;
      if(x < minx) minx = x;
      if(y < miny) miny = y;
      if(x > maxx) maxx = x;
      if(y > maxy) maxy = y;
    }

    // find closest neighbors
    Neighbor[] closeNeighbors = new Neighbor[count + 1];
    for(int i = 0; i < count + 1; i++) {
      closeNeighbors[i] = new Neighbor();

    }


    for (int i = 0; i < buffer.Length; i++) {
      // find the closest neighbors
      int   used  = 0;
      float px    = minx;
      float sx = (maxx - minx) / (float)(width - 1);
      for(int x = 0; x < width; x++) {
        float py    = miny;
        float sy = (maxy - miny) / (float)(height - 1);
        for(int y = 0; y < height; y++) {
          if(x == y) continue;
          float dx = px - controller.penrose.tiles[i].center.x;
          float dy = py - controller.penrose.tiles[i].center.y;
          float d = Mathf.Sqrt((dx * dx) + (dy * dy));

          // we keep (count+1) samples and sort the every time
          closeNeighbors[used].distance = d;
          closeNeighbors[used].x = x;
          closeNeighbors[used].y = y;
          used++;
          if(used == (count + 1)) {
            Array.Sort(closeNeighbors, delegate(Neighbor a, Neighbor b) { return a.distance.CompareTo(b.distance); });
            used--; // discard the farthest
          }
          py += sy;
        }
        px += sx;
      }

      // closest have been found
      // get the total of all distances and copies of the neighbors
      float total                              = 0;
      for(int j = 0; j < count - 1; j++) {
        neighbors[i, j] = closeNeighbors[j];
        total += closeNeighbors[j].distance;
      }

      // weight the values
      for(int j = 0; j < count - 1; j++) {
        neighbors[i, j].weight = closeNeighbors[j].distance / total;
      }
    }
  }

// resample the rectangle into the tile buffer
  public void Resample() {
    for(int i = 0; i < buffer.Length; i++) {
      Color pix = Color.black;
      for(var j = 0; j < count; j++) {
        pix += rectBuffer[neighbors[i,j].x,neighbors[i,j].y] * neighbors[i,j].weight;
      }
      buffer[i] = pix;
    }
  }

  public override string DebugText() { return ""; }

  public override void Init() {
    base.Init();
    setting    = new Settings();
    rectBuffer = new Color[width, height];
    neighbors= new Neighbor[buffer.Length,count];
    for(int x = 0; x < buffer.Length; x++) {
      for(int y = 0; y < count; y++) {
          neighbors[x,y]=new Neighbor(); 
      }
    }

  }

  public override void OnEnd() {  }

  public override void Draw() {
    if(!initialized) {
      InitWeights();
      initialized = true;

    }
    for (int x = 0; x < width; x++) {
      for(int y = 0; y < height; y++) {
        rectBuffer[x,y] = Color.HSVToRGB((x*0.1f+y*0.1f+ controller.dance.fixedTime)%1f, 1f, 1f);
      }
    }
    Resample();
  }

  public override void OnStart() {
    if(controller.example2dEffectSettings.Length > 0) {
      setting = controller.example2dEffectSettings[Random.Range(0, controller.example2dEffectSettings.Length)];
    } else {
      setting.Randomize();
    }

    controller.debugText.text = $"";
    buffer.Clear();
  }

  [System.Serializable]
  public class Settings {
    public void Randomize() {
    }
  }

}

