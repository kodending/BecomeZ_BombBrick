using UnityEngine;

public static class GriffinHuntRules
{
    public const float DashStart=.12f,Grab=.34f,Apex=.72f,Fall=.86f;
    public const float Landing=Fall+GriffinCombatRules.SkyDiveDescentDuration,End=Landing+.24f;
    public const float GrabRatio=1.2f,SlamRatio=2.4f,Knockdown=.6f,Height=4.8f,CarryClearance=1.7f;

    public static float Lift(float time)
    {
        float rise=Mathf.Clamp01((time-Grab)/(Apex-Grab));
        float fall=GriffinCombatRules.GlideProgress(time,Fall);
        return Mathf.Sin(rise*Mathf.PI*.5f)*Height*(1f-fall);
    }
}
