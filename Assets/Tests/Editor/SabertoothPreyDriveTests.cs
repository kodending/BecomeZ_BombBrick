using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SabertoothPreyDriveTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object source,target;
    private int nextId;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Prey Drive Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=battleType.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(Vector3.zero,Vector3.one*100f));
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*2f);
        Set(source,"target",target);Set(target,"target",source);
        ((List<string>)Get(source,"activeTraitIds")).Add(SabertoothCombatRules.PreyDriveTraitId);
    }

    [TearDown]
    public void TearDown()
    {
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
               created.transform.parent==null)Object.DestroyImmediate(created);
    }

    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Prey Unit");model.transform.position=position;
        Set(unit,"id",nextId++);Set(unit,"model",model);Set(unit,"ring",new GameObject("Prey Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",SabertoothCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",1);
        Set(unit,"collisionRadius",0.3f);
        ((IList)battleType.GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private Vector3 Position(object unit)=>((GameObject)Get(unit,"model")).transform.position;
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
    private IEnumerator Bite()
    {
        Set(source,"fangLockTarget",target);
        return (IEnumerator)Call("ResolveFangLock",source,
            GameMasterData.Skill(SabertoothCombatRules.SkillId),
            battleType.GetField("combatGeneration",Private).GetValue(battle));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void FirstHitDragsBothBodiesTowardTheirOwnSideAndPreservesThreeHitDamage(int team)
    {
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        var routine=Bite();Assert.That(routine.MoveNext(),Is.True);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That(Get(source,"preyFocusTarget"),Is.Null,"No recruitment before the pull ends.");
        Call("UpdatePreyDrag",source,0.5f);
        Assert.That(Position(source).x,Is.EqualTo(team==0?-1.275f:1.275f).Within(0.001f));
        Assert.That(Get(source,"preyFocusTarget"),Is.Null);
        Call("UpdatePreyDrag",source,0.5f);
        Assert.That(Position(source).x,Is.EqualTo(team==0?-2.55f:2.55f).Within(0.001f));
        Assert.That(Position(target).x-Position(source).x,Is.EqualTo(2f).Within(0.001f));
        Assert.That(Get(source,"preyFocusTarget"),Is.SameAs(target));
        Assert.That(Get(target,"preyDraggedBy"),Is.Null);
        Assert.That(routine.MoveNext(),Is.True);Assert.That(routine.MoveNext(),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(3820f).Within(0.01f));
        Assert.That(Status(source).ControlImmune,Is.False);
    }

    [TestCase("unit")]
    [TestCase("edge")]
    public void ObstructionStopsBothBodiesAndStillRallies(string obstacle)
    {
        if(obstacle=="unit")Unit(0,Vector3.left*1.1f);
        else battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(new Vector3(4.5f,0,0),new Vector3(10,10,10)));
        var routine=Bite();routine.MoveNext();Call("UpdatePreyDrag",source,1f);
        Assert.That(Position(source).x,Is.GreaterThan(-0.6f));
        Assert.That(Position(target).x-Position(source).x,Is.EqualTo(2f).Within(0.001f));
        Assert.That(Get(source,"preyFocusTarget"),Is.SameAs(target));
        Assert.That(Get(target,"preyDraggedBy"),Is.Null);
    }

    [Test]
    public void DisplacementImmunityPreventsMotionButNotRally()
    {
        Status(target).ControlImmune=true;
        var routine=Bite();routine.MoveNext();Call("UpdatePreyDrag",source,1f);
        Assert.That(Position(source),Is.EqualTo(Vector3.zero));
        Assert.That(Position(target),Is.EqualTo(Vector3.right*2f));
        Assert.That(Get(source,"preyFocusTarget"),Is.SameAs(target));
    }

    [Test]
    public void RallyRecruitsOnceWithinRadiusAndDoesNotInterruptOngoingAttackOrTaunt()
    {
        object otherEnemy=Unit(1,new Vector3(6,0,4));
        object nearby=Unit(0,new Vector3(2,0,3));
        object busy=Unit(0,new Vector3(3,0,3));
        object taunted=Unit(0,new Vector3(4,0,1));
        object far=Unit(0,new Vector3(2,0,5.11f));
        foreach(var ally in new[]{nearby,busy,taunted,far})Set(ally,"target",otherEnemy);
        Set(busy,"attackMovementLockTimer",0.5f);
        Set(taunted,"forcedTarget",otherEnemy);Set(taunted,"forcedTargetTimer",1f);
        Call("RallyPreyAttack",source,target);
        Assert.That(Get(nearby,"target"),Is.SameAs(target));
        Assert.That(Get(busy,"target"),Is.SameAs(otherEnemy));
        Assert.That(Get(busy,"preyFocusTarget"),Is.SameAs(target));
        Assert.That(Get(taunted,"preyFocusTarget"),Is.Null);
        Assert.That(Get(far,"preyFocusTarget"),Is.Null);
        Assert.That(Get(otherEnemy,"preyFocusTarget"),Is.Null);
        Set(busy,"attackMovementLockTimer",0f);Call("ApplyPreyFocus",busy);
        Assert.That(Get(busy,"target"),Is.SameAs(target));
        ((GameObject)Get(far,"model")).transform.position=Vector3.one;
        Call("ApplyPreyFocus",far);
        Assert.That(Get(far,"target"),Is.SameAs(otherEnemy),"Entering later does not recruit.");
        Assert.That(((CombatEffectIconState)Get(target,"combatEffectIcons")).Has(
            CombatEffectIconKind.Hunted),Is.True);
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void GradualPullStartsAndStopsSoftlyAndKeepsDistanceAcrossFrameRates(int fps)
    {
        var routine=Bite();routine.MoveNext();
        float firstQuarter=0f,middle=0f,lastQuarter=0f;
        for(int frame=1;frame<=fps;frame++)
        {
            Call("UpdatePreyDrag",source,1f/fps);
            if(frame==fps/4)firstQuarter=Mathf.Abs(Position(source).x);
            if(frame==fps/2)middle=Mathf.Abs(Position(source).x);
            if(frame==fps*3/4)lastQuarter=Mathf.Abs(Position(source).x);
            if(frame<fps)Assert.That(Get(source,"preyFocusTarget"),Is.Null);
        }
        // Floating-point accumulation can finish on the next update.
        if(Get(source,"preyDragTarget")!=null)Call("UpdatePreyDrag",source,0.0001f);
        float total=Mathf.Abs(Position(source).x);
        Assert.That(firstQuarter,Is.LessThan(middle-firstQuarter),"Ease into the pull.");
        Assert.That(total-lastQuarter,Is.LessThan(lastQuarter-middle),"Ease out of the pull.");
        Assert.That(total,Is.EqualTo(2.55f).Within(0.001f));
        Assert.That(Position(target).x-Position(source).x,Is.EqualTo(2f).Within(0.001f));
        Assert.That(Get(source,"preyFocusTarget"),Is.SameAs(target));
    }

    [Test]
    public void FirstRallyWinsAndTauntTemporarilyOverridesFocus()
    {
        object ally=Unit(0,new Vector3(1,0,2));
        object secondTarget=Unit(1,new Vector3(2,0,2));
        Call("RallyPreyAttack",source,target);
        Call("RallyPreyAttack",source,secondTarget);
        Assert.That(Get(ally,"preyFocusTarget"),Is.SameAs(target));
        Call("SetTarget",ally,secondTarget);
        Assert.That(Get(ally,"target"),Is.SameAs(target));
        Set(ally,"forcedTarget",secondTarget);Set(ally,"forcedTargetTimer",1f);
        Call("SetTarget",ally,secondTarget);Call("ApplyPreyFocus",ally);
        Assert.That(Get(ally,"target"),Is.SameAs(secondTarget));
        Set(ally,"forcedTargetTimer",0f);Call("ApplyPreyFocus",ally);
        Assert.That(Get(ally,"target"),Is.SameAs(target));
        Set(ally,"preyFocusTimer",0f);Call("ApplyPreyFocus",ally);
        Assert.That(Get(ally,"target"),Is.SameAs(target),"Expiry does not restore the old target.");
        Call("SetTarget",ally,secondTarget);
        Assert.That(Get(ally,"target"),Is.SameAs(secondTarget),"Ordinary targeting resumes.");
    }

    [TestCase("death")]
    [TestCase("interruption")]
    [TestCase("cleanup")]
    public void InterruptedPullReleasesBothBodiesWithoutRecruitment(string ending)
    {
        var routine=Bite();routine.MoveNext();Call("UpdatePreyDrag",source,0.1f);
        if(ending=="death")Set(target,"alive",false);
        if(ending=="interruption")Set(source,"positionRevision",99);
        if(ending=="cleanup")Call("FinishFangLock",source,false);
        else Call("UpdatePreyDrag",source,0.1f);
        Assert.That(Get(source,"preyDragTarget"),Is.Null);
        Assert.That(Get(target,"preyDraggedBy"),Is.Null);
        Assert.That(Get(source,"preyFocusTarget"),Is.Null);
    }

    [Test]
    public void DeadPreyDoesNotHoldAlliesAndTheSavedTraitRemainsSelectable()
    {
        object enemy=Unit(1,new Vector3(5,0,3));
        Call("RallyPreyAttack",source,target);Set(target,"alive",false);
        Call("SetTarget",source,enemy);
        Assert.That(Get(source,"target"),Is.SameAs(enemy));
        Assert.That(Call("HasPreyFocus",source),Is.False);
        var row=MonsterTraitCatalog.TestSelectable((MonsterDefinition)Get(source,"definition"),true)
            .Single(trait=>trait.id==SabertoothCombatRules.PreyDriveTraitId);
        Assert.That(row.displayName,Is.EqualTo("먹잇감 몰이"));
    }

    [Test]
    public void LongCastFinishesBeforeItsQueuedTargetChangeEvenAfterMarkExpires()
    {
        object ally=Unit(0,new Vector3(1,0,3));
        object oldTarget=Unit(1,new Vector3(4,0,4));
        Set(ally,"target",oldTarget);Set(ally,"attackMovementLockTimer",4f);
        Call("RallyPreyAttack",source,target);
        Set(ally,"preyFocusTimer",0f);Call("ApplyPreyFocus",ally);
        Assert.That(Get(ally,"target"),Is.SameAs(oldTarget));
        Set(ally,"attackMovementLockTimer",0f);Call("ApplyPreyFocus",ally);
        Assert.That(Get(ally,"target"),Is.SameAs(target));
        Assert.That(Call("HasPreyFocus",ally),Is.False,"The handoff must not extend the 2.5s buff.");
    }

    [Test]
    public void RaptorNextLeapFollowsRallyInsteadOfAnUnrelatedLowHealthEnemy()
    {
        object weak=Unit(1,new Vector3(3,0,3));Set(weak,"hp",1f);
        Call("RallyPreyAttack",source,target);
        Assert.That(Call("SelectRaptorLeapTarget",source,5f),Is.SameAs(target));
        Set(source,"forcedTarget",weak);Set(source,"forcedTargetTimer",1f);
        Assert.That(Call("SelectRaptorLeapTarget",source,5f),Is.SameAs(weak));
    }
}
