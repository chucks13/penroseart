// Verifies the canonical Tuning Window navigation and responsive layout contract.
using NUnit.Framework;

/// <summary>Specifies the visible Tuning Window navigation and responsive layout contract.</summary>
public sealed class TuningWorkspaceLayoutTests
{
    /// <summary>The canonical workspace exposes only the three focused tabs approved by the ticket.</summary>
    [Test]
    public void CanonicalTabsAreLiveRhythmAndTransitions()
    {
        Assert.That(TuningWorkspaceLayout.Tabs, Is.EqualTo(new[] { "Live", "Rhythm", "Transitions" }));
    }

    /// <summary>A narrow desktop window stacks Transition navigation above its settings.</summary>
    [Test]
    public void NarrowWindowUsesStackedTransitionLayout()
    {
        var layout = TuningWorkspaceLayout.ForWidth(560f);

        Assert.That(layout, Is.EqualTo(TuningWorkspaceFlow.Stacked));
    }

    /// <summary>A wide desktop window keeps Transition navigation and settings side by side.</summary>
    [Test]
    public void WideWindowUsesSplitTransitionLayout()
    {
        var layout = TuningWorkspaceLayout.ForWidth(900f);

        Assert.That(layout, Is.EqualTo(TuningWorkspaceFlow.Split));
    }

}
