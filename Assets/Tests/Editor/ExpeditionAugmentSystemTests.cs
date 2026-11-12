using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;
using UnityEngine;
using Random=System.Random;

public sealed class ExpeditionAugmentSystemTests
{
    private sealed class FixedRollRandom : Random
    {
        private readonly int roll;

        public FixedRollRandom(int rollValue)
        {
            roll=rollValue;
        }

        public override int Next(int maxValue)
        {
            return Math.Max(0,Math.Min(roll,maxValue-1));
        }
    }

    [Test]
    public void SelectionPreservesFirstOrdinalMatchAndLiveChangesWithoutAllocating()
    {
        var first=new ExpeditionAugmentSelectionSave{augmentId="Mixed"};
        var duplicate=new ExpeditionAugmentSelectionSave{augmentId="Mixed"};
        var nullId=new ExpeditionAugmentSelectionSave{augmentId=null};
        var run=new ExpeditionRunSave();run.augments.Add(null);run.augments.Add(first);
        run.augments.Add(duplicate);run.augments.Add(nullId);
        Assert.That(ExpeditionAugmentRuntime.Selection(null,"Mixed"),Is.Null);
        Assert.That(ExpeditionAugmentRuntime.Selection(run,"Mixed"),Is.SameAs(first));
        Assert.That(ExpeditionAugmentRuntime.Selection(run,"mixed"),Is.Null);
        Assert.That(ExpeditionAugmentRuntime.Selection(run,null),Is.SameAs(nullId));
        for(int i=0;i<16;i++){ExpeditionAugmentRuntime.Selection(run,"Mixed");ExpeditionAugmentRuntime.Selection(run,"missing");}
        bool matches=true;
        TestDelegate work=()=>
        {
            for(int i=0;i<1024;i++)
                matches&=ReferenceEquals(ExpeditionAugmentRuntime.Selection(run,"Mixed"),first)&&
                    ExpeditionAugmentRuntime.Selection(run,"missing")==null;
        };
        work();Assert.That(work,Is.Not.AllocatingGCMemory());
        Assert.That(matches,Is.True);
        first.augmentId="changed";
        Assert.That(ExpeditionAugmentRuntime.Selection(run,"Mixed"),Is.SameAs(duplicate));
        run.augments.Remove(duplicate);
        Assert.That(ExpeditionAugmentRuntime.Selection(run,"Mixed"),Is.Null);
        run.augments=null;Assert.That(ExpeditionAugmentRuntime.Selection(run,"Mixed"),Is.Null);
    }

    [Test]
    public void SyncedDatabaseContainsApprovedAugmentAndTransformPools()
    {
        GameMasterDatabase database=GameMasterData.Database;

        Assert.That(database,Is.Not.Null);
        Assert.That(database.augments,Has.Count.EqualTo(26));
        Assert.That(database.augments.Count(row=>row!=null&&row.enabled),Is.EqualTo(20));
        Assert.That(database.augments.Single(row=>
            row.id=="augment_overcharge_workshop").enabled,Is.False);
        Assert.That(database.augments.Single(row=>
            row.id=="augment_mutation_recombobulator").enabled,Is.False);
        Assert.That(database.augments.Single(row=>
            row.id=="augment_golden_dismantler").enabled,Is.False);
        Assert.That(database.augments.Single(row=>
            row.id=="augment_isolation_tactics").enabled,Is.False);
        Assert.That(database.augments.Single(row=>
            row.id=="augment_battle_bond").enabled,Is.False);
        Assert.That(ExpeditionAugmentRuntime.Supports(
            "augment_overcharge_workshop"),Is.False);
        Assert.That(ExpeditionAugmentRuntime.Supports(
            "augment_mutation_recombobulator"),Is.False);
        Assert.That(ExpeditionAugmentRuntime.Supports(
            "augment_golden_dismantler"),Is.False);
        Assert.That(ExpeditionAugmentRuntime.Supports(
            "augment_isolation_tactics"),Is.False);
        Assert.That(ExpeditionAugmentRuntime.Supports(
            "augment_battle_bond"),Is.False);
        Assert.That(database.itemTransforms,Has.Count.EqualTo(36));
        Assert.That(database.itemTransforms,Has.All.Matches<ItemTransformMasterRow>(row=>
            row!=null&&row.enabled&&
            row.greatSuccessPercent+row.successPercent+
            row.noChangePercent+row.failurePercent==100));
    }

    [Test]
    public void AugmentDraftIsDeterministicAndExcludesDisabledRows()
    {
        ExpeditionRunSave first=new ExpeditionRunSave{wave=1};
        ExpeditionRunSave second=new ExpeditionRunSave{wave=1};

        string[] firstDraft=ExpeditionAugmentSystem.CreateOrRestoreDraft(first,417).ToArray();
        string[] secondDraft=ExpeditionAugmentSystem.CreateOrRestoreDraft(second,417).ToArray();

        Assert.That(firstDraft,Has.Length.EqualTo(3));
        Assert.That(firstDraft,Is.EqualTo(secondDraft));
        Assert.That(firstDraft,Has.None.EqualTo("augment_completed_item_aging"));
        Assert.That(firstDraft,Has.None.EqualTo("augment_battle_contract"));
        Assert.That(first.augmentRerollsRemaining,Is.EqualTo(1));
    }

    [Test]
    public void AugmentsAreOfferedOnlyAtFirstAndSeventhWaves()
    {
        Assert.That(ExpeditionAugmentSystem.IsOfferWave(1),Is.True);
        Assert.That(ExpeditionAugmentSystem.IsOfferWave(7),Is.True);
        for(int wave=2;wave<=12;wave++)
            if(wave!=7)Assert.That(
                ExpeditionAugmentSystem.IsOfferWave(wave),Is.False,"Wave "+wave);
    }

    [TestCase(0,CorruptionRollOutcome.GreatSuccess)]
    [TestCase(14,CorruptionRollOutcome.GreatSuccess)]
    [TestCase(15,CorruptionRollOutcome.Success)]
    [TestCase(84,CorruptionRollOutcome.Success)]
    [TestCase(85,CorruptionRollOutcome.Failure)]
    [TestCase(99,CorruptionRollOutcome.Failure)]
    public void CorruptionUsesApprovedProbabilityBands(
        int roll,CorruptionRollOutcome expected)
    {
        ItemTransformMasterRow row=GameMasterData.ItemTransform("complete_giant_heart");

        Assert.That(row,Is.Not.Null);
        Assert.That(ExpeditionItemTransformSystem.RollCorruption(
            row,new FixedRollRandom(roll)),Is.EqualTo(expected));
    }

