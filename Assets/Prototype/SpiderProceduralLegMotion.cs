using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1040)]
public sealed class SpiderProceduralLegMotion:MonoBehaviour
{
    private sealed class GaitLeg
    {
        public Transform upper;
        public Transform lower;
        public int phase;
    }

    private readonly List<GaitLeg> gaitLegs=new List<GaitLeg>(8);
    private ProceduralGroundedLimbMotion grounder;
    private Animator animator;
    private VoxelEnemyReaction reaction;
    private bool initialized;
    private bool gaitPoseReady;
    private Vector3 previousPosition;
    private float gaitClock;
    private float gaitWeight;

    public int LimbCount=>grounder==null?0:grounder.LimbCount;
    public bool IsInitialized=>initialized&&grounder!=null&&grounder.IsConfigured;
    public int GaitLegCount=>gaitLegs.Count;
    public float GaitWeight=>gaitWeight;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if(initialized)return;
        animator=GetComponentInChildren<Animator>(true);
        if(animator==null)throw new InvalidOperationException("Spider Animator is missing.");
        Transform visual=animator.transform;
        gaitLegs.Clear();
        List<ProceduralGroundedLimbMotion.LimbBinding> bindings=
            new List<ProceduralGroundedLimbMotion.LimbBinding>(8);
        for(int leg=1;leg<=4;leg++)
        {
            AddLeg(bindings,visual,leg,"L",(leg+1)%2);
            AddLeg(bindings,visual,leg,"R",leg%2);
        }
        grounder=GetComponent<ProceduralGroundedLimbMotion>();
        if(grounder==null)grounder=gameObject.AddComponent<ProceduralGroundedLimbMotion>();
        grounder.ConfigureGait(0.09f,0.16f,0.24f,1.25f);
        grounder.ConfigureSolver(6,1f);
        reaction=GetComponent<VoxelEnemyReaction>();
        grounder.Configure(animator,reaction,bindings);
        gaitPoseReady=false;
        gaitClock=0f;
        gaitWeight=0f;
        initialized=true;
    }

    private void LateUpdate()
    {
        if(!initialized||animator==null||!animator.enabled||
           reaction!=null&&reaction.IsDying)return;
        float dt=Mathf.Clamp(Time.deltaTime,0.0001f,0.05f);
        Vector3 position=Vector3.ProjectOnPlane(transform.position,Vector3.up);
        if(!gaitPoseReady)
        {
            previousPosition=position;
            gaitPoseReady=true;
            return;
        }
        float speed=Vector3.Distance(position,previousPosition)/dt;
        previousPosition=position;
        bool moving=speed>0.08f;
        gaitWeight=Mathf.MoveTowards(gaitWeight,moving?1f:0f,
            dt*(moving?12f:7f));
        if(gaitWeight<=0.0001f)return;

        float cadence=Mathf.Lerp(2.1f,3.7f,Mathf.Clamp01(speed/3.2f));
        gaitClock=Mathf.Repeat(gaitClock+dt*cadence,1f);
        for(int index=0;index<gaitLegs.Count;index++)
        {
            GaitLeg leg=gaitLegs[index];
            float phase=Mathf.Repeat(gaitClock+(leg.phase==0?0f:0.5f),1f);
            EvaluateSkitterPose(phase,out float swing,out float lift);
            float upperAngle=(swing*26f+lift*11f)*gaitWeight;
            float lowerAngle=(-swing*17f-lift*30f)*gaitWeight;
            leg.upper.localRotation*=Quaternion.Euler(upperAngle,0f,0f);
            leg.lower.localRotation*=Quaternion.Euler(lowerAngle,0f,0f);
        }
    }

    public static void EvaluateSkitterPose(float phase,out float swing,out float lift)
    {
        float radians=Mathf.Repeat(phase,1f)*Mathf.PI*2f;
        swing=Mathf.Sin(radians);
        lift=Mathf.Max(0f,Mathf.Sin(radians));
    }

    public void ResetPlanting()
    {
        if(grounder!=null)grounder.ResetPlanting();
        gaitPoseReady=false;
        gaitWeight=0f;
    }

    public static Vector3 EstimateFootContact(
        Transform upper,Transform lower,Renderer lowerRenderer)
    {
        if(upper==null||lower==null)throw new ArgumentNullException();
        if(lowerRenderer==null)
        {
            Vector3 continuation=lower.position-upper.position;
            return lower.position+continuation;
        }
        Bounds bounds=lowerRenderer.bounds;
        Vector3 outward=Vector3.ProjectOnPlane(lower.position-upper.position,Vector3.up);
        if(outward.sqrMagnitude<0.000001f)
            outward=Vector3.ProjectOnPlane(lower.forward,Vector3.up);
        outward.Normalize();
        float reach=Mathf.Abs(outward.x)*bounds.extents.x+
                    Mathf.Abs(outward.z)*bounds.extents.z;
        Vector3 point=bounds.center+outward*reach;
        point.y=bounds.min.y;
        return point;
    }

    private static void AddLeg(
        ICollection<ProceduralGroundedLimbMotion.LimbBinding> bindings,
        Transform visual,int leg,string side,int phase)
    {
        string upperPath="Root/Head/Leg"+leg+"Upper."+side;
        string lowerPath=upperPath+"/Leg"+leg+"Lower."+side;
        Transform upper=visual.Find(upperPath);
        Transform lower=visual.Find(lowerPath);
        if(upper==null||lower==null)
            throw new InvalidOperationException("Spider limb path is missing: "+lowerPath);
        Renderer renderer=lower.GetComponentInChildren<Renderer>(true);
        Vector3 footWorld=EstimateFootContact(upper,lower,renderer);
        SpiderProceduralLegMotion owner=visual.GetComponentInParent<SpiderProceduralLegMotion>();
        if(owner!=null)owner.gaitLegs.Add(new GaitLeg
        {
            upper=upper,
            lower=lower,
            phase=phase
        });
        bindings.Add(new ProceduralGroundedLimbMotion.LimbBinding
        {
            name="Leg"+leg+"."+side,
            joints=new[]{upper,lower},
            endBone=lower,
            endLocalPoint=lower.InverseTransformPoint(footWorld),
            correctionLimits=new[]{48f,72f},
            stepPhase=phase
        });
    }
}
