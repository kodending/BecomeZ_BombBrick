using UnityEngine;

// Shared timing for evolution effects attached to the existing monster or card portrait.
public static class MonsterEvolutionCelebration
{
    public const string LightningResource="CombatEffects/Evolution/UpgradeLightning8";
    public static float DurationFor(int rank)=>rank>=4?3.8f:rank==3?2.8f:2f;
    public static float ImpactAt(int rank)=>rank>=4?1.65f:rank==3?1.1f:.65f;
    public static float StampAt(int rank)=>ImpactAt(rank)+(rank>=4?.28f:rank==3?.22f:.18f);

    public static float ChargeProgress(int rank,float age)
    {
        float t=Mathf.Clamp01(age/ImpactAt(rank));
        if(rank>=4)return t<.7f?.32f*Mathf.Pow(t/.7f,1.35f):.32f+.68f*Mathf.Pow((t-.7f)/.3f,2f);
        if(rank==3)return t<.6f?.4f*Mathf.SmoothStep(0f,1f,t/.6f):.4f+.6f*Mathf.Pow((t-.6f)/.4f,.65f);
        return Mathf.SmoothStep(0f,1f,t);
    }

    public static float GrowthProgress(int rank,float age)
    {
        float t=Mathf.Clamp01((age-ImpactAt(rank))/.1f);
        return 1f-Mathf.Pow(1f-t,3f);
    }

    public static float BodyScale(int rank,float age)
    {
        float hit=age-ImpactAt(rank);
        if(hit<0f)
        {
            float p=ChargeProgress(rank,age);
            return 1f-.06f*p+Mathf.Sin(p*Mathf.PI*(rank>=4?12f:rank==3?8f:4f))*.012f*p;
        }
        float peak=rank>=4?1.6f:rank==3?1.4f:1.25f;
        if(hit<.07f)return Mathf.Lerp(.94f,peak,1f-Mathf.Pow(1f-hit/.07f,3f));
        float t=Mathf.Clamp01((hit-.07f)/(rank>=4?.6f:rank==3?.48f:.38f));
        float bounce=Mathf.Cos(t*Mathf.PI*3f);if(bounce<0f)bounce*=.15f;
        return 1f+(peak-1f)*bounce*(1f-t)*(1f-t);
    }

    public static float SilhouetteAlpha(int rank,float age)
    {
        if(age<0f||age>=DurationFor(rank))return 0f;
        float hit=age-ImpactAt(rank);
        if(hit<0f)return Mathf.Clamp01(age/.16f);
        return 1f-Mathf.SmoothStep(0f,1f,hit/(rank>=4?.24f:rank==3?.2f:.16f));
    }

    public static float StampScale(int rank,float age)
    {
        float t=(age-StampAt(rank))/.35f;
        if(t<0f)return 0f;
        if(t>=1f||CombatFlashPresentation.Reduced)return 1f;
        float peak=rank>=4?2.35f:rank==3?2f:1.75f;
        if(t<.3f)return Mathf.Lerp(peak,.9f,t/.3f);
        float u=(t-.3f)/.7f;
        return 1f-.1f*Mathf.Cos(u*Mathf.PI*3f)*(1f-u)*(1f-u);
    }

    public static int LightningFrame(int rank,float age)
    {
        float t=age-ImpactAt(rank);
        return rank<3||rank>4||t<0f||t>=.25f?-1:Mathf.Clamp(Mathf.FloorToInt(t*32f),0,7);
    }
}
