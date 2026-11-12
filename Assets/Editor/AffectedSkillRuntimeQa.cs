using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class AffectedSkillRuntimeQa
{
    public static void Attach(string label)
    {
        SkillFocusRuntimeQa.Guard();
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(string.IsNullOrWhiteSpace(label))throw new ArgumentException("A QA label is required.");
        foreach(char value in label)if(!char.IsLetterOrDigit(value)&&value!='_'&&value!='-')throw new ArgumentException("Invalid QA label.");
        var primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        if(primary==null||(string)typeof(SkillFocusRuntimeQaObserver).GetField("label",flags).GetValue(primary)!=label)
            throw new InvalidOperationException("Launch the primary observer with the same label first.");
        if((bool)typeof(SkillFocusRuntimeQaObserver).GetField("prepared",flags).GetValue(primary))throw new InvalidOperationException("Attach before preparation.");
        if(primary.GetComponent<AffectedSkillRuntimeQaObserver>()!=null)throw new InvalidOperationException("Affected-skill observer already attached.");
        string path=Path.GetFullPath(Path.Combine("Logs/CombatSkillRepairQA/20261020",label));
        if(Directory.Exists(path))throw new IOException("Affected-skill QA output already exists.");
        primary.gameObject.AddComponent<AffectedSkillRuntimeQaObserver>().Begin(primary,label,path);
    }
}

