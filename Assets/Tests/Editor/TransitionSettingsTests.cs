using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RepertoireFlags = Repertoire;

public sealed class TransitionSettingsTests
{
    private const string TempAssetFolder = "Assets/Tests/Editor/TempTransitionSettings";
    private const string TempResourcesRoot = "Assets/Tests/Editor/TempTransitionSettingsResources";
    private const string TempResourcesFolder = TempResourcesRoot + "/Resources/TransitionSettings";

    [SetUp]
    public void SetUp()
    {
        CleanupTempAssets();
    }

    [TearDown]
    public void TearDown()
    {
        CleanupTempAssets();
    }

    [Test]
    public void CodeDefaultsExposeValidAuthoringDefaults()
    {
        var factory = new Factory<TransitionBase>();
        foreach (var transitionType in factory.Types)
        {
            var transition = (TransitionBase)System.Activator.CreateInstance(transitionType);
            AssertValidSettings(transition.CodeDefaults, transitionType.Name);
        }
    }

    [Test]
    public void EnsureCatalogAssetsCreatesOneSettingsAssetPerRuntimeTransition()
    {
        var assets = TransitionSettingsAssetUtility.EnsureCatalogAssets(TempAssetFolder);
        var factory = new Factory<TransitionBase>();

        Assert.That(assets.Count, Is.EqualTo(factory.Count));
        foreach (var transitionType in factory.Types)
        {
            var assetPath = TransitionSettingsAssetUtility.AssetPathFor(transitionType, TempAssetFolder);
            var asset = AssetDatabase.LoadAssetAtPath<TransitionSettingsAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, transitionType.Name);
            Assert.That(asset.TransitionTypeName, Is.EqualTo(transitionType.FullName));
        }
    }

    [Test]
    public void EnsureAssetReturnsExistingAssetWithoutConstrainingDuration()
    {
        var defaults = new TestSettingsTransition().CodeDefaults;
        var asset = TransitionSettingsAssetUtility.EnsureAsset(typeof(TestSettingsTransition), defaults, TempAssetFolder);
        asset.Settings.RunwayBeats = 15;
        asset.Settings.TailBeats = 3;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var existing = TransitionSettingsAssetUtility.EnsureAsset(typeof(TestSettingsTransition), defaults, TempAssetFolder);

        Assert.That(existing.Settings.RunwayBeats, Is.EqualTo(15));
        Assert.That(existing.Settings.TailBeats, Is.EqualTo(3));
    }

    [Test]
    public void RestoreDefaultsCopiesCompleteCodeDefaultsBackIntoSettingsAsset()
    {
        var defaults = new DirectionalWipe().CodeDefaults;
        var asset = TransitionSettingsAssetUtility.EnsureAsset(typeof(DirectionalWipe), defaults, TempAssetFolder);
        asset.Settings.DefaultDurationSeconds = 12f;
        asset.Settings.ExternalBlendDefaultProgress = 0.25f;
        asset.Settings.DirectionalReactiveEdgeWidth = 0.25f;
        asset.Settings.DirectionalLowBandResponseGain = 9f;

        TransitionSettingsAssetUtility.RestoreDefaults(typeof(DirectionalWipe), defaults, TempAssetFolder);

        Assert.That(asset.Settings.DefaultDurationSeconds, Is.EqualTo(defaults.DefaultDurationSeconds));
        Assert.That(asset.Settings.ExternalBlendDefaultProgress, Is.EqualTo(defaults.ExternalBlendDefaultProgress));
        Assert.That(asset.Settings.DirectionalReactiveEdgeWidth, Is.EqualTo(defaults.DirectionalReactiveEdgeWidth));
        Assert.That(asset.Settings.DirectionalLowBandResponseGain, Is.EqualTo(defaults.DirectionalLowBandResponseGain));
    }

    [Test]
    public void RepertoireReadsSavedResourcesSettingsWhenAssetExists()
    {
        var defaults = new TestSettingsTransition().CodeDefaults;
        var asset = TransitionSettingsAssetUtility.EnsureAsset(typeof(TestSettingsTransition), defaults, TempResourcesFolder);
        asset.Settings.DefaultDurationSeconds = 9f;
        asset.Settings.RunwayBeats = 3;
        asset.Settings.TailBeats = 2;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var repertoire = new TestSettingsTransition().Repertoire;

        Assert.That(repertoire.DefaultDurationSeconds, Is.EqualTo(9f));
        Assert.That(repertoire.RunwayBeats, Is.EqualTo(3));
        Assert.That(repertoire.TailBeats, Is.EqualTo(2));
    }

    [Test]
    public void ToRepertoireAcceptsRunwayAndTailAtTwelveBeats()
    {
        var settings = new TransitionSettings
        {
            RunwayBeats = 10,
            TailBeats = 2,
        };

        var repertoire = settings.ToRepertoire();

        Assert.That(repertoire.RunwayBeats, Is.EqualTo(10));
        Assert.That(repertoire.TailBeats, Is.EqualTo(2));
        Assert.That(repertoire.DurationBeats, Is.EqualTo(12));
    }

    [Test]
    public void ToRepertoireAcceptsZeroRunwayAndZeroTailHardCut()
    {
        var settings = new TransitionSettings
        {
            RunwayBeats = 0,
            TailBeats = 0,
            DefaultDurationSeconds = 0f,
        };

        var repertoire = settings.ToRepertoire();

        Assert.That(repertoire.RunwayBeats, Is.EqualTo(0));
        Assert.That(repertoire.TailBeats, Is.EqualTo(0));
        Assert.That(repertoire.DurationBeats, Is.EqualTo(0));
        Assert.That(repertoire.DefaultDurationSeconds, Is.EqualTo(0f));
    }

    [Test]
    public void ToRepertoireRejectsRunwayAndTailAboveTwelveBeats()
    {
        var settings = new TransitionSettings
        {
            RunwayBeats = 10,
            TailBeats = 3,
        };

        Assert.That(() => settings.ToRepertoire(), Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }

    [Test]
    public void ToRepertoireRejectsNegativeRunwayOrTail()
    {
        var negativeRunway = new TransitionSettings
        {
            RunwayBeats = -1,
            TailBeats = 0,
        };
        var negativeTail = new TransitionSettings
        {
            RunwayBeats = 0,
            TailBeats = -1,
        };

        Assert.That(() => negativeRunway.ToRepertoire(), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        Assert.That(() => negativeTail.ToRepertoire(), Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }

    [Test]
    public void EditorConstrainDurationClampsNegativeRunwayAndTailToZero()
    {
        var settings = new TransitionSettings
        {
            RunwayBeats = -1,
            TailBeats = -2,
        };

        var changed = TransitionSettingsAssetUtility.ConstrainDuration(settings);

        Assert.That(changed, Is.True);
        Assert.That(settings.RunwayBeats, Is.EqualTo(0));
        Assert.That(settings.TailBeats, Is.EqualTo(0));
        Assert.That(settings.DurationBeats, Is.EqualTo(0));
    }

    [Test]
    public void EditorConstrainDurationReducesTailToKeepRunwayPlusTailAtTwelveBeats()
    {
        var settings = new TransitionSettings
        {
            RunwayBeats = 10,
            TailBeats = 5,
        };

        var changed = TransitionSettingsAssetUtility.ConstrainDuration(settings);

        Assert.That(changed, Is.True);
        Assert.That(settings.RunwayBeats, Is.EqualTo(10));
        Assert.That(settings.TailBeats, Is.EqualTo(2));
        Assert.That(settings.DurationBeats, Is.EqualTo(12));
    }

    [Test]
    public void EditorConstrainDurationCapsRunwayWhenRunwayAloneExceedsTwelveBeats()
    {
        var settings = new TransitionSettings
        {
            RunwayBeats = 15,
            TailBeats = 3,
        };

        var changed = TransitionSettingsAssetUtility.ConstrainDuration(settings);

        Assert.That(changed, Is.True);
        Assert.That(settings.RunwayBeats, Is.EqualTo(12));
        Assert.That(settings.TailBeats, Is.EqualTo(0));
        Assert.That(settings.DurationBeats, Is.EqualTo(12));
    }

    private static void AssertValidSettings(TransitionSettings actual, string context)
    {
        Assert.That(actual.HasValidDuration, Is.True, context);
        Assert.That(actual.ExternalBlendDefaultProgress, Is.InRange(0f, 1f), context);
        var repertoire = actual.ToRepertoire();
        Assert.That(repertoire.RunwayBeats, Is.GreaterThanOrEqualTo(0), context);
        Assert.That(repertoire.TailBeats, Is.GreaterThanOrEqualTo(0), context);
        Assert.That(repertoire.DurationBeats, Is.LessThanOrEqualTo(TransitionRepertoire.MaxDurationBeats), context);
        Assert.That(repertoire.DefaultDurationSeconds, Is.GreaterThanOrEqualTo(0f), context);
        Assert.That(System.Enum.IsDefined(typeof(TransitionShape), repertoire.Shape), Is.True, context);
        Assert.That(System.Enum.IsDefined(typeof(TransitionIntensity), repertoire.Intensity), Is.True, context);
    }

    private static void CleanupTempAssets()
    {
        if (AssetDatabase.IsValidFolder(TempAssetFolder))
        {
            AssetDatabase.DeleteAsset(TempAssetFolder);
        }

        if (AssetDatabase.IsValidFolder(TempResourcesRoot))
        {
            AssetDatabase.DeleteAsset(TempResourcesRoot);
        }

        AssetDatabase.Refresh();
    }

    private sealed class TestSettingsTransition : TransitionBase
    {
        protected override TransitionSettings BuildCodeDefaults()
        {
            return new TransitionSettings
            {
                Tags = RepertoireFlags.HandlesDrop,
                RunwayBeats = 1,
                TailBeats = 0,
                Shape = TransitionShape.Blend,
                Intensity = TransitionIntensity.Subtle,
                DefaultDurationSeconds = 1f,
            };
        }

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
        }
    }
}
