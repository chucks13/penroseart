using NUnit.Framework;

public sealed class TransitionRepertoireTests
{
    [Test]
    public void DefaultCompletesOnAFourBeatImpact()
    {
        var repertoire = TransitionRepertoire.Default;

        Assert.That(repertoire.Tags, Is.EqualTo(Repertoire.None));
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
            Repertoire.HandlesDrop,
            runwayBeats: 4,
            tailBeats: 4,
            TransitionShape.Noise,
            TransitionIntensity.High,
            defaultDurationSeconds: 4f);

        Assert.That(repertoire.Tags, Is.EqualTo(Repertoire.HandlesDrop));
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
    public void ConcreteTransitionsAdvertiseTweakableDefaultRepertoires()
    {
        AssertRepertoire(new Fade().Repertoire, Repertoire.None, 4, 0, TransitionShape.Blend, TransitionIntensity.Subtle,
            defaultDurationSeconds: 4f);
        AssertRepertoire(new RGBFade().Repertoire, Repertoire.RespondsToEnergy, 4, 4, TransitionShape.ChannelBlend, TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
        AssertRepertoire(new DirectionalWipe().Repertoire, Repertoire.RespondsToEnergy, 4, 4, TransitionShape.DirectionalWipe, TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
        AssertRepertoire(new IndexWipe().Repertoire, Repertoire.None, 4, 0, TransitionShape.IndexWipe, TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
        AssertRepertoire(new FizzleTransition().Repertoire, Repertoire.HandlesDrop, 4, 4, TransitionShape.Dissolve, TransitionIntensity.High,
            defaultDurationSeconds: 4f);
        AssertRepertoire(new IrisTransition().Repertoire, Repertoire.HandlesDrop, 4, 4, TransitionShape.Iris, TransitionIntensity.High,
            defaultDurationSeconds: 4f);
        AssertRepertoire(new NoiseTransition().Repertoire, Repertoire.HandlesDrop | Repertoire.RespondsToEnergy, 4, 4, TransitionShape.Noise, TransitionIntensity.High,
            defaultDurationSeconds: 4f);
    }

    private static void AssertRepertoire(
        TransitionRepertoire actual,
        Repertoire tags,
        int runwayBeats,
        int tailBeats,
        TransitionShape shape,
        TransitionIntensity intensity,
        float defaultDurationSeconds)
    {
        Assert.That(actual.Tags, Is.EqualTo(tags));
        Assert.That(actual.RunwayBeats, Is.EqualTo(runwayBeats));
        Assert.That(actual.TailBeats, Is.EqualTo(tailBeats));
        Assert.That(actual.Shape, Is.EqualTo(shape));
        Assert.That(actual.Intensity, Is.EqualTo(intensity));
        Assert.That(actual.DefaultDurationSeconds, Is.EqualTo(defaultDurationSeconds));
    }
}
