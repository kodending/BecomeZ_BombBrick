using System;
using System.Collections.Generic;
using UnityEngine;

public enum ExpeditionDifficulty
{
    Beginner=0,
    Normal=1,
    Veteran=2,
    Hell=3
}

public static class ExpeditionDifficultyRules
{
    public static ExpeditionDifficulty Clamp(ExpeditionDifficulty difficulty)
    {
        return (ExpeditionDifficulty)Mathf.Clamp((int)difficulty,0,3);
    }

    public static string DisplayName(ExpeditionDifficulty difficulty)
    {
        switch(Clamp(difficulty))
        {
            case ExpeditionDifficulty.Beginner:return "초보자";
            case ExpeditionDifficulty.Veteran:return "숙련자";
            case ExpeditionDifficulty.Hell:return "헬";
            default:return "보통";
        }
    }

    public static float HealthMultiplier(ExpeditionDifficulty difficulty)
    {
        switch(Clamp(difficulty))
        {
            case ExpeditionDifficulty.Beginner:return Percent("beginner","health",65);
            case ExpeditionDifficulty.Veteran:return Percent("veteran","health",100);
            case ExpeditionDifficulty.Hell:return Percent("hell","health",135);
            default:return Percent("normal","health",80);
        }
    }

    public static float AttackMultiplier(ExpeditionDifficulty difficulty)
    {
        return 1f;
    }

    public static Vector2 EnemyQualityScoreRange(ExpeditionDifficulty difficulty)
    {
        string key;
        int minimum,maximum;
        switch(Clamp(difficulty))
        {
            case ExpeditionDifficulty.Beginner:key="beginner";minimum=0;maximum=43;break;
            case ExpeditionDifficulty.Veteran:key="veteran";minimum=30;maximum=84;break;
            case ExpeditionDifficulty.Hell:key="hell";minimum=57;maximum=101;break;
            default:key="normal";minimum=10;maximum=70;break;
        }
        return new Vector2(
            GameMasterData.ExpeditionInt("difficulty_"+key+"_quality_score_min",minimum),
            GameMasterData.ExpeditionInt("difficulty_"+key+"_quality_score_max_exclusive",maximum));
    }

    public static string EnemyQualityGradeRange(ExpeditionDifficulty difficulty)
    {
        Vector2 range=EnemyQualityScoreRange(difficulty);
        return MonsterStatQuality.OverallGrade(range.x)+"~"+
            MonsterStatQuality.OverallGrade(Mathf.Min(100f,range.y-0.001f));
    }

    private static float Percent(string difficulty,string stat,int fallback)
    {
        return Mathf.Max(1,GameMasterData.ExpeditionInt(
            "difficulty_"+difficulty+"_"+stat+"_percent",fallback))/100f;
    }

    public static int CaptureQualityRolls(ExpeditionDifficulty difficulty)
    {
        switch(Clamp(difficulty))
        {
            case ExpeditionDifficulty.Veteran:return 2;
            case ExpeditionDifficulty.Hell:return 3;
            default:return 1;
        }
    }
}

public enum ExpeditionMonsterRank
{
    Rank1=1,
    Rank2=2,
    Rank3=3
}

public enum ExpeditionItemTransformState
{
    None=0,
    Overcharged=1,
    CorruptedGreatSuccess=2,
    CorruptedSuccess=3,
    CorruptedFailure=4,
    Radiant=5
}

public enum ExpeditionRunStatus
{
    Active=0,
    Completed=1,
    Failed=2,
    Abandoned=3
}

public enum ExpeditionRunCheckpoint
{
    Preparation=0,
    Shop=1,
    AugmentDraft=2,
    GrowthShop=3,
    GrowthReward=GrowthShop,
    InBattle=4
}

[Serializable]
public sealed class ExpeditionMonsterStateSave
{
    public string instanceId;
    public string monsterId;
    public ExpeditionMonsterRank rank=ExpeditionMonsterRank.Rank1;
    public string rank2TraitId;
    public string rank3TraitId;
    public int growthTotal;
    public List<string> itemInstanceIds=new List<string>();
    public bool deployed;
    public int deploymentX;
    public int deploymentY;
    public int benchSlot=-1;
    public float currentHpRatio=1f;
    public bool temporary;
    public string sourceAugmentId;
    public bool transcended;
    public int transcendenceFailureBonus;
    public List<string> appliedSpecialItemIds=new List<string>();
}

[Serializable]
public sealed class ExpeditionItemInstanceSave
{
    public string instanceId;
    public string itemId;
    public ExpeditionItemTransformState transformState;
    public int transformSeed;
    public bool temporary;
    public bool destroyOnUnequip;
    public bool transformLocked;
}

