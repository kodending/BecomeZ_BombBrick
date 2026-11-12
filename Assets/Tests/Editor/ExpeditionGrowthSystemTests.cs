using System.Linq;
using NUnit.Framework;

public sealed class ExpeditionGrowthSystemTests
{
    [Test]
    public void ShopOffersThreeStableRandomCandidatesForTwoGoldRerolls()
    {
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="candidate_test",wave=2,expeditionCurrency=10
        };
        for(int index=0;index<8;index++)
            run.monsters.Add(new ExpeditionMonsterStateSave
            {
                instanceId="monster_"+index,monsterId="missing",
                growthTotal=index==0?ExpeditionGrowthSystem.MaximumGrowth:0
            });

        string[] first=ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId).ToArray();
        string[] second=ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId).ToArray();

        Assert.That(first,Has.Length.EqualTo(3));
        Assert.That(first,Is.EqualTo(second));
        Assert.That(first,Does.Not.Contain("monster_0"));
        run.expeditionCurrency=1;
        Assert.That(ExpeditionGrowthSystem.TryReroll(run,out _),Is.False);
        Assert.That(run.expeditionCurrency,Is.EqualTo(1));
        Assert.That(run.growthShopRerollCount,Is.Zero);
        Assert.That(ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId),Is.EqualTo(first));
        run.expeditionCurrency=10;
        Assert.That(ExpeditionGrowthSystem.TryReroll(
            run,out string error),Is.True,error);
        string[] rerolled=ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId).ToArray();
        Assert.That(rerolled,Has.Length.EqualTo(3));
        Assert.That(rerolled,Is.Not.EqualTo(first));
        Assert.That(run.expeditionCurrency,Is.EqualTo(8));
        Assert.That(run.growthShopRerollCount,Is.EqualTo(1));
    }

    [Test]
    public void RerollKeepsSelectedCandidateAndReplacesTheRest()
    {
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="candidate_pin_test",wave=2,expeditionCurrency=10
        };
        for(int index=0;index<8;index++)
            run.monsters.Add(new ExpeditionMonsterStateSave
            {
                instanceId="monster_"+index,monsterId="missing"
            });
        string[] before=ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId).ToArray();
        string selected=before[1];

        Assert.That(ExpeditionGrowthSystem.TryReroll(
            run,new[]{selected},out string error),Is.True,error);
        string[] after=ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId).ToArray();

        Assert.That(after[1],Is.EqualTo(selected));
        Assert.That(after.Where((id,index)=>index!=1),
            Is.Not.EquivalentTo(before.Where((id,index)=>index!=1)));
        Assert.That(run.expeditionCurrency,Is.EqualTo(8));
    }

    [Test]
    public void GrowthThresholdsAutomaticallyGrantStars()
    {
        Assert.That(ExpeditionGrowthSystem.MaximumGrowth,Is.EqualTo(12));
        Assert.That(ExpeditionGrowthSystem.RankForGrowth("missing",2),
            Is.EqualTo(ExpeditionMonsterRank.Rank1));
        Assert.That(ExpeditionGrowthSystem.RankForGrowth("missing",3),
            Is.EqualTo(ExpeditionMonsterRank.Rank2));
        Assert.That(ExpeditionGrowthSystem.RankForGrowth("missing",9),
            Is.EqualTo(ExpeditionMonsterRank.Rank3));
        Assert.That(ExpeditionGrowthSystem.RankForGrowth("missing",12),
            Is.EqualTo(ExpeditionMonsterRank.Rank3));
    }

    [TestCase(1,3,ExpeditionMonsterRank.Rank2)]
    [TestCase(5,9,ExpeditionMonsterRank.Rank3)]
    public void EarlyAwakeningPermanentlyReachesTheNextRank(
        int initialGrowth,int expectedGrowth,ExpeditionMonsterRank expectedRank)
    {
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="early_awakening",wave=1,augmentDraftWave=1
        };
        run.augmentDraftIds.Add(ExpeditionAugmentRuntime.EarlyAwakening);
        run.monsters.Add(new ExpeditionMonsterStateSave
        {
            instanceId="target",monsterId="missing",growthTotal=initialGrowth,
            rank=ExpeditionGrowthSystem.RankForGrowth("missing",initialGrowth)
        });

        Assert.That(ExpeditionAugmentSystem.TrySelect(
            run,ExpeditionAugmentRuntime.EarlyAwakening,"target","",17,
            out string error),Is.True,error);
        Assert.That(run.monsters[0].growthTotal,Is.EqualTo(expectedGrowth));
        Assert.That(run.monsters[0].rank,Is.EqualTo(expectedRank));
    }

    [Test]
    public void EarlyAwakenedMonsterSkipsTheNextThreeGrowthShops()
    {
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="early_lock",wave=1,augmentDraftWave=1
        };
        run.augmentDraftIds.Add(ExpeditionAugmentRuntime.EarlyAwakening);
        run.monsters.Add(new ExpeditionMonsterStateSave
        {
            instanceId="target",monsterId="missing",growthTotal=1
        });
        Assert.That(ExpeditionAugmentSystem.TrySelect(
            run,ExpeditionAugmentRuntime.EarlyAwakening,"target","",23,
            out string error),Is.True,error);

        for(int wave=2;wave<=4;wave++)
        {
            run.wave=wave;
            Assert.That(ExpeditionGrowthSystem.ShopCandidates(run),Is.Empty,
                "Wave "+wave);
            Assert.That(ExpeditionGrowthSystem.CanCompleteShop(run),Is.True,
                "Wave "+wave);
        }

        run.wave=5;
        Assert.That(ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId),Does.Contain("target"));
    }

    [Test]
    public void TranscendenceAutomaticallyActivatesAtThreeStarGrowthTwelve()
    {
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="transcendence_growth",wave=1,augmentDraftWave=1,
            expeditionCurrency=0
        };
        run.augmentDraftIds.Add(ExpeditionAugmentRuntime.TranscendenceGene);
        ExpeditionMonsterStateSave target=new ExpeditionMonsterStateSave
        {
            instanceId="target",monsterId="missing",growthTotal=11,
            rank=ExpeditionMonsterRank.Rank3
        };
        run.monsters.Add(target);

        Assert.That(ExpeditionAugmentSystem.TrySelect(
            run,ExpeditionAugmentRuntime.TranscendenceGene,"target","",31,
            out string error),Is.True,error);
        Assert.That(target.transcended,Is.False);

        target.growthTotal=12;
        Assert.That(ExpeditionAugmentRuntime.TryActivateTranscendence(
            run,target.instanceId),Is.True);
        Assert.That(target.transcended,Is.True);
        Assert.That(run.expeditionCurrency,Is.Zero);
        ExpeditionAugmentCombatModifiers modifiers=
            ExpeditionAugmentRuntime.CombatModifiers(run,target.instanceId,1);
        Assert.That(modifiers.maxHealthBonus,Is.EqualTo(0.35f).Within(0.001f));
        Assert.That(modifiers.attackDamageBonus,Is.EqualTo(0.30f).Within(0.001f));
        Assert.That(modifiers.defenseMultiplier,Is.EqualTo(1.20f).Within(0.001f));
        Assert.That(modifiers.statusResistanceMultiplier,
            Is.EqualTo(1.20f).Within(0.001f));
        Assert.That(modifiers.harmfulDurationMultiplier,
            Is.EqualTo(0.80f).Within(0.001f));
    }

    [Test]
    public void TranscendenceRejectsTemporarySupport()
    {
        ExpeditionRunSave run=new ExpeditionRunSave();
        run.monsters.Add(new ExpeditionMonsterStateSave
        {
            instanceId="temporary",temporary=true,growthTotal=12,
            rank=ExpeditionMonsterRank.Rank3
        });

        Assert.That(ExpeditionAugmentSystem.CanTargetMonster(
            run,ExpeditionAugmentRuntime.TranscendenceGene,"temporary"),Is.False);
    }

    [Test]
    public void FirstPurchaseEachWaveIsFreeAndEachOfferCanBeBoughtOnce()
    {
        Assert.That(new[]{1,2,3}.Select(ExpeditionGrowthSystem.PurchaseCost),
            Is.EqualTo(new[]{3,5,7}));
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="growth_test",wave=1,expeditionCurrency=100
        };
        run.monsters.Add(new ExpeditionMonsterStateSave
        {
            instanceId="first",monsterId="missing"
        });
        run.monsters.Add(new ExpeditionMonsterStateSave
        {
            instanceId="second",monsterId="missing"
        });

        int initialGold=run.expeditionCurrency;
        Assert.That(ExpeditionGrowthSystem.SelectionCost(
            run,new[]{"first","second"}),
            Is.EqualTo(ExpeditionGrowthSystem.PurchaseCost(
                ExpeditionGrowthSystem.OfferAmount(run,"second"))));
        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,"first",out int firstAmount,out _,out string firstError),Is.True,
            firstError);
        Assert.That(firstAmount,Is.InRange(1,3));
        Assert.That(run.expeditionCurrency,Is.EqualTo(initialGold));
        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,"first",out _,out _,out _),Is.False);

        int secondOffer=ExpeditionGrowthSystem.OfferAmount(run,"second");
        int secondCost=ExpeditionGrowthSystem.PurchaseCost(secondOffer);
        run.expeditionCurrency=secondCost-1;
        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,"second",out _,out _,out _),Is.False);
        Assert.That(run.monsters[1].growthTotal,Is.Zero);
        Assert.That(run.growthShopPurchasedMonsterIds,Does.Not.Contain("second"));
        Assert.That(run.expeditionCurrency,Is.EqualTo(secondCost-1));
        run.expeditionCurrency=initialGold;
        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,"second",out int secondAmount,out _,out string secondError),Is.True,
            secondError);
        Assert.That(secondAmount,Is.EqualTo(secondOffer));
        Assert.That(run.expeditionCurrency,
            Is.EqualTo(initialGold-ExpeditionGrowthSystem.PurchaseCost(secondOffer)));

        run.wave=2;
        run.expeditionCurrency=0;
        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,"first",out _,out _,out string nextWaveError),Is.True,nextWaveError);
        Assert.That(run.expeditionCurrency,Is.Zero);
    }

    [Test]
    public void PurchasedMonsterCanReturnAndGrowAgainAfterReroll()
    {
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="growth_return_test",wave=1,expeditionCurrency=100
        };
        for(int index=0;index<3;index++)
            run.monsters.Add(new ExpeditionMonsterStateSave
            {
                instanceId="monster_"+index,monsterId="missing"
            });
        string target=ExpeditionGrowthSystem.ShopCandidates(run)[0].instanceId;

        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,target,out int firstGrowth,out _,out string purchaseError),
            Is.True,purchaseError);
        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,target,out _,out _,out _),Is.False);
        Assert.That(ExpeditionGrowthSystem.TryReroll(
            run,out string rerollError),Is.True,rerollError);
        Assert.That(ExpeditionGrowthSystem.ShopCandidates(run)
            .Select(value=>value.instanceId),Does.Contain(target));
        Assert.That(ExpeditionGrowthSystem.TryPurchase(
            run,target,out int secondGrowth,out _,out string secondError),
            Is.True,secondError);
        Assert.That(run.monsters.First(value=>value.instanceId==target).growthTotal,
            Is.EqualTo(firstGrowth+secondGrowth));
    }

    [Test]
    public void RankMilestonesAddBonusGrowthEventsWithoutFractions()
    {
        MonsterGrowthMasterRow row=new MonsterGrowthMasterRow
        {
            hpWeight=50,attackWeight=20,attackSpeedWeight=15,
            defenseWeight=7,statusResistanceWeight=8,weightTotal=100,
            rank2BonusGrowthCount=3,rank3BonusGrowthCount=6,
            hpPointsPerStat=10,attackPointsPerStat=20,
            attackSpeedPointsPerStat=10,defensePointsPerStat=20,
            statusResistancePointsPerStat=20
        };

        ExpeditionGrowthBonuses result=ExpeditionGrowthSystem.CalculateBonuses(
            row,100,9,ExpeditionMonsterRank.Rank3);

        Assert.That(result.maxHealth,Is.EqualTo(90));
        Assert.That(result.attackDamage,Is.EqualTo(18));
        Assert.That(result.attackSpeedScore,Is.EqualTo(27));
        Assert.That(result.defense,Is.EqualTo(6));
        Assert.That(result.statusResistance,Is.EqualTo(7));
    }

    [Test]
    public void ObservationPacingStrengthensHealthGrowthAndRetainsSpeciesWeights()
    {
        var rows=GameMasterData.Database.monsterGrowths;
        Assert.That(rows,Has.Count.EqualTo(80));
        foreach(var row in rows)
        {
            Assert.That(row.hpPointsPerStat,Is.EqualTo(5),row.monsterId);
            Assert.That(row.attackPointsPerStat,Is.EqualTo(20),row.monsterId);
            Assert.That(row.attackSpeedPointsPerStat,Is.EqualTo(20),row.monsterId);
            var bonus=ExpeditionGrowthSystem.CalculateBonuses(row,100,9,
                ExpeditionMonsterRank.Rank3);
            Assert.That(bonus.maxHealth,Is.EqualTo(18*row.hpWeight/5),row.monsterId);
            Assert.That(bonus.attackDamage,Is.EqualTo(18*row.attackWeight/20),row.monsterId);
            Assert.That(bonus.attackSpeedScore,Is.EqualTo(18*row.attackSpeedWeight/20),row.monsterId);
        }
    }
}
