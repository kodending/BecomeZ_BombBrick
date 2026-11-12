using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(10000)]
public sealed class SpriteMonsterAnimationDriver : MonoBehaviour
{
    public const string MovingParameter="Moving";
    public const string StrugglingParameter="Struggling";
    public const string AttackParameter="Attack";
    public const string SkillAttackParameter="SkillAttack";
    public const string DeathParameter="Death";
    public const string CaptureParameter="Capture";
    [Header("Profile Frames")]
    [SerializeField] private Sprite[] idleFrames=Array.Empty<Sprite>();
    [SerializeField] private Sprite[] runFrames=Array.Empty<Sprite>();
    [SerializeField] private Sprite[] attackFrames=Array.Empty<Sprite>();
    [SerializeField] private Sprite[] skillAttackFrames=Array.Empty<Sprite>();
    [SerializeField] private Sprite[] deathFrames=Array.Empty<Sprite>();
    [SerializeField] private Sprite[] captureFrames=Array.Empty<Sprite>();
    [SerializeField] private Sprite[] struggleFrames=Array.Empty<Sprite>();
    [SerializeField,Min(1f)] private float idleFramesPerSecond=7f;
    [SerializeField,Min(1f)] private float runFramesPerSecond=11f;
    [SerializeField,Min(1f)] private float attackFramesPerSecond=12f;
    [SerializeField,Min(1f)] private float skillAttackFramesPerSecond=11f;
    [SerializeField,Min(1f)] private float deathFramesPerSecond=8f;
    [SerializeField,Min(1f)] private float captureFramesPerSecond=12f;
    [SerializeField,Min(1f)] private float struggleFramesPerSecond=12f;
    [SerializeField,Min(0)] private int basicAttackHitFrameIndex=3;
    [SerializeField,Min(0)] private int skillAttackHitFrameIndex=3;
    [SerializeField] private AnimationClip skillWindupClip;
    [SerializeField] private AnimationClip skillImpactClip;
    [SerializeField] private AnimationClip knockdownClip;
    [SerializeField] private Animator specialClipAnimator;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private SpriteMonsterBillboard billboard;
    private bool moving;
    private bool struggling;
    private bool oneShot;
    private bool terminal;
    private bool frozenFrame;
    private bool statusFrozen;
    public bool IsSkillAnticipating { get; private set; }
    private Sprite statusFrozenSprite;
    private Vector3 statusFrozenPosition,statusFrozenScale;
    private string requestedMotion="Idle";
    private bool griffinWingAttack;
    private float motionElapsed;
    private float motionPlaybackSpeed=1f;
    private float globalSpeedMultiplier=1f;
    private float combatPlaybackSpeed=1f;
    public float CombatPlaybackSpeed=>combatPlaybackSpeed;
    // Basic attacks already derive their pose speed from the paced attack interval.
    private float CombatMotionRate=>requestedMotion=="Attack"||struggling&&struggleClock!=null?1f:combatPlaybackSpeed;
    private bool reverseSkillAttack;
    private bool deathRollFrame;
    private int currentFrameIndex;
    private Action pendingBasicAttackHit;
    private bool basicAttackHitDispatched;
    private Action pendingSkillAttackHit;
    private bool skillAttackHitDispatched;
    private float skillAttackRecoveryPlaybackSpeed=1f;
    private int skillAttackHoldFrameIndex=-1;
    private float skillAttackHoldRemaining;
    private Func<float> skillAttackClock;
    private Func<float> struggleClock;
    private float struggleClockTime;
    private float skillAttackClockTime;
    private Vector3 skillAttackHoldPosition;
    private float knockdownRemaining;
    private bool animatorSuppressed;
    private Sprite pinnedFrameSprite;
    private bool skillImpactClipActive;
    private Vector3 skillPoseScale=Vector3.one;
    private bool earthPulseAttack;
    private float earthPulseElapsed;
    private int earthPulseStrike;
    private Action<int> earthPulseFollowupHit;
    private Action<int> earthPulseDownstroke;
    private bool worldOverturnAttack;
    private bool bearSpiritAttack;
    private bool bearAvatarRoar;
    private bool wolfHuntAttack;
    private bool tyrannoStompAttack;
    private float tyrannoStompElapsed;
    private float tyrannoStompWindup=TyrannoCombatRules.TremorCastSeconds;
    private bool tyrannoExtinction;
    private bool tyrannoMarchAttack;
    private float tyrannoMarchElapsed;
    private Func<bool> tyrannoMarchValid;
    private Action<float> tyrannoMarchAdvance;
    private Action tyrannoMarchFinished;
    public bool TyrannoMarchActive=>tyrannoMarchAttack;
    private bool mantisGuillotineAttack;
    private bool mantisReaperAction;
    private int mantisReaperMode;
    private float mantisReaperTime;
    private Action mantisReaperCancelled;
    private float mantisGuillotineElapsed;
    private Func<bool> mantisGuillotineValid;
    private Action<float> mantisGuillotineAdvance;
    private Action mantisGuillotineFinished;
    private bool tyrannoMawAttack;
    private float tyrannoMawElapsed;
    private Func<bool> tyrannoMawValid;
    private Action<float> tyrannoMawAdvance;
    private Action tyrannoMawFinished;
    public bool TyrannoMawActive=>tyrannoMawAttack;
    private bool ninjaShadowAttack,ninjaHidden,ninjaPreviousRenderingOff;
    private float ninjaShadowElapsed;
    private Func<bool> ninjaShadowValid;
    private Action ninjaVanish,ninjaSubstitute,ninjaArrive;
    private bool ninjaCrossAttack;
    private bool ninjaEightGateAttack;
    private bool ninjaCounterAttack;
    private Action ninjaCounterFinished;
    private Action<float,float> ninjaGateAdvance;
    private Action ninjaGateFinished;
    private int ninjaCrossBeat;
    private Action<int> ninjaCrossMove,ninjaCrossHit,ninjaCrossDepart;
    private Action ninjaCrossFinished;
    private int ninjaChainStrikeCount=4;
    private bool ninjaChainSheathe;
    private Vector3 ninjaMoonApproachOffset;
    private float NinjaChainInterval=>ninjaChainSheathe?NinjaCatRank3Rules.BlackMoonStepInterval:NinjaCatRank3Rules.CrossStepInterval;
    private float NinjaChainFirstHit=>ninjaChainSheathe?NinjaCatCombatRules.ArrivalTime+NinjaCatRank3Rules.BlackMoonContactDelay:NinjaCatRank3Rules.CrossFirstHitTime;
    private float NinjaChainFinalArrival=>NinjaCatCombatRules.ArrivalTime+ninjaChainStrikeCount*NinjaChainInterval;
    private float NinjaChainFinalHit=>NinjaChainFinalArrival+(ninjaChainSheathe?NinjaCatRank3Rules.BlackMoonSheatheDuration:.1f);
    private float NinjaChainEnd=>NinjaChainFinalHit+.15f;
    private bool mantisCrossAttack;
    private float mantisCrossElapsed;
    private float mantisCrossInterval=MantisCombatRules.WaveInterval;
    public bool MantisCrossAttackActive=>mantisCrossAttack;
    public float MantisCrossTime=>mantisCrossElapsed;
    private bool bearRagingAttack;
    private float bearRagingElapsed,bearRagingWindup;
    private int bearRagingNextStrike;
    private Func<int,bool> bearRagingHit;
    private ParticleSystem worldOverturnCharge;
    private float EarthPulseContactTime => worldOverturnAttack
        ?KingKongCombatRules.WorldOverturnWindupDuration+0.08f
        :earthPulseStrike==2?0.37f:earthPulseStrike==1?0.22f:0.28f;
    private Vector3 impactRecoilOffset;
    private Vector3 appliedRecoilOffset;
    private float impactRecoilRemaining;
    private Vector3 contactShakeOffset;
    private float contactShakeRemaining;
    private const float ContactReactionDuration=.12f;
    private float contactCompressionAmount;
    private int contactReactionPriority;
    private Vector3 contactBaseScale,contactAppliedScale;
    private bool contactScaleApplied;
    private bool readabilityContactActive;
    private bool readabilityBasicPoseActive,readabilityBasicPoseContactShown,readabilityBasicPoseScaleApplied;
    private int readabilityBasicPoseStyle,readabilityBasicPoseContactFrame=-1;
    private Vector3 readabilityBasicPoseDirection,readabilityBasicPoseBaseScale,readabilityBasicPoseAppliedScale;
    private Transform readabilityBasicPoseOrigin,readabilityBasicPoseTarget;
    private Sprite impactHeldSprite;
    private float impactHoldRemaining,impactReactionCooldown;
    private int impactReactionStartedFrame=-1;
    private float monkeyThrowRemaining,monkeyThrowDuration;
    private bool monkeyFinalThrow;
    private Vector3 raptorStrikeOffset;
    private float raptorStrikeRemaining,raptorStrikeDuration;
    private int raptorStrikeStartedFrame=-1;
    private bool beetleSweepAttack;
    public bool BeetleSweepAttacking=>beetleSweepAttack&&oneShot&&requestedMotion=="Attack"&&!terminal;
    public float BeetleSweepPhase=>(motionElapsed-BasicAttackHitTime+.12f)/.24f;
    private bool beetleFlipAttack,beetleCrushAttack;
    private float beetleOverturnTime=-1f;
    private Sprite[] imperialHornFrames;
    private Vector3 imperialHornPosition,imperialHornScale,imperialHornBasePosition,imperialHornBaseScale;
    private Vector3 imperialHornFoot;
    private float imperialHornHeight;
    private float beetleFlipElapsed;
    private float beetleFlipTimeScale=1f;
    private Func<bool> beetleFlipValid;
    private Action<float> beetleFlipAdvance;
    private Action beetleFlipFinished;
    private bool mammothChargeAttack,mammothTossAttack,mammothWallRushAttack,mammothBraceAttack,mammothExpulsionAttack;
    private float mammothBraceImpactAt=-10f;
    private float mammothChargeElapsed;
    private Func<bool> mammothChargeValid;
    private Action<float> mammothChargeAdvance;
    private Action mammothChargeFinished;

    public int IdleFrameCount => idleFrames!=null?idleFrames.Length:0;
    public int RunFrameCount => runFrames!=null?runFrames.Length:0;
    public int AttackFrameCount => attackFrames!=null?attackFrames.Length:0;
    public int SkillAttackFrameCount =>
        skillAttackFrames!=null?skillAttackFrames.Length:0;
    public int DeathFrameCount => deathFrames!=null?deathFrames.Length:0;
    public int CaptureFrameCount => captureFrames!=null?captureFrames.Length:0;
    public int StruggleFrameCount =>
        struggleFrames!=null?struggleFrames.Length:0;
    public string CurrentMotionName => knockdownRemaining>0f
        ?"Death"
        :skillAttackHoldFrameIndex>=0?"SkillAttack":requestedMotion;
    public int CurrentFrameIndex => currentFrameIndex;
    public float CurrentPlaybackSpeed => motionPlaybackSpeed;
    public bool BearAvatarRoarCharging => bearAvatarRoar&&skillAttackHoldFrameIndex>=0&&
        skillAttackHoldFrameIndex<skillAttackHitFrameIndex;
    public bool BearAvatarRoarImpact => bearAvatarRoar&&skillAttackHoldFrameIndex==skillAttackHitFrameIndex;
    public float GlobalSpeedMultiplier => globalSpeedMultiplier;
    public float TyrannoStompProgress => tyrannoMawAttack?Mathf.Clamp01(tyrannoMawElapsed/TyrannoRank3Rules.MawWindup):
        tyrannoStompAttack?Mathf.Clamp01(tyrannoStompElapsed/tyrannoStompWindup):0f;
    public float BasicAttackDuration => MotionDuration("Attack");
    public float BasicAttackWindupProgress=>Mathf.Clamp01(motionElapsed/Mathf.Max(.001f,BasicAttackHitTime));
    public float BasicAttackAnimationTime=>motionElapsed;
    public float SkillAttackDuration => MotionDuration("SkillAttack");
    public float BasicAttackHitTime =>
        MotionFrameTime("Attack",basicAttackHitFrameIndex);
    public float SkillAttackHitTime =>
        MotionFrameTime("SkillAttack",skillAttackHitFrameIndex);
    public int SkillAttackHitFrameIndex => skillAttackHitFrameIndex;
    public float SkillAttackAuthoredScale=>AuthoredScaleFor("SkillAttack");
    public Sprite GetSkillAttackFrame(int frameIndex)=>
        skillAttackFrames==null||skillAttackFrames.Length==0
            ?null:skillAttackFrames[Mathf.Clamp(frameIndex,0,skillAttackFrames.Length-1)];
    public float WolfCompanionScale(bool striking,bool moving=false)=>
        AuthoredScaleFor(striking?"Attack":moving?"Run":"Idle");
    public AnimationClip SkillWindupClip => skillWindupClip;
    public AnimationClip SkillImpactClip => skillImpactClip;
    public AnimationClip KnockdownClip => knockdownClip;
    public Animator SpecialClipAnimator => specialClipAnimator;
    public bool HasPendingBasicAttackHit =>
        pendingBasicAttackHit!=null&&!basicAttackHitDispatched;
    public bool IsBearRagingAttackActive => bearRagingAttack;
    public bool HasPendingSkillAttackHit =>
        pendingSkillAttackHit!=null&&!skillAttackHitDispatched;
    public bool IsSkillActionActive => !terminal&&!statusFrozen&&knockdownRemaining<=0f&&
        (requestedMotion=="SkillAttack"&&(oneShot||skillAttackHoldFrameIndex>=0)||
         mantisReaperAction||beetleOverturnTime>=0f);
    public bool IsActionLocked => statusFrozen||oneShot||skillAttackHoldFrameIndex>=0||
                                  knockdownRemaining>0f;
    public string CurrentFrameName =>
        spriteRenderer!=null&&spriteRenderer.sprite!=null
            ?spriteRenderer.sprite.name
            :string.Empty;
    public bool LocksHorizontalFacing =>
        statusFrozen||
        CurrentMotionName=="Attack"||
        CurrentMotionName=="SkillAttack"||
        CurrentMotionName=="Death"||
        CurrentMotionName=="Capture"||
        CurrentMotionName=="Struggle";

    private void Awake()
    {
        spriteRenderer=GetComponent<SpriteRenderer>();
        ResolveAnimator();
    }

    private void OnEnable()
    {
        if(spriteRenderer==null)spriteRenderer=GetComponent<SpriteRenderer>();
        ResolveAnimator();
        if(Application.isPlaying)ResetForReuse();
    }

    private void Update()
    {
        if(statusFrozen||IsSkillAnticipating||CombatSkillClock.ActorFrozen(this))return;
        using(CombatSkillClock.ActorScope(this))
        {
            float dt=CombatSkillClock.ActorDelta(this);
            if(CombatSkillClock.Active&&dt<=0f)return;
            ClearAppliedRecoil();
            impactRecoilRemaining=Mathf.Max(0f,impactRecoilRemaining-dt);
            Advance(dt);
        }
    }

    public void SetSkillPoseScale(Vector3 scale)
    {
        skillPoseScale=scale;
    }

    public void PlayImpactRecoil(Vector3 worldDirection,float distance)
    {
        if(terminal||frozenFrame||statusFrozen)return;
        // Visual-only recoil: never move the collision body or its target position.
        Vector3 right=transform.right;
        Vector3 offset=right*Mathf.Sign(Vector3.Dot(worldDirection,right))*distance;
        offset=transform.parent==null?offset:
            transform.parent.InverseTransformVector(offset);
        if(impactRecoilRemaining>.1f&&impactRecoilOffset.sqrMagnitude>=offset.sqrMagnitude)return;
        impactRecoilOffset=offset;
        impactRecoilRemaining=0.14f;
    }

    public void PlayContactReaction(Vector3 worldDirection,bool strong)
        =>PlayContactReactionCore(worldDirection,strong,0);

    public void PlayBasicContactReaction(Vector3 worldDirection,bool strong)
        =>PlayContactReactionCore(worldDirection,strong,CombatReadabilityPresentation.RefinedSkills?1:0);

    public void PlaySkillContactReaction(Vector3 worldDirection,bool strong)
        =>PlayContactReactionCore(worldDirection,strong,
            CombatReadabilityPresentation.Local?2:0);

