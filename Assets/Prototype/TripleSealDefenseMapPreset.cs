using UnityEngine;

[CreateAssetMenu(fileName="TripleSealDefenseMapPreset",menuName="Bomb Brick/Maps/Triple Seal Defense Map Preset")]
public sealed class TripleSealDefenseMapPreset : CombatMapPreset
{
    [Header("Defense Objective")]
    [Min(1)] public int sealMaxHealth=180;
    [Min(3f)] public float sealDistanceFromCenter=13f;
    [Min(0.5f)] public float sealHitRadius=1.4f;

    [Header("Assault Waves")]
    [Min(1)] public int waveCount=4;
    [Min(0f)] public float firstWaveDelay=1.5f;
    [Min(0f)] public float nextWaveDelay=1.2f;
    public int[] enemiesPerWave={6,8,9,12};
    [Min(10f)] public float enemySpawnDistance=29f;
    [Min(0f)] public float laneSpread=4.5f;

    [Header("Defense Enemy Roles")]
    [Range(0f,1f)] public float hunterRatio=0.18f;
    [Min(1f)] public float siegeHealthMultiplier=2.2f;
    [Min(0.1f)] public float siegeMoveMultiplier=0.72f;
    [Min(1f)] public float siegeSealDamageMultiplier=3f;
    [Min(1f)] public float commanderHealthMultiplier=1.6f;
    [Min(1f)] public float commanderWaveSpeedMultiplier=1.12f;

    [Header("Reward")]
    [Min(1)] public int rewardScorePerSurvivingSeal=900;
    [Range(0f,1f)] public float highHealthThreshold=0.7f;
    [Range(0f,1f)] public float damagedThreshold=0.3f;
    [Min(1f)] public float perfectDefenseMultiplier=1.35f;
}
