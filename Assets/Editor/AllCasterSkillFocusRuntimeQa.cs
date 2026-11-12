using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class AllCasterSkillFocusRuntimeQa
{
    public static void Attach(string label)
    {
        SkillFocusRuntimeQa.Guard();
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(string.IsNullOrWhiteSpace(label))throw new ArgumentException("A QA label is required.");
        foreach(char value in label)
            if(!char.IsLetterOrDigit(value)&&value!='_'&&value!='-')throw new ArgumentException("Invalid QA label.");
        var primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        if(primary==null)throw new InvalidOperationException("Start the primary observer first.");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        if((string)typeof(SkillFocusRuntimeQaObserver).GetField("label",flags).GetValue(primary)!=label)
            throw new InvalidOperationException("The all-caster label must match the primary observer.");
        if(primary.GetComponent<AllCasterSkillFocusRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("All-caster diagnostics are already attached.");
        string output=Path.GetFullPath(Path.Combine("Logs/AllCasterSkillFocusQA/20261019",label));
        if(Directory.Exists(output))throw new IOException("All-caster QA output already exists.");
        primary.gameObject.AddComponent<AllCasterSkillFocusRuntimeQaObserver>().Begin(primary,label,output);
    }
}

// Reflection and per-unit CSV sampling add overhead; this observes coverage, never FPS.
[DefaultExecutionOrder(10050)]
public sealed class AllCasterSkillFocusRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const BindingFlags Methods=Instance|BindingFlags.Static;
    private const int MissingRecord=1,WrongScale=2,MissingGold=4,VisibleHud=8,MissingMirror=16,MissingGather=32;
    private readonly Dictionary<object,Actor> actors=new Dictionary<object,Actor>();
    private readonly List<CastEvent> casts=new List<CastEvent>();
    private readonly List<PendingCheck> pending=new List<PendingCheck>();
    private readonly StringBuilder csv=new StringBuilder("frame,realtime,unscaledTime,primaryElapsed,battleElapsed,timeScale,id,team,species,skillUses,castDelta,alive,eligible,exclusion,globalSourceId,globalPending,directorFocus,globalBusy,globalIntervalReady,isGlobalCaster,hasPresentation,localActive,presentationAge,recoveryAt,driverScale,driverSkillAction,commonSkillAction,rendererVisible,outlineExists,outlined,emphasized,outlineR,outlineG,outlineB,gold,hudVisible,mirrorExists,mirrorVisible,gatherCached,gatherVisible,presentationStateMissingBits,cameraFov,lensShiftX,lensShiftY,initialCameraFov,cameraFovChange,lensShiftChangeX,lensShiftChangeY,projectionM02,projectionM12,projectionM03,projectionM13,primaryFinished\n");
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private MethodInfo membership,commonAction,hud;
    private FieldInfo localPool;
    private string label,output;
    private bool begun,attachedAfterBattleStarted,cameraBaselineCaptured;
    private float initialCameraFov,maximumCameraFovChange,maximumCameraLensChange;
    private Vector2 initialCameraLensShift;
    private double startedAt;
    private int frames,rows,observedCastUses,playerCastUses,enemyCastUses,firstFrameMissingUses;
    private int passedUses,recheckedPassUses,normalExceptionUses,missingUses,unresolvedUses;
    private int missingRecordUses,wrongScaleUses,missingGoldUses,visibleHudUses,missingMirrorUses,missingGatherUses;
    private int cameraFovViolations,cameraLensViolations,cameraFovChangeViolations,cameraLensChangeViolations;
    private int maximumConcurrentPresentations,maximumGatherVisible;

    public bool Finished { get; private set; }

    [Serializable] private sealed class Actor
    {
        public int id,initialSkillUses,lastSkillUses,observedUses,passedUses,missingUses,exceptionUses;
        public string team,species;
        [NonSerialized] public object unit;
        [NonSerialized] public Snapshot current=new Snapshot();
    }
    private sealed class Snapshot
    {
        public bool alive,eligible,global,hasPresentation,localActive,driverSkill,action,rendererVisible;
        public bool outlineExists,outlined,emphasized,gold,hudVisible,mirrorExists,mirrorVisible;
        public int uses,gatherCached,gatherVisible,missing;
        public string exclusion="";
        public double age=-1d,recoveryAt=-1d;
        public float scale=1f;
        public Color outline;
    }
    [Serializable] private sealed class CastEvent
    {
        public int frame,recheckedFrame,id,uses,delta,firstMissingBits,recheckMissingBits;
        public string team,species,classification;
        public bool globalCaster,globalBusy,directorFocus;
        public double unscaledTime;
    }
    private sealed class PendingCheck
    {public Actor actor;public CastEvent cast;}
    [Serializable] private sealed class Report
    {
        public string label,completion,primaryResult;
        public string limitation="Read-only per-unit coverage companion. Reflection and CSV allocation add overhead; no FPS or build-performance claim. Skill echoes without a new caster action, passive attacks, ML, ghosts, clones and minions are excluded. First-frame missing state is retained even when the next frame proves death, actual action completion or battle completion.";
        public string missingBitMeaning="1=record,2=1.5 scale,4=gold emphasized outline,8=visible caster HUD,16=mirror,32=three cached gather sprites";
        public bool primaryFinished,attachedAfterBattleStarted;
        public int frames,rows,observedCastUses,playerCastUses,enemyCastUses,firstFrameMissingUses;
        public int passedUses,recheckedPassUses,normalExceptionUses,missingUses,unresolvedUses;
        public int missingRecordUses,wrongScaleUses,missingGoldUses,visibleHudUses,missingMirrorUses,missingGatherUses;
        public int cameraFovViolations,cameraLensViolations,cameraFovChangeViolations,cameraLensChangeViolations;
        public int maximumConcurrentPresentations,maximumGatherVisible;
        public float initialCameraFov,initialCameraLensShiftX,initialCameraLensShiftY,maximumCameraFovChange,maximumCameraLensChange;
        public double elapsed;
        public Actor[] actors;
        public CastEvent[] casts;
    }

    public void Begin(SkillFocusRuntimeQaObserver owner,string requestedLabel,string directory)
    {
        SkillFocusRuntimeQa.Guard();
        primary=owner;label=requestedLabel;output=directory;
        Type type=typeof(MonsterAutoBattlePrototype);
        membership=type.GetMethod("HasSkillCastPresentation",Methods);
        commonAction=type.GetMethod("CommonSkillActionActive",Methods);
        hud=type.GetMethod("TryGetUnitWorldHudRect",Methods);
        localPool=type.GetField("localSkillPresentations",Instance);
        if(membership==null||commonAction==null||hud==null||localPool==null)
            throw new InvalidOperationException("The per-unit skill presentation observation contract is missing.");
        attachedAfterBattleStarted=Read<bool>(primary,"seenBattle",false);
        Directory.CreateDirectory(output);startedAt=Time.unscaledTimeAsDouble;begun=true;
    }

    private void LateUpdate()
    {
        if(!begun||Finished)return;
        if(primary==null){Complete("PRIMARY_OBSERVER_MISSING",false);return;}
        battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
        bool primaryFinished=Read<bool>(primary,"finished",false);
        if(battle!=null)
        {
            var units=Field(battle,"units") as IList;
            bool seen=Read<bool>(primary,"seenBattle",false);
            if(units!=null)Observe(units,seen,primaryFinished);
        }
        if(primaryFinished)Complete("PRIMARY_FINISHED",true);
    }

    private void Observe(IList units,bool seenBattle,bool primaryFinished)
    {
        double now=Time.unscaledTimeAsDouble;
        object global=Field(battle,"skillFocusUnit");
        var director=Read<CombatCameraDirector>(battle,"combatCameraDirector",null);
        bool directorFocus=director!=null&&director.SkillFocusActive;
        bool globalPending=Read<bool>(battle,"skillFocusCastPending",false);
        Camera camera=Read<Camera>(battle,"mainCamera",null);
        bool intervalReady=director!=null&&now>=Read<double>(director,"skillFocusEligibleAt",double.MaxValue);
        int concurrent=0;
        foreach(object unit in units)
        {
            Actor actor;
            if(!actors.TryGetValue(unit,out actor))
            {
                var definition=Read<MonsterDefinition>(unit,"definition",null);
                actor=new Actor{unit=unit,id=Read<int>(unit,"id",-1),team=Convert.ToString(Field(unit,"team")),
                    species=definition==null?"":definition.monsterId,initialSkillUses=Read<int>(unit,"skillUses",0)};
                actor.lastSkillUses=actor.initialSkillUses;actors.Add(unit,actor);
            }
            Capture(actor.current,unit,global,now);
            if(actor.current.hasPresentation)concurrent++;
        }
        if(!seenBattle)return;
        frames++;maximumConcurrentPresentations=Mathf.Max(maximumConcurrentPresentations,concurrent);
        if(camera!=null&&!cameraBaselineCaptured)
        {initialCameraFov=camera.fieldOfView;initialCameraLensShift=camera.lensShift;cameraBaselineCaptured=true;}
        if(camera!=null&&Mathf.Abs(camera.fieldOfView-36f)>.001f)cameraFovViolations++;
        if(camera!=null&&camera.lensShift.sqrMagnitude>.00000001f)cameraLensViolations++;
        if(camera!=null)
        {
            float fovChange=Mathf.Abs(camera.fieldOfView-initialCameraFov);
            float lensChange=(camera.lensShift-initialCameraLensShift).magnitude;
            maximumCameraFovChange=Mathf.Max(maximumCameraFovChange,fovChange);
            maximumCameraLensChange=Mathf.Max(maximumCameraLensChange,lensChange);
            if(fovChange>.001f)cameraFovChangeViolations++;
            if(lensChange>.0001f)cameraLensChangeViolations++;
        }
        for(int i=pending.Count-1;i>=0;i--)
            if(pending[i].cast.frame<Time.frameCount){Resolve(pending[i],primaryFinished);pending.RemoveAt(i);}

        foreach(object unit in units)
        {
            Actor actor=actors[unit];Snapshot s=actor.current;
            int delta=Mathf.Max(0,s.uses-actor.lastSkillUses);actor.lastSkillUses=s.uses;
            if(delta>0&&s.eligible)
            {
                actor.observedUses+=delta;observedCastUses+=delta;
                if(actor.team=="Player")playerCastUses+=delta;else if(actor.team=="Enemy")enemyCastUses+=delta;
                var cast=new CastEvent{frame=Time.frameCount,id=actor.id,team=actor.team,species=actor.species,
                    uses=s.uses,delta=delta,unscaledTime=now,globalCaster=s.global,globalBusy=global!=null,
                    directorFocus=directorFocus,firstMissingBits=s.missing,classification=s.missing==0?"PASS":"PENDING_RECHECK"};
                casts.Add(cast);
                if(s.missing==0){passedUses+=delta;actor.passedUses+=delta;}
                else{firstFrameMissingUses+=delta;pending.Add(new PendingCheck{actor=actor,cast=cast});}
            }
            maximumGatherVisible=Mathf.Max(maximumGatherVisible,s.gatherVisible);
            Matrix4x4 p=camera==null?Matrix4x4.identity:camera.projectionMatrix;
            Append(Time.frameCount,Time.realtimeSinceStartupAsDouble,now,now-Read<double>(primary,"startedAt",now),
                Read<float>(battle,"battleElapsed",0f),Time.timeScale,actor.id,actor.team,actor.species,s.uses,delta,s.alive,s.eligible,s.exclusion,
                Read<int>(global,"id",-1),globalPending,directorFocus,global!=null,intervalReady,s.global,
                s.hasPresentation,s.localActive,s.age,s.recoveryAt,s.scale,s.driverSkill,s.action,s.rendererVisible,
                s.outlineExists,s.outlined,s.emphasized,s.outline.r,s.outline.g,s.outline.b,s.gold,s.hudVisible,
                s.mirrorExists,s.mirrorVisible,s.gatherCached,s.gatherVisible,s.missing,
                camera==null?-1f:camera.fieldOfView,camera==null?0f:camera.lensShift.x,camera==null?0f:camera.lensShift.y,
                initialCameraFov,camera==null?0f:camera.fieldOfView-initialCameraFov,
                camera==null?0f:camera.lensShift.x-initialCameraLensShift.x,camera==null?0f:camera.lensShift.y-initialCameraLensShift.y,
                p.m02,p.m12,p.m03,p.m13,primaryFinished);
            csv.AppendLine();rows++;
        }
    }

    private void Capture(Snapshot s,object unit,object global,double now)
    {
        GameObject model=Read<GameObject>(unit,"model",null);
        var driver=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);
        var renderer=driver==null?null:driver.GetComponent<SpriteRenderer>();
        s.alive=Read<bool>(unit,"alive",false)&&Read<float>(unit,"hp",0f)>0f&&model!=null&&model.activeInHierarchy;
        s.exclusion=Read<bool>(battle,"mlEpisodeActive",false)?"ML":
            Field(unit,"spiritConcertOwner")!=null?"GHOST":Read<bool>(unit,"augmentShadowClone",false)?"CLONE":
            Field(unit,"mantisBroodOwner")!=null||Read<bool>(unit,"rank3MycokingSummon",false)?"MINION":
            driver==null||renderer==null?"NO_SPRITE":"";
        s.eligible=s.exclusion.Length==0;s.uses=Read<int>(unit,"skillUses",0);s.global=ReferenceEquals(unit,global);
        s.hasPresentation=(bool)membership.Invoke(membership.IsStatic?null:battle,new[]{unit});
        s.scale=driver==null?1f:Read<float>(driver,"skillFocusScale",1f);
        s.driverSkill=driver!=null&&driver.IsSkillActionActive;
        s.action=(bool)commonAction.Invoke(commonAction.IsStatic?null:battle,new[]{unit});
        s.rendererVisible=renderer!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&renderer.sprite!=null;
        var outline=Read<ScreenSpaceOutlineTarget>(unit,"outline",null);
        s.outlineExists=outline!=null;s.outlined=outline!=null&&outline.IsOutlined;
        s.emphasized=Read<bool>(outline,"emphasized",false);s.outline=Read<Color>(outline,"color",Color.clear);
        Color gold=CombatFlashPresentation.Peak(new Color(1f,.86f,.32f,1f));
        s.gold=s.outlined&&s.emphasized&&Mathf.Abs(s.outline.r-gold.r)<.01f&&
            Mathf.Abs(s.outline.g-gold.g)<.01f&&Mathf.Abs(s.outline.b-gold.b)<.01f;
        object[] args={unit,new Rect()};s.hudVisible=(bool)hud.Invoke(hud.IsStatic?null:battle,args);
        object local=null;
        foreach(object candidate in (IEnumerable)localPool.GetValue(battle))
            if(Read<bool>(candidate,"active",false)&&ReferenceEquals(Field(candidate,"unit"),unit)){local=candidate;break;}
        s.localActive=local!=null;
        var mirror=s.global?Read<SkillFocusSpriteMirror>(battle,"skillFocusForeground",null):
            Read<SkillFocusSpriteMirror>(local,"mirror",null);
        s.age=s.global?now-Read<double>(battle,"skillFocusCommittedAt",now):
            local==null?-1d:now-Read<double>(local,"startedAt",now);
        s.recoveryAt=s.global?Read<double>(battle,"skillFocusRecoveryAt",-1d):Read<double>(local,"recoveryAt",-1d);
        var foreground=mirror==null?null:mirror.GetComponent<SpriteRenderer>();
        s.mirrorExists=mirror!=null;s.mirrorVisible=foreground!=null&&foreground.enabled&&foreground.gameObject.activeInHierarchy;
        var gather=Read<SpriteRenderer[]>(mirror,"gatherSprites",null);
        s.gatherCached=gather==null?0:gather.Length;s.gatherVisible=0;
        if(gather!=null)foreach(var ghost in gather)
            if(ghost!=null&&ghost.enabled&&ghost.gameObject.activeInHierarchy)s.gatherVisible++;
        s.missing=!s.hasPresentation?MissingRecord:0;
        if(s.scale<1.499f)s.missing|=WrongScale;
        if(!s.gold)s.missing|=MissingGold;
        if(s.hudVisible)s.missing|=VisibleHud;
        if(!s.mirrorExists||s.rendererVisible&&!s.mirrorVisible)s.missing|=MissingMirror;
        if(s.rendererVisible&&s.gatherCached!=3)s.missing|=MissingGather;
    }

    private void Resolve(PendingCheck check,bool primaryFinished)
    {
        Snapshot s=check.actor.current;CastEvent cast=check.cast;
        cast.recheckedFrame=Time.frameCount;cast.recheckMissingBits=s.missing;
        if(s.missing==0)
        {cast.classification="PASS_AFTER_ONE_FRAME";passedUses+=cast.delta;recheckedPassUses+=cast.delta;check.actor.passedUses+=cast.delta;return;}
        if(!s.alive||!s.action||primaryFinished||Read<bool>(battle,"battleEnding",false))
        {
            cast.classification=!s.alive?"NORMAL_DEATH_OR_DEACTIVATION":
                primaryFinished||Read<bool>(battle,"battleEnding",false)?"NORMAL_BATTLE_END":"NORMAL_ACTION_END_BEFORE_RECHECK";
            normalExceptionUses+=cast.delta;check.actor.exceptionUses+=cast.delta;return;
        }
        cast.classification="FAIL_MISSING_PRESENTATION";missingUses+=cast.delta;check.actor.missingUses+=cast.delta;
        if((s.missing&MissingRecord)!=0)missingRecordUses+=cast.delta;
        if((s.missing&WrongScale)!=0)wrongScaleUses+=cast.delta;
        if((s.missing&MissingGold)!=0)missingGoldUses+=cast.delta;
        if((s.missing&VisibleHud)!=0)visibleHudUses+=cast.delta;
        if((s.missing&MissingMirror)!=0)missingMirrorUses+=cast.delta;
        if((s.missing&MissingGather)!=0)missingGatherUses+=cast.delta;
    }

    private void Complete(string completion,bool primaryFinished)
    {
        if(Finished)return;Finished=true;
        foreach(var check in pending)
        {
            if(primaryFinished||!check.actor.current.alive||!check.actor.current.action)Resolve(check,primaryFinished);
            else{check.cast.classification="UNRESOLVED_FINAL_FRAME";unresolvedUses+=check.cast.delta;}
        }
        pending.Clear();
        string primaryOutput=Read<string>(primary,"output",null);
        string result=primaryOutput==null?null:Path.Combine(primaryOutput,"complete.txt");
        var actorReports=new Actor[actors.Count];actors.Values.CopyTo(actorReports,0);
        File.WriteAllText(Path.Combine(output,"samples.csv"),csv.ToString());
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(new Report{
            label=label,completion=completion,primaryFinished=primaryFinished,
            primaryResult=result!=null&&File.Exists(result)?File.ReadAllText(result):"",attachedAfterBattleStarted=attachedAfterBattleStarted,
            frames=frames,rows=rows,observedCastUses=observedCastUses,playerCastUses=playerCastUses,enemyCastUses=enemyCastUses,
            firstFrameMissingUses=firstFrameMissingUses,passedUses=passedUses,recheckedPassUses=recheckedPassUses,
            normalExceptionUses=normalExceptionUses,missingUses=missingUses,unresolvedUses=unresolvedUses,
            missingRecordUses=missingRecordUses,wrongScaleUses=wrongScaleUses,missingGoldUses=missingGoldUses,
            visibleHudUses=visibleHudUses,missingMirrorUses=missingMirrorUses,missingGatherUses=missingGatherUses,
            cameraFovViolations=cameraFovViolations,cameraLensViolations=cameraLensViolations,
            cameraFovChangeViolations=cameraFovChangeViolations,cameraLensChangeViolations=cameraLensChangeViolations,
            initialCameraFov=initialCameraFov,initialCameraLensShiftX=initialCameraLensShift.x,initialCameraLensShiftY=initialCameraLensShift.y,
            maximumCameraFovChange=maximumCameraFovChange,maximumCameraLensChange=maximumCameraLensChange,
            maximumConcurrentPresentations=maximumConcurrentPresentations,maximumGatherVisible=maximumGatherVisible,
            elapsed=Time.unscaledTimeAsDouble-startedAt,actors=actorReports,casts=casts.ToArray()},true));
        File.WriteAllText(Path.Combine(output,"complete.txt"),completion);
        Debug.Log("[All Caster Skill Focus QA] COMPLETE "+output+": missing="+missingUses+", casts="+observedCastUses);
    }

    private void Append(params object[] values)
    {
        foreach(object value in values)
        {
            if(csv[csv.Length-1]!='\n')csv.Append(',');
            if(value is double)csv.Append(((double)value).ToString("R",CultureInfo.InvariantCulture));
            else if(value is float)csv.Append(((float)value).ToString("R",CultureInfo.InvariantCulture));
            else if(value is IFormattable)csv.Append(((IFormattable)value).ToString(null,CultureInfo.InvariantCulture));
            else csv.Append(value);
        }
    }
    private static object Field(object value,string name)=>value==null?null:value.GetType().GetField(name,Instance)?.GetValue(value);
    private static T Read<T>(object value,string name,T fallback)
    {object found=Field(value,name);return found is T?(T)found:fallback;}
    private void OnDestroy(){if(begun&&!Finished)Complete("OBSERVER_DESTROYED_PARTIAL",false);}
}
