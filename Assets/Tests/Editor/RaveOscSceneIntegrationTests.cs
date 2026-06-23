using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public sealed class RaveOscSceneIntegrationTests
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [Test]
    public void SampleSceneHasControllerHostForRuntimeRaveOscReceiver()
    {
        var previousActiveScene = SceneManager.GetActiveScene();
        var sampleSceneWasLoaded = SceneManager.GetSceneByPath(SampleScenePath).isLoaded;
        var scene = sampleSceneWasLoaded
            ? SceneManager.GetSceneByPath(SampleScenePath)
            : EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);

        try
        {
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(scene.path, Is.EqualTo(SampleScenePath));

            var controller = FindControllerIn(scene);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.gameObject.scene.path, Is.EqualTo(SampleScenePath));
            Assert.That(controller.gameObject.GetComponent<RaveOscReceiver>(), Is.Null,
                "RaveOscReceiver should remain runtime-added by Controller.Start(), not serialized into the scene.");
        }
        finally
        {
            if (!sampleSceneWasLoaded && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }

    private static Controller FindControllerIn(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var controller = root.GetComponentInChildren<Controller>(true);
            if (controller != null)
            {
                return controller;
            }
        }

        return null;
    }
}
