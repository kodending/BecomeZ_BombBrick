using System;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class RaptorTurnAnticipation : MonoBehaviour
{
    private const float MaxMeasuredTurnRate=360f;
    private const float FullAnticipationTurnRate=180f;

    private Animator animator;
    private VoxelEnemyReaction reaction;
    private Transform head;
    private Transform neck;
    private Transform chest;
    private Transform spine;
    private Transform pelvis;
    private readonly Transform[] tails=new Transform[4];
    private bool compactTorsoRig;
    private bool ultraLowSdRig;
    private bool voxelDragonRig;
    private bool hasDesiredDirection;
    private Vector3 desiredWorldDirection;
    private Vector3 smoothedWorldDirection;
    private float desiredDirectionAge;

    public bool HasTurnChain=>head!=null&&neck!=null&&chest!=null;
    public string RigProfile=>voxelDragonRig
        ?"VoxelDragon"
        :(ultraLowSdRig?"UltraLowSD":"DEF");

    public void SetDesiredDirection(Vector3 direction)
    {
        direction.y=0f;
        if(direction.sqrMagnitude<0.001f)return;
        direction.Normalize();
        if(!hasDesiredDirection)
        {
            desiredWorldDirection=smoothedWorldDirection=direction;
            hasDesiredDirection=true;
        }
        else if(Vector3.Angle(desiredWorldDirection,direction)>=2.5f)
            desiredWorldDirection=direction;
        desiredDirectionAge=0f;
    }

    private bool initialized;
    private float lastRootYaw;
    private float smoothedTurnRate;
    private float headYaw;
    private float neckYaw;
    private float chestYaw;
    private float pelvisYaw;
    private readonly float[] tailYaw=new float[4];
    private bool outputPoseCaptured;
    private Quaternion headOutputRotation;
    private Quaternion neckOutputRotation;
    private Quaternion chestOutputRotation;
    private Quaternion pelvisOutputRotation;
    private readonly Quaternion[] tailOutputRotations=new Quaternion[4];

    public void Initialize()
    {
        animator=GetComponentInChildren<Animator>(true);
        reaction=GetComponent<VoxelEnemyReaction>();
        Transform searchRoot=animator!=null?animator.transform:transform;
        Transform[] bones=searchRoot.GetComponentsInChildren<Transform>(true);
        head=FindBone(bones,"DEF_Head");
        neck=FindBone(bones,"DEF_Neck");
        chest=FindBone(bones,"DEF_Chest");
        spine=FindBone(bones,"DEF_Spine");
        pelvis=FindBone(bones,"DEF_Pelvis");
        ultraLowSdRig=head==null||neck==null||chest==null;
        if(ultraLowSdRig)
        {
            head=searchRoot.Find("Root/Body/Neck/Head");
            neck=searchRoot.Find("Root/Body/Neck");
            chest=searchRoot.Find("Root/Body");
            spine=null;
            pelvis=searchRoot.Find("Root");
        }
        voxelDragonRig=head==null||neck==null||chest==null;
        if(voxelDragonRig)
        {
            ultraLowSdRig=false;
            head=searchRoot.Find(
                "RIG_VoxelDragon/Root/Pelvis/Chest/Neck/Head");
            neck=searchRoot.Find(
                "RIG_VoxelDragon/Root/Pelvis/Chest/Neck");
            chest=searchRoot.Find(
                "RIG_VoxelDragon/Root/Pelvis/Chest");
            spine=null;
            pelvis=searchRoot.Find(
                "RIG_VoxelDragon/Root/Pelvis");
        }
        compactTorsoRig=spine==null&&chest!=null&&pelvis!=null;
        for(int i=0;i<tails.Length;i++)
            tails[i]=voxelDragonRig
                ?(i<=2?searchRoot.Find(VoxelDragonTailPath(i)):null)
                :(ultraLowSdRig
                    ?searchRoot.Find(UltraLowSdTailPath(i))
                    :FindBone(bones,"DEF_Tail_0"+(i+1)));
        ResetMotion();
        initialized=true;
    }

    private void OnEnable()
    {
        initialized=false;
        smoothedTurnRate=headYaw=neckYaw=chestYaw=pelvisYaw=0f;
        Array.Clear(tailYaw,0,tailYaw.Length);
        outputPoseCaptured=false;
        hasDesiredDirection=false;
        desiredDirectionAge=0f;
    }

    private void LateUpdate()
    {
        if(!initialized)
        {
            Initialize();
            return;
        }
        RemovePersistedOffsets();
        if(reaction!=null&&reaction.IsDying)return;
        if(animator!=null&&!animator.enabled)return;

        float dt=Time.deltaTime;
        if(dt<=0.00001f)return;
        float rootYaw=transform.eulerAngles.y;
        float yawDelta=Mathf.DeltaAngle(lastRootYaw,rootYaw);
        lastRootYaw=rootYaw;
        if(Mathf.Abs(yawDelta)>45f)
        {
            smoothedTurnRate=0f;
            return;
        }

        float measuredRate=Mathf.Clamp(yawDelta/dt,-MaxMeasuredTurnRate,MaxMeasuredTurnRate);
        smoothedTurnRate=Damp(smoothedTurnRate,measuredRate,18f,dt);
        desiredDirectionAge+=dt;
        float turnAmount;
        if(hasDesiredDirection&&desiredDirectionAge<=0.3f)
        {
            Quaternion from=Quaternion.LookRotation(smoothedWorldDirection,Vector3.up);
            Quaternion to=Quaternion.LookRotation(desiredWorldDirection,Vector3.up);
            smoothedWorldDirection=(Quaternion.Slerp(
                from,to,1f-Mathf.Exp(-9f*dt))*Vector3.forward).normalized;
            float directionError=Vector3.SignedAngle(
                transform.forward,smoothedWorldDirection,Vector3.up);
            turnAmount=Mathf.Clamp(directionError/35f,-1f,1f);
        }
        else
            turnAmount=Mathf.Clamp(smoothedTurnRate/FullAnticipationTurnRate,-1f,1f);
        if(Mathf.Abs(turnAmount)<0.035f)turnAmount=0f;

        // The eye line commits first, the torso catches up, and the tail
        // counterbalances with progressively softer response toward its tip.
        headYaw=Damp(headYaw,turnAmount*22f,24f,dt);
        neckYaw=Damp(neckYaw,turnAmount*14f,17f,dt);
        chestYaw=Damp(chestYaw,turnAmount*7f,11f,dt);
        pelvisYaw=Damp(pelvisYaw,compactTorsoRig?turnAmount*3.5f:0f,9f,dt);
        tailYaw[0]=Damp(tailYaw[0],turnAmount*-7f,8f,dt);
        tailYaw[1]=Damp(tailYaw[1],turnAmount*-10f,6.5f,dt);
        tailYaw[2]=Damp(tailYaw[2],turnAmount*-13f,5f,dt);
        tailYaw[3]=Damp(tailYaw[3],turnAmount*-16f,4f,dt);

        ApplyYaw(head,headYaw);
        ApplyYaw(neck,neckYaw);
        ApplyYaw(chest,chestYaw);
        if(compactTorsoRig)ApplyYaw(pelvis,pelvisYaw);
        for(int i=0;i<tails.Length;i++)ApplyYaw(tails[i],tailYaw[i]);
        CaptureOutputPose();
    }

    private void ResetMotion()
    {
        lastRootYaw=transform.eulerAngles.y;
        smoothedTurnRate=headYaw=neckYaw=chestYaw=pelvisYaw=0f;
        Array.Clear(tailYaw,0,tailYaw.Length);
        outputPoseCaptured=false;
        hasDesiredDirection=false;
        desiredDirectionAge=0f;
    }

    private void RemovePersistedOffsets()
    {
        if(!outputPoseCaptured)return;
        RemovePersistedYaw(head,headYaw,headOutputRotation);
        RemovePersistedYaw(neck,neckYaw,neckOutputRotation);
        RemovePersistedYaw(chest,chestYaw,chestOutputRotation);
        if(compactTorsoRig)RemovePersistedYaw(pelvis,pelvisYaw,pelvisOutputRotation);
        for(int i=0;i<tails.Length;i++)
            RemovePersistedYaw(tails[i],tailYaw[i],tailOutputRotations[i]);
        outputPoseCaptured=false;
    }

    private void CaptureOutputPose()
    {
        headOutputRotation=LocalRotation(head);
        neckOutputRotation=LocalRotation(neck);
        chestOutputRotation=LocalRotation(chest);
        pelvisOutputRotation=LocalRotation(pelvis);
        for(int i=0;i<tails.Length;i++)tailOutputRotations[i]=LocalRotation(tails[i]);
        outputPoseCaptured=true;
    }

    private static Transform FindBone(Transform[] bones,string name)
    {
        return bones.FirstOrDefault(bone=>bone.name.Equals(name,StringComparison.Ordinal));
    }

    private static string UltraLowSdTailPath(int index)
    {
        string path="Root/Body";
        for(int i=0;i<=index;i++)path+="/Tail.0"+(i+1);
        return path;
    }

    private static string VoxelDragonTailPath(int index)
    {
        string path="RIG_VoxelDragon/Root/Pelvis";
        for(int i=0;i<=index;i++)path+="/Tail.0"+(i+1);
        return path;
    }

    private static void ApplyYaw(Transform bone,float degrees)
    {
        if(bone==null||Mathf.Abs(degrees)<0.001f)return;
        Vector3 parentUp=bone.parent!=null
            ?bone.parent.InverseTransformDirection(Vector3.up)
            :Vector3.up;
        bone.localRotation=Quaternion.AngleAxis(degrees,parentUp)*bone.localRotation;
    }

    private static void RemovePersistedYaw(Transform bone,float degrees,Quaternion previousOutput)
    {
        if(bone==null||Mathf.Abs(degrees)<0.001f)return;
        // An Animator that keys this bone has already replaced last frame's
        // output. Only undo our old offset when the pose is still unchanged,
        // which is the case for an empty/unkeyed Move clip.
        if(Quaternion.Angle(bone.localRotation,previousOutput)>0.05f)return;
        Vector3 parentUp=bone.parent!=null
            ?bone.parent.InverseTransformDirection(Vector3.up)
            :Vector3.up;
        bone.localRotation=Quaternion.AngleAxis(-degrees,parentUp)*bone.localRotation;
    }

    private static Quaternion LocalRotation(Transform bone)
    {
        return bone!=null?bone.localRotation:Quaternion.identity;
    }

    private static float Damp(float current,float target,float response,float dt)
    {
        return Mathf.Lerp(current,target,1f-Mathf.Exp(-response*dt));
    }
}
