using System;
using UnityEngine;

public abstract class CombatMapPreset : ScriptableObject
{
    public string mapId;
    public string displayName;

    [Header("Shared Encounter Placement")]
    [Min(4f)] public float minimumGroupDistance=14f;
    [Min(6f)] public float maximumGroupDistance=34f;
    [Min(2f)] public float minimumGroupSeparation=9f;
    [Min(0.5f)] public float groupSpread=2.7f;
    [Min(1f)] public float activationRadius=9f;

    [Header("Spawn Candidate Pool")]
    [Min(1)] public int candidateRingCount=4;
    [Min(4)] public int candidatesPerRing=16;
    [Min(0f)] public float candidateRadialJitter=1.8f;
    [Min(0f)] public float candidateTangentJitter=1.4f;
    [Range(0f,20f)] public float candidateAngleJitter=4.3f;

    [Header("Greybox")]
    [Min(2f)] public float centralClearingRadius=10.5f;
    public GreyboxPathPreset[] paths=Array.Empty<GreyboxPathPreset>();
    public GreyboxObstaclePreset[] obstacles=Array.Empty<GreyboxObstaclePreset>();
}

[CreateAssetMenu(fileName="AnnihilationMapPreset",menuName="Bomb Brick/Maps/Annihilation Map Preset")]
public sealed class AnnihilationMapPreset : CombatMapPreset
{
    [Header("Encounter")]
    [Min(1)] public int baseGroupCount=6;
    [Min(0)] public int groupCountVariance=1;
    [Min(1)] public int minimumEnemiesPerGroup=3;
    [Min(1)] public int maximumEnemiesPerGroup=5;
    [Min(0)] public int difficultyGroupBonus;
    [Min(0)] public int modeGroupBonus;

    public int ResolvedGroupCount => Mathf.Max(1,baseGroupCount+difficultyGroupBonus+modeGroupBonus);

    public int RollGroupCount()
    {
        int variance=Mathf.Max(0,groupCountVariance);
        return Mathf.Max(1,ResolvedGroupCount+UnityEngine.Random.Range(-variance,variance+1));
    }

    public int RollEnemiesPerGroup()
    {
        int minimum=Mathf.Max(1,minimumEnemiesPerGroup);
        int maximum=Mathf.Max(minimum,maximumEnemiesPerGroup);
        return UnityEngine.Random.Range(minimum,maximum+1);
    }
}

[Serializable]
public struct GreyboxPathPreset
{
    public string name;
    public Vector3 position;
    public Vector3 scale;
    public float yaw;

    public GreyboxPathPreset(string name,Vector3 position,Vector3 scale,float yaw)
    {
        this.name=name;
        this.position=position;
        this.scale=scale;
        this.yaw=yaw;
    }
}

[Serializable]
public struct GreyboxObstaclePreset
{
    public Vector3 center;
    public float radius;
    public float height;
    public float yaw;

    public GreyboxObstaclePreset(Vector3 center,float radius,float height,float yaw)
    {
        this.center=center;
        this.radius=radius;
        this.height=height;
        this.yaw=yaw;
    }
}
