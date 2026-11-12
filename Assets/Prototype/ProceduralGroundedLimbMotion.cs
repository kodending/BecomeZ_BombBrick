using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1050)]
public sealed class ProceduralGroundedLimbMotion:MonoBehaviour
{
    public sealed class LimbBinding
    {
        public string name;
        public Transform[] joints;
        public Transform endBone;
        public Vector3 endLocalPoint;
        public float[] correctionLimits;
        public int stepPhase;
    }

    private sealed class LimbState
    {
        public LimbBinding binding;
        public Quaternion[] animationPose;
        public Vector3 idealTarget;
        public Vector3 plantedTarget;
        public Vector3 stepStart;
        public Vector3 stepEnd;
        public float legLength;
        public float stepProgress;
        public bool planted;
        public bool stepping;
    }

    [SerializeField,Range(1,8)] private int solverIterations=3;
    [SerializeField,Range(0f,1f)] private float solverWeight=0.82f;
    [SerializeField,Range(0.08f,0.5f)] private float stepThresholdRatio=0.22f;
    [SerializeField,Range(0.08f,0.5f)] private float stepDuration=0.18f;
    [SerializeField,Range(0.02f,0.35f)] private float stepLiftRatio=0.12f;
    [SerializeField,Range(0f,0.08f)] private float groundClearance=0.018f;
    [SerializeField,Range(0f,2f)] private float stepAnticipation=0.85f;

    private readonly List<LimbState> limbs=new List<LimbState>();
    private Animator animator;
    private VoxelEnemyReaction reaction;
    private Transform motionRoot;
    private Vector3 previousRootPosition;
    private Quaternion previousRootRotation;
    private Vector3 previousRootScale;
    private Vector3 planarRootVelocity;
    private bool rootPoseReady;
    private int activePhase;

    public int LimbCount=>limbs.Count;
    public bool IsConfigured=>animator!=null&&limbs.Count>0;
    public float StepThresholdRatio=>stepThresholdRatio;
    public float StepDuration=>stepDuration;
    public float StepLiftRatio=>stepLiftRatio;
    public float StepAnticipation=>stepAnticipation;
    public int SolverIterations=>solverIterations;
    public float SolverWeight=>solverWeight;

    public void ConfigureGait(
        float thresholdRatio,float duration,float liftRatio,float anticipation)
    {
        stepThresholdRatio=Mathf.Clamp(thresholdRatio,0.08f,0.5f);
        stepDuration=Mathf.Clamp(duration,0.08f,0.5f);
        stepLiftRatio=Mathf.Clamp(liftRatio,0.02f,0.35f);
        stepAnticipation=Mathf.Clamp(anticipation,0f,2f);
    }

    public void ConfigureSolver(int iterations,float weight)
    {
        solverIterations=Mathf.Clamp(iterations,1,8);
        solverWeight=Mathf.Clamp01(weight);
    }

    public void Configure(
        Animator sourceAnimator,VoxelEnemyReaction sourceReaction,
        IEnumerable<LimbBinding> bindings)
    {
        animator=sourceAnimator;
        reaction=sourceReaction;
        motionRoot=transform;
        limbs.Clear();
        if(bindings!=null)
        {
            foreach(LimbBinding binding in bindings)
            {
                if(binding==null||binding.joints==null||binding.joints.Length==0||
                   binding.endBone==null)continue;
                float length=ChainLength(binding);
                if(length<=0.0001f)continue;
                limbs.Add(new LimbState
                {
                    binding=binding,
                    animationPose=new Quaternion[binding.joints.Length],
                    legLength=length
                });
            }
        }
        ResetPlanting();
    }

    public void ResetPlanting()
    {
        foreach(LimbState limb in limbs)
        {
            limb.planted=false;
            limb.stepping=false;
            limb.stepProgress=0f;
        }
        activePhase=0;
        rootPoseReady=false;
        planarRootVelocity=Vector3.zero;
    }

    private void OnEnable()
    {
        ResetPlanting();
    }

