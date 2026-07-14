// Verifies the shared scalar interpolation and range-mapping extensions.
using NUnit.Framework;

/// <summary>Verifies the shared scalar interpolation and range-mapping vocabulary.</summary>
public sealed class ExtensionMethodsTests
{
    /// <summary>Lerp treats its receiver as the normalized interpolation amount.</summary>
    [Test]
    public void LerpUsesReceiverAsAmount()
    {
        Assert.That(0.25f.Lerp(10f, 20f), Is.EqualTo(12.5f));
        Assert.That((-1f).Lerp(10f, 20f), Is.EqualTo(10f));
        Assert.That(2f.Lerp(10f, 20f), Is.EqualTo(20f));
    }

    /// <summary>Remap is unclamped by default and can clamp at the call site.</summary>
    [Test]
    public void RemapClampingIsOptional()
    {
        Assert.That(15f.Remap(0f, 10f, 0f, 100f), Is.EqualTo(150f));
        Assert.That(15f.Remap(0f, 10f, 0f, 100f, clamp: true), Is.EqualTo(100f));
    }

    /// <summary>Remap supports descending input and output ranges.</summary>
    [Test]
    public void RemapSupportsDescendingRanges()
    {
        Assert.That(0.5f.Remap(1f, 0f, 0f, 10f), Is.EqualTo(5f));
        Assert.That(0.5f.Remap(0f, 1f, 10f, 0f), Is.EqualTo(5f));
    }

    /// <summary>A source range with no width resolves to the output range's first value.</summary>
    [Test]
    public void RemapZeroWidthInputReturnsOutputStart()
    {
        Assert.That(5f.Remap(1f, 1f, 20f, 30f), Is.EqualTo(20f));
    }
}
