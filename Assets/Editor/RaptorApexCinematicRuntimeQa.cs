using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Native presentation capture and reflection deliberately add overhead. This is not an FPS benchmark.
[InitializeOnLoad]
public static class RaptorApexCinematicRuntimeQa
{
    public const string Root="Logs/RaptorApexCinematicQA/20261020";
    private const string Pending="RaptorApexCinematicRuntimeQa.Pending";
    internal const string OriginalScene="RaptorApexCinematicRuntimeQa.OriginalScene";
    internal const string RestoreScene="RaptorApexCinematicRuntimeQa.RestoreScene";

    static RaptorApexCinematicRuntimeQa()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))
            {
                SessionState.SetBool(Pending,false);
                EditorApplication.delayCall+=()=>Start(
                    SessionState.GetString(Pending+".Label","natural_8v8"),
                    SessionState.GetBool(Pending+".Clip",false));
            }
            else if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(RestoreScene,false))
            {
                SessionState.SetBool(RestoreScene,false);
                EditorApplication.delayCall+=()=>
                {
                    Guard();
                    string scene=SessionState.GetString(OriginalScene,"Assets/Game/Scenes/Boot.unity");
                    if(!string.IsNullOrEmpty(scene)&&File.Exists(scene)&&!SceneManager.GetActiveScene().isDirty)
                        EditorSceneManager.OpenScene(scene);
                };
            }
        };
    }

    public static void Launch(string label,bool captureClip=false)
    {
        Guard();
        if(EditorApplication.isPlaying){Start(label,captureClip);return;}
        if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before QA.");
        ValidateLabel(label);
        SessionState.SetString(OriginalScene,SceneManager.GetActiveScene().path);
        SessionState.SetString(Pending+".Label",label);
        SessionState.SetBool(Pending+".Clip",captureClip);
        SessionState.SetBool(Pending,true);
        EditorSceneManager.OpenScene(CombatEffectTestSceneSetup.ScenePath);
        EditorApplication.EnterPlaymode();
    }

    public static void Start(string label,bool captureClip=false)
    {
        Guard();ValidateLabel(label);
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>()!=null||
           UnityEngine.Object.FindObjectOfType<RaptorApexCinematicRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("Another QA observer is already active.");
        var host=new GameObject("Raptor Apex cinematic observer (editor only)");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<RaptorApexCinematicRuntimeQaObserver>().Begin(label,captureClip);
    }

    internal static void Guard()
    {
        if(Application.dataPath!="C:/Unity/BecomeZ_BombBrick/Assets")
            throw new InvalidOperationException("Wrong project.");
    }
    private static void ValidateLabel(string label)
    {
        if(string.IsNullOrWhiteSpace(label)||label.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))
            throw new ArgumentException("Invalid QA label.");
    }
}