    private void LateUpdate()
    {
        if(!IsConfigured||!animator.enabled||reaction!=null&&reaction.IsDying)return;
        float dt=Mathf.Min(0.05f,Mathf.Max(0.0001f,Time.deltaTime));
        bool rootMoved=UpdateRootMotionState(dt);
        CaptureAnimationPoseAndTargets();
        UpdateStepCycle(dt,rootMoved);
        SolveLimbs();
    }

    private bool UpdateRootMotionState(float dt)
    {
        Vector3 position=motionRoot.position;
        Quaternion rotation=motionRoot.rotation;
        if(!rootPoseReady)
        {
            previousRootPosition=position;
            previousRootRotation=rotation;
            previousRootScale=motionRoot.lossyScale;
            planarRootVelocity=Vector3.zero;
            rootPoseReady=true;
            return false;
        }
        Vector3 displacement=Vector3.ProjectOnPlane(position-previousRootPosition,Vector3.up);
        float distance=displacement.magnitude;
        float angle=Quaternion.Angle(rotation,previousRootRotation);
        Vector3 scale=motionRoot.lossyScale;
        bool scaleChanged=RelativeScaleChange(previousRootScale,scale)>0.02f;
        previousRootPosition=position;
        previousRootRotation=rotation;
        previousRootScale=scale;
        float averageLeg=AverageLegLength();
        if(distance>averageLeg*1.5f||angle>70f||scaleChanged)
        {
            ResetPlanting();
            previousRootPosition=position;
            previousRootRotation=rotation;
            previousRootScale=scale;
            planarRootVelocity=Vector3.zero;
            rootPoseReady=true;
            return false;
        }
        Vector3 measuredVelocity=distance>averageLeg*0.004f
            ?displacement/Mathf.Max(0.0001f,dt):Vector3.zero;
        float velocityBlend=1f-Mathf.Exp(-dt*16f);
        planarRootVelocity=Vector3.Lerp(planarRootVelocity,measuredVelocity,velocityBlend);
        return distance>averageLeg*0.004f||angle>0.15f;
    }

    private void CaptureAnimationPoseAndTargets()
    {
        foreach(LimbState limb in limbs)
        {
            limb.legLength=ChainLength(limb.binding);
            for(int index=0;index<limb.binding.joints.Length;index++)
                limb.animationPose[index]=limb.binding.joints[index].localRotation;
            Vector3 animatedEnd=EndPosition(limb);
            limb.idealTarget=ProbeGround(animatedEnd,limb.legLength);
            if(limb.planted)continue;
            limb.plantedTarget=limb.idealTarget;
            limb.stepStart=limb.idealTarget;
            limb.stepEnd=PredictStepEnd(limb);
            limb.planted=true;
        }
    }

    private Vector3 PredictStepEnd(LimbState limb)
    {
        Vector3 predicted=PredictStepTarget(
            limb.idealTarget,planarRootVelocity,stepDuration,stepAnticipation,
            limb.legLength*0.55f);
        Vector3 grounded=ProbeGround(predicted,limb.legLength,out bool foundGround);
        return foundGround?grounded:limb.idealTarget;
    }

    public static Vector3 PredictStepTarget(
        Vector3 idealTarget,Vector3 planarVelocity,float duration,
        float anticipation,float maximumLead)
    {
        Vector3 lead=Vector3.ProjectOnPlane(planarVelocity,Vector3.up)*
                     Mathf.Max(0f,duration)*Mathf.Max(0f,anticipation);
        lead=Vector3.ClampMagnitude(lead,Mathf.Max(0f,maximumLead));
        return idealTarget+lead;
    }

