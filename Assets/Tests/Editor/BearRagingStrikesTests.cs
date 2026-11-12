using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class BearRagingStrikesTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object bear,prey,nearby;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Raging Strikes Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=battleType.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        battleType.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        bear=CreateUnit(0,Vector3.zero);prey=CreateUnit(1,Vector3.right*.7f);
        nearby=CreateUnit(1,new Vector3(.7f,0,.5f));
        Set(bear,"bearResolveTimer",4f);Set(bear,"bearResolveTotalDuration",4f);
        Set(bear,"target",prey);Traits(bear).Add(BearCombatRules.RagingHundredStrikesTraitId);
    }

    [TearDown]
    public void TearDown()
    {
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
               created.transform.parent==null)Object.DestroyImmediate(created);
    }

    private object CreateUnit(int side,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var units=(IList)battleType.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Raging Strikes Unit");model.transform.position=position;
        Set(unit,"id",units.Count);Set(unit,"model",model);Set(unit,"ring",new GameObject("Test Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,side));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_berber"));
        Set(unit,"stableId","raging-test-"+units.Count);Set(unit,"skillId",BearCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"collisionRadius",.4f);
        Set(unit,"skillCooldownDuration",5f);Set(unit,"skillTimer",3f);
        units.Add(unit);return unit;
    }

    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private List<string> Traits(object unit)=>(List<string>)Get(unit,"activeTraitIds");
    private object Strike(object target,int strike,int generation=0)=>battleType.GetMethod("ResolveBearRagingStrike",Private)
        .Invoke(battle,new[]{bear,target,(object)100f,false,strike,generation});

    [Test]
    public void ThreeContactsDealSeventySeventyOneFiftyAndSplashOnlyOnFinisher()
    {
        float timer=3f;
        for(int hit=0;hit<3;hit++)
        {
            Assert.That(Strike(prey,hit),Is.SameAs(prey));
            Assert.That(5000f-Hp(prey),Is.EqualTo(hit==0?84f:hit==1?168f:348f).Within(.001f));
            Assert.That(5000f-Hp(nearby),Is.EqualTo(hit==2?36f:0f).Within(.001f));
            Assert.That(Get(prey,"giraffeStunTimer"),Is.EqualTo(hit==2?.2f:0f));
            Assert.That(Get(nearby,"giraffeStunTimer"),Is.EqualTo(0f));
            if(hit==0)timer=(float)Get(bear,"skillTimer");
            else Assert.That(Get(bear,"skillTimer"),Is.EqualTo(timer),"Cooldown credit is once per action.");
        }
    }

    [TestCase(BearCombatRules.WoongiTraitId)]
    [TestCase(BearCombatRules.BerberTraitId)]
    [TestCase(BearCombatRules.IronKumaTraitId)]
    [TestCase(BearCombatRules.DamiTraitId)]
    public void SpeciesBenefitsAreNotRepeatedForFollowups(string trait)
    {
        Traits(bear).Add(trait);Set(bear,"hp",4000f);Set(bear,"defense",20f);
        Strike(prey,0);
        float hp=Hp(bear),timer=(float)Get(bear,"bearResolveTimer");
        int stacks=(int)Get(bear,"bearFrenzyStacks");
        float remaining=Hp(prey);
        Strike(prey,1);Strike(prey,2);
        Assert.That(Hp(bear),Is.EqualTo(hp));
        Assert.That(Get(bear,"bearResolveTimer"),Is.EqualTo(timer));
        Assert.That(Get(bear,"bearFrenzyStacks"),Is.EqualTo(stacks));
        Assert.That(remaining-Hp(prey),Is.EqualTo(264f).Within(.001f),"Iron Kuma's extra damage never repeats.");
        if(trait==BearCombatRules.WoongiTraitId){Assert.That(hp,Is.EqualTo(4050f));Assert.That(timer,Is.EqualTo(4.25f));}
        if(trait==BearCombatRules.BerberTraitId)Assert.That(stacks,Is.EqualTo(1));
        Assert.That(((CombatStatusState)Get(bear,"combatStatuses")).Has(CombatStatusKind.Shield),Is.False,
            "Dami gains shields on skill use, never on combo contacts.");
    }

    [Test]
    public void LethalContactsTransferOnlyRemainingStrikesToNearestMeleeEnemy()
    {
        Set(prey,"hp",50f);Set(nearby,"hp",50f);
        var last=CreateUnit(1,new Vector3(-1f,0,0));
        Strike(prey,0);Assert.That(Get(prey,"alive"),Is.False);
        var next=Strike(prey,1);Assert.That(next,Is.SameAs(nearby));
        Assert.That(Get(nearby,"alive"),Is.False);
        Assert.That(Strike(next,2),Is.SameAs(last));
        Assert.That(5000f-Hp(last),Is.EqualTo(180f).Within(.001f));
        Assert.That(Get(bear,"target"),Is.SameAs(last));
    }

    [TestCase("range")]
    [TestCase("ally")]
    [TestCase("untargetable")]
    public void CannotCarryFollowupsOutsideValidMeleeTargets(string condition)
    {
        Set(prey,"alive",false);
        if(condition=="range")((GameObject)Get(nearby,"model")).transform.position=Vector3.right*10f;
        if(condition=="ally")Set(nearby,"team",Get(bear,"team"));
        if(condition=="untargetable")Set(nearby,"corruptUntargetable",true);
        Assert.That(Strike(prey,1),Is.Null);Assert.That(Hp(nearby),Is.EqualTo(5000f));
    }

    [TestCase("death")]
    [TestCase("stagger")]
    [TestCase("displacement")]
    [TestCase("reset")]
    public void InterruptedAttackerCannotApplyFollowupDamage(string condition)
    {
        Strike(prey,0);float hp=Hp(prey);
        if(condition=="death")Set(bear,"alive",false);
        if(condition=="stagger")Set(bear,"giraffeStunTimer",.2f);
        if(condition=="displacement")Set(bear,"displacing",true);
        Assert.That(Strike(prey,1,condition=="reset"?1:0),Is.Null);
        Assert.That(Hp(prey),Is.EqualTo(hp));
    }

    private static void Step(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }

    [TestCase("woongi",1f)]
    [TestCase("dami",2f)]
    [TestCase("ironkuma",3f)]
    [TestCase("berber",1.5f)]
    public void BodyTimelineOwnsThreeContactsAndPausesOnFreeze(string id,float speed)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        var body=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        driver.ResetForReuse();Step(driver,0f);var neutralPosition=driver.transform.localPosition;
        var contacts=new List<int>();
        driver.PlayBearRagingAttack(speed,i=>{contacts.Add(i);return true;});
        Step(driver,driver.BasicAttackHitTime/speed+.0001f);
        Assert.That(contacts,Is.EqualTo(new[]{0}));int first=driver.CurrentFrameIndex;
        Step(driver,.05f);Assert.That(driver.CurrentFrameIndex,Is.Not.EqualTo(first),"Body returns through the existing arm frames.");
        Step(driver,.0301f);Assert.That(contacts,Is.EqualTo(new[]{0,1}));
        driver.SetFrozen(true);var frozen=driver.GetComponent<SpriteRenderer>().sprite;
        Step(driver,2f);Assert.That(contacts.Count,Is.EqualTo(2));
        Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frozen));
        driver.SetFrozen(false);Step(driver,.159f);Assert.That(contacts.Count,Is.EqualTo(2));
        Step(driver,.0011f);Assert.That(contacts,Is.EqualTo(new[]{0,1,2}));
        Step(driver,.2f);Assert.That(driver.IsActionLocked,Is.False);
        Assert.That(driver.transform.localPosition,Is.EqualTo(neutralPosition));
        Assert.That(driver.transform.localScale,Is.EqualTo(Vector3.one));
        driver.PlayBearRagingAttack(speed,i=>{contacts.Add(i);return true;});
        Step(driver,driver.BasicAttackHitTime/speed+.001f);driver.PlayDeath();Step(driver,1f);
        Assert.That(contacts.Count,Is.EqualTo(4),"Death cancels the remaining contacts.");
    }
}
