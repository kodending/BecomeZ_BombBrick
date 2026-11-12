using System;
using UnityEngine;

[DefaultExecutionOrder(900)]
public sealed class GiraffeNeckStrike : MonoBehaviour
{
    private const float WindupDuration=0.12f;
    private const float ExtendDuration=0.14f;
    private const float ImpactDuration=0.05f;
    private const float RetractDuration=0.16f;

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
    private Transform activeTarget;
    private float maximumStretch=1f;
    private float elapsed;
    private bool playing;
    private bool impactTriggered;
    private Action<Vector3,Vector3> impactAction;

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

    public void Play(Transform target,Action<Vector3,Vector3> onImpact)
    {
        if(neck==null||head==null)Initialize();
        if(neck==null||head==null||target==null)
        {
            Vector3 point=target!=null?target.position:transform.position;
            onImpact?.Invoke(transform.position,point);
            return;
        }
        if(playing)FinishImmediately();
        RestoreAuthoredDimensions();

        neckBaseScale=authoredNeckScale;
        neckBaseRotation=neck.localRotation;
        headBaseScale=authoredHeadScale;
        headBasePosition=authoredHeadPosition;
        activeTarget=target;
        targetPoint=TargetPoint(target);

        Vector3 origin=neck.position;
        Vector3 aim=targetPoint-origin;
        if(aim.sqrMagnitude<0.001f)aim=transform.forward;
        float baseLength=Mathf.Max(0.01f,Vector3.Distance(origin,head.position));
        maximumStretch=Mathf.Clamp(aim.magnitude/baseLength,1f,6f);
        elapsed=0f;
        playing=true;
        impactTriggered=false;
        impactAction=onImpact;
    }

    private void Awake(){Initialize();}

    private void OnDisable()
    {
        CancelAndRestore();
    }

    public void CancelAndRestore()
    {
        if(playing)RestorePose();
        else RestoreAuthoredDimensions();
        playing=false;
        impactTriggered=true;
        impactAction=null;
        activeTarget=null;
    }

    private void LateUpdate()
    {
        if(!playing)return;
        if(activeTarget!=null&&activeTarget.gameObject.activeInHierarchy)
            targetPoint=TargetPoint(activeTarget);
        elapsed+=Time.deltaTime;
        float extendStart=WindupDuration;
        float impactStart=extendStart+ExtendDuration;
        float retractStart=impactStart+ImpactDuration;
        float finish=retractStart+RetractDuration;

        if(elapsed<extendStart)
        {
            float t=Smooth(Mathf.Clamp01(elapsed/WindupDuration));
            ApplyPose(Mathf.Lerp(1f,0.9f,t),t*0.7f);return;
        }
        if(elapsed<impactStart)
        {
            float t=Smooth(Mathf.Clamp01((elapsed-extendStart)/ExtendDuration));
            ApplyPose(Mathf.Lerp(0.9f,maximumStretch,t),Mathf.Lerp(0.7f,1f,t));return;
        }
        if(!impactTriggered)
        {
            impactTriggered=true;
            Action<Vector3,Vector3> callback=impactAction;impactAction=null;
            callback?.Invoke(neck.position,targetPoint);
        }
        if(elapsed<retractStart){ApplyPose(maximumStretch,1f);return;}
        if(elapsed<finish)
        {
            float t=Smooth(Mathf.Clamp01((elapsed-retractStart)/RetractDuration));
            ApplyPose(Mathf.Lerp(maximumStretch,1f,t),1f-t);return;
        }
        RestorePose();playing=false;activeTarget=null;
    }

    private void ApplyPose(float stretch,float aimWeight)
    {
        if(neck==null||head==null)return;
        neck.localScale=new Vector3(neckBaseScale.x,neckBaseScale.y*stretch,neckBaseScale.z);
        Vector3 aim=targetPoint-neck.position;
        if(aim.sqrMagnitude<0.001f)aim=transform.forward;
        Quaternion parentRotation=neck.parent!=null?neck.parent.rotation:Quaternion.identity;
        Quaternion aimed=CalculateAimedLocalRotation(parentRotation,neckBaseRotation,
            headBasePosition,neckBaseScale,stretch,aim.normalized);
        neck.localRotation=Quaternion.Slerp(neckBaseRotation,aimed,aimWeight);
        head.localPosition=headBasePosition;
        head.localScale=new Vector3(
            headBaseScale.x,headBaseScale.y/Mathf.Max(0.01f,stretch),headBaseScale.z);
    }

    private void FinishImmediately()
    {
        if(!impactTriggered)
        {
            impactTriggered=true;
            Action<Vector3,Vector3> callback=impactAction;impactAction=null;
            callback?.Invoke(neck!=null?neck.position:transform.position,targetPoint);
        }
        RestorePose();playing=false;activeTarget=null;
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

    private static Vector3 TargetPoint(Transform target)
    {
        ScreenSpaceOutlineTarget outline=target.GetComponent<ScreenSpaceOutlineTarget>();
        Renderer[] renderers=outline!=null?outline.TargetRenderers:
            target.GetComponentsInChildren<Renderer>(true);
        Renderer first=Array.Find(renderers,item=>item!=null&&item.enabled);
        if(first==null)return target.position+Vector3.up*0.65f;
        Bounds bounds=first.bounds;
        foreach(Renderer renderer in renderers)
            if(renderer!=null&&renderer.enabled)bounds.Encapsulate(renderer.bounds);
        return new Vector3(bounds.center.x,Mathf.Lerp(bounds.min.y,bounds.max.y,0.62f),bounds.center.z);
    }

    public static Quaternion CalculateAimedLocalRotation(
        Quaternion parentWorldRotation,Quaternion baseLocalRotation,
        Vector3 headLocalPosition,Vector3 neckLocalScale,float stretch,
        Vector3 aimWorldDirection)
    {
        Quaternion baseWorldRotation=parentWorldRotation*baseLocalRotation;
        Vector3 stretchedLocalDirection=new Vector3(
            headLocalPosition.x*neckLocalScale.x,
            headLocalPosition.y*neckLocalScale.y*Mathf.Max(0.01f,stretch),
            headLocalPosition.z*neckLocalScale.z);
        if(stretchedLocalDirection.sqrMagnitude<0.000001f)
            stretchedLocalDirection=Vector3.up;
        Vector3 currentWorldDirection=baseWorldRotation*stretchedLocalDirection.normalized;
        Vector3 desiredDirection=aimWorldDirection.sqrMagnitude<0.000001f
            ?currentWorldDirection:aimWorldDirection.normalized;
        Quaternion desiredWorldRotation=
            Quaternion.FromToRotation(currentWorldDirection,desiredDirection)*baseWorldRotation;
        return Quaternion.Inverse(parentWorldRotation)*desiredWorldRotation;
    }

    private static float Smooth(float value){return value*value*(3f-2f*value);}
}
