using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CaptureNetGrade
{
    Standard=0,
    Rare=1
}

[Serializable]
public sealed class CaptureNetInventorySave
{
    public string itemId;
    public int count;
}

public sealed class CaptureNetDefinition
{
    public string itemId;
    public CaptureNetGrade grade;
    public string displayName;
    public float fallbackChance;
    public string chanceConfigKey;

    public float SuccessChance=>Mathf.Clamp01(
        GameMasterData.CaptureFloat(chanceConfigKey,fallbackChance));
}

public static class CaptureNetCatalog
{
    public const string StandardId="capture_net_common";
    public const string RareId="capture_net_rare";
    public const string CommonId=StandardId;
    public const int StandardTokenCost=3;

    private static readonly CaptureNetDefinition[] Definitions=
    {
        new CaptureNetDefinition
        {
            itemId=StandardId,
            grade=CaptureNetGrade.Standard,
            displayName="포획 그물",
            fallbackChance=0.25f,
            chanceConfigKey="capture_net_common_chance"
        },
        new CaptureNetDefinition
        {
            itemId=RareId,
            grade=CaptureNetGrade.Rare,
            displayName="강화 포획 그물",
            fallbackChance=0.75f,
            chanceConfigKey="capture_net_rare_chance"
        }
    };

    public static IReadOnlyList<CaptureNetDefinition> All=>Definitions;

    public static CaptureNetDefinition Get(string itemId)
    {
        return Definitions.FirstOrDefault(definition=>
            string.Equals(definition.itemId,itemId,StringComparison.Ordinal));
    }

    public static bool IsCaptureNet(string itemId)
    {
        return Get(itemId)!=null;
    }

    public static float EffectiveSuccessChance(
        CaptureNetDefinition definition,bool tutorialGuaranteed)
    {
        if(definition==null)return 0f;
        return tutorialGuaranteed&&definition.itemId==StandardId
            ?1f
            :definition.SuccessChance;
    }

    public static Color GradeColor(CaptureNetGrade grade)
    {
        if(grade==CaptureNetGrade.Rare)
            return new Color(0.86f,0.48f,1f,1f);
        return new Color(0.34f,0.92f,0.96f,1f);
    }

    public static int TokenCost=>Mathf.Max(1,Mathf.RoundToInt(
        GameMasterData.CaptureFloat(
            "capture_net_standard_token_cost",StandardTokenCost)));
}

public static class CaptureNetInventory
{
    public static void Normalize(PlayerProfileSave profile)
    {
        if(profile==null)return;
        if(profile.captureNets==null)
            profile.captureNets=new List<CaptureNetInventorySave>();

        Dictionary<string,int> merged=new Dictionary<string,int>(
            StringComparer.Ordinal);
        foreach(CaptureNetInventorySave stack in profile.captureNets)
        {
            if(stack==null||!CaptureNetCatalog.IsCaptureNet(stack.itemId)||
               stack.count<=0)continue;
            merged.TryGetValue(stack.itemId,out int existing);
            merged[stack.itemId]=existing+stack.count;
        }
        profile.captureNets=CaptureNetCatalog.All
            .Where(definition=>merged.ContainsKey(definition.itemId))
            .Select(definition=>new CaptureNetInventorySave
            {
                itemId=definition.itemId,
                count=merged[definition.itemId]
            })
            .ToList();
    }

    public static int Count(PlayerProfileSave profile,string itemId)
    {
        if(profile==null||profile.captureNets==null)return 0;
        CaptureNetInventorySave stack=profile.captureNets.FirstOrDefault(
            value=>value!=null&&
                   string.Equals(value.itemId,itemId,StringComparison.Ordinal));
        return stack==null?0:Math.Max(0,stack.count);
    }

    public static bool TryConsume(
        PlayerProfileSave profile,string itemId,out string error)
    {
        error="";
        if(profile==null)
        {
            error="플레이어 프로필이 없습니다.";
            return false;
        }
        CaptureNetDefinition definition=CaptureNetCatalog.Get(itemId);
        if(definition==null)
        {
            error="알 수 없는 포획 그물입니다.";
            return false;
        }
        CaptureNetInventorySave stack=profile.captureNets==null?null:
            profile.captureNets.FirstOrDefault(value=>value!=null&&
                string.Equals(value.itemId,itemId,StringComparison.Ordinal)&&
                value.count>0);
        if(stack==null)
        {
            error=definition.displayName+"이(가) 없습니다.";
            return false;
        }
        stack.count--;
        if(stack.count<=0)profile.captureNets.Remove(stack);
        return true;
    }

    public static void Grant(
        PlayerProfileSave profile,string itemId,int count)
    {
        if(profile==null||count<=0||!CaptureNetCatalog.IsCaptureNet(itemId))
            return;
        if(profile.captureNets==null)
            profile.captureNets=new List<CaptureNetInventorySave>();
        CaptureNetInventorySave stack=profile.captureNets.FirstOrDefault(
            value=>value!=null&&
                   string.Equals(value.itemId,itemId,StringComparison.Ordinal));
        if(stack==null)
        {
            stack=new CaptureNetInventorySave{itemId=itemId};
            profile.captureNets.Add(stack);
        }
        stack.count=Math.Max(0,stack.count)+count;
        Normalize(profile);
    }
}
