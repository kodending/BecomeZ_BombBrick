using System;
using UnityEngine;

public static class WolfCombatRules
{
    public const string SkillId="wolf_three_sided_hunt";
    public const string NukguTraitId="intrinsic_nukgu_relentless_chase";
    public const string SeollangTraitId="intrinsic_seollang_frost_claw";
    public const string HongaTraitId="intrinsic_honga_crimson_fang";
    public const string WolyeongTraitId="intrinsic_wolyeong_moon_shadow_afterimage";
    public const string PackRushTraitId=
        "trait_wolf_annihilation_triangle";
    public const string CelestialSpectralPackTraitId=
        "trait_wolf_celestial_spectral_pack";
    public const string EclipsePackTraitId="trait_wolf_eclipse_pack";

    public const int HitCount=3;
    public const float HitInterval=0.18f;
    public const float HuntDashDuration=0.12f;
    public const float HuntPreparationDuration=0.1f;
    public const float HuntFinalHoldDuration=0.04f;
    public const float HuntIdleDuration=0.28f;
    public const float AfterimageDuration=0.18f;
    public const float HuntScatterDuration=0.46f;

    public static Vector3 HuntVertex(Vector3 target,Vector3 caster,int vertex)
    {
        Vector3 offset=caster-target;offset.y=0f;
        if(offset.sqrMagnitude<.0001f)offset=Vector3.back;
        return target+Quaternion.AngleAxis(vertex*120f,Vector3.up)*offset;
    }
    public const float ChaseDuration=4f;
    public const float ChaseMoveSpeedBonus=0.3f;
    public const float ChaseBasicDamageBonus=0.15f;
    public const float FrostDuration=3f;
    public const int FrostStacksToFreeze=3;
    public const float FreezeDuration=0.8f;
    public const float CrimsonMissingHealthRatio=0.08f;
    public const float CrimsonKillCooldownMultiplier=0.5f;
    public const float AfterimageShieldRatio=0.04f;
    public const float AfterimageShieldMaximum=0.08f;
    public const float AfterimageShieldDuration=4f;
    public const float RushDamageRatio=2.4f;
    public const float RushWidthTiles=3f;
    public const float RushSpeedTiles=24f;
    public const int SpectralSpiritCount=8;
    public const float SpectralSpiritInterval=0.04f;
    public const float SpectralSpiritDamageRatio=0.3f;
    public const float SpectralSpiritTravelDuration=0.065f;
    public const float SpectralExitDuration=0.065f;
    public const float SpectralContactHoldDuration=0.035f;
    public const float SpectralFormationRadiusTiles=1.5f;
    public const float SpectralReadyDuration=0.06f;
    public const float SpectralExecutionDamageRatio=1.8f;
    public const float SpectralExecutionDefenseIgnoreRatio=0.3f;
    public const float SpectralCastLockDuration=1.3f;
    public const float EnhancedChaseDuration=6f;
    public const float EnhancedChaseMoveSpeedBonus=0.45f;
    public const float EnhancedChaseBasicDamageBonus=0.3f;
    public const float EclipseDuration=4f;
    public const float EclipseCloneDamageRatio=0.55f;
    public const float EclipseFollowDistanceTiles=0.55f;
    public const float EclipseHitDelay=0.06f;
    public const float EclipseReturnDuration=0.12f;
    public const float EclipseIdleDuration=0.2f;
    public const float EclipseBlinkDuration=0.9f;
    public const float EclipseBurstRadiusTiles=1.5f;

    public static float EclipseCloneDamage(
        float attack,bool chaseActive,bool enhancedChase=false)
    {
        return ChaseBasicDamage(attack,chaseActive,enhancedChase)*
               EclipseCloneDamageRatio;
    }

    public static float SpectralSpiritDamage(float attack)
    {
        return Mathf.Max(0f,attack)*SpectralSpiritDamageRatio;
    }

