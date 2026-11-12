using System;
using UnityEngine;

[DefaultExecutionOrder(910)]
public sealed class GiraffeHighHeadSlam : MonoBehaviour
{
    private const float WindupDuration=0.22f;
    private const float DescendDuration=0.26f;
    private const float ImpactHoldDuration=0.05f;
    private const float RetractDuration=0.15f;

    private Transform neck;
    private Transform head;
    private Vector3 authoredNeckScale;
    private Vector3 authoredHeadScale;
    private Vector3 authoredHeadPosition;
    private bool authoredPoseCaptured;
    private Vector3 neckBaseScale;
    private Quaternion neckBaseRotation;
    private Vector3 headBaseScale;
    private Vector3 headBasePosition;
    private Vector3 targetPoint;
    private float maximumStretch=1f;
    private float elapsed;
    private bool playing;
    private bool impactTriggered;
    private Action<Vector3> impactAction;
    private CombatSkillClock skillClock;

    public bool IsPlaying=>playing;

    public void Initialize()
    {
        Animator animator=GetComponentInChildren<Animator>(true);
        Transform visual=animator!=null?animator.transform:transform;
        neck=visual.Find("Root/Body/Neck");
        head=visual.Find("Root/Body/Neck/Head");
        if(neck!=null&&head!=null&&!authoredPoseCaptured)
        {
            authoredNeckScale=neck.localScale;
            authoredHeadScale=head.localScale;
            authoredHeadPosition=head.localPosition;
            authoredPoseCaptured=true;
        }
    }

    public void Play(Vector3 worldTargetPoint,Action<Vector3> onImpact)
    {
        skillClock=CombatSkillClock.Capture();
        if(neck==null||head==null)Initialize();
        if(neck==null||head==null)
        {
            onImpact?.Invoke(worldTargetPoint);
            return;
        }
        CancelAndRestore();
        RestoreAuthoredDimensions();
        neckBaseScale=authoredNeckScale;
        neckBaseRotation=neck.localRotation;
        headBaseScale=authoredHeadScale;
        headBasePosition=authoredHeadPosition;
        targetPoint=worldTargetPoint;
        float baseLength=Mathf.Max(0.01f,Vector3.Distance(neck.position,head.position));
        maximumStretch=Mathf.Clamp(Vector3.Distance(neck.position,targetPoint)/baseLength,1f,5.5f);
        elapsed=0f;
        playing=true;
        impactTriggered=false;
        impactAction=onImpact;
    }

    public void CancelAndRestore()
    {
        if(playing)RestorePose();
        else RestoreAuthoredDimensions();
        playing=false;
        impactTriggered=true;
        impactAction=null;
    }

    private void Awake(){Initialize();}
    private void OnDisable(){CancelAndRestore();}

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            float deltaTime=skillClock==null?Time.deltaTime:skillClock.DeltaTime;
            if(!playing||CombatSkillClock.Active&&deltaTime<=0f)return;
            elapsed+=deltaTime;
            float descendStart=WindupDuration;
            float impactStart=descendStart+DescendDuration;
            float retractStart=impactStart+ImpactHoldDuration;
            float finish=retractStart+RetractDuration;

            if(elapsed<descendStart)
            {
                float t=Smooth(Mathf.Clamp01(elapsed/WindupDuration));
                ApplyPose(Mathf.Lerp(1f,1.2f,t),Vector3.up,t);
                return;
            }
            if(elapsed<impactStart)
            {
                float t=Smooth(Mathf.Clamp01((elapsed-descendStart)/DescendDuration));
                Vector3 targetDirection=(targetPoint-neck.position).normalized;
                Vector3 direction=Vector3.Slerp(Vector3.up,targetDirection,t);
                ApplyPose(Mathf.Lerp(1.2f,maximumStretch,t),direction,1f);
                return;
            }
            if(!impactTriggered)
            {
                impactTriggered=true;
                Action<Vector3> callback=impactAction;
                impactAction=null;
                callback?.Invoke(targetPoint);
            }
            if(elapsed<retractStart)
            {
                ApplyPose(maximumStretch,(targetPoint-neck.position).normalized,1f);
                return;
            }
            if(elapsed<finish)
            {
                float t=Smooth(Mathf.Clamp01((elapsed-retractStart)/RetractDuration));
                Vector3 direction=Vector3.Slerp(
                    (targetPoint-neck.position).normalized,BaseWorldDirection(),t);
                ApplyPose(Mathf.Lerp(maximumStretch,1f,t),direction,1f-t);
                return;
            }
            RestorePose();
            playing=false;
        }
    }

    private void ApplyPose(float stretch,Vector3 worldDirection,float aimWeight)
    {
        if(neck==null||head==null)return;
        neck.localScale=new Vector3(neckBaseScale.x,neckBaseScale.y*stretch,neckBaseScale.z);
        Quaternion parentRotation=neck.parent!=null?neck.parent.rotation:Quaternion.identity;
        Quaternion aimed=GiraffeNeckStrike.CalculateAimedLocalRotation(
            parentRotation,neckBaseRotation,headBasePosition,neckBaseScale,stretch,
            worldDirection.sqrMagnitude<0.0001f?BaseWorldDirection():worldDirection.normalized);
        neck.localRotation=Quaternion.Slerp(neckBaseRotation,aimed,aimWeight);
        head.localPosition=headBasePosition;
        head.localScale=new Vector3(
            headBaseScale.x,headBaseScale.y/Mathf.Max(0.01f,stretch),headBaseScale.z);
    }

    private Vector3 BaseWorldDirection()
    {
        Quaternion parentRotation=neck.parent!=null?neck.parent.rotation:Quaternion.identity;
        Quaternion baseWorld=parentRotation*neckBaseRotation;
        Vector3 local=new Vector3(
            headBasePosition.x*neckBaseScale.x,
            headBasePosition.y*neckBaseScale.y,
            headBasePosition.z*neckBaseScale.z);
        return baseWorld*(local.sqrMagnitude<0.0001f?Vector3.up:local.normalized);
    }

    private void RestorePose()
    {
        if(neck!=null){neck.localScale=neckBaseScale;neck.localRotation=neckBaseRotation;}
        if(head!=null){head.localPosition=headBasePosition;head.localScale=headBaseScale;}
    }

    private void RestoreAuthoredDimensions()
    {
        if(!authoredPoseCaptured)return;
        if(neck!=null)neck.localScale=authoredNeckScale;
        if(head!=null)
        {
            head.localPosition=authoredHeadPosition;
            head.localScale=authoredHeadScale;
        }
    }

    private static float Smooth(float value){return value*value*(3f-2f*value);}
}
