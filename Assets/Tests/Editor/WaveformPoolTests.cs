#nullable enable

using System;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>Behavior tests for the Waveform Pool text codec and its non-logging authoring diagnostics.</summary>
public sealed class WaveformPoolTests
{
    /// <summary>Verifies codec and notation defects are returned together without writing temporary warnings.</summary>
    [Test]
    public void ParseWithDiagnostics_ReturnsNumericAndNotationDefectsWithoutLogging()
    {
        const string text = "DEFINE_WAVEFORM(test){ QQQQ | 89x8 | nope | 0 }";

        var entries = WaveformPool.Parse(text, out var diagnostics);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].waveform.IsMalformed, Is.True);
        Assert.That(diagnostics, Has.Length.EqualTo(3));
        Assert.That(diagnostics[0], Does.Contain("unparseable rounding"));
        Assert.That(diagnostics[1], Does.Contain("'9'").And.Contain("0–8"));
        Assert.That(diagnostics[2], Does.Contain("'x'").And.Contain("0–8"));
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>Verifies canonical serialization round-trips names, notation, and stored scalar precision.</summary>
    [Test]
    public void SerializeThenParse_RoundTripsCanonicalEntries()
    {
        var source = new WaveformPool.Entry[]
        {
            new("first", Waveform.Parse("QQQQ", "8888", 0.333f, 0.125f, out _)),
            new("first", Waveform.Parse("EEEEEEEE", "80808080", 0.5f, -0.25f, out _)),
        };

        var serialized = WaveformPool.Serialize(source);
        var parsed = WaveformPool.Parse(serialized, out var diagnostics);

        Assert.That(diagnostics, Is.Empty);
        Assert.That(parsed, Has.Count.EqualTo(2));
        Assert.That(parsed[0].name, Is.EqualTo("first"));
        Assert.That(parsed[1].name, Is.EqualTo("first"), "Display names intentionally need not be unique.");
        Assert.That(parsed[0].waveform.sequence, Is.EqualTo("QQQQ"));
        Assert.That(parsed[0].waveform.rounding, Is.EqualTo(0.333f));
        Assert.That(parsed[0].waveform.offset, Is.EqualTo(0.125f));
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>Verifies names that would alter macro structure cannot reach the full-file serializer.</summary>
    [TestCase("")]
    [TestCase("bad)name")]
    [TestCase("bad|name")]
    [TestCase("bad\nname")]
    public void Serialize_RejectsUnsafeNames(string name)
    {
        var entries = new[]
        {
            new WaveformPool.Entry(name, Waveform.Parse("QQQQ", "8888", 0.3f, 0f, out _)),
        };

        Assert.Throws<ArgumentException>(() => WaveformPool.Serialize(entries));
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>Verifies an incomplete macro is diagnosed rather than silently treated as an empty document.</summary>
    [Test]
    public void ParseWithDiagnostics_IncompleteMacroReportsDataLossBoundary()
    {
        var entries = WaveformPool.Parse("DEFINE_WAVEFORM(broken){ QQQQ | 8888", out var diagnostics);

        Assert.That(entries, Is.Empty);
        Assert.That(diagnostics, Has.Length.EqualTo(1));
        Assert.That(diagnostics[0], Does.Contain("Malformed DEFINE_WAVEFORM").And.Contain("Stopping parse"));
        LogAssert.NoUnexpectedReceived();
    }
}
