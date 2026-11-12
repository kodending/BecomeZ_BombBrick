using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class BearKingRoarTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object bear,near,far,outside,ally,hit;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("King roar test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=battleType.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        battleType.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        bear=Unit(0,0);near=Unit(1,.7f);far=Unit(1,2.9f);outside=Unit(1,3.1f);ally=Unit(0,.8f);
        hit=Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(unitType));
        ((List<string>)Get(bear,"activeTraitIds")).Add(BearCombatRules.KingOfBeastsRoarTraitId);
    }

    [TearDown]
    public void TearDown()
    {
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&created.transform.parent==null)
                Object.DestroyImmediate(created);
    }

    private object Unit(int team,float x)
    {
        var u=Activator.CreateInstance(unitType,true);
        var units=(IList)battleType.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Roar unit");model.transform.position=Vector3.right*x;
        Set(u,"id",units.Count);Set(u,"stableId","roar-test-"+units.Count);
        Set(u,"model",model);Set(u,"ring",new GameObject("Roar ring"));
        Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(u,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_berber"));
        Set(u,"skillId",BearCombatRules.SkillId);Set(u,"alive",true);
        Set(u,"hp",5000f);Set(u,"maxHp",5000f);Set(u,"damage",100f);
        Set(u,"attackInterval",1f);Set(u,"collisionRadius",.4f);
        Set(u,"skillCooldownDuration",5f);Set(u,"skillTimer",3f);
        units.Add(u);return u;
    }
    private void Set(object u,string key,object value)=>unitType.GetField(key).SetValue(u,value);
    private object Get(object u,string key)=>unitType.GetField(key).GetValue(u);
    private float Hp(object u)=>(float)Get(u,"hp");
    private CombatStatusState Status(object u)=>(CombatStatusState)Get(u,"combatStatuses");
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
    private void Wave(float reach,bool ending=false)=>Call("ApplyBearKingRoarWave",bear,Vector3.zero,
        reach,ending?220f:180f,ending,hit);

    [Test]
    public void WaveHitsWhenItReachesEachEnemyOnceWithinThreeTiles()
    {
        Wave(.5f);Assert.That(Hp(near),Is.EqualTo(5000));
        Wave(1f);Assert.That(Hp(near),Is.EqualTo(4820));Assert.That(Hp(far),Is.EqualTo(5000));
        Wave(3f);Wave(3f);
        Assert.That(Hp(near),Is.EqualTo(4820));Assert.That(Hp(far),Is.EqualTo(4820));
        Assert.That(Hp(outside),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        Assert.That(Get(bear,"skillHits"),Is.EqualTo(2));
        Assert.That(((GameObject)Get(near,"model")).transform.position.x,Is.EqualTo(.7f));
        Assert.That(Get(near,"displacing"),Is.False);
    }

    [Test]
    public void OpeningSlowsAttacksWithoutControlAndCleansesWithOtherDebuffs()
    {
        Wave(3f);
        var slow=Status(near).Get(CombatStatusKind.AttackSpeedDown);
        Assert.That(slow.remaining,Is.EqualTo(4f));Assert.That(slow.magnitude,Is.EqualTo(.2f));
        Assert.That(Call("CurrentAttackInterval",near),Is.EqualTo(1.25f).Within(.001f));
        Assert.That(Status(near).Has(CombatStatusKind.Knockdown),Is.False);
        Status(near).DebuffImmune=true;
        Assert.That(Status(near).Has(CombatStatusKind.AttackSpeedDown),Is.False);
        Assert.That(Call("CurrentAttackInterval",near),Is.EqualTo(1f).Within(.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ImmunityBlocksItsStatusButKeepsRoarDamage(bool ending)
    {
        Status(near).DebuffImmune=true;Status(far).ControlImmune=true;
        Wave(3f,ending);
        Assert.That(Hp(near),Is.EqualTo(ending?4780:4820));
        Assert.That(Status(near).HarmfulCount,Is.EqualTo(0));
        Assert.That(Status(far).Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(Status(far).Has(CombatStatusKind.AttackSpeedDown),Is.EqualTo(!ending));
    }

    [Test]
    public void FinalRoarKnocksDownInPlaceAndNeverAppliesOpeningSlow()
    {
        Wave(3f,true);
        Assert.That(Hp(near),Is.EqualTo(4780));
        Assert.That(Status(near).Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(.8f));
        Assert.That(Status(near).Has(CombatStatusKind.AttackSpeedDown),Is.False);
        Assert.That(Get(near,"displacing"),Is.False);
    }

    [Test]
    public void BasicAttackKeepsOrdinaryCleaveWithoutPiercingRoar()
    {
        Set(bear,"bearKingRoarActive",true);Set(bear,"bearResolveTimer",4f);
        Call("ResolveBasicAttackImpact",bear,near,100f);
        Assert.That(Hp(near),Is.EqualTo(4880));Assert.That(Hp(far),Is.EqualTo(5000));
        Assert.That(Status(far).HarmfulCount,Is.EqualTo(0));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReleasedExpandingWaveSurvivesDeathAndBattleResetCancelsIt(bool reset)
    {
        var wave=(IEnumerator)Call("AdvanceBearKingRoar",bear,Vector3.zero,3f,180f,false,null,0);
        Assert.That(wave.MoveNext(),Is.True);
        if(reset)battleType.GetField("combatGeneration",Private).SetValue(battle,1);
        else Set(bear,"alive",false);
        if(!reset)
        {
            Assert.That(wave.MoveNext(),Is.True);
            battleType.GetField("combatGeneration",Private).SetValue(battle,1);
        }
        float beforeReset=Hp(near);
        Assert.That(wave.MoveNext(),Is.False);Assert.That(Hp(near),Is.EqualTo(beforeReset));
    }
}
