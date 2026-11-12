using System;
using UnityEngine;

public static class SabertoothCombatRules
{
    public const string SkillId="sabertooth_fang_lock";
    public const string GeomchiTraitId="intrinsic_geomchi_predatory_recovery";
    public const string SeolaTraitId="intrinsic_seola_frost_fang";
    public const string NoxTraitId="intrinsic_nox_night_execution";
    public const string TerakanTraitId="intrinsic_terakan_stone_fang";
    // Keep the original ID so existing rank-3 selections adopt the renewed trait.
    public const string ThroatLockTraitId="trait_sabertooth_deep_fang_mark";
    public const string PreyDriveTraitId="trait_sabertooth_relentless_fang";
    public const string BloodRefluxTraitId="trait_sabertooth_capture_signal";

    public const int HitCount=3;
    public const float TargetMaxHealthDamageRatio=0.20f;
    public const float PredatoryRecoveryRatio=0.35f;
    public const float FrostDuration=4f;
    public const int FrostStacksForFreeze=3;
    public const float FreezeDuration=1f;
    public const float ExecutionHealthThreshold=0.35f;
    public const float ExecutionDefenseIgnoreRatio=0.60f;
    public const float ExecutionFangEffectScale=1.5f;
    public const float StoneDefenseBonus=0.35f;
    public const float StoneShieldRatio=0.6f;
    public const float StoneShieldDuration=4f;
    public const float SilenceAfterReleaseDuration=1.5f;
    public const float PreyDragDistanceTiles=1.5f;
    // Finish the gradual pull before Fang Lock's final hit at 1.2 seconds.
    public const float PreyDragDuration=1f;
    public const float PreyAssistRadiusTiles=3f;
    public const float PreyFocusDuration=2.5f;
    public const float BloodRefluxRatio=0.4f;

    public static bool HasFangLockDebuff(CombatStatusState statuses)
    {
        CombatStatusEffect root=statuses?.Get(CombatStatusKind.Root);
        return root!=null&&root.stacks>0&&root.magnitude>0f;
    }

    public static SkillDamageFormulaResult DamagePerHit(
        float attackDamage,float damageMultiplier,float targetMaximumHealth)
    {
        return new SkillDamageFormulaResult(
            Mathf.Max(0f,attackDamage)*Mathf.Max(0f,damageMultiplier)/HitCount,
            Mathf.Max(0f,targetMaximumHealth)*TargetMaxHealthDamageRatio/HitCount,0f);
    }

    public static float PredatoryRecovery(float damageTaken)
    {
        return Mathf.Max(0f,damageTaken)*PredatoryRecoveryRatio;
    }

    public static float StoneShield(float defense)
    {
        return Mathf.Max(0f,defense)*StoneShieldRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,GeomchiTraitId,StringComparison.Ordinal))return "포식 회복";
        if(string.Equals(traitId,SeolaTraitId,StringComparison.Ordinal))return "빙결 송곳니";
        if(string.Equals(traitId,NoxTraitId,StringComparison.Ordinal))return "야간 처형";
        if(string.Equals(traitId,TerakanTraitId,StringComparison.Ordinal))return "암석 엄니";
        return traitId??string.Empty;
    }
}
