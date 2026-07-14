#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Pins the pure BeatManager dashboard seam: authoritative mode, independent availability, required-Pool
/// health, responsive grouping, one-based placement, timing glyph/readout helpers, and the editor-only
/// four-bar Routine storyboard.
/// </summary>
public sealed class BeatManagerDrawerVisualModelTests
{
    [Test]
    public void BuildBeatDotGlyphsUsesRaveSystemFilledDotsForMusicalBeatPosition()
    {
        var glyphs = BeatManagerDashboardModel.BuildBeatDotGlyphs(active: true, beatInBar: 3);

        Assert.That(glyphs, Is.EqualTo("●●●○"));
    }

    [Test]
    public void BuildBeatDotGlyphsUsesPlaceholderWhenBeatClockIsUnavailable()
    {
        var glyphs = BeatManagerDashboardModel.BuildBeatDotGlyphs(active: false, beatInBar: -1);

        Assert.That(glyphs, Is.EqualTo("○○○○"));
    }

    [Test]
    public void BuildBeatDotGlyphsUsesPlaceholderWhenBeatLabelIsOutOfRange()
    {
        var glyphs = BeatManagerDashboardModel.BuildBeatDotGlyphs(active: true, beatInBar: 7);

        Assert.That(glyphs, Is.EqualTo("○○○○"));
    }

    [Test]
    public void GetClampedEighthPulseValueUsesStrongerOnBeatOrOffBeatPulse()
    {
        var pulse = BeatManagerDashboardModel.GetClampedEighthPulseValue(0.25f, 1.25f);

        Assert.That(pulse, Is.EqualTo(1f));
    }

    /// <summary>An unresolved BeatManager renders Standalone labels and unavailable value rows.</summary>
    [Test]
    public void FromUsesStandaloneHeaderWhenBeatManagerIsUnavailable()
    {
        var model = BeatManagerDashboardModel.From(null, default, "");

        Assert.That(model.Synced, Is.False);
        Assert.That(model.BadgeText, Is.EqualTo("STANDALONE MODE"));
        Assert.That(model.TrackText, Is.EqualTo("Unavailable"));
        Assert.That(model.HeaderRightText, Is.EqualTo("Unavailable"));
        Assert.That(model.Envelope.HasValue, Is.False);
        Assert.That(model.Grid.HasValue, Is.False);
    }

