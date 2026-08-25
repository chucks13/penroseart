#nullable enable

using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Unit tests for the Waveform sampling kernel (<see cref="Waveform.Sample"/>) and the
/// <see cref="BeatManager"/> Bar Phase clock that drives it live.
/// </summary>
/// <remarks>
/// These pin the symmetric nearest-beat model: brightness peaks (1) on the beat and falls to 0 at the
/// midpoint between beats, with Amplitude 0 gating a beat to silence. The same <see cref="Waveform.Sample"/>
/// is what the property drawer plots, so the values asserted here are the contract the visualization must
/// match — by construction it cannot drift from runtime.
///
/// The Bar Phase assertions are anchored to the same seeded-clock values (120 BPM, <c>beatAverageMs = 500</c>)
/// asserted by <c>BeatManagerRaveOscIntegrationTests</c> via <see cref="BeatClockFixture"/>, so a regression
/// in the Bar Phase derivation trips both suites together.
/// </remarks>
public sealed class WaveformTests
{
    private const float Tol = 0.0001f;

    // --- Beat Pulse: the canonical QQQQ / 8888 ---

    [Test]
    public void BeatPulse_PeaksOnEveryBeat()
    {
        var wf = Waveform.Parse("QQQQ", "8888");
        // Four beats sit at bar fractions 0, 0.25, 0.5, 0.75 — each a full-height peak.
        Assert.That(wf.Sample(0f), Is.EqualTo(1f).Within(Tol));
        Assert.That(wf.Sample(0.25f), Is.EqualTo(1f).Within(Tol));
        Assert.That(wf.Sample(0.5f), Is.EqualTo(1f).Within(Tol));
        Assert.That(wf.Sample(0.75f), Is.EqualTo(1f).Within(Tol));
    }

    [Test]
    public void BeatPulse_TroughsAtMidpointBetweenBeats()
    {
        var wf = Waveform.Parse("QQQQ", "8888");
        // Midpoints sit halfway between adjacent beats: 0.125, 0.375, 0.625, 0.875.
        Assert.That(wf.Sample(0.125f), Is.EqualTo(0f).Within(Tol));
        Assert.That(wf.Sample(0.375f), Is.EqualTo(0f).Within(Tol));
        Assert.That(wf.Sample(0.625f), Is.EqualTo(0f).Within(Tol));
        Assert.That(wf.Sample(0.875f), Is.EqualTo(0f).Within(Tol));
    }

    [Test]
    public void Sample_WrapsBarPhaseOutsideUnitInterval()
    {
        var wf = Waveform.Parse("QQQQ", "8888");
        // Bar Phase 1.0 is the next downbeat == phase 0; 1.25 == 0.25.
        Assert.That(wf.Sample(1f), Is.EqualTo(wf.Sample(0f)).Within(Tol));
        Assert.That(wf.Sample(1.25f), Is.EqualTo(wf.Sample(0.25f)).Within(Tol));
    }

    // --- Amplitude 0 is the gate (no separate rest token) ---

    [Test]
    public void MeasureStart_SilencesBeatsTwoThreeFour()
    {
        var wf = Waveform.Parse("QQQQ", "8000"); // only beat 1 sounds
        Assert.That(wf.Sample(0f), Is.EqualTo(1f).Within(Tol));    // beat 1 peak
        Assert.That(wf.Sample(0.25f), Is.EqualTo(0f).Within(Tol)); // beat 2 gated
        Assert.That(wf.Sample(0.5f), Is.EqualTo(0f).Within(Tol));  // beat 3 gated
        Assert.That(wf.Sample(0.75f), Is.EqualTo(0f).Within(Tol)); // beat 4 gated
    }

    [Test]
    public void Alternating_PeaksOnBeatsOneAndThreeOnly()
    {
        var wf = Waveform.Parse("QQQQ", "8080");
        Assert.That(wf.Sample(0f), Is.EqualTo(1f).Within(Tol));    // beat 1 peak
        Assert.That(wf.Sample(0.25f), Is.EqualTo(0f).Within(Tol)); // beat 2 gated
        Assert.That(wf.Sample(0.5f), Is.EqualTo(1f).Within(Tol));  // beat 3 peak
        Assert.That(wf.Sample(0.75f), Is.EqualTo(0f).Within(Tol)); // beat 4 gated
    }

    // --- Rounding reshapes the peak; the trough is always 0 ---

    [Test]
    public void Rounding_Zero_IsLinearTriangle()
    {
        var wf = Waveform.Parse("QQQQ", "8888", 0f, 0f);
        // Halfway from beat 0 (phase 0) to its trough (phase 0.125) is phase 0.0625 → distance t=0.5 → 1-0.5.
        Assert.That(wf.Sample(0.0625f), Is.EqualTo(0.5f).Within(Tol));
    }

