using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(10010)]
public sealed class SpriteMonsterBillboard : MonoBehaviour
{
    [SerializeField] private bool mirrorWithMovement=true;
    [SerializeField] private bool invertHorizontalFacing=true;
    [SerializeField,Range(0f,1f)] private float mirrorDeadZone=0.08f;

    private SpriteRenderer spriteRenderer;
    private SpriteMonsterAnimationDriver animationDriver;
    private Transform facingRoot;
    private Camera cachedCamera;
    private Vector3 desiredWorldFacing;
    private bool hasDesiredWorldFacing;
    private float rollAngle;
    private Vector3 rollPivot;
    private Vector3 rollOffset;
    private Vector3 appliedRollOffset;
    private static readonly int GroundClip=Shader.PropertyToID("_GroundClip");
    private static Material groundClipMaterial;
    private MaterialPropertyBlock groundClipProperties;
    private bool groundClipActive;
    private float groundClipHeight;
    private bool groundFullySubmerged;
    public bool IsFullySubmerged=>groundClipActive&&groundFullySubmerged;
    public Quaternion ViewRotation
    {
        get
        {
            Camera camera=ResolveCamera();
            return camera==null?transform.rotation:camera.transform.rotation*Quaternion.Euler(0f,0f,rollAngle);
        }
    }

    private void Awake()
    {
        spriteRenderer=GetComponent<SpriteRenderer>();
        spriteRenderer.spriteSortPoint=SpriteSortPoint.Pivot;
        animationDriver=GetComponent<SpriteMonsterAnimationDriver>();
        facingRoot=transform.parent!=null?transform.parent:transform;
    }

    private void OnEnable()
    {
        cachedCamera=null;
        SetRollPose(0f,Vector3.zero,Vector3.zero);
    }

    public void SetRollPose(float angle,Vector3 localPivot,Vector3 worldOffset)
    {
        rollAngle=angle;
        rollPivot=localPivot;
        rollOffset=worldOffset;
        if(angle==0f&&worldOffset==Vector3.zero)
        {
            ClearRollOffset();
            Camera camera=ResolveCamera();
            if(camera!=null)transform.rotation=camera.transform.rotation;
        }
    }

    public void SetGroundClip(bool active,float height=0f)
    {
        if(!active&&!groundClipActive)return;
        groundClipActive=active;groundClipHeight=height;
        if(!active)groundFullySubmerged=false;
        ApplyGroundClip();
    }

    public void SetGroundFullySubmerged(bool value)
    {
        value&=groundClipActive;
        if(groundFullySubmerged==value)return;
        groundFullySubmerged=value;ApplyGroundClip();
    }

    private void ApplyGroundClip()
    {
        if(spriteRenderer==null)spriteRenderer=GetComponent<SpriteRenderer>();
        if(spriteRenderer==null)return;
        if(groundClipActive&&(spriteRenderer.sharedMaterial==null||!spriteRenderer.sharedMaterial.HasProperty(GroundClip)))
        {
            if(groundClipMaterial==null)
            {
                Shader shader=Resources.Load<Shader>("CombatEffects/SpriteHitFlash");
                if(shader==null)return;
                groundClipMaterial=new Material(shader){name="Shared submerging pixel sprite",hideFlags=HideFlags.HideAndDontSave};
            }
            // Keep this hit-compatible shared material after surfacing; delayed flash restores remain safe.
            spriteRenderer.sharedMaterial=groundClipMaterial;
        }
        if(groundClipProperties==null)groundClipProperties=new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(groundClipProperties);
        groundClipProperties.SetVector(GroundClip,new Vector4(groundClipHeight,groundClipActive?1f:0f,groundFullySubmerged?1f:0f,0f));
        spriteRenderer.SetPropertyBlock(groundClipProperties);
    }

    public Vector3 UnposedPoint(Vector3 localPoint)
    {
        Camera camera=ResolveCamera();
        Quaternion rotation=camera==null?transform.rotation:camera.transform.rotation;
        Vector3 offset=transform.parent==null?appliedRollOffset:
            transform.parent.TransformVector(appliedRollOffset);
        return transform.position-offset+
            rotation*Vector3.Scale(localPoint,transform.lossyScale);
    }

    internal void ClearRollOffset()
    {
        transform.localPosition-=appliedRollOffset;
        appliedRollOffset=Vector3.zero;
    }

    private void Update()=>ClearRollOffset();
    private void OnDisable(){SetGroundClip(false);SetRollPose(0f,Vector3.zero,Vector3.zero);}

    public void SetFacingDirection(Vector3 worldDirection,bool force=false)
    {
        if(!force&&animationDriver!=null&&animationDriver.LocksHorizontalFacing)return;
        worldDirection.y=0f;
        if(worldDirection.sqrMagnitude<0.0001f)return;
        desiredWorldFacing=worldDirection.normalized;
        hasDesiredWorldFacing=true;
        ApplyHorizontalFacing(ResolveCamera());
    }

    private void LateUpdate()
    {
        ClearRollOffset();
        Camera targetCamera=ResolveCamera();
        if(targetCamera==null)return;

        Quaternion roll=Quaternion.Euler(0f,0f,rollAngle);
        transform.rotation=targetCamera.transform.rotation*roll;
        Vector3 pivot=Vector3.Scale(rollPivot,transform.lossyScale);
        Vector3 offset=rollOffset+targetCamera.transform.rotation*(pivot-roll*pivot);
        appliedRollOffset=transform.parent==null?offset:
            transform.parent.InverseTransformVector(offset);
        transform.localPosition+=appliedRollOffset;
        if(groundClipActive)ApplyGroundClip();
        ApplyHorizontalFacing(targetCamera);
    }

    private void ApplyHorizontalFacing(Camera targetCamera)
    {
        if(targetCamera==null||!mirrorWithMovement||
           spriteRenderer==null||facingRoot==null)
            return;

        Vector3 facing=hasDesiredWorldFacing
            ?desiredWorldFacing
            :facingRoot.forward;
        float horizontal=Vector3.Dot(facing,targetCamera.transform.right);
        if(Mathf.Abs(horizontal)>mirrorDeadZone)
            spriteRenderer.flipX=invertHorizontalFacing
                ?horizontal<0f
                :horizontal>0f;
    }

    private Camera ResolveCamera()
    {
        if(cachedCamera!=null&&cachedCamera.isActiveAndEnabled)return cachedCamera;
        cachedCamera=Camera.main;
        if(cachedCamera==null)cachedCamera=FindObjectOfType<Camera>();
        return cachedCamera;
    }
}
