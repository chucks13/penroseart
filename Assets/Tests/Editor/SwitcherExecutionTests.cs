using NUnit.Framework;
using UnityEngine;
using RepertoireFlags = Repertoire;

public sealed class SwitcherExecutionTests
{
    private GameObject controllerObject;
    private Switcher switcher;
    private TimedTransition transition;
    private HardCutTransition hardCutTransition;

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("SwitcherExecutionTestsController");
        var controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.logDirectorSwitching = false;

        transition = new TimedTransition();
        hardCutTransition = new HardCutTransition();
        var effects = new EffectBase[] { new SolidEffect(Color.red), new SolidEffect(Color.blue) };
        var transitions = new TransitionBase[] { transition, hardCutTransition };
        controller.effects = effects;
        controller.transitions = transitions;
        transition.BindController(controller);
        transition.Init();
        hardCutTransition.BindController(controller);
        hardCutTransition.Init();
        switcher = new Switcher(controller, effects, transitions);
        switcher.SetInitialEffect(0, 0);
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void LoadedCueStartsFromScheduledBeatDomainClock()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(
            currentBeat: 7,
            beatFraction: 0f,
            secondsPerBeat: 0.5f,
            nowSeconds: 10f));

        var buffer = switcher.RenderAtTime(11.25f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(Color.red, Color.blue, 0.5f)));
    }

    /// <summary>Active and Loaded Cue snapshots remain independently observable until execution completes.</summary>
    [Test]
    public void ActiveAndLoadedCueStatusesRemainSeparateThroughExecution()
    {
        var activeCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        switcher.UpsertLoadedCue(activeCue, new SwitcherClockSnapshot(7, 0f, 0.5f, 10f));

        switcher.RenderAtTime(11f, out _);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False);
        Assert.That(switcher.ActiveCueStatus.HasCue, Is.True);
        Assert.That(switcher.ActiveCueStatus.IsLocked, Is.True);
        Assert.That(switcher.ActiveCueStatus.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.ActiveCueStatus.StartBeat, Is.EqualTo(9));
        Assert.That(switcher.ActiveCueStatus.CueMarkBeat, Is.EqualTo(10));
        Assert.That(switcher.ActiveCueStatus.CompleteBeat, Is.EqualTo(10));

        var startModel = LiveTimelineProjection.Build(new LiveTimelineInput(
            true,
            16,
            switcher.ActiveCueStatus,
            switcher.LoadedCueStatus));
        Assert.That(
            LiveTimelineRenderer.FormatActiveTransitionLabel(switcher.Status, startModel.Active),
            Does.Contain("START NOW"),
            "The real pending-to-active handoff must keep the Cue's Start visible.");

        var nextCue = new SwitcherCueDirection(
            cueMarkBeat: 20,
            targetEffectIndex: 0,
            transitionIndex: 1,
            transitionRepertoire: hardCutTransition.Repertoire);
        switcher.UpsertLoadedCue(nextCue, new SwitcherClockSnapshot(9, 0.5f, 0.5f, 11.25f));

        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(0));
        Assert.That(
            switcher.ActiveCueStatus.TargetEffectIndex,
            Is.EqualTo(1),
            "The executing Cue remains independently visible while a later Cue stages.");

        switcher.RenderAtTime(11.5f, out _);

        Assert.That(switcher.ActiveCueStatus.HasCue, Is.False);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(0));
    }

    [Test]
    public void SameMarkOfferBeforeLockIsKeptAndRidesUnchanged()
    {
        // Identity on the seam is the Cue Mark alone: a second offer at the same mark is a keep, so the loaded
        // cue rides and is never re-flavored — even a different target on the same mark does not take.
        var originalCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        var sameMarkOffer = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 0,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        var loadedOriginal = switcher.UpsertLoadedCue(originalCue, new SwitcherClockSnapshot(7, 0f, 0.5f, 10f));
        var keptSameMark = switcher.UpsertLoadedCue(sameMarkOffer, new SwitcherClockSnapshot(7, 0.5f, 0.5f, 10.25f));

        Assert.That(loadedOriginal, Is.EqualTo(CueUpsertResult.Loaded));
        Assert.That(keptSameMark, Is.EqualTo(CueUpsertResult.Kept), "A same-mark offer is a keep, not a replacement.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "The kept cue rides unchanged; it is never re-flavored.");
    }

    [Test]
    public void DifferentMarkOfferBeforeLockReplacesTheLoadedCue()
    {
        var originalCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        var differentMarkCue = new SwitcherCueDirection(
            cueMarkBeat: 12,
            targetEffectIndex: 0,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        var loadedOriginal = switcher.UpsertLoadedCue(originalCue, new SwitcherClockSnapshot(7, 0f, 0.5f, 10f));
        var loadedReplacement = switcher.UpsertLoadedCue(differentMarkCue, new SwitcherClockSnapshot(7, 0.5f, 0.5f, 10.25f));

        Assert.That(loadedOriginal, Is.EqualTo(CueUpsertResult.Loaded));
        Assert.That(loadedReplacement, Is.EqualTo(CueUpsertResult.Loaded), "A different-mark offer before the Lock Point replaces the loaded cue.");
        Assert.That(switcher.LoadedCueStatus.CanUpdate, Is.True);
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(12));
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(0));
    }

    [Test]
    public void DifferentMarkOfferAtLockPointIsRejectedAndTheLoadedCueRides()
    {
        var originalCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        var differentMarkCue = new SwitcherCueDirection(
            cueMarkBeat: 12,
            targetEffectIndex: 0,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        var loadedOriginal = switcher.UpsertLoadedCue(originalCue, new SwitcherClockSnapshot(7, 0f, 0.5f, 10f));
        // Beat 8 is the loaded cue's Lock Point (Runway 1 -> Lock Point 8): it latches the lock, and the
        // different-mark offer arriving with it is refused.
        var rejectedReplacement = switcher.UpsertLoadedCue(differentMarkCue, new SwitcherClockSnapshot(8, 0f, 0.5f, 10.5f));

        Assert.That(loadedOriginal, Is.EqualTo(CueUpsertResult.Loaded));
        Assert.That(rejectedReplacement, Is.EqualTo(CueUpsertResult.Rejected), "Once locked, a different-mark offer is rejected and the cue rides.");
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True);
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(10));
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1));
    }

    [Test]
    public void LoadedCueLocksByClockAdvanceAndStaysLockedThroughABackstepOffer()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        // Runway 1 -> Start Beat 9, Lock Point 8. Offered on beat 6 (before the Lock Point) it loads and holds.
        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(6, 0f, 0.5f, 10f));
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.False);

        // Advance ONLY the Switcher's own render clock to the Lock Point's wall time (Start Time 11.5s minus
        // one 0.5s beat = 11.0s). No new offer arrives, yet the lock latches — the status reads real state.
        switcher.RenderAtTime(11.0f, out _);
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True, "The lock latches on the Switcher's own clock, without any new offer.");

        // A jitter backstep offer at a different mark, on a beat before the Lock Point, must not reopen the
        // one-way latch.
        var backstep = new SwitcherCueDirection(
            cueMarkBeat: 12,
            targetEffectIndex: 0,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        var answer = switcher.UpsertLoadedCue(backstep, new SwitcherClockSnapshot(6, 0f, 0.5f, 11.0f));

        Assert.That(answer, Is.EqualTo(CueUpsertResult.Rejected), "A different-mark offer after lock is rejected even on a backstep beat.");
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True, "The lock is a one-way latch; a backstep cannot reopen it.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(10), "The locked cue rides.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "The locked cue rides.");
    }

    [Test]
    public void ZeroRunwayHardCutLoadedCuePromotesDestinationOnCueMark()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 1,
            transitionRepertoire: hardCutTransition.Repertoire);

        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(8, 0f, 0.5f, 10f));
        var buffer = switcher.RenderAtTime(11f, out _);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    [Test]
    public void CueArrivingAtOrPastItsLockPointIsRejected()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        // Runway 1 puts the Lock Point on beat 8: a cue arriving on it is already too late.
        var answer = switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(8, 0f, 0.5f, 10f));
        var buffer = switcher.RenderAtTime(12f, out _);

        Assert.That(answer, Is.EqualTo(CueUpsertResult.Rejected), "A cue at its Lock Point is rejected outright.");
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.red));
    }

    [Test]
    public void UpsertAnswersAcceptedWhenCueCommitsBeforeItsLockPoint()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        var answer = switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(7, 0f, 0.5f, 10f));

        Assert.That(answer, Is.EqualTo(CueUpsertResult.Loaded));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1));
    }

    [Test]
    public void RejectedLateOfferLeavesTheStageAndLoadedCueUntouched()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        // Runway 1 -> Lock Point on beat 8: an offer on the Lock Point is rejected and nothing loads.
        var answer = switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(8, 0f, 0.5f, 10f));

        Assert.That(answer, Is.EqualTo(CueUpsertResult.Rejected));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "A rejected offer leaves no loaded cue.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "A rejected offer leaves the stage untouched.");
    }

    [TestCase(12, 0)]
    [TestCase(0, 12)]
    [TestCase(6, 6)]
    [TestCase(1, 11)]
    [TestCase(11, 1)]
    [TestCase(0, 0)]
    public void CommitWindowClosesOneBeatBeforeTheRunwayStart(int runwayBeats, int tailBeats)
    {
        var repertoire = RepertoireFor(runwayBeats, tailBeats);
        var lockPointBeat = 609 - runwayBeats - 1;

        Assert.That(Switcher.CanCommitCue(609, repertoire, lockPointBeat - 1), Is.True);
        Assert.That(Switcher.CanCommitCue(609, repertoire, lockPointBeat), Is.False, "The Lock Point itself is too late to commit.");
        Assert.That(Switcher.CanCommitCue(609, repertoire, 609 - runwayBeats), Is.False);
        Assert.That(Switcher.CanCommitCue(609, repertoire, 609), Is.False, "The Cue Mark is far too late to commit.");
    }

    [TestCase(12, 0)]
    [TestCase(0, 12)]
    [TestCase(6, 6)]
    [TestCase(1, 11)]
    [TestCase(11, 1)]
    [TestCase(0, 0)]
    public void LoadedCueStatusReportsTheProjectedRunwayAndTailBeats(int runwayBeats, int tailBeats)
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 609,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: RepertoireFor(runwayBeats, tailBeats));

        // Offer well before the Lock Point and before its Start Time so the cue loads and holds.
        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(609 - runwayBeats - 10, 0f, 0.5f, 0f));
        var status = switcher.LoadedCueStatus;

        Assert.That(status.StartBeat, Is.EqualTo(609 - runwayBeats));
        Assert.That(status.CueMarkBeat, Is.EqualTo(609));
        Assert.That(status.CompleteBeat, Is.EqualTo(609 + tailBeats));
        Assert.That(status.LockPointBeat, Is.EqualTo(609 - runwayBeats - 1));
    }

    private static TransitionRepertoire RepertoireFor(int runwayBeats, int tailBeats)
    {
        return TransitionRepertoire.FromRunwayAndTail(
            RepertoireFlags.None,
            runwayBeats,
            tailBeats,
            TransitionShape.Blend,
            TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
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
        public TimedTransition()
        {
            buffer = new Color[Penrose.Total];
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats: 1,
                tailBeats: 0,
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
