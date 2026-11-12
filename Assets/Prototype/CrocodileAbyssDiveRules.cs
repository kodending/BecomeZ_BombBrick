using UnityEngine;

public static class CrocodileAbyssDiveRules
{
    public const string TraitId="trait_transcend_crocodile_abyss_death_roll";
    public const string DisplayName="심연 잠항";
    public const float ContactTime=.18f;
    public const float HitTime=.95f;
    public const float Duration=1.2f;
    public const float DamageRatio=6f;
    public const float DefenseIgnoreRatio=1f;
    public const float FullySubmergedStart=.4f,FullySubmergedEnd=.72f;
    public static bool FullySubmergedAt(float time)=>time>=FullySubmergedStart-.00001f&&time<=FullySubmergedEnd+.00001f;

    public static float DepthAt(float time)
    {
        float down=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(ContactTime,FullySubmergedStart,time));
        float up=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(FullySubmergedEnd,HitTime,time));
        return down*up;
    }

    public static float RollAt(float time)=>
        720f*Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.35f,.72f,time));
}
