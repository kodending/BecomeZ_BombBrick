using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class HunterRegionDefinition
{
    public string id;
    public string displayName;
    public int order;
    public int regionBoxCost;
    public int tokenMultiplierPercent;
    public int clearBonus;
    public string monsterIds;
    public int enemyHealthPercent=100;
    public int enemyAttackPercent=100;
    public string ruleId="None";
    private string parsedMonsterIdsSource;
    private IReadOnlyList<string> parsedMonsterIds;
    private string parsedRuleSource;
    private HunterRegionRule parsedRule;

    internal HunterRegionRule ParsedRule()
    {
        if(parsedRuleSource==ruleId)return parsedRule;
        parsedRuleSource=ruleId;
        return parsedRule=Enum.TryParse(ruleId,true,out HunterRegionRule rule)
            ?rule:HunterRegionRule.None;
    }

    public IReadOnlyList<string> MonsterIds()
    {
        if(parsedMonsterIds!=null&&parsedMonsterIdsSource==monsterIds)return parsedMonsterIds;
        parsedMonsterIdsSource=monsterIds;
        return parsedMonsterIds=string.IsNullOrWhiteSpace(monsterIds)
            ?Array.Empty<string>()
            :monsterIds.Split('|')
                .Select(value=>value.Trim())
                .Where(value=>value.Length>0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }
}

public enum HunterRegionRule
{
    None,
    PoisonFog,
    CrimsonHeat,
    FrostWave,
    SkyResonance
}

public static class HunterRegionCatalog
{
    private static IReadOnlyList<HunterRegionDefinition> cachedRegions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ClearCache() => cachedRegions=null;

    public const string FirstRegionId="prototype_region";

    // Final_10M: condition-matched unit lifts >= 1%p plus two reliable
    // positive/negative pair synergies per region. Values are percentage points.
    private static readonly Dictionary<string,float> MlBalanceSignals=
        new Dictionary<string,float>(StringComparer.Ordinal)
        {
            {"prototype_region|ager",-1.55f},
            {"prototype_region|aranea",1.09f},
            {"prototype_region|chiwoo",2.48f},
            {"prototype_region|entpone",2.44f},
            {"prototype_region|gaiason",1.73f},
            {"prototype_region|jangpung",1.11f},
            {"prototype_region|kogason",1.62f},
            {"prototype_region|lilas",3.50f},
            {"prototype_region|nukgu",-2.42f},
            {"prototype_region|tino",1.86f},
            {"prototype_region|velos",-2.47f},
            {"prototype_region|woongi",-2.10f},
            {"prototype_region|aranea|forestdeer",2.40f},
            {"prototype_region|lilas|tino",2.20f},
            {"prototype_region|forestdeer|nukgu",-3.02f},
            {"prototype_region|lilas|terakan",-2.94f},

            {"poison_fog_swamp|bonrex",-1.37f},
            {"poison_fog_swamp|geomchi",-3.81f},
            {"poison_fog_swamp|holophonas",-2.11f},
            {"poison_fog_swamp|kimbul",-4.77f},
            {"poison_fog_swamp|mukryeong",1.28f},
            {"poison_fog_swamp|nectis",3.28f},
            {"poison_fog_swamp|ragon",2.18f},
            {"poison_fog_swamp|raputo",-1.93f},
            {"poison_fog_swamp|sundongi",-2.46f},
            {"poison_fog_swamp|toto",1.39f},
            {"poison_fog_swamp|versus",2.16f},
            {"poison_fog_swamp|vivre",3.17f},
            {"poison_fog_swamp|golgaebi|kimbul",2.62f},
            {"poison_fog_swamp|doksirum|versus",2.38f},
            {"poison_fog_swamp|golgaebi|raputo",-2.63f},
            {"poison_fog_swamp|raputo|sundongi",-2.57f},

            {"crimson_jungle|berber",-2.05f},
            {"crimson_jungle|honga",-1.54f},
            {"crimson_jungle|maka",1.78f},
            {"crimson_jungle|mantius",1.55f},
            {"crimson_jungle|nox",-1.75f},
            {"crimson_jungle|tarvil",-1.15f},
            {"crimson_jungle|velok",-3.31f},
            {"crimson_jungle|magril|maka",2.86f},
            {"crimson_jungle|berber|magril",2.46f},
            {"crimson_jungle|berber|velok",-10.85f},
            {"crimson_jungle|berber|honga",-3.76f},

            {"frozen_highlands|irontusk",2.25f},
            {"frozen_highlands|krogal",-1.69f},
            {"frozen_highlands|seola",-1.73f},
            {"frozen_highlands|seollang",-1.61f},
            {"frozen_highlands|seolmyo",-1.41f},
            {"frozen_highlands|ironkuma|irontusk",1.55f},
            {"frozen_highlands|seola|seolmyo",1.47f},
            {"frozen_highlands|seola|seolgason",-2.00f},
            {"frozen_highlands|krogal|seolgason",-1.45f},

            {"sky_sanctuary|byeoljwi",1.33f},
            {"sky_sanctuary|dami",-2.63f},
            {"sky_sanctuary|geumgangsu",2.91f},
            {"sky_sanctuary|lumirum",-3.63f},
            {"sky_sanctuary|santius",1.41f},
            {"sky_sanctuary|solarbeetle",-1.47f},
            {"sky_sanctuary|temvire",1.22f},
            {"sky_sanctuary|wolyeong",-2.24f},
            {"sky_sanctuary|dami|lumirum",3.21f},
            {"sky_sanctuary|moska|temvire",1.80f},
            {"sky_sanctuary|kingpa|lumirum",-10.11f},
            {"sky_sanctuary|lumirum|temvire",-2.11f}
        };

    private sealed class RegionFallback
    {
        public string id;
        public string displayName;
        public int cost;
        public int multiplierPercent;
        public int clearBonus;
        public string monsterIds;
        public int enemyHealthPercent;
        public int enemyAttackPercent;
        public string ruleId;
    }

    private static readonly RegionFallback[] Fallbacks=
    {
        new RegionFallback
        {
            id=FirstRegionId,
            displayName="태초의 초원",
            cost=10,
            multiplierPercent=100,
            clearBonus=3,
            enemyHealthPercent=100,enemyAttackPercent=100,ruleId="None",
            monsterIds="velos|terakan|ager|lilas|woongi|nukgu|aranea|chundongi|tino|chiwoo|kogason|kingmore|gaiason|jangpung|forestdeer|entpone"
        },
        new RegionFallback
        {
            id="poison_fog_swamp",displayName="독안개 습지",cost=13,
            multiplierPercent=125,clearBonus=4,
            enemyHealthPercent=100,enemyAttackPercent=100,ruleId="PoisonFog",
            monsterIds="raputo|geomchi|golgaebi|kimbul|ragon|versus|sundongi|bonrex|toto|nectis|doksirum|flotis|eclipsedeer|holophonas|vivre|mukryeong"
        },
        new RegionFallback
        {
            id="crimson_jungle",displayName="붉은 밀림",cost=16,
            multiplierPercent=150,clearBonus=5,
            enemyHealthPercent=100,enemyAttackPercent=100,ruleId="CrimsonHeat",
            monsterIds="velok|nox|rokrok|tarvil|magril|patjwi|berber|honga|hongryeon|magno|maka|mantius|silanti|griphonas|igvire|yeomhae"
        },
        new RegionFallback
        {
            id="frozen_highlands",displayName="얼어붙은 고원",cost=20,
            multiplierPercent=180,clearBonus=7,
            enemyHealthPercent=100,enemyAttackPercent=100,ruleId="FrostWave",
            monsterIds="wilo|seola|krogal|bingareu|seoljwi|ironkuma|seollang|icearachne|seolmyo|cheolno|seolgason|irontusk|atlagon|lunadeer|frovire|haeta"
        },
        new RegionFallback
        {
            id="sky_sanctuary",displayName="천공 성역",cost=25,
            multiplierPercent=220,clearBonus=9,
            enemyHealthPercent=100,enemyAttackPercent=100,ruleId="SkyResonance",
            monsterIds="kingpa|frosk|moska|kongjwi|byeoljwi|dami|wolyeong|uno|santius|lumirum|solarbeetle|noepung|holydeer|pone|temvire|geumgangsu"
        }
    };

    public static IReadOnlyList<HunterRegionDefinition> All
    {
        get
        {
            if(Application.isPlaying&&cachedRegions!=null)return cachedRegions;
            List<HunterRegionDefinition> result=
                new List<HunterRegionDefinition>();
            for(int index=0;index<Fallbacks.Length;index++)
            {
                RegionFallback fallback=Fallbacks[index];
                string prefix="hunter_region_"+(index+1)+"_";
                result.Add(new HunterRegionDefinition
                {
                    id=GameMasterData.ExpeditionString(
                        prefix+"id",fallback.id),
                    displayName=GameMasterData.ExpeditionString(
                        prefix+"display_name",fallback.displayName),
                    order=index+1,
                    regionBoxCost=GameMasterData.ExpeditionInt(
                        prefix+"box_cost",fallback.cost),
                    tokenMultiplierPercent=GameMasterData.ExpeditionInt(
                        prefix+"token_multiplier_percent",
                        fallback.multiplierPercent),
                    clearBonus=GameMasterData.ExpeditionInt(
                        prefix+"clear_bonus",fallback.clearBonus),
                    enemyHealthPercent=GameMasterData.ExpeditionInt(
                        prefix+"enemy_health_percent",fallback.enemyHealthPercent),
                    enemyAttackPercent=GameMasterData.ExpeditionInt(
                        prefix+"enemy_attack_percent",fallback.enemyAttackPercent),
                    ruleId=GameMasterData.ExpeditionString(
                        prefix+"rule_id",fallback.ruleId),
                    monsterIds=GameMasterData.ExpeditionString(
                        prefix+"monster_ids",fallback.monsterIds)
                });
            }
            if(Application.isPlaying)cachedRegions=result;
            return result;
        }
    }

    public static HunterRegionDefinition Get(string regionId)
    {
        IReadOnlyList<HunterRegionDefinition> regions=All;
        for(int index=0;index<regions.Count;index++)
            if(string.Equals(regions[index].id,regionId,StringComparison.Ordinal))
                return regions[index];
        return regions[0];
    }

    public static HunterRegionDefinition Next(string regionId)
    {
        IReadOnlyList<HunterRegionDefinition> regions=All;
        for(int index=0;index<regions.Count-1;index++)
        {
            if(string.Equals(
                regions[index].id,regionId,StringComparison.Ordinal))
                return regions[index+1];
        }
        return null;
    }

    public static float EnemyHealthMultiplier(string regionId)
    {
        return Mathf.Max(1,Get(regionId).enemyHealthPercent)/100f;
    }

    public static float EnemyAttackMultiplier(string regionId)
    {
        return Mathf.Max(1,Get(regionId).enemyAttackPercent)/100f;
    }

    public static float RegionDamageMultiplier(string regionId,string monsterId)
    {
        IReadOnlyList<HunterRegionDefinition> regions=All;
        int map=-1,origin=-1;
        for(int i=0;i<regions.Count;i++)
        {
            if(string.Equals(regions[i].id,regionId,StringComparison.Ordinal))map=i;
            if(regions[i].MonsterIds().Contains(monsterId))origin=i;
        }
        if(map<0||origin<0)return 1f;
        if(map==(origin+1)%regions.Count)
            return GameMasterData.ExpeditionInt("region_affinity_strong_damage_percent",120)/100f;
        if(map==(origin+regions.Count-1)%regions.Count)
            return GameMasterData.ExpeditionInt("region_affinity_weak_damage_percent",80)/100f;
        return 1f;
    }

    public static string RegionAffinityDescription(string regionId)
    {
        IReadOnlyList<HunterRegionDefinition> regions=All;
        int map=-1;
        for(int i=0;i<regions.Count;i++)
            if(string.Equals(regions[i].id,regionId,StringComparison.Ordinal))map=i;
        if(map<0)return string.Empty;
        return GameLocalization.Format("battle.region_affinity_summary",
            "{0} 출신 ×{1} · {2} 출신 ×{3}",
            GameLocalization.Text(regions[(map+regions.Count-1)%regions.Count].displayName),
            (GameMasterData.ExpeditionInt("region_affinity_strong_damage_percent",120)/100f).ToString("0.0"),
            GameLocalization.Text(regions[(map+1)%regions.Count].displayName),
            (GameMasterData.ExpeditionInt("region_affinity_weak_damage_percent",80)/100f).ToString("0.0"));
    }

    public static HunterRegionRule Rule(string regionId)
        =>Get(regionId).ParsedRule();

    public static float AllDamageTakenMultiplier(string regionId)
    {
        return Rule(regionId)==HunterRegionRule.CrimsonHeat?1.15f:1f;
    }

    public static float PeriodicRuleInterval(string regionId)
    {
        HunterRegionRule rule=Rule(regionId);
        return rule==HunterRegionRule.PoisonFog?8f:
            rule==HunterRegionRule.FrostWave?10f:0f;
    }

    public static string RuleDescription(string regionId)
    {
        switch(Rule(regionId))
        {
            case HunterRegionRule.PoisonFog:
                return "독안개 · 8초마다 모든 개체에게 독 1중첩";
            case HunterRegionRule.CrimsonHeat:
                return "붉은 열기 · 모든 개체가 받는 피해 +15%";
            case HunterRegionRule.FrostWave:
                return "한파 · 10초마다 모든 개체에게 냉기 1중첩";
            case HunterRegionRule.SkyResonance:
                return "천공 공명 · 전투 시작 시 스킬 게이지 +20%";
            default:
                return "순수 전장 · 환경 효과 없음";
        }
    }

    public static List<string> BuildWaveMonsterIds(
        string regionId,int wave,int count,int seed)
    {
        return BuildWaveMonsterIds(
            regionId,wave,count,seed,
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase"));
    }

    public static List<string> BuildExpeditionWaveMonsterIds(
        string regionId,int wave,int count,string expeditionRunId,
        MonsterDatabase database)
    {
        int seed=MonsterInstanceGenerator.StableSeed(
            (expeditionRunId??string.Empty)+"_wave_composition");
        return BuildWaveMonsterIds(regionId,wave,count,seed,database);
    }

    public static List<string> BuildWaveMonsterIds(
        string regionId,int wave,int count,int seed,MonsterDatabase database)
    {
        List<string> result=null;
        var generation=BuildWaveMonsterIdsRoutine(regionId,wave,count,seed,database,false,value=>result=value);
        while(generation.MoveNext()) { }
        return result;
    }

    public static IEnumerator BuildExpeditionWaveMonsterIdsAcrossFrames(
        string regionId,int wave,int count,string expeditionRunId,
        MonsterDatabase database,Action<List<string>> completed)
    {
        int seed=MonsterInstanceGenerator.StableSeed((expeditionRunId??string.Empty)+"_wave_composition");
        return BuildWaveMonsterIdsRoutine(regionId,wave,count,seed,database,true,completed);
    }

    private static IEnumerator BuildWaveMonsterIdsRoutine(
        string regionId,int wave,int count,int seed,MonsterDatabase database,
        bool acrossFrames,Action<List<string>> completed)
    {
        HunterRegionDefinition region=Get(regionId);
        List<string> pool=region.MonsterIds()
            .Where(id=>database==null||database.Get(id)?.enabled==true)
            .ToList();
        if(pool.Count==0||count<=0){completed(new List<string>());yield break;}

        // The lone preview and the final guardian come from the same seeded final roster.
        int compositionWave=wave==8?12:wave;
        int compositionCount=wave==8
            ?GameMasterData.WaveScaling(region.id,12)?.enemyCount??count
            :count;
        if(compositionCount<=0){completed(new List<string>());yield break;}
        System.Random random=new System.Random(
            seed^MonsterInstanceGenerator.StableSeed(regionId)^
            (compositionWave*unchecked((int)0x9E3779B9)));
        List<string> best=null;
        List<string> fallback=null;
        float bestDelta=float.MaxValue;
        float bestScore=float.MinValue;
        float fallbackDelta=float.MaxValue;
        float fallbackScore=float.MinValue;
        float target=CompositionTargetScore(regionId,compositionWave);
        int attempts=CompositionCandidateCount(regionId,compositionWave);
        for(int attempt=0;attempt<attempts;attempt++)
        {
            // Keep the seeded search identical while limiting work in a player preparation frame.
            if(acrossFrames&&attempt>0&&attempt%8==0)yield return null;
            List<string> candidate=new List<string>(compositionCount);
            while(candidate.Count<compositionCount)
            {
                List<string> shuffled=new List<string>(pool);
                for(int index=shuffled.Count-1;index>0;index--)
                {
                    int swap=random.Next(index+1);
                    string value=shuffled[index];
                    shuffled[index]=shuffled[swap];
                    shuffled[swap]=value;
                }
                candidate.AddRange(shuffled.Take(compositionCount-candidate.Count));
            }
            float score=CompositionScore(regionId,candidate,database);
            // Keep runs varied inside the same difficulty band instead of
            // always locking onto one statistically optimal composition.
            float delta=Mathf.Abs(score-target)+(float)random.NextDouble()*3f;
            if(delta<fallbackDelta||
               (Mathf.Approximately(delta,fallbackDelta)&&score>fallbackScore))
            {
                fallback=candidate;
                fallbackDelta=delta;
                fallbackScore=score;
            }
            if(CompositionComboLinkCount(candidate,database)>
               MaximumCompositionComboLinks(compositionWave))continue;
            if(delta>bestDelta||
               (Mathf.Approximately(delta,bestDelta)&&score<=bestScore))continue;
            best=candidate;
            bestDelta=delta;
            bestScore=score;
        }
        List<string> result=best??fallback??new List<string>();
        if((wave==8||wave==12)&&result.Count>0)
        {
            int bossIndex=PreviewBossIndex(result,database);
            string boss=result[bossIndex];
            result[bossIndex]=result[0];
            result[0]=boss;
            if(wave==8)result=new List<string>{boss};
        }
        completed(result);
    }

    private static int PreviewBossIndex(IReadOnlyList<string> roster,MonsterDatabase database)
    {
        for(int index=0;index<roster.Count;index++)
        {
            MonsterDefinition definition=database==null?null:database.Get(roster[index]);
            MonsterCombatTag tags=MonsterTraitCatalog.ResolveTags(definition);
            if((tags&(MonsterCombatTag.DamageSkill|MonsterCombatTag.SelfBuffSkill))!=0&&
               (tags&(MonsterCombatTag.Guardian|MonsterCombatTag.MammothFamily|
                       MonsterCombatTag.MushroomFamily|MonsterCombatTag.BeetleFamily))==0)return index;
        }
        // Conditional/support-only rosters keep their original guardian rather than an empty preview.
        return 0;
    }

    public static int CompositionCandidateCount(string regionId,int wave)
    {
        int level=Mathf.Max(0,Get(regionId).order-1);
        return 256+Mathf.Clamp(wave,1,12)*32+level*32;
    }

    public static float CompositionTargetScore(string regionId,int wave)
    {
        int[] targets={0,12,18,28,48,38,48,60,82,68,88,104,130};
        int index=Mathf.Clamp(wave,1,12);
        float regionRamp=Mathf.Clamp01((index-1)/5f);
        return targets[index]+Mathf.Max(0,Get(regionId).order-1)*4f*regionRamp;
    }

    public static int MaximumCompositionComboLinks(int wave)
    {
        int[] limits={0,0,0,0,1,1,1,2,3,2,3,4,5};
        return limits[Mathf.Clamp(wave,1,12)];
    }

    public static int CompositionComboLinkCount(
        IReadOnlyList<string> monsterIds,MonsterDatabase database)
    {
        if(monsterIds==null||database==null)return 0;
        List<MonsterComboMasterRow> combos=monsterIds
            .Select(database.Get).Where(value=>value!=null)
            .Select(value=>GameMasterData.MonsterCombo(value.monsterId))
            .Where(value=>value!=null&&value.enabled).ToList();
        int links=0;
        for(int consumerIndex=0;consumerIndex<combos.Count;consumerIndex++)
        foreach(string tag in combos[consumerIndex].ConsumerTagValues())
        {
            bool produced=false;
            for(int producerIndex=0;producerIndex<combos.Count;producerIndex++)
            {
                if(producerIndex==consumerIndex&&combos[consumerIndex].crossUnitOnly)
                    continue;
                if(!combos[producerIndex].ProducerTagValues().Contains(
                       tag,StringComparer.Ordinal))continue;
                produced=true;
                break;
            }
            if(produced)links++;
        }
        return links;
    }

    public static float CompositionScore(
        IReadOnlyList<string> monsterIds,MonsterDatabase database)
    {
        if(monsterIds==null||database==null)return 0f;
        List<MonsterDefinition> definitions=monsterIds
            .Select(database.Get).Where(value=>value!=null).ToList();
        float score=definitions.Count*4f+
            definitions.Select(value=>value.monsterId).Distinct().Count();
        score+=definitions
            .GroupBy(value=>value.familyId??string.Empty,StringComparer.Ordinal)
            .Sum(group=>group.Count()*(group.Count()-1)*1.5f);
        int melee=0,ranged=0,damage=0,guardian=0,status=0,area=0,elemental=0;
        foreach(MonsterDefinition definition in definitions)
        {
            MonsterCombatTag tags=MonsterTraitCatalog.ResolveTags(definition);
            if((tags&MonsterCombatTag.Melee)!=0)melee++;
            if((tags&MonsterCombatTag.Ranged)!=0)ranged++;
            if((tags&MonsterCombatTag.DamageSkill)!=0)damage++;
            if((tags&MonsterCombatTag.Guardian)!=0)guardian++;
            if((tags&MonsterCombatTag.StatusOrMarkSkill)!=0)status++;
            if((tags&MonsterCombatTag.AreaSkill)!=0)area++;
            if((tags&MonsterCombatTag.ElementalSkill)!=0)elemental++;
        }
        if(melee>0&&ranged>0)score+=12f+Mathf.Min(melee,ranged)*2f;
        score+=Mathf.Min(guardian,1)*damage*4f;
        score+=Mathf.Min(status,area)*3f;
        score+=Mathf.Min(elemental,status)*2f;
        List<MonsterComboMasterRow> combos=definitions
            .Select(value=>GameMasterData.MonsterCombo(value.monsterId))
            .Where(value=>value!=null&&value.enabled).ToList();
        for(int consumerIndex=0;consumerIndex<combos.Count;consumerIndex++)
        {
            MonsterComboMasterRow consumer=combos[consumerIndex];
            foreach(string tag in consumer.ConsumerTagValues())
            {
                int producers=0;
                for(int producerIndex=0;producerIndex<combos.Count;producerIndex++)
                {
                    if(producerIndex==consumerIndex&&consumer.crossUnitOnly)continue;
                    if(combos[producerIndex].ProducerTagValues().Contains(tag,
                           StringComparer.Ordinal))producers++;
                }
                if(producers>0)score+=7f+Mathf.Min(2,producers-1)*1.5f;
            }
        }
        return score;
    }

    public static float CompositionScore(
        string regionId,IReadOnlyList<string> monsterIds,
        MonsterDatabase database)
    {
        return CompositionScore(monsterIds,database)+
               MlCompositionSignalScore(regionId,monsterIds);
    }

    public static float MlCompositionSignalScore(
        string regionId,IReadOnlyList<string> monsterIds)
    {
        if(monsterIds==null||monsterIds.Count==0)return 0f;
        float score=0f;
        for(int index=0;index<monsterIds.Count;index++)
        {
            string monsterId=monsterIds[index];
            if(MlBalanceSignals.TryGetValue(
                   regionId+"|"+monsterId,out float unitLift))
                score+=Mathf.Clamp(unitLift,-4f,4f);
            for(int other=index+1;other<monsterIds.Count;other++)
            {
                string first=monsterId;
                string second=monsterIds[other];
                if(string.CompareOrdinal(first,second)>0)
                {
                    string swap=first;first=second;second=swap;
                }
                if(MlBalanceSignals.TryGetValue(
                       regionId+"|"+first+"|"+second,out float synergy))
                    score+=Mathf.Clamp(synergy,-4f,4f);
            }
        }
        return score;
    }
}

public static class ExpeditionDifficultyProgress
{
    public static void Normalize(PlayerProfileSave profile)
    {
        if(profile==null)return;
        if(profile.clearedRegionDifficultyKeys==null)
            profile.clearedRegionDifficultyKeys=new List<string>();
        HashSet<string> valid=new HashSet<string>(
            HunterRegionCatalog.All.SelectMany(region=>
                Enumerable.Range(0,4).Select(index=>Key(
                    region.id,(ExpeditionDifficulty)index))),
            StringComparer.Ordinal);
        profile.clearedRegionDifficultyKeys=
            profile.clearedRegionDifficultyKeys
                .Where(valid.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }

    public static bool IsCleared(
        PlayerProfileSave profile,string regionId,
        ExpeditionDifficulty difficulty)
    {
        return profile!=null&&profile.clearedRegionDifficultyKeys!=null&&
               profile.clearedRegionDifficultyKeys.Contains(
                   Key(regionId,difficulty));
    }

    public static bool IsUnlocked(
        PlayerProfileSave profile,string regionId,
        ExpeditionDifficulty difficulty)
    {
        difficulty=ExpeditionDifficultyRules.Clamp(difficulty);
        return difficulty==ExpeditionDifficulty.Beginner||
               IsCleared(profile,regionId,
                   (ExpeditionDifficulty)((int)difficulty-1));
    }

    public static ExpeditionDifficulty HighestUnlocked(
        PlayerProfileSave profile,string regionId)
    {
        ExpeditionDifficulty result=ExpeditionDifficulty.Beginner;
        for(int index=1;index<=3;index++)
        {
            ExpeditionDifficulty difficulty=(ExpeditionDifficulty)index;
            if(!IsUnlocked(profile,regionId,difficulty))break;
            result=difficulty;
        }
        return result;
    }

    public static bool RegisterClear(
        PlayerProfileSave profile,string regionId,
        ExpeditionDifficulty difficulty)
    {
        if(profile==null)return false;
        Normalize(profile);
        string key=Key(regionId,difficulty);
        if(profile.clearedRegionDifficultyKeys.Contains(key))return false;
        profile.clearedRegionDifficultyKeys.Add(key);
        return true;
    }

    private static string Key(
        string regionId,ExpeditionDifficulty difficulty)
    {
        return (regionId??string.Empty)+"|"+
               (int)ExpeditionDifficultyRules.Clamp(difficulty);
    }
}

public static class HunterTokenRewardSystem
{
    public static int Calculate(
        string regionId,int completedWave,int totalWaves,bool cleared)
    {
        HunterRegionDefinition region=HunterRegionCatalog.Get(regionId);
        int completed=Mathf.Clamp(completedWave,0,Mathf.Max(1,totalWaves));
        int reward=Mathf.FloorToInt(
            completed*region.tokenMultiplierPercent/100f);
        if(cleared&&completed>=Mathf.Max(1,totalWaves))
            reward+=Mathf.Max(0,region.clearBonus);
        return Mathf.Max(0,reward);
    }

    public static int Settle(
        PlayerProfileSave profile,
        PlayerProfileStore store,
        MonsterDatabase database,
        ExpeditionRunSave run,
        bool victory)
    {
        return Settle(
            profile,store,database,run,victory,out _);
    }

    public static int Settle(
        PlayerProfileSave profile,
        PlayerProfileStore store,
        MonsterDatabase database,
        ExpeditionRunSave run,
        bool victory,
        out int rareCaptureNets,bool save=true)
    {
        rareCaptureNets=0;
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(store==null)throw new ArgumentNullException(nameof(store));
        if(database==null)throw new ArgumentNullException(nameof(database));
        if(run==null||string.IsNullOrWhiteSpace(run.runId))
            throw new ArgumentException(
                "A valid expedition run is required.",nameof(run));

        HunterShopSystem.NormalizeProfile(profile,database);
        if(profile.settledExpeditionRunIds.Contains(run.runId))
            return 0;

        HunterShopSystem.QueueRegionBoxReward(profile,database,run,victory);
        int balanceBefore=profile.hunterTokens;
        int reward=Calculate(
            run.regionId,run.lastCompletedWave,
            12,victory);
        profile.hunterTokens=Math.Max(0,profile.hunterTokens+reward);
        rareCaptureNets=CalculateRareCaptureNetReward(run);
        if(rareCaptureNets>0)
            CaptureNetInventory.Grant(
                profile,CaptureNetCatalog.RareId,rareCaptureNets);
        profile.settledExpeditionRunIds.Add(run.runId);
        if(profile.settledExpeditionRunIds.Count>20)
            profile.settledExpeditionRunIds.RemoveRange(
                0,profile.settledExpeditionRunIds.Count-20);

        profile.hunterShopCycle=Math.Max(0,profile.hunterShopCycle)+1;
        profile.hunterFamilyPurchaseCount=0;
        profile.hunterFamilyStockIds.Clear();

        if(victory&&run.lastCompletedWave>=12)
        {
            ExpeditionDifficultyProgress.RegisterClear(
                profile,run.regionId,run.difficulty);
            HunterRegionDefinition next=
                HunterRegionCatalog.Next(run.regionId);
            if(next!=null&&!profile.unlockedRegionIds.Contains(next.id))
                profile.unlockedRegionIds.Add(next.id);
        }

        if(save)
        {
            store.Save(profile);
            HunterEconomyTelemetry.RecordExpeditionSettlement(
                profile,run,victory,reward,balanceBefore);
        }
        return reward;
    }

    public static int CalculateRareCaptureNetReward(ExpeditionRunSave run)
    {
        if(run==null||string.IsNullOrWhiteSpace(run.runId)||
           run.lastCompletedWave<=0)return 0;
        float chance=Mathf.Min(
            GameMasterData.CaptureFloat(
                "capture_net_rare_reward_chance_max",0.12f),
            Mathf.Max(0,run.lastCompletedWave)*
            GameMasterData.CaptureFloat(
                "capture_net_rare_reward_chance_per_wave",0.01f));
        int seed=MonsterInstanceGenerator.StableSeed(
            run.runId+"_rare_capture_net_reward");
        return new System.Random(seed).NextDouble()<chance?1:0;
    }
}

public static class HunterShopSystem
{
    private static readonly string[] FamilyOrder=
    {
        "raptor","sabertooth","frog","bear",
        "crocodile","arachne","mantis","mushroom",
        "kingkong","wolf","ninja_cat","monkey",
        "tyranno","mammoth","beetle","spirit_deer",
        "wizard_mouse","griffin","wyvern","haetae"
    };

    public static void NormalizeProfile(
        PlayerProfileSave profile,MonsterDatabase database)
    {
        if(profile==null)return;
        if(profile.unlockedRegionIds==null)
            profile.unlockedRegionIds=new List<string>();
        if(profile.encounteredMonsterIds==null)
            profile.encounteredMonsterIds=new List<string>();
        if(profile.hunterFamilyStockIds==null)
            profile.hunterFamilyStockIds=new List<string>();
        if(profile.notifiedHunterRegionBoxIds==null)
            profile.notifiedHunterRegionBoxIds=new List<string>();
        if(profile.notifiedHunterFamilyBoxIds==null)
            profile.notifiedHunterFamilyBoxIds=new List<string>();
        if(profile.settledExpeditionRunIds==null)
            profile.settledExpeditionRunIds=new List<string>();
        if(profile.pendingRegionBoxRewards==null)
            profile.pendingRegionBoxRewards=new List<RegionBoxRewardSave>();
        ExpeditionDifficultyProgress.Normalize(profile);

        if(!profile.unlockedRegionIds.Contains(HunterRegionCatalog.FirstRegionId))
            profile.unlockedRegionIds.Insert(0,HunterRegionCatalog.FirstRegionId);
        HashSet<string> knownRegions=new HashSet<string>(
            HunterRegionCatalog.All.Select(region=>region.id),
            StringComparer.Ordinal);
        profile.unlockedRegionIds=profile.unlockedRegionIds
            .Where(id=>!string.IsNullOrWhiteSpace(id)&&knownRegions.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id=>HunterRegionCatalog.Get(id).order)
            .ToList();
        profile.notifiedHunterRegionBoxIds=
            profile.notifiedHunterRegionBoxIds
                .Where(id=>!string.IsNullOrWhiteSpace(id)&&
                    knownRegions.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        if(database!=null)
        {
            HashSet<string> validMonsters=new HashSet<string>(
                database.monsters.Where(definition=>
                    definition!=null&&definition.enabled)
                    .Select(definition=>definition.monsterId),
                StringComparer.Ordinal);
            foreach(OwnedMonsterSave owned in
                    profile.monsters??new List<OwnedMonsterSave>())
            {
                if(!profile.starterSelectionPending&&owned!=null&&validMonsters.Contains(owned.monsterId)&&
                   !profile.encounteredMonsterIds.Contains(owned.monsterId))
                    profile.encounteredMonsterIds.Add(owned.monsterId);
            }
            profile.encounteredMonsterIds=profile.encounteredMonsterIds
                .Where(id=>!string.IsNullOrWhiteSpace(id)&&validMonsters.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        HashSet<string> validFamilies=new HashSet<string>(
            FamilyOrder,StringComparer.Ordinal);
        profile.notifiedHunterFamilyBoxIds=
            profile.notifiedHunterFamilyBoxIds
                .Where(id=>!string.IsNullOrWhiteSpace(id)&&
                    validFamilies.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        profile.hunterFamilyStockIds=profile.hunterFamilyStockIds
            .Where(id=>!string.IsNullOrWhiteSpace(id)&&validFamilies.Contains(id)&&
                (database==null||HasEncounteredWholeFamily(
                    profile,database,id)))
            .Distinct(StringComparer.Ordinal)
            .Take(FamilyStockCount)
            .ToList();
        List<string> settled=profile.settledExpeditionRunIds
            .Where(id=>!string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if(settled.Count>20)
            settled.RemoveRange(0,settled.Count-20);
        profile.settledExpeditionRunIds=settled;
        profile.hunterShopCycle=Math.Max(0,profile.hunterShopCycle);
        profile.hunterFamilyPurchaseCount=
            Math.Max(0,profile.hunterFamilyPurchaseCount);
    }

    public static int RegionBoxRewardChoiceCount(ExpeditionDifficulty difficulty)
    {
        return (int)ExpeditionDifficultyRules.Clamp(difficulty)+1;
    }

    internal static void QueueRegionBoxReward(
        PlayerProfileSave profile,MonsterDatabase database,
        ExpeditionRunSave run,bool victory)
    {
        if(!victory||run.lastCompletedWave<12||
           run.status==ExpeditionRunStatus.Failed||
           run.status==ExpeditionRunStatus.Abandoned||
           profile.pendingRegionBoxRewards.Any(item=>
               item!=null&&item.runId==run.runId))return;

        List<MonsterDefinition> pool=RegionBoxContents(database,run.regionId);
        if(pool.Count==0)
            throw new InvalidOperationException("The clear reward region has no available monsters.");
        RegionBoxRewardSave reward=new RegionBoxRewardSave
        {
            runId=run.runId,
            regionId=HunterRegionCatalog.Get(run.regionId).id,
            difficulty=ExpeditionDifficultyRules.Clamp(run.difficulty)
        };
        System.Random random=new System.Random(MonsterInstanceGenerator.StableSeed(
            profile.profileId+"_clear_box_"+run.runId));
        for(int index=0;index<RegionBoxRewardChoiceCount(reward.difficulty);index++)
        {
            MonsterDefinition definition=pool[random.Next(pool.Count)];
            // The run ID fixes every roll even if settlement resumes after a failed save.
            string instanceId="clear_box_"+run.runId+"_"+index+"_"+definition.monsterId;
            OwnedMonsterSave candidate=PlayerProfileStore.CreateMonster(definition,instanceId);
            candidate.isNew=true;
            reward.candidates.Add(candidate);
        }
        profile.pendingRegionBoxRewards.Add(reward);
    }

    public static bool TryClaimRegionBoxReward(
        PlayerProfileSave profile,PlayerProfileStore store,MonsterDatabase database,
        string runId,int candidateIndex,out OwnedMonsterSave acquired,out string error)
    {
        acquired=null;
        error="";
        if(profile==null||store==null||database==null)
        {
            error="프로필을 불러오지 못했습니다.";
            return false;
        }
        RegionBoxRewardSave reward=string.IsNullOrWhiteSpace(runId)?null:
            profile.pendingRegionBoxRewards?.Find(item=>item!=null&&item.runId==runId);
        if(reward==null||reward.candidates==null||candidateIndex<0||
           candidateIndex>=reward.candidates.Count)
        {
            error="선택할 탐험 보상을 찾지 못했습니다.";
            return false;
        }
        OwnedMonsterSave candidate=reward.candidates[candidateIndex];
        if(candidate==null||string.IsNullOrWhiteSpace(candidate.instanceId)||
           database.Get(candidate.monsterId)?.enabled!=true||
           profile.monsters?.Any(monster=>monster!=null&&monster.instanceId==candidate.instanceId)==true)
        {
            error="보상 몬스터 데이터를 확인하지 못했습니다.";
            return false;
        }
        try
        {
            // Grant and consume are one profile commit; failed writes leave the choice available.
            PlayerProfileSave staged=JsonUtility.FromJson<PlayerProfileSave>(JsonUtility.ToJson(profile));
            RegionBoxRewardSave stagedReward=staged.pendingRegionBoxRewards.Find(item=>
                item!=null&&item.runId==runId);
            OwnedMonsterSave selected=stagedReward.candidates[candidateIndex];
            staged.pendingRegionBoxRewards.Remove(stagedReward);
            store.AddGeneratedMonster(staged,database,selected,false);
            store.Save(staged);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(staged),profile);
            acquired=profile.monsters.Find(monster=>monster!=null&&monster.instanceId==selected.instanceId);
            return true;
        }
        catch(Exception exception)
        {
            error=exception.Message;
            return false;
        }
    }

    public static bool RegisterEncounters(
        PlayerProfileSave profile,
        PlayerProfileStore store,
        MonsterDatabase database,
        IEnumerable<string> monsterIds,bool acrossFrames=false)
    {
        if(profile==null||store==null||database==null||monsterIds==null)
            return false;
        NormalizeProfile(profile,database);
        HashSet<string> valid=new HashSet<string>(
            database.monsters.Where(definition=>
                definition!=null&&definition.enabled)
                .Select(definition=>definition.monsterId),
            StringComparer.Ordinal);
        bool changed=false;
        foreach(string monsterId in monsterIds)
        {
            if(string.IsNullOrWhiteSpace(monsterId)||
               !valid.Contains(monsterId)||
               profile.encounteredMonsterIds.Contains(monsterId))
                continue;
            profile.encounteredMonsterIds.Add(monsterId);
            changed=true;
        }
        if(changed)
        {
            if(acrossFrames)store.SaveAcrossFrames(profile);
            else store.Save(profile);
        }
        return changed;
    }

    public static bool EnsureFamilyStock(
        PlayerProfileSave profile,MonsterDatabase database)
    {
        NormalizeProfile(profile,database);
        if(profile.hunterFamilyStockIds.Count>0)return false;

        List<string> eligible=EligibleFamilies(profile,database);
        System.Random random=new System.Random(
            MonsterInstanceGenerator.StableSeed(
                profile.profileId+"_hunter_shop_"+profile.hunterShopCycle));
        for(int index=eligible.Count-1;index>0;index--)
        {
            int swap=random.Next(index+1);
            string value=eligible[index];
            eligible[index]=eligible[swap];
            eligible[swap]=value;
        }
        profile.hunterFamilyStockIds.AddRange(
            eligible.Take(FamilyStockCount));
        return profile.hunterFamilyStockIds.Count>0;
    }

    public static List<string> EligibleFamilies(
        PlayerProfileSave profile,MonsterDatabase database)
    {
        if(profile==null||database==null)return new List<string>();
        return FamilyOrder.Where(family=>
            HasEncounteredWholeFamily(profile,database,family)).ToList();
    }

    public static bool HasEncounteredWholeFamily(
        PlayerProfileSave profile,MonsterDatabase database,string familyId)
    {
        if(profile==null||database==null||string.IsNullOrWhiteSpace(familyId))
            return false;
        List<string> species=FamilyBoxContents(database,familyId)
            .Select(definition=>definition.monsterId).ToList();
        if(species.Count==0)return false;
        HashSet<string> encountered=new HashSet<string>(
            profile.encounteredMonsterIds??new List<string>(),
            StringComparer.Ordinal);
        return species.All(encountered.Contains);
    }

    public static List<MonsterDefinition> RegionBoxContents(
        MonsterDatabase database,string regionId)
    {
        if(database==null)return new List<MonsterDefinition>();
        return HunterRegionCatalog.Get(regionId).MonsterIds()
            .Select(database.Get)
            .Where(definition=>definition!=null&&definition.enabled)
            .ToList();
    }

    public static List<MonsterDefinition> FamilyBoxContents(
        MonsterDatabase database,string familyId)
    {
        if(database==null||string.IsNullOrWhiteSpace(familyId))
            return new List<MonsterDefinition>();
        return database.monsters
            .Where(definition=>definition!=null&&definition.enabled&&
                string.Equals(
                    definition.familyId,familyId,StringComparison.Ordinal))
            .OrderBy(definition=>definition.serialNumber)
            .ToList();
    }

    public static bool IsRegionBoxUnlocked(
        PlayerProfileSave profile,string regionId)
    {
        return ExpeditionDifficultyProgress.IsCleared(
            profile,regionId,ExpeditionDifficulty.Beginner);
    }

    public static int CurrentFamilyBoxCost(PlayerProfileSave profile)
    {
        int purchases=profile==null
            ?0:Math.Max(0,profile.hunterFamilyPurchaseCount);
        int index=Math.Min(3,purchases);
        int[] fallback={30,38,48,60};
        return GameMasterData.ExpeditionInt(
            "hunter_family_box_cost_"+index,fallback[index]);
    }

    public static int FamilyStockCount=>
        Mathf.Max(1,GameMasterData.ExpeditionInt(
            "hunter_family_stock_count",3));

    public static int MinimumEncounteredSpecies=>
        Mathf.Max(1,GameMasterData.ExpeditionInt(
            "hunter_family_min_encountered_species",4));

    public static bool TryPurchaseCaptureNet(
        PlayerProfileSave profile,
        PlayerProfileStore store,
        out string error)
    {
        error="";
        if(profile==null||store==null)
        {
            error="프로필을 불러오지 못했습니다.";
            return false;
        }
        int cost=CaptureNetCatalog.TokenCost;
        if(profile.hunterTokens<cost)
        {
            error="사냥의 증표가 부족합니다.";
            return false;
        }
        profile.hunterTokens-=cost;
        store.GrantCaptureNet(profile,CaptureNetCatalog.StandardId,1);
        return true;
    }

    public static bool TryPurchaseRegionBox(
        PlayerProfileSave profile,
        PlayerProfileStore store,
        MonsterDatabase database,
        string regionId,
        out OwnedMonsterSave acquired,
        out string error,
        System.Random random=null)
    {
        acquired=null;
        error="";
        if(profile==null||store==null||database==null)
        {
            error="프로필을 불러오지 못했습니다.";
            return false;
        }
        NormalizeProfile(profile,database);
        HunterRegionDefinition region=HunterRegionCatalog.Get(regionId);
        if(!IsRegionBoxUnlocked(profile,region.id))
        {
            error="해당 지역 초보 난이도를 먼저 클리어해야 합니다.";
            return false;
        }
        List<string> pool=RegionBoxContents(database,region.id)
            .Select(definition=>definition.monsterId).ToList();
        if(pool.Count==0)
        {
            error="상자에 등록된 몬스터가 없습니다.";
            return false;
        }
        return TryPurchase(
            profile,store,database,
            pool[(random??NewRandom()).Next(pool.Count)],
            Mathf.Max(0,region.regionBoxCost),
            false,"region",region.id,
            out acquired,out error);
    }

    public static bool TryPurchaseFamilyBox(
        PlayerProfileSave profile,
        PlayerProfileStore store,
        MonsterDatabase database,
        string familyId,
        out OwnedMonsterSave acquired,
        out string error,
        System.Random random=null)
    {
        acquired=null;
        error="";
        if(profile==null||store==null||database==null)
        {
            error="프로필을 불러오지 못했습니다.";
            return false;
        }
        NormalizeProfile(profile,database);
        if(!profile.hunterFamilyStockIds.Contains(familyId))
        {
            error="현재 진열되지 않은 계열 상자입니다.";
            return false;
        }
        List<MonsterDefinition> pool=FamilyBoxContents(database,familyId);
        if(!HasEncounteredWholeFamily(profile,database,familyId))
        {
            error="해당 계열의 일반 개체를 모두 조우해야 합니다.";
            return false;
        }
        int cost=CurrentFamilyBoxCost(profile);
        MonsterDefinition selected=pool[(random??NewRandom()).Next(pool.Count)];
        return TryPurchase(
            profile,store,database,selected.monsterId,cost,
            true,"family",familyId,
            out acquired,out error);
    }

    public static string FamilyDisplayName(string familyId)
    {
        string korean;
        switch(familyId)
        {
            case "arachne": korean="아라크네"; break;
            case "raptor": korean="랩터"; break;
            case "sabertooth": korean="검치호"; break;
            case "frog": korean="개구리"; break;
            case "kingkong": korean="킹콩"; break;
            case "jaguar": korean="재규어"; break;
            case "crocodile": korean="악어"; break;
            case "giraffe": korean="기린"; break;
            case "bear": korean="곰"; break;
            case "beetle": korean="갑충"; break;
            case "griffin": korean="그리폰"; break;
            case "haetae": korean="해태"; break;
            case "mammoth": korean="매머드"; break;
            case "mantis": korean="사마귀"; break;
            case "monkey": korean="원숭이"; break;
            case "mushroom": korean="버섯"; break;
            case "ninja_cat": korean="닌자고양이"; break;
            case "spider": korean="거미"; break;
            case "spirit_deer": korean="정령사슴"; break;
            case "tyranno": korean="티라노"; break;
            case "wizard_mouse": korean="마법쥐"; break;
            case "wolf": korean="늑대"; break;
            case "wyvern": korean="와이번"; break;
            default: return familyId??"";
        }
        return GameLocalization.TextByKey("family."+familyId,korean);
    }

    private static bool TryPurchase(
        PlayerProfileSave profile,
        PlayerProfileStore store,
        MonsterDatabase database,
        string monsterId,
        int cost,
        bool familyBox,
        string boxType,
        string sourceId,
        out OwnedMonsterSave acquired,
        out string error)
    {
        acquired=null;
        error="";
        if(profile.hunterTokens<cost)
        {
            error="사냥의 증표가 부족합니다.";
            return false;
        }
        MonsterDefinition definition=database.Get(monsterId);
        if(definition==null||!definition.enabled)
        {
            error="상자 몬스터 데이터를 찾지 못했습니다.";
            return false;
        }

        int balanceBefore=profile.hunterTokens;
        bool speciesAlreadyOwned=profile.monsters.Any(monster=>
            monster!=null&&string.Equals(
                monster.monsterId,monsterId,StringComparison.Ordinal));
        profile.hunterTokens-=cost;
        if(familyBox)profile.hunterFamilyPurchaseCount++;
        try
        {
            string instanceId="hunter_box_"+monsterId+"_"+
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"_"+
                Guid.NewGuid().ToString("N").Substring(0,6);
            acquired=PlayerProfileStore.CreateMonster(
                definition,instanceId);
            acquired.isNew=true;
            store.AddGeneratedMonster(profile,database,acquired);
            HunterEconomyTelemetry.RecordBoxPurchase(
                profile,boxType,sourceId,cost,balanceBefore,
                acquired,speciesAlreadyOwned);
            return true;
        }
        catch(Exception exception)
        {
            profile.hunterTokens+=cost;
            if(familyBox)
                profile.hunterFamilyPurchaseCount=
                    Math.Max(0,profile.hunterFamilyPurchaseCount-1);
            store.Save(profile);
            error=exception.Message;
            acquired=null;
            return false;
        }
    }

    private static System.Random NewRandom()
    {
        return new System.Random(Guid.NewGuid().GetHashCode());
    }
}
