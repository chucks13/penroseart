using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Covers Controller.EffectiveRepertoire, the live-tuning override surface: an override entry keyed by an
// effect's catalog Name replaces its code-declared affinity; everything else falls through to the declared
// value. Uses distinct effect types so Name-keyed overrides can be shown not to leak across effects.
public sealed class EffectiveRepertoireTests
{
    private GameObject controllerObject;
    private Controller controller;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("EffectiveRepertoireTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.effects = new EffectBase[] { new AlphaEffect(), new BetaEffect() };
        controller.effectRepertoireOverrides = new System.Collections.Generic.List<Controller.EffectRepertoireOverride>();
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void OverrideWithMatchingNameReplacesDeclaredRepertoire()
    {
        var overridden = Repertoire.HandlesDrop | Repertoire.EnergyHigh;
        controller.effectRepertoireOverrides.Add(new Controller.EffectRepertoireOverride
        {
            effectName = controller.effects[0].Name,
            flags = overridden,
        });

        Assert.That(controller.EffectiveRepertoire(0), Is.EqualTo(overridden));
    }

    [Test]
    public void UnknownEffectNameFallsThroughToDeclaredRepertoire()
    {
        controller.effectRepertoireOverrides.Add(new Controller.EffectRepertoireOverride
        {
            effectName = "NoSuchEffect",
            flags = Repertoire.HandlesDrop,
        });

        Assert.That(controller.EffectiveRepertoire(0), Is.EqualTo(controller.effects[0].Repertoire));
    }

    [Test]
    public void OverrideForOneEffectDoesNotAffectAnother()
    {
        controller.effectRepertoireOverrides.Add(new Controller.EffectRepertoireOverride
        {
            effectName = controller.effects[0].Name,
            flags = Repertoire.HandlesDrop,
        });

        Assert.That(controller.EffectiveRepertoire(1), Is.EqualTo(controller.effects[1].Repertoire),
            "An override keyed to effect 0's Name must not change effect 1's effective affinity.");
    }

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    private sealed class AlphaEffect : EffectBase
    {
        public AlphaEffect()
        {
            buffer = new Color[Penrose.Total];
        }

        public override Repertoire Repertoire => Repertoire.EnergyLow | Repertoire.EnergyMid;

        public override string DebugText() => string.Empty;

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

    private sealed class BetaEffect : EffectBase
    {
        public BetaEffect()
        {
            buffer = new Color[Penrose.Total];
        }

        public override Repertoire Repertoire => Repertoire.EnergyHigh;

        public override string DebugText() => string.Empty;

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
