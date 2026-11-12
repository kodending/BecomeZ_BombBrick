using UnityEngine;

public static class MammothCombatRules
{
    public const string SkillId="mammoth_earth_charge";
    public const string KogasonTraitId="intrinsic_kogason_overwhelming_weight";
    public const string SeolgasonTraitId="intrinsic_seolgason_glacier_trail";
    public const string IrontuskTraitId="intrinsic_irontusk_steel_tusk";
    public const string GaiasonTraitId="intrinsic_gaiason_ancestor_path";

    public const float MaximumChargeDistanceTiles=3.5f;
    public const float Windup=.35f,ChargeDuration=.6f,Recovery=.22f;
    public const float ContactTime=Windup+ChargeDuration,EndTime=ContactTime+Recovery;
    public const float KnockbackDistanceTiles=0.75f;
    public const float KogasonShieldPerEnemyRatio=0.03f;
    public const float KogasonShieldDuration=5f;
    public const float GlacierTrailDuration=4f;
    public const float GlacierSlowRatio=0.35f;
    public const float GlacierSlowRefreshDuration=0.35f;
    public const float SteelTuskDefenseRatio=.35f;
    public const float AncestorShieldRatio=0.08f;
    public const float AncestorMoveSpeedBonus=0.2f;
    public const float AncestorBuffDuration=4f;

    public static float Damage(
        float skillPower,float multiplier,float defense,bool steelTusk)
    {
        return Mathf.Max(0f,skillPower)*Mathf.Max(0f,multiplier)+
               (steelTusk?Mathf.Max(0f,defense)*SteelTuskDefenseRatio:0f);
    }

    public static float ChargeProgress(float time)
    {
        float t=Mathf.Clamp01(time/ChargeDuration);
        return t*t*(1.6f-.6f*t);
    }

    public static float KogasonShield(float maximumHealth,int uniqueEnemiesHit)
    {
        return Mathf.Max(0f,maximumHealth)*KogasonShieldPerEnemyRatio*
               Mathf.Max(0,uniqueEnemiesHit);
    }

    public static float AncestorShield(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*AncestorShieldRatio;
    }

    public static Vector3 ChargeEnd(Vector3 start,Vector3 direction,float tileSize)
    {
        Vector3 flat=new Vector3(direction.x,0f,direction.z);
        if(flat.sqrMagnitude<0.001f)flat=Vector3.forward;
        return start+flat.normalized*Mathf.Max(0f,tileSize)*MaximumChargeDistanceTiles;
    }

    public static string DisplayName(string traitId)
    {
        if(traitId==KogasonTraitId)return "압도적 중량";
        if(traitId==SeolgasonTraitId)return "빙하의 자취";
        if(traitId==IrontuskTraitId)return "강철 상아";
        if(traitId==GaiasonTraitId)return "선조의 길";
        return traitId;
    }
}