    private void PlayContactReactionCore(Vector3 worldDirection,bool strong,int priority)
    {
        if(CombatReadabilityPresentation.Active&&!CombatReadabilityPresentation.ContactFeedback)return;
        if(terminal||frozenFrame||statusFrozen||knockdownRemaining>0f||
           (impactReactionCooldown>0f&&priority<=contactReactionPriority)||
           (priority>0&&IsSkillAnticipating))return;
        bool basic=priority==1,skill=priority==2;
        contactReactionPriority=priority;
        readabilityContactActive=priority>0;
        impactReactionCooldown=basic?.085f:ContactReactionDuration;
        impactReactionStartedFrame=Time.frameCount;
        Vector3 offset=Vector3.ProjectOnPlane(worldDirection,transform.forward);
        if(offset.sqrMagnitude<.0001f)offset=transform.right;
        offset=offset.normalized*(skill?(strong?.22f:.18f):basic?(strong?.18f:.13f):strong?.14f:.1f);
        if(priority==0&&CombatReadabilityPresentation.Adjusted)offset*=CombatReadabilityPresentation.ContactIntensity;
        contactShakeOffset=transform.parent==null?offset:transform.parent.InverseTransformVector(offset);
        contactShakeRemaining=ContactReactionDuration;
        contactCompressionAmount=skill?(strong?.075f:.055f):basic?(strong?.065f:.045f):strong?.04f:.025f;
        if(priority==0&&CombatReadabilityPresentation.Adjusted)contactCompressionAmount*=CombatReadabilityPresentation.ContactIntensity;
        if((priority==0&&!strong)||spriteRenderer==null)return;
        // Only retain the drawn frame. Attack clocks and their callbacks keep advancing.
        impactHeldSprite=spriteRenderer.sprite;
        impactHoldRemaining=skill?(strong?.035f:.03f):basic?(strong?.035f:.025f):.04f;
    }

    public void PlayMonkeyThrowFollowThrough(float duration,bool final,Vector3 direction)
    {
        if(terminal||statusFrozen||requestedMotion!="SkillAttack")return;
        monkeyThrowDuration=monkeyThrowRemaining=Mathf.Max(.08f,duration);
        monkeyFinalThrow=final;
        oneShot=true;
        motionElapsed=MotionFrameTime("SkillAttack",skillAttackHitFrameIndex);
        ApplyExplicitFrame();
        PlayImpactRecoil(-direction,final?.1f:.045f);
    }

    public void PlayRaptorRelentlessStrike(Vector3 towardVictim,int strike)
    {
        if(terminal||frozenFrame||statusFrozen||knockdownRemaining>0f||requestedMotion!="SkillAttack")return;
        ClearAppliedRecoil();
        Vector3 offset=Vector3.ProjectOnPlane(towardVictim,transform.forward);
        if(offset.sqrMagnitude<.0001f)
            offset=transform.right*(spriteRenderer!=null&&spriteRenderer.flipX?-1f:1f);
        offset=offset.normalized*(strike>=2?.3f:.18f);
        raptorStrikeOffset=transform.parent==null?offset:transform.parent.InverseTransformVector(offset);
        raptorStrikeDuration=raptorStrikeRemaining=strike>=2?.16f:.12f;
        raptorStrikeStartedFrame=Time.frameCount;
    }

    private void ClearAppliedRecoil()
    {
        // Strip the later billboard layer before restoring or capturing an absolute pose anchor.
        if(billboard==null)billboard=GetComponent<SpriteMonsterBillboard>();
        billboard?.ClearRollOffset();
        transform.localPosition-=appliedRecoilOffset;
        appliedRecoilOffset=Vector3.zero;
        ClearContactCompression();
        ClearReadabilityBasicPoseScale();
    }

    public void BeginReadabilityBasicAttackPose(Vector3 worldDirection,int style)=>
        TryBeginReadabilityBasicAttackPose(worldDirection,style);

    private bool TryBeginReadabilityBasicAttackPose(Vector3 worldDirection,int style)
    {
        if(!CombatReadabilityPresentation.RefinedSkills||style<0||style>6||terminal||frozenFrame||statusFrozen||
           IsSkillAnticipating||CurrentMotionName!="Attack"||!HasPendingBasicAttackHit||
           bearRagingAttack||BeetleSweepAttacking||griffinWingAttack||mantisReaperAction)return false;
        ClearAppliedRecoil();
        readabilityBasicPoseOrigin=readabilityBasicPoseTarget=null;
        readabilityBasicPoseDirection=Vector3.ProjectOnPlane(worldDirection,transform.forward);
        if(readabilityBasicPoseDirection.sqrMagnitude<.0001f)
            readabilityBasicPoseDirection=transform.right*(spriteRenderer!=null&&spriteRenderer.flipX?-1f:1f);
        readabilityBasicPoseDirection.Normalize();readabilityBasicPoseStyle=style;
        readabilityBasicPoseActive=true;readabilityBasicPoseContactShown=false;readabilityBasicPoseContactFrame=-1;
        return true;
    }

    public void BeginReadabilityTargetAttackPose(Transform origin,Transform target,int style)
    {
        if(origin==null||target==null||style!=0&&style!=1&&style!=3&&style!=4)return;
        if(!TryBeginReadabilityBasicAttackPose(target.position-origin.position,style))return;
        readabilityBasicPoseOrigin=origin;readabilityBasicPoseTarget=target;
        UpdateReadabilityTargetAttackDirection(target.position-origin.position);
    }

    private Quaternion UpdateReadabilityTargetAttackDirection(Vector3 toward)
    {
        if(billboard==null)billboard=GetComponent<SpriteMonsterBillboard>();
        // The parent may have turned this frame before the billboard restores its camera-facing rotation.
        Quaternion view=billboard!=null?billboard.ViewRotation:transform.rotation;
        Vector3 direction=Vector3.ProjectOnPlane(toward,view*Vector3.forward);
        readabilityBasicPoseDirection=direction.sqrMagnitude>.0001f?direction.normalized:
            view*Vector3.right*(spriteRenderer!=null&&spriteRenderer.flipX?-1f:1f);
        billboard?.SetFacingDirection(toward,true);
        return view;
    }

    private void ClearReadabilityBasicPoseScale()
    {
        if(!readabilityBasicPoseScaleApplied)return;
        if(transform.localScale==readabilityBasicPoseAppliedScale)transform.localScale=readabilityBasicPoseBaseScale;
        readabilityBasicPoseScaleApplied=false;
    }

    private void ClearReadabilityBasicAttackPose()
    {
        ClearReadabilityBasicPoseScale();
        readabilityBasicPoseActive=false;readabilityBasicPoseContactShown=false;readabilityBasicPoseContactFrame=-1;
        readabilityBasicPoseOrigin=readabilityBasicPoseTarget=null;
    }

    private void SampleReadabilityBasicAttackPose(out Vector3 offset,out Vector3 scale)
    {
        offset=Vector3.zero;scale=Vector3.one;
        if(!readabilityBasicPoseActive)return;
        bool contactFrame=readabilityBasicPoseContactShown&&readabilityBasicPoseContactFrame==Time.frameCount;
        if(!CombatReadabilityPresentation.Local||
           terminal||frozenFrame||IsSkillAnticipating||knockdownRemaining>0f||
           (requestedMotion!="Attack"&&!contactFrame)||(!HasPendingBasicAttackHit&&!basicAttackHitDispatched))
        {readabilityBasicPoseActive=false;readabilityBasicPoseOrigin=readabilityBasicPoseTarget=null;return;}
        bool directed=readabilityBasicPoseOrigin!=null&&readabilityBasicPoseTarget!=null;
        Vector3 toward=directed?readabilityBasicPoseTarget.position-readabilityBasicPoseOrigin.position:Vector3.zero;
        Quaternion view=directed?UpdateReadabilityTargetAttackDirection(toward):transform.rotation;
        int style=readabilityBasicPoseStyle;
        float hit=BasicAttackHitTime,lead=Mathf.Min(.06f,hit*.35f),start=hit-lead;
        float pull,thrust,recovery;Vector3 windup,impact;
        switch(style)
        {
            case 0:pull=-.04f;thrust=.16f;recovery=.14f;
                windup=new Vector3(.96f,1.03f,1f);impact=new Vector3(1.08f,.95f,1f);break;
            case 1:pull=-.06f;thrust=.12f;recovery=.2f;
                windup=new Vector3(1.08f,.9f,1f);impact=new Vector3(1.12f,.94f,1f);break;
            case 2:pull=.018f;thrust=-.055f;recovery=.12f;
                windup=new Vector3(1.025f,.98f,1f);impact=new Vector3(.96f,1.04f,1f);break;
            case 3:pull=-.025f;thrust=.1f;recovery=.11f;
                windup=new Vector3(.98f,1.02f,1f);impact=new Vector3(1.06f,.96f,1f);break;
            case 4:pull=-.06f;thrust=.14f;recovery=.18f;
                windup=new Vector3(1.07f,.93f,1f);impact=new Vector3(1.12f,.93f,1f);break;
            case 5:pull=-.03f;thrust=.075f;recovery=.14f;
                windup=new Vector3(.98f,1.02f,1f);impact=new Vector3(1.04f,.98f,1f);break;
            default:pull=-.02f;thrust=.035f;recovery=.12f;
                windup=new Vector3(1.04f,.95f,1f);impact=new Vector3(1.04f,.97f,1f);break;
        }
        if(directed)
        {
            thrust*=1.45f;
            windup=new Vector3(Mathf.Min(windup.x,.96f),Mathf.Max(windup.y,1.03f),1f);
            impact=new Vector3(Mathf.Max(impact.x,1.10f),Mathf.Min(impact.y,.95f),1f);
            thrust=Mathf.Min(thrust,toward.magnitude*.16f);
        }
        if(requestedMotion=="Attack"&&motionElapsed>=hit&&!readabilityBasicPoseContactShown)
        {
            readabilityBasicPoseContactShown=true;readabilityBasicPoseContactFrame=Time.frameCount;
            contactFrame=true;
        }
        float distance;
        if(contactFrame){distance=thrust;scale=impact;}
        else if(motionElapsed<start)
        {
            float t=Mathf.SmoothStep(0f,1f,motionElapsed/Mathf.Max(.001f,start));
            distance=pull*t;scale=Vector3.Lerp(Vector3.one,windup,t);
        }
        else if(motionElapsed<hit)
        {
            float t=Mathf.SmoothStep(0f,1f,(motionElapsed-start)/Mathf.Max(.001f,lead));
            distance=Mathf.Lerp(pull,thrust,t);scale=Vector3.Lerp(windup,impact,t);
        }
        else
        {
            float t=Mathf.SmoothStep(0f,1f,(motionElapsed-hit)/recovery);
            distance=Mathf.Lerp(thrust,0f,t);scale=Vector3.Lerp(impact,Vector3.one,t);
            if(motionElapsed>=hit+recovery)readabilityBasicPoseActive=false;
        }
        Vector3 world=readabilityBasicPoseDirection*distance;
        offset=transform.parent==null?world:transform.parent.InverseTransformVector(world);
        if(directed)
        {
            Vector3 axis=Quaternion.Inverse(view)*readabilityBasicPoseDirection;
            float horizontal=axis.x*axis.x/Mathf.Max(.0001f,axis.x*axis.x+axis.y*axis.y);
            scale=new Vector3(Mathf.Lerp(scale.y,scale.x,horizontal),Mathf.Lerp(scale.x,scale.y,horizontal),scale.z);
            if(!readabilityBasicPoseActive)readabilityBasicPoseOrigin=readabilityBasicPoseTarget=null;
        }
    }

    private void ClearContactCompression()
    {
        if(!contactScaleApplied)return;
        // An authored action may already have replaced this visual-only scale.
        if(transform.localScale==contactAppliedScale)transform.localScale=contactBaseScale;
        contactScaleApplied=false;
    }

    private void OnDisable()
    {
        SetSkillAnticipating(false);
        SetSkillFocus(0f);
        if(beetleOverturnTime>=0f||beetleFlipAttack||mammothChargeAttack)CancelPendingSkillAttack();
        if(bearRagingAttack)CancelPendingBasicAttack();
        if(mantisReaperAction||mantisGuillotineAttack||mantisCrossAttack||worldOverturnAttack||bearSpiritAttack||wolfHuntAttack||ninjaShadowAttack||tyrannoStompAttack||tyrannoMarchAttack||tyrannoMawAttack)CancelPendingSkillAttack();
        ClearAppliedRecoil();
        impactRecoilRemaining=contactShakeRemaining=raptorStrikeRemaining=0f;
        impactHoldRemaining=impactReactionCooldown=0f;
        contactReactionPriority=0;
        impactHeldSprite=null;
        readabilityContactActive=false;
        ClearReadabilityBasicAttackPose();
        skillPoseScale=Vector3.one;
        earthPulseAttack=false;
        earthPulseFollowupHit=null;
        earthPulseDownstroke=null;
    }

