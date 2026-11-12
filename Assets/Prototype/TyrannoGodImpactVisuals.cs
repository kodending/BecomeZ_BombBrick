using System;
using UnityEngine;

// The held battlefield still reacts to contact without advancing its actors or collision bodies.
public sealed class TyrannoGodImpactVisuals:IDisposable
{
    public const float Duration=.28f;
    private readonly Transform[] bodies;
    private readonly Vector3[] positions,scales,feet;
    private bool restored;

    public TyrannoGodImpactVisuals(Transform[] targets)
    {
        bodies=targets;positions=new Vector3[bodies.Length];scales=new Vector3[bodies.Length];feet=new Vector3[bodies.Length];
        for(int i=0;i<bodies.Length;i++)
        {
            Transform body=bodies[i];if(body==null)continue;
            positions[i]=body.localPosition;scales[i]=body.localScale;
            var renderer=body.GetComponent<SpriteRenderer>();
            if(renderer!=null&&renderer.sprite!=null)
            {
                Bounds bounds=PixelMantisReaper.VisibleBody(renderer.sprite);
                feet[i]=new Vector3(renderer.flipX?-bounds.center.x:bounds.center.x,bounds.min.y,0f);
            }
        }
    }

    public void Pose(float age)
    {
        if(restored||age<0f)return;
        if(age>=Duration){Dispose();return;}
        float compress=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0f,.10f,age));
        float shake=Mathf.Exp(-age*19f)*.095f;
        for(int i=0;i<bodies.Length;i++)
        {
            Transform body=bodies[i];if(body==null)continue;
            body.localPosition=positions[i];body.localScale=scales[i];
            Vector3 contact=body.TransformPoint(feet[i]);
            body.localScale=Vector3.Scale(scales[i],new Vector3(1f+compress*.13f,1f-compress*.22f,1f));
            body.position+=contact-body.TransformPoint(feet[i]);
            body.position+=body.right*(Mathf.Sin(age*157f+i*1.7f)*shake);
        }
    }

    public void Dispose()
    {
        if(restored)return;restored=true;
        for(int i=0;i<bodies.Length;i++)if(bodies[i]!=null)
        {bodies[i].localPosition=positions[i];bodies[i].localScale=scales[i];}
    }
}
