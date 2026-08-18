// Verifies AnimateShapes layer controls through its production OnStart and Draw behavior.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Behavioral seam tests for AnimateShapes' Circle/Arc foreground and complementary background.</summary>
public sealed class AnimateShapesLayerOwnershipTests
{
    /// <summary>Comparison tolerance for colors produced by otherwise identical render passes.</summary>
    private const float ColorTolerance = 0.00001f;

    /// <summary>Returns the Editor to Edit Mode even when a Play Mode ownership assertion fails.</summary>
    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterEachTest()
    {
        if (Application.isPlaying)
        {
            yield return new ExitPlayMode();
        }
    }

    /// <summary>
    /// Scenario: a foreground Waveform response and a background hue-rate edit are rendered through
    /// <see cref="AnimateShapes.Draw"/> with identical rolls and exact Circle/Arc membership.
    /// Asserts each control changes its owned partition while every Tile in the opposite partition
    /// keeps its final color.
    /// </summary>
    [UnityTest]
    public IEnumerator ForegroundAndBackgroundControlsStayInsideCircleArcOwnershipPartitions()
    {
        yield return new EnterPlayMode();

        string layoutPath = Path.Combine(Application.streamingAssetsPath, "penrose_layout.txt");
        LayoutData layout = LayoutData.Parse(File.ReadAllText(layoutPath));
        var penroseObject = new GameObject(
            "animate-shapes-layer-ownership-penrose",
            typeof(MeshFilter),
            typeof(MeshRenderer));
        var controllerObject = new GameObject("animate-shapes-layer-ownership-controller");
        AnimateShapesSyncSettingsAsset settingsAsset = Resources.Load<AnimateShapesSyncSettingsAsset>(
            EffectSyncSettingsProvider.ResourcePathFor(typeof(AnimateShapes)));
        Assert.That(settingsAsset, Is.Not.Null, "the shipped AnimateShapes Sync Settings asset");
        var savedSettings = new AnimateShapesSyncSettings();
        savedSettings.CopyFrom(settingsAsset.Settings);
        UnityEngine.Random.State savedRandomState = UnityEngine.Random.state;
        AnimPalette savedPalette = EffectBase.APalette;

        try
        {
            Penrose penrose = penroseObject.AddComponent<Penrose>();
            penrose.Init(layout);

            Controller controller = controllerObject.AddComponent<Controller>();
            controller.penrose = penrose;
            EffectBase.LoadPalette(string.Empty);
            BeatManagerWireFixture.Feed(controller.beatManager, snapshot =>
            {
                snapshot.beatInBar = 2;
                snapshot.beatAverageMs = 500;
                snapshot.beatsCountMs = new[] { 1500, 0, 500, 1000 };
            });
            controller.beatManager.Update(0f);
            Assert.That(controller.beatManager.IsSynced, Is.True, "the production Waveform response is active");

            bool[] foregroundMembership = ReadForegroundMembership(layout.shapes.Circles);
            AnimateShapesSyncSettings settings = settingsAsset.Settings;
            settings.ForegroundWaveformResponseMode = new IntRange(1, 2);
            settings.ForegroundPositionAdvancePerSecond = 0f;
            settings.BackgroundHueRate = 0f;
            settings.ForegroundWaveformStrongPositionShift = 0f;
            Color[] noForegroundResponse = Render(controller, effectDelta: 0f);
            settings.ForegroundWaveformStrongPositionShift = 0.25f;
            Color[] foregroundResponse = Render(controller, effectDelta: 0f);

            AssertOnlyOwnedPartitionChanges(
                noForegroundResponse,
                foregroundResponse,
                foregroundMembership,
                changedPartitionIsForeground: true,
                "foreground Waveform response");

            settings.ForegroundWaveformStrongPositionShift = 0f;
            settings.BackgroundHueRate = 0f;
            Color[] stationaryBackground = Render(controller, effectDelta: 1f);
            settings.BackgroundHueRate = 0.1f;
            Color[] movingBackground = Render(controller, effectDelta: 1f);

            AssertOnlyOwnedPartitionChanges(
                stationaryBackground,
                movingBackground,
                foregroundMembership,
                changedPartitionIsForeground: false,
                "background hue rate");
        }
        finally
        {
            settingsAsset.Settings.CopyFrom(savedSettings);
            UnityEngine.Random.state = savedRandomState;
            EffectBase.APalette = savedPalette;
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(penroseObject);
        }
    }

