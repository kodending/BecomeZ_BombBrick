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

// Diagnostic only: reflection and CSV recording are deliberately excluded from performance measurements.
[InitializeOnLoad]
public static class CombatMovementComparison
{
    private const string Pending="CombatMovementComparison.Pending";
    static CombatMovementComparison()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);
            EditorApplication.delayCall+=()=>Start(SessionState.GetString(Pending+".Label","before"));
        };
    }

    public static void Launch(string label)
    {
        if(EditorApplication.isPlaying){Start(label);return;}
        if(SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene before movement comparison.");
        SessionState.SetString(Pending+".Scene",SceneManager.GetActiveScene().path);
        SessionState.SetString(Pending+".Label",label);
        SessionState.SetBool(Pending,true);
        EditorSceneManager.OpenScene(CombatEffectTestSceneSetup.ScenePath);
        EditorApplication.EnterPlaymode();
    }

    public static void Start(string label)
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(UnityEngine.Object.FindObjectOfType<CombatMovementComparisonRecorder>()!=null)
            throw new InvalidOperationException("Movement comparison already running.");
        var host=new GameObject("Movement comparison observer (editor only)");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<CombatMovementComparisonRecorder>().Begin(label);
    }

    public static void RestoreScene()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        string path=SessionState.GetString(Pending+".Scene","");
        if(!string.IsNullOrEmpty(path))EditorSceneManager.OpenScene(path);
    }
}

