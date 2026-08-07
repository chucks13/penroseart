// Verifies the canonical Tuning Window navigation and responsive layout contract.
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Specifies the visible Tuning Window navigation and responsive layout contract.</summary>
public sealed class TuningWorkspaceLayoutTests
{
    /// <summary>The canonical workspace exposes live, rhythm, Transition, and Effect authoring tabs.</summary>
    [Test]
    public void CanonicalTabsIncludeEffects()
    {
        Assert.That(
            PenroseTuningWindow.WorkspaceTabs,
            Is.EqualTo(new[] { "Live", "Rhythm", "Transitions", "Effects" }));
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
    /// <summary>Four known Pool entries used to exercise every storyboard card.</summary>
    private static readonly WaveformPool.Entry[] Entries =
    {
        new("One", Waveform.Parse("QQQQ", "2222")),
        new("Two", Waveform.Parse("QQQQ", "4444")),
        new("Three", Waveform.Parse("QQQQ", "6666")),
        new("Four", Waveform.Parse("QQQQ", "8888")),
    };

    /// <summary>Popup labels matching <see cref="Entries"/>.</summary>
    private static readonly string[] Names = { "One", "Two", "Three", "Four" };

    /// <summary>Number of IMGUI repaint/layout events observed by the host.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Whether the host should model a visible live Rhythm surface's continuous repaint loop.</summary>
    internal bool ContinuousRepaint { get; set; }

    /// <summary>Renders the usable Waveform and Routine previews at the host window's current width.</summary>
    private void OnGUI()
    {
        RenderCount++;
        var model = BeatManagerDashboardModel.From(null, Entries[0].waveform, "");
        var selector = new WaveformSelectorView(0, Names, "");
        var storyboard = RoutineStoryboardView.From(
            Entries,
            RoutineStoryboardSelection.Default(Entries.Length),
            poolError: "",
            gridBar: 3,
            gridProgress: 0.5f);
        var rect = new Rect(0f, 0f, position.width, BeatManagerDashboardRenderer.DashboardHeightForWidth(position.width));
        BeatManagerDashboardRenderer.Draw(
            rect,
            model,
            selector,
            Entries[0].waveform,
            storyboard,
            Names,
            position.width);
        if (ContinuousRepaint && Event.current.type == EventType.Repaint)
        {
            Repaint();
        }
    }
}
