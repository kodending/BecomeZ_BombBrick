using UnityEngine;

public enum SealRiskType
{
    Vitality,
    Ferocity,
    Movement,
    AttackSpeed,
    Swarm
}

public enum SealBlessingType
{
    None,
    MovementSpeed,
    AttackDamage,
    AttackSpeed,
    ItemRarity
}

[CreateAssetMenu(fileName="SealExcavationMapPreset",menuName="Bomb Brick/Maps/Seal Excavation Map Preset")]
public sealed class SealExcavationMapPreset : CombatMapPreset
{
    [Header("Seal Selection")]
    [Min(3)] public int sealCount=7;
    [Min(1)] public int maximumSelectedSeals=4;
    [Range(0f,1f)] public float blessingChance=0.35f;
    [Min(0)] public int minimumBlessedSeals=1;
    [Min(0)] public int maximumBlessedSeals=3;

    [Header("Encounter")]
    [Min(1)] public int baseEnemiesPerSeal=4;
    [Min(0)] public int enemiesAddedPerOrder=1;
    [Min(1f)] public float spawnRadius=7f;
    [Min(1)] public int baseRewardScore=350;

    [Header("Risk Values")]
    [Range(0f,1f)] public float enemyHealthIncrease=0.22f;
    [Range(0f,1f)] public float enemyDamageIncrease=0.16f;
    [Range(0f,1f)] public float enemyMoveSpeedIncrease=0.13f;
    [Range(0f,1f)] public float enemyAttackSpeedIncrease=0.14f;
    [Min(1)] public int swarmEnemyIncrease=2;

    [Header("Map-only Blessings")]
    [Range(0f,1f)] public float playerMoveSpeedIncrease=0.10f;
    [Range(0f,1f)] public float playerDamageIncrease=0.15f;
    [Range(0f,1f)] public float playerAttackSpeedIncrease=0.12f;
    [Range(0f,1f)] public float itemRarityIncrease=0.18f;
}