    public static float SpectralExecutionDamage(
        float attack,float missingHealth,bool crimsonFang)
    {
        return Mathf.Max(0f,attack)*SpectralExecutionDamageRatio+
               (crimsonFang
                   ?Mathf.Max(0f,missingHealth)*CrimsonMissingHealthRatio:0f);
    }

    public static bool SpectralSpiritAddsFrost(int zeroBasedHit)
    {
        return zeroBasedHit>=0&&(zeroBasedHit+1)%3==0;
    }

    public static float ChaseMoveSpeedBonusFor(bool enhanced)
    {
        return enhanced?EnhancedChaseMoveSpeedBonus:ChaseMoveSpeedBonus;
    }

    public static float RushDamage(
        float attack,float missingHealth,bool crimsonFang)
    {
        return Mathf.Max(0f,attack)*RushDamageRatio+
               (crimsonFang
                   ?Mathf.Max(0f,missingHealth)*CrimsonMissingHealthRatio:0f);
    }

    public static float RushDistanceToBoundary(Vector3 origin,Vector3 forward,Bounds bounds)
    {
        float distance=float.PositiveInfinity;
        if(Mathf.Abs(forward.x)>.0001f)
            distance=Mathf.Min(distance,((forward.x>0f?bounds.max.x:bounds.min.x)-origin.x)/forward.x);
        if(Mathf.Abs(forward.z)>.0001f)
            distance=Mathf.Min(distance,((forward.z>0f?bounds.max.z:bounds.min.z)-origin.z)/forward.z);
        return float.IsInfinity(distance)?0f:Mathf.Max(0f,distance);
    }

    public static bool IsInsideRushSweep(Vector3 point,Vector3 origin,Vector3 forward,
        float previous,float current,float tileSize,float collisionRadius)
    {
        Vector3 offset=point-origin;offset.y=0f;
        float along=Vector3.Dot(offset,forward);
        return along>=0f&&along+collisionRadius>=previous&&along-collisionRadius<=current&&
            (offset-forward*along).magnitude<=RushWidthTiles*.5f*tileSize+collisionRadius;
    }

    public static float DistanceToSegment(Vector3 point,Vector3 start,Vector3 end)
    {
        point.y=0f;start.y=0f;end.y=0f;
        Vector3 segment=end-start;
        if(segment.sqrMagnitude<0.0001f)return Vector3.Distance(point,start);
        float t=Mathf.Clamp01(Vector3.Dot(point-start,segment)/segment.sqrMagnitude);
        return Vector3.Distance(point,start+segment*t);
    }

    public static float HitDamage(
        float attack,float ratio,float missingHealth,bool finalHit,bool crimsonFang)
    {
        return Mathf.Max(0f,attack)*Mathf.Max(0f,ratio)+
               (finalHit&&crimsonFang
                   ?Mathf.Max(0f,missingHealth)*CrimsonMissingHealthRatio:0f);
    }

    public static float ChaseBasicDamage(
        float damage,bool active,bool enhanced=false)
    {
        return Mathf.Max(0f,damage)*(active
            ?1f+(enhanced?EnhancedChaseBasicDamageBonus:ChaseBasicDamageBonus):1f);
    }

    public static float AfterimageShield(float maximumHealth,int afterimageHits)
    {
        return Mathf.Max(0f,maximumHealth)*Mathf.Min(
            AfterimageShieldMaximum,
            Mathf.Max(0,afterimageHits)*AfterimageShieldRatio);
    }

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,NukguTraitId,StringComparison.Ordinal))return "끈질긴 추적";
        if(string.Equals(traitId,SeollangTraitId,StringComparison.Ordinal))return "서리 발톱";
        if(string.Equals(traitId,HongaTraitId,StringComparison.Ordinal))return "붉은 송곳니";
        if(string.Equals(traitId,WolyeongTraitId,StringComparison.Ordinal))return "월영 잔상";
        return traitId??string.Empty;
    }
}
