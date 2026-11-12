using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class PendingWaveLoot
{
    public List<string> itemIds=new List<string>();
    public int expeditionCurrency;

    public bool IsEmpty
    {
        get{return itemIds.Count==0&&expeditionCurrency==0;}
    }

    public void Clear()
    {
        itemIds.Clear();
        expeditionCurrency=0;
    }

    public string Summary()
    {
        List<string> values=new List<string>();
        if(itemIds.Count>0)values.Add(GameLocalization.Format(
            "battle.loot_item_count","아이템 {0}개",itemIds.Count));
        if(expeditionCurrency>0)values.Add(GameLocalization.Format(
            "common.gold_amount","골드 {0}",expeditionCurrency));
        return values.Count==0?GameLocalization.Text("전리품 없음"):
            string.Join(" · ",values);
    }
}

public sealed class EnemyDropRoll
{
    public string rewardType;
    public string itemId;
    public int amount;
}

public sealed class EnemyLootProfile
{
    public ExpeditionMonsterRank rank=ExpeditionMonsterRank.Rank1;
    public List<ItemMasterRow> equippedItems=new List<ItemMasterRow>();
}

public struct ExpeditionBattleIncome
{
    public int baseIncome;
    public int interestIncome;
    public int streakIncome;
    public int Total=>baseIncome+interestIncome+streakIncome;
}

public static class ExpeditionDropSystem
{
    public const int MaximumCurrencyPerDrop=2;
    private static readonly int[] Rank3Counts=
        {0,0,0,0,0,0,0,0,1,2,3,3};
    private static readonly int[] Rank2Counts=
        {0,0,1,2,1,2,3,4,2,3,3,4};
    private static readonly int[] BaseItemCounts=
        {0,0,1,2,1,2,2,3,2,2,3,3};
    private static readonly int[] CompletedItemCounts=
        {0,0,0,0,0,0,0,0,0,1,2,2};

    public static void ApplyWaveClearGuarantees(
        ExpeditionRunSave run,PendingWaveLoot pending,System.Random random)
    {
        if(run==null)throw new ArgumentNullException("run");
        if(pending==null)throw new ArgumentNullException("pending");
        if(random==null)throw new ArgumentNullException("random");
        if(pending.itemIds.Count>0)
        {
            run.equipmentPityClears=0;
            return;
        }

        int threshold=Math.Max(
            0,GameMasterData.ExpeditionInt("equipment_pity_clear_waves",3));
        if(threshold==0)return;
        run.equipmentPityClears++;
        if(run.equipmentPityClears<threshold)return;
        List<ItemMasterRow> pool=GameMasterData.ItemsInPool("default_equipment");
        if(pool.Count==0)return;
        pending.itemIds.Add(pool[random.Next(pool.Count)].id);
        run.equipmentPityClears=0;
    }

    public static List<EnemyDropRoll> RollEnemyDrops(int wave,System.Random random)
    {
        return RollEnemyDrops(wave,ExpeditionMonsterRank.Rank1,
            new List<ItemMasterRow>(),random);
    }

    public static List<EnemyDropRoll> RollEnemyDrops(int wave,ExpeditionMonsterRank rank,
        IReadOnlyList<ItemMasterRow> equippedItems,System.Random random)
    {
        if(random==null)throw new ArgumentNullException("random");
        List<EnemyDropRoll> result=new List<EnemyDropRoll>();
        GameMasterDatabase database=GameMasterData.Database;
        if(database==null||database.drops==null)return result;
        foreach(DropMasterRow row in database.drops)
        {
            if(row==null||!row.enabled||wave<row.minWave||wave>row.maxWave)continue;
            int rankIndex=Math.Max(0,Math.Min(2,(int)rank-1));
            int itemCount=equippedItems==null?0:equippedItems.Count;
            float chance=row.chance;
            bool currency=string.Equals(
                row.rewardType,"ExpeditionCurrency",StringComparison.Ordinal);
            if(currency)
            {
                chance=Math.Min(0.4f,
                    chance+rankIndex*0.03f+itemCount*0.02f);
                if(wave>=GameMasterData.ExpeditionInt(
                       "enemy_currency_nerf_start_wave",7))
                    chance*=Math.Max(0,GameMasterData.ExpeditionInt(
                        "enemy_currency_mid_late_percent",85))/100f;
            }
            else if(string.Equals(row.rewardType,"Equipment",StringComparison.Ordinal))
                chance=Math.Min(1f,chance+itemCount*0.07f);
            if(random.NextDouble()>=chance)continue;
            int minimum=Math.Max(0,row.minAmount);
            int maximum=Math.Max(minimum,row.maxAmount);
            int amount=random.Next(minimum,maximum+1);
            if(currency)amount=Math.Min(MaximumCurrencyPerDrop,amount);
            if(amount<=0)continue;
            if(string.Equals(row.rewardType,"Equipment",StringComparison.Ordinal))
            {
                List<ItemMasterRow> pool=GameMasterData.ItemsInPool(row.itemPoolId);
                for(int i=0;i<amount&&pool.Count>0;i++)
                {
                    ItemMasterRow item=itemCount>0
                        ?equippedItems[random.Next(itemCount)]
                        :pool[random.Next(pool.Count)];
                    if(item==null||!item.IsBase)
                        item=pool[random.Next(pool.Count)];
                    if(item==null)continue;
                    result.Add(new EnemyDropRoll{rewardType=row.rewardType,itemId=item.id,amount=1});
                }
            }
            else result.Add(new EnemyDropRoll{rewardType=row.rewardType,amount=amount});
        }
        return result;
    }

