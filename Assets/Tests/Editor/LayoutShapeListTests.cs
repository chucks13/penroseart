// Verifies that the shipped layout file still deserializes into every named Shape List accessor.

using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Parses the real <c>penrose_layout.txt</c> and checks that every named Shape List accessor
/// reports the group count the layout file carries.
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
        Assert.AreEqual(17, shapes.Lines3.GroupCount, "Lines3");
        Assert.AreEqual(15, shapes.Lines4.GroupCount, "Lines4");
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
}
