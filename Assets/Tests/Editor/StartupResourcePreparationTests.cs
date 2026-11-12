using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class StartupResourcePreparationTests
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly string[] StateFields =
    {
        "mainWarmupComplete", "gameplayWarmupComplete", "warmupComplete", "previewWarmupComplete", "effectWarmupComplete",
        "mainWarmupProgress", "gameplayWarmupProgress", "<WarmupProgress>k__BackingField", "<MonsterPreviewProgress>k__BackingField"
    };

    private static Dictionary<FieldInfo, object> CaptureState()
    {
        var previous = new Dictionary<FieldInfo, object>();
        foreach (string name in StateFields)
        {
            var field = typeof(GameResourceCache).GetField(name, Flags);
            previous[field] = field.GetValue(null);
        }
        return previous;
    }

    private static void RestoreState(Dictionary<FieldInfo, object> previous)
    {
        foreach (var pair in previous) pair.Key.SetValue(null, pair.Value);
    }

    private static void Set(string name, object value) => typeof(GameResourceCache).GetField(name, Flags).SetValue(null, value);

    [UnityTest]
    public IEnumerator MainPreparationDoesNotRequireBattleCatalogsAndPreservesRandom()
    {
        var previous = CaptureState();
        var random = UnityEngine.Random.state;
        foreach (string name in StateFields) Set(name, name.Contains("Progress") ? (object)0f : false);
        try
        {
            yield return GameResourceCache.WarmupStartup();
            Assert.That(typeof(GameResourceCache).GetField("mainWarmupComplete", Flags).GetValue(null), Is.True);
            Assert.That(GameResourceCache.StartupWarmupComplete, Is.False, "Main textures alone must not release the loading screen.");
            Assert.That(GameResourceCache.StartupProgress, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(GameResourceCache.Database, Is.Not.Null);
            foreach (string name in new[] { "gameplayWarmupComplete", "warmupComplete", "previewWarmupComplete", "effectWarmupComplete" })
                Assert.That(typeof(GameResourceCache).GetField(name, Flags).GetValue(null), Is.False, name);
            foreach (string name in new[] { "MainMenuExpeditionBase", "MainMenuSkyOccluder", "MainMenuTreeOccluder", "ChundongiPeekAtlas_Runtime" })
                Assert.That(GameResourceCache.WarmupTexture("Environment/MainMenu/" + name).MoveNext(), Is.False, name);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            Assert.That(GameResourceCache.WarmupStartup().MoveNext(), Is.False);
        }
        finally { RestoreState(previous); }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void StartupCompletionRequiresBothMainAndGameplayPreparation(bool main, bool gameplay)
    {
        var previous = CaptureState();
        try
        {
            Set("mainWarmupComplete", main);
            Set("gameplayWarmupComplete", gameplay);
            foreach (string name in new[] { "mainWarmupProgress", "gameplayWarmupProgress", "<WarmupProgress>k__BackingField", "<MonsterPreviewProgress>k__BackingField" })
                Set(name, 1f);
            Assert.That(GameResourceCache.StartupWarmupComplete, Is.EqualTo(main && gameplay));
            Assert.That(GameResourceCache.StartupProgress, Is.EqualTo(main && gameplay ? 1f : .99f).Within(.0001f));
        }
        finally { RestoreState(previous); }
    }

    [Test]
    public void StartupProgressIncludesAudioEffectsCommonTexturesAndMonsterPreviews()
    {
        var previous = CaptureState();
        try
        {
            Set("mainWarmupComplete", false);
            Set("gameplayWarmupComplete", false);
            Set("mainWarmupProgress", .5f);
            Set("gameplayWarmupProgress", .25f);
            Set("<WarmupProgress>k__BackingField", .75f);
            Set("<MonsterPreviewProgress>k__BackingField", .8f);
            Assert.That(GameResourceCache.StartupProgress, Is.EqualTo(.65f).Within(.0001f));
        }
        finally { RestoreState(previous); }
    }

    [Test]
    public void GameplayPreparationOnlyMarksCompleteAfterAllYieldedStagesReturn()
    {
        var previous = CaptureState();
        var priority = Application.backgroundLoadingPriority;
        var running = typeof(GameResourceCache).GetField("gameplayWarmupRunning", Flags);
        Assert.That(running.GetValue(null), Is.False);
        Set("mainWarmupComplete", true);
        Set("gameplayWarmupComplete", false);
        var preparation = GameResourceCache.WarmupGameplay();
        try
        {
            foreach (float expectedProgress in new[] { 0f, .5f, 1f })
            {
                Assert.That(preparation.MoveNext(), Is.True);
                Assert.That(preparation.Current, Is.InstanceOf<IEnumerator>());
                Assert.That(typeof(GameResourceCache).GetField("gameplayWarmupProgress", Flags).GetValue(null), Is.EqualTo(expectedProgress));
                Assert.That(GameResourceCache.StartupWarmupComplete, Is.False);
                Assert.That(GameResourceCache.StartupProgress, Is.LessThan(1f));
            }
            Assert.That(preparation.MoveNext(), Is.False);
            Assert.That(GameResourceCache.StartupWarmupComplete, Is.True);
            Assert.That(GameResourceCache.StartupProgress, Is.EqualTo(1f));
            Assert.That(running.GetValue(null), Is.False);
            Assert.That(Application.backgroundLoadingPriority, Is.EqualTo(priority));
            var repeated = GameResourceCache.WarmupGameplay();
            try
            {
                Assert.That(repeated.MoveNext(), Is.True, "Each frontend must check its current audio owner, including a second Play session without a domain reload.");
                Assert.That(GameResourceCache.StartupWarmupComplete, Is.False);
            }
            finally { ((IDisposable)repeated).Dispose(); }
            Assert.That(Application.backgroundLoadingPriority, Is.EqualTo(priority));
        }
        finally
        {
            ((IDisposable)preparation).Dispose();
            RestoreState(previous);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void InterruptedGameplayStageDoesNotReleaseStartupGate(int yieldedStages)
    {
        var previous = CaptureState();
        var priority = Application.backgroundLoadingPriority;
        Set("mainWarmupComplete", true);
        Set("gameplayWarmupComplete", false);
        var preparation = GameResourceCache.WarmupGameplay();
        try
        {
            for (int i = 0; i < yieldedStages; i++) Assert.That(preparation.MoveNext(), Is.True);
            ((IDisposable)preparation).Dispose();
            Assert.That(GameResourceCache.StartupWarmupComplete, Is.False);
            Assert.That(GameResourceCache.StartupProgress, Is.LessThan(1f));
            Assert.That(typeof(GameResourceCache).GetField("gameplayWarmupRunning", Flags).GetValue(null), Is.False);
            Assert.That(Application.backgroundLoadingPriority, Is.EqualTo(priority));
        }
        finally
        {
            ((IDisposable)preparation).Dispose();
            RestoreState(previous);
        }
    }

    [Test]
    public void GameplayWaiterRetriesAfterOwnerIsInterruptedAndRestoresLoadingPriority()
    {
        var previous = CaptureState();
        var running = typeof(GameResourceCache).GetField("gameplayWarmupRunning", Flags);
        Assert.That(running.GetValue(null), Is.False);
        Set("gameplayWarmupComplete", false);
        var priority = Application.backgroundLoadingPriority;
        var owner = GameResourceCache.WarmupGameplay();
        var waiter = GameResourceCache.WarmupGameplay();
        try
        {
            Assert.That(owner.MoveNext(), Is.True);
            Assert.That(waiter.MoveNext(), Is.True);
            Assert.That(waiter.Current, Is.Null);
            ((IDisposable)owner).Dispose();
            Assert.That(running.GetValue(null), Is.False);
            Assert.That(GameResourceCache.StartupWarmupComplete, Is.False);
            Assert.That(Application.backgroundLoadingPriority, Is.EqualTo(priority));
            Assert.That(waiter.MoveNext(), Is.True, "Interrupted preparation must be retried before navigation can proceed.");
            Assert.That(waiter.Current, Is.InstanceOf<IEnumerator>());
            Assert.That(running.GetValue(null), Is.True);
        }
        finally
        {
            ((IDisposable)owner).Dispose();
            ((IDisposable)waiter).Dispose();
            RestoreState(previous);
        }
        Assert.That(running.GetValue(null), Is.False);
        Assert.That(Application.backgroundLoadingPriority, Is.EqualTo(priority));
    }
}
