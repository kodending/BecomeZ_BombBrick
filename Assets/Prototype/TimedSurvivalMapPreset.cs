using UnityEngine;

[CreateAssetMenu(fileName="TimedSurvivalMapPreset",menuName="Bomb Brick/Maps/Timed Survival Map Preset")]
public sealed class TimedSurvivalMapPreset : CombatMapPreset
{
    [Header("Timed Encounter")]
    [Min(10f)] public float combatDuration=60f;
    [Min(0f)] public float initialSpawnDelay=1.2f;
    [Min(0.1f)] public float startingSpawnInterval=3.2f;
    [Min(0.1f)] public float endingSpawnInterval=1.35f;
    [Min(1)] public int startingBatchSize=2;
    [Min(0)] public int additionalBatchSize=3;
    [Min(1)] public int maximumAliveEnemies=24;
    [Min(8f)] public float spawnDistance=31f;
    [Min(0f)] public float spawnDistanceJitter=3.5f;

    [Header("Escalation")]
    [Min(1f)] public float endingHealthMultiplier=1.35f;
    [Min(1f)] public float endingMoveSpeedMultiplier=1.15f;
    [Min(1f)] public float endingDamageMultiplier=1.25f;

    [Header("Completion")]
    public bool clearRemainingEnemies=true;
    [Min(0)] public int completionScore=5000;
}
