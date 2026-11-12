using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Replays deployment only before battle; observes production timing without fixed-delta or time-scale overrides.
public static class CombatTempoRuntimeQa
{
    public const string Root="Logs/CombatTempoQA/20261020";
    public static void Attach(string label,string baselineLabel="")
    {
        SkillFocusRuntimeQa.Guard();
        var primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        if(!Application.isPlaying||primary==null||(string)typeof(SkillFocusRuntimeQaObserver).GetField("label",flags).GetValue(primary)!=label||
           (bool)typeof(SkillFocusRuntimeQaObserver).GetField("prepared",flags).GetValue(primary))
            throw new InvalidOperationException("Attach to the matching primary before preparation.");
        if(primary.GetComponent<CombatTempoRuntimeQaObserver>()!=null)throw new InvalidOperationException("Already attached.");
        primary.gameObject.AddComponent<CombatTempoRuntimeQaObserver>().Begin(primary,label,baselineLabel);
    }
}

[DefaultExecutionOrder(9990)]
public sealed class CombatTempoRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private string output,label,baseline;
    private bool formationReady,statsReady,finished,seenBattle;
    private double beganAt;
    private float lastWorld;
    private readonly Dictionary<object,Track> tracks=new Dictionary<object,Track>();
    private readonly Report report=new Report();
    private StreamWriter events;
    private sealed class Track {public Vector3 position;public int attacks;public bool seen;}
    [Serializable] public sealed class UnitStart
    {
        public int id,tileX,tileY,rank;
        public string team,species,traits,motion;
        public bool transcended;
        public float x,z,hp,maxHp,damage,attackInterval,moveSpeed,skillCooldown,skillTimer,cooldown,radius,animationGlobalSpeed,animationPlayback;
    }
    [Serializable] public sealed class Setup
    {
        public int seed=20261019;
        public string label,baseline;
        public bool capturedBeforeBattle,replayed,primaryPrepared;
        public UnitStart[] units;
    }
    [Serializable] public sealed class Report
    {
        public string label,baseline,result,primaryResult;
        public string limitation="One variable-delta natural 1x battle per condition; same seed and tile deployment, not a deterministic replay. Attack intervals may include movement, control and buffs. No FPS claim.";
        public int frames,originalUnits,transcendedUnits,basicAttacks,skills,timeScaleViolations,hudVisibleSamples,hudHiddenSamples,presentationHudSamples,presentationHudHidden;
        public int playerAlive,enemyAlive;
        public float battleSeconds,totalTravelDistance,ordinaryTravelDistance,ordinaryTravelSeconds;
        public double realSeconds;
        public bool formationReplayed,startingStatsCaptured;
    }
    public void Begin(SkillFocusRuntimeQaObserver owner,string requestedLabel,string baselineLabel)
    {
        primary=owner;label=requestedLabel;baseline=baselineLabel;
        if(label.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-')||baseline.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))
            throw new ArgumentException("Invalid label.");
        output=Path.GetFullPath(Path.Combine(CombatTempoRuntimeQa.Root,label));
        if(Directory.Exists(output))throw new IOException("Output already exists.");
        Directory.CreateDirectory(output);report.label=label;report.baseline=baseline;
        events=new StreamWriter(Path.Combine(output,"attack-events.csv"),false);
        events.WriteLine("frame,time,world,id,team,species,attacks,delta,action,motion,driverFrame,playback,globalSpeed,cooldown");
    }
    private void LateUpdate()
    {
        if(finished)return;
        try
        {
            battle=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
            if(battle==null||!battle.IsSceneReady)return;
            var units=((IList)Get(battle,"units")).Cast<object>().ToArray();
            if(!formationReady)
            {
                if(Read<bool>(primary,"prepared",false)||Read<bool>(battle,"battleStarted",false))throw new InvalidOperationException("Missed deployment gate.");
                if(units.Length!=16)throw new InvalidOperationException("Expected original 8:8 roster.");
                if(baseline.Length>0)Replay(units);
                SaveSetup(units,"formation.json",false);formationReady=true;
            }
            if(!statsReady&&Read<bool>(primary,"prepared",false))
            {
                SaveSetup(units,"starting-stats.json",true);statsReady=true;report.startingStatsCaptured=true;report.originalUnits=units.Length;
                report.transcendedUnits=units.Count(Transcended);
                foreach(var unit in units)tracks[unit]=new Track{position=Position(unit),attacks=Read<int>(unit,"basicAttackCount",0),seen=true};
            }
            bool done=Read<bool>(primary,"finished",false);
            if(statsReady&&(Read<bool>(battle,"battleStarted",false)||done))Observe(units,done);
            if(done)Complete();
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(output,"exception.txt"),error.ToString());report.result="OBSERVER_EXCEPTION";
            finished=true;events.Dispose();File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(report,true));Debug.LogException(error);
        }
    }
    private void Replay(object[] units)
    {
        var source=JsonUtility.FromJson<Setup>(File.ReadAllText(Path.Combine(CombatTempoRuntimeQa.Root,baseline,"formation.json")));
        if(source.seed!=20261019||source.units.Length!=units.Length)throw new InvalidOperationException("Baseline seed or actor count differs.");
        foreach(var unit in units)
        {
            var expected=source.units.Single(x=>x.id==Read<int>(unit,"id",-1));
            if(expected.species!=Definition(unit).monsterId||expected.team!=Get(unit,"team").ToString())throw new InvalidOperationException("Baseline roster differs.");
        }
        foreach(var unit in units)Call("ReleaseUnitOccupancy",unit);
        foreach(var entry in source.units)
        {
            object unit=units.Single(x=>Read<int>(x,"id",-1)==entry.id);
            Call("OccupyUnit",unit,new Vector2Int(entry.tileX,entry.tileY));Call("ApplyDeploymentTransform",unit);
            var tile=(Vector2Int)Get(unit,"currentTile");
            if(tile.x!=entry.tileX||tile.y!=entry.tileY)throw new InvalidOperationException("Tile replay failed.");
        }
        report.formationReplayed=true;
    }
    private void SaveSetup(object[] units,string file,bool stats)
    {
        var setup=new Setup{label=label,baseline=baseline,capturedBeforeBattle=!Read<bool>(battle,"battleStarted",false),
            replayed=report.formationReplayed,primaryPrepared=Read<bool>(primary,"prepared",false),units=units.Select(Start).ToArray()};
        if(stats&&!setup.capturedBeforeBattle)throw new InvalidOperationException("Starting stats sampled after battle began.");
        File.WriteAllText(Path.Combine(output,file),JsonUtility.ToJson(setup,true));
    }
    private UnitStart Start(object unit)
    {
        var tile=(Vector2Int)Get(unit,"currentTile");var position=Position(unit);var driver=Get(unit,"spriteAnimation") as SpriteMonsterAnimationDriver;
        return new UnitStart{id=Read<int>(unit,"id",-1),tileX=tile.x,tileY=tile.y,team=Get(unit,"team").ToString(),species=Definition(unit).monsterId,
            rank=Convert.ToInt32(Get(unit,"expeditionRank")),traits=string.Join("|",((IEnumerable)Get(unit,"activeTraitIds")).Cast<object>()),transcended=Transcended(unit),
            x=position.x,z=position.z,hp=Read<float>(unit,"hp",0),maxHp=Read<float>(unit,"maxHp",0),damage=Read<float>(unit,"damage",0),
            attackInterval=Read<float>(unit,"attackInterval",0),moveSpeed=Read<float>(unit,"speedTilesPerSecond",0),skillCooldown=Read<float>(unit,"skillCooldownDuration",0),
            skillTimer=Read<float>(unit,"skillTimer",0),cooldown=Read<float>(unit,"cooldown",0),radius=Read<float>(unit,"collisionRadius",0),
            motion=driver==null?"":driver.CurrentMotionName,animationGlobalSpeed=driver==null?0:driver.GlobalSpeedMultiplier,animationPlayback=driver==null?0:driver.CurrentPlaybackSpeed};
    }
    private void Observe(object[] units,bool done)
    {
        float world=Read<float>(battle,"battleElapsed",0f);
        if(!seenBattle){seenBattle=true;beganAt=Time.unscaledTimeAsDouble;lastWorld=world;}
        float dt=Mathf.Max(0,world-lastWorld);lastWorld=world;report.frames++;
        if(Mathf.Abs(Time.timeScale-1f)>.0001f)report.timeScaleViolations++;
        var hud=battle.GetType().GetMethod("TryGetUnitWorldHudRect",Flags);
        var presentations=Get(battle,"localSkillPresentations") as IList;
        foreach(var unit in units)
        {
            Track track;if(!tracks.TryGetValue(unit,out track)){track=new Track();tracks.Add(unit,track);}
            Vector3 pos=Position(unit);int attacks=Read<int>(unit,"basicAttackCount",0);string action=Read<string>(unit,"currentAction","");
            var driver=Get(unit,"spriteAnimation") as SpriteMonsterAnimationDriver;
            if(track.seen&&!done)
            {
                float distance=new Vector2(pos.x-track.position.x,pos.z-track.position.z).magnitude;report.totalTravelDistance+=distance;
                if(action=="personality_pursue"||action=="personality_guard"||action=="move"){report.ordinaryTravelDistance+=distance;report.ordinaryTravelSeconds+=dt;}
                if(attacks>track.attacks)events.WriteLine(string.Join(",",Time.frameCount,Time.unscaledTimeAsDouble.ToString("R",CultureInfo.InvariantCulture),world.ToString("R",CultureInfo.InvariantCulture),
                    Get(unit,"id"),Get(unit,"team"),Definition(unit).monsterId,attacks,attacks-track.attacks,action,driver==null?"":driver.CurrentMotionName,
                    driver==null?-1:driver.CurrentFrameIndex,driver==null?0:driver.CurrentPlaybackSpeed,driver==null?0:driver.GlobalSpeedMultiplier,Get(unit,"cooldown")));
            }
            if(Alive(unit)&&hud!=null)
            {
                object[] args={unit,new Rect()};bool visible=(bool)hud.Invoke(battle,args);
                if(visible)report.hudVisibleSamples++;else report.hudHiddenSamples++;
                bool emphasized=presentations!=null&&presentations.Cast<object>().Any(x=>Get(x,"unit")==unit);
                if(emphasized){report.presentationHudSamples++;if(!visible)report.presentationHudHidden++;}
            }
            track.position=pos;track.attacks=attacks;track.seen=true;
        }
    }
    private void Complete()
    {
        finished=true;events.Dispose();report.battleSeconds=Read<float>(battle,"battleElapsed",0f);report.realSeconds=seenBattle?Time.unscaledTimeAsDouble-beganAt:0;
        var units=((IList)Get(battle,"units")).Cast<object>().ToArray();report.basicAttacks=units.Sum(x=>Read<int>(x,"basicAttackCount",0));report.skills=units.Sum(x=>Read<int>(x,"skillUses",0));
        report.playerAlive=units.Count(x=>Alive(x)&&Get(x,"team").ToString()=="Player");report.enemyAlive=units.Count(x=>Alive(x)&&Get(x,"team").ToString()=="Enemy");
        report.primaryResult=File.ReadAllText(Path.Combine(Read<string>(primary,"output",""),"complete.txt"));
        report.result=report.primaryResult.StartsWith("NATURAL_RESULT",StringComparison.Ordinal)&&statsReady&&report.timeScaleViolations==0?"NATURAL_1X_COMPLETE":"REVIEW_REQUIRED";
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(output,"complete.txt"),report.result);
    }
    private bool Transcended(object unit)=>(bool)battle.GetType().GetMethod("HasFamilyTranscendence",Flags).Invoke(null,new[]{unit});
    private static bool Alive(object unit)=>Read<bool>(unit,"alive",false)&&Read<float>(unit,"hp",0)>0;
    private static MonsterDefinition Definition(object unit)=>(MonsterDefinition)Get(unit,"definition");
    private static Vector3 Position(object unit)=>((GameObject)Get(unit,"model")).transform.position;
    private object Call(string name,params object[] args)=>battle.GetType().GetMethod(name,Flags).Invoke(battle,args);
    private static object Get(object value,string name)=>value==null?null:value.GetType().GetField(name,Flags)?.GetValue(value);
    private static T Read<T>(object value,string name,T fallback){object found=Get(value,name);return found is T?(T)found:fallback;}
    private void OnDestroy(){events?.Dispose();}
}
