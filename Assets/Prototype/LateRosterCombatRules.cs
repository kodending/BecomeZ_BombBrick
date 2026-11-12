using UnityEngine;

public static class BeetleCombatRules
{
    public const string SkillId="beetle_sky_flip";
    public const string JangpungTraitId="intrinsic_jangpung_horn_mastery";
    public const string NoepungTraitId="intrinsic_noepung_lightning_induction";
    public const string AtlagonTraitId="intrinsic_atlagon_fortress_carapace";
    public const string SolarbeetleTraitId="intrinsic_solarbeetle_sun_horn";
    public const float HornDamageRatio=.5f;
    public const float LightningDamageRatio=.6f;
    public const int LightningChainTargets=2;
    public const float FortressDamageReduction=.25f;
    public const float FortressDuration=5f;
    public const float SunHornShredRatio=.15f;
    public const float SunHornDuration=5f;

    public static float Damage(float attackDamage,float ratio)
    {
        return Mathf.Max(0f,attackDamage)*Mathf.Max(0f,ratio);
    }

    public static string DisplayName(string traitId)
    {
        if(traitId==JangpungTraitId)return "충각의 달인";
        if(traitId==NoepungTraitId)return "낙뢰 유도";
        if(traitId==AtlagonTraitId)return "요새 갑각";
        if(traitId==SolarbeetleTraitId)return "태양 충각";
        return traitId;
    }
}

public static class SpiritDeerCombatRules
{
    public const string SkillId="spirit_deer_sanctuary";
    public const string HolydeerTraitId="intrinsic_holydeer_holy_antler";
    public const string LunadeerTraitId="intrinsic_lunadeer_moonlight_cycle";
    public const string ForestdeerTraitId="intrinsic_forestdeer_forest_pulse";
    public const string EclipsedeerTraitId="intrinsic_eclipsedeer_eclipse_shadow";
    public const float Windup=.35f,Spread=.38f;
    public const float PersonalityTargetChance=.8f;
    public const float MaximumSanctuaryRadiusTiles=2.5f;
    public const float ScanInterval=.2f;
    public const float HolyHealingCasterMaxHpRatio=.10f;
    public const float HolyShieldRatio=.08f;
    public const float HolyShieldDuration=4f;
    public const float HolyDebuffImmunityDuration=2f;
    public const float MoonHealingAttackRatio=1f;
    public const float MoonHealingMissingHpRatio=.12f;
    public const float ForestHealingMaxHpRatio=.06f;
    public const float ForestHaste=.20f;
    public const float ForestBuffDuration=4f;
    public const float EclipseHealingAttackRatio=.60f;
    public const float EclipseHealingReduction=.25f;
    public const float EclipseResistanceShred=.15f;
    public const float EclipseDebuffDuration=4f;

    public static float ClampSanctuaryRadiusTiles(float radiusTiles)
    {
        return Mathf.Clamp(radiusTiles,.1f,MaximumSanctuaryRadiusTiles);
    }

    public static float HolyHealing(float casterMaximumHealth)
    {
        return Mathf.Max(0f,casterMaximumHealth)*HolyHealingCasterMaxHpRatio;
    }

    public static float MoonHealing(float attackDamage,float missingHealth)
    {
        return Mathf.Max(0f,attackDamage)*MoonHealingAttackRatio+
               Mathf.Max(0f,missingHealth)*MoonHealingMissingHpRatio;
    }

    public static float ForestHealing(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*ForestHealingMaxHpRatio;
    }

    public static float EclipseHealing(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*EclipseHealingAttackRatio;
    }

    public static float HolyShield(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*HolyShieldRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(traitId==HolydeerTraitId)return "성스러운 뿔";
        if(traitId==LunadeerTraitId)return "월광 순환";
        if(traitId==ForestdeerTraitId)return "숲의 맥동";
        if(traitId==EclipsedeerTraitId)return "식의 그림자";
        return traitId;
    }
}

public static class WyvernCombatRules
{
    public const string SkillId="wyvern_breath";
    public const float BoltWidthTiles=.35f,BoltPierceRatio=.6f;
    public const float BoltHold=.08f,BoltFade=.1f;
    public const string VivreTraitId="intrinsic_vivre_venom_breath";
    public const string IgvireTraitId="intrinsic_igvire_fire_breath";
    public const string FrovireTraitId="intrinsic_frovire_glacier_breath";
    public const string TemvireTraitId="intrinsic_temvire_storm_breath";
    public const float Windup=.35f,BreathDuration=.65f;
    public const int BreathHits=4;
    public const float FirstHit=.16f;
    public static float HitTime(int index)=>FirstHit+index*(BreathDuration-FirstHit)/(BreathHits-1);
    public const float DotDuration=4f;
    public const float TickInterval=1f;
    public const float VenomTickRatio=.25f;
    public const float FireTickRatio=.2f;
    public const int ChillStacks=2;
    public const float ChillDuration=4f;
    public const float FreezeDuration=1f;
    public const float StormChainRatio=.5f;

    public static float Damage(float attackDamage,float ratio)
    {
        return Mathf.Max(0f,attackDamage)*Mathf.Max(0f,ratio);
    }

    public static string DisplayName(string traitId)
    {
        if(traitId==VivreTraitId)return "맹독 숨결";
        if(traitId==IgvireTraitId)return "화염 숨결";
        if(traitId==FrovireTraitId)return "빙하 숨결";
        if(traitId==TemvireTraitId)return "폭풍 숨결";
        return traitId;
    }
}

public static class HaetaeCombatRules
{
    public const string SkillId="haetae_justice_roar";
    public const string HaetaTraitId="intrinsic_haeta_clear_judgment";
    public const string YeomhaeTraitId="intrinsic_yeomhae_flame_judgment";
    public const string GeumgangsuTraitId="intrinsic_geumgangsu_diamond_protection";
    public const string MukryeongTraitId="intrinsic_mukryeong_karma_mark";
    public const float AttackReduction=.15f;
    public const float AttackReductionDuration=4f;
    public const float Windup=.35f,WaveDuration=.25f,HealingCastThreshold=.8f;
    public const float ClearHealingRatio=.6f;
    public static float WaveProgress(float time)=>1f-Mathf.Pow(1f-Mathf.Clamp01(time/WaveDuration),2f);
    public const float FlameDuration=4f;
    public const float FlameTickInterval=1f;
    public const float FlameTickRatio=.25f;
    public const float DiamondShieldRatio=.08f;
    public const float DiamondShieldDuration=5f;
    public const float KarmaHealingReduction=.4f;
    public const float KarmaRecoveryShieldReduction=.25f;
    public const float KarmaDuration=4f;

    public static float Damage(float attackDamage,float ratio)
    {
        return Mathf.Max(0f,attackDamage)*Mathf.Max(0f,ratio);
    }

    public static float ClearHealing(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*ClearHealingRatio;
    }

    public static float DiamondShield(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*DiamondShieldRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(traitId==HaetaTraitId)return "맑은 판결";
        if(traitId==YeomhaeTraitId)return "화염 심판";
        if(traitId==GeumgangsuTraitId)return "금강 수호";
        if(traitId==MukryeongTraitId)return "죄업 각인";
        return traitId;
    }
}
