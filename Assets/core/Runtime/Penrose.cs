using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Unity component that owns the Penrose tile model, generated preview mesh, layout metadata, and 900-color runtime buffer.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Penrose : MonoBehaviour
{
    public const int Total = 900;
    public const float FullScale = 1.0f / 140.0f;

    /// <summary>Scale used by the coarse position consumed by Vortex and geometric transitions.</summary>
    private const float CoarsePositionScale = 1f / 100f;
    public Color bgColor = Color.gray;

    [Header("Display Size")]
    public float scale = 0.003f;

    public float gapScale = 0.9f;

    [HideInInspector]
    public Color[] buffer = new Color[Total]; // input buffer

    public TileData[] tiles;

    /// <summary>The Penrose pattern data (mesh, tiles, shapes) assigned by <see cref="Init"/>. Runtime-only; not serialized.</summary>
    [NonSerialized]
    public LayoutData Layout;


    public Bounds bounds;

    private readonly Vector3[] vertices = new Vector3[Total * 2 * 3];
    private readonly int[] triangles = new int[Total * 2 * 3];
    private readonly Color[] colors = new Color[Total * 2 * 3];
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material material;
    private float bgBrightness;
    private Vector2Int min;
    private Vector2Int max;

    /// <summary>
    /// Effect-layout bounds centered at zero, sized from rounded tile centers plus fixed padding.
    /// The current layout is (50,22,0); these are not Unity world-space mesh-vertex bounds.
    /// </summary>
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
    }

    /// <summary>
    /// Assigns each tile's shortest adjacency distance from the union of wall-edge tiles and section-seam tiles.
    /// The seed union is distance zero so Effects can address inward bands without treating them as concentric rings.
    /// </summary>
    private void GenerateEdgeAndSeamDistances()
    {
        for (int distance = 0; distance < 10; distance++)
        {
            bool found = false;
            for (int i = 0; i < Tiles.Length; i++)
            {
                TileData t = Tiles[i];
                if (t.edgeAndSeamDistance >= 0) // already assigned
                    continue;
                int neighborCount = t.neighbors.Length;
                if (neighborCount < 4) // wall-edge seed
                {
                    t.edgeAndSeamDistance = distance;
                    found = true;
                    continue;
                }
                for (int y = 0; y < neighborCount; y++)
                {
                    TileData t2 = Tiles[t.neighbors[y].tileIdx];
                    if (t2.section != t.section) // section-seam seed
                    {
                        t.edgeAndSeamDistance = distance;
                        found = true;
                        break;
                    }
                    if (t2.edgeAndSeamDistance == distance - 1) // one step inward
                    {
                        t.edgeAndSeamDistance = distance;
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
        for (int n = 0; n < Layout.Mesh.Length; n += 6)
        {
            var a = new Vector3(Layout.Mesh[j++] * scale, Layout.Mesh[j++] * scale, 0f);
            var b = new Vector3(Layout.Mesh[j++] * scale, Layout.Mesh[j++] * scale, 0f);
            var c = new Vector3(Layout.Mesh[j++] * scale, Layout.Mesh[j++] * scale, 0f);

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
                neighbors = new neighbor[Layout.tiles[i].neighbors.Length],
                type = Layout.tiles[i].type,
                coarsePosition =
                {
                    x = (int)((cent.x * CoarsePositionScale) + 0.5f),
                    y = (int)((cent.y * CoarsePositionScale) + 0.5f)
                },
                center = { x = cent.x * FullScale, y = cent.y * FullScale },
                section = Layout.tiles[i].section,
                tileangle = segangle,
                edgeAndSeamDistance = -3, // undefined until GenerateEdgeAndSeamDistances
                radius = (float)rad,
                angle = (float)Math.Atan2(cent.y, cent.x) * Mathf.Rad2Deg
            };

            for (var j = 0; j < Layout.tiles[i].neighbors.Length; j++)
            {
                t.neighbors[j] = new neighbor();
                t.neighbors[j].type = Layout.tiles[i].neighbors[j].type;
                t.neighbors[j].tileIdx = Layout.tiles[i].neighbors[j].tileIdx;
            }
            //            t.neighbors[j] = RawData.Tiles[idx++];
            tiles[i] = t;
        }
    }

    /// <summary>
    /// Computes effect-layout bounds from iteratively rounded tile centers, applies fixed asymmetric padding,
    /// then centers the resulting size at zero instead of preserving the computed minimum and maximum.
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

    /// <summary>
    /// Adopts the parsed layout, then generates mesh, tile metadata, shared Motif facts, bounds,
    /// edge-and-seam distances, and background brightness.
    /// </summary>
    public void Init(LayoutData layout)
    {
        Layout = layout;
        GenerateMesh();
        GenerateTiles();
        Layout.shapes.Derive(tiles);
        GenerateBounds();
        GenerateEdgeAndSeamDistances();
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
    /// Finds the logical tile whose exact center is nearest to an effect-layout point.
    /// </summary>
    /// <param name="effectLayoutPosition">
    /// Point in the same effect-layout units as <see cref="TileData.center"/> and <see cref="Bounds"/>.
    /// </param>
    /// <returns>The nearest logical tile index. Equal-distance ties preserve the previous later-index behavior.</returns>
    /// <remarks>Performs one linear scan over all 900 exact tile centers.</remarks>
    public int GetNearestTileIndex(Vector2 effectLayoutPosition)
    {
        int nearestIndex = 0;
        float nearestSquaredDistance = (effectLayoutPosition - tiles[0].center).sqrMagnitude;

        for (int i = 1; i < Total; i++)
        {
            float squaredDistance = (effectLayoutPosition - tiles[i].center).sqrMagnitude;
            if (squaredDistance > nearestSquaredDistance)
                continue;

            nearestIndex = i;
            nearestSquaredDistance = squaredDistance;
        }

        return nearestIndex;
    }

    /// <summary>
    /// Blends a tile color toward the configured background based on color brightness.
    /// </summary>
    private Color FadeColorToBgColor(Color color)
    {
        return Color.Lerp(bgColor, color, color.grayscale).MinBrightness(bgBrightness);
    }

    /// <summary>
    /// Runtime copy of one reciprocal full-edge adjacency from the layout data.
    /// </summary>
    [System.Serializable]
    public class neighbor
    {
        /// <summary>
        /// Reciprocal edge-class label, the Penrose matching rules' "edge color": 3 for fat-fat,
        /// 2 for thin-thin, and 4 or 5 for mixed-rhomb edges.
        /// No runtime system currently interprets the label.
        /// </summary>
        public int type;

        /// <summary>Tile index at the other end of this adjacency.</summary>
        public int tileIdx;
    }

    /// <summary>
    /// Runtime tile metadata derived from JSON and used by effects for geometry, topology, and grouping.
    /// </summary>
    [Serializable]
    public class TileData
    {
        /// <summary>Tile center in effect-layout units, with origin at raw mesh (0,0) and the layout JSON y-axis flipped.</summary>
        public Vector2 center;

        /// <summary>
        /// Lossy integer position computed component-wise as <c>(int)(rawCenter / 100 + 0.5)</c>.
        /// The current 900 tiles occupy 846 distinct position values; this field is not a unique tile key.
        /// </summary>
        public Vector2Int coarsePosition;

        /// <summary>Reciprocal full-edge adjacencies in layout order.</summary>
        public neighbor[] neighbors;

        /// <summary>Connected 50-tile build section, numbered 0..17 and arranged spatially as three rows of six.</summary>
        public int section;

        /// <summary>Shortest tile-adjacency distance from any wall-edge tile or tile touching a section seam.</summary>
        public int edgeAndSeamDistance;

        /// <summary>Rhomb type: 0 is fat (about 72/108 degrees); 1 is thin (about 36/144 degrees).</summary>
        public int type;

        /// <summary>Directed short-diagonal bearing in degrees, with zero at +x and positive rotation counter-clockwise.</summary>
        public float tileangle;

        /// <summary>Distance from the raw mesh origin to <see cref="center"/>, in effect-layout units.</summary>
        public float radius;

        /// <summary>Polar bearing of <see cref="center"/> in degrees, with zero at +x and positive rotation counter-clockwise.</summary>
        public float angle;

        /// <summary>
        /// Returns the tile index from one uniformly selected adjacency entry.
        /// </summary>
        public int GetRandomNeighbor()
        {
            return neighbors[Random.Range(0, neighbors.Length)].tileIdx;
        }

        public override string ToString() =>
          $"{type}, ({center.x},{center.y}), ({neighbors[0]}, {neighbors[1]}, {neighbors[3]}, {neighbors[0]})";
    }
}
