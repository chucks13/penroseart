#nullable enable

using NUnit.Framework;

/// <summary>
/// Pins the effect-facing 16-beat Phase query (<see cref="BeatManager.Phase"/> => <see cref="PhaseInfo"/>?).
/// The Director is the single writer via <see cref="BeatManager.PublishPhase"/>; these tests drive that
/// seam directly to fix the facade contract: the null rules (out of phase), the 1..16 Count, the 0..1
/// Progress enrichment, Confidence passthrough, and that the facade never latches — it mirrors whatever was
/// last published, so a beat rewind within a phrase self-corrects.
/// </summary>
public sealed class BeatManagerPhaseQueryTests
{
    /// <summary>
    /// A live-sourced BeatManager whose intra-beat fraction is exactly 0.5 (250 ms into a 500 ms beat),
    /// matching the BarPhase fixture the other rhythm-query tests pin. The Progress derivation reads it.
    /// </summary>
    private static BeatManager CreateLiveBeatManager()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.snapshot.beatInBar = 1;
        beatManager.beatData.snapshot.beatAverageMs = 500;
        beatManager.beatData.snapshot.beatsCountMs = new[] { 0, 250, 750, 1250 };
        return beatManager;
    }

    [Test]
    public void PhaseIsNullBeforeAnyPublish()
    {
        var beatManager = new BeatManager();

        Assert.That(beatManager.Phase, Is.Null);
    }

    [Test]
    public void PhaseIsNullWhenTheDirectorPublishesNone()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.PublishPhase(PhaseReading.None);

        Assert.That(beatManager.Phase, Is.Null);
    }

    [Test]
    public void PhaseIsNullOnTheStandaloneFloorEvenWithAGridPosition()
    {
        // StandAloneFloor is a mode exit: the clock is gone, so there is no Phase even when a stale grid
        // position still rides along in the reading.
        var beatManager = CreateLiveBeatManager();
        beatManager.PublishPhase(new PhaseReading(0, 5, PhaseLockState.Coasting, standAloneFloor: true));

        Assert.That(beatManager.Phase, Is.Null);
    }

    [Test]
    public void PhaseSurfacesCountConfidenceAndProgress()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.PublishPhase(new PhaseReading(0, 5, PhaseLockState.Locked, standAloneFloor: false));

        var phase = beatManager.Phase;

        Assert.That(phase, Is.Not.Null);
        Assert.That(phase!.Value.Confidence, Is.EqualTo(PhaseLockState.Locked));
        Assert.That(phase.Value.Count, Is.EqualTo(5));
        // (5 - 1 + 0.5 intra-beat) / 16
        Assert.That(phase.Value.Progress, Is.EqualTo(0.28125f).Within(0.0001f));
    }

    [Test]
    public void CoastingAndContradictedAreInPhaseReadings()
    {
        // All three PhaseLockState values are in-phase readings with a valid Count; they differ only in trust.
        var beatManager = CreateLiveBeatManager();

        beatManager.PublishPhase(new PhaseReading(0, 8, PhaseLockState.Coasting, false));
        Assert.That(beatManager.Phase?.Confidence, Is.EqualTo(PhaseLockState.Coasting));

        beatManager.PublishPhase(new PhaseReading(0, 8, PhaseLockState.Contradicted, false));
        Assert.That(beatManager.Phase?.Confidence, Is.EqualTo(PhaseLockState.Contradicted));
    }

    [Test]
    public void CountStaysWithinTheSixteenBeatGridAndProgressWithinUnit()
    {
        var beatManager = CreateLiveBeatManager();

        beatManager.PublishPhase(new PhaseReading(0, 1, PhaseLockState.Locked, false));
        Assert.That(beatManager.Phase!.Value.Count, Is.EqualTo(1));
        Assert.That(beatManager.Phase!.Value.Progress, Is.EqualTo(0.03125f).Within(0.0001f)); // (0 + 0.5) / 16
        Assert.That(beatManager.Phase!.Value.Progress, Is.InRange(0f, 1f));

        beatManager.PublishPhase(new PhaseReading(0, 16, PhaseLockState.Locked, false));
        Assert.That(beatManager.Phase!.Value.Count, Is.EqualTo(16));
        Assert.That(beatManager.Phase!.Value.Progress, Is.EqualTo(0.96875f).Within(0.0001f)); // (15 + 0.5) / 16
        Assert.That(beatManager.Phase!.Value.Progress, Is.InRange(0f, 1f));
    }

    [Test]
    public void FacadeMirrorsTheLatestPublishSoABeatRewindSelfCorrects()
    {
        // No latching: a loop (a beat rewind within a phrase) re-publishes a lower position, and the facade
        // reflects it immediately rather than holding the higher count.
        var beatManager = CreateLiveBeatManager();

        beatManager.PublishPhase(new PhaseReading(0, 14, PhaseLockState.Locked, false));
        Assert.That(beatManager.Phase!.Value.Count, Is.EqualTo(14));

        beatManager.PublishPhase(new PhaseReading(0, 2, PhaseLockState.Locked, false));
        Assert.That(beatManager.Phase!.Value.Count, Is.EqualTo(2));
    }
}
