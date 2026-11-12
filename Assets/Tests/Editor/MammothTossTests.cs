using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private object StartToss(string species="seolgason")
    {
        var type=typeof(MonsterAutoBattlePrototype);
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(source,"skillId",MammothCombatRules.SkillId);Set(source,"maxHp",500f);Set(source,"defense",60f);
        Set(source,"mammothChargeEnd",Vector3.right*8.5f);
        Trait(MammothRank3Rules.MountainToss);
        if(species=="irontusk")Trait(MammothCombatRules.IrontuskTraitId);
        MammothCall("StartMammothToss",source,0);return Get(source,"mammothToss");
    }
    [TestCase("kogason",17.5f,0f)] [TestCase("seolgason",100f,0f)]
    [TestCase("irontusk",21f,21f)] [TestCase("gaiason",13.75f,0f)]
    public void MountainHurlStaysPutPiercesOnceAndBurstsWithSpeciesPower(string species,float power,float steel)
    {
        var far=Unit(1,Vector3.right*8.5f);var outside=Unit(1,new Vector3(8.5f,0,4f));
        var ally=Unit(0,Vector3.right*4f);var toss=StartToss(species);
        MammothCall("AdvanceMammothToss",source,toss,.79f);Assert.That(Hp(target),Is.EqualTo(5000));
        MammothCall("AdvanceMammothToss",source,toss,.8f);Assert.That(Hp(target),Is.EqualTo(5000));
        MammothCall("AdvanceMammothBoulder",toss,.15f);
        Assert.That(Hp(target),Is.EqualTo(5000-power*2.2f-steel).Within(.02f));
        Assert.That(Model(target).transform.position,Is.EqualTo(Vector3.right*4f),"No carry or forced movement");
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.False);
        MammothCall("AdvanceMammothBoulder",toss,.18f);
        Assert.That(Hp(target),Is.EqualTo(5000-power*2.2f-steel).Within(.02f));
        MammothCall("AdvanceMammothBoulder",toss,.65f);
        Assert.That(Hp(target),Is.EqualTo(5000-power*2.2f-steel).Within(.02f));
        Assert.That(Hp(far),Is.EqualTo(5000-power*3.6f-steel).Within(.02f));
        Assert.That(Hp(outside),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        Assert.That(Model(source).transform.position,Is.EqualTo(Vector3.zero));
        MammothCall("AdvanceMammothBoulder",toss,.7f);
        Assert.That(Hp(far),Is.EqualTo(5000-power*3.6f-steel).Within(.02f));
    }
    [TestCase(false)] [TestCase(true)]
    public void MountainHurlStopsAtTerrainAndHostileWall(bool wall)
    {
        Model(target).transform.position=Vector3.right*8.5f;
        var toss=StartToss();
        if(wall)MammothCall("RegisterRank3IceWall",target,Vector3.right*3f,Vector3.right,Vector3.forward,5f,4f);
        else typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,new Vector3(6,1,40)));
        MammothCall("AdvanceMammothToss",source,toss,.8f);MammothCall("AdvanceMammothBoulder",toss,.8f);
        Assert.That((float)toss.GetType().GetField("shatteredAt").GetValue(toss),Is.GreaterThanOrEqualTo(0));
        Assert.That(((Vector3)toss.GetType().GetField("ground").GetValue(toss)).x,Is.LessThanOrEqualTo(3f));
        Assert.That(Hp(target),Is.EqualTo(5000));
    }
    [TestCase(false)] [TestCase(true)]
    public void MountainHurlCancellationDependsOnWhetherTheRockWasReleased(bool released)
    {
        var toss=StartToss();MammothCall("AdvanceMammothToss",source,toss,released?.8f:.4f);
        Set(source,"alive",false);MammothCall("EndMammothToss",source,toss);
        MammothCall("AdvanceMammothBoulder",toss,.65f);
        Assert.That(Get(source,"mammothToss"),Is.Null);
        Assert.That(Hp(target),Is.EqualTo(released?4780f:5000f).Within(.01f));
        Assert.That((float)Get(source,"attackMovementLockTimer"),Is.Zero);
    }
    [Test] public void MountainHurlResetCancelsReleasedRockAndRetainsSavedTraitId()
    {
        var toss=StartToss();MammothCall("AdvanceMammothToss",source,toss,.8f);
        MammothCall("ClearMammothBoulders");MammothCall("AdvanceMammothBoulder",toss,.65f);
        Assert.That(Hp(target),Is.EqualTo(5000));
        Assert.That(MammothRank3Rules.MountainToss,Is.EqualTo("trait_mammoth_annihilation_plow"));
        Assert.That(GameMasterData.Database.traits.First(t=>t.id==MammothRank3Rules.MountainToss).displayName,Is.EqualTo("산맥 투석"));
    }
    [Test] public void MountainHurlAnimationReleasesOnStrikeFrameAndFreezesDuringWindup()
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/057_kogason"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        float clock=0;int finished=0;
        driver.PlayMammothCharge(()=>true,t=>clock=t,()=>finished++,true);
        advance.Invoke(driver,new object[]{.4f});driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});
        Assert.That(clock,Is.EqualTo(.4f).Within(.001f));
        driver.SetFrozen(false);advance.Invoke(driver,new object[]{.4f});
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo((int)typeof(SpriteMonsterAnimationDriver).GetField("skillAttackHitFrameIndex",Private).GetValue(driver)));
        advance.Invoke(driver,new object[]{.26f});Assert.That(finished,Is.EqualTo(1));
    }
}
