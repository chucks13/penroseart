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
    /// Owns the wall's shared Motif facts and serves them with the packed groups through one reader.
    /// Consumed by shape-tracing Effects such as TileShapes, ShapeGlitch, Petals, and Mirror.
    /// </summary>
    /// <remarks>
    /// The facts stay load-derived rather than generator-baked because membership, Parts, Contours,
    /// positions, closure, and centroids are pure consequences of the authoritative layout. Keeping one
    /// derivation beside the reader prevents generated facts from disagreeing with changed topology or
    /// carrying a packing defect such as a repeated Line Ribbon Tile into every consumer. Centroids cannot
    /// be completed directly in <see cref="LayoutData.Parse"/> because serialized <see cref="Tile"/> values
    /// carry no centers: <see cref="Penrose.GenerateTiles"/> derives the effect-facing coordinates from
    /// <see cref="LayoutData.Mesh"/> in its y-flipped coordinate space. Derivation therefore runs immediately
    /// after that step, still inside layout loading and before any Effect initializes.
    /// </remarks>
    [Serializable]
    public class ShapeList
    {
        /// <summary>Packed Ring and Arc group data populated from the layout's <c>loops</c> JSON field.</summary>
        [SerializeField] private int[] loops;

        /// <summary>Packed star group data populated from the layout's <c>stars</c> JSON field.</summary>
        [SerializeField] private int[] stars;

        /// <summary>Packed first Line Ribbon family populated from the layout's <c>lines0</c> JSON field.</summary>
        [SerializeField] private int[] lines0;

        /// <summary>Packed second Line Ribbon family populated from the layout's <c>lines1</c> JSON field.</summary>
        [SerializeField] private int[] lines1;

        /// <summary>Packed third Line Ribbon family populated from the layout's <c>lines2</c> JSON field.</summary>
        [SerializeField] private int[] lines2;

        /// <summary>Packed fourth Line Ribbon family populated from the layout's <c>lines3</c> JSON field.</summary>
        [SerializeField] private int[] lines3;

        /// <summary>Packed Lotusball group data populated from the layout's <c>lotusballs</c> JSON field.</summary>
        [SerializeField] private int[] lotusballs;

        /// <summary>Packed Starball group data populated from the layout's <c>starballs</c> JSON field.</summary>
        [SerializeField] private int[] starballs;

        /// <summary>Packed two-tile mirror group data populated from the layout's <c>mirror2</c> JSON field.</summary>
        [SerializeField] private int[] mirror2;

        /// <summary>Packed variable-size mirror group data populated from the layout's <c>mirror10</c> JSON field.</summary>
        [SerializeField] private int[] mirror10;

        /// <summary>Facts derived from the Rings Shape List during layout loading.</summary>
        private DerivedFacts ringsFacts;

        /// <summary>Facts derived from the Stars Shape List during layout loading.</summary>
        private DerivedFacts starsFacts;

        /// <summary>Facts derived from the first Line Ribbon family during layout loading.</summary>
        private DerivedFacts lines0Facts;

        /// <summary>Facts derived from the second Line Ribbon family during layout loading.</summary>
        private DerivedFacts lines1Facts;

        /// <summary>Facts derived from the third Line Ribbon family during layout loading.</summary>
        private DerivedFacts lines2Facts;

        /// <summary>Facts derived from the fourth Line Ribbon family during layout loading.</summary>
        private DerivedFacts lines3Facts;

        /// <summary>Facts derived from the Lotusball Shape List during layout loading.</summary>
        private DerivedFacts lotusballFacts;

        /// <summary>Facts derived from the Starball Shape List during layout loading.</summary>
        private DerivedFacts starballFacts;

        /// <summary>Facts derived from the two-tile mirror Shape List during layout loading.</summary>
        private DerivedFacts mirror2Facts;

        /// <summary>Facts derived from the variable-size mirror Shape List during layout loading.</summary>
        private DerivedFacts mirror10Facts;

        /// <summary>The finest named Part role a Tile holds inside its Motif.</summary>
        public enum PartRole
        {
            /// <summary>The Tile belongs to no named internal role.</summary>
            None,

            /// <summary>The single degree-four fat Tile at the heart of a Lotusball.</summary>
            Center,

            /// <summary>The five-Tile Star at the heart of a Starball.</summary>
            Core,

            /// <summary>The Tiles around a Lotusball Center or Starball Core.</summary>
            Surround,
        }

        /// <summary>The role convention used while deriving one Shape List.</summary>
        private enum RoleKind
        {
            /// <summary>The Motif has no named internal role.</summary>
            None,

            /// <summary>The Motif is a Lotusball with a Center and Surround.</summary>
            Lotusball,

            /// <summary>The Motif is a Starball with a Core and Surround.</summary>
            Starball,
        }

        /// <summary>The ordered-path convention used while deriving one Shape List.</summary>
        private enum PathKind
        {
            /// <summary>The packed order carries no promoted path fact.</summary>
            None,

            /// <summary>The packed order is an open Line Ribbon.</summary>
            Ribbon,

            /// <summary>The packed order is a closed Ring or wall-clipped Arc.</summary>
            Ring,
        }

        /// <summary>Allocation-free access to the Ring and Arc groups stored in the serialized <c>loops</c> field.</summary>
        public Reader Rings => new(loops, ringsFacts);

        /// <summary>Allocation-free access to the star Shape List groups.</summary>
        public Reader Stars => new(stars, starsFacts);

        /// <summary>Allocation-free access to the first Line Ribbon Shape List groups.</summary>
        public Reader Lines0 => new(lines0, lines0Facts);

        /// <summary>Allocation-free access to the second Line Ribbon Shape List groups.</summary>
        public Reader Lines1 => new(lines1, lines1Facts);

        /// <summary>Allocation-free access to the third Line Ribbon Shape List groups.</summary>
        public Reader Lines2 => new(lines2, lines2Facts);

        /// <summary>Allocation-free access to the fourth Line Ribbon Shape List groups.</summary>
        public Reader Lines3 => new(lines3, lines3Facts);

        /// <summary>Allocation-free access to the Lotusball Shape List groups.</summary>
        public Reader Lotusballs => new(lotusballs, lotusballFacts);

        /// <summary>Allocation-free access to the Starball Shape List groups.</summary>
        public Reader Starballs => new(starballs, starballFacts);

        /// <summary>Allocation-free access to the two-tile mirror Shape List groups.</summary>
        public Reader Mirror2 => new(mirror2, mirror2Facts);

        /// <summary>Allocation-free access to the variable-size mirror Shape List groups.</summary>
        public Reader Mirror10 => new(mirror10, mirror10Facts);

        /// <summary>
        /// Derives the wall's shared Motif facts once while <see cref="Penrose.Init"/> loads the layout.
        /// </summary>
        /// <param name="tiles">The effect-facing Tiles after their Mesh-derived centers and y flip exist.</param>
        internal void Derive(Penrose.TileData[] tiles)
        {
            var stampByTile = new int[tiles.Length];
            var tileScratch = new int[tiles.Length];
            int nextStamp = 0;

            ringsFacts = DeriveFacts(
                loops, tiles, RoleKind.None, PathKind.Ring, stampByTile, tileScratch, ref nextStamp);
            starsFacts = DeriveFacts(
                stars, tiles, RoleKind.None, PathKind.None, stampByTile, tileScratch, ref nextStamp);
            lines0Facts = DeriveFacts(
                lines0, tiles, RoleKind.None, PathKind.Ribbon, stampByTile, tileScratch, ref nextStamp);
            lines1Facts = DeriveFacts(
                lines1, tiles, RoleKind.None, PathKind.Ribbon, stampByTile, tileScratch, ref nextStamp);
            lines2Facts = DeriveFacts(
                lines2, tiles, RoleKind.None, PathKind.Ribbon, stampByTile, tileScratch, ref nextStamp);
            lines3Facts = DeriveFacts(
                lines3, tiles, RoleKind.None, PathKind.Ribbon, stampByTile, tileScratch, ref nextStamp);
            lotusballFacts = DeriveFacts(
                lotusballs, tiles, RoleKind.Lotusball, PathKind.None, stampByTile, tileScratch, ref nextStamp);
            starballFacts = DeriveFacts(
                starballs, tiles, RoleKind.Starball, PathKind.None, stampByTile, tileScratch, ref nextStamp);
            mirror2Facts = DeriveFacts(
                mirror2, tiles, RoleKind.None, PathKind.None, stampByTile, tileScratch, ref nextStamp);
            mirror10Facts = DeriveFacts(
                mirror10, tiles, RoleKind.None, PathKind.None, stampByTile, tileScratch, ref nextStamp);
        }

        /// <summary>Builds the plain reverse-index, role, Contour, position, closure, and centroid arrays for one list.</summary>
        /// <param name="packed">The serialized packed Shape List.</param>
        /// <param name="tiles">The effect-facing Tiles carrying centers and Neighbors.</param>
        /// <param name="roleKind">The named internal roles expressed by this Motif family.</param>
        /// <param name="pathKind">The packed-order path fact expressed by this Motif family.</param>
        /// <param name="stampByTile">Reusable per-Tile stamps that deduplicate each group and Contour.</param>
        /// <param name="tileScratch">Reusable storage for one group's deduplicated Tiles.</param>
        /// <param name="nextStamp">The next unique stamp shared by every derived Shape List.</param>
        /// <returns>The derived facts held behind one <see cref="Reader"/>.</returns>
        private static DerivedFacts DeriveFacts(
            int[] packed,
            Penrose.TileData[] tiles,
            RoleKind roleKind,
            PathKind pathKind,
            int[] stampByTile,
            int[] tileScratch,
            ref int nextStamp)
        {
            var source = new Reader(packed, facts: null);
            int groupCount = source.GroupCount;
            var groupByTile = new int[tiles.Length];
            var partByTile = new PartRole[tiles.Length];
            var positionByTile = new float[tiles.Length];
            var centerByGroup = new int[groupCount];
            var centroidByGroup = new Vector2[groupCount];
            var closedByGroup = new bool[groupCount];
            var uniqueTilesByGroup = new int[groupCount][];

            for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
            {
                groupByTile[tileIndex] = -1;
                positionByTile[tileIndex] = -1f;
            }

            for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                centerByGroup[groupIndex] = -1;
                Group group = source.GetGroup(groupIndex);
                int uniqueTileCount = 0;
                int groupStamp = ++nextStamp;
                for (int packedIndex = 0; packedIndex < group.TileCount; packedIndex++)
                {
                    int tile = group[packedIndex];
                    if (stampByTile[tile] == groupStamp)
                    {
                        continue;
                    }

                    stampByTile[tile] = groupStamp;
                    tileScratch[uniqueTileCount++] = tile;
                    groupByTile[tile] = groupIndex;
                    centroidByGroup[groupIndex] += tiles[tile].center;
                }

                int[] unique = CopyTiles(tileScratch, uniqueTileCount);
                uniqueTilesByGroup[groupIndex] = unique;
                centroidByGroup[groupIndex] /= unique.Length;
                DerivePathFacts(
                    unique,
                    tiles,
                    groupIndex,
                    pathKind,
                    positionByTile,
                    closedByGroup);
                DeriveRoleFacts(
                    group,
                    unique,
                    tiles,
                    groupIndex,
                    roleKind,
                    partByTile,
                    centerByGroup);
            }

            int[][] contourByGroup = DeriveContours(
                uniqueTilesByGroup,
                groupByTile,
                tiles,
                stampByTile,
                tileScratch,
                ref nextStamp);
            return new DerivedFacts(
                groupByTile,
                partByTile,
                positionByTile,
                centerByGroup,
                centroidByGroup,
                closedByGroup,
                contourByGroup);
        }

        /// <summary>Derives the promoted packed-order position and Rings-family closure fact for one group.</summary>
        /// <param name="group">The group's deduplicated packed-order Tiles.</param>
        /// <param name="tiles">The effect-facing Tiles carrying Neighbors.</param>
        /// <param name="groupIndex">The group index receiving the derived closure fact.</param>
        /// <param name="pathKind">The path convention carried by this Shape List.</param>
        /// <param name="positionByTile">The per-Tile position array receiving normalized traversal positions.</param>
        /// <param name="closedByGroup">The per-group closure array receiving Ring/Arc classification.</param>
        private static void DerivePathFacts(
            int[] group,
            Penrose.TileData[] tiles,
            int groupIndex,
            PathKind pathKind,
            float[] positionByTile,
            bool[] closedByGroup)
        {
            if (pathKind == PathKind.None)
            {
                return;
            }

            bool isClosedRing = pathKind == PathKind.Ring
                && group.Length > 2
                && AreNeighbors(tiles, group[group.Length - 1], group[0]);
            closedByGroup[groupIndex] = isClosedRing;
            float denominator = isClosedRing ? group.Length : group.Length - 1;
            for (int pathIndex = 0; pathIndex < group.Length; pathIndex++)
            {
                positionByTile[group[pathIndex]] = denominator > 0f
                    ? pathIndex / denominator
                    : 0f;
            }
        }

        /// <summary>Derives the named Part roles and Lotusball Center for one Motif group.</summary>
        /// <param name="packedGroup">The original packed group, whose Starball prefix is meaningful.</param>
        /// <param name="uniqueGroup">The group's deduplicated Tiles.</param>
        /// <param name="tiles">The effect-facing Tiles carrying Rhomb Type and Neighbors.</param>
        /// <param name="groupIndex">The group index reported when its role invariant is broken.</param>
        /// <param name="roleKind">The role convention expressed by this Motif family.</param>
        /// <param name="partByTile">The per-Tile role array receiving the derived roles.</param>
        /// <param name="centerByGroup">The per-group array receiving a Lotusball Center Tile.</param>
        private static void DeriveRoleFacts(
            Group packedGroup,
            int[] uniqueGroup,
            Penrose.TileData[] tiles,
            int groupIndex,
            RoleKind roleKind,
            PartRole[] partByTile,
            int[] centerByGroup)
        {
            if (roleKind == RoleKind.None)
            {
                return;
            }

            if (roleKind == RoleKind.Starball)
            {
                const int coreTileCount = 5;
                for (int tileIndex = 0; tileIndex < packedGroup.TileCount; tileIndex++)
                {
                    int tile = packedGroup[tileIndex];
                    bool isCore = tileIndex < coreTileCount;
                    partByTile[tile] = isCore ? PartRole.Core : PartRole.Surround;
                }

                return;
            }

            int centerTile = -1;
            for (int tileIndex = 0; tileIndex < uniqueGroup.Length; tileIndex++)
            {
                int tile = uniqueGroup[tileIndex];
                partByTile[tile] = PartRole.Surround;
                if (tiles[tile].type == 0 && CountNeighborsInGroup(tiles, tile, uniqueGroup) == 4)
                {
                    centerTile = tile;
                }
            }

            if (centerTile < 0)
            {
                throw new InvalidDataException(
                    $"Lotusball group {groupIndex} has no fat Tile with four in-group Neighbors.");
            }

            partByTile[centerTile] = PartRole.Center;
            centerByGroup[groupIndex] = centerTile;
        }

        /// <summary>Builds each Motif's deduplicated Contour after all Motif membership is known.</summary>
        /// <param name="uniqueTilesByGroup">The deduplicated member Tiles for every group.</param>
        /// <param name="groupByTile">The complete tile-to-group reverse index whose membership outranks Contours.</param>
        /// <param name="tiles">The effect-facing Tiles carrying Neighbors.</param>
        /// <param name="stampByTile">Reusable per-Tile stamps that deduplicate each Contour.</param>
        /// <param name="tileScratch">Reusable storage for one group's deduplicated Contour Tiles.</param>
        /// <param name="nextStamp">The next unique stamp shared by every derived Shape List.</param>
        /// <returns>One plain Contour Tile array per group.</returns>
        private static int[][] DeriveContours(
            int[][] uniqueTilesByGroup,
            int[] groupByTile,
            Penrose.TileData[] tiles,
            int[] stampByTile,
            int[] tileScratch,
            ref int nextStamp)
        {
            var contours = new int[uniqueTilesByGroup.Length][];
            for (int groupIndex = 0; groupIndex < uniqueTilesByGroup.Length; groupIndex++)
            {
                int contourTileCount = 0;
                int groupStamp = ++nextStamp;
                int[] group = uniqueTilesByGroup[groupIndex];
                for (int tileIndex = 0; tileIndex < group.Length; tileIndex++)
                {
                    foreach (Penrose.neighbor neighbor in tiles[group[tileIndex]].neighbors)
                    {
                        int candidate = neighbor.tileIdx;
                        if (groupByTile[candidate] >= 0 || stampByTile[candidate] == groupStamp)
                        {
                            continue;
                        }

                        stampByTile[candidate] = groupStamp;
                        tileScratch[contourTileCount++] = candidate;
                    }
                }

                contours[groupIndex] = CopyTiles(tileScratch, contourTileCount);
            }

            return contours;
        }

        /// <summary>
        /// Copies one completed Tile-index range out of shared scratch storage so its reader remains stable.
        /// </summary>
        /// <param name="source">The shared Tile-index scratch array.</param>
        /// <param name="count">The number of populated entries to retain.</param>
        /// <returns>A right-sized stable array, or the shared empty array when the range is empty.</returns>
        private static int[] CopyTiles(int[] source, int count)
        {
            if (count == 0)
            {
                return Array.Empty<int>();
            }

            var copy = new int[count];
            Array.Copy(source, copy, count);
            return copy;
        }

        /// <summary>Counts how many of one Tile's Neighbors belong to the supplied Motif group.</summary>
        /// <param name="tiles">The effect-facing Tiles carrying Neighbors.</param>
        /// <param name="tile">The Tile whose in-group degree is requested.</param>
        /// <param name="group">The Motif's deduplicated member Tiles.</param>
        /// <returns>The Tile's Neighbor count inside the Motif.</returns>
        private static int CountNeighborsInGroup(Penrose.TileData[] tiles, int tile, int[] group)
        {
            int count = 0;
            foreach (Penrose.neighbor neighbor in tiles[tile].neighbors)
            {
                for (int candidateIndex = 0; candidateIndex < group.Length; candidateIndex++)
                {
                    if (neighbor.tileIdx == group[candidateIndex])
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>Reports whether two Tiles share one complete edge.</summary>
        /// <param name="tiles">The effect-facing Tiles carrying Neighbors.</param>
        /// <param name="fromTile">The Tile whose adjacency list is read.</param>
        /// <param name="toTile">The candidate Neighbor Tile.</param>
        /// <returns><c>true</c> when the two Tiles are Neighbors.</returns>
        private static bool AreNeighbors(Penrose.TileData[] tiles, int fromTile, int toTile)
        {
            foreach (Penrose.neighbor neighbor in tiles[fromTile].neighbors)
            {
                if (neighbor.tileIdx == toTile)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The plain arrays held behind one derived Shape List reader.</summary>
        internal sealed class DerivedFacts
        {
            /// <summary>Creates one immutable bundle of derived Shape List arrays.</summary>
            /// <param name="groupByTile">Per-Tile group membership, or -1.</param>
            /// <param name="partByTile">Per-Tile finest named Part role.</param>
            /// <param name="positionByTile">Per-Tile Ribbon or Ring position, or -1.</param>
            /// <param name="centerByGroup">Per-group Lotusball Center Tile, or -1.</param>
            /// <param name="centroidByGroup">Per-group centroid in effect-layout coordinates.</param>
            /// <param name="closedByGroup">Per-group closed Ring fact.</param>
            /// <param name="contourByGroup">Per-group Contour Tiles.</param>
            public DerivedFacts(
                int[] groupByTile,
                PartRole[] partByTile,
                float[] positionByTile,
                int[] centerByGroup,
                Vector2[] centroidByGroup,
                bool[] closedByGroup,
                int[][] contourByGroup)
            {
                GroupByTile = groupByTile;
                PartByTile = partByTile;
                PositionByTile = positionByTile;
                CenterByGroup = centerByGroup;
                CentroidByGroup = centroidByGroup;
                ClosedByGroup = closedByGroup;
                ContourByGroup = contourByGroup;
            }

            /// <summary>Per-Tile group membership, or -1.</summary>
            public int[] GroupByTile { get; }

            /// <summary>Per-Tile finest named Part role.</summary>
            public PartRole[] PartByTile { get; }

            /// <summary>Per-Tile Ribbon or Ring position, or -1.</summary>
            public float[] PositionByTile { get; }

            /// <summary>Per-group Lotusball Center Tile, or -1.</summary>
            public int[] CenterByGroup { get; }

            /// <summary>Per-group centroid in effect-layout coordinates.</summary>
            public Vector2[] CentroidByGroup { get; }

            /// <summary>Per-group closed Ring fact.</summary>
            public bool[] ClosedByGroup { get; }

            /// <summary>Per-group Contour Tiles.</summary>
            public int[][] ContourByGroup { get; }
        }

        /// <summary>
        /// Allocation-free access to one Shape List's groups and its shared load-derived Motif facts.
        /// </summary>
        public readonly struct Reader
        {
            /// <summary>The packed group pointers, tile counts, and tile indexes supplied by the layout.</summary>
            private readonly int[] packed;

            /// <summary>The load-derived plain arrays shared by every copy of this reader.</summary>
            private readonly DerivedFacts facts;

            /// <summary>Creates a reader over one packed Shape List and its derived facts without copying either.</summary>
            /// <param name="packed">The packed group and tile data to read.</param>
            /// <param name="facts">The load-derived facts, or null only while those facts are being built.</param>
            internal Reader(int[] packed, DerivedFacts facts)
            {
                this.packed = packed;
                this.facts = facts;
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

            /// <summary>Returns the Shape List group containing one Tile.</summary>
            /// <param name="tileIndex">The direct wall Tile index.</param>
            /// <returns>The zero-based group index, or -1 when the Tile belongs to no group in this list.</returns>
            public int GetGroupIndex(int tileIndex) => facts.GroupByTile[tileIndex];

            /// <summary>Returns one Tile's finest named Part role inside its Motif.</summary>
            /// <param name="tileIndex">The direct wall Tile index.</param>
            /// <returns>The Tile's Center, Core, Surround, or no named role.</returns>
            public PartRole GetPart(int tileIndex) => facts.PartByTile[tileIndex];

            /// <summary>Returns the Center Tile of one Lotusball group.</summary>
            /// <param name="groupIndex">The zero-based group index.</param>
            /// <returns>The direct Center Tile index, or -1 when this Motif has no Center role.</returns>
            public int GetCenterTile(int groupIndex) => facts.CenterByGroup[groupIndex];

            /// <summary>Returns the centroid of one group in effect-layout coordinates.</summary>
            /// <param name="groupIndex">The zero-based group index.</param>
            /// <returns>The arithmetic mean of the group's unique Tile centers.</returns>
            public Vector2 GetCentroid(int groupIndex) => facts.CentroidByGroup[groupIndex];

            /// <summary>Returns one Motif's Contour after all membership in this Shape List has won.</summary>
            /// <param name="groupIndex">The zero-based group index.</param>
            /// <returns>The deduplicated Tiles bordering this Motif without belonging to any Motif in the list.</returns>
            public Group GetContour(int groupIndex)
            {
                int[] contour = facts.ContourByGroup[groupIndex];
                return new Group(contour, 0, contour.Length);
            }

            /// <summary>Returns one Tile's normalized position along its Line Ribbon, Ring, or Arc.</summary>
            /// <param name="tileIndex">The direct wall Tile index.</param>
            /// <returns>The deduplicated packed-order position, or -1 when this list carries no position for the Tile.</returns>
            public float GetPosition(int tileIndex) => facts.PositionByTile[tileIndex];

            /// <summary>Reports whether one Rings-list group is a closed Ring rather than a wall-clipped Arc.</summary>
            /// <param name="groupIndex">The zero-based group index.</param>
            /// <returns><c>true</c> for a closed Ring; <c>false</c> for an Arc or a non-Rings motif.</returns>
            public bool IsClosed(int groupIndex) => facts.ClosedByGroup[groupIndex];
        }

        /// <summary>Allocation-free access to one Tile-index array segment.</summary>
        public readonly struct Group
        {
            /// <summary>The Tile-index source array that owns this view.</summary>
            private readonly int[] source;

            /// <summary>The absolute source-array position of this view's first Tile index.</summary>
            private readonly int start;

            /// <summary>Creates a view over a Tile-index range without copying it.</summary>
            /// <param name="source">The Tile-index array that owns the range.</param>
            /// <param name="start">The absolute source-array position of the first Tile index.</param>
            /// <param name="tileCount">The number of ordered Tile indexes in the view.</param>
            public Group(int[] source, int start, int tileCount)
            {
                this.source = source;
                this.start = start;
                TileCount = tileCount;
            }

            /// <summary>The number of ordered Tile indexes in this view.</summary>
            public int TileCount { get; }

            /// <summary>Reads one direct Tile index from this view.</summary>
            /// <param name="tileIndex">The zero-based position inside the view.</param>
            /// <value>The Tile index stored at the requested position.</value>
            public int this[int tileIndex] => source[start + tileIndex];

            /// <summary>
            /// Returns a Tile's absolute position in its source array. For packed Shape List groups this lets legacy
            /// hue arithmetic keep its exact phase without making Effects reconstruct group record boundaries.
            /// </summary>
            /// <param name="tileIndex">The zero-based position inside the view.</param>
            /// <returns>The absolute source-array position occupied by that Tile index.</returns>
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