    private void Advance(float deltaTime)
    {
        if(IsSkillAnticipating)return;
        if(!statusFrozen&&!frozenFrame&&raptorStrikeStartedFrame!=Time.frameCount)
            raptorStrikeRemaining=Mathf.Max(0f,raptorStrikeRemaining-deltaTime);
        // This frame's delta began before the impact; always draw a newly held pose once.
        if(impactReactionStartedFrame!=Time.frameCount)
        {
            impactHoldRemaining=Mathf.Max(0f,impactHoldRemaining-deltaTime);
            impactReactionCooldown=Mathf.Max(0f,impactReactionCooldown-deltaTime);
            contactShakeRemaining=Mathf.Max(0f,contactShakeRemaining-deltaTime);
        }
        if(impactReactionCooldown<=0f)contactReactionPriority=0;
        if(readabilityContactActive&&contactShakeRemaining<=0f&&impactHoldRemaining<=0f&&impactReactionCooldown<=0f)
            readabilityContactActive=false;
        if(struggling&&struggleClock!=null)
        {
            float now=struggleClock();
            deltaTime=Mathf.Max(0f,now-struggleClockTime);struggleClockTime=now;
        }
        if(skillAttackClock!=null)
        {
            float now=skillAttackClock();
            deltaTime=Mathf.Max(0f,now-skillAttackClockTime);
            skillAttackClockTime=now;
        }
        if(CombatSkillClock.Capture()!=null)deltaTime=CombatSkillClock.ActorDelta(this);
        deltaTime*=CombatMotionRate;
        if((ninjaCrossAttack||ninjaEightGateAttack||ninjaCounterAttack)&&ninjaShadowValid!=null&&!ninjaShadowValid())
        {
            CancelPendingSkillAttack();oneShot=false;
            if(!statusFrozen&&!terminal)SetMotion(moving?"Run":"Idle");
            return;
        }
        if(frozenFrame||statusFrozen)return;
        if(beetleOverturnTime>=0f)return;
        if(mantisReaperAction)return; // Combat clock drives both movement and contact poses.
        if(beetleFlipAttack)
        {
            if(beetleFlipValid!=null&&!beetleFlipValid()){InterruptCurrentAction(true);return;}
            beetleFlipElapsed+=Mathf.Max(0f,deltaTime)*globalSpeedMultiplier*beetleFlipTimeScale;
            ApplyExplicitFrame();beetleFlipAdvance?.Invoke(beetleFlipElapsed);
            if(beetleFlipAttack&&beetleFlipElapsed>=(beetleCrushAttack?BeetleFlipRules.CrushEnd:BeetleFlipRules.End))
            {CancelPendingSkillAttack();oneShot=false;SetMotion(moving?"Run":"Idle");}
            return;
        }
        if(mammothChargeAttack)
        {
            if(mammothChargeValid!=null&&!mammothChargeValid()){InterruptCurrentAction(true);return;}
            mammothChargeElapsed+=Mathf.Max(0f,deltaTime)*globalSpeedMultiplier;
            ApplyExplicitFrame();mammothChargeAdvance?.Invoke(mammothChargeElapsed);
            if(mammothChargeAttack&&mammothChargeElapsed>=(mammothExpulsionAttack?MammothExpulsionRules.CastEnd:mammothBraceAttack?MammothRank3Rules.BraceEnd:mammothTossAttack?MammothRank3Rules.TossCastEnd:mammothWallRushAttack?MammothRank3Rules.WallRushEnd:MammothCombatRules.EndTime))
            {CancelPendingSkillAttack();oneShot=false;SetMotion(moving?"Run":"Idle");}
            return;
        }
        if(deathRollFrame)return;
        float dt=Mathf.Max(0f,deltaTime);
        if(monkeyThrowRemaining>0f)
        {
            monkeyThrowRemaining=Mathf.Max(0f,monkeyThrowRemaining-dt*globalSpeedMultiplier);
            float t=monkeyThrowDuration-monkeyThrowRemaining;
            // Reuse release/recovery/drawn-back frames on each real projectile release.
            float frame=monkeyFinalThrow?skillAttackHitFrameIndex+t*skillAttackFramesPerSecond:
                t<.05f?skillAttackHitFrameIndex:Mathf.Max(0,skillAttackHitFrameIndex-1);
            motionElapsed=frame/Mathf.Max(1f,skillAttackFramesPerSecond);
            if(monkeyThrowRemaining>0f)return;
            if(monkeyFinalThrow)
            {
                monkeyFinalThrow=false;oneShot=false;
                StopSpecialClip();RestoreAnimator();SetPlaybackSpeed(1f);
                SetMotion(struggling?"Struggle":moving?"Run":"Idle");
                return;
            }
        }
        if(mantisGuillotineAttack)
        {
            if(mantisGuillotineValid!=null&&!mantisGuillotineValid()){InterruptCurrentAction(true);return;}
            mantisGuillotineElapsed+=dt*globalSpeedMultiplier;
            ApplyExplicitFrame();mantisGuillotineAdvance?.Invoke(mantisGuillotineElapsed);
            if(mantisGuillotineAttack&&mantisGuillotineElapsed>=MantisRank3Rules.GuillotineEnd)
            {CancelPendingSkillAttack();oneShot=false;SetMotion(moving?"Run":"Idle");}
            return;
        }
        if(tyrannoMawAttack)
        {
            if(tyrannoMawValid!=null&&!tyrannoMawValid()){InterruptCurrentAction(true);return;}
            tyrannoMawElapsed+=dt*globalSpeedMultiplier;
            ApplyExplicitFrame();
            tyrannoMawAdvance?.Invoke(tyrannoMawElapsed);
            if(tyrannoMawAttack&&tyrannoMawElapsed>=TyrannoRank3Rules.MawEnd)
            {CancelPendingSkillAttack();oneShot=false;SetMotion(moving?"Run":"Idle");}
            return;
        }
        if(tyrannoMarchAttack)
        {
            if(tyrannoMarchValid!=null&&!tyrannoMarchValid()){InterruptCurrentAction(true);return;}
            tyrannoMarchElapsed+=dt*globalSpeedMultiplier;
            ApplyExplicitFrame();
            tyrannoMarchAdvance?.Invoke(tyrannoMarchElapsed);
            if(tyrannoMarchAttack&&tyrannoMarchElapsed>=TyrannoRank3Rules.MarchEnd)
            {CancelPendingSkillAttack();oneShot=false;SetMotion(moving?"Run":"Idle");}
            return;
        }
        if(tyrannoStompAttack)tyrannoStompElapsed+=dt*globalSpeedMultiplier;
        if(ninjaShadowAttack){AdvanceNinjaShadowAttack(dt*globalSpeedMultiplier);return;}
        if(bearRagingAttack){AdvanceBearRagingAttack(dt*globalSpeedMultiplier);return;}
        float effectivePlaybackSpeed=Mathf.Max(
            0.01f,motionPlaybackSpeed*globalSpeedMultiplier);
        if(skillAttackHoldFrameIndex>=0)
        {
            float playbackSpeed=effectivePlaybackSpeed;
            float windupDuration=skillWindupClip!=null
                ?skillWindupClip.length
                :MotionFrameTime("SkillAttack",skillAttackHoldFrameIndex)+
                 1f/Mathf.Max(1f,skillAttackFramesPerSecond);
            if(motionElapsed<windupDuration)
            {
                float used=Mathf.Min(
                    dt,(windupDuration-motionElapsed)/playbackSpeed);
                motionElapsed=Mathf.Min(
                    windupDuration,motionElapsed+used*playbackSpeed);
                dt-=used;
            }
            if(motionElapsed>=windupDuration)
            {
                if(skillAttackFrames!=null&&skillAttackFrames.Length>0)
                    pinnedFrameSprite=skillAttackFrames[skillAttackHoldFrameIndex];
                skillAttackHoldRemaining-=dt*globalSpeedMultiplier;
                if(skillAttackHoldRemaining<=0f)
                {
                    float overflow=-skillAttackHoldRemaining;
                    int heldFrame=skillAttackHoldFrameIndex;
                    transform.localPosition=skillAttackHoldPosition;
                    skillAttackHoldFrameIndex=-1;
                    skillAttackHoldRemaining=0f;
                    pinnedFrameSprite=null;
                    skillImpactClipActive=skillImpactClip!=null;
                    motionElapsed=skillImpactClipActive
                        ?overflow
                        :MotionFrameTime("SkillAttack",heldFrame+1)+overflow;
                }
            }
        }
        else if(mantisCrossAttack)
        {
            float previous=mantisCrossElapsed;
            mantisCrossElapsed+=dt*globalSpeedMultiplier;
            float t=mantisCrossElapsed,first=MantisCombatRules.WindupDuration;
            float second=first+mantisCrossInterval;
            float frame=t<first?Mathf.Lerp(0f,skillAttackHitFrameIndex-.01f,t/first):
                t<first+.045f?skillAttackHitFrameIndex:
                t<second?skillAttackHitFrameIndex-1:
                t<second+.045f?skillAttackHitFrameIndex+1:
                Mathf.Lerp(skillAttackHitFrameIndex+2,SkillAttackFrameCount,(t-second-.045f)/.12f);
            // Even a slow frame must show each contact pose before recovery.
            if(previous<first&&t>=first)frame=skillAttackHitFrameIndex;
            else if(previous<second&&t>=second)frame=skillAttackHitFrameIndex+1;
            motionElapsed=frame/skillAttackFramesPerSecond;
            skillPoseScale=t<first?new Vector3(1.07f,.88f,1f):
                t<second?new Vector3(.95f,1.07f,1f):
                Vector3.Lerp(new Vector3(1.08f,.94f,1f),Vector3.one,(t-second)/.16f);
        }
        else if(earthPulseAttack)
        {
            float raise=worldOverturnAttack?KingKongCombatRules.WorldOverturnWindupDuration:
                earthPulseStrike==2?0.3f:earthPulseStrike==1?0.16f:0.2f;
            float contact=EarthPulseContactTime;
            float release=contact+(worldOverturnAttack?0.12f:earthPulseStrike==2?0.1f:earthPulseStrike==1?0.05f:0.06f);
            float recovery=worldOverturnAttack?0.18f:earthPulseStrike==2?0.2f:earthPulseStrike==1?0.09f:0.16f;
            float previous=earthPulseElapsed;
            earthPulseElapsed+=dt*effectivePlaybackSpeed;
            // A slow frame still renders contact before proceeding to recovery.
            if(previous<contact&&earthPulseElapsed>=contact)earthPulseElapsed=contact;
            float t=earthPulseElapsed;
            if(previous<raise&&t>=raise)earthPulseDownstroke?.Invoke(earthPulseStrike);
            // Re-time the existing raised-arms, contact and recovery frames.
            float raiseMotion=worldOverturnAttack?raise*0.5f:earthPulseStrike==2?raise*0.8f:raise;
            float frame=t<raise?Mathf.Lerp(0f,2.99f,Mathf.Clamp01(t/raiseMotion)):
                t<contact?2f:t<release?3f:4f+(t-release)/recovery*2f;
            motionElapsed=frame/skillAttackFramesPerSecond;
            Vector3 raised=worldOverturnAttack?new Vector3(0.9f,1.2f,1f):
                earthPulseStrike==2?new Vector3(0.91f,1.18f,1f):new Vector3(0.95f,1.08f,1f);
            Vector3 planted=worldOverturnAttack?new Vector3(1.25f,0.64f,1f):
                earthPulseStrike==2?new Vector3(1.22f,0.68f,1f):new Vector3(1.12f,0.78f,1f);
            skillPoseScale=t<raise?Vector3.Lerp(Vector3.one,raised,t/raiseMotion):
                t<contact?Vector3.Lerp(raised,planted,(t-raise)/(contact-raise)):
                t<release?planted:Vector3.Lerp(planted,Vector3.one,(t-release)/recovery);
        }
        else motionElapsed+=dt*effectivePlaybackSpeed;
        if(skillAttackHoldFrameIndex>=0)return;
        if(knockdownRemaining>0f)
        {
            knockdownRemaining-=dt;
            float knockdownEnd=knockdownClip!=null
                ?knockdownClip.length
                :MotionDuration("Death");
            motionElapsed=Mathf.Min(motionElapsed,knockdownEnd);
            if(DeathFrameCount>0&&motionElapsed>=knockdownEnd)
                pinnedFrameSprite=deathFrames[DeathFrameCount-1];
            if(knockdownRemaining>0f)return;
            oneShot=false;
            pinnedFrameSprite=null;
            StopSpecialClip();
            RestoreAnimator();
            SetPlaybackSpeed(1f);
            SetMotion(struggling?"Struggle":moving?"Run":"Idle");
            return;
        }
        if(terminal)
        {
            float duration=MotionDuration(requestedMotion);
            if(duration>0f)motionElapsed=Mathf.Min(motionElapsed,duration);
            return;
        }
        if(!oneShot)return;
        float oneShotDuration=requestedMotion=="SkillAttack"&&
                              skillImpactClipActive&&skillImpactClip!=null
            ?skillImpactClip.length
            :MotionDuration(requestedMotion);
        if(oneShotDuration<=0f||motionElapsed<oneShotDuration)return;
        ApplyExplicitFrame();
        if(requestedMotion=="Attack"&&HasPendingBasicAttackHit)
        {
            Debug.LogWarning(
                "Sprite basic attack reached the end without its Animation "+
                "Event. Resolving the pending hit as a fail-safe.",
                this);
            NotifyBasicAttackHitFrameFromAnimation();
        }
        else if(requestedMotion=="SkillAttack"&&HasPendingSkillAttackHit)
        {
            Debug.LogWarning(
                "Sprite skill attack reached the end without its Animation "+
                "Event. Resolving the pending hit as a fail-safe.",
                this);
            NotifySkillAttackHitFrameFromAnimation();
        }
        if(requestedMotion=="SkillAttack"&&skillAttackHoldFrameIndex>=0)return;
        if(earthPulseAttack)
        {
            if(earthPulseFollowupHit!=null&&earthPulseStrike<2)
            {
                earthPulseStrike++;
                earthPulseElapsed=0f;
                motionElapsed=0f;
                skillPoseScale=Vector3.one;
                skillAttackHitDispatched=false;
                skillAttackRecoveryPlaybackSpeed=motionPlaybackSpeed;
                pendingSkillAttackHit=()=>earthPulseFollowupHit?.Invoke(earthPulseStrike);
                ApplyExplicitFrame();
                return;
            }
            earthPulseAttack=false;
            earthPulseFollowupHit=null;
            earthPulseDownstroke=null;
            worldOverturnAttack=false;
            ClearWorldOverturnCharge();
            skillPoseScale=Vector3.one;
        }
        oneShot=false;
        reverseSkillAttack=false;
        skillImpactClipActive=false;
        if(mantisCrossAttack){mantisCrossAttack=false;skillPoseScale=Vector3.one;}
        if(bearSpiritAttack){bearSpiritAttack=false;bearAvatarRoar=false;skillPoseScale=Vector3.one;}
        if(wolfHuntAttack){wolfHuntAttack=false;skillPoseScale=Vector3.one;}
        if(tyrannoStompAttack){tyrannoStompAttack=false;skillPoseScale=Vector3.one;}
        StopSpecialClip();
        RestoreAnimator();
        SetPlaybackSpeed(1f);
        SetMotion(struggling?"Struggle":moving?"Run":"Idle");
    }

    private void LateUpdate()
    {
        if(IsSkillAnticipating){SuppressAnimator();return;}
        if(CombatSkillClock.ActorFrozen(this))return;
        using(CombatSkillClock.ActorScope(this))
        {
            if(statusFrozen){ApplyStatusFrozenPose();return;}
            ClearAppliedRecoil();
            if(frozenFrame)return;
            SuppressAnimator();
            ApplyExplicitFrame();
            ApplyPinnedFrame();
            if(IsPoseLocked&&spriteRenderer!=null)spriteRenderer.enabled=true;
            if(requestedMotion=="SkillAttack"&&skillAttackHoldFrameIndex>=0)
                transform.localPosition=skillAttackHoldPosition+
                    Vector3.right*Mathf.Sin(CombatSkillClock.ActorTime(this)*54f)*
                    (tyrannoStompAttack&&tyrannoStompElapsed>=tyrannoStompWindup-.1f?0f:0.018f);
            if(impactRecoilRemaining>0f&&!terminal)
            {
                float t=1f-impactRecoilRemaining/0.14f;
                float kick=t<0.2f?t/0.2f:Mathf.Pow((1f-t)/0.8f,2f);
                appliedRecoilOffset=impactRecoilOffset*kick;
            }
            if(contactShakeRemaining>0f&&!terminal&&knockdownRemaining<=0f)
            {
                float t=Mathf.Clamp01(1f-contactShakeRemaining/ContactReactionDuration);
                float recoil=Mathf.Cos(t*Mathf.PI*4f)*(1f-t)*(1f-t);
                if(CombatReadabilityPresentation.Adjusted)recoil=(1f-t)*(1f-t);
                Vector3 shake=contactShakeOffset*recoil;
                // Preserve a larger skill recoil instead of adding another displacement to it.
                if(shake.sqrMagnitude>appliedRecoilOffset.sqrMagnitude)appliedRecoilOffset=shake;
            }
            if(raptorStrikeRemaining>0f&&!terminal&&knockdownRemaining<=0f)
            {
                float t=Mathf.Clamp01(1f-raptorStrikeRemaining/raptorStrikeDuration);
                float thrust=t<.18f?Mathf.Lerp(.45f,1f,t/.18f):t<.65f
                    ?Mathf.Lerp(1f,-.2f,Mathf.SmoothStep(0f,1f,(t-.18f)/.47f))
                    :Mathf.Lerp(-.2f,0f,Mathf.SmoothStep(0f,1f,(t-.65f)/.35f));
                appliedRecoilOffset+=raptorStrikeOffset*thrust;
            }
            if(worldOverturnAttack&&earthPulseElapsed<KingKongCombatRules.WorldOverturnWindupDuration)
            {
                float strength=Mathf.Lerp(0.025f,0.075f,
                    earthPulseElapsed/KingKongCombatRules.WorldOverturnWindupDuration);
                appliedRecoilOffset+=new Vector3(Mathf.Sin(earthPulseElapsed*150f),
                    Mathf.Sin(earthPulseElapsed*193f)*0.45f,0f)*strength;
            }
            if(tyrannoMarchAttack&&tyrannoMarchElapsed>=TyrannoRank3Rules.MarchWindup&&
                tyrannoMarchElapsed<TyrannoRank3Rules.MarchWindup+TyrannoRank3Rules.MarchDuration)
            {
                float step=(tyrannoMarchElapsed-TyrannoRank3Rules.MarchWindup)%TyrannoRank3Rules.MarchStrideDuration;
                float lift=Mathf.SmoothStep(0f,1f,step/.14f)*
                    (1f-Mathf.SmoothStep(0f,1f,(step-.25f)/(TyrannoRank3Rules.MarchFootContact-.25f)));
                appliedRecoilOffset+=Vector3.up*(lift*.12f);
            }
            if(bearRagingAttack)
            {
                float t=bearRagingElapsed-bearRagingWindup;
                float kick=t<0f?0f:t<.04f?.055f:
                    t<BearCombatRules.RagingSecondHitTime?-.035f:t<.12f?-.07f:
                    t<.205f?-.035f:t<BearCombatRules.RagingThirdHitTime?0f:
                    .13f*(1f-Mathf.Clamp01((t-BearCombatRules.RagingThirdHitTime)/BearCombatRules.RagingRecoveryDuration));
                appliedRecoilOffset+=Vector3.right*kick*(spriteRenderer!=null&&spriteRenderer.flipX?-1f:1f);
            }
            if(ninjaCrossAttack&&ninjaChainSheathe&&ninjaShadowElapsed>=NinjaCatCombatRules.ArrivalTime&&ninjaShadowElapsed<NinjaChainFinalArrival)
            {
                float phase=(ninjaShadowElapsed-NinjaCatCombatRules.ArrivalTime)%NinjaChainInterval;
                float approach=1f-Mathf.Clamp01(phase/NinjaCatRank3Rules.BlackMoonContactDelay);
                appliedRecoilOffset+=ninjaMoonApproachOffset*approach*approach;
            }
            if(contactShakeRemaining>0f&&!terminal&&!IsActionLocked&&impactRecoilRemaining<=0f&&
               ((requestedMotion=="Idle"&&IdleFrameCount>0)||(requestedMotion=="Run"&&RunFrameCount>0)))
            {
                float remaining=contactShakeRemaining/ContactReactionDuration;
                float compression=contactCompressionAmount*remaining*remaining;
                contactBaseScale=transform.localScale;
                contactAppliedScale=Vector3.Scale(contactBaseScale,new Vector3(1f+compression,1f-compression,1f));
                contactScaleApplied=true;
                transform.localScale=contactAppliedScale;
            }
            SampleReadabilityBasicAttackPose(out Vector3 attackOffset,out Vector3 attackScale);
            appliedRecoilOffset+=attackOffset;
            transform.localPosition+=appliedRecoilOffset;
        }
    }

