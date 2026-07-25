using System.Collections.Generic;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using RepertoireFlags = Repertoire;

// Execution-seam tests for the plan-driven Switcher (ADR-0020). Immutable Track Cue Sheets are
// handed in, per-player wire snapshots advance execution, and assertions stay on public stage state.
public sealed class SwitcherExecutionTests
{
    private GameObject controllerObject;
    private Controller controller;
    private Switcher switcher;
    private Director director;
    private TimedTransition transition;
    private TimedTransition cueTransition;
    private HardCutTransition hardCutTransition;

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    /// <summary>Builds the real Switcher/Director cycle required by sheet execution.</summary>
    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("SwitcherExecutionTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.logDirectorSwitching = false;
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);

        transition = new TimedTransition();
        cueTransition = new TimedTransition(runwayBeats: 2, tailBeats: 2);
        hardCutTransition = new HardCutTransition();
        var effects = new EffectBase[]
        {
            new SolidEffect(Color.red),
            new SolidEffect(Color.blue),
            new SolidEffect(Color.green),
        };
        var transitions = new TransitionBase[] { transition, hardCutTransition, cueTransition };
        controller.effects = effects;
        controller.transitions = transitions;
        transition.BindController(controller);
        transition.Init();
        hardCutTransition.BindController(controller);
        hardCutTransition.Init();
        cueTransition.BindController(controller);
        cueTransition.Init();
        controller.effectDeck = new[] { 0, 1, 2 };
        controller.transitionDeck = new[] { 0, 1 };
        controller.currentTransition = 0;
        controller.timer = new Timer(controller.effectTime, false);
        switcher = new Switcher(controller, effects, transitions);
        switcher.SetInitialEffect(0, 0);
        controller.switcher = switcher;
        director = new Director(
            controller,
            switcher,
            controller.timer,
            controller.effectDeck,
            controller.transitionDeck,
            controller.currentTransition);
        controller.director = director;
        switcher.BindDirector(director);
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void RenderAtTimeAdvancesTransitionProgressFromStartTiming()
    {
        switcher.StartTransition(1, 0, TransitionStartTiming.FromBeatClock(startTime: 10f, secondsPerBeat: 0.5f));

        var buffer = switcher.RenderAtTime(10.25f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(Color.red, Color.blue, 0.5f)));
    }

    [Test]
    public void RenderPromotesDestinationAfterStartedTransitionDuration()
    {
        switcher.StartTransition(1, 0, TransitionStartTiming.FromDefaultDuration(startTime: 10f));

        var buffer = switcher.RenderAtTime(11.1f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(switcher.Status.CurrentTransitionName, Is.EqualTo(string.Empty));
        Assert.That(switcher.Status.SourceEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    [Test]
    public void ZeroDurationTransitionPromotesDestinationImmediately()
    {
        switcher.StartTransition(1, 1, TransitionStartTiming.FromDefaultDuration(startTime: 10f));

        var buffer = switcher.RenderAtTime(10f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    [Test]
    public void StartTransitionWhileRenderingReplacesMoveFromPreviousTarget()
    {
        switcher.StartTransition(1, 0, TransitionStartTiming.FromDefaultDuration(startTime: 10f));
        switcher.RenderAtTime(10.25f, out _);

        switcher.StartTransition(0, 0, TransitionStartTiming.FromDefaultDuration(startTime: 20f));
        var buffer = switcher.RenderAtTime(20.5f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.SourceEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(0));
        Assert.That(transition.A, Is.EqualTo(1));
        Assert.That(transition.B, Is.EqualTo(0));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(Color.blue, Color.red, 0.5f)));
    }

    #region Sheet execution

    /// <summary>
    /// Pins an on-time Cue Mark: its full Runway begins at zero, Impact lands on the mark, and Tail completes after.
    /// </summary>
    [Test]
    public void ACueFiresSoItsImpactPointLandsOnTheCueMark()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var repertoire = cueTransition.Repertoire;
        var runwayStart = mark.Beat - repertoire.RunwayBeats;
        switcher.Cast(sheet);

        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        var runwayStartTime = Time.time;
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Runway is under way the instant the cast lands.");
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f).Within(0.001f), "An on-time cast runs the full Runway from zero.");

        switcher.RenderAtTime(runwayStartTime + (repertoire.RunwayBeats * 0.5f), out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Impact Point is mid-flight, not completion.");
        Assert.That(
            switcher.Status.TransitionProgress,
            Is.EqualTo(repertoire.ImpactPoint).Within(0.01f),
            "The Impact Point lands exactly on the Cue Mark beat.");

        var buffer = switcher.RenderAtTime(
            runwayStartTime + ((repertoire.RunwayBeats + repertoire.TailBeats) * 0.5f),
            out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1), "The Tail completes after the Impact.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    /// <summary>
    /// Pins late entry inside a Runway: execution starts already progressed while preserving the mark's Impact time.
    /// </summary>
    [Test]
    public void ALateEntryCompressesTheRunwayAndStillLandsImpactOnTheCueMark()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var repertoire = cueTransition.Repertoire;
        var lateBeat = mark.Beat - repertoire.RunwayBeats + 1;
        switcher.Cast(sheet);

        FeedSwitcherFrame(lateBeat, phrases, generation: 1);
        var lateTime = Time.time;
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "A late cast fires rather than being refused.");
        Assert.That(
            switcher.Status.TransitionProgress,
            Is.EqualTo(0.25f).Within(0.001f),
            "The Runway is compressed: the transition is already under way at cast.");

        switcher.RenderAtTime(lateTime + ((mark.Beat - lateBeat) * 0.5f), out _);
        Assert.That(
            switcher.Status.TransitionProgress,
            Is.EqualTo(repertoire.ImpactPoint).Within(0.01f),
            "Compression preserves the Impact on the Cue Mark beat.");
    }

    /// <summary>Pins the zero-duration edge: a hard-cut mark promotes its destination on the mark tick.</summary>
    [Test]
    public void AHardCutMarkPromotesItsDestinationImmediately()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 1);
        var mark = sheet.Marks[0];
        switcher.Cast(sheet);

        FeedSwitcherFrame(mark.Beat, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1), "A zero-duration cast promotes without a render tick.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        var buffer = switcher.RenderAtTime(Time.time, out _);
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    /// <summary>Pins the one firing rule: a late jump performs the last due mark and checks every earlier mark off.</summary>
    [Test]
    public void ALateJumpPerformsTheLastDueMarkAndChecksOffEarlierMarks()
    {
        var phrases = new[] { Phrase(1, 256, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: the long structure produces two marks.");
        var first = sheet.Marks[0];
        var second = sheet.Marks[1];
        switcher.Cast(sheet);

        FeedSwitcherFrame(second.Beat, phrases, generation: 1, loopRolling: true);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(second.EffectIndex), "The later due mark wins.");
        switcher.RenderAtTime(1_000_000f, out _);

        FeedSwitcherFrame(first.Beat, phrases, generation: 1, loopRolling: true);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(second.EffectIndex), "The earlier skipped mark was silently checked off.");
    }

    /// <summary>Pins loop check-offs: rolling backward and re-crossing an already-fired mark does not re-fire it.</summary>
    [Test]
    public void ALoopReCrossingAnAlreadyFiredCueDoesNotRefireIt()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var runwayStart = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(sheet);

        FeedSwitcherFrame(runwayStart, phrases, generation: 1, loopRolling: true);
        switcher.RenderAtTime(1_000_000f, out _);
        FeedSwitcherFrame(runwayStart - 1, phrases, generation: 1, loopRolling: true);
        FeedSwitcherFrame(runwayStart, phrases, generation: 1, loopRolling: true);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex), "Rolling loop motion preserves the fired check-off.");
    }

    /// <summary>
    /// Pins the permanence of a check-off: a back-cue re-crosses an already-fired mark without a rolling
    /// loop, and the arrival still does not re-perform. Loop state is not what protects a fired cue —
    /// nothing does, because a fired cue is simply done for the life of the handover.
    /// </summary>
    [Test]
    public void ABackCueDoesNotReperformAnAlreadyFiredCue()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var runwayStart = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(sheet);

        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);
        FeedSwitcherFrame(runwayStart - 1, phrases, generation: 1);
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex), "The back-cue re-crossing left the check-off standing.");
        Assert.That(switcher.FiredMarks[0], Is.True);
    }

    /// <summary>
    /// Pins the operator override as a performed move: an immediate pick starts a real Transition into
    /// the chosen Effect rather than cutting to it.
    /// </summary>
    [Test]
    public void AnOperatorOverrideStartsATransitionRatherThanCutting()
    {
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Setup: effect zero is on the wall.");

        director.ShowNow(2, controller.effectTime);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "A transition owns the frame, so nothing was cut.");
        Assert.That(switcher.Status.SourceEffectIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0), "The staged card performs the override.");
    }

    /// <summary>
    /// Pins fire-and-forget: an operator override interjects without disturbing the plan in force, so the
    /// sheet stays cast with its check-offs intact and an already-performed mark never fires a second time.
    /// </summary>
    [Test]
    public void AnOperatorOverrideLeavesTheInForceSheetAndItsCheckOffsAlone()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        switcher.Cast(sheet);
        FeedSwitcherFrame(mark.Beat, phrases, generation: 1);
        Assert.That(switcher.FiredMarks[0], Is.True, "Setup: the mark performed.");

        director.ShowNow(2, controller.effectTime);
        // Production frame order: the Director maintains and hands over before the Switcher executes, so
        // a sheet the override had wiped would be rebuilt here and re-cast with cleared check-offs.
        FeedDirectorFrame(mark.Beat, phrases, generation: 1);

        Assert.That(switcher.Sheet.StructureGeneration, Is.EqualTo(1), "The override left the plan in force.");
        Assert.That(switcher.FiredMarks[0], Is.True, "The override did not reset the check-off.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2), "The plan did not snap the wall back off the override.");
    }

    /// <summary>Pins frozen decisions: Hold performs nothing and checks nothing off, so release still performs the mark.</summary>
    [Test]
    public void HoldChecksNothingOffAndTheMarkPerformsAfterRelease()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var runwayStart = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(sheet);
        controller.heldEffect = 0;

        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Hold freezes the Director's answer.");

        controller.heldEffect = -1;
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "Release performs the still-unchecked mark.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark.EffectIndex));
    }

    /// <summary>Pins handover identity: re-casting the same player/generation does not reset fired check-offs.</summary>
    [Test]
    public void RecastingTheSameSheetDoesNotResetCheckOffs()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var runwayStart = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(sheet);
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);

        switcher.Cast(sheet);
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex), "Idempotent handover preserves the fired check-off.");
    }

    /// <summary>Pins staleness reset: a performed plan mark starts a fresh Grid-start window.</summary>
    [Test]
    public void APerformedPlanMarkResetsTheStalenessWindow()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorFrame(focusBeat: 4, phrases, generation: 1);
        var sheet = ExpectedDirectorSheet(generation: 1);
        var mark = sheet.Marks[0];
        FeedGridStarts(3, focusBeat: 4, phrases, generation: 1);

        FeedSwitcherFrame(
            mark.Beat - controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats,
            phrases,
            generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);
        var overriddenEffect = mark.EffectIndex == 1 ? 2 : 1;
        director.SetNextEffect(overriddenEffect);

        FeedGridStarts(3, focusBeat: mark.Beat + 1, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex), "Three Grid starts after the plan mark remain below a reset ceiling.");
        Assert.That(director.DecideCue(mark).EffectIndex, Is.EqualTo(overriddenEffect), "No one-off consumed the pending Director override.");
    }

    /// <summary>Pins staleness ownership: at the Grid ceiling the Switcher asks the Director, whose override decides the one-off.</summary>
    [Test]
    public void StalenessMakesTheSwitcherAskTheDirectorForAOneOff()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorFrame(focusBeat: 4, phrases, generation: 1);
        var sheet = ExpectedDirectorSheet(generation: 1);
        var dealt = sheet.DealAt(4);
        var overriddenEffect = dealt.EffectIndex == 1 ? 2 : 1;
        director.SetNextEffect(overriddenEffect);

        FeedGridStarts(TrackCueSheet.MaximumGapBeats / TrackCueSheet.GridBeats, focusBeat: 4, phrases, generation: 1);

        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(overriddenEffect), "The Director's one-shot answer wins; the Switcher selected nothing.");
        Assert.That(controller.currentTransition, Is.EqualTo(dealt.TransitionIndex));
    }

    /// <summary>Pins the independent Standalone seconds path after sheet execution.</summary>
    [Test]
    public void StandaloneSecondsPathIsUnaffectedBySheetExecution()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 1);
        switcher.Cast(sheet);
        FeedSwitcherFrame(sheet.Marks[0].Beat, phrases, generation: 1);

        switcher.StartTransition(0, 0, TransitionStartTiming.FromDefaultDuration(startTime: 20f));
        var buffer = switcher.RenderAtTime(21.1f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.red));
    }

    #endregion

    /// <summary>Builds a deterministic sheet restricted to one target Effect and Transition for execution assertions.</summary>
    private TrackCueSheet BuildExecutionSheet(StructurePhrase[] phrases, int generation, int transitionIndex)
    {
        FeedWire(focusBeat: 1, phrases, generation, loopRolling: false, gridBeat: 1);
        var structure = controller.beatManager.Players[0].Structure;
        return TrackCueSheet.Build(
            structure,
            new[] { new EffectDescriptor(1, controller.EffectiveRepertoire(1)) },
            new[] { new TransitionDescriptor(transitionIndex, controller.transitions[transitionIndex].Repertoire) },
            generation,
            playerNumber: 1);
    }

    /// <summary>Builds the deterministic full-catalog sheet the Director owns for player one.</summary>
    private TrackCueSheet ExpectedDirectorSheet(int generation)
    {
        return TrackCueSheet.Build(
            controller.beatManager.Players[0].Structure,
            EffectDescriptors(),
            TransitionDescriptors(),
            generation,
            playerNumber: 1);
    }

    /// <summary>Feeds one wire frame and lets only the Switcher execute its already-handed-over sheet.</summary>
    private void FeedSwitcherFrame(
        int focusBeat,
        StructurePhrase[] phrases,
        int generation,
        bool loopRolling = false,
        int? gridBeat = null)
    {
        FeedWire(focusBeat, phrases, generation, loopRolling, gridBeat);
        switcher.Tick();
    }

    /// <summary>Feeds one production-order frame so the Director maintains/hands over before Switcher execution.</summary>
    private void FeedDirectorFrame(int focusBeat, StructurePhrase[] phrases, int generation, int? gridBeat = null)
    {
        FeedWire(focusBeat, phrases, generation, loopRolling: false, gridBeat);
        director.Tick(0f);
        switcher.Tick();
    }

    /// <summary>Feeds on-air Grid starts while holding the sheet player's beat inside the run-in.</summary>
    private void FeedGridStarts(int count, int focusBeat, StructurePhrase[] phrases, int generation)
    {
        for (var i = 0; i < count; i++)
        {
            FeedSwitcherFrame(focusBeat, phrases, generation, gridBeat: 16);
            FeedSwitcherFrame(focusBeat, phrases, generation, gridBeat: 1);
        }
    }

    /// <summary>Translates one player wire snapshot, including the per-player loop lane, into BeatManager values.</summary>
    private void FeedWire(
        int focusBeat,
        StructurePhrase[] phrases,
        int generation,
        bool loopRolling,
        int? gridBeat)
    {
        var onAirGridBeat = gridBeat ?? (((focusBeat - 1) % 16) + 1);
        BeatManagerWireFixture.Feed(controller.beatManager, snapshot =>
        {
            snapshot.beatInBar = ((focusBeat - 1) % 4) + 1;
            snapshot.beat = new BeatPosition { current = focusBeat, total = -1 };
            snapshot.bpm = 120f;
            snapshot.timingGrid = new TimingGrid
            {
                beat = onAirGridBeat,
                bar = ((onAirGridBeat - 1) / 4) + 1,
                state = "locked",
            };
            snapshot.playersLive = "1";
            snapshot.players ??= new PlayerState[RaveWireSnapshot.PlayerCount];
            var player = PlayerState.Unavailable;
            player.clock = new PlayerClock
            {
                bpm = 120f,
                beat = focusBeat,
                bar = ((focusBeat - 1) / 4) + 1,
                beatInBar = ((focusBeat - 1) % 4) + 1,
                beatPulse = 1f,
            };
            player.transport = new PlayerTransport { playing = 1, cued = 0, onAir = 1, master = 1, synced = 1 };
            player.loopState = new LoopState
            {
                active = loopRolling ? 1 : 0,
                set = loopRolling ? 1 : 0,
                lengthBeats = loopRolling ? 16f : 0f,
                lengthMs = loopRolling ? 8_000 : 0,
                sizeNumerator = loopRolling ? 16 : 0,
                sizeDenominator = 1,
            };
            player.structure = new PlayerStructure
            {
                generation = generation,
                trackId = "track" + generation,
                source = "analyzed",
                totalBeats = 512,
                phraseCount = phrases.Length,
                phrases = phrases,
            };
            snapshot.players[0] = player;
        });
        controller.beatManager.Update(0f);
    }

    /// <summary>Builds Effect descriptors in the same catalog order as the Director.</summary>
    private IReadOnlyList<EffectDescriptor> EffectDescriptors()
    {
        var descriptors = new EffectDescriptor[controller.effects.Length];
        for (var i = 0; i < descriptors.Length; i++)
        {
            descriptors[i] = new EffectDescriptor(i, controller.EffectiveRepertoire(i));
        }

        return descriptors;
    }

    /// <summary>Builds Transition descriptors in the same catalog order as the Director.</summary>
    private IReadOnlyList<TransitionDescriptor> TransitionDescriptors()
    {
        var descriptors = new TransitionDescriptor[controller.transitions.Length];
        for (var i = 0; i < descriptors.Length; i++)
        {
            descriptors[i] = new TransitionDescriptor(i, controller.transitions[i].Repertoire);
        }

        return descriptors;
    }

    /// <summary>Creates one structure phrase for deterministic Track Cue Sheet construction.</summary>
    private static StructurePhrase Phrase(int startBeat, int endBeat, string type)
    {
        return new StructurePhrase
        {
            startBeat = startBeat,
            endBeat = endBeat,
            type = type,
            variant = 0,
        };
    }

    private sealed class SolidEffect : EffectBase
    {
        private readonly Color color;

        public SolidEffect(Color color)
        {
            this.color = color;
            buffer = new Color[Penrose.Total];
        }

        public override string DebugText() => string.Empty;

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = color;
            }
        }
    }

    private sealed class HardCutTransition : TransitionBase
    {
        public HardCutTransition()
        {
            buffer = new Color[Penrose.Total];
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats: 0,
                tailBeats: 0,
                TransitionShape.Blend,
                TransitionIntensity.High,
                defaultDurationSeconds: 0f));
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

    private sealed class TimedTransition : TransitionBase
    {
        private readonly int runwayBeats;
        private readonly int tailBeats;

        /// <summary>Creates a blend Transition whose beat-domain shape can pin both rendering and cue timing.</summary>
        public TimedTransition(int runwayBeats = 1, int tailBeats = 0)
        {
            this.runwayBeats = runwayBeats;
            this.tailBeats = tailBeats;
            buffer = new Color[Penrose.Total];
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats,
                tailBeats,
                TransitionShape.Blend,
                TransitionIntensity.Medium,
                defaultDurationSeconds: 1f));
        }

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
            var colors = new[] { Color.red, Color.blue };
            var source = colors[A];
            var target = colors[B];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Color.Lerp(source, target, V);
            }
        }
    }
}
