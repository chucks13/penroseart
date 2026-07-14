// Verifies the canonical Tuning Window navigation and responsive layout contract.
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Specifies the visible Tuning Window navigation and responsive layout contract.</summary>
public sealed class TuningWorkspaceLayoutTests
{
    /// <summary>The canonical workspace exposes only the three focused tabs approved by the ticket.</summary>
    [Test]
    public void CanonicalTabsAreLiveRhythmAndTransitions()
    {
        Assert.That(PenroseTuningWindow.WorkspaceTabs, Is.EqualTo(new[] { "Live", "Rhythm", "Transitions" }));
    }

    /// <summary>A narrow desktop window stacks Transition navigation above its settings.</summary>
    [Test]
    public void NarrowWindowUsesStackedTransitionLayout()
    {
        var layout = PenroseTuningWindow.FlowForWidth(560f);

        Assert.That(layout, Is.EqualTo(TuningWorkspaceFlow.Stacked));
    }

    /// <summary>A wide desktop window keeps Transition navigation and settings side by side.</summary>
    [Test]
    public void WideWindowUsesSplitTransitionLayout()
    {
        var layout = PenroseTuningWindow.FlowForWidth(900f);

        Assert.That(layout, Is.EqualTo(TuningWorkspaceFlow.Split));
    }

