using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class MasterConfigRow
{
    public string key;
    public string valueType;
    public string value;
    public string description;
    public bool enabled;
    public int dataVersion;
}

[Serializable] public sealed class DropMasterRow
{
    public string id;
    public string rewardType;
    public int minWave;
    public int maxWave;
    public float chance;
    public int minAmount;
    public int maxAmount;
    public string itemPoolId;
    public string commitRule;
    public bool enabled;
    public int dataVersion;
}

[Serializable] public sealed class SkillMasterRow
{
    public string id;
    public string displayName;
    public string behavior;
    public float baseCooldown;
    public float damageMultiplier;
    public float radiusTiles;
    public float durationSeconds;
    public int maxStacks;
    public float stackBonus;
    public float secondaryDamageMultiplier;
    public float casterMaxHpRatio;
    public float targetCurrentHpRatio;
    public float targetMissingHpRatio;
    public float hpScalingCapAttackMultiplier;
    public float defenseIgnoreRatio;
    public bool hpScalingTrueDamage;
    public float damagePerStackMultiplier;
    public string statusEffect;
    public float statusValue;
    public float statusDurationSeconds;
    public float castRangeTiles;
    public string damageFormulaNote;
    public string implementationStatus;
    public bool enabled;
    public int dataVersion;
}

[Serializable] public sealed class GrowthCardRarityMasterRow
{
    public int minWave;
    public int maxWave;
    public float commonWeight;
    public float rareWeight;
    public float epicWeight;
    public float legendaryWeight;
    public bool enabled;
    public int dataVersion;
}

[Serializable] public sealed class MonsterGrowthMasterRow
{
    public string monsterId;
    public string displayName;
    public string familyId;
    public string growthRole;
    public int hpWeight;
    public int attackWeight;
    public int attackSpeedWeight;
    public int defenseWeight;
    public int statusResistanceWeight;
    public int weightTotal=100;
    public int rank2RequiredTotalGrowth=3;
    public int rank3RequiredTotalGrowth=9;
    public int rank2BonusGrowthCount=3;
    public int rank3BonusGrowthCount=6;
    public bool moveSpeedGrowthEnabled;
    public string growthGradeBasis;
    public string fractionalRule;
    public int hpPointsPerStat=5;
    public int attackPointsPerStat=20;
    public int attackSpeedPointsPerStat=20;
    public int defensePointsPerStat=20;
    public int statusResistancePointsPerStat=20;
    public string implementationStatus;
    public string balanceNote;
    public string skillScalingStat="Attack";
    public float skillScalingRatio=1f;
    public int scalingVersion;
    public string fixedRank2TraitId;
    public string fixedRank3TraitId;
    public int fixedTraitVersion;
}

[Serializable] public sealed class GrowthGradeMasterRow
{
    public string growthGrade;
    public int totalGrowthValue;
    public bool enabled;
    public string calculationBasis;
    public string note;
}

[Serializable] public sealed class WaveScalingMasterRow
{
    public string regionId;
    public int wave;
    public int enemyCount;
    public string enemyMonsterIds;
    public float hpMultiplier=1f;
    public float attackDamageMultiplier=1f;
    public float attackSpeedMultiplier=1f;
    public float moveSpeedMultiplier=1f;
    public string threatLabel;
    public bool enabled;
    public int dataVersion;

    public List<string> MonsterIds()
    {
        return string.IsNullOrWhiteSpace(enemyMonsterIds)
            ?new List<string>()
            :enemyMonsterIds.Split('|').Select(id=>id.Trim())
                .Where(id=>id.Length>0).ToList();
    }
}

[Serializable] public sealed class PermanentResearchMasterRow
{
    public string id;
    public string branch;
    public int displayOrder;
    public string nameKey;
    public string descriptionKey;
    public int maxRank;
    public float valuePerRank;
    public string valueKind;
    public bool capstone;
    public bool enabled;
    public int dataVersion;
}

[Serializable] public sealed class MonsterComboMasterRow
{
    public string monsterId;
    public string producerTags;
    public string consumerTags;
    public bool crossUnitOnly;
    public bool enabled;
    public int dataVersion;

    public IEnumerable<string> ProducerTagValues()=>TagValues(producerTags);
    public IEnumerable<string> ConsumerTagValues()=>TagValues(consumerTags);

    private static IEnumerable<string> TagValues(string raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ?Enumerable.Empty<string>()
            :raw.Split('|').Select(value=>value.Trim())
                .Where(value=>value.Length>0).Distinct(StringComparer.Ordinal);
    }
}

[Serializable] public sealed class TraitMasterRow
{
    public string id;
    public string displayName;
    public string requiredTags;
    public string category;
    public string triggerRule;
    public string effectSummary;
    public bool enabled;
    public int dataVersion;
}

