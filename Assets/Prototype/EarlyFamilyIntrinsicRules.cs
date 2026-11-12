using System;
using UnityEngine;

public static class RaptorCombatRules
{
    // Keep the saved trait ID so existing Rank 3 monsters receive the replacement.
    public const string PiercingHuntTraitId="trait_raptor_blood_trail";
    public const string RelentlessHuntTraitId="trait_raptor_chain_leap";
    public const string ArmorHuntTraitId="trait_raptor_pack_target";
    public const string VelosTraitId="intrinsic_velos_chain_hunt";
    public const string VelokTraitId="intrinsic_velok_demonic_pursuit";
    public const string RaputoTraitId="intrinsic_raputo_scorpion_venom";
    public const string WiloTraitId="intrinsic_wilo_ice_carapace";

    public const float ChainHuntCooldownRatio=0.5f;
    public const int RelentlessStrikeCount=3;
    public const float RelentlessDuration=0.4f;
    public const float LeapWindupDuration=0.04f;
    public const float LeapImpactHoldDuration=0.025f;
    public const float LeapReturnDuration=0.08f;
    public const float LeapApproachArcHeight=0.22f;
    public const float LeapReturnArcHeight=0.08f;
    public const float ArmorBreakDuration=4f;
    public const float ArmorBreakRatio=0.5f;
    public const float ExecuteHealthThreshold=0.35f;
    public const float ExecuteDamageBonus=0.4f;
    public const float VenomDuration=4f;
    public const float VenomTickInterval=1f;
    public const float VenomDamagePerTickRatio=0.12f;
    public const float IceShieldRatio=0.15f;
    public const float IceShieldDuration=4f;
    public const float IceSlowDuration=2f;
    public const float IceSlowRefreshDuration=0.25f;
    public const float IceSlowRatio=0.25f;
    public const float IceChillDuration=4f;
    public const int IceChillStacksToFreeze=3;
    public const float IceFreezeDuration=1f;
    public const float ApexPredatorDuration=4f;
    public const float ApexPredatorAttackSpeedBonus=0.40f;
    public const float ApexPredatorBasicDamageBonus=0.60f;
    public const float ApexPredatorCooldownReduction=0.5f;
    public const float ApexPredatorKillExtension=2f;

    public static float LeapApproachDuration(float configuredDuration)
    {
        // Both travel and sprite hit timing must use the same accelerated duration.
        return Mathf.Max(0.05f,configuredDuration*0.5f);
    }

    public static Vector3 PiercingLandingPosition(
        Vector3 start,Vector3 target,float attackDistance)
    {
        Vector3 direction=target-start;direction.y=0f;
        if(direction.sqrMagnitude<0.001f)direction=Vector3.right;
        return target+direction.normalized*Mathf.Max(0f,attackDistance);
    }

    public static bool IsInPiercingSweep(
        Vector3 target,Vector3 previous,Vector3 current,float attackDistance)
    {
        target.y=previous.y=current.y=0f;
        return WolfCombatRules.DistanceToSegment(target,previous,current)<=
            Mathf.Max(0f,attackDistance);
    }

    public static float ApexPredatorBasicDamage(float damage,bool active)
    {
        return Mathf.Max(0f,damage)*(active?1f+ApexPredatorBasicDamageBonus:1f);
    }

    public static float ApexPredatorCooldown(float remaining,bool active)
    {
        return Mathf.Max(0f,remaining-(active?ApexPredatorCooldownReduction:0f));
    }

    public static float LeapDamageMultiplier(string traitId,float targetHealthRatio)
    {
        return string.Equals(traitId,VelokTraitId,StringComparison.Ordinal)&&
               targetHealthRatio<=ExecuteHealthThreshold
            ?1f+ExecuteDamageBonus:1f;
    }

    public static float CooldownAfterLeapKill(float cooldown)
    {
        return Mathf.Max(0f,cooldown)*ChainHuntCooldownRatio;
    }

    public static float RelentlessStrikeDamageMultiplier(int strike)
    {
        return strike==0||strike==1?0.6f:strike==2?1f:0f;
    }

    public static float RelentlessStrikeTime(int strike)
    {
        return strike==0?0f:strike==1?0.14f:0.32f;
    }

    public static float PhysicalEffectiveHealth(float currentHealth,float defense)
    {
        return Mathf.Max(0f,currentHealth)*
               (1f+Mathf.Max(0f,defense)/100f);
    }

    public static Vector3 LeapImpactPosition(
        Vector3 lockedPosition,Vector3 currentTargetPosition,bool targetActive)
    {
        return targetActive?currentTargetPosition:lockedPosition;
    }

