using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class VoxelEnemyReaction : MonoBehaviour
{
    private const float HitDuration=0.16f;
    private const float LocalHitStopDuration=0.035f;
    private const float FlashDuration=0.23f;
    private const float DeathDuration=0.65f;
    private const float DeathGravityMultiplier=2.5f;
    private const int MaxPooledEnemyRagdolls=6;
    private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty=Shader.PropertyToID("_Color");
    private static readonly int HitAmount=Shader.PropertyToID("_HitAmount");
    private static readonly int HitColor=Shader.PropertyToID("_HitColor");
    private static readonly Color HitFlashColor=new Color(1f,0.08f,0.045f);
    private static Material spriteHitFlashMaterial;

    private Animator animator;
    private SpriteMonsterAnimationDriver spriteAnimation;
    private SpriteRenderer hitSprite;
    private Material spriteFlashOriginalMaterial;
    private bool redContactFlash;
    private bool initialized;
    private Transform[] joints;
    private Vector3[] bindLocalPositions;
    private Quaternion[] bindLocalRotations;
    private Vector3[] hitLocalPositions;
    private Quaternion[] hitLocalRotations;
    private Renderer[] renderers;
    private bool[] originalRendererEnabled;
    private MaterialPropertyBlock[] originalRendererBlocks;
    private MaterialPropertyBlock scratchRendererBlock;
    private Color[] originalBaseColors;
    private Color[] originalColors;
    private float hitTime;
    private float flashTimer;
    private float activeFlashDuration=FlashDuration;
    private float flashPeak=.52f;
    private float contactAccentCooldown;
    private int contactStartedFrame=-1;
    private int contactPriority;
    private Color flashColor=HitFlashColor;
    private Color statusTintColor=Color.white;
    private float statusTintAmount;
    private float deathTime;
    private bool capturedDeath;
    private float hitLocalDisplacement=0.018f;
    private bool compactUnitRig;
    private Vector3 hitAnimationRootPosition;
    private Quaternion hitAnimationRootRotation;
    private Vector3 hitDirection;
    private bool hitting;
    private bool dying;
    private bool hitPoseCaptured;
    private Transform animationRoot;
    private Dictionary<string,Transform> sourcePoseByName;
    private EnemyRagdollProxy activeRagdoll;
    private string ragdollPoolKey;
    private Transform activeRagdollPoolRoot;
    private Stack<EnemyRagdollProxy> activeRagdollPool;

    private static readonly Dictionary<string,Transform> RagdollPoolRoots=new Dictionary<string,Transform>();
    private static readonly Dictionary<string,Stack<EnemyRagdollProxy>> AvailableRagdollsByRig=
        new Dictionary<string,Stack<EnemyRagdollProxy>>();

    public bool IsDying => dying;

    public void Initialize()
    {
        if(initialized)return;
        initialized=true;
        animator=GetComponentInChildren<Animator>(true);
        spriteAnimation=
            GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        animationRoot=animator!=null?animator.transform:transform;
        float rigScale=Mathf.Max(0.0001f,Mathf.Abs(animationRoot.lossyScale.x));
        // Blender-authored compact rigs can arrive with a 0.01 file unit and
        // therefore require a very large runtime root scale. A fixed local
        // translation would become a meter-scale world displacement and make
        // every voxel part appear to explode on an ordinary hit.
        compactUnitRig=rigScale>4f;
        hitLocalDisplacement=compactUnitRig?0.006f/rigScale:0.018f;
        joints=animationRoot.GetComponentsInChildren<Transform>(true)
            .Where(t=>t!=animationRoot&&!t.name.StartsWith("PART_",StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetDepth)
            .ToArray();
        hitLocalPositions=new Vector3[joints.Length];
        hitLocalRotations=new Quaternion[joints.Length];
        bindLocalPositions=new Vector3[joints.Length];
        bindLocalRotations=new Quaternion[joints.Length];
        for(int i=0;i<joints.Length;i++)
        {
            bindLocalPositions[i]=joints[i].localPosition;
            bindLocalRotations[i]=joints[i].localRotation;
        }

        renderers=GetComponentsInChildren<Renderer>(true);
        originalRendererEnabled=new bool[renderers.Length];
        originalRendererBlocks=new MaterialPropertyBlock[renderers.Length];
        originalBaseColors=new Color[renderers.Length];
        originalColors=new Color[renderers.Length];
        for(int i=0;i<renderers.Length;i++)
        {
            originalRendererEnabled[i]=renderers[i].enabled;
            originalRendererBlocks[i]=new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(originalRendererBlocks[i]);
            Material material=renderers[i].sharedMaterial;
            originalBaseColors[i]=material!=null&&material.HasProperty(BaseColor)
                ?material.GetColor(BaseColor)
                :Color.white;
            originalColors[i]=material!=null&&material.HasProperty(ColorProperty)
                ?material.GetColor(ColorProperty)
                :originalBaseColors[i];
        }
        if(spriteAnimation!=null)
        {
            hitSprite=spriteAnimation.GetComponent<SpriteRenderer>();
            if(spriteHitFlashMaterial==null)
            {
                Shader shader=Resources.Load<Shader>("CombatEffects/SpriteHitFlash");
                if(shader!=null)spriteHitFlashMaterial=new Material(shader)
                    {name="Shared pixel hit silhouette",hideFlags=HideFlags.HideAndDontSave};
            }
            PixelCombatEffect.PrepareContactImpacts();return;
        }
        ConfigureRagdollCollisionLayers();
        sourcePoseByName=new Dictionary<string,Transform>(StringComparer.Ordinal);
        foreach(Transform pose in animationRoot.GetComponentsInChildren<Transform>(true))
            if(!sourcePoseByName.ContainsKey(pose.name))sourcePoseByName.Add(pose.name,pose);
        ragdollPoolKey=BuildRigKey(animationRoot);
        EnsureRagdollPool(animationRoot,ragdollPoolKey);
        activeRagdollPoolRoot=RagdollPoolRoots[ragdollPoolKey];
        activeRagdollPool=AvailableRagdollsByRig[ragdollPoolKey];
    }

    public void PrepareForReuse()
    {
        Initialize();
        ReleaseActiveRagdoll();
        RestoreHitPose();
        statusTintAmount=0f;
        RestoreFlash();
        RestoreLiveRenderers();
        hitting=dying=false;
        capturedDeath=false;
        hitPoseCaptured=false;
        hitTime=flashTimer=deathTime=contactAccentCooldown=0f;
        contactStartedFrame=-1;
        for(int i=0;i<joints.Length;i++)
        {
            if(joints[i]==null)continue;
            joints[i].localPosition=bindLocalPositions[i];
            joints[i].localRotation=bindLocalRotations[i];
        }
        if(animator!=null)
        {
            animator.enabled=true;
            animator.speed=1f;
            animator.Rebind();
            animator.Update(0f);
            int stateHash=animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            if(stateHash!=0)animator.Play(stateHash,0,0f);
            animator.Update(0f);
        }
        if(spriteAnimation!=null)spriteAnimation.ResetForReuse();
    }

    public void ClearDeathVisual()
    {
        ReleaseActiveRagdoll();
        RestoreLiveRenderers();
        if(spriteAnimation!=null)spriteAnimation.ResetForReuse();
        capturedDeath=false;
        dying=false;
        deathTime=0f;
    }

    private void OnDestroy()
    {
        RestoreFlash();
        ReleaseActiveRagdoll();
    }

    private void OnDisable()
    {
        RestoreFlash();
    }

    public void PlayHit(Vector3 worldImpactDirection,bool strong=false,bool contact=true)
        =>PlayHitCore(worldImpactDirection,strong,contact,false);

    public void PlayBasicHitFeedback(Vector3 worldImpactDirection,bool strong)
        =>PlayHitCore(worldImpactDirection,strong,true,CombatReadabilityPresentation.RefinedSkills);

    public void PlaySkillHitFeedback(Vector3 worldImpactDirection,bool strong)
        =>PlayHitCore(worldImpactDirection,strong,true,false,
            CombatReadabilityPresentation.Local);

    private void PlayHitCore(Vector3 worldImpactDirection,bool strong,bool contact,bool basic,bool skill=false)
    {
        if(CombatReadabilityPresentation.Active&&!CombatReadabilityPresentation.ContactFeedback)return;
        if(dying)return;
        Initialize();
        if(hitting)RestoreHitPose();
        if(spriteAnimation!=null)
        {
            Vector3 incoming=CombatReadabilityPresentation.ProjectileContactDirection;
            if(contact&&CombatReadabilityPresentation.RefinedSkills&&incoming.sqrMagnitude>.000001f&&
               CombatReadabilityPresentation.ProjectileContactRenderer==spriteAnimation.GetComponent<SpriteRenderer>())
                worldImpactDirection=incoming;
            if(contact)
            {
                if(contactAccentCooldown>0f
                   &&(!(CombatReadabilityPresentation.Local)||
                      (skill?2:basic?1:0)<=contactPriority)
                  )return;
                contactAccentCooldown=basic?.085f:.12f;
                contactStartedFrame=Time.frameCount;
                contactPriority=skill?2:basic?1:0;
            }
            else if(flashTimer>0f||contactAccentCooldown>0f)return;
            hitPoseCaptured=false;
            hitting=false;
            activeFlashDuration=skill?(strong?.10f:.08f):basic?(strong?.075f:.06f):contact?(strong?.11f:.09f):.05f;
            flashTimer=activeFlashDuration;
            flashPeak=skill?(strong?.80f:.68f):basic?(strong?.68f:.52f):contact?(strong?.7f:.55f):.2f;
            if(!basic&&!skill)flashPeak*=CombatReadabilityPresentation.ContactIntensity;
            redContactFlash=contact;
            flashColor=contact?HitFlashColor:new Color(1f,.6f,.4f);
            if(contact)
            {
                if(basic)spriteAnimation.PlayBasicContactReaction(worldImpactDirection,strong);
                else if(skill)spriteAnimation.PlaySkillContactReaction(worldImpactDirection,strong);
                else
                    spriteAnimation.PlayContactReaction(worldImpactDirection,strong);
                PixelCombatEffect.PlayContactImpact(transform,
                    spriteAnimation.GetComponent<SpriteRenderer>(),worldImpactDirection,strong,skill);
            }
            UpdateFlash(0f);
            return;
        }
        flashColor=HitFlashColor;
        activeFlashDuration=skill?(strong?.10f:.08f):basic?(strong?.075f:.06f):FlashDuration;
        flashPeak=skill?(strong?.80f:.68f):basic?(strong?.68f:.52f):.52f;
        if(!basic&&!skill)flashPeak*=CombatReadabilityPresentation.ContactIntensity;
        if(animator!=null&&animator.enabled)
        {
            animator.speed=1f;
            animator.Update(0f);
        }
        hitDirection=worldImpactDirection.sqrMagnitude>0.001f?worldImpactDirection.normalized:transform.forward;
        hitAnimationRootPosition=animationRoot.localPosition;
        hitAnimationRootRotation=animationRoot.localRotation;
        for(int i=0;i<joints.Length;i++)
        {
            hitLocalPositions[i]=joints[i].localPosition;
            hitLocalRotations[i]=joints[i].localRotation;
        }
        hitPoseCaptured=true;
        hitTime=0f;
        flashTimer=activeFlashDuration;
        hitting=true;
        if(animator!=null)animator.speed=0f;
    }

    public void PlayTintHit(Color color)
    {
        if(dying)return;
        Initialize();
        if(redContactFlash&&flashTimer>0f)return;
        flashColor=color;
        activeFlashDuration=FlashDuration;flashPeak=.52f;
        flashTimer=FlashDuration;
    }

    public bool PlayReturnMark(float duration)
    {
        if(dying)return false;
        Initialize();
        RestoreHitPose();
        RestoreFlash();
        hitting=false;
        if(spriteAnimation==null)return false;
        SpriteRenderer sprite=spriteAnimation.GetComponent<SpriteRenderer>();
        spriteAnimation.InterruptCurrentAction();
        if(sprite==null||sprite.sprite==null||
           !SpriteDeathFragments.CreateReturn(sprite,duration))return false;
        sprite.enabled=false;
        return true;
    }

    public void FinishReturnMark()
    {
        Initialize();
        RestoreLiveRenderers();
        spriteAnimation?.ResetForReuse();
    }

    public void SetStatusTint(Color color,float amount)
    {
        Initialize();
        amount=Mathf.Clamp01(amount);
        if(statusTintColor==color&&Mathf.Approximately(statusTintAmount,amount))return;
        statusTintColor=color;
        statusTintAmount=amount;
        if(flashTimer<=0f)RestoreFlash();
    }

    public void PlayDeath(Vector3 worldImpactDirection,Action onBurst=null)
    {
        if(dying)return;
        Initialize();
        RestoreHitPose();
        RestoreFlash();
        hitting=false;
        dying=true;
        capturedDeath=false;
        deathTime=0f;
        if(spriteAnimation!=null)
        {
            SpriteRenderer sprite=spriteAnimation.GetComponent<SpriteRenderer>();
            spriteAnimation.FreezeCurrentFrame();
            if(sprite==null||sprite.sprite==null||
               !SpriteDeathFragments.Create(sprite,worldImpactDirection,onBurst))
                onBurst?.Invoke();
            return;
        }
        if(animator!=null)
        {
            animator.speed=0f;
            animator.enabled=false;
        }

        Vector3 impact=worldImpactDirection.sqrMagnitude>0.001f?worldImpactDirection.normalized:transform.forward;
        impact.y=0f;
        if(impact.sqrMagnitude<0.001f)impact=transform.forward;
        impact.Normalize();
        activeRagdoll=AcquireRagdoll();
        if(activeRagdoll!=null)activeRagdoll.Activate(animationRoot,sourcePoseByName,impact,activeRagdollPoolRoot);
        for(int i=0;i<renderers.Length;i++)renderers[i].enabled=false;
        onBurst?.Invoke();
    }

    public void PlayCapturedDeath(Vector3 worldImpactDirection)
    {
        if(dying)return;
        Initialize();
        if(spriteAnimation!=null)
        {
            RestoreHitPose();
            RestoreFlash();
            hitting=false;
            dying=true;
            capturedDeath=true;
            deathTime=0f;
            spriteAnimation.PlayCapture();
            return;
        }
        PlayDeath(worldImpactDirection);
        capturedDeath=dying;
    }

    public void PlayCaptureHold()
    {
        if(dying)return;
        Initialize();
        RestoreHitPose();
        RestoreFlash();
        hitting=false;
        dying=true;
        capturedDeath=true;
        deathTime=0f;
        if(spriteAnimation!=null)
        {
            spriteAnimation.InterruptCurrentAction();
            spriteAnimation.SetStruggling(true);
            return;
        }
        if(animator!=null)animator.speed=0f;
    }

    public void ResolveCaptureFailure(Vector3 worldImpactDirection,Action onBurst=null)
    {
        if(!capturedDeath)return;
        if(spriteAnimation!=null)
            spriteAnimation.SetStruggling(false);
        capturedDeath=false;
        dying=false;
        PlayDeath(worldImpactDirection,onBurst);
    }

    public bool BeginCaptureLift()
    {
        if(!capturedDeath||activeRagdoll==null)return false;
        activeRagdoll.BeginLift();
        return true;
    }

    public void SetCaptureLiftOffset(Vector3 offset)
    {
        if(!capturedDeath||activeRagdoll==null)return;
        activeRagdoll.SetLiftOffset(offset);
    }

    public void FinishCapturedDeath()
    {
        ReleaseActiveRagdoll();
        if(spriteAnimation!=null)
            spriteAnimation.SetStruggling(false);
        RestoreLiveRenderers();
        capturedDeath=false;
        dying=false;
        deathTime=0f;
    }

    private void Update()
    {
        float dt=Time.deltaTime;
        float feedbackDelta=dt;
        // This frame's delta began before a new impact, just as in the sprite recoil driver.
        if(redContactFlash&&CombatReadabilityPresentation.Adjusted&&contactStartedFrame==Time.frameCount)feedbackDelta=0f;
        contactAccentCooldown=Mathf.Max(0f,contactAccentCooldown-feedbackDelta);
        if(hitting)UpdateHit(dt);
        if(flashTimer>0f)UpdateFlash(feedbackDelta);
        if(dying)UpdateDeath(dt);
    }

    private void FixedUpdate()
    {
        if(dying&&activeRagdoll!=null)activeRagdoll.ApplyExtraGravity();
    }

    private void UpdateFlash(float dt)
    {
        flashTimer=Mathf.Max(0f,flashTimer-dt);
        float remaining=flashTimer/Mathf.Max(.001f,activeFlashDuration);
        float amount=(spriteAnimation!=null?remaining:Mathf.Sin((1f-remaining)*Mathf.PI))*
            CombatFlashPresentation.AccentScale;
        for(int i=0;i<renderers.Length;i++)
        {
            scratchRendererBlock??=new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(scratchRendererBlock);
            bool silhouette=redContactFlash&&renderers[i]==hitSprite&&spriteHitFlashMaterial!=null;
            if(silhouette)
            {
                if(hitSprite.sharedMaterial!=spriteHitFlashMaterial)
                {
                    spriteFlashOriginalMaterial=hitSprite.sharedMaterial;
                    hitSprite.sharedMaterial=spriteHitFlashMaterial;
                }
                scratchRendererBlock.SetColor(HitColor,HitFlashColor);
                scratchRendererBlock.SetFloat(HitAmount,amount*flashPeak);
            }
            scratchRendererBlock.SetColor(BaseColor,Color.Lerp(
                RestingColor(originalBaseColors[i]),flashColor,silhouette?0f:amount*flashPeak));
            scratchRendererBlock.SetColor(ColorProperty,Color.Lerp(
                RestingColor(originalColors[i]),flashColor,silhouette?0f:amount*flashPeak));
            renderers[i].SetPropertyBlock(scratchRendererBlock);
        }
        if(flashTimer<=0f)RestoreFlash();
    }

    private void RestoreFlash()
    {
        if(redContactFlash&&hitSprite!=null&&hitSprite.sharedMaterial==spriteHitFlashMaterial)
            hitSprite.sharedMaterial=spriteFlashOriginalMaterial;
        redContactFlash=false;
        spriteFlashOriginalMaterial=null;
        if(renderers==null||originalRendererBlocks==null)return;
        flashTimer=0f;
        for(int i=0;i<renderers.Length;i++)
        {
            if(renderers[i]==null)continue;
            if(spriteAnimation!=null&&renderers[i] is SpriteRenderer)
            {
                scratchRendererBlock??=new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(scratchRendererBlock);
                scratchRendererBlock.SetFloat(HitAmount,0f);
                scratchRendererBlock.SetColor(BaseColor,RestingColor(originalBaseColors[i]));
                scratchRendererBlock.SetColor(ColorProperty,RestingColor(originalColors[i]));
                renderers[i].SetPropertyBlock(scratchRendererBlock);
                continue;
            }
            renderers[i].SetPropertyBlock(originalRendererBlocks[i]);
            if(statusTintAmount<=0f)continue;
            scratchRendererBlock??=new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(scratchRendererBlock);
            scratchRendererBlock.SetColor(BaseColor,RestingColor(originalBaseColors[i]));
            scratchRendererBlock.SetColor(ColorProperty,RestingColor(originalColors[i]));
            renderers[i].SetPropertyBlock(scratchRendererBlock);
        }
    }

    private Color RestingColor(Color original)
    {
        return Color.Lerp(original,statusTintColor,statusTintAmount);
    }

    private void UpdateHit(float dt)
    {
        hitTime+=dt;
        Vector3 localImpact=transform.InverseTransformDirection(hitDirection);
        localImpact.y=0f;
        if(localImpact.sqrMagnitude<0.001f)localImpact=Vector3.forward;
        localImpact.Normalize();
        if(compactUnitRig)
        {
            float t=Mathf.Clamp01(hitTime/HitDuration);
            float pulse=Mathf.Sin(t*Mathf.PI);
            animationRoot.localPosition=hitAnimationRootPosition-localImpact*(hitLocalDisplacement*pulse);
            animationRoot.localRotation=hitAnimationRootRotation*
                Quaternion.Euler(0f,-localImpact.x*1.4f*pulse,localImpact.z*0.9f*pulse);
            if(animator!=null&&hitTime>=LocalHitStopDuration)animator.speed=1f;
            if(hitTime<HitDuration)return;
            RestoreHitPose();
            hitting=false;
            return;
        }
        for(int i=0;i<joints.Length;i++)
        {
            float delay=Mathf.Min(0.065f,GetDepth(joints[i])*0.006f);
            float t=Mathf.Clamp01((hitTime-delay)/(HitDuration-delay));
            float pulse=Mathf.Sin(t*Mathf.PI);
            float weight=1f-Mathf.Min(0.55f,i*0.018f);
            joints[i].localPosition=hitLocalPositions[i]-localImpact*(hitLocalDisplacement*pulse*weight);
            joints[i].localRotation=hitLocalRotations[i]*Quaternion.Euler(0f,-localImpact.x*5.5f*pulse*weight,localImpact.z*3.5f*pulse*weight);
        }
        if(animator!=null&&hitTime>=LocalHitStopDuration)animator.speed=1f;
        if(hitTime<HitDuration)return;
        RestoreHitPose();
        hitting=false;
    }

    private void UpdateDeath(float dt)
    {
        deathTime+=dt;
        if(capturedDeath)return;
        if(deathTime<DeathDuration)return;
        ReleaseActiveRagdoll();
        RestoreLiveRenderers();
        dying=false;
        gameObject.SetActive(false);
    }

    private void RestoreHitPose()
    {
        if(joints==null||!hitPoseCaptured)return;
        if(animationRoot!=null)
        {
            animationRoot.localPosition=hitAnimationRootPosition;
            animationRoot.localRotation=hitAnimationRootRotation;
        }
        for(int i=0;i<joints.Length;i++)
        {
            if(joints[i]==null)continue;
            if(hitLocalPositions!=null&&i<hitLocalPositions.Length)joints[i].localPosition=hitLocalPositions[i];
            if(hitLocalRotations!=null&&i<hitLocalRotations.Length)joints[i].localRotation=hitLocalRotations[i];
        }
        hitPoseCaptured=false;
        if(animator!=null)
        {
            animator.speed=1f;
            if(animator.enabled)animator.Update(0f);
        }
    }

    private void RestoreLiveRenderers()
    {
        if(renderers==null||originalRendererEnabled==null)return;
        for(int i=0;i<renderers.Length;i++)
            if(renderers[i]!=null)renderers[i].enabled=originalRendererEnabled[i];
    }

    private static string BuildRigKey(Transform templateRoot)
    {
        string[] names=templateRoot.GetComponentsInChildren<Transform>(true)
            .Select(item=>item.name)
            .OrderBy(name=>name,StringComparer.Ordinal)
            .ToArray();
        // Bone-compatible variants can share animation clips, but their death
        // ragdolls must retain the correct meshes and materials.
        string[] renderSignatures=templateRoot.GetComponentsInChildren<Renderer>(true)
            .Select(renderer=>
            {
                MeshFilter filter=renderer.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skinned=renderer as SkinnedMeshRenderer;
                Mesh mesh=filter!=null?filter.sharedMesh:skinned!=null?skinned.sharedMesh:null;
                string materials=string.Join(",",renderer.sharedMaterials
                    .Where(material=>material!=null)
                    .Select(material=>material.GetInstanceID().ToString()));
                return renderer.name+":"+(mesh!=null?mesh.GetInstanceID():0)+":"+materials;
            })
            .OrderBy(signature=>signature,StringComparer.Ordinal)
            .ToArray();
        return templateRoot.name+"|"+string.Join("/",names)+"|"+string.Join("/",renderSignatures);
    }

    private static void EnsureRagdollPool(Transform templateRoot,string key)
    {
        ConfigureRagdollCollisionLayers();
        if(RagdollPoolRoots.ContainsKey(key))return;
        Transform poolRoot=new GameObject("Enemy Ragdoll Pool - "+templateRoot.name).transform;
        Stack<EnemyRagdollProxy> pool=new Stack<EnemyRagdollProxy>();
        RagdollPoolRoots.Add(key,poolRoot);
        AvailableRagdollsByRig.Add(key,pool);
        for(int i=0;i<MaxPooledEnemyRagdolls;i++)
            pool.Push(new EnemyRagdollProxy(templateRoot,poolRoot,i));
    }

    private static void ConfigureRagdollCollisionLayers()
    {
        int deathPartLayer=LayerMask.NameToLayer("EnemyDeathPart");
        if(deathPartLayer<0)return;

        // Death ragdolls pass through one another, while still colliding with
        // the Default layer used by the floor and other world geometry.
        Physics.IgnoreLayerCollision(deathPartLayer,deathPartLayer,true);

        int actorLayer=LayerMask.NameToLayer("CombatActor");
        if(actorLayer>=0)Physics.IgnoreLayerCollision(deathPartLayer,actorLayer,true);
    }

    private EnemyRagdollProxy AcquireRagdoll()
    {
        if(activeRagdollPool==null)return null;
        while(activeRagdollPool.Count>0)
        {
            EnemyRagdollProxy proxy=activeRagdollPool.Pop();
            if(proxy!=null&&proxy.IsValid)return proxy;
        }
        return null;
    }

    private void ReleaseActiveRagdoll()
    {
        if(activeRagdoll==null)return;
        activeRagdoll.Release(activeRagdollPoolRoot);
        if(activeRagdollPoolRoot!=null&&activeRagdollPool!=null)activeRagdollPool.Push(activeRagdoll);
        activeRagdoll=null;
    }

    private static Rigidbody FindEnemyParentBody(Transform parent,Dictionary<Transform,Rigidbody> bodies)
    {
        for(Transform current=parent;current!=null;current=current.parent)
            if(bodies.TryGetValue(current,out Rigidbody body))return body;
        return null;
    }

    private static float EnemyBoneMass(string boneName)
    {
        if(boneName.IndexOf("Pelvis",StringComparison.OrdinalIgnoreCase)>=0||
           boneName.IndexOf("Spine",StringComparison.OrdinalIgnoreCase)>=0||
           string.Equals(boneName,"Body",StringComparison.OrdinalIgnoreCase))return 3.2f;
        if(boneName.IndexOf("Chest",StringComparison.OrdinalIgnoreCase)>=0)return 2.4f;
        if(boneName.IndexOf("Neck",StringComparison.OrdinalIgnoreCase)>=0)return 1.5f;
        if(boneName.IndexOf("Head",StringComparison.OrdinalIgnoreCase)>=0)return 1.8f;
        return 1.25f;
    }

    internal sealed class SpriteDeathFragments:MonoBehaviour
    {
        private const int Grid=48;
        private const int MaxPhasePixels=1200;
        private const int MaxFlyingPixels=64;
        private const float BurstDelay=0.05f;
        private static Material particleMaterial;
        private static Material voxelMaterial;
        private static Mesh voxelMesh;

        private struct PhasePixel
        {
            public Vector3 origin;
            public Vector3 scatter;
            public Color32 color;
            public float age;
            public float delay;
            public float duration;
            public float size;
        }

        private struct FlyingPixel
        {
            public Vector3 position;
            public Vector3 velocity;
            public Color32 color;
            public float life;
            public float maxLife;
            public float spin;
            public float size;
        }

        private readonly List<PhasePixel> phasePixels=
            new List<PhasePixel>(MaxPhasePixels);
        private readonly List<FlyingPixel> flyingPixels=
            new List<FlyingPixel>(MaxFlyingPixels);
        private readonly ParticleSystem.Particle[] renderedPixels=
            new ParticleSystem.Particle[MaxPhasePixels+MaxFlyingPixels];
        private ParticleSystem pixelParticles;
        private float elapsed;
        private bool returning,popProfile;
        private float returnDuration;
        private Vector3 burstCenter;
        private float visibleBodyWidth,visibleBodySpan;
        private Action onBurst;
        private SpriteRenderer heldSource;
        internal bool IsDeathBurst=>!returning;

        public static bool Create(SpriteRenderer source,Vector3 impact,Action onBurst)
        {
            GameObject root=new GameObject("Sprite Death Fragments");
            if(source.gameObject.scene.IsValid()&&source.gameObject.scene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,source.gameObject.scene);
            SpriteDeathFragments effect=root.AddComponent<SpriteDeathFragments>();
            effect.onBurst=onBurst;
            if(effect.Initialize(source,impact))return true;
            if(Application.isPlaying)Destroy(root);
            else DestroyImmediate(root);
            return false;
        }

        public static bool CreateReturn(SpriteRenderer source,float duration)
        {
            GameObject root=new GameObject("Return Mark Pixel Reassembly");
            if(source.gameObject.scene.IsValid()&&source.gameObject.scene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,source.gameObject.scene);
            SpriteDeathFragments effect=root.AddComponent<SpriteDeathFragments>();
            effect.returning=true;
            effect.returnDuration=Mathf.Max(0.1f,duration);
            if(effect.Initialize(source,Vector3.zero))return true;
            if(Application.isPlaying)Destroy(root);
            else DestroyImmediate(root);
            return false;
        }

        private bool Initialize(SpriteRenderer source,Vector3 impact)
        {
            Sprite sourceSprite=source.sprite;
            burstCenter=source.bounds.center;
            popProfile=CombatReadabilityPresentation.Local&&!returning;
            if(popProfile)
            {
                burstCenter=CombatReadabilityPresentation.VisibleBodyCenter(source);
                Bounds visible=PixelMantisReaper.VisibleBody(sourceSprite);
                visibleBodyWidth=source.transform.TransformVector(Vector3.right*visible.size.x).magnitude;
                visibleBodySpan=Mathf.Max(visibleBodyWidth,
                    source.transform.TransformVector(Vector3.up*visible.size.y).magnitude);
            }
            if(!returning)heldSource=source;
            Bounds bounds=sourceSprite.bounds;
            Color32[] colors=ReadCurrentFrame(sourceSprite);
            float cellWidth=bounds.size.x/Grid;
            float cellHeight=bounds.size.y/Grid;
            float cellSize=Mathf.Min(
                source.transform.TransformVector(Vector3.right*cellWidth).magnitude,
                source.transform.TransformVector(Vector3.up*cellHeight).magnitude);
            impact.y=0f;
            if(impact.sqrMagnitude<0.001f)impact=transform.forward;
            impact.Normalize();

            for(int y=0;y<Grid;y++)
            for(int x=0;x<Grid;x++)
            {
                int index=y*Grid+x;
                if(colors[index].a<32||phasePixels.Count>=MaxPhasePixels)continue;
                float localX=bounds.min.x+(x+0.5f)*cellWidth;
                float localY=bounds.min.y+(y+0.5f)*cellHeight;
                Vector3 localPosition=new Vector3(
                    source.flipX?-localX:localX,
                    source.flipY?-localY:localY,0f);
                Vector3 origin=source.transform.TransformPoint(localPosition);
                Color sampled=(Color)colors[index]*source.color;
                Color32 color=sampled;
                Vector3 scatterDirection=
                    source.transform.right*UnityEngine.Random.Range(-1f,1f)+
                    source.transform.up*UnityEngine.Random.Range(-0.7f,1f)+
                    source.transform.forward*UnityEngine.Random.Range(popProfile?-.8f:-.18f,popProfile?.8f:.18f);
                if(scatterDirection.sqrMagnitude<0.001f)
                    scatterDirection=source.transform.up;
                if(popProfile)
                {
                    Vector3 radial=origin-burstCenter;
                    if(radial.sqrMagnitude<.0001f)radial=scatterDirection;
                    scatterDirection=radial.normalized*.6f+scatterDirection.normalized*.4f;
                }
                float distanceSample=UnityEngine.Random.Range(.18f,.42f);
                float scatterDistance=Mathf.Max(
                    cellSize*3f,Mathf.Min(source.bounds.size.x,source.bounds.size.y)*
                    distanceSample);
                if(popProfile)scatterDistance=visibleBodySpan*Mathf.Lerp(2f,3f,(distanceSample-.18f)/.24f);
                phasePixels.Add(new PhasePixel
                {
                    origin=origin,scatter=scatterDirection.normalized*scatterDistance,
                    color=color,age=0f,
                    delay=returning?0f:BurstDelay,
                    duration=returning?returnDuration:
                        UnityEngine.Random.Range(0.10f,0.14f)*(popProfile?10f:1f),
                    size=cellSize
                });
                if(returning||flyingPixels.Count>=MaxFlyingPixels||
                   UnityEngine.Random.value>=0.12f)continue;
                Vector3 scatter=new Vector3(
                    UnityEngine.Random.Range(-1f,1f),
                    UnityEngine.Random.Range(0.18f,1.35f),
                    UnityEngine.Random.Range(-1f,1f)).normalized;
                Vector3 launchDirection=(scatter+impact*0.22f).normalized;
                float life=UnityEngine.Random.Range(0.2f,0.32f)*(popProfile?4f:1f);
                float speedSample=UnityEngine.Random.Range(1.2f,2f);
                flyingPixels.Add(new FlyingPixel
                {
                    position=origin,
                    velocity=popProfile?launchDirection*visibleBodyWidth*Mathf.Lerp(6f,9f,(speedSample-1.2f)/.8f):
                        launchDirection*Mathf.Max(0.25f,source.bounds.size.magnitude)*speedSample,
                    color=color,life=life,maxLife=life,
                    spin=UnityEngine.Random.Range(-18f,18f),size=cellSize*UnityEngine.Random.Range(1.6f,2.4f)*(popProfile?1.5f:1f)
                });
            }

            if(phasePixels.Count==0)return false;
            ConfigureParticles(source);
            RenderPixels();
            return true;
        }

        private void Update() => Advance(Time.deltaTime);

        private void Advance(float dt)
        {
            float previous=elapsed;
            elapsed+=dt;
            float flyingDt=Mathf.Max(0f,elapsed-Mathf.Max(previous,BurstDelay));
            bool active=false;
            for(int i=0;i<phasePixels.Count;i++)
            {
                PhasePixel pixel=phasePixels[i];
                pixel.age+=dt;
                phasePixels[i]=pixel;
                if(pixel.age<pixel.delay+pixel.duration)active=true;
            }
            for(int i=0;i<flyingPixels.Count;i++)
            {
                FlyingPixel pixel=flyingPixels[i];
                float aliveDt=Mathf.Min(flyingDt,Mathf.Max(0f,pixel.life));
                pixel.life-=flyingDt;
                if(popProfile&&aliveDt>0f)
                {
                    const float drag=2.4f;
                    float q=Mathf.Exp(-drag*aliveDt),travel=(1f-q)/drag;
                    Vector3 gravity=Vector3.down*.6f;
                    pixel.position+=pixel.velocity*travel+gravity*((aliveDt-travel)/drag);
                    pixel.velocity=pixel.velocity*q+gravity*travel;
                }
                else if(!popProfile&&pixel.life>0f)
                {
                    pixel.velocity*=Mathf.Exp(-3.8f*flyingDt);
                    pixel.velocity+=Vector3.down*18f*flyingDt;
                    pixel.position+=pixel.velocity*flyingDt;
                }
                if(pixel.life>0f)active=true;
                flyingPixels[i]=pixel;
            }
            if(!returning&&previous<BurstDelay&&elapsed>=BurstDelay)
            {
                if(heldSource!=null)heldSource.enabled=false;
                heldSource=null;
                Action burst=onBurst;
                onBurst=null;
                burst?.Invoke();
            }
            if(!active)
            {
                if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
                return;
            }
            RenderPixels();
        }

        private void ConfigureParticles(SpriteRenderer source)
        {
            pixelParticles=gameObject.AddComponent<ParticleSystem>();
            pixelParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main=pixelParticles.main;
            main.loop=false;
            main.playOnAwake=false;
            main.maxParticles=renderedPixels.Length;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            if(popProfile)main.startRotation3D=true;
            ParticleSystem.EmissionModule emission=pixelParticles.emission;
            emission.enabled=false;
            ParticleSystemRenderer renderer=
                pixelParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial=ParticleMaterial;
            if(popProfile)
            {
                PrepareVoxelResources();renderer.renderMode=ParticleSystemRenderMode.Mesh;
                renderer.mesh=voxelMesh;renderer.sharedMaterial=voxelMaterial;
            }
            renderer.sortingLayerID=source.sortingLayerID;
            renderer.sortingOrder=source.sortingOrder+(popProfile?3:0);
            pixelParticles.Play();
        }

        private void RenderPixels()
        {
            if(!returning&&!popProfile&&elapsed<BurstDelay)
            {
                pixelParticles.SetParticles(renderedPixels,0);
                return;
            }
            Camera camera=Camera.main;
            Vector3 cameraForward=camera==null?Vector3.forward:camera.transform.forward;
            int count=0;
            for(int i=0;i<phasePixels.Count;i++)
            {
                PhasePixel pixel=phasePixels[i];
                if(pixel.age>=pixel.delay+pixel.duration)continue;
                float t=Mathf.Clamp01((pixel.age-pixel.delay)/pixel.duration);
                if(returning)
                {
                    float distance=Mathf.Sin(t*Mathf.PI);
                    float visibility=Mathf.Abs(t*2f-1f);
                    Color32 returningColor=pixel.color;
                    returningColor.a=(byte)Mathf.RoundToInt(
                        returningColor.a*Mathf.SmoothStep(0f,1f,visibility));
                    renderedPixels[count++]=MakeParticle(
                        pixel.origin+pixel.scatter*distance-
                        cameraForward*(distance*0.08f),
                        pixel.size*Mathf.Lerp(0.08f,1f,
                            Mathf.SmoothStep(0f,1f,visibility)),
                        returningColor,0f);
                    continue;
                }
                if(popProfile&&elapsed<BurstDelay)
                {
                    float warm=Mathf.Clamp01(elapsed/BurstDelay);
                    Color warmColor=Color.Lerp(pixel.color,Color.white,warm*.7f);
                    warmColor.a=pixel.color.a/255f;
                    renderedPixels[count++]=MakeParticle(pixel.origin,
                        pixel.size*Mathf.Lerp(1f,1.12f,warm),warmColor,i*137.508f);
                    continue;
                }
                // One readable pop: a brief held silhouette, then every pixel releases together.
                float spread=popProfile?(1f-Mathf.Exp(-5f*t))/(1f-Mathf.Exp(-5f)):
                    1f-Mathf.Pow(1f-t,3f);
                Color color=Color.Lerp(pixel.color,Color.white,popProfile?
                    Mathf.Clamp01(1f-(pixel.age-pixel.delay)/.025f):(1f-t)*.45f);
                color.a=pixel.color.a/255f*(popProfile?1f-Mathf.SmoothStep(0f,1f,(t-.55f)/.45f):1f);
                float phaseSize=popProfile?pixel.size*(t<.12f?Mathf.Lerp(1.8f,1.1f,t/.12f):Mathf.Lerp(1.1f,1f,t)):
                    pixel.size*1.01f*(1f-t)*(1f-t);
                renderedPixels[count++]=MakeParticle(
                    burstCenter+(pixel.origin-burstCenter)*(popProfile?
                        Mathf.Lerp(1.12f,1.28f,spread):1f-.06f*(1f-spread))+
                    pixel.scatter*spread-cameraForward*(spread*.08f),
                    phaseSize,color,popProfile?i*137.508f+(pixel.age-pixel.delay)*55f:0f);
            }
            for(int i=0;i<flyingPixels.Count;i++)
            {
                FlyingPixel pixel=flyingPixels[i];
                if(pixel.life<=0f||elapsed<BurstDelay)continue;
                float life01=Mathf.Clamp01(pixel.life/pixel.maxLife);
                Color32 flyingColor=pixel.color;
                if(popProfile)
                {
                    Color tint=Color.Lerp(pixel.color,Color.white,
                        Mathf.Clamp01(1f-(elapsed-BurstDelay)/.025f));
                    tint.a=pixel.color.a/255f*(1f-Mathf.SmoothStep(0f,1f,(1f-life01-.55f)/.45f));flyingColor=tint;
                }
                renderedPixels[count++]=MakeParticle(
                    pixel.position,
                    pixel.size*Mathf.SmoothStep(0.08f,popProfile?1.1f:.72f,life01),
                    flyingColor,pixel.spin*elapsed*20f);
            }
            pixelParticles.SetParticles(renderedPixels,count);
        }

        private ParticleSystem.Particle MakeParticle(
            Vector3 position,float size,Color32 color,float rotation)
        {
            var particle=new ParticleSystem.Particle
            {
                position=position,startSize=size,startColor=color,
                rotation=rotation,startLifetime=1f,remainingLifetime=1f
            };
            if(popProfile)particle.rotation3D=new Vector3(
                Mathf.Repeat(rotation*.63f+18f,360f),Mathf.Repeat(rotation*.37f+31f,360f),Mathf.Repeat(rotation,360f));
            return particle;
        }

        internal static void PrepareVoxelResources()
        {
            if(voxelMesh!=null&&voxelMaterial!=null)return;
            Vector3[] corners={new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),
                new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            int[] faces={4,5,6,7,1,0,3,2,5,1,2,6,0,4,7,3,7,6,2,3,0,1,5,4};
            var vertices=new Vector3[24];var uv=new Vector2[24];var colors=new Color[24];var triangles=new int[36];
            float[] shades={1f,.85f,.65f,.75f,.9f,.55f};
            var texture=new Texture2D(6,1,TextureFormat.RGBA32,false)
                {name="Death Voxel Face Shades",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            for(int face=0;face<6;face++)
            {
                texture.SetPixel(face,0,new Color(shades[face],shades[face],shades[face],1f));
                for(int j=0;j<4;j++)
                {
                    int index=face*4+j;vertices[index]=corners[faces[index]];uv[index]=new Vector2((face+.5f)/6f,.5f);
                    float shade=.88f+shades[face]*.12f;colors[index]=new Color(shade,shade,shade,1f);
                }
                int v=face*4,t=face*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;
                triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            texture.Apply(false,true);
            voxelMesh=new Mesh{name="Death Voxel Unit Cube",hideFlags=HideFlags.HideAndDontSave};
            voxelMesh.vertices=vertices;voxelMesh.uv=uv;voxelMesh.colors=colors;voxelMesh.triangles=triangles;
            voxelMesh.RecalculateBounds();voxelMesh.RecalculateNormals();
            voxelMaterial=new Material(Shader.Find("Particles/Standard Unlit"))
                {name="Death Voxel Powder",hideFlags=HideFlags.HideAndDontSave,mainTexture=texture,color=Color.white,renderQueue=3000};
            voxelMaterial.SetFloat("_Mode",2f);voxelMaterial.SetFloat("_ColorMode",0f);
            voxelMaterial.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            voxelMaterial.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            voxelMaterial.SetInt("_BlendOp",(int)UnityEngine.Rendering.BlendOp.Add);
            voxelMaterial.SetInt("_ZWrite",0);voxelMaterial.SetInt("_Cull",(int)UnityEngine.Rendering.CullMode.Back);
            voxelMaterial.EnableKeyword("_ALPHABLEND_ON");voxelMaterial.SetOverrideTag("RenderType","Transparent");
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.CompilePass(voxelMaterial,0,true);
#endif
        }

        private static Color32[] ReadCurrentFrame(Sprite sprite)
        {
            Rect original=sprite.rect;
            return ReadSpritePixels(sprite.texture,sprite.packed?sprite.textureRect:original,
                original.size,sprite.packed?sprite.textureRectOffset:Vector2.zero);
        }

        private static Color32[] ReadSpritePixels(Texture2D source,Rect frame,Vector2 originalSize,Vector2 offset)
        {
            RenderTexture target=RenderTexture.GetTemporary(
                Grid,Grid,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            target.filterMode=FilterMode.Point;
            RenderTexture previous=RenderTexture.active;
            Texture2D readable=null;
            try
            {
                Graphics.Blit(
                    source,target,
                    new Vector2(frame.width/source.width,frame.height/source.height),
                    new Vector2(frame.x/source.width,frame.y/source.height));
                RenderTexture.active=target;
                readable=new Texture2D(
                    Grid,Grid,TextureFormat.RGBA32,false,false);
                readable.ReadPixels(new Rect(0f,0f,Grid,Grid),0,0,false);
                readable.Apply(false,false);
                Color32[] colors=readable.GetPixels32();
                if(offset==Vector2.zero&&Mathf.Approximately(frame.width,originalSize.x)&&
                   Mathf.Approximately(frame.height,originalSize.y))return colors;
                // Character atlases are unrotated; restore trimmed pixels to the original pose canvas.
                var canvas=new Color32[Grid*Grid];
                for(int y=0;y<Grid;y++)for(int x=0;x<Grid;x++)
                {
                    float localX=(x+.5f)*originalSize.x/Grid-offset.x;
                    float localY=(y+.5f)*originalSize.y/Grid-offset.y;
                    if(localX<0f||localY<0f||localX>=frame.width||localY>=frame.height)continue;
                    int sx=Mathf.Min(Grid-1,Mathf.FloorToInt(localX/frame.width*Grid));
                    int sy=Mathf.Min(Grid-1,Mathf.FloorToInt(localY/frame.height*Grid));
                    canvas[y*Grid+x]=colors[sy*Grid+sx];
                }
                return canvas;
            }
            finally
            {
                RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);
                if(readable!=null)
                {
                    if(Application.isPlaying)Destroy(readable);
                    else DestroyImmediate(readable);
                }
            }
        }

        private static Material ParticleMaterial
        {
            get
            {
                if(particleMaterial!=null)return particleMaterial;
                Texture2D texture=new Texture2D(1,1,TextureFormat.RGBA32,false)
                {
                    name="Sprite Death Pixel",filterMode=FilterMode.Point,
                    hideFlags=HideFlags.HideAndDontSave
                };
                texture.SetPixel(0,0,Color.white);
                texture.Apply(false,true);
                // Match sprite transparency so capture fragments sort above the focus shade.
                Shader shader=Shader.Find("Sprites/Default");
                particleMaterial=new Material(shader)
                {
                    name="Sprite Death Pixel Material",
                    hideFlags=HideFlags.HideAndDontSave,
                    mainTexture=texture
                };
                return particleMaterial;
            }
        }
    }

    private sealed class EnemyRagdollProxy
    {
        private static readonly string[] PhysicsBoneNames=
        {
            "Pelvis","Spine","Head","Thigh_L","Foot_L",
            "Thigh_R","Foot_R","Tail_01","Tail_03"
        };
        private static readonly HashSet<string> UltraLowSdRaptorPhysicsBones=
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Body","Neck","Head",
                "Arm.L","Hand.L","Arm.R","Hand.R",
                "Tail.01","Tail.02","Tail.03",
                "Thigh.L","Shin.L","Foot.L",
                "Thigh.R","Shin.R","Foot.R"
            };
        private static readonly HashSet<string> UltraLowSdFrogPhysicsBones=
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Body","Head",
                "Arm.L","Hand.L","Arm.R","Hand.R",
                "UpperLeg.L","LowerLeg.L","Foot.L",
                "UpperLeg.R","LowerLeg.R","Foot.R"
            };
        private static readonly HashSet<string> UltraLowSdGiraffePhysicsBones=
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Body","Neck","Head",
                "FrontUpper.L","FrontLower.L","FrontHoof.L",
                "FrontUpper.R","FrontLower.R","FrontHoof.R",
                "HindUpper.L","HindLower.L","HindHoof.L",
                "HindUpper.R","HindLower.R","HindHoof.R",
                "TailBase","TailTip"
            };
        private static readonly HashSet<string> UltraLowSdSpiderPhysicsBones=
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Head","Abdomen",
                "Leg1Upper.L","Leg1Lower.L","Leg1Upper.R","Leg1Lower.R",
                "Leg2Upper.L","Leg2Lower.L","Leg2Upper.R","Leg2Lower.R",
                "Leg3Upper.L","Leg3Lower.L","Leg3Upper.R","Leg3Lower.R",
                "Leg4Upper.L","Leg4Lower.L","Leg4Upper.R","Leg4Lower.R"
            };

        private readonly GameObject root;
        private readonly Transform[] poseTransforms;
        private readonly Rigidbody[] bodies;
        private readonly Renderer[] proxyRenderers;
        private readonly Rigidbody headBody;
        private readonly Rigidbody liftBody;
        private readonly Vector3[] bodyResetPositions;
        private readonly Quaternion[] bodyResetRotations;
        private readonly bool[] poseIsRenderer;
        private readonly float modelScale;
        private readonly bool skinnedProxy;
        private readonly bool compactTorsoRig;
        private readonly bool voxelDragonRaptorRig;
        private readonly bool ultraLowSdRaptorRig;
        private readonly bool ultraLowSdFrogRig;
        private readonly bool ultraLowSdGiraffeRig;
        private readonly bool ultraLowSdSpiderRig;
        private Vector3 liftStartPosition;
        private Quaternion liftStartRotation;

        public bool IsValid => root!=null;

        private static bool IsPhysicsBone(Transform candidate)
        {
            if(candidate.GetComponent<Renderer>()!=null)return false;
            string boneName=candidate.name;
            if(UltraLowSdRaptorPhysicsBones.Contains(boneName))return true;
            if(UltraLowSdFrogPhysicsBones.Contains(boneName))return true;
            if(UltraLowSdGiraffePhysicsBones.Contains(boneName))return true;
            if(UltraLowSdSpiderPhysicsBones.Contains(boneName))return true;
            for(int i=0;i<PhysicsBoneNames.Length;i++)
                if(boneName.EndsWith(PhysicsBoneNames[i],StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }

        public EnemyRagdollProxy(Transform templateRoot,Transform poolRoot,int index)
        {
            modelScale=Mathf.Max(0.0001f,Mathf.Abs(templateRoot.lossyScale.x));
            skinnedProxy=
                templateRoot.GetComponentInChildren<SkinnedMeshRenderer>(true)!=null;
            root=UnityEngine.Object.Instantiate(templateRoot.gameObject);
            root.name="Enemy Ragdoll Proxy "+index;
            root.SetActive(false);
            root.transform.SetParent(poolRoot,false);
            root.transform.localScale=Vector3.one;
            Transform[] proxyHierarchy=root.GetComponentsInChildren<Transform>(true);
            bool defBoneRig=proxyHierarchy.Any(item=>
                item.name.StartsWith("DEF_",StringComparison.OrdinalIgnoreCase));
            compactTorsoRig=
                defBoneRig&&
                !proxyHierarchy.Any(item=>item.name.EndsWith("Spine",StringComparison.OrdinalIgnoreCase))&&
                proxyHierarchy.Any(item=>item.name.EndsWith("Chest",StringComparison.OrdinalIgnoreCase));
            voxelDragonRaptorRig=
                proxyHierarchy.Any(item=>item.name=="Pelvis")&&
                proxyHierarchy.Any(item=>item.name=="Chest")&&
                proxyHierarchy.Any(item=>item.name=="Tail.01")&&
                proxyHierarchy.Any(item=>item.name=="Thigh.L")&&
                proxyHierarchy.Any(item=>item.name=="Thigh.R");
            ultraLowSdRaptorRig=
                proxyHierarchy.Any(item=>item.name=="Body")&&
                proxyHierarchy.Any(item=>item.name=="Tail.01")&&
                proxyHierarchy.Any(item=>item.name=="Thigh.L")&&
                proxyHierarchy.Any(item=>item.name=="Thigh.R");
            ultraLowSdFrogRig=
                proxyHierarchy.Any(item=>item.name=="Body")&&
                proxyHierarchy.Any(item=>item.name=="Head")&&
                proxyHierarchy.Any(item=>item.name=="UpperLeg.L")&&
                proxyHierarchy.Any(item=>item.name=="UpperLeg.R");
            ultraLowSdGiraffeRig=
                proxyHierarchy.Any(item=>item.name=="Body")&&
                proxyHierarchy.Any(item=>item.name=="Neck")&&
                proxyHierarchy.Any(item=>item.name=="FrontUpper.L")&&
                proxyHierarchy.Any(item=>item.name=="HindUpper.R");
            ultraLowSdSpiderRig=
                proxyHierarchy.Any(item=>item.name=="Head")&&
                proxyHierarchy.Any(item=>item.name=="Abdomen")&&
                proxyHierarchy.Any(item=>item.name=="Leg1Upper.L")&&
                proxyHierarchy.Any(item=>item.name=="Leg4Upper.R");
            if(!skinnedProxy)
                foreach(Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if(item==root.transform)continue;
                    item.localPosition*=modelScale;
                    if(item.GetComponent<Renderer>()!=null)
                        item.localScale*=modelScale;
                }
            Animator proxyAnimator=root.GetComponent<Animator>();
            if(proxyAnimator!=null)proxyAnimator.enabled=false;

            int deathLayer=LayerMask.NameToLayer("EnemyDeathPart");
            PhysicMaterial material=new PhysicMaterial("Enemy Ragdoll Heavy")
            {
                dynamicFriction=0.95f,
                staticFriction=1f,
                bounciness=0f,
                frictionCombine=PhysicMaterialCombine.Maximum,
                bounceCombine=PhysicMaterialCombine.Minimum
            };
            proxyRenderers=root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer=>renderer!=null&&!renderer.gameObject.name.EndsWith(" [Part Outline]",StringComparison.Ordinal))
                .ToArray();
            foreach(Renderer renderer in proxyRenderers)
            {
                renderer.enabled=false;
                MeshFilter filter=renderer.GetComponent<MeshFilter>();
                if(filter!=null&&filter.sharedMesh!=null)
                {
                    BoxCollider box=renderer.GetComponent<BoxCollider>();
                    if(box==null)box=renderer.gameObject.AddComponent<BoxCollider>();
                    box.center=filter.sharedMesh.bounds.center;
                    box.size=filter.sharedMesh.bounds.size;
                    box.material=material;
                    box.isTrigger=false;
                }
                if(deathLayer>=0)renderer.gameObject.layer=deathLayer;
            }

            Dictionary<Transform,Rigidbody> bodyByBone=new Dictionary<Transform,Rigidbody>();
            List<Rigidbody> bodyList=new List<Rigidbody>();
            foreach(Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                bool compactTorsoBone=compactTorsoRig&&
                    bone.name.StartsWith("DEF_",StringComparison.OrdinalIgnoreCase)&&
                    (bone.name.EndsWith("Chest",StringComparison.OrdinalIgnoreCase)||
                     bone.name.EndsWith("Neck",StringComparison.OrdinalIgnoreCase));
                bool voxelDragonTorsoBone=voxelDragonRaptorRig&&
                    (string.Equals(bone.name,"Pelvis",StringComparison.OrdinalIgnoreCase)||
                     string.Equals(bone.name,"Chest",StringComparison.OrdinalIgnoreCase));
                if(compactTorsoRig&&!bone.name.StartsWith("DEF_",StringComparison.OrdinalIgnoreCase))continue;
                if(!IsPhysicsBone(bone)&&!compactTorsoBone&&
                   !voxelDragonTorsoBone)continue;
                Rigidbody body=bone.gameObject.AddComponent<Rigidbody>();
                body.mass=EnemyBoneMass(bone.name);
                body.drag=0.15f;
                body.angularDrag=0.4f;
                body.interpolation=RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                body.isKinematic=true;
                body.useGravity=false;
                body.detectCollisions=false;
                SphereCollider sphere=bone.gameObject.AddComponent<SphereCollider>();
                float childDistance=float.MaxValue;
                for(int child=0;child<bone.childCount;child++)
                    childDistance=Mathf.Min(childDistance,bone.GetChild(child).localPosition.magnitude);
                if(childDistance==float.MaxValue)childDistance=0.55f;
                sphere.radius=Mathf.Clamp(childDistance*0.28f,0.09f,0.34f);
                sphere.material=material;
                sphere.isTrigger=false;
                if(deathLayer>=0)bone.gameObject.layer=deathLayer;
                bodyByBone.Add(bone,body);
                bodyList.Add(body);
            }

            foreach(KeyValuePair<Transform,Rigidbody> pair in bodyByBone)
            {
                Rigidbody parentBody=FindEnemyParentBody(pair.Key.parent,bodyByBone);
                if(parentBody==null)continue;
                ConfigurableJoint joint=pair.Key.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody=parentBody;
                joint.autoConfigureConnectedAnchor=false;
                joint.anchor=Vector3.zero;
                joint.connectedAnchor=parentBody.transform.InverseTransformPoint(pair.Key.position);
                joint.enableCollision=false;
                joint.enablePreprocessing=true;
                joint.xMotion=ConfigurableJointMotion.Locked;
                joint.yMotion=ConfigurableJointMotion.Locked;
                joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=ConfigurableJointMotion.Limited;
                joint.angularYMotion=ConfigurableJointMotion.Limited;
                joint.angularZMotion=ConfigurableJointMotion.Limited;
                joint.lowAngularXLimit=new SoftJointLimit{limit=-35f};
                joint.highAngularXLimit=new SoftJointLimit{limit=35f};
                joint.angularYLimit=new SoftJointLimit{limit=50f};
                joint.angularZLimit=new SoftJointLimit{limit=50f};
                joint.projectionMode=JointProjectionMode.PositionAndRotation;
                joint.projectionDistance=0.015f;
                joint.projectionAngle=5f;
            }
            bodies=bodyList.ToArray();
            bodyResetPositions=new Vector3[bodies.Length];
            bodyResetRotations=new Quaternion[bodies.Length];
            headBody=bodyByBone.FirstOrDefault(pair=>pair.Key.name.EndsWith("Head",StringComparison.OrdinalIgnoreCase)).Value;
            liftBody=bodyList
                .OrderBy(body=>CaptureLiftBodyPriority(body.name))
                .ThenBy(body=>GetDepth(body.transform))
                .FirstOrDefault();
            poseTransforms=root.GetComponentsInChildren<Transform>(true)
                .Where(item=>item!=root.transform)
                .ToArray();
            poseIsRenderer=poseTransforms.Select(item=>item.GetComponent<Renderer>()!=null).ToArray();
            root.transform.localPosition=new Vector3(index*12f,-1000f,0f);
            root.SetActive(true);
            for(int i=0;i<bodies.Length;i++)
            {
                bodies[i].isKinematic=false;
                bodies[i].useGravity=false;
                bodies[i].detectCollisions=true;
                bodies[i].Sleep();
            }
        }

        public void Activate(Transform sourceRoot,Dictionary<string,Transform> sourceByName,Vector3 impact,Transform poolRoot)
        {
            root.transform.SetParent(poolRoot,false);
            root.transform.SetPositionAndRotation(sourceRoot.position,sourceRoot.rotation);
            // The proxy is authored once from the normal enemy size. Runtime variants
            // (siege enemies, elites, bosses) scale the live model root, so preserve
            // that relative scale when the pooled ragdoll is activated.
            float liveScale=Mathf.Max(0.0001f,Mathf.Abs(sourceRoot.lossyScale.x));
            float relativeScale=liveScale/modelScale;
            Vector3 liveLossyScale=sourceRoot.lossyScale;
            root.transform.localScale=skinnedProxy
                ?new Vector3(
                    Mathf.Abs(liveLossyScale.x),
                    Mathf.Abs(liveLossyScale.y),
                    Mathf.Abs(liveLossyScale.z))
                :Vector3.one*relativeScale;
            for(int i=0;i<poseTransforms.Length;i++)
            {
                Transform target=poseTransforms[i];
                if(!sourceByName.TryGetValue(target.name,out Transform source))continue;
                target.localPosition=skinnedProxy
                    ?source.localPosition
                    :source.localPosition*modelScale;
                target.localRotation=source.localRotation;
                target.localScale=skinnedProxy
                    ?source.localScale
                    :(poseIsRenderer[i]
                        ?source.localScale*modelScale
                        :source.localScale);
            }
            Physics.SyncTransforms();
            SyncBodyPoses();
            for(int i=0;i<proxyRenderers.Length;i++)
            {
                bool sourceEnabled=true;
                if(sourceByName.TryGetValue(proxyRenderers[i].transform.name,out Transform sourceTransform))
                {
                    Renderer sourceRenderer=sourceTransform.GetComponent<Renderer>();
                    if(sourceRenderer!=null)sourceEnabled=sourceRenderer.enabled;
                }
                proxyRenderers[i].enabled=sourceEnabled;
            }
            for(int i=0;i<bodies.Length;i++)
            {
                bodies[i].isKinematic=false;
                bodies[i].detectCollisions=true;
                bodies[i].velocity=Vector3.zero;
                bodies[i].angularVelocity=Vector3.zero;
                bodies[i].useGravity=true;
                bodies[i].WakeUp();
            }
            if(headBody!=null)
            {
                // Compact rigs without a DEF_Spine use Chest/Neck as their torso
                // chain. UltraLowSD rigid-part raptors also have a short Body/Neck
                // chain, so both use a softer impulse to keep the joined body intact.
                bool shortTorsoRig=compactTorsoRig||ultraLowSdRaptorRig||
                    voxelDragonRaptorRig||ultraLowSdFrogRig||
                    ultraLowSdGiraffeRig||ultraLowSdSpiderRig;
                float impactImpulse=shortTorsoRig?4.5f:23.4f;
                float upwardImpulse=shortTorsoRig?0f:1.65f;
                headBody.AddForce(impact*impactImpulse+Vector3.up*upwardImpulse,ForceMode.Impulse);
            }
        }

        public void BeginLift()
        {
            if(liftBody==null)
            {
                liftStartPosition=root.transform.position;
                liftStartRotation=root.transform.rotation;
            }
            else
            {
                liftStartPosition=liftBody.position;
                liftStartRotation=liftBody.rotation;
            }
            for(int i=0;i<bodies.Length;i++)
            {
                bodies[i].velocity=Vector3.zero;
                bodies[i].angularVelocity=Vector3.zero;
                bodies[i].detectCollisions=false;
                bool isLiftAnchor=bodies[i]==liftBody;
                bodies[i].isKinematic=isLiftAnchor;
                bodies[i].useGravity=!isLiftAnchor;
                if(isLiftAnchor)bodies[i].Sleep();
                else bodies[i].WakeUp();
            }
            Physics.SyncTransforms();
        }

        public void SetLiftOffset(Vector3 offset)
        {
            if(liftBody==null)
            {
                root.transform.SetPositionAndRotation(liftStartPosition+offset,liftStartRotation);
            }
            else
            {
                liftBody.position=liftStartPosition+offset;
                liftBody.rotation=liftStartRotation;
            }
            Physics.SyncTransforms();
        }

        private static int CaptureLiftBodyPriority(string bodyName)
        {
            if(string.Equals(bodyName,"Body",StringComparison.OrdinalIgnoreCase))return 0;
            if(bodyName.EndsWith("Pelvis",StringComparison.OrdinalIgnoreCase)||
               bodyName.EndsWith("Hips",StringComparison.OrdinalIgnoreCase))return 1;
            if(bodyName.EndsWith("Chest",StringComparison.OrdinalIgnoreCase)||
               bodyName.EndsWith("Spine",StringComparison.OrdinalIgnoreCase))return 2;
            return 3;
        }

        public void Release(Transform poolRoot)
        {
            if(root==null)return;
            for(int i=0;i<bodies.Length;i++)
            {
                if(!bodies[i].isKinematic)
                {
                    bodies[i].velocity=Vector3.zero;
                    bodies[i].angularVelocity=Vector3.zero;
                }
                bodies[i].useGravity=false;
                bodies[i].detectCollisions=false;
                bodies[i].isKinematic=true;
                bodies[i].Sleep();
            }
            for(int i=0;i<proxyRenderers.Length;i++)proxyRenderers[i].enabled=false;
            if(poolRoot!=null)
            {
                root.transform.SetParent(poolRoot,false);
                root.transform.localPosition=new Vector3(0f,-1000f,0f);
            }
            Physics.SyncTransforms();
            SyncBodyPoses();
            for(int i=0;i<bodies.Length;i++)bodies[i].Sleep();
        }

        public void ApplyExtraGravity()
        {
            Vector3 extraGravity=Physics.gravity*(DeathGravityMultiplier-1f);
            for(int i=0;i<bodies.Length;i++)
                if(bodies[i]!=null&&!bodies[i].isKinematic)
                    bodies[i].AddForce(extraGravity,ForceMode.Acceleration);
        }

        private void SyncBodyPoses()
        {
            for(int i=0;i<bodies.Length;i++)
            {
                bodyResetPositions[i]=bodies[i].transform.position;
                bodyResetRotations[i]=bodies[i].transform.rotation;
            }
            for(int i=0;i<bodies.Length;i++)
            {
                bodies[i].position=bodyResetPositions[i];
                bodies[i].rotation=bodyResetRotations[i];
            }
        }
    }

    private static int GetDepth(Transform item)
    {
        int depth=0;
        for(Transform current=item;current!=null;current=current.parent)depth++;
        return depth;
    }
}

