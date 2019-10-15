using System;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Penrose : MonoBehaviour {

  public const int Total = 900;
  public const float FullScale = 1.0f / 140.0f;

  public Color bgColor = Color.gray;

  [Header("Display Size")]
  public float scale = 0.003f;

  public float gapScale = 0.9f;

  [HideInInspector]
  public Color[] buffer = new Color[Total]; // input buffer

  [HideInInspector]
  public TileData[] tiles;

  private readonly Vector3[] vertices = new Vector3[Total * 2 * 3];
  private readonly int[] triangles = new int[Total * 2 * 3];
  private readonly Color[] colors = new Color[Total * 2 * 3];

  private Mesh mesh;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  private Material material;

  private float bgBrightness;

  private void Awake() {
    meshFilter   = GetComponent<MeshFilter>();
    meshRenderer = GetComponent<MeshRenderer>();
    material     = new Material(Shader.Find("Unlit/Penrose")) {hideFlags = HideFlags.HideAndDontSave, name = "PenMaterial"};
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

    mesh = new Mesh {vertices = vertices, triangles = triangles, colors = colors, name = "PenMesh", hideFlags = HideFlags.HideAndDontSave};

    meshFilter.mesh       = mesh;
    meshRenderer.material = material;
  }

  private void GenerateTiles() {
    float xMax = float.MinValue;
    float xMin = float.MaxValue;
    float yMax = float.MinValue;
    float yMin = float.MaxValue;
      
    var j = 0;
    tiles = new TileData[Total];
    for(var i = 0; i < Total; i++) {
      var t = new TileData {
        neighbors = new int[4], type = RawData.Tiles[j++], center = {x = RawData.Tiles[j++] * FullScale, y = RawData.Tiles[j++] * FullScale}
      };

      for(var a = 0; a < 4; a++) t.neighbors[a] = RawData.Tiles[j++];

      tiles[i] = t;

      xMax = Mathf.Max(t.center.x, xMax);
      yMax = Mathf.Max(t.center.y, yMax);
      xMin = Mathf.Min(t.center.x, xMin);
      yMin = Mathf.Min(t.center.y, yMin);

      
    }


    for(int i = 0; i < tiles.Length; i++) {
      var t = tiles[i];
      var x  = Mathf.Round(t.center.x + xMax);
      var y  = Mathf.Round(t.center.y + yMax);
      var d1 = Mathf.Sqrt((x * x) + (y * y));

      Debug.Log($"i: {i} ({x }, {y})");
    }

    var ix = Mathf.Round(xMax) + Mathf.Round(Mathf.Abs(Mathf.Round(xMin)));
    var iy = Mathf.Round(yMax) + Mathf.Round(Mathf.Abs(Mathf.Round(yMin)));
    Debug.Log($"({xMin}, {yMin}) to ({xMax}, {yMax}), ({ix}, {iy}), {ix * iy} ");
  }

  void Start() {
    GenerateMesh();
    GenerateTiles();
    bgBrightness = bgColor.grayscale;
  }

  void UpdateVertexColors() {
    // color all the mesh vertices
    var x = 0;

    for(var i = 0; i < buffer.Length; i++) {
      // set the vertex color
      for(int j = 0; j < 6; j++) colors[x++] = FadeColorToBgColor(buffer[i]);
    }

    mesh.colors = colors;
  }

  void Update() {
    UpdateVertexColors();
    //mesh.RecalculateNormals();
  }

  private Color FadeColorToBgColor(Color color) {
    return Color.Lerp(bgColor, color, color.grayscale).MinBrightness(bgBrightness);
  }

  [Serializable]
  public class TileData {

    public Vector2 center;
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