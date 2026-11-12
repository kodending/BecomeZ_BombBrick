using System;
using System.Collections.Generic;
using UnityEngine;

public enum AutoBattleMlPlacementPhase
{
    SelectMonster,
    SelectTile,
    Battle
}

[Serializable]
public sealed class AutoBattleMlUnitObservation
{
    public string stableId="";
    public string monsterId="";
    public string familyId="";
    public string personalityId="";
    public int monsterSerial;
    public readonly List<string> activeTraitIds=new List<string>();
    public readonly List<string> itemIds=new List<string>();
    public bool present;
    public bool deployed;
    public float maxHp;
    public float damage;
    public float attackInterval;
    public float moveSpeed;
    public int attackRange;
    public int rank;
    public int traitCount;
    public int deploymentSlots=1;
    public Vector2Int tile;
}

[Serializable]
public sealed class AutoBattleMlTileObservation
{
    public Vector2Int coordinate;
    public bool occupied;
}

[Serializable]
public sealed class AutoBattleMlSnapshot
{
    public int wave;
    public string regionId="";
    public int difficulty;
    public string scenarioProfile="";
    public int deploymentCount;
    public int maximumDeploymentCount;
    public readonly List<AutoBattleMlUnitObservation> roster=
        new List<AutoBattleMlUnitObservation>();
    public readonly List<AutoBattleMlTileObservation> deploymentTiles=
        new List<AutoBattleMlTileObservation>();
    public readonly List<AutoBattleMlUnitObservation> enemies=
        new List<AutoBattleMlUnitObservation>();
}

[Serializable]
public sealed class AutoBattleMlOutcome
{
    public bool won;
    public float battleSeconds;
    public int playerDeaths;
    public int playerSurvivors;
    public int enemySurvivors;
    public float survivingPlayerHpRatio;
    public float enemyRemainingHpRatio;
}

public static class AutoBattleMlReward
{
    public const float ValidPlacementReward=0.01f;
    public const float InvalidActionPenalty=-0.05f;

    public static float Terminal(AutoBattleMlOutcome outcome)
    {
        if(outcome==null)return -1f;
        float result=outcome.won?1f:-1f;
        float survival=Mathf.Clamp01(outcome.survivingPlayerHpRatio)*0.35f;
        float enemyProgress=(1f-Mathf.Clamp01(outcome.enemyRemainingHpRatio))*0.25f;
        float speed=outcome.won?Mathf.Clamp01(1f-outcome.battleSeconds/120f)*0.15f:0f;
        return result+survival+enemyProgress+speed;
    }
}