    private void UpdateStepCycle(float dt,bool rootMoved)
    {
        bool phaseStepping=false;
        foreach(LimbState limb in limbs)
        {
            if(!limb.stepping)continue;
            phaseStepping=true;
            limb.stepProgress=Mathf.Clamp01(limb.stepProgress+dt/stepDuration);
            float eased=limb.stepProgress*limb.stepProgress*(3f-2f*limb.stepProgress);
            Vector3 target=Vector3.Lerp(limb.stepStart,limb.stepEnd,eased);
            target+=Vector3.up*(Mathf.Sin(limb.stepProgress*Mathf.PI)*
                                limb.legLength*stepLiftRatio);
            limb.plantedTarget=target;
            if(limb.stepProgress<1f)continue;
            limb.stepping=false;
            limb.plantedTarget=limb.stepEnd;
        }
        if(phaseStepping)return;

        float largestDrift=0f;
        foreach(LimbState limb in limbs)
        {
            if(limb.binding.stepPhase!=activePhase)continue;
            largestDrift=Mathf.Max(largestDrift,
                Vector3.Distance(limb.plantedTarget,limb.idealTarget)/limb.legLength);
        }
        if(largestDrift<stepThresholdRatio||!rootMoved&&largestDrift<stepThresholdRatio*1.35f)
            return;

        bool started=false;
        foreach(LimbState limb in limbs)
        {
            if(limb.binding.stepPhase!=activePhase)continue;
            float drift=Vector3.Distance(limb.plantedTarget,limb.idealTarget)/limb.legLength;
            if(drift<stepThresholdRatio*0.45f)continue;
            limb.stepStart=limb.plantedTarget;
            // The body keeps travelling while this foot is in the air. Landing on
            // the current ideal position makes every step trail behind the body and
            // reads as foot sliding, especially on fast creatures with short legs.
            limb.stepEnd=PredictStepEnd(limb);
            limb.stepProgress=0f;
            limb.stepping=true;
            started=true;
        }
        if(started)activePhase=1-activePhase;
    }

    private void SolveLimbs()
    {
        foreach(LimbState limb in limbs)
        {
            if(Vector3.Distance(limb.binding.joints[0].position,limb.plantedTarget)>
               limb.legLength*1.85f)
            {
                limb.plantedTarget=limb.idealTarget;
                limb.stepping=false;
            }
            ConstrainedCcdIkSolver.Solve(
                limb.binding.joints,limb.binding.endBone,limb.binding.endLocalPoint,
                limb.plantedTarget,limb.animationPose,limb.binding.correctionLimits,
                solverIterations,solverWeight);
        }
    }

    private Vector3 ProbeGround(Vector3 position,float legLength)
    {
        return ProbeGround(position,legLength,out _);
    }

    private Vector3 ProbeGround(Vector3 position,float legLength,out bool foundGround)
    {
        float height=Mathf.Max(0.25f,legLength*0.65f);
        Ray ray=new Ray(position+Vector3.up*height,Vector3.down);
        if(Physics.Raycast(ray,out RaycastHit hit,height*2.4f,
            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
        {
            foundGround=true;
            return hit.point+Vector3.up*groundClearance;
        }
        foundGround=false;
        return position;
    }

    private static float ChainLength(LimbBinding binding)
    {
        float length=0f;
        for(int index=0;index<binding.joints.Length-1;index++)
            length+=Vector3.Distance(binding.joints[index].position,
                binding.joints[index+1].position);
        length+=Vector3.Distance(binding.endBone.position,
            binding.endBone.TransformPoint(binding.endLocalPoint));
        return length;
    }

    private float AverageLegLength()
    {
        if(limbs.Count==0)return 1f;
        float total=0f;
        foreach(LimbState limb in limbs)total+=limb.legLength;
        return total/limbs.Count;
    }

    private static float RelativeScaleChange(Vector3 before,Vector3 after)
    {
        float x=Mathf.Abs(after.x-before.x)/Mathf.Max(0.0001f,Mathf.Abs(before.x));
        float y=Mathf.Abs(after.y-before.y)/Mathf.Max(0.0001f,Mathf.Abs(before.y));
        float z=Mathf.Abs(after.z-before.z)/Mathf.Max(0.0001f,Mathf.Abs(before.z));
        return Mathf.Max(x,Mathf.Max(y,z));
    }

    private static Vector3 EndPosition(LimbState limb)
    {
        return limb.binding.endBone.TransformPoint(limb.binding.endLocalPoint);
    }
}
