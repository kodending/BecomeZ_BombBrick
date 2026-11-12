using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class KingKongLeylineDamageAccountingTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private KingKongLeylineCinematicTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object source,primary,secondary;
    private UnityEngine.Random.State randomState;

    [SetUp] public void Setup()
    {
        randomState=UnityEngine.Random.state;
        fixture=new KingKongLeylineCinematicTests();fixture.Setup();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        source=Get<object>(fixture,"source");primary=Get<object>(fixture,"primary");secondary=Get<object>(fixture,"secondary");
        Set(battle,"mlEpisodeActive",true);
        Set(battle,"mainCamera",null);
        object state=Call("FamilyTranscendenceStateFor",source,true);
        Set(state,"timer",6f);
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.Cleanup();}
        finally{UnityEngine.Random.state=randomState;}
    }

    [TestCase(0,false,false)] [TestCase(1,false,false)]
    [TestCase(0,true,false)] [TestCase(1,true,false)]
    [TestCase(0,false,true)] [TestCase(1,false,true)]
    [TestCase(0,true,true)] [TestCase(1,true,true)]
    public void PulsesAndBasicSplashesReportTheirActualDamageOnceOnEitherTeam(int team,bool splash,bool shielded)
    {
        SetTeam(source,team);SetTeam(primary,1-team);SetTeam(secondary,1-team);
        var victims=new[]{primary,secondary};
        float[] hp=victims.Select(v=>Get<float>(v,"hp")).ToArray();
        if(shielded)foreach(object victim in victims)Set(victim,"itemShield",100000f);
        float skillBefore=Get<float>(source,"skillDamage"),allBefore=Get<float>(source,"damageDealt");
        int hitsBefore=Get<int>(source,"skillHits");

        if(splash)Call("HandleFamilyTranscendenceBasicAttack",source,primary,100f);
        else Call("TickTranscendentKingKongZone",source,Call("FamilyTranscendenceStateFor",source,false),.01f);

        float[] losses=victims.Select((v,i)=>hp[i]-Get<float>(v,"hp")).ToArray();
        float actual=losses.Sum();int successful=losses.Count(x=>x>0f);
        if(shielded)Assert.That(actual,Is.Zero);
        else Assert.That(actual,Is.GreaterThan(0f));
        if(splash)Assert.That(losses[0],Is.Zero,"The leyline splash still excludes the normal attack's primary target.");
        Assert.That(Get<float>(source,"skillDamage")-skillBefore,Is.EqualTo(actual).Within(.001f),
            "The statistics must show the same damage that was removed from the victims' HP.");
        Assert.That(Get<int>(source,"skillHits")-hitsBefore,Is.EqualTo(successful));
        Assert.That(Get<float>(source,"damageDealt")-allBefore,Is.EqualTo(actual).Within(.001f),
            "Recording skill damage must not duplicate the shared total-damage counter.");
    }

    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Hidden).Invoke(battle,args);
    private static T Get<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Hidden).GetValue(owner);
    private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Hidden).SetValue(owner,value);
    private static void SetTeam(object owner,int team)
    {
        FieldInfo field=owner.GetType().GetField("team",Hidden);field.SetValue(owner,Enum.ToObject(field.FieldType,team));
    }
}
