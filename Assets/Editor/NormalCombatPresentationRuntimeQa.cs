using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class NormalCombatPresentationRuntimeQa
{
    public static void Attach(string label)
    {
        SkillFocusRuntimeQa.Guard();
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        var primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        if(primary==null||(string)typeof(SkillFocusRuntimeQaObserver).GetField("label",flags).GetValue(primary)!=label)
            throw new InvalidOperationException("Launch the primary observer with the same label first.");
        if((bool)typeof(SkillFocusRuntimeQaObserver).GetField("prepared",flags).GetValue(primary))
            throw new InvalidOperationException("Attach before combat preparation.");
        if(primary.GetComponent<NormalCombatPresentationRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("Normal-combat observer already attached.");
        string output=Path.GetFullPath(Path.Combine(label.Contains("anticipation")?
            "Logs/SkillAnticipationQA/20261020":"Logs/NormalCombatPresentationQA/20261019",label));
        if(Directory.Exists(output))throw new IOException("QA output already exists.");
        primary.gameObject.AddComponent<NormalCombatPresentationRuntimeQaObserver>().Begin(primary,label,output);
    }
}

// Read-only natural battle evidence. Native PNG capture and diagnostic I/O are not an FPS benchmark.
[DefaultExecutionOrder(10080)]
public sealed class NormalCombatPresentationRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private readonly Dictionary<object,Actor> actors=new Dictionary<object,Actor>();
    private readonly Dictionary<string,float> previousPenetrations=new Dictionary<string,float>();
    private readonly List<Cast> casts=new List<Cast>();
    private readonly List<Preparation> preparations=new List<Preparation>();
    private readonly List<Shake> shakes=new List<Shake>();
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private Camera camera;
    private MethodInfo actionMethod;
    private StreamWriter unitCsv,worldCsv,pairCsv;
    private string label,output;
    private bool begun,cameraCaptured,previousWorldValid,anticipationScenario;
    private float expectedSpeed,previousWorld,homeFov,lastShakeStart=-1f;
    private Vector2 homeLens;
    private Vector3 homeCameraPosition;
    private Quaternion homeCameraRotation;
    private Matrix4x4 homeProjection;
    private double startedAt;
    private readonly Report report=new Report();
    public bool Finished {get;private set;}

    private sealed class Actor
    {
        public int uses,frame;
        public Vector3 position,slide;
        public float cooldown,skillTimer,motionAge,hp;
        public bool alive,observed,pending;
        public double presentationStarted;
        public Cast cast;
        public Preparation preparation;
    }
    private sealed class Snapshot
    {
        public object unit,presentation;
        public int id,uses,driverFrame,mirrorOrder,sourceOrder;
        public string team,species,motion;
        public bool alive,eligible,action,gold,mirror,ordinary,pending,anticipatingDriver,skillBody,noticeStyle;
        public int gatherCached,gatherVisible;
        public float hp,cooldown,skillTimer,motionAge,scale,radius;
        public Vector3 position,slide;
        public double presentationStarted,recoveryAt,committedAt;
    }
    [Serializable] private sealed class Cast
    {
        public int id,uses,frame,fullBodySamples,bodySamples,preparationSequence;
        public string team,species,end="OBSERVING";
        public double at,presentationStarted=-1d,firstBodyEndedAt=-1d,endedAt=-1d,committedAt=-1d,ownAnticipationSeconds=-1d;
        public bool ownRecord,freshRecord,initialScale,initialGold,initialMirror,initialFront;
    }
    [Serializable] private sealed class Shake
    {public float startedAt,until,duration,amplitudePixels;}
    [Serializable] private sealed class Preparation
    {
        public int sequence,id,startUses,pendingSamples,committedUses,flagSamples,fullVisualSamples,gatherSamples,noticeStyleSamples;
        public int earlyUses,skillTimerIncreaseSamples,bodyBeforeCommitSamples,positionComparisons,positionChangeSamples;
        public string team,species,state="PENDING";
        public double startedAt,firstSeenScaled,committedAt=-1d,commitElapsed=-1d,endedAt=-1d;
        public float startSkillTimer,maximumPositionDelta;
    }
    [Serializable] private sealed class Report
    {
        public string label,completion,primaryResult,result;
        public string limitation="Read-only isolated-profile natural battle. Anticipation timing uses each local record's scaled startedAt/committedAt; native sampling can miss transitions and does not establish exact rendered duration. Pending cancellation is allowed when the source/target becomes invalid or controlled. Later reactive actions are not linked to an expired cast. Native capture is not an FPS benchmark. Contact metrics exclude deliberate aerial/forced motion.";
        public int frames,rows,startingUnits,eligibleUses,playerUses,enemyUses,missingOwnRecords,reusedPresentationRecords;
        public int initialVisualFailures,activeBodySamples,activeBodyVisualFailures,completedPresentationRecords,partialPresentationRecords;
        public int timeScaleViolations,exclusiveClockFrames,shadeFrames,worldComparisons,worldProgressSamples,worldNonProgressSamples;
        public int emphasisFrames,otherActorComparisons,otherActorProgressSamples,otherCooldownProgressSamples;
        public int hitSlideSamples,hitSlideStarts,hitSlideMovementSamples,hitSlideDecelerationSamples,hitSlideSpeedViolations;
        public int slidePairComparisons,slideOverlapSamples,newOrWorseSlideOverlapSamples,ordinaryOverlapSamples;
        public int cameraPoseViolations,cameraLensViolations,cameraProjectionViolations,shakeActiveSamples,shakeRestoredSamples,shakeExpiredOffsetViolations;
        public int cameraPanSamples,cameraPanReturningSamples;
        public int cameraZoomSamples,cameraZoomReturningSamples,cameraZoomRestoredSamples,cameraZoomRangeViolations,cameraCropBoundsViolations;
        public float maximumCameraPanDistance;
        public float maximumCameraZoom=1f,finalCameraZoom=1f,maximumCameraCropOvershoot;
        public int basicHitPresetShakes,skillHitPresetShakes,criticalHitPresetShakes;
        public int pendingSamples,pendingUseViolations,pendingSkillTimerIncreaseSamples,pendingBodyViolations,pendingPositionComparisons,pendingPositionChanges;
        public int pendingFlagMissingSamples,ownAnticipationTooShort,missingPreparationEvidence,maximumConcurrentPending;
        public int pendingWorldProgressSamples,pendingOtherActorProgressSamples,cancelledPreparations,partialPreparations;
        public int maximumConcurrentEmphasis;
        public float initialSpeed,maximumSlideSpeed,maximumSlidePenetration,maximumProjectionError,maximumShakePixels,maximumShakeDuration;
        public bool slideContractPresent,attachedBeforePreparation;
        public double realSeconds,minimumPreparationSeconds;
        public Cast[] casts;
        public Shake[] shakes;
        public Preparation[] preparations;
    }

    public void Begin(SkillFocusRuntimeQaObserver owner,string requestedLabel,string directory)
    {
        primary=owner;label=requestedLabel;output=directory;Directory.CreateDirectory(output);
        unitCsv=Writer("samples.csv","frame,time,world,timeScale,id,team,species,alive,hp,uses,castDelta,action,motion,driverFrame,motionAge,x,y,z,cooldown,skillTimer,presentation,started,recoveryAt,scale,gold,mirror,mirrorOrder,sourceOrder,slideX,slideZ,slideSpeed,radius,scaledTime,pending,committedAt,anticipationElapsed,anticipatingDriver,skillBody,gatherCached,gatherVisible,noticeStyleReady");
        worldCsv=Writer("world.csv","frame,time,world,worldDelta,timeScale,deltaTime,emphasized,otherProgress,exclusiveClock,shade,fov,lensX,lensY,shakeX,shakeY,shakeStarted,shakeUntil,shakeAmplitude,projectionError,scaledTime,pending,appliedPanX,appliedPanY,appliedZoom,panActive,panReturning,uvMinX,uvMinY,uvMaxX,uvMaxY");
        pairCsv=Writer("contacts.csv","frame,time,idA,idB,slideA,slideB,distance,radiusSum,penetration,previousPenetration,newOrWorse");
        report.label=label;report.attachedBeforePreparation=true;anticipationScenario=label.Contains("anticipation");
        var preparationDuration=typeof(MonsterAutoBattlePrototype).GetField("SkillAnticipationDuration",Flags);
        if(preparationDuration==null)throw new InvalidOperationException("Production preparation duration is missing.");
        report.minimumPreparationSeconds=Convert.ToDouble(preparationDuration.GetRawConstantValue());
        if(report.minimumPreparationSeconds<=0d)throw new InvalidOperationException("Production preparation duration must be positive.");
        expectedSpeed=Time.timeScale;report.initialSpeed=expectedSpeed;startedAt=Time.unscaledTimeAsDouble;begun=true;
    }

    private StreamWriter Writer(string name,string header)
    {var writer=new StreamWriter(Path.Combine(output,name),false,new UTF8Encoding(false),65536);writer.WriteLine(header);return writer;}

    private void LateUpdate()
    {
        if(!begun||Finished)return;
        try
        {
            if(primary==null){Complete("PRIMARY_MISSING");return;}
            battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
            var units=Field(battle,"units") as IList;
            bool done=Read<bool>(primary,"finished",false);
            if(units!=null&&Read<bool>(primary,"prepared",false)&&(Read<bool>(battle,"battleStarted",false)||done))Observe(units,done);
            if(done)Complete("PRIMARY_FINISHED");
        }
        catch(Exception error)
        {File.WriteAllText(Path.Combine(output,"exception.txt"),error.ToString());Complete("OBSERVER_EXCEPTION");Debug.LogException(error);}
    }

    private void Observe(IList units,bool done)
    {
        report.frames++;if(report.startingUnits==0)report.startingUnits=units.Count;
        double now=Time.unscaledTimeAsDouble,scaledNow=Time.timeAsDouble;float world=Read<float>(battle,"battleElapsed",0f);
        if(Mathf.Abs(Time.timeScale-expectedSpeed)>.0001f)report.timeScaleViolations++;
        if(CombatSkillClock.Active)report.exclusiveClockFrames++;
        var shade=Read<SpriteRenderer>(battle,"skillFocusShade",null);
        bool dark=shade!=null&&shade.enabled&&shade.gameObject.activeInHierarchy&&shade.color.a>.01f;
        if(dark)report.shadeFrames++;
        float worldDelta=previousWorldValid?world-previousWorld:0f;
        if(previousWorldValid&&!done&&Time.deltaTime>0f)
        {report.worldComparisons++;if(worldDelta>0f)report.worldProgressSamples++;else report.worldNonProgressSamples++;}
        previousWorld=world;previousWorldValid=true;
        if(actionMethod==null)actionMethod=battle.GetType().GetMethod("CommonSkillActionActive",Flags);
        var snapshots=new List<Snapshot>();int emphasized=0,pending=0;
        foreach(object unit in units)
        {var sample=Sample(unit);snapshots.Add(sample);if(sample.presentation!=null)emphasized++;if(sample.pending)pending++;}
        if(emphasized>0)report.emphasisFrames++;
        report.maximumConcurrentEmphasis=Mathf.Max(report.maximumConcurrentEmphasis,emphasized);
        report.maximumConcurrentPending=Mathf.Max(report.maximumConcurrentPending,pending);
        if(pending>0&&worldDelta>0f)report.pendingWorldProgressSamples++;
        int otherProgress=0;
        foreach(var sample in snapshots)
        {
            Actor old;if(!actors.TryGetValue(sample.unit,out old)){old=new Actor();actors.Add(sample.unit,old);}
            int delta=old.observed?sample.uses-old.uses:sample.uses;
            if(anticipationScenario)ObservePreparation(sample,old,scaledNow);
            if(sample.eligible&&delta>0)
            {
                report.eligibleUses+=delta;if(sample.team=="Player")report.playerUses+=delta;else report.enemyUses+=delta;
                for(int use=0;use<delta;use++)
                {
                    if(old.cast!=null)EndCast(old.cast,"REPLACED_BY_NEW_CAST",now);
                    double priorStarted=old.cast==null?-1d:old.cast.presentationStarted;
                    var preparation=old.preparation;
                    var cast=new Cast{id=sample.id,uses=sample.uses,frame=Time.frameCount,team=sample.team,species=sample.species,
                        at=now,presentationStarted=sample.presentationStarted,ownRecord=sample.presentation!=null,
                        freshRecord=anticipationScenario?preparation!=null&&preparation.startedAt==sample.presentationStarted&&preparation.committedUses==0:
                            sample.presentationStarted>priorStarted&&Math.Abs(sample.presentationStarted-now)<.00001d,
                        initialScale=sample.scale>=1.499f,initialGold=sample.gold,initialMirror=sample.mirror,
                        initialFront=sample.mirror&&sample.mirrorOrder>sample.sourceOrder,
                        preparationSequence=anticipationScenario&&preparation!=null?preparation.sequence:0,
                        committedAt=sample.committedAt,ownAnticipationSeconds=sample.committedAt<0d?-1d:sample.committedAt-sample.presentationStarted};
                    casts.Add(cast);old.cast=cast;
                    if(anticipationScenario)
                    {
                        if(preparation==null||preparation.startedAt!=sample.presentationStarted||preparation.pendingSamples==0)report.missingPreparationEvidence++;
                        if(cast.ownAnticipationSeconds<report.minimumPreparationSeconds-.0001d)report.ownAnticipationTooShort++;
                        if(preparation!=null&&preparation.startedAt==sample.presentationStarted)
                        {preparation.committedUses++;preparation.committedAt=sample.committedAt;preparation.commitElapsed=cast.ownAnticipationSeconds;preparation.state="COMMITTED";}
                    }
                    if(!cast.ownRecord)report.missingOwnRecords++;
                    if(cast.ownRecord&&!cast.freshRecord)report.reusedPresentationRecords++;
                    if(!cast.initialScale||!cast.initialGold||!cast.initialMirror||!cast.initialFront)report.initialVisualFailures++;
                }
            }
            if(old.cast!=null)
            {
                var cast=old.cast;
                if(!sample.alive){EndCast(cast,"SOURCE_INVALID",now);old.cast=null;}
                else if(sample.presentation==null||sample.presentationStarted!=cast.presentationStarted)
                {EndCast(cast,"RECORD_CLEARED",now);old.cast=null;}
                else if(sample.action&&sample.recoveryAt<0d)
                {
                    cast.bodySamples++;report.activeBodySamples++;
                    if(sample.scale>=1.499f&&sample.gold&&sample.mirror&&sample.mirrorOrder>sample.sourceOrder)cast.fullBodySamples++;
                    else report.activeBodyVisualFailures++;
                }
                else if(!sample.action&&cast.firstBodyEndedAt<0d)cast.firstBodyEndedAt=now;
            }
            float slide=sample.slide.magnitude;report.maximumSlideSpeed=Mathf.Max(report.maximumSlideSpeed,slide);
            if(slide>.001f)
            {
                report.hitSlideSamples++;if(slide>3.3001f)report.hitSlideSpeedViolations++;
                if(!old.observed||old.slide.sqrMagnitude<.000001f)report.hitSlideStarts++;
                if(old.observed&&(sample.position-old.position).sqrMagnitude>.00000001f)report.hitSlideMovementSamples++;
                if(old.observed&&slide<old.slide.magnitude-.00001f)report.hitSlideDecelerationSamples++;
            }
            if(old.observed&&old.alive&&sample.alive&&emphasized>0&&sample.presentation==null&&!done)
            {
                report.otherActorComparisons++;
                bool progress=(sample.position-old.position).sqrMagnitude>.00000001f||Mathf.Abs(sample.motionAge-old.motionAge)>.00001f||sample.driverFrame!=old.frame;
                if(progress){report.otherActorProgressSamples++;otherProgress++;}
                if(sample.cooldown<old.cooldown-.00001f)report.otherCooldownProgressSamples++;
            }
            if(old.observed&&old.alive&&sample.alive&&pending>0&&!sample.pending&&!done&&
               ((sample.position-old.position).sqrMagnitude>.00000001f||Mathf.Abs(sample.motionAge-old.motionAge)>.00001f||sample.driverFrame!=old.frame))
                report.pendingOtherActorProgressSamples++;
            Write(unitCsv,Time.frameCount,now,world,Time.timeScale,sample.id,sample.team,sample.species,sample.alive,sample.hp,sample.uses,delta,
                Read<string>(sample.unit,"currentAction",""),sample.motion,sample.driverFrame,sample.motionAge,sample.position.x,sample.position.y,sample.position.z,
                sample.cooldown,sample.skillTimer,sample.presentation!=null,sample.presentationStarted,sample.recoveryAt,sample.scale,sample.gold,sample.mirror,
                sample.mirrorOrder,sample.sourceOrder,sample.slide.x,sample.slide.z,slide,sample.radius,scaledNow,sample.pending,
                sample.committedAt,sample.presentationStarted<0d?-1d:scaledNow-sample.presentationStarted,sample.anticipatingDriver,sample.skillBody,
                sample.gatherCached,sample.gatherVisible,sample.noticeStyle);
            old.observed=true;old.uses=sample.uses;old.position=sample.position;old.slide=sample.slide;old.motionAge=sample.motionAge;
            old.cooldown=sample.cooldown;old.skillTimer=sample.skillTimer;old.frame=sample.driverFrame;old.alive=sample.alive;old.hp=sample.hp;report.rows++;
            old.pending=sample.pending;old.presentationStarted=sample.presentationStarted;
        }
        ObserveContacts(snapshots,now);
        ObserveCamera(now,world,worldDelta,emphasized,otherProgress,dark,pending);
    }

    private void ObservePreparation(Snapshot sample,Actor old,double now)
    {
        var preparation=old.preparation;
        if(preparation!=null&&preparation.startedAt!=sample.presentationStarted&&preparation.endedAt<0d)
        {
            preparation.endedAt=now;
            if(preparation.committedUses==0){preparation.state="CANCELLED_OR_INVALIDATED";report.cancelledPreparations++;}
            else preparation.state="COMMITTED_THEN_RELEASED";
        }
        if(!sample.pending)return;
        if(preparation==null||preparation.startedAt!=sample.presentationStarted)
        {
            preparation=new Preparation{sequence=preparations.Count+1,id=sample.id,team=sample.team,species=sample.species,
                startedAt=sample.presentationStarted,firstSeenScaled=now,startUses=old.observed?old.uses:sample.uses,startSkillTimer=sample.skillTimer};
            preparations.Add(preparation);old.preparation=preparation;
        }
        preparation.pendingSamples++;report.pendingSamples++;
        if(sample.uses!=preparation.startUses){preparation.earlyUses++;report.pendingUseViolations++;}
        if(sample.skillTimer>preparation.startSkillTimer+.0001f){preparation.skillTimerIncreaseSamples++;report.pendingSkillTimerIncreaseSamples++;}
        if(sample.skillBody){preparation.bodyBeforeCommitSamples++;report.pendingBodyViolations++;}
        if(sample.anticipatingDriver)preparation.flagSamples++;else report.pendingFlagMissingSamples++;
        if(sample.scale>=1.499f&&sample.gold&&sample.mirror&&sample.mirrorOrder>sample.sourceOrder)preparation.fullVisualSamples++;
        if(sample.gatherCached==3&&sample.gatherVisible>0)preparation.gatherSamples++;
        if(sample.noticeStyle)preparation.noticeStyleSamples++;
        if(old.observed&&old.pending&&old.presentationStarted==sample.presentationStarted&&sample.alive&&old.alive)
        {
            report.pendingPositionComparisons++;preparation.positionComparisons++;
            float distance=(sample.position-old.position).magnitude;
            preparation.maximumPositionDelta=Mathf.Max(preparation.maximumPositionDelta,distance);
            if(distance>.0001f){preparation.positionChangeSamples++;report.pendingPositionChanges++;}
        }
    }

    private Snapshot Sample(object unit)
    {
        var driver=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);
        var model=Read<GameObject>(unit,"model",null);var source=driver==null?null:driver.GetComponent<SpriteRenderer>();
        object presentation=null;var pool=Field(battle,"localSkillPresentations") as IEnumerable;
        if(pool!=null)foreach(object item in pool)
            if(Read<bool>(item,"active",false)&&ReferenceEquals(Field(item,"unit"),unit)){presentation=item;break;}
        var mirror=Read<SkillFocusSpriteMirror>(presentation,"mirror",null);var foreground=mirror==null?null:mirror.GetComponent<SpriteRenderer>();
        var gather=Read<SpriteRenderer[]>(mirror,"gatherSprites",null);int visibleGather=0;
        if(gather!=null)foreach(var sprite in gather)if(sprite!=null&&sprite.enabled&&sprite.gameObject.activeInHierarchy)visibleGather++;
        var outline=Read<ScreenSpaceOutlineTarget>(unit,"outline",null);Color color=Read<Color>(outline,"color",Color.clear);
        Color gold=CombatFlashPresentation.Peak(new Color(1f,.86f,.32f,1f));
        var definition=Read<MonsterDefinition>(unit,"definition",null);
        bool alive=Read<bool>(unit,"alive",false)&&Read<float>(unit,"hp",0f)>0f&&model!=null&&model.activeInHierarchy;
        report.slideContractPresent|=unit.GetType().GetField("hitSlideVelocity",Flags)!=null;
        return new Snapshot{unit=unit,presentation=presentation,id=Read<int>(unit,"id",-1),uses=Read<int>(unit,"skillUses",0),
            pending=Field(presentation,"pending")!=null,committedAt=Read<double>(presentation,"committedAt",-1d),
            anticipatingDriver=driver!=null&&(bool)(driver.GetType().GetProperty("IsSkillAnticipating",Flags)?.GetValue(driver)??false),
            skillBody=driver!=null&&driver.IsSkillActionActive,gatherCached=gather==null?0:gather.Length,gatherVisible=visibleGather,
            noticeStyle=Field(battle,"skillFocusNoticeStyle")!=null,
            team=Field(unit,"team").ToString(),species=definition==null?"":definition.monsterId,alive=alive,
            eligible=driver!=null&&Field(unit,"spiritConcertOwner")==null&&!Read<bool>(unit,"augmentShadowClone",false)&&Field(unit,"mantisBroodOwner")==null&&!Read<bool>(unit,"rank3MycokingSummon",false),
            action=actionMethod!=null&&(bool)actionMethod.Invoke(actionMethod.IsStatic?null:battle,new[]{unit}),
            gold=outline!=null&&outline.IsOutlined&&Read<bool>(outline,"emphasized",false)&&Mathf.Abs(color.r-gold.r)<.01f&&Mathf.Abs(color.g-gold.g)<.01f&&Mathf.Abs(color.b-gold.b)<.01f,
            mirror=foreground!=null&&foreground.enabled&&foreground.gameObject.activeInHierarchy,
            mirrorOrder=foreground==null?-1:foreground.sortingOrder,sourceOrder=source==null?-1:source.sortingOrder,
            scale=Read<float>(driver,"skillFocusScale",1f),hp=Read<float>(unit,"hp",0f),cooldown=Read<float>(unit,"cooldown",0f),skillTimer=Read<float>(unit,"skillTimer",0f),
            position=model==null?Vector3.zero:model.transform.position,slide=Read<Vector3>(unit,"hitSlideVelocity",Vector3.zero),radius=Read<float>(unit,"collisionRadius",0f),
            motion=driver==null?"":driver.CurrentMotionName,driverFrame=driver==null?-1:driver.CurrentFrameIndex,motionAge=Read<float>(driver,"motionElapsed",0f),
            presentationStarted=Read<double>(presentation,"startedAt",-1d),recoveryAt=Read<double>(presentation,"recoveryAt",-1d),
            ordinary=alive&&!Read<bool>(unit,"displacing",false)&&!Read<bool>(unit,"leaping",false)&&!Read<bool>(unit,"shadowSlashing",false)&&!Read<bool>(unit,"griffinSkyfalling",false)&&
                !Read<bool>(unit,"frogHopping",false)&&!Read<bool>(unit,"crocodilePreyFlying",false)&&Field(unit,"preyDraggedBy")==null&&Field(unit,"griffinHeldBy")==null&&Field(unit,"beetleFlippedBy")==null&&Field(unit,"monkeyNimbus")==null&&Field(unit,"mammothCharge")==null};
    }

    private void ObserveContacts(List<Snapshot> samples,double now)
    {
        for(int i=0;i<samples.Count;i++)for(int j=i+1;j<samples.Count;j++)
        {
            var a=samples[i];var b=samples[j];if(!a.ordinary||!b.ordinary)continue;
            Vector3 difference=a.position-b.position;difference.y=0f;
            float distance=difference.magnitude,sum=a.radius+b.radius,penetration=Mathf.Max(0f,sum-distance);
            string key=Mathf.Min(a.id,b.id)+":"+Mathf.Max(a.id,b.id);float previous;previousPenetrations.TryGetValue(key,out previous);
            bool slide=a.slide.sqrMagnitude>.000001f||b.slide.sqrMagnitude>.000001f;
            bool worsening=slide&&penetration>.025f&&penetration>previous+.025f;
            if(penetration>.025f)report.ordinaryOverlapSamples++;
            if(slide)
            {
                report.slidePairComparisons++;report.maximumSlidePenetration=Mathf.Max(report.maximumSlidePenetration,penetration);
                if(penetration>.025f)report.slideOverlapSamples++;if(worsening)report.newOrWorseSlideOverlapSamples++;
            }
            if(slide||penetration>.025f)Write(pairCsv,Time.frameCount,now,a.id,b.id,a.slide.magnitude,b.slide.magnitude,distance,sum,penetration,previous,worsening);
            previousPenetrations[key]=penetration;
        }
    }

    private void ObserveCamera(double now,float world,float worldDelta,int emphasized,int otherProgress,bool shade,int pending)
    {
        if(director==null)director=Read<CombatCameraDirector>(battle,"combatCameraDirector",null);
        if(camera==null)camera=Read<Camera>(battle,"mainCamera",null);if(camera==null)return;
        Vector2 shake=Read<Vector2>(director,"appliedShakeOffset",Vector2.zero);
        Vector2 appliedPan=director==null?Vector2.zero:director.AppliedAnticipationPanOffset;
        float zoom=director==null?1f:director.AppliedAnticipationZoom;
        bool panActive=director!=null&&director.AnticipationPanActive,returning=Read<bool>(director,"anticipationPanReturning",false);
        if(float.IsNaN(zoom)||float.IsInfinity(zoom)||zoom<.9999f||zoom>1.1401f)report.cameraZoomRangeViolations++;
        float safeZoom=float.IsNaN(zoom)||float.IsInfinity(zoom)||zoom<=0f?1f:zoom;
        report.finalCameraZoom=zoom;report.maximumCameraZoom=Mathf.Max(report.maximumCameraZoom,zoom);
        if(zoom>1.0001f){report.cameraZoomSamples++;if(!panActive)report.cameraZoomReturningSamples++;}
        if(!panActive&&!returning&&Mathf.Abs(zoom-1f)<.0001f)report.cameraZoomRestoredSamples++;
        Vector2 projectionOffset=shake+appliedPan;
        // Actual projection is T(offset) * S(zoom) * base: remove translation first, then scale.
        Matrix4x4 projection=Matrix4x4.Scale(new Vector3(1f/safeZoom,1f/safeZoom,1f))*
            Matrix4x4.Translate(new Vector3(-projectionOffset.x*2f,-projectionOffset.y*2f,0f))*camera.projectionMatrix;
        Vector2 uvMin=Vector2.one*((1f-1f/safeZoom)*.5f)-projectionOffset/safeZoom;
        Vector2 uvMax=Vector2.one*((1f+1f/safeZoom)*.5f)-projectionOffset/safeZoom;
        float overshoot=Mathf.Max(0f,Mathf.Max(Mathf.Max(-uvMin.x,-uvMin.y),Mathf.Max(uvMax.x-1f,uvMax.y-1f)));
        if(panActive||returning)
        {report.maximumCameraCropOvershoot=Mathf.Max(report.maximumCameraCropOvershoot,overshoot);if(overshoot>.0001f)report.cameraCropBoundsViolations++;}
        Vector3 pan=director==null?Vector3.zero:director.AnticipationPanOffset;
        Vector3 cameraBase=camera.transform.position;
        if(pan.sqrMagnitude>.00000001f)
        {
            report.cameraPanSamples++;
            report.maximumCameraPanDistance=Mathf.Max(report.maximumCameraPanDistance,pan.magnitude);
            if(!director.AnticipationPanActive)report.cameraPanReturningSamples++;
        }
        if(!cameraCaptured)
        {cameraCaptured=true;homeFov=camera.fieldOfView;homeLens=camera.lensShift;homeCameraPosition=cameraBase;homeCameraRotation=camera.transform.rotation;homeProjection=projection;}
        if((cameraBase-homeCameraPosition).sqrMagnitude>.00000001f||Quaternion.Angle(camera.transform.rotation,homeCameraRotation)>.001f)report.cameraPoseViolations++;
        if(Mathf.Abs(camera.fieldOfView-homeFov)>.0001f||(camera.lensShift-homeLens).sqrMagnitude>.00000001f)report.cameraLensViolations++;
        float error=0f;for(int i=0;i<16;i++)error=Mathf.Max(error,Mathf.Abs(projection[i]-homeProjection[i]));
        report.maximumProjectionError=Mathf.Max(report.maximumProjectionError,error);if(error>.0001f)report.cameraProjectionViolations++;
        float start=Read<float>(director,"shakeStartedAt",0f),until=Read<float>(director,"shakeUntil",0f),amplitude=Read<float>(director,"shakeAmplitudePixels",0f);
        if(start!=lastShakeStart&&until>start)
        {
            lastShakeStart=start;float duration=until-start;
            shakes.Add(new Shake{startedAt=start,until=until,duration=duration,amplitudePixels=amplitude});
            report.maximumShakeDuration=Mathf.Max(report.maximumShakeDuration,duration);report.maximumShakePixels=Mathf.Max(report.maximumShakePixels,amplitude);
            // Compatible presets, not an instrumented ReportHitImpact call count; bespoke skill shakes remain separate.
            if(Mathf.Abs(amplitude-2f)<.001f&&Mathf.Abs(duration-.065f)<.003f)report.basicHitPresetShakes++;
            if(Mathf.Abs(amplitude-7f)<.001f&&Mathf.Abs(duration-.14f)<.003f)report.skillHitPresetShakes++;
            if(Mathf.Abs(amplitude-9f)<.001f&&Mathf.Abs(duration-.16f)<.003f)report.criticalHitPresetShakes++;
        }
        if(shake!=Vector2.zero)report.shakeActiveSamples++;
        if(Time.unscaledTime>=until)
        {if(shake==Vector2.zero)report.shakeRestoredSamples++;else report.shakeExpiredOffsetViolations++;}
        Write(worldCsv,Time.frameCount,now,world,worldDelta,Time.timeScale,Time.deltaTime,emphasized,otherProgress,CombatSkillClock.Active,shade,
            camera.fieldOfView,camera.lensShift.x,camera.lensShift.y,shake.x,shake.y,start,until,amplitude,error,Time.timeAsDouble,pending,
            appliedPan.x,appliedPan.y,zoom,panActive,returning,uvMin.x,uvMin.y,uvMax.x,uvMax.y);
    }

    private void EndCast(Cast cast,string reason,double now)
    {if(cast.end!="OBSERVING")return;cast.end=reason;cast.endedAt=now;report.completedPresentationRecords++;}

    private void Complete(string completion)
    {
        if(Finished)return;Finished=true;
        foreach(var actor in actors.Values)if(actor.cast!=null&&actor.cast.end=="OBSERVING")report.partialPresentationRecords++;
        foreach(var preparation in preparations)if(preparation.state=="PENDING")report.partialPreparations++;
        unitCsv?.Dispose();worldCsv?.Dispose();pairCsv?.Dispose();report.completion=completion;
        string path=Read<string>(primary,"output",null),result=path==null?null:Path.Combine(path,"complete.txt");
        report.primaryResult=result!=null&&File.Exists(result)?File.ReadAllText(result):"";
        report.realSeconds=Time.unscaledTimeAsDouble-startedAt;report.casts=casts.ToArray();report.shakes=shakes.ToArray();report.preparations=preparations.ToArray();
        bool failed=report.timeScaleViolations+report.exclusiveClockFrames+report.shadeFrames+report.missingOwnRecords+report.reusedPresentationRecords+report.initialVisualFailures+
            report.activeBodyVisualFailures+report.hitSlideSpeedViolations+report.newOrWorseSlideOverlapSamples+report.cameraPoseViolations+
            report.cameraLensViolations+report.cameraProjectionViolations+report.shakeExpiredOffsetViolations+report.cameraZoomRangeViolations+report.cameraCropBoundsViolations+
            report.pendingUseViolations+report.pendingSkillTimerIncreaseSamples+report.pendingBodyViolations+report.pendingPositionChanges+
            report.pendingFlagMissingSamples+report.ownAnticipationTooShort+report.missingPreparationEvidence>0;
        bool insufficient=report.frames==0||report.eligibleUses==0||report.emphasisFrames==0||report.worldProgressSamples==0||report.otherActorProgressSamples==0||!report.slideContractPresent||
            anticipationScenario&&(report.pendingSamples==0||report.pendingWorldProgressSamples==0||report.pendingOtherActorProgressSamples==0);
        report.result=completion!="PRIMARY_FINISHED"||failed||insufficient?"REVIEW_REQUIRED":"OBSERVED_CONTRACT_PASS";
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(output,"complete.txt"),completion);
        Debug.Log("[Normal Combat QA] "+report.result+" "+label+" uses="+report.eligibleUses);
    }

    private static void Write(StreamWriter writer,params object[] values)
    {
        var line=new StringBuilder();
        for(int i=0;i<values.Length;i++)
        {if(i>0)line.Append(',');var value=values[i];if(value is float)line.Append(((float)value).ToString("R",CultureInfo.InvariantCulture));else if(value is double)line.Append(((double)value).ToString("R",CultureInfo.InvariantCulture));else if(value is IFormattable)line.Append(((IFormattable)value).ToString(null,CultureInfo.InvariantCulture));else line.Append(value);}
        writer.WriteLine(line);
    }
    private static object Field(object value,string name)=>value==null?null:value.GetType().GetField(name,Flags)?.GetValue(value);
    private static T Read<T>(object value,string name,T fallback){object found=Field(value,name);return found is T?(T)found:fallback;}
    private void OnDestroy(){if(begun&&!Finished)Complete("OBSERVER_DESTROYED_PARTIAL");}
}
