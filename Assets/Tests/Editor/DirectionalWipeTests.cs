using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Checks DirectionalWipe's caller-visible projection and reactive edge behavior.</summary>
public sealed class DirectionalWipeTests
{
    /// <summary>
    /// Scenario: the external blender changes the wipe angle between draws over the shipped coarse tile geometry.
    /// Asserts the same off-axis tile changes source sides, preserving both the coarse projection and live angle input.
    /// </summary>
    [Test]
    public void BlendUsesCurrentAngleWhenProjectingCoarseTileGeometry()
    {
        string layoutPath = Path.Combine(Application.streamingAssetsPath, "penrose_layout.txt");
        LayoutData layout = LayoutData.Parse(File.ReadAllText(layoutPath));
        var penroseObject = new GameObject("directional-wipe-geometry", typeof(MeshFilter), typeof(MeshRenderer));
        var controllerObject = new GameObject("directional-wipe-controller");

        try
        {
            Penrose penrose = penroseObject.AddComponent<Penrose>();
            MethodInfo awake = typeof(Penrose).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(awake, "Penrose.Awake setup method");
            awake.Invoke(penrose, null);
            penrose.Init(layout);

            Controller controller = controllerObject.AddComponent<Controller>();
            controller.penrose = penrose;
            controller.beatManager = new BeatManager();

            var transition = new DirectionalWipe();
            transition.BindController(controller);
            transition.Init();

            int tileIndex = 0;
            float projectionMargin = float.NegativeInfinity;
            for (int i = 0; i < Penrose.Total; i++)
            {
                Vector2Int position = penrose.tiles[i].coarsePosition;
                float candidateMargin = position.y - Mathf.Abs(position.x);
                if (candidateMargin > projectionMargin)
                {
                    tileIndex = i;
                    projectionMargin = candidateMargin;
                }
            }

            Assert.That(projectionMargin, Is.GreaterThan(10f), "the layout must contain a tile clear of both wipe edges");

            var sourceA = new Color[Penrose.Total];
            var sourceB = new Color[Penrose.Total];
            var positiveAngle = new Color[Penrose.Total];
            var negativeAngle = new Color[Penrose.Total];
            for (int i = 0; i < Penrose.Total; i++)
            {
                sourceA[i] = Color.red;
                sourceB[i] = Color.blue;
            }

            transition.setFaders(new[] { 0.5f.ToString(), (Mathf.PI / 4f).ToString() });
            transition.Blend(positiveAngle, sourceA, sourceB);
            transition.setFaders(new[] { 0.5f.ToString(), (-Mathf.PI / 4f).ToString() });
            transition.Blend(negativeAngle, sourceA, sourceB);

            AssertSameColor(Color.blue, positiveAngle[tileIndex]);
            AssertSameColor(Color.red, negativeAngle[tileIndex]);
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(penroseObject);
        }
    }

    [Test]
    public void LowBandBrightnessLeavesColorUnchangedAwayFromEdge()
    {
        var color = new Color(0.4f, 0.2f, 0.1f, 1f);

        AssertSameColor(color, DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 0f, lowBandLevel: 1f));
    }

    [Test]
    public void EdgeBrightnessHasBaseLiftWithoutLowBandPulse()
    {
        var color = new Color(0.4f, 0.2f, 0.1f, 0.75f);

        var baseEdge = DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 1f, lowBandLevel: 0f);

        Assert.That(baseEdge.r, Is.GreaterThan(color.r));
        Assert.That(baseEdge.g, Is.GreaterThan(color.g));
        Assert.That(baseEdge.b, Is.GreaterThan(color.b));
        Assert.That(baseEdge.r, Is.LessThan(0.55f));
        Assert.That(baseEdge.a, Is.EqualTo(color.a));
    }

    [Test]
    public void LowBandBrightnessAddsReactiveLiftAtWipeEdge()
    {
        var color = new Color(0.4f, 0.2f, 0.1f, 0.75f);

        var baseEdge = DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 1f, lowBandLevel: 0f);
        var brightened = DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 1f, lowBandLevel: 1f);

        Assert.That(brightened.r, Is.GreaterThan(baseEdge.r + 0.25f));
        Assert.That(brightened.g, Is.GreaterThan(baseEdge.g + 0.15f));
        Assert.That(brightened.b, Is.GreaterThan(baseEdge.b + 0.1f));
        Assert.That(brightened.r, Is.LessThanOrEqualTo(1f));
        Assert.That(brightened.a, Is.EqualTo(color.a));
    }

    [Test]
    public void EdgePresencePeaksAtWipeBoundaryAndFallsAway()
    {
        Assert.That(DirectionalWipe.EdgePresence(projectedProgress: 0.5f, transitionProgress: 0.5f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(DirectionalWipe.EdgePresence(projectedProgress: 0.51f, transitionProgress: 0.5f), Is.GreaterThan(0f));
        Assert.That(DirectionalWipe.EdgePresence(projectedProgress: 0.8f, transitionProgress: 0.5f), Is.EqualTo(0f).Within(0.001f));
    }

    private static void AssertSameColor(Color expected, Color actual)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }
}