    /// <summary>Before Start, the live header counts down Lock, Start, and End from the current beat.</summary>
    [Test]
    public void LiveTimelineFormatsUpcomingTransitionCountdowns()
    {
        var cue = new SwitcherCueStatus(true, false, 117, 2, 1, 113, 114, 119, 3, 2);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            true,
            112,
            12,
            SwitcherCueStatus.Empty,
            cue));

        Assert.That(
            LiveTimelineRenderer.FormatPendingTimingStatus(model.Pending),
            Is.EqualTo("LOCK IN 1 · START IN 2 · END IN 7"));
    }

    /// <summary>The active Transition bar names A, B, and the Transition while counting down to End.</summary>
    [Test]
    public void LiveTimelineFormatsActiveTransitionBar()
    {
        var cue = new SwitcherCueStatus(true, true, 117, 2, 1, 113, 114, 119, 3, 2);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            true,
            115,
            15,
            cue,
            SwitcherCueStatus.Empty));
        var switcher = new SwitcherStatus(
            true,
            -1,
            string.Empty,
            0,
            "Waves",
            1,
            "Fluid",
            0,
            "Fade",
            "Fade",
            0.75f);

        Assert.That(
            LiveTimelineRenderer.FormatActiveTransitionLabel(switcher, model.Active),
            Is.EqualTo("Waves → Fluid · Fade · END IN 4"));
    }

    /// <summary>On the first Runway beat, the live header calls out Start Now before settling into Active.</summary>
    [Test]
    public void LiveTimelineFormatsTransitionStartNow()
    {
        var cue = new SwitcherCueStatus(true, true, 117, 2, 1, 113, 114, 119, 3, 2);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            true,
            114,
            14,
            SwitcherCueStatus.Empty,
            cue));

        Assert.That(
            LiveTimelineRenderer.FormatPendingTimingStatus(model.Pending),
            Is.EqualTo("LOCKED · START NOW · END IN 5"));
    }

    /// <summary>The Live identity comes from the Cue whose timing is rendered, not newly staged Director choices.</summary>
    [Test]
    public void LiveTimelineFormatsIdentityFromTheSwitcherCueSnapshot()
    {
        var gameObject = new GameObject("Live Cue Identity Test");
        var controller = gameObject.AddComponent<Controller>();
        try
        {
            var cue = new SwitcherCueStatus(true, true, 117, 2, 1, 113, 114, 119, 3, 2);

            Assert.That(ControllerStatusText.FormatSwitcherCue(controller, cue), Is.EqualTo("#2 · #1"));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    /// <summary>Narrow and wide Tuning Window layouts survive real Editor repaint events without exceptions.</summary>
    [UnityTest]
    public IEnumerator TuningWindowRendersNarrowAndWideWithoutExceptions()
    {
        var window = ScriptableObject.CreateInstance<PenroseTuningWindow>();
        try
        {
            window.position = new Rect(0f, 0f, 560f, 640f);
            window.Show();
            window.Repaint();
            yield return null;

            window.position = new Rect(0f, 0f, 900f, 640f);
            window.Repaint();
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The actual Rhythm renderer survives stacked and split Editor repaint events.</summary>
    [UnityTest]
    public IEnumerator RhythmDashboardRendersNarrowAndWideWithoutExceptions()
    {
        var window = ScriptableObject.CreateInstance<RhythmDashboardSmokeHost>();
        try
        {
            window.position = new Rect(0f, 0f, 560f, BeatManagerDashboardRenderer.DashboardHeightForWidth(560f));
            window.Show();
            window.Repaint();
            yield return null;

            window.position = new Rect(0f, 0f, 900f, BeatManagerDashboardRenderer.DashboardHeightForWidth(900f));
            window.Repaint();
            yield return null;

            Assert.That(window.RenderCount, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The two-row Live renderer survives narrow and wide Editor repaint events.</summary>
    [UnityTest]
    public IEnumerator LiveTimelineRendersNarrowAndWideWithoutExceptions()
    {
        var window = ScriptableObject.CreateInstance<LiveTimelineSmokeHost>();
        try
        {
            window.position = new Rect(0f, 0f, 360f, 180f);
            window.Show();
            window.Repaint();
            yield return null;

            window.position = new Rect(0f, 0f, 900f, 180f);
            window.Repaint();
            yield return null;

            Assert.That(window.RenderCount, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A visible Rhythm surface keeps repainting across Editor frames instead of waiting for Inspector ticks.</summary>
    [UnityTest]
    public IEnumerator RhythmDashboardMaintainsInteractiveRepaintCadence()
    {
        var window = ScriptableObject.CreateInstance<RhythmDashboardSmokeHost>();
        try
        {
            window.position = new Rect(0f, 0f, 900f, BeatManagerDashboardRenderer.DashboardHeightForWidth(900f));
            window.ContinuousRepaint = true;
            window.Show();
            window.Repaint();
            yield return null;

            var startingRenderCount = window.RenderCount;
            for (var frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            Assert.That(window.RenderCount - startingRenderCount, Is.GreaterThanOrEqualTo(6),
                "The visible Rhythm dashboard stopped repainting between Editor frames.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The compact Controller Inspector survives a real host-window repaint without exceptions.</summary>
    [UnityTest]
    public IEnumerator CompactControllerInspectorRendersWithoutExceptions()
    {
        var gameObject = new GameObject("Controller Inspector Smoke Test");
        var controller = gameObject.AddComponent<Controller>();
        var inspector = Editor.CreateEditor(controller, typeof(ControllerEditor));
        var window = ScriptableObject.CreateInstance<ControllerInspectorSmokeHost>();
        try
        {
            window.Inspector = inspector;
            window.position = new Rect(0f, 0f, 360f, 640f);
            window.Show();
            window.Repaint();
            yield return null;

            Assert.That(window.RenderCount, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            window.Close();
            Object.DestroyImmediate(inspector);
            Object.DestroyImmediate(gameObject);
        }
    }
}

/// <summary>Hosts the compact Controller Inspector inside a real IMGUI EditorWindow for smoke coverage.</summary>
internal sealed class ControllerInspectorSmokeHost : EditorWindow
{
    /// <summary>The Inspector rendered on the next IMGUI event.</summary>
    internal Editor Inspector { get; set; }

    /// <summary>Number of IMGUI repaint/layout events observed by the host.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Renders the assigned Inspector through the same IMGUI call used by Unity's Inspector window.</summary>
    private void OnGUI()
    {
        RenderCount++;
        if (Inspector != null)
        {
            Inspector.OnInspectorGUI();
        }
    }
}

/// <summary>Hosts the responsive Rhythm renderer inside a real IMGUI EditorWindow for smoke coverage.</summary>
internal sealed class RhythmDashboardSmokeHost : EditorWindow
{
    /// <summary>Number of IMGUI repaint/layout events observed by the host.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Whether the host should model a visible live Rhythm surface's continuous repaint loop.</summary>
    internal bool ContinuousRepaint { get; set; }

    /// <summary>Renders explicit Standalone and required-Pool failure facts at the host window's current width.</summary>
    private void OnGUI()
    {
        RenderCount++;
        var error = $"Required Waveform Pool '{WaveformPool.FileName}' contains no Waveforms.";
        var model = BeatManagerDashboardModel.From(null, default, error);
        var selector = new WaveformSelectorView(-1, System.Array.Empty<string>(), error);
        var rect = new Rect(0f, 0f, position.width, BeatManagerDashboardRenderer.DashboardHeightForWidth(position.width));
        BeatManagerDashboardRenderer.Draw(rect, model, selector, default, position.width);
        if (ContinuousRepaint && Event.current.type == EventType.Repaint)
        {
            Repaint();
        }
    }
}

/// <summary>Hosts the real rolling Live renderer for responsive Editor repaint tests.</summary>
internal sealed class LiveTimelineSmokeHost : EditorWindow
{
    /// <summary>Number of IMGUI events observed by the host.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Draws a representative cross-Grid Transition at the host window's current width.</summary>
    private void OnGUI()
    {
        RenderCount++;
        var activeCue = new SwitcherCueStatus(true, true, 101, 1, 0, 99, 100, 105, 1, 4);
        var pendingCue = new SwitcherCueStatus(true, false, 116, 2, 1, 111, 112, 116, 4, 0);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(true, 101, 1, activeCue, pendingCue));
        var switcher = new SwitcherStatus(
            true,
            -1,
            string.Empty,
            0,
            "Waves",
            1,
            "Fluid",
            0,
            "Fade",
            "Fade",
            0.75f);
        LiveTimelineRenderer.Draw(model, switcher, "CrystalGrowth · IrisTransition");
    }
}
