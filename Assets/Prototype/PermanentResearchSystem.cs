using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class PermanentResearchSettlement
{
    public int experience;
    public int experienceBefore;
    public int experienceAfter;
    public int levelBefore;
    public int levelAfter;
    public int improvedWaves;
    public int newMilestones;
    public bool repeatedBracketPenalty;
}

public static class PermanentResearchSystem
{
    public const int MaximumLevel=30;
    public const int FirstLevelExperience=40;
    public const int LevelExperienceStep=10;

    public const string Health="research_survival_health";
    public const string Defense="research_survival_defense";
    public const string Resistance="research_survival_resistance";
    public const string Recovery="research_survival_recovery";
    public const string FrontlinePreparation="research_survival_frontline";
    public const string Attack="research_combat_attack";
    public const string AttackSpeed="research_combat_attack_speed";
    public const string SkillMastery="research_combat_skill_mastery";
    public const string CriticalTraining="research_combat_critical";
    public const string AdvancePreparation="research_combat_advance";
    public const string StartingFunds="research_support_starting_funds";
    public const string ShopNegotiation="research_support_shop";
    public const string CaptureProficiency="research_support_capture";
    public const string LootAppraisal="research_support_loot";
    // Retain the saved ID so older allocations can return their point during normalization.
    public const string AdvanceScouting="research_support_scouting";

    private static readonly PermanentResearchMasterRow[] Fallback=
    {
        Row(Health,"survival",1,5,.02f,"percent",false),
        Row(Defense,"survival",2,4,.02f,"percent",false),
        Row(Resistance,"survival",3,4,.02f,"percent",false),
        Row(Recovery,"survival",4,3,.03f,"percent",false),
        Row(FrontlinePreparation,"survival",5,1,.08f,"percent",true),
        Row(Attack,"combat",1,5,.015f,"percent",false),
        Row(AttackSpeed,"combat",2,4,.01f,"percent",false),
        Row(SkillMastery,"combat",3,4,.02f,"percent",false),
        Row(CriticalTraining,"combat",4,4,.01f,"percent",false),
        Row(AdvancePreparation,"combat",5,1,.10f,"percent",true),
        Row(StartingFunds,"support",1,3,1f,"flat",false),
        Row(ShopNegotiation,"support",2,3,1f,"flat",false),
        Row(CaptureProficiency,"support",3,4,.02f,"percent",false),
        Row(LootAppraisal,"support",4,4,1f,"flat",false)
    };

    public static IReadOnlyList<PermanentResearchMasterRow> All
    {
        get
        {
            IReadOnlyList<PermanentResearchMasterRow> master=
                GameMasterData.PermanentResearch;
            return master!=null&&master.Count>0?master:Fallback;
        }
    }

    public static void Normalize(PlayerProfileSave profile)
    {
        if(profile==null)return;
        profile.permanentResearchExperience=Mathf.Clamp(
            profile.permanentResearchExperience,0,ExperienceForLevel(MaximumLevel));
        if(profile.permanentResearchAllocations==null)
            profile.permanentResearchAllocations=
                new List<PermanentResearchAllocationSave>();
        if(profile.permanentResearchRoutes==null)
            profile.permanentResearchRoutes=
                new List<PermanentResearchRouteProgressSave>();
        if(profile.settledPermanentResearchRunIds==null)
            profile.settledPermanentResearchRunIds=new List<string>();

        Dictionary<string,PermanentResearchMasterRow> definitions=All
            .Where(row=>row!=null&&!string.IsNullOrWhiteSpace(row.id))
            .GroupBy(row=>row.id,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.Ordinal);
        profile.permanentResearchAllocations=profile.permanentResearchAllocations
            .Where(value=>value!=null&&definitions.ContainsKey(value.researchId))
            .GroupBy(value=>value.researchId,StringComparer.Ordinal)
            .Select(group=>new PermanentResearchAllocationSave
            {
                researchId=group.Key,
                rank=Mathf.Clamp(group.Max(value=>value.rank),0,
                    definitions[group.Key].maxRank)
            })
            .Where(value=>value.rank>0).ToList();
        profile.permanentResearchRoutes=profile.permanentResearchRoutes
            .Where(value=>value!=null&&!string.IsNullOrWhiteSpace(value.routeKey))
            .GroupBy(value=>value.routeKey,StringComparer.Ordinal)
            .Select(group=>new PermanentResearchRouteProgressSave
            {
                routeKey=group.Key,
                bestCompletedWave=Mathf.Clamp(
                    group.Max(value=>value.bestCompletedWave),0,12),
                lastFailedBracket=Mathf.Clamp(
                    group.Last().lastFailedBracket,-1,3)
            }).ToList();
        profile.settledPermanentResearchRunIds=
            profile.settledPermanentResearchRunIds
                .Where(id=>!string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal).ToList();
        if(profile.settledPermanentResearchRunIds.Count>50)
            profile.settledPermanentResearchRunIds.RemoveRange(0,
                profile.settledPermanentResearchRunIds.Count-50);

        int excess=Mathf.Max(0,SpentPoints(profile)-Level(profile));
        if(excess>0)
        {
            foreach(PermanentResearchAllocationSave value in
                    profile.permanentResearchAllocations.AsEnumerable().Reverse())
            {
                int removed=Mathf.Min(value.rank,excess);
                value.rank-=removed;
                excess-=removed;
                if(excess==0)break;
            }
            profile.permanentResearchAllocations.RemoveAll(value=>value.rank<=0);
        }
    }

