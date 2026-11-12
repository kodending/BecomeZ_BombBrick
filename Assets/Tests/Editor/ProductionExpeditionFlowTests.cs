using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using BombBrick.Game.Scenes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ProductionExpeditionFlowTests
{
    [Test]
    public void BattleTutorialCompletionIsSeparateFromTownRewardGuidance()
    {
        MethodInfo method=typeof(TownSceneController).GetMethod(
            "HasCompletedFirstBattleTutorial",
            BindingFlags.Static|BindingFlags.NonPublic);
        PlayerProfileSave profile=new PlayerProfileSave
        {
            firstExpeditionGuideStep=
                PlayerProfileStore.FirstExpeditionGuideBattleStep,
            firstExpeditionCaptureGuideStep=2
        };

        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
        profile.firstExpeditionCaptureGuideStep=3;
        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
        profile.firstExpeditionCaptureGuideStep=4;
        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
        profile.firstExpeditionCaptureGuideStep=6;
        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
        profile.firstExpeditionCaptureGuideStep=7;
        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
        profile.firstExpeditionCaptureGuideStep=8;
        Assert.That(method.Invoke(null,new object[]{profile}),Is.True);
        profile.firstExpeditionCaptureGuideStep=0;
        profile.settledExpeditionRunIds.Add("finished_run");
        Assert.That(method.Invoke(null,new object[]{profile}),Is.True);
        profile.settledExpeditionRunIds.Clear();
        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
    }

    [Test]
    public void FirstBattleTutorialGuaranteesExactlyTwoItemDrops()
    {
        Assert.That(
            MonsterAutoBattlePrototype.ShouldGuaranteeFirstTutorialItemDrop(
                4,1,0),Is.True);
        Assert.That(
            MonsterAutoBattlePrototype.ShouldGuaranteeFirstTutorialItemDrop(
                4,1,1),Is.True);
        Assert.That(
            MonsterAutoBattlePrototype.ShouldGuaranteeFirstTutorialItemDrop(
                4,1,2),Is.False);
        Assert.That(
            MonsterAutoBattlePrototype.ShouldGuaranteeFirstTutorialItemDrop(
                4,2,0),Is.False);
        Assert.That(
            MonsterAutoBattlePrototype.ShouldGuaranteeFirstTutorialItemDrop(
                3,1,0),Is.False);
    }

    [Test]
    public void FirstItemTransferTutorialRequiresAnotherMonster()
    {
        Assert.That(MonsterAutoBattlePrototype.IsValidFirstTutorialItemTransfer(
            true,true,"owner_a","owner_b"),Is.True);
        Assert.That(MonsterAutoBattlePrototype.IsValidFirstTutorialItemTransfer(
            true,true,"owner_a","owner_a"),Is.False);
        Assert.That(MonsterAutoBattlePrototype.IsValidFirstTutorialItemTransfer(
            true,false,"owner_a","owner_b"),Is.False);
        Assert.That(MonsterAutoBattlePrototype.IsValidFirstTutorialItemTransfer(
            false,true,"owner_a","owner_b"),Is.False);
    }

    [Test]
    public void GrowthShopTutorialAppearsOnlyBeforeFirstCaptureGuide()
    {
        MethodInfo method=typeof(MonsterAutoBattlePrototype).GetMethod(
            "IsFirstGrowthShopTutorialPending",
            BindingFlags.Static|BindingFlags.NonPublic);
        PlayerProfileSave profile=new PlayerProfileSave
        {
            firstExpeditionGuideStep=
                PlayerProfileStore.FirstExpeditionGuideBattleStep
        };

        Assert.That(method.Invoke(null,new object[]{profile}),Is.True);
        profile.firstExpeditionCaptureGuideStep=1;
        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
        profile.firstExpeditionCaptureGuideStep=2;
        Assert.That(method.Invoke(null,new object[]{profile}),Is.False);
    }

    [Test]
    public void SlowMovementDoesNotFallBackToIdleBetweenFrames()
    {
        Assert.That(
            MonsterAutoBattlePrototype.HasMovementProgress(0.004f,1f/60f),
            Is.True);
        Assert.That(
            MonsterAutoBattlePrototype.HasMovementProgress(0f,1f/60f),
            Is.False);
    }

    [Test]
    public void NewProfileReceivesEightRandomStartersWithSixBasicQualityCompanions()
    {
        string root=Path.Combine(
            Path.GetTempPath(),
            "BombBrickFamilyStarterTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            foreach(var basic in profile.monsters.Take(6))
            {
                Assert.That(database.Get(basic.monsterId).enabled,Is.True);
                Assert.That(basic.isShiny,Is.False);
                Assert.That(MonsterStatQuality.OverallScore(basic),
                    Is.GreaterThanOrEqualTo(30f).And.LessThan(70f));
            }
            Assert.That(profile.monsters,Has.Count.EqualTo(8));
            Assert.That(profile.monsters.Select(m=>m.monsterId),Is.Unique);
            Assert.That(profile.starterSelectionPending,Is.True);
            Assert.That(profile.starterRerollInstanceIds,Has.Count.EqualTo(2));
            Assert.That(profile.expeditionPreset.rosterInstanceIds,Is.Empty);
            Assert.That(profile.firstExpeditionGuideInitialized,Is.True);
            Assert.That(profile.firstExpeditionGuideVersion,Is.EqualTo(2));
            Assert.That(profile.firstExpeditionGuideStep,Is.Zero);
            Assert.That(profile.firstExpeditionCaptureGuideStep,Is.Zero);
            foreach(CaptureNetDefinition net in CaptureNetCatalog.All)
                Assert.That(
                    CaptureNetInventory.Count(profile,net.itemId),
                    Is.Zero,
                    net.itemId);
        }
        finally
        {
            if(Directory.Exists(root))
                Directory.Delete(root,true);
        }
    }

    [Test]
    public void FirstCaptureGuideAndGrantedNetPersistAcrossReload()
    {
        string root=Path.Combine(
            Path.GetTempPath(),"BombBrickCaptureGuideTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            profile.firstExpeditionGuideStep=
                PlayerProfileStore.FirstExpeditionGuideBattleStep;
            profile.firstExpeditionCaptureGuideStep=1;
            CaptureNetInventory.Grant(
                profile,CaptureNetCatalog.StandardId,1);
            store.Save(profile);

            PlayerProfileSave reloaded=
                new PlayerProfileStore(root).LoadOrCreate(database);

            Assert.That(reloaded.firstExpeditionGuideStep,
                Is.EqualTo(PlayerProfileStore.FirstExpeditionGuideBattleStep));
            Assert.That(reloaded.firstExpeditionCaptureGuideStep,Is.EqualTo(1));
            Assert.That(
                CaptureNetInventory.Count(
                    reloaded,CaptureNetCatalog.StandardId),
                Is.EqualTo(1));
        }
        finally
        {
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    [Test]
    public void ExpeditionSessionTracksLaunchAndFinalSummary()
    {
        GameRunSession run=new GameRunSession();

        run.BeginExpedition(
            "test_region",true,ExpeditionDifficulty.Veteran);
        run.Tick(12.5f);

        Assert.That(run.ExpeditionRequested,Is.True);
        Assert.That(run.ResumeRequested,Is.True);
        Assert.That(run.RegionId,Is.EqualTo("test_region"));
        Assert.That(run.SelectedExpeditionDifficulty,
            Is.EqualTo(ExpeditionDifficulty.Veteran));
        Assert.That(run.ElapsedSeconds,Is.EqualTo(12.5f).Within(0.001f));

        run.FinishExpedition(
            ChallengeResult.Cleared,12,12,2,47);
        run.RegisterPlayerExperienceSettlement(35,95,60,0,2);

        Assert.That(run.Result,Is.EqualTo(ChallengeResult.Cleared));
        Assert.That(run.CompletedWave,Is.EqualTo(12));
        Assert.That(run.TotalWaves,Is.EqualTo(12));
        Assert.That(run.RemainingLives,Is.EqualTo(2));
        Assert.That(run.Gold,Is.EqualTo(47));
        Assert.That(run.HasPlayerExperienceSettlement,Is.True);
        Assert.That(run.PlayerExperienceBefore,Is.EqualTo(35));
        Assert.That(run.PlayerExperienceAfter,Is.EqualTo(95));
        Assert.That(run.PlayerExperienceEarned,Is.EqualTo(60));
        Assert.That(run.PlayerLevelBefore,Is.Zero);
        Assert.That(run.PlayerLevelAfter,Is.EqualTo(2));

        run.RegisterExpeditionProgressUnlock(
            "test_region","next_region",ExpeditionDifficulty.Normal);
        Assert.That(run.HasExpeditionProgressUnlockNotice,Is.True);
        Assert.That(run.ProgressUnlockSourceRegionId,Is.EqualTo("test_region"));
        Assert.That(run.ProgressUnlockRegionId,Is.EqualTo("next_region"));
        Assert.That(run.ProgressUnlockDifficulty,
            Is.EqualTo(ExpeditionDifficulty.Normal));

        run.LeaveExpeditionFlow();
        Assert.That(run.ExpeditionRequested,Is.False);
        Assert.That(run.ResumeRequested,Is.False);
        Assert.That(run.Result,Is.EqualTo(ChallengeResult.Cleared));
        Assert.That(run.HasExpeditionProgressUnlockNotice,Is.True);
        run.AcknowledgeExpeditionProgressUnlock();
        Assert.That(run.HasExpeditionProgressUnlockNotice,Is.False);
    }

    [Test]
    public void ProductionLaunchOverridesSerializedRandomTestRoster()
    {
        Assert.That(
            MonsterAutoBattlePrototype.ShouldUseRandomizedStartingRoster(
                true,false),
            Is.True);
        Assert.That(
            MonsterAutoBattlePrototype.ShouldUseRandomizedStartingRoster(
                true,true),
            Is.False);
        Assert.That(
            MonsterAutoBattlePrototype.ShouldUseRandomizedStartingRoster(
                false,false),
            Is.False);
    }

    [Test]
    public void RuntimeShadersRequiredByExpeditionArePackaged()
    {
        Assert.That(Resources.Load<Material>("RuntimeShaders/URPLit"),Is.Not.Null);
        Assert.That(Resources.Load<Material>("RuntimeShaders/URPUnlit"),Is.Not.Null);
        Assert.That(Resources.Load<Material>("RuntimeShaders/URPParticlesUnlit"),Is.Not.Null);
    }

    [Test]
    public void ProductionScenesAreInPlayableBuildOrder()
    {
        string[] enabled=EditorBuildSettings.scenes
            .Where(scene=>scene.enabled)
            .Select(scene=>scene.path)
            .ToArray();

        Assert.That(enabled,Is.EqualTo(new[]
        {
            GameSceneCatalog.BootPath,
            GameSceneCatalog.TownPath,
            GameSceneCatalog.ExpeditionBattlePath,
            GameSceneCatalog.ResultPath
        }));
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                GameSceneCatalog.ExpeditionBattlePath),
            Is.Not.Null);
    }

    [Test]
    public void ProductionBattleSceneDoesNotForceEnvironmentPreview()
    {
        string sceneYaml=File.ReadAllText(
            GameSceneCatalog.ExpeditionBattlePath);

        Assert.That(
            sceneYaml,
            Does.Not.Match(@"(?m)^[ \t]*environmentPreviewRegionId:[ \t]*\S+"));
    }

    [Test]
    public void TownRosterKeepsDroppedSlotsThroughMoveSwapRemovalAndReload()
    {
        string root=Path.Combine(Path.GetTempPath(),"BombBrickTownSlotTests",
            Guid.NewGuid().ToString("N"));
        GameObject host=new GameObject("Town slot regression");
        host.SetActive(false);
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            profile.firstExpeditionGuideStep=1;
            string[] ids=profile.monsters.Select(monster=>monster.instanceId).ToArray();
            TownSceneController town=host.AddComponent<TownSceneController>();
            BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(TownSceneController).GetField("profile",flags).SetValue(town,profile);
            typeof(TownSceneController).GetField("profileStore",flags).SetValue(town,store);
            typeof(TownSceneController).GetField("database",flags).SetValue(town,database);
            MethodInfo place=typeof(TownSceneController).GetMethod("PlaceMonsterInRoster",flags);
            MethodInfo remove=typeof(TownSceneController).GetMethod("RemoveMonsterFromRoster",flags);

            place.Invoke(town,new object[]{ids[0],7});
            Assert.That(profile.expeditionPreset.rosterInstanceIds,
                Is.EqualTo(new[]{"","","","","","","",ids[0]}));
            Assert.That(profile.expeditionPreset.RosterCount,Is.EqualTo(1));
            Assert.That(profile.firstExpeditionGuideStep,Is.EqualTo(1),
                "An occupied eighth slot must not count as a full roster.");
            place.Invoke(town,new object[]{ids[1],3});
            place.Invoke(town,new object[]{ids[0],5});
            Assert.That(profile.expeditionPreset.rosterInstanceIds[3],Is.EqualTo(ids[1]));
            Assert.That(profile.expeditionPreset.rosterInstanceIds[5],Is.EqualTo(ids[0]));
            place.Invoke(town,new object[]{ids[1],5});
            Assert.That(profile.expeditionPreset.rosterInstanceIds[3],Is.EqualTo(ids[0]));
            Assert.That(profile.expeditionPreset.rosterInstanceIds[5],Is.EqualTo(ids[1]));
            remove.Invoke(town,new object[]{ids[0]});
            Assert.That(profile.expeditionPreset.rosterInstanceIds[3],Is.Empty);
            Assert.That(profile.expeditionPreset.rosterInstanceIds[5],Is.EqualTo(ids[1]));
            place.Invoke(town,new object[]{ids[2],5});
            place.Invoke(town,new object[]{ids[3],0});

            PlayerProfileSave restored=store.LoadOrCreate(database);
            Assert.That(restored.expeditionPreset.rosterInstanceIds,
                Is.EqualTo(new[]{ids[3],"","","","",ids[2]}));
            Assert.That(restored.expeditionPreset.RosterCount,Is.EqualTo(2));
            Assert.That(restored.expeditionPreset.placements.Select(item=>item.instanceId),
                Is.EquivalentTo(new[]{ids[3],ids[2]}));
            Assert.That(restored.monsters,Has.Count.EqualTo(8));
            Assert.That(restored.firstExpeditionGuideStep,Is.EqualTo(1));

            // Fill the remaining six slots in a nonsequential order.
            int[] slots={7,1,6,2,4,3};
            string[] remaining=ids.Where(id=>id!=ids[3]&&id!=ids[2]).ToArray();
            for(int i=0;i<slots.Length;i++)
            {
                place.Invoke(town,new object[]{remaining[i],slots[i]});
                Assert.That(profile.expeditionPreset.rosterInstanceIds[slots[i]],
                    Is.EqualTo(remaining[i]));
                Assert.That(profile.expeditionPreset.RosterCount,Is.EqualTo(3+i));
                Assert.That(profile.firstExpeditionGuideStep,Is.EqualTo(i==5?2:1));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    [Test]
    public void SparseRosterNormalizationClearsInvalidEntriesWithoutShiftingSlots()
    {
        string root=Path.Combine(Path.GetTempPath(),"BombBrickTownSlotTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            string first=profile.monsters[0].instanceId;
            string last=profile.monsters[1].instanceId;
            profile.expeditionPreset.rosterInstanceIds=
                new List<string>{first,null,"missing_monster",first,last," "};
            store.Save(profile);

            PlayerProfileSave restored=store.LoadOrCreate(database);
            Assert.That(restored.expeditionPreset.rosterInstanceIds,
                Is.EqualTo(new[]{first,"","","",last}));
            Assert.That(restored.expeditionPreset.RosterCount,Is.EqualTo(2));
            Assert.That(restored.expeditionPreset.placements.Select(item=>item.instanceId),
                Is.EqualTo(new[]{first,last}));
            Assert.That(store.LoadOrCreate(database).expeditionPreset.rosterInstanceIds,
                Is.EqualTo(restored.expeditionPreset.rosterInstanceIds));
            Assert.Throws<InvalidOperationException>(()=>store.SetExpeditionRoster(
                restored,database,new[]{first,"","unknown"}));
            Assert.Throws<InvalidOperationException>(()=>store.SetExpeditionRoster(
                restored,database,new[]{first,"",first}));
        }
        finally
        {
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    [Test]
    public void StarterRosterReplacementKeepsEightUniqueMonsterTypes()
    {
        string root=Path.Combine(
            Path.GetTempPath(),
            "BombBrickProductionFlowTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            List<string> original=profile.monsters
                .Where(monster=>monster!=null)
                .Select(monster=>monster.instanceId)
                .Take(PlayerProfileStore.ExpeditionRosterSize)
                .ToList();
            HashSet<string> keptMonsterIds=original.Take(7)
                .Select(instanceId=>profile.monsters.First(
                    monster=>monster.instanceId==instanceId).monsterId)
                .ToHashSet(StringComparer.Ordinal);
            MonsterDefinition additionalDefinition=database.monsters.First(
                definition=>definition!=null&&definition.enabled&&
                    !keptMonsterIds.Contains(definition.monsterId));
            OwnedMonsterSave additional=store.AcquireMonster(
                profile,database,additionalDefinition.monsterId);
            additional.hpMultiplier=1.17f;

            List<string> replacement=new List<string>(original);
            replacement[7]=additional.instanceId;
            store.SetExpeditionRoster(profile,database,replacement);

            Assert.That(
                profile.expeditionPreset.rosterInstanceIds,
                Has.Count.EqualTo(8));
            Assert.That(
                profile.expeditionPreset.rosterInstanceIds
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(8));
            Assert.That(
                profile.expeditionPreset.rosterInstanceIds[7],
                Is.EqualTo(additional.instanceId));
            Assert.That(
                profile.monsters.Find(
                    monster=>monster.instanceId==additional.instanceId)
                    .hpMultiplier,
                Is.EqualTo(1.17f).Within(0.001f));
            Assert.That(
                profile.expeditionPreset.placements
                    .Select(placement=>placement.instanceId),
                Is.EquivalentTo(
                    profile.expeditionPreset.rosterInstanceIds));

            List<string> duplicate=new List<string>(replacement);
            duplicate[7]=duplicate[0];
            Assert.Throws<InvalidOperationException>(()=>
                store.SetExpeditionRoster(profile,database,duplicate));

            OwnedMonsterSave duplicateType=store.AcquireMonster(
                profile,database,profile.monsters.First(monster=>
                    monster.instanceId==replacement[0]).monsterId);
            List<string> duplicateSpecies=new List<string>(replacement);
            duplicateSpecies[7]=duplicateType.instanceId;
            Assert.Throws<InvalidOperationException>(()=>
                store.SetExpeditionRoster(profile,database,duplicateSpecies));

            profile.expeditionPreset.rosterInstanceIds=duplicateSpecies;
            store.Save(profile);
            PlayerProfileSave normalized=store.LoadOrCreate(database);
            Assert.That(normalized.expeditionPreset.rosterInstanceIds
                .Select(instanceId=>normalized.monsters.First(monster=>
                    monster.instanceId==instanceId).monsterId)
                .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(normalized.expeditionPreset.rosterInstanceIds.Count));

            List<string> reduced=replacement.Take(3).ToList();
            store.SetExpeditionRoster(normalized,database,reduced);
            PlayerProfileSave restored=store.LoadOrCreate(database);
            Assert.That(restored.expeditionPreset.configured,Is.True);
            Assert.That(
                restored.expeditionPreset.rosterInstanceIds,
                Is.EqualTo(reduced));
        }
        finally
        {
            if(Directory.Exists(root))
                Directory.Delete(root,true);
        }
    }
}
