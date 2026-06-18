using NUnit.Framework;

/// <summary>
/// Pins the Wall Variant Lock policy on <see cref="BeatManager"/> (Candidate 1 of the editor architecture
/// review): <c>LockVariant</c> clamps into the Pool and never yields Auto, <c>ReleaseToAuto</c> is the only
/// path to the Auto sentinel, and <c>ResolveDisplayVariant</c> resolves lock → on-screen → 0 as a pure
/// function with the on-screen variant passed in. These exercise the runtime core directly, with no editor
/// assembly and no live Controller — the testability the extraction was for.
/// </summary>
public sealed class BeatManagerWallVariantLockTests
{
    [Test]
    public void ReleaseToAutoSetsTheAutoSentinel()
    {
        var beatManager = new BeatManager();
        beatManager.LockVariant(2);

        beatManager.ReleaseToAuto();

        Assert.That(beatManager.activeVariant, Is.EqualTo(-1));
    }

    [Test]
    public void LockVariantClampsNegativeToZero()
    {
        var beatManager = new BeatManager();

        beatManager.LockVariant(-100);

        Assert.That(beatManager.activeVariant, Is.EqualTo(0));
    }

    [Test]
    public void LockVariantClampsAbovePoolToAStableCeiling()
    {
        var beatManager = new BeatManager();

        beatManager.LockVariant(int.MaxValue);
        var ceiling = beatManager.activeVariant;
        Assert.That(ceiling, Is.GreaterThanOrEqualTo(0), "a lock must resolve to a real Pool index");

        // Any larger request lands on the same ceiling — proves the upper clamp without hardcoding Pool size.
        beatManager.LockVariant(ceiling + 5000);
        Assert.That(beatManager.activeVariant, Is.EqualTo(ceiling));
    }

    [Test]
    public void LockVariantNeverYieldsAuto()
    {
        var beatManager = new BeatManager();

        // -1 is Auto's sentinel, but a *lock* request must still pin a real index — only ReleaseToAuto frees it.
        beatManager.LockVariant(-1);

        Assert.That(beatManager.activeVariant, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void ResolveDisplayVariantPrefersTheLock()
    {
        var beatManager = new BeatManager();
        beatManager.LockVariant(0);

        Assert.That(beatManager.ResolveDisplayVariant(7), Is.EqualTo(0)); // on-screen ignored while locked
        Assert.That(beatManager.ResolveDisplayVariant(-1), Is.EqualTo(0));
    }

    [Test]
    public void ResolveDisplayVariantUsesOnScreenWhenAuto()
    {
        var beatManager = new BeatManager();
        beatManager.ReleaseToAuto();

        Assert.That(beatManager.ResolveDisplayVariant(3), Is.EqualTo(3));
    }

    [Test]
    public void ResolveDisplayVariantFallsBackToZeroWhenAutoAndNoOnScreen()
    {
        var beatManager = new BeatManager();
        beatManager.ReleaseToAuto();

        Assert.That(beatManager.ResolveDisplayVariant(-1), Is.EqualTo(0));
    }
}