public sealed class VoxelPlayerRagdoll : MonoBehaviour
{
    private static readonly HumanBodyBones[] RagdollBones=
    {
        HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,
        HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,
        HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand
    };

    private Animator animator;
    private Transform[] bones;
    private Vector3[] localPositions;
    private Quaternion[] localRotations;
    private Rigidbody[] bodies;
    private Collider[] colliders;
    private bool initialized;
    private bool ragdolled;
    private PhysicMaterial ragdollMaterial;

    public bool IsRagdolled => ragdolled;

    public void Initialize()
    {
        if(initialized)return;
        animator=GetComponent<Animator>();
        if(animator==null||!animator.isHuman)return;
        ragdollMaterial=new PhysicMaterial("Player Ragdoll High Friction")
        {
            dynamicFriction=0.95f,
            staticFriction=1f,
            bounciness=0f,
            frictionCombine=PhysicMaterialCombine.Maximum,
            bounceCombine=PhysicMaterialCombine.Minimum
        };

        List<Transform> boneList=new List<Transform>();
        List<Rigidbody> bodyList=new List<Rigidbody>();
        List<Collider> colliderList=new List<Collider>();
        Dictionary<Transform,Rigidbody> bodyByBone=new Dictionary<Transform,Rigidbody>();
        foreach(HumanBodyBones boneId in RagdollBones)
        {
            Transform bone=animator.GetBoneTransform(boneId);
            if(bone==null||bodyByBone.ContainsKey(bone))continue;
            Rigidbody body=bone.GetComponent<Rigidbody>();
            if(body==null)body=bone.gameObject.AddComponent<Rigidbody>();
            body.mass=BoneMass(boneId);
            body.drag=0.15f;
            body.angularDrag=0.35f;
            body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic=true;
            body.useGravity=false;

            SphereCollider sphere=bone.GetComponent<SphereCollider>();
            if(sphere==null)sphere=bone.gameObject.AddComponent<SphereCollider>();
            Vector3 worldScale=bone.lossyScale;
            float largestScale=Mathf.Max(Mathf.Abs(worldScale.x),Mathf.Abs(worldScale.y),Mathf.Abs(worldScale.z));
            sphere.radius=BoneRadius(boneId)/Mathf.Max(0.0001f,largestScale);
            sphere.enabled=false;
            sphere.material=ragdollMaterial;
            int actorLayer=LayerMask.NameToLayer("CombatActor");
            if(actorLayer>=0)bone.gameObject.layer=actorLayer;

            boneList.Add(bone);
            bodyList.Add(body);
            colliderList.Add(sphere);
            bodyByBone.Add(bone,body);
        }

        bones=boneList.ToArray();
        bodies=bodyList.ToArray();
        colliders=colliderList.ToArray();
        localPositions=bones.Select(b=>b.localPosition).ToArray();
        localRotations=bones.Select(b=>b.localRotation).ToArray();
        for(int i=0;i<colliders.Length-1;i++)
        for(int j=i+1;j<colliders.Length;j++)
            Physics.IgnoreCollision(colliders[i],colliders[j],true);

        for(int i=0;i<bones.Length;i++)
        {
            Rigidbody parentBody=FindParentBody(bones[i].parent,bodyByBone);
            if(parentBody==null)continue;
            CharacterJoint joint=bones[i].GetComponent<CharacterJoint>();
            if(joint==null)joint=bones[i].gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody=parentBody;
            joint.autoConfigureConnectedAnchor=false;
            joint.anchor=Vector3.zero;
            joint.connectedAnchor=parentBody.transform.InverseTransformPoint(bones[i].position);
            joint.enableCollision=false;
            joint.enablePreprocessing=true;
            joint.lowTwistLimit=new SoftJointLimit{limit=-35f};
            joint.highTwistLimit=new SoftJointLimit{limit=35f};
            joint.swing1Limit=new SoftJointLimit{limit=50f};
            joint.swing2Limit=new SoftJointLimit{limit=50f};
        }
        initialized=true;
    }

