using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SabertoothSilenceTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object source,target;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Throat Lock Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        var runField=battleType.GetField("expeditionRun",Private);
        runField.SetValue(battle,Activator.CreateInstance(runField.FieldType));
        battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(Vector3.zero,Vector3.one*100f));
        source=CreateUnit(0,true);target=CreateUnit(1,false);
        Set(source,"target",target);Set(target,"target",source);
    }

    [TearDown]
    public void TearDown()
    {
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
               created.transform.parent==null)Object.DestroyImmediate(created);
    }

    private object CreateUnit(int side,bool trait)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Throat Lock Unit");model.transform.position=Vector3.right*side;
        Set(unit,"model",model);Set(unit,"ring",new GameObject("Throat Lock Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,side));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi"));
        Set(unit,"stableId",Guid.NewGuid().ToString());
        Set(unit,"skillId",SabertoothCombatRules.SkillId);Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);
        if(trait)((List<string>)Get(unit,"activeTraitIds")).Add(SabertoothCombatRules.ThroatLockTraitId);
        Statuses(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);
        ((IList)battleType.GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }

    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private CombatStatusState Statuses(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
    private IEnumerator Bite(object caster,object victim)
    {
        Set(caster,"fangLockTarget",victim);
        return (IEnumerator)Call("ResolveFangLock",caster,
            GameMasterData.Skill(SabertoothCombatRules.SkillId),
            battleType.GetField("combatGeneration",Private).GetValue(battle));
    }

    [Test]
    public void FirstImpactSilencesThroughThreeHitsThenExactlyOnePointFiveSeconds()
    {
        var routine=Bite(source,target);
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
        Assert.That(routine.MoveNext(),Is.True);
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.True);
        Assert.That(Call("TryUseSkill",target,false),Is.False);
        Assert.That(Call("TryUseSkill",target,true),Is.False,"Combined casts cannot bypass silence.");
        float before=(float)Get(source,"hp");
        Call("ResolveBasicAttackImpact",target,source,100f);
        Assert.That((float)Get(source,"hp"),Is.LessThan(before),"Basic attacks still land.");
        Statuses(target).Tick(0.6f);
        // Applying silence to the caster must not cancel a skill already in progress.
        Statuses(source).ApplyTimed(CombatStatusKind.Silence,3f);
        Assert.That(routine.MoveNext(),Is.True);
        Statuses(target).Tick(0.6f);
        Assert.That(routine.MoveNext(),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(5000f-180f-1000f).Within(0.01f));
        Assert.That(Statuses(target).Has(CombatStatusKind.Root),Is.False);
        Assert.That((float)Call("CurrentAttackInterval",target),Is.EqualTo(1f).Within(0.001f));
        Assert.That(Statuses(target).Get(CombatStatusKind.Silence).remaining,Is.EqualTo(1.5f));
        Assert.That(Statuses(target).Get(CombatStatusKind.Silence).activeHolds,Is.Zero);
        Statuses(target).Tick(1.49f,null,true); // Capture's non-control freeze must not extend silence.
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.True);
        Statuses(target).Tick(0.011f);
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
        Assert.That(Get(target,"skillTimer"),Is.EqualTo(0f),"Silence does not add cooldown.");
    }

    [Test]
    public void OverlappingCastersWaitForLastReleaseWithoutStackingExtraTail()
    {
        object second=CreateUnit(0,true);
        var firstBite=Bite(source,target);var secondBite=Bite(second,target);
        firstBite.MoveNext();secondBite.MoveNext();
        var silence=Statuses(target).Get(CombatStatusKind.Silence);
        Assert.That(silence.activeHolds,Is.EqualTo(2));
        Call("FinishFangLock",source,false);
        Statuses(target).Tick(5f);
        Assert.That(silence.activeHolds,Is.EqualTo(1));
        Assert.That(silence.remaining,Is.EqualTo(1.5f));
        Call("FinishFangLock",second,false);Call("FinishFangLock",second,false);
        Assert.That(silence.activeHolds,Is.Zero,"Repeated cleanup cannot release another caster's hold.");
        Statuses(target).Tick(1.5f);
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
    }

    [TestCase("cleanse")]
    [TestCase("immunity")]
    [TestCase("clear")]
    public void RemovedSilenceDoesNotResurrectWhenBiteFinishes(string ending)
    {
        var routine=Bite(source,target);routine.MoveNext();
        if(ending=="cleanse")Statuses(target).RemoveAllHarmful();
        else if(ending=="immunity")Statuses(target).DebuffImmune=true;
        else Statuses(target).Clear();
        routine.MoveNext();routine.MoveNext();
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
        Assert.That(Get(source,"fangLockSilence"),Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ImmunityPreventsSilenceAndFalseActivation(bool control)
    {
        if(control)Statuses(target).ControlImmune=true;
        else Statuses(target).DebuffImmune=true;
        var routine=Bite(source,target);routine.MoveNext();
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
        Assert.That(Get(source,"fangLockSilence"),Is.Null);
        Assert.That(Get(source,"traitActivations"),Is.EqualTo(0));
    }

    [Test]
    public void QueuedEchoWaitsDuringSilenceAndExecutesAfterItExpires()
    {
        Set(target,"skillEchoId",SabertoothCombatRules.SkillId);
        Set(target,"skillEchoTarget",source);Set(target,"rank3EchoScale",0.65f);
        Statuses(target).ApplyTimed(CombatStatusKind.Silence,1.5f);
        Call("ExecuteSkillEcho",target);
        Assert.That(Get(target,"skillHits"),Is.EqualTo(0));
        Assert.That(Get(target,"skillEchoId"),Is.EqualTo(SabertoothCombatRules.SkillId));
        Assert.That(Get(target,"rank3EchoScale"),Is.EqualTo(0.65f));
        Assert.That((float)Get(target,"skillEchoTimer"),Is.GreaterThan(0f));
        Statuses(target).Tick(1.5f);Call("ExecuteSkillEcho",target);
        Assert.That(Get(target,"skillHits"),Is.EqualTo(1));
        Assert.That(Get(target,"skillEchoId"),Is.Empty);
    }

    [Test]
    public void CasterDeathReleasesSilenceImmediatelyEvenBetweenBiteHits()
    {
        var routine=Bite(source,target);routine.MoveNext();
        Call("ApplyDamage",target,source,10000f,false,false,false,false,0f,0f,1f,false,false,false);
        Assert.That(Get(source,"alive"),Is.False);
        Assert.That(Get(source,"fangLockSilence"),Is.Null);
        Assert.That(Statuses(target).Get(CombatStatusKind.Silence).activeHolds,Is.Zero);
        Statuses(target).Tick(1.5f);
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
        Assert.That(routine.MoveNext(),Is.False);
    }

    [Test]
    public void CleanseAndReapplyCreateANewHoldThatOldCasterCannotRelease()
    {
        var first=Bite(source,target);first.MoveNext();
        Statuses(target).RemoveAllHarmful();
        object second=CreateUnit(0,true);
        var next=Bite(second,target);next.MoveNext();
        Call("FinishFangLock",source,false);
        var silence=Statuses(target).Get(CombatStatusKind.Silence);
        Assert.That(silence.activeHolds,Is.EqualTo(1));
        Statuses(target).Tick(3f);
        Assert.That(silence.remaining,Is.EqualTo(1.5f));
        Call("FinishFangLock",second,false);
        Statuses(target).Tick(1.5f);
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
    }

    [Test]
    public void EchoHoldTracksActualRootThenLeavesTheSameTail()
    {
        Statuses(target).ApplyTimed(CombatStatusKind.Root,0.78f,source,0.3f);
        var silence=(CombatStatusEffect)Call("ApplyThroatLockSilence",source,target);
        var hold=(IEnumerator)Call("HoldEchoThroatLockSilence",target,silence);
        Assert.That(hold.MoveNext(),Is.True);
        Statuses(target).Tick(0.78f);Assert.That(hold.MoveNext(),Is.False);
        Assert.That(silence.remaining,Is.EqualTo(1.5f));
        Statuses(target).Tick(1.5f);
        Assert.That(Statuses(target).Has(CombatStatusKind.Silence),Is.False);
    }

    [Test]
    public void SavedTraitIdSelectsRenewedThroatLockWithDistinctStatusIcon()
    {
        var monster=Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi");
        var row=MonsterTraitCatalog.TestSelectable(monster,true)
            .Single(trait=>trait.id=="trait_sabertooth_deep_fang_mark");
        Assert.That(row.displayName,Is.EqualTo("숨통 끊기"));
        Assert.That(row.category,Is.EqualTo("SkillEvolution"));
        Assert.That(GameMasterData.Database.traits.Single(trait=>trait.id==row.id).effectSummary,
            Does.Contain("1.5초").And.Contain("침묵"));
        Assert.That(PixelCombatEffect.SilenceIcon.width,Is.EqualTo(64));
        Assert.That(PixelCombatEffect.SilenceIcon.filterMode,Is.EqualTo(FilterMode.Bilinear));
        Assert.That(PixelCombatEffect.SilenceIcon,Is.Not.SameAs(PixelCombatEffect.FangLockRootIcon));
        var database=JsonUtility.FromJson<LocalizationDatabaseFile>(
            Resources.Load<TextAsset>(GameLocalization.ResourcePath).text);
        var text=database.entries.Single(entry=>entry.key==SabertoothCombatRules.ThroatLockTraitId+".concise");
        Assert.That(text.ko,Does.Contain("1.5초간 침묵"));
        Assert.That(text.en,Does.Contain("1.5s").And.Contain("basic attacks"));
    }
}