    [Test]
    public void OverchargeAndFailureProduceDifferentEffectiveItemStats()
    {
        ExpeditionItemInstanceSave normal=new ExpeditionItemInstanceSave{
            instanceId="normal",itemId="complete_giant_heart"};
        ExpeditionItemInstanceSave overcharged=new ExpeditionItemInstanceSave{
            instanceId="overcharged",itemId="complete_giant_heart",
            transformState=ExpeditionItemTransformState.Overcharged};
        ExpeditionItemInstanceSave failed=new ExpeditionItemInstanceSave{
            instanceId="failed",itemId="complete_giant_heart",
            transformState=ExpeditionItemTransformState.CorruptedFailure};

        ItemMasterRow normalStats=ExpeditionItemTransformSystem.ResolveEffectiveMaster(normal);
        ItemMasterRow overchargedStats=
            ExpeditionItemTransformSystem.ResolveEffectiveMaster(overcharged);
        ItemMasterRow failedStats=ExpeditionItemTransformSystem.ResolveEffectiveMaster(failed);

        Assert.That(overchargedStats.maxHealthBonus,Is.GreaterThan(normalStats.maxHealthBonus));
        Assert.That(failedStats.maxHealthBonus,Is.LessThan(normalStats.maxHealthBonus));
    }

    [Test]
    public void CorruptionAlwaysTransformsAndLocksTheCompletedItem()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionItemInstanceSave item=ExpeditionItemSystem.CreateItem(
            run,"complete_giant_heart",true);

        bool applied=ExpeditionItemTransformSystem.TryApplyCorruption(
            run,item.instanceId,17,out CorruptionRollOutcome outcome,out string error);

