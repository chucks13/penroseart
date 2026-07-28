using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DirectorStagingTests
{
    private GameObject controllerObject;
    private Controller controller;
    private Switcher switcher;
    private Director director;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("DirectorStagingTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.effects = new EffectBase[] { new TestEffect(), new TestEffect(), new TestEffect() };
        controller.transitions = new TransitionBase[] { new TestTransition(), new TestTransition(), new TestTransition() };
        foreach (var transition in controller.transitions)
        {
            transition.BindController(controller);
            transition.Init();
        }

        controller.effectDeck = new[] { 1, 2, 0 };
        controller.transitionDeck = new[] { 1, 2, 0 };
        controller.currentTransition = 0;
        controller.timer = new Timer(controller.effectTime, false);

        switcher = new Switcher(controller, controller.effects, controller.transitions);
        switcher.SetInitialEffect(0, controller.currentTransition);
        controller.switcher = switcher;

        director = new Director(
            controller,
            switcher,
            controller.timer,
            controller.effectDeck,
            controller.transitionDeck,
            controller.currentTransition);
        controller.director = director;
        // Pin the run salt (ADR-0008) so every dealt card is deterministic across test runs.
        director.SheetSalt = 0;
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        UnityEngine.Object.DestroyImmediate(controllerObject);
    }

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    /// <summary>Disabled sequencing diagnostics never resolve their deferred display reads.</summary>
    [Test]
    public void DisabledDirectorTraceDoesNotResolveItsMessage()
    {
        var resolved = false;

        controller.LogDirectorSwitching(() =>
        {
            resolved = true;
            return "unused";
        });

        Assert.That(resolved, Is.False);
    }

    /// <summary>
    /// Pins the seam the keyboard's Shift+letter staging goes through: a catalog index stages, and anything
    /// else is rejected rather than quietly staged. The keyboard bank walks past the end of shorter catalogs,
    /// so its call site guards on the same range before asking.
    /// </summary>
    [Test]
    public void SetNextEffectStagesACatalogIndexAndRejectsAnythingElse()
    {
        director.SetNextEffect(2);
        Assert.That(director.Status.NextEffectIndex, Is.EqualTo(2), "A valid index stages the Director's next Effect.");

        Assert.That(() => director.SetNextEffect(controller.effects.Length), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => director.SetNextEffect(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(director.Status.NextEffectIndex, Is.EqualTo(2), "A rejected request leaves the staged Effect alone.");
    }

    [Test]
    public void ConstructorStagesNextEffectAndNextTransition()
    {
        var status = director.Status;

        Assert.That(status.NextEffectIndex, Is.EqualTo(1));
        Assert.That(status.NextTransitionIndex, Is.EqualTo(0));
        Assert.That(status.NextEffectIndex, Is.Not.EqualTo(switcher.CurrentEffectIndex));
    }

    [Test]
    public void StandaloneMoveConsumesStagedNextEffectAndNextTransition()
    {
        director.SetNextEffect(2);
        director.SetNextTransition(2);

        director.OnTimerFinished();

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(2));
        Assert.That(director.Status.NextEffectIndex, Is.Not.EqualTo(2));
        Assert.That(director.Status.NextTransitionIndex, Is.EqualTo(1));
    }

    [Test]
    public void ManualSelectionsAreOneShotWhenHoldSelectedIsOff()
    {
        director.SetNextEffect(2);
        director.SetNextTransition(2);

        director.OnTimerFinished();
        RenderTransitionPastCompletion();

        Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(switcher.CurrentEffectIndex, Is.EqualTo(2));
        Assert.That(director.Status.NextEffectIndex, Is.Not.EqualTo(2));
        Assert.That(director.Status.NextTransitionIndex, Is.EqualTo(1));
    }

    [Test]
    public void HoldSelectedTransitionKeepsSelectedTransitionStagedAfterCompletion()
    {
        director.SetNextTransition(2);
        director.SetHoldSelectedTransition(true);

        director.OnTimerFinished();
        RenderTransitionPastCompletion();

        Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(director.Status.HoldSelectedTransition, Is.True);
        Assert.That(director.Status.NextTransitionIndex, Is.EqualTo(2));
    }

    [Test]
    public void HoldSelectedEffectKeepsSelectedEffectStagedAfterCompletion()
    {
        director.SetNextEffect(2);
        director.SetHoldSelectedEffect(true);

        director.OnTimerFinished();
        RenderTransitionPastCompletion();

        Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(director.Status.HoldSelectedEffect, Is.True);
        Assert.That(director.Status.NextEffectIndex, Is.EqualTo(2));
    }

    private void RenderTransitionPastCompletion()
    {
        switcher.RenderAtTime(Time.time + 10f, out _);
    }

    private sealed class TestEffect : EffectBase
    {
        public TestEffect()
        {
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
        }
    }

    private sealed class TestTransition : TransitionBase
    {
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
