using System;
using UnityEngine;

public static class GriffinCombatRules
{
    public const string SkillId="griffin_sky_dive";
    public const string PoneTraitId="intrinsic_pone_royal_claw";
    public const string GriponasTraitId="intrinsic_griphonas_hellgate_landing";
    public const string HoloponasTraitId="intrinsic_holophonas_hologram_afterimage";
    public const string EntPoneTraitId="intrinsic_entpone_ancient_tree_embrace";

    public const float SkyfallDuration=0.7f;
    public const float SkyDiveDescentDuration=0.05f;
    public const float SkyDiveWindup=0.14f;
    public const float SkyDiveRangeTiles=2.5f;
    public const int SkyDiveAscentFrames=3;
    public const float SkyDiveLiftHeight=5.6f;
    public const float KnockbackDistanceTiles=0.75f;
    public const float RoyalClawRadiusTiles=0.75f;
    public const float RoyalClawDamageRatio=0.5f;
    public const float HellfireDuration=4f;
    public const float HellfireTickInterval=1f;
    public const float HellfireDamageRatio=0.25f;
    public const float AfterimageDelay=0.6f;
    public const float AfterimageDamageMultiplier=0.5f;
    public const float ShieldRatio=0.08f;
    public const float ShieldDuration=5f;

    public static float RoyalClawDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*RoyalClawDamageRatio;
    }

    public static float HellfireDamage(float attackDamage)
    {
        return Mathf.Max(0f,attackDamage)*HellfireDamageRatio;
    }

    public static float AfterimageDamage(float attackDamage,float skillRatio)
    {
        return Mathf.Max(0f,attackDamage)*Mathf.Max(0f,skillRatio)*
               AfterimageDamageMultiplier;
    }

    public static float Shield(float maximumHealth)
    {
        return Mathf.Max(0f,maximumHealth)*ShieldRatio;
    }

    public static float SkyDiveLift(
        float elapsed,float ascentDuration,float descentDuration)
    {
        float ascent=Mathf.Max(0.01f,ascentDuration);
        if(elapsed<=ascent)
            return Mathf.SmoothStep(
                0f,SkyDiveLiftHeight,Mathf.Clamp01((elapsed-SkyDiveWindup)/Mathf.Max(.01f,ascent-SkyDiveWindup-.06f)));
        float descent=Mathf.Max(0.01f,descentDuration);
        float progress=Mathf.Clamp01((elapsed-ascent)/descent);
        return Mathf.Lerp(SkyDiveLiftHeight,0f,DiveProgress(progress));
    }

    public static float GlideProgress(float elapsed,float ascentDuration)
    {
        return DiveProgress(Mathf.Clamp01((elapsed-ascentDuration)/SkyDiveDescentDuration));
    }

    public static float TyphoonLiftHeight(Vector3 launch,Camera camera)
    {
        if(camera==null)return 14f;
        for(float height=8f;height<=80f;height+=2f)
        {
            Vector3 viewport=camera.WorldToViewportPoint(launch+Vector3.up*height);
            if(viewport.y>1.25f||viewport.z<=camera.nearClipPlane)return height;
        }
        return 80f;
    }

    public static float TyphoonLift(float elapsed,float ascent,float height)
    {
        float rise=Mathf.Clamp01((elapsed-SkyDiveWindup)/Mathf.Max(.01f,ascent-SkyDiveWindup-.12f));
        return height*rise*rise*(1f-GlideProgress(elapsed,ascent));
    }

    // Start the plunge immediately, then accelerate along the same diagonal to contact.
    private static float DiveProgress(float t)=>t*(.65f+.35f*t);

    public static string DisplayName(string traitId)
    {
        if(string.Equals(traitId,PoneTraitId,StringComparison.Ordinal))return "왕조의 발톱";
        if(string.Equals(traitId,GriponasTraitId,StringComparison.Ordinal))return "지옥문 착지";
        if(string.Equals(traitId,HoloponasTraitId,StringComparison.Ordinal))return "홀로 잔상";
        if(string.Equals(traitId,EntPoneTraitId,StringComparison.Ordinal))return "고목의 품";
        return traitId??string.Empty;
    }
}
