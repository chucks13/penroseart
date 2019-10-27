using System;
using UnityEngine;

public abstract class ScreenEffect : EffectBase {
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
  protected static int width = -1;
  protected static int height = -1;
  protected static ScreenMap[][] neighbors;
  protected Color[] screenBuffer;

  private static void InitWeights() {
    // get the tiles
    var penrose = Controller.Instance.penrose;
    var tiles = penrose.Tiles;
    var bounds = penrose.Bounds;
    var min = bounds.min;
    var max = bounds.max;

    var xSpacing = (max.x - min.x) / width;
    var ySpacing = (max.y - min.y) / height;

    // loop through all the tiles
    for(var i = 0; i < Penrose.Total; i++) {
      // create closeNeighbors array
      var closeNeighbors = new ScreenMap[count];
      for(var j = 0; j < count; j++) closeNeighbors[j] = new ScreenMap();

      // reset index and x position
      var index = 0;
      var xPos = min.x;
      for(var x = 0; x < width; x++) {
        // reset y position
        var yPos = min.y;
        for(var y = 0; y < height; y++) {
          var screenPos = new Vector2(xPos, yPos);
          var distance = Vector2.Distance(screenPos, tiles[i].center);

          if(distance < closeNeighbors[index].distance) {
            closeNeighbors[index].position.x = x;
            closeNeighbors[index].position.y = y;
            closeNeighbors[index].distance = distance;
            Array.Sort(closeNeighbors, (a, b) => a.distance.CompareTo(b.distance));
          }

          // increase index if we need too
          if(index < count - 1) index++;

          // move y forward
          yPos += ySpacing;
        }

        // move x forward
        xPos += xSpacing;
      }

      // closest have been found
      // get the total of all distances and 
      // add the closeNeighbors to the neighbors array
      // ignoring the last closeNeighbor as that was used for sorting
      var totalDistance = 0f;
      for(var j = 0; j < count; j++) {
        totalDistance += closeNeighbors[j].distance;
        neighbors[i][j] = closeNeighbors[j];
      }

      // set the weight for the color value based on distance
      for(var j = 0; j < count; j++)
        neighbors[i][j].weight = closeNeighbors[j].distance / totalDistance;
    }
  }

  // resample the rectangle into the tile buffer
  protected static void ConvertScreenBuffer(ref Color[] screenBuffer, in Color[] buffer) {
    for(var i = 0; i < buffer.Length; i++) {
      var pix = Color.black;
      for(var j = 0; j < count; j++) {
        var index = neighbors[i][j].position.x + (neighbors[i][j].position.y * width);
        pix += screenBuffer[index] * neighbors[i][j].weight;
      }

      buffer[i] = pix;
    }
  }

  public override void Init() {
    base.Init();

    // have we set the static width and height
    if(width < 0) {
      width = (int)penrose.Bounds.size.x.Round();
      height = (int)penrose.Bounds.size.y.Round();
    }

    // create the 2d buffer
    screenBuffer = new Color[width * height];

    // have we set the neighbors
    if(neighbors != null) return;

    neighbors = new ScreenMap[buffer.Length][];
    for(var x = 0; x < buffer.Length; x++) {
      neighbors[x] = new ScreenMap[count];
      for(var y = 0; y < count; y++) neighbors[x][y] = new ScreenMap();
    }

    InitWeights();

    //for(var x = 0; x < buffer.Length; x++) {
    //  for(var y = 0; y < count; y++) Debug.Log(neighbors[x][y]);
    //}
  }

  protected class ScreenMap {
    public Vector2Int position;
    public float distance = 1000000f;
    public float weight;

    public override string ToString() { return$"{position}, {distance}, {weight}"; }
  }
}