public sealed class CombatMovementComparisonRecorder:MonoBehaviour
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const int Seed=20261019;
    private static readonly Type BattleType=typeof(MonsterAutoBattlePrototype);
    private static readonly Type UnitType=BattleType.GetNestedType("Unit",BindingFlags.NonPublic);
    private static readonly FieldInfo DriverMoving=typeof(SpriteMonsterAnimationDriver).GetField("moving",Private);
    private static readonly MethodInfo AttackMovementLocked=BattleType.GetMethod("IsAttackMovementLocked",BindingFlags.Static|BindingFlags.NonPublic);
    private static readonly MethodInfo InertiaBlocked=BattleType.GetMethod("InertiaBlocked",BindingFlags.Static|BindingFlags.NonPublic);
    private static readonly string[] DenseRoster={"velos","ironkuma","geomchi","woongi","ager","nukgu","kogason","jangpung"};
    private static readonly string[][] Rosters=
    {
        new[]{"velos","ironkuma"},
        new[]{"aranea","ager"},
        new[]{"velos","ironkuma","geomchi","woongi","aranea","ager","kongjwi","holydeer"},
        new[]{"velos","chundongi","nukgu","kogason","pone","nectis","jangpung","kongjwi"}
    };
    private static readonly string[] Names={"melee_2v2","ranged_2v2","mixed_8v8","skate_skills_8v8"};
    private readonly Dictionary<object,Vector3> previous=new Dictionary<object,Vector3>();
    private readonly Dictionary<object,float> stuck=new Dictionary<object,float>();
    private readonly Dictionary<object,int> attacks=new Dictionary<object,int>();
    private readonly Dictionary<FieldInfo,object> configuration=new Dictionary<FieldInfo,object>();
    private readonly StringBuilder csv=new StringBuilder(256000);
    private MonsterAutoBattlePrototype battle;
    private string output;
    private int scenario=-1,frameCount,attackStarts,rangeViolations,stuckSamples,penetrationSamples,actors;
    private int lastScenario=2;
    private bool skillsScenario;
    private float observed,previousElapsed,minimumSeparation=float.PositiveInfinity,maximumSpeed,maximumRangeExcess;
    private float maximumWalkingSpeed,longestStuckSeconds,minimumDt=float.PositiveInfinity,maximumDt;
    private float originalTimeScale,originalCaptureDelta;
    private bool prepared,seenBattle,capturedStart,capturedFight,finished;
    private float timeout,observationLimit=25f;
    private bool captureClip;
    private bool hitFeedback;
    private bool crowding,dense,fastCrowdInjected;
    private int contactFxCount;
    private int clipFrames,clipWidth,clipHeight;
    private readonly StringBuilder clipTimes=new StringBuilder("frame,battleElapsed,timeScale,deltaTime,realtime\n");

    [Serializable] private sealed class Report
    {
        public string scenario,label,result,limitation="Editor diagnostic observer; variable simulation dt, not a frame-time benchmark. Basic attack counters are sampled against the current target, not an authoritative attack-entry hook. Raw overlaps include skills and summons; inspect action/displacement context.";
        public int seed,actors,frames,attackStarts,rangeViolations,stuckSamples,penetrationSamples;
        public float observedSeconds,minimumSurfaceSeparation,maximumSpeed,maximumBasicAttackRangeExcess;
        public float maximumWalkingSpeed,longestStuckSeconds,minimumSimulationDt,maximumSimulationDt;
        public int finalPlayerAlive,finalEnemyAlive;
        public string[] finalParticipants;
    }

    public void Begin(string label)
    {
        if(string.IsNullOrWhiteSpace(label)||label.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))
            throw new ArgumentException("Comparison label must contain letters, digits, - or _.");
        hitFeedback=label.StartsWith("hitfeedback_",StringComparison.Ordinal);
        crowding=label.StartsWith("crowding_",StringComparison.Ordinal);
        dense=crowding&&label.Contains("_dense");
        output=Path.GetFullPath(Path.Combine(crowding?"Logs/InertiaCrowdingQA/20261019":hitFeedback?"Logs/HitFeedbackQA/20261019":"Logs/MovementInertiaQA/20261019",label));
        Directory.CreateDirectory(output);
        foreach(var field in typeof(CombatEffectTestConfiguration).GetFields(BindingFlags.Public|BindingFlags.Static))
            configuration.Add(field,field.GetValue(null) is IList list?list.Cast<object>().ToArray():field.GetValue(null));
        originalTimeScale=Time.timeScale;originalCaptureDelta=Time.captureDeltaTime;
        Time.timeScale=1f;
        captureClip=label.EndsWith("_clip",StringComparison.Ordinal)||label.EndsWith("_clip_smooth",StringComparison.Ordinal);
        if(captureClip)
        {
            lastScenario=0;
            observationLimit=10.04f;
            Directory.CreateDirectory(Path.Combine(output,"Frames"));
            Time.captureDeltaTime=1f/30f;
            StartCoroutine(CaptureClipFrames());
        }
        if(label.EndsWith("_full",StringComparison.Ordinal)){scenario=1;lastScenario=2;observationLimit=90f;}
        if(label.StartsWith("skate_skills",StringComparison.Ordinal)){scenario=2;lastScenario=3;skillsScenario=true;}
        if((hitFeedback||crowding)&&captureClip){scenario=1;lastScenario=2;observationLimit=10.04f;}
        if((hitFeedback||crowding)&&label.EndsWith("_skills",StringComparison.Ordinal))
        {scenario=2;lastScenario=3;skillsScenario=true;}
        Next();
    }

    private void Next()
    {
        if(++scenario>lastScenario)
        {
            finished=true;
            Time.captureDeltaTime=originalCaptureDelta;
            File.WriteAllText(Path.Combine(output,"complete.txt"),"Requested real production-battle Play Mode scenarios completed. Seed "+Seed);
            Debug.Log("[Movement Comparison] COMPLETE "+output);
            return;
        }
        CombatEffectTestConfiguration.Active=true;
        CombatEffectTestConfiguration.RegionId=HunterRegionCatalog.FirstRegionId;
        string[] roster=dense?DenseRoster:Rosters[scenario];
        CombatEffectTestConfiguration.AllyMonsterIds.Clear();CombatEffectTestConfiguration.AllyMonsterIds.AddRange(roster);
        CombatEffectTestConfiguration.EnemyMonsterIds.Clear();CombatEffectTestConfiguration.EnemyMonsterIds.AddRange(roster);
        CombatEffectTestConfiguration.AllyCount=CombatEffectTestConfiguration.EnemyCount=roster.Length;
        CombatEffectTestConfiguration.AllyRank=CombatEffectTestConfiguration.EnemyRank=
            skillsScenario?ExpeditionMonsterRank.Rank3:ExpeditionMonsterRank.Rank1;
        CombatEffectTestConfiguration.AllyTranscended=CombatEffectTestConfiguration.EnemyTranscended=false;
        CombatEffectTestConfiguration.AllyRank2TraitId=CombatEffectTestConfiguration.EnemyRank2TraitId="";
        CombatEffectTestConfiguration.AllyRank3TraitId=CombatEffectTestConfiguration.EnemyRank3TraitId="";
        CombatEffectTestConfiguration.AllyPersonalityId=CombatEffectTestConfiguration.EnemyPersonalityId="vengeful";
        CombatEffectTestConfiguration.ClearStartRequest();
        previous.Clear();stuck.Clear();attacks.Clear();csv.Clear();
        csv.AppendLine("frame,time,id,team,species,x,z,vx,vz,actualSpeed,targetId,distance,attackReach,action,basicAttacks,skillUses,surfaceGap,displacing,inertiaVx,inertiaVz,driverMotion,driverFrame,driverMoving,facingX,facingZ,driverActionLocked,driverFacingLocked,attackMovementLocked,unitMoving,targetDirectionX,targetDirectionZ,hp,timeScale,contactRecoilRemaining,flashRemaining,contactFxCount,inertiaBlocked,cooldown,blockedTimer,stuckTimer,collisionRadius,groundHeight,reservedAttackSlot,routeIndex,routeCount,nearbyBodies");
        prepared=seenBattle=capturedStart=capturedFight=false;
        fastCrowdInjected=false;
        observed=previousElapsed=maximumSpeed=maximumRangeExcess=0f;minimumSeparation=float.PositiveInfinity;
        maximumWalkingSpeed=longestStuckSeconds=maximumDt=0f;minimumDt=float.PositiveInfinity;
        frameCount=attackStarts=rangeViolations=stuckSamples=penetrationSamples=actors=0;
        timeout=Time.realtimeSinceStartup+observationLimit*(captureClip?12f:3f)+30f;
        UnityEngine.Random.InitState(Seed);
        SceneManager.LoadScene(CombatEffectTestSceneController.BattleSceneName);
    }

    private void LateUpdate()
    {
        if(finished)return;
        if(Time.realtimeSinceStartup>timeout){Save("SETUP_OR_RENDER_TIMEOUT");finished=true;return;}
        battle=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
        if(battle==null||!battle.IsSceneReady)return;
        if(captureClip)Time.captureDeltaTime=1f/30f;
        var units=((IList)B("units")).Cast<object>().ToArray();
        if(!prepared)
        {
            // The existing test scene randomizes enemy deployment independently of the battle seed.
            // Reuse its generated spawn positions only on the first run, then replay those exact tiles.
            string baseline=Path.Combine(Path.GetDirectoryName(output),hitFeedback?
                (skillsScenario?"hitfeedback_before_skills":"hitfeedback_before_clip_smooth"):"before",Names[scenario]+".formation.csv");
            if(crowding&&!dense)baseline=Path.GetFullPath(Path.Combine("Logs/HitFeedbackQA/20261019",
                skillsScenario?"hitfeedback_after_skills":"hitfeedback_after_full",Names[scenario]+".formation.csv"));
            if(File.Exists(baseline))RestoreFormation(units,File.ReadAllLines(baseline));
            if(dense)ApplyDenseFormation(units);
            File.WriteAllLines(Path.Combine(output,Names[scenario]+".formation.csv"),units.Select(u=>
                U(u,"id")+","+U(u,"currentTile").ToString().Replace("(","").Replace(")","").Replace(" ","")));
            var run=(ExpeditionRunSave)B("expeditionRun");run.activeBattleSeed=Seed;
            if(skillsScenario)ApplyFixedSkillTraits(units,run);
            UnityEngine.Random.InitState(Seed);
            foreach(object unit in units)
                UnitType.GetField("personalityPolicy").SetValue(unit,PersonalityCombatPolicy.FromId("vengeful"));
            SaveStartingStats(units);
            File.WriteAllLines(Path.Combine(output,Names[scenario]+".starting-world.csv"),
                new[]{"id,x,z,radius"}.Concat(units.Select(u=>string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R}",
                    U(u,"id"),Position(u).x,Position(u).z,U(u,"collisionRadius")))));
            actors=units.Count(u=>Active(u));
            prepared=true;
            if(!battle.StartPreparedCombatEffectTestBattle())throw new InvalidOperationException("Test battle start rejected.");
            return;
        }
        if(!(bool)B("battleStarted"))
        {
            if(seenBattle){Save("RESULT "+B("resultText"));Next();}
            return;
        }
        seenBattle=true;
        float elapsed=(float)B("battleElapsed");
        float dt=elapsed-previousElapsed;if(dt<=0f)return;
        if(crowding&&Path.GetFileName(output).Contains("_fast")&&!fastCrowdInjected&&elapsed>=2f)
        {
            var events=new List<string>{"id,time,vx,vz,accepted"};
            foreach(object unit in units)
            {
                if(!Active(unit)||(bool)InertiaBlocked.Invoke(null,new[]{unit}))continue;
                object target=U(unit,"target");if(target==null||!Active(target))continue;
                Vector3 momentum=Flat(Position(target)-Position(unit)).normalized*CombatMovementInertia.MaximumCoastSpeed;
                Call("BeginUnitInertia",unit,momentum);
                events.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4}",
                    U(unit,"id"),elapsed,momentum.x,momentum.z,((Vector3)U(unit,"inertiaVelocity")).sqrMagnitude>.001f));
            }
            File.WriteAllLines(Path.Combine(output,"maximum-coast-injection.csv"),events);
            fastCrowdInjected=true;
        }
        previousElapsed=observed=elapsed;frameCount++;
        minimumDt=Mathf.Min(minimumDt,dt);maximumDt=Mathf.Max(maximumDt,dt);
        if(!capturedStart&&elapsed>=.25f){ScreenCapture.CaptureScreenshot(Path.Combine(output,Names[scenario]+".start.png"));capturedStart=true;}
        if(!capturedFight&&elapsed>=4f){ScreenCapture.CaptureScreenshot(Path.Combine(output,Names[scenario]+".fight.png"));capturedFight=true;}
        contactFxCount=ContactFxCount();
        foreach(object unit in units)
        {
            if(!Active(unit))continue;
            Vector3 position=Position(unit);
            Vector3 old;float speed=previous.TryGetValue(unit,out old)?Flat(position-old).magnitude/dt:0f;
            previous[unit]=position;maximumSpeed=Mathf.Max(maximumSpeed,speed);
            object target=U(unit,"target");bool hasTarget=target!=null&&Active(target);
            float distance=hasTarget?Flat(Position(target)-position).magnitude:-1f;
            float reach=hasTarget?MonsterAutoBattlePrototype.AttackDistanceForRange((int)U(unit,"attackRange"),(float)U(unit,"collisionRadius"),(float)U(target,"collisionRadius")):0f;
            int count=(int)U(unit,"basicAttackCount"),prior;attacks.TryGetValue(unit,out prior);
            if(count>prior)
            {
                attackStarts+=count-prior;
                float excess=distance-reach;maximumRangeExcess=Mathf.Max(maximumRangeExcess,excess);
                if(hasTarget&&excess>.12f)rangeViolations++;
            }
            attacks[unit]=count;
            float gap=float.PositiveInfinity;int nearbyBodies=0;
            foreach(object other in units)if(other!=unit&&Active(other))
            {
                float separation=Flat(Position(other)-position).magnitude-(float)U(unit,"collisionRadius")-(float)U(other,"collisionRadius");
                gap=Mathf.Min(gap,separation);if(separation<.12f)nearbyBodies++;
            }
            minimumSeparation=Mathf.Min(minimumSeparation,gap);
            if(gap<-.12f&&!(bool)U(unit,"displacing"))penetrationSamples++;
            string action=(string)U(unit,"currentAction");
            bool walking=WalkingAction(action)&&!(bool)U(unit,"displacing");
            if(walking)maximumWalkingSpeed=Mathf.Max(maximumWalkingSpeed,speed);
            float stuckTime;stuck.TryGetValue(unit,out stuckTime);
            stuckTime=hasTarget&&distance>reach+.002f&&speed<.03f&&walking?stuckTime+dt:0f;
            longestStuckSeconds=Mathf.Max(longestStuckSeconds,stuckTime);
            stuck[unit]=stuckTime;if(stuckTime>=2f)stuckSamples++;
            Vector3 velocity=(Vector3)U(unit,"velocity");
            Vector3 inertia=(Vector3)U(unit,"inertiaVelocity");
            var driver=(SpriteMonsterAnimationDriver)U(unit,"spriteAnimation");
            Vector3 facing=(Vector3)U(unit,"facingDirection");
            Vector3 targetDirection=hasTarget?Flat(Position(target)-position).normalized:Vector3.zero;
            csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:F5},{2},{3},{4},{5:F5},{6:F5},{7:F5},{8:F5},{9:F5},{10},{11:F5},{12:F5},{13},{14},{15},{16:F5},{17},{18:F5},{19:F5},{20},{21},{22},{23:F5},{24:F5},{25},{26},{27},{28},{29:F5},{30:F5},{31:F5},{32:F5},{33:F5},{34:F5},{35},{36},{37:F5},{38:F5},{39:F5},{40:F5},{41:F5},{42},{43},{44},{45}\n",
                frameCount,elapsed,U(unit,"id"),U(unit,"team"),((MonsterDefinition)U(unit,"definition")).monsterId,
                position.x,position.z,velocity.x,velocity.z,speed,hasTarget?U(target,"id"):-1,distance,reach,action,count,U(unit,"skillUses"),gap,U(unit,"displacing"),inertia.x,inertia.z,
                driver!=null?driver.CurrentMotionName:"None",driver!=null?driver.CurrentFrameIndex:-1,driver!=null&&(bool)DriverMoving.GetValue(driver),
                facing.x,facing.z,driver!=null&&driver.IsActionLocked,driver!=null&&driver.LocksHorizontalFacing,
                AttackMovementLocked.Invoke(null,new[]{unit}),U(unit,"moving"),targetDirection.x,targetDirection.z,
                U(unit,"hp"),Time.timeScale,OptionalFloat(driver,"contactShakeRemaining"),OptionalFloat(U(unit,"reaction"),"flashTimer"),contactFxCount,
                InertiaBlocked.Invoke(null,new[]{unit}),U(unit,"cooldown"),U(unit,"blockedTimer"),U(unit,"stuckTimer"),U(unit,"collisionRadius"),
                position.y-(float)U(unit,"visualHeightOffset"),U(unit,"reservedAttackSlot"),U(unit,"routeIndex"),((IList)U(unit,"route")).Count,nearbyBodies);
        }
        if(((string)B("resultText")).Length>0)
        {ScreenCapture.CaptureScreenshot(Path.Combine(output,Names[scenario]+".result.png"));Save("NATURAL_RESULT "+B("resultText"));Next();}
        else if(elapsed>=observationLimit){Save("OBSERVATION_LIMIT "+observationLimit);Next();}
    }

    private static bool WalkingAction(string action)=>action=="personality_pursue"||action=="personality_guard"||
        action=="personality_advance"||action=="personality_retreat"||action=="attack_detour"||
        action=="reroute"||action=="pursue"||action=="advance"||action=="retreat"||action=="hex_step";

    private IEnumerator CaptureClipFrames()
    {
        var end=new WaitForEndOfFrame();
        while(!finished)
        {
            yield return end;
            if(finished||!seenBattle||battle==null)continue;
            Texture2D pixels=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if(pixels==null)throw new InvalidOperationException("No native Game View frame.");
                if(clipFrames==0){clipWidth=pixels.width;clipHeight=pixels.height;}
                if(pixels.width!=clipWidth||pixels.height!=clipHeight)
                    throw new InvalidOperationException("Game View dimensions changed during capture.");
                string file=Path.Combine(output,"Frames","frame"+(++clipFrames).ToString("D6")+".png");
                if(File.Exists(file))throw new IOException("Capture frame already exists: "+file);
                File.WriteAllBytes(file,pixels.EncodeToPNG());
                clipTimes.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:F7},{2:F7},{3:F7},{4:F7}\n",
                    clipFrames,(float)B("battleElapsed"),Time.timeScale,Time.deltaTime,Time.realtimeSinceStartup);
                File.WriteAllText(Path.Combine(output,"frame-times.csv"),clipTimes.ToString());
            }
            finally{if(pixels!=null)Destroy(pixels);}
        }
    }

    private void SaveStartingStats(object[] units)
    {
        string[] fields={"id","team","definition","maxHp","damage","defense","statusResistance","attackRange", "attackInterval","speedTilesPerSecond","skillId","activeTraitIds","collisionRadius","skillTimer","cooldown","expeditionRank"};
        var lines=new List<string>{string.Join(",",fields)};
        foreach(object unit in units)
        {
            var values=new List<string>();
            foreach(string field in fields)
            {
                object value=field=="expeditionRank"&&U(unit,"team").ToString()=="Player"
                    ?((ExpeditionRunSave)B("expeditionRun")).monsters.First(m=>m.instanceId==((OwnedMonsterSave)U(unit,"ownedInstance")).instanceId).rank
                    :U(unit,field);
                if(field=="definition")value=((MonsterDefinition)value).monsterId;
                else if(field=="activeTraitIds")value=string.Join("|",((IEnumerable)value).Cast<object>().Select(v=>v.ToString()));
                values.Add(Convert.ToString(value,CultureInfo.InvariantCulture));
            }
            lines.Add(string.Join(",",values));
        }
        File.WriteAllLines(Path.Combine(output,Names[scenario]+".stats.csv"),lines);
    }

    private void ApplyFixedSkillTraits(object[] units,ExpeditionRunSave run)
    {
        foreach(object unit in units)
        {
            var definition=(MonsterDefinition)U(unit,"definition");
            var owned=(OwnedMonsterSave)U(unit,"ownedInstance");
            var state=U(unit,"team").ToString()=="Player"
                ?run.monsters.First(m=>m.instanceId==owned.instanceId)
                :(ExpeditionMonsterStateSave)U(unit,"enemyProgression");
            state.rank2TraitId=ExpeditionTraitProgression.FixedTrait(definition,2).id;
            state.rank3TraitId=ExpeditionTraitProgression.FixedTrait(definition,3).id;
            Call("RefreshUnitFromOwnedData",unit,definition,owned);
            var active=(IList)U(unit,"activeTraitIds");
            if(!active.Contains(state.rank2TraitId)||!active.Contains(state.rank3TraitId))
                throw new InvalidOperationException("Fixed rank traits were not applied: "+definition.monsterId);
            UnitType.GetField("hp").SetValue(unit,U(unit,"maxHp"));
        }
    }

    private void RestoreFormation(object[] units,string[] lines)
    {
        foreach(object unit in units)Call("ReleaseUnitOccupancy",unit);
        foreach(string line in lines)
        {
            string[] c=line.Split(',');int id=int.Parse(c[0]);
            object unit=units.First(u=>(int)U(u,"id")==id);
            Call("OccupyUnit",unit,new Vector2Int(int.Parse(c[1]),int.Parse(c[2])));
            Call("ApplyDeploymentTransform",unit);
        }
    }

    private void ApplyDenseFormation(object[] units)
    {
        Vector3 center=Vector3.zero;
        foreach(object unit in units)center+=Flat(Position(unit));
        center/=units.Length;
        float spacing=units.Max(u=>(float)U(u,"collisionRadius"))*2f+.05f;
        foreach(var team in units.GroupBy(u=>U(u,"team").ToString()))
        {
            float side=team.Key=="Player"?-1f:1f;int index=0;
            foreach(object unit in team.OrderBy(u=>(int)U(u,"id")))
            {
                Vector3 ground=center+new Vector3(side*(2.5f+(index/4)*spacing),0f,(index%4-1.5f)*spacing);
                Vector3 free=(Vector3)BattleType.GetMethod("ResolveFreePosition",Private).Invoke(battle,new object[]{ground,U(unit,"collisionRadius")});
                if((free-ground).sqrMagnitude>.0001f)throw new InvalidOperationException("Dense diagnostic formation hits the arena boundary.");
                ((GameObject)U(unit,"model")).transform.position=ground+Vector3.up*(float)U(unit,"visualHeightOffset");
                ((GameObject)U(unit,"ring")).transform.position=ground+Vector3.up*.025f;
                UnitType.GetField("lastGroundPosition").SetValue(unit,ground);
                UnitType.GetField("currentTile").SetValue(unit,BattleType.GetMethod("NearestGridCoordinate",Private).Invoke(battle,new object[]{ground}));
                index++;
            }
        }
        foreach(object a in units)foreach(object b in units)
            if(a!=b&&Flat(Position(a)-Position(b)).magnitude<(float)U(a,"collisionRadius")+(float)U(b,"collisionRadius")+.025f)
                throw new InvalidOperationException("Dense diagnostic formation starts with overlapping bodies.");
    }

    private void Save(string result)
    {
        object[] units=((IList)B("units")).Cast<object>().ToArray();
        File.WriteAllText(Path.Combine(output,Names[scenario]+".csv"),csv.ToString());
        File.WriteAllText(Path.Combine(output,Names[scenario]+".json"),JsonUtility.ToJson(new Report
        {
            scenario=Names[scenario],label=Path.GetFileName(output),result=result,seed=Seed,actors=actors,frames=frameCount,
            attackStarts=attackStarts,rangeViolations=rangeViolations,stuckSamples=stuckSamples,penetrationSamples=penetrationSamples,
            observedSeconds=observed,minimumSurfaceSeparation=float.IsPositiveInfinity(minimumSeparation)?0f:minimumSeparation,
            maximumSpeed=maximumSpeed,maximumBasicAttackRangeExcess=maximumRangeExcess,
            maximumWalkingSpeed=maximumWalkingSpeed,longestStuckSeconds=longestStuckSeconds,
            minimumSimulationDt=float.IsPositiveInfinity(minimumDt)?0f:minimumDt,maximumSimulationDt=maximumDt,
            finalPlayerAlive=units.Count(u=>Active(u)&&U(u,"team").ToString()=="Player"),
            finalEnemyAlive=units.Count(u=>Active(u)&&U(u,"team").ToString()=="Enemy"),
            finalParticipants=units.Select(u=>U(u,"id")+":"+((MonsterDefinition)U(u,"definition")).monsterId+
                ":basic="+U(u,"basicAttackCount")+":skill="+U(u,"skillUses")+":alive="+U(u,"alive")).ToArray()
        },true));
        Debug.Log("[Movement Comparison] "+Names[scenario]+": "+result);
    }

    private object B(string name)=>BattleType.GetField(name,Private).GetValue(battle);
    private static float OptionalFloat(object instance,string name)
    {
        if(instance==null)return 0f;
        return Convert.ToSingle(instance.GetType().GetField(name,Private)?.GetValue(instance)??0f);
    }
    private static int ContactFxCount()
    {
        return Convert.ToInt32(typeof(PixelCombatEffect).GetProperty("ActiveContactImpacts",BindingFlags.Static|BindingFlags.Public)?.GetValue(null,null)??0);
    }
    private static object U(object unit,string name)=>UnitType.GetField(name).GetValue(unit);
    private static Vector3 Position(object unit)=>((GameObject)U(unit,"model")).transform.position;
    private static Vector3 Flat(Vector3 v){v.y=0;return v;}
    private static bool Active(object unit)=>unit!=null&&(bool)U(unit,"alive")&&!(bool)U(unit,"inStorage")&&((GameObject)U(unit,"model")).activeInHierarchy;
    private void Call(string name,params object[] args)=>BattleType.GetMethod(name,Private).Invoke(battle,args);

    private void OnDestroy()
    {
        Time.timeScale=originalTimeScale;Time.captureDeltaTime=originalCaptureDelta;
        foreach(var pair in configuration)
            if(pair.Key.GetValue(null) is IList list)
            {list.Clear();foreach(object value in (object[])pair.Value)list.Add(value);}
            else pair.Key.SetValue(null,pair.Value);
    }
}
