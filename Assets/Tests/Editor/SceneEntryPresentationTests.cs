using System.Reflection;
using BombBrick.Game.Core;
using BombBrick.Game.Scenes;
using NUnit.Framework;
using UnityEngine;

public sealed class SceneEntryPresentationTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo FlowInstance = typeof(GameFlowController).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo FrontendInstance = typeof(GameFrontend).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private object previousFlow, previousFrontend;
    private GameObject host;
    private GameFlowController flow;

    [SetUp]
    public void SetUp()
    {
        previousFlow = FlowInstance.GetValue(null);
        previousFrontend = FrontendInstance.GetValue(null);
        host = new GameObject("Scene entry presentation fixture");
        host.SetActive(false);
        flow = host.AddComponent<GameFlowController>();
        FlowInstance.SetValue(null, flow);
        FrontendInstance.SetValue(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        var frontend = host.GetComponent<GameFrontend>();
        if (frontend != null)
            ((System.Collections.Generic.Queue<System.Action>)Get(frontend, "pendingSaves")).Clear();
        Object.DestroyImmediate(host);
        FlowInstance.SetValue(null, previousFlow);
        FrontendInstance.SetValue(null, previousFrontend);
    }

    [Test]
    public void TownDoesNotCreateInitialRevealUntilItsDataIsReady()
    {
        FlowInstance.SetValue(null, null);
        var town = host.AddComponent<TownSceneController>();
        Set(town, "profile", new PlayerProfileSave { starterSelectionPending = true });
        Update(town);
        Assert.That(Get(town, "starterReveal"), Is.Null);
        Assert.That(Get(town, "scenePresentationStarted"), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TownDoesNotCreateInitialRevealBehindLoadingOrBeforeFirstPaint(bool loading)
    {
        Set(flow, "isTransitioning", loading);
        Set(flow, "scenePresentedFrame", -1);
        var town = host.AddComponent<TownSceneController>();
        Set(town, "<IsSceneReady>k__BackingField", true);
        Set(town, "profile", new PlayerProfileSave { starterSelectionPending = true });
        Update(town);
        Assert.That(Get(town, "starterReveal"), Is.Null);
        Assert.That(Get(town, "scenePresentationStarted"), Is.False);
    }

    [Test]
    public void TownStartsPanelGuideAndUnlockNoticeWhenVisibleOnlyOnce()
    {
        FlowInstance.SetValue(null, null);
        var town = host.AddComponent<TownSceneController>();
        Set(town, "<IsSceneReady>k__BackingField", true);
        Set(town, "screenRevealStartedAt", -100f);
        Set(town, "guideStepChangedAt", -100f);
        Set(town, "progressUnlockNoticeOpen", true);
        Set(town, "progressUnlockNoticeStartedAt", -100f);
        Update(town);
        Assert.That(Get(town, "screenRevealStartedAt"), Is.EqualTo(Time.unscaledTime));
        Assert.That(Get(town, "guideStepChangedAt"), Is.EqualTo(Time.unscaledTime));
        Assert.That(Get(town, "progressUnlockNoticeStartedAt"), Is.EqualTo(Time.unscaledTime));
        Set(town, "screenRevealStartedAt", -10f);
        Update(town);
        Assert.That(Get(town, "screenRevealStartedAt"), Is.EqualTo(-10f));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ResultDoesNotAdvanceExperienceBehindLoadingOrBeforeFirstPaint(bool loading)
    {
        Set(flow, "isTransitioning", loading);
        Set(flow, "scenePresentedFrame", -1);
        var result = host.AddComponent<ResultSceneController>();
        Set(result, "playerExperienceReady", true);
        Set(result, "playerExperienceTarget", 100);
        Set(result, "playerExperienceAnimationStartsAt", -100f);
        Update(result);
        Assert.That(Get(result, "displayedPlayerExperience"), Is.EqualTo(0f));
        Assert.That(Get(result, "scenePresentationStarted"), Is.False);
    }

    [Test]
    public void ResultStartsPanelAndExperienceDelayWhenVisibleOnlyOnce()
    {
        FlowInstance.SetValue(null, null);
        var result = host.AddComponent<ResultSceneController>();
        Set(result, "playerExperienceTarget", 100);
        Update(result);
        Assert.That(Get(result, "scenePresentationStarted"), Is.False);
        Set(result, "playerExperienceReady", true);
        Set(result, "screenRevealStartedAt", -100f);
        Set(result, "playerExperienceAnimationStartsAt", -100f);
        Update(result);
        Assert.That(Get(result, "screenRevealStartedAt"), Is.EqualTo(Time.unscaledTime));
        Assert.That(Get(result, "playerExperienceAnimationStartsAt"), Is.EqualTo(Time.unscaledTime + .8f));
        Assert.That(Get(result, "displayedPlayerExperience"), Is.EqualTo(0f));
        Set(result, "screenRevealStartedAt", -10f);
        Set(result, "playerExperienceAnimationStartsAt", Time.unscaledTime + 5f);
        Update(result);
        Assert.That(Get(result, "screenRevealStartedAt"), Is.EqualTo(-10f));
        Assert.That(Get(result, "playerExperienceAnimationStartsAt"), Is.EqualTo(Time.unscaledTime + 5f));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PendingSaveNoticeDefersTheVisiblePresentationStart(bool townScene)
    {
        FlowInstance.SetValue(null, null);
        var frontend = host.AddComponent<GameFrontend>();
        FrontendInstance.SetValue(null, frontend);
        ((System.Collections.Generic.Queue<System.Action>)Get(frontend, "pendingSaves")).Enqueue(() => {});
        MonoBehaviour scene;
        if (townScene)
        {
            scene = host.AddComponent<TownSceneController>();
            Set(scene, "<IsSceneReady>k__BackingField", true);
        }
        else
        {
            scene = host.AddComponent<ResultSceneController>();
            Set(scene, "playerExperienceReady", true);
        }
        Update(scene);
        Assert.That(Get(scene, "scenePresentationStarted"), Is.False);
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static void Update(object target) => target.GetType().GetMethod("Update", Fields).Invoke(target, null);
}
