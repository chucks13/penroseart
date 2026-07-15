// Contract tests for BeatManager Phrase, Drop, Fill, Energy, Loop, and Grid values.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>Tests structural wire groups and their direct progress/build/decay conveniences.</summary>
public sealed class BeatManagerStructureTests
{
    /// <summary>Verifies current and next Phrase facts stay in their explicitly separate wire groups.</summary>
    [Test]
    public void PhraseAndNextPhraseRemainSeparateWireLanes()
    {
        var beatManager = LiveManager(snapshot =>
        {
            snapshot.phraseState = new PhraseState { label = "Intro", countBeats = 24, lengthBeats = 32, irregular = 0 };
            snapshot.nextPhraseState = new LabeledCountdown { label = "Drop", countBeats = 24, lengthBeats = 32 };
        });

        Assert.That(beatManager.Phrase.Name, Is.EqualTo("Intro"));
        Assert.That(beatManager.Phrase.BeatsRemaining, Is.EqualTo(24));
        Assert.That(beatManager.Phrase.LengthBeats, Is.EqualTo(32));
        Assert.That(beatManager.NextPhrase.Name, Is.EqualTo("Drop"));
        Assert.That(beatManager.NextPhrase.BeatsUntil, Is.EqualTo(24));
    }

    /// <summary>Verifies an active Drop preserves its raw wire count and readable remaining-beat value.</summary>
    [Test]
    public void DropKeepsRawCountAndAddsReadableActiveInterpretation()
    {
        var beatManager = LiveManager(snapshot =>
            snapshot.dropState = new CountdownState { active = 1, countBeats = 20, lengthBeats = 32, remaining = 2 });

        Assert.That(beatManager.Drop.Active, Is.True);
        Assert.That(beatManager.Drop.BeatsRemaining, Is.EqualTo(20));
        Assert.That(beatManager.Drop.BeatsUntil, Is.Null);
        Assert.That(beatManager.Drop.Remaining, Is.EqualTo(2));
        Assert.That(beatManager.Drop.Progress, Is.Not.Null);
    }

    /// <summary>Verifies an upcoming Fill preserves its raw wire count and readable beats-until value.</summary>
    [Test]
    public void FillKeepsRawCountAndAddsReadableUpcomingInterpretation()
    {
        var beatManager = LiveManager(snapshot =>
            snapshot.fillState = new CountdownState { active = 0, countBeats = 7, lengthBeats = 4, remaining = 3 });

        Assert.That(beatManager.Fill.Active, Is.False);
        Assert.That(beatManager.Fill.BeatsRemaining, Is.Null);
        Assert.That(beatManager.Fill.BeatsUntil, Is.EqualTo(7));
        Assert.That(beatManager.Fill.LengthBeats, Is.EqualTo(4));
    }

    /// <summary>Verifies stock envelopes support both the full event length and a shorter caller window.</summary>
    [Test]
    public void BuildAndDecayCanUseTheFullDurationOrAShorterWindow()
    {
        var beatManager = LiveManager(snapshot =>
            snapshot.dropState = new CountdownState { active = 1, countBeats = 16, lengthBeats = 32, remaining = 1 });

        Assert.That(beatManager.Drop.Build(), Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(beatManager.Drop.Decay(), Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(beatManager.Drop.Build(16), Is.EqualTo(1f).Within(0.01f));
        Assert.That(beatManager.Drop.Decay(16), Is.EqualTo(0f).Within(0.01f));
    }

    /// <summary>Verifies current Energy derives its Trend while the explicit next lane remains separate.</summary>
    [Test]
    public void EnergyAndNextEnergyExposeCurrentTrendAndUpcomingState()
    {
        var beatManager = LiveManager(snapshot =>
        {
            snapshot.energyState = new LabeledCountdown { label = "Low", countBeats = 48, lengthBeats = 64 };
            snapshot.nextEnergyState = new LabeledCountdown { label = "High", countBeats = 48, lengthBeats = 64 };
        });

        Assert.That(beatManager.Energy.Level, Is.EqualTo(Energy.Low));
        Assert.That(beatManager.Energy.Trend, Is.EqualTo(EnergyTrend.Rising));
        Assert.That(beatManager.NextEnergy.Level, Is.EqualTo(Energy.High));
        Assert.That(beatManager.NextEnergy.BeatsUntil, Is.EqualTo(48));
    }

    /// <summary>Verifies Loop exposes both raw size fields and their derived nominal beat length.</summary>
    [Test]
    public void LoopExposesItsRawSizeFractionAndDerivedNominalBeats()
    {
        var beatManager = LiveManager(snapshot => snapshot.loopState = new LoopState
        {
            active = 1, set = 1, lengthBeats = 0.5f, lengthMs = 250, sizeNumerator = 1, sizeDenominator = 2,
        });

        Assert.That(beatManager.Loop.SizeNumerator, Is.EqualTo(1));
        Assert.That(beatManager.Loop.SizeDenominator, Is.EqualTo(2));
        Assert.That(beatManager.Loop.NominalSizeBeats, Is.EqualTo(0.5f));
    }

    /// <summary>Verifies Grid exposes durable placement without manufacturing a hub-owned wrap event.</summary>
    [Test]
    public void GridExposesStateAndPositionWithoutManufacturingWrapEvents()
    {
        var beatManager = LiveManager(snapshot =>
            snapshot.timingGrid = new TimingGrid { state = "locked", beat = 5, bar = 2 });

        Assert.That(beatManager.Grid.State, Is.EqualTo(GridState.Locked));
        Assert.That(beatManager.Grid.Beat, Is.EqualTo(5));
        Assert.That(beatManager.Grid.Bar, Is.EqualTo(2));
        Assert.That(beatManager.Grid.Progress, Is.Not.Null);
    }

    /// <summary>Captures one live frame after applying a focused mutation to a deterministic snapshot.</summary>
    private static BeatManager LiveManager(System.Action<RaveOnAirSnapshot> mutate)
    {
        var snapshot = BeatClockFixture.CreateSnapshot(120f, 0f);
        mutate(snapshot);
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        return beatManager;
    }
}
