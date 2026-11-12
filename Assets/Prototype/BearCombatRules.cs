using System;
using UnityEngine;

public static class BearCombatRules
{
    public const string SkillId="bear_fighting_spirit";
    public const string MountainColossusTraitId="trait_bear_mountain_colossus";
    public const string RagingHundredStrikesTraitId=
        "trait_bear_raging_hundred_strikes";
    public const string KingOfBeastsRoarTraitId=
        "trait_bear_king_of_beasts_roar";
    public const string WoongiTraitId="intrinsic_woongi_persistent_spirit";
    public const string DamiTraitId="intrinsic_dami_warm_embrace";
    public const string IronKumaTraitId="intrinsic_ironkuma_iron_instinct";
    public const string BerberTraitId="intrinsic_berber_wild_frenzy";

    public const float ResolveDuration=4f;
    public const float PreparationDuration=.25f;
    public const float AvatarRoarPreparationDuration=.85f;
    public const float AvatarRoarImpactHoldDuration=.3f;
    public const float AvatarRoarTravelDuration=.62f;
    public const float ResolveAttackDamageBonus=0.2f;
    public const float ResolveAttackSpeedBonus=0.2f;
    public const float ResolveDamageReduction=0.15f;
    public const float BasicCleaveDamageRatio=0.3f;
    public const float WoongiExtensionPerHit=0.25f;
    public const float WoongiMaximumExtension=1.5f;
    public const float WoongiHealingRatio=0.01f;
    public const float DamiShieldRatio=0.1f;
    public const float DamiShieldDuration=6f;
    public const float IronKumaDefenseBonus=0.35f;
    public const float IronKumaDefenseDamageRatio=0.35f;
    public const int BerberMaximumStacks=5;
    public const float BerberAttackSpeedPerStack=0.06f;
    public const float MountainDuration=3f;
    public const float MountainVisualScale=1.35f;
    public const float MountainShieldRatio=0.15f;
    public const float MountainScaleTransitionDuration=0.2f;
    public const float MountainAttackRangeTiles=1.8f;
    public const float MountainAttackArcDegrees=180f;
    public const float MountainPrimaryDamageRatio=1.4f;
    public const float MountainSecondaryDamageRatio=1f;
    public const int RagingStrikeCount=3;
    public const float RagingFirstDamageRatio=0.7f;
    public const float RagingSecondDamageRatio=0.7f;
    public const float RagingThirdDamageRatio=1.5f;
    public const float RagingSecondHitTime=0.08f;
    public const float RagingThirdHitTime=0.24f;
    public const float RagingRecoveryDuration=0.18f;
    public const float RagingFinisherStaggerDuration=0.2f;
    public const float KingRoarStartRadiusTiles=3f;
    public const float KingRoarStartDamageRatio=1.8f;
    public const float KingRoarIntimidationDuration=4f;
    public const float KingRoarAttackSpeedSlow=0.2f;
    public const float KingRoarTravelDuration=0.36f;
    public const float KingRoarEndRadiusTiles=3f;
    public const float KingRoarEndDamageRatio=2.2f;
    public const float KingRoarEndKnockdownDuration=0.8f;
    public const float KingRoarBerberEndDamageBonus=0.5f;

    public static float RagingStrikeDamage(float basicAttackDamage,int strikeIndex)
    {
        if(strikeIndex==0)return Mathf.Max(0f,basicAttackDamage)*RagingFirstDamageRatio;
        if(strikeIndex==1)
            return Mathf.Max(0f,basicAttackDamage)*RagingSecondDamageRatio;
        if(strikeIndex==2)
            return Mathf.Max(0f,basicAttackDamage)*RagingThirdDamageRatio;
        return 0f;
    }

    public static float KingRoarEndDamage(float attackDamage,int frenzyStacks)
    {
        float damage=Mathf.Max(0f,attackDamage)*KingRoarEndDamageRatio;
        return frenzyStacks>=BerberMaximumStacks
            ?damage*(1f+KingRoarBerberEndDamageBonus):damage;
    }

    public static float MountainShield(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*MountainShieldRatio;
    }

    public static bool IsInsideMountainAttackArc(Vector3 forward,Vector3 direction)
    {
        forward.y=0f;direction.y=0f;
        if(forward.sqrMagnitude<0.0001f||direction.sqrMagnitude<0.0001f)
            return true;
        float minimumDot=Mathf.Cos(MountainAttackArcDegrees*0.5f*Mathf.Deg2Rad);
        return Vector3.Dot(forward.normalized,direction.normalized)>=minimumDot-0.0001f;
    }

    public static float DamageTakenMultiplier(bool resolveActive)
    {
        return resolveActive?1f-ResolveDamageReduction:1f;
    }

    public static float WoongiHealing(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*WoongiHealingRatio;
    }

    public static float DamiShield(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*DamiShieldRatio;
    }

    public static float IronKumaDamage(float defense)
    {
        return Mathf.Max(0f,defense)*IronKumaDefenseDamageRatio;
    }

    public static float BerberAttackSpeed(int stacks)
    {
        return Mathf.Clamp(stacks,0,BerberMaximumStacks)*BerberAttackSpeedPerStack;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,MountainColossusTraitId,StringComparison.Ordinal))
            return "산맥 거신";
        if(string.Equals(traitId,RagingHundredStrikesTraitId,StringComparison.Ordinal))
            return "폭주 백련타";
        if(string.Equals(traitId,KingOfBeastsRoarTraitId,StringComparison.Ordinal))
            return "백수왕의 포효";
        if(string.Equals(traitId,WoongiTraitId,StringComparison.Ordinal))return "끈질긴 투지";
        if(string.Equals(traitId,DamiTraitId,StringComparison.Ordinal))return "포근한 품";
        if(string.Equals(traitId,IronKumaTraitId,StringComparison.Ordinal))return "철갑 본능";
        if(string.Equals(traitId,BerberTraitId,StringComparison.Ordinal))return "야성 폭주";
        return traitId??string.Empty;
    }
}