    public static int Level(PlayerProfileSave profile)
    {
        int experience=profile==null?0:Mathf.Max(0,profile.permanentResearchExperience);
        return LevelForExperience(experience);
    }

    public static int LevelForExperience(int experience)
    {
        experience=Mathf.Clamp(experience,0,ExperienceForLevel(MaximumLevel));
        int level=0;
        while(level<MaximumLevel&&experience>=ExperienceForLevel(level+1))level++;
        return level;
    }

    public static int ExperienceForLevel(int level)
    {
        int clamped=Mathf.Clamp(level,0,MaximumLevel);
        return clamped*(2*FirstLevelExperience+(clamped-1)*LevelExperienceStep)/2;
    }

    public static int ExperienceIntoLevel(PlayerProfileSave profile)
    {
        int level=Level(profile);
        return Mathf.Max(0,(profile?.permanentResearchExperience??0)-
            ExperienceForLevel(level));
    }

    public static int ExperienceToNextLevel(PlayerProfileSave profile)
    {
        int level=Level(profile);
        return level>=MaximumLevel?0:
            ExperienceForLevel(level+1)-ExperienceForLevel(level);
    }

    public static int Rank(PlayerProfileSave profile,string researchId)
    {
        if(profile?.permanentResearchAllocations==null)return 0;
        foreach(var item in profile.permanentResearchAllocations)
            if(item!=null&&string.Equals(item.researchId,researchId,StringComparison.Ordinal))
                return Mathf.Max(0,item.rank);
        return 0;
    }

    public static int SpentPoints(PlayerProfileSave profile)
    {
        return profile?.permanentResearchAllocations==null?0:
            profile.permanentResearchAllocations.Where(value=>value!=null)
                .Sum(value=>Mathf.Max(0,value.rank));
    }

    public static int AvailablePoints(PlayerProfileSave profile)
    {
        return Mathf.Max(0,Level(profile)-SpentPoints(profile));
    }

    public static bool IsCapstoneAvailable(
        PlayerProfileSave profile,PermanentResearchMasterRow definition)
    {
        if(definition==null||!definition.capstone)return true;
        return All.Where(row=>row!=null&&row.enabled&&!row.capstone&&
                string.Equals(row.branch,definition.branch,StringComparison.Ordinal))
            .All(row=>Rank(profile,row.id)>=row.maxRank);
    }

    public static bool TryChangeRank(
        PlayerProfileSave profile,string researchId,int delta)
    {
        if(profile==null||delta==0)return false;
        PermanentResearchMasterRow definition=All.FirstOrDefault(row=>
            row!=null&&string.Equals(row.id,researchId,StringComparison.Ordinal));
        if(definition==null)return false;
        int current=Rank(profile,researchId);
        int target=Mathf.Clamp(current+Math.Sign(delta),0,definition.maxRank);
        bool protectsAllocatedCapstone=target<current&&!definition.capstone&&
            All.Any(row=>row!=null&&row.capstone&&
                string.Equals(row.branch,definition.branch,StringComparison.Ordinal)&&
                Rank(profile,row.id)>0);
        if(target==current||target>current&&
           (AvailablePoints(profile)<=0||!IsCapstoneAvailable(profile,definition))||
           protectsAllocatedCapstone)
            return false;
        PermanentResearchAllocationSave allocation=
            profile.permanentResearchAllocations.FirstOrDefault(value=>value!=null&&
                string.Equals(value.researchId,researchId,StringComparison.Ordinal));
        if(allocation==null)
        {
            allocation=new PermanentResearchAllocationSave{researchId=researchId};
            profile.permanentResearchAllocations.Add(allocation);
        }
        allocation.rank=target;
        if(target==0)profile.permanentResearchAllocations.Remove(allocation);
        return true;
    }

