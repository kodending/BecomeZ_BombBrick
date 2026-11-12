using System;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class GameBackgroundTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameFrontend frontend;
    private GameUserSettings settings;
    private Scene scene;
    private string originalSceneName;
    private object originalInstance;
    private float originalScale, originalVolume;
    private static FieldInfo Instance => typeof(GameFrontend).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);

    [SetUp]
    public void SetUp()
    {
        scene = SceneManager.GetActiveScene();
        originalSceneName = scene.name;
        originalScale = Time.timeScale; originalVolume = AudioListener.volume;
        originalInstance = Instance.GetValue(null);
        scene.name = GameSceneCatalog.Battle;
        host = new GameObject("Background settings test"); host.SetActive(false);
        frontend = host.AddComponent<GameFrontend>();
        settings = new GameUserSettings { masterVolume = .6f };
        Instance.SetValue(null, frontend);
        Set(frontend, "initialized", true); Set(frontend, "settings", settings);
        Set(frontend, "applicationFocused", true);
        Time.timeScale = 2f; AudioListener.volume = .6f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        scene.name = originalSceneName;
        Instance.SetValue(null, originalInstance);
        Time.timeScale = originalScale; AudioListener.volume = originalVolume;
    }

    [Test]
    public void LegacyDefaultsAreEnabledAndBothIndependentChoicesRoundTrip()
    {
        var legacy = JsonUtility.FromJson<GameUserSettings>("{\"cameraShake\":1}");
        Assert.That(legacy.pauseWhenUnfocused && legacy.muteWhenUnfocused, Is.True);
        foreach (bool pause in new[] { false, true })
        foreach (bool mute in new[] { false, true })
        {
            settings.pauseWhenUnfocused = pause; settings.muteWhenUnfocused = mute;
            var loaded = JsonUtility.FromJson<GameUserSettings>(JsonUtility.ToJson(settings));
            Assert.That(loaded.pauseWhenUnfocused, Is.EqualTo(pause));
            Assert.That(loaded.muteWhenUnfocused, Is.EqualTo(mute));
            Assert.That(loaded.masterVolume, Is.EqualTo(.6f));
        }
    }

    [TestCase(true, true, 2f)] [TestCase(true, false, 2f)]
    [TestCase(false, true, 2f)] [TestCase(false, false, 2f)]
    [TestCase(true, true, 0f)] [TestCase(true, true, .5f)]
    public void FocusRoundTripPreservesSpeedAndIndependentMute(bool pause, bool mute, float scale)
    {
        settings.pauseWhenUnfocused = pause; settings.muteWhenUnfocused = mute;
        Time.timeScale = scale;
        Call(frontend, "OnApplicationFocus", false);
        Assert.That(Time.timeScale, Is.EqualTo(pause ? 0f : scale));
        Assert.That(AudioListener.volume, Is.EqualTo(mute ? 0f : .6f));
        Assert.That(GameFrontend.BlocksBackgroundInput, Is.True);
        Call(frontend, "OnApplicationFocus", false); // duplicate loss must not replace saved speed with zero
        Call(frontend, "OnApplicationFocus", true);
        Assert.That(Time.timeScale, Is.EqualTo(scale));
        Assert.That(AudioListener.volume, Is.EqualTo(.6f));
        Assert.That(settings.masterVolume, Is.EqualTo(.6f));
        Assert.That(GameFrontend.BlocksBackgroundInput, Is.True, "Return click stays blocked until release.");
    }

    [TestCase(true)] [TestCase(false)]
    public void FocusAndMinimizeMustBothReturnBeforeResuming(bool focusReturnsFirst)
    {
        Call(frontend, "OnApplicationFocus", false);
        Call(frontend, "OnApplicationPause", true);
        Call(frontend, focusReturnsFirst ? "OnApplicationFocus" : "OnApplicationPause", focusReturnsFirst);
        Assert.That(Time.timeScale, Is.Zero); Assert.That(AudioListener.volume, Is.Zero);
        Call(frontend, focusReturnsFirst ? "OnApplicationPause" : "OnApplicationFocus", !focusReturnsFirst);
        Assert.That(Time.timeScale, Is.EqualTo(2f)); Assert.That(AudioListener.volume, Is.EqualTo(.6f));
    }

    [Test]
    public void ManualMenuPauseRemainsUntilThePlayerClosesIt()
    {
        Set(frontend, "menuOpen", true); Set(frontend, "previousTimeScale", 2f); Time.timeScale = 0f;
        Call(frontend, "OnApplicationFocus", false); Call(frontend, "OnApplicationFocus", true);
        Assert.That(Time.timeScale, Is.Zero); Assert.That(Get<bool>(frontend, "menuOpen"), Is.True);
        frontend.ClosePanels();
        Assert.That(Time.timeScale, Is.EqualTo(2f));
    }

    [Test]
    public void ClosingAnExistingMenuInBackgroundDoesNotResumeUntilFocusReturns()
    {
        Set(frontend, "menuOpen", true); Set(frontend, "previousTimeScale", 2f); Time.timeScale = 0f;
        Call(frontend, "OnApplicationFocus", false);
        frontend.ClosePanels();
        Assert.That(Time.timeScale, Is.Zero);
        Call(frontend, "OnApplicationFocus", true);
        Assert.That(Time.timeScale, Is.EqualTo(2f));
    }

    [Test]
    public void CompletingAPendingSaveWhileUnfocusedKeepsTheBackgroundPause()
    {
        bool saved = false;
        Get<Queue<Action>>(frontend, "pendingSaves").Enqueue(() => saved = true);
        Set(frontend, "saveTimeScale", 2f); Time.timeScale = 0f;
        Call(frontend, "OnApplicationFocus", false);
        Assert.That(frontend.RetryPendingSave(), Is.True); Assert.That(saved, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Call(frontend, "OnApplicationFocus", true);
        Assert.That(Time.timeScale, Is.EqualTo(2f));
    }

    [Test]
    public void SettingsChangesWhileUnfocusedKeepMuteAndRestoreLatestConfiguredVolume()
    {
        Call(frontend, "OnApplicationFocus", false);
        settings.masterVolume = .3f;
        GameFrontend.ApplyBackgroundSettings(settings);
        Assert.That(AudioListener.volume, Is.Zero);
        settings.pauseWhenUnfocused = false;
        GameFrontend.ApplyBackgroundSettings(settings);
        Assert.That(Time.timeScale, Is.EqualTo(2f)); Assert.That(GameFrontend.BlocksBackgroundInput, Is.True);
        Call(frontend, "OnApplicationFocus", true);
        Assert.That(AudioListener.volume, Is.EqualTo(.3f));
    }

    [Test]
    public void BattleFocusLossCancelsNetItemAndFusionDragsWithoutChangingManualPause()
    {
        var battle = host.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle, "paused", true);
        Set(battle, "draggingCaptureNet", true); Set(battle, "draggedCaptureNetItemId", "test-net");
        Set(battle, "draggingItem", true); Set(battle, "draggedItemInstanceId", "test-item");
        Set(battle, "draggingSpecialItem", true); Set(battle, "draggedSpecialItemInstanceId", "test-special");
        Call(battle, "OnApplicationFocus", false);
        Assert.That(Get<bool>(battle, "paused"), Is.True);
        foreach (string field in new[] { "draggingCaptureNet", "draggingItem", "draggingSpecialItem" })
            Assert.That(Get<bool>(battle, field), Is.False, field);
        foreach (string field in new[] { "draggedCaptureNetItemId", "draggedItemInstanceId", "draggedSpecialItemInstanceId" })
            Assert.That(Get<string>(battle, field), Is.Empty, field);
    }
}
