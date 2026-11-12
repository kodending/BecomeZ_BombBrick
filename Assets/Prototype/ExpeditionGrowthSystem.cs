using System;
using System.Collections.Generic;
using System.Linq;

public struct ExpeditionGrowthBonuses
{
    public int maxHealth;
    public int attackDamage;
    public int attackSpeedScore;
    public int defense;
    public int statusResistance;
}

public static class ExpeditionGrowthSystem
{
    public const int MaximumGrowth=12;
    public const int ShopCandidateCount=3;
    public static int RerollCost=>Math.Max(0,GameMasterData.ExpeditionInt("growth_shop_reroll_cost",2));

    public static void PrepareWave(ExpeditionRunSave run)
    {
        if(run==null)throw new ArgumentNullException(nameof(run));
        if(run.growthShopPurchasedMonsterIds==null)
            run.growthShopPurchasedMonsterIds=new List<string>();
        if(run.growthShopCandidateMonsterIds==null)
            run.growthShopCandidateMonsterIds=new List<string>();
        if(run.growthShopWave==run.wave)return;
        run.growthShopWave=run.wave;
        run.growthShopFreePurchaseUsed=false;
        run.growthShopRerollCount=0;
        run.growthShopCandidateMonsterIds.Clear();
        run.growthShopPurchasedMonsterIds.Clear();
    }

    public static int OfferAmount(ExpeditionRunSave run,string monsterInstanceId)
    {
        if(run==null||string.IsNullOrWhiteSpace(monsterInstanceId))return 1;
        int seed=MonsterInstanceGenerator.StableSeed(
            run.runId+"_growth_shop_"+run.wave+"_"+monsterInstanceId);
        int roll=(seed&int.MaxValue)%100;
        int plusThreeChance=Math.Min(10,5+run.wave/3);
        int plusTwoChance=Math.Min(35,20+run.wave);
        int amount=roll<plusThreeChance?3:
            roll<plusThreeChance+plusTwoChance?2:1;
        return amount+ExpeditionAugmentRuntime.BlessingGrowthBonus(run,run.wave);
    }

    public static List<ExpeditionMonsterStateSave> ShopCandidates(
        ExpeditionRunSave run)
    {
        if(run==null)return new List<ExpeditionMonsterStateSave>();
        PrepareWave(run);
        if(run.growthShopCandidateMonsterIds.Count==0)
            run.growthShopCandidateMonsterIds.AddRange(
                RollCandidateIds(run,null,run.growthShopRerollCount));
        return run.growthShopCandidateMonsterIds.Select(id=>
                run.monsters.FirstOrDefault(value=>value!=null&&
                    value.instanceId==id))
            .Where(value=>value!=null&&
                !ExpeditionAugmentRuntime.BlocksGrowthShop(
                    run,value.instanceId,run.wave)).ToList();
    }

    public static bool TryReroll(ExpeditionRunSave run,out string error)
    {
        return TryReroll(run,null,out error);
    }

    public static bool TryReroll(
        ExpeditionRunSave run,IEnumerable<string> preservedMonsterIds,
        out string error)
    {
        return TryReroll(run,preservedMonsterIds,null,out error);
    }

    public static int RerollCostFor(
        ExpeditionRunSave run,PlayerProfileSave profile)
    {
        int discount=run!=null&&run.growthShopRerollCount==0
            ?PermanentResearchSystem.FirstPaidShopRerollDiscount(profile):0;
        return Math.Max(0,RerollCost-discount);
    }

    public static bool TryReroll(
        ExpeditionRunSave run,IEnumerable<string> preservedMonsterIds,
        PlayerProfileSave profile,out string error)
    {
        error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        PrepareWave(run);
        List<string> previous=ShopCandidates(run)
            .Select(value=>value.instanceId).ToList();
        HashSet<string> preserved=new HashSet<string>(
            (preservedMonsterIds??Enumerable.Empty<string>())
                .Where(previous.Contains),StringComparer.Ordinal);
        if(previous.Count>0&&preserved.Count==previous.Count)
        {error="선택하지 않은 성장 후보가 없습니다.";return false;}
        int rerollCost=RerollCostFor(run,profile);
        if(run.expeditionCurrency<rerollCost)
        {error="리롤에 필요한 골드가 부족합니다.";return false;}
        List<string> replacements=RollCandidateIds(
            run,previous,run.growthShopRerollCount+1,
            previous.Count-preserved.Count,preserved);
        if(replacements.Count<previous.Count-preserved.Count)
        {error="새로 등장할 성장 후보가 없습니다.";return false;}
        int replacementIndex=0;
        List<string> next=previous.Select(id=>preserved.Contains(id)
            ?id:replacements[replacementIndex++]).ToList();
        bool refreshesPurchased=next.Any(
            run.growthShopPurchasedMonsterIds.Contains);
        if(next.Count==0||next.SequenceEqual(previous)&&!refreshesPurchased)
        {error="새로 등장할 성장 후보가 없습니다.";return false;}

        run.expeditionCurrency-=rerollCost;
        run.growthShopRerollCount++;
        run.growthShopPurchasedMonsterIds.RemoveAll(next.Contains);
        run.growthShopCandidateMonsterIds.Clear();
        run.growthShopCandidateMonsterIds.AddRange(next);
        return true;
    }

