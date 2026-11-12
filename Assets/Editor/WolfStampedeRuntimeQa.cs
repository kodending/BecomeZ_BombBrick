using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WolfStampedeRuntimeQa
{
    public static void Start()
    {
        if(!string.Equals(Application.dataPath.Replace('\\','/'),"C:/Unity/BecomeZ_BombBrick/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Wolf QA: wrong project.");
        if(!Application.isPlaying)throw new InvalidOperationException("Wolf QA requires existing Play Mode.");
        if(UnityEngine.Object.FindObjectOfType<WolfStampedeRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("Wolf QA is already running.");
        var host=new GameObject("Wolf stampede runtime QA (editor only)");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<WolfStampedeRuntimeQaObserver>().Begin();
    }
}

[DefaultExecutionOrder(10200)]
public sealed class WolfStampedeRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const string LogPath="Logs/WolfStampedeQA/20261023/runtime.txt";
    private static readonly string[] Species={"nukgu","seollang","honga","wolyeong"};
    private static readonly string[] Opponents={"woongi","geomchi","tino","lilas","ironkuma","haeta","velos","seolgason"};
    private MonsterAutoBattlePrototype battle;
    private object source,previousCinematic;
    private object[] units,targets;
    private float[] hpBefore;
    private int episode,cast,cinematics;
    private double deadline,episodeDeadline,castDeadline;
    private bool finished;

    public void Begin()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        Log("BEGIN controlled 50000HP/100damage/0defense/0speed; 8 species/team episodes; 24 hits per cast");
        Time.timeScale=1f;deadline=Time.realtimeSinceStartupAsDouble+180d;
        LoadEpisode();
    }

    private void LoadEpisode()
    {
        Configure(Species[episode/2],episode%2);battle=null;units=null;source=null;targets=null;
        cast=0;cinematics=0;previousCinematic=null;
        episodeDeadline=Time.realtimeSinceStartupAsDouble+25d;
        Log("EPISODE "+episode+" "+Species[episode/2]+" team="+(episode%2));
        SceneManager.LoadScene(CombatEffectTestSceneController.BattleSceneName);
    }

    private static void Configure(string species,int side)
    {
        CombatEffectTestConfiguration.Active=true;CombatEffectTestConfiguration.MixedRank4v8=false;
        CombatEffectTestConfiguration.RegionId=HunterRegionCatalog.FirstRegionId;
        CombatEffectTestConfiguration.ClearStartRequest();
        CombatEffectTestConfiguration.AllyMonsterIds.Clear();CombatEffectTestConfiguration.EnemyMonsterIds.Clear();
        CombatEffectTestConfiguration.AllyMonsterIds.AddRange(side==0?new[]{species}:Opponents);
        CombatEffectTestConfiguration.EnemyMonsterIds.AddRange(side==1?new[]{species}:Opponents);
        CombatEffectTestConfiguration.AllyMonsterId=side==0?species:Opponents[0];
        CombatEffectTestConfiguration.EnemyMonsterId=side==1?species:Opponents[0];
        CombatEffectTestConfiguration.AllyCount=side==0?1:8;CombatEffectTestConfiguration.EnemyCount=side==1?1:8;
        CombatEffectTestConfiguration.AllyRank=side==0?ExpeditionMonsterRank.Rank3:ExpeditionMonsterRank.Rank1;
        CombatEffectTestConfiguration.EnemyRank=side==1?ExpeditionMonsterRank.Rank3:ExpeditionMonsterRank.Rank1;
        CombatEffectTestConfiguration.AllyTranscended=side==0;CombatEffectTestConfiguration.EnemyTranscended=side==1;
        CombatEffectTestConfiguration.AllyRank2TraitId=CombatEffectTestConfiguration.AllyRank3TraitId="";
        CombatEffectTestConfiguration.EnemyRank2TraitId=CombatEffectTestConfiguration.EnemyRank3TraitId="";
        CombatEffectTestConfiguration.AllyPersonalityId=CombatEffectTestConfiguration.EnemyPersonalityId="vengeful";
    }

    private void LateUpdate()
    {
        if(finished)return;
        try { Observe(); }
        catch(Exception error){Finish(false,error.ToString());}
    }

    private void Observe()
    {
        double now=Time.realtimeSinceStartupAsDouble;
        Require(now<deadline,"whole-run realtime timeout");Require(now<episodeDeadline,"episode realtime timeout");
        if(battle==null)battle=FindObjectOfType<MonsterAutoBattlePrototype>();
        if(battle==null||!battle.IsSceneReady)return;
        if(units==null)
        {
            units=((IList)Get(battle,"units")).Cast<object>().Where(u=>(bool)Get(u,"alive")).ToArray();
            Require(units.Length==9,"fixture must contain 9 alive units");
            source=units.Single(u=>Convert.ToInt32(Get(u,"team"))==episode%2);
            targets=units.Where(u=>!ReferenceEquals(u,source)).ToArray();
            Require(((MonsterDefinition)Get(source,"definition")).monsterId==Species[episode/2],"fixture subject species");
            bool sourceRank=HasProgression(source,ExpeditionMonsterRank.Rank3,true);
            bool targetRanks=targets.Length==8&&targets.All(u=>HasProgression(u,ExpeditionMonsterRank.Rank1,false));
            if(!sourceRank||!targetRanks)
                foreach(var unit in units)LogRank(unit);
            Require(sourceRank,"fixture subject rank3+transcended in its authoritative progression");
            Require(targetRanks,"fixture targets rank1+nontranscended in their authoritative progression");
            foreach(var unit in units){Set(unit,"hp",50000f);Set(unit,"maxHp",50000f);Set(unit,"damage",100f);Set(unit,"defense",0f);Set(unit,"speedTilesPerSecond",0f);}
            Log("FIXTURE PASS source=1 rank3+transcended targets=8 rank1+nontranscended hp=50000 damage=100 defense=0 speed=0");
        }
        foreach(var unit in units){Set(unit,"cooldown",999f);Set(unit,"skillTimer",999f);}
        if(!(bool)Get(battle,"battleStarted"))
        {
            if(!(bool)Get(battle,"waveIntroActive")&&!(bool)Get(battle,"waveArrivalActive")&&!(bool)Get(battle,"mapIntroductionActive"))
                battle.StartPreparedCombatEffectTestBattle();
            return;
        }
        if(cast==0)
        {
            foreach(var unit in units){Set(unit,"hp",50000f);Set(unit,"maxHp",50000f);Set(unit,"damage",100f);Set(unit,"defense",0f);Set(unit,"speedTilesPerSecond",0f);}
            Cast();return;
        }
        object cinematic=Get(battle,"wolfStampedeCinematic");
        if(cinematic!=null)
        {
            if(!ReferenceEquals(cinematic,previousCinematic))cinematics++;
            Require(CombatSkillClock.Active&&Time.timeScale==0f,"cinematic must hold its clock");
        }
        previousCinematic=cinematic;
        Require(now<castDeadline,"cast realtime timeout");
        if(Get(source,"wolfStampede")!=null)return;
        Require(cinematic==null&&!CombatSkillClock.Active&&Mathf.Approximately(Time.timeScale,1f),"cast clock/timeScale restore");
        Require(cinematics==(cast==1?1:0),"cinematic count expected "+(cast==1?1:0)+", got "+cinematics);
        object family=Call("FamilyTranscendenceStateFor",source,false);
        Require(family!=null&&(int)Get(family,"wolfCinematicGeneration")== (int)Get(battle,"combatGeneration"),"generation marker");
        Require((int)Get(source,"skillHits")==cast*24,"skillHits expected "+cast*24+", got "+Get(source,"skillHits"));
        Require(targets.Select((u,i)=>(float)Get(u,"hp")<hpBefore[i]).All(hit=>hit),"all eight targets must lose HP per cast");
        Require(units.All(u=>(int)Get(u,"basicAttackCount")==0)&&targets.All(u=>(int)Get(u,"skillUses")==0),"no basic/other skill interference");
        Require((int)Get(source,"skillUses")==cast&&(float)Get(source,"hp")==50000f,"only requested casts may execute");
        Log("CAST PASS episode="+episode+" cast="+cast+" cinematics="+cinematics+" hits="+Get(source,"skillHits")+" targetHP="+string.Join(",",targets.Select(u=>Get(u,"hp")))+" clock=False scale=1 generation="+Get(family,"wolfCinematicGeneration"));
        if(cast==1){Cast();return;}
        if(++episode<8)LoadEpisode();else Finish(true,"8 episodes; 16 casts; 384 skill hits; 8 cinematics");
    }

    private void Cast()
    {
        Set(source,"target",targets[0]);hpBefore=targets.Select(u=>(float)Get(u,"hp")).ToArray();
        cast++;cinematics=0;previousCinematic=null;
        Require((bool)Call("ExecuteReadySkill",source,false)&&Get(source,"wolfStampede")!=null,"ExecuteReadySkill must commit a stampede");
        castDeadline=Time.realtimeSinceStartupAsDouble+(cast==1?8d:5d);
    }

    private void Finish(bool passed,string message)
    {
        if(finished)return;finished=true;
        Log((passed?"PASS ":"FAIL ")+message);
        if(battle!=null)Call("CancelWolfStampedeCinematic");
        Time.timeScale=1f;Configure("nukgu",0);
        var controller=FindObjectOfType<CombatEffectTestSceneController>();
        if(controller!=null)
        {
            ((IList)Get(controller,"allyRoster")).Clear();((IList)Get(controller,"allyRoster")).Add("nukgu");
            var roster=(IList)Get(controller,"enemyRoster");roster.Clear();foreach(string id in Opponents)roster.Add(id);
            Set(controller,"allyCount",1);Set(controller,"enemyCount",8);
            Set(controller,"allyRank",ExpeditionMonsterRank.Rank3);Set(controller,"enemyRank",ExpeditionMonsterRank.Rank1);
            Set(controller,"allyTranscended",true);Set(controller,"enemyTranscended",false);Set(controller,"mixedRank4v8",false);Set(controller,"deploymentPrepared",true);
        }
        SceneManager.LoadScene(CombatEffectTestSceneController.BattleSceneName);Destroy(gameObject);
    }

    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private ExpeditionMonsterStateSave Progression(object unit)=>Convert.ToInt32(Get(unit,"team"))==0
        ?ExpeditionItemSystem.MonsterState((ExpeditionRunSave)Get(battle,"expeditionRun"),(string)Get(unit,"stableId"))
        :(ExpeditionMonsterStateSave)Get(unit,"enemyProgression");
    private bool HasProgression(object unit,ExpeditionMonsterRank rank,bool transcended)
    {
        var progression=Progression(unit);
        return progression!=null&&progression.rank==rank&&progression.transcended==transcended&&
            (bool)Get(unit,"augmentTranscended")==transcended&&
            MonsterAutoBattlePrototype.DisplayStarCount(progression)==(transcended?4:(int)rank);
    }
    private void LogRank(object unit)
    {
        var definition=(MonsterDefinition)Get(unit,"definition");
        var progression=Progression(unit);var owned=(OwnedMonsterSave)Get(unit,"ownedInstance");
        Log("RANK species="+definition.monsterId+" family="+definition.familyId+" team="+Get(unit,"team")+
            " enemyRankField="+Get(unit,"expeditionRank")+" augmentTranscended="+Get(unit,"augmentTranscended")+
            " progression="+(progression==null?"<missing>":JsonUtility.ToJson(progression))+
            " owned="+(owned==null?"<missing>":JsonUtility.ToJson(owned)));
    }
    private static void Log(string text){File.AppendAllText(LogPath,DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine);Debug.Log("Wolf QA: "+text);}
    private static FieldInfo Field(object value,string name)=>value.GetType().GetField(name,Flags)??throw new MissingFieldException(value.GetType().Name,name);
    private static object Get(object value,string name)=>Field(value,name).GetValue(value);
    private static void Set(object value,string name,object setting)=>Field(value,name).SetValue(value,setting);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Flags).Invoke(battle,args);
}
