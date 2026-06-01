using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RaveOscSceneIntegrationTests
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [Test]
    public void SampleSceneHasControllerHostForRuntimeRaveOscReceiver()
    {
        var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        Assert.That(scene.isLoaded, Is.True);
        Assert.That(scene.path, Is.EqualTo(SampleScenePath));

        var controller = Object.FindFirstObjectByType<Controller>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.gameObject.scene.path, Is.EqualTo(SampleScenePath));
        Assert.That(controller.gameObject.GetComponent<RaveOscReceiver>(), Is.Null,
            "RaveOscReceiver should remain runtime-added by Controller.Start(), not serialized into the scene.");
    }
}
