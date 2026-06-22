using NUnit.Framework;

public sealed class LiveControllerAccessTests
{
    [Test]
    public void TryGetReturnsFalseOutsidePlayModeWithoutTouchingSingletonInstance()
    {
        var found = LiveControllerAccess.TryGet(out var controller);

        Assert.That(found, Is.False);
        Assert.That(controller, Is.Null);
    }
}
