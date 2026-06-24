#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Pins the contrived rhythm query layer from ADR-0002: nullable queries on BeatManager where null
/// always means "not available right now", tri-state mapping (-1 → null, 0 → upcoming, 1 → in
/// progress), beat-smoothed phrase progress, Levels smoothing, and the Color Bank forms.
/// </summary>
public sealed class BeatManagerContrivedQueriesTests
{
    /// <summary>
    /// A BeatManager pinned to the live source so Update never overwrites the beat data the test
    /// writes. The clock matches the BarPhase fixture pinned by the integration tests:
    /// beat 1 of 4, 250 ms to the next of 500 ms average → BarPhase 0.125, intra-beat fraction 0.5.
    /// </summary>
    private static BeatManager CreateLiveBeatManager()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.snapshot.bpm = 128f; // positive BPM = usable beat clock (IsActive derives from this)
        beatManager.beatData.snapshot.beatInBar = 1;
        beatManager.beatData.snapshot.beatAverageMs = 500;
        beatManager.beatData.snapshot.beatsCountMs = new[] { 0, 250, 750, 1250 };
        return beatManager;
    }

    // --- Envelope ---

    [Test]
    public void EnvelopeIsNullWithoutBeatClock()
    {
        var beatManager = new BeatManager(); // fresh transport carries no BPM, so no beat clock

        Assert.That(beatManager.Envelope(0), Is.Null);
    }

    [Test]
    public void EnvelopeEvaluatesVariantWaveformAtBarPhase()
    {
        var beatManager = CreateLiveBeatManager();

        // Same fixture as GetBeatBrightnessUsesBarPhaseWaveformsForMusicalVariants: at BarPhase 0.125
        // the Beat Pulse trough reads 0 and the offbeat variant peak reads 1.
        Assert.That(beatManager.BarPhase, Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(beatManager.Envelope(0), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(beatManager.Envelope(5), Is.EqualTo(1f).Within(0.0001f));
    }

    // --- Fill / Drop phrase events ---

    [Test]
    public void FillIsNullWhenTriStateIsUnavailable()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.fillState = CountdownState.Unavailable;

        Assert.That(beatManager.Fill, Is.Null);
    }

    [Test]
    public void FillIsNullOnDefaultBeatData()
    {
        var beatManager = new BeatManager();

        Assert.That(beatManager.Fill, Is.Null);
        Assert.That(beatManager.Drop, Is.Null);
        Assert.That(beatManager.Energy, Is.Null);
        Assert.That(beatManager.Phrase, Is.Null);
        Assert.That(beatManager.Levels, Is.Null);
    }

    [Test]
    public void FillCountsDownWhileUpcoming()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.fillState = new CountdownState { active = 0, countBeats = 16, lengthBeats = 8, remaining = 1 };

        var fill = beatManager.Fill;

        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.inProgress, Is.False);
        Assert.That(fill.Value.beatsUntilStart, Is.EqualTo(16));
        Assert.That(fill.Value.msUntilStart, Is.EqualTo(8000)); // 16 beats × the 500 ms average interval
        Assert.That(fill.Value.progress, Is.Null);
        Assert.That(fill.Value.beatsUntilEnd, Is.Null);
        // (32 - (16 - 0.5 intra-beat)) / 32 of the anticipation window already elapsed.
        Assert.That(fill.Value.anticipation, Is.EqualTo(0.515625f).Within(0.0001f));
        Assert.That(fill.Value.lengthBeats, Is.EqualTo(8));
        Assert.That(fill.Value.remaining, Is.EqualTo(1));
    }

    [Test]
    public void FillReportsBeatSmoothedProgressWhileInProgress()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.fillState = new CountdownState { active = 1, countBeats = 6, lengthBeats = 8, remaining = 1 };

        var fill = beatManager.Fill;

        // 2 of 8 beats elapsed plus the 0.5 intra-beat fraction from the shared beat clock.
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.inProgress, Is.True);
        Assert.That(fill.Value.beatsUntilStart, Is.Null);
        Assert.That(fill.Value.msUntilStart, Is.Null);
        Assert.That(fill.Value.anticipation, Is.Null);
        Assert.That(fill.Value.beatsUntilEnd, Is.EqualTo(6));
        Assert.That(fill.Value.progress, Is.EqualTo(0.3125f).Within(0.0001f));
    }

    [Test]
    public void FillMapsWireUnknownsToNullFieldsInsideAValidState()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.fillState = new CountdownState { active = 0, countBeats = -1, lengthBeats = -1, remaining = -1 };

        var fill = beatManager.Fill;

        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.beatsUntilStart, Is.Null);
        Assert.That(fill.Value.msUntilStart, Is.Null);
        Assert.That(fill.Value.anticipation, Is.Null);
        Assert.That(fill.Value.lengthBeats, Is.Null);
        Assert.That(fill.Value.remaining, Is.Null);
    }

    [Test]
    public void FillWithNoMoreOccurrencesIsValuesNotAThirdState()
    {
        var beatManager = CreateLiveBeatManager();
        // The wire shape when the track HAS fills but they are all behind the playhead:
        // still available (active 0), no known next start, zero occurrences left.
        beatManager.beatData.snapshot.fillState = new CountdownState { active = 0, countBeats = -1, lengthBeats = -1, remaining = 0 };

        var fill = beatManager.Fill;

        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.inProgress, Is.False);
        Assert.That(fill.Value.beatsUntilStart, Is.Null);
        Assert.That(fill.Value.anticipation, Is.Null);
        Assert.That(fill.Value.remaining, Is.EqualTo(0)); // zero is a valid number, not a state
    }

    [Test]
    public void AnticipationIsNullOutsideTheWindowAndFullAtTheStart()
    {
        var beatManager = CreateLiveBeatManager();

        beatManager.beatData.snapshot.dropState = new CountdownState { active = 0, countBeats = BeatManager.AnticipationWindowBeats + 1, lengthBeats = 16, remaining = 1 };
        Assert.That(beatManager.Drop!.Value.anticipation, Is.Null);

        beatManager.beatData.snapshot.dropState = new CountdownState { active = 0, countBeats = 0, lengthBeats = 16, remaining = 1 };
        Assert.That(beatManager.Drop!.Value.anticipation, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void MsUntilStartIsNullWithoutAUsableBeatInterval()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.beatAverageMs = -1;
        beatManager.beatData.snapshot.dropState = new CountdownState { active = 0, countBeats = 16, lengthBeats = 16, remaining = 1 };

        var drop = beatManager.Drop;

        Assert.That(drop, Is.Not.Null);
        Assert.That(drop!.Value.beatsUntilStart, Is.EqualTo(16));
        Assert.That(drop.Value.msUntilStart, Is.Null);
    }

    [Test]
    public void DropMirrorsFillCooking()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.dropState = new CountdownState { active = 1, countBeats = 6, lengthBeats = 8, remaining = 2 };

        var drop = beatManager.Drop;

        Assert.That(drop, Is.Not.Null);
        Assert.That(drop!.Value.inProgress, Is.True);
        Assert.That(drop.Value.beatsUntilStart, Is.Null);
        Assert.That(drop.Value.beatsUntilEnd, Is.EqualTo(6));
        Assert.That(drop.Value.progress, Is.EqualTo(0.3125f).Within(0.0001f));
        Assert.That(drop.Value.remaining, Is.EqualTo(2));
    }

    // --- Energy (closed vocabulary) ---

    [Test]
    public void EnergyParsesClosedVocabularyOnce()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.energyState = new NamedState { current = "High", next = "Mid", active = 1, countBeats = 4, lengthBeats = 16, remaining = 2 };

        var energy = beatManager.Energy;

        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.next, Is.EqualTo(EnergyLevel.Mid));
        Assert.That(energy.Value.beatsUntilChange, Is.EqualTo(4));
        Assert.That(energy.Value.normalized, Is.EqualTo(1f));
        Assert.That(energy.Value.direction, Is.EqualTo(-1));
        // (16 - 4 + 0.5 intra-beat) / 16 of the same-energy run already elapsed.
        Assert.That(energy.Value.runProgress, Is.EqualTo(0.78125f).Within(0.0001f));
        Assert.That(energy.Value.runLengthBeats, Is.EqualTo(16));
        Assert.That(energy.Value.changesRemaining, Is.EqualTo(2));
    }

    [Test]
    public void EnergyParsesLabelsCaseInsensitively()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.energyState = new NamedState { current = "low", next = "HIGH", active = 1, countBeats = 8, lengthBeats = 16, remaining = 1 };

        var energy = beatManager.Energy;

        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.Low));
        Assert.That(energy.Value.next, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.normalized, Is.EqualTo(0f));
        Assert.That(energy.Value.direction, Is.EqualTo(1));
    }

    [Test]
    public void EnergyDegradesToNullOnUnrecognizedLabelNeverToAWrongTier()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.energyState = new NamedState { current = "Banana", next = "Mid", active = 1, countBeats = 4, lengthBeats = 16, remaining = 2 };

        Assert.That(beatManager.Energy, Is.Null);
    }

    [Test]
    public void EnergyIsNullWhenTriStateIsUnavailable()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.energyState = new NamedState { current = "High", next = "Mid", active = -1, countBeats = -1, lengthBeats = -1, remaining = -1 };

        Assert.That(beatManager.Energy, Is.Null);
    }

    [Test]
    public void EnergyTreatsUnknownNextLabelAsSteadyDirection()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.energyState = new NamedState { current = "Mid", next = "", active = 1, countBeats = 4, lengthBeats = 16, remaining = 2 };

        var energy = beatManager.Energy;

        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.next, Is.Null);
        Assert.That(energy.Value.direction, Is.EqualTo(0));
    }

    // --- Track Phrase (open vocabulary) ---

    [Test]
    public void PhrasePassesOpenVocabularyLabelsThroughAndCooksStructure()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.phraseState = new NamedState { current = "Chorus 2", next = "Break", active = 1, countBeats = 12, lengthBeats = 32, remaining = 8 };

        var phrase = beatManager.Phrase;

        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.Value.label, Is.EqualTo("Chorus 2"));
        Assert.That(phrase.Value.next, Is.EqualTo("Break"));
        Assert.That(phrase.Value.inPhrase, Is.True);
        Assert.That(phrase.Value.beatsUntilNext, Is.EqualTo(12));
        Assert.That(phrase.Value.lengthBeats, Is.EqualTo(32));
        Assert.That(phrase.Value.remaining, Is.EqualTo(8));
        // (32 - 12) elapsed beats plus the 0.5 intra-beat fraction, over 32.
        Assert.That(phrase.Value.progress, Is.EqualTo(0.640625f).Within(0.0001f));
    }

    [Test]
    public void PhrasePreservesUpcomingTriStateForNextPhrasePlanning()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.phraseState = new NamedState { current = "Break", next = "Drop", active = 0, countBeats = 9, lengthBeats = 64, remaining = 7 };

        var phrase = beatManager.Phrase;

        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.Value.label, Is.EqualTo("Break"));
        Assert.That(phrase.Value.next, Is.EqualTo("Drop"));
        Assert.That(phrase.Value.inPhrase, Is.False);
        Assert.That(phrase.Value.beatsUntilNext, Is.EqualTo(9));
        Assert.That(phrase.Value.lengthBeats, Is.EqualTo(64));
        Assert.That(phrase.Value.remaining, Is.EqualTo(7));
        Assert.That(phrase.Value.progress, Is.Null);
    }

    [Test]
    public void PhraseIsNullWithoutALabelOrWhenUnavailable()
    {
        var beatManager = CreateLiveBeatManager();

        beatManager.beatData.snapshot.phraseState = new NamedState { current = "", next = "Break", active = 1, countBeats = 12, lengthBeats = 32, remaining = 8 };
        Assert.That(beatManager.Phrase, Is.Null);

        beatManager.beatData.snapshot.phraseState = new NamedState { current = "Drop", next = "Break", active = -1, countBeats = -1, lengthBeats = -1, remaining = -1 };
        Assert.That(beatManager.Phrase, Is.Null);
    }

    // --- Levels smoothing and the Color Bank ---

    [Test]
    public void LevelsAreNullBeforeAnyLiveSamples()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.Update(0f);

        Assert.That(beatManager.Levels, Is.Null);
        Assert.That(beatManager.LevelsRgb, Is.Null);
        Assert.That(beatManager.LevelsHue, Is.Null);
        Assert.That(beatManager.LevelsPalette, Is.Null);
    }

    [Test]
    public void LevelsSnapToTheFirstLiveSample()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.levels = new Levels { low = 0.2f, mid = 0.4f, high = 0.8f };

        beatManager.Update(0f);

        var levels = beatManager.Levels;
        Assert.That(levels, Is.Not.Null);
        Assert.That(levels!.Value.low, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(levels.Value.mid, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(levels.Value.high, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void LevelsRiseOnAttackAndFallOnRelease()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.levelsAttackSeconds = 0.1f;
        beatManager.levelsReleaseSeconds = 0.4f;
        beatManager.beatData.snapshot.levels = new Levels { low = 0.2f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0f);

        // Rising low band uses the attack time-constant.
        beatManager.beatData.snapshot.levels = new Levels { low = 1f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0.1f);
        var expectedAttack = 0.2f + ((1f - 0.2f) * (1f - Mathf.Exp(-0.1f / 0.1f)));
        Assert.That(beatManager.Levels!.Value.low, Is.EqualTo(expectedAttack).Within(0.0001f));

        // Falling low band uses the slower release time-constant.
        beatManager.beatData.snapshot.levels = new Levels { low = 0f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0.2f);
        var expectedRelease = expectedAttack + ((0f - expectedAttack) * (1f - Mathf.Exp(-0.1f / 0.4f)));
        Assert.That(beatManager.Levels!.Value.low, Is.EqualTo(expectedRelease).Within(0.0001f));
        Assert.That(beatManager.Levels!.Value.mid, Is.EqualTo(0.4f).Within(0.0001f));
    }

    [Test]
    public void LevelsResetInsteadOfReleasingFromStaleValuesAfterAGap()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.levels = new Levels { low = 0.9f, mid = 0.9f, high = 0.9f };
        beatManager.Update(0f);

        beatManager.beatData.snapshot.levels = Levels.Unavailable;
        beatManager.Update(0.1f);
        Assert.That(beatManager.Levels, Is.Null);

        // The next live sample snaps in fresh; nothing decays from the pre-gap 0.9.
        beatManager.beatData.snapshot.levels = new Levels { low = 0.1f, mid = 0.1f, high = 0.1f };
        beatManager.Update(0.2f);
        Assert.That(beatManager.Levels!.Value.low, Is.EqualTo(0.1f).Within(0.0001f));
    }

    [Test]
    public void LevelsRgbMapsBandsStraightOntoChannels()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.levels = new Levels { low = 0.2f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0f);

        var color = beatManager.LevelsRgb;

        Assert.That(color, Is.Not.Null);
        Assert.That(color!.Value.r, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(color.Value.g, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(color.Value.b, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(color.Value.a, Is.EqualTo(1f));
    }

    [Test]
    public void LevelsHueTracksTheSpectralCentroid()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.levels = new Levels { low = 0f, mid = 0f, high = 0.8f };
        beatManager.Update(0f);

        var color = beatManager.LevelsHue;
        var expected = Color.HSVToRGB(2f / 3f, 1f, 0.8f);

        Assert.That(color, Is.Not.Null);
        Assert.That(color!.Value.r, Is.EqualTo(expected.r).Within(0.0001f));
        Assert.That(color.Value.g, Is.EqualTo(expected.g).Within(0.0001f));
        Assert.That(color.Value.b, Is.EqualTo(expected.b).Within(0.0001f));
    }

    [Test]
    public void LevelsHueIsBlackAtSilence()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.levels = new Levels { low = 0f, mid = 0f, high = 0f };
        beatManager.Update(0f);

        Assert.That(beatManager.LevelsHue, Is.EqualTo((Color?)Color.black));
    }

    [Test]
    public void LevelsPaletteIsNullWithoutALiveController()
    {
        // The palette-mediated form needs the Controller-owned AnimPalette; in headless edit-mode
        // tests there must be no Controller, and the query must say "unavailable" rather than spawn one.
        Assume.That(Controller.HasInstance, Is.False, "These tests assume no live Controller in the scene.");

        var beatManager = CreateLiveBeatManager();
        beatManager.beatData.snapshot.levels = new Levels { low = 0.5f, mid = 0.5f, high = 0.5f };
        beatManager.Update(0f);

        Assert.That(beatManager.Levels, Is.Not.Null);
        Assert.That(beatManager.LevelsPalette, Is.Null);
    }

    // --- Source transitions ---

    [Test]
    public void SimulatorClearsPhraseAndLevelStateToUnavailable()
    {
        // Stale live values (or stale scene-serialized values) must not replay through the contrived
        // queries once the simulator owns the beat: the simulator is a clock, not a musical analysis.
        var beatManager = new BeatManager { simulatedBpm = 120f };
        beatManager.beatData.snapshot.fillState = new CountdownState { active = 1, countBeats = 2, lengthBeats = 8, remaining = 1 };
        beatManager.beatData.snapshot.dropState = new CountdownState { active = 0, countBeats = 16, lengthBeats = 32, remaining = 2 };
        beatManager.beatData.snapshot.phraseState = new NamedState { current = "Drop", next = "Break", active = 1, countBeats = 12, lengthBeats = 32, remaining = 8 };
        beatManager.beatData.snapshot.energyState = new NamedState { current = "High", next = "Mid", active = 1, countBeats = 4, lengthBeats = 16, remaining = 2 };
        beatManager.beatData.snapshot.levels = new Levels { low = 0.5f, mid = 0.5f, high = 0.5f };

        beatManager.Update(0f);

        Assert.That(beatManager.IsActive, Is.True, "the simulated beat clock itself stays active");
        Assert.That(beatManager.Envelope(0), Is.Not.Null);
        Assert.That(beatManager.Fill, Is.Null);
        Assert.That(beatManager.Drop, Is.Null);
        Assert.That(beatManager.Phrase, Is.Null);
        Assert.That(beatManager.Energy, Is.Null);
        Assert.That(beatManager.Levels, Is.Null);
    }

    [Test]
    public void NoBeatStateClearsEverythingIncludingTheEnvelope()
    {
        var beatManager = new BeatManager { simulatedBpm = 120f, simulatedBeatEnabled = false };
        beatManager.beatData.snapshot.fillState = new CountdownState { active = 1, countBeats = 2, lengthBeats = 8, remaining = 1 };

        beatManager.Update(0f);

        Assert.That(beatManager.IsActive, Is.False);
        Assert.That(beatManager.Envelope(0), Is.Null);
        Assert.That(beatManager.Fill, Is.Null);
    }
}