[DefaultExecutionOrder(10200)]
public sealed class RaptorApexCinematicRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private const int Seed=20261020;
    private static readonly string[] Roster={"velos","krogal","magril","flotis","seolgason","atlagon","igvire","haeta"};
    private static readonly float[] CaptureTimes={.4f,.95f,1.65f,2.85f,3.85f,4.95f,5.7f};
    private readonly Dictionary<FieldInfo,object> configuration=new Dictionary<FieldInfo,object>();
    private readonly List<Episode> episodes=new List<Episode>();
    private readonly HashSet<object> returnedWings=new HashSet<object>();
    private readonly HashSet<object> registeredShinySources=new HashSet<object>();
    private readonly Queue<Shot> shots=new Queue<Shot>();
    private readonly StringBuilder frames=new StringBuilder("file,kind,episode,frame,realtime,unscaled,battleElapsed,cinematicElapsed,timeScale,captureDelta,width,height,requestRealtime,requestUnscaled,requestElapsed\n");
    private readonly StringBuilder samples=new StringBuilder("frame,realtime,world,cinematic,episode,elapsed,sourceId,team,timeScale,captureDelta,clockActive,clockExecuting,queue,skills,basics,alive,freezePositionDelta,freezeHpDelta,freezeCooldownDelta,freezeSkillTimerDelta,freezeApexDelta,freezeStatusChanged,freezeDriverChanged,battleElapsedDelta,wingAwake,wingReveal,wingEnabled,wingAlpha,wingOrder,sourceOrder\n");
    private readonly StringBuilder unitSamples=new StringBuilder("frame,episode,unit,team,alive,x,y,z,hp,cooldown,skillTimer,apex,skills,basics,motion,animFrame,status\n");
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype previousBattle,battle;
    private object priorState;
    private Episode current;
    private Dictionary<object,Snapshot> frozen;
    private string output,label,raptorSpecies="velos";
    private bool captureClip,awaitScene,configured,finished,restored,seenBattle,shinyRequested;
    private float initialTimeScale,initialCaptureDelta;
    private double timeout,startedAt,normalSince=-1d,nextClipAt,clipUntil;
    private int rows,clipFrameCount,maximumQueue,freezeViolations,clockViolations,rateViolations;
    private int unitsAtStart,transcendedPlayer,transcendedEnemy;
    private float frozenBattleElapsed,normalWorldSince=-1f;
    private string pendingResult;

    [Serializable] private sealed class Episode
    {
        public int number,sourceId,samples,freezeViolations,clockViolations,rateViolations,frameStart,frameEnd;
        public string team,species;
        public double realStart,realEnd;
        public float maximumElapsed,savedSpeed,apexAtStart,apexAtEnd,worldAtStart,worldAtEnd;
        public bool completed,returnedWingSeen,returnedWingAfterOneWorldSecond,registeredShinyFrames;
        public float returnedWingAtWorld,returnMovementDistance;
        public int particlesAtReturn,particlesAfterOneWorldSecond;
        public string particleSignatureAtReturn,particleSignatureAfterOneWorldSecond;
        public string[] screenshots;
        [NonSerialized] public object source;
        [NonSerialized] public readonly List<string> files=new List<string>();
        [NonSerialized] public readonly HashSet<int> captured=new HashSet<int>();
        [NonSerialized] public Vector3 returnPosition;
    }
    private sealed class Snapshot
    {
        public Vector3 position;
        public float hp,cooldown,skillTimer,apex;
        public int skills,basics,driverFrame,spriteId;
        public string statuses,motion;
    }
    private sealed class Shot
    {
        public string file,kind;
        public int episode,requestFrame;
        public double realtime,unscaled;
        public float world,elapsed,scale,capture;
    }
    [Serializable] private sealed class Report
    {
        public string label,result,mode="NATURAL_PRODUCTION_8V8";
        public string limitation="Actual production AI skills in a fixed rank3 8:8 roster. Exactly one raptor on each team receives the existing rank4 progression before combat advances. No skill, activation, damage, HP, movement or cinematic is forced. Native end-of-frame PNG capture and reflection add overhead, so this is not a performance benchmark. Still timestamps record capture request; PNG frames record end-of-frame sampling. The harness does not configure a capture framerate.";
        public int seed,unitsAtStart,transcendedPlayer,transcendedEnemy,rows,episodes,maximumQueue,freezeViolations,clockViolations,rateViolations,skills,basics,clipFrames,wingFlowEvidenceCount;
        public float realSeconds,worldSeconds,finalTimeScale,finalCaptureDelta,resumedWorldSeconds;
        public string raptorSpecies;
        public bool shinyRequested;
        public bool allySeen,enemySeen,allyWingReturned,enemyWingReturned,configurationRestored,finalClockActive,finalCinematicActive;
        public Episode[] casts;
    }

    public void Begin(string requestedLabel,bool movie)
    {
        RaptorApexCinematicRuntimeQa.Guard();
        label=requestedLabel;captureClip=movie;shinyRequested=label.Contains("_shiny");
        raptorSpecies=new[]{"velok","raputo","wilo"}.FirstOrDefault(id=>label.Contains(id))??"velos";
        output=Path.GetFullPath(Path.Combine(RaptorApexCinematicRuntimeQa.Root,label));
        if(Directory.Exists(output))throw new IOException("QA output already exists.");
        Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"Native"));
        if(captureClip)Directory.CreateDirectory(Path.Combine(output,"Frames"));
        foreach(var field in typeof(CombatEffectTestConfiguration).GetFields(BindingFlags.Public|BindingFlags.Static))
            configuration[field]=field.GetValue(null) is IList list?list.Cast<object>().ToArray():field.GetValue(null);
        initialTimeScale=Time.timeScale;initialCaptureDelta=Time.captureDeltaTime;
        previousBattle=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
        SkillFocusRuntimeQa.Start("rank3_raptorapex_"+label);
        primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        primary.enabled=false;awaitScene=true;
        Set(primary,"capturedNeutral",true);Set(primary,"capturedFocused",true);
        string[] roster=(string[])Roster.Clone();roster[0]=raptorSpecies;
        if(label.Contains("_mirror"))for(int i=0;i<roster.Length;i++)roster[i]=raptorSpecies;
        CombatEffectTestConfiguration.AllyMonsterIds.Clear();CombatEffectTestConfiguration.AllyMonsterIds.AddRange(roster);
        CombatEffectTestConfiguration.EnemyMonsterIds.Clear();CombatEffectTestConfiguration.EnemyMonsterIds.AddRange(roster);
        CombatEffectTestConfiguration.AllyCount=CombatEffectTestConfiguration.EnemyCount=8;
        CombatEffectTestConfiguration.AllyRank=CombatEffectTestConfiguration.EnemyRank=ExpeditionMonsterRank.Rank3;
        CombatEffectTestConfiguration.AllyTranscended=CombatEffectTestConfiguration.EnemyTranscended=false;
        timeout=Time.realtimeSinceStartupAsDouble+120d;
        StartCoroutine(CaptureNative());
    }

    private void LateUpdate()
    {
        if(finished)return;
        try{Observe();}
        catch(Exception error){File.WriteAllText(Path.Combine(output,"exception.txt"),error.ToString());Complete("OBSERVER_EXCEPTION");Debug.LogException(error);}
    }

    private void Observe()
    {
        if(Time.realtimeSinceStartupAsDouble>timeout){Complete("REALTIME_LIMIT");return;}
        if(primary==null){Complete("PRIMARY_MISSING");return;}
        if(awaitScene)
        {
            var candidate=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
            if(candidate==null||ReferenceEquals(candidate,previousBattle))return;
            primary.enabled=true;awaitScene=false;return;
        }
        battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
        if(battle==null||!battle.IsSceneReady||!Read<bool>(primary,"prepared",false))return;
        var units=Units();
        if(!configured)
        {
            ConfigureRaptors(units);
            configured=true;
            File.WriteAllText(Path.Combine(output,"starting-roster.json"),RosterJson(units));
            return;
        }
        bool running=Read<bool>(battle,"battleStarted",false);
        if(!running&&!seenBattle)return;
        if(!seenBattle){seenBattle=true;startedAt=Time.realtimeSinceStartupAsDouble;RequestShot("neutral",null);}
        object state=Field(battle,"raptorCinematic");
        if(!ReferenceEquals(state,priorState))
        {
            if(current!=null)EndEpisode();
            if(state!=null)BeginEpisode(state,units);
            else {frozen=null;normalSince=Time.realtimeSinceStartupAsDouble;normalWorldSince=Read<float>(battle,"battleElapsed",0f);}
            priorState=state;
        }
        float elapsed=state==null?0f:Read<float>(state,"elapsed",0f);
        object source=state==null?null:Field(state,"unit");
        object queue=Field(battle,"raptorCinematicQueue");
        int queued=queue is ICollection collection?collection.Count:0;
        maximumQueue=Math.Max(maximumQueue,queued);
        int skills=units.Sum(u=>Read<int>(u,"skillUses",0)),basics=units.Sum(u=>Read<int>(u,"basicAttackCount",0));
        float position=0f,hp=0f,cooldown=0f,skillTimer=0f,apex=0f,worldDelta=0f;
        int status=0,driver=0;
        bool clockExecuting=false;
        var clock=state==null?null:Field(state,"clock");
        if(clock!=null)clockExecuting=Read<bool>(clock,"executing",false);
        if(current!=null&&state!=null)
        {
            current.samples++;current.maximumElapsed=Mathf.Max(current.maximumElapsed,elapsed);
            foreach(object unit in units)
            {
                Snapshot before;
                if(frozen==null||!frozen.TryGetValue(unit,out before))continue;
                Snapshot now=Take(unit);
                position=Mathf.Max(position,Vector3.Distance(before.position,now.position));
                hp=Mathf.Max(hp,Mathf.Abs(before.hp-now.hp));cooldown=Mathf.Max(cooldown,Mathf.Abs(before.cooldown-now.cooldown));
                skillTimer=Mathf.Max(skillTimer,Mathf.Abs(before.skillTimer-now.skillTimer));apex=Mathf.Max(apex,Mathf.Abs(before.apex-now.apex));
                if(before.statuses!=now.statuses)status++;
                if(before.driverFrame!=now.driverFrame||before.motion!=now.motion||before.spriteId!=now.spriteId)driver++;
                unitSamples.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R},{12},{13},{14},{15},\"{16}\"\n",
                    Time.frameCount,current.number,Read<int>(unit,"id",-1),Team(unit),Read<bool>(unit,"alive",false),now.position.x,now.position.y,now.position.z,now.hp,now.cooldown,now.skillTimer,now.apex,now.skills,now.basics,now.motion,now.driverFrame,now.statuses);
            }
            worldDelta=Mathf.Abs(Read<float>(battle,"battleElapsed",0f)-frozenBattleElapsed);
            if(position>.0001f||hp>.0001f||cooldown>.0001f||skillTimer>.0001f||apex>.0001f||worldDelta>.0001f||status>0||driver>0||skills!=frozen.Values.Sum(v=>v.skills)||basics!=frozen.Values.Sum(v=>v.basics))
            {freezeViolations++;current.freezeViolations++;}
            if(!CombatSkillClock.Active||clockExecuting){clockViolations++;current.clockViolations++;}
            if(Time.timeScale!=0f||Time.captureDeltaTime!=initialCaptureDelta){rateViolations++;current.rateViolations++;}
            for(int i=0;i<CaptureTimes.Length;i++)
                if(elapsed>=CaptureTimes[i]&&current.captured.Add(i))RequestShot("stage_"+i.ToString("D2")+"_"+CaptureTimes[i].ToString("0.00",CultureInfo.InvariantCulture),current);
            if(captureClip&&Time.realtimeSinceStartupAsDouble>=nextClipAt)
            {nextClipAt=Time.realtimeSinceStartupAsDouble+.05d;RequestShot("clip",current);}
            normalSince=-1d;normalWorldSince=-1f;clipUntil=Time.realtimeSinceStartupAsDouble+2.2d;
        }
        else
        {
            foreach(Episode episode in episodes)
            {
                var body=Renderer(episode.source);
                var wings=body==null?null:body.GetComponent<PixelRaptorApexWings>();
                if(!episode.completed||wings==null||!wings.IsAwakened||
                   !wings.IsVisible||Read<float>(episode.source,"raptorApexPredatorTimer",0f)<=0f)continue;
                if(!episode.returnedWingSeen)
                {
                    episode.returnedWingSeen=true;returnedWings.Add(episode.source);
                    episode.returnedWingAtWorld=Read<float>(battle,"battleElapsed",0f);
                    episode.returnPosition=Take(episode.source).position;
                    episode.particlesAtReturn=wings.WingParticles==null?0:wings.WingParticles.particleCount;
                    episode.particleSignatureAtReturn=ParticleSignature(wings.WingParticles);
                    RequestShot("returned_wings",episode);
                }
                else if(!episode.returnedWingAfterOneWorldSecond&&Read<float>(battle,"battleElapsed",0f)-episode.returnedWingAtWorld>=1f)
                {
                    episode.returnedWingAfterOneWorldSecond=true;
                    episode.returnMovementDistance=Vector3.Distance(episode.returnPosition,Take(episode.source).position);
                    episode.particlesAfterOneWorldSecond=wings.WingParticles==null?0:wings.WingParticles.particleCount;
                    episode.particleSignatureAfterOneWorldSecond=ParticleSignature(wings.WingParticles);
                    RequestShot("returned_wings_after_1_world_second",episode);
                }
            }
            if(captureClip&&Time.realtimeSinceStartupAsDouble<=clipUntil&&Time.realtimeSinceStartupAsDouble>=nextClipAt)
            {nextClipAt=Time.realtimeSinceStartupAsDouble+.05d;RequestShot("clip",null);}
        }
        var renderer=source==null?null:Renderer(source);
        var aura=renderer==null?null:renderer.GetComponent<PixelRaptorApexWings>();
        var wingRenderer=aura==null||aura.WingParticles==null?null:aura.WingParticles.GetComponent<ParticleSystemRenderer>();
        rows++;
        samples.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3},{4},{5:R},{6},{7},{8:R},{9:R},{10},{11},{12},{13},{14},{15},{16:R},{17:R},{18:R},{19:R},{20:R},{21},{22},{23:R},{24},{25:R},{26},{27:R},{28},{29}\n",
            Time.frameCount,Time.realtimeSinceStartupAsDouble,Read<float>(battle,"battleElapsed",0f),state!=null,current==null?0:current.number,elapsed,
            source==null?-1:Read<int>(source,"id",-1),source==null?"":Team(source),Time.timeScale,Time.captureDeltaTime,CombatSkillClock.Active,clockExecuting,queued,skills,basics,
            units.Count(u=>Read<bool>(u,"alive",false)),position,hp,cooldown,skillTimer,apex,status,driver,worldDelta,aura!=null&&aura.IsAwakened,aura==null?0f:aura.Reveal,
            wingRenderer!=null&&wingRenderer.enabled,wingRenderer==null?0f:aura.Opacity,wingRenderer==null?0:wingRenderer.sortingOrder,renderer==null?0:renderer.sortingOrder);
        bool ally=episodes.Any(e=>e.completed&&e.team=="Player"),enemy=episodes.Any(e=>e.completed&&e.team=="Enemy");
        bool allyWing=episodes.Any(e=>e.returnedWingSeen&&e.team=="Player"),enemyWing=episodes.Any(e=>e.returnedWingSeen&&e.team=="Enemy");
        if(ally&&enemy&&allyWing&&enemyWing&&state==null&&normalWorldSince>=0f&&Read<float>(battle,"battleElapsed",0f)-normalWorldSince>=2f)
            Complete(freezeViolations+clockViolations+rateViolations==0?"OBSERVED_CONTRACT_PASS":"OBSERVED_CONTRACT_FAIL");
        else if(!running||Read<string>(battle,"resultText","").Length>0)
            Complete(ally&&enemy&&allyWing&&enemyWing&&freezeViolations+clockViolations+rateViolations==0?"NATURAL_RESULT_PASS":"NATURAL_RESULT_INCOMPLETE");
    }

    private void ConfigureRaptors(object[] units)
    {
        var run=Read<ExpeditionRunSave>(battle,"expeditionRun",null);
        if(run==null)throw new InvalidOperationException("Isolated expedition run missing.");
        run.activeBattleSeed=Seed;UnityEngine.Random.InitState(Seed);
        var configuredTeams=new HashSet<string>();
        foreach(object unit in units)
        {
            var definition=Read<MonsterDefinition>(unit,"definition",null);
            if(definition==null||definition.monsterId!=raptorSpecies||!configuredTeams.Add(Team(unit)))continue;
            var owned=Read<OwnedMonsterSave>(unit,"ownedInstance",null);
            var progression=Team(unit)=="Player"?run.monsters.First(m=>m.instanceId==owned.instanceId):Read<ExpeditionMonsterStateSave>(unit,"enemyProgression",null);
            progression.transcended=true;progression.growthTotal=ExpeditionGrowthSystem.MaximumGrowth;
            if(shinyRequested)owned.isShiny=true;
            typeof(MonsterAutoBattlePrototype).GetMethod("RefreshUnitFromOwnedData",Flags).Invoke(battle,new object[]{unit,definition,owned});
            if(shinyRequested&&BindRegisteredShinyFrames(unit,definition))registeredShinySources.Add(unit);
        }
        unitsAtStart=units.Length;
        transcendedPlayer=units.Count(u=>Team(u)=="Player"&&Read<bool>(u,"augmentTranscended",false));
        transcendedEnemy=units.Count(u=>Team(u)=="Enemy"&&Read<bool>(u,"augmentTranscended",false));
        if(unitsAtStart!=16||transcendedPlayer!=1||transcendedEnemy!=1)
            throw new InvalidOperationException("Expected 8:8 and exactly one actual transcended raptor per team.");
    }

    private void BeginEpisode(object state,object[] units)
    {
        object source=Field(state,"unit");
        current=new Episode{number=episodes.Count+1,source=source,sourceId=Read<int>(source,"id",-1),team=Team(source),
            species=Read<MonsterDefinition>(source,"definition",null)?.monsterId??"",realStart=Time.realtimeSinceStartupAsDouble,
            frameStart=Time.frameCount,worldAtStart=Read<float>(battle,"battleElapsed",0f),apexAtStart=Read<float>(source,"raptorApexPredatorTimer",0f),
            savedSpeed=Read<float>(state,"savedSpeed",-1f),registeredShinyFrames=registeredShinySources.Contains(source)};
        episodes.Add(current);frozen=units.ToDictionary(u=>u,Take);frozenBattleElapsed=Read<float>(battle,"battleElapsed",0f);
        normalSince=-1d;normalWorldSince=-1f;RequestShot("cinematic_begin",current);
    }

    // RefreshUnitFromOwnedData changes progression, while sprite profiles are selected at spawn.
    // This diagnostic binds the registered shiny prefab's actual seven animation profiles before combat advances.
    private static bool BindRegisteredShinyFrames(object unit,MonsterDefinition definition)
    {
        if(string.IsNullOrWhiteSpace(definition.shinyResourcePath))return false;
        var prefab=Resources.Load<GameObject>(definition.shinyResourcePath);
        var template=prefab==null?null:prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var driver=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);
        if(template==null||driver==null)throw new InvalidOperationException("Registered shiny sprite profile missing: "+definition.monsterId);
        driver.Configure(Read<Sprite[]>(template,"idleFrames",null),Read<Sprite[]>(template,"runFrames",null),
            Read<Sprite[]>(template,"attackFrames",null),Read<Sprite[]>(template,"skillAttackFrames",null),
            Read<Sprite[]>(template,"deathFrames",null),Read<Sprite[]>(template,"captureFrames",null),Read<Sprite[]>(template,"struggleFrames",null));
        if(driver.IdleFrameCount==0)throw new InvalidOperationException("Registered shiny idle profile is empty.");
        driver.GetComponent<SpriteRenderer>().sprite=driver.GetIdleFrame(0f);
        return true;
    }

    private static string ParticleSignature(ParticleSystem system)
    {
        if(system==null)return "";
        var particles=new ParticleSystem.Particle[Mathf.Max(1,system.particleCount)];int count=system.GetParticles(particles);
        var value=new StringBuilder(count*25);
        for(int i=0;i<count;i++)value.AppendFormat(CultureInfo.InvariantCulture,"{0:R}:{1:R}:{2:R}:{3:R}|",
            particles[i].position.x,particles[i].position.y,particles[i].position.z,particles[i].remainingLifetime);
        return value.ToString();
    }

    private void EndEpisode()
    {
        current.realEnd=Time.realtimeSinceStartupAsDouble;current.frameEnd=Time.frameCount;
        current.worldAtEnd=Read<float>(battle,"battleElapsed",0f);
        current.apexAtEnd=Read<float>(current.source,"raptorApexPredatorTimer",0f);
        current.completed=current.maximumElapsed>=5.7f;
        current.screenshots=current.files.ToArray();RequestShot("cinematic_end",current);current=null;
    }

    private void RequestShot(string kind,Episode episode)
    {
        int number=episode==null?0:episode.number;
        bool clip=kind=="clip";
        string relative=clip?"Frames/frame"+(++clipFrameCount).ToString("D6")+".png":
            "Native/"+number.ToString("D2")+"_"+kind+"_frame"+Time.frameCount+".png";
        if(episode!=null&&!clip)episode.files.Add(relative);
        shots.Enqueue(new Shot{file=relative,kind=kind,episode=number,requestFrame=Time.frameCount,realtime=Time.realtimeSinceStartupAsDouble,
            unscaled=Time.unscaledTimeAsDouble,world=battle==null?0f:Read<float>(battle,"battleElapsed",0f),
            elapsed=priorState==null?0f:Read<float>(priorState,"elapsed",0f),scale=Time.timeScale,capture=Time.captureDeltaTime});
    }

    private IEnumerator CaptureNative()
    {
        var end=new WaitForEndOfFrame();
        while(!finished||shots.Count>0)
        {
            yield return end;
            if(shots.Count==0)continue;
            Texture2D pixels=null;
            try
            {
                pixels=ScreenCapture.CaptureScreenshotAsTexture();
                if(pixels==null)throw new InvalidOperationException("Native Game View capture returned null.");
                double sampledReal=Time.realtimeSinceStartupAsDouble,sampledUnscaled=Time.unscaledTimeAsDouble;
                float sampledWorld=battle==null?0f:Read<float>(battle,"battleElapsed",0f);
                object sampledState=Field(battle,"raptorCinematic");
                float sampledElapsed=sampledState==null?0f:Read<float>(sampledState,"elapsed",0f);
                bool needsNative=shots.Any(shot=>shot.kind!="clip"),needsClip=shots.Any(shot=>shot.kind=="clip");
                byte[] nativePng=needsNative?pixels.EncodeToPNG():null;
                int clipWidth=0,clipHeight=0;
                byte[] clipPng=needsClip?EncodeClip(pixels,out clipWidth,out clipHeight):null;
                while(shots.Count>0)
                {
                    Shot shot=shots.Dequeue();bool clip=shot.kind=="clip";
                    File.WriteAllBytes(Path.Combine(output,shot.file),clip?clipPng:nativePng);
                    frames.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10},{11},{12:R},{13:R},{14:R}\n",
                        shot.file,shot.kind,shot.episode,Time.frameCount,sampledReal,sampledUnscaled,sampledWorld,
                        sampledState==null?shot.elapsed:sampledElapsed,Time.timeScale,Time.captureDeltaTime,
                        clip?clipWidth:pixels.width,clip?clipHeight:pixels.height,shot.realtime,shot.unscaled,shot.elapsed);
                }
                File.WriteAllText(Path.Combine(output,"frame-times.csv"),frames.ToString());
            }
            catch(Exception error)
            {
                File.WriteAllText(Path.Combine(output,"capture-exception.txt"),error.ToString());
                pendingResult="CAPTURE_EXCEPTION";finished=true;shots.Clear();
                File.WriteAllText(Path.Combine(output,"samples.csv"),samples.ToString());
                File.WriteAllText(Path.Combine(output,"unit-samples.csv"),unitSamples.ToString());
                WriteReport(false);Debug.LogException(error);
            }
            finally{if(pixels!=null)Destroy(pixels);}
        }
        if(finished)RestoreAndExit();
    }

    private static byte[] EncodeClip(Texture2D native,out int width,out int height)
    {
        width=Mathf.Min(960,native.width);height=Mathf.Max(1,Mathf.RoundToInt(native.height*(width/(float)native.width)));
        RenderTexture previous=RenderTexture.active;
        RenderTexture target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default);
        Texture2D small=null;
        try
        {
            Graphics.Blit(native,target);RenderTexture.active=target;
            small=new Texture2D(width,height,TextureFormat.RGB24,false);
            small.ReadPixels(new Rect(0,0,width,height),0,0,false);small.Apply(false,false);
            return small.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
            if(small!=null)Destroy(small);
        }
    }

    private void Complete(string result)
    {
        if(finished)return;
        if(current!=null)EndEpisode();
        pendingResult=result;finished=true;
        File.WriteAllText(Path.Combine(output,"samples.csv"),samples.ToString());
        File.WriteAllText(Path.Combine(output,"unit-samples.csv"),unitSamples.ToString());
        WriteReport(false);
        RequestShot("final",null);
        Debug.Log("[Raptor Apex QA] "+result+" "+output);
    }

    private void RestoreAndExit()
    {
        if(restored)return;restored=true;
        if(battle!=null)
        {
            var cancel=typeof(MonsterAutoBattlePrototype).GetMethod("CancelRaptorApexCinematic",Flags);
            if(cancel!=null)cancel.Invoke(battle,null);
        }
        if(primary!=null)DestroyImmediate(primary.gameObject);
        RestoreConfiguration();
        WriteReport(true);
        File.WriteAllText(Path.Combine(output,"complete.txt"),pendingResult);
        SessionState.SetBool(RaptorApexCinematicRuntimeQa.RestoreScene,true);
        EditorApplication.delayCall+=()=>EditorApplication.ExitPlaymode();
    }

    private void WriteReport(bool configurationRestored)
    {
        object[] units=battle==null?new object[0]:Units();
        foreach(Episode episode in episodes)episode.screenshots=episode.files.ToArray();
        var report=new Report{label=label,result=pendingResult,seed=Seed,unitsAtStart=unitsAtStart,transcendedPlayer=transcendedPlayer,transcendedEnemy=transcendedEnemy,
            rows=rows,episodes=episodes.Count,maximumQueue=maximumQueue,freezeViolations=freezeViolations,clockViolations=clockViolations,rateViolations=rateViolations,
            skills=units.Sum(u=>Read<int>(u,"skillUses",0)),basics=units.Sum(u=>Read<int>(u,"basicAttackCount",0)),clipFrames=clipFrameCount,
            realSeconds=seenBattle?(float)(Time.realtimeSinceStartupAsDouble-startedAt):0f,worldSeconds=battle==null?0f:Read<float>(battle,"battleElapsed",0f),
            finalTimeScale=Time.timeScale,finalCaptureDelta=Time.captureDeltaTime,allySeen=episodes.Any(e=>e.completed&&e.team=="Player"),
            enemySeen=episodes.Any(e=>e.completed&&e.team=="Enemy"),allyWingReturned=episodes.Any(e=>e.returnedWingSeen&&e.team=="Player"),
            enemyWingReturned=episodes.Any(e=>e.returnedWingSeen&&e.team=="Enemy"),configurationRestored=configurationRestored,
            finalClockActive=CombatSkillClock.Active,finalCinematicActive=Field(battle,"raptorCinematic")!=null,
            raptorSpecies=raptorSpecies,shinyRequested=shinyRequested,
            resumedWorldSeconds=normalWorldSince<0f?0f:Read<float>(battle,"battleElapsed",0f)-normalWorldSince,
            wingFlowEvidenceCount=episodes.Count(e=>e.returnedWingAfterOneWorldSecond&&e.particlesAtReturn>0&&e.particlesAfterOneWorldSecond>0&&
                e.particleSignatureAtReturn!=e.particleSignatureAfterOneWorldSecond),casts=episodes.ToArray()};
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(report,true));
    }

    private void RestoreConfiguration()
    {
        foreach(var pair in configuration)
            if(pair.Key.GetValue(null) is IList list){list.Clear();foreach(object value in (object[])pair.Value)list.Add(value);}
            else pair.Key.SetValue(null,pair.Value);
        Time.timeScale=initialTimeScale;Time.captureDeltaTime=initialCaptureDelta;
    }
    private void OnDestroy(){if(!restored)RestoreConfiguration();}
    private object[] Units()=>((IList)Field(battle,"units")).Cast<object>().ToArray();
    private static string Team(object unit)=>Field(unit,"team")?.ToString()??"";
    private static SpriteRenderer Renderer(object unit)
    {
        var animation=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);
        return animation==null?Read<GameObject>(unit,"model",null)?.GetComponentInChildren<SpriteRenderer>():animation.GetComponent<SpriteRenderer>();
    }
    private static Snapshot Take(object unit)
    {
        var model=Read<GameObject>(unit,"model",null);
        var animation=Read<SpriteMonsterAnimationDriver>(unit,"spriteAnimation",null);
        var renderer=Renderer(unit);
        var status=Field(unit,"combatStatuses");var effects=Field(status,"effects") as IDictionary;
        var statuses=new List<string>();
        if(effects!=null)foreach(DictionaryEntry pair in effects)
        {
            var effect=pair.Value;
            statuses.Add(Convert.ToInt32(pair.Key)+":"+Read<float>(effect,"remaining",0f).ToString("R",CultureInfo.InvariantCulture)+":"+
                Read<float>(effect,"tickRemaining",0f).ToString("R",CultureInfo.InvariantCulture)+":"+Read<int>(effect,"stacks",0));
        }
        statuses.Sort(StringComparer.Ordinal);
        return new Snapshot{position=model==null?Vector3.zero:model.transform.position,hp=Read<float>(unit,"hp",0f),cooldown=Read<float>(unit,"cooldown",0f),
            skillTimer=Read<float>(unit,"skillTimer",0f),apex=Read<float>(unit,"raptorApexPredatorTimer",0f),skills=Read<int>(unit,"skillUses",0),
            basics=Read<int>(unit,"basicAttackCount",0),driverFrame=animation==null?-1:animation.CurrentFrameIndex,motion=animation==null?"":animation.CurrentMotionName,
            spriteId=renderer==null||renderer.sprite==null?0:renderer.sprite.GetInstanceID(),statuses=string.Join("|",statuses)};
    }
    private static object Field(object instance,string name)=>instance==null?null:instance.GetType().GetField(name,Flags)?.GetValue(instance);
    private static T Read<T>(object instance,string name,T fallback)
    {
        object value=Field(instance,name);return value is T?(T)value:fallback;
    }
    private static void Set(object instance,string name,object value)
    {
        var field=instance.GetType().GetField(name,Flags);if(field==null)throw new MissingFieldException(instance.GetType().Name,name);field.SetValue(instance,value);
    }
    private static string RosterJson(object[] units)
    {
        var lines=units.Select(u=>string.Format(CultureInfo.InvariantCulture,"{{\"id\":{0},\"team\":\"{1}\",\"species\":\"{2}\",\"rank\":\"{3}\",\"transcended\":{4},\"apex\":{5:R},\"isShiny\":{6},\"spritePath\":\"{7}\"}}",
            Read<int>(u,"id",-1),Team(u),Read<MonsterDefinition>(u,"definition",null)?.monsterId??"",Field(u,"expeditionRank"),
            Read<bool>(u,"augmentTranscended",false)?"true":"false",Read<float>(u,"raptorApexPredatorTimer",0f),
            Read<OwnedMonsterSave>(u,"ownedInstance",null)?.isShiny==true?"true":"false",AssetDatabase.GetAssetPath(Renderer(u)?.sprite).Replace("\\","/")));
        return "["+string.Join(",\n",lines)+"]";
    }
}

