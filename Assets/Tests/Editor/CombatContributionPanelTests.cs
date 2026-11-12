using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatContributionPanelTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host;
    private MonsterAutoBattlePrototype battle;
    private ExpeditionRunSave run;
    private float timeScale,captureDeltaTime;

    [SetUp] public void Setup()
    {
        timeScale=Time.timeScale;captureDeltaTime=Time.captureDeltaTime;
        host=new GameObject("Combat contribution test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        run=new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId};
        SetBattle("expeditionRun",run);
        AddUnit(1);
        for(int wave=1;wave<=4;wave++)
        {
            run.wave=wave;
            Call("CaptureWaveCombatContribution",true);
        }
        run.wave=5;
    }

    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(host);
        Time.timeScale=timeScale;Time.captureDeltaTime=captureDeltaTime;
    }

    [Test] public void PreparationAndResumeShowPreviousWaveCompletedStats()
    {
        Set(AddUnit(2),"inStorage",true);
        Set(AddUnit(3),"augmentShadowClone",true);
        AddUnit(4,true);
        object page=Call("RefreshCombatContributionPages",10f);

        Assert.That(GetBattle("combatDataSelectedWave"),Is.EqualTo(4));
        Assert.That(Pages.Cast<object>().Select(item=>(int)Get(item,"wave")),Is.EqualTo(new[]{1,2,3,4}));
        Assert.That(Get(page,"preparing"),Is.False);
        Assert.That(Get(page,"live"),Is.False);
        IList allies=(IList)Get(page,"allies");
        Assert.That(allies.Count,Is.EqualTo(1));
        Assert.That(Get(allies[0],"damageDealt"),Is.EqualTo(574f));
        Assert.That(Get(((IList)Get(History[3],"allies"))[0],"damageDealt"),Is.EqualTo(574f));
    }

    [Test] public void NextWaveKeepsPreviousTabUntilBattleActuallyStarts()
    {
        Call("RefreshCombatContributionPages",10f);
        SetBattle("combatDataSelectedWave",4);
        Assert.That(Call("RefreshCombatContributionPages",10.01f),Is.SameAs(History[3]));
        run.wave=6;
        object page=Call("RefreshCombatContributionPages",10.02f);
        Assert.That(Get(page,"wave"),Is.EqualTo(4));
        Assert.That(GetBattle("combatDataSelectedWave"),Is.EqualTo(4));
        SetBattle("battleStarted",true);
        page=Call("RefreshCombatContributionPages",10.03f);
        Assert.That(Get(page,"wave"),Is.EqualTo(6));
        Assert.That(GetBattle("combatDataSelectedWave"),Is.EqualTo(6));
        SetBattle("combatDataSelectedWave",2);
        Assert.That(Call("RefreshCombatContributionPages",10.04f),Is.SameAs(History[1]));
    }

    [Test] public void LiveAndCompletedStatsSurvivePreviewAndRetryDoesNotDuplicateWave()
    {
        AddUnit(2,true);
        Call("RefreshCombatContributionPages",10f);
        SetBattle("battleStarted",true);
        object live=Call("RefreshCombatContributionPages",10.01f);
        Assert.That(Get(live,"live"),Is.True);
        Assert.That(Get(live,"preparing"),Is.False);
        Assert.That(Get(((IList)Get(live,"allies"))[0],"damageDealt"),Is.EqualTo(574f));
        Assert.That(Get(((IList)Get(live,"enemies"))[0],"damageDealt"),Is.EqualTo(574f));

        SetBattle("battleStarted",false);
        SetBattle("resultText","아군 승리");
        Call("CaptureWaveCombatContribution",true);
        object result=Call("RefreshCombatContributionPages",10.02f);
        Assert.That(Get(result,"won"),Is.True);
        Assert.That(Get(result,"preparing"),Is.False);
        Assert.That(Get(((IList)Get(result,"allies"))[0],"damageDealt"),Is.EqualTo(574f));
        Assert.That(Pages.Count,Is.EqualTo(5));

        SetBattle("resultText","");
        object retry=Call("RefreshCombatContributionPages",10.03f);
        Assert.That(Get(retry,"preparing"),Is.False);
        Assert.That(Get(((IList)Get(retry,"allies"))[0],"damageDealt"),Is.EqualTo(574f));
        Assert.That(Pages.Count,Is.EqualTo(5));
        Assert.That(History.Count,Is.EqualTo(5));
        Assert.That(Get(((IList)Get(History[4],"allies"))[0],"damageDealt"),Is.EqualTo(574f));
    }

    [Test] public void PreparationRefreshFollowsDeploymentChanges()
    {
        History.Clear();run.wave=1;
        Call("RefreshCombatContributionPages",10f);
        object second=AddUnit(2);
        object page=Call("RefreshCombatContributionPages",10.21f);
        Assert.That(((IList)Get(page,"allies")).Count,Is.EqualTo(2));
        Set(second,"inStorage",true);
        page=Call("RefreshCombatContributionPages",10.42f);
        Assert.That(((IList)Get(page,"allies")).Count,Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompletedWaveRetainsBothTeamsAcrossResumeAndRetryUpdatesTheWave(bool won)
    {
        MonsterDatabase database=ScriptableObject.CreateInstance<MonsterDatabase>();
        try
        {
            SetBattle("monsterDatabase",database);
            object enemy=AddUnit(2,true);
            Set(enemy,"damageDealt",120f);
            Set(enemy,"damageTaken",45f);
            Set(enemy,"healingReceived",17f);
            Set(enemy,"alive",false);
            SetBattle("resultText",won?"아군 승리":"적군 승리");
            Call("CaptureWaveCombatContribution",won);
            Call("SyncTransientRunState");

            // Resume from the serialized checkpoint after the original combat units change.
            run=JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(run));
            SetBattle("expeditionRun",run);
            Set(enemy,"damageDealt",0f);
            Call("RestoreTransientRunState");
            object page=Call("RefreshCombatContributionPages",10f);
            Assert.That(Get(page,"wave"),Is.EqualTo(5));
            Assert.That(Get(page,"won"),Is.EqualTo(won));
            IList allies=(IList)Get(page,"allies");
            IList enemies=(IList)Get(page,"enemies");
            Assert.That(allies.Count,Is.EqualTo(1));
            Assert.That(Get(allies[0],"damageDealt"),Is.EqualTo(574f));
            Assert.That(enemies.Count,Is.EqualTo(1));
            Assert.That(Get(enemies[0],"damageDealt"),Is.EqualTo(120f));
            Assert.That(Get(enemies[0],"damageTaken"),Is.EqualTo(45f));
            Assert.That(Get(enemies[0],"healingReceived"),Is.EqualTo(17f));
            Assert.That(Get(enemies[0],"survived"),Is.False);

            SetBattle("resultText",won?"적군 승리":"아군 승리");
            Call("CaptureWaveCombatContribution",!won);
            page=Call("RefreshCombatContributionPages",10.01f);
            Assert.That(Get(page,"won"),Is.EqualTo(!won));
            Assert.That(Get(((IList)Get(page,"enemies"))[0],"damageDealt"),Is.EqualTo(0f));
            Assert.That(Pages.Count,Is.EqualTo(5));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(database);
        }
    }

    private object AddUnit(int id,bool enemy=false)
    {
        Type type=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        object unit=Activator.CreateInstance(type,true);
        if(enemy)Set(unit,"team",Enum.ToObject(type.GetField("team").FieldType,1));
        Set(unit,"id",id);Set(unit,"stableId","ally_"+id);Set(unit,"displayName","Ally "+id);
        Set(unit,"alive",true);Set(unit,"damageDealt",574f);Set(unit,"damageTaken",16f);
        Set(unit,"healingReceived",6f);Set(unit,"comboActivations",2);Set(unit,"comboBonusDamage",24f);
        ((IList)GetBattle("units")).Add(unit);
        return unit;
    }
    private IList Pages=>(IList)GetBattle("combatContributionPages");
    private IList History=>(IList)GetBattle("combatContributionHistory");
    private object GetBattle(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private).GetValue(battle);
    private void SetBattle(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private).SetValue(battle,value);
    private static object Get(object item,string name)=>item.GetType().GetField(name).GetValue(item);
    private static void Set(object item,string name,object value)=>item.GetType().GetField(name).SetValue(item,value);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
}
