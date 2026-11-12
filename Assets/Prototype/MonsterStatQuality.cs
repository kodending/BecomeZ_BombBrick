using UnityEngine;

public enum MonsterIndividualStat
{
    Health,
    AttackDamage,
    AttackSpeed,
    Defense,
    StatusResistance,
    MoveSpeed
}

public static class MonsterStatQuality
{
    public static int MinimumPercent(MonsterIndividualStat stat)
    {
        switch(stat)
        {
            case MonsterIndividualStat.Health:
                return Config("hp_percent_min",85);
            case MonsterIndividualStat.AttackDamage:
                return Config("damage_percent_min",85);
            case MonsterIndividualStat.AttackSpeed:
                return Config("attack_speed_percent_min",90);
            case MonsterIndividualStat.Defense:
                return Config("defense_percent_min",85);
            case MonsterIndividualStat.StatusResistance:
                return Config("status_resistance_percent_min",85);
            case MonsterIndividualStat.MoveSpeed:
                return Config("move_speed_percent_min",94);
            default:
                return 100;
        }
    }

    public static int MaximumPercent(MonsterIndividualStat stat)
    {
        switch(stat)
        {
            case MonsterIndividualStat.Health:
                return Config("hp_percent_max",115);
            case MonsterIndividualStat.AttackDamage:
                return Config("damage_percent_max",115);
            case MonsterIndividualStat.AttackSpeed:
                return Config("attack_speed_percent_max",110);
            case MonsterIndividualStat.Defense:
                return Config("defense_percent_max",115);
            case MonsterIndividualStat.StatusResistance:
                return Config("status_resistance_percent_max",115);
            case MonsterIndividualStat.MoveSpeed:
                return Config("move_speed_percent_max",106);
            default:
                return 100;
        }
    }

    public static float Multiplier(
        OwnedMonsterSave monster,MonsterIndividualStat stat)
    {
        if(monster==null)return 1f;
        switch(stat)
        {
            case MonsterIndividualStat.Health:
                return monster.hpMultiplier;
            case MonsterIndividualStat.AttackDamage:
                return monster.damageMultiplier;
            case MonsterIndividualStat.AttackSpeed:
                return monster.attackSpeedMultiplier;
            case MonsterIndividualStat.Defense:
                return monster.defenseMultiplier;
            case MonsterIndividualStat.StatusResistance:
                return monster.statusResistanceMultiplier;
            case MonsterIndividualStat.MoveSpeed:
                return monster.moveSpeedMultiplier;
            default:
                return 1f;
        }
    }

    public static float QualityScore(
        OwnedMonsterSave monster,MonsterIndividualStat stat)
    {
        return QualityScore(Multiplier(monster,stat),stat);
    }

    public static float QualityScore(
        float multiplier,MonsterIndividualStat stat)
    {
        float minimum=MinimumPercent(stat)/100f;
        float maximum=MaximumPercent(stat)/100f;
        if(maximum<=minimum)return 50f;
        return Mathf.Clamp01((multiplier-minimum)/(maximum-minimum))*100f;
    }

    public static string StatGrade(
        OwnedMonsterSave monster,MonsterIndividualStat stat)
    {
        float score=QualityScore(monster,stat);
        if(score>=90f)return "S";
        if(score>=70f)return "A";
        if(score>=30f)return "B";
        if(score>=10f)return "C";
        return "D";
    }

    public static float OverallScore(OwnedMonsterSave monster)
    {
        if(monster==null)return 50f;
        float total=0f;
        total+=QualityScore(monster,MonsterIndividualStat.Health);
        total+=QualityScore(monster,MonsterIndividualStat.AttackDamage);
        total+=QualityScore(monster,MonsterIndividualStat.AttackSpeed);
        total+=QualityScore(monster,MonsterIndividualStat.Defense);
        total+=QualityScore(monster,MonsterIndividualStat.StatusResistance);
        total+=QualityScore(monster,MonsterIndividualStat.MoveSpeed);
        return total/6f;
    }

    public static string OverallGrade(OwnedMonsterSave monster)
    {
        return OverallGrade(OverallScore(monster));
    }

    public static string OverallGrade(float score)
    {
        score=Mathf.Clamp(score,0f,100f);
        if(score>=97f)return "S+";
        if(score>=94f)return "S";
        if(score>=90f)return "S-";
        if(score>=84f)return "A+";
        if(score>=77f)return "A";
        if(score>=70f)return "A-";
        if(score>=57f)return "B+";
        if(score>=43f)return "B";
        if(score>=30f)return "B-";
        if(score>=23f)return "C+";
        if(score>=17f)return "C";
        if(score>=10f)return "C-";
        if(score>=6f)return "D+";
        if(score>=3f)return "D";
        return "D-";
    }

    private static int Config(string key,int fallback)
    {
        return GameMasterData.GenerationInt(key,fallback);
    }
}
