
#nullable enable

using NUnit.Framework;
using UnityEngine;

public sealed class FlockBeatIntegrationTests
{
    /// <summary>
    /// Verifies that broad movement activity is strongly bass-weighted while retaining smaller Mid/High influence.
    /// </summary>
    [TestCase(0f, 0f, 0f, 0f)]
    [TestCase(0.5f, 0f, 0f, 0.625f)]
    [TestCase(0f, 0.5f, 0f, 0.0625f)]
    [TestCase(0f, 0f, 0.5f, 0f)]
    [TestCase(1f, 1f, 1f, 1f)]
    public void MovementActivityWeightsLowBandMost(float low, float mid, float high, float expected)
    {
        Assert.That(Flock.GetMovementActivity(low, mid, high, true), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>Verifies that Standalone restores normal travel instead of treating unavailable levels as silence.</summary>
    [Test]
    public void StandaloneMovementUsesAnActiveDefault()
    {
        Assert.That(Flock.GetMovementActivity(0f, 0f, 0f, false), Is.EqualTo(0.65f).Within(0.0001f));
    }

    /// <summary>Verifies that Standalone keeps Routine-driven visuals active without a placed musical Grid.</summary>
    [TestCase(0.25f, true, 0.25f)]
    [TestCase(0f, false, 1f)]
    public void RoutineEnvelopeUsesStandaloneFallback(float envelope, bool isSynced, float expected)
    {
        Assert.That(Flock.GetRoutineEnvelope(envelope, isSynced), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>Verifies that quiet activity nearly rests the flock while full activity permits energetic travel.</summary>
    [TestCase(0f, 0.1f)]
    [TestCase(0.5f, 0.8f)]
    [TestCase(1f, 1.5f)]
    public void MovementSpeedSpansQuietAndActivePostures(float activity, float expected)
    {
        Assert.That(Flock.GetMovementSpeedMultiplier(activity), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>Verifies that the Mid maneuver keeps its beat response but cannot agitate a quiet flock.</summary>
    [TestCase(0.2f, 1f, 0f)]
    [TestCase(0.425f, 1f, 0.5f)]
    [TestCase(0.65f, 0.5f, 0.5f)]
    public void MidManeuverIsThresholdedAndActivityGated(float mid, float activity, float expected)
    {
        Assert.That(Flock.GetMidManeuver(mid, activity), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>Verifies that relative spectral balance drives agitation without reacting during silence.</summary>
    [TestCase(0.25f, 1f, 0f)]
    [TestCase(0.45f, 1f, 0.5f)]
    [TestCase(0.65f, 0.5f, 0.5f)]
    public void SpectralAgitationUsesCentroidAndActivity(float centroid, float activity, float expected)
    {
        Assert.That(Flock.GetSpectralAgitation(centroid, activity), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>Verifies that shorter Fill windows receive stronger choreography and onset impulses.</summary>
    [TestCase(4, 1f)]
    [TestCase(2, 1.4142135f)]
    [TestCase(1, 2f)]
    [TestCase(8, 1f)]
    public void FillDurationBoostCompensatesForShortWindows(int lengthBeats, float expected)
    {
        Assert.That(Flock.GetFillDurationBoost(lengthBeats), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>Verifies that Fill orbit builds before onset over a lead window capped at two beats.</summary>
    [TestCase(3, 0f, 4, 0f)]
    [TestCase(2, 0f, 4, 0f)]
    [TestCase(2, 1f, 4, 0.5f)]
    [TestCase(1, 1f, 4, 1f)]
    [TestCase(1, 0f, 1, 0f)]
    [TestCase(1, 1f, 1, 2f)]
    public void FillApproachBuildsBeforeOnset(
        int beatsUntil,
        float beatProgress,
        int lengthBeats,
        float expected)
    {
        Assert.That(
            Flock.GetFillApproach(false, beatsUntil, beatProgress, lengthBeats),
            Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>Verifies that the anticipated orbit remains fully present throughout the active Fill.</summary>
    [TestCase(4, 1f)]
    [TestCase(2, 1.4142135f)]
    [TestCase(1, 2f)]
    public void ActiveFillHoldsItsDurationAwareOrbit(int lengthBeats, float expected)
    {
        Assert.That(
            Flock.GetFillApproach(true, null, null, lengthBeats),
            Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>
    /// Verifies the eight-beat Drop runway advances continuously through the current beat.
    /// </summary>
    [TestCase(8, 0f, 0f)]
    [TestCase(8, 1f, 0.04296875f)]
    [TestCase(4, 0f, 0.5f)]
    [TestCase(2, 0f, 0.84375f)]
    [TestCase(1, 1f, 1f)]
    public void DropApproachBuildsAcrossTheFinalEightBeats(int beatsUntil, float beatProgress, float expected)
    {
        Assert.That(
            Flock.GetDropApproach(false, beatsUntil, beatProgress),
            Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>
    /// Verifies that gathering rests when no upcoming Drop is inside the eight-beat runway.
    /// </summary>
    [Test]
    public void DropApproachRestsOutsideTheRunway()
    {
        Assert.That(Flock.GetDropApproach(false, 9, 0.5f), Is.Zero);
        Assert.That(Flock.GetDropApproach(false, null, 0.5f), Is.Zero);
        Assert.That(Flock.GetDropApproach(true, null, 0.5f), Is.Zero);
    }

    /// <summary>
    /// Verifies that wander turns relative to current travel rather than imposing one shared world-space heading.
    /// </summary>
    [Test]
    public void WanderDirectionTurnsRelativeToCurrentVelocity()
    {
        Vector2 direction = Flock.GetWanderDirection(Vector2.right, Mathf.PI * 0.5f);

        Assert.That(direction.x, Is.Zero.Within(0.0001f));
        Assert.That(direction.y, Is.EqualTo(1f).Within(0.0001f));
    }

    /// <summary>Verifies that collective course bends turn relative to travel without imposing a world heading.</summary>
    [TestCase(1f, 1f)]
    [TestCase(-1f, -1f)]
    public void CollectiveTurnDirectionBendsTheSharedHeading(float turnDirection, float expectedY)
    {
        Vector2 direction = Flock.GetCollectiveTurnDirection(Vector2.right, turnDirection);

        Assert.That(direction.x, Is.Zero.Within(0.0001f));
        Assert.That(direction.y, Is.EqualTo(expectedY).Within(0.0001f));
    }

    /// <summary>
    /// Verifies that neighbors across wrapped edges use the shortest visible offset on both axes.
    /// </summary>
    [Test]
    public void WrappedOffsetUsesTheShortestVisiblePath()
    {
        Vector2 offset = Flock.GetWrappedOffset(
            new Vector2(9f, 9f),
            new Vector2(-9f, -8f),
            new Vector2(20f, 20f));

        Assert.That(offset.x, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(offset.y, Is.EqualTo(3f).Within(0.0001f));
    }

    /// <summary>
    /// Verifies that a Fill onset adds immediate motion around the wall center without discarding existing velocity.
    /// </summary>
    [Test]
    public void FillImpulseAddsTangentialVelocity()
    {
        var boid = CreateBoid(new Vector2(2f, 0f), new Vector2(1f, 0f));

        boid.ApplyTangentialImpulse(Vector2.zero, 1f, 4f);

        Assert.That(boid.velocity.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(boid.velocity.y, Is.EqualTo(4f).Within(0.0001f));
    }

    /// <summary>
    /// Verifies that a Drop onset replaces most gathered motion with an outward burst while retaining some momentum.
    /// </summary>
    [Test]
    public void DropImpulseLaunchesRadiallyAndRetainsMomentum()
    {
        var boid = CreateBoid(new Vector2(2f, 0f), new Vector2(0f, 4f));

        boid.ApplyRadialImpulse(Vector2.zero, 20f, 0.25f);

        Assert.That(boid.velocity.x, Is.EqualTo(20f).Within(0.0001f));
        Assert.That(boid.velocity.y, Is.EqualTo(1f).Within(0.0001f));
    }

    /// <summary>Creates a deterministic boid state for caller-visible steering tests.</summary>
    private static Flock.Boid CreateBoid(Vector2 position, Vector2 velocity)
    {
        return new Flock.Boid(Vector2.one * -10f, Vector2.one * 10f, new Flock())
        {
            position = position,
            velocity = velocity,
        };
    }
    /// <summary>Verifies that the Routine envelope spans the authored trail half-life range.</summary>
    [TestCase(0f, 0.03f)]
    [TestCase(0.5f, 0.14f)]
    [TestCase(1f, 0.25f)]
    public void TrailRetentionFollowsRoutineHalfLife(float envelope, float halfLifeSeconds)
    {
        Assert.That(
            Flock.GetTrailRetention(envelope, halfLifeSeconds),
            Is.EqualTo(0.5f).Within(0.0001f));
    }

    /// <summary>Verifies that equivalent elapsed time produces the same trail decay at different frame rates.</summary>
    [Test]
    public void TrailRetentionIsFrameRateIndependent()
    {
        float sixtyFpsStep = Flock.GetTrailRetention(0.75f, 1f / 60f);
        float thirtyFpsStep = Flock.GetTrailRetention(0.75f, 1f / 30f);

        Assert.That(
            Mathf.Pow(sixtyFpsStep, 60f),
            Is.EqualTo(Mathf.Pow(thirtyFpsStep, 30f)).Within(0.0001f));
    }

    /// <summary>Verifies that the Routine coherently shifts, saturates, and brightens the authored palette color.</summary>
    [Test]
    public void RoutineColorMovesFromMutedToShiftedPaletteColor()
    {
        var color = Color.HSVToRGB(0.25f, 0.5f, 0.75f);
        color.a = 0.4f;

        Color muted = Flock.ApplyRoutineColor(color, 0f);
        Color shifted = Flock.ApplyRoutineColor(color, 1f);

        Color.RGBToHSV(muted, out var mutedHue, out var mutedSaturation, out var mutedValue);
        Color.RGBToHSV(shifted, out var shiftedHue, out var shiftedSaturation, out var shiftedValue);
        Assert.That(mutedHue, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(mutedSaturation, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(mutedValue, Is.EqualTo(0.525f).Within(0.0001f));
        Assert.That(shiftedHue, Is.EqualTo(0.45f).Within(0.0001f));
        Assert.That(shiftedSaturation, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(shiftedValue, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(muted.a, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(shifted.a, Is.EqualTo(0.4f).Within(0.0001f));
    }
}