    public static Vector3 LeapContactPosition(
        Vector3 sourcePosition,Vector3 targetPosition,
        float sourceRadius,float targetRadius)
    {
        Vector3 away=sourcePosition-targetPosition;away.y=0f;
        if(away.sqrMagnitude<0.001f)away=Vector3.left;
        return targetPosition+away.normalized*
            (Mathf.Max(0f,sourceRadius)+Mathf.Max(0f,targetRadius));
    }

    public static Vector3 LeapReturnPosition(
        Vector3 contactPosition,Vector3 targetPosition,float attackDistance)
    {
        Vector3 away=contactPosition-targetPosition;away.y=0f;
        if(away.sqrMagnitude<0.001f)away=Vector3.left;
        return targetPosition+away.normalized*Mathf.Max(0f,attackDistance);
    }

    public static float VenomTickDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*VenomDamagePerTickRatio;
    }

    public static float IceShield(float maximumHealth,float gainMultiplier=1f)
    {
        return Mathf.Max(0f,maximumHealth)*IceShieldRatio*
               Mathf.Max(0f,gainMultiplier);
    }

    public static float IceSpeedMultiplier(bool slowed)
    {
        return slowed?1f-IceSlowRatio:1f;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,VelosTraitId,StringComparison.Ordinal))return "연속 사냥";
        if(string.Equals(traitId,VelokTraitId,StringComparison.Ordinal))return "악마의 추격";
        if(string.Equals(traitId,RaputoTraitId,StringComparison.Ordinal))return "전갈 독침";
        if(string.Equals(traitId,WiloTraitId,StringComparison.Ordinal))return "얼음 갑각";
        return traitId??string.Empty;
    }
}

public enum FrogStolenBuffKind
{
    None,Shield,AttackDamage,AttackSpeed,Defense,MoveSpeed,Resistance,DefenseRatio,ResistanceRatio,DamageReduction
}

public sealed class FrogStolenBuff
{
    public FrogStolenBuffKind kind;
    public float magnitude;
    public float remaining;
}

public static class FrogCombatRules
{
    public const string SkillId="frog_tongue_harpoon";
    public const string ReflexSnatchTraitId="trait_frog_reflex_snatch";
    public const string BuffThiefTraitId="trait_frog_buff_thief";
    // Preserve the saved trait ID while replacing Hunting Chorus.
    public const string TongueSlamTraitId="trait_frog_hunting_chorus";
    public const string GolgaebiTraitId="intrinsic_golgaebi_ancient_mucus";
    public const string KingpaTraitId="intrinsic_kingpa_royal_decree";
    public const string RokrokTraitId="intrinsic_rokrok_explosive_saliva";
    public const string FroskTraitId="intrinsic_frosk_soul_recovery";

    public const float PullDistanceTiles=0.75f;
    public const float RecoilDistanceTiles=1.5f;
    public const float TongueTensionDuration=0.12f;
    public const float PullDuration=0.45f;
    public const float RecoilHopHeight=0.55f;
    public const float AncientMucusDefenseReduction=0.2f;
    public const float AncientMucusDuration=4f;
    public const float RoyalAttackSpeedBonus=0.15f;
    public const float RoyalResistanceBonus=0.15f;
    public const float RoyalBuffDuration=3f;
    public const float RoyalBuffRadiusTiles=2.5f;
    public const float ExplosiveSalivaRadiusTiles=1.2f;
    public const float ExplosiveSalivaDamageRatio=0.6f;
    public const float SoulHealingRatio=0.3f;
    public const float BuffTheftDuration=4f;
    public const float ShieldTheftMaximumHealthRatio=0.12f;
    public const float FallbackAttackSpeedTheft=0.15f;
    public const float BossBuffCopyRatio=0.5f;
    public const float SlamDuration=0.7f;
    public const float SlamLiftDuration=0.35f;
    public const float SlamHoldDuration=0.15f;
    public const float SlamHeight=2.4f;
    public const float SlamKnockdownDuration=0.8f;
    public const float TranscendentBranchRadiusTiles=3f;
    public const float TranscendentSecondaryDamageRatio=0.5f;
    public const float TranscendentChargeDrainRatio=0.75f;
    public const float TranscendentKnockdownDuration=1f;

    public static float SoulHealing(float skillDamage)
    {
        return Mathf.Max(0f,skillDamage)*SoulHealingRatio;
    }

    public static float StolenMagnitude(float magnitude,bool boss)
    {
        return Mathf.Max(0f,magnitude)*(boss?BossBuffCopyRatio:1f);
    }

    public static float StolenShield(float maximumHealth,float available,bool boss)
    {
        return StolenMagnitude(Mathf.Min(
            Mathf.Max(0f,available),Mathf.Max(0f,maximumHealth)*
            ShieldTheftMaximumHealthRatio),boss);
    }

