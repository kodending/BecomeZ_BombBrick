using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class CaptureFlowTests
{
    [Test]
    public void CaptureNetGradesUseApprovedDataDrivenChances()
    {
        GameMasterData.ClearCache();
        Assert.That(CaptureNetCatalog.All.Count,Is.EqualTo(2));
        Assert.That(
            CaptureNetCatalog.Get(CaptureNetCatalog.StandardId).SuccessChance,
            Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(
            CaptureNetCatalog.Get(CaptureNetCatalog.RareId).SuccessChance,
            Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(
            CaptureNetCatalog.EffectiveSuccessChance(
                CaptureNetCatalog.Get(CaptureNetCatalog.StandardId),true),
            Is.EqualTo(1f));
        Assert.That(
            CaptureNetCatalog.EffectiveSuccessChance(
                CaptureNetCatalog.Get(CaptureNetCatalog.RareId),true),
            Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void CaptureSuccessParcelUsesThreeCrispPixelFrames()
    {
        PropertyInfo property=typeof(MonsterAutoBattlePrototype).GetProperty(
            "CaptureParcelFrames",
            BindingFlags.Static|BindingFlags.NonPublic);
        Sprite[] frames=(Sprite[])property.GetValue(null);

        Assert.That(frames.Length,Is.EqualTo(3));
        Assert.That(frames.All(frame=>frame!=null),Is.True);
        Assert.That(frames.All(frame=>
            frame.texture.filterMode==FilterMode.Point),Is.True);
        PropertyInfo frontProperty=
            typeof(MonsterAutoBattlePrototype).GetProperty(
                "CaptureParcelOpenFront",
                BindingFlags.Static|BindingFlags.NonPublic);
        Assert.That(frontProperty.GetValue(null),Is.Not.Null);
        PropertyInfo shadeProperty=
            typeof(MonsterAutoBattlePrototype).GetProperty(
                "CaptureFocusShadeSprite",
                BindingFlags.Static|BindingFlags.NonPublic);
        Sprite shade=(Sprite)shadeProperty.GetValue(null);
        Assert.That(shade,Is.Not.Null);
        Assert.That(shade.texture.filterMode,Is.EqualTo(FilterMode.Point));
    }

    [Test]
    public void CaptureNetIsConsumedOnlyWhenAnAttemptResolves()
    {
        PlayerProfileSave profile=new PlayerProfileSave
        {
            captureNetStarterPackGranted=true
        };
        CaptureNetInventory.Grant(
            profile,CaptureNetCatalog.CommonId,2);

        Assert.That(
            CaptureNetInventory.Count(
                profile,CaptureNetCatalog.CommonId),
            Is.EqualTo(2));
        Assert.That(
            CaptureNetInventory.TryConsume(
                profile,CaptureNetCatalog.CommonId,out string error),
            Is.True,error);
        Assert.That(
            CaptureNetInventory.Count(
                profile,CaptureNetCatalog.CommonId),
            Is.EqualTo(1));
    }

    [Test]
    public void PendingCaptureCanResolveOnlyOnceAndCreatesNewPermanentMonster()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.Get("wilo");
        OwnedMonsterSave enemy=PlayerProfileStore.CreateMonster(
            definition,"capture_enemy");
        PendingMonsterCapture pending=new PendingMonsterCapture();
        pending.Arm(
            enemy.instanceId,
            CaptureNetCatalog.RareId,
            0.75f);

        bool first=pending.ResolveDefeat(
            enemy,definition,0.75f,0.74f,"captured_wilo");
        bool second=pending.ResolveDefeat(
            enemy,definition,0.75f,0f,"duplicate_capture");

        Assert.That(first,Is.True);
        Assert.That(second,Is.False);
        Assert.That(pending.resolved,Is.True);
        Assert.That(pending.capturedMonster,Is.Not.Null);
        Assert.That(pending.capturedMonster.isNew,Is.True);
        Assert.That(
            pending.capturedMonster.personalityId,
            Is.EqualTo(enemy.personalityId));
        var expected=ExpeditionCaptureSystem.CreateCapturedMonster(enemy,definition,"captured_wilo");
        foreach(MonsterIndividualStat stat in Enum.GetValues(typeof(MonsterIndividualStat)))
            Assert.That(MonsterStatQuality.Multiplier(pending.capturedMonster,stat),
                Is.EqualTo(MonsterStatQuality.Multiplier(expected,stat)));
        Assert.That(MonsterInstanceGenerator.MultipliersAreValid(pending.capturedMonster),Is.True);
    }

    [Test]
    public void ImmediateCaptureCommitSurvivesAFailedWaveAndReload()
    {
        string root=Path.Combine(
            Path.GetTempPath(),
            "BombBrickCaptureFlow_"+Guid.NewGuid().ToString("N"));
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            MonsterDefinition definition=database.Get("velok");
            OwnedMonsterSave enemy=PlayerProfileStore.CreateMonster(
                definition,"failed_wave_enemy");
            OwnedMonsterSave captured=
                ExpeditionCaptureSystem.CreateCapturedMonster(
                    enemy,definition,"failed_wave_capture");

            store.AddGeneratedMonster(profile,database,captured);
            PlayerProfileSave reloaded=
                new PlayerProfileStore(root).LoadOrCreate(database);

            Assert.That(
                reloaded.monsters.Any(monster=>
                    monster!=null&&
                    monster.instanceId=="failed_wave_capture"),
                Is.True);
            Assert.That(
                reloaded.monsters.Single(monster=>
                    monster.instanceId=="failed_wave_capture").isNew,
                Is.True);
        }
        finally
        {
            if(Directory.Exists(root))
                Directory.Delete(root,true);
        }
    }

    [Test]
    public void ExpeditionResultTracksEachCapturedMonsterOnce()
    {
        GameRunSession session=new GameRunSession();
        session.BeginExpedition("prototype_region",false);
        session.RegisterCapturedMonster("capture_a");
        session.RegisterCapturedMonster("capture_a");
        session.RegisterCapturedMonster("capture_b");

        Assert.That(
            session.CapturedMonsterInstanceIds,
            Is.EqualTo(new[]{"capture_a","capture_b"}));
    }

    [Test]
    public void ExpeditionResultUsesCapturedMonstersIdleSprite()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.monsters.First(monster=>
            monster!=null&&!string.IsNullOrWhiteSpace(monster.resourcePath));
        OwnedMonsterSave owned=PlayerProfileStore.CreateMonster(
            definition,"result_idle_test");
        GameObject root=new GameObject("Result Portrait Test");
        BombBrick.Game.Scenes.ResultSceneController controller=
            root.AddComponent<BombBrick.Game.Scenes.ResultSceneController>();
        try
        {
            Sprite portrait=(Sprite)typeof(
                    BombBrick.Game.Scenes.ResultSceneController)
                .GetMethod("ResolveCapturedMonsterPortrait",
                    BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(controller,new object[]{definition,owned});
            Assert.That(portrait,Is.Not.Null,definition.monsterId);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
