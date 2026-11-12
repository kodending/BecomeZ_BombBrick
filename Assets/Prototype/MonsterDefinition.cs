using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName="BombBrick/Monster Definition",fileName="MonsterDefinition")]
public sealed class MonsterDefinition : ScriptableObject
{
    [Header("Identity")]
    public string monsterId;
    [Tooltip("Player-facing collection number, e.g. 001.")]
    public string serialNumber;
    public string displayName;
    public string familyId;
    [FormerlySerializedAs("grade")] public MonsterGrade defaultGrade=MonsterGrade.Common;
    public bool enabled=true;
    [Min(1)] public int dataVersion=1;

    [Header("Presentation")]
    public string resourcePath;
    public string shinyResourcePath;
    public string portraitResourcePath;
    public string animationSetId;
    [Min(0.1f)] public float visualScale=1f;
    [Min(0.1f)] public float modelHeight=1.38f;
    [Min(0.1f)] public float groundSpanTileRatio=0.78f;
    [Min(0.1f)] public float collisionRadius=0.62f;

    [Header("Combat")]
    public bool isBoss;
    [Tooltip("Legacy authoring value. Combat behavior is resolved from skillId and personality.")]
    public string tactic="Hunter";
    [Min(1f)] public float maxHp=105f;
    [Min(0f)] public float attackDamage=22f;
    [Min(0.1f)] public float attackInterval=0.9f;
    [Min(0.1f)] public float moveSpeedTilesPerSecond=2.75f;
    [Min(1)] public int attackRange=1;
    [Min(0f),Tooltip("Reduces incoming physical damage with diminishing returns.")]
    public float defense;
    [Min(0f),Tooltip("Reduces hostile status durations with diminishing returns.")]
    public float statusResistance;
    public string basicAttackId="basic_melee";
    public string skillId;

    [Header("Persistent Trait")]
    [Tooltip("Species-defined trait permanently retained by every owned instance.")]
    public string intrinsicTraitId;
}
