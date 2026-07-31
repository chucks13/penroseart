using NUnit.Framework;

public sealed class TransitionRepertoireTests
{
    [Test]
    public void DefaultCompletesOnAFourBeatImpact()
    {
        var repertoire = TransitionRepertoire.Default;

        Assert.That(repertoire.Shape, Is.EqualTo(TransitionShape.Blend));
        Assert.That(repertoire.Intensity, Is.EqualTo(TransitionIntensity.Subtle));
        Assert.That(repertoire.DefaultDurationSeconds, Is.EqualTo(4f));
        Assert.That(repertoire.RunwayBeats, Is.EqualTo(4));
        Assert.That(repertoire.TailBeats, Is.EqualTo(0));
        Assert.That(repertoire.DurationBeats, Is.EqualTo(4));
        Assert.That(repertoire.ImpactPoint, Is.EqualTo(1f));
        Assert.That(repertoire.HasTail, Is.False);
    }

    [Test]
    public void FromRunwayAndTailComputesDurationAndImpactPoint()
    {
        var repertoire = TransitionRepertoire.FromRunwayAndTail(
            runwayBeats: 4,
            tailBeats: 4,
            TransitionShape.Noise,
            TransitionIntensity.High,
            defaultDurationSeconds: 4f);

        Assert.That(repertoire.Shape, Is.EqualTo(TransitionShape.Noise));
        Assert.That(repertoire.Intensity, Is.EqualTo(TransitionIntensity.High));
        Assert.That(repertoire.DefaultDurationSeconds, Is.EqualTo(4f));
        Assert.That(repertoire.RunwayBeats, Is.EqualTo(4));
        Assert.That(repertoire.TailBeats, Is.EqualTo(4));
        Assert.That(repertoire.DurationBeats, Is.EqualTo(8));
        Assert.That(repertoire.ImpactPoint, Is.EqualTo(0.5f));
        Assert.That(repertoire.HasTail, Is.True);
    }

    [Test]
    public void FromRunwayAndTailAcceptsHardCut()
    {
        var repertoire = TransitionRepertoire.FromRunwayAndTail(
            runwayBeats: 0,
            tailBeats: 0,
            TransitionShape.Blend,
            TransitionIntensity.High,
            defaultDurationSeconds: 0f);

        Assert.That(repertoire.RunwayBeats, Is.EqualTo(0));
        Assert.That(repertoire.TailBeats, Is.EqualTo(0));
        Assert.That(repertoire.DurationBeats, Is.EqualTo(0));
        Assert.That(repertoire.ImpactPoint, Is.EqualTo(1f));
        Assert.That(repertoire.HasTail, Is.False);
    }

    [Test]
    public void FromRunwayAndTailRejectsDurationAboveTwelveBeats()
    {
        Assert.That(
            () => TransitionRepertoire.FromRunwayAndTail(
                runwayBeats: 10,
                tailBeats: 3,
                TransitionShape.Blend,
                TransitionIntensity.Subtle,
                defaultDurationSeconds: 4f),
            Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }


    [Test]
    public void FromRunwayAndTailRejectsNegativeRunwayOrTail()
    {
        Assert.That(
            () => TransitionRepertoire.FromRunwayAndTail(
                runwayBeats: -1,
                tailBeats: 0,
                TransitionShape.Blend,
                TransitionIntensity.Subtle,
                defaultDurationSeconds: 4f),
            Throws.TypeOf<System.ArgumentOutOfRangeException>());
        Assert.That(
            () => TransitionRepertoire.FromRunwayAndTail(
                runwayBeats: 0,
                tailBeats: -1,
                TransitionShape.Blend,
                TransitionIntensity.Subtle,
                defaultDurationSeconds: 4f),
            Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConcreteTransitionsExposeValidTweakableRepertoires()
    {
        var factory = new Factory<TransitionBase>();
        foreach (var transitionType in factory.Types)
        {
            var transition = (TransitionBase)System.Activator.CreateInstance(transitionType);
            AssertValidRepertoire(transition.Repertoire, transitionType.Name);
        }
    }

    private static void AssertValidRepertoire(TransitionRepertoire actual, string context)
    {
        Assert.That(actual.RunwayBeats, Is.GreaterThanOrEqualTo(0), context);
        Assert.That(actual.TailBeats, Is.GreaterThanOrEqualTo(0), context);
        Assert.That(actual.DurationBeats, Is.EqualTo(actual.RunwayBeats + actual.TailBeats), context);
        Assert.That(actual.DurationBeats, Is.LessThanOrEqualTo(TransitionRepertoire.MaxDurationBeats), context);
        Assert.That(actual.ImpactPoint, Is.EqualTo(actual.DurationBeats == 0 ? 1f : actual.RunwayBeats / (float)actual.DurationBeats).Within(0.0001f), context);
        Assert.That(actual.HasTail, Is.EqualTo(actual.TailBeats > 0), context);
        Assert.That(actual.DefaultDurationSeconds, Is.GreaterThanOrEqualTo(0f), context);
        Assert.That(System.Enum.IsDefined(typeof(TransitionShape), actual.Shape), Is.True, context);
        Assert.That(System.Enum.IsDefined(typeof(TransitionIntensity), actual.Intensity), Is.True, context);
    }
}

public sealed class EffectRepertoireTests
{
    [Test]
    public void ConcreteEffectsExposeOnlyKnownRepertoireFlags()
    {
        const Repertoire known = Repertoire.HandlesFill | Repertoire.HandlesDrop
            | Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;
        var factory = new Factory<EffectBase>();
        foreach (var effectType in factory.Types)
        {
            var effect = (EffectBase)System.Activator.CreateInstance(effectType);
            Assert.That(
                effect.Repertoire & ~known,
                Is.EqualTo(Repertoire.None),
                effectType.Name);
        }
    }

    [Test]
    public void ConcreteEffectsExposeInitialEnergyAffinityPerformersAtEachLevel()
    {
        var factory = new Factory<EffectBase>();
        var declared = Repertoire.None;

        foreach (var effectType in factory.Types)
        {
            var effect = (EffectBase)System.Activator.CreateInstance(effectType);
            declared |= effect.Repertoire;
        }

        Assert.That(declared & Repertoire.EnergyLow, Is.Not.EqualTo(Repertoire.None), "At least one Performer should advertise a Low-energy affinity.");
        Assert.That(declared & Repertoire.EnergyMid, Is.Not.EqualTo(Repertoire.None), "At least one Performer should advertise a Mid-energy affinity.");
        Assert.That(declared & Repertoire.EnergyHigh, Is.Not.EqualTo(Repertoire.None), "At least one Performer should advertise a High-energy affinity.");
    }

    [Test]
    public void ConcreteEffectsExposeInitialFillAndDropCapablePerformers()
    {
        var factory = new Factory<EffectBase>();
        var hasFillCapableEffect = false;
        var hasDropCapableEffect = false;

        foreach (var effectType in factory.Types)
        {
            var effect = (EffectBase)System.Activator.CreateInstance(effectType);
            hasFillCapableEffect |= (effect.Repertoire & Repertoire.HandlesFill) != 0;
            hasDropCapableEffect |= (effect.Repertoire & Repertoire.HandlesDrop) != 0;
        }

        Assert.That(hasFillCapableEffect, Is.True);
        Assert.That(hasDropCapableEffect, Is.True);
    }
}