// Read-only reflection/CSV diagnostics add overhead and do not establish FPS or exact rendered timing.
[DefaultExecutionOrder(10090)]
public sealed class AffectedSkillRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] StateFields={"mammothCharge","mammothToss","mammothBrace","mammothExpulsion"};
    private readonly Dictionary<object,Actor> actors=new Dictionary<object,Actor>();
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private StreamWriter unitsCsv,cameraCsv;
    private string output;
    private bool begun,cameraCaptured;
    private float homeFov;
    private Quaternion homeRotation;
    private object focusOwner;
    private bool previousPanActive,previousPanReturning;
    private FocusEpisode focusEpisode;
    private readonly List<FocusEpisode> focusEpisodes=new List<FocusEpisode>();
    private readonly HashSet<int> alliedOwners=new HashSet<int>();
    private readonly Dictionary<object,double> previousPresentationStarts=new Dictionary<object,double>();
    private readonly Report report=new Report();
    public bool Finished {get;private set;}

    private sealed class Actor
    {
        public UnitReport summary;
        public readonly object[] state=new object[4];
        public readonly float[] time=new float[4],stopped=new float[4];
        public readonly bool[] reported=new bool[4];
        public float previousWorld;
    }
    [Serializable] private sealed class UnitReport
    {
        public int id,samples,skillUses,skillHits,stalledEpisodes,orphanCallbackSamples;
        public string team,species;
        public bool original,alive;
        public float hp,skillDamage,maximumMammothStateTime,longestUnexplainedStall;
        public float[] maximumStateTimes=new float[4];
    }
    [Serializable] private sealed class Report
    {
        public string label,completion,primaryResult,result;
        public string limitation="Read-only natural 8:8 companion; all unit objects, including later summons, are sampled. Reflection and disk I/O add overhead. A two-battle-second stationary Mammoth state is flagged only while alive and without observed pause, preparation, freeze or silk. Bounded pan need not center an off-center source. A new owner is legal once the previous presentation ends; return can be interrupted by a fresh allied preparation. Samples can skip the exact acceptance or fully returned frame. No skill, clock, camera or save is changed.";
        public string[] mammothStateOrder=StateFields;
        public int frames,rows,startingUnits,panSamples,panSourceViolations,zoomViolations,rotationViolations,stalledEpisodes,orphanCallbackSamples;
        public int focusExecutionSamples,focusMinimumHoldSamples,focusOwnerSwitchViolations,focusReleasedBeforeBodyEnd,competingAllyPreparationSamples,competingEnemyPreparationSamples;
        public int projectionZoomSamples,projectionZoomReturningSamples,projectionZoomRestoredSamples,projectionZoomRangeViolations,cropBoundsViolations;
        public int uniqueAlliedOwners,legalFocusOwnerChanges,freshFocusAcceptedDuringReturn,deferredPreviouslyObservedFocusStarts;
        public int[] alliedOwnerIds;
        public float maximumPanOffset,maximumCenterError,maximumFovDelta,maximumRotationDelta;
        public float maximumProjectionZoom=1f,finalProjectionZoom=1f,maximumCropOvershoot;
        public FocusEpisode[] focusEpisodes;
        public UnitReport[] units;
    }
    [Serializable] private sealed class FocusEpisode
    {
        public int sequence,id,pendingSamples,executionSamples,minimumHoldSamples,competingAllySamples,competingEnemySamples;
        public string team,species,endReason;
        public double presentationStartedAt;
        public bool acceptedDuringObservedReturn,wasPreviouslyObservedPresentation;
        public float startedAt,endedAt=-1f,maximumZoom=1f;
    }

    public void Begin(SkillFocusRuntimeQaObserver owner,string label,string directory)
    {
        primary=owner;report.label=label;output=directory;Directory.CreateDirectory(output);
        unitsCsv=Writer("units.csv","frame,unscaled,scaled,world,id,team,species,original,alive,hp,skillUses,skillHits,skillDamage,action,requestedMotion,anticipating,driverCharge,driverChargeTime,validCallback,advanceCallback,finishedCallback,chargeTime,tossTime,braceTime,expulsionTime,freeze,silk,controlled,paused,positionRevision,chargeRevision,tossRevision,braceRevision,expulsionRevision,x,y,z");
        cameraCsv=Writer("camera.csv","frame,unscaled,world,panActive,sourceId,sourceTeam,sourcePending,sourceAlive,sourceMatched,offsetX,offsetY,offsetZ,viewportX,viewportY,centerError,fov,fovDelta,rotationDelta,sourcePresentationActive,sourceSkillAction,heldOwnerId,panReturning,appliedZoom,appliedPanX,appliedPanY,appliedShakeX,appliedShakeY,uvMinX,uvMinY,uvMaxX,uvMaxY,competingAllyPending,competingEnemyPending,sourceMinimumHold,focusEpisode,sourcePresentationStartedAt,priorOwnerStillValid,freshAcceptedDuringReturn");begun=true;
    }
    private StreamWriter Writer(string file,string header)
    {var writer=new StreamWriter(Path.Combine(output,file),false,new UTF8Encoding(false),65536);writer.WriteLine(header);return writer;}
    private void LateUpdate()
    {
        if(!begun||Finished)return;
        try
        {
            if(primary==null){Complete("PRIMARY_MISSING");return;}
            battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
            bool done=Read<bool>(primary,"finished",false);
            var units=Field(battle,"units") as IList;
            if(units!=null&&Read<bool>(primary,"prepared",false)&&(Read<bool>(battle,"battleStarted",false)||done))Observe(units,done);
            if(done)Complete("PRIMARY_FINISHED");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"exception.txt"),error.ToString());Complete("OBSERVER_EXCEPTION");Debug.LogException(error);}
    }
    private void Observe(IList units,bool done)
    {
        bool original=report.frames==0;if(original)report.startingUnits=units.Count;report.frames++;
        float world=Read<float>(battle,"battleElapsed",0f);
        bool paused=done||Read<bool>(battle,"paused",false)||Time.timeScale<=0f||BombBrick.Game.Core.GameFrontend.BlocksGameplay;
        var silkMethod=typeof(MonsterAutoBattlePrototype).GetMethod("SpiderSilkBound",Flags);
        foreach(object unit in units)
        {
            Actor actor;if(!actors.TryGetValue(unit,out actor))
            {
                actor=new Actor{previousWorld=world,summary=new UnitReport{id=Read<int>(unit,"id",-1),team=Field(unit,"team")?.ToString(),
                    species=Read<MonsterDefinition>(unit,"definition",null)?.monsterId??"",original=original}};actors.Add(unit,actor);
            }
            var summary=actor.summary;summary.samples++;summary.alive=Read<bool>(unit,"alive",false);summary.hp=Read<float>(unit,"hp",0f);
            summary.skillUses=Read<int>(unit,"skillUses",0);summary.skillHits=Read<int>(unit,"skillHits",0);summary.skillDamage=Read<float>(unit,"skillDamage",0f);
            var driver=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);var model=Read<GameObject>(unit,"model",null);
            var statuses=Read<CombatStatusState>(unit,"combatStatuses",null);
            bool freeze=statuses!=null&&statuses.Has(CombatStatusKind.Freeze),silk=silkMethod!=null&&(bool)silkMethod.Invoke(null,new[]{unit});
            bool controlled=statuses!=null&&(statuses.Has(CombatStatusKind.Knockdown)||statuses.Has(CombatStatusKind.Airborne)||statuses.Has(CombatStatusKind.Fear))||
                Read<float>(unit,"giraffeStunTimer",0f)>0f||Read<float>(unit,"crocodileImmobilizedTimer",0f)>0f||Read<bool>(unit,"displacing",false)||
                Field(unit,"preyDraggedBy")!=null||Field(unit,"griffinHeldBy")!=null;
            bool anticipating=driver!=null&&driver.IsSkillAnticipating,charge=Read<bool>(driver,"mammothChargeAttack",false);
            bool valid=Field(driver,"mammothChargeValid")!=null,advance=Field(driver,"mammothChargeAdvance")!=null,finished=Field(driver,"mammothChargeFinished")!=null;
            if(charge&&(!valid||!advance||!finished)){summary.orphanCallbackSamples++;report.orphanCallbackSamples++;}
            float[] times=new float[4];int[] revisions=new int[4];
            for(int i=0;i<4;i++)
            {
                object state=Field(unit,StateFields[i]);times[i]=Read<float>(state,"time",-1f);revisions[i]=Read<int>(state,"revision",-1);
                summary.maximumStateTimes[i]=Mathf.Max(summary.maximumStateTimes[i],times[i]);summary.maximumMammothStateTime=Mathf.Max(summary.maximumMammothStateTime,times[i]);
                bool same=state!=null&&ReferenceEquals(state,actor.state[i])&&Mathf.Abs(times[i]-actor.time[i])<.00001f;
                bool blocked=paused||freeze||silk||anticipating||!summary.alive||model==null||!model.activeInHierarchy;
                actor.stopped[i]=same&&!blocked?actor.stopped[i]+Mathf.Max(0f,world-actor.previousWorld):0f;
                if(!same||blocked)actor.reported[i]=false;
                summary.longestUnexplainedStall=Mathf.Max(summary.longestUnexplainedStall,actor.stopped[i]);
                if(actor.stopped[i]>=2f&&!actor.reported[i]){actor.reported[i]=true;summary.stalledEpisodes++;report.stalledEpisodes++;}
                actor.state[i]=state;actor.time[i]=times[i];
            }
            actor.previousWorld=world;Vector3 p=model==null?Vector3.zero:model.transform.position;report.rows++;
            Write(unitsCsv,Time.frameCount,Time.unscaledTimeAsDouble,Time.timeAsDouble,world,summary.id,summary.team,summary.species,summary.original,summary.alive,summary.hp,
                summary.skillUses,summary.skillHits,summary.skillDamage,Read<string>(unit,"currentAction",""),Read<string>(driver,"requestedMotion",""),anticipating,charge,
                Read<float>(driver,"mammothChargeElapsed",-1f),valid,advance,finished,times[0],times[1],times[2],times[3],freeze,silk,controlled,paused,
                Read<int>(unit,"positionRevision",-1),revisions[0],revisions[1],revisions[2],revisions[3],p.x,p.y,p.z);
        }
        ObserveCamera(units,world);
    }
    private void ObserveCamera(IList units,float world)
    {
        var camera=Read<Camera>(battle,"mainCamera",null);var director=Read<CombatCameraDirector>(battle,"combatCameraDirector",null);if(camera==null||director==null)return;
        if(!cameraCaptured){cameraCaptured=true;homeFov=camera.fieldOfView;homeRotation=camera.transform.rotation;}
        float fov=Mathf.Abs(camera.fieldOfView-homeFov),rotation=Quaternion.Angle(camera.transform.rotation,homeRotation);
        report.maximumFovDelta=Mathf.Max(report.maximumFovDelta,fov);report.maximumRotationDelta=Mathf.Max(report.maximumRotationDelta,rotation);
        if(fov>.0001f)report.zoomViolations++;if(rotation>.001f)report.rotationViolations++;
        var source=Read<SpriteRenderer>(director,"anticipationPanSource",null);object owner=null;
        if(source!=null)foreach(object unit in units){var driver=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);if(driver!=null&&driver.GetComponent<SpriteRenderer>()==source){owner=unit;break;}}
        object presentation=Presentation(owner);
        double presentationStarted=Read<double>(presentation,"startedAt",-1d);
        bool pending=Field(presentation,"pending")!=null,body=SkillAction(owner),presented=presentation!=null,minimumHold=MinimumHold(presentation);
        bool active=director.AnticipationPanActive,alive=Read<bool>(owner,"alive",false);string team=Field(owner,"team")?.ToString()??"";
        bool returning=Read<bool>(director,"anticipationPanReturning",false);
        float zoom=director.AppliedAnticipationZoom;
        if(float.IsNaN(zoom)||float.IsInfinity(zoom)||zoom<.9999f||zoom>1.1401f)report.projectionZoomRangeViolations++;
        float safeZoom=float.IsNaN(zoom)||float.IsInfinity(zoom)||zoom<=0f?1f:zoom;
        report.finalProjectionZoom=zoom;report.maximumProjectionZoom=Mathf.Max(report.maximumProjectionZoom,zoom);
        if(zoom>1.0001f){report.projectionZoomSamples++;if(!active)report.projectionZoomReturningSamples++;}
        if(!active&&!returning&&Mathf.Abs(zoom-1f)<.0001f)report.projectionZoomRestoredSamples++;
        bool previousOwnerValid=FocusOwnerStillValid(),acceptedDuringReturn=false;
        if(active)
        {
            report.panSamples++;if(owner==null||team!="Player"||!presented||!pending&&!body&&!minimumHold||!alive)report.panSourceViolations++;
            bool changed=focusEpisode==null||!ReferenceEquals(focusOwner,owner)||
                focusEpisode.presentationStartedAt!=presentationStarted||!previousPanActive;
            if(changed)
            {
                if(focusEpisode!=null)
                {
                    if(previousOwnerValid)report.focusOwnerSwitchViolations++;else report.legalFocusOwnerChanges++;
                    if(focusEpisode.endedAt<0f){focusEpisode.endedAt=world;focusEpisode.endReason=previousOwnerValid?"REPLACED_WHILE_VALID":"ENDED_BEFORE_NEXT_SAMPLE";}
                    acceptedDuringReturn=!previousPanActive&&previousPanReturning;
                    if(acceptedDuringReturn)report.freshFocusAcceptedDuringReturn++;
                }
                focusOwner=owner;focusEpisode=new FocusEpisode{sequence=focusEpisodes.Count+1,id=Read<int>(owner,"id",-1),team=team,
                    species=Read<MonsterDefinition>(owner,"definition",null)?.monsterId??"",startedAt=world,
                    presentationStartedAt=presentationStarted,acceptedDuringObservedReturn=acceptedDuringReturn};focusEpisodes.Add(focusEpisode);
                double previousStart;
                if(owner!=null&&previousPresentationStarts.TryGetValue(owner,out previousStart)&&previousStart==presentationStarted)
                {focusEpisode.wasPreviouslyObservedPresentation=true;report.deferredPreviouslyObservedFocusStarts++;}
                if(team=="Player")alliedOwners.Add(focusEpisode.id);
            }
            if(pending)focusEpisode.pendingSamples++;
            if(body&&!pending){report.focusExecutionSamples++;focusEpisode.executionSamples++;}
            if(!pending&&!body&&minimumHold){report.focusMinimumHoldSamples++;focusEpisode.minimumHoldSamples++;}
            focusEpisode.maximumZoom=Mathf.Max(focusEpisode.maximumZoom,zoom);
        }
        else if(focusOwner!=null)
        {
            if(previousPanActive&&previousOwnerValid&&!Read<bool>(battle,"paused",false)&&
                !BombBrick.Game.Core.GameFrontend.BlocksGameplay&&Read<bool>(battle,"battleStarted",false)&&!Read<bool>(battle,"battleEnding",false))
                report.focusReleasedBeforeBodyEnd++;
            if(focusEpisode!=null&&focusEpisode.endedAt<0f){focusEpisode.endedAt=world;focusEpisode.endReason=previousOwnerValid?"RELEASED_WHILE_VALID":"SOURCE_FINISHED_OR_INVALID";}
        }
        int competingAlly=0,competingEnemy=0;
        if(active)foreach(object unit in units)if(!ReferenceEquals(unit,owner)&&Field(Presentation(unit),"pending")!=null)
        {if(Field(unit,"team")?.ToString()=="Player")competingAlly++;else competingEnemy++;}
        report.competingAllyPreparationSamples+=competingAlly;report.competingEnemyPreparationSamples+=competingEnemy;
        if(active&&focusEpisode!=null){focusEpisode.competingAllySamples+=competingAlly;focusEpisode.competingEnemySamples+=competingEnemy;}
        Vector2 appliedPan=director.AppliedAnticipationPanOffset,shake=Read<Vector2>(director,"appliedShakeOffset",Vector2.zero),total=appliedPan+shake;
        Vector2 uvMin=Vector2.one*((1f-1f/safeZoom)*.5f)-total/safeZoom,uvMax=Vector2.one*((1f+1f/safeZoom)*.5f)-total/safeZoom;
        float overshoot=Mathf.Max(0f,Mathf.Max(Mathf.Max(-uvMin.x,-uvMin.y),Mathf.Max(uvMax.x-1f,uvMax.y-1f)));
        if(active||returning){report.maximumCropOvershoot=Mathf.Max(report.maximumCropOvershoot,overshoot);if(overshoot>.0001f)report.cropBoundsViolations++;}
        Vector3 view=new Vector3(-1f,-1f,-1f);float error=-1f;Vector3 offset=director.AnticipationPanOffset;
        if(source!=null&&source.sprite!=null){Vector3 center=((Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Flags).Invoke(null,new object[]{source.sprite})).center;if(source.flipX)center.x=-center.x;if(source.flipY)center.y=-center.y;
            view=camera.WorldToViewportPoint(source.transform.TransformPoint(center));error=Vector2.Distance(new Vector2(view.x,view.y),Vector2.one*.5f);report.maximumCenterError=Mathf.Max(report.maximumCenterError,error);}
        report.maximumPanOffset=Mathf.Max(report.maximumPanOffset,offset.magnitude);
        Write(cameraCsv,Time.frameCount,Time.unscaledTimeAsDouble,world,active,Read<int>(owner,"id",-1),team,pending,alive,owner!=null,offset.x,offset.y,offset.z,view.x,view.y,error,camera.fieldOfView,fov,rotation,
            presented,body,Read<int>(focusOwner,"id",-1),returning,zoom,appliedPan.x,appliedPan.y,shake.x,shake.y,uvMin.x,uvMin.y,uvMax.x,uvMax.y,competingAlly,competingEnemy,minimumHold,
            focusEpisode==null?0:focusEpisode.sequence,presentationStarted,previousOwnerValid,acceptedDuringReturn);
        if(!active&&!returning&&Mathf.Abs(zoom-1f)<.0001f)focusOwner=null;
        previousPanActive=active;
        previousPanReturning=returning;
        previousPresentationStarts.Clear();
        foreach(object unit in units){object record=Presentation(unit);if(record!=null)previousPresentationStarts[unit]=Read<double>(record,"startedAt",-1d);}
    }
    private object Presentation(object unit)
    {
        var presentations=Field(battle,"localSkillPresentations") as IList;
        if(unit!=null&&presentations!=null)foreach(object state in presentations)
            if(Read<bool>(state,"active",false)&&ReferenceEquals(Field(state,"unit"),unit))return state;
        return null;
    }
    private bool SkillAction(object unit)
    {
        var method=typeof(MonsterAutoBattlePrototype).GetMethod("CommonSkillActionActive",Flags);
        return unit!=null&&method!=null&&(bool)method.Invoke(method.IsStatic?null:battle,new[]{unit});
    }
    private bool FocusOwnerStillValid()
    {
        object held=Presentation(focusOwner);var model=Read<GameObject>(focusOwner,"model",null);
        return focusEpisode!=null&&held!=null&&Read<double>(held,"startedAt",-1d)==focusEpisode.presentationStartedAt&&
            Read<bool>(focusOwner,"alive",false)&&Read<float>(focusOwner,"hp",0f)>0f&&model!=null&&model.activeInHierarchy&&
            Read<double>(held,"recoveryAt",-1d)<0d&&(Field(held,"pending")!=null||SkillAction(focusOwner)||MinimumHold(held));
    }
    private static bool MinimumHold(object presentation)
    {
        double committed=Read<double>(presentation,"committedAt",-1d),elapsed=Time.timeAsDouble-committed;
        return presentation!=null&&Field(presentation,"pending")==null&&Read<double>(presentation,"recoveryAt",-1d)<0d&&
            committed>=0d&&elapsed>=0d&&elapsed<.12d;
    }
    private void Complete(string completion)
    {
        if(Finished)return;Finished=true;unitsCsv?.Dispose();cameraCsv?.Dispose();report.completion=completion;
        string primaryOutput=Read<string>(primary,"output",null),path=primaryOutput==null?null:Path.Combine(primaryOutput,"complete.txt");report.primaryResult=path!=null&&File.Exists(path)?File.ReadAllText(path):"";
        var summaries=new List<UnitReport>();foreach(var actor in actors.Values)summaries.Add(actor.summary);report.units=summaries.ToArray();
        report.focusEpisodes=focusEpisodes.ToArray();
        report.uniqueAlliedOwners=alliedOwners.Count;report.alliedOwnerIds=new int[alliedOwners.Count];alliedOwners.CopyTo(report.alliedOwnerIds);Array.Sort(report.alliedOwnerIds);
        report.result=completion=="PRIMARY_FINISHED"&&report.frames>0&&report.panSamples>0&&report.focusExecutionSamples>0&&report.projectionZoomSamples>0&&
            report.panSourceViolations+report.zoomViolations+report.rotationViolations+report.stalledEpisodes+report.orphanCallbackSamples+
            report.focusOwnerSwitchViolations+report.focusReleasedBeforeBodyEnd+report.projectionZoomRangeViolations+report.cropBoundsViolations+
            report.deferredPreviouslyObservedFocusStarts==0?"OBSERVED_CONTRACT_PASS":"REVIEW_REQUIRED";
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(output,"complete.txt"),completion);
    }
    private static void Write(StreamWriter writer,params object[] values)
    {
        var line=new StringBuilder();for(int i=0;i<values.Length;i++){if(i>0)line.Append(',');object value=values[i];string text=value is IFormattable?((IFormattable)value).ToString(value is float||value is double?"R":null,CultureInfo.InvariantCulture):value?.ToString()??"";
            line.Append('"').Append(text.Replace("\"","\"\"")).Append('"');}writer.WriteLine(line);
    }
    private static object Field(object value,string name)=>value==null?null:value.GetType().GetField(name,Flags)?.GetValue(value);
    private static T Read<T>(object value,string name,T fallback){object found=Field(value,name);return found is T?(T)found:fallback;}
    private void OnDestroy(){if(begun&&!Finished)Complete("OBSERVER_DESTROYED_PARTIAL");}
}
