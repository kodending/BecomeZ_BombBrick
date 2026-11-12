using System.Reflection;
using BombBrick.Game.Core;
using BombBrick.Game.Scenes;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SceneActivationTests
{
    [Test]
    public void LoadingAndItsReleaseFrameCannotConsumeTheFirstVisibleIntro()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        var singleton = typeof(GameFlowController).GetField("instance", hidden);
        object previous = singleton.GetValue(null);
        var host = new GameObject("First rendered scene gate test"); host.SetActive(false);
        try
        {
            var flow = host.AddComponent<GameFlowController>(); singleton.SetValue(null, flow);
            var transition = typeof(GameFlowController).GetField("isTransitioning", hidden);
            var frame = typeof(GameFlowController).GetField("scenePresentedFrame", hidden);
            Assert.That(GameFlowController.CanPresentScene, Is.False);
            Assert.That(GameFlowController.PresentationDeltaTime, Is.Zero);
            transition.SetValue(flow, true);
            flow.NotifySceneRendered();
            Assert.That(frame.GetValue(flow), Is.EqualTo(-1), "An overlay repaint does not expose the destination.");
            transition.SetValue(flow, false);
            Assert.That(GameFlowController.CanPresentScene, Is.False, "Finishing I/O alone must not start the intro.");
            flow.NotifySceneRendered();
            Assert.That(frame.GetValue(flow), Is.EqualTo(Time.frameCount));
            Assert.That(GameFlowController.CanPresentScene, Is.False, "Wait until the painted frame has completed.");
            flow.NotifySceneRendered();
            Assert.That(frame.GetValue(flow), Is.EqualTo(Time.frameCount));
            if (Time.frameCount > 0)
            {
                frame.SetValue(flow, Time.frameCount - 1);
                Assert.That(GameFlowController.CanPresentScene, Is.True);
                Assert.That(GameFlowController.PresentationDeltaTime, Is.Zero, "The cold rendering frame can have a large delta.");
            }
            transition.SetValue(flow, true);
            Assert.That(GameFlowController.CanPresentScene, Is.False);
            Assert.That(GameFlowController.PresentationDeltaTime, Is.Zero);
        }
        finally { Object.DestroyImmediate(host); singleton.SetValue(null, previous); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingDestinationKeepsTheCurrentSceneAndRootsUsable(bool reload)
    {
        var scene=SceneManager.GetActiveScene();
        var host=new GameObject("Inactive flow validation fixture");host.SetActive(false);
        var flow=host.AddComponent<GameFlowController>();
        var visible=new GameObject("Current scene root");
        var inactive=new GameObject("Inactive current scene root");inactive.SetActive(false);
        bool started=false;flow.SceneTransitionStarted+=name=>started=true;
        try
        {
            LogAssert.Expect(LogType.Error,"[Game Flow] Scene 'Missing_scene_activation_fixture' is not available in build settings.");
            typeof(GameFlowController).GetMethod("LoadScene",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(flow,new object[]{"Missing_scene_activation_fixture",reload});
            Assert.That(started,Is.False);
            Assert.That(flow.IsTransitioning,Is.False);
            Assert.That(flow.LastTransitionError,Is.Not.Empty);
            Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(scene));
            Assert.That(scene.isLoaded,Is.True);
            Assert.That(visible.activeSelf,Is.True);
            Assert.That(inactive.activeSelf,Is.False);
        }
        finally
        {
            Object.DestroyImmediate(visible);Object.DestroyImmediate(inactive);Object.DestroyImmediate(host);
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void DestinationReadinessControlsRollbackAndPreservesInactiveRoots(bool townScene, bool ready)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        var originalScene = SceneManager.GetActiveScene();
        var singleton = typeof(GameFlowController).GetField("instance", hidden);
        object originalFlow = singleton.GetValue(null);
        var host = new GameObject("Scene initialization rollback fixture"); host.SetActive(false);
        var flow = host.AddComponent<GameFlowController>(); singleton.SetValue(null, flow);
        Scene previous = default(Scene), destination = default(Scene);
        string fixtureFolder = "Assets/__SceneActivationTestFixtures_" + System.Guid.NewGuid().ToString("N");
        try
        {
            // Open saved empty fixtures without saving or replacing the runner's untitled scene.
            string sceneTemplate = System.IO.File.ReadAllText(GameSceneCatalog.BootPath);
            int firstObject = sceneTemplate.IndexOf("--- !u!1 &", System.StringComparison.Ordinal);
            Assert.That(firstObject, Is.GreaterThan(0));
            string emptyScene = sceneTemplate.Substring(0, firstObject);
            Assert.That(UnityEditor.AssetDatabase.IsValidFolder(fixtureFolder), Is.False);
            UnityEditor.AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(fixtureFolder));
            string previousPath = fixtureFolder + "/Previous.unity";
            string destinationPath = fixtureFolder + "/Destination.unity";
            System.IO.File.WriteAllText(previousPath, emptyScene);
            System.IO.File.WriteAllText(destinationPath, emptyScene);
            UnityEditor.AssetDatabase.ImportAsset(previousPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            UnityEditor.AssetDatabase.ImportAsset(destinationPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            previous = EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Additive);
            var visible = new GameObject("Previously visible root");
            SceneManager.MoveGameObjectToScene(visible, previous);
            var inactive = new GameObject("Intentionally inactive root"); inactive.SetActive(false);
            SceneManager.MoveGameObjectToScene(inactive, previous);
            destination = EditorSceneManager.OpenScene(destinationPath, OpenSceneMode.Additive);
            var destinationRoot = new GameObject("Destination controller"); destinationRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(destinationRoot, destination);
            MonoBehaviour controller = townScene
                ? (MonoBehaviour)destinationRoot.AddComponent<TownSceneController>()
                : destinationRoot.AddComponent<MonsterAutoBattlePrototype>();
            controller.enabled = false;
            controller.GetType().GetField("<IsSceneReady>k__BackingField", hidden).SetValue(controller, ready);
            typeof(GameFlowController).GetField("previousScene", hidden).SetValue(flow, previous);
            typeof(GameFlowController).GetField("scenePresentedFrame", hidden).SetValue(flow, 123);
            typeof(GameFlowController).GetField("isTransitioning", hidden).SetValue(flow, true);
            visible.SetActive(false);
            SceneManager.SetActiveScene(destination);

            bool accepted = (bool)typeof(GameFlowController).GetMethod("ConfirmSceneInitialization", hidden)
                .Invoke(flow, new object[] { destination, new[] { visible } });

            Assert.That(accepted, Is.EqualTo(ready), "A disabled/inactive controller must not imply successful initialization.");
            Assert.That(previous.isLoaded, Is.True, "Retain the previous scene until initialization succeeds.");
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(ready ? destination : previous));
            Assert.That(visible.activeSelf, Is.EqualTo(!ready));
            Assert.That(inactive.activeSelf, Is.False, "Rollback must not reveal intentionally inactive roots.");
            Assert.That(string.IsNullOrEmpty(flow.LastTransitionError), Is.EqualTo(ready));
            if (!ready)
                Assert.That(typeof(GameFlowController).GetField("scenePresentedFrame", hidden).GetValue(flow), Is.EqualTo(-1));
        }
        finally
        {
            SceneManager.SetActiveScene(originalScene);
            Object.DestroyImmediate(host);
            singleton.SetValue(null, originalFlow);
            if (destination.IsValid() && destination.isLoaded) EditorSceneManager.CloseScene(destination, true);
            if (previous.IsValid() && previous.isLoaded) EditorSceneManager.CloseScene(previous, true);
            if (UnityEditor.AssetDatabase.IsValidFolder(fixtureFolder))
                Assert.That(UnityEditor.AssetDatabase.DeleteAsset(fixtureFolder), Is.True, "Remove only this test's uniquely owned fixture folder.");
        }
    }
}
