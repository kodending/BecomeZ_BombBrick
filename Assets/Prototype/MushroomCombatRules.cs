using System;
using UnityEngine;

public static class MushroomCombatRules
{
    public const string SkillId="mushroom_spore_colony";
    public const string VersusTraitId="intrinsic_versus_chain_germination";
    public const string DoksirumTraitId="intrinsic_doksirum_poisonous_mycelium";
    public const string LumirumTraitId="intrinsic_lumirum_healing_spore";
    public const string KingmoreTraitId="intrinsic_kingmore_vigorous_propagation";

    public const int SporeCount=3;
    public const float SporeOffsetRadiusTiles=0.65f;
    public const float ChainDamageBonusPerHit=0.2f;
    public const float PoisonDuration=3f;
    public const float PoisonTickInterval=1f;
    public const float PoisonTickDamageRatio=0.2f;
    public const float HealingRatio=0.35f;
    public const float SporeSeedDuration=4f;
    public const float ChildMushroomDamageMultiplier=0.5f;
    public const float GasTickInterval=.5f,GasSpreadDuration=.35f;
    public const float GasChildDamageRatio=.275f,GasChildRadiusTiles=1f,GasChildDelay=1.2f;

    public static float ExplosionDamage(
        float attackDamage,float ratio,int previousHits,bool chainGermination)
    {
        float multiplier=chainGermination
            ?1f+Mathf.Max(0,previousHits)*ChainDamageBonusPerHit:1f;
        return Mathf.Max(0f,attackDamage)*Mathf.Max(0f,ratio)*multiplier;
    }

    public static float PoisonTickDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*PoisonTickDamageRatio;
    }

    public static float Healing(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*HealingRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,VersusTraitId,StringComparison.Ordinal))return "연쇄 발아";
        if(string.Equals(traitId,DoksirumTraitId,StringComparison.Ordinal))return "맹독 균사";
        if(string.Equals(traitId,LumirumTraitId,StringComparison.Ordinal))return "치유 포자";
        if(string.Equals(traitId,KingmoreTraitId,StringComparison.Ordinal))return "왕성한 번식";
        return traitId??string.Empty;
    }
}
