using System;
using UnityEngine;

public static class MantisCombatRules
{
    public const string SkillId="mantis_cross_scythe_wind";
    public const string MantiusTraitId="intrinsic_mantius_precise_dissection";
    public const string FlotisTraitId="intrinsic_flotis_hallucinatory_pollen";
    public const string SantiusTraitId="intrinsic_santius_sand_scythe_wind";
    public const string NectisTraitId="intrinsic_nectis_reaper_tailoring";

    public const int WaveCount=2;
    public const float WindupDuration=0.12f;
    public const float WaveInterval=0.1f;
    public const float WindSpeed=30f;
    public const float WaveAngle=12f;
    public const float PreciseSecondHitBonus=0.4f;
    public const float BleedTickDamageRatio=0.2f;
    public const float BleedTickInterval=1f;
    public const float ExtendedRangeTiles=1.5f;
    public const float ExtendedWidthMultiplier=1.5f;
    public const float PollenAttackSpeedSlow=0.2f;
    public const float PollenMoveSpeedSlow=0.3f;
    public const float PollenDuration=4f;
    public const float RecastDamageMultiplier=0.6f;

    public static float HitDamage(
        float attackDamage,float ratio,int hitIndex,bool preciseDissection)
    {
        float multiplier=preciseDissection&&hitIndex==1
            ?1f+PreciseSecondHitBonus:1f;
        return Mathf.Max(0f,attackDamage)*Mathf.Max(0f,ratio)*multiplier;
    }

    public static float BleedTickDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*BleedTickDamageRatio;
    }

    public static float RangeTiles(float baseRange,bool extended)
    {
        return Mathf.Max(0f,baseRange)+(extended?ExtendedRangeTiles:0f);
    }

    public static float WidthTiles(float baseWidth,bool extended)
    {
        return Mathf.Max(0f,baseWidth)*(extended?ExtendedWidthMultiplier:1f);
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,MantiusTraitId,StringComparison.Ordinal))return "정밀 절단";
        if(string.Equals(traitId,FlotisTraitId,StringComparison.Ordinal))return "환각 꽃가루";
        if(string.Equals(traitId,SantiusTraitId,StringComparison.Ordinal))return "모래 낫바람";
        if(string.Equals(traitId,NectisTraitId,StringComparison.Ordinal))return "사신의 재단";
        return traitId??string.Empty;
    }
}
