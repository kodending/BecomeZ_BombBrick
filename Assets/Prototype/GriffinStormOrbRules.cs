using UnityEngine;

public static class GriffinStormOrbRules
{
    public const float AbsorbDuration=8f,ContractDuration=.35f,EjectDuration=.8f;
    public const float MinimumFlight=.55f,MaximumFlight=.95f;
    public const int BasicShots=3;
    public const float BasicDamageRatio=.5f,StoredDamageRatio=.35f;
    public const float Burst=AbsorbDuration+ContractDuration;
    public const float End=Burst+EjectDuration+MaximumFlight;

    public static float LaunchTime(int index,int count)=>Burst+
        EjectDuration*index/Mathf.Max(1,count-1);
    public static float FlightTime(int seed)=>Mathf.Lerp(MinimumFlight,MaximumFlight,
        Mathf.Repeat(seed*.61803399f,.9999f));

    public static Vector3 AbsorbPosition(Vector3 point,Vector3 origin,Vector3 anchor,float progress)
    {
        float t=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress));
        Vector3 center=Vector3.Lerp(origin,anchor,t)+Vector3.up*
            (Mathf.Sin(Mathf.PI*t)*Vector3.Distance(origin,anchor)*.18f);
        return center+(point-origin)*(1f-t)*(1f-t);
    }

    public static Vector3 ReturnPosition(Vector3 start,Vector3 end,float progress,int seed,
        Vector3 tangent,Vector3 up,Vector3 right,float height)
    {
        float t=Mathf.Clamp01(progress);
        if(t<=0f)return start;if(t>=1f)return end;
        // Preserve the orbit's departure direction, then accelerate into one clear impact.
        float p=t*t,side=(seed&1)==0?1f:-1f;
        float reach=Mathf.Max(height,Vector3.Distance(start,end)*.38f);
        Vector3 a=start+tangent.normalized*reach*.8f;
        Vector3 b=Vector3.Lerp(start,end,.68f)+up*reach*.55f+right*reach*.38f*side;
        float inverse=1f-p;
        Vector3 point=inverse*inverse*inverse*start+3f*inverse*inverse*p*a+
            3f*inverse*p*p*b+p*p*p*end;
        float envelope=Mathf.Sin(Mathf.PI*p);
        if(seed%3==1)point+=right*(Mathf.Sin(2f*Mathf.PI*p)*envelope*reach*.4f);
        else if(seed%3==2)point+=(right*Mathf.Sin(2f*Mathf.PI*p)+
            up*(1f-Mathf.Cos(2f*Mathf.PI*p)))*envelope*reach*.28f*side;
        return point;
    }
}
