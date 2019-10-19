using System;
using UnityEngine;

public abstract class TwoDeeEffect : EffectBase {

  public enum Direction {

    Up,
    UpLeft,
    UpRight,
    Down,
    DownLeft,
    DownRight,
    Left,
    Right,
    Clockwise,
    CounterClockwise

  }

  private static readonly int count = 4;
  protected static readonly int width = 44;
  protected static readonly int height = 22;

  protected static Neighbor[,] neighbors;

  private static bool initialized;

  public Color[,] twoDeeBuffer;

  private static void InitWeights() {
    // get the tiles
    var tiles = Controller.Instance.penrose.tiles;

    // find extents of the tiles
    var maxX = -100000f;
    var maxY = -100000f;
    var minX = 100000f;
    var minY = 1000000f;

    for(var i = 0; i < Penrose.Total; i++) {
      var x = tiles[i].center.x;
      var y = tiles[i].center.y;

      minX = Mathf.Min(minX, x);
      minY = Mathf.Min(minY, y);
      maxX = Mathf.Max(maxX, x);
      maxY = Mathf.Max(maxY, y);
    }
    
    // loop through all the tiles
    for(var i = 0; i < Penrose.Total; i++) {

      // create closeNeighbors array
      // to sort we need one extra array element
      var total = count + 1;
      var closeNeighbors = new Neighbor[total];
      for(var j = 0; j < total; j++) closeNeighbors[j] = new Neighbor();
      
      // reset index, px and sx
      var index = 0;
      var px = minX;
      var sx = (maxX - minX) / (width - 1f);

      for(var x = 0; x < width; x++) {
        // reset py and sy
        var py = minY;
        var sy = (maxY - minY) / (height - 1f);

        for(var y = 0; y < height; y++) {
          // find closest neighbors
          // by first sorting the array
          Array.Sort(closeNeighbors, (a, b) => a.distance.CompareTo(b.distance));

          // set the new sample to the current index
          closeNeighbors[index].index = i;
          closeNeighbors[index].position.x = x;
          closeNeighbors[index].position.y = y;

          // find and set the distance of the sample
          var dx = px - tiles[i].center.x;
          var dy = py - tiles[i].center.y;
          var d = Mathf.Sqrt(dx * dx + dy * dy);
          closeNeighbors[index].distance = d;

          // increase index if we need too
          if(index < total - 1) index++;

          // move y forward
          py += sy;
        }

        // move x forward
        px += sx;
      }

      // closest have been found
      // get the total of all distances and 
      // add the closeNeighbors to the neighbors array
      // ignoring the last closeNeighbor as that was used for sorting
      var totalDistance = 0f;
      for(var j = 0; j < count; j++) {
        totalDistance += closeNeighbors[j].distance;
        neighbors[i, j] = closeNeighbors[j];
      }

      // set the weight for the color value based on distance
      for(var j = 0; j < count; j++) neighbors[i, j].weight = closeNeighbors[j].distance / totalDistance;
    }
  }

  // resample the rectangle into the tile buffer
  protected static void Convert2dBuffer(ref Color[,] twoDeeBuffer, in Color[] buffer) {
    for(var i = 0; i < buffer.Length; i++) {
      var pix = Color.black;
      for(var j = 0; j < count; j++) 
        pix += twoDeeBuffer[neighbors[i, j].position.x, neighbors[i, j].position.y] * neighbors[i, j].weight;

      buffer[i] = pix;
    }
  }

  public override void Init() {
    base.Init();

    twoDeeBuffer = new Color[width, height];

    if(initialized) return;

    neighbors = new Neighbor[buffer.Length, count];
    for(var x = 0; x < buffer.Length; x++) {
      for(var y = 0; y < count; y++) neighbors[x, y] = new Neighbor();
    }

    InitWeights();

    //for(var x = 0; x < buffer.Length; x++) {
    //  for(var y = 0; y < count; y++) Debug.Log(neighbors[x, y]);
    //}

    initialized = true;
  }

  protected class Neighbor {

    public int index = -1;
    public Vector2Int position;
    public float distance = 1000000f;
    public float weight;

    public override string ToString() { return $"{index}, {position}, {distance}, {weight}"; }

  }

}