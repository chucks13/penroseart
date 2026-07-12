// Seam-1 tests for the event doorways: wire snapshot in, Span doorway reads out (beat-data ticket 16).

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

/// <summary>
/// Seam-1 tests for the event doorways (beat-data ticket 16): wire snapshot state in, Span
/// doorway reads out. Covers per-lane sentinel → null translation, Started/Ended/Changed/Wrapped
/// single-frame truth (including "signals outlive facts"), Stock Envelope anchoring and
/// rest-at-0, and the Grid reshape. Expected values come from the wire contract's worked examples
/// (phrase "Up" 6 16 0, drop 1 16 16 1, fill 0 6 8 2, energy "Mid" 22 48, loop 1 1 4.0 1875 4 1,
/// the timing-grid table) and hand-worked smoothstep points — never from re-running the
/// implementation's math.
/// </summary>
public sealed class BeatManagerSpanDoorwayTests
{
    // ---- Phrase -------------------------------------------------------------------------------

    /// <summary>The contract's worked phrase example, fed as real bytes, serves span and next facts.</summary>
    [Test]
    public void PhraseServesWireFactsFromRealBytes()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WritePhraseState(ref bundle, "/rave/onair/phrase_state", "Up", 6, 16, 0);
            OnAirOscWriter.WriteLabeledCountdown(ref bundle, "/rave/onair/next_phrase_state", "Drop", 6, 16);
        });
        beatManager.Update(0f);

        var phrase = beatManager.Phrase;
        Assert.That(phrase.Span.Current, Is.Not.Null);
        Assert.That(phrase.Span.Current!.Value.Name, Is.EqualTo("Up"));
        Assert.That(phrase.Span.Current!.Value.BeatsRemaining, Is.EqualTo(6));
        Assert.That(phrase.Span.Current!.Value.LengthBeats, Is.EqualTo(16));
        Assert.That(phrase.Span.Current!.Value.Irregular, Is.False);
        // 6 of 16 beats remain including the current one: 10 elapsed, no sub-beat clock fed.
        Assert.That(phrase.Span.Progress, Is.EqualTo(0.625f).Within(0.0001f));
        Assert.That(phrase.NextName, Is.EqualTo("Drop"));
        Assert.That(phrase.NextInBeats, Is.EqualTo(6));
        Assert.That(phrase.NextLengthBeats, Is.EqualTo(16));
    }

    /// <summary>The complete unavailable phrase shapes read as null facts, never empty-string names.</summary>
    [Test]
    public void PhraseUnavailableShapesReadNull()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WritePhraseState(ref bundle, "/rave/onair/phrase_state", "", -1, -1, -1);
            OnAirOscWriter.WriteLabeledCountdown(ref bundle, "/rave/onair/next_phrase_state", "", -1, -1);
        });
        beatManager.Update(0f);

        var phrase = beatManager.Phrase;
        Assert.That(phrase.Span.Current, Is.Null);
        Assert.That(phrase.Span.Progress, Is.Null);
        Assert.That(phrase.NextName, Is.Null);
        Assert.That(phrase.NextInBeats, Is.Null);
        Assert.That(phrase.NextLengthBeats, Is.Null);
        Assert.That(phrase.Span.Started, Is.False);
        Assert.That(phrase.Span.Ended, Is.False);
    }

    /// <summary>An unknown non-empty label is an opaque phrase name — served untouched, never rejected or mapped.</summary>
    [Test]
    public void PhraseServesOpaqueLabelsAsNames()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WritePhraseState(ref bundle, "/rave/onair/phrase_state", "Verse 2", 5, 25, 1));
        beatManager.Update(0f);

        Assert.That(beatManager.Phrase.Span.Current!.Value.Name, Is.EqualTo("Verse 2"));
        Assert.That(beatManager.Phrase.Span.Current!.Value.Irregular, Is.True, "a 25-beat phrase breaks the ÷16 grid");
    }

    /// <summary>
    /// A phrase appearing fires Changed but never Started (the onset was not witnessed); a name
    /// change during continuous presence is a boundary — Ended and Started fire together, one
    /// frame only.
    /// </summary>
    [Test]
    public void PhraseBoundaryEdgesFireOnNameChangeNotOnAppearance()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.Update(0f);

        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Intro", countBeats = 16, lengthBeats = 16, irregular = 0 };
        beatManager.Update(0f);
        Assert.That(beatManager.Phrase.Changed, Is.True, "a phrase appeared from nothing");
        Assert.That(beatManager.Phrase.Span.Started, Is.False, "appearance is not a witnessed onset");
        Assert.That(beatManager.Phrase.Span.Ended, Is.False);

        beatManager.Update(0f);
        Assert.That(beatManager.Phrase.Changed, Is.False, "an edge is true for exactly one frame");

        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Up", countBeats = 16, lengthBeats = 16, irregular = 0 };
        beatManager.Update(0f);
        Assert.That(beatManager.Phrase.Changed, Is.True);
        Assert.That(beatManager.Phrase.Span.Started, Is.True, "the new phrase began this frame");
        Assert.That(beatManager.Phrase.Span.Ended, Is.True, "the old phrase ended this frame");

        beatManager.Update(0f);
        Assert.That(beatManager.Phrase.Span.Started, Is.False);
        Assert.That(beatManager.Phrase.Span.Ended, Is.False);
    }

    /// <summary>Signals outlive facts: the Ended edge fires the frame the phrase facts vanish.</summary>
    [Test]
    public void PhraseEndedFiresTheFrameFactsVanish()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Outro", countBeats = 4, lengthBeats = 16, irregular = 0 };
        beatManager.Update(0f);

        beatManager.WireSnapshot.phraseState = PhraseState.Unavailable;
        beatManager.Update(0f);

        Assert.That(beatManager.Phrase.Span.Current, Is.Null);
        Assert.That(beatManager.Phrase.Span.Ended, Is.True, "the facts vanished this frame");
        Assert.That(beatManager.Phrase.Changed, Is.True, "vanishing is a change");
        Assert.That(beatManager.Phrase.Span.Build(), Is.EqualTo(0f), "envelopes rest at 0 outside the span");

        beatManager.Update(0f);
        Assert.That(beatManager.Phrase.Span.Ended, Is.False);
    }

    // ---- Drop ---------------------------------------------------------------------------------

    /// <summary>The contract's upcoming-drop example: anticipation facts serve, the span is not inside.</summary>
    [Test]
    public void DropUpcomingServesAnticipationFacts()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/drop_state", 0, 6, 16, 1));
        beatManager.Update(0f);

        var drop = beatManager.Drop;
        Assert.That(drop.Span.Current, Is.Null);
        Assert.That(drop.NextInBeats, Is.EqualTo(6));
        Assert.That(drop.NextLengthBeats, Is.EqualTo(16));
        Assert.That(drop.RemainingOnTrack, Is.EqualTo(1));
        Assert.That(drop.Span.Build(), Is.EqualTo(0f), "envelopes rest at 0 outside the span");
        Assert.That(drop.Span.Decay(), Is.EqualTo(0f));
        Assert.That(drop.Span.Decay(8f), Is.EqualTo(0f), "an override duration cannot wake a resting envelope");
    }

    /// <summary>The contract's active-drop example: span facts serve; the countdown belongs to the running drop, not a next one.</summary>
    [Test]
    public void DropActiveServesSpanFacts()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 16, 16, 1));
        beatManager.Update(0f);

        var drop = beatManager.Drop;
        Assert.That(drop.Span.Current, Is.Not.Null);
        Assert.That(drop.Span.Current!.Value.BeatsRemaining, Is.EqualTo(16));
        Assert.That(drop.Span.Current!.Value.LengthBeats, Is.EqualTo(16));
        Assert.That(drop.NextInBeats, Is.Null, "the wire's countdown describes the running drop");
        Assert.That(drop.NextLengthBeats, Is.Null);
        Assert.That(drop.RemainingOnTrack, Is.EqualTo(1));
        // On the drop's first beat: nothing elapsed.
        Assert.That(drop.Span.Progress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(drop.Span.Build(), Is.EqualTo(0f));
        Assert.That(drop.Span.Decay(), Is.EqualTo(1f), "Decay peaks at the span's start");
    }

    /// <summary>The complete unavailable drop shape reads as null facts and resting signals.</summary>
    [Test]
    public void DropUnavailableShapeReadsNull()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/drop_state", -1, -1, -1, -1));
        beatManager.Update(0f);

        var drop = beatManager.Drop;
        Assert.That(drop.Span.Current, Is.Null);
        Assert.That(drop.NextInBeats, Is.Null);
        Assert.That(drop.NextLengthBeats, Is.Null);
        Assert.That(drop.RemainingOnTrack, Is.Null);
        Assert.That(drop.Span.Started, Is.False);
        Assert.That(drop.Span.Ended, Is.False);
    }

    /// <summary>
    /// The contract's running-drop rule: remaining can read 0 while active is still 1 after the
    /// drop point passes — a running drop is detectable from Current, never from the count.
    /// </summary>
    [Test]
    public void DropRunningIsDetectableFromCurrentWhileRemainingReadsZero()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 4, 16, 0));
        beatManager.Update(0f);

        Assert.That(beatManager.Drop.RemainingOnTrack, Is.EqualTo(0));
        Assert.That(beatManager.Drop.Span.Current, Is.Not.Null, "active == 1 is the running-drop truth");
    }

    /// <summary>The Started edge fires exactly the frame counting-down turns active, and only that frame.</summary>
    [Test]
    public void DropStartedFiresTheFrameTheDropLands()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.dropState = new CountdownState { active = 0, countBeats = 1, lengthBeats = 16, remaining = 1 };
        beatManager.Update(0f);
        Assert.That(beatManager.Drop.Span.Started, Is.False);

        beatManager.WireSnapshot.dropState = new CountdownState { active = 1, countBeats = 16, lengthBeats = 16, remaining = 1 };
        beatManager.Update(0f);
        Assert.That(beatManager.Drop.Span.Started, Is.True, "counting-down → active is the witnessed slam");

        beatManager.WireSnapshot.dropState = new CountdownState { active = 1, countBeats = 15, lengthBeats = 16, remaining = 1 };
        beatManager.Update(0f);
        Assert.That(beatManager.Drop.Span.Started, Is.False, "an edge is true for exactly one frame");
    }

    /// <summary>
    /// Mid-event activation: a drop first observed already running reads Current without a
    /// synthesized Started edge, and its Ended edge still fires the frame the facts vanish.
    /// </summary>
    [Test]
    public void DropFirstObservedMidEventReadsCurrentWithoutAStartedEdge()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 8, 16, 1));
        beatManager.Update(0f);

        Assert.That(beatManager.Drop.Span.Current, Is.Not.Null);
        Assert.That(beatManager.Drop.Span.Started, Is.False, "the onset happened before the hub could witness it");

        beatManager.WireSnapshot.dropState = CountdownState.Unavailable;
        beatManager.Update(0f);
        Assert.That(beatManager.Drop.Span.Ended, Is.True, "signals outlive facts");
        Assert.That(beatManager.Drop.Span.Current, Is.Null);
    }

    /// <summary>
    /// Envelope anchoring at the span's midpoint (hand-worked smoothstep points): the default
    /// window is the drop's own length; an override re-windows the same anchor.
    /// </summary>
    [Test]
    public void DropEnvelopesAnchorAtSpanStartWithDefaultAndOverrideWindows()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 8, 16, 1));
        beatManager.Update(0f);

        var span = beatManager.Drop.Span;
        // 8 of 16 beats remain: 8 elapsed. smoothstep(0.5) = 0.5 by hand.
        Assert.That(span.Progress, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(span.Build(), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(span.Decay(), Is.EqualTo(0.5f).Within(0.0001f));
        // An 8-beat window is fully elapsed: Build holds 1, Decay has fallen to rest.
        Assert.That(span.Build(8f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(span.Decay(8f), Is.EqualTo(0f).Within(0.0001f));
        // A 32-beat window is a quarter elapsed: smoothstep(0.25) = 0.15625 by hand.
        Assert.That(span.Build(32f), Is.EqualTo(0.15625f).Within(0.0001f));
    }

    /// <summary>Span progress sweeps with the shared sub-beat clock instead of stepping once per beat.</summary>
    [Test]
    public void SpanProgressIsSmoothedByTheSharedIntraBeatClock()
    {
        // The seeded 120 BPM metronome at t = 0.25 s is half a beat in (the repo's worked clock
        // example), so the phrase's 10 whole elapsed beats carry an extra half: 10.5 / 16.
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        beatManager.WireSnapshot.phraseState = new PhraseState { label = "Up", countBeats = 6, lengthBeats = 16, irregular = 0 };
        beatManager.Update(0.25f);

        Assert.That(beatManager.Phrase.Span.Progress, Is.EqualTo(0.65625f).Within(0.0001f));
    }

    // ---- Fill ---------------------------------------------------------------------------------

    /// <summary>The contract's upcoming-fill example: the live set's selected fill serves its anticipation facts.</summary>
    [Test]
    public void FillUpcomingServesAnticipationFacts()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/fill_state", 0, 6, 8, 2));
        beatManager.Update(0f);

        var fill = beatManager.Fill;
        Assert.That(fill.Span.Current, Is.Null);
        Assert.That(fill.NextInBeats, Is.EqualTo(6));
        Assert.That(fill.NextLengthBeats, Is.EqualTo(8));
        Assert.That(fill.RemainingOnTrack, Is.EqualTo(2));
    }

    /// <summary>The contract's active-fill example serves span facts, and Ended fires when the lane goes unavailable.</summary>
    [Test]
    public void FillActiveServesSpanFactsAndEndedFiresWhenFactsVanish()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/fill_state", 1, 4, 4, 1));
        beatManager.Update(0f);

        var fill = beatManager.Fill;
        Assert.That(fill.Span.Current, Is.Not.Null);
        Assert.That(fill.Span.Current!.Value.BeatsRemaining, Is.EqualTo(4));
        Assert.That(fill.Span.Current!.Value.LengthBeats, Is.EqualTo(4));
        Assert.That(fill.RemainingOnTrack, Is.EqualTo(1), "the selected fill counts itself while active");
        Assert.That(fill.Span.Progress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(fill.Span.Decay(), Is.EqualTo(1f));

        beatManager.WireSnapshot.fillState = CountdownState.Unavailable;
        beatManager.Update(0f);
        Assert.That(beatManager.Fill.Span.Ended, Is.True, "signals outlive facts");
        Assert.That(beatManager.Fill.Span.Decay(), Is.EqualTo(0f), "envelopes rest once outside");
    }

    // ---- Energy -------------------------------------------------------------------------------

    /// <summary>The contract's worked energy examples serve the run, the next different run, and a Rising trend.</summary>
    [Test]
    public void EnergyServesRunNextAndRisingTrendFromRealBytes()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteLabeledCountdown(ref bundle, "/rave/onair/energy_state", "Mid", 22, 48);
            OnAirOscWriter.WriteLabeledCountdown(ref bundle, "/rave/onair/next_energy_state", "High", 6, 32);
        });
        beatManager.Update(0f);

        var energy = beatManager.Energy;
        Assert.That(energy.Run.Current, Is.Not.Null);
        Assert.That(energy.Run.Current!.Value.Level, Is.EqualTo(Energy.Mid));
        Assert.That(energy.Run.Current!.Value.BeatsRemaining, Is.EqualTo(22));
        Assert.That(energy.Run.Current!.Value.LengthBeats, Is.EqualTo(48));
        // 22 of 48 remain including the current beat: 26 elapsed, no sub-beat clock fed.
        Assert.That(energy.Run.Progress, Is.EqualTo(26f / 48f).Within(0.0001f));
        Assert.That(energy.NextLevel, Is.EqualTo(Energy.High));
        Assert.That(energy.NextChangeInBeats, Is.EqualTo(6));
        Assert.That(energy.NextRunLengthBeats, Is.EqualTo(32));
        Assert.That(energy.Trend, Is.EqualTo(EnergyTrend.Rising));
    }

    /// <summary>Trend falls with a lower next level, holds Steady when no different run is known, and is null without a current level.</summary>
    [Test]
    public void EnergyTrendFollowsTheLadderAndNullsWithoutACurrentLevel()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "High", countBeats = 8, lengthBeats = 32 };
        beatManager.WireSnapshot.nextEnergyState = new LabeledCountdown { label = "Low", countBeats = 8, lengthBeats = 16 };
        beatManager.Update(0f);
        Assert.That(beatManager.Energy.Trend, Is.EqualTo(EnergyTrend.Falling));

        beatManager.WireSnapshot.nextEnergyState = LabeledCountdown.Unavailable;
        beatManager.Update(0f);
        Assert.That(beatManager.Energy.Trend, Is.EqualTo(EnergyTrend.Steady),
            "the wire only announces differing runs — none known ahead is the analysis saying holding");

        beatManager.WireSnapshot.energyState = LabeledCountdown.Unavailable;
        beatManager.Update(0f);
        Assert.That(beatManager.Energy.Trend, Is.Null);
        Assert.That(beatManager.Energy.Run.Current, Is.Null);
        Assert.That(beatManager.Energy.NextLevel, Is.Null);
    }

    /// <summary>A level change during continuous presence is a run boundary: Changed, Started, and Ended fire together, one frame.</summary>
    [Test]
    public void EnergyChangedAndRunBoundaryEdgesFireOnLevelChange()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "Mid", countBeats = 4, lengthBeats = 48 };
        beatManager.Update(0f);
        Assert.That(beatManager.Energy.Changed, Is.True, "a level appeared from nothing");
        Assert.That(beatManager.Energy.Run.Started, Is.False, "appearance is not a witnessed onset");

        beatManager.Update(0f);
        Assert.That(beatManager.Energy.Changed, Is.False);

        beatManager.WireSnapshot.energyState = new LabeledCountdown { label = "High", countBeats = 32, lengthBeats = 32 };
        beatManager.Update(0f);
        Assert.That(beatManager.Energy.Changed, Is.True);
        Assert.That(beatManager.Energy.Run.Started, Is.True, "the new run began this frame");
        Assert.That(beatManager.Energy.Run.Ended, Is.True, "the old run ended this frame");

        beatManager.Update(0f);
        Assert.That(beatManager.Energy.Run.Started, Is.False);
        Assert.That(beatManager.Energy.Run.Ended, Is.False);
    }

    // ---- Loop ---------------------------------------------------------------------------------

    /// <summary>The contract's rolling-loop bytes serve every fact directly on the flat Loop view.</summary>
    [Test]
    public void LoopRollingServesFlatFactsFromRealBytes()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLoopState(ref bundle, "/rave/onair/loop_state", 1, 1, 4.0f, 1875, 4, 1));
        beatManager.Update(0f);

        var loop = beatManager.Loop;
        Assert.That(loop.Rolling, Is.True);
        Assert.That(loop.LengthBeats, Is.EqualTo(4.0f).Within(0.0001f));
        Assert.That(loop.LengthMs, Is.EqualTo(1875));
        Assert.That(loop.NominalSizeBeats, Is.EqualTo(4.0f).Within(0.0001f));
        Assert.That(loop.RegionSet, Is.True);
    }

    /// <summary>Rolling and set stay independent: an idle player's set region and lengths remain real facts.</summary>
    [Test]
    public void LoopSetButIdlePreservesFlatRegionFacts()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLoopState(ref bundle, "/rave/onair/loop_state", 0, 1, 4.0f, 1875, 4, 1));
        beatManager.Update(0f);

        Assert.That(beatManager.Loop.Rolling, Is.False, "nothing is rolling");
        Assert.That(beatManager.Loop.RegionSet, Is.True, "the region persists while paused");
        Assert.That(beatManager.Loop.LengthBeats, Is.EqualTo(4f).Within(0.0001f));
    }

    /// <summary>Fractional loops are real (a 1/2-beat loop is 0.5), and a 0/0 fraction is no nominal size, never a zero-beat loop.</summary>
    [Test]
    public void LoopServesFractionalNominalSizeAndTreatsZeroOverZeroAsNone()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.loopState = new LoopState
        { active = 1, set = 1, lengthBeats = 0.5f, lengthMs = 234, sizeNumerator = 1, sizeDenominator = 2 };
        beatManager.Update(0f);
        Assert.That(beatManager.Loop.NominalSizeBeats, Is.EqualTo(0.5f).Within(0.0001f));

        beatManager.WireSnapshot.loopState = new LoopState
        { active = 1, set = 1, lengthBeats = 0.5f, lengthMs = 234, sizeNumerator = 0, sizeDenominator = 0 };
        beatManager.Update(0f);
        Assert.That(beatManager.Loop.NominalSizeBeats, Is.Null);
    }

    /// <summary>No region and rolling transitions remain direct facts, while the all-sentinel lane reads null.</summary>
    [Test]
    public void LoopDistinguishesNoRegionAndRollingStatesFromUnavailable()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.loopState = new LoopState
        { active = 0, set = 0, lengthBeats = 0f, lengthMs = 0, sizeNumerator = 0, sizeDenominator = 0 };
        beatManager.Update(0f);
        Assert.That(beatManager.Loop.RegionSet, Is.False, "the wire's real answer: no region");
        Assert.That(beatManager.Loop.Rolling, Is.False);

        beatManager.WireSnapshot.loopState = new LoopState
        { active = 1, set = 1, lengthBeats = 4f, lengthMs = 1875, sizeNumerator = 4, sizeDenominator = 1 };
        beatManager.Update(0f);
        Assert.That(beatManager.Loop.Rolling, Is.True);

        beatManager.WireSnapshot.loopState = new LoopState
        { active = 0, set = 1, lengthBeats = 4f, lengthMs = 1875, sizeNumerator = 4, sizeDenominator = 1 };
        beatManager.Update(0f);
        Assert.That(beatManager.Loop.Rolling, Is.False);

        beatManager.WireSnapshot.loopState = LoopState.Unavailable;
        beatManager.Update(0f);
        Assert.That(beatManager.Loop.RegionSet, Is.Null, "the all-sentinel shape is unavailable, not a no");
        Assert.That(beatManager.Loop.Rolling, Is.Null);
    }

    // ---- Grid ---------------------------------------------------------------------------------

    /// <summary>A placed grid serves its facts from real bytes; the state word passes through as data.</summary>
    [Test]
    public void GridServesPlacedFactsFromRealBytes()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 1);
            OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 5, 2, "locked");
        });
        beatManager.Update(0f);

        var grid = beatManager.Grid;
        Assert.That(grid.Current, Is.Not.Null);
        Assert.That(grid.Current!.Value.State, Is.EqualTo(GridState.Locked));
        Assert.That(grid.Current!.Value.Beat, Is.EqualTo(5));
        Assert.That(grid.Current!.Value.Bar, Is.EqualTo(2));
        // Beat 5 of the 16-count: 4 beats elapsed, no sub-beat clock fed.
        Assert.That(grid.Current!.Value.Progress, Is.EqualTo(0.25f).Within(0.0001f));
    }

    /// <summary>Grid State is served data, never a gate: a disputed grid serves its position like a locked one.</summary>
    [Test]
    public void GridStateIsServedDataNeverAGate()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.beatInBar = 1;
        beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = 9, bar = 3, state = "disputed" };
        beatManager.Update(0f);

        Assert.That(beatManager.Grid.Current!.Value.State, Is.EqualTo(GridState.Disputed));
        Assert.That(beatManager.Grid.Current!.Value.Beat, Is.EqualTo(9), "trust is the consumer's to weigh");
    }

    /// <summary>
    /// The contract's partial shape (-1 -1 "coasting" — a focus player with no placeable beat)
    /// serves state-only facts, distinct from the complete unavailable shape's null read.
    /// </summary>
    [Test]
    public void GridPartialCoastingShapeServesStateOnlyFacts()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 2);
            OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", -1, -1, "coasting");
        });
        beatManager.Update(0f);

        var grid = beatManager.Grid;
        Assert.That(grid.Current, Is.Not.Null, "a focus-player grid exists");
        Assert.That(grid.Current!.Value.State, Is.EqualTo(GridState.Coasting));
        Assert.That(grid.Current!.Value.Beat, Is.Null, "no beat can be placed");
        Assert.That(grid.Current!.Value.Bar, Is.Null);
        Assert.That(grid.Current!.Value.Progress, Is.Null);
        Assert.That(grid.Build(), Is.EqualTo(0f), "no anchor — the envelopes rest");
        Assert.That(grid.Decay(), Is.EqualTo(0f));
    }

    /// <summary>The complete unavailable shape (-1 -1 "") and Standalone Mode both read a null Grid.</summary>
    [Test]
    public void GridUnavailableShapeAndStandaloneReadNull()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 2);
            OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", -1, -1, "");
        });
        beatManager.Update(0f);
        Assert.That(beatManager.Grid.Current, Is.Null);

        var standalone = new BeatManager();
        standalone.Update(0f);
        Assert.That(standalone.Grid.Current, Is.Null, "losing the clock is a null Grid read, not a fourth state");
        Assert.That(standalone.Grid.Wrapped, Is.False);
    }

    /// <summary>
    /// The wrap edge fires the frame the 16-count returns to the One — one frame — and the grid
    /// appearing is a mode change, never a wrap.
    /// </summary>
    [Test]
    public void GridWrappedFiresOnTheWrapToTheOneNotOnAppearance()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 1);
            OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 9, 3, "locked");
        });
        beatManager.Update(0f);
        Assert.That(beatManager.Grid.Wrapped, Is.False, "the grid appearing is not a boundary the music crossed");

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 1);
            OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 8, 2, "locked");
        });
        beatManager.Update(0f);
        Assert.That(beatManager.Grid.Wrapped, Is.False, "a backward position that does not reach the One is not a wrap");

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 1);
            OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 1, 1, "locked");
        });
        beatManager.Update(0f);
        Assert.That(beatManager.Grid.Wrapped, Is.True, "the 16-count came back to the One");

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 1);
            OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 2, 1, "locked");
        });
        beatManager.Update(0f);
        Assert.That(beatManager.Grid.Wrapped, Is.False, "an edge is true for exactly one frame");
    }

    /// <summary>
    /// The cyclic envelopes re-anchor at each wrap (hand-worked smoothstep points): Started/Ended
    /// collapse into the wrap, so the Decay peaks again on every One.
    /// </summary>
    [Test]
    public void GridEnvelopesAreCyclicAndReanchorAtTheWrap()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.beatInBar = 1;
        beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = 9, bar = 3, state = "locked" };
        beatManager.Update(0f);

        // Beat 9: 8 of 16 beats elapsed. smoothstep(0.5) = 0.5 by hand.
        Assert.That(beatManager.Grid.Build(), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Grid.Decay(), Is.EqualTo(0.5f).Within(0.0001f));
        // A 4-beat window is long past: the Decay has rested since beat 5.
        Assert.That(beatManager.Grid.Decay(4f), Is.EqualTo(0f).Within(0.0001f));

        beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = 1, bar = 1, state = "locked" };
        beatManager.Update(0f);
        Assert.That(beatManager.Grid.Wrapped, Is.True);
        Assert.That(beatManager.Grid.Build(), Is.EqualTo(0f).Within(0.0001f), "re-anchored on the One");
        Assert.That(beatManager.Grid.Decay(), Is.EqualTo(1f).Within(0.0001f), "the Decay peaks again on the One");
    }

    // ---- Fixtures -----------------------------------------------------------------------------

    /// <summary>Writes lanes into one OSC bundle, so tests feed the wire contract's own bytes.</summary>
    private delegate void LaneWriter(ref OscBundleWriter bundle);

    /// <summary>
    /// Feeds real OSC bytes through the production parser into the manager, exactly as the live
    /// transport does — the seam-1 entry: wire in, doorways out.
    /// </summary>
    private static void FeedWire(BeatManager beatManager, LaneWriter writeLanes)
    {
        var buffer = new byte[1024];
        var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);
        writeLanes(ref bundle);
        var packet = System.MemoryExtensions.AsSpan(buffer, 0, bundle.Finish()).ToArray();

        using var parser = new RaveOscPacketParser();
        parser.Dispatch(packet);
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True, "the fed lanes must parse");
        beatManager.FeedWireSnapshot(snapshot);
    }
}