    public static float SlamArcHeight(float progress)
    {
        float elapsed=Mathf.Clamp01(progress)*SlamDuration;
        if(elapsed<SlamLiftDuration)
            return Mathf.SmoothStep(0f,SlamHeight,elapsed/SlamLiftDuration);
        float fall=Mathf.Clamp01((elapsed-SlamLiftDuration-SlamHoldDuration)/
            (SlamDuration-SlamLiftDuration-SlamHoldDuration));
        return SlamHeight*(1f-fall*fall);
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,GolgaebiTraitId,StringComparison.Ordinal))return "고대 점액";
        if(string.Equals(traitId,KingpaTraitId,StringComparison.Ordinal))return "왕실 칙령";
        if(string.Equals(traitId,RokrokTraitId,StringComparison.Ordinal))return "폭발 타액";
        if(string.Equals(traitId,FroskTraitId,StringComparison.Ordinal))return "영혼 회수";
        return traitId??string.Empty;
    }
}

public static class KingKongCombatRules
{
    public const string SkillId="kingkong_earth_pulse";
    public const string TripleEarthPulseTraitId="trait_kingkong_triple_earth_pulse";
    public const string ContinentalFissureTraitId="trait_kingkong_continental_fissure";
    public const string WorldOverturnTraitId="trait_kingkong_world_overturn";
    public const string LilasTraitId="intrinsic_lilas_royal_pressure";
    public const string MagrilTraitId="intrinsic_magril_lava_fissure";
    public const string BingareuTraitId="intrinsic_bingareu_glacial_pulse";
    public const string MoskaTraitId="intrinsic_moska_life_resonance";

    public const float AirborneDuration=0.6f;
    public const float ComboAirborneBonusDuration=0.4f;
    public const float ComboDamageMultiplier=1.25f;
    public const float LilasAttackReduction=0.15f;
    public const float LilasWeakDuration=4f;
    public const float LilasShieldPerHitRatio=0.03f;
    public const float LilasShieldDuration=5f;
    public const float LavaDuration=4f;
    public const float LavaTickInterval=1f;
    public const float LavaTickDamageRatio=0.25f;
    public const float LavaComboBonus=0.6f;
    public const float GlacialSlowRatio=0.25f;
    public const float GlacialSlowDuration=3f;
    public const int GlacialChillStacksToFreeze=3;
    public const float GlacialFreezeDuration=1f;
    public const float GlacialComboFreezeDuration=1.25f;
    public const float GlacialComboDamageBonus=0.2f;
    public const float LifeHealingRatio=0.25f;
    public const float RallyAttackSpeedBonus=0.15f;
    public const float RallyDuration=4f;
    public const float TriplePulseFollowupDelay=0.44f;
    public const float TriplePulseFollowupRadiusTiles=2.7f;
    public const float TriplePulseFollowupDamageRatio=0.7f;
    public const float TriplePulseFinalDelay=0.51f;
    public const float TriplePulseFinalRadiusTiles=3.2f;
    public const float TriplePulseFinalDamageRatio=1f;
    public const float ContinentalFissureRadiusTiles=5f;
    public const float ContinentalFissureTravelDuration=0.35f;
    public const float ContinentalFissureDamageRatio=1.2f;
    public const float WorldOverturnRadiusTiles=3f;
    public const float WorldOverturnWindupDuration=0.45f;
    public const float WorldOverturnDamageRatio=2f;
    public const float WorldOverturnKnockdownDuration=1f;

    public static float PulseLiftHeight(float progress)
    {
        float t=Mathf.Clamp01(progress);
        return t<0.28f?0.75f*(1f-Mathf.Pow(1f-t/0.28f,2f)):
            0.75f*(1f-Mathf.Pow((t-0.28f)/0.72f,2f));
    }

    public static bool InsideContinentalFissure(
        Vector3 offset,float radius,float fromRadius=0f)
    {
        offset.y=0f;
        radius=Mathf.Max(0f,radius);fromRadius=Mathf.Max(0f,fromRadius);
        return offset.sqrMagnitude>=fromRadius*fromRadius&&
               offset.sqrMagnitude<=radius*radius;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,LilasTraitId,StringComparison.Ordinal))return "왕의 위압";
        if(string.Equals(traitId,MagrilTraitId,StringComparison.Ordinal))return "용암 균열";
        if(string.Equals(traitId,BingareuTraitId,StringComparison.Ordinal))return "빙하 고동";
        if(string.Equals(traitId,MoskaTraitId,StringComparison.Ordinal))return "생명의 진동";
        return traitId??string.Empty;
    }
}

