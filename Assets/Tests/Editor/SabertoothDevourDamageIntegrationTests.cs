using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SabertoothDevourDamageIntegrationTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private SabertoothDevourCinematicTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object actor;
    private object[] victims;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        random=UnityEngine.Random.state;
        fixture=new SabertoothDevourCinematicTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally{UnityEngine.Random.state=random;}
    }

    [TestCase("geomchi",0)] [TestCase("geomchi",1)]
    [TestCase("seola",0)] [TestCase("seola",1)]
    [TestCase("nox",0)] [TestCase("nox",1)]
    [TestCase("terakan",0)] [TestCase("terakan",1)]
    public void StageReturnAndNormalUnitTicksApplyTheClosedJawsDamageToEightOpponents(string species,int team)
    {
        actor=Get<object>(fixture,team==0?"source":"target");
        Set(actor,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(actor,"defense",100f);
        for(int i=1;i<8;i++)FixtureCall("Unit",1-team,new Vector3((team==0?1f:-1f)*(2f+i*.4f),0f,i*.3f));
        victims=((IList)Get<object>(battle,"units")).Cast<object>().Where(u=>u!=actor).ToArray();
        foreach(object victim in victims)
        {
            Set(victim,"skillTimer",999f);Set(victim,"cooldown",999f);
            Set(victim,"defense",50f);Set(victim,"itemShield",20f);Set(victim,"itemShieldTimer",100f);
        }
        Set(actor,"skillTimer",0f);
        Assert.That(Call("UseReadySkill",actor,false),Is.True);
        Assert.That(Call("UpdateSabertoothCinematic"),Is.True);
        foreach(object victim in victims)Assert.That(Get<float>(victim,"hp"),Is.EqualTo(5000f));
        FixtureCall("FinishStage",true);
        object state=Get<object>(actor,"sabertoothDevour");Assert.That(state,Is.Not.Null);
        Assert.That(Get<float>(state,"time"),Is.Zero);
        Assert.That(Get<bool>(state,"cinematicOpen"),Is.True);
        float power=(float)Call("SkillPower",actor)*SabertoothDevourRules.DamageRatio;
        float[] expected=victims.Select(v=>Mathf.Max(0f,power/(1f+(float)Call("CurrentDefense",v)/100f)-20f)).ToArray();

        TickTo(SabertoothDevourRules.HitTime-.001f);
        foreach(object victim in victims)Assert.That(Get<float>(victim,"hp"),Is.EqualTo(5000f),"Open jaws cannot deal early damage.");
        TickTo(SabertoothDevourRules.HitTime+.001f);
        Assert.That(Get<bool>(state,"hit"),Is.True);
        var effect=Get<PixelSabertoothDevour>(state,"effect");
        Assert.That(effect,Is.Not.Null);Assert.That(effect.Age,Is.GreaterThanOrEqualTo(SabertoothDevourRules.HitTime));
        for(int i=0;i<victims.Length;i++)
        {
            Assert.That(expected[i],Is.GreaterThan(0f));
            Assert.That(5000f-Get<float>(victims[i],"hp"),Is.EqualTo(expected[i]).Within(.03f),
                species+" team "+team+": the closed jaw must remove health after armor and the ordinary shield.");
        }
        Assert.That(Get<float>(actor,"skillDamage"),Is.EqualTo(expected.Sum()).Within(.1f));
        Assert.That(Get<int>(actor,"skillHits"),Is.EqualTo(8));
        float total=Get<float>(actor,"skillDamage");
        Call("AdvanceSabertoothDevour",actor,10f);
        Assert.That(Get<object>(actor,"sabertoothDevour"),Is.Null);
        Assert.That(Get<float>(actor,"skillDamage"),Is.EqualTo(total));
        Assert.That(Get<int>(actor,"skillHits"),Is.EqualTo(8));
    }

    private void TickTo(float until)
    {
        object state=Get<object>(actor,"sabertoothDevour");
        int ticks=0;
        while(Get<float>(state,"time")<until-.000001f)
        {
            Assert.That(++ticks,Is.LessThan(1000),"The committed devour must keep advancing in the ordinary unit loop.");
            float dt=Mathf.Min(.02f,until-Get<float>(state,"time"));
            Set(battle,"battleElapsed",Get<float>(battle,"battleElapsed")+dt);
            Call("UpdateUnit",actor,dt);
            foreach(object victim in victims)Call("UpdateUnit",victim,dt);
            Assert.That(Get<object>(actor,"sabertoothDevour"),Is.SameAs(state));
        }
    }

    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Fields).Invoke(battle,args);
    private object FixtureCall(string name,params object[] args)=>typeof(SabertoothDevourCinematicTests).GetMethod(name,Fields).Invoke(fixture,args);
    private static T Get<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Fields).GetValue(owner);
    private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Fields).SetValue(owner,value);
}