[Serializable] public sealed class PersonalityMasterRow
{
    public string id;
    public string displayName;
    public string resolutionMode;
    public float courageWeight;
    public float aggressionWeight;
    public float disciplineWeight;
    public float composureWeight;
    public float cooperationWeight;
    public float adaptabilityWeight;
    public float lowCourageWeight;
    public float lowAggressionWeight;
    public float lowDisciplineWeight;
    public float lowComposureWeight;
    public float lowCooperationWeight;
    public float lowAdaptabilityWeight;
    public int priority;
    public string behaviorSummary;
    public bool enabled;
    public int dataVersion;
}

[Serializable] public sealed class ItemMasterRow
{
    public string id;
    public string displayName;
    public string itemKind;
    public string componentAId;
    public string componentBId;
    public string itemPoolId;
    public float attackDamageBonus;
    public float attackSpeedBonus;
    public float maxHealthBonus;
    public float damageReductionBonus;
    public float criticalChance;
    public float criticalDamageBonus;
    public float skillPowerBonus;
    public float defenseBonus;
    public float statusResistanceBonus;
    public float skillCooldownReductionBonus;
    public string specialEffect;
    public float effectParam1;
    public float effectParam2;
    public float effectParam3;
    public bool enabled;
    public int dataVersion;

    public bool IsBase
    {
        get{return string.Equals(itemKind,"Base",StringComparison.OrdinalIgnoreCase);}
    }

    public bool IsCompleted
    {
        get{return string.Equals(itemKind,"Completed",StringComparison.OrdinalIgnoreCase);}
    }
}

[Serializable] public sealed class AugmentMasterRow
{
    public string id;
    public string displayName;
    public string category;
    public string offerWaves;
    public string selectionMode;
    public string targetRule;
    public string effectSummary;
    public string triggerRule;
    public string numericParams;
    public string durationRule;
    public string grantedItemId;
    public string conflictIds;
    public string eligibilityRule;
    public string retryRule;
    public bool enabled;
    public int dataVersion;

    public bool IsOfferedAt(int wave)
    {
        if(string.IsNullOrWhiteSpace(offerWaves))return false;
        return offerWaves.Split('|').Any(value=>
            int.TryParse(value.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int parsed)&&
            parsed==wave);
    }

    public IEnumerable<string> ConflictIds()
    {
        return string.IsNullOrWhiteSpace(conflictIds)
            ?Enumerable.Empty<string>()
            :conflictIds.Split('|').Select(value=>value.Trim()).Where(value=>value.Length>0);
    }

    public float NumericParam(string key,float fallback=0f)
    {
        if(string.IsNullOrWhiteSpace(key)||string.IsNullOrWhiteSpace(numericParams))
            return fallback;
        foreach(string token in numericParams.Split('|'))
        {
            int separator=token.IndexOf('=');
            if(separator<=0||!string.Equals(token.Substring(0,separator).Trim(),
                   key,StringComparison.OrdinalIgnoreCase))continue;
            return float.TryParse(token.Substring(separator+1).Trim(),
                NumberStyles.Float,CultureInfo.InvariantCulture,out float value)
                ?value:fallback;
        }
        return fallback;
    }
}

[Serializable] public sealed class ItemTransformMasterRow
{
    public string itemId;
    public string displayName;
    public string overchargeEffect;
    public string overchargeParams;
    public string corruptionGreatName;
    public string corruptionGreatEffectId;
    public string corruptionGreatEffect;
    public string corruptionGreatParams;
    public string corruptionSuccessEffect;
    public string corruptionNoChangeEffect;
    public string corruptionFailureEffect;
    public string corruptionFailureParams;
    public int greatSuccessPercent;
    public int successPercent;
    public int noChangePercent;
    public int failurePercent;
    public string transformLockRule;
    public bool enabled;
    public int dataVersion;

    public float GreatParam(string key,float fallback=0f)
    {
        string raw=GreatParamRaw(key);
        return float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,
            out float value)?value:fallback;
    }

    public int GreatParamInt(string key,int fallback=0)
    {
        string raw=GreatParamRaw(key);
        return int.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,
            out int value)?value:fallback;
    }

    public bool GreatParamBool(string key,bool fallback=false)
    {
        string raw=GreatParamRaw(key);
        return bool.TryParse(raw,out bool value)?value:fallback;
    }

    public string GreatParamRaw(string key,string fallback="")
    {
        if(string.IsNullOrWhiteSpace(key)||string.IsNullOrWhiteSpace(
            corruptionGreatParams))return fallback;
        foreach(string token in corruptionGreatParams.Split('|'))
        {
            int separator=token.IndexOf('=');
            if(separator<=0)continue;
            if(!string.Equals(token.Substring(0,separator).Trim(),key,
                StringComparison.OrdinalIgnoreCase))continue;
            return token.Substring(separator+1).Trim();
        }
        return fallback;
    }
}

