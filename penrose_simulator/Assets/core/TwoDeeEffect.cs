using System;
using UnityEngine;

public abstract class TwoDeeEffect : EffectBase {

  public static readonly int count = 3;
  public static readonly int width = 44;
  public static readonly int height = 22;

  protected static Neighbor[,] neighbors;

  private static bool initialized;

  protected struct Neighbor {

    public int x;
    public int y;
    public float distance;
    public float weight;

  };

  
  public Color[,] twoDeeBuffer;

  private static void InitWeights() {
    // find extents of tiles;
    float maxX = -1000000f;
    float maxY = -1000000f;
    float minX = 1000000f;
    float minY = 1000000f;

    var tiles = Controller.Instance.penrose.tiles;

    for(int i = 0; i < Penrose.Total; i++) {
      var x = tiles[i].center.x;
      var y = tiles[i].center.y;

      minX = Mathf.Min(minX, x);
      minY = Mathf.Min(minY, y);
      maxX = Mathf.Max(maxX, x);
      maxY = Mathf.Max(maxY, y);
    }

    // find closest neighbors
    var closeNeighbors = new Neighbor[count + 1];
    for(int i = 0; i < count + 1; i++) {
      closeNeighbors[i] = new Neighbor();
    }

    for(int i = 0; i < Penrose.Total; i++) {

      // find the closest neighbors
      int used = 0;
      float px = minX;
      float sx = (maxX - minX) / (width - 1f);
      
      for(int x = 0; x < width; x++) {
        float py = minY;
        float sy = (maxY - minY) / (height - 1f);

        for(int y = 0; y < height; y++) {
          if(x == y) continue;
          float dx = px - tiles[i].center.x;
          float dy = py - tiles[i].center.y;
          float d = Mathf.Sqrt((dx * dx) + (dy * dy));

          // we keep (count+1) samples and sort the every time
          closeNeighbors[used].distance = d;
          closeNeighbors[used].x = x;
          closeNeighbors[used].y = y;
          used++;
          if(used == (count + 1)) {
            Array.Sort(closeNeighbors, (a, b) => a.distance.CompareTo(b.distance));
            used--; // discard the farthest
          }

          py += sy;
        }

        px += sx;
      }

      // closest have been found
      // get the total of all distances and copies of the neighbors
      float total = 0;
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
  protected static void ConvertBuffer(ref Color[,] twoDeeBuffer, in Color[] buffer) {
    for(int i = 0; i < buffer.Length; i++) {
      Color pix = Color.black;
      for(var j = 0; j < count; j++) {
        pix += twoDeeBuffer[neighbors[i, j].x, neighbors[i, j].y] * neighbors[i, j].weight;
      }

      buffer[i] = pix;
    }
  }

  public override void Init() {
    base.Init();

    twoDeeBuffer = new Color[width, height];

    if(initialized) return;

    neighbors = new Neighbor[buffer.Length, count];
    for(int x = 0; x < buffer.Length; x++) {
      for(int y = 0; y < count; y++) {
        neighbors[x, y] = new Neighbor();
      }
    }

    InitWeights();

    initialized = true;
  }

}