    /// <summary>Renders one production AnimateShapes activation under a repeatable Roll and draw sequence.</summary>
    /// <param name="controller">The live Controller that owns the production Penrose and musical surfaces.</param>
    /// <param name="effectDelta">Frame delta supplied to the Effect's production draw.</param>
    /// <returns>A copy of the final 900-Tile color buffer.</returns>
    private static Color[] Render(Controller controller, float effectDelta)
    {
        UnityEngine.Random.InitState(155);
        var effect = new AnimateShapes();
        effect.BindController(controller);
        effect.Init();
        effect.OnStart();
        effect.effectTime = 12f;
        effect.effectDelta = effectDelta;
        Assert.That(
            effect.waveform.Envelope,
            Is.GreaterThan(0f),
            "the held Waveform is away from its trough");

        UnityEngine.Random.InitState(1550);
        effect.Draw();
        return (Color[])effect.buffer.Clone();
    }

    /// <summary>Builds exact foreground membership from every packed Circle and Arc group.</summary>
    /// <param name="circles">The production Shape List reader used by AnimateShapes.</param>
    /// <returns>One membership flag per wall Tile; false is the exact complementary background.</returns>
    private static bool[] ReadForegroundMembership(LayoutData.ShapeList.Reader circles)
    {
        var membership = new bool[Penrose.Total];
        for (int groupIndex = 0; groupIndex < circles.GroupCount; groupIndex++)
        {
            LayoutData.ShapeList.Group group = circles.GetGroup(groupIndex);
            for (int tileIndex = 0; tileIndex < group.TileCount; tileIndex++)
            {
                membership[group[tileIndex]] = true;
            }
        }
        return membership;
    }

    /// <summary>Asserts that two frames differ within one owned partition and nowhere in its complement.</summary>
    /// <param name="before">Frame rendered before the setting change.</param>
    /// <param name="after">Frame rendered after the setting change.</param>
    /// <param name="foregroundMembership">Exact Circle/Arc membership by Tile.</param>
    /// <param name="changedPartitionIsForeground">Whether foreground rather than background owns the change.</param>
    /// <param name="controlName">Control description included in assertion failures.</param>
    private static void AssertOnlyOwnedPartitionChanges(
        Color[] before,
        Color[] after,
        bool[] foregroundMembership,
        bool changedPartitionIsForeground,
        string controlName)
    {
        int ownedChanges = 0;
        for (int tileIndex = 0; tileIndex < Penrose.Total; tileIndex++)
        {
            bool changed = !ColorsMatch(before[tileIndex], after[tileIndex]);
            bool belongsToChangedPartition =
                foregroundMembership[tileIndex] == changedPartitionIsForeground;
            if (belongsToChangedPartition)
            {
                if (changed)
                {
                    ownedChanges++;
                }
                continue;
            }

            Assert.That(
                changed,
                Is.False,
                $"{controlName} crossed into Tile {tileIndex} of the opposite partition");
        }

        Assert.That(ownedChanges, Is.GreaterThan(0), $"{controlName} must exercise its owned partition");
    }

    /// <summary>Compares two rendered colors component-by-component within the seam tolerance.</summary>
    /// <param name="left">First rendered color.</param>
    /// <param name="right">Second rendered color.</param>
    /// <returns>True when every color component matches within <see cref="ColorTolerance"/>.</returns>
    private static bool ColorsMatch(Color left, Color right)
    {
        return Mathf.Abs(left.r - right.r) <= ColorTolerance &&
            Mathf.Abs(left.g - right.g) <= ColorTolerance &&
            Mathf.Abs(left.b - right.b) <= ColorTolerance &&
            Mathf.Abs(left.a - right.a) <= ColorTolerance;
    }
}