    [Test]
    public void Rounding_Full_HoldsFlatTopButTroughStillZero()
    {
        var wf = Waveform.Parse("QQQQ", "8888", 1f, 0f);
        // Flat top: 80% of the way to the trough (t=0.8, inside the 0.85 plateau) is still pinned at 1.
        Assert.That(wf.Sample(0.1f), Is.EqualTo(1f).Within(Tol));
        // The trough still bottoms out at 0 regardless of rounding — rounding never lifts the floor.
        Assert.That(wf.Sample(0.125f), Is.EqualTo(0f).Within(Tol));
    }

    // --- Phase offset slides the whole Waveform without changing its shape ---

    [Test]
    public void Offset_HalfBeat_LandsThePeakOnTheOffbeat()
    {
        var wf = Waveform.Parse("QQQQ", "8888", Waveform.BeatPulseRounding, 0.5f);
        // A 0.5-beat offset == 0.125 bar fraction: the peak that was on the downbeat now lands at phase 0.125.
        Assert.That(wf.Sample(0.125f), Is.EqualTo(1f).Within(Tol));
        // And the downbeat itself now sits in a trough (between the wrapped beat 4 and the offbeat-1 peak).
        Assert.That(wf.Sample(0f), Is.EqualTo(0f).Within(Tol));
    }

    // --- Peak spacing ---

    [Test]
    public void ShortestNonZeroPeakSpacing_ReturnsClosestAudiblePeakGap()
    {
        var wf = Waveform.Parse("HQQ", "844"); // audible peaks at 0, 0.5, 0.75; wrap gap back to 0 is 0.25
        Assert.That(wf.ShortestNonZeroPeakSpacing(), Is.EqualTo(0.25f).Within(Tol));
    }

    [Test]
    public void ShortestNonZeroPeakSpacing_IgnoresSilentPeaks()
    {
        var wf = Waveform.Parse("QQQQ", "8080"); // audible peaks at 0 and 0.5 only
        Assert.That(wf.ShortestNonZeroPeakSpacing(), Is.EqualTo(0.5f).Within(Tol));
    }

    [Test]
    public void ShortestNonZeroPeakSpacing_ReturnsFullBarForSingleAudiblePeak()
    {
        var wf = Waveform.Parse("QQQQ", "8000");
        Assert.That(wf.ShortestNonZeroPeakSpacing(), Is.EqualTo(1f).Within(Tol));
    }

    [Test]
    public void ShortestNonZeroPeakSpacing_ReturnsZeroWhenNoAudiblePeaksExist()
    {
        var wf = Waveform.Parse("QQQQ", "0000");
        Assert.That(wf.ShortestNonZeroPeakSpacing(), Is.EqualTo(0f).Within(Tol));
    }

    /// <summary>
    /// Scenario: "beats 2 and 4" (QQQQ / 0808). Asserts trough-to-trough progress runs 0 → 0.5 → 1
    /// across each audible Hump, from the trough half a beat before its peak to the trough half a beat
    /// after, and rests at 0 through the silent beats.
    /// </summary>
    [Test]
    public void TroughToTrough_SweepsOnceAcrossEachAudibleHumpAndRestsInSilence()
    {
        var wf = Waveform.Parse("QQQQ", "0808");
        Assert.That(wf.SampleTroughToTrough(0f), Is.EqualTo(0f).Within(Tol));         // beat 1, silent
        Assert.That(wf.SampleTroughToTrough(0.125f), Is.EqualTo(0f).Within(Tol));     // trough before beat 2
        Assert.That(wf.SampleTroughToTrough(0.1875f), Is.EqualTo(0.25f).Within(Tol)); // rising into beat 2
        Assert.That(wf.SampleTroughToTrough(0.25f), Is.EqualTo(0.5f).Within(Tol));    // beat 2 peak
        Assert.That(wf.SampleTroughToTrough(0.3125f), Is.EqualTo(0.75f).Within(Tol)); // falling after beat 2
        Assert.That(wf.SampleTroughToTrough(0.375f), Is.EqualTo(0f).Within(Tol));     // trough after beat 2, silence ahead
        Assert.That(wf.SampleTroughToTrough(0.5f), Is.EqualTo(0f).Within(Tol));       // beat 3, silent
        Assert.That(wf.SampleTroughToTrough(0.75f), Is.EqualTo(0.5f).Within(Tol));    // beat 4 peak
    }