    public static int InterestIncome(int currency)
    {
        int step=Math.Max(1,GameMasterData.ExpeditionInt(
            "interest_currency_step",10));
        int cap=Math.Max(0,GameMasterData.ExpeditionInt(
            "interest_currency_cap",3));
        return Math.Min(cap,Math.Max(0,currency)/step);
    }

    public static ExpeditionBattleIncome ResolveBattleIncome(
        ExpeditionRunSave run,bool victory)
    {
        if(run==null)throw new ArgumentNullException("run");
        if(victory)
        {
            run.consecutiveWins++;
            run.consecutiveLosses=0;
        }
        else
        {
            run.consecutiveLosses++;
            run.consecutiveWins=0;
        }
        int streak=victory?run.consecutiveWins:run.consecutiveLosses;
        return new ExpeditionBattleIncome
        {
            baseIncome=Math.Max(0,GameMasterData.ExpeditionInt(
                "battle_base_currency",2)),
            interestIncome=InterestIncome(run.expeditionCurrency),
            streakIncome=streak>=4?2:streak>=2?1:0
        };
    }

    public static EnemyLootProfile BuildEnemyProfile(
        int wave,int enemyIndex,System.Random random)
    {
        return BuildEnemyProfile(wave,enemyIndex,random,1,Math.Max(1,enemyIndex+1));
    }

    public static EnemyLootProfile BuildEnemyProfile(
        int wave,int enemyIndex,System.Random random,int regionOrder,int enemyCount)
    {
        if(random==null)throw new ArgumentNullException("random");
        if(enemyIndex<0)throw new ArgumentOutOfRangeException("enemyIndex");
        EnemyLootProfile result=new EnemyLootProfile();
        int index=Math.Max(0,Math.Min(Rank3Counts.Length-1,wave-1));
        int rank3Count=Rank3Counts[index];
        int rank2Count=Rank2Counts[index];
        result.rank=enemyIndex<rank3Count
            ?ExpeditionMonsterRank.Rank3
            :enemyIndex<rank3Count+rank2Count
                ?ExpeditionMonsterRank.Rank2
                :ExpeditionMonsterRank.Rank1;

        int completedItems=wave==12
            ?enemyIndex==0?CompletedItemCounts[index]:0
            :enemyIndex<CompletedItemCounts[index]?1:0;
        int baseItems=wave==12
            ?enemyIndex>0&&enemyIndex<=BaseItemCounts[index]?1:0
            :enemyIndex>=CompletedItemCounts[index]&&
             enemyIndex<CompletedItemCounts[index]+BaseItemCounts[index]?1:0;
        GameMasterDatabase database=GameMasterData.Database;
        List<ItemMasterRow> completedPool=database==null||database.items==null
            ?new List<ItemMasterRow>()
            :database.items.Where(item=>item!=null&&item.enabled&&item.IsCompleted&&
                string.Equals(item.itemPoolId,"default_equipment",StringComparison.Ordinal))
                .ToList();
        AddRandomItems(result,completedPool,completedItems,random);
        AddRandomItems(result,GameMasterData.ItemsInPool("default_equipment"),
            baseItems,random);
        int regionalBudget=wave<=1?0:
            Math.Max(0,(Math.Max(1,regionOrder)-1)*(wave+2)/6);
        // The final guardian's transcendence and two completed items are its full budget.
        int regionalItems=wave==12&&enemyIndex==0?0:
            regionalBudget/Math.Max(1,enemyCount)+
            (enemyIndex<regionalBudget%Math.Max(1,enemyCount)?1:0);
        AddRegionalItems(result,regionalItems,regionOrder,wave,random);
        return result;
    }

    private static void AddRegionalItems(
        EnemyLootProfile profile,int count,int regionOrder,int wave,
        System.Random random)
    {
        GameMasterDatabase database=GameMasterData.Database;
        if(database==null||database.items==null)return;
        float completedChance=Math.Min(
            0.65f,Math.Max(0f,(regionOrder-2)*0.12f+(wave-6)*0.04f));
        for(int index=0;index<count&&profile.equippedItems.Count<3;index++)
        {
            bool completed=random.NextDouble()<completedChance;
            List<ItemMasterRow> pool=database.items.Where(item=>
                item!=null&&item.enabled&&
                string.Equals(item.itemPoolId,"default_equipment",StringComparison.Ordinal)&&
                (completed?item.IsCompleted:item.IsBase)&&
                profile.equippedItems.All(owned=>owned.id!=item.id)).ToList();
            if(pool.Count==0)continue;
            profile.equippedItems.Add(pool[random.Next(pool.Count)]);
        }
    }

    private static void AddRandomItems(
        EnemyLootProfile profile,List<ItemMasterRow> pool,int count,System.Random random)
    {
        for(int i=0;i<count&&pool.Count>0;i++)
        {
            int selected=random.Next(pool.Count);
            profile.equippedItems.Add(pool[selected]);
            pool.RemoveAt(selected);
        }
    }

    public static void Add(PendingWaveLoot pending,EnemyDropRoll drop)
    {
        if(pending==null||drop==null)return;
        if(drop.rewardType=="Equipment"&&!string.IsNullOrEmpty(drop.itemId))
            pending.itemIds.Add(drop.itemId);
        else if(drop.rewardType=="ExpeditionCurrency")
            pending.expeditionCurrency+=drop.amount;
    }

    public static void Commit(ExpeditionRunSave run,PendingWaveLoot pending)
    {
        if(run==null)throw new ArgumentNullException("run");
        if(pending==null)return;
        foreach(string itemId in pending.itemIds)
            ExpeditionItemSystem.CreateItem(run,itemId,true);
        run.expeditionCurrency+=pending.expeditionCurrency;
        pending.Clear();
    }

    public static void Discard(PendingWaveLoot pending)
    {
        if(pending!=null)pending.Clear();
    }
}