[Serializable]
public sealed class ExpeditionAugmentSelectionSave
{
    public string augmentId;
    public int acquiredWave;
    public string targetMonsterInstanceId;
    public string targetItemInstanceId;
    public int deterministicSeed;
    public int runtimeWave;
    public int remainingWaves;
    public int triggerCount;
    public int counter;
    public bool triggered;
    public string runtimeState;
}

[Serializable]
public sealed class ExpeditionSpecialItemSave
{
    public string instanceId;
    public string itemId;
    public int count=1;
    public string boundMonsterInstanceId;
    public bool temporary;
    public bool occupiesItemSlot;
}

[Serializable]
public sealed class ExpeditionCombatContributionSave
{
    public string monsterInstanceId;
    public string monsterId;
    public string displayName;
    public float damageDealt;
    public float damageTaken;
    public float healingReceived;
    public int comboActivations;
    public float comboBonusDamage;
    public bool survived;
}

[Serializable]
public sealed class ExpeditionWaveCombatContributionSave
{
    public int wave;
    public bool won;
    public List<ExpeditionCombatContributionSave> allies=
        new List<ExpeditionCombatContributionSave>();
    public List<ExpeditionCombatContributionSave> enemies=
        new List<ExpeditionCombatContributionSave>();
}

[Serializable]
public sealed class ExpeditionCompletionSave
{
    public bool victory;
    public PermanentResearchSettlement research;
    public int hunterTokens;
    public int rareCaptureNets;
    public bool firstBeginnerClear;
    public string nextRegionId;
}

[Serializable]
public sealed class ExpeditionRunSave
{
    public int version=15;
    public string profileId;
    public string runId;
    public string regionId;
    public ExpeditionDifficulty difficulty=ExpeditionDifficulty.Normal;
    public ExpeditionRunStatus status=ExpeditionRunStatus.Active;
    public bool completionPending;
    public ExpeditionCompletionSave completion;
    public ExpeditionRunCheckpoint checkpoint=ExpeditionRunCheckpoint.Preparation;
    public long startedUtcTicks;
    public long lastSavedUtcTicks;
    public int lastCompletedWave;
    public int wave=1;
    public int activeBattleWave;
    public int activeBattleSeed;
    public int lives=3;
    public int maximumLives=3;
    public int maxDeploymentCount=3;
    public int expeditionCurrency;
    public int consecutiveWins;
    public int consecutiveLosses;
    public int equipmentPityClears;
    public int shopWave;
    public int shopRefreshCount;
    public List<string> shopItemIds=new List<string>();
    public int growthShopWave;
    public int growthShopCompletedWave;
    public bool growthShopFreePurchaseUsed;
    public int growthShopRerollCount;
    public List<string> growthShopCandidateMonsterIds=new List<string>();
    public List<string> growthShopPurchasedMonsterIds=new List<string>();
    public List<ExpeditionMonsterStateSave> monsters=new List<ExpeditionMonsterStateSave>();
    public List<ExpeditionItemInstanceSave> itemInstances=new List<ExpeditionItemInstanceSave>();
    public List<string> inventoryItemInstanceIds=new List<string>();
    public List<CombatGrowthCardSelectionSave> growthCards=new List<CombatGrowthCardSelectionSave>();
    public List<ExpeditionAugmentSelectionSave> augments=new List<ExpeditionAugmentSelectionSave>();
    public List<ExpeditionSpecialItemSave> specialItems=new List<ExpeditionSpecialItemSave>();
    public List<string> augmentDraftIds=new List<string>();
    public List<CombatGrowthCardDraftEntry> growthCardDraft=
        new List<CombatGrowthCardDraftEntry>();
    public List<string> lastBlessingRewardIds=new List<string>();
    public List<string> completedItemChoiceIds=new List<string>();
    public string completedItemChoiceSource;
    public string completedItemChoiceTarget;
    public int completedItemChoiceRerollCount;
    public bool growthCardDraftRerolled;
    public int growthCardDraftSequence;
    public int augmentDraftWave;
    public int augmentRerollsRemaining;
    public int augmentChoiceSequence;
    public int lastAugmentProcessedWave;
    public int augmentDraftSequence;
    public int shopRollSequence;
    public int captureAttemptedWave;
    public int captureAttemptSequence;
    public string selectedCaptureEnemyInstanceId;
    public string selectedCaptureNetItemId;
    public List<string> capturedMonsterInstanceIds=new List<string>();
    public List<ExpeditionWaveCombatContributionSave> combatContributionHistory=
        new List<ExpeditionWaveCombatContributionSave>();
}
