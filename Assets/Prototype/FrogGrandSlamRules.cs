using UnityEngine;

public static class FrogGrandSlamRules
{
    public const float CastContact=.18f,PullEnd=.73f,LiftEnd=1.08f,SlamStart=1.24f,HitTime=1.45f,Duration=1.70f;
    public const float LiftHeight=2.8f;
    public const float ThrowDistanceTiles=2.25f;

    public static float ThrowProgressAt(float elapsed)
    {
        float progress=Mathf.InverseLerp(SlamStart,HitTime,elapsed);
        return progress*progress;
    }

    public static float HeightAt(float elapsed)
    {
        if(elapsed<=PullEnd||elapsed>=HitTime)return 0f;
        if(elapsed<LiftEnd)return LiftHeight*Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(PullEnd,LiftEnd,elapsed));
        if(elapsed<=SlamStart)return LiftHeight;
        float fall=Mathf.InverseLerp(SlamStart,HitTime,elapsed);
        return LiftHeight*(1f-fall*fall*fall);
    }
}
