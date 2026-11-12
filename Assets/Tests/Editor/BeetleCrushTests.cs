using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private object StartBeetleCrush(string species="jangpung")
    {Trait(BeetleRank3Rules.GoldenCoffin);return StartBeetle(species);}

    [TestCase("jangpung",60f,300f,120f)] [TestCase("noepung",60f,300f,120f)]
    [TestCase("solarbeetle",60f,300f,120f)] [TestCase("atlagon",16.32f,81.6f,32.64f)]
    public void BeetleCrushDamageLandsOnceWithoutAddingSplashToPrimary(string species,float hook,float crush,float splash)
    {
        var flip=StartBeetleCrush(species);var near=Unit(1,Vector3.right*2.6f);
        var far=Unit(1,Vector3.right*5);var ally=Unit(0,new Vector3(2,0,1));
        Assert.That(MammothCall("HasBeetleRank3",source),Is.False);
        MammothCall("AdvanceBeetleFlip",source,flip,.3f);
        Assert.That(Hp(target),Is.EqualTo(5000-hook).Within(.01f));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushLanding-.01f);
        Assert.That(Hp(near),Is.EqualTo(5000f));Assert.That(Hp(target),Is.EqualTo(5000-hook).Within(.01f));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushLanding);
        Assert.That(Hp(target),Is.EqualTo(5000-hook-crush).Within(.01f));
        Assert.That(Hp(near),Is.EqualTo(5000-splash).Within(.01f));
        Assert.That(Hp(far),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.True);
        Assert.That(Get(source,"beetleFlip"),Is.SameAs(flip));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushEnd+.1f);
        Assert.That(Hp(target),Is.EqualTo(5000-hook-crush).Within(.01f));
        Assert.That(Get(source,"beetleFlip"),Is.Null);Assert.That(Get(source,"beetleSweepTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void BeetleCrushCancellationClearsBothAirPosesWithoutLandingHit()
    {
        var actor=Model(source).AddComponent<SpriteMonsterBillboard>();Set(source,"spriteBillboard",actor);
        var victim=Model(target).AddComponent<SpriteMonsterBillboard>();Set(target,"spriteBillboard",victim);
        var flip=StartBeetleCrush();MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushApex);
        var offset=typeof(SpriteMonsterBillboard).GetField("rollOffset",Private);
        Assert.That(((Vector3)offset.GetValue(actor)).y,Is.GreaterThan(4f));
        Assert.That(((Vector3)offset.GetValue(victim)).y,Is.GreaterThan(3f));
        MammothCall("InterruptPositionalAction",source);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushLanding);
        Assert.That(offset.GetValue(actor),Is.EqualTo(Vector3.zero));
        Assert.That(offset.GetValue(victim),Is.EqualTo(Vector3.zero));
        Assert.That(Hp(target),Is.EqualTo(4940f).Within(.01f));
        Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
    }

    [TestCase(true)] [TestCase(false)]
    public void BeetleCrushEscapedOrDeadPrimaryCannotTakeRemoteCrushButAreaStillLands(bool escape)
    {
        var flip=StartBeetleCrush();var near=Unit(1,Vector3.right*2.6f);
        MammothCall("AdvanceBeetleFlip",source,flip,1f);
        if(escape){MammothCall("InterruptPositionalAction",target);Model(target).transform.position=Vector3.right*9;}
        else{Set(target,"alive",false);MammothCall("CleanupRank3Unit",target);}
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushEnd+.1f);
        Assert.That(Hp(target),Is.EqualTo(4940f).Within(.01f));
        Assert.That(Hp(near),Is.EqualTo(4880f).Within(.01f));
        Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
    }

    [Test]
    public void BeetleCrushControlImmuneTargetTakesDamageWithoutBeingFlipped()
    {
        var flip=StartBeetleCrush();var status=(CombatStatusState)Get(target,"combatStatuses");status.ControlImmune=true;
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushEnd+.1f);
        Assert.That(Hp(target),Is.EqualTo(4640f).Within(.01f));
        Assert.That(status.Has(CombatStatusKind.Airborne),Is.False);
        Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False);
    }

    [Test]
    public void BeetleCrushCasterStaysCloseToVictimAtImpactAndSettlesBeforeRecovery()
    {
        var actor=Model(source).AddComponent<SpriteMonsterBillboard>();Set(source,"spriteBillboard",actor);
        var flip=StartBeetleCrush();
        var offset=typeof(SpriteMonsterBillboard).GetField("rollOffset",Private);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushLanding);
        Assert.That(((Vector3)offset.GetValue(actor)).y,Is.EqualTo(.35f).Within(.001f));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushCasterLanding);
        Assert.That(offset.GetValue(actor),Is.EqualTo(Vector3.zero));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.CrushEnd);
        Assert.That(typeof(SpriteMonsterBillboard).GetField("rollAngle",Private).GetValue(actor),Is.EqualTo(0f));
    }

    [Test]
    public void BeetleCrushAnimationFreezesAndOwnsFullOnePointFourFiveSeconds()
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/061_jangpung"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        float clock=0;int finished=0;driver.PlayBeetleFlip(()=>true,t=>clock=t,()=>finished++,true);
        advance.Invoke(driver,new object[]{.96f});driver.SetFrozen(true);
        advance.Invoke(driver,new object[]{1f});Assert.That(clock,Is.EqualTo(.96f).Within(.001f));
        driver.SetFrozen(false);advance.Invoke(driver,new object[]{.44f});
        Assert.That(finished,Is.Zero);Assert.That(clock,Is.EqualTo(1.4f).Within(.001f));
        advance.Invoke(driver,new object[]{.06f});Assert.That(finished,Is.EqualTo(1));
        driver.PlayDeath();Assert.That(finished,Is.EqualTo(1));
    }
}