        Assert.That(applied,Is.True,error);
        Assert.That(outcome,Is.Not.EqualTo(CorruptionRollOutcome.NoChange));
        Assert.That(item.transformState,Is.Not.EqualTo(ExpeditionItemTransformState.None));
        Assert.That(item.transformLocked,Is.True);
    }

    [Test]
    public void GreatSuccessEffectIsExposedByEquippedItemAggregation()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave monster=new ExpeditionMonsterStateSave{
            instanceId="monster",
            itemInstanceIds=new System.Collections.Generic.List<string>{
                "great_success","",""}};
        run.monsters.Add(monster);
        run.itemInstances.Add(new ExpeditionItemInstanceSave{
            instanceId="great_success",
            itemId="complete_giant_heart",
            transformState=ExpeditionItemTransformState.CorruptedGreatSuccess});

        CombatItemModifiers modifiers=ExpeditionItemSystem.Aggregate(run,monster);
        ItemMasterRow great=ExpeditionItemTransformSystem.ResolveEffectiveMaster(
            run.itemInstances[0]);
        ItemMasterRow overcharged=ExpeditionItemTransformSystem.ResolveEffectiveMaster(
            new ExpeditionItemInstanceSave{itemId="complete_giant_heart",
                transformState=ExpeditionItemTransformState.Overcharged});

        Assert.That(modifiers.transformedEffects,Has.Count.EqualTo(1));
        Assert.That(modifiers.transformedEffects[0].itemId,Is.EqualTo("complete_giant_heart"));
        Assert.That(modifiers.transformedEffects[0].corruptionGreatEffectId,Is.Not.Empty);
        Assert.That(great.maxHealthBonus,Is.EqualTo(overcharged.maxHealthBonus));
    }

    [Test]
    public void RuntimeSupportsAllThirtySixCorruptionGreatSuccessEffects()
    {
        ItemTransformMasterRow[] enabled=GameMasterData.Database.itemTransforms
            .Where(row=>row!=null&&row.enabled).ToArray();
        string[] duplicateIds=enabled.GroupBy(row=>row.corruptionGreatEffectId)
            .Where(group=>group.Count()>1).Select(group=>group.Key).ToArray();
        string[] unsupported=enabled.Where(row=>
                !CorruptionGreatSuccessRules.Supports(row.corruptionGreatEffectId))
            .Select(row=>row.itemId+":"+row.corruptionGreatEffectId).ToArray();

        Assert.That(enabled,Has.Length.EqualTo(36));
        Assert.That(duplicateIds,Is.Empty,"Great-success effect IDs must be unique.");
        Assert.That(CorruptionGreatSuccessRules.SupportedEffectIds,
            Has.Count.EqualTo(36));
        Assert.That(unsupported,Is.Empty);
    }

    [Test]
    public void GreatSuccessParameterParserReadsNumericBooleanAndTextValues()
    {
        ItemTransformMasterRow bloodBlade=
            GameMasterData.ItemTransform("complete_magic_blade_core");
        ItemTransformMasterRow giant=
            GameMasterData.ItemTransform("complete_giant_heart");

        Assert.That(bloodBlade.GreatParam("damage_heal_ratio"),Is.EqualTo(.08f)
            .Within(.0001f));
        Assert.That(bloodBlade.GreatParamBool("store_overheal"),Is.True);
        Assert.That(bloodBlade.GreatParamRaw("bonus_damage_type"),
            Is.EqualTo("DefenseIgnore"));
        Assert.That(giant.GreatParamInt("uses_per_wave"),Is.EqualTo(1));
        Assert.That(giant.GreatParam("missing",17f),Is.EqualTo(17f));
    }

    [Test]
    public void CorruptionDiagnosticTelemetryCountsAndResetsActivations()
    {
        CorruptionGreatSuccessDiagnostics.Reset();
        CorruptionGreatSuccessDiagnostics.Record("CorruptGiantAwakening");
        CorruptionGreatSuccessDiagnostics.Record("CorruptGiantAwakening");
        CorruptionGreatSuccessDiagnostics.Record("CorruptSkillEcho");

        Assert.That(CorruptionGreatSuccessDiagnostics.Count(
            "CorruptGiantAwakening"),Is.EqualTo(2));
        Assert.That(CorruptionGreatSuccessDiagnostics.Count(
            "CorruptSkillEcho"),Is.EqualTo(1));
        Assert.That(CorruptionGreatSuccessDiagnostics.Summary(),
            Does.StartWith("activated=2/36"));

        CorruptionGreatSuccessDiagnostics.Reset();
        Assert.That(CorruptionGreatSuccessDiagnostics.Count(
            "CorruptGiantAwakening"),Is.Zero);
    }

    [Test]
    public void EveryCorruptionEffectHasReadablePresentationFeedback()
    {
        foreach(ItemTransformMasterRow row in GameMasterData.Database.itemTransforms
            .Where(value=>value!=null&&value.enabled))
        {
            CorruptionFeedbackStyle style=
                CorruptionGreatSuccessPresentation.Style(row.corruptionGreatEffectId);
            string label=CorruptionGreatSuccessPresentation.Label(row);

            Assert.That(label,Does.StartWith("◆ "),row.itemId);
            Assert.That(label.Length,Is.GreaterThan(3),row.itemId);
            Assert.That(style.color.a,Is.GreaterThan(0f),row.itemId);
            Assert.That(style.pulseScale,Is.InRange(.75f,1.25f),row.itemId);
        }
    }

    [Test]
    public void RuntimeSupportsEveryEnabledSyncedAugment()
    {
        string[] unsupported=GameMasterData.Augments
            .Where(row=>!ExpeditionAugmentRuntime.Supports(row.id))
            .Select(row=>row.id).ToArray();

        Assert.That(GameMasterData.Augments,Has.Count.EqualTo(20));
        Assert.That(GameMasterData.Augment("augment_mutation_recombobulator"),Is.Null);
        Assert.That(unsupported,Is.Empty);
    }

    [Test]
    public void ImmediateLifeAndWindfallAugmentsApplyDatabaseValues()
    {
        AugmentMasterRow lifeRow=GameMasterData.Augment(
            ExpeditionAugmentRuntime.LifeExpansion);
        ExpeditionRunSave run=new ExpeditionRunSave{
            wave=1,lives=3,maximumLives=3,expeditionCurrency=0};
        ExpeditionAugmentSelectionSave life=new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.LifeExpansion,
            acquiredWave=1,deterministicSeed=1};

        ExpeditionAugmentRuntime.ApplyOnAcquire(run,life,new Random(1));

        Assert.That(lifeRow.effectSummary,Is.EqualTo(
            "최대 목숨이 5가 되고, 목숨을 2 회복합니다."));
        Assert.That(lifeRow.NumericParam("life_gain"),Is.EqualTo(2f));
        Assert.That(lifeRow.NumericParam("max_lives"),Is.EqualTo(5f));
        Assert.That(run.lives,Is.EqualTo(5));
        Assert.That(run.maximumLives,Is.EqualTo(5));

        ExpeditionAugmentSelectionSave windfall=new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.FieldLoan,
            acquiredWave=1,deterministicSeed=2};
        ExpeditionAugmentRuntime.ApplyOnAcquire(run,windfall,new Random(2));
        Assert.That(run.expeditionCurrency,Is.EqualTo(30));

        ExpeditionDropSystem.Commit(run,new PendingWaveLoot{expeditionCurrency=4});
        Assert.That(run.expeditionCurrency,Is.EqualTo(34));
    }

    [Test]
    public void LastBlessingAutomaticallyGrantsGoldAndCompletedItemOnce()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{
            runId="last_blessing_test",wave=7,lives=1,expeditionCurrency=3};
        ExpeditionAugmentSelectionSave blessing=new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.LastBlessing};
        run.augments.Add(blessing);

        bool triggered=ExpeditionAugmentRuntime.TryGrantLastBlessingReward(
            run,out int gold,out ItemMasterRow item);

        Assert.That(triggered,Is.True);
        Assert.That(gold,Is.EqualTo(20));
        Assert.That(run.expeditionCurrency,Is.EqualTo(23));
        Assert.That(item,Is.Not.Null);
        Assert.That(item.IsCompleted,Is.True);
        Assert.That(run.inventoryItemInstanceIds,Has.Count.EqualTo(1));
        Assert.That(blessing.triggered,Is.True);

        bool triggeredAgain=ExpeditionAugmentRuntime.TryGrantLastBlessingReward(
            run,out _,out _);
        Assert.That(triggeredAgain,Is.False);
        Assert.That(run.expeditionCurrency,Is.EqualTo(23));
        Assert.That(run.inventoryItemInstanceIds,Has.Count.EqualTo(1));
    }

    [Test]
    public void LifeMortgageUsesDatabaseValuesAndTriggersLastBlessingAtOneLife()
    {
        AugmentMasterRow row=GameMasterData.Augment(
            ExpeditionAugmentRuntime.LifeMortgage);
        ExpeditionRunSave run=new ExpeditionRunSave{
            runId="life_mortgage_test",wave=5,lives=2,maximumLives=3,
            expeditionCurrency=3};
        ExpeditionAugmentSelectionSave blessing=new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.LastBlessing};
        ExpeditionAugmentSelectionSave mortgage=new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.LifeMortgage};
        run.augments.Add(blessing);
        run.augments.Add(mortgage);

        ExpeditionAugmentRuntime.ApplyOnAcquire(run,mortgage,new Random(7));

        Assert.That(row.effectSummary,Is.EqualTo(
            "목숨 -1. 완성 아이템 3개 중 1개와 15골드 획득."));
        Assert.That(row.NumericParam("life_cost"),Is.EqualTo(1f));
        Assert.That(row.NumericParam("currency_reward"),Is.EqualTo(15f));
        Assert.That(run.lives,Is.EqualTo(1));
        Assert.That(run.expeditionCurrency,Is.EqualTo(38));
        Assert.That(run.inventoryItemInstanceIds,Has.Count.EqualTo(1));
        Assert.That(blessing.triggered,Is.True);
    }

    [Test]
    public void AutoDeploymentRefundsPriorUpgradesAndExpandsEveryTwoWaves()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{
            wave=5,maxDeploymentCount=6,expeditionCurrency=2};
        ExpeditionAugmentSelectionSave deployment=new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.AutoDeployment,acquiredWave=5};
        run.augments.Add(deployment);

        ExpeditionAugmentRuntime.ApplyOnAcquire(run,deployment,new Random(1));

        Assert.That(deployment.counter,Is.EqualTo(16));
        Assert.That(run.expeditionCurrency,Is.EqualTo(18));
        Assert.That(run.maxDeploymentCount,Is.EqualTo(7));
        Assert.That(ExpeditionDeploymentSystem.TryUpgrade(run),Is.False);

        ExpeditionAugmentRuntime.ProcessWaveCleared(run,6,1);
        Assert.That(run.maxDeploymentCount,Is.EqualTo(7));
        ExpeditionAugmentRuntime.ProcessWaveCleared(run,7,2);
        Assert.That(run.maxDeploymentCount,Is.EqualTo(8));
    }

    [Test]
    public void ProtagonistUsesDatabaseStatsAndClimaxRules()
    {
        AugmentMasterRow row=GameMasterData.Augment(
            ExpeditionAugmentRuntime.Protagonist);
        ExpeditionRunSave run=new ExpeditionRunSave{wave=1};
        run.augments.Add(new ExpeditionAugmentSelectionSave
        {
            augmentId=ExpeditionAugmentRuntime.Protagonist,
            targetMonsterInstanceId="hero"
        });

        ExpeditionAugmentCombatModifiers modifiers=
            ExpeditionAugmentRuntime.CombatModifiers(run,"hero",1);

        Assert.That(row,Is.Not.Null);
        Assert.That(row.effectSummary,Is.EqualTo(
            "몬스터 1명 지정: 체력·공격력 +15%. 게이지 100에서 Wave당 1회 클라이맥스."));
        Assert.That(row.dataVersion,Is.EqualTo(3));
        Assert.That(modifiers.protagonist,Is.True);
        Assert.That(modifiers.maxHealthBonus,
            Is.EqualTo(row.NumericParam("health_bonus")).Within(.0001f));
        Assert.That(modifiers.attackDamageBonus,
            Is.EqualTo(row.NumericParam("attack_bonus")).Within(.0001f));
        Assert.That(row.NumericParam("basic_hit_gain"),Is.EqualTo(4f));
        Assert.That(row.NumericParam("hit_received_gain"),Is.EqualTo(3f));
        Assert.That(row.NumericParam("skill_gain"),Is.EqualTo(25f));
        Assert.That(row.NumericParam("hit_gain_icd"),Is.EqualTo(.5f));
        Assert.That(row.NumericParam("kill_assist_gain"),Is.EqualTo(10f));
        Assert.That(row.NumericParam("climax_duration"),Is.EqualTo(8f));
    }

    [Test]
    public void GiantAndTimeCompressionProduceApprovedCombatModifiers()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{wave=1};
        run.augments.Add(new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.GiantSigil,
            targetMonsterInstanceId="monster"});
        run.augments.Add(new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.TimeCompression});

        ExpeditionAugmentCombatModifiers modifiers=
        ExpeditionAugmentRuntime.CombatModifiers(run,"monster",1);

        Assert.That(modifiers.deploymentSlots,Is.EqualTo(2));
        Assert.That(modifiers.maxHealthBonus,Is.EqualTo(0.80f).Within(0.0001f));
        Assert.That(modifiers.attackDamageBonus,Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(modifiers.defenseBonus,Is.Zero);
        Assert.That(modifiers.statusResistanceBonus,Is.Zero);
        Assert.That(modifiers.damageReduction,Is.Zero);
        Assert.That(modifiers.skillPowerMultiplier,Is.EqualTo(1.25f).Within(0.0001f));
        Assert.That(modifiers.skillAreaMultiplier,Is.EqualTo(1.30f).Within(0.0001f));
        Assert.That(modifiers.modelScaleMultiplier,Is.EqualTo(1.35f).Within(0.0001f));
        Assert.That(modifiers.skillCooldownMultiplier,Is.EqualTo(0.70f).Within(0.0001f));
        Assert.That(modifiers.basicAttackDamageMultiplier,Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(modifiers.minimumSkillCooldown,Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void GoldenDismantlerRestoresTheOriginalTwoComponents()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionItemInstanceSave completed=ExpeditionItemSystem.CreateItem(
            run,"complete_giant_heart",true);
        ItemMasterRow master=GameMasterData.Item(completed.itemId);

        bool dismantled=ExpeditionItemSystem.TryDismantle(
            run,completed.instanceId,out ExpeditionItemInstanceSave first,
            out ExpeditionItemInstanceSave second,out string error);

        Assert.That(dismantled,Is.True,error);
        Assert.That(new[]{first.itemId,second.itemId},Is.EquivalentTo(
            new[]{master.componentAId,master.componentBId}));
        Assert.That(run.itemInstances.Any(value=>
            value.instanceId==completed.instanceId),Is.False);
    }

    [Test]
    public void RetryCriticalAugmentStateSurvivesJsonRoundTrip()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{
            runId="retry",wave=5,lives=2,augmentDraftWave=5,
            augmentDraftIds=new System.Collections.Generic.List<string>{
                "a","b","c"},
            augmentRerollsRemaining=0};
        run.augments.Add(new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.EarlyAwakening,
            acquiredWave=5,targetMonsterInstanceId="monster",remainingWaves=3});
        run.specialItems.Add(new ExpeditionSpecialItemSave{
            instanceId="catalyst",itemId="special_corruption_catalyst",count=1});

        ExpeditionRunSave restored=JsonUtility.FromJson<ExpeditionRunSave>(
            JsonUtility.ToJson(run));

        Assert.That(restored.wave,Is.EqualTo(5));
        Assert.That(restored.lives,Is.EqualTo(2));
        Assert.That(restored.augmentDraftIds,Is.EqualTo(new[]{"a","b","c"}));
        Assert.That(restored.augments[0].remainingWaves,Is.EqualTo(3));
        Assert.That(restored.specialItems[0].itemId,
            Is.EqualTo("special_corruption_catalyst"));
    }

    [Test]
    public void DuplicateInventoryItemsRemainSeparateInstances()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionItemInstanceSave first=ExpeditionItemSystem.CreateItem(
            run,"base_fang",true);
        ExpeditionItemInstanceSave second=ExpeditionItemSystem.CreateItem(
            run,"base_fang",true);

        IReadOnlyList<ExpeditionItemInstanceSave> inventory=
            ExpeditionItemSystem.InventoryItems(run);

        Assert.That(inventory.Count,Is.EqualTo(2));
        Assert.That(inventory.Select(item=>item.instanceId),Is.EquivalentTo(
            new[]{first.instanceId,second.instanceId}));
        Assert.That(inventory.Select(item=>item.instanceId).Distinct().Count(),
            Is.EqualTo(2));
    }

    [Test]
    public void SpecialItemGrantCreatesOneVisibleInstancePerUse()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();

        ExpeditionAugmentRuntime.GrantSpecialItem(
            run,"special_golden_dismantler",5);

        Assert.That(run.specialItems,Has.Count.EqualTo(5));
        Assert.That(run.specialItems,Has.All.Matches<ExpeditionSpecialItemSave>(
            item=>item.count==1));
        Assert.That(run.specialItems.Select(item=>item.instanceId).Distinct().Count(),
            Is.EqualTo(5));
    }

    [Test]
    public void ConsumingSpecialItemRemovesTheExactDraggedInstance()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionAugmentRuntime.GrantSpecialItem(
            run,"special_golden_dismantler",3);
        string consumedId=run.specialItems[1].instanceId;

        bool consumed=ExpeditionAugmentRuntime.TryConsumeSpecialItemInstance(
            run,consumedId,out string error);

        Assert.That(consumed,Is.True,error);
        Assert.That(run.specialItems,Has.Count.EqualTo(2));
        Assert.That(run.specialItems.Any(item=>item.instanceId==consumedId),Is.False);
    }

    [Test]
    public void PermanentMonsterCoreIsConsumedAndStoredAsIrreversibleMark()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave monster=new ExpeditionMonsterStateSave{
            instanceId="monster"};
        run.monsters.Add(monster);
        ExpeditionSpecialItemSave core=ExpeditionAugmentRuntime.GrantSpecialItem(
            run,"special_combined_cast_core");

        bool applied=ExpeditionAugmentRuntime.TryApplyPermanentMonsterSpecialItem(
            run,core.instanceId,monster.instanceId,out string error);

        Assert.That(applied,Is.True,error);
        Assert.That(run.specialItems,Is.Empty);
        Assert.That(monster.appliedSpecialItemIds,
            Does.Contain("special_combined_cast_core"));
        Assert.That(ExpeditionAugmentRuntime.HasBoundSpecialItem(
            run,"special_combined_cast_core",monster.instanceId),Is.True);
        Assert.That(ExpeditionAugmentRuntime.CombatModifiers(
            run,monster.instanceId,1).combinedCaster,Is.True);
    }

    [Test]
    public void ReturnMarkIsGrantedAndMovedAsLockedCompletedEquipment()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{wave=1};
        ExpeditionMonsterStateSave monster=new ExpeditionMonsterStateSave{
            instanceId="monster"};
        run.monsters.Add(monster);
        ExpeditionAugmentSelectionSave selection=new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.ReturnMark,acquiredWave=1};

        ExpeditionAugmentRuntime.ApplyOnAcquire(run,selection,new Random(1));

        Assert.That(run.specialItems,Is.Empty);
        ExpeditionItemInstanceSave item=run.itemInstances.Single(value=>
            value.itemId=="special_return_mark");
        Assert.That(item.transformLocked,Is.True);
        Assert.That(GameMasterData.Item(item.itemId).IsCompleted,Is.True);
        Assert.That(run.inventoryItemInstanceIds,Does.Contain(item.instanceId));
        Assert.That(ExpeditionItemSystem.Equip(
            run,monster.instanceId,item.instanceId,0,out string error),Is.True,error);
        Assert.That(ExpeditionItemSystem.Aggregate(run,monster).specialEffects
            .Select(value=>value.specialEffect),Does.Contain("ReturnMark"));
        ExpeditionAugmentCombatModifiers modifiers=
            ExpeditionAugmentRuntime.CombatModifiers(run,monster.instanceId,1);
        Assert.That(modifiers.returnMarked,Is.True);
        Assert.That(modifiers.returnDelay,Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(modifiers.returnHealthRatio,Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(modifiers.returnSkillChargeRatio,Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void LegacyPermanentReturnMarkMigratesIntoAnEquipmentSlot()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave monster=new ExpeditionMonsterStateSave{
            instanceId="monster"};
        monster.appliedSpecialItemIds.Add("special_return_mark");
        run.monsters.Add(monster);

        ExpeditionAugmentRuntime.CombatModifiers(run,monster.instanceId,1);

        ExpeditionItemInstanceSave item=run.itemInstances.Single(value=>
            value.itemId=="special_return_mark");
        Assert.That(monster.appliedSpecialItemIds,
            Does.Not.Contain("special_return_mark"));
        Assert.That(monster.itemInstanceIds,Does.Contain(item.instanceId));
        Assert.That(item.transformLocked,Is.True);
    }

    [Test]
    public void LegacyBoundCoreMigratesToConsumedPermanentMark()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave monster=new ExpeditionMonsterStateSave{
            instanceId="monster"};
        run.monsters.Add(monster);
        run.specialItems.Add(new ExpeditionSpecialItemSave{
            instanceId="legacy_core",
            itemId="special_combined_cast_core",
            boundMonsterInstanceId=monster.instanceId});

        Assert.That(ExpeditionAugmentRuntime.HasBoundSpecialItem(
            run,"special_combined_cast_core",monster.instanceId),Is.True);
        Assert.That(run.specialItems,Is.Empty);
        Assert.That(monster.appliedSpecialItemIds,
            Does.Contain("special_combined_cast_core"));
    }

    [Test]
    public void LegacyStackedSpecialItemsAreNormalizedIntoIndividualInstances()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        run.specialItems.Add(new ExpeditionSpecialItemSave{
            instanceId="legacy_stack",
            itemId="special_golden_dismantler",
            count=5});

        ExpeditionAugmentRuntime.TryConsumeSpecialItem(
            run,"special_golden_dismantler",out string error);

        Assert.That(error,Is.Empty);
        Assert.That(run.specialItems,Has.Count.EqualTo(4));
        Assert.That(run.specialItems,Has.All.Matches<ExpeditionSpecialItemSave>(
            item=>item.count==1));
    }

    [Test]
    public void TemporaryItemIsDestroyedWhenUnequipped()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave monster=new ExpeditionMonsterStateSave{
            instanceId="monster"};
        run.monsters.Add(monster);
        ExpeditionItemInstanceSave item=ExpeditionItemSystem.CreateItem(
            run,"complete_giant_heart",true);
        item.temporary=true;
        item.destroyOnUnequip=true;
        item.transformLocked=true;
        Assert.That(ExpeditionItemSystem.Equip(
            run,monster.instanceId,item.instanceId,0,out string equipError),
            Is.True,equipError);

        Assert.That(ExpeditionItemSystem.Unequip(
            run,monster.instanceId,0,out string unequipError),Is.True,unequipError);

        Assert.That(run.itemInstances.Any(value=>
            value.instanceId==item.instanceId),Is.False);
        Assert.That(run.inventoryItemInstanceIds,Does.Not.Contain(item.instanceId));
    }

    [Test]
    public void DoubleSupplyOffersRadiantChoiceAndStoresRewardInInventory()
    {
        AugmentMasterRow row=GameMasterData.Augment(ExpeditionAugmentRuntime.DoubleSupply);
        Assert.That(row.selectionMode,Is.EqualTo("Choice3"));
        Assert.That(row.effectSummary,
            Is.EqualTo("찬란한 완성 아이템 1개를 선택해 획득."));
        Assert.That(row.NumericParam("candidate_count"),Is.EqualTo(3f));

        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionItemInstanceSave radiant=
            ExpeditionItemSystem.CreateRadiantCompletedItem(
                run,"complete_giant_heart");
        ItemMasterRow normal=GameMasterData.Item(radiant.itemId);
        ItemMasterRow enhanced=ExpeditionItemTransformSystem.ResolveEffectiveMaster(radiant);

        Assert.That(radiant.transformState,Is.EqualTo(
            ExpeditionItemTransformState.Radiant));
        Assert.That(radiant.temporary,Is.False);
        Assert.That(run.inventoryItemInstanceIds,Does.Contain(radiant.instanceId));
        Assert.That(enhanced.maxHealthBonus,Is.GreaterThan(normal.maxHealthBonus));
        Assert.That(ExpeditionItemTransformSystem.DisplayName(radiant),
            Is.EqualTo("찬란한 "+normal.displayName));
        Assert.That(ExpeditionItemTransformSystem.TryApplyCorruption(
            run,radiant.instanceId,1,out _,out _),Is.False);
    }

    [Test]
    public void PandoraTransformsOnlyPreBattleMaterialsAfterEnteringNextWave()
    {
        ExpeditionRunSave run=new ExpeditionRunSave{runId="pandora",wave=1};
        ExpeditionAugmentSelectionSave pandora=new ExpeditionAugmentSelectionSave
        {
            augmentId=ExpeditionAugmentRuntime.PandoraWorkshop,
            acquiredWave=1,deterministicSeed=7
        };
        run.augments.Add(pandora);
        ExpeditionAugmentRuntime.ApplyOnAcquire(run,pandora,new Random(7));
        pandora.triggered=true; // Legacy saves used this flag for a one-time grant.

        ExpeditionItemInstanceSave existing=ExpeditionItemSystem.CreateItem(
            run,"base_fang",true);
        ExpeditionAugmentRuntime.PreparePandoraWorkshopWave(run);
        ExpeditionItemInstanceSave waveLoot=ExpeditionItemSystem.CreateItem(
            run,"base_heart",true);
        string lootItemId=waveLoot.itemId;
        int countBeforeTransition=run.inventoryItemInstanceIds.Count;

        ExpeditionAugmentRuntime.ProcessWaveCleared(run,1,99);
        Assert.That(existing.itemId,Is.EqualTo("base_fang"),
            "승리 처리 중에는 변환하면 안 됩니다.");
        Assert.That(waveLoot.itemId,Is.EqualTo(lootItemId));

        run.wave=2;
        List<string> transformedIds=new List<string>();
        ExpeditionItemInstanceSave granted=
            ExpeditionAugmentRuntime.ProcessPandoraWorkshopWaveEntered(
                run,1,101,transformedIds);

        Assert.That(existing.itemId,Is.Not.EqualTo("base_fang"));
        Assert.That(transformedIds,Is.EquivalentTo(new[]{existing.instanceId}));
        Assert.That(waveLoot.itemId,Is.EqualTo(lootItemId),
            "직전 Wave 전리품은 해당 전환에서 판도라 영향을 받으면 안 됩니다.");
        Assert.That(granted,Is.Not.Null);
        Assert.That(GameMasterData.Item(granted.itemId).IsBase,Is.True);
        Assert.That(run.inventoryItemInstanceIds.Count,
            Is.EqualTo(countBeforeTransition+1));
        Assert.That(pandora.counter,Is.EqualTo(2));
    }

    [Test]
    public void PandoraRerollsStoredEquipmentWithinItsTierAndKeepsEveryVariant()
    {
        const string originalId="complete_giant_heart";
        ExpeditionRunSave run=new ExpeditionRunSave{runId="pandora_equipment",wave=1};
        run.augments.Add(new ExpeditionAugmentSelectionSave
        {
            augmentId=ExpeditionAugmentRuntime.PandoraWorkshop,acquiredWave=1
        });
        var variants=Enum.GetValues(typeof(ExpeditionItemTransformState))
            .Cast<ExpeditionItemTransformState>().ToDictionary(state=>state,state=>
            {
                ExpeditionItemInstanceSave item=ExpeditionItemSystem.CreateItem(run,originalId,true);
                item.transformState=state;
                item.transformLocked=state!=ExpeditionItemTransformState.None;
                item.transformSeed=19;
                return item;
            });
        ExpeditionItemInstanceSave unique=ExpeditionItemSystem.CreateItem(run,"special_return_mark",true);
        ExpeditionItemInstanceSave equipped=ExpeditionItemSystem.CreateItem(run,originalId,true);
        run.monsters.Add(new ExpeditionMonsterStateSave{instanceId="ally"});
        ExpeditionAugmentRuntime.PreparePandoraWorkshopWave(run);
        Assert.That(ExpeditionItemSystem.Equip(run,"ally",equipped.instanceId,0,out string error),
            Is.True,error);

        run.wave=2;
        List<string> transformed=new List<string>();
        ExpeditionItemInstanceSave grant=ExpeditionAugmentRuntime.ProcessPandoraWorkshopWaveEntered(
            run,1,121,transformed);
        Assert.That(GameMasterData.Item(grant.itemId).IsBase,Is.True);
        Assert.That(transformed,Is.EquivalentTo(variants.Values.Select(item=>item.instanceId)));
        foreach(var variant in variants)
        {
            ExpeditionItemInstanceSave item=variant.Value;
            ItemMasterRow replacement=GameMasterData.Item(item.itemId);
            Assert.That(item.itemId,Is.Not.EqualTo(originalId));
            Assert.That(replacement.IsCompleted,Is.True);
            Assert.That(replacement.itemPoolId,Is.EqualTo("default_equipment"));
            Assert.That(item.transformState,Is.EqualTo(variant.Key));
            Assert.That(item.transformLocked,Is.EqualTo(variant.Key!=ExpeditionItemTransformState.None));
            Assert.That(item.transformSeed,Is.EqualTo(19));
            if(variant.Key==ExpeditionItemTransformState.CorruptedGreatSuccess)
                Assert.That(GameMasterData.ItemTransform(item.itemId)?.corruptionGreatEffectId,Is.Not.Null.And.Not.Empty);
        }
        Assert.That(equipped.itemId,Is.EqualTo(originalId));
        Assert.That(unique.itemId,Is.EqualTo("special_return_mark"));
    }

    [TestCase(1)]
    [TestCase(7)]
    public void PandoraGrantsOneBasicItemOnAcquisitionAndEachNewWave(int acquiredWave)
    {
        ExpeditionRunSave run=new ExpeditionRunSave{runId="pandora",wave=acquiredWave};
        ExpeditionAugmentSelectionSave pandora=new ExpeditionAugmentSelectionSave
        {
            augmentId=ExpeditionAugmentRuntime.PandoraWorkshop,acquiredWave=acquiredWave
        };
        run.augments.Add(pandora);
        ExpeditionAugmentRuntime.ApplyOnAcquire(run,pandora,new Random(1));

        for(int wave=acquiredWave;wave<=acquiredWave+2;wave++)
        {
            run.wave=wave;
            ExpeditionItemInstanceSave grant=
                ExpeditionAugmentRuntime.ProcessPandoraWorkshopWaveEntered(run,wave-1,wave);
            Assert.That(grant,Is.Not.Null);
            Assert.That(GameMasterData.Item(grant.itemId).IsBase,Is.True);
            Assert.That(run.inventoryItemInstanceIds,Does.Contain(grant.instanceId));

            run=UnityEngine.JsonUtility.FromJson<ExpeditionRunSave>(UnityEngine.JsonUtility.ToJson(run));
            ExpeditionAugmentRuntime.PreparePandoraWorkshopWave(run);
            Assert.That(ExpeditionAugmentRuntime.ProcessPandoraWorkshopWaveEntered(
                run,wave-1,wave),Is.Null);
            Assert.That(run.inventoryItemInstanceIds.Count,Is.EqualTo(wave-acquiredWave+1));
        }
    }

    [Test]
    public void ReserveReinforcementOffersThreeChoicesAndScalesWithAcquireWave()
    {
        AugmentMasterRow row=GameMasterData.Augment(
            ExpeditionAugmentRuntime.ReserveReinforcement);

        Assert.That(row.selectionMode,Is.EqualTo("Choice3Reinforcement"));
        Assert.That(row.NumericParam("candidate_count"),Is.EqualTo(3f));
        Assert.That(ExpeditionAugmentRuntime.ReserveReinforcementGrowth(1),
            Is.EqualTo(1));
        Assert.That(ExpeditionAugmentRuntime.ReserveReinforcementGrowth(5),
            Is.EqualTo(5));
        Assert.That(ExpeditionAugmentRuntime.ReserveReinforcementGrowth(9),
            Is.EqualTo(9));
    }

    [Test]
    public void ReserveReinforcementRemainsPartOfTheRunOnlyRoster()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        run.monsters.Add(new ExpeditionMonsterStateSave
        {
            instanceId="augment_reserve_test",temporary=true,
            sourceAugmentId=ExpeditionAugmentRuntime.ReserveReinforcement
        });

        Assert.That(ExpeditionRunFactory.IsTemporaryRosterMember(
            run,"augment_reserve_test"),Is.True);
        Assert.That(ExpeditionRunFactory.IsTemporaryRosterMember(
            run,"missing"),Is.False);
    }

    [Test]
    public void FickleBlessingShufflesAllThreeModesAndRepeatsEveryThreeWaves()
    {
        HashSet<string> orders=new HashSet<string>();
        for(int seed=0;seed<6;seed++)
        {
            ExpeditionAugmentSelectionSave selection=new ExpeditionAugmentSelectionSave
            {
                augmentId=ExpeditionAugmentRuntime.FickleBlessing,
                acquiredWave=1,deterministicSeed=seed
            };
            int[] cycle=Enumerable.Range(1,3).Select(wave=>
                ExpeditionAugmentRuntime.BlessingMode(selection,wave)).ToArray();
            Assert.That(cycle,Is.EquivalentTo(new[]{0,1,2}));
            Assert.That(ExpeditionAugmentRuntime.BlessingMode(selection,4),
                Is.EqualTo(cycle[0]));
            orders.Add(string.Join(",",cycle));
        }
        Assert.That(orders,Has.Count.EqualTo(6));
    }

    [Test]
    public void FickleGrowthBlessingAddsOneToEveryGrowthOffer()
    {
        AugmentMasterRow row=GameMasterData.Augment(
            ExpeditionAugmentRuntime.FickleBlessing);
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="fickle_growth",wave=5
        };
        const string monsterInstanceId="fickle_monster";
        int normal=ExpeditionGrowthSystem.OfferAmount(run,monsterInstanceId);
        ExpeditionAugmentSelectionSave selection=new ExpeditionAugmentSelectionSave
        {
            augmentId=ExpeditionAugmentRuntime.FickleBlessing,acquiredWave=5,
            deterministicSeed=Enumerable.Range(0,6).First(seed=>
                ExpeditionAugmentRuntime.BlessingMode(
                    new ExpeditionAugmentSelectionSave
                    {acquiredWave=5,deterministicSeed=seed},5)==2)
        };
        run.augments.Add(selection);

        Assert.That(row.NumericParam("blue_growth_bonus"),Is.EqualTo(1f));
        Assert.That(ExpeditionGrowthSystem.OfferAmount(run,monsterInstanceId),
            Is.EqualTo(normal+1));

        selection.deterministicSeed=Enumerable.Range(0,6).First(seed=>
            ExpeditionAugmentRuntime.BlessingMode(
                new ExpeditionAugmentSelectionSave
                {acquiredWave=5,deterministicSeed=seed},5)==0);
        ExpeditionAugmentCombatModifiers modifiers=
            ExpeditionAugmentRuntime.CombatModifiers(run,monsterInstanceId,5);
        Assert.That(modifiers.attackDamageBonus,Is.EqualTo(0.20f).Within(0.0001f));
        Assert.That(modifiers.defenseBonus,Is.EqualTo(10f).Within(0.0001f));
        Assert.That(modifiers.statusResistanceBonus,Is.EqualTo(10f).Within(0.0001f));
    }

    [Test]
    public void EveryEnabledItemTransformHasProbabilitiesAndAChangedOverchargeResult()
    {
        foreach(ItemTransformMasterRow transform in GameMasterData.Database.itemTransforms
            .Where(row=>row!=null&&row.enabled))
        {
            ItemMasterRow normal=ExpeditionItemTransformSystem.ResolveEffectiveMaster(
                new ExpeditionItemInstanceSave{itemId=transform.itemId});
            ItemMasterRow overcharged=ExpeditionItemTransformSystem.ResolveEffectiveMaster(
                new ExpeditionItemInstanceSave{
                    itemId=transform.itemId,
                    transformState=ExpeditionItemTransformState.Overcharged});

            Assert.That(transform.greatSuccessPercent+transform.successPercent+
                        transform.noChangePercent+transform.failurePercent,
                Is.EqualTo(100),transform.itemId);
            Assert.That(transform.greatSuccessPercent,Is.EqualTo(15),transform.itemId);
            Assert.That(transform.successPercent,Is.EqualTo(70),transform.itemId);
            Assert.That(transform.noChangePercent,Is.Zero,transform.itemId);
            Assert.That(transform.failurePercent,Is.EqualTo(15),transform.itemId);
            Assert.That(transform.corruptionGreatEffectId,Is.Not.Empty,transform.itemId);
            Assert.That(ItemNumericSignature(overcharged),
                Is.Not.EqualTo(ItemNumericSignature(normal)),
                transform.itemId+" overcharge did not change any numeric value.");
        }
    }

    [Test]
    public void EveryEnabledAugmentCanInitializeItsRuntimeState()
    {
        foreach(AugmentMasterRow augment in GameMasterData.Augments)
        {
            ExpeditionRunSave run=new ExpeditionRunSave{
                runId="augment_test",wave=1,lives=3,maximumLives=3,
                maxDeploymentCount=3,expeditionCurrency=50};
            run.monsters.Add(new ExpeditionMonsterStateSave{
                instanceId="monster",rank=ExpeditionMonsterRank.Rank3});
            run.itemInstances.Add(new ExpeditionItemInstanceSave{
                instanceId="item",itemId="complete_giant_heart"});
            ExpeditionAugmentSelectionSave selection=new ExpeditionAugmentSelectionSave{
                augmentId=augment.id,
                acquiredWave=1,
                targetMonsterInstanceId="monster",
                deterministicSeed=17};

            Assert.DoesNotThrow(()=>ExpeditionAugmentRuntime.ApplyOnAcquire(
                run,selection,new Random(17)),augment.id);
            Assert.That(ExpeditionAugmentRuntime.Supports(augment.id),Is.True,augment.id);
        }
    }

    [TestCase(ExpeditionAugmentRuntime.TwinCommand)]
    public void CurrentWavePreparationAugmentsRemainPendingAfterAcquisition(
        string augmentId)
    {
        ExpeditionRunSave run=new ExpeditionRunSave{
            runId="current_wave_augment",wave=6,lives=3,maximumLives=3};
        ExpeditionAugmentSelectionSave selection=new ExpeditionAugmentSelectionSave{
            augmentId=augmentId,acquiredWave=6,deterministicSeed=61};

        ExpeditionAugmentRuntime.ApplyOnAcquire(run,selection,new Random(61));

        Assert.That(selection.runtimeWave,Is.Zero,
            "The scene preparation pass must initialize this augment on its acquired Wave.");
    }

    [Test]
    public void TwinCommandUsesDatabaseChargeAndRequiresTwoDeployableMonsters()
    {
        AugmentMasterRow row=GameMasterData.Augment(ExpeditionAugmentRuntime.TwinCommand);
        Assert.That(row.NumericParam("initial_skill_charge"),Is.EqualTo(0.30f));
        Assert.That(row.NumericParam("skill_resonance_charge"),Is.EqualTo(0.35f));
        Assert.That(ExpeditionAugmentRuntime.ReduceRemainingCooldown(2f,0.30f),
            Is.EqualTo(1.4f).Within(0.0001f));
        Assert.That(ExpeditionAugmentRuntime.ChargeSkillCooldown(8f,10f,0.35f),
            Is.EqualTo(4.5f).Within(0.0001f));

        ExpeditionRunSave run=new ExpeditionRunSave{
            wave=1,augmentDraftWave=1,maxDeploymentCount=2};
        run.augmentDraftIds.Add(ExpeditionAugmentRuntime.TwinCommand);
        run.monsters.Add(new ExpeditionMonsterStateSave{instanceId="first"});
        Assert.That(ExpeditionAugmentSystem.TrySelect(run,
            ExpeditionAugmentRuntime.TwinCommand,"","",17,out _),Is.False);
        run.monsters.Add(new ExpeditionMonsterStateSave{instanceId="second"});
        run.maxDeploymentCount=1;
        Assert.That(ExpeditionAugmentSystem.TrySelect(run,
            ExpeditionAugmentRuntime.TwinCommand,"","",17,out _),Is.False);
        run.maxDeploymentCount=2;
        Assert.That(ExpeditionAugmentSystem.TrySelect(run,
            ExpeditionAugmentRuntime.TwinCommand,"","",17,out _),Is.True);
    }

    [Test]
    public void CombinedCastUsesDatabaseCooldownAndRequiresTwoMonsters()
    {
        AugmentMasterRow row=GameMasterData.Augment(ExpeditionAugmentRuntime.CombinedCast);
        Assert.That(row.effectSummary,Is.EqualTo(
            "지휘관만 스킬 사용. 시전 시 아군 전원 스킬 발동, 참여당 쿨타임 +0.5초."));
        Assert.That(row.NumericParam("extra_cooldown_per_skill"),Is.EqualTo(0.5f));
        Assert.That(row.NumericParam("max_cooldown"),Is.EqualTo(18f));
        Assert.That(ExpeditionAugmentRuntime.AddCombinedCastCooldown(8f,3,0.5f,18f),
            Is.EqualTo(9.5f).Within(0.0001f));
        Assert.That(ExpeditionAugmentRuntime.AddCombinedCastCooldown(17.5f,3,0.5f,18f),
            Is.EqualTo(18f).Within(0.0001f));

        ExpeditionRunSave run=new ExpeditionRunSave{
            wave=1,augmentDraftWave=1,maxDeploymentCount=2};
        run.augmentDraftIds.Add(ExpeditionAugmentRuntime.CombinedCast);
        run.monsters.Add(new ExpeditionMonsterStateSave{instanceId="first"});
        Assert.That(ExpeditionAugmentSystem.TrySelect(run,
            ExpeditionAugmentRuntime.CombinedCast,"","",17,out _),Is.False);
        run.monsters.Add(new ExpeditionMonsterStateSave{instanceId="second"});
        Assert.That(ExpeditionAugmentSystem.TrySelect(run,
            ExpeditionAugmentRuntime.CombinedCast,"","",17,out _),Is.True);
    }

    [Test]
    public void ShadowCloneUsesApprovedPureGrowthRatiosAndFallbackRule()
    {
        AugmentMasterRow row=GameMasterData.Augment(
            ExpeditionAugmentRuntime.ShadowClone);

        Assert.That(row.effectSummary,Is.EqualTo(
            "지정 아군의 기본공격을 복제한 50% 능력치의 그림자 분신을 매 전투 소환합니다."));
        Assert.That(row.triggerRule,Does.Contain("가장 가까운 아군 진영 빈 타일"));
        Assert.That(row.NumericParam("health_attack_def_res_ratio"),Is.EqualTo(0.50f));
        Assert.That(row.NumericParam("attack_speed_move_range_ratio"),Is.EqualTo(1f));
        Assert.That(row.NumericParam("model_scale"),Is.EqualTo(0.85f));
        Assert.That(row.NumericParam("deployment_cost"),Is.Zero);
        Assert.That(row.NumericParam("support_target",1f),Is.Zero);
        Assert.That(row.NumericParam("death_trigger",1f),Is.Zero);
        Assert.That(row.dataVersion,Is.EqualTo(3));
    }

    [Test]
    public void LegacyInheritanceUsesEveryCompletedItemEffect()
    {
        AugmentMasterRow row=GameMasterData.Augment(
            ExpeditionAugmentRuntime.LegacyInheritance);
        Assert.That(row.effectSummary,Is.EqualTo(
            "Wave당 1회, 사망 아군의 완성 아이템 효과를 가장 가까운 아군이 계승합니다."));
        Assert.That(row.NumericParam("virtual_slot_limit"),Is.EqualTo(3f));
        Assert.That(row.NumericParam("include_transformed_effects"),Is.EqualTo(1f));
        Assert.That(row.NumericParam("exclude_summons"),Is.EqualTo(1f));

        ExpeditionRunSave run=new ExpeditionRunSave();
        ExpeditionMonsterStateSave inheritedState=new ExpeditionMonsterStateSave();
        run.monsters.Add(inheritedState);
        ExpeditionItemInstanceSave health=ExpeditionItemSystem.CreateItem(
            run,"complete_giant_heart",false);
        ExpeditionItemInstanceSave critical=ExpeditionItemSystem.CreateItem(
            run,"complete_executioner_eye",false);
        ExpeditionItemInstanceSave spell=ExpeditionItemSystem.CreateItem(
            run,"complete_archmage_clock",false);
        spell.transformState=ExpeditionItemTransformState.CorruptedGreatSuccess;
        inheritedState.itemInstanceIds.AddRange(new[]
            {health.instanceId,critical.instanceId,spell.instanceId});

        CombatItemModifiers modifiers=ExpeditionItemSystem.Aggregate(
            run,inheritedState);
        Assert.That(modifiers.maxHealthBonus,Is.GreaterThan(0f));
        Assert.That(modifiers.criticalChance,Is.GreaterThan(0f));
        Assert.That(modifiers.criticalDamageBonus,Is.GreaterThan(0f));
        Assert.That(modifiers.skillPowerBonus,Is.GreaterThan(0f));
        Assert.That(modifiers.skillCooldownReductionBonus,Is.GreaterThan(0f));
        Assert.That(modifiers.specialEffects.Any(item=>
            item.specialEffect=="TimedSkillPowerRamp"),Is.True);
        Assert.That(modifiers.transformedEffects.Any(transform=>
            transform.itemId=="complete_archmage_clock"),Is.True);
        Assert.That(row.dataVersion,Is.EqualTo(2));
    }

    private static string ItemNumericSignature(ItemMasterRow item)
    {
        return string.Join("|",new[]
        {
            item.attackDamageBonus,item.attackSpeedBonus,item.maxHealthBonus,
            item.damageReductionBonus,item.criticalChance,item.criticalDamageBonus,
            item.skillPowerBonus,item.defenseBonus,item.statusResistanceBonus,
            item.skillCooldownReductionBonus,item.effectParam1,item.effectParam2,
            item.effectParam3
        }.Select(value=>value.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
    }
}
