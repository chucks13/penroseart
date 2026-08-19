// Verifies AnimateShapes layer controls through its production OnStart and Draw behavior.
using System.Collections;
using System.Collections.Generic;
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
    /// Scenario: the regular background is rendered at a Waveform crest and trough, while the active
    /// Drop background is rendered at the same two Waveform positions, through
    /// <see cref="AnimateShapes.Draw"/> with identical rolls and exact Circle/Arc membership. Asserts
    /// the regular crest reaches its configured human-weighted brightness by lifting toward the
    /// color's own greatest component, the trough reaches its independent floor, the Drop background
    /// ignores both the Waveform and its settings, and Standalone ignores the Synced response.
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
            BeatClockFixture.SeedBeatClock(controller.beatManager, 120f, 0f);
            controller.beatManager.Update(0f);
            Assert.That(controller.beatManager.IsSynced, Is.True, "the production Waveform response is active");

            bool[] foregroundMembership = ReadForegroundMembership(layout.shapes.Circles);
            int backgroundTile = FindFirstBackgroundTile(foregroundMembership);
            AnimateShapesSyncSettings settings = settingsAsset.Settings;
            Waveform waveform = CreateWaveformFixture(controller.beatManager);
            Assert.That(waveform.Envelope, Is.EqualTo(1f).Within(ColorTolerance), "the test begins at a Waveform crest");
            settings.ForegroundPositionAdvancePerSecond = 0f;
            settings.BackgroundHueRate = 0f;
            settings.BackgroundWaveformBrightnessFloor = 0.2f;
            settings.BackgroundWaveformPeakBrightnessTarget = 0f;
            Color[] sourceHueFrame = Render(controller, waveform, effectDelta: 0f);
            Color.RGBToHSV(sourceHueFrame[backgroundTile], out float sourceHue, out _, out _);
            const float darkBlueHue = 2f / 3f;
            settings.BackgroundHueRate = Mathf.Repeat(darkBlueHue - sourceHue, 1f);
            Color[] unliftedBlueCrest = Render(controller, waveform, effectDelta: 1f);
            const float brightnessTarget = 0.6f;
            Assert.That(
                HumanWeightedRgbBrightness(unliftedBlueCrest[backgroundTile]),
                Is.LessThan(brightnessTarget),
                "the fixture must exercise a full-Value color that still reads dark");

            settings.BackgroundWaveformPeakBrightnessTarget = brightnessTarget;
            Color[] liftedBlueCrest = Render(controller, waveform, effectDelta: 1f);
            AssertOnlyOwnedPartitionChanges(
                unliftedBlueCrest,
                liftedBlueCrest,
                foregroundMembership,
                changedPartitionIsForeground: false,
                "regular-background peak-brightness target");
            Color liftedBlue = liftedBlueCrest[backgroundTile];
            Assert.That(
                HumanWeightedRgbBrightness(liftedBlue),
                Is.EqualTo(brightnessTarget).Within(ColorTolerance),
                "the regular crest should reach its configured brightness");
            Assert.That(
                liftedBlue.maxColorComponent,
                Is.EqualTo(unliftedBlueCrest[backgroundTile].maxColorComponent).Within(ColorTolerance),
                "the lift should use the source color's own greatest component");
            Assert.That(
                liftedBlue.maxColorComponent - Mathf.Min(liftedBlue.r, liftedBlue.g, liftedBlue.b),
                Is.GreaterThan(0.1f),
                "the lifted crest should remain visibly chromatic rather than reaching neutral");

            settings.BackgroundWaveformBrightnessFloor = 0.9f;
            Color[] highFloorBlueCrest = Render(controller, waveform, effectDelta: 1f);
            AssertFramesMatch(
                liftedBlueCrest,
                highFloorBlueCrest,
                "the Waveform floor must not change a crest");

            settings.BackgroundHueRate = 0f;
            settings.BackgroundWaveformPeakBrightnessTarget = 0f;
            Color[] stationaryBackground = Render(controller, waveform, effectDelta: 1f);
            settings.BackgroundHueRate = 0.1f;
            Color[] movingBackground = Render(controller, waveform, effectDelta: 1f);

            AssertOnlyOwnedPartitionChanges(
                stationaryBackground,
                movingBackground,
                foregroundMembership,
                changedPartitionIsForeground: false,
                "background hue rate");

            BeatClockFixture.SeedBeatClock(controller.beatManager, 120f, 0.25f);
            controller.beatManager.Update(0.25f);
            Assert.That(waveform.Envelope, Is.EqualTo(0f).Within(ColorTolerance), "the test is at a Waveform trough");
            settings.BackgroundHueRate = Mathf.Repeat(darkBlueHue - sourceHue, 1f);
            settings.BackgroundWaveformBrightnessFloor = 0.2f;
            settings.BackgroundWaveformPeakBrightnessTarget = 0f;
            Color[] lowTargetTrough = Render(controller, waveform, effectDelta: 1f);
            settings.BackgroundWaveformPeakBrightnessTarget = 1f;
            Color[] highTargetTrough = Render(controller, waveform, effectDelta: 1f);
            AssertFramesMatch(
                lowTargetTrough,
                highTargetTrough,
                "the peak-brightness target must not change a trough");
            Assert.That(
                highTargetTrough[backgroundTile].maxColorComponent,
                Is.EqualTo(settings.BackgroundWaveformBrightnessFloor).Within(ColorTolerance),
                "the Waveform trough should reach its configured floor");

            settings.BackgroundWaveformBrightnessFloor = 0.7f;
            Color[] raisedFloorTrough = Render(controller, waveform, effectDelta: 1f);
            AssertOnlyOwnedPartitionChanges(
                highTargetTrough,
                raisedFloorTrough,
                foregroundMembership,
                changedPartitionIsForeground: false,
                "independent Waveform trough floor");
            Assert.That(
                raisedFloorTrough[backgroundTile].maxColorComponent,
                Is.EqualTo(settings.BackgroundWaveformBrightnessFloor).Within(ColorTolerance),
                "the raised Waveform trough should reach its configured floor");

            BeatClockFixture.SeedActiveDrop(
                controller.beatManager,
                bpm: 120f,
                timeSeconds: 0f,
                beatsRemaining: 16,
                lengthBeats: 16);
            controller.beatManager.Update(0f);
            Assert.That(waveform.Envelope, Is.EqualTo(1f).Within(ColorTolerance), "the Drop comparison begins at a Waveform crest");
            settings.BackgroundWaveformBrightnessFloor = 0.5f;
            settings.BackgroundWaveformPeakBrightnessTarget = 0.6f;
            settings.BackgroundDropTileHueStep = 0.001f;
            settings.BackgroundDropHueRate = 0.5f;
            settings.BackgroundDropValue = 1f;
            Color[] dropFrame = Render(controller, waveform, effectDelta: 0f);
            settings.BackgroundWaveformBrightnessFloor = 0f;
            settings.BackgroundWaveformPeakBrightnessTarget = 1f;
            Color[] dropFrameWithOppositeWaveformSettings = Render(controller, waveform, effectDelta: 0f);
            AssertFramesMatch(
                dropFrame,
                dropFrameWithOppositeWaveformSettings,
                "the active-Drop background must ignore regular-background Waveform settings");

            BeatClockFixture.SeedActiveDrop(
                controller.beatManager,
                bpm: 120f,
                timeSeconds: 0.25f,
                beatsRemaining: 16,
                lengthBeats: 16);
            controller.beatManager.Update(0.25f);
            Assert.That(waveform.Envelope, Is.EqualTo(0f).Within(ColorTolerance), "the Drop comparison ends at a Waveform trough");
            Color[] dropTroughFrame = Render(controller, waveform, effectDelta: 0f);
            AssertBackgroundPartitionsMatch(
                dropFrame,
                dropTroughFrame,
                foregroundMembership,
                "the active-Drop background must ignore the Waveform envelope");

            var backgroundColors = new HashSet<Color32>();
            for (int tileIndex = 0; tileIndex < Penrose.Total; tileIndex++)
            {
                if (!foregroundMembership[tileIndex])
                {
                    backgroundColors.Add(dropFrame[tileIndex]);
                }
            }
            Assert.That(
                backgroundColors.Count,
                Is.GreaterThan(300),
                "the 334 background Tiles should retain a smooth Drop hue gradient");
            AssertContinuousBackgroundHueField(
                dropFrame,
                foregroundMembership,
                settings.BackgroundDropTileHueStep);

            controller.beatManager.SetLiveBeatSource(false);
            controller.beatManager.Update(0f);
            Assert.That(controller.beatManager.IsSynced, Is.False, "the final comparison is Standalone");
            settings.BackgroundWaveformBrightnessFloor = 0f;
            settings.BackgroundWaveformPeakBrightnessTarget = 0f;
            Color[] standaloneWithoutResponse = Render(controller, waveform, effectDelta: 0f);
            settings.BackgroundWaveformBrightnessFloor = 1f;
            settings.BackgroundWaveformPeakBrightnessTarget = 1f;
            Color[] standaloneWithEditedSyncResponse = Render(controller, waveform, effectDelta: 0f);
            AssertFramesMatch(
                standaloneWithoutResponse,
                standaloneWithEditedSyncResponse,
                "Standalone must not acquire the Synced background response");
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
    /// <param name="controller">The live Controller that provides the production Penrose and musical surfaces.</param>
    /// <param name="waveform">The test-owned artistic Waveform assigned through the Effect's public seam.</param>
    /// <param name="effectDelta">Frame delta supplied to the Effect's production draw.</param>
    /// <param name="effectTime">Effect clock position supplied to the production draw.</param>
    /// <returns>A copy of the final 900-Tile color buffer.</returns>
    private static Color[] Render(
        Controller controller,
        Waveform waveform,
        float effectDelta,
        float effectTime = 12f)
    {
        UnityEngine.Random.InitState(155);
        var effect = new AnimateShapes();
        effect.BindController(controller);
        effect.Init();
        effect.OnStart();
        effect.waveform = waveform;
        effect.effectTime = effectTime;
        effect.effectDelta = effectDelta;

        UnityEngine.Random.InitState(1550);
        effect.Draw();
        return (Color[])effect.buffer.Clone();
    }

    /// <summary>Acquires a test-owned Waveform without replacing the application's shared Waveforms surface.</summary>
    /// <param name="beatManager">The live musical clock the test Waveform reads.</param>
    /// <returns>A deterministic clock-bound Waveform independent of shipped Preset tuning.</returns>
    private static Waveform CreateWaveformFixture(BeatManager beatManager)
    {
        const string waveformName = "layer ownership pulse";
        var waveforms = new Waveforms(beatManager, new[]
        {
            new WaveformPool.Entry(
                waveformName,
                Waveform.Parse("QQQQ", "8888", 0.3f, 0f, out _)),
        });
        return waveforms.Named(waveformName);
    }

    /// <summary>Finds one Tile owned by AnimateShapes' complementary background.</summary>
    /// <param name="foregroundMembership">Exact Circle/Arc membership by Tile.</param>
    /// <returns>The first Tile index outside the foreground partition.</returns>
    private static int FindFirstBackgroundTile(bool[] foregroundMembership)
    {
        for (int tileIndex = 0; tileIndex < foregroundMembership.Length; tileIndex++)
        {
            if (!foregroundMembership[tileIndex])
            {
                return tileIndex;
            }
        }

        throw new AssertionException("AnimateShapes must have a complementary background Tile");
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

    /// <summary>Asserts two production frames match across both ownership partitions.</summary>
    /// <param name="left">First rendered frame.</param>
    /// <param name="right">Second rendered frame.</param>
    /// <param name="scenario">Scenario included in assertion failures.</param>
    private static void AssertFramesMatch(Color[] left, Color[] right, string scenario)
    {
        for (int tileIndex = 0; tileIndex < Penrose.Total; tileIndex++)
        {
            Assert.That(
                ColorsMatch(left[tileIndex], right[tileIndex]),
                Is.True,
                $"{scenario} at Tile {tileIndex}");
        }
    }

    /// <summary>Asserts two production frames match throughout the complementary background partition.</summary>
    /// <param name="left">First rendered frame.</param>
    /// <param name="right">Second rendered frame.</param>
    /// <param name="foregroundMembership">Circle/Arc membership that identifies foreground Tiles.</param>
    /// <param name="scenario">Scenario included in assertion failures.</param>
    private static void AssertBackgroundPartitionsMatch(
        Color[] left,
        Color[] right,
        bool[] foregroundMembership,
        string scenario)
    {
        for (int tileIndex = 0; tileIndex < Penrose.Total; tileIndex++)
        {
            if (foregroundMembership[tileIndex])
            {
                continue;
            }

            Assert.That(
                ColorsMatch(left[tileIndex], right[tileIndex]),
                Is.True,
                $"{scenario} at background Tile {tileIndex}");
        }
    }

    /// <summary>
    /// Asserts the active-Drop background keeps the authored smooth hue field between nearby Tiles.
    /// </summary>
    /// <param name="frame">Active-Drop frame rendered at a Waveform crest.</param>
    /// <param name="foregroundMembership">Exact Circle/Arc membership by Tile.</param>
    /// <param name="sourceHueStep">Authored source-hue step between consecutive Tile indexes.</param>
    private static void AssertContinuousBackgroundHueField(
        Color[] frame,
        bool[] foregroundMembership,
        float sourceHueStep)
    {
        int comparisons = 0;
        int previousTileIndex = -1;
        float previousHue = 0f;
        for (int tileIndex = 0; tileIndex < Penrose.Total; tileIndex++)
        {
            if (foregroundMembership[tileIndex])
            {
                continue;
            }

            Color.RGBToHSV(frame[tileIndex], out float hue, out _, out _);
            if (previousTileIndex >= 0 && (tileIndex - previousTileIndex) * sourceHueStep <= 0.003f)
            {
                float hueDistance = Mathf.Abs(Mathf.DeltaAngle(previousHue * 360f, hue * 360f)) / 360f;
                Assert.That(
                    hueDistance,
                    Is.LessThan(0.02f),
                    $"adjacent background source hues at Tiles {previousTileIndex} and {tileIndex}");
                comparisons++;
            }

            previousTileIndex = tileIndex;
            previousHue = hue;
        }

        Assert.That(comparisons, Is.GreaterThan(100), "the continuity check must exercise the moving field");
    }

    /// <summary>Measures rendered RGB brightness with human sensitivity weighted toward green.</summary>
    /// <param name="color">Rendered color to measure.</param>
    /// <returns>The Rec. 709 weighted sum of the color's RGB channels.</returns>
    private static float HumanWeightedRgbBrightness(Color color)
    {
        return (0.2126f * color.r) + (0.7152f * color.g) + (0.0722f * color.b);
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
