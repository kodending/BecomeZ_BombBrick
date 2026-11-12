using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatGrowthCardDatabaseRow
{
    public string cardId;
    public string displayName;
    public CombatGrowthCardEffect effect;
    public float commonValue;
    public float rareValue;
    public float epicValue;
    public float legendaryValue;
    public float commonSecondaryValue;
    public float rareSecondaryValue;
    public float epicSecondaryValue;
    public float legendarySecondaryValue;
    [TextArea] public string triggerRule;
    [TextArea] public string stackingRule;
    [TextArea] public string resetRule;
    public bool enabled=true;
    [Min(1)] public int dataVersion=1;
}

[CreateAssetMenu(menuName="BombBrick/Combat Growth Card Database",fileName="CombatGrowthCardDatabase")]
public sealed class CombatGrowthCardDatabase : ScriptableObject
{
    public List<CombatGrowthCardDatabaseRow> cards=new List<CombatGrowthCardDatabaseRow>();
}
