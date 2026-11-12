using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private void ReadyBeetleSweep(string species="jangpung")
    {
        Trait(BeetleRank3Rules.MassTheft);
        var flip=StartBeetle(species);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.End+.01f);
        Set(target,"hp",5000f);
    }

    [Test]
    public void BeetleSweepStartsAfterSkillWithoutStealingStatsOrResizing()
    {
        Trait(BeetleRank3Rules.MassTheft);var flip=StartBeetle();
        Assert.That(MammothCall("HasBeetleRank3",source),Is.False);
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.Landing);
        Assert.That(Get(source,"beetleSweepTimer"),Is.EqualTo(0f));
        MammothCall("AdvanceBeetleFlip",source,flip,BeetleFlipRules.End);
        Assert.That(Get(source,"beetleSweepTimer"),Is.EqualTo(6f));
        Assert.That(Hp(target),Is.EqualTo(4820f).Within(.01f));
        foreach(var u in new[]{source,target})
        {
            Assert.That(Get(u,"damage"),Is.EqualTo(100f));
            Assert.That(Get(u,"rank3AttackBonusTimer"),Is.EqualTo(0f));
            Assert.That(Get(u,"rank3DefenseBonusTimer"),Is.EqualTo(0f));
            Assert.That(Get(u,"rank3AttackSpeedBonusTimer"),Is.EqualTo(0f));
            Assert.That(Model(u).transform.localScale,Is.EqualTo(Vector3.one));
        }
        Assert.That(Get(source,"defense"),Is.EqualTo(68f));
        Assert.That(Get(target,"defense"),Is.EqualTo(0f));
    }

    [TestCase("jangpung",80f)] [TestCase("noepung",80f)]
    [TestCase("solarbeetle",80f)] [TestCase("atlagon",21.76f)]
    public void BeetleSweepActualBasicAttackHitsFrontOnePointFiveTiles(string species,float extra)
    {
        ReadyBeetleSweep(species);
        var near=Unit(1,new Vector3(1.2f,0,1f));
        var back=Unit(1,Vector3.left);var far=Unit(1,Vector3.right*3.2f);
        var ally=Unit(0,Vector3.right);
        MammothCall("Attack",source,target);
        Assert.That(Hp(target),Is.EqualTo(4900f-extra).Within(.01f));
        Assert.That(Hp(near),Is.EqualTo(5000f-extra).Within(.01f));
        Assert.That(Hp(back),Is.EqualTo(5000f));Assert.That(Hp(far),Is.EqualTo(5000f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(1));
    }

    [Test]
    public void BeetleSweepRefreshDoesNotStackAndExpiryRestoresNormalBasic()
    {
        ReadyBeetleSweep();var near=Unit(1,new Vector3(1.2f,0,1f));
        MammothCall("TickBeetleSweep",source,4f);
        MammothCall("GrantBeetleSweep",source,Vector3.zero);
        Assert.That(Get(source,"beetleSweepTimer"),Is.EqualTo(6f));
        MammothCall("TickBeetleSweep",source,6f);
        MammothCall("Attack",source,target);
        Assert.That(Hp(target),Is.EqualTo(4900f).Within(.01f));
        Assert.That(Hp(near),Is.EqualTo(5000f));
    }

    [Test]
    public void BeetleSweepInterruptedBeforeLandingDoesNotGrantAndCleanupClears()
    {
        Trait(BeetleRank3Rules.MassTheft);var flip=StartBeetle();
        MammothCall("AdvanceBeetleFlip",source,flip,.4f);
        MammothCall("InterruptPositionalAction",source);
        Assert.That(Get(source,"beetleSweepTimer"),Is.EqualTo(0f));
        MammothCall("GrantBeetleSweep",source,Vector3.zero);
        MammothCall("CleanupRank3Unit",source);
        Assert.That(Get(source,"beetleSweepTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void BeetleSweepFrozenSwingKeepsItsFrameAndOrdinaryAttackClearsPose()
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/061_jangpung"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        int hits=0;driver.PlayBeetleSweepAttack(1f,()=>hits++);
        advance.Invoke(driver,new object[]{.03f});float phase=driver.BeetleSweepPhase;
        driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});
        Assert.That(driver.BeetleSweepPhase,Is.EqualTo(phase));Assert.That(hits,Is.Zero);
        driver.SetFrozen(false);advance.Invoke(driver,new object[]{driver.BasicAttackHitTime});
        typeof(SpriteMonsterAnimationDriver).GetMethod("ApplyExplicitFrame",Private).Invoke(driver,null);
        Assert.That(hits,Is.EqualTo(1));
        advance.Invoke(driver,new object[]{1f});Assert.That(hits,Is.EqualTo(1));
        driver.PlayBasicAttack(1f,null);Assert.That(driver.BeetleSweepAttacking,Is.False);
    }
}