    /// <summary>NEXT BEAT follows the next musical label instead of reusing the current label's zero-ms gate.</summary>
    [Test]
    public void FromUsesTheFollowingMusicalLabelForNextBeat()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot
        {
            beatInBar = 2,
            beatAverageMs = 400,
            beatsCountMs = new[] { 1200, 0, 400, 800 },
            onBeats = new[] { false, true, false, false },
        });
        beatManager.Update(0f);

        var model = BeatManagerDashboardModel.From(beatManager, Waveform.Parse("QQQQ", "8888"), "");

        Assert.That(model.NextBeat.Value, Is.EqualTo("400ms"));
    }

    /// <summary>A live beat count remains Synced even when every optional musical lane is unavailable.</summary>
    [Test]
    public void FromUsesIsSyncedAsTheOnlyModeAuthority()
    {
        var beatManager = LiveManager(new RaveOnAirSnapshot { beatInBar = 1 });

        var model = BeatManagerDashboardModel.From(beatManager, default, "");

        Assert.That(model.Synced, Is.True);
        Assert.That(model.BadgeText, Is.EqualTo("SYNCED MODE"));
        Assert.That(model.TrackText, Is.EqualTo("Unavailable"));
        Assert.That(model.NextBeat.Value, Is.EqualTo("Unavailable"));
        Assert.That(model.OnBeat, Is.Null);
        Assert.That(model.BeatPulse, Is.Zero);
        Assert.That(model.OffBeatPulse, Is.Zero);
        Assert.That(model.EighthPulse, Is.Zero);
        Assert.That(model.OffBeatGateAt(0), Is.Null);
        Assert.That(model.BarPhase, Is.Zero);
        Assert.That(model.Grid.HasValue, Is.False);
    }

    /// <summary>Standalone mode suppresses stale or default facts instead of presenting fabricated music.</summary>
    [Test]
    public void FromMakesEveryLiveFactUnavailableInStandaloneMode()
    {
        var beatManager = LiveManager(new RaveOnAirSnapshot
        {
            beatInBar = 2,
            bpm = 128f,
            track = "Stale Track",
            phraseState = new PhraseState { label = "Verse", countBeats = 8, lengthBeats = 16, irregular = 0 },
            nextPhraseState = new LabeledCountdown { label = "Drop", countBeats = 8, lengthBeats = 16 },
            timingGrid = new TimingGrid { state = "locked", beat = 6, bar = 2 },
        });
        beatManager.SetLiveBeatSource(false);
        beatManager.Update(1f);

        var model = BeatManagerDashboardModel.From(beatManager, default, "");

        Assert.That(model.BadgeText, Is.EqualTo("STANDALONE MODE"));
        Assert.That(model.TrackText, Is.EqualTo("Unavailable"));
        Assert.That(model.HeaderRightText, Is.EqualTo("Unavailable"));
        Assert.That(model.NextBeat.Value, Is.EqualTo("Unavailable"));
        Assert.That(model.OnBeatGate.Value, Is.EqualTo("Unavailable"));
        Assert.That(model.OnBeat, Is.Null);
        Assert.That(model.BeatPulse, Is.Null);
        Assert.That(model.OffBeatPulse, Is.Null);
        Assert.That(model.EighthPulse, Is.Null);
        Assert.That(model.OffBeatGateAt(0), Is.Null);
        Assert.That(model.BarPhase, Is.Null);
        Assert.That(model.Grid.HasValue, Is.False);
    }

    /// <summary>Required Waveform Pool failures remain part of the dashboard's immutable health facts.</summary>
    [Test]
    public void FromPreservesRequiredWaveformPoolFailure()
    {
        const string error = "Required Waveform Pool 'waveforms.txt' contains no Waveforms.";

        var model = BeatManagerDashboardModel.From(null, default, error);
        var healthy = BeatManagerDashboardModel.From(null, Waveform.Parse("QQQQ", "8888"), "");

        Assert.That(model.WaveformAvailable, Is.False);
        Assert.That(model.WaveformMessage, Is.EqualTo(error));
        Assert.That(healthy.WaveformAvailable, Is.True);
        Assert.That(healthy.WaveformMessage, Is.Empty);
    }

    /// <summary>Dashboard groups expose the scan order and responsive flow used by the renderer.</summary>
    [Test]
    public void DashboardGroupsAndFlowDescribeNarrowAndWidePresentation()
    {
        var model = BeatManagerDashboardModel.From(null, default, "");

        Assert.That(model.Groups, Is.EqualTo(new[]
        {
            RhythmDashboardGroup.Timing,
            RhythmDashboardGroup.Waveform,
            RhythmDashboardGroup.Routine,
        }));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(560f), Is.EqualTo(RhythmDashboardFlow.Stacked));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(819f), Is.EqualTo(RhythmDashboardFlow.Stacked));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(820f), Is.EqualTo(RhythmDashboardFlow.Split));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(900f), Is.EqualTo(RhythmDashboardFlow.Split));
    }

    /// <summary>Grid displays the runtime-provided one-based musical counts.</summary>
    [Test]
    public void FromKeepsGridCountsOneBased()
    {
        var beatManager = LiveManager(new RaveOnAirSnapshot
        {
            beatInBar = 2,
            bpm = 128f,
            timingGrid = new TimingGrid { state = "locked", beat = 5, bar = 2 },
        });

        var model = BeatManagerDashboardModel.From(beatManager, default, "");

        Assert.That(model.Grid.Readout, Is.EqualTo("Bar 2 · Beat 5"));
    }

    /// <summary>The dashboard follows the staged Effect and refuses to invent an ambiguous Pool label.</summary>
    [Test]
    public void EffectRhythmSelectionFollowsCurrentEffectAndRejectsAmbiguousPoolIdentity()
    {
        var entries = new[]
        {
            new WaveformPool.Entry("One", Waveform.Parse("QQQQ", "2222")),
            new WaveformPool.Entry("Two", Waveform.Parse("QQQQ", "4444")),
            new WaveformPool.Entry("Three", Waveform.Parse("QQQQ", "6666")),
            new WaveformPool.Entry("Four", Waveform.Parse("QQQQ", "8888")),
        };
        var names = new[] { "One", "Two", "Three", "Four" };
        var beatManager = new BeatManager();
        var waveformEffect = new RhythmTestEffect
        {
            waveform = entries[2].waveform.Bind(beatManager),
        };
        var routineEffect = new RhythmTestEffect();
        routineEffect.SetRoutine(
            Routine.Of(
                entries[3].waveform.Bind(beatManager),
                entries[1].waveform.Bind(beatManager),
                entries[2].waveform.Bind(beatManager),
                entries[0].waveform.Bind(beatManager)));
        var effects = new EffectBase[] { waveformEffect, routineEffect };
        var transitions = new TransitionBase[] { new Fade() };
        var gameObject = new GameObject("Effect rhythm selection test");

        try
        {
            var controller = gameObject.AddComponent<Controller>();
            controller.effects = effects;
            controller.transitions = transitions;
            controller.switcher = new Switcher(controller, effects, transitions);
            controller.switcher.SetInitialEffect(0, 0);

            var waveformSelection = EffectRhythmSelectionView.From(
                controller,
                entries,
                names,
                poolError: "",
                gridBar: null,
                gridProgress: null);

            Assert.That(waveformSelection.EffectName, Is.EqualTo(nameof(RhythmTestEffect)));
            Assert.That(waveformSelection.WaveformSelector.ShownIndex, Is.EqualTo(2));
            Assert.That(waveformSelection.Waveform?.sequence, Is.EqualTo("QQQQ"));
            Assert.That(waveformSelection.Waveform?.amplitude, Is.EqualTo("6666"));
            Assert.That(waveformSelection.Routine.IsUsable, Is.False);
            Assert.That(waveformSelection.Routine.IsError, Is.False);

            controller.switcher.SetInitialEffect(1, 0);
            var routineSelection = EffectRhythmSelectionView.From(
                controller,
                entries,
                names,
                poolError: "",
                gridBar: null,
                gridProgress: null);

            Assert.That(routineSelection.Routine.IsUsable, Is.True);
            Assert.That(routineSelection.Routine.EntryAt(0)?.name, Is.EqualTo("Four"));
            Assert.That(routineSelection.Routine.EntryAt(1)?.name, Is.EqualTo("Two"));
            Assert.That(routineSelection.Routine.EntryAt(2)?.name, Is.EqualTo("Three"));
            Assert.That(routineSelection.Routine.EntryAt(3)?.name, Is.EqualTo("One"));
            Assert.That(routineSelection.WaveformSelector.IsError, Is.False);

            controller.switcher.SetInitialEffect(0, 0);
            var ambiguousEntries = new[]
            {
                new WaveformPool.Entry("Three A", entries[2].waveform),
                new WaveformPool.Entry("Three B", entries[2].waveform),
            };
            var ambiguousSelection = EffectRhythmSelectionView.From(
                controller,
                ambiguousEntries,
                new[] { "Three A", "Three B" },
                poolError: "",
                gridBar: null,
                gridProgress: null);

            Assert.That(ambiguousSelection.WaveformSelector.ShownIndex, Is.EqualTo(-1));
            Assert.That(ambiguousSelection.WaveformSelector.IsError, Is.True);
            Assert.That(ambiguousSelection.WaveformSelector.Error, Does.Contain("multiple Pool entries"));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    /// <summary>A required Pool failure remains an error even before an Effect owns the Switcher stage.</summary>
    [Test]
    public void RequiredPoolFailureOutranksMissingEffectStage()
    {
        const string error = "Required Waveform Pool is missing.";
        var selection = EffectRhythmSelectionView.From(
            controller: null,
            poolEntries: null,
            poolNames: System.Array.Empty<string>(),
            poolError: error,
            gridBar: null,
            gridProgress: null);

        Assert.That(selection.WaveformSelector.Error, Is.EqualTo(error));
        Assert.That(selection.WaveformSelector.IsError, Is.True);
        Assert.That(selection.Routine.Error, Is.EqualTo(error));
        Assert.That(selection.Routine.IsError, Is.True);
    }

    /// <summary>Four Pool-matched Routine bars preserve order and sample the placed bar through Waveform.</summary>
    [Test]
    public void RoutineStoryboardOrdersSelectionsAndSamplesThePlacedGridBar()
    {
        var entries = new[]
        {
            new WaveformPool.Entry("One", Waveform.Parse("QQQQ", "2222")),
            new WaveformPool.Entry("Two", Waveform.Parse("QQQQ", "4444")),
            new WaveformPool.Entry("Three", Waveform.Parse("QQQQ", "6666")),
            new WaveformPool.Entry("Four", Waveform.Parse("QQQQ", "8888")),
        };
        var selection = RoutineStoryboardSelection.Default(entries.Length)
            .Select(barIndex: 0, waveformIndex: 3, entries.Length)
            .Select(barIndex: 3, waveformIndex: 0, entries.Length);

        var placedBars = new[]
        {
            RoutineStoryboardView.From(entries, selection, poolError: "", gridBar: 1, gridProgress: 0f),
            RoutineStoryboardView.From(entries, selection, poolError: "", gridBar: 2, gridProgress: 0.25f),
            RoutineStoryboardView.From(entries, selection, poolError: "", gridBar: 3, gridProgress: 0.5f),
            RoutineStoryboardView.From(entries, selection, poolError: "", gridBar: 4, gridProgress: 0.75f),
        };

        Assert.That(placedBars[0].EntryAt(0)?.name, Is.EqualTo("Four"));
        Assert.That(placedBars[0].EntryAt(1)?.name, Is.EqualTo("Two"));
        Assert.That(placedBars[0].EntryAt(2)?.name, Is.EqualTo("Three"));
        Assert.That(placedBars[0].EntryAt(3)?.name, Is.EqualTo("One"));
        Assert.That(placedBars[0].ActiveBar, Is.EqualTo(1));
        Assert.That(placedBars[1].ActiveBar, Is.EqualTo(2));
        Assert.That(placedBars[2].ActiveBar, Is.EqualTo(3));
        Assert.That(placedBars[3].ActiveBar, Is.EqualTo(4));
        Assert.That(placedBars[0].ActiveBarPhase, Is.Zero);
        Assert.That(placedBars[1].ActiveBarPhase, Is.Zero);
        Assert.That(placedBars[2].ActiveBarPhase, Is.Zero);
        Assert.That(placedBars[3].ActiveBarPhase, Is.Zero);
        Assert.That(placedBars[0].Envelope, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(placedBars[1].Envelope, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(placedBars[2].Envelope, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(placedBars[3].Envelope, Is.EqualTo(0.25f).Within(0.0001f));
    }

    /// <summary>Unavailable Grid placement keeps the editor preview at the runtime Routine's zero rest.</summary>
    [Test]
    public void RoutineStoryboardWithoutPlacementRestsAtZero()
    {
        var entries = new[]
        {
            new WaveformPool.Entry("One", Waveform.Parse("QQQQ", "8888")),
        };

        var storyboard = RoutineStoryboardView.From(
            entries,
            RoutineStoryboardSelection.Default(entries.Length),
            poolError: "",
            gridBar: null,
            gridProgress: null);

        Assert.That(storyboard.IsUsable, Is.True);
        Assert.That(storyboard.ActiveBar, Is.Null);
        Assert.That(storyboard.ActiveBarPhase, Is.Null);
        Assert.That(storyboard.Envelope, Is.Zero);
    }

    /// <summary>An unusable required Pool reports its failure and never supplies replacement storyboard bars.</summary>
    [Test]
    public void RoutineStoryboardPreservesRequiredPoolFailureWithoutSubstitution()
    {
        const string error = "Required Waveform Pool contains no Waveforms.";

        var storyboard = RoutineStoryboardView.From(
            System.Array.Empty<WaveformPool.Entry>(),
            RoutineStoryboardSelection.Default(0),
            error,
            gridBar: 1,
            gridProgress: 0f);

        Assert.That(storyboard.IsUsable, Is.False);
        Assert.That(storyboard.Error, Is.EqualTo(error));
        Assert.That(storyboard.EntryAt(0), Is.Null);
        Assert.That(storyboard.Envelope, Is.Zero);
    }

    /// <summary>Projecting Effect-owned Routine choices leaves the serialized Pool document byte-for-byte unchanged.</summary>
    [Test]
    public void RoutineStoryboardSelectionDoesNotMutatePoolDocument()
    {
        var entries = new[]
        {
            new WaveformPool.Entry("One", Waveform.Parse("QQQQ", "2222")),
            new WaveformPool.Entry("Two", Waveform.Parse("QQQQ", "4444")),
            new WaveformPool.Entry("Three", Waveform.Parse("QQQQ", "6666")),
            new WaveformPool.Entry("Four", Waveform.Parse("QQQQ", "8888")),
        };
        var before = WaveformPool.Serialize(entries);

        var selection = RoutineStoryboardSelection.Default(entries.Length)
            .Select(barIndex: 0, waveformIndex: 3, entries.Length);
        var storyboard = RoutineStoryboardView.From(
            entries,
            selection,
            poolError: "",
            gridBar: null,
            gridProgress: null);

        Assert.That(storyboard.EntryAt(0)?.name, Is.EqualTo("Four"));
        Assert.That(WaveformPool.Serialize(entries), Is.EqualTo(before));
    }

    /// <summary>Captures one live BeatManager frame from an exact OSC-shaped snapshot.</summary>
    private static BeatManager LiveManager(RaveOnAirSnapshot snapshot)
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        return beatManager;
    }

    /// <summary>Minimal Effect carrying both supported rhythm configuration forms.</summary>
    private sealed class RhythmTestEffect : EffectBase
    {
        /// <summary>The optional four-bar choreography observed through the same private field shape as Angles.</summary>
        private Routine? routine;

        /// <summary>Allocates the Effect buffer required by the base contract.</summary>
        public RhythmTestEffect()
        {
            buffer = new Color[Penrose.Total];
        }

        /// <summary>Assigns the concrete Effect-owned Routine used by the dashboard regression.</summary>
        /// <param name="value">The exact four-bar Routine the Effect holds.</param>
        public void SetRoutine(Routine value)
        {
            routine = value;
        }

        /// <summary>Returns the held Routine so the test field is ordinary observed state.</summary>
        public Routine? GetRoutine() => routine;

        /// <summary>Returns no additional debug text.</summary>
        public override string DebugText() => string.Empty;

        /// <summary>Performs no deactivation work.</summary>
        public override void OnEnd() { }

        /// <summary>Performs no drawing; this test observes rhythm configuration only.</summary>
        public override void Draw() { }
    }
}
