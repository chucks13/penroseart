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

    /// <summary>Renders explicit Standalone and required-Pool failure facts at the host window's current width.</summary>
    private void OnGUI()
    {
        RenderCount++;
        var error = $"Required Waveform Pool '{WaveformPool.FileName}' contains no Waveforms.";
        var model = BeatManagerDashboardModel.From(null, default, error);
        var selector = new WaveformSelectorView(-1, System.Array.Empty<string>(), error);
        var rect = new Rect(0f, 0f, position.width, BeatManagerDashboardRenderer.DashboardHeightForWidth(position.width));
        BeatManagerDashboardRenderer.Draw(rect, model, selector, default, position.width);
    }
}
