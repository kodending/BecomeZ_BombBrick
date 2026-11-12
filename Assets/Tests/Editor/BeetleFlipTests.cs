using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private object StartBeetle(string species="jangpung")
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(source,"skillId",BeetleCombatRules.SkillId);Set(source,"defense",68f);
        Model(target).transform.position=Vector3.right*1.5f;
        MammothCall("StartBeetleFlip",source,target,GameMasterData.Skill(BeetleCombatRules.SkillId),0);
        return Get(source,"beetleFlip");
    }

    [TestCase("jangpung",60f,120f)] [TestCase("noepung",60f,120f)]
    [TestCase("solarbeetle",60f,120f)] [TestCase("atlagon",16.32f,32.64f)]
    public void BeetleDamageMatchesHookAndLandingAndSpeciesStat(string species,float hook,float landing)
    {
        var flip=StartBeetle(species);var near=Unit(1,Vector3.right*2);var far=Unit(1,Vector3.right*5);
        var ally=Unit(0,Vector3.right*2);
        MammothCall("AdvanceBeetleFlip",source,flip,.29f);Assert.That(Hp(target),Is.EqualTo(5000));
        MammothCall("AdvanceBeetleFlip",source,flip,.3f);
        Assert.That(Hp(target),Is.EqualTo(5000-hook).Within(.01f));Assert.That(Get(target,"beetleFlippedBy"),Is.SameAs(source));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing-.01f);Assert.That(Hp(near),Is.EqualTo(5000));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing);
        Assert.That(Hp(target),Is.EqualTo(5000-hook-landing).Within(.01f));
        Assert.That(Hp(near),Is.EqualTo(5000-landing).Within(.01f));
        Assert.That(Hp(far),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.True);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing+.1f);
        Assert.That(Hp(target),Is.EqualTo(5000-hook-landing).Within(.01f));
    }

    [Test]
    public void BeetleBaseRetainsPrimaryForRank4Followup()
    {
        StartBeetle();Assert.That(Get(source,"beetleSkyFlipTarget"),Is.Null);
        Assert.That(MammothCall("PendingTranscendenceTarget",source),Is.SameAs(target));
    }

    [Test]
    public void BeetleSecondCasterCannotStealHeldVictim()
    {
        var flip=StartBeetle();MammothCall("AdvanceBeetleFlip",source,flip,.4f);
        var second=Unit(0,Vector3.right*.2f);
        MammothCall("StartBeetleFlip",second,target,GameMasterData.Skill(BeetleCombatRules.SkillId),0);
        var other=Get(second,"beetleFlip");MammothCall("AdvanceBeetleFlip",second,other,.4f);
        Assert.That(Get(target,"beetleFlippedBy"),Is.SameAs(source));
        MammothCall("EndBeetleFlip",second,other);
        Assert.That(Get(target,"beetleFlippedBy"),Is.SameAs(source));
    }

    [Test]
    public void BeetleLongFrameStillHitsEachStageOnlyOnce()
    {
        var flip=StartBeetle();MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.End+.1f);
        Assert.That(Hp(target),Is.EqualTo(4820).Within(.01f));Assert.That(Get(source,"beetleFlip"),Is.Null);
        Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
    }

    [TestCase(true)] [TestCase(false)]
    public void BeetleInterruptionReleasesVictimWithoutPrematureLandingDamage(bool caster)
    {
        var flip=StartBeetle();MammothCall("AdvanceBeetleFlip",source,flip,.4f);
        MammothCall("InterruptPositionalAction",caster?source:target);
        MammothCall("AdvanceBeetleFlip",source,flip,.5f);
        Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Airborne),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4940).Within(.01f));
        Assert.That(Model(target).transform.position,Is.EqualTo(Vector3.right*1.5f));
    }

    [Test]
    public void BeetleImmuneVictimTakesDamageWithoutBeingHeldOrKnockedDown()
    {
        var flip=StartBeetle();var status=(CombatStatusState)Get(target,"combatStatuses");status.ControlImmune=true;
        MammothCall("AdvanceBeetleFlip",source,flip,.4f);Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing);
        Assert.That(Hp(target),Is.EqualTo(4820).Within(.01f));Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False);
    }

    [Test]
    public void BeetleCommittedFlipFollowsItsTargetAfterItLeavesRangeWithoutExpandingLandingDamage()
    {
        var flip=StartBeetle();object oldPosition=Unit(1,Model(target).transform.position);
        Model(target).transform.position=Vector3.right*10f;
        var skill=GameMasterData.Skill(BeetleCombatRules.SkillId);
        float radius=MonsterAutoBattlePrototype.TileSize*skill.radiusTiles;
        object near=Unit(1,Model(target).transform.position+Vector3.forward*.6f);
        object outside=Unit(1,Model(target).transform.position+Vector3.forward*(radius+.5f));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Hook);
        Assert.That(Get(source,"beetleFlip"),Is.SameAs(flip));
        Assert.That(Get(target,"beetleFlippedBy"),Is.SameAs(source));
        Assert.That(Hp(target),Is.EqualTo(4940f).Within(.01f));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing);
        Assert.That(Hp(target),Is.EqualTo(4820f).Within(.01f));
        Assert.That(Hp(near),Is.EqualTo(4880f).Within(.01f));
        Assert.That(Hp(outside),Is.EqualTo(5000f));Assert.That(Hp(oldPosition),Is.EqualTo(5000f));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.End);
        Assert.That(Get(source,"beetleFlip"),Is.Null);Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
    }

    [Test]
    public void BeetleTargetDeathDuringWindupStillCancelsTheAcceptedFlip()
    {
        var flip=StartBeetle();Set(target,"alive",false);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Hook);
        Assert.That(Get(source,"beetleFlip"),Is.Null);Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
    }

    [Test]
    public void BeetlePrimaryDeathReleasesPoseButLandingStillDamagesNearbyEnemy()
    {
        var flip=StartBeetle();var near=Unit(1,Vector3.right*2);
        MammothCall("AdvanceBeetleFlip",source,flip,.4f);Set(target,"alive",false);
        MammothCall("CleanupRank3Unit",target);Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing);Assert.That(Hp(near),Is.EqualTo(4880).Within(.01f));
    }

    [TestCase("jangpung",230f)] [TestCase("noepung",240f)] [TestCase("solarbeetle",180f)]
    public void BeetleIntrinsicStillAppliesAtLanding(string species,float total)
    {
        Trait(species=="jangpung"?BeetleCombatRules.JangpungTraitId:species=="noepung"?BeetleCombatRules.NoepungTraitId:BeetleCombatRules.SolarbeetleTraitId);
        var flip=StartBeetle(species);MammothCall("AdvanceBeetleFlip",source,flip,.3f);
        Assert.That(Hp(target),Is.EqualTo(4940).Within(.01f));MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing);
        Assert.That(Hp(target),Is.EqualTo(5000-total).Within(.01f));
        if(species=="solarbeetle")Assert.That((float)Get(target,"itemDefenseShredRatio"),Is.EqualTo(.15f));
    }

    [TestCase("061_jangpung")] [TestCase("062_noepung")]
    [TestCase("063_atlagon")] [TestCase("064_solarbeetle")]
    public void BeetleAnimationPausesWhenFrozenAndEndsExactlyOnce(string prefab)
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        float clock=0;int finished=0;driver.PlayBeetleFlip(()=>true,t=>clock=t,()=>finished++);
        advance.Invoke(driver,new object[]{.3f});Assert.That(clock,Is.EqualTo(.3f).Within(.001f));
        driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});Assert.That(clock,Is.EqualTo(.3f).Within(.001f));
        driver.SetFrozen(false);advance.Invoke(driver,new object[]{BeetleFlipRules.Landing-BeetleFlipRules.Hook});
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(driver.SkillAttackHitFrameIndex));
        advance.Invoke(driver,new object[]{BeetleFlipRules.End-BeetleFlipRules.Landing+.01f});Assert.That(finished,Is.EqualTo(1));
        driver.PlayDeath();Assert.That(finished,Is.EqualTo(1));
    }
}
