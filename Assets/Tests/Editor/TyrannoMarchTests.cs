using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoMarchTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private HashSet<GameObject> existing;
    [SetUp] public void Setup()=>existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
    [TearDown] public void Cleanup()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }

    [TestCase(1f)] [TestCase(2f)]
    public void MarchClockFreezesAndCancellationReleasesOnce(float speed)
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        float time=0f;int ended=0;bool valid=true;
        driver.SetGlobalSpeedMultiplier(speed);
        driver.PlayTyrannoMarch(()=>valid,t=>time=t,()=>ended++);
        advance.Invoke(driver,new object[]{.3f/speed});
        Assert.That(time,Is.EqualTo(.3f).Within(.001f));
        driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});
        Assert.That(time,Is.EqualTo(.3f).Within(.001f));
        driver.SetFrozen(false);advance.Invoke(driver,new object[]{.4f/speed});
        Assert.That(time,Is.EqualTo(.7f).Within(.001f));
        Assert.That(driver.TyrannoMarchActive,Is.True);
        valid=false;advance.Invoke(driver,new object[]{.01f});
        Assert.That(driver.TyrannoMarchActive,Is.False);Assert.That(ended,Is.EqualTo(1));
        driver.PlayDeath();Assert.That(ended,Is.EqualTo(1));
    }

    [Test]
    public void EachStridePlantsOnTheHitPoseAndHoldsItsGround()
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        int hit=(int)typeof(SpriteMonsterAnimationDriver).GetField("skillAttackHitFrameIndex",Private).GetValue(driver);
        driver.PlayTyrannoMarch(()=>true,t=>{},()=>{});
        float elapsed=0f;
        for(int stride=0;stride<2;stride++)
        {
            float start=stride*TyrannoRank3Rules.MarchStrideDuration;
            float raised=TyrannoRank3Rules.MarchWindup+start+.2f;
            advance.Invoke(driver,new object[]{raised-elapsed});elapsed=raised;
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(hit-1));
            float raisedScale=driver.transform.localScale.y;
            float planted=TyrannoRank3Rules.MarchWindup+start+TyrannoRank3Rules.MarchFootContact+.001f;
            advance.Invoke(driver,new object[]{planted-elapsed});elapsed=planted;
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(hit));
            Assert.That(driver.transform.localScale.y,Is.LessThan(raisedScale*.8f));
            float distance=TyrannoRank3Rules.MarchTravelProgress(start+TyrannoRank3Rules.MarchFootContact);
            Assert.That(distance,Is.EqualTo((stride+1)*.5f).Within(.0001f));
            Assert.That(TyrannoRank3Rules.MarchTravelProgress(start+.499f),Is.EqualTo(distance).Within(.0001f));
        }
        float previous=0f;
        for(int sample=0;sample<=100;sample++)
        {
            float progress=TyrannoRank3Rules.MarchTravelProgress(sample*.01f);
            Assert.That(progress-previous,Is.InRange(0f,.023f),"No backward movement or jumps");
            previous=progress;
        }
        Assert.That(previous,Is.EqualTo(1f));
    }

    [TestCase(false,0)] [TestCase(true,0)] [TestCase(false,1)] [TestCase(false,2)]
    public void MarchSweepsOnceCarriesEnemiesAndCleansUp(bool immune,int interruption)
    {
        var host=new GameObject("March battle");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();var type=typeof(MonsterAutoBattlePrototype);
        var ut=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,new Vector3(40,1,40)));
        if(interruption==2)type.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(new Vector3(-7.5f,0,0),new Vector3(25,1,40)));
        var units=(IList)type.GetField("units",Private).GetValue(battle);
        for(int i=0;i<4;i++)
        {
            var unit=Activator.CreateInstance(ut,true);units.Add(unit);
            var model=i==0?Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/041_tino")):new GameObject("March target");
            model.transform.position=i==0?Vector3.zero:i==1?new Vector3(2f,0,0):i==2?new Vector3(2f,0,4f):new Vector3(2f,0,0);
            ut.GetField("model").SetValue(unit,model);ut.GetField("ring").SetValue(unit,new GameObject("Ring"));
            ut.GetField("definition").SetValue(unit,Resources.Load<MonsterDefinition>("MonsterData/Monster_magno"));
            ut.GetField("alive").SetValue(unit,true);ut.GetField("hp").SetValue(unit,10000f);
            ut.GetField("maxHp").SetValue(unit,10000f);ut.GetField("damage").SetValue(unit,100f);
            ut.GetField("stableId").SetValue(unit,"march-"+i);ut.GetField("id").SetValue(unit,i);
            ut.GetField("team").SetValue(unit,Enum.ToObject(ut.GetField("team").FieldType,i==0||i==3?0:1));
            if(i==0)ut.GetField("spriteAnimation").SetValue(unit,model.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        }
        Func<int,float> hp=i=>(float)ut.GetField("hp").GetValue(units[i]);
        Func<int,Vector3> pos=i=>((GameObject)ut.GetField("model").GetValue(units[i])).transform.position;
        ut.GetField("target").SetValue(units[0],units[1]);
        if(immune)ut.GetField("rank3ForceImmuneTimer").SetValue(units[1],10f);
        type.GetMethod("StartTyrannoMarch",Private).Invoke(battle,new[]{units[0],(object)0});
        var march=ut.GetField("tyrannoMarch").GetValue(units[0]);
        var tick=type.GetMethod("AdvanceTyrannoMarch",Private);
        var protectedMethod=type.GetMethod("TyrannoMarchProtected",Private);
        Assert.That(protectedMethod.Invoke(null,new[]{units[0]}),Is.True);
        tick.Invoke(battle,new[]{units[0],march,(object).59f});
        Assert.That(pos(0).x,Is.Zero);Assert.That(hp(1),Is.EqualTo(10000f));
        if(interruption==1)
        {
            tick.Invoke(battle,new[]{units[0],march,(object)1f});
            Assert.That(ut.GetField("tyrannoPushedBy").GetValue(units[1]),Is.SameAs(units[0]));
            ut.GetField("alive").SetValue(units[0],false);
            ((SpriteMonsterAnimationDriver)ut.GetField("spriteAnimation").GetValue(units[0])).PlayDeath();
            Assert.That(ut.GetField("tyrannoPushedBy").GetValue(units[1]),Is.Null);
            Assert.That(ut.GetField("tyrannoMarch").GetValue(units[0]),Is.Null);
            Assert.That(hp(1),Is.EqualTo(9800f));return;
        }
        for(float t=.6f;t<1.62f;t+=.016f)tick.Invoke(battle,new[]{units[0],march,(object)t});
        tick.Invoke(battle,new[]{units[0],march,(object)1.6f});
        Assert.That(hp(1),Is.EqualTo(9800f));
        if(interruption==2){Assert.That(pos(0).x,Is.LessThan(4f));Assert.That(pos(1).x,Is.LessThanOrEqualTo(5f));}
        else
        {
            Assert.That(pos(0).x,immune?Is.LessThan(1f):Is.EqualTo(4f*MonsterAutoBattlePrototype.TileSize).Within(.01f));
            Assert.That(pos(1).x,immune?Is.EqualTo(2f):Is.GreaterThan(6f));
        }
        Assert.That(hp(2),Is.EqualTo(10000f));Assert.That(hp(3),Is.EqualTo(10000f));
        Assert.That(protectedMethod.Invoke(null,new[]{units[0]}),Is.False);
        tick.Invoke(battle,new[]{units[0],march,(object)TyrannoRank3Rules.MarchContact});
        tick.Invoke(battle,new[]{units[0],march,(object)(TyrannoRank3Rules.MarchContact+.01f)});
        Assert.That(hp(1),Is.EqualTo(9540f),"One collision and one finisher");
        Assert.That(ut.GetField("tyrannoPushedBy").GetValue(units[1]),Is.Null);
        type.GetMethod("EndTyrannoMarch",Private).Invoke(battle,new[]{units[0],march});
        Assert.That(ut.GetField("tyrannoMarch").GetValue(units[0]),Is.Null);
    }
}
