using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Editor-only controlled casts reuse the production preparation, executor and effect callbacks.
public static class SkillCameraLifetimeRuntimeQa
{
    public const string Root="Logs/SkillCameraLifetimeQA/20261020";
    public static void StartControlled(string label,int first=0,int count=0)
    {
        SkillFocusRuntimeQa.Guard();
        if(!Application.isPlaying||UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("Start after the initial test Battle/audio warmup, without a primary observer.");
        var battle=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
        if(battle==null||!battle.IsSceneReady)throw new InvalidOperationException("Initial Battle is not ready.");
        var host=new GameObject("Camera lifetime controlled suite (editor only)");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<SkillCameraLifetimeRuntimeQaObserver>().Begin(label,true,first,count);
    }
    public static void Attach(string label)
    {
        SkillFocusRuntimeQa.Guard();
        var primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        if(!Application.isPlaying||primary==null||SkillCameraLifetimeRuntimeQaObserver.Read<bool>(primary,"prepared",false))
            throw new InvalidOperationException("Attach before primary preparation.");
        var host=new GameObject("Camera lifetime natural observer (editor only)");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<SkillCameraLifetimeRuntimeQaObserver>().Begin(label,false,0,0);
    }
}

[DefaultExecutionOrder(10100)]
public sealed class SkillCameraLifetimeRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static readonly Type BattleType=typeof(MonsterAutoBattlePrototype);
    private readonly List<Case> cases=new List<Case>();
    private readonly List<Cast> casts=new List<Cast>();
    private readonly HashSet<string> captured=new HashSet<string>();
    private readonly List<object> observedStates=new List<object>();
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private MonsterAutoBattlePrototype sceneBeforeStart;
    private CombatCameraDirector director;
    private object source,priorLease;
    private Cast current;
    private Case selected;
    private StreamWriter csv;
    private string output,label;
    private bool controlled,configured,requested,finished,advancing,primaryAwaitingScene;
    private int index,lastIndex,usesBefore,castNumber;
    private double timeout,requestAt,sceneAt;
    private float homeFov;
    private readonly Report report=new Report();
    public bool Finished=>finished;