    private static List<string> RollCandidateIds(
        ExpeditionRunSave run,ICollection<string> previous,int roll,
        int count=ShopCandidateCount,ICollection<string> excluded=null)
    {
        HashSet<string> previousIds=previous==null
            ?new HashSet<string>(StringComparer.Ordinal)
            :new HashSet<string>(previous,StringComparer.Ordinal);
        HashSet<string> excludedIds=excluded==null
            ?new HashSet<string>(StringComparer.Ordinal)
            :new HashSet<string>(excluded,StringComparer.Ordinal);
        return run.monsters.Where(value=>value!=null&&
                value.growthTotal<MaximumGrowth&&
                !ExpeditionAugmentRuntime.BlocksGrowthShop(
                    run,value.instanceId,run.wave)&&
                !excludedIds.Contains(value.instanceId))
            .OrderBy(value=>previousIds.Contains(value.instanceId)?1:0)
            .ThenBy(value=>MonsterInstanceGenerator.StableSeed(
                run.runId+"_growth_shop_candidates_"+run.wave+"_"+roll+"_"+
                value.instanceId))
            .Take(Math.Max(0,count)).Select(value=>value.instanceId).ToList();
    }

    public static int PurchaseCost(int amount)
    {
        if(amount>=3)return Math.Max(0,
            GameMasterData.ExpeditionInt("growth_shop_plus3_cost",7));
        if(amount==2)return Math.Max(0,
            GameMasterData.ExpeditionInt("growth_shop_plus2_cost",5));
        return Math.Max(0,
            GameMasterData.ExpeditionInt("growth_shop_plus1_cost",3));
    }

    public static int SelectionCost(
        ExpeditionRunSave run,IEnumerable<string> monsterInstanceIds)
    {
        if(run==null||monsterInstanceIds==null)return 0;
        bool freeAvailable=!run.growthShopFreePurchaseUsed;
        int total=0;
        HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(string id in monsterInstanceIds)
        {
            if(string.IsNullOrWhiteSpace(id)||!seen.Add(id))continue;
            ExpeditionMonsterStateSave monster=run.monsters.FirstOrDefault(value=>
                value!=null&&value.instanceId==id);
            if(monster==null||monster.growthTotal>=MaximumGrowth||
               ExpeditionAugmentRuntime.BlocksGrowthShop(
                   run,monster.instanceId,run.wave)||
               run.growthShopPurchasedMonsterIds.Contains(id))continue;
            int amount=Math.Min(OfferAmount(run,id),
                MaximumGrowth-monster.growthTotal);
            if(freeAvailable)freeAvailable=false;
            else total+=PurchaseCost(amount);
        }
        return total;
    }

    public static bool TryPurchase(
        ExpeditionRunSave run,string monsterInstanceId,
        out int appliedGrowth,out ExpeditionMonsterRank previousRank,
        out string error)
    {
        appliedGrowth=0;
        previousRank=ExpeditionMonsterRank.Rank1;
        error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        PrepareWave(run);
        ExpeditionMonsterStateSave monster=run.monsters.FirstOrDefault(value=>
            value!=null&&value.instanceId==monsterInstanceId);
        if(monster==null){error="성장시킬 몬스터를 찾을 수 없습니다.";return false;}
        previousRank=monster.rank;
        if(monster.growthTotal>=MaximumGrowth)
        {error="이미 최대 성장에 도달했습니다.";return false;}
        if(ExpeditionAugmentRuntime.BlocksGrowthShop(
               run,monster.instanceId,run.wave))
        {error="조기 각성으로 성장 상점 출현이 제한된 몬스터입니다.";return false;}
        if(run.growthShopPurchasedMonsterIds.Contains(monsterInstanceId))
        {error="현재 카드에서 이미 성장했습니다.";return false;}

        appliedGrowth=Math.Min(
            OfferAmount(run,monsterInstanceId),MaximumGrowth-monster.growthTotal);
        int cost=run.growthShopFreePurchaseUsed?PurchaseCost(appliedGrowth):0;
        if(run.expeditionCurrency<cost)
        {error="골드가 부족합니다.";appliedGrowth=0;return false;}

        run.expeditionCurrency-=cost;
        run.growthShopFreePurchaseUsed=true;
        run.growthShopPurchasedMonsterIds.Add(monsterInstanceId);
        monster.growthTotal+=appliedGrowth;
        monster.rank=RankForGrowth(monster.monsterId,monster.growthTotal);
        ExpeditionAugmentRuntime.TryActivateTranscendence(
            run,monsterInstanceId);
        return true;
    }

