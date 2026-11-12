using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SpeciesSkillScalingTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static;
    Type type=typeof(MonsterAutoBattlePrototype),unitType;
    MonsterAutoBattlePrototype battle;
    HashSet<GameObject> before;
    object source,target;
    [SetUp] public void Setup()
    {
        before=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Species scaling verification");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        source=Unit(0);target=Unit(1);
    }
    object Unit(int side)
    {
        object u=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Scaling unit");model.transform.position=Vector3.right*side*.7f;
        Set(u,"model",model);Set(u,"ring",new GameObject("Scaling ring"));
        Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,side));
        Set(u,"id",side);Set(u,"stableId","scaling-"+side);Set(u,"alive",true);
        Set(u,"hp",100000f);Set(u,"maxHp",100000f);Set(u,"damage",100f);Set(u,"collisionRadius",.4f);
        Set(u,"attackInterval",1f);Set(u,"skillCooldownDuration",9f);Set(u,"skillTimer",9f);
        ((IList)type.GetField("units",Private).GetValue(battle)).Add(u);return u;
    }
    void Set(object u,string key,object value){unitType.GetField(key).SetValue(u,value);}
    object Get(object u,string key){return unitType.GetField(key).GetValue(u);}
    object Call(string name,params object[] args){return type.GetMethod(name,Private).Invoke(battle,args);}
    void Species(string id)
    {
        var def=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        Assert.That(def,Is.Not.Null);Set(source,"definition",def);Set(source,"skillId",def.skillId);
        float health=1000f;
        Set(source,"maxHp",health);Set(source,"hp",health);Set(source,"defense",60f);
    }
    [TearDown] public void TearDown()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!before.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }
    [TestCase("lilas",32.5f)] [TestCase("bingareu",25f)] [TestCase("moska",32.5f)]
    [TestCase("woongi",35f)] [TestCase("dami",32.5f)] [TestCase("tino",42.5f)]
    [TestCase("terakan",15f)] [TestCase("ironkuma",12f)] [TestCase("cheolno",20.4f)]
    [TestCase("kogason",35f)] [TestCase("irontusk",21f)] [TestCase("gaiason",27.5f)] [TestCase("geumgangsu",27.5f)]
    public void EachSpeciesUsesItsLiveStatAndIgnoresAttackInvestment(string id,float expected)
    {
        Species(id);Assert.That((float)Call("SkillPower",source),Is.EqualTo(expected).Within(.001f));
        Set(source,"damage",900f);
        Assert.That((float)Call("SkillPower",source),Is.EqualTo(expected).Within(.001f));
        string stat=GameMasterData.MonsterGrowth(id).skillScalingStat;
        Set(source,stat=="MaxHealth"?"maxHp":"defense",stat=="MaxHealth"?(float)Get(source,"maxHp")*2f:120f);
        Assert.That((float)Call("SkillPower",source),Is.EqualTo(expected*2).Within(.001f));
    }
    [TestCase("tino",102f)] [TestCase("cheolno",48.96f)] [TestCase("magno",240f)]
    public void BaseSkillUsesSpeciesPowerAndAttackBuffOnlyAffectsAttackSpecies(string id,float expected)
    {
        Species(id);Set(source,"rank3AttackBonusTimer",2f);Set(source,"rank3AttackBonusRatio",.5f);
        float dealt=(float)Call("ApplySkillFormulaDamage",source,target,GameMasterData.Skill(TyrannoCombatRules.SkillId),false,0,1f,false);
        Assert.That(dealt,Is.EqualTo(id=="magno"?expected*1.5f:expected).Within(.02f));
    }
    [TestCase("tino",85f)] [TestCase("cheolno",40.8f)] [TestCase("magno",200f)]
    public void Rank3AndRank4HitShareTheSameScaling(string id,float expected)
    {
        Species(id);float dealt=(float)Call("Rank3Hit",source,target,2f,0f,0f);
        Assert.That(dealt,Is.EqualTo(expected).Within(.02f));
    }
    [TestCase("woongi",107f)] [TestCase("dami",106.5f)] [TestCase("ironkuma",102.4f)]
    public void SpiritBasicAttackKeepsOneOrdinaryAttackAndAddsSpeciesPowerOnce(string id,float expected)
    {
        Species(id);Set(source,"bearResolveTimer",4f);Set(source,"bearResolveTotalDuration",4f);
        Call("ResolveBasicAttackImpact",source,target,100f);
        Assert.That(100000f-(float)Get(target,"hp"),Is.EqualTo(expected).Within(.02f));
    }
    [Test] public void RagingHpSpeciesKeepsOneBasicAttackAcrossThreeContacts()
    {
        Species("woongi");Set(source,"bearResolveTimer",4f);Set(source,"bearResolveTotalDuration",4f);
        Set(source,"target",target);
        ((List<string>)Get(source,"activeTraitIds")).Add(BearCombatRules.RagingHundredStrikesTraitId);
        for(int hit=0;hit<3;hit++)Call("ResolveBearRagingStrike",source,target,100f,false,hit,0);
        Assert.That(100000f-(float)Get(target,"hp"),Is.EqualTo(100f+35f*2.1f).Within(.02f));
        Assert.That((float)Get(source,"skillTimer"),Is.EqualTo(8.1f).Within(.001f));
    }
    [Test] public void DefenseShieldsUseDefenseAndHaveExplicitCaps()
    {
        Assert.That(SabertoothCombatRules.StoneShield(60f),Is.EqualTo(36f));
        Assert.That(TyrannoCombatRules.SteelSkinShield(60f,1),Is.EqualTo(12f));
        Assert.That(TyrannoCombatRules.SteelSkinShield(60f,20),Is.EqualTo(36f));
    }
    [Test] public void LegacyMissingRowsRemainAttackScaled()
    {
        Assert.That(SpeciesSkillScaling.Power(null,37,1000,90),Is.EqualTo(37));
        Assert.That(SpeciesSkillScaling.Power(new MonsterGrowthMasterRow(),37,1000,90),Is.EqualTo(37));
    }

    [TestCase(0,.8f,false)] [TestCase(0,1.2f,false)]
    [TestCase(1,.8f,false)] [TestCase(1,1.2f,false)]
    [TestCase(0,.8f,true)] [TestCase(0,1.2f,true)]
    [TestCase(1,.8f,true)] [TestCase(1,1.2f,true)]
    public void RegionAffinityScalesPhysicalAndTrueDamageOnceOnEitherTeam(
        int team,float multiplier,bool skill)
    {
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        Set(source,"regionDamageMultiplier",multiplier);Set(target,"defense",100f);
        float dealt=(float)Call("ApplyDamage",source,target,100f,skill,!skill,
            false,false,0f,20f,1f,false,false,false);
        Assert.That(dealt,Is.EqualTo(70f*multiplier).Within(.02f));
    }

    [TestCase(.8f,false)] [TestCase(1.2f,false)]
    [TestCase(.8f,true)] [TestCase(1.2f,true)]
    public void RegionAffinityScalesMonsterPoisonButPreservesEnvironmentalPoison(
        float multiplier,bool environmental)
    {
        Set(source,"regionDamageMultiplier",multiplier);
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        statuses.ApplyStacks(CombatStatusKind.Poison,5f,2,10,source,10f,1f,
            false,environmental);
        type.GetField("periodicStatusTick",Private).SetValue(battle,
            new Action<CombatStatusEffect>(effect=>Call("HandlePeriodicStatusTick",effect)));
        Call("UpdateCombatStatuses",target,1f,false,false);
        Assert.That(100000f-(float)Get(target,"hp"),
            Is.EqualTo(20f*(environmental?1f:multiplier)).Within(.02f));
    }

    [TestCase(.8f)] [TestCase(1.2f)]
    public void MixedPoisonPreservesEachCastersAffinityAndEnvironmentalDamage(float multiplier)
    {
        Set(source,"regionDamageMultiplier",multiplier);
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        statuses.ApplyStacks(CombatStatusKind.Poison,5f,1,10,source,10f,1f,true,true);
        statuses.ApplyStacks(CombatStatusKind.Poison,5f,1,10,source,10f,1f,true);
        object second=Unit(0);Set(second,"regionDamageMultiplier",.8f);
        var poison=statuses.ApplyStacks(CombatStatusKind.Poison,5f,1,10,second,10f,1f,true);
        type.GetField("periodicStatusUnit",Private).SetValue(battle,target);
        Call("HandlePeriodicStatusTick",poison);
        Assert.That(100000f-(float)Get(target,"hp"),
            Is.EqualTo(10f+10f*multiplier+8f).Within(.02f));
    }
}
