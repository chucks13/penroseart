using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Data contracts for the Penrose Wall's data files under <c>Assets/StreamingAssets/</c>.
///
/// The wall's data is split by rate of change:
/// <list type="bullet">
/// <item><see cref="LayoutData"/> — <c>penrose_layout.txt</c>: the 900-tile Penrose pattern
/// (preview mesh, tile topology, decorative shapes). Fixed for the life of the project.</item>
/// <item><see cref="WiringData"/> — <c>wiring_*.txt</c>: physical LED addressing for one
/// art piece. Each build of the wall gets its own wiring file; the Controller selects one
/// via the <c>WIRING_*</c> define at the top of <c>Controller.cs</c> and reads it from
/// StreamingAssets at startup, so wiring mistakes are fixed by editing the text file and
/// restarting — no Unity, no rebuild.</item>
/// </list>
///
/// Both files are hand-documentable text: lines starting with <c>//</c> are comments, the
/// rest is a JSON body parsed with <see cref="JsonUtility"/>. Field names in these classes
/// are the JSON contract — do not rename them without regenerating the data files.
/// </summary>
internal static class WallDataText
{
    /// <summary>
    /// Strips <c>//</c> comment lines from a wall data file, returning the bare JSON body.
    /// Only whole-line comments are supported (leading whitespace allowed).
    /// </summary>
    public static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        using var reader = new StringReader(text);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }
}

/// <summary>
/// The Penrose pattern itself, loaded from <c>Assets/StreamingAssets/penrose_layout.txt</c>:
/// preview mesh geometry, tile topology, and decorative shape lists.
/// Independent of any physical wiring — see <see cref="WiringData"/> for that.
/// </summary>
[Serializable]
public class LayoutData
{
    /// <summary>One reciprocal full-edge adjacency between this tile and another tile.</summary>
    [Serializable]
    public class Neighbor
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

    /// <summary>Source topology for one Penrose tile: rhombus kind, build section, and adjacency.</summary>
    [Serializable]
    public class Tile
    {
        /// <summary>Rhomb type: 0 is fat (about 72/108 degrees); 1 is thin (about 36/144 degrees).</summary>
        public int type;

        /// <summary>Connected 50-tile build section, numbered 0..17 and arranged spatially as three rows of six.</summary>
        public int section;

        /// <summary>Full-edge adjacencies to neighboring tiles.</summary>
        public Neighbor[] neighbors;
    }

    /// <summary>
    /// Named decorative index lists over the 900 tiles, each packed as
    /// [group count, pointer×group count, then per group: (tile count, tileIdx×tile count)].
    /// Consumed by shape-tracing effects (TileShapes, ShapeGlitch, Petals, Mirror, …).
    /// </summary>
    [Serializable]
    public class ShapeList
    {
        /// <summary>Packed loop group data populated from the layout's <c>loops</c> JSON field.</summary>
        [SerializeField] private int[] loops;

        /// <summary>Packed star group data populated from the layout's <c>stars</c> JSON field.</summary>
        [SerializeField] private int[] stars;

        /// <summary>Packed first Line Ribbon family populated from the layout's <c>lines0</c> JSON field.</summary>
        [SerializeField] private int[] lines0;

        /// <summary>Packed second Line Ribbon family populated from the layout's <c>lines1</c> JSON field.</summary>
        [SerializeField] private int[] lines1;

        /// <summary>Packed third Line Ribbon family populated from the layout's <c>lines2</c> JSON field.</summary>
        [SerializeField] private int[] lines2;

        /// <summary>Packed fourth Line Ribbon slot populated from the layout's <c>lines3</c> JSON field.</summary>
        [SerializeField] private int[] lines3;

        /// <summary>Packed fifth Line Ribbon slot populated from the layout's <c>lines4</c> JSON field.</summary>
        [SerializeField] private int[] lines4;

        /// <summary>Packed Lotusball group data populated from the layout's <c>lotusballs</c> JSON field.</summary>
        [SerializeField] private int[] lotusballs;