    [Serializable] public sealed class Case
    {
        public string family,species,skill,skillName,trait,traitName;
        public int rank;
        public bool secondMantisCast;
    }
    [Serializable] public sealed class Cast
    {
        public int caseIndex,number,id,usesBefore,usesAfter,hits,rows,maximumPendingEffects,tailSamples,bodySamples,pendingSamples;
        public string family,species,skill,skillName,trait,traitName,result,endReason;
        public bool requestAccepted,cameraSeen,cameraEnded,impactSeen,secondMantisCast,transformedAtRequest;
        public double requestedAt,committedAt=-1d,preparationSeconds=-1d,cameraBeganAt=-1d,cameraEndedAt=-1d,bodyEndedAt=-1d;
        public float damage,maximumZoom=1f,maximumFovDelta;
        public int timeScaleViolations,shadeSamples,earlyLeaseEndSamples,zoomBoundsViolations;
    }
    [Serializable] public sealed class Report
    {
        public string label,result,mode;
        public string limitation="Controlled mode manually requests the real production BeginCommonSkillFocus and thereafter uses normal preparation, executor, animation and effect completion. Target HP is raised; all ordinary locomotion and basic/automatic skill attacks are suppressed, while skill-scripted movement is retained. This is not natural battle or a performance benchmark. Natural mode changes no combat state. Screen captures add overhead; capture request timestamps are recorded. Lease counters are checked against independently recorded effect state, not taken alone as proof of final damage timing.";
        public int plannedCases,completedCases,rows,timeScaleViolations,shadeSamples,earlyLeaseEndSamples,zoomBoundsViolations;
        public Case[] plan;
        public Cast[] casts;
    }
    public void Begin(string requestedLabel,bool controlledMode,int first,int count)
    {
        if(string.IsNullOrWhiteSpace(requestedLabel)||requestedLabel.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))throw new ArgumentException("Invalid label.");
        label=requestedLabel;controlled=controlledMode;report.label=label;report.mode=controlled?"CONTROLLED_PRODUCTION_CASTS":"NATURAL_8V8_READ_ONLY";
        output=Path.GetFullPath(Path.Combine(SkillCameraLifetimeRuntimeQa.Root,label));
        if(Directory.Exists(output))throw new IOException("Output already exists.");
        Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"Native"));
        csv=new StreamWriter(Path.Combine(output,"samples.csv"),false,new System.Text.UTF8Encoding(false),65536);
        csv.WriteLine("case,cast,frame,scaled,unscaled,world,timeScale,id,species,skillUses,skillHits,skillDamage,action,motion,driverSkill,localActive,pending,startedAt,committedAt,recoveryAt,lease,leaseOwner,leasePendingEffects,impactComplete,bodyActive,panActive,panReturning,zoom,appliedPanX,appliedPanY,sourceAlive,fov,fovDelta,uvMinX,uvMinY,uvMaxX,uvMaxY,independentEffectState,rendererEnabled,rendererActive,spriteName,applicationFocused");
        if(controlled)
        {
            BuildPlan();report.plan=cases.ToArray();report.plannedCases=cases.Count;
            if(first<0||first>=cases.Count)throw new ArgumentOutOfRangeException(nameof(first));
            index=first;lastIndex=count<=0?cases.Count:Math.Min(cases.Count,first+count);
            File.WriteAllText(Path.Combine(output,"plan.json"),JsonUtility.ToJson(report,true));
            StartCase();
        }
        else {primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();sceneAt=Time.realtimeSinceStartupAsDouble;}
    }
    private void BuildPlan()
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var definitions=database.monsters.Where(d=>d!=null&&d.enabled&&!string.IsNullOrEmpty(d.familyId)).ToDictionary(d=>d.monsterId);
        var groups=GameMasterData.Database.monsterGrowths.Where(g=>definitions.ContainsKey(g.monsterId)).GroupBy(g=>g.familyId).OrderBy(g=>g.Key).ToArray();
        foreach(var family in groups)
        {
            var members=family.OrderBy(g=>g.monsterId).ToArray();
            AddCase(members[0],definitions[members[0].monsterId],1,"");
            foreach(var member in members.GroupBy(g=>g.fixedRank3TraitId).Select(g=>g.First()).OrderBy(g=>g.fixedRank3TraitId))
                if(!string.IsNullOrEmpty(member.fixedRank3TraitId))AddCase(member,definitions[member.monsterId],3,member.fixedRank3TraitId);
        }
        if(groups.Length!=20||cases.Count!=80)throw new InvalidOperationException("Expected 20 families and 80 base/fixed-rank3 variants; actual "+groups.Length+"/"+cases.Count);
        Case mantis=cases.Single(c=>c.trait=="trait_mantis_reaper_emergence");
        cases.Add(new Case{family=mantis.family,species=mantis.species,skill=mantis.skill,skillName=mantis.skillName,trait=mantis.trait,traitName=mantis.traitName,rank=3,secondMantisCast=true});
    }
    private void AddCase(MonsterGrowthMasterRow growth,MonsterDefinition definition,int rank,string trait)
    {
        cases.Add(new Case{family=growth.familyId,species=growth.monsterId,rank=rank,skill=definition.skillId,
            skillName=GameMasterData.Skill(definition.skillId)?.displayName??"",trait=trait,
            traitName=GameMasterData.Database.traits.FirstOrDefault(t=>t.id==trait)?.displayName??""});
    }
    private void StartCase()
    {
        selected=cases[index];configured=requested=false;source=priorLease=null;current=null;castNumber=0;observedStates.Clear();
        string caseLabel=(selected.rank==3?"rank3_":"rank1_")+label+"_case"+index.ToString("D2");
        sceneBeforeStart=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
        SkillFocusRuntimeQa.Start(caseLabel);primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        // LoadScene completes next frame; never let a new observer prepare the previous Battle instance.
        primary.enabled=false;primaryAwaitingScene=true;
        Set(primary,"capturedNeutral",true);Set(primary,"capturedFocused",true);
        CombatEffectTestConfiguration.AllyMonsterIds.Clear();CombatEffectTestConfiguration.AllyMonsterIds.Add(selected.species);
        CombatEffectTestConfiguration.EnemyMonsterIds.Clear();CombatEffectTestConfiguration.EnemyMonsterIds.AddRange(Enumerable.Repeat("ironkuma",4));
        CombatEffectTestConfiguration.AllyCount=1;CombatEffectTestConfiguration.EnemyCount=4;
        // The reused primary applies fixed rank3 traits to both teams before its prepare gate.
        CombatEffectTestConfiguration.EnemyRank=selected.rank==3?ExpeditionMonsterRank.Rank3:ExpeditionMonsterRank.Rank1;
        CombatEffectTestConfiguration.AllyRank3TraitId=selected.trait;CombatEffectTestConfiguration.EnemyRank3TraitId="";
        sceneAt=Time.realtimeSinceStartupAsDouble;timeout=sceneAt+70d;
    }
    private void Update()
    {
        if(!controlled||finished||!configured||battle==null)return;
        // Suppress unrelated basic/automatic skill requests, without changing effect or world clocks.
        foreach(object unit in Units())
        {
            Set(unit,"cooldown",9999f);
            if(unit!=source||!Read<bool>(unit,"rank3MantisReaper",false))Set(unit,"skillTimer",9999f);
            if(unit!=source)Set(unit,"speedTilesPerSecond",0f);
        }
        if(source!=null)Set(source,"skillTimer",requested&&Presentation(source)!=null&&Field(Presentation(source),"pending")!=null?0f:9999f);
    }
    private void LateUpdate()
    {
        if(finished||advancing)return;
        try
        {
            if(primary==null){if(controlled)throw new InvalidOperationException("Primary missing.");Complete("PRIMARY_MISSING");return;}
            if(primaryAwaitingScene)
            {
                var candidate=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
                if(candidate==null||ReferenceEquals(candidate,sceneBeforeStart))return;
                primary.enabled=true;primaryAwaitingScene=false;return;
            }
            battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
            if(battle==null||!battle.IsSceneReady)
            {if(controlled&&Time.realtimeSinceStartupAsDouble>timeout)FinishCase("SCENE_TIMEOUT");return;}
            director=Field(battle,"combatCameraDirector") as CombatCameraDirector;
            if(!Read<bool>(primary,"prepared",false))return;
            if(!configured)
            {
                homeFov=director==null?36f:(Field(battle,"mainCamera") as Camera).fieldOfView;
                if(controlled)ConfigurePair();configured=true;
            }
            if(controlled&&Time.realtimeSinceStartupAsDouble>timeout){FinishCase("CAST_TIMEOUT");return;}
            if(!Read<bool>(battle,"battleStarted",false))
            {if(Read<bool>(primary,"finished",false)){if(controlled)FinishCase("BATTLE_ENDED");else Complete("PRIMARY_FINISHED");}return;}
            if(controlled&&!requested)
            {
                if(Read<bool>(battle,"waveIntroActive",false)||Read<bool>(battle,"waveArrivalActive",false))return;
                RequestCast();
                if(finished||advancing)return;
            }
            Observe();
            if(controlled&&requested&&current!=null)
            {
                bool committed=Read<int>(source,"skillUses",0)>usesBefore;
                if(committed&&Presentation(source)==null&&Field(battle,"skillCameraPresentation")==null&&!Body(source)&&Time.timeAsDouble-requestAt>1d)
                {
                    if(selected.secondMantisCast&&castNumber==1)
                    {requested=false;current=null;return;}
                    FinishCase("CAST_COMPLETE");
                }
            }
            if(!controlled&&Read<bool>(primary,"finished",false))Complete("PRIMARY_FINISHED");
        }
        catch(Exception error)
        {File.WriteAllText(Path.Combine(output,"exception.txt"),error.ToString());Complete("OBSERVER_EXCEPTION");Debug.LogException(error);}
    }
    private void ConfigurePair()
    {
        object[] units=Units();source=units.Single(u=>Field(u,"team").ToString()=="Player");
        foreach(object unit in units)Call("ReleaseUnitOccupancy",unit);
        int enemy=0;
        foreach(object unit in units)
        {
            Vector2Int tile=unit==source?new Vector2Int(3,2):new[]{new Vector2Int(4,2),new Vector2Int(4,1),new Vector2Int(4,3),new Vector2Int(5,2)}[enemy++];
            Call("OccupyUnit",unit,tile);Call("ApplyDeploymentTransform",unit);
            Set(unit,"maxHp",1000000f);Set(unit,"hp",unit==source?500000f:1000000f);
            Set(unit,"cooldown",9999f);Set(unit,"skillTimer",9999f);Set(unit,"speedTilesPerSecond",0f);
        }
        Vector3 center=Read<GameObject>(source,"model",null).transform.position;
        foreach(object enemyUnit in units.Where(u=>u!=source))
        {
            var model=Read<GameObject>(enemyUnit,"model",null);Vector3 offset=model.transform.position-center;
            Vector3 position=center+new Vector3(offset.x*.6f,offset.y,offset.z*.6f);
            model.transform.position=position;Set(enemyUnit,"lastGroundPosition",new Vector3(position.x,0f,position.z));
            Read<GameObject>(enemyUnit,"ring",null).transform.position=new Vector3(position.x,.025f,position.z);
        }
        Set(source,"target",units.First(u=>u!=source));
        string traits=string.Join("|",((IEnumerable)Field(source,"activeTraitIds")).Cast<object>());
        if(selected.rank==3&&!traits.Split('|').Contains(selected.trait))throw new InvalidOperationException("Selected fixed trait missing: "+selected.trait);
        File.WriteAllText(Path.Combine(output,"setup-"+index.ToString("D2")+".json"),JsonUtility.ToJson(new Setup{selected=selected,actualTraits=traits,actors=units.Length,hp=Read<float>(source,"hp",0)},true));
    }
    [Serializable] private sealed class Setup {public Case selected;public string actualTraits;public int actors;public float hp;}
    private void RequestCast()
    {
        if(Body(source)||Presentation(source)!=null||Read<bool>(source,"displacing",false)||Read<float>(source,"rank3SkillLockTimer",0f)>0f)return;
        Set(source,"target",Units().First(u=>u!=source&&Read<bool>(u,"alive",false)));
        Set(source,"skillTimer",0f);usesBefore=Read<int>(source,"skillUses",0);requestAt=Time.timeAsDouble;castNumber++;
        object target=Field(source,"target");
        File.AppendAllText(Path.Combine(output,"request-readiness.csv"),string.Join(",",new object[]{index,castNumber,F(Time.timeAsDouble),Call("CanStartFocusedSkill",source,target),F(Vector3.Distance(Read<GameObject>(source,"model",null).transform.position,Read<GameObject>(target,"model",null).transform.position)),Read<SpriteMonsterAnimationDriver>(source,"spriteAnimation",null).IsActionLocked,Field(source,"currentAction"),F(Read<float>(source,"rank3SkillLockTimer",0)),Field(source,"beetleOverturn")!=null})+Environment.NewLine);
        current=NewCast(source);current.requestAccepted=(bool)Call("BeginCommonSkillFocus",source,false);requested=true;
        current.transformedAtRequest=Read<bool>(source,"rank3MantisReaper",false);
        if(!current.requestAccepted)FinishCase("REQUEST_REJECTED");
    }
    private Cast NewCast(object unit)
    {
        var definition=Read<MonsterDefinition>(unit,"definition",null);
        var cast=new Cast{caseIndex=controlled?index:-1,number=controlled?castNumber:casts.Count+1,id=Read<int>(unit,"id",-1),species=definition.monsterId,
            family=definition.familyId,skill=definition.skillId,skillName=GameMasterData.Skill(definition.skillId)?.displayName??"",
            trait=controlled?selected.trait:"",traitName=controlled?selected.traitName:"",requestedAt=Time.timeAsDouble,
            usesBefore=Read<int>(unit,"skillUses",0),secondMantisCast=controlled&&selected.secondMantisCast&&castNumber==2};
        casts.Add(cast);return cast;
    }
    private void Observe()
    {
        object lease=Field(battle,"skillCameraPresentation");object owner=Field(lease,"unit");
        if(!controlled&&lease!=null&&!ReferenceEquals(lease,priorLease))
        {
            if(current!=null&&priorLease!=null&&!current.cameraEnded)
            {current.cameraEnded=true;current.cameraEndedAt=Time.timeAsDouble;current.endReason="SAME_FRAME_OWNER_REPLACEMENT";}
            source=owner;current=NewCast(owner);current.requestAccepted=true;observedStates.Clear();
        }
        if(current==null){priorLease=lease;return;}
        object presentation=lease!=null&&owner==source?Field(lease,"presentation"):Presentation(source);
        bool pending=Field(presentation,"pending")!=null,body=Body(source),local=Read<bool>(presentation,"active",false);
        int effects=Read<int>(lease,"pendingEffects",0);bool impact=Read<bool>(lease,"impactComplete",false);
        current.rows++;report.rows++;if(body)current.bodySamples++;if(pending)current.pendingSamples++;
        if(current.bodySamples>0&&!body&&current.bodyEndedAt<0d)current.bodyEndedAt=Time.timeAsDouble;
        if(owner==source&&lease!=null)
        {
            current.cameraSeen=true;if(current.cameraBeganAt<0d)current.cameraBeganAt=Time.timeAsDouble;
            current.maximumPendingEffects=Math.Max(current.maximumPendingEffects,effects);current.impactSeen|=impact;
            if(!pending&&!body&&effects>0)current.tailSamples++;
            if(controlled&&ShouldCapture()&&pending)Capture("preparation");
            if(controlled&&ShouldCapture()&&!pending&&effects>0&&!body)Capture("effect-tail");
            if(controlled&&ShouldCapture()&&!pending&&body)Capture("execution");
        }
        if(priorLease!=null&&lease==null&&current.cameraSeen&&!current.cameraEnded)
        {
            current.cameraEnded=true;current.cameraEndedAt=Time.timeAsDouble;
            int remaining=Read<int>(priorLease,"pendingEffects",0);bool completedImpact=Read<bool>(priorLease,"impactComplete",false);
            bool raptor=Read<MonsterDefinition>(source,"definition",null).familyId=="raptor";
            current.endReason=completedImpact&&raptor?"MAIN_IMPACT":remaining==0&&!body?"BODY_AND_EFFECTS_COMPLETE":"INVALIDATED_OR_UNEXPECTED";
            if(Read<bool>(source,"alive",false)&&!Read<bool>(battle,"battleEnding",false)&&!(raptor&&completedImpact)&&(remaining>0||body))
            {current.earlyLeaseEndSamples++;report.earlyLeaseEndSamples++;}
            if(controlled&&ShouldCapture())Capture("camera-return");
        }
        current.usesAfter=Read<int>(source,"skillUses",0);current.hits=Read<int>(source,"skillHits",0);current.damage=Read<float>(source,"skillDamage",0f);
        double committed=Read<double>(presentation,"committedAt",-1d),started=Read<double>(presentation,"startedAt",-1d);
        if(current.usesAfter>current.usesBefore&&committed>=0d&&current.committedAt<0d)
        {current.committedAt=committed;current.preparationSeconds=committed-started;}
        if(Mathf.Abs(Time.timeScale-1f)>.0001f){current.timeScaleViolations++;report.timeScaleViolations++;}
        if(Field(battle,"skillFocusShade") is SpriteRenderer shade&&shade.enabled&&shade.gameObject.activeInHierarchy&&shade.color.a>0f)
        {current.shadeSamples++;report.shadeSamples++;}
        float zoom=director==null?1f:director.AppliedAnticipationZoom;
        Vector2 pan=director==null?Vector2.zero:director.AppliedAnticipationPanOffset;
        Vector2 shake=Read<Vector2>(director,"appliedShakeOffset",Vector2.zero);
        Vector2 total=pan+shake,min=Vector2.one*(1f-1f/zoom)*.5f-total/zoom,max=Vector2.one*(1f+1f/zoom)*.5f-total/zoom;
        if(zoom<.99999f||zoom>1.14001f||zoom>1.00001f&&(min.x<-.0001f||min.y<-.0001f||max.x>1.0001f||max.y>1.0001f))
        {current.zoomBoundsViolations++;report.zoomBoundsViolations++;}
        var camera=Field(battle,"mainCamera") as Camera;float fov=camera==null?36f:camera.fieldOfView;
        current.maximumZoom=Mathf.Max(current.maximumZoom,zoom);current.maximumFovDelta=Mathf.Max(current.maximumFovDelta,Mathf.Abs(fov-homeFov));
        var driver=Read<SpriteMonsterAnimationDriver>(source,"spriteAnimation",null);
        var renderer=driver==null?null:driver.GetComponent<SpriteRenderer>();
        csv.WriteLine(string.Join(",",new object[]{controlled?index:-1,current.number,Time.frameCount,F(Time.timeAsDouble),F(Time.unscaledTimeAsDouble),F(Read<float>(battle,"battleElapsed",0)),F(Time.timeScale),current.id,current.species,current.usesAfter,current.hits,F(current.damage),Field(source,"currentAction"),driver?.CurrentMotionName,driver!=null&&driver.IsSkillActionActive,local,pending,F(started),F(committed),F(Read<double>(presentation,"recoveryAt",-1d)),lease!=null,Read<int>(owner,"id",-1),effects,impact,body,director!=null&&director.AnticipationPanActive,Read<bool>(director,"anticipationPanReturning",false),F(zoom),F(pan.x),F(pan.y),Read<bool>(source,"alive",false),F(fov),F(fov-homeFov),F(min.x),F(min.y),F(max.x),F(max.y),IndependentState(),renderer!=null&&renderer.enabled,renderer!=null&&renderer.gameObject.activeInHierarchy,renderer==null||renderer.sprite==null?"":renderer.sprite.name.Replace(',','_'),Application.isFocused}));
        priorLease=lease;
    }
    private bool ShouldCapture()=>new[]{"raptor","crocodile","kingkong","mammoth","beetle","wyvern","haetae","mantis"}.Contains(selected.family);
    private string IndependentState()
    {
        foreach(string field in new[]{"mammothBoulders","beetleGravityFields","haetaeRoars","wyvernBreaths","wyvernHeavenBreaths"})
            if(Field(battle,field) is IEnumerable list)
                foreach(object state in list)if(Field(state,"source")==source&&!observedStates.Contains(state))observedStates.Add(state);
        object reaper=Field(source,"mantisReaperAction");if(reaper!=null&&!observedStates.Contains(reaper))observedStates.Add(reaper);
        var parts=new List<string>();
        foreach(object state in observedStates)
        {
            var values=new List<string>();
            foreach(string name in new[]{"time","flight","shatteredAt","finished","landed","launched","completed","cancelled","heavenly","nextHit","hits","hit","kind","age"})
            {object value=Field(state,name);if(value!=null)values.Add(name+"="+Convert.ToString(value,CultureInfo.InvariantCulture));}
            if(Field(state,"strikes") is IList strikes)values.Add("strikes="+strikes.Count+"/resolved="+strikes.Cast<object>().Count(s=>Read<bool>(s,"resolved",false)));
            foreach(string name in new[]{"effect","pressureEffect","rainEffect"})
                if(Field(state,name) is Component fx&&fx!=null)
                {
                    var fxValues=new List<string>();
                    foreach(string field in new[]{"age","time","finished","ending","endingAge","duration","windup"})
                    {object value=Field(fx,field);if(value!=null)fxValues.Add(field+"="+Convert.ToString(value,CultureInfo.InvariantCulture));}
                    values.Add(name+":"+fx.gameObject.activeInHierarchy+":"+string.Join("/",fxValues));
                }
            parts.Add(state.GetType().Name+":"+string.Join("/",values));
        }
        parts.Add("raptor:impact="+Read<bool>(source,"leapImpactResolved",false)+"/returning="+Read<bool>(source,"leapReturning",false)+"/hits="+Read<int>(source,"leapHitCount",0));
        parts.Add("mantis:transformed="+Read<bool>(source,"rank3MantisReaper",false));
        foreach(object victim in Units().Where(u=>Field(u,"team").ToString()!=Field(source,"team").ToString()))
            parts.Add("target"+Read<int>(victim,"id",-1)+":displacing="+Read<bool>(victim,"displacing",false)+"/preyFlying="+Read<bool>(victim,"crocodilePreyFlying",false)+"/hp="+F(Read<float>(victim,"hp",0)));
        if(Read<MonsterDefinition>(source,"definition",null).familyId=="kingkong")
            foreach(var sprite in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>().Where(s=>s.name.IndexOf("Fissure",StringComparison.OrdinalIgnoreCase)>=0))
                parts.Add("fissure:"+sprite.name.Replace(',','_')+"/scale="+F(sprite.transform.localScale.x)+"/alpha="+F(sprite.color.a));
        return string.Join(";",parts);
    }
    private void Capture(string phase)
    {
        string key=index+"-"+castNumber+"-"+phase;if(!captured.Add(key))return;
        string file=Path.Combine(output,"Native",index.ToString("D2")+"-"+selected.species+"-"+castNumber+"-"+phase+".png");
        ScreenCapture.CaptureScreenshot(file);
        File.AppendAllText(Path.Combine(output,"native-requests.csv"),string.Join(",",new object[]{Time.frameCount,F(Time.timeAsDouble),F(Time.unscaledTimeAsDouble),index,castNumber,phase,file})+Environment.NewLine);
    }
    private void FinishCase(string reason)
    {
        if(advancing)return;
        foreach(Cast cast in casts.Where(c=>c.caseIndex==index&&c.result==null))
        {
            bool skip=cast.secondMantisCast&&cast.transformedAtRequest;
            cast.result=reason=="CAST_COMPLETE"&&cast.usesAfter>cast.usesBefore&&(skip?!cast.cameraSeen:cast.cameraSeen&&cast.cameraEnded)&&
                cast.timeScaleViolations==0&&cast.shadeSamples==0&&cast.earlyLeaseEndSamples==0&&cast.zoomBoundsViolations==0&&cast.maximumFovDelta<.0001f?"PASS":"REVIEW_REQUIRED";
            if(cast.preparationSeconds>=0d&&cast.preparationSeconds<.4999d)cast.result="REVIEW_REQUIRED";
            if(cast.endReason==null)cast.endReason=reason;
        }
        report.completedCases++;csv.Flush();Save("RUNNING");
        if(primary!=null&&!Read<bool>(primary,"finished",false))typeof(SkillFocusRuntimeQaObserver).GetMethod("Complete",Flags).Invoke(primary,new object[]{"CONTROLLED_"+reason});
        if(reason!="CAST_COMPLETE"||casts.Any(c=>c.caseIndex==index&&c.result=="REVIEW_REQUIRED"))
        {Complete("REVIEW_REQUIRED_AT_CASE_"+index);return;}
        advancing=true;StartCoroutine(NextCase());
    }
    private IEnumerator NextCase()
    {
        if(primary!=null)Destroy(primary.gameObject);primary=null;yield return null;
        index++;advancing=false;
        if(index>=lastIndex){Complete("SUITE_COMPLETE");yield break;}
        StartCase();
    }
    private void Complete(string reason)
    {
        if(finished)return;finished=true;csv?.Flush();Save(reason);csv?.Dispose();
        Debug.Log("[Camera Lifetime QA] "+reason+" "+output);
    }
    private void Save(string reason)
    {
        report.result=reason;report.casts=casts.ToArray();
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(report,true));
    }
    private object[] Units()=>((IList)Field(battle,"units")).Cast<object>().ToArray();
    private object Presentation(object unit)=>Call("SkillPresentationFor",unit);
    private bool Body(object unit)=>(bool)BattleType.GetMethod("CommonSkillActionActive",Flags).Invoke(null,new[]{unit});
    private object Call(string name,params object[] args)=>BattleType.GetMethod(name,Flags).Invoke(battle,args);
    private static string F(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
    private static object Field(object target,string name)=>target?.GetType().GetField(name,Flags)?.GetValue(target);
    public static T Read<T>(object target,string name,T fallback)=>Field(target,name) is T value?value:fallback;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
    private void OnApplicationFocus(bool focused)
    {
        if(string.IsNullOrEmpty(output))return;
        object lease=Field(battle,"skillCameraPresentation");
        File.AppendAllText(Path.Combine(output,"application-focus-events.csv"),string.Join(",",new object[]{Time.frameCount,F(Time.timeAsDouble),F(Time.unscaledTimeAsDouble),focused,Application.isFocused,index,Read<int>(source,"id",-1),lease!=null,Read<int>(lease,"pendingEffects",0),director!=null&&director.AnticipationPanActive,source!=null&&Read<bool>(Presentation(source),"active",false)})+Environment.NewLine);
    }
    private void OnDestroy(){csv?.Dispose();}
}
