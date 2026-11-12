using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class NinjaEightGateTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private IList units;
    private HashSet<GameObject> existing;
    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Gate test");host.SetActive(false);battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)battleType.GetField("units",Private).GetValue(battle);
    }
    [TearDown] public void TearDown()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }
    private object Invoke(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
    private static object Get(object o,string field)=>o.GetType().GetField(field).GetValue(o);
    private static void Set(object o,string field,object value)=>o.GetType().GetField(field).SetValue(o,value);
    private object Unit(int team,Vector3 position)
    {
        object u=Activator.CreateInstance(unitType,true);var model=new GameObject("Gate actor");model.transform.position=position;
        Set(u,"model",model);Set(u,"alive",true);Set(u,"hp",10000f);Set(u,"maxHp",10000f);Set(u,"damage",100f);
        Set(u,"ninjaHuntPending",true);Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));units.Add(u);return u;
    }
    private object Cast(object source,object target)=>Invoke("CreateNinjaEightGateCast",source,target,1f,0);
    private void Advance(object cast,float a,float b)=>Invoke("AdvanceNinjaEightGate",cast,a,b);
    private static SpriteMonsterAnimationDriver Driver(string id="037_chundongi")=>
        Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+id)).GetComponentInChildren<SpriteMonsterAnimationDriver>();
    private static void Step(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }

    [TestCase(.008333333f)] [TestCase(.1f)] [TestCase(2f)]
    public void CenterTakesThreePathHitsAndOneBlastRegardlessOfFrameRate(float step)
    {
        var source=Unit(0,Vector3.left*6);var enemy=Unit(1,Vector3.zero);var cast=Cast(source,enemy);
        Advance(cast,0f,NinjaCatRank3Rules.GateLaunchTime);Assert.That(Get(enemy,"hp"),Is.EqualTo(10000f));
        for(float t=NinjaCatRank3Rules.GateLaunchTime;t<2f;t+=step)Advance(cast,t,t+step);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(9580f));Assert.That(Get(source,"skillHits"),Is.EqualTo(4));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(420f).Within(.001f));
        Assert.That(((CombatStatusState)Get(enemy,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.True);
        Advance(cast,0,2);Assert.That(Get(enemy,"hp"),Is.EqualTo(9580f));
    }

    [Test] public void EachMovingKunaiHitsOnlyOnceAndCannotHitAheadOfItsVisiblePosition()
    {
        var source=Unit(0,Vector3.left*6);var primary=Unit(1,Vector3.zero);
        var pathEnemy=Unit(1,NinjaCatRank3Rules.GatePoint(Vector3.zero,0)*.8f);var cast=Cast(source,primary);
        float launch=NinjaCatRank3Rules.GateLaunchTime;
        Advance(cast,0,launch+.003f);Assert.That(Get(pathEnemy,"hp"),Is.EqualTo(10000f));
        Advance(cast,launch+.003f,launch+.06f);Assert.That(Get(pathEnemy,"hp"),Is.EqualTo(9940f));
        Advance(cast,launch+.06f,launch+.08f);Assert.That(Get(pathEnemy,"hp"),Is.EqualTo(9940f));
        Assert.That(Get(primary,"hp"),Is.EqualTo(10000f));
    }

    [Test] public void FixedBlastSurvivesPrimaryDeathAndMovementAndIgnoresAlliesAndHiddenEnemies()
    {
        var source=Unit(0,Vector3.left*6);var primary=Unit(1,Vector3.zero);var inside=Unit(1,Vector3.right*3.5f);
        var outside=Unit(1,Vector3.right*4.9f);var ally=Unit(0,Vector3.zero);var hidden=Unit(1,Vector3.zero);
        Set(hidden,"ninjaCrossActive",true);var cast=Cast(source,primary);
        ((GameObject)Get(primary,"model")).transform.position=Vector3.right*30;Set(primary,"alive",false);Set(primary,"hp",0f);
        Assert.That(Invoke("NinjaEightGateValid",cast),Is.EqualTo(true));Advance(cast,0,2);
        Assert.That(Get(inside,"hp"),Is.EqualTo(9760f));
        foreach(var ignored in new[]{outside,ally,hidden})Assert.That(Get(ignored,"hp"),Is.EqualTo(10000f));
        Assert.That(Get(cast,"center"),Is.EqualTo(Vector3.zero));
    }

    [TestCase("death")] [TestCase("recast")] [TestCase("battle_end")] [TestCase("forced_move")]
    public void InvalidCastCannotDealDelayedDamage(string reason)
    {
        var source=Unit(0,Vector3.left*6);var primary=Unit(1,Vector3.zero);var cast=Cast(source,primary);
        if(reason=="death")Set(source,"alive",false);
        if(reason=="recast")Set(source,"skillUses",1);
        if(reason=="battle_end")battleType.GetField("battleEnding",Private).SetValue(battle,true);
        if(reason=="forced_move")Set(source,"positionRevision",1);
        Advance(cast,0,2);Assert.That(Get(primary,"hp"),Is.EqualTo(10000f));
        Invoke("FinishNinjaEightGate",cast);Assert.That(Get(cast,"finished"),Is.EqualTo(true));
    }

    [TestCase("death")] [TestCase("stun")] [TestCase("freeze")] [TestCase("battle_reset")]
    public void ReleasedFormationFinishesAfterCasterIsDisabledAndResetCancelsIt(string reason)
    {
        var source=Unit(0,Vector3.left*6);var enemy=Unit(1,Vector3.zero);var cast=Cast(source,enemy);
        Advance(cast,0,NinjaCatRank3Rules.GateLaunchTime);
        if(reason=="death"){Set(source,"alive",false);Set(source,"hp",0f);}
        if(reason=="stun")Set(source,"giraffeStunTimer",5f);
        if(reason=="freeze")((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Freeze,5f,enemy);
        if(reason=="battle_reset")battleType.GetField("combatGeneration",Private).SetValue(battle,1);
        Invoke("FinishNinjaEightGate",cast);
        Advance(cast,NinjaCatRank3Rules.GateLaunchTime,2f);Invoke("FinishNinjaEightGate",cast);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(reason=="battle_reset"?10000f:9580f));
        Assert.That(Get(cast,"finished"),Is.EqualTo(true));
    }

    [TestCase("037_chundongi")] [TestCase("038_seolmyo")] [TestCase("039_sundongi")] [TestCase("040_hongryeon")]
    public void HandSignHoldsForChargeThenLaunchesAndFreezeAndInterruptRestoreCorrectly(string id)
    {
        var driver=Driver(id);var renderer=driver.GetComponent<SpriteRenderer>();int finished=0,arrived=0;float sampled=0;
        driver.PlayNinjaEightGateAttack(()=>true,()=>{},()=>{},()=>arrived++,(a,b)=>sampled=b,()=>finished++);
        Step(driver,.2f);Assert.That(renderer.forceRenderingOff,Is.True);
        Step(driver,.28f);Assert.That(arrived,Is.EqualTo(1));Assert.That(renderer.forceRenderingOff,Is.False);
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(driver.SkillAttackHitFrameIndex-1));
        var sprite=renderer.sprite;driver.SetFrozen(true);Step(driver,2f);
        Assert.That(sampled,Is.EqualTo(.48f).Within(.0001f));Assert.That(renderer.sprite,Is.SameAs(sprite));
        driver.SetFrozen(false);Step(driver,.34f);Assert.That(renderer.sprite,Is.SameAs(sprite));
        Step(driver,.02f);Assert.That(driver.CurrentFrameIndex,Is.EqualTo(driver.SkillAttackHitFrameIndex));
        Step(driver,.5f);Assert.That(finished,Is.EqualTo(1));Assert.That(driver.IsActionLocked,Is.False);
        driver.PlayNinjaEightGateAttack(()=>true,()=>{},()=>{},()=>{},(a,b)=>{},()=>finished++);
        Step(driver,.2f);driver.InterruptCurrentAction();Step(driver,2f);
        Assert.That(renderer.forceRenderingOff,Is.False);Assert.That(finished,Is.EqualTo(2));
    }

    [Test] public void EightPixelBladesConvergeTogetherAndCancellationDestroysFormation()
    {
        var renderer=Driver().GetComponent<SpriteRenderer>();var fx=PixelNinjaEightGate.Play(Vector3.zero,renderer,()=>true);
        fx.Sample(.6f);
        var blades=(SpriteRenderer[])typeof(PixelNinjaEightGate).GetField("blades",Private).GetValue(fx);
        var before=new Vector3[8];
        for(int i=0;i<8;i++)
        {Assert.That(blades[i].enabled,Is.True);before[i]=blades[i].transform.position;
            Assert.That(blades[i].sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));}
        fx.Sample(NinjaCatRank3Rules.GateLaunchTime+NinjaCatRank3Rules.GateFlightDuration*.5f);
        for(int i=0;i<8;i++)
        {Vector3 p=blades[i].transform.position;p.y=0;before[i].y=0;Assert.That(p.magnitude,Is.EqualTo(before[i].magnitude*.5f).Within(.001f));}
        fx.Cancel();Assert.That(fx==null,Is.True);
    }
}
