using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerProfileV6Tests
{
    [Test]
    public void SyncedMasterDatabaseContainsApprovedCoreTables()
    {
        GameMasterDatabase database=GameMasterData.Database;
        Assert.That(database,Is.Not.Null);
        Assert.That(database.drops,Has.Count.EqualTo(2));
        Assert.That(database.skills,Has.Count.EqualTo(24));
        Assert.That(database.growthCardRarities,Has.Count.EqualTo(4));
        Assert.That(database.waveScalings,Has.Count.EqualTo(60));
        Assert.That(database.traits,Has.Count.EqualTo(202));
        Assert.That(database.traits.Count(row=>row!=null&&
            string.Equals(row.category,"Intrinsic",StringComparison.OrdinalIgnoreCase)),
            Is.EqualTo(80));
        Assert.That(database.personalities,Has.Count.EqualTo(14));
        Assert.That(database.items,Has.Count.EqualTo(45));
    }

    [Test]
    public void TranscendedMonsterIsDisplayedWithFourStars()
    {
        ExpeditionMonsterStateSave state=new ExpeditionMonsterStateSave{
            rank=ExpeditionMonsterRank.Rank3,transcended=true};

        Assert.That(MonsterAutoBattlePrototype.DisplayStarCount(state),Is.EqualTo(4));

        state.transcended=false;
        Assert.That(MonsterAutoBattlePrototype.DisplayStarCount(state),Is.EqualTo(3));
    }

    [Test]
    public void EveryEquipmentAndSpecialItemHasAnInventoryIcon()
    {
        foreach(ItemMasterRow item in GameMasterData.Database.items)
            Assert.That(Resources.Load<Texture2D>("UI/ItemIcons/"+item.id),Is.Not.Null,
                item.id);
        string[] specialIds=
        {
            "special_combined_cast_core","special_return_mark",
            "special_corruption_catalyst",
            "special_trait_recombobulator","special_golden_dismantler"
        };
        foreach(string itemId in specialIds)
            Assert.That(Resources.Load<Texture2D>("UI/SpecialItemIcons/"+itemId),
                Is.Not.Null,itemId);
        string[] systemIds=
        {
            "gold","augment","twin_command","fusion",
            "reroll","deployment","life","shop"
        };
        foreach(string iconId in systemIds)
            Assert.That(Resources.Load<Texture2D>("UI/SystemIcons/"+iconId),
                Is.Not.Null,iconId);
    }

    [TestCase("RaptorReferencePaletteV2Enemy.prefab")]
    [TestCase("VelokEnemy.prefab")]
    [TestCase("RaputoEnemy.prefab")]
    [TestCase("WiloEnemy.prefab")]
    public void PixelRaptors001To004KeepDeathFramesForKnockdown(
        string prefabName)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/"+prefabName);
        GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<
                    SpriteMonsterAnimationDriver>(true);
            Assert.That(driver,Is.Not.Null,prefabName);
            Assert.That(driver.DeathFrameCount,Is.EqualTo(6),prefabName);
            Assert.That(
                instance.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty,prefabName);
            Assert.That(
                instance.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                Is.Empty,prefabName);

            driver.PlayKnockdown(1f);

            Assert.That(
                driver.CurrentMotionName,
                Is.EqualTo("Death"),
                prefabName);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static Bounds CombinedBounds(IEnumerable<Renderer> renderers)
    {
        Bounds bounds=new Bounds();
        bool initialized=false;
        foreach(Renderer renderer in renderers)
        {
            if(renderer==null||!renderer.enabled)continue;
            if(!initialized)
            {
                bounds=renderer.bounds;
                initialized=true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        Assert.That(initialized,Is.True);
        return bounds;
    }

    [Test]
    public void SyncedExpeditionConfigurationMatchesCurrentDesign()
    {
        Assert.That(GameMasterData.ExpeditionInt("wave_count",-1),Is.EqualTo(12));
        Assert.That(GameMasterData.ExpeditionInt("starting_lives",-1),Is.EqualTo(3));
        Assert.That(GameMasterData.ExpeditionInt("roster_size",-1),Is.EqualTo(8));
        Assert.That(GameMasterData.ExpeditionInt("max_deployment_limit",-1),Is.EqualTo(8));
        Assert.That(GameMasterData.ExpeditionInt("deployment_tile_count",-1),Is.EqualTo(15));
        Assert.That(ExpeditionItemSystem.MaxSlots,Is.EqualTo(3));
        Assert.That(CombatGrowthCardDraftService.DraftSize,Is.EqualTo(3));
        Assert.That(GameMasterData.ExpeditionInt("growth_card_reroll_cost",-1),
            Is.EqualTo(1));
        Assert.That(ExpeditionShopSystem.StockCount,Is.EqualTo(5));
        Assert.That(ExpeditionShopSystem.ItemCost,Is.EqualTo(5));
        Assert.That(Enumerable.Range(1,12).Where(ExpeditionShopSystem.IsShopWave),
            Is.EqualTo(new[]{3,6,9,11}));
    }

    [Test]
    public void ExpeditionCurrencyUpgradesDeploymentWithEscalatingCosts()
    {
        Assert.That(ExpeditionDeploymentSystem.MaximumLimit,Is.EqualTo(8));
        Assert.That(new[]{3,4,5,6,7}.Select(ExpeditionDeploymentSystem.NextUpgradeCost),
            Is.EqualTo(new[]{3,5,8,12,16}));
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            maxDeploymentCount=3,
            expeditionCurrency=44
        };
        for(int expected=4;expected<=8;expected++)
        {
            Assert.That(ExpeditionDeploymentSystem.TryUpgrade(run),Is.True);
            Assert.That(run.maxDeploymentCount,Is.EqualTo(expected));
        }
        Assert.That(run.expeditionCurrency,Is.Zero);
        Assert.That(ExpeditionDeploymentSystem.TryUpgrade(run),Is.False);

        ExpeditionRunSave poorRun=new ExpeditionRunSave
        {
            maxDeploymentCount=3,
            expeditionCurrency=2
        };
        Assert.That(ExpeditionDeploymentSystem.TryUpgrade(poorRun),Is.False);
        Assert.That(poorRun.maxDeploymentCount,Is.EqualTo(3));
        Assert.That(poorRun.expeditionCurrency,Is.EqualTo(2));
    }

    [Test]
    public void ItemShopHasFiveSlotsAndEscalatingRefreshCosts()
    {
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="shop_test",
            wave=3,
            expeditionCurrency=20
        };
        ExpeditionShopSystem.EnsureStock(run,11);
        Assert.That(run.shopItemIds,Has.Count.EqualTo(5));
        Assert.That(run.shopItemIds.All(id=>GameMasterData.Item(id)?.IsBase==true),Is.True);
        Assert.That(ExpeditionShopSystem.RefreshCost(run),Is.Zero);

        Assert.That(ExpeditionShopSystem.TryRefresh(run,12,out string firstError),Is.True,firstError);
        Assert.That(run.expeditionCurrency,Is.EqualTo(20));
        Assert.That(ExpeditionShopSystem.RefreshCost(run),Is.EqualTo(1));
        Assert.That(ExpeditionShopSystem.TryRefresh(run,13,out string secondError),Is.True,secondError);
        Assert.That(run.expeditionCurrency,Is.EqualTo(19));
        Assert.That(ExpeditionShopSystem.RefreshCost(run),Is.EqualTo(2));
        Assert.That(ExpeditionShopSystem.TryRefresh(run,14,out string thirdError),Is.True,thirdError);
        Assert.That(run.expeditionCurrency,Is.EqualTo(17));
        Assert.That(ExpeditionShopSystem.RefreshCost(run),Is.EqualTo(3));

        Assert.That(ExpeditionShopSystem.TryPurchase(
            run,0,out ExpeditionItemInstanceSave purchased,out string purchaseError),Is.True,purchaseError);
        Assert.That(purchased,Is.Not.Null);
        Assert.That(run.expeditionCurrency,Is.EqualTo(12));
        Assert.That(run.shopItemIds[0],Is.Empty);
        Assert.That(run.inventoryItemInstanceIds,Does.Contain(purchased.instanceId));

        string[] stock=run.shopItemIds.ToArray();
        run.expeditionCurrency=2;
        Assert.That(ExpeditionShopSystem.TryRefresh(run,15,out _),Is.False);
        Assert.That(run.expeditionCurrency,Is.EqualTo(2));
        Assert.That(run.shopRefreshCount,Is.EqualTo(3));
        Assert.That(run.shopItemIds,Is.EqualTo(stock));
    }

    [Test]
    public void EveryRegionHasTwelveCompositionDrivenWavesUsingItsOwnPool()
    {
        MonsterDatabase monsterDatabase=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            HashSet<string> pool=new HashSet<string>(region.MonsterIds());
            for(int wave=1;wave<=12;wave++)
            {
                WaveScalingMasterRow scaling=
                    GameMasterData.WaveScaling(region.id,wave);
                Assert.That(scaling,Is.Not.Null,region.id+" Wave "+wave);
                List<string> composition=
                    HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
                        region.id,wave,scaling.enemyCount,
                        "profile_v6_"+region.id,monsterDatabase);
                Assert.That(composition,Has.Count.EqualTo(scaling.enemyCount));
                Assert.That(composition.All(id=>
                    monsterDatabase.Get(id)!=null&&pool.Contains(id)),Is.True);
                Assert.That(scaling.hpMultiplier,Is.EqualTo(1f));
                Assert.That(scaling.attackDamageMultiplier,Is.EqualTo(1f));
                Assert.That(scaling.attackSpeedMultiplier,Is.EqualTo(1f));
                Assert.That(scaling.moveSpeedMultiplier,Is.EqualTo(1f));
            }
            Assert.That(GameMasterData.WaveScaling(region.id,1).enemyCount,Is.EqualTo(2));
            Assert.That(GameMasterData.WaveScaling(region.id,12).enemyCount,Is.EqualTo(8));
            Assert.That(GameMasterData.WaveScaling(region.id,5).hpMultiplier,
                Is.EqualTo(1f).Within(.001f));
            Assert.That(GameMasterData.WaveScaling(region.id,8).hpMultiplier,
                Is.EqualTo(1f).Within(.001f));
            Assert.That(GameMasterData.WaveScaling(region.id,12).hpMultiplier,
                Is.EqualTo(1f).Within(.001f));
        }
        Assert.That(HunterRegionCatalog.All.SelectMany(region=>region.MonsterIds())
            .Distinct().Count(),Is.EqualTo(80));
        Assert.That(HunterRegionCatalog.All.Select(region=>
                HunterRegionCatalog.EnemyHealthMultiplier(region.id)),
            Is.EqualTo(new[]{1f,1f,1f,1f,1f}));
        Assert.That(HunterRegionCatalog.All.Select(region=>
                HunterRegionCatalog.EnemyAttackMultiplier(region.id)),
            Is.EqualTo(new[]{1f,1f,1f,1f,1f}));
        Assert.That(HunterRegionCatalog.All.Select(region=>
                HunterRegionCatalog.Rule(region.id)),
            Is.EqualTo(new[]{HunterRegionRule.None,HunterRegionRule.PoisonFog,
                HunterRegionRule.CrimsonHeat,HunterRegionRule.FrostWave,
                HunterRegionRule.SkyResonance}));
    }

    [Test]
    public void KnockbackInterpolationUsesStableEndpoints()
    {
        MethodInfo method=typeof(MonsterAutoBattlePrototype).GetMethod(
            "EvaluateDisplacementPosition",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.That(method,Is.Not.Null);
        Vector3 start=new Vector3(1f,0f,2f);
        Vector3 end=new Vector3(5f,0f,6f);
        Assert.That((Vector3)method.Invoke(null,new object[]{start,end,0f}),Is.EqualTo(start));
        Assert.That((Vector3)method.Invoke(null,new object[]{start,end,0.5f}),
            Is.EqualTo(Vector3.Lerp(start,end,0.5f)));
        Assert.That((Vector3)method.Invoke(null,new object[]{start,end,1f}),Is.EqualTo(end));
    }

    [Test]
    public void Raptors001To004UsePixelSpritesAndSharedAnimation()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        RuntimeAnimatorController sharedController=
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                PixelRaptorFamilySetup.ControllerPath);
        for(int i=0;i<UltraLowSDRaptorSetup.MonsterIds.Length;i++)
        {
            string monsterId=UltraLowSDRaptorSetup.MonsterIds[i];
            MonsterDefinition definition=database.Get(monsterId);
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                UltraLowSDRaptorSetup.PrefabPaths[i]);
            Assert.That(definition,Is.Not.Null,monsterId);
            Assert.That(definition.serialNumber,
                Is.EqualTo((i+1).ToString("000")),
                monsterId);
            Assert.That(definition.animationSetId,
                Is.EqualTo(PixelRaptorFamilySetup.AnimationSetId),
                monsterId);
            Assert.That(prefab,Is.Not.Null,monsterId);
            Transform visual=prefab.transform.Find("Visual");
            Assert.That(visual,Is.Not.Null,monsterId);
            Assert.That(
                visual.GetComponent<SpriteRenderer>(),
                Is.Not.Null,monsterId);
            Assert.That(
                visual.GetComponent<SpriteMonsterAnimationDriver>(),
                Is.Not.Null,monsterId);

            Animator animator=prefab.GetComponent<Animator>();
            Assert.That(animator,Is.Not.Null,monsterId);
            Assert.That(animator.runtimeAnimatorController,
                Is.SameAs(sharedController),monsterId);
            Assert.That(animator.cullingMode,Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));

            SpriteMonsterAnimationEventRelay relay=
                prefab.GetComponent<SpriteMonsterAnimationEventRelay>();
            Assert.That(relay,Is.Not.Null,monsterId);
            Assert.That(relay.Driver,Is.SameAs(
                visual.GetComponent<SpriteMonsterAnimationDriver>()),monsterId);
        }

        Assert.That(AssetDatabase.GetAssetPath(sharedController),
            Is.EqualTo(PixelRaptorFamilySetup.ControllerPath));
        Assert.DoesNotThrow(PixelRaptorFamilySetup.ValidateAll);
    }

    [Test]
    public void BattleCameraUsesThirtyFiveDegreePitch()
    {
        Assert.That(MonsterAutoBattlePrototype.CameraPitchDegrees,Is.EqualTo(35f));
    }

    [Test]
    public void ConstrainedCcdIkMovesVirtualFootTowardTargetWithinJointLimits()
    {
        GameObject root=new GameObject("CCD IK test root");
        GameObject upper=new GameObject("Upper");
        GameObject lower=new GameObject("Lower");
        try
        {
            upper.transform.SetParent(root.transform,false);
            lower.transform.SetParent(upper.transform,false);
            lower.transform.localPosition=Vector3.right;
            Vector3 endLocal=Vector3.right;
            Vector3 target=new Vector3(1.35f,1.1f,0f);
            float before=Vector3.Distance(lower.transform.TransformPoint(endLocal),target);
            Quaternion[] animationPose={Quaternion.identity,Quaternion.identity};
            float[] limits={35f,55f};

            float after=ConstrainedCcdIkSolver.Solve(
                new[]{upper.transform,lower.transform},lower.transform,endLocal,target,
                animationPose,limits,6,1f);

            Assert.That(after,Is.LessThan(before));
            Assert.That(Quaternion.Angle(animationPose[0],upper.transform.localRotation),
                Is.LessThanOrEqualTo(limits[0]+0.01f));
            Assert.That(Quaternion.Angle(animationPose[1],lower.transform.localRotation),
                Is.LessThanOrEqualTo(limits[1]+0.01f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void EveryActiveMonsterIntrinsicIsMastered()
    {
        Assert.DoesNotThrow(IntrinsicTraitMasterSetup.Validate);
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        Assert.That(database.monsters.Where(monster=>monster!=null&&monster.enabled)
            .All(monster=>MonsterTraitCatalog.Get(monster.intrinsicTraitId)!=null),Is.True);
    }

    [Test]
    public void CombatStatusStateCapsStacksRefreshesDurationAndConsumesThem()
    {
        CombatStatusState state=new CombatStatusState();
        state.ApplyStacks(CombatStatusKind.StickySilk,4f,1,3,"source",0f,0f,true);
        state.Tick(1f);
        state.ApplyStacks(CombatStatusKind.StickySilk,4f,3,3,"source",0f,0f,true);

        CombatStatusEffect silk=state.Get(CombatStatusKind.StickySilk);
        Assert.That(silk,Is.Not.Null);
        Assert.That(silk.stacks,Is.EqualTo(3));
        Assert.That(silk.remaining,Is.EqualTo(4f).Within(0.0001f));
        Assert.That(state.ConsumeStacks(CombatStatusKind.StickySilk,2),Is.EqualTo(2));
        Assert.That(state.Get(CombatStatusKind.StickySilk).stacks,Is.EqualTo(1));
        Assert.That(state.ConsumeStacks(CombatStatusKind.StickySilk,3),Is.EqualTo(1));
        Assert.That(state.Has(CombatStatusKind.StickySilk),Is.False);
    }

    [Test]
    public void CombatStatusStateTicksPeriodicEffectsAndExpiresThem()
    {
        CombatStatusState state=new CombatStatusState();
        state.ApplyStacks(CombatStatusKind.Poison,2.1f,2,3,null,0f,1f,false);
        int ticks=0;

        state.Tick(1.05f,effect=>{if(effect.kind==CombatStatusKind.Poison)ticks++;});
        state.Tick(1.1f,effect=>{if(effect.kind==CombatStatusKind.Poison)ticks++;});

        Assert.That(ticks,Is.EqualTo(2));
        Assert.That(state.Has(CombatStatusKind.Poison),Is.False);
    }

    [Test]
    public void PoisonStacksCombineTheirPerStackTickDamage()
    {
        CombatStatusState state=new CombatStatusState();
        state.ApplyStacks(CombatStatusKind.Poison,4f,1,
            CombatStatusState.MaximumPoisonStacks,null,2f,1f,true);
        state.ApplyStacks(CombatStatusKind.Poison,4f,1,
            CombatStatusState.MaximumPoisonStacks,null,4f,1f,true);

        CombatStatusEffect poison=state.Get(CombatStatusKind.Poison);
        Assert.That(poison.stacks,Is.EqualTo(2));
        Assert.That(poison.magnitude*poison.stacks,Is.EqualTo(6f));
    }

    [Test]
    public void CombatStatusDebuffImmunityBlocksHarmfulEffectsButKeepsShields()
    {
        CombatStatusState state=new CombatStatusState();
        state.ApplyTimed(CombatStatusKind.Root,2f);
        state.DebuffImmune=true;
        state.ApplyStacks(CombatStatusKind.Poison,3f,1,3);
        state.ApplyShield(3f,12f);

        Assert.That(state.Has(CombatStatusKind.Root),Is.False);
        Assert.That(state.Has(CombatStatusKind.Poison),Is.False);
        Assert.That(state.Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(12f));

        state.DebuffImmune=false;
        state.ApplyTimed(CombatStatusKind.Root,1f);
        Assert.That(state.Has(CombatStatusKind.Root),Is.True);
    }

    [Test]
    public void CombatStatusStateRemovesEveryHarmfulEffectButKeepsBuffs()
    {
        CombatStatusState state=new CombatStatusState();
        state.ApplyStacks(CombatStatusKind.Poison,4f,3,5);
        state.ApplyTimed(CombatStatusKind.Burning,4f);
        state.ApplyTimed(CombatStatusKind.Root,2f);
        state.ApplyTimed(CombatStatusKind.Marked,3f);
        state.ApplyTimed(CombatStatusKind.Displaced,2f);
        state.ApplyTimed(CombatStatusKind.ComboRally,3f);
        state.ApplyShield(3f,10f);

        Assert.That(state.RemoveAllHarmful(),Is.EqualTo(5));
        Assert.That(state.Has(CombatStatusKind.Poison),Is.False);
        Assert.That(state.Has(CombatStatusKind.Burning),Is.False);
        Assert.That(state.Has(CombatStatusKind.Root),Is.False);
        Assert.That(state.Has(CombatStatusKind.Marked),Is.False);
        Assert.That(state.Has(CombatStatusKind.Displaced),Is.False);
        Assert.That(state.Has(CombatStatusKind.ComboRally),Is.True);
        Assert.That(state.Has(CombatStatusKind.Shield),Is.True);
    }

    [Test]
    public void CombatStatusStateCanHalveHarmfulDurationsAndCleanseOneEffect()
    {
        CombatStatusState state=new CombatStatusState{harmfulDurationMultiplier=.5f};
        state.ApplyTimed(CombatStatusKind.Root,4f);
        state.ApplyStacks(CombatStatusKind.Poison,6f,1,5);
        state.ApplyTimed(CombatStatusKind.ComboRally,4f);

        Assert.That(state.Get(CombatStatusKind.Root).remaining,
            Is.EqualTo(2f).Within(.0001f));
        Assert.That(state.Get(CombatStatusKind.Poison).remaining,
            Is.EqualTo(3f).Within(.0001f));
        Assert.That(state.Get(CombatStatusKind.ComboRally).remaining,
            Is.EqualTo(4f).Within(.0001f));
        Assert.That(state.HarmfulCount,Is.EqualTo(2));
        Assert.That(state.RemoveOneHarmful(),Is.True);
        Assert.That(state.HarmfulCount,Is.EqualTo(1));
        Assert.That(state.Has(CombatStatusKind.ComboRally),Is.True);
    }

    [Test]
    public void CombatStatusShieldAbsorbsDamageAndRemovesEmptyShield()
    {
        CombatStatusState state=new CombatStatusState();
        state.ApplyShield(4f,18f,"silanti");

        Assert.That(state.AbsorbShield(7f),Is.EqualTo(7f).Within(0.0001f));
        Assert.That(state.Get(CombatStatusKind.Shield).magnitude,
            Is.EqualTo(11f).Within(0.0001f));
        Assert.That(state.AbsorbShield(20f),Is.EqualTo(11f).Within(0.0001f));
        Assert.That(state.Has(CombatStatusKind.Shield),Is.False);
    }

    [Test]
    public void CombatEffectIconsSupportPersistentAndTimedSharedIndicators()
    {
        CombatEffectIconState icons=new CombatEffectIconState();
        icons.Show(CombatEffectIconKind.AttackUp);
        icons.Show(CombatEffectIconKind.ResistanceDown,1f);

        icons.Tick(1.1f);

        Assert.That(icons.Has(CombatEffectIconKind.AttackUp),Is.True);
        Assert.That(icons.Has(CombatEffectIconKind.ResistanceDown),Is.False);
        icons.Hide(CombatEffectIconKind.AttackUp);
        Assert.That(icons.Has(CombatEffectIconKind.AttackUp),Is.False);
    }

    [Test]
    public void AttackRangeBandsUseConfiguredTileReach()
    {
        const float sourceRadius=0.55f;
        const float targetRadius=0.6f;
        float melee=MonsterAutoBattlePrototype.AttackDistanceForRange(1,sourceRadius,targetRadius);
        float midrange=MonsterAutoBattlePrototype.AttackDistanceForRange(2,sourceRadius,targetRadius);
        float ranged=MonsterAutoBattlePrototype.AttackDistanceForRange(3,sourceRadius,targetRadius);

        Assert.That(melee,Is.EqualTo(sourceRadius+targetRadius+0.62f+1.224f).Within(0.0001f));
        Assert.That(midrange,Is.EqualTo(sourceRadius+targetRadius+0.62f+4.25f).Within(0.0001f));
        Assert.That(ranged,Is.EqualTo(sourceRadius+targetRadius+0.62f+8.5f).Within(0.0001f));
    }

    [Test]
    public void RangedAtArenaEdgeStopsRetreatingAndFights()
    {
        Assert.That(MonsterAutoBattlePrototype.ShouldRangedRetreat(
            true,1.2f,4f,0.6f,2,2,false,false),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldRangedRetreat(
            true,1.2f,4f,0.6f,2,2,false,true),Is.True);
    }

    [Test]
    public void OnlyLongRangeUnitsCanKite()
    {
        Assert.That(MonsterAutoBattlePrototype.CanKiteAtRange(1),Is.False);
        Assert.That(MonsterAutoBattlePrototype.CanKiteAtRange(2),Is.False);
        Assert.That(MonsterAutoBattlePrototype.CanKiteAtRange(3),Is.True);
    }

    [Test]
    public void MeleeAndMidrangeNeverMoveAwayFromTheirTarget()
    {
        Vector3 target=Vector3.right;
        Assert.That(MonsterAutoBattlePrototype.MovementMakesForwardProgress(
            1,Vector3.left,target),Is.False);
        Assert.That(MonsterAutoBattlePrototype.MovementMakesForwardProgress(
            2,Vector3.forward,target),Is.False);
        Assert.That(MonsterAutoBattlePrototype.MovementMakesForwardProgress(
            1,Vector3.right,target),Is.True);
        Assert.That(MonsterAutoBattlePrototype.MovementMakesForwardProgress(
            3,Vector3.left,target),Is.True);
        Assert.That(MonsterAutoBattlePrototype.MovementMakesForwardProgress(
            1,Vector3.left,target,true),Is.True);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void PursuitAfterSkillRelocationDoesNotReturnToDeploymentRow(int attackRange)
    {
        GameObject host=new GameObject("Post Skill Pursuit Test");host.SetActive(false);
        GameObject sourceModel=new GameObject("Pursuer");
        GameObject targetModel=new GameObject("Relocated Target");
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            Type unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
            object source=Activator.CreateInstance(unitType,true);
            object target=Activator.CreateInstance(unitType,true);
            unitType.GetField("model").SetValue(source,sourceModel);
            unitType.GetField("model").SetValue(target,targetModel);
            unitType.GetField("alive").SetValue(source,true);
            unitType.GetField("alive").SetValue(target,true);
            unitType.GetField("target").SetValue(source,target);
            unitType.GetField("attackRange").SetValue(source,attackRange);
            unitType.GetField("initialTile").SetValue(source,new Vector2Int(6,3));
            sourceModel.transform.position=new Vector3(0.75f,0f,1.07f);
            targetModel.transform.position=new Vector3(-1f,0f,-3.32f);
            MethodInfo steer=typeof(MonsterAutoBattlePrototype).GetMethod(
                "CalculateTacticalMovement",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int frame=0;frame<120;frame++)
            {
                Vector3 delta=targetModel.transform.position-sourceModel.transform.position;
                if(delta.magnitude<=1f)break;
                Vector3 desired=(Vector3)steer.Invoke(battle,
                    new object[]{source,delta,delta.magnitude,3f});
                Assert.That(Vector3.Dot(desired.normalized,delta.normalized),Is.GreaterThan(0.99f));
                Assert.That(MonsterAutoBattlePrototype.MovementMakesForwardProgress(
                    attackRange,desired,delta),Is.True);
                sourceModel.transform.position+=desired/60f;
            }
            Assert.That(Vector3.Distance(sourceModel.transform.position,targetModel.transform.position),
                Is.LessThanOrEqualTo(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceModel);
            UnityEngine.Object.DestroyImmediate(targetModel);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void RangedRetreatRequiresClosePressureAndAttackCadence()
    {
        Assert.That(MonsterAutoBattlePrototype.ShouldRangedRetreat(
            true,1.2f,4f,0.6f,1,2,false,true),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldRangedRetreat(
            true,3f,4f,0.6f,2,2,false,true),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldRangedRetreat(
            true,1.2f,4f,0.6f,2,2,false,true),Is.True);
    }

    [Test]
    public void OnlyFlankingMeleeDirectlyChasesMovingRangedTarget()
    {
        Assert.That(MonsterAutoBattlePrototype.ShouldMeleeDirectChase(
            false,true,true,false,true),Is.True);
        Assert.That(MonsterAutoBattlePrototype.ShouldMeleeDirectChase(
            false,true,true,true),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldMeleeDirectChase(
            true,true,true,false),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldMeleeDirectChase(
            false,true,true,false),Is.False);
    }

    [Test]
    public void FrontlinersUseTheFacingHalfWhileFlankersMayCircleBehind()
    {
        Assert.That(MonsterAutoBattlePrototype.AllowsAttackApproach(
            -1f,false),Is.True);
        Assert.That(MonsterAutoBattlePrototype.AllowsAttackApproach(
            0.8f,false),Is.False);
        Assert.That(MonsterAutoBattlePrototype.AllowsAttackApproach(
            0.8f,true),Is.True);
    }

    [Test]
    public void TargetLockHasAReadabilityMinimum()
    {
        Assert.That(MonsterAutoBattlePrototype.StableTargetLockDuration(1.25f),
            Is.EqualTo(2.4f));
        Assert.That(MonsterAutoBattlePrototype.StableTargetLockDuration(3f),
            Is.EqualTo(3f));
    }

    [Test]
    public void FeaturedPersonalityPoolContainsOnlySixEventDrivenProfiles()
    {
        string[] featured={
            "vengeful","fickle","cooperative","protective","bold","timid"};
        foreach(string id in featured)
            Assert.That(PersonalityCombatPolicy.IsFeatured(id),Is.True,id);
        Assert.That(PersonalityCombatPolicy.IsFeatured("persistent"),Is.False);
        Assert.That(PersonalityCombatPolicy.IsSupported("persistent"),Is.True,
            "Old saves keep loading legacy personality ids.");

        PersonalityCombatPolicy bold=PersonalityCombatPolicy.From(
            new OwnedMonsterSave{personalityId="bold"});
        PersonalityCombatPolicy timid=PersonalityCombatPolicy.From(
            new OwnedMonsterSave{personalityId="timid"});
        Assert.That(bold.targetStyle,Is.EqualTo(PersonalityTargetStyle.Nearest));
        Assert.That(timid.timidRetreat,Is.True);
    }

    [Test]
    public void EventDrivenPersonalityRulesRespectReadabilityLimits()
    {
        PersonalityCombatPolicy cooperative=PersonalityCombatPolicy.From(new OwnedMonsterSave
            {personalityId="cooperative"});
        PersonalityCombatPolicy fickle=PersonalityCombatPolicy.From(new OwnedMonsterSave
            {personalityId="fickle"});

        Assert.That(cooperative.focusFireBonus,Is.GreaterThan(0f));
        Assert.That(fickle.rotateAfterBasicAttack,Is.True);
        Assert.That(fickle.ShouldRotateAfterBasicAttack(1),Is.False);
        Assert.That(fickle.ShouldRotateAfterBasicAttack(2),Is.False);
        Assert.That(fickle.ShouldRotateAfterBasicAttack(3),Is.True);
        Assert.That(PersonalityCombatPolicy.IsWithinLocalTargetWindow(4f,5.5f,1.5f),
            Is.True);
        Assert.That(PersonalityCombatPolicy.IsWithinLocalTargetWindow(4f,5.6f,1.5f),
            Is.False);
        Assert.That(PersonalityCombatPolicy.ShouldVengefulRetarget(
            10f,10f,100f,true),Is.True);
        Assert.That(PersonalityCombatPolicy.ShouldProtectiveIntercept(
            40f,100f,true),Is.True);
        Assert.That(PersonalityCombatPolicy.ShouldTimidRetreat(
            true,false,true,true,true),Is.True);
        Assert.That(PersonalityCombatPolicy.ShouldTimidRetreat(
            true,true,true,true,true),Is.False);
        Assert.That(PersonalityCombatPolicy.ShouldTimidRetreat(
            true,false,false,false,true),Is.False);
    }

    [Test]
    public void EccentricPersonalityKeepsOneCriterionForTheBattle()
    {
        Assert.That(PersonalityCombatPolicy.EccentricCriterion(123),
            Is.EqualTo(PersonalityCombatPolicy.EccentricCriterion(123)));
        Assert.That(PersonalityCombatPolicy.EccentricCriterion(123),Is.InRange(0,3));
    }

    [Test]
    public void NavigationRoutePersistsUntilItsWaypointsAreConsumed()
    {
        Assert.That(MonsterAutoBattlePrototype.ShouldKeepNavigationRoute(
            true,0,3),Is.True);
        Assert.That(MonsterAutoBattlePrototype.ShouldKeepNavigationRoute(
            true,3,3),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldKeepNavigationRoute(
            false,0,3),Is.False);
    }

    [Test]
    public void BlockedUnitWaitsOnlyUntilAnAlternateRouteOpens()
    {
        Assert.That(MonsterAutoBattlePrototype.ShouldWaitForOpenPath(
            true,0,0),Is.True);
        Assert.That(MonsterAutoBattlePrototype.ShouldWaitForOpenPath(
            true,0,2),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldWaitForOpenPath(
            false,0,0),Is.False);
    }

    [Test]
    public void DiagonalNavigationUsesPhysicalGapsButNotTerrainCorners()
    {
        Assert.That(MonsterAutoBattlePrototype.CanTraverseDiagonal(
            true,true),Is.True);
        Assert.That(MonsterAutoBattlePrototype.CanTraverseDiagonal(
            true,false),Is.False);
        Assert.That(MonsterAutoBattlePrototype.CanTraverseDiagonal(
            false,true),Is.False);
    }

    [Test]
    public void NavigationBoundsIncludeTheirInvisibleEdgeNodes()
    {
        Bounds bounds=new Bounds(Vector3.zero,new Vector3(10f,0.2f,6f));

        Assert.That(MonsterAutoBattlePrototype.NavigationPointInsideBounds(
            new Vector3(-5f,0f,3f),bounds),Is.True);
        Assert.That(MonsterAutoBattlePrototype.NavigationPointInsideBounds(
            new Vector3(5.01f,0f,0f),bounds),Is.False);
    }

    [Test]
    public void PartialNavigationPrefersTheClosestReachablePoint()
    {
        Assert.That(MonsterAutoBattlePrototype.IsCloserNavigationPoint(
            4f,5f,9f,2f),Is.True);
        Assert.That(MonsterAutoBattlePrototype.IsCloserNavigationPoint(
            9f,1f,9f,2f),Is.True);
        Assert.That(MonsterAutoBattlePrototype.IsCloserNavigationPoint(
            10f,1f,9f,2f),Is.False);
    }

    [Test]
    public void MeleeAttackSlotsUseTheVisibleAttackReach()
    {
        Assert.That(MonsterAutoBattlePrototype.AttackSlotDistance(
            2.28f,0.52f,0.46f),Is.EqualTo(2.2f).Within(0.0001f));
    }

    [Test]
    public void CollisionSlideKeepsMovementSpeedAlongTheSurface()
    {
        Vector3 movement=new Vector3(1f,0f,1f);
        Vector3 slide=MonsterAutoBattlePrototype.FrictionlessSlide(
            movement,Vector3.left);

        Assert.That(slide.x,Is.EqualTo(0f).Within(0.0001f));
        Assert.That(slide.magnitude,
            Is.EqualTo(movement.magnitude).Within(0.0001f));
    }

    [Test]
    public void SkillUseDecisionDoesNotReceivePersonalityData()
    {
        SkillUseSnapshot leap=new SkillUseSnapshot
        {
            behavior="AssassinLeap",targetDistanceTiles=0.2f
        };
        Assert.That(SkillUseDecision.ShouldUse(leap),Is.True);
    }

    [Test]
    public void MammothEarthChargeCanCastAtMeleeRangeAfterAirborneRecovery()
    {
        SkillUseSnapshot charge=new SkillUseSnapshot
        {
            behavior="EarthCharge",targetDistanceTiles=0.2f
        };

        Assert.That(SkillUseDecision.ShouldUse(charge),Is.True);
    }

    [Test]
    public void SkillUseDecisionKeepsOnlySkillSpecificOpportunityChecks()
    {
        SkillUseSnapshot area=new SkillUseSnapshot
        {
            behavior="ClusterBreath",enemiesInEffect=0
        };
        SkillUseSnapshot support=new SkillUseSnapshot
        {
            behavior="SpiritSanctuary",alliesInEffect=0
        };
        SkillUseSnapshot selfBuff=new SkillUseSnapshot
        {
            behavior="FightingSpirit",enemiesInEffect=0
        };

        Assert.That(SkillUseDecision.ShouldUse(area),Is.False);
        Assert.That(SkillUseDecision.ShouldUse(support),Is.False);
        Assert.That(SkillUseDecision.ShouldUse(selfBuff),Is.True);
        area.enemiesInEffect=1;
        support.alliesInEffect=1;
        Assert.That(SkillUseDecision.ShouldUse(area),Is.True);
        Assert.That(SkillUseDecision.ShouldUse(support),Is.True);
    }

    [Test]
    public void EveryEnabledMonsterSkillHasSkillDecisionRule()
    {
        MonsterDatabase monsters=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        foreach(MonsterDefinition monster in monsters.monsters.Where(monster=>
                    monster!=null&&monster.enabled))
        {
            SkillMasterRow skill=GameMasterData.Skill(monster.skillId);
            Assert.That(skill,Is.Not.Null,monster.monsterId);
            Assert.That(SkillUseDecision.SupportsBehavior(skill.behavior),Is.True,
                monster.monsterId+" / "+skill.behavior);
        }
    }

    [Test]
    public void ItemRecipesAreOrderIndependentAndCompletedItemsCannotBeComponents()
    {
        ItemMasterRow forward=GameMasterData.ItemRecipe("base_fang","base_feather");
        ItemMasterRow reverse=GameMasterData.ItemRecipe("base_feather","base_fang");
        Assert.That(forward,Is.Not.Null);
        Assert.That(forward.id,Is.EqualTo("complete_berserker_claw"));
        Assert.That(reverse,Is.SameAs(forward));
        Assert.That(GameMasterData.ItemRecipe("complete_berserker_claw","base_fang"),Is.Null);
    }

    [Test]
    public void ExpeditionItemsEquipToThreeSlotsAndFuseIntoInventory()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        run.monsters.Add(new ExpeditionMonsterStateSave{instanceId="monster_1"});
        ExpeditionItemInstanceSave fang=ExpeditionItemSystem.CreateItem(run,"base_fang");
        ExpeditionItemInstanceSave feather=ExpeditionItemSystem.CreateItem(run,"base_feather");

        Assert.That(ExpeditionItemSystem.Equip(run,"monster_1",fang.instanceId,2,out string equipError),
            Is.True,equipError);
        Assert.That(run.monsters[0].itemInstanceIds,Has.Count.EqualTo(3));
        Assert.That(run.monsters[0].itemInstanceIds[2],Is.EqualTo(fang.instanceId));

        Assert.That(ExpeditionItemSystem.TryFuse(
            run,fang.instanceId,feather.instanceId,out ExpeditionItemInstanceSave result,out string fuseError),
            Is.True,fuseError);
        Assert.That(result.itemId,Is.EqualTo("complete_berserker_claw"));
        Assert.That(run.inventoryItemInstanceIds,Does.Contain(result.instanceId));
        Assert.That(run.monsters[0].itemInstanceIds[2],Is.Empty);
        Assert.That(run.itemInstances,Has.Count.EqualTo(1));
    }

    [Test]
    public void DuplicateEquippedItemsAddAllStatBonuses()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave monster=new ExpeditionMonsterStateSave{instanceId="monster_1"};
        run.monsters.Add(monster);
        for(int slot=0;slot<3;slot++)
        {
            ExpeditionItemInstanceSave item=ExpeditionItemSystem.CreateItem(run,"complete_tyrant_fang");
            Assert.That(ExpeditionItemSystem.Equip(run,monster.instanceId,item.instanceId,slot,out string error),
                Is.True,error);
        }
        CombatItemModifiers modifiers=ExpeditionItemSystem.Aggregate(run,monster);
        Assert.That(modifiers.attackDamageBonus,Is.EqualTo(0.60f).Within(0.0001f));
    }

    [Test]
    public void EightComponentsCoverEveryUnorderedRecipePair()
    {
        List<ItemMasterRow> bases=GameMasterData.Database.items.Where(row=>row.IsBase).ToList();
        List<ItemMasterRow> completed=GameMasterData.Database.items.Where(row=>row.IsCompleted).ToList();
        Assert.That(bases,Has.Count.EqualTo(8));
        Assert.That(completed,Has.Count.EqualTo(37));
        for(int first=0;first<bases.Count;first++)
            for(int second=first;second<bases.Count;second++)
                Assert.That(GameMasterData.ItemRecipe(bases[first].id,bases[second].id),Is.Not.Null,
                    bases[first].id+" + "+bases[second].id);
    }

    [Test]
    public void NewDefensiveAndCooldownComponentStatsAggregate()
    {
        CombatItemModifiers modifiers=ExpeditionItemSystem.Aggregate(new[]{
            GameMasterData.Item("base_sand"),GameMasterData.Item("base_scale"),
            GameMasterData.Item("base_talisman")});
        Assert.That(modifiers.skillCooldownReductionBonus,Is.EqualTo(.08f).Within(.0001f));
        Assert.That(modifiers.defenseBonus,Is.EqualTo(10f).Within(.0001f));
        Assert.That(modifiers.statusResistanceBonus,Is.EqualTo(10f).Within(.0001f));
    }

    [Test]
    public void EquippedItemCanMoveBetweenMonstersAndDisplacedItemReturnsToInventory()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave firstMonster=new ExpeditionMonsterStateSave{instanceId="monster_1"};
        ExpeditionMonsterStateSave secondMonster=new ExpeditionMonsterStateSave{instanceId="monster_2"};
        run.monsters.Add(firstMonster);
        run.monsters.Add(secondMonster);
        ExpeditionItemInstanceSave fang=ExpeditionItemSystem.CreateItem(run,"base_fang");
        ExpeditionItemInstanceSave heart=ExpeditionItemSystem.CreateItem(run,"base_heart");
        Assert.That(ExpeditionItemSystem.Equip(run,firstMonster.instanceId,fang.instanceId,0,out _),Is.True);
        Assert.That(ExpeditionItemSystem.Equip(run,secondMonster.instanceId,heart.instanceId,0,out _),Is.True);

        Assert.That(ExpeditionItemSystem.Equip(
            run,secondMonster.instanceId,fang.instanceId,0,out string error),Is.True,error);

        Assert.That(ExpeditionItemSystem.MonsterState(run,firstMonster.instanceId).itemInstanceIds[0],Is.Empty);
        Assert.That(ExpeditionItemSystem.MonsterState(run,secondMonster.instanceId).itemInstanceIds[0],
            Is.EqualTo(fang.instanceId));
        Assert.That(run.inventoryItemInstanceIds,Does.Contain(heart.instanceId));
        Assert.That(run.inventoryItemInstanceIds,Does.Not.Contain(fang.instanceId));
    }

    [Test]
    public void WaveLootOnlyChangesExpeditionInventoryWhenCommitted()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        PendingWaveLoot pending=new PendingWaveLoot();
        pending.itemIds.Add("base_fang");
        pending.expeditionCurrency=3;

        Assert.That(run.itemInstances,Is.Empty);
        Assert.That(run.expeditionCurrency,Is.Zero);

        ExpeditionDropSystem.Commit(run,pending);

        Assert.That(run.itemInstances,Has.Count.EqualTo(1));
        Assert.That(run.itemInstances[0].itemId,Is.EqualTo("base_fang"));
        Assert.That(run.inventoryItemInstanceIds,Does.Contain(run.itemInstances[0].instanceId));
        Assert.That(run.expeditionCurrency,Is.EqualTo(3));
        Assert.That(pending.IsEmpty,Is.True);
    }

    [Test]
    public void WaveClearGuaranteeGrantsEquipmentOnThirdDryClear()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        for(int clear=1;clear<=3;clear++)
        {
            PendingWaveLoot pending=new PendingWaveLoot();
            ExpeditionDropSystem.ApplyWaveClearGuarantees(
                run,pending,new System.Random(clear));
            Assert.That(pending.expeditionCurrency,Is.Zero);
            Assert.That(pending.itemIds.Count,Is.EqualTo(clear==3?1:0));
        }
        Assert.That(run.equipmentPityClears,Is.Zero);
    }

    [Test]
    public void NaturalEquipmentDropResetsWaveEquipmentPity()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{equipmentPityClears=2};
        PendingWaveLoot pending=new PendingWaveLoot();
        pending.itemIds.Add("base_fang");

        ExpeditionDropSystem.ApplyWaveClearGuarantees(
            run,pending,new System.Random(1));

        Assert.That(run.equipmentPityClears,Is.Zero);
        Assert.That(pending.itemIds,Is.EqualTo(new[]{"base_fang"}));
    }

    [Test]
    public void EnemyCurrencyDropsAreRareAndNeverExceedTwoGold()
    {
        DropMasterRow currency=GameMasterData.Database.drops.First(row=>
            row.id=="drop_currency");
        Assert.That(currency.chance,Is.EqualTo(0.25f));
        Assert.That(currency.minAmount,Is.EqualTo(1));
        Assert.That(currency.maxAmount,Is.EqualTo(2));
        Assert.That(GameMasterData.Database.drops.Any(row=>
            row.rewardType=="PromotionCertificate"),Is.False);

        ItemMasterRow baseItem=GameMasterData.Item("base_fang");
        ItemMasterRow completedItem=GameMasterData.Item("complete_tyrant_fang");
        int lowCurrency=0;
        int highCurrency=0;
        List<EnemyDropRoll> allDrops=new List<EnemyDropRoll>();
        for(int seed=0;seed<2000;seed++)
        {
            List<EnemyDropRoll> low=ExpeditionDropSystem.RollEnemyDrops(
                1,ExpeditionMonsterRank.Rank1,new ItemMasterRow[0],new System.Random(seed));
            List<EnemyDropRoll> high=ExpeditionDropSystem.RollEnemyDrops(
                10,ExpeditionMonsterRank.Rank3,new[]{baseItem,completedItem},new System.Random(seed));
            allDrops.AddRange(low);
            allDrops.AddRange(high);
            lowCurrency+=low.Where(drop=>drop.rewardType=="ExpeditionCurrency").Sum(drop=>drop.amount);
            highCurrency+=high.Where(drop=>drop.rewardType=="ExpeditionCurrency").Sum(drop=>drop.amount);
        }
        Assert.That(highCurrency,Is.GreaterThan(lowCurrency));
        Assert.That(allDrops.Where(drop=>drop.rewardType=="ExpeditionCurrency")
            .All(drop=>drop.amount>=1&&
                drop.amount<=ExpeditionDropSystem.MaximumCurrencyPerDrop),
            Is.True);
        List<EnemyDropRoll> equipmentDrops=allDrops.Where(drop=>
            drop.rewardType=="Equipment").ToList();
        Assert.That(equipmentDrops,Is.Not.Empty);
        Assert.That(equipmentDrops.All(drop=>
            GameMasterData.Item(drop.itemId).IsBase),Is.True);
    }

    [Test]
    public void BattleIncomeUsesInterestAndWinLossStreaks()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{expeditionCurrency=30};

        ExpeditionBattleIncome firstWin=
            ExpeditionDropSystem.ResolveBattleIncome(run,true);
        ExpeditionBattleIncome secondWin=
            ExpeditionDropSystem.ResolveBattleIncome(run,true);
        ExpeditionBattleIncome firstLoss=
            ExpeditionDropSystem.ResolveBattleIncome(run,false);
        ExpeditionBattleIncome secondLoss=
            ExpeditionDropSystem.ResolveBattleIncome(run,false);

        Assert.That(firstWin.Total,Is.EqualTo(5));
        Assert.That(secondWin.Total,Is.EqualTo(6));
        Assert.That(firstLoss.Total,Is.EqualTo(5));
        Assert.That(secondLoss.Total,Is.EqualTo(6));
        Assert.That(run.consecutiveWins,Is.Zero);
        Assert.That(run.consecutiveLosses,Is.EqualTo(2));
    }

    [Test]
    public void EnemyGoldIsOnlyReducedFromTheMidGame()
    {
        Assert.That(GameMasterData.ExpeditionInt(
            "enemy_currency_nerf_start_wave",0),Is.EqualTo(7));
        Assert.That(GameMasterData.ExpeditionInt(
            "enemy_currency_mid_late_percent",0),Is.EqualTo(85));
        ItemMasterRow[] items=
        {
            GameMasterData.Item("base_fang"),
            GameMasterData.Item("complete_tyrant_fang")
        };
        int wave1=0;
        int wave6=0;
        int wave7=0;
        for(int seed=0;seed<500;seed++)
        {
            wave1+=ExpeditionDropSystem.RollEnemyDrops(
                1,ExpeditionMonsterRank.Rank3,items,new System.Random(seed))
                .Where(drop=>drop.rewardType=="ExpeditionCurrency")
                .Sum(drop=>drop.amount);
            wave6+=ExpeditionDropSystem.RollEnemyDrops(
                6,ExpeditionMonsterRank.Rank3,items,new System.Random(seed))
                .Where(drop=>drop.rewardType=="ExpeditionCurrency")
                .Sum(drop=>drop.amount);
            wave7+=ExpeditionDropSystem.RollEnemyDrops(
                7,ExpeditionMonsterRank.Rank3,items,new System.Random(seed))
                .Where(drop=>drop.rewardType=="ExpeditionCurrency")
                .Sum(drop=>drop.amount);
        }
        Assert.That(wave6,Is.EqualTo(wave1));
        Assert.That(wave7,Is.LessThan(wave6));
    }

    [Test]
    public void EnemyProfilesFollowFixedWaveBudgets()
    {
        int[] enemyCounts={2,3,3,4,4,5,5,6,6,7,7,8};
        int[] rank3Counts={0,0,0,0,0,0,0,0,1,2,3,3};
        int[] rank2Counts={0,0,1,2,1,2,3,4,2,3,3,4};
        int[] baseItemCounts={0,0,1,2,1,2,2,3,2,2,3,3};
        int[] completedItemCounts={0,0,0,0,0,0,0,0,0,1,2,2};
        for(int wave=1;wave<=12;wave++)
        {
            List<EnemyLootProfile> profiles=Enumerable.Range(
                0,enemyCounts[wave-1]).Select(index=>
                    ExpeditionDropSystem.BuildEnemyProfile(
                        wave,index,new System.Random(wave*100+index))).ToList();
            Assert.That(profiles.Count(profile=>
                profile.rank==ExpeditionMonsterRank.Rank3),
                Is.EqualTo(rank3Counts[wave-1]),"Wave "+wave+" Rank3");
            Assert.That(profiles.Count(profile=>
                profile.rank==ExpeditionMonsterRank.Rank2),
                Is.EqualTo(rank2Counts[wave-1]),"Wave "+wave+" Rank2");
            Assert.That(profiles.SelectMany(profile=>profile.equippedItems)
                .Count(item=>item.IsBase),
                Is.EqualTo(baseItemCounts[wave-1]),"Wave "+wave+" base items");
            Assert.That(profiles.SelectMany(profile=>profile.equippedItems)
                .Count(item=>item.IsCompleted),
                Is.EqualTo(completedItemCounts[wave-1]),
                "Wave "+wave+" completed items");
        }
    }

    [Test]
    public void LaterRegionsAddMoreEnemyEquipmentWithoutChangingWaveRanks()
    {
        const int wave=10;
        const int enemyCount=6;
        List<EnemyLootProfile> first=Enumerable.Range(0,enemyCount)
            .Select(index=>ExpeditionDropSystem.BuildEnemyProfile(
                wave,index,new System.Random(1000+index),1,enemyCount)).ToList();
        List<EnemyLootProfile> last=Enumerable.Range(0,enemyCount)
            .Select(index=>ExpeditionDropSystem.BuildEnemyProfile(
                wave,index,new System.Random(1000+index),5,enemyCount)).ToList();

        Assert.That(last.Sum(profile=>profile.equippedItems.Count),
            Is.GreaterThan(first.Sum(profile=>profile.equippedItems.Count)));
        Assert.That(last.Select(profile=>profile.rank),
            Is.EqualTo(first.Select(profile=>profile.rank)));
        Assert.That(last.All(profile=>profile.equippedItems.Count<=3),Is.True);
    }

    [Test]
    public void FirstWaveNeverAddsRegionalEquipment()
    {
        const int enemyCount=2;
        List<EnemyLootProfile> profiles=Enumerable.Range(0,enemyCount)
            .Select(index=>ExpeditionDropSystem.BuildEnemyProfile(
                1,index,new System.Random(2200+index),5,enemyCount)).ToList();

        Assert.That(profiles.SelectMany(profile=>profile.equippedItems),Is.Empty);
    }

    [Test]
    public void DiscardedWaveLootNeverChangesExpeditionInventory()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        PendingWaveLoot pending=new PendingWaveLoot();
        pending.itemIds.Add("base_heart");
        pending.expeditionCurrency=2;
        ExpeditionDropSystem.Discard(pending);

        Assert.That(run.itemInstances,Is.Empty);
        Assert.That(run.expeditionCurrency,Is.Zero);
        Assert.That(pending.IsEmpty,Is.True);
    }

    [Test]
    public void CapturedMonsterPreservesIdentityButRollsNewStatsBeforeOwnershipCommit()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.Get("wilo");
        OwnedMonsterSave enemy=PlayerProfileStore.CreateMonster(definition,"enemy_capture_test");
        PlayerProfileSave profile=new PlayerProfileSave();
        PendingMonsterCapture pending=new PendingMonsterCapture();
        pending.Arm(enemy.instanceId);

        bool succeeded=pending.ResolveDefeat(
            enemy,definition,1f,0.99f,"player_capture_test");

        Assert.That(succeeded,Is.True);
        Assert.That(profile.monsters,Is.Empty,
            "The pending capture object must not mutate ownership before its caller commits it.");
        Assert.That(pending.IsReady,Is.True);
        Assert.That(pending.capturedMonster.instanceId,Is.EqualTo("player_capture_test"));
        Assert.That(pending.capturedMonster.monsterId,Is.EqualTo(enemy.monsterId));
        Assert.That(pending.capturedMonster.personalityId,Is.EqualTo(enemy.personalityId));
        Assert.That(pending.capturedMonster.personalitySeed,Is.EqualTo(enemy.personalitySeed));
        var expected=ExpeditionCaptureSystem.CreateCapturedMonster(enemy,definition,"player_capture_test");
        foreach(MonsterIndividualStat stat in Enum.GetValues(typeof(MonsterIndividualStat)))
            Assert.That(MonsterStatQuality.Multiplier(pending.capturedMonster,stat),
                Is.EqualTo(MonsterStatQuality.Multiplier(expected,stat)));
        Assert.That(MonsterStatQuality.OverallScore(pending.capturedMonster),
            Is.Not.EqualTo(MonsterStatQuality.OverallScore(enemy)));

        pending.Clear();
        Assert.That(pending.IsReady,Is.False,
            "A failed wave must be able to discard the pending capture without ownership changes.");
        Assert.That(profile.monsters,Is.Empty);
    }

    [Test]
    public void ExpeditionRankHasThreeImmediatelyComparableVisualScales()
    {
        Assert.That(MonsterAutoBattlePrototype.RankVisualScale(ExpeditionMonsterRank.Rank1),
            Is.EqualTo(1f));
        Assert.That(MonsterAutoBattlePrototype.RankVisualScale(ExpeditionMonsterRank.Rank2),
            Is.EqualTo(1.15f));
        Assert.That(MonsterAutoBattlePrototype.RankVisualScale(ExpeditionMonsterRank.Rank3),
            Is.EqualTo(1.3f));
        Assert.That(MonsterAutoBattlePrototype.RankHealthMultiplier(ExpeditionMonsterRank.Rank2),
            Is.EqualTo(1.6f));
        Assert.That(MonsterAutoBattlePrototype.RankHealthMultiplier(ExpeditionMonsterRank.Rank3),
            Is.EqualTo(2.5f));
        Assert.That(MonsterAutoBattlePrototype.RankDamageMultiplier(ExpeditionMonsterRank.Rank2),
            Is.EqualTo(1.25f));
        Assert.That(MonsterAutoBattlePrototype.RankDamageMultiplier(ExpeditionMonsterRank.Rank3),
            Is.EqualTo(1.6f));
    }

    [Test]
    public void FocusFireAndVengeanceApplyToActualOutgoingDamageWithoutAStackCap()
    {
        CombatGrowthCardModifiers modifiers=new CombatGrowthCardModifiers
        {
            focusFireDamageBonus=0.35f,
            vengeanceAttackDamagePerDeath=0.21f
        };

        Assert.That(CombatGrowthCardCombatRules.OutgoingDamageMultiplier(modifiers,2,1),
            Is.EqualTo(1.42f).Within(0.0001f));
        Assert.That(CombatGrowthCardCombatRules.OutgoingDamageMultiplier(modifiers,2,2),
            Is.EqualTo(1.917f).Within(0.0001f));
        Assert.That(CombatGrowthCardCombatRules.OutgoingDamageMultiplier(modifiers,20,3),
            Is.EqualTo(8.84f).Within(0.0001f));
    }

    [Test]
    public void ExpeditionTraitsArePreviewedAndActivateByGrowthRank()
    {
        MonsterDefinition definition=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase").Get("velos");
        ExpeditionMonsterStateSave first=new ExpeditionMonsterStateSave{instanceId="first"};
        ExpeditionMonsterStateSave second=new ExpeditionMonsterStateSave{instanceId="second"};

        ExpeditionTraitProgression.AssignPreviewTraits(
            first,definition,new System.Random(7124));
        ExpeditionTraitProgression.AssignPreviewTraits(
            second,definition,new System.Random(7124));

        Assert.That(first.rank2TraitId,Is.Not.Empty);
        Assert.That(first.rank3TraitId,Is.Not.Empty);
        Assert.That(first.rank3TraitId,Is.Not.EqualTo(first.rank2TraitId));
        Assert.That(first.rank2TraitId,Is.EqualTo(second.rank2TraitId));
        Assert.That(first.rank3TraitId,Is.EqualTo(second.rank3TraitId));
        Assert.That(MonsterTraitCatalog.IsCompatible(first.rank2TraitId,definition),Is.True);
        Assert.That(MonsterTraitCatalog.IsCompatible(first.rank3TraitId,definition),Is.True);
        Assert.That(ExpeditionTraitProgression.ActiveTraitIds(first),Is.Empty);

        first.rank=ExpeditionMonsterRank.Rank2;
        Assert.That(ExpeditionTraitProgression.ActiveTraitIds(first),
            Is.EqualTo(new[]{first.rank2TraitId}));
        first.rank=ExpeditionMonsterRank.Rank3;
        Assert.That(ExpeditionTraitProgression.ActiveTraitIds(first),
            Is.EqualTo(new[]{first.rank2TraitId,first.rank3TraitId}));
    }

    [Test]
    public void TraitRuntimeCombinesPermanentAndActivatedExpeditionTraits()
    {
        MonsterDefinition definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        definition.intrinsicTraitId="trait_cleave_fang";
        OwnedMonsterSave owned=new OwnedMonsterSave{intrinsicTraitId="trait_hunting_rhythm"};
        ExpeditionMonsterStateSave expedition=new ExpeditionMonsterStateSave
        {
            rank=ExpeditionMonsterRank.Rank3,
            rank2TraitId="trait_hunt_mark",
            rank3TraitId="trait_exploit_opening"
        };
        try
        {
            List<string> active=MonsterTraitRuntime.ActiveTraitIds(definition,owned,expedition);
            Assert.That(active,Is.EqualTo(new[]
            {
                "trait_hunting_rhythm","trait_hunt_mark","trait_exploit_opening"
            }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void BasicAttackTraitsStackWithoutAHiddenCap()
    {
        string[] traits=
        {
            "trait_vital_strike","trait_ironclad_strike",
            "trait_resolute_strike","trait_giant_hunt"
        };
        MonsterTraitCombatState state=new MonsterTraitCombatState();
        for(int i=0;i<11;i++)MonsterTraitRuntime.ResolveBasicAttack(
            traits,state,"enemy",false,false,100f,1000f,80f,0.4f,900f);
        BasicAttackTraitResult twelfth=MonsterTraitRuntime.ResolveBasicAttack(
            traits,state,"boss",false,true,100f,1000f,80f,0.4f,900f);

        Assert.That(twelfth.bonusFlatDamage,Is.EqualTo(141f).Within(0.0001f));
        Assert.That(twelfth.activations,Is.EqualTo(4));
    }

    [Test]
    public void CrossHuntAlternatesTargetSwitchRequests()
    {
        string[] traits={"trait_cross_hunt"};
        MonsterTraitCombatState state=new MonsterTraitCombatState();
        BasicAttackTraitResult first=MonsterTraitRuntime.ResolveBasicAttack(
            traits,state,"enemy",false,false);
        BasicAttackTraitResult second=MonsterTraitRuntime.ResolveBasicAttack(
            traits,state,"enemy",false,false);

        Assert.That(first.switchTarget,Is.True);
        Assert.That(second.switchTarget,Is.False);
    }

    [Test]
    public void SkillEchoCanOnlyBeScheduledOncePerBattle()
    {
        MonsterTraitCombatState state=new MonsterTraitCombatState();
        string[] traits={"trait_skill_dual_manifest"};

        Assert.That(MonsterTraitRuntime.TryScheduleSkillEcho(traits,state),Is.True);
        Assert.That(MonsterTraitRuntime.TryScheduleSkillEcho(traits,state),Is.False);
        state.Reset();
        Assert.That(MonsterTraitRuntime.TryScheduleSkillEcho(traits,state),Is.True);
    }

    [Test]
    public void CompatibleSkillTraitsResolveTogetherWithoutRecursiveExecution()
    {
        string[] traits={"trait_skill_distributed_cast"};
        SkillHitTraitResult result=MonsterTraitRuntime.ResolveSkillHit(
            traits,true,true,true,true);

        Assert.That(result.splitDamage,Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(result.secondaryTargets,Is.EqualTo(2));
        Assert.That(result.delayedAreaDamage,Is.Zero);
        Assert.That(result.killChainDamage,Is.Zero);
        Assert.That(result.afterimageDamage,Is.Zero);
        Assert.That(result.spreadingDamage,Is.Zero);
        Assert.That(result.activations,Is.EqualTo(1));
    }

    [Test]
    public void PassiveFocusSkillDoesNotReceiveActiveSkillTraits()
    {
        MonsterDefinition definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        definition.skillId="focus_chain";
        try
        {
            MonsterCombatTag tags=MonsterTraitCatalog.ResolveTags(definition);
            Assert.That(tags.HasFlag(MonsterCombatTag.Skill),Is.False);
            Assert.That(MonsterTraitCatalog.IsCompatible("trait_skill_echo",definition),Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void StoredInstinctBuildsOnBasicsAndIsConsumedBySkillUse()
    {
        string[] traits=
        {
            "trait_stored_instinct","trait_hunt_afterglow","trait_backflow",
            "trait_battle_acceleration"
        };
        MonsterTraitCombatState state=new MonsterTraitCombatState();
        for(int i=0;i<5;i++)
            MonsterTraitRuntime.ResolveBasicAttack(traits,state,"enemy",false,false);

        SkillUseTraitResult preview=MonsterTraitRuntime.PreviewSkillUse(traits,state);
        Assert.That(preview.damageMultiplier,Is.EqualTo(1.4f).Within(0.0001f));
        Assert.That(preview.radiusMultiplier,Is.EqualTo(1.2f).Within(0.0001f));
        Assert.That(MonsterTraitRuntime.CommitSkillUse(traits,state),Is.EqualTo(3));
        Assert.That(state.storedInstinctStacks,Is.Zero);

        BasicAttackTraitResult next=MonsterTraitRuntime.ResolveBasicAttack(
            traits,state,"enemy",false,false);
        Assert.That(next.bonusPrimaryDamage,Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(next.splashDamage,Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(next.skillCooldownReduction,Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void SkillBrandAndFinisherLinkPersistUntilTheirFollowUpAction()
    {
        string[] traits={"trait_skill_brand","trait_finisher_link"};
        MonsterTraitCombatState state=new MonsterTraitCombatState();
        Assert.That(MonsterTraitRuntime.RecordSkillHit(traits,state,"enemy"),Is.True);
        BasicAttackTraitResult branded=MonsterTraitRuntime.ResolveBasicAttack(
            traits,state,"enemy",false,false);
        Assert.That(branded.bonusPrimaryDamage,Is.EqualTo(0.35f).Within(0.0001f));

        MonsterTraitRuntime.RecordBasicAttackKill(traits,state);
        Assert.That(state.finisherReady,Is.True);
        Assert.That(MonsterTraitRuntime.CommitSkillUse(traits,state),Is.EqualTo(1));
        Assert.That(state.finisherReady,Is.False);
    }

    [Test]
    public void TeamTraitCooldownsPreventRecursiveSupportAttacks()
    {
        string[] traits={"trait_assist_signal","trait_protective_counter"};
        MonsterTraitCombatState state=new MonsterTraitCombatState();

        Assert.That(MonsterTraitRuntime.TryTriggerAssistSignal(traits,state),Is.True);
        Assert.That(MonsterTraitRuntime.TryTriggerAssistSignal(traits,state),Is.False);
        Assert.That(MonsterTraitRuntime.TryTriggerProtectiveCounter(traits,state),Is.True);
        Assert.That(MonsterTraitRuntime.TryTriggerProtectiveCounter(traits,state),Is.False);
        state.Tick(2f);
        Assert.That(MonsterTraitRuntime.TryTriggerAssistSignal(traits,state),Is.True);
        Assert.That(MonsterTraitRuntime.TryTriggerProtectiveCounter(traits,state),Is.True);
    }

    [Test]
    public void SharedMarkCanBeConsumedByOneAlliedAttack()
    {
        string[] traits={"trait_shared_mark"};
        MonsterTraitCombatState state=new MonsterTraitCombatState();
        MonsterTraitRuntime.RecordShareableMark(traits,state,"enemy",true);

        Assert.That(MonsterTraitRuntime.TryConsumeSharedMark(traits,state,"other"),Is.False);
        Assert.That(MonsterTraitRuntime.TryConsumeSharedMark(traits,state,"enemy"),Is.True);
        Assert.That(MonsterTraitRuntime.TryConsumeSharedMark(traits,state,"enemy"),Is.False);
    }

    [Test]
    public void CommandAndElementResonanceStackOnTheNextBasicAttack()
    {
        MonsterTraitCombatState receiver=new MonsterTraitCombatState();
        Assert.That(MonsterTraitRuntime.GrantCommandedAttack(
            new[]{"trait_chain_command"},receiver),Is.True);
        Assert.That(MonsterTraitRuntime.GrantElementResonance(
            new[]{"trait_element_resonance"},receiver),Is.True);

        BasicAttackTraitResult result=MonsterTraitRuntime.ResolveBasicAttack(
            new string[0],receiver,"enemy",false,false);
        Assert.That(result.bonusPrimaryDamage,Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(result.splashDamage,Is.EqualTo(0.45f).Within(0.0001f));
        Assert.That(receiver.commandedBasicAttacks,Is.Zero);
        Assert.That(receiver.elementResonanceAttacks,Is.Zero);
    }

    [Test]
    public void SyncedSkillBalanceMatchesCurrentCombatValues()
    {
        Assert.That(GameMasterData.Skill("guardian_taunt").baseCooldown,Is.EqualTo(5.5f));
        Assert.That(GameMasterData.Skill("assassin_leap").damageMultiplier,Is.EqualTo(1.45f));
        Assert.That(GameMasterData.Skill("cluster_breath").radiusTiles,Is.EqualTo(2.8f));
        Assert.That(GameMasterData.Skill("focus_chain").maxStacks,Is.EqualTo(5));
        Assert.That(GameMasterData.Skill(FamilySharedSkillSetup.RaptorSkillId).behavior,
            Is.EqualTo("AssassinLeap"));
    }

    [Test]
    public void SkillExecutorDispatchesByMasterBehaviorAndTrigger()
    {
        MonsterSkillExecutor<string> executor=new MonsterSkillExecutor<string>();
        string received="";
        executor.Register("GuardianTaunt",MonsterSkillTrigger.Active,(context,skill)=>
        {
            received=context+":"+skill.id;
            return true;
        });

        Assert.That(executor.TryExecute("guardian_taunt",MonsterSkillTrigger.Active,"unit"),Is.True);
        Assert.That(received,Is.EqualTo("unit:guardian_taunt"));
        Assert.That(executor.TryExecute("guardian_taunt",MonsterSkillTrigger.BasicAttackPrepare,"unit"),Is.False);
        Assert.That(executor.TryExecute("missing_skill",MonsterSkillTrigger.Active,"unit"),Is.False);
    }

    [Test]
    public void GuardianCombatTagComesFromSkillInsteadOfLegacyTactic()
    {
        MonsterDefinition definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        definition.skillId="guardian_taunt";
        definition.tactic="Hunter";
        try
        {
            Assert.That(MonsterTraitCatalog.ResolveTags(definition).HasFlag(MonsterCombatTag.Guardian),Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void GrowthCardDraftContainsThreeDifferentCards()
    {
        for(int seed=0;seed<1000;seed++)
        {
            List<CombatGrowthCardDraftEntry> draft=CombatGrowthCardDraftService.CreateDraft(1,seed);
            Assert.That(draft,Has.Count.EqualTo(3));
            Assert.That(draft.Select(card=>card.cardId).Distinct().Count(),Is.EqualTo(3));
        }
    }

    [Test]
    public void GrowthCardDraftUsesOneSharedRarity()
    {
        for(int seed=0;seed<1000;seed++)
            Assert.That(CombatGrowthCardDraftService.CreateDraft(6,seed)
                .Select(card=>card.rarity).Distinct().Count(),Is.EqualTo(1));
    }

    [Test]
    public void GrowthCardsPauseOnlyBeforeTheSecondAugmentAtWaveSeven()
    {
        Assert.That(CombatGrowthCardDraftService.IsRewardAfterWave(3),Is.True);
        Assert.That(CombatGrowthCardDraftService.IsRewardAfterWave(4),Is.True);
        Assert.That(CombatGrowthCardDraftService.IsRewardAfterWave(5),Is.True);
        Assert.That(CombatGrowthCardDraftService.IsRewardAfterWave(6),Is.False);
        Assert.That(CombatGrowthCardDraftService.IsRewardAfterWave(8),Is.True);
        Assert.That(CombatGrowthCardDraftService.IsRewardAfterWave(9),Is.True);
        Assert.That(CombatGrowthCardDraftService.IsRewardAfterWave(12),Is.False);
    }

    [Test]
    public void BalanceGrowthPresetUsesOneCardPerRewardWaveAndIsDeterministic()
    {
        List<CombatGrowthCardSelectionSave> first=
            CombatGrowthCardBalancePreset.CreateForWave(12);
        List<CombatGrowthCardSelectionSave> second=
            CombatGrowthCardBalancePreset.CreateForWave(12);

        Assert.That(first,Has.Count.EqualTo(10));
        Assert.That(second.Select(card=>card.cardId+":"+card.rarity),
            Is.EqualTo(first.Select(card=>card.cardId+":"+card.rarity)));
        Assert.That(CombatGrowthCardBalancePreset.CreateForWave(1),Is.Empty);
    }

    [Test]
    public void BalanceStatisticsReportsAggregateResults()
    {
        AutoBattleBalanceStatistics statistics=new AutoBattleBalanceStatistics();
        statistics.Record(true,10f,1);
        statistics.Record(false,20f,3);

        Assert.That(statistics.CompletedRuns,Is.EqualTo(2));
        Assert.That(statistics.Wins,Is.EqualTo(1));
        Assert.That(statistics.WinRate,Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(statistics.AverageBattleSeconds,Is.EqualTo(15f).Within(0.0001f));
        Assert.That(statistics.AveragePlayerDeaths,Is.EqualTo(2f).Within(0.0001f));

        statistics.Reset();
        Assert.That(statistics.CompletedRuns,Is.Zero);
        Assert.That(statistics.WinRate,Is.Zero);
    }

    [Test]
    public void HeuristicBalancePolicySelectsDeploymentDeterministically()
    {
        List<AutoBattleFormationCandidate> candidates=new List<AutoBattleFormationCandidate>
        {
            new AutoBattleFormationCandidate{stableId="slow",maxHp=100f,damage=10f,attackInterval=2f,attackRange=1,rank=1},
            new AutoBattleFormationCandidate{stableId="ranged",maxHp=90f,damage=24f,attackInterval=1f,attackRange=4,rank=1},
            new AutoBattleFormationCandidate{stableId="bruiser",maxHp=180f,damage=20f,attackInterval=1.2f,attackRange=1,rank=2},
            new AutoBattleFormationCandidate{stableId="spare",maxHp=80f,damage=8f,attackInterval=2f,attackRange=1,rank=1}
        };

        List<string> first=AutoBattleHeuristicPolicy.SelectDeployment(candidates,2,12345);
        List<string> second=AutoBattleHeuristicPolicy.SelectDeployment(candidates,2,12345);

        Assert.That(first,Is.EqualTo(second));
        Assert.That(first,Does.Contain("ranged"));
        Assert.That(first,Does.Contain("bruiser"));
        Assert.That(AutoBattleHeuristicPolicy.PreferredDeploymentRow(4,0,2),Is.EqualTo(0));
        Assert.That(AutoBattleHeuristicPolicy.PreferredDeploymentRow(1,0,2),Is.EqualTo(2));
    }

    [Test]
    public void HeuristicGrowthPolicyUsesOneDeterministicGrowthShopChoicePerWave()
    {
        ExpeditionRunSave first=BalanceGrowthRun("balance_growth");
        ExpeditionRunSave second=BalanceGrowthRun("balance_growth");

        List<string> firstChoices=AutoBattleHeuristicPolicy
            .ApplyGrowthShopProgression(first,12,240731);
        List<string> secondChoices=AutoBattleHeuristicPolicy
            .ApplyGrowthShopProgression(second,12,240731);

        Assert.That(firstChoices,Has.Count.EqualTo(12));
        Assert.That(secondChoices,Is.EqualTo(firstChoices));
        Assert.That(second.monsters.Select(row=>row.growthTotal),
            Is.EqualTo(first.monsters.Select(row=>row.growthTotal)));
        Assert.That(first.monsters.Sum(row=>row.growthTotal),Is.InRange(12,36));
        Assert.That(first.growthCards,Is.Empty);
    }

    private static ExpeditionRunSave BalanceGrowthRun(string runId)
    {
        ExpeditionRunSave run=new ExpeditionRunSave{runId=runId};
        for(int i=0;i<8;i++)run.monsters.Add(new ExpeditionMonsterStateSave
        {
            instanceId="growth_"+i,monsterId="missing"
        });
        return run;
    }

    [Test]
    public void BalanceReportAggregatesMonsterResultsAndBuildsCsv()
    {
        AutoBattleBalanceReport report=new AutoBattleBalanceReport
            {wave=6,baseSeed=77,regionId="ancient_forest",difficulty="Veteran"};
        report.trials.Add(new AutoBattleBalanceTrialRecord
            {runIndex=0,wave=6,seed=77,won=true,battleSeconds=12f,playerDeaths=1});
        report.trials.Add(new AutoBattleBalanceTrialRecord
            {runIndex=1,wave=6,seed=7996,won=false,battleSeconds=18f,playerDeaths=3,
                timedOut=true});
        report.units.Add(new AutoBattleBalanceUnitRecord
            {runIndex=0,wave=6,monsterId="aranea",displayName="아라네아",team="Player",deployed=true,survived=true,won=true,damageDealt=100f,kills=2});
        report.units.Add(new AutoBattleBalanceUnitRecord
            {runIndex=1,wave=6,monsterId="aranea",displayName="아라네아",team="Player",deployed=true,survived=false,won=false,damageDealt=60f,kills=0});

        report.RebuildAggregates();
        string csv=AutoBattleBalanceReportWriter.BuildCsv(report);

        Assert.That(report.completedRuns,Is.EqualTo(2));
        Assert.That(report.timedOutRuns,Is.EqualTo(1));
        Assert.That(report.winRate,Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(report.averageBattleSeconds,Is.EqualTo(15f).Within(0.0001f));
        Assert.That(report.monsters,Has.Count.EqualTo(1));
        Assert.That(report.monsters[0].averageDamageDealt,Is.EqualTo(80f).Within(0.0001f));
        Assert.That(csv,Does.Contain("trial,6,0,77,1"));
        Assert.That(csv,Does.StartWith("record_type,wave,run,seed,won,timed_out"));
        Assert.That(csv,Does.Contain("\"aranea\",\"아라네아\",\"Player\""));
        Assert.That(csv,Does.Contain("\"ancient_forest\",\"Veteran\""));
    }

    [Test]
    public void EarlyWaveLegendaryRateIsApproximatelyOnePercent()
    {
        const int samples=200000;
        int[] counts=new int[4];
        System.Random random=new System.Random(48621);
        for(int i=0;i<samples;i++)
            counts[(int)CombatGrowthCardDraftService.RollRarity(1,random)]++;

        Assert.That(counts[(int)CombatGrowthCardRarity.Common]/(float)samples,
            Is.InRange(0.735f,0.745f));
        Assert.That(counts[(int)CombatGrowthCardRarity.Rare]/(float)samples,
            Is.InRange(0.215f,0.225f));
        Assert.That(counts[(int)CombatGrowthCardRarity.Epic]/(float)samples,
            Is.InRange(0.027f,0.033f));
        Assert.That(counts[(int)CombatGrowthCardRarity.Legendary]/(float)samples,
            Is.InRange(0.008f,0.012f));
    }

    [Test]
    public void GrowthCardEffectsStackWithoutCountLimit()
    {
        List<CombatGrowthCardSelectionSave> selections=new List<CombatGrowthCardSelectionSave>();
        for(int i=0;i<11;i++)
        {
            selections.Add(new CombatGrowthCardSelectionSave
            {
                cardId="growth_vitality",
                rarity=CombatGrowthCardRarity.Legendary
            });
            selections.Add(new CombatGrowthCardSelectionSave
            {
                cardId="growth_hardened_hide",
                rarity=CombatGrowthCardRarity.Legendary
            });
        }

        CombatGrowthCardModifiers modifiers=CombatGrowthCardCatalog.Aggregate(selections);
        Assert.That(modifiers.maxHealthBonus,Is.EqualTo(2.97f).Within(0.0001f));
        Assert.That(modifiers.damageTakenMultiplier,Is.EqualTo(Mathf.Pow(0.86f,11)).Within(0.0001f));
    }

    [Test]
    public void GrowthCardPoolContainsTheTwentyTwoApprovedCards()
    {
        string[] expected=
        {
            "growth_vitality",
            "growth_fang",
            "growth_battle_rhythm",
            "growth_hardened_hide",
            "growth_iron_bones",
            "growth_unshaken_mind",
            "growth_overflowing_magic",
            "growth_fast_cycle",
            "growth_survival_feast",
            "growth_last_stand",
            "growth_focus_fire",
            "growth_vengeance",
            "growth_weakness_exploit",
            "growth_first_strike",
            "growth_relentless_pursuit",
            "growth_victory_acceleration",
            "growth_regeneration",
            "growth_battle_preparation",
            "growth_last_defense",
            "growth_unyielding_bond",
            "growth_long_battle",
            "growth_life_cycle"
        };

        Assert.That(CombatGrowthCardCatalog.All.Select(card=>card.id),
            Is.EquivalentTo(expected));
    }

    [Test]
    public void EveryGrowthCardHasOptimizedArtwork()
    {
        foreach(CombatGrowthCardDefinition card in CombatGrowthCardCatalog.All)
        {
            Texture2D artwork=Resources.Load<Texture2D>("GrowthCards/"+card.id);
            Assert.That(artwork,Is.Not.Null,card.id+" artwork is missing.");
            Assert.That(artwork.width,Is.EqualTo(512),card.id+" artwork width");
            Assert.That(artwork.height,Is.EqualTo(512),card.id+" artwork height");
        }
    }

    [Test]
    public void GrowthCardCatalogLoadsTheSyncedGoogleSheetDatabase()
    {
        CombatGrowthCardDatabase database=
            Resources.Load<CombatGrowthCardDatabase>("CombatData/CombatGrowthCardDatabase");

        Assert.That(database,Is.Not.Null);
        Assert.That(database.cards,Has.Count.EqualTo(22));
        Assert.That(database.cards.Single(row=>row.cardId=="growth_focus_fire").displayName,
            Is.EqualTo("집중 포화"));
        Assert.That(database.cards.Single(row=>row.cardId=="growth_vengeance").legendaryValue,
            Is.EqualTo(21f));

        CombatGrowthCardCatalog.ReloadFromResources();
        Assert.That(CombatGrowthCardCatalog.All,Has.Count.EqualTo(22));
        Assert.That(CombatGrowthCardCatalog.Get("growth_last_stand")
            .Value(CombatGrowthCardRarity.Legendary),Is.EqualTo(40f));
    }

    [TestCase("growth_vitality",8f,12f,18f,27f)]
    [TestCase("growth_fang",6f,9f,14f,21f)]
    [TestCase("growth_battle_rhythm",6f,9f,14f,21f)]
    [TestCase("growth_hardened_hide",4f,6f,9f,14f)]
    [TestCase("growth_survival_feast",3f,4.5f,7f,10f)]
    [TestCase("growth_last_stand",12f,18f,27f,40f)]
    [TestCase("growth_focus_fire",3f,4f,6f,9f)]
    [TestCase("growth_vengeance",6f,9f,14f,21f)]
    [TestCase("growth_iron_bones",6f,10f,15f,23f)]
    [TestCase("growth_unshaken_mind",8f,13f,20f,30f)]
    [TestCase("growth_overflowing_magic",8f,12f,18f,27f)]
    [TestCase("growth_fast_cycle",8f,13f,20f,30f)]
    [TestCase("growth_weakness_exploit",10f,15f,23f,35f)]
    [TestCase("growth_first_strike",8f,12f,18f,27f)]
    [TestCase("growth_relentless_pursuit",1f,1.5f,2.2f,3.3f)]
    [TestCase("growth_victory_acceleration",6f,9f,14f,21f)]
    [TestCase("growth_regeneration",4f,6f,9f,14f)]
    [TestCase("growth_battle_preparation",8f,12f,18f,27f)]
    [TestCase("growth_last_defense",8f,12f,18f,27f)]
    [TestCase("growth_unyielding_bond",5f,8f,12f,18f)]
    [TestCase("growth_long_battle",2f,3f,4.5f,7f)]
    [TestCase("growth_life_cycle",3f,4.5f,7f,10f)]
    public void ApprovedGrowthCardRarityValuesArePreserved(
        string cardId,float common,float rare,float epic,float legendary)
    {
        CombatGrowthCardDefinition definition=CombatGrowthCardCatalog.Get(cardId);

        Assert.That(definition,Is.Not.Null);
        Assert.That(definition.Value(CombatGrowthCardRarity.Common),Is.EqualTo(common));
        Assert.That(definition.Value(CombatGrowthCardRarity.Rare),Is.EqualTo(rare));
        Assert.That(definition.Value(CombatGrowthCardRarity.Epic),Is.EqualTo(epic));
        Assert.That(definition.Value(CombatGrowthCardRarity.Legendary),Is.EqualTo(legendary));
    }

    [Test]
    public void ConditionalGrowthCardsAggregateEverySelection()
    {
        List<CombatGrowthCardSelectionSave> selections=new List<CombatGrowthCardSelectionSave>
        {
            new CombatGrowthCardSelectionSave
                {cardId="growth_survival_feast",rarity=CombatGrowthCardRarity.Legendary},
            new CombatGrowthCardSelectionSave
                {cardId="growth_last_stand",rarity=CombatGrowthCardRarity.Legendary},
            new CombatGrowthCardSelectionSave
                {cardId="growth_focus_fire",rarity=CombatGrowthCardRarity.Legendary},
            new CombatGrowthCardSelectionSave
                {cardId="growth_focus_fire",rarity=CombatGrowthCardRarity.Common},
            new CombatGrowthCardSelectionSave
                {cardId="growth_vengeance",rarity=CombatGrowthCardRarity.Legendary}
        };

        CombatGrowthCardModifiers modifiers=CombatGrowthCardCatalog.Aggregate(selections);

        Assert.That(modifiers.teamKillMissingHealthHealRatio,Is.EqualTo(0.10f).Within(0.0001f));
        Assert.That(modifiers.lowHealthAttackSpeedBonus,Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(modifiers.focusFireDamageBonus,Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(modifiers.vengeanceAttackDamagePerDeath,Is.EqualTo(0.21f).Within(0.0001f));
    }

    [Test]
    public void ExtendedGrowthCardModifiersKeepSecondaryAndCooldownValues()
    {
        CombatGrowthCardModifiers modifiers=CombatGrowthCardCatalog.Aggregate(new[]
        {
            new CombatGrowthCardSelectionSave{cardId="growth_unshaken_mind",rarity=CombatGrowthCardRarity.Legendary},
            new CombatGrowthCardSelectionSave{cardId="growth_unshaken_mind",rarity=CombatGrowthCardRarity.Legendary},
            new CombatGrowthCardSelectionSave{cardId="growth_fast_cycle",rarity=CombatGrowthCardRarity.Legendary},
            new CombatGrowthCardSelectionSave{cardId="growth_overflowing_magic",rarity=CombatGrowthCardRarity.Legendary}
        });
        Assert.That(modifiers.statusResistanceBonus,Is.EqualTo(60f).Within(0.0001f));
        Assert.That(modifiers.statusRecoveryDamageTakenMultiplier,Is.EqualTo(0.81f).Within(0.0001f));
        Assert.That(modifiers.cooldownRecoverySpeed,Is.EqualTo(0.30f).Within(0.0001f));
        Assert.That(modifiers.skillPowerBonus,Is.EqualTo(0.27f).Within(0.0001f));
    }

    [Test]
    public void ConditionalDamageCardsCombineWithoutHiddenCaps()
    {
        CombatGrowthCardModifiers modifiers=new CombatGrowthCardModifiers
        {
            focusFireDamageBonus=.03f,executeDamageBonus=.35f,openingDamageBonus=.27f,
            persistentTargetDamagePerHit=.033f,longBattleAttackDamagePerInterval=.07f
        };
        float actual=CombatGrowthCardCombatRules.OutgoingDamageMultiplier(
            modifiers,0,3,.2f,10f,4);
        float expected=(1f+.03f*2f)*(1f+.35f)*(1f+.033f*4f)*(1f+.07f*2f);
        Assert.That(actual,Is.EqualTo(expected).Within(.0001f));
        Assert.That(CombatGrowthCardCombatRules.OutgoingDamageMultiplier(
            modifiers,0,1,1f,5f,0),Is.EqualTo(1.27f*1.07f).Within(.0001f));
    }

    [Test]
    public void ResetRosterDropsDisabledMonstersAndLeavesFirstDeckEmpty()
    {
        MonsterDatabase database=ScriptableObject.CreateInstance<MonsterDatabase>();
        List<MonsterDefinition> definitions=new List<MonsterDefinition>();
        try
        {
            definitions.Add(CreateDefinition("disabled_test",false));
            foreach(string id in new[]{"velos","geomchi","golgaebi","ager",
                        "kongjwi","woongi","aranea","holydeer"})
                definitions.Add(CreateDefinition(id,true));
            database.monsters.AddRange(definitions);

            PlayerProfileSave profile=new PlayerProfileSave();
            for(int i=0;i<8;i++)
            {
                profile.monsters.Add(new OwnedMonsterSave
                {
                    instanceId="disabled_"+i,
                    monsterId="disabled_test"
                });
            }

            MethodInfo normalize=typeof(PlayerProfileStore).GetMethod(
                "Normalize",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(normalize,Is.Not.Null);
            normalize.Invoke(null,new object[]{profile,database});

            Assert.That(profile.monsters.Count(monster=>database.Get(monster.monsterId).enabled),
                Is.EqualTo(8));
            Assert.That(profile.expeditionPreset.rosterInstanceIds,Is.Empty);
            Assert.That(profile.firstExpeditionGuideStep,Is.EqualTo(8));
        }
        finally
        {
            foreach(MonsterDefinition definition in definitions)
                UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(database);
        }
    }

    private static MonsterDefinition CreateDefinition(string monsterId,bool enabled)
    {
        MonsterDefinition source=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(monsterId);
        MonsterDefinition definition=source!=null?UnityEngine.Object.Instantiate(source):ScriptableObject.CreateInstance<MonsterDefinition>();
        definition.monsterId=monsterId;
        definition.enabled=enabled;
        return definition;
    }

    [Test]
    public void IndependentStatRollsStayInsideConfiguredRanges()
    {
        MonsterDefinition definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        definition.monsterId="test_monster";
        definition.intrinsicTraitId="trait_test_intrinsic";

        try
        {
            for(int i=0;i<1000;i++)
            {
                OwnedMonsterSave monster=new OwnedMonsterSave
                {
                    instanceId="roll_"+i
                };
                MonsterInstanceGenerator.InitializeNewInstance(monster,definition);

                Assert.That(monster.hpMultiplier,Is.InRange(0.85f,1.15f));
                Assert.That(monster.damageMultiplier,Is.InRange(0.85f,1.15f));
                Assert.That(monster.attackSpeedMultiplier,Is.InRange(0.90f,1.10f));
                Assert.That(monster.defenseMultiplier,Is.InRange(0.85f,1.15f));
                Assert.That(monster.statusResistanceMultiplier,Is.InRange(0.85f,1.15f));
                Assert.That(monster.moveSpeedMultiplier,Is.InRange(0.94f,1.06f));
                Assert.That(monster.intrinsicTraitId,Is.EqualTo(definition.intrinsicTraitId));
                Assert.That(MonsterInstanceGenerator.AxesAreValid(monster),Is.True);
                Assert.That(PersonalityCombatPolicy.IsFeatured(monster.personalityId),
                    Is.True,monster.personalityId);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void EveryPlayableMonsterHasALoadablePortraitSprite()
    {
        MonsterDatabase database=
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        Assert.That(database.monsters.Count(monster=>
            monster!=null&&monster.enabled),Is.EqualTo(80));

        foreach(MonsterDefinition monster in database.monsters.Where(
            value=>value!=null&&value.enabled))
        {
            GameObject prefab=Resources.Load<GameObject>(monster.resourcePath);
            SpriteMonsterAnimationDriver driver=prefab!=null
                ?prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true)
                :null;
            Assert.That(driver,Is.Not.Null,monster.monsterId);
            Assert.That(driver.GetIdleFrame(0f),Is.Not.Null,
                monster.monsterId+" idle portrait failed to load.");
        }
    }

    [Test]
    public void Velos001UsesPixelSpriteAndKeepsGameplayIdentity()
    {
        MonsterDatabase database=
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database==null
            ?null:database.Get(UltraLowSDRaptorSetup.VelosMonsterId);
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);

        Assert.That(definition,Is.Not.Null);
        Assert.That(definition.serialNumber,Is.EqualTo("001"));
        Assert.That(definition.displayName,Is.EqualTo("벨로스"));
        Assert.That(definition.familyId,Is.EqualTo("raptor"));
        Assert.That(definition.skillId,Is.EqualTo("raptor_hunting_leap"));
        Assert.That(definition.intrinsicTraitId,
            Is.EqualTo("intrinsic_velos_chain_hunt"));
        Assert.That(definition.animationSetId,
            Is.EqualTo(PixelRaptorFamilySetup.AnimationSetId));
        Assert.That(database.Get("sprite_velociraptor_test_000"),Is.Null);
        Assert.That(database.monsters.Any(monster=>
            monster!=null&&monster.serialNumber=="000"),Is.False);
        Assert.That(database.Get("test_patchscale_rex"),Is.Null);
        string[] removedTesterAssets=
        {
            "Assets/Resources/MonsterData/Monster_sprite_velociraptor_test_000.asset",
            "Assets/Resources/Enemies/SpriteTestVelociraptor000Enemy.prefab",
            "Assets/Resources/MonsterData/Monster_test_patchscale_rex.asset",
            "Assets/Resources/Enemies/TestVoxelDragonEnemy.prefab",
            "Assets/Resources/Enemies/TestVoxelDragonAnimation",
            "Assets/Resources/Enemies/TestAquaDinoEnemy.prefab",
            "Assets/Resources/Enemies/TestAquaDinoAnimation",
            "Assets/Resources/MonsterPortraits/test_patchscale_rex.png"
        };
        foreach(string path in removedTesterAssets)
            Assert.That(AssetDatabase.LoadMainAssetAtPath(path),Is.Null,path);
        Assert.That(prefab,Is.Not.Null);
        Transform visual=prefab.transform.Find("Visual");
        Assert.That(visual,Is.Not.Null);
        Assert.That(visual.GetComponent<SpriteRenderer>(),Is.Not.Null);
        Assert.That(
            visual.GetComponent<SpriteMonsterAnimationDriver>(),
            Is.Not.Null);
        Assert.DoesNotThrow(PixelRaptorFamilySetup.ValidateAll);
    }

    [Test]
    public void TownProfileUsesAnimatedPixelIdleFrameForVelos()
    {
        MonsterDatabase database=
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database==null
            ?null:database.Get(UltraLowSDRaptorSetup.VelosMonsterId);
        GameObject townObject=new GameObject("TownPortraitResolverTest");
        try
        {
            BombBrick.Game.Scenes.TownSceneController controller=
                townObject.AddComponent<
                    BombBrick.Game.Scenes.TownSceneController>();
            typeof(BombBrick.Game.Scenes.TownSceneController).GetField(
                "database",BindingFlags.Instance|BindingFlags.NonPublic)
                ?.SetValue(controller,database);
            MethodInfo resolver=
                typeof(BombBrick.Game.Scenes.TownSceneController).GetMethod(
                    "ResolveMonsterPortrait",
                    BindingFlags.Instance|BindingFlags.NonPublic);

            Assert.That(definition,Is.Not.Null);
            Assert.That(resolver,Is.Not.Null);
            Sprite portrait=(Sprite)resolver.Invoke(
                controller,new object[]{definition});
            Assert.That(portrait,Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(portrait),
                Does.Contain("Resources/Enemies/PixelRaptors"));
            Assert.That(portrait.name,Does.Contain("Idle"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(townObject);
        }
    }

    [Test]
    public void Version5MigrationKeepsPermanentDataAndDropsRunProgression()
    {
        const string json=
            "{\"version\":5,\"profileId\":\"migration_test\",\"monsters\":[{"+
            "\"instanceId\":\"legacy_01\",\"monsterId\":\"raptor_reference_palette_v2\","+
            "\"customName\":\"돌진이\",\"personalityId\":\"bold\",\"personalitySeed\":123,"+
            "\"courage\":80,\"aggression\":70,\"discipline\":60,\"composure\":50,"+
            "\"cooperation\":40,\"adaptability\":30,\"hpMultiplier\":1.12,"+
            "\"damageMultiplier\":0.91,\"attackSpeedMultiplier\":1.04,\"moveSpeedMultiplier\":0.98,"+
            "\"grade\":3,\"level\":12,\"experience\":777,\"traitIds\":[\"legacy_trait\"],"+
            "\"deployed\":true,\"deploymentX\":8,\"deploymentY\":1,\"benchSlot\":-1,"+
            "\"locked\":true,\"favorite\":true,\"acquiredUtcTicks\":456}]}";

        Type legacyType=typeof(PlayerProfileStore).GetNestedType(
            "LegacyPlayerProfileSave",BindingFlags.NonPublic);
        MethodInfo migrate=typeof(PlayerProfileStore).GetMethod(
            "MigrateLegacyProfile",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.That(legacyType,Is.Not.Null);
        Assert.That(migrate,Is.Not.Null);

        object legacy=JsonUtility.FromJson(json,legacyType);
        PlayerProfileSave profile=(PlayerProfileSave)migrate.Invoke(null,new[]{legacy});

        Assert.That(profile.version,Is.EqualTo(6));
        Assert.That(profile.profileId,Is.EqualTo("migration_test"));
        Assert.That(profile.monsters,Has.Count.EqualTo(1));
        OwnedMonsterSave monster=profile.monsters[0];
        Assert.That(monster.instanceId,Is.EqualTo("legacy_01"));
        Assert.That(monster.customName,Is.EqualTo("돌진이"));
        Assert.That(monster.personalityId,Is.EqualTo("bold"));
        Assert.That(monster.hpMultiplier,Is.EqualTo(1.12f).Within(0.0001f));
        Assert.That(monster.damageMultiplier,Is.EqualTo(0.91f).Within(0.0001f));
        Assert.That(monster.locked,Is.True);
        Assert.That(monster.favorite,Is.True);
        Assert.That(profile.expeditionPreset.rosterInstanceIds,Contains.Item("legacy_01"));
        Assert.That(profile.expeditionPreset.placements[0].deployed,Is.True);

        HashSet<string> v6Fields=new HashSet<string>(StringComparer.Ordinal);
        foreach(FieldInfo field in typeof(OwnedMonsterSave).GetFields())v6Fields.Add(field.Name);
        Assert.That(v6Fields,Does.Not.Contain("grade"));
        Assert.That(v6Fields,Does.Not.Contain("level"));
        Assert.That(v6Fields,Does.Not.Contain("experience"));
        Assert.That(v6Fields,Does.Not.Contain("traitIds"));
        Assert.That(v6Fields,Does.Not.Contain("equipment"));
        Assert.That(v6Fields,Does.Not.Contain("upgrades"));
    }
}