    /// <summary>
    /// Scenario: "beats 2 and 4" (QQQQ / 0808). Asserts peak-to-peak progress runs 0 → 1 from beat 2
    /// to beat 4 and again from beat 4 around the bar to beat 2, with the silent beats inside each span
    /// rather than counted as peaks.
    /// </summary>
    [Test]
    public void PeakToPeak_SweepsBetweenAudiblePeaksAcrossSilentHumps()
    {
        var wf = Waveform.Parse("QQQQ", "0808");
        Assert.That(wf.SamplePeakToPeak(0.25f), Is.EqualTo(0f).Within(Tol));     // beat 2 peak
        Assert.That(wf.SamplePeakToPeak(0.5f), Is.EqualTo(0.5f).Within(Tol));    // beat 3, halfway to beat 4
        Assert.That(wf.SamplePeakToPeak(0.75f), Is.EqualTo(0f).Within(Tol));     // beat 4 peak
        Assert.That(wf.SamplePeakToPeak(0f), Is.EqualTo(0.5f).Within(Tol));      // beat 1, halfway around to beat 2
        Assert.That(wf.SamplePeakToPeak(0.125f), Is.EqualTo(0.75f).Within(Tol)); // beat 1.5
    }

    /// <summary>
    /// Scenario: "measure start" (QQQQ / 8000) has one audible peak and QQQQ / 0000 has none. Asserts
    /// the lone peak's sweep spans the whole bar, and that both sweeps rest at 0 with no audible peak.
    /// </summary>
    [Test]
    public void PeakToPeak_LonePeakSpansTheBarAndNoPeakRestsAtZero()
    {
        var lone = Waveform.Parse("QQQQ", "8000");
        Assert.That(lone.SamplePeakToPeak(0f), Is.EqualTo(0f).Within(Tol));
        Assert.That(lone.SamplePeakToPeak(0.75f), Is.EqualTo(0.75f).Within(Tol));

        var silent = Waveform.Parse("QQQQ", "0000");
        Assert.That(silent.SamplePeakToPeak(0.3f), Is.EqualTo(0f).Within(Tol));
        Assert.That(silent.SampleTroughToTrough(0.3f), Is.EqualTo(0f).Within(Tol));
    }

    // --- Malformation is logged, never silently substituted ---

    [Test]
    public void Malformed_LengthMismatch_IsFlaggedAndLogged()
    {
        LogAssert.Expect(LogType.Warning, new Regex("length mismatch"));
        var wf = Waveform.Parse("QQQQ", "888"); // 4 widths, 3 amplitudes
        Assert.That(wf.IsMalformed, Is.True);
        // The missing 4th amplitude reads as silent rather than throwing or substituting a fallback.
        Assert.That(wf.Sample(0.75f), Is.EqualTo(0f).Within(Tol));
    }

    [Test]
    public void Malformed_WidthsUnderfillBar_IsFlaggedAndBounded()
    {
        LogAssert.Expect(LogType.Warning, new Regex("widths sum to"));
        var wf = Waveform.Parse("QQ", "88"); // two quarters fill only half the bar
        Assert.That(wf.IsMalformed, Is.True);
        // The unfilled back half of the bar reads as trough, bounded to one bar — nothing runs away.
        Assert.That(wf.Sample(0.75f), Is.EqualTo(0f).Within(Tol));
    }

    /// <summary>Verifies authoring callers receive exact diagnostics without transient Console warnings.</summary>
    [Test]
    public void ParseWithDiagnostics_InvalidAmplitudeIsMalformedAndSilentWithoutLogging()
    {
        var waveform = Waveform.Parse("QQQQ", "89x8", Waveform.BeatPulseRounding, 0f, out var diagnostics);

        Assert.That(waveform.IsMalformed, Is.True);
        Assert.That(diagnostics, Has.Length.EqualTo(2));
        Assert.That(diagnostics[0], Does.Contain("'9'").And.Contain("0–8"));
        Assert.That(diagnostics[1], Does.Contain("'x'").And.Contain("0–8"));
        Assert.That(waveform.Sample(0.25f), Is.EqualTo(0f).Within(Tol));
        Assert.That(waveform.Sample(0.5f), Is.EqualTo(0f).Within(Tol));
        LogAssert.NoUnexpectedReceived();
    }

    // --- BeatManager Bar Phase clock (the live forcing function behind Sample) ---

    /// <summary>Verifies the offered Bar Phase starts at zero on the measure downbeat.</summary>
    [Test]
    public void BarPhase_IsZeroOnTheDownbeat()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f); // beat 1, full 500 ms to next
        beatManager.Update(0f);
        Assert.That(beatManager.Timing.BarProgress, Is.EqualTo(0f).Within(Tol));
    }

    /// <summary>Verifies the offered Bar Phase advances smoothly within the measure.</summary>
    [Test]
    public void BarPhase_AdvancesProportionallyWithinTheBar()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f); // half a 0.5 s beat = 1/8 bar
        beatManager.Update(0.25f);
        Assert.That(beatManager.Timing.BarProgress, Is.EqualTo(0.125f).Within(Tol));
    }
}
