using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void ResonanceReady()
    {
        GasReady();Trait(MushroomRank3Rules.SporeResonance);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
    }
    private float ResonanceHit(object attacker,object victim,float amount=1f,bool skill=false,bool basic=true)=>
        (float)ReaperCall("ApplyDamage",attacker,victim,amount,skill,basic,false,false,0f,0f,1f,false,false,false);
    private int Resonances(object zone=null)=>(int)(zone??GasZones[0]).GetType()
        .GetField("resonanceCount").GetValue(zone??GasZones[0]);

    [TestCase(false)]
    [TestCase(true)]
    public void ResonanceRespondsToCasterOrAllyAfterWindupUsingMushroomAttack(bool ally)
    {
        ResonanceReady();GasCreate();object attacker=ally?Unit(0,Vector3.zero):source;
        Set(attacker,"damage",ally?900f:100f);
        ResonanceHit(attacker,target);GasTick(.07f);Assert.That(Hp(target),Is.EqualTo(4999f));
        GasTick(0f);Assert.That(Hp(target),Is.EqualTo(4999f));
        GasTick(.01f);Assert.That(Hp(target),Is.EqualTo(4959f));
        Assert.That(Resonances(),Is.EqualTo(1));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(40f));
    }

    [Test]
    public void ResonanceIgnoresSkillDotEnemyHitsAndUntargetableVictims()
    {
        ResonanceReady();GasCreate();
        ResonanceHit(source,target,1f,true,true);ResonanceHit(source,target,1f,true,false);
        object friendly=Unit(0,Model(target).transform.position);ResonanceHit(target,friendly);
        Set(target,"ninjaUntargetableTimer",1f);ResonanceHit(source,target);
        Set(target,"ninjaUntargetableTimer",0f);GasTick(.5f);
        Assert.That(Resonances(),Is.Zero,"Gas DoT and damage from enemies cannot trigger it");
    }

    [Test]
    public void ResonanceSharesCooldownAcrossAlliesAndOverlappingClouds()
    {
        ResonanceReady();GasCreate();GasCreate();object ally=Unit(0,Vector3.zero);
        ResonanceHit(source,target);ResonanceHit(ally,target);GasTick(.08f);
        Assert.That(Hp(target),Is.EqualTo(4958f));
        GasTick(.61f);ResonanceHit(ally,target);
        Assert.That(Resonances()+Resonances(GasZones[1]),Is.EqualTo(1));
        GasTick(.01f);ResonanceHit(ally,target);GasTick(.08f);
        Assert.That(Resonances()+Resonances(GasZones[1]),Is.EqualTo(2));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(100f)); // two bursts + one DoT tick
    }

    [Test]
    public void ResonanceCapsAtSixEvenIfCloudDurationIsExtended()
    {
        ResonanceReady();GasCreate();object gas=GasZones[0];
        gas.GetType().GetField("duration").SetValue(gas,10f);
        for(int i=0;i<9;i++){ResonanceHit(source,target);GasTick(.7f);}
        Assert.That(Resonances(),Is.EqualTo(6));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(400f));
    }

    [Test]
    public void ResonanceCanTriggerOnShieldHitAndKillingBlowAtOriginalPosition()
    {
        ResonanceReady();GasCreate();Set(target,"itemShield",20f);
        ResonanceHit(source,target,10f);Assert.That(Resonances(),Is.EqualTo(1));
        GasTick(.7f);
        object nearby=Unit(1,Model(target).transform.position+Vector3.forward*.6f);
        Set(target,"hp",1f);ResonanceHit(source,target,100f);
        Model(target).transform.position=Vector3.right*50f;
        GasTick(.08f);Assert.That(Hp(nearby),Is.EqualTo(4960f));
    }

    [Test]
    public void ResonanceBurstIsIntersectionOfGasAndOneTileAndAllowsNewEntrants()
    {
        ResonanceReady();Model(target).transform.position=Vector3.right*3.3f;GasCreate(center:Vector3.zero);
        object inside=Unit(1,Vector3.right*2.6f),outsideGas=Unit(1,Vector3.right*4.1f),
            outsideBurst=Unit(1,Vector3.zero),newcomer=Unit(1,Vector3.right*20f);
        ResonanceHit(source,target);
        Model(newcomer).transform.position=Vector3.right*2.8f;
        Model(inside).transform.position=Vector3.right*20f;GasTick(.08f);
        Assert.That(Hp(target),Is.EqualTo(4959f));Assert.That(Hp(newcomer),Is.EqualTo(4960f));
        Assert.That(Hp(inside),Is.EqualTo(5000f));Assert.That(Hp(outsideGas),Is.EqualTo(5000f));
        Assert.That(Hp(outsideBurst),Is.EqualTo(5000f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResonancePendingBurstSurvivesDeathAndCancelsOnReset(bool reset)
    {
        ResonanceReady();GasCreate();ResonanceHit(source,target);
        if(reset)ReaperCall("ClearMushroomGasZones");else Set(source,"alive",false);
        GasTick(.1f);Assert.That(Hp(target),Is.EqualTo(reset?4999f:4959f));
        Assert.That(GasZones.Count,Is.EqualTo(reset?0:1));
    }

    [Test]
    public void ResonanceReservedBeforeExpiryFinishesButExpiredCloudCannotRetrigger()
    {
        ResonanceReady();GasCreate();GasTick(3.96f);ResonanceHit(source,target);
        GasTick(.08f);Assert.That(Get(source,"skillDamage"),Is.EqualTo(200f));
        ResonanceHit(source,target);GasTick(.2f);
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(200f));
        GasTick(.3f);Assert.That(GasZones.Count,Is.Zero);
    }

    [Test]
    public void ResonanceStillWaitsForSporeProjectileAndEchoHasNoExtraProcBudget()
    {
        ResonanceReady();cast=(IEnumerator)ReaperCall("ResolveSporeColony",source,target,
            GameMasterData.Skill(MushroomCombatRules.SkillId),1f,false,0);
        Assert.That(cast.MoveNext(),Is.True);Assert.That(GasZones.Count,Is.Zero);
        Arrive(Object.FindObjectOfType<PixelStarProjectile>());Assert.That(cast.MoveNext(),Is.False);
        Assert.That((bool)GasZones[0].GetType().GetField("resonance").GetValue(GasZones[0]),Is.True);
        ReaperCall("ClearMushroomGasZones");GasCreate(echo:true);ResonanceHit(source,target);GasTick(.1f);
        Assert.That(Hp(target),Is.EqualTo(4999f));Assert.That(Resonances(),Is.Zero);
    }
}