[CreateAssetMenu(menuName="BombBrick/Game Master Database")]
public sealed class GameMasterDatabase : ScriptableObject
{
    public List<DropMasterRow> drops=new List<DropMasterRow>();
    public List<SkillMasterRow> skills=new List<SkillMasterRow>();
    public List<MasterConfigRow> expeditionConfigs=new List<MasterConfigRow>();
    public List<GrowthCardRarityMasterRow> growthCardRarities=new List<GrowthCardRarityMasterRow>();
    public List<MonsterGrowthMasterRow> monsterGrowths=new List<MonsterGrowthMasterRow>();
    public List<GrowthGradeMasterRow> growthGrades=new List<GrowthGradeMasterRow>();
    public List<WaveScalingMasterRow> waveScalings=new List<WaveScalingMasterRow>();
    public List<PermanentResearchMasterRow> permanentResearch=
        new List<PermanentResearchMasterRow>();
    public List<MonsterComboMasterRow> monsterCombos=new List<MonsterComboMasterRow>();
    public List<TraitMasterRow> traits=new List<TraitMasterRow>();
    public List<PersonalityMasterRow> personalities=new List<PersonalityMasterRow>();
    public List<MasterConfigRow> monsterGenerationConfigs=new List<MasterConfigRow>();
    public List<ItemMasterRow> items=new List<ItemMasterRow>();
    public List<MasterConfigRow> captureConfigs=new List<MasterConfigRow>();
    public List<AugmentMasterRow> augments=new List<AugmentMasterRow>();
    public List<ItemTransformMasterRow> itemTransforms=new List<ItemTransformMasterRow>();
}

public static class GameMasterData
{
    private const string ResourcePath="CombatData/GameMasterDatabase";
    private static GameMasterDatabase cached;

    public static GameMasterDatabase Database
    {
        get
        {
            if(cached==null)cached=Resources.Load<GameMasterDatabase>(ResourcePath);
            return cached;
        }
    }

    public static void ClearCache()
    {
        cached=null;
        MonsterTraitCatalog.ClearCache();
        HunterRegionCatalog.ClearCache();
    }

    public static int ExpeditionInt(string key,int fallback)
    {
        return ConfigInt(Database==null?null:Database.expeditionConfigs,key,fallback);
    }

    public static bool ExpeditionBool(string key,bool fallback)
    {
        return ConfigBool(Database==null?null:Database.expeditionConfigs,key,fallback);
    }

    public static string ExpeditionString(string key,string fallback)
    {
        MasterConfigRow row=FindConfig(Database==null?null:Database.expeditionConfigs,key);
        return row==null||string.IsNullOrWhiteSpace(row.value)?fallback:row.value;
    }

    public static int GenerationInt(string key,int fallback)
    {
        return ConfigInt(Database==null?null:Database.monsterGenerationConfigs,key,fallback);
    }

    public static float CaptureFloat(string key,float fallback)
    {
        MasterConfigRow row=FindConfig(
            Database==null?null:Database.captureConfigs,key);
        return row!=null&&float.TryParse(
            row.value,NumberStyles.Float,CultureInfo.InvariantCulture,
            out float value)
            ?value:fallback;
    }

    public static SkillMasterRow Skill(string id)
    {
        var rows=Database==null?null:Database.skills;
        if(rows!=null)
            foreach(var row in rows)
                if(row!=null&&row.enabled&&string.Equals(row.id,id,StringComparison.Ordinal))return row;
        return null;
    }

    public static PersonalityMasterRow Personality(string id)
    {
        return Database==null||Database.personalities==null
            ?null
            :Database.personalities.FirstOrDefault(row=>row!=null&&row.enabled&&
                string.Equals(row.id,id,StringComparison.Ordinal));
    }

    public static ItemMasterRow Item(string id)
    {
        return Database==null||Database.items==null
            ?null
            :Database.items.FirstOrDefault(row=>row!=null&&row.enabled&&
                string.Equals(row.id,id,StringComparison.Ordinal));
    }

    public static AugmentMasterRow Augment(string id)
    {
        return Database==null||Database.augments==null
            ?null
            :Database.augments.FirstOrDefault(row=>row!=null&&row.enabled&&
                string.Equals(row.id,id,StringComparison.Ordinal));
    }

    public static IReadOnlyList<AugmentMasterRow> Augments
    {
        get
        {
            return Database==null||Database.augments==null
                ?Array.Empty<AugmentMasterRow>()
                :Database.augments.Where(row=>row!=null&&row.enabled).ToList();
        }
    }

    public static ItemTransformMasterRow ItemTransform(string itemId)
    {
        return Database==null||Database.itemTransforms==null
            ?null
            :Database.itemTransforms.FirstOrDefault(row=>row!=null&&row.enabled&&
                string.Equals(row.itemId,itemId,StringComparison.Ordinal));
    }

