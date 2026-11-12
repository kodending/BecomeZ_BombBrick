using System;

public static class ExpeditionDeploymentSystem
{
    public static int MaximumLimit
    {
        get{return Math.Max(1,Math.Min(
            GameMasterData.ExpeditionInt("roster_size",8),
            GameMasterData.ExpeditionInt("max_deployment_limit",8)));}
    }

    public static int NextUpgradeCost(int currentLimit)
    {
        int target=currentLimit+1;
        if(target>MaximumLimit)return 0;
        int fallback=target==4?3:target==5?5:target==6?8:target==7?12:16;
        return Math.Max(1,GameMasterData.ExpeditionInt(
            "deployment_upgrade_cost_"+target,fallback));
    }

    public static bool TryUpgrade(ExpeditionRunSave run)
    {
        if(run==null)return false;
        if(ExpeditionAugmentRuntime.BlocksDeploymentPurchase(run))return false;
        int current=Math.Max(1,run.maxDeploymentCount);
        if(current>=MaximumLimit)return false;
        int cost=NextUpgradeCost(current);
        if(cost<=0||run.expeditionCurrency<cost)return false;
        run.expeditionCurrency-=cost;
        run.maxDeploymentCount=current+1;
        return true;
    }
}
