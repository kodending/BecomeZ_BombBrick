using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class NinjaBlackMoonTests
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
        var host=new GameObject("Moon test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
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
        object u=Activator.CreateInstance(unitType,true);var model=new GameObject("Moon actor");model.transform.position=position;
        Set(u,"model",model);Set(u,"alive",true);Set(u,"hp",10000f);Set(u,"maxHp",10000f);Set(u,"damage",100f);
        Set(u,"ninjaHuntPending",true);Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        units.Add(u);return u;
    }
    private object Cast(object source,object primary)=>Invoke("CreateNinjaBlackMoonCast",source,primary,1f,0);
    private void Cut(object cast,int i)
    {
        Set(cast,"positioned",((Array)Get(cast,"targets")).GetValue(i));Invoke("HitNinjaBlackMoon",cast,i);
    }
    private static SpriteMonsterAnimationDriver Driver(string id="037_chundongi")=>
        Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+id)).GetComponentInChildren<SpriteMonsterAnimationDriver>();
    private static void Step(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }

    [TestCase(.008333333f)] [TestCase(.23f)]
    public void RealBeginAndSafeLandingReachEveryCutAndFinalBurstAtPacedAnimationSpeed(float step)
    {
        battleType.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        var source=Unit(0,Vector3.left*4f);var primary=Unit(1,Vector3.zero);
        var secondary=Unit(1,Vector3.right*1.6f);
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_chundongi");
        Set(source,"definition",definition);Set(source,"skillId",definition.skillId);
        ((IList)Get(source,"activeTraitIds")).Add(NinjaCatRank3Rules.BlackMoonHunt);
        var driver=Driver();Set(source,"spriteAnimation",driver);
        driver.SetCombatPlaybackSpeed(BombBrick.Combat.CombatStatFormula.CombatAnimationRate);
        Assert.That(Invoke("BeginShadowHunt",source,GameMasterData.Skill(definition.skillId)),Is.EqualTo(true));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Invoke("BeginNinjaBlackMoon",source,0);
        Step(driver,.1f);
        Assert.That(Get(primary,"hp"),Is.EqualTo(10000f),"The begin/vanish pose must not deal contact damage.");
        bool observedCut=false;
        for(float t=0f;t<3f;t+=step)
        {
            Step(driver,step);
            if((float)Get(primary,"hp")==9950f)observedCut=true;
        }
        Assert.That(observedCut||step>.1f,Is.True,"A normal frame stream must expose the cut before detonation.");
        Assert.That(Get(primary,"hp"),Is.EqualTo(9670f).Within(.01f));
        Assert.That(Get(secondary,"hp"),Is.EqualTo(9850f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(480f).Within(.01f));
        Assert.That(Get(source,"ninjaHuntPending"),Is.EqualTo(false));
        Assert.That(driver.IsActionLocked,Is.False);
    }

    [TestCase(1)] [TestCase(3)] [TestCase(6)]
    public void EachCutUsesContactFrameAndSingleFinalBurstUsesSheathedFrame(int count)
    {
        var driver=Driver();var events=new List<string>();int completed=0;
        driver.PlayNinjaCrossAttack(()=>true,()=>events.Add("vanish"),()=>events.Add("log"),
            i=>events.Add("move"+i),i=>
            {
                Assert.That(driver.CurrentFrameIndex,Is.EqualTo(i==count?driver.SkillAttackFrameCount-1:driver.SkillAttackHitFrameIndex));
                events.Add("hit"+i);
            },()=>completed++,count,true);
        for(int i=0;i<240;i++)Step(driver,1f/120f);
        var expected=new List<string>{"vanish","log"};
        for(int i=0;i<=count;i++){expected.Add("move"+i);expected.Add("hit"+i);}
        CollectionAssert.AreEqual(expected,events);Assert.That(completed,Is.EqualTo(1));Assert.That(driver.IsActionLocked,Is.False);
    }

    [TestCase(1)] [TestCase(6)]
    public void TargetsReceiveFiftyThenOneHundredAndOnlyPrimaryGetsOneEightyWithoutOverflow(int count)
    {
        var source=Unit(0,Vector3.left*4f);var primary=Unit(1,Vector3.zero);
        for(int i=1;i<count;i++)Unit(1,Vector3.right*i*.4f);
        var cast=Cast(source,primary);Set(source,"ninjaCrossActive",true);
        for(int i=0;i<count;i++)Cut(cast,i);
        for(int i=1;i<=count;i++)Assert.That(Get(units[i],"hp"),Is.EqualTo(9950f));
        Invoke("HitNinjaBlackMoon",cast,count);
        Assert.That(Get(primary,"hp"),Is.EqualTo(9670f));
        for(int i=2;i<=count;i++)Assert.That(Get(units[i],"hp"),Is.EqualTo(9850f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(count*2+1));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(count*150f+180f));
        Assert.That(Get(source,"ninjaCrossActive"),Is.EqualTo(false));
        Invoke("HitNinjaBlackMoon",cast,count);Assert.That(Get(primary,"hp"),Is.EqualTo(9670f));
        Invoke("FinishNinjaBlackMoon",cast);Assert.That(Get(source,"ninjaHuntPending"),Is.EqualTo(false));
    }

    [Test] public void SelectionIsFixedAtSixNearestDistinctEnemiesWithPrimaryFirst()
    {
        var source=Unit(0,Vector3.left*4f);var primary=Unit(1,Vector3.zero);
        var ally=Unit(0,Vector3.zero);var dead=Unit(1,Vector3.zero);Set(dead,"alive",false);
        var hidden=Unit(1,Vector3.zero);Set(hidden,"ninjaCrossActive",true);
        var outside=Unit(1,Vector3.right*MonsterAutoBattlePrototype.TileSize*4f);
        for(int i=1;i<8;i++)Unit(1,Vector3.right*i*.2f);
        var cast=Cast(source,primary);var selected=(Array)Get(cast,"targets");
        Assert.That(selected.Length,Is.EqualTo(6));Assert.That(selected.GetValue(0),Is.SameAs(primary));
        foreach(var ignored in new[]{ally,dead,hidden,outside,units[11],units[12]})CollectionAssert.DoesNotContain(selected,ignored);
        Unit(1,Vector3.zero);Assert.That(((Array)Get(cast,"targets")).Length,Is.EqualTo(6));
    }

    [Test] public void PrimaryDeathDoesNotCancelOtherMarksAndMovingVictimStillBursts()
    {
        var source=Unit(0,Vector3.left*4f);var primary=Unit(1,Vector3.zero);var secondary=Unit(1,Vector3.right);
        var cast=Cast(source,primary);Cut(cast,0);Set(primary,"hp",0f);Set(primary,"alive",false);
        Assert.That(Invoke("NinjaBlackMoonValid",cast),Is.EqualTo(true));Cut(cast,1);
        ((GameObject)Get(secondary,"model")).transform.position=Vector3.right*50f;
        Invoke("HitNinjaBlackMoon",cast,2);
        Assert.That(Get(secondary,"hp"),Is.EqualTo(9850f));Assert.That(Get(primary,"hp"),Is.EqualTo(0f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
    }

    [TestCase("death")] [TestCase("recast")] [TestCase("battle_end")] [TestCase("interrupt")]
    public void CancellationPreventsDelayedBurstAndReleasesProtection(string reason)
    {
        var source=Unit(0,Vector3.left*4f);var primary=Unit(1,Vector3.zero);var cast=Cast(source,primary);
        var driver=Driver();
        driver.PlayNinjaCrossAttack(()=> (bool)Invoke("NinjaBlackMoonValid",cast),
            ()=>Set(source,"ninjaCrossActive",true),()=>{},i=>Set(cast,"positioned",primary),
            i=>Invoke("HitNinjaBlackMoon",cast,i),()=>Invoke("FinishNinjaBlackMoon",cast),1,true);
        Step(driver,.38f);Assert.That(Get(primary,"hp"),Is.EqualTo(9950f));
        if(reason=="death")Set(source,"alive",false);
        if(reason=="recast"){Set(source,"skillUses",1);Set(source,"ninjaCrossActive",false);}
        if(reason=="battle_end")battleType.GetField("battleEnding",Private).SetValue(battle,true);
        if(reason=="interrupt")driver.InterruptCurrentAction();
        Step(driver,2f);Assert.That(Get(primary,"hp"),Is.EqualTo(9950f));
        Assert.That(Get(source,"ninjaCrossActive"),Is.EqualTo(false));Assert.That(driver.IsActionLocked,Is.False);
    }

    [Test] public void FreezePausesMoonDetonationAndResumesOnce()
    {
        var driver=Driver();int hits=0;
        driver.PlayNinjaCrossAttack(()=>true,()=>{},()=>{},i=>{},i=>hits++,()=>{},1,true);
        Step(driver,.38f);Assert.That(hits,Is.EqualTo(1));driver.SetFrozen(true);
        var sprite=driver.GetComponent<SpriteRenderer>().sprite;Step(driver,3f);
        Assert.That(hits,Is.EqualTo(1));Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(sprite));
        driver.SetFrozen(false);Step(driver,1f);Step(driver,1f);Assert.That(hits,Is.EqualTo(2));
    }

    [Test] public void BodySealFollowsTargetThenSplitsAndExpiresEvenIfTargetDies()
    {
        var renderer=Driver().GetComponent<SpriteRenderer>();Step(renderer.GetComponent<SpriteMonsterAnimationDriver>(),0f);
        bool valid=true;var mark=PixelNinjaMoonMark.Play(renderer,()=>valid);
        var animate=typeof(PixelNinjaMoonMark).GetMethod("Animate",Private);
        Vector3 original=mark.transform.position;
        renderer.transform.position+=Vector3.right*2f;animate.Invoke(mark,new object[]{.12f});
        Assert.That(mark.transform.position,Is.EqualTo(original+Vector3.right*2f));
        Assert.That(mark.transform.localScale.x,Is.InRange(.5399f,.7801f));
        Assert.That(mark.GetComponentInChildren<SpriteRenderer>().sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        mark.Prime();animate.Invoke(mark,new object[]{.2f});
        Assert.That(mark.transform.localScale.x,Is.GreaterThan(.72f));
        mark.Explode();valid=false;animate.Invoke(mark,new object[]{.08f});
        Assert.That(mark.transform.Find("Split Left").GetComponent<SpriteRenderer>().enabled,Is.True);
        animate.Invoke(mark,new object[]{.35f});Assert.That(mark==null,Is.True);
        mark=PixelNinjaMoonMark.Play(renderer,()=>false);Assert.That(mark==null,Is.True);
    }

    [Test] public void EveryMoonRushDisappearsBetweenHitsAndCancellationRestoresTheBody()
    {
        var driver=Driver();var renderer=driver.GetComponent<SpriteRenderer>();
        int arrivals=0,hits=0,departures=0;
        driver.PlayNinjaCrossAttack(()=>true,()=>{},()=>{},i=>
            {arrivals++;driver.SetNinjaMoonApproach(Vector3.right);},i=>hits++,()=>{},6,true,i=>departures++);
        Step(driver,.43f);Assert.That(hits,Is.EqualTo(1));Assert.That(renderer.forceRenderingOff,Is.False);
        Step(driver,.025f);Assert.That(departures,Is.EqualTo(1));Assert.That(renderer.forceRenderingOff,Is.True);
        Step(driver,.045f);Assert.That(arrivals,Is.EqualTo(2));Assert.That(renderer.forceRenderingOff,Is.False);
        Step(driver,.06f);Assert.That(hits,Is.EqualTo(2));
        Step(driver,.08f);Assert.That(renderer.forceRenderingOff,Is.True);
        driver.InterruptCurrentAction();Assert.That(renderer.forceRenderingOff,Is.False);
        Step(driver,2f);Assert.That(hits,Is.EqualTo(2));
    }
}