    public static bool ResetAllocations(PlayerProfileSave profile)
    {
        if(profile?.permanentResearchAllocations==null||
           profile.permanentResearchAllocations.Count==0)return false;
        profile.permanentResearchAllocations.Clear();
        return true;
    }

    public static float Value(PlayerProfileSave profile,string researchId)
    {
        if(researchId==AdvanceScouting)return 0f;
        // Combat queries do not need the sorted UI catalog or a cached snapshot of editable rows.
        var rows=GameMasterData.Database==null?null:GameMasterData.Database.permanentResearch;
        bool hasMaster=false;
        if(rows!=null)
            foreach(var row in rows)
            {
                if(row==null||!row.enabled)continue;
                hasMaster=true;
                if(string.Equals(row.id,researchId,StringComparison.Ordinal))
                    return Rank(profile,researchId)*row.valuePerRank;
            }
        if(!hasMaster)
            foreach(var row in Fallback)
                if(string.Equals(row.id,researchId,StringComparison.Ordinal))
                    return Rank(profile,researchId)*row.valuePerRank;
        return 0f;
    }

    public static bool Unlocked(PlayerProfileSave profile,string researchId)
    {
        return Rank(profile,researchId)>0;
    }

    public static int FirstPaidShopRerollDiscount(PlayerProfileSave profile)=>
        Mathf.RoundToInt(Value(profile,ShopNegotiation));
    public static int LootRerollDiscount(PlayerProfileSave profile)=>
        Mathf.RoundToInt(Value(profile,LootAppraisal));
    public static int StartingGoldBonus(PlayerProfileSave profile)=>
        Mathf.RoundToInt(Value(profile,StartingFunds));
    public static float CaptureChanceBonus(PlayerProfileSave profile)=>
        Value(profile,CaptureProficiency);
    public static float RecoveryMultiplier(PlayerProfileSave profile)=>
        1f+Value(profile,Recovery);
    public static float SkillPowerMultiplier(PlayerProfileSave profile)=>
        1f+Value(profile,SkillMastery);

    public static string DisplayName(PermanentResearchMasterRow definition)
    {
        if(definition==null)return "";
        return GameLocalization.TextByKey(definition.nameKey,
            FallbackName(definition.id));
    }

    public static string Description(PermanentResearchMasterRow definition)
    {
        if(definition==null)return "";
        return GameLocalization.TextByKey(definition.descriptionKey,
            FallbackDescription(definition.id));
    }

    public static string BranchName(string branch)
    {
        return GameLocalization.TextByKey("research.branch."+branch,
            branch=="survival"?"생존 연구":
            branch=="combat"?"전투 연구":"탐험 지원 연구");
    }

    public static PermanentResearchSettlement Settle(
        PlayerProfileSave profile,PlayerProfileStore store,
        MonsterDatabase database,ExpeditionRunSave run,bool victory,bool save=true)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(store==null)throw new ArgumentNullException(nameof(store));
        if(database==null)throw new ArgumentNullException(nameof(database));
        if(run==null||string.IsNullOrWhiteSpace(run.runId))
            throw new ArgumentException("A valid expedition run is required.",nameof(run));
        Normalize(profile);
        PermanentResearchSettlement result=new PermanentResearchSettlement
        {
            experienceBefore=profile.permanentResearchExperience,
            levelBefore=Level(profile)
        };
        if(profile.settledPermanentResearchRunIds.Contains(run.runId))
        {
            result.experienceAfter=result.experienceBefore;
            result.levelAfter=result.levelBefore;
            return result;
        }
        int completed=Mathf.Clamp(run.lastCompletedWave,0,12);
        string key=RouteKey(run.regionId,run.difficulty);
        PermanentResearchRouteProgressSave route=
            profile.permanentResearchRoutes.FirstOrDefault(value=>value.routeKey==key);
        if(route==null)
        {
            route=new PermanentResearchRouteProgressSave{routeKey=key};
            profile.permanentResearchRoutes.Add(route);
        }
        int previousBest=route.bestCompletedWave;
        int bracket=completed<=0?0:Mathf.Clamp((completed-1)/3,0,3);
        int crisisClears=(completed>=4?1:0)+(completed>=8?1:0);
        int baseReward=completed*4+crisisClears*8+
            (victory&&completed>=12?20:0);
        bool repeated=!victory&&completed>0&&completed<=previousBest&&
            route.lastFailedBracket==bracket;
        if(repeated)
            baseReward=Mathf.Max(8,Mathf.RoundToInt(baseReward*.35f));

