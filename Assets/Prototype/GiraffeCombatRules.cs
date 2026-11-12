using System;
using UnityEngine;

public static class GiraffeCombatRules
{
    public const string SkillId="giraffe_high_head_slam";
    public const string GiriniTraitId="intrinsic_girini_high_sight";
    public const string GeriJacksonTraitId="intrinsic_gerijackson_moonwalk";
    public const string SerapiTraitId="intrinsic_serapi_soul_afterimage";
    public const string ToyrafTraitId="intrinsic_toyraf_block_assembly";

    public const float SlamDuration=0.68f;
    public const float StunDuration=0.6f;
    public const float CooldownReductionPerHit=0.7f;
    public const float MoonwalkDistanceTiles=1f;
    public const float MoonwalkAttackSpeedBonus=0.15f;
    public const float MoonwalkBuffDuration=3f;
    public const float SoulAfterimageDelay=1f;
    public const float SoulAfterimageDamageRatio=0.4f;
    public const int MaximumBlocks=3;
    public const float EmpoweredDamageMultiplier=1.35f;
    public const float EmpoweredRadiusMultiplier=1.35f;

    public static float CooldownAfterHits(float cooldown,int hits)
    {
        return Mathf.Max(0f,cooldown-Mathf.Max(0,hits)*CooldownReductionPerHit);
    }

    public static bool AdvanceAndConsumeEmpowerment(ref int blocks)
    {
        blocks=Mathf.Clamp(blocks+1,0,MaximumBlocks);
        if(blocks<MaximumBlocks)return false;
        blocks=0;
        return true;
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,GiriniTraitId,StringComparison.Ordinal))return "높은 시야";
        if(string.Equals(traitId,GeriJacksonTraitId,StringComparison.Ordinal))return "문워크";
        if(string.Equals(traitId,SerapiTraitId,StringComparison.Ordinal))return "영혼의 잔상";
        if(string.Equals(traitId,ToyrafTraitId,StringComparison.Ordinal))return "블록 조립";
        return traitId??string.Empty;
    }
}
