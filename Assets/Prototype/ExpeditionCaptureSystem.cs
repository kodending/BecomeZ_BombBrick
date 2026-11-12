using System;

[Serializable]
public sealed class PendingMonsterCapture
{
    public string targetEnemyInstanceId="";
    public string captureNetItemId="";
    public float successChance;
    public bool resolved;
    public OwnedMonsterSave capturedMonster;

    public bool IsArmed=>!string.IsNullOrWhiteSpace(targetEnemyInstanceId);
    public bool IsReady=>capturedMonster!=null;

    public void Arm(string enemyInstanceId)
    {
        Arm(enemyInstanceId,"",1f);
    }

    public void Arm(
        string enemyInstanceId,string netItemId,float chance)
    {
        targetEnemyInstanceId=enemyInstanceId??"";
        captureNetItemId=netItemId??"";
        successChance=Math.Max(0f,Math.Min(1f,chance));
        resolved=false;
        capturedMonster=null;
    }

    public bool ResolveDefeat(
        OwnedMonsterSave defeatedEnemy,MonsterDefinition definition,
        float successChance,float roll,string capturedInstanceId,
        ExpeditionDifficulty difficulty=ExpeditionDifficulty.Normal)
    {
        if(defeatedEnemy==null||definition==null||resolved||
           capturedMonster!=null||
           !string.Equals(targetEnemyInstanceId,defeatedEnemy.instanceId,StringComparison.Ordinal))
            return false;
        resolved=true;
        if(roll>=Math.Max(0f,Math.Min(1f,successChance)))return false;
        capturedMonster=ExpeditionCaptureSystem.CreateCapturedMonster(
            defeatedEnemy,definition,capturedInstanceId,difficulty);
        return true;
    }

    public void Clear()
    {
        targetEnemyInstanceId="";
        captureNetItemId="";
        successChance=0f;
        resolved=false;
        capturedMonster=null;
    }
}

public static class ExpeditionCaptureSystem
{
    public static OwnedMonsterSave CreateCapturedMonster(
        OwnedMonsterSave defeatedEnemy,MonsterDefinition definition,string capturedInstanceId,
        ExpeditionDifficulty difficulty=ExpeditionDifficulty.Normal)
    {
        if(defeatedEnemy==null)throw new ArgumentNullException(nameof(defeatedEnemy));
        if(definition==null)throw new ArgumentNullException(nameof(definition));
        if(string.IsNullOrWhiteSpace(capturedInstanceId))
            throw new ArgumentException("Captured monster instance ID is required.",nameof(capturedInstanceId));

        OwnedMonsterSave captured=new OwnedMonsterSave
        {
            instanceId=capturedInstanceId,
            monsterId=definition.monsterId,
            customName="",
            intrinsicTraitId=defeatedEnemy.intrinsicTraitId,
            personalityId=defeatedEnemy.personalityId,
            personalitySeed=defeatedEnemy.personalitySeed,
            courage=defeatedEnemy.courage,
            aggression=defeatedEnemy.aggression,
            discipline=defeatedEnemy.discipline,
            composure=defeatedEnemy.composure,
            cooperation=defeatedEnemy.cooperation,
            adaptability=defeatedEnemy.adaptability,
            hpMultiplier=defeatedEnemy.hpMultiplier,
            damageMultiplier=defeatedEnemy.damageMultiplier,
            attackSpeedMultiplier=defeatedEnemy.attackSpeedMultiplier,
            defenseMultiplier=defeatedEnemy.defenseMultiplier,
            statusResistanceMultiplier=
                defeatedEnemy.statusResistanceMultiplier,
            moveSpeedMultiplier=defeatedEnemy.moveSpeedMultiplier,
            isShiny=defeatedEnemy.isShiny,
            locked=false,
            favorite=false,
            isNew=true,
            acquiredUtcTicks=DateTime.UtcNow.Ticks
        };
        MonsterInstanceGenerator.EnsureInitialized(captured,definition);
        MonsterInstanceGenerator.ApplyCaptureQualityRerolls(
            captured,difficulty,
            MonsterInstanceGenerator.StableSeed(capturedInstanceId+"_capture_quality"));
        return captured;
    }
}
