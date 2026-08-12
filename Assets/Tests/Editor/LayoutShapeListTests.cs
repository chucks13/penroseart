// Verifies the shipped layout's Shape List decoding and effect-facing Penrose geometry.

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

    /// <summary>Loads and parses the shipped layout file once per test.</summary>
    [SetUp]
    public void SetUp()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "penrose_layout.txt");
        layout = LayoutData.Parse(File.ReadAllText(path));
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

        Assert.AreEqual(69, shapes.Loops.GroupCount, "Loops");
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
    /// Scenario: runtime geometry is generated from the shipped layout and every exact tile center is queried.
    /// Asserts the nearest-tile interface preserves all 900 logical tile identities despite collisions in the
    /// separate coarse positions.
    /// </summary>
    [Test]
    public void NearestTileLookupReturnsEveryTileAtItsExactCenter()
    {
        var gameObject = new GameObject("penrose-geometry-test", typeof(MeshFilter), typeof(MeshRenderer));

        try
        {
            Penrose penrose = gameObject.AddComponent<Penrose>();
            // EditMode does not run MonoBehaviour lifecycle methods, so invoke the same setup Unity runs in Play Mode.
            MethodInfo awake = typeof(Penrose).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(awake, "Penrose.Awake setup method");
            awake.Invoke(penrose, null);
            penrose.Init(layout);

            for (int i = 0; i < Penrose.Total; i++)
            {
                Assert.AreEqual(
                    i,
                    penrose.GetNearestTileIndex(penrose.tiles[i].center),
                    $"tile {i} exact center");
            }
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
