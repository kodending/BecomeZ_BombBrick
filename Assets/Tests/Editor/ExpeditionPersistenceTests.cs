using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ExpeditionPersistenceTests
{
    [Test]
    public void PermanentProfileAndOwnedMonsterSchemasExcludeRunProgression()
    {
        HashSet<string> profileFields=new HashSet<string>(
            typeof(PlayerProfileSave).GetFields(
                BindingFlags.Instance|BindingFlags.Public)
                .Select(field=>field.Name),
            StringComparer.Ordinal);
        HashSet<string> monsterFields=new HashSet<string>(
            typeof(OwnedMonsterSave).GetFields(
                BindingFlags.Instance|BindingFlags.Public)
                .Select(field=>field.Name),
            StringComparer.Ordinal);

        Assert.That(profileFields,Does.Not.Contain("expeditionRun"));
        Assert.That(profileFields,Does.Not.Contain("gold"));
        Assert.That(profileFields,Does.Not.Contain("lives"));
        Assert.That(monsterFields,Does.Not.Contain("rank"));
        Assert.That(monsterFields,Does.Contain("rank2TraitId"));
        Assert.That(monsterFields,Does.Contain("rank3TraitId"));
        Assert.That(monsterFields,Does.Not.Contain("itemInstanceIds"));
        Assert.That(monsterFields,Does.Not.Contain("transcended"));
    }

    [Test]
    public void RunFactoryCopiesIdentityWithoutCopyingPermanentPotential()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.Get(
            "raptor_reference_palette_v2");
        PlayerProfileSave profile=CreateSingleMonsterProfile(definition);
        OwnedMonsterSave owned=profile.monsters[0];
        owned.hpMultiplier=1.14f;
        owned.damageMultiplier=0.88f;
        owned.personalityId="bold";

        ExpeditionRunSave run=ExpeditionRunFactory.Create(
            profile,database,new[]{owned},"test_region",3,3,
            ExpeditionDifficulty.Hell);
        ExpeditionMonsterStateSave state=run.monsters.Single();
        HashSet<string> runMonsterFields=new HashSet<string>(
            typeof(ExpeditionMonsterStateSave).GetFields(
                BindingFlags.Instance|BindingFlags.Public)
                .Select(field=>field.Name),
            StringComparer.Ordinal);

        Assert.That(run.profileId,Is.EqualTo(profile.profileId));
        Assert.That(run.difficulty,Is.EqualTo(ExpeditionDifficulty.Hell));
        Assert.That(run.expeditionCurrency,Is.EqualTo(5));
        Assert.That(state.instanceId,Is.EqualTo(owned.instanceId));
        Assert.That(state.monsterId,Is.EqualTo(owned.monsterId));
        Assert.That(state.rank,Is.EqualTo(ExpeditionMonsterRank.Rank1));
        Assert.That(runMonsterFields,Does.Not.Contain("hpMultiplier"));
        Assert.That(runMonsterFields,Does.Not.Contain("damageMultiplier"));
        Assert.That(runMonsterFields,Does.Not.Contain("personalityId"));
        Assert.That(runMonsterFields,Does.Not.Contain("intrinsicTraitId"));
    }

    [Test]
    public void ActiveRunRoundTripsInASeparateCheckpointFile()
    {
        string root=CreateTemporaryRoot();
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            MonsterDefinition definition=database.Get(
                "raptor_reference_palette_v2");
            PlayerProfileSave profile=CreateSingleMonsterProfile(definition);
            float permanentHpMultiplier=profile.monsters[0].hpMultiplier;
            ExpeditionRunSave run=ExpeditionRunFactory.Create(
                profile,database,profile.monsters,"test_region");
            run.wave=6;
            run.lastCompletedWave=5;
            run.lives=2;
            run.expeditionCurrency=37;
            run.consecutiveWins=4;
            run.equipmentPityClears=2;
            run.maxDeploymentCount=5;
            run.checkpoint=ExpeditionRunCheckpoint.GrowthReward;
            run.growthShopWave=6;
            run.growthShopRerollCount=2;
            run.growthShopCandidateMonsterIds.Add(run.monsters[0].instanceId);
            run.monsters[0].rank=ExpeditionMonsterRank.Rank3;
            run.monsters[0].growthTotal=9;
            run.growthCardDraft.Add(new CombatGrowthCardDraftEntry
            {
                cardId="growth_sharp_claw",
                rarity=CombatGrowthCardRarity.Epic
            });
            run.augments.Add(new ExpeditionAugmentSelectionSave
            {
                augmentId="augment_battle_contract",acquiredWave=5
            });
            run.augmentDraftIds.Add("augment_battle_contract");
            ExpeditionItemInstanceSave item=new ExpeditionItemInstanceSave
            {
                instanceId="run_item_01",
                itemId="base_fang"
            };
            run.itemInstances.Add(item);
            run.monsters[0].itemInstanceIds[0]=item.instanceId;
            run.combatContributionHistory.Add(
                new ExpeditionWaveCombatContributionSave
                {
                    wave=5,
                    won=true,
                    allies=new List<ExpeditionCombatContributionSave>
                    {
                        new ExpeditionCombatContributionSave
                        {
                            monsterInstanceId=run.monsters[0].instanceId,
                            monsterId=run.monsters[0].monsterId,
                            displayName=definition.displayName,
                            damageDealt=321f,
                            damageTaken=45f,
                            healingReceived=12f,
                            comboActivations=3,
                            comboBonusDamage=27f,
                            survived=true
                        }
                    }
                });

            ExpeditionRunStore store=new ExpeditionRunStore(
                profile.profileId,root);
            store.SaveActive(run,profile,database);
            ExpeditionRunSave restored=store.LoadActive(
                profile,database);

            Assert.That(File.Exists(store.ActivePath),Is.True);
            Assert.That(restored,Is.Not.Null);
            Assert.That(restored.version,
                Is.EqualTo(ExpeditionRunStore.CurrentVersion));
            Assert.That(restored.runId,Is.EqualTo(run.runId));
            Assert.That(restored.wave,Is.EqualTo(6));
            Assert.That(restored.lastCompletedWave,Is.EqualTo(5));
            Assert.That(restored.lives,Is.EqualTo(2));
            Assert.That(restored.expeditionCurrency,Is.EqualTo(37));
            Assert.That(restored.consecutiveWins,Is.EqualTo(4));
            Assert.That(restored.equipmentPityClears,Is.EqualTo(2));
            Assert.That(restored.maxDeploymentCount,Is.EqualTo(5));
            Assert.That(restored.checkpoint,
                Is.EqualTo(ExpeditionRunCheckpoint.GrowthReward));
            Assert.That(restored.growthShopRerollCount,Is.EqualTo(2));
            Assert.That(restored.growthShopCandidateMonsterIds,
                Is.EqualTo(run.growthShopCandidateMonsterIds));
            Assert.That(restored.monsters[0].rank,
                Is.EqualTo(ExpeditionMonsterRank.Rank3));
            Assert.That(restored.monsters[0].itemInstanceIds[0],
                Is.EqualTo(item.instanceId));
            Assert.That(restored.growthCardDraft.Single().cardId,
                Is.EqualTo("growth_sharp_claw"));
            Assert.That(restored.combatContributionHistory.Single().wave,
                Is.EqualTo(5));
            Assert.That(restored.combatContributionHistory.Single().won,
                Is.True);
            Assert.That(restored.combatContributionHistory.Single().allies
                    .Single().damageDealt,
                Is.EqualTo(321f));
            Assert.That(restored.augments,Is.Empty);
            Assert.That(restored.augmentDraftIds,Is.Empty);
            Assert.That(profile.monsters[0].hpMultiplier,
                Is.EqualTo(permanentHpMultiplier).Within(0.0001f));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void InterruptedBattleRoundTripsWithItsLockedSeed()
    {
        string root=CreateTemporaryRoot();
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            MonsterDefinition definition=database.Get(
                "raptor_reference_palette_v2");
            PlayerProfileSave profile=CreateSingleMonsterProfile(definition);
            ExpeditionRunSave run=ExpeditionRunFactory.Create(
                profile,database,profile.monsters,"test_region");
            run.wave=4;
            run.checkpoint=ExpeditionRunCheckpoint.InBattle;
            run.activeBattleWave=4;
            run.activeBattleSeed=4721;
            ExpeditionRunStore store=new ExpeditionRunStore(
                profile.profileId,root);

            store.SaveActive(run,profile,database);
            ExpeditionRunSave restored=store.LoadActive(profile,database);

            Assert.That(restored.checkpoint,
                Is.EqualTo(ExpeditionRunCheckpoint.InBattle));
            Assert.That(restored.activeBattleWave,Is.EqualTo(4));
            Assert.That(restored.activeBattleSeed,Is.EqualTo(4721));
            restored.checkpoint=ExpeditionRunCheckpoint.Preparation;
            Assert.That(ExpeditionRunFactory.Normalize(
                restored,profile,database),Is.True);
            Assert.That(restored.activeBattleWave,Is.Zero);
            Assert.That(restored.activeBattleSeed,Is.Zero);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void CompletingRunArchivesResultAndRemovesActiveCheckpoint()
    {
        string root=CreateTemporaryRoot();
        try
        {
            MonsterDatabase database=Resources.Load<MonsterDatabase>(
                "MonsterData/MonsterDatabase");
            MonsterDefinition definition=database.Get(
                "raptor_reference_palette_v2");
            PlayerProfileSave profile=CreateSingleMonsterProfile(definition);
            ExpeditionRunSave run=ExpeditionRunFactory.Create(
                profile,database,profile.monsters,"test_region");
            ExpeditionRunStore store=new ExpeditionRunStore(
                profile.profileId,root);
            store.SaveActive(run,profile,database);

            store.Complete(run,true,profile,database);

            Assert.That(run.status,
                Is.EqualTo(ExpeditionRunStatus.Completed));
            Assert.That(File.Exists(store.ActivePath),Is.False);
            Assert.That(File.Exists(store.LastResultPath),Is.True);
            Assert.That(store.LoadActive(profile,database),Is.Null);
            Assert.That(profile.monsters,Has.Count.EqualTo(1));
            Assert.That(profile.monsters[0].instanceId,
                Is.EqualTo("persistence_owned_01"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void RunFromAnotherProfileIsRejected()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.Get(
            "raptor_reference_palette_v2");
        PlayerProfileSave profile=CreateSingleMonsterProfile(definition);
        ExpeditionRunSave run=ExpeditionRunFactory.Create(
            profile,database,profile.monsters,"test_region");
        run.profileId="another_profile";

        Assert.That(ExpeditionRunFactory.Normalize(
            run,profile,database),Is.False);
    }

    private static PlayerProfileSave CreateSingleMonsterProfile(
        MonsterDefinition definition)
    {
        PlayerProfileSave profile=new PlayerProfileSave
        {
            profileId="persistence_test_profile",
            monsters=new List<OwnedMonsterSave>(),
            expeditionPreset=new ExpeditionPresetSave()
        };
        OwnedMonsterSave owned=PlayerProfileStore.CreateMonster(
            definition,"persistence_owned_01");
        profile.monsters.Add(owned);
        profile.expeditionPreset.rosterInstanceIds.Add(owned.instanceId);
        profile.expeditionPreset.placements.Add(
            new FormationPlacementSave
            {
                instanceId=owned.instanceId,
                deployed=true,
                deploymentX=8,
                deploymentY=1,
                benchSlot=-1
            });
        return profile;
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),"BombBrickExpeditionStoreTests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if(!string.IsNullOrWhiteSpace(root)&&Directory.Exists(root))
            Directory.Delete(root,true);
    }
}
