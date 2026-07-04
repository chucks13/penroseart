using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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

public sealed class SingletonTests
{
    private sealed class TestSingleton : Singleton<TestSingleton>
    {
    }

    [SetUp]
    public void SetUp()
    {
        ResetTestSingleton();
    }

    [TearDown]
    public void TearDown()
    {
        ResetTestSingleton();
    }

    [Test]
    public void InstanceThrowsWithoutCreatingAComponent()
    {
        var beforeCount = CountTestSingletons();

        var exception = Assert.Throws<System.InvalidOperationException>(() => _ = TestSingleton.Instance);

        Assert.That(exception.Message, Does.Contain("has no registered scene instance"));
        Assert.That(CountTestSingletons(), Is.EqualTo(beforeCount));
    }

    [Test]
    public void ControllerInstanceThrowsWithoutCreatingAControllerComponent()
    {
        var singletonField = SingletonInstanceField<Controller>();
        var originalInstance = singletonField.GetValue(null);
        singletonField.SetValue(null, null);

        try
        {
            var beforeCount = CountControllers();

            var exception = Assert.Throws<System.InvalidOperationException>(() => _ = Controller.Instance);

            Assert.That(exception.Message, Does.Contain("Controller has no registered scene instance"));
            Assert.That(CountControllers(), Is.EqualTo(beforeCount));
        }
        finally
        {
            singletonField.SetValue(null, originalInstance);
        }
    }

    private static int CountTestSingletons()
    {
        return Object.FindObjectsByType<TestSingleton>(FindObjectsInactive.Include).Length;
    }

    private static int CountControllers()
    {
        return Object.FindObjectsByType<Controller>(FindObjectsInactive.Include).Length;
    }

    private static void ResetTestSingleton()
    {
        SingletonInstanceField<TestSingleton>().SetValue(null, null);
    }

    private static FieldInfo SingletonInstanceField<T>() where T : MonoBehaviour
    {
        return typeof(Singleton<T>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
    }
}