    private void OnWillRenderObject()
    {
        if(IsSkillAnticipating||CombatSkillClock.ActorFrozen(this))return;
        if(statusFrozen)
        {
            // Billboard already composed this frame's roll; retain it until the next pose update.
            if(spriteRenderer!=null)spriteRenderer.sprite=statusFrozenSprite;
            transform.localScale=statusFrozenScale;
            return;
        }
        if(frozenFrame||!IsPoseLocked)return;
        ApplyExplicitFrame();
        ApplyPinnedFrame();
    }

    public void Configure(
        Sprite[] idle,
        Sprite[] run,
        Sprite[] attack,
        Sprite[] skillAttack,
        Sprite[] death,
        Sprite[] capture,
        Sprite[] struggle)
    {
        idleFrames=idle??Array.Empty<Sprite>();
        runFrames=run??Array.Empty<Sprite>();
        attackFrames=attack??Array.Empty<Sprite>();
        skillAttackFrames=skillAttack??Array.Empty<Sprite>();
        deathFrames=death??Array.Empty<Sprite>();
        captureFrames=capture??Array.Empty<Sprite>();
        struggleFrames=struggle??Array.Empty<Sprite>();
    }

    public void ConfigureSpecialClips(
        AnimationClip windup,
        AnimationClip impact,
        AnimationClip knockdown)
    {
        skillWindupClip=windup;
        skillImpactClip=impact;
        knockdownClip=knockdown;
    }

    public void ConfigureSpecialAnimator(Animator value)
    {
        specialClipAnimator=value;
    }

    public void ConfigureHitFrames(
        int basicAttackFrameIndex,
        int skillAttackFrameIndex)
    {
        basicAttackHitFrameIndex=Mathf.Clamp(
            basicAttackFrameIndex,0,Mathf.Max(0,AttackFrameCount-1));
        skillAttackHitFrameIndex=Mathf.Clamp(
            skillAttackFrameIndex,0,Mathf.Max(0,SkillAttackFrameCount-1));
    }

    public void SetMoving(bool value)
    {
        if(statusFrozen||IsSkillAnticipating)return;
        moving=value;
        ResolveAnimator();
        if(animator!=null)animator.SetBool(MovingParameter,moving);
        if(!terminal&&!struggling&&!IsActionLocked)
            SetMotion(moving?"Run":"Idle");
    }

    public void SetCombatPlaybackSpeed(float value)
    {
        combatPlaybackSpeed=Mathf.Clamp(value,.1f,2f);
        SetGlobalSpeedMultiplier(globalSpeedMultiplier);
    }

    public void SetGlobalSpeedMultiplier(float value)
    {
        globalSpeedMultiplier=Mathf.Clamp(value,0.1f,2f);
        if(animator!=null)
            animator.speed=statusFrozen||IsSkillAnticipating?0f:motionPlaybackSpeed*globalSpeedMultiplier*CombatMotionRate;
    }

    public void SetSkillAnticipating(bool value)
    {
        if(IsSkillAnticipating==value)return;
        ClearAppliedRecoil();
        if(value&&readabilityContactActive)
        {
            contactShakeRemaining=impactHoldRemaining=impactReactionCooldown=0f;
            impactHeldSprite=null;readabilityContactActive=false;contactReactionPriority=0;
        }
        if(value)ClearReadabilityBasicAttackPose();
        IsSkillAnticipating=value;
        if(animator!=null)animator.speed=statusFrozen||value?0f:motionPlaybackSpeed*globalSpeedMultiplier*CombatMotionRate;
    }

    // Unlike the terminal death freeze, a status pause preserves motion time and pending hits.
    public void SetFrozen(bool value)
    {
        if(terminal||statusFrozen==value)return;
        ClearAppliedRecoil();
        if(value)raptorStrikeRemaining=0f;
        statusFrozen=value;
        if(value)
        {
            impactHoldRemaining=0f;
            contactShakeRemaining=0f;
            impactHeldSprite=null;
            RestoreNinjaVisibility();
            if(spriteRenderer==null)spriteRenderer=GetComponent<SpriteRenderer>();
            statusFrozenSprite=spriteRenderer==null?null:spriteRenderer.sprite;
            statusFrozenPosition=transform.localPosition;
            statusFrozenScale=transform.localScale;
        }
        else statusFrozenSprite=null;
        if(animator!=null)animator.speed=value||IsSkillAnticipating?0f:motionPlaybackSpeed*globalSpeedMultiplier*CombatMotionRate;
    }

    private void ApplyStatusFrozenPose()
    {
        ClearAppliedRecoil();
        if(spriteRenderer!=null)spriteRenderer.sprite=statusFrozenSprite;
        transform.localPosition=statusFrozenPosition;
        transform.localScale=statusFrozenScale;
    }

    public void SetStruggling(bool value,Func<float> combatClock=null)
    {
        if(!value)
        {
            struggleClock=null;
            struggling=false;ResolveAnimator();
            if(animator!=null)animator.SetBool(StrugglingParameter,false);
        }
        if(terminal||statusFrozen||knockdownRemaining>0f||skillAttackHoldFrameIndex>=0||
           skillImpactClipActive)return;
        struggling=value;
        ResolveAnimator();
        if(animator!=null)animator.SetBool(StrugglingParameter,struggling);
        if(struggling)
        {
            CancelPendingHitEvents();
            oneShot=false;
            SetPlaybackSpeed(1f);
            SetMotion("Struggle");
            struggleClock=combatClock;struggleClockTime=combatClock==null?0f:combatClock();
            return;
        }
        SetMotion(moving?"Run":"Idle");
    }

    public void InterruptCurrentAction(bool cancelSkillHold=false)
    {
        if(terminal||statusFrozen||knockdownRemaining>0f||
           (skillAttackHoldFrameIndex>=0&&!cancelSkillHold)||
           skillImpactClipActive)return;
        knockdownRemaining=0f;
        CancelPendingHitEvents();
        oneShot=false;
        RestoreAnimator();
        SetPlaybackSpeed(1f);
        SetMotion(moving?"Run":"Idle");
    }

    public void StopForSilkBinding()
    {
        if(terminal)return;
        // Binding cancels queued hits, including held windups, instead of postponing them.
        CancelPendingHitEvents();oneShot=false;moving=false;struggling=false;
        if(!statusFrozen&&knockdownRemaining<=0f)
        {
            RestoreAnimator();SetPlaybackSpeed(1f);SetMotion("Idle");
            ApplyExplicitFrame();
        }
        SetFrozen(true);
    }

    public void FreezeCurrentFrame()
    {
        impactHoldRemaining=0f;
        contactShakeRemaining=0f;
        impactHeldSprite=null;
        SetFrozen(false);
        CancelPendingHitEvents();
        oneShot=false;
        terminal=true;
        frozenFrame=true;
        ResolveAnimator();
        if(animator!=null)animator.speed=0f;
    }

    public void PlayBasicAttack()
    {
        PlayBasicAttack(1f,null);
    }

    public void PlayBasicAttack(
        float playbackSpeed,
        Action hitCallback)
    {
        if(terminal||IsActionLocked)return;
        beetleSweepAttack=false;griffinWingAttack=false;CancelPendingSkillAttack();
        pendingBasicAttackHit=hitCallback;
        basicAttackHitDispatched=false;
        PlayTrigger(
            AttackParameter,
            "Attack",
            false,
            playbackSpeed,
            false);
    }

    public void PlayBeetleSweepAttack(float speed,Action hit)
    {
        if(terminal||IsActionLocked)return;
        PlayBasicAttack(speed,hit);beetleSweepAttack=true;ApplyExplicitFrame();
    }

    public void UseGriffinWingPose()
    {
        if(requestedMotion!="Attack"||!HasPendingBasicAttackHit)return;
        griffinWingAttack=true;
        ApplyExplicitFrame();
    }

    public void PlayBearRagingAttack(float playbackSpeed,Func<int,bool> hitCallback)
    {
        if(terminal||IsActionLocked)return;
        PlayBasicAttack(playbackSpeed,null);
        bearRagingAttack=true;
        bearRagingElapsed=0f;
        bearRagingWindup=BasicAttackHitTime/Mathf.Max(.01f,playbackSpeed);
        bearRagingNextStrike=0;
        bearRagingHit=hitCallback;
        ApplyBearRagingPose();
    }

    private void AdvanceBearRagingAttack(float dt)
    {
        float nextTime=bearRagingElapsed+dt;
        // One timeline owns both contact poses and damage; status freeze pauses both.
        while(bearRagingNextStrike<BearCombatRules.RagingStrikeCount)
        {
            float contact=bearRagingWindup+(bearRagingNextStrike==0?0f:
                bearRagingNextStrike==1?BearCombatRules.RagingSecondHitTime:BearCombatRules.RagingThirdHitTime);
            if(nextTime<contact)break;
            bearRagingElapsed=contact;
            ApplyBearRagingPose();
            ApplyExplicitFrame();
            int strike=bearRagingNextStrike++;
            bool keepAttacking=bearRagingHit==null||bearRagingHit(strike);
            if(!bearRagingAttack)return; // Damage/reflection may kill or interrupt the attacker.
            if(!keepAttacking){InterruptCurrentAction();return;}
        }
        bearRagingElapsed=nextTime;
        if(bearRagingElapsed>=bearRagingWindup+BearCombatRules.RagingThirdHitTime+
           BearCombatRules.RagingRecoveryDuration)
        {
            CancelPendingBasicAttack();
            oneShot=false;
            SetPlaybackSpeed(1f);
            SetMotion(moving?"Run":"Idle");
            return;
        }
        ApplyBearRagingPose();
    }

    private void ApplyBearRagingPose()
    {
        float t=bearRagingElapsed-bearRagingWindup;
        float contact=basicAttackHitFrameIndex;
        float last=Mathf.Max(contact,AttackFrameCount-1);
        float raised=Mathf.Max(0f,contact-2f);
        float frame=t<0f?Mathf.Lerp(0f,contact,bearRagingElapsed/Mathf.Max(.001f,bearRagingWindup)):
            t<.035f?Mathf.Lerp(contact,last,t/.035f):
            t<BearCombatRules.RagingSecondHitTime?Mathf.Lerp(last,contact,(t-.035f)/.045f):
            t<.12f?Mathf.Lerp(contact,raised,(t-BearCombatRules.RagingSecondHitTime)/.04f):
            t<.205f?raised:t<BearCombatRules.RagingThirdHitTime?
                Mathf.Lerp(raised,contact,(t-.205f)/.035f):
            t<.29f?contact:Mathf.Lerp(contact,last,(t-.29f)/.13f);
        motionElapsed=(frame+.001f)/attackFramesPerSecond;
        Vector3 drawnBack=new Vector3(.97f,1.04f,1f);
        Vector3 impact=new Vector3(1.06f,.96f,1f);
        skillPoseScale=t<.12f?Vector3.one:t<.205f?drawnBack:
            t<BearCombatRules.RagingThirdHitTime?Vector3.Lerp(drawnBack,impact,(t-.205f)/.035f):
            Vector3.Lerp(impact,Vector3.one,(t-BearCombatRules.RagingThirdHitTime)/BearCombatRules.RagingRecoveryDuration);
    }

    public void PlaySkillAttack()
    {
        PlaySkillAttack(1f,null);
    }

    public void PlaySkillAttack(
        float playbackSpeed,
        Action hitCallback)
    {
        PlaySkillAttack(playbackSpeed,playbackSpeed,hitCallback);
    }

    public void PlaySkillAttack(
        float playbackSpeed,
        float recoveryPlaybackSpeed,
        Action hitCallback)
    {
        if(skillWindupClip!=null&&skillImpactClip!=null)
        {
            PlaySkillAttackWithHold(
                TyrannoCombatRules.TremorWindupFrames-1,
                TyrannoCombatRules.TremorCastSeconds,
                recoveryPlaybackSpeed,hitCallback);
            return;
        }
        if(terminal||IsActionLocked)return;
        CancelPendingBasicAttack();
        pendingSkillAttackHit=hitCallback;
        skillAttackHitDispatched=false;
        reverseSkillAttack=false;
        skillAttackHoldFrameIndex=-1;
        skillAttackHoldRemaining=0f;
        skillImpactClipActive=false;
        skillAttackRecoveryPlaybackSpeed=Mathf.Clamp(
            recoveryPlaybackSpeed,0.1f,6f);
        PlayTrigger(
            SkillAttackParameter,
            "SkillAttack",
            false,
            playbackSpeed,
            false);
    }

    public void PlaySkillAttackReverse(float playbackSpeed)
    {
        if(terminal||IsActionLocked)return;
        CancelPendingBasicAttack();
        CancelPendingSkillAttack();
        reverseSkillAttack=true;
        PlayTrigger(
            SkillAttackParameter,
            "SkillAttack",
            false,
            playbackSpeed,
            false);
    }

    public void PlayEarthPulseAttack(
        float playbackSpeed,Action hitCallback,Action<int> followupHitCallback=null,Action<int> downstrokeCallback=null)
    {
        if(terminal||IsActionLocked)return;
        PlaySkillAttack(playbackSpeed,hitCallback);
        earthPulseAttack=true;
        earthPulseElapsed=0f;
        earthPulseStrike=0;
        earthPulseFollowupHit=followupHitCallback;
        earthPulseDownstroke=downstrokeCallback;
    }

    public void PlayMantisCrossAttack(Action hitCallback,float interval=MantisCombatRules.WaveInterval)
    {
        if(terminal||IsActionLocked)return;
        PlaySkillAttack(1f,1f,hitCallback);
        mantisCrossAttack=true;mantisCrossElapsed=0f;mantisCrossInterval=interval;
    }

    public void PlayWorldOverturnAttack(float playbackSpeed,Action hitCallback,ParticleSystem charge=null,Action<int> downstrokeCallback=null)
    {
        if(terminal||IsActionLocked)
        {
            if(charge!=null)Destroy(charge.gameObject);
            return;
        }
        PlayEarthPulseAttack(playbackSpeed,hitCallback,downstrokeCallback:downstrokeCallback);
        worldOverturnAttack=true;
        worldOverturnCharge=charge;
    }

    public void PlayBearFightingSpirit(float recoveryPlaybackSpeed,Action hitCallback)
    {
        if(terminal||IsActionLocked)return;
        PlaySkillAttackWithHold(skillAttackHitFrameIndex-1,
            BearCombatRules.PreparationDuration,recoveryPlaybackSpeed,hitCallback);
        bearSpiritAttack=true;
        ApplyExplicitFrame();
    }

    public void PlayBearAvatarRoar(Action hitCallback,bool prepared=false)
    {
        if(terminal||statusFrozen)return;
        if(prepared)InterruptCurrentAction(true);
        else if(IsActionLocked)return;
        PlaySkillAttackWithHold(skillAttackHitFrameIndex-1,
            BearCombatRules.AvatarRoarPreparationDuration,.55f,hitCallback);
        bearSpiritAttack=true;
        bearAvatarRoar=true;
        if(prepared)
        {
            skillAttackHoldFrameIndex=-1;skillAttackHoldRemaining=0f;
            motionElapsed=MotionFrameTime("SkillAttack",skillAttackHitFrameIndex)+.001f;
        }
        ApplyExplicitFrame();
    }

