using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class ExclusiveSkillTimeRuntimeQa
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
        if((bool)typeof(SkillFocusRuntimeQaObserver).GetField("prepared",flags).GetValue(primary)||
           (bool)typeof(SkillFocusRuntimeQaObserver).GetField("seenBattle",flags).GetValue(primary))
            throw new InvalidOperationException("Attach before the primary observer prepares or starts combat.");
        if(primary.GetComponent<ExclusiveSkillTimeRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("Exclusive-time diagnostics are already attached.");
        string output=Path.GetFullPath(Path.Combine("Logs/ExclusiveSkillTimeQA/20261019",label));
        if(Directory.Exists(output))throw new IOException("Exclusive-time QA output already exists.");
        var observer=primary.gameObject.AddComponent<ExclusiveSkillTimeRuntimeQaObserver>();
        try{observer.Begin(primary,label,output);}
        catch{UnityEngine.Object.DestroyImmediate(observer);throw;}
    }
}

// Read-only diagnostic sampling and buffered CSV I/O are not a frame-rate benchmark.
[DefaultExecutionOrder(10070)]
public sealed class ExclusiveSkillTimeRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const float Epsilon=.0001f;
    private static readonly string[] DriverTimes={"motionElapsed","earthPulseElapsed","tyrannoStompElapsed",
        "tyrannoMarchElapsed","tyrannoMawElapsed","mantisGuillotineElapsed","mantisReaperTime",
        "ninjaShadowElapsed","mantisCrossElapsed","bearRagingElapsed","beetleFlipElapsed","mammothChargeElapsed"};
    private readonly Dictionary<object,Sample> previous=new Dictionary<object,Sample>();
    private readonly Dictionary<Component,Projectile> previousProjectiles=new Dictionary<Component,Projectile>();
    private readonly Dictionary<string,int> exceptions=new Dictionary<string,int>();
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private StreamWriter csv,projectileCsv;
    private string label,output,previousPhase="";
    private object previousOwner;
    private CombatSkillClock previousClock;
    private int previousFrame=-1,frames,rows,introFrames,executingFrames,normalFrames,initialUnits;
    private float previousWorld,previousOwnerTime;
    private double startedAt;
    private bool begun,seenExecution;
    private int worldFreezeComparisons,worldDriftViolations,timeScaleViolations,introShadeViolations,executionShadeViolations;
    private int nonCasterComparisons,cooldownViolations,skillTimerViolations,driverComparisons,driverViolations;
    private int statusComparisons,statusRemainingViolations,statusTickViolations,unexpectedPositionViolations;
    private int aiComparisons,aiViolations,casterClockProgressSamples,casterAnimationProgressSamples;
    private int ownedProjectileSamples,ownedProjectileProgressSamples,otherProjectileComparisons,otherProjectileProgressViolations;
    private int normalResumeComparisons,normalWorldProgressSamples;
    public bool Finished {get;private set;}

    private sealed class Status
    {public CombatStatusKind kind;public float remaining,tick;public int stacks,holds;}
    private sealed class Sample
    {
        public int id,frame,skills,basics,route;public string team,species,motion,action,requested;
        public float hp,cooldown,skillTimer,decision,repath;public Vector3 position;
        public bool alive,forced;public readonly float[] driverTimes=new float[DriverTimes.Length];
        public readonly Dictionary<object,Status> statuses=new Dictionary<object,Status>();
    }
    private sealed class Projectile
    {public Component component;public CombatSkillClock clock;public Vector3 position;public float age;}
    [Serializable] private sealed class ExceptionCount
    {public string classification;public int samples;}
    [Serializable] private sealed class Report
    {
        public string label,result,completion,primaryResult;
        public string limitation="Read-only natural battle. Comparisons require consecutive frames with the same phase and cast token. Newly applied/removed statuses, new motions, death and owned forced movement are separately classified. Same-object status timer decreases remain violations. AI evidence uses basic-attack counts and decision/repath timer ticks, not an instrumented method-call counter. Buffered diagnostic I/O is not an FPS benchmark.";
        public bool primaryFinished;
        public int initialUnits,frames,rows,introFrames,executingFrames,normalFrames;
        public int worldFreezeComparisons,worldDriftViolations,timeScaleViolations,introShadeViolations,executionShadeViolations;
        public int nonCasterComparisons,cooldownViolations,skillTimerViolations,driverComparisons,driverViolations;
        public int statusComparisons,statusRemainingViolations,statusTickViolations,unexpectedPositionViolations;
        public int aiComparisons,aiViolations,casterClockProgressSamples,casterAnimationProgressSamples;
        public int ownedProjectileSamples,ownedProjectileProgressSamples,otherProjectileComparisons,otherProjectileProgressViolations;
        public int normalResumeComparisons,normalWorldProgressSamples;
        public double realSeconds;public ExceptionCount[] exceptions;
    }

    public void Begin(SkillFocusRuntimeQaObserver owner,string requestedLabel,string directory)
    {
        primary=owner;label=requestedLabel;output=directory;
        Directory.CreateDirectory(output);
        csv=new StreamWriter(Path.Combine(output,"samples.csv"),false,new UTF8Encoding(false),65536);
        projectileCsv=new StreamWriter(Path.Combine(output,"projectiles.csv"),false,new UTF8Encoding(false),65536);
        csv.WriteLine("frame,unscaledTime,phase,ownerId,samePhaseOwner,battleElapsed,timeScale,tokenTime,tokenDelta,directorActive,pending,shadeVisible,shadeAlpha,id,team,species,alive,hp,x,y,z,motion,requestedMotion,driverFrame,driverTimers,cooldown,skillTimer,decisionTimer,repathTimer,routeIndex,basicAttacks,skillUses,action,ownedForcedMotion,statuses,classifications,cooldownDecrease,skillTimerDecrease,driverAdvance,statusRemainingDecrease,statusTickDecrease,aiTick,positionChanged");
        projectileCsv.WriteLine("frame,unscaledTime,phase,ownerId,instanceId,type,owned,age,ageDelta,x,y,z,moved,compared,violation");
        startedAt=Time.unscaledTimeAsDouble;begun=true;
    }

    private void LateUpdate()
    {
        if(!begun||Finished)return;
        try
        {
            if(primary==null){Complete("PRIMARY_MISSING",false);return;}
            battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
            bool done=Read<bool>(primary,"finished",false);
            var units=Field(battle,"units") as IList;
            if(units!=null&&Read<bool>(primary,"seenBattle",false))Observe(units,done);
            if(done)Complete("PRIMARY_FINISHED",true);
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(output,"exception.txt"),error.ToString());
            Complete("OBSERVER_EXCEPTION",false);
            Debug.LogException(error);
        }
    }

    private void Observe(IList units,bool done)
    {
        frames++;if(initialUnits==0)initialUnits=units.Count;
        var clock=Read<CombatSkillClock>(battle,"skillFocusClock",null);
        object owner=Field(battle,"skillFocusUnit");int ownerId=Read<int>(owner,"id",-1);
        bool pending=Read<bool>(battle,"skillFocusCastPending",false);
        string phase=pending?"intro":clock!=null&&clock.Executing?"executing":"none";
        if(phase=="intro")introFrames++;
        else if(phase=="executing"){executingFrames++;seenExecution=true;}
        else normalFrames++;
        float world=Read<float>(battle,"battleElapsed",0f),tokenTime=clock==null?0f:clock.Time;
        bool consecutive=!done&&previousFrame==Time.frameCount-1&&phase==previousPhase&&
            ReferenceEquals(clock,previousClock)&&ReferenceEquals(owner,previousOwner);
        bool frozen=phase!="none";
        var director=Read<CombatCameraDirector>(battle,"combatCameraDirector",null);
        var shade=Read<SpriteRenderer>(battle,"skillFocusShade",null);
        bool shadeVisible=shade!=null&&shade.enabled&&shade.gameObject.activeInHierarchy;
        float alpha=shade==null?0f:shade.color.a;
        if(!done&&frozen)
        {
            if(Time.timeScale!=0f)timeScaleViolations++;
            if(phase=="intro"&&(!shadeVisible||alpha<.64f))introShadeViolations++;
            if(phase=="executing"&&shadeVisible&&alpha>.001f)executionShadeViolations++;
            if(consecutive)
            {
                worldFreezeComparisons++;
                if(Mathf.Abs(world-previousWorld)>Epsilon)worldDriftViolations++;
                if(phase=="executing"&&tokenTime>previousOwnerTime+Epsilon)casterClockProgressSamples++;
            }
        }
        if(consecutive&&phase=="none"&&seenExecution&&Time.timeScale>0f)
        {normalResumeComparisons++;if(world>previousWorld+Epsilon)normalWorldProgressSamples++;}
        float expectedStep=clock==null?0f:clock.DeltaTime;
        foreach(object unit in units)
        {
            Sample current=Snapshot(unit,owner,clock),old;
            bool compare=consecutive&&frozen&&previous.TryGetValue(unit,out old);
            var notes=new List<string>();
            bool cooldown=false,skill=false,driverAdvance=false,statusRemaining=false,statusTick=false,ai=false,moved=false;
            if(compare)
            {
                old=previous[unit];bool caster=ReferenceEquals(unit,owner);
                if(!caster)nonCasterComparisons++;
                if(current.hp!=old.hp)Classify(notes,"hp_changed");
                if(current.alive!=old.alive)Classify(notes,"life_changed");
                if(current.motion!=old.motion||current.requested!=old.requested)Classify(notes,"new_motion");
                if(current.skills!=old.skills)Classify(notes,"skill_use_changed");
                bool inspect=!caster||phase=="intro";
                if(inspect)
                {
                    cooldown=current.cooldown<old.cooldown-Epsilon;
                    skill=current.skillTimer<old.skillTimer-Epsilon;
                    if(cooldown)cooldownViolations++;if(skill)skillTimerViolations++;
                    if(current.alive&&old.alive&&current.motion==old.motion&&current.requested==old.requested&&
                       current.skills==old.skills&&current.basics==old.basics)
                    {
                        driverComparisons++;
                        for(int i=0;i<DriverTimes.Length;i++)if(current.driverTimes[i]>old.driverTimes[i]+Epsilon)driverAdvance=true;
                        if(current.frame!=old.frame)driverAdvance=true;
                        if(driverAdvance)driverViolations++;
                    }
                    moved=(current.position-old.position).sqrMagnitude>.00000001f;
                    if(moved)
                    {
                        if(phase=="executing"&&(current.forced||old.forced))Classify(notes,"owned_forced_movement");
                        else if(current.hp!=old.hp||current.alive!=old.alive)Classify(notes,"movement_with_hit_or_death");
                        else unexpectedPositionViolations++;
                    }
                }
                else
                {
                    for(int i=0;i<DriverTimes.Length;i++)if(current.driverTimes[i]>old.driverTimes[i]+Epsilon)
                    {casterAnimationProgressSamples++;break;}
                    if(current.frame!=old.frame)Classify(notes,"caster_frame_progress");
                }
                foreach(var item in current.statuses)
                {
                    Status before;
                    if(!old.statuses.TryGetValue(item.Key,out before)){Classify(notes,"new_status");continue;}
                    statusComparisons++;
                    if(item.Value.remaining<before.remaining-Epsilon)statusRemaining=true;
                    if(item.Value.tick<before.tick-Epsilon)statusTick=true;
                }
                foreach(var item in old.statuses)
                    if(!current.statuses.ContainsKey(item.Key))Classify(notes,"removed_status");
                if(statusRemaining)statusRemainingViolations++;if(statusTick)statusTickViolations++;
                if(current.alive&&old.alive)
                {
                    aiComparisons++;
                    ai=current.basics>old.basics||phase=="intro"&&
                        (current.decision<old.decision-Epsilon||current.repath<old.repath-Epsilon)||
                        LooksLikeTick(old.decision-current.decision,expectedStep)||
                        LooksLikeTick(old.repath-current.repath,expectedStep);
                    if(ai)aiViolations++;
                    if(current.action!=old.action)Classify(notes,"action_changed");
                    if(current.route!=old.route)Classify(notes,"route_changed");
                }
            }
            previous[unit]=current;rows++;
            Write(csv,Time.frameCount,Time.unscaledTimeAsDouble,phase,ownerId,consecutive,world,Time.timeScale,
                tokenTime,expectedStep,director!=null&&director.SkillFocusActive,pending,shadeVisible,alpha,
                current.id,current.team,current.species,current.alive,current.hp,current.position.x,current.position.y,current.position.z,
                current.motion,current.requested,current.frame,Timers(current.driverTimes),current.cooldown,current.skillTimer,
                current.decision,current.repath,current.route,current.basics,current.skills,current.action,current.forced,
                Statuses(current.statuses),string.Join("|",notes.ToArray()),cooldown,skill,driverAdvance,statusRemaining,statusTick,ai,moved);
        }
        ObserveProjectiles(phase,ownerId,clock,consecutive&&frozen);
        previousFrame=Time.frameCount;previousPhase=phase;previousClock=clock;previousOwner=owner;
        previousWorld=world;previousOwnerTime=tokenTime;
    }

    private static bool LooksLikeTick(float decrease,float step)=>step>Epsilon&&decrease>Epsilon&&
        Mathf.Abs(decrease-step)<Mathf.Max(.0002f,step*.08f);

    private Sample Snapshot(object unit,object owner,CombatSkillClock clock)
    {
        var model=Read<GameObject>(unit,"model",null);
        var driver=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);
        var definition=Read<MonsterDefinition>(unit,"definition",null);
        var value=new Sample{id=Read<int>(unit,"id",-1),team=Convert.ToString(Field(unit,"team")),
            species=definition==null?"":definition.monsterId,alive=Read<bool>(unit,"alive",false)&&model!=null&&model.activeInHierarchy,
            hp=Read<float>(unit,"hp",0f),position=model==null?Vector3.zero:model.transform.position,
            motion=driver==null?"":driver.CurrentMotionName,requested=Read<string>(driver,"requestedMotion",""),frame=driver==null?-1:driver.CurrentFrameIndex,
            cooldown=Read<float>(unit,"cooldown",0f),skillTimer=Read<float>(unit,"skillTimer",0f),decision=Read<float>(unit,"decisionTimer",0f),
            repath=Read<float>(unit,"repathTimer",0f),route=Read<int>(unit,"routeIndex",0),skills=Read<int>(unit,"skillUses",0),
            basics=Read<int>(unit,"basicAttackCount",0),action=Read<string>(unit,"currentAction","")};
        value.forced=clock!=null&&Read<bool>(unit,"displacing",false)&&ReferenceEquals(Field(unit,"displacementClock"),clock)||
            Linked(unit,"preyDraggedBy",owner)||Linked(unit,"griffinHeldBy",owner)||Linked(unit,"beetleFlippedBy",owner)||
            Linked(unit,"tyrannoPushedBy",owner)||Linked(unit,"tyrannoMawPulledBy",owner)||
            Linked(Field(Field(unit,"beetleOverturnedBy"),"owner"),"source",owner)||
            Linked(Field(Field(unit,"beetleGravityHeld"),"field"),"source",owner);
        for(int i=0;i<DriverTimes.Length;i++)value.driverTimes[i]=Read<float>(driver,DriverTimes[i],0f);
        var statuses=Read<CombatStatusState>(unit,"combatStatuses",null);
        if(statuses!=null)foreach(var status in statuses.ActiveEffects)
            value.statuses[status]=new Status{kind=status.kind,remaining=status.remaining,tick=status.tickRemaining,stacks=status.stacks,holds=status.activeHolds};
        return value;
    }

    private void ObserveProjectiles(string phase,int ownerId,CombatSkillClock clock,bool compare)
    {
        var projectiles=new List<Component>();
        AddProjectiles<PixelStarProjectile>(projectiles);AddProjectiles<PixelMonkeyReturn>(projectiles);
        AddProjectiles<PixelMonkeyMortar>(projectiles);AddProjectiles<PixelSpiderCaptureNet>(projectiles);AddProjectiles<PixelWizardSupernova>(projectiles);
        var next=new Dictionary<Component,Projectile>();
        foreach(var component in projectiles)
        {
            object boxed=Field(component,"flight");
            CombatSkillClock token=boxed is CombatProjectileSource.Flight?((CombatProjectileSource.Flight)boxed).skillClock:null;
            var current=new Projectile{component=component,clock=token,position=component.transform.position,
                age=Read<float>(component,"elapsed",Read<float>(component,"age",0f))};
            Projectile old;bool compared=compare&&previousProjectiles.TryGetValue(component,out old);
            float delta=compared?current.age-previousProjectiles[component].age:0f;
            bool moved=compared&&(current.position-previousProjectiles[component].position).sqrMagnitude>.00000001f;
            bool owned=phase=="executing"&&token!=null&&ReferenceEquals(token,clock);
            bool violation=compared&&!owned&&(delta>Epsilon||moved);
            if(owned){ownedProjectileSamples++;if(compared&&(delta>Epsilon||moved))ownedProjectileProgressSamples++;}
            else if(compared){otherProjectileComparisons++;if(violation)otherProjectileProgressViolations++;}
            Write(projectileCsv,Time.frameCount,Time.unscaledTimeAsDouble,phase,ownerId,component.GetInstanceID(),component.GetType().Name,
                owned,current.age,delta,current.position.x,current.position.y,current.position.z,moved,compared,violation);
            next[component]=current;
        }
        previousProjectiles.Clear();foreach(var item in next)previousProjectiles.Add(item.Key,item.Value);
    }

    private static void AddProjectiles<T>(List<Component> list)where T:Component
    {foreach(var projectile in UnityEngine.Object.FindObjectsOfType<T>())list.Add(projectile);}
    private static bool Linked(object unit,string field,object owner)=>owner!=null&&ReferenceEquals(Field(unit,field),owner);
    private void Classify(List<string> notes,string name)
    {if(notes.Contains(name))return;notes.Add(name);int count;exceptions.TryGetValue(name,out count);exceptions[name]=count+1;}
    private static string Timers(float[] values)
    {var text=new StringBuilder();for(int i=0;i<values.Length;i++){if(i>0)text.Append('|');text.Append(DriverTimes[i]).Append('=').Append(values[i].ToString("R",CultureInfo.InvariantCulture));}return text.ToString();}
    private static string Statuses(Dictionary<object,Status> values)
    {var text=new StringBuilder();foreach(var item in values.Values){if(text.Length>0)text.Append('|');text.Append(item.kind).Append(':').Append(item.remaining.ToString("R",CultureInfo.InvariantCulture)).Append('/').Append(item.tick.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(item.stacks).Append(':').Append(item.holds);}return text.ToString();}

    private void Complete(string completion,bool primaryFinished)
    {
        if(Finished)return;Finished=true;csv?.Dispose();projectileCsv?.Dispose();
        string directory=Read<string>(primary,"output",null),path=directory==null?null:Path.Combine(directory,"complete.txt");
        string primaryResult=path!=null&&File.Exists(path)?File.ReadAllText(path):"";
        int violations=worldDriftViolations+timeScaleViolations+introShadeViolations+executionShadeViolations+
            cooldownViolations+skillTimerViolations+driverViolations+statusRemainingViolations+statusTickViolations+
            unexpectedPositionViolations+aiViolations+otherProjectileProgressViolations;
        bool evidence=introFrames>0&&executingFrames>0&&worldFreezeComparisons>0&&nonCasterComparisons>0&&
            driverComparisons>0&&aiComparisons>0&&casterClockProgressSamples>0&&casterAnimationProgressSamples>0&&normalWorldProgressSamples>0;
        var counts=new List<ExceptionCount>();foreach(var item in exceptions)counts.Add(new ExceptionCount{classification=item.Key,samples=item.Value});
        string result=primaryFinished&&primaryResult.IndexOf("TIMEOUT",StringComparison.OrdinalIgnoreCase)<0&&violations==0&&evidence?"PASS":"REVIEW_REQUIRED";
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(new Report{
            label=label,result=result,completion=completion,primaryFinished=primaryFinished,primaryResult=primaryResult,initialUnits=initialUnits,
            frames=frames,rows=rows,introFrames=introFrames,executingFrames=executingFrames,normalFrames=normalFrames,
            worldFreezeComparisons=worldFreezeComparisons,worldDriftViolations=worldDriftViolations,timeScaleViolations=timeScaleViolations,
            introShadeViolations=introShadeViolations,executionShadeViolations=executionShadeViolations,nonCasterComparisons=nonCasterComparisons,
            cooldownViolations=cooldownViolations,skillTimerViolations=skillTimerViolations,driverComparisons=driverComparisons,driverViolations=driverViolations,
            statusComparisons=statusComparisons,statusRemainingViolations=statusRemainingViolations,statusTickViolations=statusTickViolations,
            unexpectedPositionViolations=unexpectedPositionViolations,aiComparisons=aiComparisons,aiViolations=aiViolations,
            casterClockProgressSamples=casterClockProgressSamples,casterAnimationProgressSamples=casterAnimationProgressSamples,
            ownedProjectileSamples=ownedProjectileSamples,ownedProjectileProgressSamples=ownedProjectileProgressSamples,
            otherProjectileComparisons=otherProjectileComparisons,otherProjectileProgressViolations=otherProjectileProgressViolations,
            normalResumeComparisons=normalResumeComparisons,normalWorldProgressSamples=normalWorldProgressSamples,
            realSeconds=Time.unscaledTimeAsDouble-startedAt,exceptions=counts.ToArray()},true));
        File.WriteAllText(Path.Combine(output,"complete.txt"),result+" "+completion);
        Debug.Log("[Exclusive Skill Time QA] "+result+" "+output+"; violations="+violations);
    }

    private static void Write(StreamWriter writer,params object[] values)
    {
        for(int i=0;i<values.Length;i++)
        {
            if(i>0)writer.Write(',');object value=values[i];
            string text=value is double?((double)value).ToString("R",CultureInfo.InvariantCulture):
                value is float?((float)value).ToString("R",CultureInfo.InvariantCulture):
                value is IFormattable?((IFormattable)value).ToString(null,CultureInfo.InvariantCulture):Convert.ToString(value);
            if(text.IndexOfAny(new[]{',','"','\r','\n'})>=0)writer.Write('"'+text.Replace("\"","\"\"")+'"');else writer.Write(text);
        }
        writer.WriteLine();
    }
    private static object Field(object value,string name)=>value==null?null:value.GetType().GetField(name,Flags)?.GetValue(value);
    private static T Read<T>(object value,string name,T fallback){object found=Field(value,name);return found is T?(T)found:fallback;}
    private void OnDestroy(){if(begun&&!Finished)Complete("OBSERVER_DESTROYED_PARTIAL",false);}
}
