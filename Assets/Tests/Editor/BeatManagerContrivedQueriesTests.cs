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
        beatManager.WireSnapshot.bpm = 128f; // positive BPM = usable beat clock (IsActive derives from this)
        beatManager.WireSnapshot.beatInBar = 1;
        beatManager.WireSnapshot.beatAverageMs = 500;
        beatManager.WireSnapshot.beatsCountMs = new[] { 0, 250, 750, 1250 };
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

    /// <summary>The wire's unavailable Fill tri-state (-1) reads as a null Fill, never an inactive one.</summary>
    [Test]
    public void FillIsNullWhenTriStateIsUnavailable()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.fillState = CountdownState.Unavailable;

        Assert.That(beatManager.FillQuery, Is.Null);
    }

    [Test]
    public void FillIsNullOnDefaultBeatData()
    {
        var beatManager = new BeatManager();

        Assert.That(beatManager.FillQuery, Is.Null);
        Assert.That(beatManager.DropQuery, Is.Null);
        Assert.That(beatManager.EnergyQuery, Is.Null);
        Assert.That(beatManager.PhraseQuery, Is.Null);
        Assert.That(beatManager.LevelsQuery, Is.Null);
    }

    /// <summary>An upcoming Fill serves its countdown side — beats/ms until start and the anticipation ramp — with the in-progress fields null.</summary>
    [Test]
    public void FillCountsDownWhileUpcoming()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.fillState = new CountdownState { active = 0, countBeats = 16, lengthBeats = 8, remaining = 1 };

        var fill = beatManager.FillQuery;

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

    /// <summary>A playing Fill serves beat-smoothed progress and beats-until-end, with the countdown fields null.</summary>
    [Test]
    public void FillReportsBeatSmoothedProgressWhileInProgress()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.fillState = new CountdownState { active = 1, countBeats = 6, lengthBeats = 8, remaining = 1 };

        var fill = beatManager.FillQuery;

        // 2 of 8 beats elapsed plus the 0.5 intra-beat fraction from the shared beat clock.
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.inProgress, Is.True);
        Assert.That(fill.Value.beatsUntilStart, Is.Null);
        Assert.That(fill.Value.msUntilStart, Is.Null);
        Assert.That(fill.Value.anticipation, Is.Null);
        Assert.That(fill.Value.beatsUntilEnd, Is.EqualTo(6));
        Assert.That(fill.Value.progress, Is.EqualTo(0.3125f).Within(0.0001f));
    }

    /// <summary>Inside a valid Fill state, each field's -1 wire unknown maps to null on its own.</summary>
    [Test]
    public void FillMapsWireUnknownsToNullFieldsInsideAValidState()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.fillState = new CountdownState { active = 0, countBeats = -1, lengthBeats = -1, remaining = -1 };

        var fill = beatManager.FillQuery;

        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.beatsUntilStart, Is.Null);
        Assert.That(fill.Value.msUntilStart, Is.Null);
        Assert.That(fill.Value.anticipation, Is.Null);
        Assert.That(fill.Value.lengthBeats, Is.Null);
        Assert.That(fill.Value.remaining, Is.Null);
    }

    /// <summary>A track whose fills are all behind the playhead reads remaining == 0 — a number, not an unavailable state.</summary>
    [Test]
    public void FillWithNoMoreOccurrencesIsValuesNotAThirdState()
    {
        var beatManager = CreateLiveBeatManager();
        // The wire shape when the track HAS fills but they are all behind the playhead:
        // still available (active 0), no known next start, zero occurrences left.
        beatManager.WireSnapshot.fillState = new CountdownState { active = 0, countBeats = -1, lengthBeats = -1, remaining = 0 };

        var fill = beatManager.FillQuery;

        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.inProgress, Is.False);
        Assert.That(fill.Value.beatsUntilStart, Is.Null);
        Assert.That(fill.Value.anticipation, Is.Null);
        Assert.That(fill.Value.remaining, Is.EqualTo(0)); // zero is a valid number, not a state
    }

    /// <summary>The anticipation ramp is null outside the 32-beat window and reads 1 on the start beat.</summary>
    [Test]
    public void AnticipationIsNullOutsideTheWindowAndFullAtTheStart()
    {
        var beatManager = CreateLiveBeatManager();

        beatManager.WireSnapshot.dropState = new CountdownState { active = 0, countBeats = BeatManager.AnticipationWindowBeats + 1, lengthBeats = 16, remaining = 1 };
        Assert.That(beatManager.DropQuery!.Value.anticipation, Is.Null);

        beatManager.WireSnapshot.dropState = new CountdownState { active = 0, countBeats = 0, lengthBeats = 16, remaining = 1 };
        Assert.That(beatManager.DropQuery!.Value.anticipation, Is.EqualTo(1f).Within(0.0001f));
    }

    /// <summary>msUntilStart is null when the average beat interval is unavailable, while beatsUntilStart still serves.</summary>
    [Test]
    public void MsUntilStartIsNullWithoutAUsableBeatInterval()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.beatAverageMs = -1;
        beatManager.WireSnapshot.dropState = new CountdownState { active = 0, countBeats = 16, lengthBeats = 16, remaining = 1 };

        var drop = beatManager.DropQuery;

        Assert.That(drop, Is.Not.Null);
        Assert.That(drop!.Value.beatsUntilStart, Is.EqualTo(16));
        Assert.That(drop.Value.msUntilStart, Is.Null);
    }

    /// <summary>Drop cooks through the same shared phrase-event shape as Fill (in-progress fields, progress, remaining).</summary>
    [Test]
    public void DropMirrorsFillCooking()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.dropState = new CountdownState { active = 1, countBeats = 6, lengthBeats = 8, remaining = 2 };

        var drop = beatManager.DropQuery;

        Assert.That(drop, Is.Not.Null);
        Assert.That(drop!.Value.inProgress, Is.True);
        Assert.That(drop.Value.beatsUntilStart, Is.Null);
        Assert.That(drop.Value.beatsUntilEnd, Is.EqualTo(6));
        Assert.That(drop.Value.progress, Is.EqualTo(0.3125f).Within(0.0001f));
        Assert.That(drop.Value.remaining, Is.EqualTo(2));
    }

    // --- Energy (closed vocabulary) ---

    /// <summary>A full energy wire state parses the closed vocabulary once and serves tier, direction, and run shape.</summary>
    [Test]
    public void EnergyParsesClosedVocabularyOnce()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "High", countBeats = 4, lengthBeats = 16 };
        beatManager.WireSnapshot.nextEnergyState = new LabeledCountdown { label = "Mid", countBeats = 4, lengthBeats = 8 };

        var energy = beatManager.EnergyQuery;

        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.next, Is.EqualTo(EnergyLevel.Mid));
        Assert.That(energy.Value.beatsUntilChange, Is.EqualTo(4));
        Assert.That(energy.Value.normalized, Is.EqualTo(1f));
        Assert.That(energy.Value.direction, Is.EqualTo(-1));
        // (16 - 4 + 0.5 intra-beat) / 16 of the same-energy run already elapsed.
        Assert.That(energy.Value.runProgress, Is.EqualTo(0.78125f).Within(0.0001f));
        Assert.That(energy.Value.runLengthBeats, Is.EqualTo(16));
        Assert.That(energy.Value.nextRunLengthBeats, Is.EqualTo(8));
    }

    /// <summary>Wire energy labels parse case-insensitively ("low", "HIGH").</summary>
    [Test]
    public void EnergyParsesLabelsCaseInsensitively()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "low", countBeats = 8, lengthBeats = 16 };
        beatManager.WireSnapshot.nextEnergyState = new LabeledCountdown { label = "HIGH", countBeats = 8, lengthBeats = 16 };

        var energy = beatManager.EnergyQuery;

        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.Low));
        Assert.That(energy.Value.next, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.normalized, Is.EqualTo(0f));
        Assert.That(energy.Value.direction, Is.EqualTo(1));
    }

    /// <summary>An unrecognized energy label reads as null Energy, never a wrong tier.</summary>
    [Test]
    public void EnergyDegradesToNullOnUnrecognizedLabelNeverToAWrongTier()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "Banana", countBeats = 4, lengthBeats = 16 };
        beatManager.WireSnapshot.nextEnergyState = new LabeledCountdown { label = "Mid", countBeats = 4, lengthBeats = 16 };

        Assert.That(beatManager.EnergyQuery, Is.Null);
    }

    /// <summary>An empty/unavailable energy label reads as null Energy — the label is the availability signal.</summary>
    [Test]
    public void EnergyIsNullWhenLabelIsUnavailable()
    {
        // Energy no longer has its own active tri-state on the wire; an empty/null label is the
        // unavailable signal now (the label fails the closed Low/Mid/High parse).
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.energyState = LabeledCountdown.Unavailable;

        Assert.That(beatManager.EnergyQuery, Is.Null);
    }

    /// <summary>An unknown next-energy label serves next = null with a steady (0) direction.</summary>
    [Test]
    public void EnergyTreatsUnknownNextLabelAsSteadyDirection()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "Mid", countBeats = 4, lengthBeats = 16 };
        beatManager.WireSnapshot.nextEnergyState = new LabeledCountdown { label = "", countBeats = 4, lengthBeats = 16 };

        var energy = beatManager.EnergyQuery;

        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.next, Is.Null);
        Assert.That(energy.Value.direction, Is.EqualTo(0));
    }

    // --- Track Phrase (open vocabulary) ---

    /// <summary>Phrase labels pass through as an open vocabulary while countdown, length, and progress are cooked.</summary>
    [Test]
    public void PhrasePassesOpenVocabularyLabelsThroughAndCooksStructure()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Chorus 2", countBeats = 12, lengthBeats = 32, irregular = 0 };

        var phrase = beatManager.PhraseQuery;

        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.Value.label, Is.EqualTo("Chorus 2"));
        Assert.That(phrase.Value.beatsUntilNext, Is.EqualTo(12));
        Assert.That(phrase.Value.lengthBeats, Is.EqualTo(32));
        Assert.That(phrase.Value.irregular, Is.False);
        // (32 - 12) elapsed beats plus the 0.5 intra-beat fraction, over 32.
        Assert.That(phrase.Value.progress, Is.EqualTo(0.640625f).Within(0.0001f));
    }

    /// <summary>The phrase irregular tri-state maps 1/0/-1 to true/false/null.</summary>
    [Test]
    public void PhraseIrregularTriStateMapsToNullableBool()
    {
        var beatManager = CreateLiveBeatManager();

        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Chorus 2", countBeats = 12, lengthBeats = 32, irregular = 1 };
        Assert.That(beatManager.PhraseQuery!.Value.irregular, Is.True);

        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Chorus 2", countBeats = 12, lengthBeats = 32, irregular = 0 };
        Assert.That(beatManager.PhraseQuery!.Value.irregular, Is.False);

        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Chorus 2", countBeats = 12, lengthBeats = 32, irregular = -1 };
        Assert.That(beatManager.PhraseQuery!.Value.irregular, Is.Null);
    }

    /// <summary>A phrase with no label — empty or the full unavailable shape — reads as null Phrase.</summary>
    [Test]
    public void PhraseIsNullWithoutALabelOrWhenUnavailable()
    {
        var beatManager = CreateLiveBeatManager();

        beatManager.WireSnapshot.phraseState = new PhraseState { label = "", countBeats = 12, lengthBeats = 32, irregular = 0 };
        Assert.That(beatManager.PhraseQuery, Is.Null);

        beatManager.WireSnapshot.phraseState = PhraseState.Unavailable;
        Assert.That(beatManager.PhraseQuery, Is.Null);
    }

    // --- Next Phrase planning (separate labeled countdown from the phrase-in-progress) ---

    /// <summary>NextPhrase serves the upcoming label with its countdown and its own length.</summary>
    [Test]
    public void NextPhrasePassesLabelAndCooksCountdown()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.nextPhraseState = new LabeledCountdown { label = "Drop", countBeats = 9, lengthBeats = 64 };

        var nextPhrase = beatManager.NextPhrase;

        Assert.That(nextPhrase, Is.Not.Null);
        Assert.That(nextPhrase!.Value.label, Is.EqualTo("Drop"));
        Assert.That(nextPhrase.Value.beatsUntilChange, Is.EqualTo(9));
        Assert.That(nextPhrase.Value.lengthBeats, Is.EqualTo(64));
    }

    /// <summary>The unavailable next-phrase shape (no label) reads as null NextPhrase.</summary>
    [Test]
    public void NextPhraseIsNullWithoutALabel()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.nextPhraseState = LabeledCountdown.Unavailable;

        Assert.That(beatManager.NextPhrase, Is.Null);
    }

    // --- Loop state (tri-state gating) ---

    /// <summary>The wire's unavailable loop tri-state (-1) reads as a null Loop.</summary>
    [Test]
    public void LoopIsNullWhenActiveTriStateIsUnavailable()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.loopState = LoopState.Unavailable;

        Assert.That(beatManager.LoopQuery, Is.Null);
    }

    /// <summary>An idle-but-set loop region (active 0, set 1) is real non-null data with its full shape served.</summary>
    [Test]
    public void LoopIdleButRegionSetIsRealDataNotUnavailable()
    {
        // active=0 (not rolling) with set=1 (a region exists) is idle-but-set: real data, not "unavailable".
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.loopState = new LoopState { active = 0, set = 1, lengthBeats = 4f, lengthMs = 2000, sizeNumerator = 1, sizeDenominator = 4 };

        var loop = beatManager.LoopQuery;

        Assert.That(loop, Is.Not.Null);
        Assert.That(loop!.Value.looping, Is.False);
        Assert.That(loop.Value.regionSet, Is.True);
        Assert.That(loop.Value.lengthBeats, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(loop.Value.lengthMs, Is.EqualTo(2000));
        Assert.That(loop.Value.sizeNumerator, Is.EqualTo(1));
        Assert.That(loop.Value.sizeDenominator, Is.EqualTo(4));
    }

    /// <summary>A rolling loop serves looping and regionSet both true.</summary>
    [Test]
    public void LoopRollingAndRegionSetBothReadTrue()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.loopState = new LoopState { active = 1, set = 1, lengthBeats = 8f, lengthMs = 4000, sizeNumerator = 1, sizeDenominator = 2 };

        var loop = beatManager.LoopQuery;

        Assert.That(loop, Is.Not.Null);
        Assert.That(loop!.Value.looping, Is.True);
        Assert.That(loop.Value.regionSet, Is.True);
    }

    /// <summary>Inside a valid loop state, each -1 length/size field maps to null on its own.</summary>
    [Test]
    public void LoopMapsNegativeLengthBeatsToNullInsideAValidState()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.loopState = new LoopState { active = 0, set = 0, lengthBeats = -1f, lengthMs = -1, sizeNumerator = -1, sizeDenominator = -1 };

        var loop = beatManager.LoopQuery;

        Assert.That(loop, Is.Not.Null);
        Assert.That(loop!.Value.looping, Is.False);
        Assert.That(loop.Value.regionSet, Is.False);
        Assert.That(loop.Value.lengthBeats, Is.Null);
        Assert.That(loop.Value.lengthMs, Is.Null);
        Assert.That(loop.Value.sizeNumerator, Is.Null);
        Assert.That(loop.Value.sizeDenominator, Is.Null);
    }

    // --- Levels smoothing and the Color Bank ---

    [Test]
    public void LevelsAreNullBeforeAnyLiveSamples()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.Update(0f);

        Assert.That(beatManager.LevelsQuery, Is.Null);
        Assert.That(beatManager.LevelsRgb, Is.Null);
        Assert.That(beatManager.LevelsHue, Is.Null);
        Assert.That(beatManager.LevelsPalette, Is.Null);
    }

    /// <summary>The first live Levels sample snaps in unsmoothed.</summary>
    [Test]
    public void LevelsSnapToTheFirstLiveSample()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.levels = new Levels { low = 0.2f, mid = 0.4f, high = 0.8f };

        beatManager.Update(0f);

        var levels = beatManager.LevelsQuery;
        Assert.That(levels, Is.Not.Null);
        Assert.That(levels!.Value.low, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(levels.Value.mid, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(levels.Value.high, Is.EqualTo(0.8f).Within(0.0001f));
    }

    /// <summary>Rising bands follow the attack time-constant; falling bands follow the slower release.</summary>
    [Test]
    public void LevelsRiseOnAttackAndFallOnRelease()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.levelsAttackSeconds = 0.1f;
        beatManager.levelsReleaseSeconds = 0.4f;
        beatManager.WireSnapshot.levels = new Levels { low = 0.2f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0f);

        // Rising low band uses the attack time-constant.
        beatManager.WireSnapshot.levels = new Levels { low = 1f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0.1f);
        var expectedAttack = 0.2f + ((1f - 0.2f) * (1f - Mathf.Exp(-0.1f / 0.1f)));
        Assert.That(beatManager.LevelsQuery!.Value.low, Is.EqualTo(expectedAttack).Within(0.0001f));

        // Falling low band uses the slower release time-constant.
        beatManager.WireSnapshot.levels = new Levels { low = 0f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0.2f);
        var expectedRelease = expectedAttack + ((0f - expectedAttack) * (1f - Mathf.Exp(-0.1f / 0.4f)));
        Assert.That(beatManager.LevelsQuery!.Value.low, Is.EqualTo(expectedRelease).Within(0.0001f));
        Assert.That(beatManager.LevelsQuery!.Value.mid, Is.EqualTo(0.4f).Within(0.0001f));
    }

    /// <summary>After a Levels gap the smoothing state drops, so the next live sample snaps in fresh instead of releasing from stale values.</summary>
    [Test]
    public void LevelsResetInsteadOfReleasingFromStaleValuesAfterAGap()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.levels = new Levels { low = 0.9f, mid = 0.9f, high = 0.9f };
        beatManager.Update(0f);

        beatManager.WireSnapshot.levels = Levels.Unavailable;
        beatManager.Update(0.1f);
        Assert.That(beatManager.LevelsQuery, Is.Null);

        // The next live sample snaps in fresh; nothing decays from the pre-gap 0.9.
        beatManager.WireSnapshot.levels = new Levels { low = 0.1f, mid = 0.1f, high = 0.1f };
        beatManager.Update(0.2f);
        Assert.That(beatManager.LevelsQuery!.Value.low, Is.EqualTo(0.1f).Within(0.0001f));
    }

    /// <summary>The raw Color Bank form maps low/mid/high straight onto R/G/B with full alpha.</summary>
    [Test]
    public void LevelsRgbMapsBandsStraightOntoChannels()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.levels = new Levels { low = 0.2f, mid = 0.4f, high = 0.8f };
        beatManager.Update(0f);

        var color = beatManager.LevelsRgb;

        Assert.That(color, Is.Not.Null);
        Assert.That(color!.Value.r, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(color.Value.g, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(color.Value.b, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(color.Value.a, Is.EqualTo(1f));
    }

    /// <summary>The hue Color Bank form points the hue at the spectral centroid of the bands (high-only reads blue).</summary>
    [Test]
    public void LevelsHueTracksTheSpectralCentroid()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.levels = new Levels { low = 0f, mid = 0f, high = 0.8f };
        beatManager.Update(0f);

        var color = beatManager.LevelsHue;
        var expected = Color.HSVToRGB(2f / 3f, 1f, 0.8f);

        Assert.That(color, Is.Not.Null);
        Assert.That(color!.Value.r, Is.EqualTo(expected.r).Within(0.0001f));
        Assert.That(color.Value.g, Is.EqualTo(expected.g).Within(0.0001f));
        Assert.That(color.Value.b, Is.EqualTo(expected.b).Within(0.0001f));
    }

    /// <summary>The hue Color Bank form reads black at silence — no energy means no centroid to point at.</summary>
    [Test]
    public void LevelsHueIsBlackAtSilence()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.levels = new Levels { low = 0f, mid = 0f, high = 0f };
        beatManager.Update(0f);

        Assert.That(beatManager.LevelsHue, Is.EqualTo((Color?)Color.black));
    }

    /// <summary>The palette Color Bank form is null in edit mode when no live Controller owns a palette.</summary>
    [Test]
    public void LevelsPaletteIsNullWithoutALiveController()
    {
        // The palette-mediated form needs the Controller-owned AnimPalette; in headless edit-mode
        // tests there must be no Controller, and the query must say "unavailable" rather than spawn one.
        Assume.That(Controller.HasInstance, Is.False, "These tests assume no live Controller in the scene.");

        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.levels = new Levels { low = 0.5f, mid = 0.5f, high = 0.5f };
        beatManager.Update(0f);

        Assert.That(beatManager.LevelsQuery, Is.Not.Null);
        Assert.That(beatManager.LevelsPalette, Is.Null);
    }

    // --- Source transitions ---

    /// <summary>Dropping to Standalone clears phrase/level/envelope reads to null instead of replaying stale live values.</summary>
    [Test]
    public void StandaloneClearsPhraseLevelAndEnvelopeStateToUnavailable()
    {
        // Stale live values (or stale scene-serialized values) must not replay through the contrived queries
        // once the live source drops out: Standalone is a no-beat state, not a musical analysis.
        var beatManager = new BeatManager();
        beatManager.WireSnapshot.fillState = new CountdownState { active = 1, countBeats = 2, lengthBeats = 8, remaining = 1 };
        beatManager.WireSnapshot.dropState = new CountdownState { active = 0, countBeats = 16, lengthBeats = 32, remaining = 2 };
        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Drop", countBeats = 12, lengthBeats = 32, irregular = 0 };
        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "High", countBeats = 4, lengthBeats = 16 };
        beatManager.WireSnapshot.nextEnergyState = new LabeledCountdown { label = "Mid", countBeats = 4, lengthBeats = 16 };
        beatManager.WireSnapshot.levels = new Levels { low = 0.5f, mid = 0.5f, high = 0.5f };

        beatManager.Update(0f);

        Assert.That(beatManager.IsActive, Is.False);
        Assert.That(beatManager.Envelope(0), Is.Null);
        Assert.That(beatManager.FillQuery, Is.Null);
        Assert.That(beatManager.DropQuery, Is.Null);
        Assert.That(beatManager.PhraseQuery, Is.Null);
        Assert.That(beatManager.EnergyQuery, Is.Null);
        Assert.That(beatManager.LevelsQuery, Is.Null);
    }

    // --- Subdivision rhythm family (PulseOf / GateOf) ---

    /// <summary>
    /// Live manager pinned to an exact bar position: beat <paramref name="beatInBar"/> with
    /// <paramref name="msToNextBeat"/> left of the 500 ms average, so BarPhase and every Subdivision phase land
    /// on known values. The default 500 sits exactly on that beat's onset (intra-beat fraction 0).
    /// </summary>
    private static BeatManager LiveAtBeat(int beatInBar, int msToNextBeat = 500)
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        var snapshot = beatManager.WireSnapshot;
        snapshot.bpm = 128f;
        snapshot.beatInBar = beatInBar;
        snapshot.beatAverageMs = 500;
        // IntraBeatFraction reads the next label's slot (label % 4); only that slot must carry a countdown.
        var countdowns = new[] { -1, -1, -1, -1 };
        countdowns[beatInBar % 4] = msToNextBeat;
        snapshot.beatsCountMs = countdowns;
        return beatManager;
    }

    [Test]
    public void SubdivisionQueriesAreNullWithoutBeatClock()
    {
        var beatManager = new BeatManager(); // Standalone: no clock

        Assert.That(beatManager.PulseOf(Subdivision.Beat), Is.Null);
        Assert.That(beatManager.GateOf(Subdivision.Sixteenth), Is.Null);
    }

    [Test]
    public void BeatPulsePeaksOnEveryBeatOnset()
    {
        Assert.That(LiveAtBeat(1).PulseOf(Subdivision.Beat), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(LiveAtBeat(2).PulseOf(Subdivision.Beat), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(LiveAtBeat(3).PulseOf(Subdivision.Beat), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(LiveAtBeat(4).PulseOf(Subdivision.Beat), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void BackbeatFiresOnTwoAndFourNotOneAndThree()
    {
        // The hard gate is open only on the 2 and 4 onsets.
        Assert.That(LiveAtBeat(1).GateOf(Subdivision.Backbeat), Is.EqualTo(0f));
        Assert.That(LiveAtBeat(2).GateOf(Subdivision.Backbeat), Is.EqualTo(1f));
        Assert.That(LiveAtBeat(3).GateOf(Subdivision.Backbeat), Is.EqualTo(0f));
        Assert.That(LiveAtBeat(4).GateOf(Subdivision.Backbeat), Is.EqualTo(1f));

        // The smooth pulse peaks there too.
        Assert.That(LiveAtBeat(2).PulseOf(Subdivision.Backbeat), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(LiveAtBeat(4).PulseOf(Subdivision.Backbeat), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void BarPulseDecaysAcrossTheWholeBarFromTheDownbeat()
    {
        Assert.That(LiveAtBeat(1).PulseOf(Subdivision.Bar), Is.EqualTo(1f).Within(0.0001f));   // downbeat peak
        Assert.That(LiveAtBeat(3).PulseOf(Subdivision.Bar), Is.EqualTo(0.5f).Within(0.0001f)); // halfway through the bar
    }

    [Test]
    public void OffbeatIsTheSynthesizedSiblingOfTheBeatAtTheAnd()
    {
        // CreateLiveBeatManager pins BarPhase 0.125 — the "and" of beat 1, where the offbeat onset lands and
        // the on-beat pulse sits in its trough (the same fixture the Envelope offbeat-variant test uses).
        var beatManager = CreateLiveBeatManager();

        Assert.That(beatManager.BarPhase, Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(beatManager.PulseOf(Subdivision.Offbeat), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(beatManager.GateOf(Subdivision.Offbeat), Is.EqualTo(1f));
        Assert.That(beatManager.PulseOf(Subdivision.Beat), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.GateOf(Subdivision.Beat), Is.EqualTo(0f));
    }

    [Test]
    public void SixteenthGateRatchetsOnAndOffWithinABeat()
    {
        Assert.That(LiveAtBeat(1).GateOf(Subdivision.Sixteenth), Is.EqualTo(1f));      // beat onset = a 16th onset
        Assert.That(LiveAtBeat(1, 400).GateOf(Subdivision.Sixteenth), Is.EqualTo(0f)); // 1/5 into the beat = mid-16th, shut
        Assert.That(LiveAtBeat(1, 375).GateOf(Subdivision.Sixteenth), Is.EqualTo(1f)); // 1/4 into the beat = next 16th onset
    }
}