        /// <summary>Packed Starball group data populated from the layout's <c>starballs</c> JSON field.</summary>
        [SerializeField] private int[] starballs;

        /// <summary>Packed two-tile mirror group data populated from the layout's <c>mirror2</c> JSON field.</summary>
        [SerializeField] private int[] mirror2;

        /// <summary>Packed variable-size mirror group data populated from the layout's <c>mirror10</c> JSON field.</summary>
        [SerializeField] private int[] mirror10;

        /// <summary>Allocation-free access to the loop Shape List groups.</summary>
        public Reader Loops => new(loops);

        /// <summary>Allocation-free access to the star Shape List groups.</summary>
        public Reader Stars => new(stars);

        /// <summary>Allocation-free access to the first Line Ribbon Shape List groups.</summary>
        public Reader Lines0 => new(lines0);

        /// <summary>Allocation-free access to the second Line Ribbon Shape List groups.</summary>
        public Reader Lines1 => new(lines1);

        /// <summary>Allocation-free access to the third Line Ribbon Shape List groups.</summary>
        public Reader Lines2 => new(lines2);

        /// <summary>Allocation-free access to the fourth Line Ribbon Shape List slot.</summary>
        public Reader Lines3 => new(lines3);

        /// <summary>Allocation-free access to the fifth Line Ribbon Shape List slot.</summary>
        public Reader Lines4 => new(lines4);

        /// <summary>Allocation-free access to the Lotusball Shape List groups.</summary>
        public Reader Lotusballs => new(lotusballs);

        /// <summary>Allocation-free access to the Starball Shape List groups.</summary>
        public Reader Starballs => new(starballs);

        /// <summary>Allocation-free access to the two-tile mirror Shape List groups.</summary>
        public Reader Mirror2 => new(mirror2);

        /// <summary>Allocation-free access to the variable-size mirror Shape List groups.</summary>
        public Reader Mirror10 => new(mirror10);

        /// <summary>Allocation-free access to the groups stored in one packed Shape List array.</summary>
        public readonly struct Reader
        {
            /// <summary>The packed group pointers, tile counts, and tile indexes supplied by the layout.</summary>
            private readonly int[] packed;

            /// <summary>Creates a reader over one packed Shape List array without copying its contents.</summary>
            /// <param name="packed">The packed group and tile data to read.</param>
            public Reader(int[] packed)
            {
                this.packed = packed;
            }

            /// <summary>The number of groups declared at the start of the packed array.</summary>
            public int GroupCount => packed[0];

            /// <summary>Decodes one group pointer into an allocation-free tile view.</summary>
            /// <param name="groupIndex">The zero-based group index.</param>
            /// <returns>The selected group's ordered tile indexes.</returns>
            public Group GetGroup(int groupIndex)
            {
                int pointer = packed[groupIndex + 1];
                return new Group(packed, pointer + 1, packed[pointer]);
            }
        }

        /// <summary>Allocation-free access to one decoded Shape List group's ordered tile indexes.</summary>
        public readonly struct Group
        {
            /// <summary>The packed Shape List array that owns this group.</summary>
            private readonly int[] packed;

            /// <summary>The absolute packed-array position of this group's first tile index.</summary>
            private readonly int start;

            /// <summary>Creates a group view over a decoded payload range without copying its tile indexes.</summary>
            /// <param name="packed">The packed Shape List array that owns the group.</param>
            /// <param name="start">The absolute packed-array position of the first tile index.</param>
            /// <param name="tileCount">The number of ordered tile indexes in the group.</param>
            public Group(int[] packed, int start, int tileCount)
            {
                this.packed = packed;
                this.start = start;
                TileCount = tileCount;
            }

            /// <summary>The number of ordered tile indexes in this group.</summary>
            public int TileCount { get; }

            /// <summary>Reads one direct tile index from this group.</summary>
            /// <param name="tileIndex">The zero-based position inside the group.</param>
            /// <value>The tile index stored at the requested group position.</value>
            public int this[int tileIndex] => packed[start + tileIndex];

            /// <summary>
            /// Returns a tile's absolute position in the packed source array so legacy hue arithmetic keeps its exact phase
            /// without making Effects reconstruct group record boundaries.
            /// </summary>
            /// <param name="tileIndex">The zero-based position inside the group.</param>
            /// <returns>The absolute packed-array position occupied by that tile index.</returns>
            public int PackedIndex(int tileIndex)
            {
                return start + tileIndex;
            }
        }
    }

