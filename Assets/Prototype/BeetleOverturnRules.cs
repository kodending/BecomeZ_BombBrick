using UnityEngine;

public static class BeetleOverturnRules
{
    public const float Warning=.52f,RiseEnd=.98f,ApexEnd=1.24f,Landing=1.42f,End=1.72f;
    public const float Flight=Landing-Warning,DamageRatio=3f;
    public const string TraitId="trait_transcend_beetle_horn_of_gravity";
    public const string DisplayName="패왕 충각";
    public static float Travel(float time)=>Mathf.Clamp01((time-Warning)/Flight);
    private static float Scatter(int seed,int salt)=>Mathf.Repeat(Mathf.Sin(seed*127.1f+salt*311.7f)*43758.5453f,1f);
    public static float PeakHeight(int seed)=>4.65f+.85f*Scatter(seed,11);
    public static float Height(float time,int seed=0)
    {
        if(time<=Warning||time>=Landing)return 0f;
        float peak=RiseEnd+(Scatter(seed,23)-.5f)*.10f;
        float fall=ApexEnd+(Scatter(seed,47)-.5f)*.06f;
        if(time<peak){float rise=Mathf.InverseLerp(Warning,peak,time);return 1f-(1f-rise)*(1f-rise);}
        if(time<fall)return 1f-.08f*Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(peak,fall,time));
        float drop=Mathf.InverseLerp(fall,Landing,time);return .92f*(1f-drop*drop);
    }
    public static Vector3 AirOffset(float time,int seed)
    {
        float phase=Travel(time),arc=Mathf.Sin(phase*Mathf.PI);arc*=arc;
        return new Vector3((Scatter(seed,61)-.5f)*.45f*arc,PeakHeight(seed)*Height(time,seed),
            ((Scatter(seed,89)-.5f)*.28f+.04f*Mathf.Sin(phase*Mathf.PI*2f))*arc);
    }
    public static float Roll(float time,int seed)
    {
        if(time<Warning||time>=Landing)return 0f;
        // Whole turns meet the upright landing pose without an abrupt half-turn reset.
        float turns=3f+Mathf.Floor(Scatter(seed,71)*3f);
        return turns*360f*Mathf.SmoothStep(0f,1f,Travel(time));
    }
    private static void SourceImpulse(float time,out float charge,out float kick)
    {
        float release=Mathf.Clamp01((time-Warning)/.10f);release=1f-Mathf.Pow(1f-release,3f);
        charge=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0f,Warning-.07f,time))*(1f-release);
        kick=release*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(Warning+.10f,RiseEnd,time)));
    }
    public static Vector3 SourcePose(float time)
    {
        float charge,kick;SourceImpulse(time,out charge,out kick);
        return new Vector3(-.10f*charge+.14f*kick,-.065f*charge+.16f*kick,0f);
    }
    public static Vector3 SourceScale(float time)
    {
        float charge,kick;SourceImpulse(time,out charge,out kick);
        return new Vector3(1f+.10f*charge-.10f*kick,1f-.18f*charge+.18f*kick,1f);
    }
    public static int BodyFrame(float time)=>time<.12f?0:time<.28f?1:time<Warning?2:time<Warning+.12f?3:
        time<RiseEnd?4:time<ApexEnd?5:time<Landing?6:7;
}
