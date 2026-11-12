using System;
using UnityEngine;

public static class NinjaCatCombatRules
{
    public const string SkillId="ninja_cat_shadow_hunt";
    public const string ChundongiTraitId="intrinsic_chundongi_opening_strike";
    public const string SeolmyoTraitId="intrinsic_seolmyo_frost_kunai";
    public const string SundongiTraitId="intrinsic_sundongi_poison_mist_assassination";
    public const string HongryeonTraitId="intrinsic_hongryeon_flame_afterimage";

    public const float UntargetableDuration=0.25f;
    public const float VanishTime=0.10f;
    public const float SubstitutionTime=0.18f;
    public const float ArrivalTime=0.32f;
    public const float StrikeTime=0.40f;
    public const float StrikeHoldDuration=0.03f;
    public const float RecoveryEndTime=0.61f;
    public const int EmpoweredBasicAttackCount=2;
    public const float EmpoweredBasicAttackBonus=0.2f;
    public const int OpeningStrikeHitCount=2;
    public const float OpeningStrikeDamageRatio=0.6f;
    public const float FrostDuration=3f;
    public const int FrostStacks=2;
    public const int FrostStacksToFreeze=3;
    public const float FreezeDuration=1f;
    public const float PoisonDuration=3f;
    public const float PoisonTickInterval=1f;
    public const float PoisonTickDamageRatio=0.25f;
    public const float HealingReduction=0.35f;
    public const float AfterimageDamageRatio=0.55f;
    public const float AfterimageRadiusTiles=1.25f;

    public static float EmpoweredBasicDamage(float damage,bool empowered)
    {
        return Mathf.Max(0f,damage)*(empowered?1f+EmpoweredBasicAttackBonus:1f);
    }

    public static float OpeningStrikeDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*OpeningStrikeDamageRatio;
    }

    public static float PoisonTickDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*PoisonTickDamageRatio;
    }

    public static float AfterimageDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*AfterimageDamageRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,ChundongiTraitId,StringComparison.Ordinal))return "빈틈 포착";
        if(string.Equals(traitId,SeolmyoTraitId,StringComparison.Ordinal))return "빙설 쿠나이";
        if(string.Equals(traitId,SundongiTraitId,StringComparison.Ordinal))return "독안개 암살";
        if(string.Equals(traitId,HongryeonTraitId,StringComparison.Ordinal))return "화염 잔상";
        return traitId??string.Empty;
    }
}
