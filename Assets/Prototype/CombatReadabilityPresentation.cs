using UnityEngine;

// Every running battle uses local emphasis; older presets are only for offline regression fixtures.
public static class CombatReadabilityPresentation
{
    public static int BattleUsers {get;private set;}
    public static void RegisterBattle()=>BattleUsers++;
    public static void UnregisterBattle()=>BattleUsers=Mathf.Max(0,BattleUsers-1);

    private static bool Comparing
    {
        get
        {
#if UNITY_EDITOR
            return !Application.isPlaying&&CombatEffectTestConfiguration.Active;
#else
            return false;
#endif
        }
    }

    public static bool Active
    {
        get
        {
#if UNITY_EDITOR
            if(Comparing)return CombatReadabilityPreview.Active;
#endif
            return BattleUsers>0;
        }
    }

    public static bool Local
    {
        get
        {
#if UNITY_EDITOR
            if(Comparing)return CombatReadabilityPreview.Active&&CombatReadabilityPreview.Preset==CombatReadabilityPreset.Local;
#endif
            return Active;
        }
    }

    public static bool Adjusted
    {
        get
        {
#if UNITY_EDITOR
            if(Comparing)return CombatReadabilityPreview.Adjusted;
#endif
            return Active;
        }
    }

    public static bool RefinedSkills=>Local&&!CombatSkillClock.Active;
    public static float BodyVisualScale=>Local?.9f:1f;
    public static float EffectVisualScale=>Local?BodyVisualScale*1.35f:1f;
    public static float PreparationDuration=>!Adjusted?1f:Local?.35f:.3f;
    public static float PreparationSpeed=>!Adjusted?.08f:Local?1f:.45f;
    public static float ShadeAlpha=>!Adjusted?.5f:Local?0f:.15f;
    public static float SkillBodyScale=>!Adjusted?1.5f:Local?1f:1.12f;
    public static int HologramCount=>Adjusted?1:3;
    public static float HologramOpacity=>Adjusted?.45f:1f;
    public static float HologramScale(int index)=>Adjusted?1.14f:1.42f+index*.14f;
    public static float ContactIntensity=>Adjusted?.55f:1f;
    public static float ContactSize=>Adjusted?.75f:1f;
    public static float SkillSoundGain=>Adjusted?.7f:1f;
    public static bool ContactFeedback
    {
        get
        {
#if UNITY_EDITOR
            if(CombatEffectTestConfiguration.Active)return CombatReadabilityPreview.ContactFeedback;
#endif
            return true;
        }
    }
    public static bool SkillAudio
    {
        get
        {
#if UNITY_EDITOR
            if(CombatEffectTestConfiguration.Active)return CombatReadabilityPreview.SkillAudio;
#endif
            return true;
        }
    }

#if UNITY_EDITOR
    public static SpriteRenderer ProjectileContactRenderer
    {get=>CombatReadabilityPreview.ProjectileContactRenderer;set=>CombatReadabilityPreview.ProjectileContactRenderer=value;}
    public static Vector3 ProjectileContactDirection
    {get=>CombatReadabilityPreview.ProjectileContactDirection;set=>CombatReadabilityPreview.ProjectileContactDirection=value;}
#else
    public static SpriteRenderer ProjectileContactRenderer;
    public static Vector3 ProjectileContactDirection;
#endif

    public static Vector3 VisibleBodyCenter(SpriteRenderer renderer)
    {
        var center=PixelMantisReaper.VisibleBody(renderer.sprite).center;
        if(renderer.flipX)center.x=-center.x;
        if(renderer.flipY)center.y=-center.y;
        return renderer.transform.TransformPoint(center);
    }

    public static Matrix4x4 WorldHudMatrix(Matrix4x4 current,Rect body)
    {
        float scale=(Local?.75f:1f)*BodyVisualScale;
        var pivot=new Vector3(body.center.x,body.yMin-6f,0f);
        return current*Matrix4x4.TRS(pivot*(1f-scale),Quaternion.identity,new Vector3(scale,scale,1f));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        BattleUsers=0;
#if !UNITY_EDITOR
        ProjectileContactRenderer=null;ProjectileContactDirection=Vector3.zero;
#endif
    }
}