    public static ItemMasterRow ItemRecipe(string componentAId,string componentBId)
    {
        if(Database==null||Database.items==null)return null;
        return Database.items.FirstOrDefault(row=>row!=null&&row.enabled&&row.IsCompleted&&
            ((string.Equals(row.componentAId,componentAId,StringComparison.Ordinal)&&
              string.Equals(row.componentBId,componentBId,StringComparison.Ordinal))||
             (string.Equals(row.componentAId,componentBId,StringComparison.Ordinal)&&
              string.Equals(row.componentBId,componentAId,StringComparison.Ordinal))));
    }

    public static List<ItemMasterRow> ItemsInPool(string poolId)
    {
        return Database==null||Database.items==null
            ?new List<ItemMasterRow>()
            :Database.items.Where(row=>row!=null&&row.enabled&&row.IsBase&&
                string.Equals(row.itemPoolId,poolId,StringComparison.Ordinal)).ToList();
    }

    public static GrowthCardRarityMasterRow GrowthRarity(int wave)
    {
        return Database==null||Database.growthCardRarities==null
            ?null
            :Database.growthCardRarities.FirstOrDefault(row=>row!=null&&row.enabled&&
                wave>=row.minWave&&wave<=row.maxWave);
    }

    public static MonsterGrowthMasterRow MonsterGrowth(string monsterId)
    {
        return Database==null||Database.monsterGrowths==null
            ?null
            :Database.monsterGrowths.FirstOrDefault(row=>row!=null&&
                string.Equals(row.monsterId,monsterId,StringComparison.Ordinal));
    }

    public static int GrowthTotalValue(string grade,int fallback=100)
    {
        GrowthGradeMasterRow row=Database==null||Database.growthGrades==null
            ?null
            :Database.growthGrades.FirstOrDefault(value=>value!=null&&value.enabled&&
                string.Equals(value.growthGrade,grade,StringComparison.OrdinalIgnoreCase));
        return row==null?fallback:Math.Max(1,row.totalGrowthValue);
    }

    public static WaveScalingMasterRow WaveScaling(string regionId,int wave)
    {
        if(Database==null||Database.waveScalings==null)return null;
        if(string.IsNullOrWhiteSpace(regionId))
            return Database.waveScalings.FirstOrDefault(row=>
                row!=null&&row.enabled&&row.wave==wave);
        return Database.waveScalings.FirstOrDefault(row=>
                   row!=null&&row.enabled&&row.wave==wave&&
                   string.Equals(row.regionId,regionId,StringComparison.Ordinal))??
               Database.waveScalings.FirstOrDefault(row=>
                   row!=null&&row.enabled&&row.wave==wave&&
                   string.IsNullOrWhiteSpace(row.regionId));
    }

    public static WaveScalingMasterRow WaveScaling(int wave)=>WaveScaling(null,wave);

    public static IReadOnlyList<PermanentResearchMasterRow> PermanentResearch
    {
        get
        {
            return Database==null||Database.permanentResearch==null
                ?Array.Empty<PermanentResearchMasterRow>()
                :Database.permanentResearch.Where(row=>row!=null&&row.enabled&&
                    row.id!=PermanentResearchSystem.AdvanceScouting)
                    .OrderBy(row=>row.branch)
                    .ThenBy(row=>row.displayOrder).ToList();
        }
    }

    public static PermanentResearchMasterRow Research(string id)
    {
        return PermanentResearch.FirstOrDefault(row=>
            string.Equals(row.id,id,StringComparison.Ordinal));
    }

    public static MonsterComboMasterRow MonsterCombo(string monsterId)
    {
        return Database==null||Database.monsterCombos==null
            ?null
            :Database.monsterCombos.FirstOrDefault(row=>row!=null&&row.enabled&&
                string.Equals(row.monsterId,monsterId,StringComparison.Ordinal));
    }

    private static int ConfigInt(IEnumerable<MasterConfigRow> rows,string key,int fallback)
    {
        MasterConfigRow row=FindConfig(rows,key);
        return row!=null&&int.TryParse(row.value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value)
            ?value:fallback;
    }

    private static bool ConfigBool(IEnumerable<MasterConfigRow> rows,string key,bool fallback)
    {
        MasterConfigRow row=FindConfig(rows,key);
        return row!=null&&bool.TryParse(row.value,out bool value)?value:fallback;
    }

    private static MasterConfigRow FindConfig(IEnumerable<MasterConfigRow> rows,string key)
    {
        return rows==null?null:rows.FirstOrDefault(row=>row!=null&&row.enabled&&
            string.Equals(row.key,key,StringComparison.Ordinal));
    }
}
