#nullable enable

using System.Reflection;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Pins the single-writer publish from <see cref="Director.Tick"/> into the BeatManager facade: every frame
/// the Director mirrors its Grid verdict — including "off the grid" the instant the clock is gone — so
/// effects read the live Grid through <see cref="BeatManager.Grid"/> without reaching into the Switching
/// layer. Asserts the wiring, not GridSync's internal grid math (covered by GridSyncTests).
/// </summary>
public sealed class DirectorGridPublishTests
{
    private GameObject controllerObject = null!;
    private Controller controller = null!;
    private Switcher switcher = null!;
    private Director director = null!;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("DirectorGridPublishTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.logDirectorSwitching = false;
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);
        controller.effects = new EffectBase[] { new TestEffect(), new TestEffect(), new TestEffect() };
        controller.transitions = new TransitionBase[] { new TestTransition(), new TestTransition() };
        foreach (var transition in controller.transitions)
        {
            transition.BindController(controller);
            transition.Init();
        }

        controller.effectDeck = new[] { 1, 2, 0 };
        controller.transitionDeck = new[] { 0 };
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
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void SyncedTickPublishesTheLiveGridToTheFacade()
    {
        // Synced beat with no phrase: GridSync lines the grid up on the running beat (offset 0), so the
        // 1..16 Count is beat-mod-16. Beat 5 -> Count 5. The point is the wiring: a real position reaches
        // the facade after Tick.
        SetSyncedBeat(5);

        director.Tick(0f);

        var grid = controller.beatManager.Grid;
        Assert.That(grid, Is.Not.Null);
        Assert.That(grid!.Value.Count, Is.EqualTo(5));
    }

    [Test]
    public void StandaloneTickPublishesNoneSoTheFacadeReadsOffTheGrid()
    {
        // Prime a real Grid, then drop the clock. The standalone branch must republish None the same frame
        // so a stale synced reading never lingers on the facade.
        SetSyncedBeat(5);
        director.Tick(0f);
        Assert.That(controller.beatManager.Grid, Is.Not.Null);

        SetStandalone();
        director.Tick(0f);

        Assert.That(controller.beatManager.Grid, Is.Null);
    }

    [Test]
    public void SyncedWithoutAnAbsoluteBeatPublishesNone()
    {
        // Synced mode (beatInBar present) but no absolute beat is limbo: no grid position is derivable, so
        // the facade reads off the grid rather than holding a stale count.
        SetSyncedBeat(7);
        director.Tick(0f);
        Assert.That(controller.beatManager.Grid, Is.Not.Null);

        // Drop only the absolute beat; beatInBar stays >= 1 so IsSyncedMode is still true.
        controller.beatManager.beatData.snapshot.beat = new BeatPosition { current = -1, total = -1 };
        director.Tick(0f);

        Assert.That(controller.beatManager.Grid, Is.Null);
    }

    /// <summary>Seeds a synced, absolute-beat transport with no phrase data (so GridSync uses its beat-grid fallback).</summary>
    private void SetSyncedBeat(int beat)
    {
        var snapshot = controller.beatManager.beatData.snapshot;
        snapshot.bpm = 120f;
        snapshot.beat = new BeatPosition { current = beat, total = -1 };
        snapshot.beatInBar = ((beat - 1) % 4) + 1;
    }

    /// <summary>Drops the 4-count tick so the manager leaves synced mode (Standalone).</summary>
    private void SetStandalone()
    {
        controller.beatManager.beatData.snapshot.beatInBar = -1;
        controller.beatManager.beatData.snapshot.beat = new BeatPosition { current = -1, total = -1 };
    }

    private static void SetControllerSingleton(Controller? instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            !.SetValue(null, instance);
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