    public static bool CanCompleteShop(ExpeditionRunSave run)
    {
        return run!=null&&(run.growthShopFreePurchaseUsed||
            run.monsters.All(value=>value==null||value.growthTotal>=MaximumGrowth||
                ExpeditionAugmentRuntime.BlocksGrowthShop(
                    run,value.instanceId,run.wave)));
    }

    public static ExpeditionMonsterRank RankForGrowth(
        string monsterId,int growthTotal)
    {
        MonsterGrowthMasterRow row=GameMasterData.MonsterGrowth(monsterId);
        int rank2=row==null?3:row.rank2RequiredTotalGrowth;
        int rank3=row==null?9:row.rank3RequiredTotalGrowth;
        if(growthTotal>=rank3)return ExpeditionMonsterRank.Rank3;
        if(growthTotal>=rank2)return ExpeditionMonsterRank.Rank2;
        return ExpeditionMonsterRank.Rank1;
    }

    public static ExpeditionGrowthBonuses Bonuses(
        ExpeditionMonsterStateSave state,OwnedMonsterSave owned,
        ExpeditionMonsterRank effectiveRank,int growthTotal=-1)
    {
        if(state==null||owned==null)return new ExpeditionGrowthBonuses();
        MonsterGrowthMasterRow row=GameMasterData.MonsterGrowth(state.monsterId);
        if(row==null)return new ExpeditionGrowthBonuses();
        int total=growthTotal<0?state.growthTotal:growthTotal;
        string grade=owned.isShiny
            ?"SHINY":MonsterStatQuality.OverallGrade(owned);
        int budget=GameMasterData.GrowthTotalValue(
            grade,owned.isShiny?110:FallbackBudget(grade));
        return CalculateBonuses(row,budget,total,effectiveRank);
    }

    public static ExpeditionGrowthBonuses CalculateBonuses(
        MonsterGrowthMasterRow row,int growthBudget,int growthTotal,
        ExpeditionMonsterRank effectiveRank)
    {
        if(row==null||growthTotal<=0||growthBudget<=0)
            return new ExpeditionGrowthBonuses();
        int effective=Math.Max(0,growthTotal);
        if(effectiveRank>=ExpeditionMonsterRank.Rank2)
            effective+=Math.Max(0,row.rank2BonusGrowthCount);
        if(effectiveRank>=ExpeditionMonsterRank.Rank3)
            effective+=Math.Max(0,row.rank3BonusGrowthCount);
        int total=Math.Max(1,row.weightTotal);
        return new ExpeditionGrowthBonuses
        {
            maxHealth=StatGain(effective,growthBudget,row.hpWeight,total,
                row.hpPointsPerStat),
            attackDamage=StatGain(effective,growthBudget,row.attackWeight,total,
                row.attackPointsPerStat),
            attackSpeedScore=StatGain(effective,growthBudget,row.attackSpeedWeight,
                total,row.attackSpeedPointsPerStat),
            defense=StatGain(effective,growthBudget,row.defenseWeight,total,
                row.defensePointsPerStat),
            statusResistance=StatGain(effective,growthBudget,
                row.statusResistanceWeight,total,row.statusResistancePointsPerStat)
        };
    }

    private static int StatGain(
        int effectiveGrowth,int budget,int weight,int weightTotal,int pointsPerStat)
    {
        if(weight<=0||pointsPerStat<=0)return 0;
        long numerator=(long)effectiveGrowth*budget*weight;
        long denominator=(long)weightTotal*pointsPerStat;
        return (int)(numerator/Math.Max(1L,denominator));
    }

    private static int FallbackBudget(string grade)
    {
        switch(grade)
        {
            case "S+": return 102;
            case "S": return 100;
            case "S-": return 98;
            case "A+": return 96;
            case "A": return 94;
            case "A-": return 92;
            case "B+": return 90;
            case "B": return 88;
            case "B-": return 86;
            case "C+": return 84;
            case "C": return 82;
            case "C-": return 80;
            case "D+": return 78;
            case "D": return 76;
            default: return 74;
        }
    }
}