    public void ResetRagdoll()
    {
        Initialize();
        if(!initialized)return;
        for(int i=0;i<bodies.Length;i++)
        {
            if(!bodies[i].isKinematic)
            {
                bodies[i].velocity=Vector3.zero;
                bodies[i].angularVelocity=Vector3.zero;
            }
            bodies[i].isKinematic=true;
            bodies[i].useGravity=false;
            colliders[i].enabled=false;
            bones[i].localPosition=localPositions[i];
            bones[i].localRotation=localRotations[i];
        }
        ragdolled=false;
        animator.enabled=true;
        animator.speed=1f;
        animator.Rebind();
        animator.Update(0f);
    }

    public void PlayDeath(Vector3 knockbackDirection)
    {
        Initialize();
        if(!initialized||ragdolled)return;
        ragdolled=true;
        animator.speed=0f;
        animator.enabled=false;
        knockbackDirection.y=0f;
        if(knockbackDirection.sqrMagnitude<0.001f)knockbackDirection=-transform.forward;
        knockbackDirection.Normalize();
        for(int i=0;i<bodies.Length;i++)
        {
            colliders[i].enabled=true;
            bodies[i].isKinematic=false;
            bodies[i].useGravity=true;
            bodies[i].velocity=Vector3.zero;
            bodies[i].angularVelocity=Vector3.zero;
        }
        Rigidbody head=animator.GetBoneTransform(HumanBodyBones.Head)?.GetComponent<Rigidbody>();
        if(head==null)head=animator.GetBoneTransform(HumanBodyBones.Chest)?.GetComponent<Rigidbody>();
        if(head!=null)
        {
            head.AddForce(knockbackDirection*4.2f+Vector3.up*0.9f,ForceMode.Impulse);
            head.AddTorque(Vector3.Cross(Vector3.up,knockbackDirection)*0.8f,ForceMode.Impulse);
        }
    }

