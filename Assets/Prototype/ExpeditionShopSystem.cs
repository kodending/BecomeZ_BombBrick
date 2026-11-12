using System;
using System.Collections.Generic;

public static class ExpeditionShopSystem
{
    public static int StockCount=>Math.Max(1,GameMasterData.ExpeditionInt("shop_item_count",5));
    public static int ItemCost=>Math.Max(0,GameMasterData.ExpeditionInt("shop_base_item_cost",5));

    public static bool IsShopWave(int wave)
    {
        string configured=GameMasterData.ExpeditionString("shop_open_waves","3|6|9|11");
        string[] values=configured.Split(new[]{'|',','},StringSplitOptions.RemoveEmptyEntries);
        for(int i=0;i<values.Length;i++)
            if(int.TryParse(values[i].Trim(),out int configuredWave)&&configuredWave==wave)return true;
        return false;
    }

    public static int RefreshCost(ExpeditionRunSave run)
    {
        return RefreshCost(run,null);
    }

    public static int RefreshCost(
        ExpeditionRunSave run,PlayerProfileSave profile)
    {
        if(run==null||run.shopRefreshCount<=0)return 0;
        int initial=Math.Max(0,GameMasterData.ExpeditionInt("shop_refresh_initial_cost",1));
        int step=Math.Max(0,GameMasterData.ExpeditionInt("shop_refresh_cost_step",1));
        int discount=run.shopRefreshCount==1
            ?PermanentResearchSystem.FirstPaidShopRerollDiscount(profile):0;
        return Math.Max(0,initial+(run.shopRefreshCount-1)*step-discount);
    }

    public static void EnsureStock(ExpeditionRunSave run,int seed)
    {
        if(run==null)throw new ArgumentNullException("run");
        EnsureCollections(run);
        if(run.shopWave==run.wave&&run.shopItemIds.Count==StockCount)return;
        run.shopWave=run.wave;
        run.shopRefreshCount=0;
        RollStock(run,new Random(seed));
    }

    public static bool TryRefresh(ExpeditionRunSave run,int seed,out string error)
    {
        return TryRefresh(run,seed,null,out error);
    }

    public static bool TryRefresh(
        ExpeditionRunSave run,int seed,PlayerProfileSave profile,out string error)
    {
        error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        EnsureCollections(run);
        int cost=RefreshCost(run,profile);
        if(run.expeditionCurrency<cost){error="골드가 부족합니다.";return false;}
        run.expeditionCurrency-=cost;
        run.shopRefreshCount++;
        RollStock(run,new Random(seed));
        return true;
    }

    public static bool TryPurchase(
        ExpeditionRunSave run,int slotIndex,out ExpeditionItemInstanceSave purchased,out string error)
    {
        purchased=null;error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        EnsureCollections(run);
        if(slotIndex<0||slotIndex>=run.shopItemIds.Count)
        {error="유효하지 않은 상점 칸입니다.";return false;}
        string itemId=run.shopItemIds[slotIndex];
        if(string.IsNullOrEmpty(itemId)){error="이미 판매된 아이템입니다.";return false;}
        if(run.expeditionCurrency<ItemCost){error="골드가 부족합니다.";return false;}
        if(GameMasterData.Item(itemId)==null){error="상점 아이템 정보를 찾을 수 없습니다.";return false;}
        run.expeditionCurrency-=ItemCost;
        purchased=ExpeditionItemSystem.CreateItem(run,itemId,true);
        run.shopItemIds[slotIndex]="";
        return true;
    }

    private static void RollStock(ExpeditionRunSave run,Random random)
    {
        run.shopItemIds.Clear();
        List<ItemMasterRow> pool=GameMasterData.ItemsInPool("default_equipment");
        for(int i=0;i<StockCount;i++)
        {
            ItemMasterRow item=pool.Count==0?null:pool[random.Next(pool.Count)];
            run.shopItemIds.Add(item==null?"":item.id);
        }
    }

    private static void EnsureCollections(ExpeditionRunSave run)
    {
        if(run.shopItemIds==null)run.shopItemIds=new List<string>();
    }
}
