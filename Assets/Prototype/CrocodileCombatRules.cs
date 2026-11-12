using System;
using UnityEngine;

public static class CrocodileCombatRules
{
    public const string SkillId="crocodile_death_roll";
    public const string AgerTraitId="intrinsic_ager_tough_scales";
    public const string KimbulTraitId="intrinsic_kimbul_golden_feast";
    public const string KrogalTraitId="intrinsic_krogal_guardian_jaw";
    public const string TarvilTraitId="intrinsic_tarvil_underworld_mark";
    // Keep the persisted ID so existing Rank 3 selections become Prey Cannon.
    public const string PreyCannonTraitId="trait_crocodile_annihilation_spin";
    // Preserve the saved selection while replacing Reverse Hell with Crushing Roll.
    public const string CrushingRollTraitId="trait_crocodile_reverse_hell";
    public const string AbsoluteJawTraitId="trait_crocodile_absolute_jaw";

    public const int HitCount=3;
    public const float PrimaryDamageRatio=1f;
    public const float SecondaryDamageRatio=0.8f;
    public const float FinalDamageRatio=1.8f;
    public const float MissingHealthRatio=0.08f;
    public const float RollDuration=1f;
    public const float ToughScalesDamageReduction=0.35f;
    public const float GoldenFeastHealRatio=0.35f;
    public const float GuardianShieldRatio=0.12f;
    public const float GuardianShieldDuration=5f;
    public const float UnderworldMarkDuration=5f;
    public const float UnderworldHealingReduction=0.4f;
    public const float UnderworldKillCooldownRecovery=0.5f;
    public const float PreyCannonLaunchTime=0.43f;
    public const float PreyCannonDistanceTiles=2f;
    public const float PreyCannonFlightDuration=0.3f;
    public const float PreyCannonArcHeight=0.7f;
    public const float PreyCannonRadiusTiles=1f;
    public const float PreyCannonDamageRatio=1.2f;
    public const float PreyCannonKnockdownDuration=0.6f;
    public const int CrushingRollSweepCount=2;
    public const float CrushingRollDamageRatio=0.8f;
    public const float CrushingRollRadiusTiles=2f;
    public const float CrushingRollStart=0.08f;
    public const float CrushingRollSpinDuration=0.35f;
    public const float AbsoluteJawFinalDamageRatio=3.2f;
    public const float AbsoluteJawDefenseIgnoreRatio=0.5f;
    public const float AbsoluteJawDuration=RollDuration;
    public const float AbsoluteJawBraceDuration=0.15f;
    public const float TranscendentPreyCannonRadiusTiles=1.5f;
    public const float TranscendentFinalSweepDamageRatio=1.4f;
    public const float TranscendentAbsoluteJawDamageRatio=4f;

    public static float HitDamage(float attack,int hitIndex,float targetMissingHealth)
    {
        float damage=Mathf.Max(0f,attack)*
                     (hitIndex<=0?PrimaryDamageRatio:
                         hitIndex>=HitCount-1?FinalDamageRatio:SecondaryDamageRatio);
        return damage+(hitIndex>=HitCount-1
            ?Mathf.Max(0f,targetMissingHealth)*MissingHealthRatio:0f);
    }

    public static float GoldenFeastHealing(float damageDealt)
    {
        return Mathf.Max(0f,damageDealt)*GoldenFeastHealRatio;
    }

    public static bool TryPreyCannonContact(
        Vector3 from,Vector3 to,Vector3 center,float radius,out float fraction)
    {
        Vector3 step=to-from,offset=from-center;
        step.y=0f;offset.y=0f;
        fraction=0f;
        float c=offset.sqrMagnitude-radius*radius;
        if(c<=0f)return true;
        float a=step.sqrMagnitude,b=Vector3.Dot(offset,step);
        float discriminant=b*b-a*c;
        if(a<0.000001f||b>=0f||discriminant<0f)return false;
        fraction=(-b-Mathf.Sqrt(discriminant))/a;
        return fraction>=0f&&fraction<=1f;
    }

    public static float CooldownAfterMarkedDeath(float remainingCooldown)
    {
        return Mathf.Max(0f,remainingCooldown)*(1f-UnderworldKillCooldownRecovery);
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,AgerTraitId,StringComparison.Ordinal))return "질긴 비늘";
        if(string.Equals(traitId,KimbulTraitId,StringComparison.Ordinal))return "황금 포식";
        if(string.Equals(traitId,KrogalTraitId,StringComparison.Ordinal))return "수호의 턱";
        if(string.Equals(traitId,TarvilTraitId,StringComparison.Ordinal))return "저승 낙인";
        return traitId??string.Empty;
    }
}