    private static Rigidbody FindParentBody(Transform parent,Dictionary<Transform,Rigidbody> bodies)
    {
        for(Transform current=parent;current!=null;current=current.parent)
            if(bodies.TryGetValue(current,out Rigidbody body))return body;
        return null;
    }

    private static float BoneMass(HumanBodyBones bone)
    {
        if(bone==HumanBodyBones.Hips||bone==HumanBodyBones.Chest)return 2.2f;
        if(bone==HumanBodyBones.Spine||bone==HumanBodyBones.Head)return 1.5f;
        return 0.75f;
    }

    private static float BoneRadius(HumanBodyBones bone)
    {
        if(bone==HumanBodyBones.Hips||bone==HumanBodyBones.Chest)return 0.085f;
        if(bone==HumanBodyBones.Spine||bone==HumanBodyBones.Head)return 0.075f;
        if(bone==HumanBodyBones.LeftUpperLeg||bone==HumanBodyBones.RightUpperLeg)return 0.06f;
        return 0.045f;
    }
}

public sealed class VoxelPlayerDamageReaction : MonoBehaviour
{
    private const float FlashDuration=0.23f;
    private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty=Shader.PropertyToID("_Color");
    private Renderer[] renderers;
    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock scratchBlock;
    private float timer;

    public void Initialize()
    {
        renderers=GetComponentsInChildren<Renderer>(true);
        originalBlocks=new MaterialPropertyBlock[renderers.Length];
        for(int i=0;i<renderers.Length;i++)
        {
            originalBlocks[i]=new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(originalBlocks[i]);
        }
    }

    public void PlayHit()
    {
        if(renderers==null)Initialize();
        timer=FlashDuration;
    }

    private void LateUpdate()
    {
        if(timer<=0f)return;
        timer=Mathf.Max(0f,timer-Time.deltaTime);
        float normalized=1f-timer/FlashDuration;
        float amount=Mathf.Sin(normalized*Mathf.PI);
        Color flash=Color.Lerp(Color.white,new Color(1f,0.12f,0.08f),amount*0.52f*CombatFlashPresentation.AccentScale);
        for(int i=0;i<renderers.Length;i++)
        {
            scratchBlock??=new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(scratchBlock);
            scratchBlock.SetColor(BaseColor,flash);
            scratchBlock.SetColor(ColorProperty,flash);
            renderers[i].SetPropertyBlock(scratchBlock);
        }
        if(timer>0f)return;
        for(int i=0;i<renderers.Length;i++)renderers[i].SetPropertyBlock(originalBlocks[i]);
    }
}
