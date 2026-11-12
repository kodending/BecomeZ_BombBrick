using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class Rank3SkillModifierTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object source,target,second;
    private HashSet<GameObject> existing;
    private UnityEngine.Random.State random;

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());random=UnityEngine.Random.state;
        var host=new GameObject("Rank3 shared skill modifier test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        var run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Flags);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Flags).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        source=Unit(0,0,Vector3.left*3);target=Unit(1,1,Vector3.zero);second=Unit(2,1,Vector3.right);
    }
    private object Unit(int id,int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var model=new GameObject("Rank3 modifier actor");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",new GameObject("Rank3 modifier ring"));Set(unit,"id",id);Set(unit,"stableId","modifier-"+id);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);Set(unit,"hp",10000f);Set(unit,"maxHp",10000f);
        Set(unit,"damage",100f);Set(unit,"collisionRadius",.4f);Set(unit,"skillTraitDamageMultiplier",1f);Set(unit,"augmentSkillPowerMultiplier",1f);
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Flags).GetValue(battle)).Add(unit);return unit;
    }
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Flags).Invoke(battle,args);
    [TearDown] public void TearDown()
    {
        UnityEngine.Random.state=random;
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }

    [TestCase(false,"equipment",150f)] [TestCase(true,"equipment",150f)]
    [TestCase(false,"ramp",125f)] [TestCase(true,"ramp",125f)]
    [TestCase(false,"augment",200f)] [TestCase(true,"augment",200f)]
    [TestCase(false,"combined",350f)] [TestCase(true,"combined",350f)]
    [TestCase(false,"critical",150f)] [TestCase(true,"critical",150f)]
    public void Rank3DirectAndAreaContactsUseTheSameSkillModifiersAsOrdinarySkills(bool area,string modifier,float expected)
    {
        var items=(CombatItemModifiers)Get(source,"itemModifiers");
        if(modifier=="equipment"||modifier=="combined")items.skillPowerBonus=.5f;
        if(modifier=="ramp"||modifier=="combined")Set(source,"itemSkillPowerRamp",.25f);
        if(modifier=="augment"||modifier=="combined")Set(source,"augmentSkillPowerMultiplier",2f);
        if(modifier=="critical")
        {
            items.criticalChance=1f;items.specialEffects.Add(new ItemMasterRow{specialEffect="SkillCritical"});
            UnityEngine.Random.InitState(20261020);
        }
        if(area)Call("Rank3AreaDamage",source,Vector3.zero,2f,1f,0f,null);
        else Call("Rank3Hit",source,target,1f,0f,0f);
        Assert.That(10000f-(float)Get(target,"hp"),Is.EqualTo(expected).Within(.01f));
        Assert.That(10000f-(float)Get(second,"hp"),Is.EqualTo(area?expected:0f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(area?2:1));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(expected*(area?2:1)).Within(.01f));
        if(modifier=="critical")Assert.That(Get(source,"lastSkillHitCritical"),Is.EqualTo(true));
    }

    [Test] public void Rank3ContactScalesItsExplicitTrueDamageTogetherWithPhysicalDamage()
    {
        ((CombatItemModifiers)Get(source,"itemModifiers")).skillPowerBonus=.5f;
        Set(source,"augmentSkillPowerMultiplier",2f);Set(target,"defense",10000f);
        Call("Rank3Hit",source,target,1f,1f,25f);
        Assert.That(10000f-(float)Get(target,"hp"),Is.EqualTo(375f).Within(.01f));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(375f).Within(.01f));
    }

    [TestCase(false)] [TestCase(true)]
    public void Rank3CallersPreappliedTraitRatioIsNotMultipliedAgainByTheSharedSkillPath(bool area)
    {
        Set(source,"skillTraitDamageMultiplier",1.4f);
        ((CombatItemModifiers)Get(source,"itemModifiers")).skillPowerBonus=.5f;
        if(area)Call("Rank3AreaDamage",source,Vector3.zero,2f,1.4f,0f,null);
        else Call("Rank3Hit",source,target,1.4f,0f,0f);
        Assert.That(10000f-(float)Get(target,"hp"),Is.EqualTo(210f).Within(.01f));
        Assert.That(10000f-(float)Get(second,"hp"),Is.EqualTo(area?210f:0f).Within(.01f));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(210f*(area?2:1)).Within(.01f));
    }
}
