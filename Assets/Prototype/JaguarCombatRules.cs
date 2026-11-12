using System;
using UnityEngine;

public static class JaguarCombatRules
{
    public const string SkillId="jaguar_shadow_slash";
    public const string BumiTraitId="intrinsic_bumi_hunt_spots";
    public const string StonetoraTraitId="intrinsic_stonetora_rock_hide";
    public const string MuntashaTraitId="intrinsic_muntasha_eclipse_afterimage";
    public const string SuntigerTraitId="intrinsic_suntiger_solar_mark";

    public const float HuntMarkDuration=5f;
    public const float HuntMarkDamageBonus=0.2f;
    public const float HuntMarkKillCooldownReductionRatio=0.35f;
    public const float RockShieldPerHit=0.08f;
    public const float RockShieldMaximum=0.24f;
    public const float RockShieldDuration=4f;
    public const float EclipseDelay=0.6f;
    public const float EclipseDamageRatio=0.45f;
    public const float SolarBurnDuration=3f;
    public const float SolarBurnDamageRatio=0.6f;
    public const float SolarBurnTickInterval=0.5f;

    public static float MarkedDamageMultiplier(bool marked,bool initialSlash)
    {
        return marked&&!initialSlash?1f+HuntMarkDamageBonus:1f;
    }

    public static float CooldownAfterMarkedKill(float remainingCooldown)
    {
        return Mathf.Max(0f,remainingCooldown)*(1f-HuntMarkKillCooldownReductionRatio);
    }

    public static float RockShieldRatio(int enemiesHit)
    {
        return Mathf.Min(RockShieldMaximum,Mathf.Max(0,enemiesHit)*RockShieldPerHit);
    }

    public static float EclipseDamage(float originalSlashDamage)
    {
        return Mathf.Max(0f,originalSlashDamage)*EclipseDamageRatio;
    }

    public static float SolarBurnDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*SolarBurnDamageRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,BumiTraitId,StringComparison.Ordinal))return "사냥의 반점";
        if(string.Equals(traitId,StonetoraTraitId,StringComparison.Ordinal))return "암석 외피";
        if(string.Equals(traitId,MuntashaTraitId,StringComparison.Ordinal))return "월식 잔영";
        if(string.Equals(traitId,SuntigerTraitId,StringComparison.Ordinal))return "태양 각인";
        return traitId??string.Empty;
    }
}