        int improved=Mathf.Max(0,completed-previousBest);
        int milestones=0;
        int[] milestoneWaves={3,6,9,12};
        for(int i=0;i<milestoneWaves.Length;i++)
            if(previousBest<milestoneWaves[i]&&completed>=milestoneWaves[i])
                milestones++;
        int raw=baseReward+improved*5+milestones*15;
        float difficultyMultiplier=DifficultyExperienceMultiplier(run.difficulty);
        int reward=Mathf.Max(0,Mathf.RoundToInt(raw*difficultyMultiplier));

        route.bestCompletedWave=Mathf.Max(previousBest,completed);
        route.lastFailedBracket=victory?-1:bracket;
        profile.permanentResearchExperience=Mathf.Clamp(
            profile.permanentResearchExperience+reward,0,
            ExperienceForLevel(MaximumLevel));
        profile.settledPermanentResearchRunIds.Add(run.runId);
        if(profile.settledPermanentResearchRunIds.Count>50)
            profile.settledPermanentResearchRunIds.RemoveRange(0,
                profile.settledPermanentResearchRunIds.Count-50);
        if(save)store.Save(profile);

        result.experience=Mathf.Max(
            0,profile.permanentResearchExperience-result.experienceBefore);
        result.experienceAfter=profile.permanentResearchExperience;
        result.levelAfter=Level(profile);
        result.improvedWaves=improved;
        result.newMilestones=milestones;
        result.repeatedBracketPenalty=repeated;
        return result;
    }

    public static string RouteKey(string regionId,ExpeditionDifficulty difficulty)
    {
        return (regionId??string.Empty)+"|"+
               (int)ExpeditionDifficultyRules.Clamp(difficulty);
    }

    public static float DifficultyExperienceMultiplier(
        ExpeditionDifficulty difficulty)
    {
        switch(ExpeditionDifficultyRules.Clamp(difficulty))
        {
            case ExpeditionDifficulty.Beginner:return 1f;
            case ExpeditionDifficulty.Veteran:return 1.55f;
            case ExpeditionDifficulty.Hell:return 2f;
            default:return 1.25f;
        }
    }

    private static PermanentResearchMasterRow Row(
        string id,string branch,int order,int maxRank,float value,
        string valueKind,bool capstone)
    {
        return new PermanentResearchMasterRow
        {
            id=id,branch=branch,displayOrder=order,
            nameKey="research."+id+".name",
            descriptionKey="research."+id+".description",
            maxRank=maxRank,valuePerRank=value,valueKind=valueKind,
            capstone=capstone,enabled=true,dataVersion=1
        };
    }

    private static string FallbackName(string id)
    {
        switch(id)
        {
            case Health:return "기초 체력 훈련";
            case Defense:return "방어 훈련";
            case Resistance:return "저항 훈련";
            case Recovery:return "회복 숙련";
            case FrontlinePreparation:return "전선 대비";
            case Attack:return "공격 훈련";
            case AttackSpeed:return "공격 속도 훈련";
            case SkillMastery:return "스킬 숙련";
            case CriticalTraining:return "치명타 훈련";
            case AdvancePreparation:return "선제 준비";
            case StartingFunds:return "출발 자금";
            case ShopNegotiation:return "상점 협상";
            case CaptureProficiency:return "포획 숙련";
            case LootAppraisal:return "전리품 감정";
            case AdvanceScouting:return "선행 정찰";
            default:return id??"";
        }
    }

    private static string FallbackDescription(string id)
    {
        switch(id)
        {
            case Health:return "아군 최대 체력 +2% / 단계";
            case Defense:return "아군 방어력 +2% / 단계";
            case Resistance:return "아군 상태 저항 +2% / 단계";
            case Recovery:return "받는 회복과 보호막 +3% / 단계";
            case FrontlinePreparation:return "전투 시작 후 6초 동안 받는 피해 -8%";
            case Attack:return "아군 공격력 +1.5% / 단계";
            case AttackSpeed:return "아군 공격 속도 +1% / 단계";
            case SkillMastery:return "스킬 피해·회복·보호막 +2% / 단계";
            case CriticalTraining:return "아군 치명타 확률 +1% / 단계";
            case AdvancePreparation:return "전투 시작 시 스킬 게이지 10% 충전";
            case StartingFunds:return "탐험 시작 골드 +1 / 단계";
            case ShopNegotiation:return "각 상점의 첫 유료 리롤 비용 -1 / 단계";
            case CaptureProficiency:return "포획 성공 확률 +2%p / 단계";
            case LootAppraisal:return "전리품 재감정 비용 -1 / 단계";
            case AdvanceScouting:return "적 정보는 기본 공개되며, 기존에 사용한 연구 포인트는 반환됩니다.";
            default:return "";
        }
    }
}
