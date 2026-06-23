using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using System.IO;

/*
[System.Serializable]
public class SavedState
{
	public string Name;
	public string SaveToString() { return JsonUtility.ToJson(this); }
}

[System.Serializable]
public class MySavedState : SavedState
{
	public int[][]Loops;
	public Color Background;
}
*/

/// <summary>
/// Deserialized Penrose geometry, shape, and wiring data loaded from StreamingAssets JSON.
/// </summary>
[System.Serializable]
public class JsonData
{
    /// <summary>
    /// JSON shape-neighbor entry describing a neighboring tile and edge color.
    /// </summary>
    [System.Serializable]
    public class neighbor
    {
        public int type;
        public int tileIdx;
    }

    /// <summary>
    /// JSON tile entry containing the source topology for one Penrose tile.
    /// </summary>
    [System.Serializable]
    public class tile
    {
        //        public int[] triangles;    // triangle indexes
        public int type;            // 0 or 1 for thin or fat
        public int section;
        public neighbor[] neighbors;
    };

    /// <summary>
    /// Packed named shape collections loaded from Penrose JSON.
    /// </summary>
    [System.Serializable]
    public class shapelist
    {
        public int[] loops;
        public int[] stars;
        public int[] lines0;
        public int[] lines1;
        public int[] lines2;
        public int[] lines3;
        public int[] lines4;
        public int[] lotusballs;
        public int[] starballs;
        public int[] mirror2;
        public int[] mirror10;
    };

    // [count,pointers[count],(length,tiles[length])
    // raw data
    public float[] Mesh;
    public tile[] tiles;            // 900 of these
    public int[] wires;             // 1800 of these, wiring order for rendering
    public shapelist shapes;

    /// <summary>
    /// Loads and deserializes a Penrose JSON data file from StreamingAssets.
    /// </summary>
    public static JsonData CreateFromJSON(string fileName)
    {
        var sr = new StreamReader(Application.streamingAssetsPath + "/" + fileName);
        var fileContents = sr.ReadToEnd();
        sr.Close();
        return JsonUtility.FromJson<JsonData>(fileContents);
    }


}

