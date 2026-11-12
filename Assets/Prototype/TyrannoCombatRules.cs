using System;
using UnityEngine;

public static class TyrannoCombatRules
{
    public const string SkillId="tyranno_tyrant_tremor";
    public const string TinoTraitId="intrinsic_tino_feeding_instinct";
    public const string MagnoTraitId="intrinsic_magno_lava_footprint";
    public const string IronRexTraitId="intrinsic_cheolno_steel_skin";
    public const string BoneRexTraitId="intrinsic_bonrex_ancient_fissure";
    public const float TremorCastSeconds=1f;
    public const float Rank3CastSeconds=1.5f;
    public const int TremorWindupFrames=3;

    public const float FeedingBonusDamageRatio=0.6f;
    public const float FeedingHealRatio=0.05f;
    public const float LavaDuration=4f;
    public const float LavaTickInterval=1f;
    public const float LavaTickDamageRatio=0.25f;
    public const float ShieldPerHitRatio=0.2f;
    public const float ShieldMaximumRatio=0.6f;
    public const float ShieldDuration=5f;
    public const float FissureShredRatio=0.2f;
    public const float FissureDuration=5f;

    public static float FeedingBonusDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*FeedingBonusDamageRatio;
    }

    public static float FeedingHealing(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*FeedingHealRatio;
    }

    public static float SteelSkinShield(float defense,int hits)
    {
        return Mathf.Max(0f,defense)*
            Mathf.Min(ShieldMaximumRatio,Mathf.Max(0,hits)*ShieldPerHitRatio);
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,TinoTraitId,StringComparison.Ordinal))return "포식 본능";
        if(string.Equals(traitId,MagnoTraitId,StringComparison.Ordinal))return "용암 발자국";
        if(string.Equals(traitId,IronRexTraitId,StringComparison.Ordinal))return "강철 피부";
        if(string.Equals(traitId,BoneRexTraitId,StringComparison.Ordinal))return "고대의 균열";
        return traitId??string.Empty;
    }
}
