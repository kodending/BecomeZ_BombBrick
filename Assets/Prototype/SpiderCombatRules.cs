using System;
using UnityEngine;

public static class SpiderCombatRules
{
    public const string SkillId="arachne_giant_capture_web";
    public const string BasicAttackId="basic_silk_needle";
    public const string AraneaTraitId="intrinsic_aranea_precise_knot";
    public const string SilantiTraitId="intrinsic_silanti_desert_silk";
    public const string RagonTraitId="intrinsic_ragon_toxic_prank";
    public const string IcearachneTraitId="intrinsic_icearachne_freezing_silk";

    public const float RequiredStayDuration=0.8f;
    public const float BaseRootDuration=1f;
    public const float PreciseKnotRootDuration=1.6f;
    public const float PreciseKnotPullDistanceTiles=0.75f;
    public const float BaseSlowRatio=0.35f;
    public const float FreezingSilkSlowRatio=0.5f;
    public const float FreezingSilkDuration=1.25f;
    public const float TraitTickInterval=1f;
    public const float DesertSilkDamageRatio=0.25f;
    public const int ToxicPrankStacks=3;
    public const float ToxicPrankDamageRatio=0.7f;

    public static float RootDuration(bool preciseKnot)
    {
        return preciseKnot?PreciseKnotRootDuration:BaseRootDuration;
    }

    public static float SlowRatio(bool freezingSilk)
    {
        return freezingSilk?FreezingSilkSlowRatio:BaseSlowRatio;
    }

    public static float DesertSilkDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*DesertSilkDamageRatio;
    }

    public static float ToxicPrankDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*ToxicPrankDamageRatio;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,AraneaTraitId,StringComparison.Ordinal))return "정교한 매듭";
        if(string.Equals(traitId,SilantiTraitId,StringComparison.Ordinal))return "사막의 실";
        if(string.Equals(traitId,RagonTraitId,StringComparison.Ordinal))return "독기 장난";
        if(string.Equals(traitId,IcearachneTraitId,StringComparison.Ordinal))return "빙결실";
        return traitId??string.Empty;
    }
}