public static class WizardMouseCombatRules
{
    public const string SkillId="wizard_mouse_magic_star_barrage";
    public const string MilkyWayBarrageTraitId="trait_wizard_mouse_milky_way_barrage";
    public const string PentagramAnnihilationTraitId=
        "trait_wizard_mouse_pentagram_annihilation";
    public const string SupernovaDescentTraitId=
        "trait_wizard_mouse_supernova_descent";
    public const string KongjwiTraitId="intrinsic_kongjwi_grateful_starlight";
    public const string PatjwiTraitId="intrinsic_patjwi_spiteful_focus";
    public const string SeoljwiTraitId="intrinsic_seoljwi_frost_star";
    public const string ByeoljwiTraitId="intrinsic_byeoljwi_star_refraction";
    public const int ProjectileCount=5;
    public const float PreparationDuration=0.25f;
    public const float ProjectileInterval=0.12f;
    public const float FocusLastProjectileMultiplier=1.4f;
    public const float HealingBaseRatio=0.6f;
    public const float HealingPerTargetRatio=0.15f;
    public const float HealingMaximumRatio=1.2f;
    public const int FrostStacksToFreeze=3;
    public const float FrostDuration=3f;
    public const float FreezeDuration=1f;
    public const float RefractionDamageRatio=0.25f;
    public const int MilkyWayFragmentCount=2;
    public const float MilkyWayFragmentDamageRatio=0.42f;
    public const float MilkyWayFragmentRadiusTiles=3f;
    public const float MilkyWayFragmentFlightDuration=0.45f;
    public const float PentagramRadiusTiles=2.2f;
    public const int PentagramPulseCount=3;
    public const float PentagramPulseInterval=1f;
    public const float PentagramPulseDamageRatio=1f;
    public const float SupernovaAscentDuration=0.32f;
    public const float SupernovaDelay=0.55f;
    public const float SupernovaPullDuration=0.25f;
    public const float SupernovaRadiusTiles=2.5f;
    public const float SupernovaDamageRatio=2.5f;
    public const float SupernovaPullTiles=0.75f;
    public const float SupernovaKnockdownDuration=1f;
    public const float SupernovaRefractionRadiusTiles=3f;

    public static float MilkyWayFragmentDamage(float attack)
    {
        return Mathf.Max(0f,attack)*MilkyWayFragmentDamageRatio;
    }

    public static float PentagramPulseDamage(float attack)
    {
        return Mathf.Max(0f,attack)*PentagramPulseDamageRatio;
    }

    public static float SupernovaDamage(float attack)
    {
        return Mathf.Max(0f,attack)*SupernovaDamageRatio;
    }

    public static float SupernovaStoredStarDamage(float attack,float starRatio,bool focused)
    {
        float damage=0f;
        for(int i=0;i<ProjectileCount;i++)damage+=ProjectileDamage(attack,starRatio,focused,i);
        return damage;
    }

    public static Color SupernovaColor(string monsterId)
    {
        if(string.Equals(monsterId,"kongjwi",StringComparison.Ordinal))
            return new Color(1f,0.78f,0.22f);
        if(string.Equals(monsterId,"patjwi",StringComparison.Ordinal))
            return new Color(1f,0.16f,0.42f);
        if(string.Equals(monsterId,"seoljwi",StringComparison.Ordinal))
            return new Color(0.22f,0.82f,1f);
        if(string.Equals(monsterId,"byeoljwi",StringComparison.Ordinal))
            return new Color(0.56f,0.34f,1f);
        return new Color(0.96f,0.34f,1f);
    }

    public static float ProjectileDamage(
        float attack,float ratio,bool focused,int index)
    {
        float scale=1f;
        if(focused&&index==ProjectileCount-1)scale*=FocusLastProjectileMultiplier;
        return Mathf.Max(0f,attack)*Mathf.Max(0f,ratio)*scale;
    }

    public static float GratefulHealing(float attack,int distinctTargets)
    {
        float ratio=Mathf.Min(
            HealingMaximumRatio,
            HealingBaseRatio+Mathf.Max(0,distinctTargets)*HealingPerTargetRatio);
        return Mathf.Max(0f,attack)*ratio;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,KongjwiTraitId,StringComparison.Ordinal))return "보은의 별빛";
        if(string.Equals(traitId,PatjwiTraitId,StringComparison.Ordinal))return "심술 집중";
        if(string.Equals(traitId,SeoljwiTraitId,StringComparison.Ordinal))return "서리별";
        if(string.Equals(traitId,ByeoljwiTraitId,StringComparison.Ordinal))return "별의 굴절";
        return traitId??string.Empty;
    }
}