/// <summary>
/// Unity component that owns the Penrose tile model, generated preview mesh, JSON metadata, and 900-color runtime buffer.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Penrose : MonoBehaviour
{
    public const int Total = 900;
    public const float FullScale = 1.0f / 140.0f;
    private const float TestScale = 1f / 100f;
    public Color bgColor = Color.gray;

    [Header("Display Size")]
    public float scale = 0.003f;

    public float gapScale = 0.9f;

    [HideInInspector]
    public Color[] buffer = new Color[Total]; // input buffer

    public TileData[] tiles;

    public JsonData JsonRawData = new JsonData();


    public Bounds bounds;

    private readonly Vector3[] vertices = new Vector3[Total * 2 * 3];
    private readonly int[] triangles = new int[Total * 2 * 3];
    private readonly Color[] colors = new Color[Total * 2 * 3];
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material material;
    private float bgBrightness;
    private Dictionary<Vector2, int> centerLookup;
    private Vector2[] centers;
    private Vector2Int min;
    private Vector2Int max;

    /// <summary>Bounds of the generated Penrose tile layout in Unity/world coordinates.</summary>
    public Bounds Bounds => bounds;

    /// <summary>Runtime metadata for all logical Penrose tiles.</summary>
    public TileData[] Tiles => tiles;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        material =
          new Material(Shader.Find("Unlit/Penrose"))
          {
              hideFlags = HideFlags.HideAndDontSave,
              name = "PenMaterial"
          };
        //JsonRawData = JsonData.CreateFromJSON("rawdata.json");
        JsonRawData = JsonUtility.FromJson<JsonData>(Controller.Instance.jsonSource);
        /*---------------------------

        var mySavedState = new MySavedState
        {
            Name = "someSavedState",
            Loops =
            {
                { 1, 3, 5, 7, 9},
                { 0, 2, 4, 6},
                { 11, 22}
            },

            Background = Color.black
        };

        var jsonString = mySavedState.SaveToString();

        -------------------------*/

    }

    /// <summary>
    /// Derives ring numbers for each tile from integer tile positions.
    /// </summary>
    private void GenerateRings()
    {
        for (int ring = 0; ring < 10; ring++)
        {
            bool found = false;
            for (int i = 0; i < Tiles.Length; i++)
            {
                TileData t = Tiles[i];
                if (t.ring >= 0)            // already marked
                    continue;
                int neighborCount = t.neighbors.Length;
                if (neighborCount < 4)         // outside edge, automatic
                {
                    t.ring = ring;
                    found = true;
                    continue;
                }
                for (int y = 0; y < neighborCount; y++)
                {
                    TileData t2 = Tiles[t.neighbors[y].tileIdx];
                    if (t2.section != t.section)        // borders another section, automatic
                    {
                        t.ring = ring;
                        found = true;
                        break;
                    }
                    if (t2.ring == ring - 1)    // touches previous ring
                    {
                        t.ring = ring;
                        found = true;
                        break;
                    }
                }
            }
            if (!found)     // nothing marked
                break;
        }
    }

    /// <summary>
    /// Builds the Unity preview mesh from JSON mesh floats, applying scale, gap, and y-axis flip.
    /// </summary>
    private void GenerateMesh()
    {
        var i = 0;
        var j = 0;
        Vector3 reflect = new Vector3(1, -1, 1);

        // grab the geometry
        for (int n = 0; n < JsonRawData.Mesh.Length; n += 6)
        {
            var a = new Vector3(JsonRawData.Mesh[j++] * scale, JsonRawData.Mesh[j++] * scale, 0f);
            var b = new Vector3(JsonRawData.Mesh[j++] * scale, JsonRawData.Mesh[j++] * scale, 0f);
            var c = new Vector3(JsonRawData.Mesh[j++] * scale, JsonRawData.Mesh[j++] * scale, 0f);

            var ab = b - a;
            var ac = c - a;

            if (Vector3.Cross(ab, ac).z > 0)
            {
                Vector3 x = c;
                c = a;
                a = x;
            }

            var middle = (a + c) / 2;
            a = middle + (a - middle) * gapScale;
            b = middle + (b - middle) * gapScale;
            c = middle + (c - middle) * gapScale;

            a.y *= -1f;
            b.y *= -1f;
            c.y *= -1f;
            vertices[i + 0] = c;
            vertices[i + 1] = b;
            vertices[i + 2] = a;

            triangles[i + 0] = i + 0;
            triangles[i + 1] = i + 1;
            triangles[i + 2] = i + 2;

            colors[i + 0] = bgColor;
            colors[i + 1] = bgColor;
            colors[i + 2] = bgColor;

            i += 3;
        }

        mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            colors = colors,
            name = "PenMesh",
            hideFlags = HideFlags.HideAndDontSave
        };

        meshFilter.mesh = mesh;
        meshRenderer.material = material;
    }

    /// <summary>
    /// Builds runtime TileData objects from JSON topology and generated mesh centers.
    /// </summary>
    private void GenerateTiles()
    {
        int ix2 = 0;
        tiles = new TileData[Total];
        centers = new Vector2[Total];
        centerLookup = new Dictionary<Vector2, int>();
        for (var i = 0; i < Total; i++)
        {
            var cent = (vertices[ix2] + vertices[ix2 + 2]) / 2;

            // find angle
            Vector2 maxseg = cent - vertices[ix2];

            cent /= scale;
            float segangle = (float)Math.Atan2(maxseg.y, maxseg.x) * Mathf.Rad2Deg;
            Vector2 center = new Vector2(cent.x * FullScale, cent.y * FullScale);
            double rad = Math.Sqrt((center.x * center.x) + (center.y * center.y));
            if (segangle > 180f)
                segangle -= 180f;
            ix2 += 6;
            var t = new TileData
            {
                neighbors = new neighbor[JsonRawData.tiles[i].neighbors.Length],
                type = JsonRawData.tiles[i].type,
                position = { x = (int)((cent.x * TestScale) + 0.5f), y = (int)((cent.y * TestScale) + 0.5f) },
                center = { x = cent.x * FullScale, y = cent.y * FullScale },
                section = JsonRawData.tiles[i].section,
                tileangle = segangle,
                ring = -3,                   // undefined
                radius = (float)rad,
                angle = (float)Math.Atan2(cent.y, cent.x) * Mathf.Rad2Deg
            };

            for (var j = 0; j < JsonRawData.tiles[i].neighbors.Length; j++)
            {
                t.neighbors[j] = new neighbor();
                t.neighbors[j].type = JsonRawData.tiles[i].neighbors[j].type;
                t.neighbors[j].tileIdx = JsonRawData.tiles[i].neighbors[j].tileIdx;
            }
            //            t.neighbors[j] = RawData.Tiles[idx++];
            tiles[i] = t;
            centers[i] = t.position;
            centerLookup[centers[i]] = i;
        }
    }

    /// <summary>
    /// Computes the aggregate Bounds that enclose all generated tile mesh vertices.
    /// </summary>
    private void GenerateBounds()
    {
        // find extents of the tiles
        var maxX = -1000000f;
        var maxY = -1000000f;
        var minX = 1000000f;
        var minY = 1000000f;

        for (var i = 0; i < Penrose.Total; i++)
        {
            var x = tiles[i].center.x;
            var y = tiles[i].center.y;

            minX = minX.Min(x).Round();
            minY = minY.Min(y).Round();
            maxX = maxX.Max(x).Round();
            maxY = maxY.Max(y).Round();
        }

        max = new Vector2Int((int)maxX, (int)maxY);
        min = new Vector2Int((int)minX, (int)minY);

        min.x -= 5;
        max.x += 5;

        min.y -= 1;
        max.y += 2;

        //    Debug.Log($"{min}, {max}, {max - min}");

        var size = max - min;
        bounds = new Bounds(Vector3.zero, new Vector3(size.x, size.y));
        //    Debug.Log(bounds.size);
    }

    /*
  // there is a bug in the json data
  private void patchLoops()
    {
        int shapeidx = JsonRawData.shapes.loops[3];          // broken one here
        int countidx = JsonRawData.shapes.loops[shapeidx];
        int count = JsonRawData.shapes.loops[countidx];
    }
    */
    /// <summary>
    /// Generates mesh, tile metadata, bounds, rings, and background brightness after JSON is loaded.
    /// </summary>
    public void Init()
    {
        //    patchLoops();
        GenerateMesh();
        GenerateTiles();
        GenerateBounds();
        GenerateRings();
        bgBrightness = bgColor.grayscale;
    }

    /// <summary>
    /// Copies each logical tile color into the six vertex-color slots for that tile.
    /// </summary>
    private void UpdateVertexColors()
    {
        // color all the mesh vertices
        var x = 0;

        for (var i = 0; i < buffer.Length; i++)
        {
            // set the vertex color
            for (int j = 0; j < 6; j++) colors[x++] = FadeColorToBgColor(buffer[i]);
        }

        mesh.colors = colors;
    }

    /// <summary>
    /// Updates the Unity mesh preview from the current 900-tile buffer.
    /// </summary>
    public void UpdateModelColors()
    {
        UpdateVertexColors();

        //mesh.RecalculateNormals();
    }

    /// <summary>
    /// Finds the logical tile index at an integerized Penrose position.
    /// </summary>
    public int GetIndexFromPosition(Vector2 position)
    {
        // if we have a correct position already then return the index
        if (centerLookup.ContainsKey(position)) return centerLookup[position];

        // try to find the nearest position
        var idx = -1;
        var minDistance = 100000000f;

        for (int i = 0; i < Total; i++)
        {
            var d = (position - centers[i]).magnitude; // get the distance
            if (d > minDistance) continue;              // continue unless we find a shorter distance

            idx = i;
            minDistance = d;
        }

        if (idx < 0 || idx > Total)
            throw new IndexOutOfRangeException($"{idx}: {minDistance}, {position}");

        return centerLookup[centers[idx]];
    }

    /// <summary>
    /// Blends a tile color toward the configured background based on color brightness.
    /// </summary>
    private Color FadeColorToBgColor(Color color)
    {
        return Color.Lerp(bgColor, color, color.grayscale).MinBrightness(bgBrightness);
    }

    [System.Serializable]
    /// <summary>
    /// JSON shape-neighbor entry describing a neighboring tile and edge color.
    /// </summary>
    public class neighbor
    {
        public int type;
        public int tileIdx;
    }
    [Serializable]
    /// <summary>
    /// Runtime tile metadata derived from JSON and used by effects for geometry, topology, and grouping.
    /// </summary>
    public class TileData
    {
        public Vector2 center;
        public Vector2Int position;
        public neighbor[] neighbors;
        public int section;
        public int ring;
        public int type;
        public float tileangle;
        public float radius;
        public float angle;

        /// <summary>
        /// Returns a random valid neighbor tile index, retrying until a non-negative neighbor is found.
        /// </summary>
        public int GetRandomNeighbor()
        {
            return neighbors[Random.Range(0, neighbors.Length)].tileIdx;
        }

        public override string ToString() =>
          $"{type}, ({center.x},{center.y}), ({neighbors[0]}, {neighbors[1]}, {neighbors[3]}, {neighbors[0]})";
    }
}
