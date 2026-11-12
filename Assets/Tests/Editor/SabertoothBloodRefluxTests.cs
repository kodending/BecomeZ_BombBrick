using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SabertoothBloodRefluxTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object holder,prey,otherEnemy;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Blood Reflux Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=battleType.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(Vector3.zero,Vector3.one*100f));
        holder=CreateUnit(0,true);prey=CreateUnit(1,false);otherEnemy=CreateUnit(1,false);
        Set(holder,"target",prey);Set(prey,"target",holder);Set(otherEnemy,"target",holder);
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
        var units=(IList)battleType.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Blood Reflux Unit");model.transform.position=Vector3.right*side;
        Set(unit,"id",units.Count);Set(unit,"model",model);
        Set(unit,"ring",new GameObject("Blood Reflux Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,side));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",SabertoothCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);
        if(trait)Traits(unit).Add(SabertoothCombatRules.BloodRefluxTraitId);
        Statuses(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);
        units.Add(unit);return unit;
    }

    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private List<string> Traits(object unit)=>(List<string>)Get(unit,"activeTraitIds");
    private CombatStatusState Statuses(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
    private float Hit(object attacker,object target,float damage,bool reactive=true,bool skill=false)=>
        (float)Call("ApplyDamage",attacker,target,damage,skill,!skill, false,reactive,0f,0f,1f,false,false,false);
    private IEnumerator Bite(object caster,object victim)
    {
        Set(caster,"fangLockTarget",victim);
        var routine=(IEnumerator)Call("ResolveFangLock",caster,
            GameMasterData.Skill(SabertoothCombatRules.SkillId),
            battleType.GetField("combatGeneration",Private).GetValue(battle));
        Assert.That(routine.MoveNext(),Is.True);return routine;
    }

    [TestCase(false,true,false)]
    [TestCase(true,true,false)]
    [TestCase(true,true,true)]
    [TestCase(true,false,true)]
    public void EnemyHitsReturnFortyPercentOfMitigatedHpLossToHeldEnemy(
        bool thirdParty,bool reactive,bool skill)
    {
        Bite(holder,prey);Set(holder,"defense",100f);Set(prey,"defense",10000f);
        float before=Hp(prey);
        float lost=Hit(thirdParty?otherEnemy:prey,holder,100f,reactive,skill);
        Assert.That(lost,Is.EqualTo(50f).Within(0.001f));
        Assert.That(before-Hp(prey),Is.EqualTo(20f).Within(0.001f),"Prey's armor cannot mitigate true damage.");
        Assert.That(Get(holder,"skillHits"),Is.EqualTo(1),"Reflection is not another bite hit.");
    }

    [TestCase(60f,16f)]
    [TestCase(100f,0f)]
    [TestCase(150f,0f)]
    public void ShieldAbsorptionIsExcluded(float shield,float reflected)
    {
        Bite(holder,prey);Statuses(holder).ApplyShield(5f,shield,holder);
        float before=Hp(prey);Hit(otherEnemy,holder,100f);
        Assert.That(before-Hp(prey),Is.EqualTo(reflected).Within(0.001f));
    }

    [Test]
    public void RefluxIsNotAmplifiedAgainByOvertimeOrSourceBonuses()
    {
        Bite(holder,prey);
        battleType.GetField("battleElapsed",Private).SetValue(battle,300f);
        Set(holder,"rank3AttackBonusTimer",5f);Set(holder,"rank3AttackBonusRatio",2f);
        Set(holder,"lastSkillCritical",true);
        float before=Hp(prey);float lost=Hit(otherEnemy,holder,100f);
        Assert.That(before-Hp(prey),Is.EqualTo(lost*0.4f).Within(0.001f));
    }

    [TestCase("windup")]
    [TestCase("released")]
    [TestCase("no_trait")]
    [TestCase("dead_prey")]
    [TestCase("lethal_hit")]
    [TestCase("ally")]
    public void RefluxOnlyRunsDuringLiveGripForEnemyDamage(string condition)
    {
        if(condition=="no_trait")Traits(holder).Clear();
        if(condition=="windup")Set(holder,"fangLockTarget",prey);
        else Bite(holder,prey);
        if(condition=="released")Call("FinishFangLock",holder,false);
        if(condition=="dead_prey"){Set(prey,"hp",0f);Set(prey,"alive",false);}
        if(condition=="lethal_hit")Set(holder,"hp",50f);
        object attacker=condition=="ally"?CreateUnit(0,false):otherEnemy;
        float before=Hp(prey);Hit(attacker,holder,100f);
        Assert.That(Hp(prey),Is.EqualTo(before).Within(0.001f));
    }

    [Test]
    public void TwoBloodRefluxHoldersAndExistingReflectionCannotLoop()
    {
        Traits(prey).Add(SabertoothCombatRules.BloodRefluxTraitId);
        Bite(holder,prey);Bite(prey,holder);Set(prey,"corruptReflectionRatio",1f);
        float holderBefore=Hp(holder),preyBefore=Hp(prey);
        Hit(otherEnemy,holder,100f);
        Assert.That(holderBefore-Hp(holder),Is.EqualTo(100f).Within(0.001f));
        Assert.That(preyBefore-Hp(prey),Is.EqualTo(40f).Within(0.001f));
        Assert.That(battleType.GetField("reflectedDamageDepth",Private).GetValue(battle),Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IncomingExistingReflectionOrDamageLinkCannotStartReflux(bool link)
    {
        Bite(holder,prey);
        if(link){Set(otherEnemy,"corruptDamageLinkTarget",holder);Set(otherEnemy,"corruptDamageLinkRatio",0.5f);}
        else Set(otherEnemy,"corruptReflectionRatio",0.5f);
        float before=Hp(prey),holderBefore=Hp(holder);Hit(holder,otherEnemy,100f);
        Assert.That(holderBefore-Hp(holder),Is.EqualTo(50f).Within(0.001f));
        Assert.That(Hp(prey),Is.EqualTo(before));
        Hit(otherEnemy,holder,100f);
        Assert.That(before-Hp(prey),Is.EqualTo(40f).Within(0.001f),"Normal hits still work after reflection finishes.");
    }

    [Test]
    public void ThreeBitesAndGeomchiRecoveryRemainUnchangedAndReleaseStopsReflux()
    {
        Traits(holder).Add(SabertoothCombatRules.GeomchiTraitId);
        var routine=Bite(holder,prey);Hit(otherEnemy,holder,100f);
        routine.MoveNext();Assert.That(routine.MoveNext(),Is.False);
        Assert.That(Hp(holder),Is.EqualTo(4935f).Within(0.001f));
        Assert.That(Hp(prey),Is.EqualTo(5000f-180f-1000f-40f).Within(0.01f));
        Assert.That(Get(holder,"skillHits"),Is.EqualTo(3));
        Assert.That(Get(holder,"fangLockBloodReflux"),Is.False);
        float before=Hp(prey);Hit(otherEnemy,holder,100f);Assert.That(Hp(prey),Is.EqualTo(before));
    }

    [Test]
    public void ExistingJawPulsesRedAndPoolReuseClearsTint()
    {
        Bite(holder,prey);var effect=(PixelCombatEffect)Get(prey,"fangLockDebuffEffect");
        Assert.That(effect,Is.Not.Null);Hit(otherEnemy,holder,100f);
        Assert.That(effect.GetComponent<SpriteRenderer>().color,Is.EqualTo(new Color(1f,0.16f,0.24f)));
        Statuses(prey).Clear();typeof(PixelCombatEffect).GetMethod("Update",Private).Invoke(effect,null);
        Assert.That(effect.gameObject.activeSelf,Is.False);
        var reused=PixelCombatEffect.PlaySabertoothFangLock(((GameObject)Get(prey,"model")).transform,null,false);
        Assert.That(reused.GetComponent<SpriteRenderer>().color,Is.EqualTo(Color.white));
    }

    [Test]
    public void ReflectionIconFollowsGripAndSharedSourcesUseOneSlot()
    {
        Func<int> count=()=>
        {
            Call("CollectCombatHudIcons",holder);
            return ((IList)battleType.GetField("combatHudIcons",Private).GetValue(battle))
                .Cast<object>().Count(entry=>Equals(entry.GetType().GetField("effect").GetValue(entry),
                    CombatEffectIconKind.Reflection));
        };
        Set(holder,"fangLockTarget",prey);Assert.That(count(),Is.Zero);
        Bite(holder,prey);Assert.That(count(),Is.EqualTo(1));
        Set(holder,"corruptReflectionRatio",0.5f);Assert.That(count(),Is.EqualTo(1));
        Call("FinishFangLock",holder,false);Assert.That(count(),Is.EqualTo(1));
        Set(holder,"corruptReflectionRatio",0f);Assert.That(count(),Is.Zero);
    }

    [Test]
    public void SavedThirdTraitSelectsBloodRefluxAndDescriptionsMatch()
    {
        var row=MonsterTraitCatalog.TestSelectable(Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi"),true)
            .Single(trait=>trait.id==SabertoothCombatRules.BloodRefluxTraitId);
        Assert.That(row.displayName,Is.EqualTo("피의 역류"));
        Assert.That(row.category,Is.EqualTo("SkillEvolution"));
        var data=GameMasterData.Database.traits.Single(trait=>trait.id==row.id);
        Assert.That(data.effectSummary,Does.Contain("40%").And.Contain("고정 피해").And.Contain("보호막"));
        var localization=JsonUtility.FromJson<LocalizationDatabaseFile>(Resources.Load<TextAsset>(GameLocalization.ResourcePath).text);
        Assert.That(localization.entries.Single(entry=>entry.key==row.id+".concise").ko,
            Does.Contain("40%").And.Contain("반사 피해 제외"));
    }
}
