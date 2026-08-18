// Verifies the shipped layout's Shape List decoding and effect-facing Penrose geometry.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Parses the real <c>penrose_layout.txt</c> and checks its named Shape Lists and runtime tile geometry.
/// </summary>
public class LayoutShapeListTests
{
    /// <summary>The layout parsed from StreamingAssets, shared by every case.</summary>
    private LayoutData layout;

    /// <summary>The runtime geometry whose layout load derives shared Shape List facts.</summary>
    private Penrose penrose;

    /// <summary>The temporary Unity host for the runtime geometry.</summary>
    private GameObject penroseObject;

    /// <summary>Loads the shipped layout and derives its effect-facing geometry facts once per test.</summary>
    [SetUp]
    public void SetUp()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "penrose_layout.txt");
        layout = LayoutData.Parse(File.ReadAllText(path));
        penroseObject = new GameObject("penrose-geometry-test", typeof(MeshFilter), typeof(MeshRenderer));
        penrose = penroseObject.AddComponent<Penrose>();

        // EditMode does not run MonoBehaviour lifecycle methods, so invoke the same setup Unity runs in Play Mode.
        MethodInfo awake = typeof(Penrose).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(awake, "Penrose.Awake setup method");
        awake.Invoke(penrose, null);
        penrose.Init(layout);
    }

    /// <summary>Destroys the temporary runtime geometry after each test.</summary>
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(penroseObject);
    }

    /// <summary>
    /// Scenario: the shipped layout is parsed through the same entry point the Controller uses.
    /// Asserts every named Shape List accessor reports its layout group count, which fails loudly
    /// if a serialization change leaves any packed array unpopulated.
    /// </summary>
    [Test]
    public void ParsedLayoutPopulatesEveryNamedShapeList()
    {
        LayoutData.ShapeList shapes = layout.shapes;

        Assert.AreEqual(73, shapes.Rings.GroupCount, "Rings");
        Assert.AreEqual(45, shapes.Stars.GroupCount, "Stars");
        Assert.AreEqual(7, shapes.Lines0.GroupCount, "Lines0");
        Assert.AreEqual(15, shapes.Lines1.GroupCount, "Lines1");
        Assert.AreEqual(17, shapes.Lines2.GroupCount, "Lines2");
        Assert.AreEqual(15, shapes.Lines3.GroupCount, "Lines3");
        Assert.AreEqual(49, shapes.Lotusballs.GroupCount, "Lotusballs");
        Assert.AreEqual(32, shapes.Starballs.GroupCount, "Starballs");
        Assert.AreEqual(446, shapes.Mirror2.GroupCount, "Mirror2");
        Assert.AreEqual(163, shapes.Mirror10.GroupCount, "Mirror10");
    }

    /// <summary>
    /// Scenario: a Shape List group is read through the reader the Effects use.
    /// Asserts every star group holds five in-range tile indexes, so a pointer or payload that
    /// decoded to the wrong offset cannot pass unnoticed.
    /// </summary>
    [Test]
    public void StarGroupsDecodeToFiveTilesInRange()
    {
        LayoutData.ShapeList.Reader stars = layout.shapes.Stars;

        for (int i = 0; i < stars.GroupCount; i++)
        {
            LayoutData.ShapeList.Group group = stars.GetGroup(i);
            Assert.AreEqual(5, group.TileCount, $"star group {i} tile count");
            for (int j = 0; j < group.TileCount; j++)
                Assert.That(group[j], Is.InRange(0, Penrose.Total - 1), $"star group {i} tile {j}");
        }
    }

    /// <summary>
    /// Scenario: every Shape List is read after runtime layout loading derives its shared facts.
    /// Asserts reverse membership, unique-Tile centroids, and membership-first Contours agree with
    /// the packed groups and effect-facing Tile centers.
    /// </summary>
    [Test]
    public void SharedFactsMatchEveryPackedShapeListGroup()
    {
        foreach (LayoutData.ShapeList.Reader reader in GetShapeLists())
        {
            for (int groupIndex = 0; groupIndex < reader.GroupCount; groupIndex++)
            {
                LayoutData.ShapeList.Group group = reader.GetGroup(groupIndex);
                var uniqueTiles = new HashSet<int>();
                Vector2 centerSum = Vector2.zero;
                for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
                {
                    int tile = group[tileIndex];
                    Assert.AreEqual(groupIndex, reader.GetGroupIndex(tile), $"group {groupIndex} Tile {tile}");
                    if (uniqueTiles.Add(tile))
                        centerSum += penrose.tiles[tile].center;
                }

                Vector2 expectedCentroid = centerSum / uniqueTiles.Count;
                Assert.That(
                    Vector2.Distance(expectedCentroid, reader.GetCentroid(groupIndex)),
                    Is.LessThan(0.000001f),
                    $"group {groupIndex} centroid");

                LayoutData.ShapeList.Group contour = reader.GetContour(groupIndex);
                var contourTiles = new HashSet<int>();
                for (int contourIndex = 0; contourIndex < contour.TileCount; contourIndex++)
                {
                    int tile = contour[contourIndex];
                    Assert.IsTrue(contourTiles.Add(tile), $"group {groupIndex} duplicate Contour Tile {tile}");
                    Assert.AreEqual(-1, reader.GetGroupIndex(tile), $"group {groupIndex} member claimed as Contour Tile {tile}");
                }
            }
        }
    }

    /// <summary>
    /// Scenario: the shared Starball role facts are read from every shipped Starball group.
    /// Asserts the first five packed Tiles are the fat closed-Star Core and the remaining five
    /// Tiles are the thin Surround.
    /// </summary>
    [Test]
    public void StarballsExposeVerifiedCoreAndSurroundParts()
    {
        LayoutData.ShapeList.Reader starballs = layout.shapes.Starballs;
        for (int groupIndex = 0; groupIndex < starballs.GroupCount; groupIndex++)
        {
            LayoutData.ShapeList.Group group = starballs.GetGroup(groupIndex);
            Assert.AreEqual(10, group.TileCount, $"Starball group {groupIndex} tile count");
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                int tile = group[tileIndex];
                bool isCore = tileIndex < 5;
                Assert.AreEqual(isCore ? 0 : 1, penrose.tiles[tile].type, $"Starball group {groupIndex} Tile {tile} type");
                Assert.AreEqual(
                    isCore ? LayoutData.ShapeList.PartRole.Core : LayoutData.ShapeList.PartRole.Surround,
                    starballs.GetPart(tile),
                    $"Starball group {groupIndex} Tile {tile} Part");
            }

            for (int coreIndex = 0; coreIndex < 5; coreIndex++)
            {
                Assert.IsTrue(
                    AreNeighbors(group[coreIndex], group[(coreIndex + 1) % 5]),
                    $"Starball group {groupIndex} Core edge {coreIndex}");
            }
        }
    }

    /// <summary>
    /// Scenario: the shared Lotusball role facts are read from every shipped Lotusball group.
    /// Asserts each group has exactly one fat Center with four in-group Neighbors and every other
    /// member is Surround.
    /// </summary>
    [Test]
    public void LotusballsExposeUniqueDegreeFourCenters()
    {
        LayoutData.ShapeList.Reader lotusballs = layout.shapes.Lotusballs;
        for (int groupIndex = 0; groupIndex < lotusballs.GroupCount; groupIndex++)
        {
            LayoutData.ShapeList.Group group = lotusballs.GetGroup(groupIndex);
            int center = lotusballs.GetCenterTile(groupIndex);
            int centerCount = 0;
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                int tile = group[tileIndex];
                bool isCenter = tile == center;
                if (isCenter)
                    centerCount++;

                Assert.AreEqual(
                    isCenter ? LayoutData.ShapeList.PartRole.Center : LayoutData.ShapeList.PartRole.Surround,
                    lotusballs.GetPart(tile),
                    $"Lotusball group {groupIndex} Tile {tile} Part");
            }

            Assert.AreEqual(1, centerCount, $"Lotusball group {groupIndex} Center count");
            Assert.AreEqual(0, penrose.tiles[center].type, $"Lotusball group {groupIndex} Center type");
            Assert.AreEqual(4, CountNeighborsInGroup(center, group), $"Lotusball group {groupIndex} Center degree");
        }
    }

    /// <summary>
    /// Scenario: the Rings Shape List is read as ordered neighboring fat-Tile paths.
    /// Asserts every packed path and the shared closed-Ring versus wall-clipped-Arc classification,
    /// including the corresponding normalized traversal positions.
    /// </summary>
    [Test]
    public void RingsExposePackedPathsClosureAndPositions()
    {
        LayoutData.ShapeList.Reader rings = layout.shapes.Rings;
        for (int groupIndex = 0; groupIndex < rings.GroupCount; groupIndex++)
        {
            LayoutData.ShapeList.Group group = rings.GetGroup(groupIndex);
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                int tile = group[tileIndex];
                Assert.AreEqual(0, penrose.tiles[tile].type, $"Rings group {groupIndex} Tile {tile} type");
                if (tileIndex > 0)
                {
                    Assert.IsTrue(
                        AreNeighbors(group[tileIndex - 1], tile),
                        $"Rings group {groupIndex} path edge {tileIndex - 1}");
                }
            }

            bool expectedClosed = group.TileCount > 2 && AreNeighbors(group[group.TileCount - 1], group[0]);
            Assert.AreEqual(expectedClosed, rings.IsClosed(groupIndex), $"Rings group {groupIndex} closure");
            float denominator = expectedClosed ? group.TileCount : group.TileCount - 1;
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                float expectedPosition = denominator > 0f ? tileIndex / denominator : 0f;
                Assert.AreEqual(
                    expectedPosition,
                    rings.GetPosition(group[tileIndex]),
                    $"Rings group {groupIndex} position {tileIndex}");
            }
        }
    }

    /// <summary>
    /// Scenario: each Line Ribbon family exposes one position per distinct Tile in packed travel order.
    /// Asserts the shared position fact deduplicates the repeated Tile 466 in Lines2 group 10 instead
    /// of shortening the remainder of that Ribbon.
    /// </summary>
    [Test]
    public void LineRibbonPositionsDeduplicateRepeatedTiles()
    {
        var ribbons = new[]
        {
            layout.shapes.Lines0,
            layout.shapes.Lines1,
            layout.shapes.Lines2,
            layout.shapes.Lines3,
        };

        for (int familyIndex = 0; familyIndex < ribbons.Length; familyIndex++)
        {
            LayoutData.ShapeList.Reader ribbon = ribbons[familyIndex];
            for (int groupIndex = 0; groupIndex < ribbon.GroupCount; groupIndex++)
            {
                LayoutData.ShapeList.Group group = ribbon.GetGroup(groupIndex);
                var uniqueTiles = new List<int>();
                var seenTiles = new HashSet<int>();
                for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
                {
                    int tile = group[tileIndex];
                    if (seenTiles.Add(tile))
                        uniqueTiles.Add(tile);
                }

                for (int tileIndex = 1; tileIndex < uniqueTiles.Count; tileIndex++)
                {
                    Assert.IsTrue(
                        AreNeighbors(uniqueTiles[tileIndex - 1], uniqueTiles[tileIndex]),
                        $"Line Ribbon family {familyIndex} group {groupIndex} edge {tileIndex - 1}");
                }

                float denominator = uniqueTiles.Count - 1;
                for (int tileIndex = 0; tileIndex < uniqueTiles.Count; tileIndex++)
                {
                    float expectedPosition = denominator > 0f ? tileIndex / denominator : 0f;
                    Assert.AreEqual(
                        expectedPosition,
                        ribbon.GetPosition(uniqueTiles[tileIndex]),
                        $"Line Ribbon family {familyIndex} group {groupIndex} position {tileIndex}");
                }
            }
        }

        LayoutData.ShapeList.Group repeatedGroup = layout.shapes.Lines2.GetGroup(10);
        int occurrences = 0;
        for (int tileIndex = 0; tileIndex < repeatedGroup.TileCount; tileIndex++)
        {
            if (repeatedGroup[tileIndex] == 466)
                occurrences++;
        }

        Assert.AreEqual(2, occurrences, "Lines2 group 10 repeated Tile 466");
    }

    /// <summary>
    /// Scenario: runtime geometry is generated from the shipped layout and every exact tile center is queried.
    /// Asserts the nearest-tile interface preserves all 900 logical tile identities despite collisions in the
    /// separate coarse positions.
    /// </summary>
    [Test]
    public void NearestTileLookupReturnsEveryTileAtItsExactCenter()
    {
        for (int i = 0; i < Penrose.Total; i++)
        {
            Assert.AreEqual(
                i,
                penrose.GetNearestTileIndex(penrose.tiles[i].center),
                $"tile {i} exact center");
        }
    }

    /// <summary>Returns every shipped Shape List reader for common seam assertions.</summary>
    /// <returns>The ten named Shape List readers.</returns>
    private LayoutData.ShapeList.Reader[] GetShapeLists()
    {
        LayoutData.ShapeList shapes = layout.shapes;
        return new[]
        {
            shapes.Rings,
            shapes.Stars,
            shapes.Lines0,
            shapes.Lines1,
            shapes.Lines2,
            shapes.Lines3,
            shapes.Lotusballs,
            shapes.Starballs,
            shapes.Mirror2,
            shapes.Mirror10,
        };
    }

    /// <summary>Reports whether two runtime Tiles share one complete edge.</summary>
    /// <param name="fromTile">The Tile whose Neighbor list is read.</param>
    /// <param name="toTile">The candidate neighboring Tile.</param>
    /// <returns><c>true</c> when the Tiles are Neighbors.</returns>
    private bool AreNeighbors(int fromTile, int toTile)
    {
        foreach (Penrose.neighbor neighbor in penrose.tiles[fromTile].neighbors)
        {
            if (neighbor.tileIdx == toTile)
                return true;
        }

        return false;
    }

    /// <summary>Counts one runtime Tile's Neighbors that belong to a packed Motif group.</summary>
    /// <param name="tile">The Tile whose in-group degree is requested.</param>
    /// <param name="group">The Motif group whose membership is tested.</param>
    /// <returns>The Tile's Neighbor count inside the Motif.</returns>
    private int CountNeighborsInGroup(int tile, LayoutData.ShapeList.Group group)
    {
        int count = 0;
        foreach (Penrose.neighbor neighbor in penrose.tiles[tile].neighbors)
        {
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                if (neighbor.tileIdx == group[tileIndex])
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }
}
