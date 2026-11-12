using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void CrownReady(string intrinsic=null)
    {
        GasReady(intrinsic);Trait(MushroomRank3Rules.ParasiticCrown);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
    }
    private void CrownCreate(float scale=1f)=>ReaperCall("CreateMushroomCrown",source,target,
        Model(target).transform.position,scale,1f,0,null,0);
    private void CrownTick(float dt)=>ReaperCall("UpdateMushroomCrowns",dt);
    private IList Crowns=>(IList)typeof(MonsterAutoBattlePrototype).GetField("mushroomCrowns",Private).GetValue(battle);

    [Test]
    public void CrownWaitsForProjectileThenErodesThreeTimesAndBloomsOnce()
    {
        CrownReady();object nearby=Unit(1,Model(target).transform.position+Vector3.forward*2.4f);
        object outside=Unit(1,Model(target).transform.position+Vector3.forward*3f);
        cast=(IEnumerator)ReaperCall("ResolveSporeColony",source,target,
            GameMasterData.Skill(MushroomCombatRules.SkillId),1f,false,0);
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Crowns.Count,Is.Zero);
        Arrive(Object.FindObjectOfType<PixelStarProjectile>());
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Crowns.Count,Is.EqualTo(1));Assert.That(GasZones.Count,Is.Zero);
        CrownTick(.99f);Assert.That(Hp(target),Is.EqualTo(5000f));
        CrownTick(.01f);Assert.That(Hp(target),Is.EqualTo(4950f));
        CrownTick(1f);Assert.That(Hp(target),Is.EqualTo(4900f));Assert.That(Hp(nearby),Is.EqualTo(5000f));
        CrownTick(1f);Assert.That(Hp(target),Is.EqualTo(4610f));Assert.That(Hp(nearby),Is.EqualTo(4760f));
        Assert.That(Hp(outside),Is.EqualTo(5000f));Assert.That(Hp(source),Is.EqualTo(5000f));
        CrownTick(5f);Assert.That(Hp(target),Is.EqualTo(4610f));Assert.That(Crowns.Count,Is.Zero);
    }

    [Test]
    public void CrownFollowsHostAndUsesItsFinalPositionForBloom()
    {
        CrownReady();object old=Unit(1,Model(target).transform.position);CrownCreate();CrownTick(1f);
        Model(target).transform.position=Vector3.right*15f;
        object moved=Unit(1,Model(target).transform.position);CrownTick(2f);
        Assert.That(Hp(old),Is.EqualTo(5000f));Assert.That(Hp(moved),Is.EqualTo(4760f));
        Assert.That(Hp(target),Is.EqualTo(4610f));
    }

    [TestCase(1f)]
    [TestCase(51f)]
    [TestCase(101f)]
    public void CrownLethalErosionBloomsOnceAndCancelsRemainingTicks(float hp)
    {
        CrownReady();Set(target,"hp",hp);object nearby=Unit(1,Model(target).transform.position);
        CrownCreate();CrownTick(3f);Assert.That(Get(target,"alive"),Is.False);
        Assert.That(Hp(nearby),Is.EqualTo(4760f));Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
        CrownTick(5f);Assert.That(Hp(nearby),Is.EqualTo(4760f));
    }

    [Test]
    public void CrownExternalKillingBlowBloomsImmediatelyAtDeathPosition()
    {
        CrownReady();CrownCreate();CrownTick(.5f);
        Model(target).transform.position=Vector3.right*15f;
        object nearby=Unit(1,Model(target).transform.position);Set(target,"hp",1f);
        ResonanceHit(source,target,10f);
        Assert.That(Hp(nearby),Is.EqualTo(4760f));
        Model(target).transform.position=Vector3.right*50f;CrownTick(4f);
        Assert.That(Hp(nearby),Is.EqualTo(4760f));Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
    }

    [Test]
    public void CrownRecastDoesNotResetOlderParasiteAndPauseDoesNotAdvanceIt()
    {
        CrownReady();CrownCreate();CrownTick(2f);CrownCreate();CrownTick(0f);
        Assert.That(Hp(target),Is.EqualTo(4900f));CrownTick(1f);
        Assert.That(Hp(target),Is.EqualTo(4560f));CrownTick(2f);
        Assert.That(Hp(target),Is.EqualTo(4220f));Assert.That(Get(source,"traitActivations"),Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CrownSurvivesCasterDeathAndCancelsOnBattleReset(bool reset)
    {
        CrownReady();CrownCreate();
        if(reset)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else Set(source,"alive",false);
        CrownTick(4f);Assert.That(Hp(target),Is.EqualTo(reset?5000f:4610f));
        CrownTick(1f);Assert.That(Crowns.Count,Is.Zero);
    }

    [Test]
    public void CrownDamageIsAttackScaledWithNoCurrentHealthDamage()
    {
        CrownReady();Set(target,"maxHp",100000f);Set(target,"hp",100000f);CrownCreate(.6f);CrownTick(10f);
        Assert.That(Hp(target),Is.EqualTo(100000f-234f).Within(.01f));
    }

    [TestCase(false,420f)]
    [TestCase(true,350f)]
    public void CrownVersusBonusCountsOnlySuccessfulErosionHits(bool blocked,float expected)
    {
        CrownReady(MushroomCombatRules.VersusTraitId);CrownCreate();
        if(blocked)Set(target,"ninjaUntargetableTimer",1f);
        CrownTick(1f);Set(target,"ninjaUntargetableTimer",0f);CrownTick(2f);
        Assert.That(5000f-Hp(target),Is.EqualTo(expected));
    }

    [Test]
    public void CrownSpeciesPoisonHealingAndDeathSeedKeepTheirOwnTriggers()
    {
        CrownReady(MushroomCombatRules.DoksirumTraitId);CrownCreate();CrownTick(3f);
        var poison=((CombatStatusState)Get(target,"combatStatuses")).Get(CombatStatusKind.Poison);
        Assert.That(poison.stacks,Is.EqualTo(3));Assert.That(poison.magnitude,Is.EqualTo(20f));
        Assert.That(poison.remaining,Is.EqualTo(3f));
        ReaperCall("ClearMushroomCrowns");((IList)Get(source,"activeTraitIds")).Clear();
        Trait(MushroomCombatRules.LumirumTraitId);Set(source,"hp",1000f);CrownCreate();CrownTick(2f);
        Assert.That(Hp(source),Is.EqualTo(1000f));CrownTick(1f);Assert.That(Hp(source),Is.EqualTo(1080f));
        ReaperCall("ClearMushroomCrowns");((IList)Get(source,"activeTraitIds")).Clear();
        Trait(MushroomCombatRules.KingmoreTraitId);CrownCreate();CrownTick(1f);
        Assert.That(Get(target,"mushroomSporeMarkSource"),Is.SameAs(source));
        Assert.That(Get(target,"mushroomSporeMarkTimer"),Is.EqualTo(4f));
        Assert.That(Get(target,"mushroomSporeChildDamage"),Is.EqualTo(27.5f));
        Assert.That(Get(target,"mushroomSporeChildDelay"),Is.EqualTo(1.2f));
    }

    [Test]
    public void CrownPixelEffectFollowsGrowsPausesAndCleansUp()
    {
        CrownReady();typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,false);
        CrownCreate();var crown=Crowns[0];var fx=(PixelParasiticCrown)crown.GetType().GetField("effect").GetValue(crown);
        fx.transform.SetParent(null,true);foreach(var ps in fx.GetComponentsInChildren<ParticleSystem>())ps.Play();
        CrownTick(.5f);var cap=fx.GetComponentInChildren<SpriteRenderer>();Vector3 early=cap.transform.localScale;
        Vector3 at=cap.transform.position;Model(target).transform.position+=Vector3.right*3f;
        CrownTick(1f);Assert.That(cap.transform.position.x-at.x,Is.EqualTo(3f).Within(.01f));
        Assert.That(cap.transform.localScale.x,Is.GreaterThan(early.x));
        Assert.That(cap.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Vector3 paused=cap.transform.localScale;CrownTick(0f);Assert.That(cap.transform.localScale,Is.EqualTo(paused));
        CrownTick(1.5f);CrownTick(.2f);Assert.That(cap.enabled,Is.False);
        Assert.That(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),Is.EqualTo(72));
        ReaperCall("ClearMushroomCrowns");Assert.That(fx==null,Is.True);Assert.That(Crowns.Count,Is.Zero);
    }
}
