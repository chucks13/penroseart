using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Penrose : MonoBehaviour {
  public const int Total = 900;
  public const float FullScale = 1.0f / 140.0f;
  private const float TestScale = 1f / 100f;
  public Color bgColor = Color.gray;

  [Header("Display Size")]
  public float scale = 0.003f;

  public float gapScale = 0.9f;

  [HideInInspector]
  public Color[] buffer = new Color[Total]; // input buffer

  [HideInInspector]
  public TileData[] tiles;

  [HideInInspector]
  public Vector2 max;

  [HideInInspector]
  public Vector2 min;

  private readonly Vector3[] vertices = new Vector3[Total * 2 * 3];
  private readonly int[] triangles = new int[Total * 2 * 3];
  private readonly Color[] colors = new Color[Total * 2 * 3];
  private Mesh mesh;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  private Material material;
  private float bgBrightness;
  private Dictionary<Vector2, int> positionLookup;
  private Vector2[] positions;

  private void Awake() {
    meshFilter = GetComponent<MeshFilter>();
    meshRenderer = GetComponent<MeshRenderer>();
    material =
      new Material(Shader.Find("Unlit/Penrose")) {
                                                   hideFlags = HideFlags.HideAndDontSave,
                                                   name = "PenMaterial"
                                                 };
  }

  private void GenerateMesh() {
    var i = 0;
    var j = 0;

    // grab the geometry
    for(int n = 0; n < RawData.Mesh.Length; n += 6) {
      var a = new Vector3(RawData.Mesh[j++] * scale, RawData.Mesh[j++] * scale, 0f);
      var b = new Vector3(RawData.Mesh[j++] * scale, RawData.Mesh[j++] * scale, 0f);
      var c = new Vector3(RawData.Mesh[j++] * scale, RawData.Mesh[j++] * scale, 0f);

      var ab = b - a;
      var ac = c - a;

      if(Vector3.Cross(ab, ac).z > 0) {
        Vector3 x = c;
        c = a;
        a = x;
      }

      var middle = (a + c) / 2;
      a = middle + (a - middle) * gapScale;
      b = middle + (b - middle) * gapScale;
      c = middle + (c - middle) * gapScale;

      vertices[i + 0] = a;
      vertices[i + 1] = b;
      vertices[i + 2] = c;

      triangles[i + 0] = i + 0;
      triangles[i + 1] = i + 1;
      triangles[i + 2] = i + 2;

      colors[i + 0] = bgColor;
      colors[i + 1] = bgColor;
      colors[i + 2] = bgColor;

      i += 3;
    }

    mesh = new Mesh {
                      vertices = vertices, triangles = triangles, colors = colors, name = "PenMesh",
                      hideFlags = HideFlags.HideAndDontSave
                    };

    meshFilter.mesh = mesh;
    meshRenderer.material = material;
  }

  private void GenerateTiles() {
    var j = 0;
    tiles = new TileData[Total];
    positions = new Vector2[Total];
    positionLookup = new Dictionary<Vector2, int>();
    for(var i = 0; i < Total; i++) {
      var t = new TileData {
                             neighbors = new int[4], type = RawData.Tiles[j++],
                             position = {
                                          x = (int)((RawData.Tiles[j++] * TestScale) + 0.5f),
                                          y = (int)((RawData.Tiles[j++] * TestScale) + 0.5f)
                                        },
                             center = {
                                        x = RawData.Tiles[j - 2] * FullScale,
                                        y = RawData.Tiles[j - 1] * FullScale
                                      },
                           };

      for(var a = 0; a < 4; a++) t.neighbors[a] = RawData.Tiles[j++];
      tiles[i] = t;
      positions[i] = t.position;
      positionLookup[positions[i]] = i;
    }
  }

  private void GenerateBounds() {
    // find extents of the tiles
    var maxX = -1000000f;
    var maxY = -1000000f;
    var minX =  1000000f;
    var minY =  1000000f;

    for(var i = 0; i < Penrose.Total; i++) {
      var x = tiles[i].center.x;
      var y = tiles[i].center.y;

      minX = minX.Min(x).Round(); 
      minY = minY.Min(y).Round();
      maxX = maxX.Max(x).Round();
      maxY = maxY.Max(y).Round();
    }

    max = new Vector2(maxX, maxY);
    min = new Vector2(minX, minY);

    min.x -= 5f;
    max.x += 5f;

    min.y -= 1f;
    max.y += 2f;

    Debug.Log($"{min}, {max}, {max - min}");
  }

  public void Init() {
    GenerateMesh();
    GenerateTiles();
    GenerateBounds();
    bgBrightness = bgColor.grayscale;
  }

  private void UpdateVertexColors() {
    // color all the mesh vertices
    var x = 0;

    for(var i = 0; i < buffer.Length; i++) {
      // set the vertex color
      for(int j = 0; j < 6; j++) colors[x++] = FadeColorToBgColor(buffer[i]);
    }

    mesh.colors = colors;
  }

  public void Send() {
    UpdateVertexColors();

    //mesh.RecalculateNormals();
  }

  public int GetIndexFromPosition(Vector2 position) {
    // if we have a correct position already then return the index
    if(positionLookup.ContainsKey(position)) return positionLookup[position];

    // try to find the nearest position
    var idx = -1;
    var minDistance = 100000000f;
    for(int i = 0; i < Total; i++) {
      // get the distance 
      var d = (position - positions[i]).magnitude;

      // continue unless we find a shorter distance
      if(d > minDistance) continue;

      idx = i;
      minDistance = d;
    }

    if(idx < 0 || idx > Total) throw new IndexOutOfRangeException($"{idx}: {minDistance}, {position}");

    return positionLookup[positions[idx]];
  }

  private Color FadeColorToBgColor(Color color) {
    return Color.Lerp(bgColor, color, color.grayscale).MinBrightness(bgBrightness);
  }

  [Serializable]
  public class TileData {
    public Vector2 center;
    public Vector2Int position;
    public int[] neighbors;
    public int type;

    public int GetRandomNeighbor() {
      var neighbor = neighbors[Random.Range(0, 4)];
      return neighbor == -1 ? GetRandomNeighbor() : neighbor;
    }

    public override string ToString() =>
      $"{type}, ({center.x},{center.y}), ({neighbors[0]}, {neighbors[1]}, {neighbors[3]}, {neighbors[0]})";
  }
}