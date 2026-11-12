using System;
using UnityEngine;

public static class MonkeyCombatRules
{
    public const string SkillId="monkey_acrobatic_barrage";
    public const string ChiwooTraitId="intrinsic_chiwoo_precise_hands";
    public const string TotoTraitId="intrinsic_toto_poison_fruit";
    public const string UnoTraitId="intrinsic_uno_cloud_bullet";
    public const string MakaTraitId="intrinsic_maka_powder_pouch";

    public const int ProjectileCount=5;
    public const float ProjectileInterval=0.12f;
    public const float FinalThrowInterval=0.28f;
    public const float BarrageReleaseDuration=ProjectileInterval*(ProjectileCount-2)+FinalThrowInterval;
    public const float SkillProjectileVisualMultiplier=2f;
    public const float PreciseHandsBonusPerHit=0.1f;
    public const int PoisonStacksToBurst=3;
    public const float PoisonBurstDamageRatio=0.7f;
    public const float PoisonStackDuration=9999f;
    public const int CloudBounceCount=2;
    public const float CloudBounceRadiusTiles=2.5f;
    public const float PowderDamageMultiplier=1.6f;
    public const float PowderExplosionRadiusTiles=2f;

    public static float ProjectileDamage(
        float attackDamage,float ratio,int consecutiveHits,bool preciseHands,bool powder)
    {
        float multiplier=preciseHands
            ?1f+Mathf.Max(0,consecutiveHits)*PreciseHandsBonusPerHit:1f;
        if(powder)multiplier*=PowderDamageMultiplier;
        return Mathf.Max(0f,attackDamage)*Mathf.Max(0f,ratio)*multiplier;
    }

    public static float PoisonBurstDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*PoisonBurstDamageRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,ChiwooTraitId,StringComparison.Ordinal))return "정확한 손놀림";
        if(string.Equals(traitId,TotoTraitId,StringComparison.Ordinal))return "맹독 열매";
        if(string.Equals(traitId,UnoTraitId,StringComparison.Ordinal))return "구름탄";
        if(string.Equals(traitId,MakaTraitId,StringComparison.Ordinal))return "화약 주머니";
        return traitId??string.Empty;
    }
}
