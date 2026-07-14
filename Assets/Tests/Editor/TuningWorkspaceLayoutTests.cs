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

    /// <summary>Follow Director moves the authoring target while preserving the last pinned choice.</summary>
    [Test]
    public void FollowDirectorTracksStagedTransitions()
    {
        var selection = TransitionAuthoringSelection.Restore(
                TransitionSelectionMode.FollowDirector,
                authoringIndex: 0,
                pinnedIndex: 0,
                catalogCount: 3)
            .SetMode(TransitionSelectionMode.FollowDirector, directorNextIndex: 1, catalogCount: 3)
            .ObserveDirector(directorNextIndex: 2, catalogCount: 3);

        Assert.That(selection.Mode, Is.EqualTo(TransitionSelectionMode.FollowDirector));
        Assert.That(selection.AuthoringIndex, Is.EqualTo(2));
        Assert.That(selection.PinnedIndex, Is.EqualTo(0));
    }

    /// <summary>Pin Selection freezes the authoring target while Director observation continues separately.</summary>
    [Test]
    public void PinSelectionRemainsStableAcrossDirectorChanges()
    {
        var selection = TransitionAuthoringSelection.Restore(
                TransitionSelectionMode.FollowDirector,
                authoringIndex: 0,
                pinnedIndex: 0,
                catalogCount: 3)
            .SetMode(TransitionSelectionMode.FollowDirector, directorNextIndex: 1, catalogCount: 3)
            .SetMode(TransitionSelectionMode.PinSelection, directorNextIndex: 1, catalogCount: 3)
            .ObserveDirector(directorNextIndex: 2, catalogCount: 3);

        Assert.That(selection.Mode, Is.EqualTo(TransitionSelectionMode.PinSelection));
        Assert.That(selection.AuthoringIndex, Is.EqualTo(1));
        Assert.That(selection.PinnedIndex, Is.EqualTo(1));
    }

    /// <summary>An explicit pinned choice changes only the authoring selection, not any live runtime choice.</summary>
    [Test]
    public void ExplicitPinnedChoiceUpdatesTheAuthoringTarget()
    {
        var selection = TransitionAuthoringSelection.Restore(
                TransitionSelectionMode.FollowDirector,
                authoringIndex: 0,
                pinnedIndex: 0,
                catalogCount: 3)
            .SetMode(TransitionSelectionMode.PinSelection, directorNextIndex: 2, catalogCount: 3)
            .SelectPinned(authoringIndex: 2, catalogCount: 3);

        Assert.That(selection.AuthoringIndex, Is.EqualTo(2));
        Assert.That(selection.PinnedIndex, Is.EqualTo(2));
    }

    /// <summary>Before Start, the live header counts down Lock, Start, and End from the current beat.</summary>
    [Test]
    public void LiveTimelineFormatsUpcomingTransitionCountdowns()
    {
        var cue = new SwitcherCueStatus(true, false, 117, 2, 1, 113, 114, 119, 3, 2);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            true,
            12,
            SwitcherCueStatus.Empty,
            cue));

        Assert.That(
            LiveTimelineRenderer.FormatPendingTimingStatus(model.Pending),
            Is.EqualTo("LOCK IN 1 · START IN 2 · END IN 7"));
    }

    /// <summary>The Live header keeps the next Cue visible across the full Cue Sheet gap.</summary>
    [TestCase(64, "NEXT CUE IN 64 BEATS")]
    [TestCase(1, "NEXT CUE IN 1 BEAT")]
    [TestCase(0, "NEXT CUE NOW")]
    [TestCase(null, "NEXT CUE —")]
    public void LiveTimelineFormatsNextCueCountdown(int? beatsUntil, string expected)
    {
        Assert.That(LiveTimelineRenderer.FormatNextCueCountdown(beatsUntil), Is.EqualTo(expected));
    }

    /// <summary>The active Transition bar names A, B, and the Transition while counting down to End.</summary>
    [Test]
    public void LiveTimelineFormatsActiveTransitionBar()
    {
        var cue = new SwitcherCueStatus(true, true, 117, 2, 1, 113, 114, 119, 3, 2);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            true,
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

    /// <summary>At the pending-to-active handoff, the executing Transition retains the Start Now callout.</summary>
    [Test]
    public void LiveTimelineFormatsActiveTransitionStartNow()
    {
        var cue = new SwitcherCueStatus(true, true, 117, 2, 1, 113, 114, 119, 3, 2);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            true,
            14,
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
            0f);

        Assert.That(
            LiveTimelineRenderer.FormatActiveTransitionLabel(switcher, model.Active),
            Is.EqualTo("Waves → Fluid · Fade · START NOW · END IN 5"));
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
            window.position = new Rect(0f, 0f, 360f, 240f);
            window.Show();
            window.Repaint();
            yield return null;

            window.position = new Rect(0f, 0f, 900f, 240f);
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
        var pendingCue = new SwitcherCueStatus(true, false, 117, 2, 1, 112, 113, 117, 4, 0);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(true, 1, activeCue, pendingCue, 16));
        var switcher = new SwitcherStatus(
            true,
            -1,
            string.Empty,
            0,
            "ChromaticInterferenceWaves",
            1,
            "FluidVoronoiConstellation",
            0,
            "IrisTransitionWithLongDescriptiveName",
            "IrisTransitionWithLongDescriptiveName",
            0.75f);
        LiveTimelineRenderer.Draw(
            model,
            switcher,
            "RecursiveCrystalGrowthWithLongDescriptiveName · KaleidoscopicIrisTransition");
    }
}