    public void PlayWolfHuntAttack(float recoveryPlaybackSpeed,Action hitCallback,bool spectral=false)
    {
        if(terminal||IsActionLocked)return;
        if(spectral)
            PlaySkillAttackWithHold(0,2f,recoveryPlaybackSpeed,null);
        else PlaySkillAttack(SkillAttackHitTime/WolfCombatRules.HuntPreparationDuration,
            recoveryPlaybackSpeed,hitCallback);
        wolfHuntAttack=true;
        ApplyExplicitFrame();
    }

    public void PlayNinjaShadowAttack(Func<bool> valid,Action vanish,Action substitute,Action arrive,Action hit)
    {
        if(terminal||IsActionLocked)return;
        PlaySkillAttack(1f,hit);
        ninjaShadowAttack=true;
        ninjaShadowElapsed=0f;
        ninjaShadowValid=valid;
        ninjaVanish=vanish;
        ninjaSubstitute=substitute;
        ninjaArrive=arrive;
        ApplyNinjaShadowPose();
        ApplyExplicitFrame();
    }

    public void PlayNinjaCrossAttack(Func<bool> valid,Action vanish,Action substitute,
        Action<int> move,Action<int> hit,Action finished,int strikeCount=4,bool sheathe=false,Action<int> depart=null)
    {
        if(terminal||IsActionLocked)return;
        PlayNinjaShadowAttack(valid,vanish,substitute,null,null);
        ninjaCrossAttack=true;ninjaCrossBeat=0;
        ninjaChainStrikeCount=Mathf.Clamp(strikeCount,1,6);ninjaChainSheathe=sheathe;
        ninjaCrossMove=move;ninjaCrossHit=hit;ninjaCrossFinished=finished;ninjaCrossDepart=depart;
    }

    public void SetNinjaMoonApproach(Vector3 worldDirection)
    {
        // Only the sprite lunges. The combat body already occupies its safe landing position.
        Vector3 offset=-worldDirection.normalized*MonsterAutoBattlePrototype.TileSize*.55f;
        ninjaMoonApproachOffset=transform.parent==null?offset:transform.parent.InverseTransformVector(offset);
    }

    public void PlayNinjaCounterAttack(Func<bool> valid,Action vanish,Action substitute,
        Action arrive,Action hit,Action finished)
    {
        if(terminal||statusFrozen||knockdownRemaining>0f)return;
        InterruptCurrentAction();
        if(IsActionLocked)return;
        PlayNinjaShadowAttack(valid,vanish,substitute,arrive,hit);
        ninjaCounterAttack=true;ninjaCounterFinished=finished;
        // The intercepted hit vanishes the body immediately; log, arrival and strike keep their order.
        AdvanceNinjaShadowAttack(NinjaCatCombatRules.VanishTime);
    }

    public void PlayNinjaEightGateAttack(Func<bool> valid,Action vanish,Action substitute,
        Action arrive,Action<float,float> advance,Action finished)
    {
        if(terminal||IsActionLocked)return;
        PlayNinjaShadowAttack(valid,vanish,substitute,arrive,null);
        ninjaEightGateAttack=true;ninjaGateAdvance=advance;ninjaGateFinished=finished;
    }

    private void AdvanceNinjaShadowAttack(float dt)
    {
        if(!skillAttackHitDispatched&&ninjaShadowValid!=null&&!ninjaShadowValid())
        {
            InterruptCurrentAction();return;
        }
        float previous=ninjaShadowElapsed,next=previous+dt;
        // Resolve each beat once and in order, even when a slow frame crosses several.
        if(ninjaVanish!=null&&next>=NinjaCatCombatRules.VanishTime)
        {
            ninjaShadowElapsed=NinjaCatCombatRules.VanishTime;
            ApplyNinjaShadowPose();ApplyExplicitFrame();
            ninjaPreviousRenderingOff=spriteRenderer.forceRenderingOff;
            ninjaHidden=true;spriteRenderer.forceRenderingOff=true;
            Action action=ninjaVanish;ninjaVanish=null;action();
            if(!ninjaShadowAttack)return;
        }
        if(ninjaSubstitute!=null&&next>=NinjaCatCombatRules.SubstitutionTime)
        {
            Action action=ninjaSubstitute;ninjaSubstitute=null;action();
            if(!ninjaShadowAttack)return;
        }
        if(ninjaCrossAttack){AdvanceNinjaCrossAttack(next);return;}
        if(ninjaEightGateAttack)
        {
            if(ninjaArrive!=null&&next>=NinjaCatRank3Rules.GateArrivalTime)
            {
                Action action=ninjaArrive;ninjaArrive=null;action();RestoreNinjaVisibility();
                if(!ninjaEightGateAttack)return;
            }
            ninjaShadowElapsed=next;ApplyNinjaShadowPose();ApplyExplicitFrame();
            ninjaGateAdvance?.Invoke(previous,next);
            if(ninjaEightGateAttack&&next>=NinjaCatRank3Rules.GateRecoveryEnd)
            {
                CancelPendingSkillAttack();oneShot=false;SetPlaybackSpeed(1f);
                SetMotion(moving?"Run":"Idle");
            }
            return;
        }
        if(ninjaArrive!=null&&next>=NinjaCatCombatRules.ArrivalTime)
        {
            Action action=ninjaArrive;ninjaArrive=null;action();
            RestoreNinjaVisibility();
            if(!ninjaShadowAttack)return;
        }
        if(!skillAttackHitDispatched&&next>=NinjaCatCombatRules.StrikeTime)
        {
            ninjaShadowElapsed=NinjaCatCombatRules.StrikeTime;
            ApplyNinjaShadowPose();ApplyExplicitFrame();
            if(!ninjaShadowAttack)return;
        }
        ninjaShadowElapsed=next;
        if(next>=NinjaCatCombatRules.RecoveryEndTime)
        {
            CancelPendingSkillAttack();oneShot=false;SetPlaybackSpeed(1f);
            SetMotion(moving?"Run":"Idle");return;
        }
        ApplyNinjaShadowPose();
    }

    private void ApplyNinjaShadowPose()
    {
        float t=ninjaShadowElapsed;
        float raised=Mathf.Max(0,skillAttackHitFrameIndex-1);
        if(ninjaEightGateAttack)
        {
            float gateFrame=t<NinjaCatCombatRules.VanishTime?Mathf.Lerp(0f,raised,t/NinjaCatCombatRules.VanishTime):
                t<NinjaCatRank3Rules.GateLaunchTime?raised:
                Mathf.Lerp(skillAttackHitFrameIndex,SkillAttackFrameCount-.01f,
                    Mathf.InverseLerp(NinjaCatRank3Rules.GateLaunchTime,NinjaCatRank3Rules.GateRecoveryEnd,t));
            motionElapsed=(gateFrame+.001f)/skillAttackFramesPerSecond;return;
        }
        if(ninjaCrossAttack&&t>=NinjaCatCombatRules.ArrivalTime)
        {
            bool final=t>=NinjaChainFinalArrival;
            int step=Mathf.Min(ninjaChainStrikeCount-1,Mathf.FloorToInt((t-NinjaCatCombatRules.ArrivalTime+.0001f)/NinjaChainInterval));
            float begin=final?NinjaChainFinalArrival:
                NinjaCatCombatRules.ArrivalTime+step*NinjaChainInterval;
            float hit=final?NinjaChainFinalHit:
                NinjaChainFirstHit+step*NinjaChainInterval;
            float hold=final?NinjaCatRank3Rules.CrossFinisherHold:ninjaChainSheathe?.05f:.016f;
            float end=final?NinjaChainEnd:begin+NinjaChainInterval;
            float crossFrame=t<hit?Mathf.Lerp(0,raised,Mathf.InverseLerp(begin,hit,t)):
                t<hit+hold?skillAttackHitFrameIndex:
                Mathf.Lerp(skillAttackHitFrameIndex,Mathf.Max(0,SkillAttackFrameCount-.01f),Mathf.InverseLerp(hit+hold,end,t));
            if(final&&ninjaChainSheathe)
                crossFrame=Mathf.Lerp(skillAttackHitFrameIndex,SkillAttackFrameCount-1,Mathf.InverseLerp(begin,hit,t));
            motionElapsed=(crossFrame+.001f)/skillAttackFramesPerSecond;return;
        }
        float frame=t<NinjaCatCombatRules.VanishTime
            ?Mathf.Lerp(0f,raised,t/NinjaCatCombatRules.VanishTime)
            :t<NinjaCatCombatRules.StrikeTime?raised
            :t<NinjaCatCombatRules.StrikeTime+NinjaCatCombatRules.StrikeHoldDuration
                ?skillAttackHitFrameIndex
                :Mathf.Lerp(skillAttackHitFrameIndex,Mathf.Max(0,SkillAttackFrameCount-.01f),
                    (t-NinjaCatCombatRules.StrikeTime-NinjaCatCombatRules.StrikeHoldDuration)/
                    (NinjaCatCombatRules.RecoveryEndTime-NinjaCatCombatRules.StrikeTime-NinjaCatCombatRules.StrikeHoldDuration));
        motionElapsed=(frame+.001f)/skillAttackFramesPerSecond;
    }

    private void AdvanceNinjaCrossAttack(float next)
    {
        int beats=ninjaChainSheathe?3:2;
        while(ninjaCrossBeat<ninjaChainStrikeCount*beats+2)
        {
            int step=ninjaCrossBeat/beats,beat=ninjaCrossBeat%beats;
            float time=step==ninjaChainStrikeCount?(beat==1?NinjaChainFinalHit:NinjaChainFinalArrival):
                (beat==1?NinjaChainFirstHit:NinjaCatCombatRules.ArrivalTime+(beat==2?NinjaCatRank3Rules.BlackMoonVanishDelay:0f))+step*NinjaChainInterval;
            if(next<time)break;
            ninjaCrossBeat++;ninjaShadowElapsed=time;
            ApplyNinjaShadowPose();ApplyExplicitFrame();
            if(beat==1)ninjaCrossHit?.Invoke(step);
            else if(beat==2)
            {
                ninjaCrossDepart?.Invoke(step);
                if(!ninjaCrossAttack)return;
                ninjaPreviousRenderingOff=spriteRenderer.forceRenderingOff;
                ninjaHidden=true;spriteRenderer.forceRenderingOff=true;
            }
            else{ninjaCrossMove?.Invoke(step);RestoreNinjaVisibility();}
            if(!ninjaCrossAttack)return;
            if(ninjaShadowValid!=null&&!ninjaShadowValid())
            {
                InterruptCurrentAction();return;
            }
        }
        ninjaShadowElapsed=next;
        if(next>=NinjaChainEnd)
        {
            CancelPendingSkillAttack();oneShot=false;SetPlaybackSpeed(1f);
            SetMotion(moving?"Run":"Idle");return;
        }
        ApplyNinjaShadowPose();
    }

    private void RestoreNinjaVisibility()
    {
        if(ninjaHidden&&spriteRenderer!=null)spriteRenderer.forceRenderingOff=ninjaPreviousRenderingOff;
        ninjaHidden=false;
    }

    private void ClearWorldOverturnCharge()
    {
        if(worldOverturnCharge==null)return;
        worldOverturnCharge.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(Application.isPlaying)Destroy(worldOverturnCharge.gameObject);
        else DestroyImmediate(worldOverturnCharge.gameObject);
        worldOverturnCharge=null;
    }

    public void PlayTyrannoStomp(Action hitCallback,bool extinction=false)
    {
        if(terminal||IsActionLocked)return;
        tyrannoExtinction=extinction;
        tyrannoStompWindup=extinction?TyrannoRank3Rules.ExtinctionWindup:TyrannoCombatRules.TremorCastSeconds;
        PlaySkillAttackWithHold(TyrannoCombatRules.TremorWindupFrames-1,
            tyrannoStompWindup,1.25f,()=>{
                hitCallback?.Invoke();
                HoldSkillAttackHitFrame(extinction?.085f:.055f);
            });
        tyrannoStompAttack=true;tyrannoStompElapsed=0f;
    }

    public void PlayMantisGuillotine(Func<bool> valid,Action<float> advance,Action finished)
    {
        if(terminal||IsActionLocked)return;
        CancelPendingHitEvents();PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
        mantisGuillotineAttack=true;mantisGuillotineElapsed=0f;
        mantisGuillotineValid=valid;mantisGuillotineAdvance=advance;mantisGuillotineFinished=finished;
        ApplyExplicitFrame();
    }

    public void PlayMantisReaper(int mode,Action cancelled)
    {
        if(terminal||IsActionLocked)return;
        CancelPendingHitEvents();PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
        mantisReaperAction=true;mantisReaperMode=mode;mantisReaperTime=0f;
        mantisReaperCancelled=cancelled;ApplyExplicitFrame();
    }

    public void PoseMantisReaper(float time)
    {mantisReaperTime=time;ApplyExplicitFrame();}

    private void ApplyMantisReaperFrame()
    {
        if(skillAttackFrames==null||skillAttackFrames.Length==0)return;
        float t=mantisReaperTime;
        float frame=mantisReaperMode==0?(t<.24f?0f:t<.44f?1f:SkillAttackFrameCount-1):
            t<.12f?Mathf.Lerp(0f,skillAttackHitFrameIndex-1,t/.12f):
            t<.18f?skillAttackHitFrameIndex:t<.22f?skillAttackHitFrameIndex-1:
            t<.29f?skillAttackHitFrameIndex+1:
            Mathf.Lerp(skillAttackHitFrameIndex+2,SkillAttackFrameCount-.01f,(t-.29f)/.13f);
        currentFrameIndex=Mathf.Clamp((int)frame,0,skillAttackFrames.Length-1);
        SetSprite(skillAttackFrames[currentFrameIndex]);
        Vector3 pose=mantisReaperMode==0?Vector3.Lerp(new Vector3(1.08f,.83f,1f),Vector3.one,Mathf.Clamp01((t-.2f)/.3f)):
            t<.12f?new Vector3(1.06f,.9f,1f):t<.29f?new Vector3(1.08f,.98f,1f):Vector3.one;
        transform.localScale=pose*ScaleFor("SkillAttack");
    }

    private void ApplyMantisGuillotineFrame()
    {
        if(skillAttackFrames==null||skillAttackFrames.Length==0)return;
        float t=mantisGuillotineElapsed,first=MantisRank3Rules.GuillotineLaunch;
        float second=first+MantisRank3Rules.GuillotineInterval;
        float frame=t<.12f?0f:t<first?Mathf.Lerp(0f,skillAttackHitFrameIndex-1,(t-.12f)/.18f):
            t<first+.055f?skillAttackHitFrameIndex:t<second?skillAttackHitFrameIndex-1:
            t<.66f?Mathf.Min(skillAttackHitFrameIndex+1,SkillAttackFrameCount-1):
            Mathf.Lerp(skillAttackHitFrameIndex+1,SkillAttackFrameCount-.01f,(t-.66f)/.28f);
        Vector3 pose=t<.12f?new Vector3(1.08f,.84f,1f):t<first?new Vector3(.91f,1.12f,1f):
            t<second?new Vector3(1.12f,.92f,1f):t<.78f?Vector3.one:
            Vector3.Lerp(new Vector3(1.14f,.82f,1f),Vector3.one,(t-.84f)/.1f);
        currentFrameIndex=Mathf.Clamp(Mathf.FloorToInt(frame),0,skillAttackFrames.Length-1);
        SetSprite(skillAttackFrames[currentFrameIndex]);transform.localScale=pose*ScaleFor("SkillAttack");
    }

    public void PlayTyrannoMarch(Func<bool> valid,Action<float> advance,Action finished)
    {
        if(terminal||IsActionLocked)return;
        CancelPendingHitEvents();
        PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
        tyrannoMarchAttack=true;tyrannoMarchElapsed=0f;
        tyrannoMarchValid=valid;tyrannoMarchAdvance=advance;tyrannoMarchFinished=finished;
        ApplyExplicitFrame();
    }

