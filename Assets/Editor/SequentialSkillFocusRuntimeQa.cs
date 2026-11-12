using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class SequentialSkillFocusRuntimeQa
{
    public static void Attach(string label)
    {
        SkillFocusRuntimeQa.Guard();
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(string.IsNullOrWhiteSpace(label))throw new ArgumentException("A QA label is required.");
        foreach(char value in label)
            if(!char.IsLetterOrDigit(value)&&value!='_'&&value!='-')throw new ArgumentException("Invalid QA label.");
        var primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        if(primary==null||(string)typeof(SkillFocusRuntimeQaObserver).GetField("label",flags).GetValue(primary)!=label)
            throw new InvalidOperationException("Start the primary observer with the same label first.");
        if(primary.GetComponent<SequentialSkillFocusRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("Sequential diagnostics are already attached.");
        string output=Path.GetFullPath(Path.Combine("Logs/SequentialSkillFocusQA/20261019",label));
        if(Directory.Exists(output))throw new IOException("Sequential QA output already exists.");
        primary.gameObject.AddComponent<SequentialSkillFocusRuntimeQaObserver>().Begin(primary,label,output);
    }
}

// Read-only diagnostics. Per-unit reflection and CSV allocation are not an FPS benchmark.
[DefaultExecutionOrder(10060)]
public sealed class SequentialSkillFocusRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private readonly Dictionary<object,Actor> actors=new Dictionary<object,Actor>();
    private readonly List<Intro> intros=new List<Intro>();
    private readonly List<Cast> casts=new List<Cast>();
    private readonly StringBuilder csv=new StringBuilder("frame,unscaledTime,primaryElapsed,battleElapsed,timeScale,id,team,species,eligible,skillUses,castDelta,globalSourceId,globalPending,directorActive,globalStartedAt,ownIntro,ownStartedAt,ownElapsed,ownStartUses,ownCommitted,matchedOwnIntro,localActive,actionActive,alive,scale,gold,hudVisible,mirrorVisible,gatherCached,gatherVisible,shadeAlpha,shadeVisible,noticeStateExpected,visualStrength,demotedAction,demotionMissingBits,cameraFov,lensShiftX,lensShiftY,fovChange,lensChange,queueCount,sourceOrder,sourceLayer,mirrorOrder,mirrorLayer,shadeOrder,shadeLayer,oldBehindShade,primaryFinished\n");
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private MethodInfo actionMethod,hudMethod;
    private string label,output;
    private bool begun,attachedAfterBattleStarted,cameraCaptured;
    private float homeFov,maximumFovChange,maximumLensChange;
    private Vector2 homeLens;
    private Intro current;
    private int frames,rows,observedUses,playerUses,enemyUses,matchedUses,missingIntroUses,earlyUses,reusedIntroUses;
    private int pendingUseViolations,demotedActionSamples,demotionViolations,cameraViolations;
    private int spotlightSortingViolations,localSortingViolations,oldBehindShadeSamples,unexpectedShadeFrames;
    private double startedAt;
    public bool Finished {get;private set;}

    [Serializable] private sealed class Actor
    {
        public int id,lastUses,observedUses;
        public string team,species;
        [NonSerialized] public object unit;
        [NonSerialized] public Intro lastIntro;
    }
    [Serializable] private sealed class Intro
    {
        public int sequence,id,startUses,pendingSamples,stopSamples,fullScaleStopSamples,goldStopSamples,shadeStopSamples,noticeStateSamples,mirrorStopSamples,gatherCachedStopSamples,gatherMaximumVisible;
        public int committedUses,pendingUseViolations,demotedActionSamples,demotionViolations;
        public string team,species;
        public double startedAt,firstObservedAt,committedAt=-1d,firstCommitElapsed=-1d,endedAt=-1d;
        public string endState="OBSERVING";
    }
    [Serializable] private sealed class Cast
    {
        public int frame,id,delta,uses,introSequence,startUses;
        public string team,species,result;
        public double unscaledTime,ownStartedAt=-1d,elapsed=-1d;
        public bool globalCaster,pending;
    }
    private sealed class Visual
    {
        public bool local,action,alive,gold,hud,mirror;
        public float scale=1f;
        public int cached,gather,sourceOrder=-1,sourceLayer=-1,mirrorOrder=-1,mirrorLayer=-1;
    }
    [Serializable] private sealed class Report
    {
        public string label,completion,primaryResult;
        public string limitation="Read-only natural-battle observation. Each eligible skillUses increment is linked to that caster's own global introduction, never another caster's clock. Notice state is an expectation from production GUI state; native screenshots verify visible punctuation. Diagnostic sampling is not an FPS benchmark.";
        public string demotionMissingBitMeaning="1=local record,2=1.5 scale,4=gold,8=visible HUD,16=mirror";
        public bool primaryFinished,attachedAfterBattleStarted;
        public int frames,rows,observedUses,playerUses,enemyUses,matchedUses,missingIntroUses,earlyUses,reusedIntroUses,pendingUseViolations;
        public int committedIntros,introsMissingStopEvidence,demotedActionSamples,demotionViolations,cameraViolations;
        public int spotlightSortingViolations,localSortingViolations,oldBehindShadeSamples,unexpectedShadeFrames;
        public float initialCameraFov,initialLensX,initialLensY,maximumFovChange,maximumLensChange;
        public double elapsed;
        public Intro[] intros;
        public Cast[] casts;
    }

    public void Begin(SkillFocusRuntimeQaObserver owner,string requestedLabel,string directory)
    {
        SkillFocusRuntimeQa.Guard();primary=owner;label=requestedLabel;output=directory;
        actionMethod=typeof(MonsterAutoBattlePrototype).GetMethod("CommonSkillActionActive",Flags);
        hudMethod=typeof(MonsterAutoBattlePrototype).GetMethod("TryGetUnitWorldHudRect",Flags);
        if(actionMethod==null||hudMethod==null)throw new InvalidOperationException("Presentation observation contract missing.");
        attachedAfterBattleStarted=Read<bool>(primary,"seenBattle",false);
        Directory.CreateDirectory(output);startedAt=Time.unscaledTimeAsDouble;begun=true;
    }

    private void LateUpdate()
    {
        if(!begun||Finished)return;
        if(primary==null){Complete("PRIMARY_MISSING",false);return;}
        battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
        bool done=Read<bool>(primary,"finished",false);
        var units=Field(battle,"units") as IList;
        if(units!=null)Observe(units,Read<bool>(primary,"seenBattle",false),done);
        if(done)Complete("PRIMARY_FINISHED",true);
    }

    private void Observe(IList units,bool seen,bool done)
    {
        double now=Time.unscaledTimeAsDouble;
        foreach(object unit in units)
            if(!actors.ContainsKey(unit))
            {
                var definition=Read<MonsterDefinition>(unit,"definition",null);
                actors.Add(unit,new Actor{unit=unit,id=Read<int>(unit,"id",-1),lastUses=Read<int>(unit,"skillUses",0),
                    team=Convert.ToString(Field(unit,"team")),species=definition==null?"":definition.monsterId});
            }
        if(!seen)return;
        frames++;
        var director=Read<CombatCameraDirector>(battle,"combatCameraDirector",null);
        object global=Field(battle,"skillFocusUnit");
        bool pending=Read<bool>(battle,"skillFocusCastPending",false);
        double globalStarted=Read<double>(director,"skillFocusStartedAt",-1d);
        if(current!=null&&(global==null||Read<int>(global,"id",-1)!=current.id||globalStarted!=current.startedAt))
        {current.endedAt=now;current.endState=current.committedUses>0?"COMMITTED_THEN_RELEASED":"CANCELLED_WITHOUT_USE";current=null;}
        if(global!=null&&current==null)
        {
            Actor source=actors[global];
            current=new Intro{sequence=intros.Count+1,id=source.id,team=source.team,species=source.species,
                startedAt=globalStarted,firstObservedAt=now,startUses=source.lastUses};
            intros.Add(current);source.lastIntro=current;
        }
        var camera=Read<Camera>(battle,"mainCamera",null);
        if(camera!=null&&!cameraCaptured){homeFov=camera.fieldOfView;homeLens=camera.lensShift;cameraCaptured=true;}
        float fovChange=camera==null?0f:Mathf.Abs(camera.fieldOfView-homeFov);
        float lensChange=camera==null?0f:(camera.lensShift-homeLens).magnitude;
        maximumFovChange=Mathf.Max(maximumFovChange,fovChange);maximumLensChange=Mathf.Max(maximumLensChange,lensChange);
        if(fovChange>.001f||lensChange>.0001f)cameraViolations++;
        var shade=Read<SpriteRenderer>(battle,"skillFocusShade",null);
        float shadeAlpha=shade==null?0f:shade.color.a;
        bool shadeVisible=shade!=null&&shade.enabled&&shade.gameObject.activeInHierarchy;
        if(!pending&&shadeVisible&&shadeAlpha>.001f)unexpectedShadeFrames++;
        int shadeOrder=shade==null?-1:shade.sortingOrder,shadeLayer=shade==null?-1:shade.sortingLayerID;
        var queue=Field(battle,"pendingSkillFocusCasts") as IList;
        float strength=Read<float>(battle,"skillFocusVisualStrength",0f);
        bool notice=global!=null&&strength>0f&&Field(battle,"skillFocusNoticeStyle")!=null;
        foreach(object unit in units)
        {
            Actor actor=actors[unit];Intro own=actor.lastIntro;
            int uses=Read<int>(unit,"skillUses",0),delta=Mathf.Max(0,uses-actor.lastUses);
            bool eligible=Eligible(unit),isGlobal=ReferenceEquals(unit,global),matched=false;
            Visual visual=ReadVisual(unit,isGlobal);
            if(delta>0&&eligible)
            {
                observedUses+=delta;actor.observedUses+=delta;
                if(actor.team=="Player")playerUses+=delta;else if(actor.team=="Enemy")enemyUses+=delta;
                var cast=new Cast{frame=Time.frameCount,id=actor.id,team=actor.team,species=actor.species,uses=uses,delta=delta,
                    unscaledTime=now,introSequence=own==null?0:own.sequence,startUses=own==null?-1:own.startUses,
                    ownStartedAt=own==null?-1d:own.startedAt,elapsed=own==null?-1d:now-own.startedAt,globalCaster=isGlobal,pending=pending&&isGlobal};
                if(own==null||!isGlobal||!ReferenceEquals(current,own))
                {cast.result="FAIL_NO_OWN_ACTIVE_GLOBAL_INTRO";missingIntroUses+=delta;}
                else if(own.committedUses>0){cast.result="FAIL_INTRO_REUSED";reusedIntroUses+=delta;}
                else if(cast.elapsed<.38d-.0001d||cast.pending)
                {cast.result="FAIL_BEFORE_OWN_STOP_FINISHED";earlyUses+=delta;}
                else {cast.result="PASS_OWN_INTRO_THEN_COMMIT";matched=true;matchedUses+=delta;}
                if(own!=null)
                {own.committedUses+=delta;own.committedAt=now;if(own.firstCommitElapsed<0d)own.firstCommitElapsed=cast.elapsed;}
                casts.Add(cast);
            }
            actor.lastUses=uses;
            if(isGlobal&&current!=null&&pending)
            {
                current.pendingSamples++;
                if(uses!=current.startUses){current.pendingUseViolations++;pendingUseViolations++;}
                if(Time.timeScale==0f)
                {
                    current.stopSamples++;
                    if(visual.scale>=1.499f)current.fullScaleStopSamples++;
                    if(visual.gold)current.goldStopSamples++;
                    if(shadeVisible&&shadeAlpha>=.64f)current.shadeStopSamples++;
                    if(notice)current.noticeStateSamples++;
                    if(visual.mirror)current.mirrorStopSamples++;
                    if(visual.cached==3)current.gatherCachedStopSamples++;
                }
                current.gatherMaximumVisible=Mathf.Max(current.gatherMaximumVisible,visual.gather);
            }
            bool demoted=own!=null&&own.committedUses>0&&!isGlobal&&visual.action&&visual.alive&&eligible;
            if(isGlobal&&visual.mirror&&visual.mirrorOrder!=30004)spotlightSortingViolations++;
            if(visual.local&&visual.mirror&&visual.mirrorOrder!=29996)localSortingViolations++;
            bool behindShade=demoted&&visual.mirror&&shadeVisible&&shadeAlpha>.01f&&
                visual.mirrorLayer==shadeLayer&&visual.mirrorOrder<shadeOrder;
            if(behindShade)oldBehindShadeSamples++;
            int missing=0;
            if(demoted)
            {
                if(!visual.local)missing|=1;if(visual.scale<1.499f)missing|=2;if(!visual.gold)missing|=4;
                if(visual.hud)missing|=8;if(!visual.mirror)missing|=16;
                demotedActionSamples++;own.demotedActionSamples++;
                if(missing!=0){demotionViolations++;own.demotionViolations++;}
            }
            Append(Time.frameCount,now,now-Read<double>(primary,"startedAt",now),Read<float>(battle,"battleElapsed",0f),Time.timeScale,
                actor.id,actor.team,actor.species,eligible,uses,delta,Read<int>(global,"id",-1),pending,director!=null&&director.SkillFocusActive,
                globalStarted,own==null?0:own.sequence,own==null?-1d:own.startedAt,own==null?-1d:now-own.startedAt,own==null?-1:own.startUses,
                own!=null&&own.committedUses>0,matched,visual.local,visual.action,visual.alive,visual.scale,visual.gold,visual.hud,visual.mirror,
                visual.cached,visual.gather,shadeAlpha,shadeVisible,notice,strength,demoted,missing,camera==null?-1f:camera.fieldOfView,
                camera==null?0f:camera.lensShift.x,camera==null?0f:camera.lensShift.y,fovChange,lensChange,
                queue==null?0:queue.Count,visual.sourceOrder,visual.sourceLayer,visual.mirrorOrder,visual.mirrorLayer,
                shadeOrder,shadeLayer,behindShade,done);
            csv.AppendLine();rows++;
        }
    }

    private Visual ReadVisual(object unit,bool global)
    {
        var value=new Visual();var driver=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);
        var source=driver==null?null:driver.GetComponent<SpriteRenderer>();
        value.sourceOrder=source==null?-1:source.sortingOrder;value.sourceLayer=source==null?-1:source.sortingLayerID;
        var model=Read<GameObject>(unit,"model",null);
        value.alive=Read<bool>(unit,"alive",false)&&Read<float>(unit,"hp",0f)>0f&&model!=null&&model.activeInHierarchy;
        value.action=(bool)actionMethod.Invoke(actionMethod.IsStatic?null:battle,new[]{unit});
        value.scale=Read<float>(driver,"skillFocusScale",1f);
        var outline=Read<ScreenSpaceOutlineTarget>(unit,"outline",null);
        Color gold=CombatFlashPresentation.Peak(new Color(1f,.86f,.32f,1f)),color=Read<Color>(outline,"color",Color.clear);
        value.gold=outline!=null&&outline.IsOutlined&&Read<bool>(outline,"emphasized",false)&&
            Mathf.Abs(gold.r-color.r)<.01f&&Mathf.Abs(gold.g-color.g)<.01f&&Mathf.Abs(gold.b-color.b)<.01f;
        object[] args={unit,new Rect()};value.hud=(bool)hudMethod.Invoke(hudMethod.IsStatic?null:battle,args);
        object local=null;var pool=Field(battle,"localSkillPresentations") as IEnumerable;
        if(pool!=null)foreach(object entry in pool)
            if(Read<bool>(entry,"active",false)&&ReferenceEquals(Field(entry,"unit"),unit)){local=entry;break;}
        value.local=local!=null;
        var mirror=global?Read<SkillFocusSpriteMirror>(battle,"skillFocusForeground",null):Read<SkillFocusSpriteMirror>(local,"mirror",null);
        var renderer=mirror==null?null:mirror.GetComponent<SpriteRenderer>();
        value.mirrorOrder=renderer==null?-1:renderer.sortingOrder;value.mirrorLayer=renderer==null?-1:renderer.sortingLayerID;
        value.mirror=renderer!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy;
        var gather=Read<SpriteRenderer[]>(mirror,"gatherSprites",null);value.cached=gather==null?0:gather.Length;
        if(gather!=null)foreach(var ghost in gather)if(ghost!=null&&ghost.enabled&&ghost.gameObject.activeInHierarchy)value.gather++;
        return value;
    }

    private bool Eligible(object unit)
    {
        return !Read<bool>(battle,"mlEpisodeActive",false)&&Field(unit,"spiritConcertOwner")==null&&
            !Read<bool>(unit,"augmentShadowClone",false)&&Field(unit,"mantisBroodOwner")==null&&
            !Read<bool>(unit,"rank3MycokingSummon",false)&&Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null)!=null;
    }

    private void Complete(string completion,bool primaryFinished)
    {
        if(Finished)return;Finished=true;
        int committed=0,evidenceMissing=0;
        foreach(var intro in intros)
            if(intro.committedUses>0)
            {committed++;if(intro.stopSamples==0||intro.fullScaleStopSamples==0||intro.goldStopSamples==0||intro.shadeStopSamples==0||intro.noticeStateSamples==0||intro.mirrorStopSamples==0||intro.gatherCachedStopSamples==0)evidenceMissing++;}
        string path=Read<string>(primary,"output",null),result=path==null?null:Path.Combine(path,"complete.txt");
        File.WriteAllText(Path.Combine(output,"samples.csv"),csv.ToString());
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(new Report{
            label=label,completion=completion,primaryResult=result!=null&&File.Exists(result)?File.ReadAllText(result):"",primaryFinished=primaryFinished,
            attachedAfterBattleStarted=attachedAfterBattleStarted,frames=frames,rows=rows,observedUses=observedUses,playerUses=playerUses,enemyUses=enemyUses,
            matchedUses=matchedUses,missingIntroUses=missingIntroUses,earlyUses=earlyUses,reusedIntroUses=reusedIntroUses,pendingUseViolations=pendingUseViolations,
            committedIntros=committed,introsMissingStopEvidence=evidenceMissing,demotedActionSamples=demotedActionSamples,demotionViolations=demotionViolations,
            cameraViolations=cameraViolations,initialCameraFov=homeFov,initialLensX=homeLens.x,initialLensY=homeLens.y,
            spotlightSortingViolations=spotlightSortingViolations,localSortingViolations=localSortingViolations,oldBehindShadeSamples=oldBehindShadeSamples,
            unexpectedShadeFrames=unexpectedShadeFrames,
            maximumFovChange=maximumFovChange,maximumLensChange=maximumLensChange,elapsed=Time.unscaledTimeAsDouble-startedAt,intros=intros.ToArray(),casts=casts.ToArray()},true));
        File.WriteAllText(Path.Combine(output,"complete.txt"),completion);
        Debug.Log("[Sequential Skill Focus QA] COMPLETE "+output+": matched="+matchedUses+", uses="+observedUses);
    }
    private void Append(params object[] values)
    {
        foreach(object value in values)
        {
            if(csv[csv.Length-1]!='\n')csv.Append(',');
            if(value is double)csv.Append(((double)value).ToString("R",CultureInfo.InvariantCulture));
            else if(value is float)csv.Append(((float)value).ToString("R",CultureInfo.InvariantCulture));
            else if(value is IFormattable)csv.Append(((IFormattable)value).ToString(null,CultureInfo.InvariantCulture));else csv.Append(value);
        }
    }
    private static object Field(object value,string name)=>value==null?null:value.GetType().GetField(name,Flags)?.GetValue(value);
    private static T Read<T>(object value,string name,T fallback){object found=Field(value,name);return found is T?(T)found:fallback;}
    private void OnDestroy(){if(begun&&!Finished)Complete("OBSERVER_DESTROYED_PARTIAL",false);}
}
