
#nullable enable

using NUnit.Framework;
using UnityEngine;

public sealed class FlockBeatIntegrationTests
{
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
        return new Flock.Boid(Vector2.one * -10f, Vector2.one * 10f, new Flock(), Flock.StandaloneDefaults.WanderFrequency)
        {
            position = position,
            velocity = velocity,
        };
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

}
