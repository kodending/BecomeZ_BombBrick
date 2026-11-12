using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private PixelStarProjectile[] FanShots()=>Object.FindObjectsOfType<PixelStarProjectile>()
        .Where(p=>p.name=="Pixel Mantis Myriad Scythe").ToArray();
    private void StartFan(string intrinsic=null)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_mantius"));
        Set(source,"skillId",MantisCombatRules.SkillId);Trait(MantisRank3Rules.MyriadEyes);
        if(intrinsic!=null)Trait(intrinsic);
        cast=(IEnumerator)typeof(MonsterAutoBattlePrototype).GetMethod("ResolveMantisFan",Private)
            .Invoke(battle,new[]{source,target,1f,1f,0});
        Assert.That(cast.MoveNext(),Is.True);
    }
    private void FinishFan(PixelStarProjectile[] shots)
    {foreach(var shot in shots)if(shot!=null)AdvanceMantis(shot,1f);}

    [TestCase(false)]
    [TestCase(true)]
    public void MantisFanCapsOverlapPerVolleyAndBleedsInEitherArrivalOrder(bool reverse)
    {
        object near=Unit(1,Vector3.right*.6f),behind=Unit(1,Vector3.left*2f);
        object side=Unit(1,Vector3.forward*3f),ally=Unit(0,Vector3.right*2f);
        StartFan();var first=FanShots();Assert.That(first.Length,Is.EqualTo(7));
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(cast.MoveNext(),Is.True);
        var second=FanShots().Except(first).ToArray();Assert.That(second.Length,Is.EqualTo(7));
        FinishFan(reverse?second:first);
        Assert.That(Hp(target),Is.EqualTo(4840f));Assert.That(Hp(near),Is.EqualTo(4900f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Bleed),Is.False);
        FinishFan(reverse?first:second);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4680f));Assert.That(Hp(near),Is.EqualTo(4800f));
        var bleed=((CombatStatusState)Get(target,"combatStatuses")).Get(CombatStatusKind.Bleed);
        Assert.That(bleed,Is.Not.Null);Assert.That(bleed.remaining,Is.EqualTo(3f));
        foreach(var untouched in new[]{behind,side,ally})Assert.That(Hp(untouched),Is.EqualTo(5000f));
    }

    [Test]
    public void MantisFanSweepsCurvesDuringLongFramesAndRechecksMovingEnemies()
    {
        float range=MonsterAutoBattlePrototype.TileSize*7f;
        object curve=Unit(1,MantisRank3Rules.MyriadPoint(Vector3.zero,Vector3.right,60f,range,.18f));
        object entering=Unit(1,new Vector3(3f,0f,6f));
        StartFan();var first=FanShots();
        Model(target).transform.position=Vector3.left*3f;
        Model(entering).transform.position=Vector3.right*3f;
        FinishFan(first);
        Assert.That(Hp(curve),Is.EqualTo(4900f));Assert.That(Hp(entering),Is.EqualTo(4900f));
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(cast.MoveNext(),Is.True);FinishFan(FanShots());Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [TestCase(MantisCombatRules.MantiusTraitId,384f,false)]
    [TestCase(MantisCombatRules.FlotisTraitId,320f,true)]
    [TestCase(MantisCombatRules.SantiusTraitId,320f,false)]
    public void MantisFanPreservesSpeciesDamageAndDoubleHitPollen(string trait,float damage,bool pollen)
    {
        StartFan(trait);FinishFan(FanShots());Assert.That(cast.MoveNext(),Is.True);
        FinishFan(FanShots());Assert.That(cast.MoveNext(),Is.False);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.HallucinatoryPollen),Is.EqualTo(pollen));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MantisFanUsesSevenTileRangeWithSantiusExtension(bool sand)
    {
        object far=Unit(1,Vector3.right*(MonsterAutoBattlePrototype.TileSize*7.8f));
        StartFan(sand?MantisCombatRules.SantiusTraitId:null);FinishFan(FanShots());
        Assert.That(Hp(far),Is.EqualTo(sand?4900f:5000f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MantisFanSurvivesCasterDeathAndCancelsOnReset(bool reset)
    {
        StartFan();var first=FanShots();
        if(reset)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else Set(source,"alive",false);
        FinishFan(first);
        if(!reset){Assert.That(cast.MoveNext(),Is.True);FinishFan(FanShots());}
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(FanShots(),Is.Empty);Assert.That(Hp(target),Is.EqualTo(reset?5000f:4680f));
    }

    [Test]
    public void MantisFanReleasedSecondVolleyUsesBattleTimeAcrossFreeze()
    {
        var driver=Model(source).AddComponent<SpriteMonsterAnimationDriver>();
        typeof(SpriteMonsterAnimationDriver).GetField("skillAttackFrames",Private)
            .SetValue(driver,Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray());
        Set(source,"spriteAnimation",driver);driver.PlayMantisCrossAttack(()=>{},.12f);
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        advance.Invoke(driver,new object[]{.12f});StartFan();
        typeof(SpriteMonsterAnimationDriver).GetField("statusFrozen",Private).SetValue(driver,true);
        advance.Invoke(driver,new object[]{.5f});
        var wait=(WaitUntil)cast.Current;
        Assert.That(wait.keepWaiting,Is.True);
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,.5f);
        Assert.That(wait.keepWaiting,Is.False);Assert.That(cast.MoveNext(),Is.True);
        Assert.That(FanShots().Length,Is.EqualTo(14));
    }

    [Test]
    public void MantisFanNectisKillAddsOnlyTwoContactEchoes()
    {
        object nearby=Unit(1,new Vector3(4f,0f,3f)),another=Unit(1,new Vector3(4f,0f,-3f));
        Set(target,"hp",60f);StartFan(MantisCombatRules.NectisTraitId);
        FinishFan(FanShots());Assert.That(cast.MoveNext(),Is.True);FinishFan(FanShots());
        float beforeA=Hp(nearby),beforeB=Hp(another);
        Assert.That(cast.MoveNext(),Is.True);
        var echoes=Object.FindObjectsOfType<PixelStarProjectile>().Where(p=>p.name=="Pixel Mantis Myriad Echo").ToArray();
        Assert.That(echoes.Length,Is.EqualTo(2));
        Assert.That(Hp(nearby),Is.EqualTo(beforeA));FinishFan(echoes);
        Assert.That(beforeA-Hp(nearby),Is.EqualTo(60f));Assert.That(beforeB-Hp(another),Is.EqualTo(60f));
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Object.FindObjectsOfType<PixelStarProjectile>(),Is.Empty);
    }
}
