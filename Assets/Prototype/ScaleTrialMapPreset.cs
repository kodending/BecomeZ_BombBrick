using UnityEngine;

[CreateAssetMenu(fileName="ScaleTrialMapPreset",menuName="Bomb Brick/Maps/Scale Trial Map Preset")]
public sealed class ScaleTrialMapPreset : CombatMapPreset
{
    [Header("Six Stage Trial")]
    [Min(1)] public int stageCount=6;
    public int[] enemiesPerStage={4,5,7,9,11,14};
    public float[] enemyHealthMultiplier={1f,1.12f,1.28f,1.48f,1.72f,2f};
    public float[] enemySpeedMultiplier={1f,1.03f,1.07f,1.11f,1.16f,1.22f};
    public float[] rewardMultiplier={1f,1.35f,1.8f,2.4f,3.2f,4.25f};

    [Header("Choice UI")]
    [Min(0f)] public float choiceRevealDelay=0.65f;
    [Min(0.1f)] public float challengeHoldDuration=0.7f;

    public ScaleTrialMapPreset()
    {
        mapId="scale-trial-greybox-01";
        displayName="선택의 저울 그레이박스 01";
        centralClearingRadius=16f;
        minimumGroupDistance=11f;
        maximumGroupDistance=18f;
        activationRadius=30f;
        paths=new[]
        {
            new GreyboxPathPreset("South Entry",new Vector3(0f,-0.27f,-25f),new Vector3(8f,0.05f,22f),0f),
            new GreyboxPathPreset("North Exit",new Vector3(0f,-0.27f,25f),new Vector3(8f,0.05f,22f),0f),
            new GreyboxPathPreset("West Spawn Lane",new Vector3(-22f,-0.27f,0f),new Vector3(22f,0.05f,7f),90f),
            new GreyboxPathPreset("East Spawn Lane",new Vector3(22f,-0.27f,0f),new Vector3(22f,0.05f,7f),90f)
        };
        obstacles=new[]
        {
            new GreyboxObstaclePreset(new Vector3(-10f,0f,-9f),3.1f,2.8f,24f),
            new GreyboxObstaclePreset(new Vector3(10f,0f,-9f),3.1f,2.8f,-24f),
            new GreyboxObstaclePreset(new Vector3(-11f,0f,10f),3.4f,3.2f,-18f),
            new GreyboxObstaclePreset(new Vector3(11f,0f,10f),3.4f,3.2f,18f)
        };
    }

    public int GetEnemyCount(int stage)
    {
        int index=Mathf.Clamp(stage-1,0,enemiesPerStage.Length-1);
        return Mathf.Max(1,enemiesPerStage[index]);
    }

    public float GetHealthMultiplier(int stage) => GetArrayValue(enemyHealthMultiplier,stage,1f);
    public float GetSpeedMultiplier(int stage) => GetArrayValue(enemySpeedMultiplier,stage,1f);
    public float GetRewardMultiplier(int stage) => GetArrayValue(rewardMultiplier,stage,1f);

    private static float GetArrayValue(float[] values,int stage,float fallback)
    {
        if(values==null||values.Length==0)return fallback;
        return Mathf.Max(0.01f,values[Mathf.Clamp(stage-1,0,values.Length-1)]);
    }
}
