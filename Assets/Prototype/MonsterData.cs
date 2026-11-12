using System;
using UnityEngine;

public enum MonsterGrade
{
    Common=1,
    Rare=2,
    Epic=3,
    Legendary=4
}

public static class MonsterGradeRules
{
    public static int Cost(MonsterGrade grade)
    {
        return Mathf.Clamp((int)grade,1,4);
    }

    public static string DisplayName(MonsterGrade grade)
    {
        if(grade==MonsterGrade.Rare)return "희귀";
        if(grade==MonsterGrade.Epic)return "에픽";
        if(grade==MonsterGrade.Legendary)return "전설";
        return "보통";
    }

    public static int TraitSlots(MonsterGrade grade)
    {
        return Mathf.Clamp((int)grade-1,0,3);
    }
}

[Serializable]
public sealed class MonsterUpgradeSave
{
    public string upgradeId;
    public int level;
    public float value;
}
