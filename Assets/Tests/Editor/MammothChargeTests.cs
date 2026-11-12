using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private object MammothCall(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype)
        .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static).Invoke(battle,args);

    private object StartMammoth(string species)
    {
        var type=typeof(MonsterAutoBattlePrototype);
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(source,"skillId",MammothCombatRules.SkillId);Set(source,"maxHp",500f);Set(source,"defense",60f);
        Set(source,"mammothChargeStart",Vector3.zero);Set(source,"mammothChargeEnd",Vector3.right*5.95f);
        Set(source,"mammothChargeDamageMultiplier",1f);Set(source,"mammothChargeRadiusMultiplier",1f);
        MammothCall("StartMammothCharge",source,0);return Get(source,"mammothCharge");
    }

    [TestCase("kogason",21f,12.25f)] [TestCase("seolgason",120f,70f)]
    [TestCase("irontusk",25.2f,14.7f)] [TestCase("gaiason",16.5f,9.625f)]
    public void MammothHitsOnContactOnceThenOnlyLandingRadius(string species,float contact,float landing)
    {
        Model(target).transform.position=Vector3.right*2;
        var far=Unit(1,Vector3.right*5.8f);var outside=Unit(1,new Vector3(5.8f,0,4f));
        var ally=Unit(0,Vector3.right*2f);var charge=StartMammoth(species);
        MammothCall("AdvanceMammothCharge",source,charge,.34f);
        Assert.That(Hp(target),Is.EqualTo(5000));Assert.That(Model(source).transform.position.x,Is.Zero);
        MammothCall("AdvanceMammothCharge",source,charge,.6f);
        Assert.That(Hp(target),Is.EqualTo(5000-contact).Within(.01f),"Contact before arrival");
        Assert.That(Hp(far),Is.EqualTo(5000));
        MammothCall("AdvanceMammothCharge",source,charge,.65f);
        Assert.That(Hp(target),Is.EqualTo(5000-contact).Within(.01f),"No repeated path hit");
        MammothCall("AdvanceMammothCharge",source,charge,.95f);
        Assert.That(Hp(target),Is.EqualTo(5000-contact).Within(.01f),"Outside final blast");
        Assert.That(Hp(far),Is.EqualTo(5000-contact-landing).Within(.01f));
        Assert.That(Hp(outside),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        Assert.That(Model(source).transform.position.x,Is.EqualTo(5.95f).Within(.01f));
        MammothCall("EndMammothCharge",source,charge);
        Assert.That(Get(source,"mammothCharge"),Is.Null);
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).DebuffImmune,Is.False);
    }

    [Test]
    public void MammothLongFrameSweepsButSteelBonusAppliesOncePerVictim()
    {
        Model(target).transform.position=Vector3.right*5.8f;Trait(MammothCombatRules.IrontuskTraitId);
        var mid=Unit(1,Vector3.right*2f);var charge=StartMammoth("irontusk");
        MammothCall("AdvanceMammothCharge",source,charge,.95f);
        Assert.That(Hp(target),Is.EqualTo(5000-25.2f-14.7f-21f).Within(.01f));
        Assert.That(Hp(mid),Is.EqualTo(5000-25.2f-21f).Within(.01f));
        MammothCall("AdvanceMammothCharge",source,charge,1.1f);
        Assert.That(Hp(target),Is.EqualTo(5000-60.9f).Within(.01f));
    }

    [TestCase(false)] [TestCase(true)]
    public void MammothInterruptionReleasesMotionAndPreservesPriorImmunity(bool previouslyImmune)
    {
        var statuses=(CombatStatusState)Get(source,"combatStatuses");statuses.DebuffImmune=previouslyImmune;
        var charge=StartMammoth("kogason");MammothCall("AdvanceMammothCharge",source,charge,.4f);
        Assert.That(statuses.DebuffImmune,Is.True);
        Set(source,"positionRevision",1);MammothCall("AdvanceMammothCharge",source,charge,.95f);
        Assert.That(Get(source,"mammothCharge"),Is.Null);Assert.That(Hp(target),Is.EqualTo(5000));
        Assert.That(statuses.DebuffImmune,Is.EqualTo(previouslyImmune));
        Assert.That((float)Get(source,"attackMovementLockTimer"),Is.Zero);
    }

    [Test]
    public void AncestorShieldUsesCasterHealthRegardlessOfRecipientHealth()
    {
        StartMammoth("gaiason");Model(target).transform.position=Vector3.right*2f;
        var ally=Unit(0,Vector3.right*2f);Set(ally,"maxHp",10000f);
        cast=(IEnumerator)MammothCall("ResolveMammothAncestorPath",source,Vector3.zero,Vector3.right*6f,0);
        Assert.That(cast.MoveNext(),Is.True);
        Assert.That(((CombatStatusState)Get(ally,"combatStatuses")).AbsorbShield(100f),Is.EqualTo(40f).Within(.01f));
    }

    [TestCase("057_kogason")] [TestCase("058_seolgason")]
    [TestCase("059_irontusk")] [TestCase("060_gaiason")]
    public void MammothAnimationFreezesAndCompletesWithoutDuplicateCallback(string prefab)
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        float time=0;int completed=0;
        driver.PlayMammothCharge(()=>true,t=>time=t,()=>completed++);
        advance.Invoke(driver,new object[]{.3f});Assert.That(time,Is.EqualTo(.3f).Within(.001f));
        driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});Assert.That(time,Is.EqualTo(.3f).Within(.001f));
        driver.SetFrozen(false);advance.Invoke(driver,new object[]{.66f});
        Assert.That(completed,Is.Zero);Assert.That(time,Is.EqualTo(.96f).Within(.001f));
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo((int)typeof(SpriteMonsterAnimationDriver).GetField("skillAttackHitFrameIndex",Private).GetValue(driver)));
        advance.Invoke(driver,new object[]{.3f});Assert.That(completed,Is.EqualTo(1));
        driver.PlayDeath();Assert.That(completed,Is.EqualTo(1));
    }
}