    public void PoseBeetleOverturn(float time)
    {
        if(terminal)return;
        if(beetleOverturnTime<0f)
        {CancelPendingHitEvents();PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);}
        beetleOverturnTime=time;ApplyExplicitFrame();
    }

    public void PoseImperialHorn(float time,Sprite[] frames)
    {
        if(terminal)return;
        if(frames==null||frames.Length<8){PoseBeetleOverturn(time);return;}
        if(beetleOverturnTime<0f)
        {
            Sprite original=spriteRenderer==null?null:spriteRenderer.sprite;
            Vector3 position=transform.localPosition,scale=transform.localScale;
            CancelPendingHitEvents();PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
            imperialHornFrames=frames;imperialHornBasePosition=position;imperialHornBaseScale=scale;
            Bounds anchor=PixelMantisReaper.VisibleBody(frames[7]);
            Bounds body=original==null?anchor:PixelMantisReaper.VisibleBody(original);
            imperialHornScale=scale*(body.size.y/Mathf.Max(.001f,anchor.size.y));
            imperialHornHeight=body.size.y*Mathf.Abs(scale.y);
            imperialHornFoot=new Vector3(anchor.center.x,anchor.min.y,0f);
            float facing=spriteRenderer!=null&&spriteRenderer.flipX?-1f:1f;
            imperialHornPosition=position+new Vector3(facing*(body.center.x*scale.x-anchor.center.x*imperialHornScale.x),
                body.min.y*scale.y-anchor.min.y*imperialHornScale.y,0f);
        }
        beetleOverturnTime=time;ApplyExplicitFrame();
    }

    public void EndBeetleOverturnPose()
    {
        if(beetleOverturnTime<0f)return;
        beetleOverturnTime=-1f;InterruptCurrentAction(true);
    }

    private void ApplyBeetleOverturnFrame()
    {
        if(imperialHornFrames!=null)
        {
            currentFrameIndex=BeetleOverturnRules.BodyFrame(beetleOverturnTime);
            SetSprite(imperialHornFrames[currentFrameIndex]);
            Vector3 scale=Vector3.Scale(imperialHornScale,BeetleOverturnRules.SourceScale(beetleOverturnTime));
            Vector3 foot=imperialHornFoot,offset=BeetleOverturnRules.SourcePose(beetleOverturnTime)*imperialHornHeight;
            float facing=spriteRenderer!=null&&spriteRenderer.flipX?-1f:1f;foot.x*=facing;offset.x*=facing;
            transform.localPosition=imperialHornPosition+Vector3.Scale(foot,imperialHornScale-scale)+offset;
            transform.localScale=scale;return;
        }
        if(skillAttackFrames==null||skillAttackFrames.Length==0)return;
        float t=beetleOverturnTime;int hit=skillAttackHitFrameIndex;
        float frame=t<BeetleOverturnRules.Warning?Mathf.Lerp(0,Mathf.Max(0,hit-1),t/BeetleOverturnRules.Warning):
            t<BeetleOverturnRules.Landing?hit:Mathf.Lerp(hit,skillAttackFrames.Length-.01f,
                (t-BeetleOverturnRules.Landing)/(BeetleOverturnRules.End-BeetleOverturnRules.Landing));
        Vector3 low=new Vector3(1.17f,.78f,1),raised=new Vector3(.92f,1.16f,1);
        Vector3 pose=t<BeetleOverturnRules.Warning?Vector3.Lerp(Vector3.one,low,t/BeetleOverturnRules.Warning):
            t<BeetleOverturnRules.Warning+.13f?Vector3.Lerp(low,raised,(t-BeetleOverturnRules.Warning)/.13f):
            t<BeetleOverturnRules.Landing?raised:Vector3.Lerp(raised,Vector3.one,
                (t-BeetleOverturnRules.Landing)/(BeetleOverturnRules.End-BeetleOverturnRules.Landing));
        currentFrameIndex=Mathf.Clamp(Mathf.FloorToInt(frame),0,skillAttackFrames.Length-1);
        SetSprite(skillAttackFrames[currentFrameIndex]);transform.localScale=pose*ScaleFor("SkillAttack");
    }

    public void PlayBeetleFlip(Func<bool> valid,Action<float> advance,Action finished,bool crush=false,float timeScale=1f)
    {
        if(terminal||IsActionLocked)return;
        CancelPendingHitEvents();PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
        beetleFlipAttack=true;beetleCrushAttack=crush;beetleFlipElapsed=0f;
        beetleFlipTimeScale=Mathf.Max(.01f,timeScale);
        beetleFlipValid=valid;beetleFlipAdvance=advance;beetleFlipFinished=finished;
        ApplyExplicitFrame();
    }

    private void ApplyBeetleFlipFrame()
    {
        if(skillAttackFrames==null||skillAttackFrames.Length==0)return;
        float t=beetleFlipElapsed;int hit=skillAttackHitFrameIndex;
        float landing=beetleCrushAttack?BeetleFlipRules.CrushCasterLanding:BeetleFlipRules.Landing;
        float fall=beetleCrushAttack?BeetleFlipRules.CrushFall:BeetleFlipRules.FallStart;
        float end=beetleCrushAttack?BeetleFlipRules.CrushEnd:BeetleFlipRules.End;
        float frame=t<BeetleFlipRules.Hook?Mathf.Lerp(0,Mathf.Max(0,hit-1),t/BeetleFlipRules.Hook):
            t<landing?hit:
            Mathf.Lerp(hit,skillAttackFrames.Length-.01f,Mathf.Max(0,t-landing-.07f)/.23f);
        Vector3 low=new Vector3(1.16f,.79f,1),raised=new Vector3(.89f,1.22f,1),slam=new Vector3(1.28f,.69f,1);
        Vector3 pose=t<BeetleFlipRules.Hook?Vector3.Lerp(Vector3.one,low,t/BeetleFlipRules.Hook):
            t<BeetleFlipRules.RiseEnd?Vector3.Lerp(low,raised,Mathf.Sin(Mathf.Clamp01(
                (t-BeetleFlipRules.Hook)/(BeetleFlipRules.RiseEnd-BeetleFlipRules.Hook))*Mathf.PI*.5f)):
            t<fall?raised:
            t<landing?Vector3.Lerp(raised,slam,beetleCrushAttack?(t-fall)/(landing-fall):Mathf.Pow((t-fall)/(landing-fall),2)):
            Vector3.Lerp(slam,Vector3.one,(t-landing-.08f)/(end-landing-.08f));
        if(beetleCrushAttack&&t>=BeetleFlipRules.CrushJump&&t<fall)
            pose=Vector3.Lerp(low,new Vector3(1.04f,.94f,1),Mathf.Clamp01(
                (t-BeetleFlipRules.CrushJump)/(BeetleFlipRules.CrushApex-BeetleFlipRules.CrushJump)));
        currentFrameIndex=Mathf.Clamp(Mathf.FloorToInt(frame),0,skillAttackFrames.Length-1);
        SetSprite(skillAttackFrames[currentFrameIndex]);transform.localScale=pose*ScaleFor("SkillAttack");
    }

    public bool PlayMammothCharge(Func<bool> valid,Action<float> advance,Action finished,bool toss=false,bool wallRush=false,bool brace=false,bool expulsion=false)
    {
        if(terminal||IsActionLocked)return false;
        CancelPendingHitEvents();PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
        mammothChargeAttack=true;mammothTossAttack=toss;mammothWallRushAttack=wallRush;mammothBraceAttack=brace;mammothExpulsionAttack=expulsion;mammothChargeElapsed=0f;
        mammothBraceImpactAt=-10f;
        mammothChargeValid=valid;mammothChargeAdvance=advance;mammothChargeFinished=finished;
        ApplyExplicitFrame();
        return true;
    }

    public void PlayMammothBraceImpact(Vector3 direction)
    {
        if(!mammothBraceAttack||mammothChargeElapsed>=MammothRank3Rules.BraceGuardEnd)return;
        mammothBraceImpactAt=mammothChargeElapsed;PlayImpactRecoil(direction,.1f);
    }

    public void SyncMammothChargeTime(float time)
    {
        if(!mammothChargeAttack||!mammothExpulsionAttack)return;
        mammothChargeElapsed=Mathf.Max(mammothChargeElapsed,time);ApplyMammothChargeFrame();
    }

    private void ApplyMammothChargeFrame()
    {
        float t=mammothChargeElapsed;Vector3 pose;Sprite[] frames;int frame;
        if(mammothExpulsionAttack)
        {
            frames=skillAttackFrames;if(frames==null||frames.Length==0)return;
            float contact=MammothExpulsionRules.Contact,release=contact+MammothExpulsionRules.HitStop;
            if(t>=release&&t<MammothExpulsionRules.CastEnd-.12f&&runFrames!=null&&runFrames.Length>0)
            {
                currentFrameIndex=Mathf.FloorToInt((t-release)*24f)%runFrames.Length;
                SetSprite(runFrames[currentFrameIndex]);transform.localScale=Vector3.one*ScaleFor("Run");return;
            }
            frame=t<contact?Mathf.FloorToInt(t/contact*Mathf.Max(1,skillAttackHitFrameIndex)):
                skillAttackHitFrameIndex+Mathf.FloorToInt(Mathf.Max(0f,t-release)*10f);
            pose=t<contact?Vector3.Lerp(Vector3.one,new Vector3(1.16f,.76f,1f),t/contact):
                t<release?new Vector3(.91f,1.2f,1f):
                Vector3.Lerp(new Vector3(.91f,1.2f,1f),Vector3.one,Mathf.SmoothStep(0f,1f,(t-release)/.4f));
            currentFrameIndex=Mathf.Clamp(frame,0,frames.Length-1);SetSprite(frames[currentFrameIndex]);
            transform.localScale=pose*ScaleFor("SkillAttack");return;
        }
        if(mammothBraceAttack)
        {
            frames=skillAttackFrames;if(frames==null||frames.Length==0)return;
            int planted=Mathf.Max(0,skillAttackHitFrameIndex-1);
            frame=t<MammothRank3Rules.BraceWindup?Mathf.FloorToInt(t/MammothRank3Rules.BraceWindup*planted):
                t<MammothRank3Rules.BraceStrike?planted:skillAttackHitFrameIndex+Mathf.FloorToInt((t-MammothRank3Rules.BraceStrike)*12f);
            var low=new Vector3(1.13f,.82f,1);
            pose=t<MammothRank3Rules.BraceWindup?Vector3.Lerp(Vector3.one,low,t/MammothRank3Rules.BraceWindup):
                t<MammothRank3Rules.BraceGuardEnd?low:
                t<MammothRank3Rules.BraceStrike?Vector3.Lerp(low,new Vector3(.94f,1.12f,1),(t-MammothRank3Rules.BraceGuardEnd)/.15f):
                Vector3.Lerp(new Vector3(1.17f,.88f,1),Vector3.one,(t-MammothRank3Rules.BraceStrike)/.4f);
            if(t>=MammothRank3Rules.BraceWindup&&t<MammothRank3Rules.BraceGuardEnd)
            {
                float contact=Mathf.Pow(1-Mathf.Clamp01((t-mammothBraceImpactAt)/.18f),2);
                pose.x+=contact*.075f;pose.y-=contact*.08f;
            }
            currentFrameIndex=Mathf.Clamp(frame,0,frames.Length-1);SetSprite(frames[currentFrameIndex]);
            transform.localScale=pose*ScaleFor("SkillAttack");return;
        }
        if(mammothWallRushAttack)
        {
            bool running=t>=MammothRank3Rules.WallRushWindup&&t<MammothRank3Rules.WallRushArrival;
            frames=running?runFrames:skillAttackFrames;
            frame=running?Mathf.FloorToInt((t-MammothRank3Rules.WallRushWindup)*22f):
                t<MammothRank3Rules.WallRushWindup?Mathf.FloorToInt(t/MammothRank3Rules.WallRushWindup*Mathf.Max(1,skillAttackHitFrameIndex)):
                skillAttackHitFrameIndex+Mathf.FloorToInt((t-MammothRank3Rules.WallRushArrival)*12f);
            pose=running?new Vector3(1.07f,.93f,1):
                t<MammothRank3Rules.WallRushWindup?Vector3.Lerp(Vector3.one,new Vector3(1.11f,.87f,1),t/MammothRank3Rules.WallRushWindup):
                Vector3.Lerp(new Vector3(1.1f,.88f,1),Vector3.one,(t-MammothRank3Rules.WallRushArrival)/.28f);
            for(int i=0;i<3;i++)
            {
                float since=t-MammothRank3Rules.WallRushContactTime(i);
                if(since>=0f&&since<.1f){float weight=1f-since/.1f;pose.x+=weight*.09f;pose.y-=weight*.09f;
                    frames=skillAttackFrames;frame=skillAttackHitFrameIndex;running=false;break;}
            }
            if(frames==null||frames.Length==0)return;
            currentFrameIndex=running?frame%frames.Length:Mathf.Clamp(frame,0,frames.Length-1);
            SetSprite(frames[currentFrameIndex]);transform.localScale=pose*ScaleFor(running?"Run":"SkillAttack");return;
        }
        if(mammothTossAttack)
        {
            frames=skillAttackFrames;
            frame=t<.35f?Mathf.FloorToInt(t/.35f*Mathf.Max(1,skillAttackHitFrameIndex-1)):
                t<MammothRank3Rules.TossRelease?Mathf.Max(0,skillAttackHitFrameIndex-1):
                skillAttackHitFrameIndex+Mathf.FloorToInt((t-MammothRank3Rules.TossRelease)*10f);
            pose=t<.35f?Vector3.Lerp(Vector3.one,new Vector3(1.11f,.86f,1),t/.35f):
                t<.67f?Vector3.Lerp(new Vector3(1.11f,.86f,1),new Vector3(.93f,1.13f,1),(t-.35f)/.32f):
                t<.8f?Vector3.Lerp(new Vector3(.93f,1.13f,1),new Vector3(1.12f,.9f,1),(t-.67f)/.13f):
                Vector3.Lerp(new Vector3(1.12f,.9f,1),Vector3.one,(t-.8f)/.25f);
            if(frames==null||frames.Length==0)return;
            currentFrameIndex=Mathf.Clamp(frame,0,frames.Length-1);SetSprite(frames[currentFrameIndex]);
            transform.localScale=pose*ScaleFor("SkillAttack");return;
        }
        if(t<MammothCombatRules.Windup)
        {
            frames=skillAttackFrames;frame=Mathf.FloorToInt(t/MammothCombatRules.Windup*Mathf.Max(1,skillAttackHitFrameIndex));
            pose=Vector3.Lerp(Vector3.one,new Vector3(1.07f,.93f,1f),t/MammothCombatRules.Windup);
            pose.y+=Mathf.Sin(t*85f)*.012f;
        }
        else if(t<MammothCombatRules.ContactTime)
        {
            frames=runFrames;float run=t-MammothCombatRules.Windup;
            frame=Mathf.FloorToInt(run*18f);float step=Mathf.Sin(run/.18f*Mathf.PI*2f);
            pose=new Vector3(1.025f-step*.02f,.97f+step*.025f,1f);
        }
        else
        {
            frames=skillAttackFrames;float recovery=(t-MammothCombatRules.ContactTime)/MammothCombatRules.Recovery;
            frame=skillAttackHitFrameIndex+Mathf.FloorToInt(recovery*3f);
            pose=Vector3.Lerp(new Vector3(1.11f,.86f,1f),Vector3.one,Mathf.SmoothStep(0f,1f,recovery));
        }
        if(frames==null||frames.Length==0)return;
        currentFrameIndex=t>=MammothCombatRules.Windup&&t<MammothCombatRules.ContactTime?frame%frames.Length:
            Mathf.Clamp(frame,0,frames.Length-1);
        SetSprite(frames[currentFrameIndex]);transform.localScale=pose*ScaleFor(t>=MammothCombatRules.Windup&&t<MammothCombatRules.ContactTime?"Run":"SkillAttack");
    }

    public void PlayTyrannoMaw(Func<bool> valid,Action<float> advance,Action finished)
    {
        if(terminal||IsActionLocked)return;
        CancelPendingHitEvents();
        PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
        tyrannoMawAttack=true;tyrannoMawElapsed=0f;
        tyrannoMawValid=valid;tyrannoMawAdvance=advance;tyrannoMawFinished=finished;
        ApplyExplicitFrame();
    }

    private void ApplyTyrannoMawFrame()
    {
        if(skillAttackFrames==null||skillAttackFrames.Length==0)return;
        float t=tyrannoMawElapsed,contact=TyrannoRank3Rules.MawWindup;
        float close=TyrannoRank3Rules.MawCloseTime;
        float frame=t<contact?Mathf.Min(skillAttackHitFrameIndex-1,t/.55f*(skillAttackHitFrameIndex-1)):
            t<close+.09f?skillAttackHitFrameIndex:
            Mathf.Lerp(skillAttackHitFrameIndex,SkillAttackFrameCount-.01f,(t-close-.09f)/.29f);
        Vector3 raised=new Vector3(.95f,1.09f,1f),planted=new Vector3(1.15f,.83f,1f);
        Vector3 pose=t<.9f?Vector3.Lerp(Vector3.one,raised,t/.55f):
            t<contact?Vector3.Lerp(raised,planted,Mathf.Pow((t-.9f)/.1f,2f)):
            t<close?Vector3.Lerp(planted,new Vector3(1.06f,.96f,1f),(t-contact)/.25f):
            Vector3.Lerp(new Vector3(1.12f,.87f,1f),Vector3.one,(t-close-.08f)/.3f);
        currentFrameIndex=Mathf.Clamp(Mathf.FloorToInt(frame),0,skillAttackFrames.Length-1);
        SetSprite(skillAttackFrames[currentFrameIndex]);transform.localScale=pose*ScaleFor("SkillAttack");
    }

    private void ApplyTyrannoMarchFrame()
    {
        float t=tyrannoMarchElapsed;
        float movingTime=t-TyrannoRank3Rules.MarchWindup;
        bool walking=movingTime>=0f&&movingTime<TyrannoRank3Rules.MarchDuration;
        float frame;
        Vector3 pose;
        if(walking)
        {
            float step=movingTime%TyrannoRank3Rules.MarchStrideDuration;
            float contact=TyrannoRank3Rules.MarchFootContact;
            int raised=Mathf.Max(0,skillAttackHitFrameIndex-1);
            frame=step<.1f?Mathf.Lerp(0f,raised+.99f,step/.1f):
                step<contact?raised:skillAttackHitFrameIndex;
            Vector3 lifted=new Vector3(.97f,1.06f,1f),planted=new Vector3(1.14f,.82f,1f);
            pose=step<.25f?Vector3.Lerp(Vector3.one,lifted,step/.14f):
                step<contact?Vector3.Lerp(lifted,planted,Mathf.Pow((step-.25f)/(contact-.25f),2f)):
                Vector3.Lerp(planted,Vector3.one,(step-contact-.065f)/.095f);
        }
        else if(t<TyrannoRank3Rules.MarchWindup)
        {
            frame=0f;
            pose=Vector3.Lerp(Vector3.one,new Vector3(1.12f,.86f,1f),Mathf.Clamp01(t/TyrannoRank3Rules.MarchWindup));
        }
        else
        {
            float impact=t-TyrannoRank3Rules.MarchContact;
            frame=impact<0f?Mathf.Max(0,skillAttackHitFrameIndex-1):
                impact<.085f?skillAttackHitFrameIndex:
                Mathf.Lerp(skillAttackHitFrameIndex,SkillAttackFrameCount-.01f,(impact-.085f)/.165f);
            pose=impact<0f?new Vector3(.96f,1.08f,1f):
                Vector3.Lerp(new Vector3(1.14f,.82f,1f),Vector3.one,Mathf.Clamp01((impact-.085f)/.165f));
        }
        var frames=skillAttackFrames;
        if(frames==null||frames.Length==0)return;
        currentFrameIndex=Mathf.Clamp(Mathf.FloorToInt(frame),0,frames.Length-1);
        SetSprite(frames[currentFrameIndex]);
        transform.localScale=pose*ScaleFor("SkillAttack");
    }

    public void PlaySkillAttackWithHold(
        int holdFrameIndex,float totalWindupSeconds,
        float recoveryPlaybackSpeed,Action hitCallback,Func<float> combatClock=null)
    {
        if(terminal||IsActionLocked)return;
        ClearAppliedRecoil();
        CancelPendingBasicAttack();
        pendingSkillAttackHit=hitCallback;
        skillAttackHitDispatched=false;
        skillAttackHoldFrameIndex=Mathf.Clamp(
            holdFrameIndex,0,Mathf.Max(0,skillAttackHitFrameIndex-1));
        float windupDuration=skillWindupClip!=null
            ?skillWindupClip.length
            :MotionFrameTime("SkillAttack",skillAttackHoldFrameIndex)+
             1f/Mathf.Max(1f,skillAttackFramesPerSecond);
        skillAttackHoldRemaining=Mathf.Max(
            0f,totalWindupSeconds-windupDuration);
        skillAttackHoldPosition=transform.localPosition;
        skillImpactClipActive=false;
        skillAttackRecoveryPlaybackSpeed=Mathf.Clamp(
            recoveryPlaybackSpeed,0.1f,6f);
        PlayTrigger(SkillAttackParameter,"SkillAttack",false,1f,false);
        SuppressAnimator();
        motionElapsed=0f;
        pinnedFrameSprite=null;
        skillAttackClock=combatClock;
        skillAttackClockTime=combatClock==null?0f:combatClock();
        ApplyExplicitFrame();
    }

    public void HoldSkillAttackHitFrame(float duration)
        =>HoldSkillAttackFrame(skillAttackHitFrameIndex,duration);

    public void HoldSkillAttackFrame(int frameIndex,float duration)
    {
        if(terminal||statusFrozen||requestedMotion!="SkillAttack"||
           skillAttackFrames==null||skillAttackFrames.Length==0)return;
        ClearAppliedRecoil();
        skillAttackHoldFrameIndex=Mathf.Clamp(
            frameIndex,0,skillAttackFrames.Length-1);
        currentFrameIndex=skillAttackHoldFrameIndex;
        skillAttackHoldRemaining=Mathf.Max(0f,duration);
        skillAttackHoldPosition=transform.localPosition;
        pinnedFrameSprite=skillAttackFrames[skillAttackHoldFrameIndex];
        motionElapsed=MotionFrameTime("SkillAttack",skillAttackHoldFrameIndex)+
                      1f/Mathf.Max(1f,skillAttackFramesPerSecond);
    }

    public void SetDeathRollFrame(bool value)
    {
        if(statusFrozen)return;
        deathRollFrame=value&&!terminal&&requestedMotion=="SkillAttack";
        if(deathRollFrame)
            motionElapsed=MotionFrameTime("SkillAttack",skillAttackHitFrameIndex);
    }

    public void PlayDeath()
    {
        knockdownRemaining=0f;
        contactShakeRemaining=0f;
        PlayTrigger(DeathParameter,"Death",true,1f,true);
    }

    public void PlayKnockdown(float duration)
    {
        if(terminal||statusFrozen||duration<=0f)return;
        contactShakeRemaining=0f;
        if(knockdownRemaining>0f)
        {
            knockdownRemaining=Mathf.Max(
                knockdownRemaining,Mathf.Max(0.01f,duration));
            if(requestedMotion!="Death")SetMotion("Death",true);
            SuppressAnimator();
            return;
        }
        float fallTime=Mathf.Min(0.25f,Mathf.Max(0.01f,duration));
        float playbackSpeed=knockdownClip!=null
            ?knockdownClip.length/fallTime
            :MotionFrameTime("Death",DeathFrameCount-1)/fallTime;
        knockdownRemaining=Mathf.Max(0.01f,duration);
        PlayTrigger(DeathParameter,"Death",false,playbackSpeed,true);
        SuppressAnimator();
        ApplyExplicitFrame();
    }

    public void SetKnockdownActive(float remaining)
    {
        if(statusFrozen)return;
        if(remaining>0f)
        {
            PlayKnockdown(remaining);
            return;
        }
        if(knockdownRemaining<=0f||terminal)return;
        knockdownRemaining=0f;
        oneShot=false;
        pinnedFrameSprite=null;
        StopSpecialClip();
        RestoreAnimator();
        SetPlaybackSpeed(1f);
        SetMotion(struggling?"Struggle":moving?"Run":"Idle");
    }

    public void PlayCapture()
    {
        knockdownRemaining=0f;
        skillImpactClipActive=false;
        PlayTrigger(CaptureParameter,"Capture",true,1f,true);
    }

    public void NotifyBasicAttackHitFrameFromAnimation()
    {
        if(IsSkillAnticipating||CombatSkillClock.ActorFrozen(this)||CombatSkillClock.Active&&CombatSkillClock.ActorDelta(this)<=0f)return;
        using(CombatSkillClock.ActorScope(this))
        {
            if(bearRagingAttack)return;
            if(statusFrozen||requestedMotion!="Attack"||basicAttackHitDispatched)return;
            basicAttackHitDispatched=true;
            Action callback=pendingBasicAttackHit;
            pendingBasicAttackHit=null;
            callback?.Invoke();
        }
    }

    public void NotifySkillAttackHitFrameFromAnimation()
    {
        if(IsSkillAnticipating||CombatSkillClock.ActorFrozen(this)||CombatSkillClock.Active&&CombatSkillClock.ActorDelta(this)<=0f)return;
        using(CombatSkillClock.ActorScope(this))
        {
            if(statusFrozen||mammothChargeAttack||mantisReaperAction||mantisGuillotineAttack||ninjaCrossAttack||ninjaEightGateAttack||tyrannoMarchAttack||tyrannoMawAttack)return;
            if(ninjaShadowAttack&&ninjaShadowElapsed<NinjaCatCombatRules.StrikeTime)return;
            if(earthPulseAttack&&earthPulseElapsed<EarthPulseContactTime)return;
            if(requestedMotion!="SkillAttack"||skillAttackHitDispatched||
               skillAttackHoldFrameIndex>=0)return;
            skillAttackHitDispatched=true;
            ClearWorldOverturnCharge();
            Action callback=pendingSkillAttackHit;
            pendingSkillAttackHit=null;
            SetPlaybackSpeed(skillAttackRecoveryPlaybackSpeed);
            skillAttackRecoveryPlaybackSpeed=1f;
            callback?.Invoke();
        }
    }

    public void ResetForReuse()
    {
        if(readabilityBasicPoseActive||readabilityBasicPoseScaleApplied)ClearAppliedRecoil();
        ClearReadabilityBasicAttackPose();
        combatPlaybackSpeed=1f;
        SetSkillAnticipating(false);
        SetSkillFocus(0f);
        ResolveAnimator();
        StopSpecialClip();
        RestoreAnimator();
        moving=false;
        struggling=false;
        oneShot=false;
        terminal=false;
        frozenFrame=false;
        statusFrozen=false;
        statusFrozenSprite=null;
        impactHoldRemaining=impactReactionCooldown=contactShakeRemaining=0f;
        contactReactionPriority=0;
        impactHeldSprite=null;
        readabilityContactActive=false;
        requestedMotion="Idle";
        motionElapsed=0f;
        motionPlaybackSpeed=1f;
        globalSpeedMultiplier=1f;
        currentFrameIndex=0;
        knockdownRemaining=0f;
        CancelPendingHitEvents();
        if(animator==null){ApplyExplicitFrame();return;}
        animator.speed=1f;
        if(!animator.isActiveAndEnabled)
        {
            ApplyExplicitFrame();
            return;
        }
        animator.Rebind();
        animator.Update(0f);
        animator.SetBool(MovingParameter,false);
        animator.SetBool(StrugglingParameter,false);
        animator.ResetTrigger(AttackParameter);
        animator.ResetTrigger(SkillAttackParameter);
        animator.ResetTrigger(DeathParameter);
        animator.ResetTrigger(CaptureParameter);
        animator.Play("Idle",0,0f);
        animator.Update(0f);
        ApplyExplicitFrame();
    }

    public Sprite[] IdleFrames=>idleFrames;
    public Sprite[] DeathFrames=>deathFrames;
    public float IdleScale=>AuthoredScaleFor("Idle");

    internal System.Collections.Generic.IEnumerable<Sprite> CombatFrames()
    {
        foreach(var frames in new[]{idleFrames,runFrames,attackFrames,skillAttackFrames,deathFrames,struggleFrames})
        {
            if(frames==null)continue;
            foreach(Sprite frame in frames)if(frame!=null)yield return frame;
        }
    }

    public Sprite GetIdleFrame(float unscaledTime)
    {
        if(idleFrames==null||idleFrames.Length==0)return null;
        int index=Mathf.FloorToInt(
            Mathf.Max(0f,unscaledTime)*idleFramesPerSecond)%idleFrames.Length;
        return idleFrames[index];
    }

    public Sprite GetRunFrame(float time)
    {
        if(runFrames==null||runFrames.Length==0)return GetIdleFrame(time);
        return runFrames[Mathf.FloorToInt(Mathf.Max(0f,time)*runFramesPerSecond)%runFrames.Length];
    }

    // Authored Drag sheets are imported into the Struggle profile.
    public float DragScale=>ScaleFor("Struggle");
    public Sprite[] DragFrames=>struggleFrames;
    public float DragFramesPerSecond=>struggleFramesPerSecond;

    public Sprite GetBasicAttackFrame(float time)
    {
        if(attackFrames==null||attackFrames.Length==0)return GetIdleFrame(time);
        return attackFrames[Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f,time)*attackFramesPerSecond),0,attackFrames.Length-1)];
    }

    private void PlayTrigger(
        string parameter,
        string motionName,
        bool isTerminal,
        float playbackSpeed,
        bool cancelPendingHitEvents)
    {
        if(terminal)return;
        if(isTerminal){SetSkillAnticipating(false);SetFrozen(false);}
        frozenFrame=false;
        StopSpecialClip();
        if(cancelPendingHitEvents)CancelPendingHitEvents();
        ResolveAnimator();
        RestoreAnimator();
        oneShot=!isTerminal;
        terminal=isTerminal;
        struggling=false;
        SetPlaybackSpeed(playbackSpeed);
        SetMotion(motionName,true);
        if(animator==null)return;
        animator.SetBool(StrugglingParameter,false);
        animator.ResetTrigger(AttackParameter);
        animator.ResetTrigger(SkillAttackParameter);
        animator.ResetTrigger(DeathParameter);
        animator.ResetTrigger(CaptureParameter);
        animator.speed=motionPlaybackSpeed*globalSpeedMultiplier*CombatMotionRate;
        animator.SetTrigger(parameter);
    }

    private void ResolveAnimator()
    {
        if(specialClipAnimator==null)
            specialClipAnimator=GetComponent<Animator>();
        if(animator==null&&transform.parent!=null)
            animator=transform.parent.GetComponentInParent<Animator>(true);
        if(animator==null)animator=GetComponent<Animator>();
        if(animator==null)animator=GetComponentInChildren<Animator>(true);
        DisableAnimator(animator);
        if(specialClipAnimator!=animator)DisableAnimator(specialClipAnimator);
        animatorSuppressed=animator!=null||specialClipAnimator!=null;
    }

    private void SuppressAnimator()
    {
        ResolveAnimator();
        DisableAnimator(animator);
        if(specialClipAnimator!=animator)DisableAnimator(specialClipAnimator);
    }

    private void RestoreAnimator()
    {
        ResolveAnimator();
        DisableAnimator(animator);
        if(specialClipAnimator!=animator)DisableAnimator(specialClipAnimator);
    }

    private static void DisableAnimator(Animator value)
    {
        if(value!=null)value.enabled=false;
    }

    private void ApplyExplicitFrame()
    {
        if(statusFrozen){ApplyStatusFrozenPose();return;}
        if(beetleOverturnTime>=0f){ApplyBeetleOverturnFrame();return;}
        if(beetleFlipAttack){ApplyBeetleFlipFrame();return;}
        if(mammothChargeAttack){ApplyMammothChargeFrame();return;}
        if(mantisReaperAction){ApplyMantisReaperFrame();return;}
        if(mantisGuillotineAttack){ApplyMantisGuillotineFrame();return;}
        if(tyrannoMawAttack){ApplyTyrannoMawFrame();return;}
        if(tyrannoMarchAttack){ApplyTyrannoMarchFrame();return;}
        if(tyrannoStompAttack)
        {
            float t=tyrannoStompElapsed/tyrannoStompWindup;
            skillPoseScale=t<.18f?Vector3.Lerp(Vector3.one,new Vector3(1.04f,.92f,1f),t/.18f):
                t<.9f?Vector3.Lerp(new Vector3(1.04f,.92f,1f),new Vector3(.98f,1.04f,1f),(t-.18f)/.5f):
                t<1f?new Vector3(.98f,1.04f,1f):
                Vector3.Lerp(tyrannoExtinction?new Vector3(1.1f,.86f,1f):new Vector3(1.06f,.93f,1f),
                    Vector3.one,(tyrannoStompElapsed-tyrannoStompWindup-(tyrannoExtinction?.085f:.055f))/.17f);
        }
        if(wolfHuntAttack)
            skillPoseScale=requestedMotion=="SkillAttack"&&currentFrameIndex<skillAttackHitFrameIndex
                ?new Vector3(1.08f,.82f,1f):Vector3.one;
        if(bearSpiritAttack)
            skillPoseScale=requestedMotion=="SkillAttack"&&skillAttackHoldFrameIndex>=0&&
                skillAttackHoldFrameIndex<skillAttackHitFrameIndex
                ?new Vector3(1.08f,.86f,1f):
                bearAvatarRoar&&skillAttackHoldFrameIndex==skillAttackHitFrameIndex
                    ?new Vector3(1.06f,1.04f,1f):Vector3.one;
        if(spriteRenderer==null)
            spriteRenderer=GetComponent<SpriteRenderer>();
        if(spriteRenderer==null)return;

        if(skillAttackHoldFrameIndex>=0&&skillWindupClip!=null)
        {
            currentFrameIndex=Mathf.Min(
                skillAttackHoldFrameIndex,
                Mathf.FloorToInt(motionElapsed*skillAttackFramesPerSecond));
            SetSprite(skillAttackFrames[currentFrameIndex]);
            transform.localScale=skillPoseScale*ScaleFor("SkillAttack");
            return;
        }
        if(requestedMotion=="SkillAttack"&&
           skillImpactClipActive&&skillImpactClip!=null)
        {
            currentFrameIndex=Mathf.Clamp(
                skillAttackHitFrameIndex+
                Mathf.FloorToInt(motionElapsed*skillAttackFramesPerSecond),
                skillAttackHitFrameIndex,SkillAttackFrameCount-1);
            SetSprite(skillAttackFrames[currentFrameIndex]);
            transform.localScale=skillPoseScale*ScaleFor(requestedMotion);
            NotifySkillAttackHitFrameFromAnimation();
            return;
        }
        if(knockdownRemaining>0f&&knockdownClip!=null)
        {
            float fallTime=knockdownClip.length;
            currentFrameIndex=Mathf.Clamp(
                Mathf.RoundToInt(
                    Mathf.Clamp01(motionElapsed/Mathf.Max(.001f,fallTime))*
                    Mathf.Max(0,DeathFrameCount-1)),
                0,Mathf.Max(0,DeathFrameCount-1));
            SetSprite(deathFrames[currentFrameIndex]);
            transform.localScale=Vector3.one*ScaleFor("Death");
            return;
        }

        Sprite[] frames=FramesFor(requestedMotion);
        if(frames==null||frames.Length==0)return;

        float framesPerSecond=FramesPerSecondFor(requestedMotion);
        if(requestedMotion=="SkillAttack"&&skillAttackHoldFrameIndex>=0&&
           motionElapsed>=MotionFrameTime(
               "SkillAttack",skillAttackHoldFrameIndex))
            currentFrameIndex=skillAttackHoldFrameIndex;
        else
        {
            int absoluteFrame=Mathf.FloorToInt(
                Mathf.Max(0f,motionElapsed)*framesPerSecond);
            currentFrameIndex=Loops(requestedMotion)
                ?absoluteFrame%frames.Length
                :Mathf.Clamp(absoluteFrame,0,frames.Length-1);
        }
        if(requestedMotion=="SkillAttack"&&reverseSkillAttack)
            currentFrameIndex=frames.Length-1-currentFrameIndex;
        // Keep the attack timeline and hit event, with feet planted while the drawn wings move.
        bool standingWing=griffinWingAttack&&requestedMotion=="Attack"&&idleFrames.Length>0;
        SetSprite(standingWing?idleFrames[0]:frames[currentFrameIndex]);
        transform.localScale=
            skillPoseScale*ScaleFor(standingWing?"Idle":requestedMotion);
        if(BeetleSweepAttacking)
        {
            float contact=motionElapsed-BasicAttackHitTime;
            Vector3 low=new Vector3(1.10f,.82f,1f),swing=new Vector3(1.28f,.96f,1f);
            Vector3 pose=contact<0?Vector3.Lerp(Vector3.one,low,Mathf.Clamp01(motionElapsed/Mathf.Max(.01f,BasicAttackHitTime))):
                contact<.07f?swing:Vector3.Lerp(swing,Vector3.one,(contact-.07f)/.2f);
            transform.localScale=pose*ScaleFor("Attack");
        }
        if(requestedMotion=="Attack"&&
           currentFrameIndex>=basicAttackHitFrameIndex)
            NotifyBasicAttackHitFrameFromAnimation();
        else if(requestedMotion=="SkillAttack"&&!reverseSkillAttack&&
                currentFrameIndex>=skillAttackHitFrameIndex)
            NotifySkillAttackHitFrameFromAnimation();
        // Hit callbacks see the original source transform; the extra pose is presentation only.
        SampleReadabilityBasicAttackPose(out Vector3 attackOffset,out Vector3 attackScale);
        readabilityBasicPoseScaleApplied=attackScale!=Vector3.one;
        if(readabilityBasicPoseScaleApplied)
        {
            readabilityBasicPoseBaseScale=transform.localScale;
            readabilityBasicPoseAppliedScale=Vector3.Scale(readabilityBasicPoseBaseScale,attackScale);
            transform.localScale=readabilityBasicPoseAppliedScale;
        }
    }

    private void ApplyPinnedFrame()
    {
        if(pinnedFrameSprite==null)return;
        if(spriteRenderer==null)spriteRenderer=GetComponent<SpriteRenderer>();
        if(spriteRenderer!=null)spriteRenderer.sprite=BearAvatarFrame(pinnedFrameSprite);
    }

    public bool BearAvatarActive { get; private set; }
    private float bearAvatarScale=1f;

    public void SetBearAvatar(bool active)
    {
        if(BearAvatarActive==active)return;
        BearAvatarActive=active;
        Sprite[] spirit=PixelBearUpperBodyHologram.SpiritFrames("Idle",true);
        if(active&&idleFrames.Length>0&&spirit.Length>0)
            bearAvatarScale=FamilyTranscendenceRules.BearAvatarScale*
                idleFrames[0].bounds.size.y/spirit[0].bounds.size.y;
        // A status freeze keeps its original frame and clock, including on reversion.
        if(statusFrozen)
        {
            Sprite[] frames=active?PixelBearUpperBodyHologram.SpiritFrames(CurrentMotionName,true):FramesFor(CurrentMotionName);
            if(frames.Length>0)statusFrozenSprite=frames[Mathf.Clamp(currentFrameIndex,0,frames.Length-1)];
            statusFrozenScale=Vector3.one*ScaleFor(requestedMotion);
        }
        ApplyExplicitFrame();
    }

    private Sprite BearAvatarFrame(Sprite original)
    {
        if(!BearAvatarActive)return original;
        Sprite[] spirit=PixelBearUpperBodyHologram.SpiritFrames(CurrentMotionName,true);
        if(spirit.Length==0)return original;
        int count=FramesFor(CurrentMotionName).Length;
        int index=count<=1?0:Mathf.RoundToInt(currentFrameIndex*(spirit.Length-1f)/(count-1f));
        return spirit[Mathf.Clamp(index,0,spirit.Length-1)];
    }

    private void SetSprite(Sprite value)
    {
        value=BearAvatarFrame(value);
        if(impactHoldRemaining>0f&&impactHeldSprite!=null&&!terminal&&!statusFrozen&&knockdownRemaining<=0f)
            value=impactHeldSprite;
        if(spriteRenderer!=null&&spriteRenderer.sprite!=value)
            spriteRenderer.sprite=value;
    }

    private Sprite[] FramesFor(string motionName)
    {
        switch(motionName)
        {
            case "Run":return runFrames;
            case "Attack":return attackFrames;
            case "SkillAttack":return skillAttackFrames;
            case "Death":return deathFrames;
            case "Capture":return captureFrames;
            case "Struggle":return struggleFrames;
            default:return idleFrames;
        }
    }

    private static bool Loops(string motionName)
    {
        return motionName=="Idle"||
               motionName=="Run"||
               motionName=="Struggle";
    }

    private float skillFocusScale=1f;

    public void SetSkillFocus(float amount)
    {
        float maximum=1.5f;
        if(CombatReadabilityPresentation.Active)maximum=CombatReadabilityPresentation.SkillBodyScale;
        float scale=1f+(maximum-1f)*Mathf.Clamp01(amount);
        if(Mathf.Approximately(scale,skillFocusScale))return;
        float ratio=scale/skillFocusScale;
        skillFocusScale=scale;
        // Keep held poses and authored squash intact, including when the combat clock is stopped.
        if(statusFrozen)statusFrozenScale*=ratio;
        if(contactScaleApplied){contactBaseScale*=ratio;contactAppliedScale*=ratio;}
        transform.localScale*=ratio;
    }

    private float ScaleFor(string motionName)
    {
        float scale=skillFocusScale*AuthoredScaleFor(motionName);
        scale*=CombatReadabilityPresentation.BodyVisualScale;
        return scale;
    }

    private float AuthoredScaleFor(string motionName)
    {
        if(BearAvatarActive)return bearAvatarScale;
        switch(motionName)
        {
            case "Run":return 1.14f;
            case "Attack":return 1.15f;
            case "SkillAttack":return 1.24f;
            case "Death":return 1.08f;
            case "Capture":return 1.04f;
            case "Struggle":return 1.15f;
            default:return 1f;
        }
    }

    private float FramesPerSecondFor(string motionName)
    {
        switch(motionName)
        {
            case "Run":return runFramesPerSecond;
            case "Attack":return attackFramesPerSecond;
            case "SkillAttack":return skillAttackFramesPerSecond;
            case "Death":return deathFramesPerSecond;
            case "Capture":return captureFramesPerSecond;
            case "Struggle":return struggleFramesPerSecond;
            default:return idleFramesPerSecond;
        }
    }

    private float MotionDuration(string motionName)
    {
        Sprite[] frames=FramesFor(motionName);
        if(frames==null||frames.Length==0)return 0f;
        return frames.Length/Mathf.Max(1f,FramesPerSecondFor(motionName));
    }

    private float MotionFrameTime(string motionName,int frameIndex)
    {
        Sprite[] frames=FramesFor(motionName);
        if(frames==null||frames.Length==0)return 0f;
        int index=Mathf.Clamp(frameIndex,0,frames.Length-1);
        return index/Mathf.Max(1f,FramesPerSecondFor(motionName));
    }

    private void SetMotion(string motionName,bool forceRestart=false)
    {
        if(!forceRestart&&requestedMotion==motionName)return;
        if(readabilityBasicPoseActive&&!(readabilityBasicPoseContactShown&&
           readabilityBasicPoseContactFrame==Time.frameCount&&(motionName=="Idle"||motionName=="Run")))
            ClearReadabilityBasicAttackPose();
        requestedMotion=motionName;
        motionElapsed=0f;
        currentFrameIndex=0;
        ApplyExplicitFrame();
    }

    private void SetPlaybackSpeed(float value)
    {
        motionPlaybackSpeed=Mathf.Clamp(value,0.1f,6f);
        if(animator!=null)
            animator.speed=statusFrozen||IsSkillAnticipating?0f:motionPlaybackSpeed*globalSpeedMultiplier*CombatMotionRate;
    }

    private void CancelPendingBasicAttack()
    {
        if(readabilityBasicPoseActive||readabilityBasicPoseScaleApplied)ClearAppliedRecoil();
        ClearReadabilityBasicAttackPose();
        griffinWingAttack=false;
        beetleSweepAttack=false;
        if(bearRagingAttack){ClearAppliedRecoil();skillPoseScale=Vector3.one;}
        bearRagingAttack=false;
        bearRagingHit=null;
        pendingBasicAttackHit=null;
        basicAttackHitDispatched=false;
    }

    private void CancelPendingSkillAttack()
    {
        if(imperialHornFrames!=null)
        {
            imperialHornFrames=null;transform.localPosition=imperialHornBasePosition;transform.localScale=imperialHornBaseScale;
        }
        beetleOverturnTime=-1f;
        beetleFlipAttack=false;beetleCrushAttack=false;beetleFlipValid=null;beetleFlipAdvance=null;
        Action beetleFinished=beetleFlipFinished;beetleFlipFinished=null;
        mammothChargeAttack=false;mammothTossAttack=false;mammothWallRushAttack=false;mammothBraceAttack=false;mammothExpulsionAttack=false;mammothChargeValid=null;mammothChargeAdvance=null;
        Action mammothFinished=mammothChargeFinished;mammothChargeFinished=null;
        mantisReaperAction=false;
        Action reaperCancelled=mantisReaperCancelled;mantisReaperCancelled=null;
        if(mantisCrossAttack){mantisCrossAttack=false;skillPoseScale=Vector3.one;}
        monkeyThrowRemaining=0f;
        raptorStrikeRemaining=0f;
        mantisGuillotineAttack=false;mantisGuillotineValid=null;mantisGuillotineAdvance=null;
        Action guillotineFinished=mantisGuillotineFinished;mantisGuillotineFinished=null;
        tyrannoMawAttack=false;tyrannoMawValid=null;tyrannoMawAdvance=null;
        Action mawFinished=tyrannoMawFinished;tyrannoMawFinished=null;
        tyrannoMarchAttack=false;tyrannoMarchValid=null;tyrannoMarchAdvance=null;
        Action marchFinished=tyrannoMarchFinished;tyrannoMarchFinished=null;
        RestoreNinjaVisibility();
        ninjaShadowAttack=false;ninjaShadowValid=null;
        ninjaVanish=null;ninjaSubstitute=null;ninjaArrive=null;
        ninjaCrossAttack=false;ninjaCrossMove=null;ninjaCrossHit=null;ninjaCrossDepart=null;
        ninjaMoonApproachOffset=Vector3.zero;
        Action crossFinished=ninjaCrossFinished;ninjaCrossFinished=null;
        ninjaEightGateAttack=false;ninjaGateAdvance=null;
        Action gateFinished=ninjaGateFinished;ninjaGateFinished=null;
        ninjaCounterAttack=false;
        Action counterFinished=ninjaCounterFinished;ninjaCounterFinished=null;
        ClearWorldOverturnCharge();
        worldOverturnAttack=false;
        bearSpiritAttack=false;
        bearAvatarRoar=false;
        wolfHuntAttack=false;
        tyrannoStompAttack=false;tyrannoStompElapsed=0f;
        earthPulseAttack=false;
        earthPulseElapsed=0f;
        earthPulseStrike=0;
        earthPulseFollowupHit=null;
        earthPulseDownstroke=null;
        deathRollFrame=false;
        ClearAppliedRecoil();
        impactRecoilRemaining=0f;
        skillPoseScale=Vector3.one;
        if(skillAttackHoldFrameIndex>=0)
            transform.localPosition=skillAttackHoldPosition;
        pendingSkillAttackHit=null;
        skillAttackHitDispatched=false;
        skillAttackRecoveryPlaybackSpeed=1f;
        reverseSkillAttack=false;
        skillAttackHoldFrameIndex=-1;
        skillAttackHoldRemaining=0f;
        skillImpactClipActive=false;
        skillAttackClock=null;
        pinnedFrameSprite=null;
        StopSpecialClip();
        crossFinished?.Invoke();
        gateFinished?.Invoke();
        counterFinished?.Invoke();
        marchFinished?.Invoke();
        mawFinished?.Invoke();
        guillotineFinished?.Invoke();
        reaperCancelled?.Invoke();
        mammothFinished?.Invoke();
        beetleFinished?.Invoke();
    }

    private bool IsPoseLocked =>
        skillAttackHoldFrameIndex>=0||knockdownRemaining>0f;

    private void StopSpecialClip()
    {
        if(specialClipAnimator==null)return;
        specialClipAnimator.speed=0f;
        specialClipAnimator.enabled=false;
    }

    private void CancelPendingHitEvents()
    {
        CancelPendingBasicAttack();
        CancelPendingSkillAttack();
    }
}