    /// <summary>
    /// 10800 raw coordinate floats: 1800 triangles × 3 vertices × (x,y), with tile k owning triangles 2k and 2k+1.
    /// This is the source geometry for both the Unity preview mesh and effect-visible tile geometry derived by <see cref="Penrose"/>.
    /// </summary>
    public float[] Mesh;

    /// <summary>900 tiles in tile-index order.</summary>
    public Tile[] tiles;

    /// <summary>Decorative shape index lists.</summary>
    public ShapeList shapes;

    /// <summary>
    /// Parses a layout file (comment-stripped JSON) and validates its basic shape.
    /// Throws <see cref="InvalidDataException"/> on malformed data so a bad file fails loud at startup.
    /// </summary>
    public static LayoutData Parse(string text)
    {
        var layout = JsonUtility.FromJson<LayoutData>(WallDataText.StripComments(text));
        if (layout?.Mesh == null || layout.tiles == null || layout.shapes == null)
            throw new InvalidDataException("Layout file is missing Mesh, tiles, or shapes.");
        if (layout.tiles.Length != Penrose.Total)
            throw new InvalidDataException($"Layout file has {layout.tiles.Length} tiles; expected {Penrose.Total}.");
        if (layout.Mesh.Length != Penrose.Total * 2 * 6)
            throw new InvalidDataException($"Layout file has {layout.Mesh.Length} mesh floats; expected {Penrose.Total * 2 * 6}.");
        return layout;
    }
}

/// <summary>
/// Physical LED addressing for one art piece, loaded from an <c>Assets/StreamingAssets/wiring_*.txt</c> file.
/// Each output is one daisy-chained LED string; each entry is the half-tile index (0..1799)
/// that LED displays (tile = value / 2 — two LEDs per tile, one per triangle).
/// The flattened outputs define the global LED index space the S2 Mini boards address
/// (see <c>Assets/core/Hardware/S2_MINI_PROTOCOL.md</c>).
/// </summary>
[Serializable]
public class WiringData
{
    /// <summary>One physical LED string, in daisy-chain order.</summary>
    [Serializable]
    public class Output
    {
        public int[] leds;
    }

    /// <summary>The physical LED strings, in output order.</summary>
    public Output[] outputs;

    /// <summary>
    /// Parses a wiring file and flattens its outputs into one wire map:
    /// physical LED <c>i</c> (global index) displays tile <c>map[i] / 2</c>.
    /// Validates that all outputs together form a permutation of 0..total−1, so a
    /// mis-authored wiring file fails loud at startup instead of scrambling the wall.
    /// </summary>
    public static int[] ParseToWireMap(string text)
    {
        var wiring = JsonUtility.FromJson<WiringData>(WallDataText.StripComments(text));
        if (wiring?.outputs == null || wiring.outputs.Length == 0)
            throw new InvalidDataException("Wiring file has no outputs.");

        int total = 0;
        foreach (var output in wiring.outputs)
        {
            if (output?.leds == null || output.leds.Length == 0)
                throw new InvalidDataException("Wiring file contains an empty output.");
            total += output.leds.Length;
        }

        var map = new int[total];
        var seen = new bool[total];
        int i = 0;
        foreach (var output in wiring.outputs)
        {
            foreach (var led in output.leds)
            {
                if (led < 0 || led >= total)
                    throw new InvalidDataException($"Wiring LED index {led} is outside 0..{total - 1}.");
                if (seen[led])
                    throw new InvalidDataException($"Wiring LED index {led} appears more than once.");
                seen[led] = true;
                map[i++] = led;
            }
        }
        return map;
    }
}
