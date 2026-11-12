using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private object StartBrace(object caster,string species="kogason")
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        Set(caster,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(caster,"skillId",MammothCombatRules.SkillId);Set(caster,"maxHp",500f);Set(caster,"hp",500f);Set(caster,"defense",60f);
        Set(caster,"mammothChargeEnd",Model(caster).transform.position+Vector3.right*3.4f);
        ((IList)Get(caster,"activeTraitIds")).Add(MammothRank3Rules.MountainBrace);
        MammothCall("StartMammothBrace",caster,0);return Get(caster,"mammothBrace");
    }
    private float BraceDamage(object attacker,object victim,float damage,bool skill=false)=>
        (float)MammothCall("ApplyDamage",attacker,victim,damage,skill,false,false,false,0f,0f,1f,false,false,false);

    [Test] public void MountainBraceSharesResolvedDamageOnceAndKeepsDamageAttribution()
    {
        var brace=StartBrace(source);var ally=Unit(0,Vector3.right);Set(source,"defense",10000f);
        MammothCall("AdvanceMammothBrace",source,brace,.4f);
        Assert.That(BraceDamage(target,ally,100f,true),Is.EqualTo(70f).Within(.001f));
        Assert.That(Hp(source),Is.EqualTo(485f).Within(.001f),"Transfer is not reduced by armor a second time");
        Assert.That(Hp(ally),Is.EqualTo(4930f).Within(.001f));
        Assert.That((float)Get(target,"damageDealt"),Is.EqualTo(85f).Within(.001f));
        Assert.That((float)Get(target,"skillDamage"),Is.EqualTo(15f).Within(.001f),"Caller adds the original victim's damage separately");
        Assert.That((float)brace.GetType().GetField("received").GetValue(brace),Is.EqualTo(15f).Within(.001f));
    }
    [Test] public void MountainBraceTransfersOnlyWhatPassedAlliedShieldAndUsesOwnShield()
    {
        var brace=StartBrace(source);var ally=Unit(0,Vector3.right);
        Set(ally,"itemShield",80f);Set(source,"itemShield",10f);
        MammothCall("AdvanceMammothBrace",source,brace,.4f);
        Assert.That(BraceDamage(target,ally,100),Is.EqualTo(14f).Within(.001f));
        Assert.That(Hp(source),Is.EqualTo(500));Assert.That((float)Get(source,"itemShield"),Is.EqualTo(7f).Within(.001f));
        Assert.That((float)Get(ally,"itemShield"),Is.Zero);
    }
    [Test] public void MountainBraceUsesNearestProtectorWithoutAChain()
    {
        var brace=StartBrace(source);var other=Unit(0,Vector3.right*1.5f);var second=StartBrace(other);
        var ally=Unit(0,Vector3.right*2f);
        MammothCall("AdvanceMammothBrace",source,brace,.4f);MammothCall("AdvanceMammothBrace",other,second,.4f);
        BraceDamage(target,ally,100);
        Assert.That(Hp(ally),Is.EqualTo(4930).Within(.001f));Assert.That(Hp(source),Is.EqualTo(500));
        Assert.That(Hp(other),Is.EqualTo(485).Within(.001f));
    }
    [TestCase(.34f)] [TestCase(3.35f)]
    public void MountainBraceOnlyProtectsDuringItsGuardWindow(float time)
    {
        var brace=StartBrace(source);var ally=Unit(0,Vector3.right);
        MammothCall("AdvanceMammothBrace",source,brace,time);BraceDamage(target,ally,100);
        Assert.That(Hp(ally),Is.EqualTo(4900));Assert.That(Hp(source),Is.EqualTo(500));
    }
    [Test] public void MountainBraceRangeIsLiveAndProtectionEndsImmediatelyOnInterruption()
    {
        var brace=StartBrace(source);var ally=Unit(0,Vector3.right*4.26f);
        MammothCall("AdvanceMammothBrace",source,brace,.4f);BraceDamage(target,ally,100);
        Assert.That(Hp(source),Is.EqualTo(500));
        Model(ally).transform.position=Vector3.right*4.24f;BraceDamage(target,ally,100);
        Assert.That(Hp(source),Is.EqualTo(485).Within(.001f));
        Set(source,"positionRevision",1);BraceDamage(target,ally,100);
        Assert.That(Hp(source),Is.EqualTo(485).Within(.001f));
        MammothCall("AdvanceMammothBrace",source,brace,3.5f);
        Assert.That(Get(source,"mammothBrace"),Is.Null);Assert.That((float)Get(source,"attackMovementLockTimer"),Is.Zero);
        Assert.That(Hp(target),Is.EqualTo(5000));
    }
    [Test] public void MountainBraceStopsOnBattleResetAndDoesNotGrantImmunity()
    {
        var brace=StartBrace(source);MammothCall("AdvanceMammothBrace",source,brace,.4f);
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).DebuffImmune,Is.False);
        typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        MammothCall("AdvanceMammothBrace",source,brace,3.5f);
        Assert.That(Get(source,"mammothBrace"),Is.Null);Assert.That(Hp(target),Is.EqualTo(5000));
    }
    [TestCase("kogason",35f)] [TestCase("seolgason",200f)]
    [TestCase("irontusk",63f)] [TestCase("gaiason",27.5f)]
    public void MountainBraceSweepsOnlyForwardOnTheHitFrameWithSpeciesScaling(string species,float expected)
    {
        var brace=StartBrace(source,species);
        if(species=="irontusk")Trait(MammothCombatRules.IrontuskTraitId);
        Model(target).transform.position=Vector3.right*.8f;
        var side=Unit(1,new Vector3(1.8f,0,2));var behind=Unit(1,Vector3.left);
        var outside=Unit(1,Vector3.right*3.71f);var ally=Unit(0,Vector3.right);
        MammothCall("AdvanceMammothBrace",source,brace,3.499f);
        Assert.That(Hp(target),Is.EqualTo(5000));
        MammothCall("AdvanceMammothBrace",source,brace,3.5f);
        Assert.That(Hp(target),Is.EqualTo(5000-expected).Within(.001f));
        Assert.That(Hp(side),Is.EqualTo(5000-expected).Within(.001f));
        Assert.That(Hp(behind),Is.EqualTo(5000));Assert.That(Hp(outside),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        MammothCall("AdvanceMammothBrace",source,brace,3.8f);
        Assert.That(Hp(target),Is.EqualTo(5000-expected).Within(.001f));
        Assert.That(Model(source).transform.position,Is.EqualTo(Vector3.zero));
        MammothCall("EndMammothBrace",source,brace);Assert.That(Get(source,"mammothBrace"),Is.Null);
    }
    [Test] public void MountainBraceFrozenCasterStopsSharingUntilThawed()
    {
        var brace=StartBrace(source);var ally=Unit(0,Vector3.right);
        MammothCall("AdvanceMammothBrace",source,brace,.4f);
        var status=(CombatStatusState)Get(source,"combatStatuses");status.ApplyTimed(CombatStatusKind.Freeze,2f);
        BraceDamage(target,ally,100);Assert.That(Hp(source),Is.EqualTo(500));
        status.Remove(CombatStatusKind.Freeze);BraceDamage(target,ally,100);
        Assert.That(Hp(source),Is.EqualTo(485).Within(.001f));
        status.ApplyTimed(CombatStatusKind.Knockdown,1f);BraceDamage(target,ally,100);
        Assert.That(Hp(source),Is.EqualTo(485).Within(.001f));
        MammothCall("AdvanceMammothBrace",source,brace,3.5f);Assert.That(Get(source,"mammothBrace"),Is.Null);
    }
    [Test] public void MountainBraceCanDieFromSharingAndImmediatelyReleasesAllies()
    {
        var brace=StartBrace(source);Set(source,"hp",5f);var ally=Unit(0,Vector3.right);
        MammothCall("AdvanceMammothBrace",source,brace,.4f);BraceDamage(target,ally,100);
        Assert.That((bool)Get(source,"alive"),Is.False);Assert.That(Get(source,"mammothBrace"),Is.Null);
        Assert.That(Hp(ally),Is.EqualTo(4930).Within(.001f));
        BraceDamage(target,ally,100);Assert.That(Hp(ally),Is.EqualTo(4830).Within(.001f));
    }
    [TestCase("057_kogason")] [TestCase("058_seolgason")]
    [TestCase("059_irontusk")] [TestCase("060_gaiason")]
    public void MountainBraceAnimationFreezesAndSynchronizesSweep(string prefab)
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();var advance=driver.GetType().GetMethod("Advance",Private);
        float clock=0;int completed=0;
        driver.PlayMammothCharge(()=>true,t=>clock=t,()=>completed++,false,false,true);
        advance.Invoke(driver,new object[]{.5f});driver.SetFrozen(true);advance.Invoke(driver,new object[]{2f});
        Assert.That(clock,Is.EqualTo(.5f).Within(.001f));driver.SetFrozen(false);
        advance.Invoke(driver,new object[]{3.00001f});
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo((int)driver.GetType().GetField("skillAttackHitFrameIndex",Private).GetValue(driver)));
        Assert.That(completed,Is.Zero);advance.Invoke(driver,new object[]{.4f});Assert.That(completed,Is.EqualTo(1));
        driver.PlayDeath();Assert.That(completed,Is.EqualTo(1));
    }
}
