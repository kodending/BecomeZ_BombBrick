using System;
using UnityEngine;

public static class TyrannoGodFootstepsRules
{
    public const float FieldPreludeEndTime=1.60f,BounceDuration=.24f;
    public const int StepCount=3;

    public static float StepTimeAt(int index)=>index==0?.38f:index==1?.83f:1.29f;

    public static int CompletedStepsAt(float time)
    {
        int count=0;
        while(count<StepCount&&time>=StepTimeAt(count))count++;
        return count;
    }

    public static float BounceHeightAt(float time)
    {
        float height=0f;
        for(int step=0;step<StepCount;step++)
        {
            float phase=(time-StepTimeAt(step))/BounceDuration;
            if(phase>0f&&phase<1f)
                height=4f*phase*(1f-phase)*(step==0?.25f:step==1?.36f:.48f);
        }
        return height;
    }

    public static Vector2 CameraJoltAt(float time)
    {
        Vector2 offset=Vector2.zero;
        for(int step=0;step<StepCount;step++)
        {
            float age=time-StepTimeAt(step),phase=age/.20f;
            if(phase<0f||phase>=1f)continue;
            float force=(step==0?.07f:step==1?.10f:.14f)*(1f-phase)*(1f-phase);
            offset+=new Vector2(Mathf.Sin(age*121f+step)*.45f,Mathf.Cos(age*104f))*force;
        }
        return offset;
    }
}

public sealed class TyrannoGodFootstepsVisuals:IDisposable
{
    private readonly Transform[] bodies;
    private readonly Vector3[] localPositions;
    private bool restored;

    public TyrannoGodFootstepsVisuals(Transform[] targets)
    {
        bodies=targets;localPositions=new Vector3[bodies.Length];
        for(int i=0;i<bodies.Length;i++)
            if(bodies[i]!=null)localPositions[i]=bodies[i].localPosition;
    }

    public void Pose(float time)
    {
        if(restored)return;
        if(time>=TyrannoGodFootstepsRules.FieldPreludeEndTime){Dispose();return;}
        float height=TyrannoGodFootstepsRules.BounceHeightAt(time);
        for(int i=0;i<bodies.Length;i++)
        {
            Transform body=bodies[i];if(body==null)continue;
            body.localPosition=localPositions[i];
            body.position+=Vector3.up*height;
        }
    }

    public void Dispose()
    {
        if(restored)return;restored=true;
        for(int i=0;i<bodies.Length;i++)
            if(bodies[i]!=null)bodies[i].localPosition=localPositions[i];
    }
}
