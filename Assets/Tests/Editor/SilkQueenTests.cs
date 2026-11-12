using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SilkQueenTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object queen,victim,state;
    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Silk queen test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        queen=Unit(0);Set(queen,"augmentTranscended",true);victim=Unit(1);Set(victim,"hp",0f);
        state=Call("FamilyTranscendenceStateFor",queen,true);Call("BeginSilkQueenDomain",queen,victim,state);
    }
    [TearDown] public void Cleanup()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }
    private object Unit(int team)
    {
        var u=Activator.CreateInstance(unitType,true);var list=(IList)type.GetField("units",Private).GetValue(battle);
        Set(u,"id",list.Count);Set(u,"stableId","silk-test-"+list.Count);
        Set(u,"model",new GameObject("Silk test unit"));Set(u,"ring",new GameObject("Silk test shadow"));
        Set(u,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+(team==0?"aranea":"woongi")));
        Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(u,"alive",true);
        Set(u,"hp",1000f);Set(u,"maxHp",1000f);Set(u,"damage",100f);Set(u,"collisionRadius",.3f);
        list.Add(u);return u;
    }
    private void Set(object o,string key,object value)=>o.GetType().GetField(key).SetValue(o,value);
    private object Get(object o,string key)=>o.GetType().GetField(key).GetValue(o);
    private object Call(string method,params object[] args)=>type.GetMethod(method,Private).Invoke(battle,args);

    [TestCase("ally")] [TestCase("alive")] [TestCase("clone")] [TestCase("summon")]
    [TestCase("claimed")] [TestCase("expired")] [TestCase("dead_owner")]
    public void RejectsIneligibleCorpses(string kind)
    {
        if(kind=="ally")Set(victim,"team",Get(queen,"team"));
        if(kind=="alive")Set(victim,"hp",1f);
        if(kind=="clone")Set(victim,"augmentShadowClone",true);
        if(kind=="summon")Set(victim,"rank3MycokingSummon",true);
        if(kind=="claimed")Set(victim,"silkHarvested",true);
        if(kind=="expired")Set(state,"timer",0f);
        if(kind=="dead_owner")Set(queen,"hp",0f);
        Assert.That(Call("ReserveSilkPuppet",victim),Is.Null);Assert.That(Get(state,"charges"),Is.EqualTo(2));
    }
    [Test] public void ReservesOnlyTwoIncludingPendingAndCannotReharvestAcrossQueens()
    {
        Assert.That(Call("ReserveSilkPuppet",victim),Is.SameAs(queen));
        Assert.That(Call("ReserveSilkPuppet",victim),Is.Null);
        var second=Unit(1);Set(second,"hp",0f);Assert.That(Call("ReserveSilkPuppet",second),Is.SameAs(queen));
        Call("BeginSilkQueenDomain",queen,victim,state);
        var third=Unit(1);Set(third,"hp",0f);Assert.That(Call("ReserveSilkPuppet",third),Is.Null);
        Assert.That(Get(state,"silkPuppetsPending"),Is.EqualTo(2));
        var other=Unit(0);Set(other,"augmentTranscended",true);
        var next=Call("FamilyTranscendenceStateFor",other,true);Call("BeginSilkQueenDomain",other,victim,next);
        Assert.That(Call("ReserveSilkPuppet",victim),Is.Null);
    }
    [Test] public void RecastDoesNotExceedTwoLivingPuppets()
    {
        for(int i=0;i<2;i++){var puppet=Unit(0);Set(puppet,"silkPuppet",true);Set(puppet,"rank3SummonOwner",queen);}
        Call("BeginSilkQueenDomain",queen,victim,state);
        Assert.That(Call("ReserveSilkPuppet",victim),Is.Null);
    }
    [Test] public void WholeBattlefieldIncludesDeathsFarFromThePrimaryTarget()
    {
        ((GameObject)Get(victim,"model")).transform.position=new Vector3(15f,0,12f);
        Assert.That(Call("ReserveSilkPuppet",victim),Is.SameAs(queen));
    }
    [Test] public void FieldMatchesRectangularArenaAndReleasesOnOwnerDeath()
    {
        var arena=new Bounds(new Vector3(3,0,2),new Vector3(20,.2f,12));
        var fx=PixelSilkQueenDomain.Create(arena,null);
        Assert.That(fx.GetComponent<MeshRenderer>().bounds.size.x,Is.EqualTo(20f).Within(.03f));
        Assert.That(fx.GetComponent<MeshRenderer>().bounds.size.z,Is.EqualTo(12f).Within(.03f));
        Set(state,"silkDomain",fx);Call("CleanupRank3Unit",queen);
        Assert.That(Get(state,"timer"),Is.Zero);Assert.That(Get(state,"silkDomain"),Is.Null);
        typeof(PixelSilkQueenDomain).GetMethod("Animate",Private).Invoke(fx,new object[]{.3f});
        Assert.That(fx==null,Is.True);
    }
    [Test] public void OriginalNeonSpiderKeepsItsCentralEmblemAndRisingBoundary()
    {
        var fx=PixelSilkQueenDomain.Create(new Bounds(Vector3.zero,new Vector3(20,.2f,12)),null);
        Mesh mesh=fx.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh.name,Is.EqualTo("Neon Spider Silk Domain"));
        Assert.That(mesh.vertexCount,Is.EqualTo(3120),"The original spider emblem, floor reflection and layered rim must not be replaced by plain strands.");
        Assert.That(mesh.bounds.size.y,Is.EqualTo(.58f).Within(.001f));
        Assert.That(Array.Exists(mesh.colors,c=>c.b>c.r+.1f&&c.b>c.g+.2f&&c.a>.8f),Is.True);
        fx.Reveal(.6f);Assert.That(fx.transform.localScale.y,Is.EqualTo(1f));
        fx.Reveal(0f);Assert.That(fx.transform.localScale.y,Is.EqualTo(.1f).Within(.001f));
        Assert.That(fx.GetComponent<MeshRenderer>().sharedMaterial.color.a,Is.Zero,"A cached cinematic must be able to start the original reveal again.");
    }
    [TestCase(false)] [TestCase(true)] public void PendingRaiseCancelsOnDeathOrReset(bool reset)
    {
        Assert.That(Call("ReserveSilkPuppet",victim),Is.SameAs(queen));
        var sequence=(IEnumerator)Call("RaiseSilkPuppet",queen,victim,state,Vector3.zero,0);
        Assert.That(sequence.MoveNext(),Is.True);
        if(reset)type.GetField("combatGeneration",Private).SetValue(battle,1);else Set(queen,"hp",0f);
        Assert.That(sequence.MoveNext(),Is.False);Assert.That(Get(state,"silkPuppetsPending"),Is.Zero);
    }
    [TestCase(false)] [TestCase(true)] public void PuppetsExpireOrDisappearWithTheirOwner(bool ownerDeath)
    {
        var puppet=Unit(0);Set(puppet,"silkPuppet",true);Set(puppet,"rank3SummonOwner",queen);
        Set(puppet,"rank3SummonTimer",5f);Set(puppet,"silkPuppetRise",.55f);
        Assert.That(Call("UpdateRank3SpecialUnit",puppet,.55f),Is.True);
        Assert.That(Get(puppet,"rank3SummonTimer"),Is.EqualTo(5f));
        if(ownerDeath)Set(queen,"alive",false);
        Assert.That(Call("UpdateRank3SpecialUnit",puppet,ownerDeath?.01f:5f),Is.True);
        Assert.That(Get(puppet,"alive"),Is.False);
    }
    [Test] public void DomainNoLongerDealsPeriodicDamageOrAppliesSlow()
    {
        Set(victim,"hp",1000f);Call("UpdateFamilyTranscendence",queen,1.1f);
        Assert.That(Get(victim,"hp"),Is.EqualTo(1000f));Assert.That(Get(victim,"rank3ActionSlowTimer"),Is.Zero);
        Assert.That(Get(victim,"frogSlowTimer"),Is.Zero);
    }
    [Test] public void PuppetCannotUseSkillsAndUsesOnlyOrdinaryDamage()
    {
        Set(victim,"hp",1000f);Set(queen,"silkPuppet",true);Set(queen,"augmentShadowClone",true);
        Set(queen,"augmentTranscended",false);Set(queen,"target",victim);
        Assert.That(Call("TryUseSkill",queen,true),Is.False);
        Call("ResolveBasicAttackImpact",queen,victim,40f);
        Assert.That(Get(victim,"hp"),Is.EqualTo(960f).Within(.01f));
        Assert.That(Get(queen,"traitActivations"),Is.Zero);
    }
    [Test] public void GhostKeepsAnimatedSpriteAndUsesTranslucentBodyWithPixelOutlineAndOneStraightThread()
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi"));
        var body=go.GetComponentInChildren<SpriteRenderer>();
        var original=body.sprite;var fx=PixelSilkPuppet.Create(body,()=>true);
        typeof(PixelSilkPuppet).GetField("age",Private).SetValue(fx,.55f);
        typeof(PixelSilkPuppet).GetMethod("Draw",Private).Invoke(fx,null);
        Assert.That(body.sprite,Is.SameAs(original));Assert.That(body.color.a,Is.EqualTo(.7f));
        var lines=body.GetComponentsInChildren<LineRenderer>();Assert.That(lines.Length,Is.EqualTo(1));
        Assert.That(lines[0].positionCount,Is.EqualTo(2));
        Assert.That(lines[0].GetPosition(0).x,Is.EqualTo(lines[0].GetPosition(1).x));
        Assert.That(lines[0].GetPosition(0).z,Is.EqualTo(lines[0].GetPosition(1).z));
        Assert.That(body.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(2));
        fx.Depart();fx.Depart();
        Assert.That(Object.FindObjectsOfType<PixelBearAvatarDeparture>().Length,Is.EqualTo(1));
    }
}
