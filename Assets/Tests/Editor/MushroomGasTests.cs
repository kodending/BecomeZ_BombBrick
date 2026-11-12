using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void GasReady(string intrinsic=null)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_versus"));
        Set(source,"skillId",MushroomCombatRules.SkillId);
        if(intrinsic!=null)Trait(intrinsic);
    }
    private void GasCreate(object caster=null,Vector3? center=null,float scale=1f,bool echo=false)=>
        ReaperCall("CreateMushroomGas",caster??source,center??Model(target).transform.position,
            GameMasterData.Skill(MushroomCombatRules.SkillId),scale,1f,echo,0,null,null,0);
    private void GasTick(float dt)=>ReaperCall("UpdateMushroomGasZones",dt);
    private IList GasZones=>(IList)typeof(MonsterAutoBattlePrototype).GetField("mushroomGasZones",Private).GetValue(battle);

    [Test]
    public void GasWaitsHalfSecondThenDealsEightTicksAndExpires()
    {
        GasReady();GasCreate();Assert.That(Hp(target),Is.EqualTo(5000f));
        GasTick(.49f);Assert.That(Hp(target),Is.EqualTo(5000f));
        GasTick(.01f);Assert.That(Hp(target),Is.EqualTo(4980f));
        GasTick(3.5f);Assert.That(Hp(target),Is.EqualTo(4840f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(8));Assert.That(GasZones.Count,Is.Zero);
        GasTick(10f);Assert.That(Hp(target),Is.EqualTo(4840f));
    }

    [Test]
    public void GasStaysAtImpactAndOnlyDamagesEnemiesCurrentlyInside()
    {
        GasReady();Vector3 center=Model(target).transform.position;GasCreate();
        GasTick(.5f);Model(target).transform.position=Vector3.right*30f;
        GasTick(1f);Assert.That(Hp(target),Is.EqualTo(4980f));
        object newcomer=Unit(1,center);Model(target).transform.position=center;
        GasTick(.5f);Assert.That(Hp(target),Is.EqualTo(4960f));Assert.That(Hp(newcomer),Is.EqualTo(4980f));
        Assert.That(Hp(source),Is.EqualTo(5000f));
    }

    [TestCase(false,160f)]
    [TestCase(true,320f)]
    public void GasOverlapSharesGateOnlyForSameCaster(bool otherCaster,float expected)
    {
        GasReady();GasCreate();object caster=otherCaster?Unit(0,Vector3.zero):source;
        GasCreate(caster);GasTick(4f);
        Assert.That(Hp(target),Is.EqualTo(5000f-expected));
    }

    [Test]
    public void GasStaggeredEchoCannotDoubleDamageWithinHalfSecond()
    {
        GasReady();GasCreate();GasTick(.25f);GasCreate(scale:.6f,echo:true);
        GasTick(.25f);Assert.That(Hp(target),Is.EqualTo(4980f));
        GasTick(.25f);Assert.That(Hp(target),Is.EqualTo(4980f));
        GasTick(.25f);Assert.That(Hp(target),Is.EqualTo(4960f));
    }

    [TestCase(MushroomCombatRules.VersusTraitId,212f)]
    [TestCase(MushroomCombatRules.DoksirumTraitId,220f)]
    public void GasIntrinsicDamageKeepsBoundedChainOrThreePoisonTicks(string intrinsic,float total)
    {
        GasReady(intrinsic);GasCreate();GasTick(4f);
        Assert.That(Hp(target),Is.EqualTo(5000f-total).Within(.01f));
    }

    [Test]
    public void GasHealingIsDistributedAcrossEightTicksWithoutOverlappingTwice()
    {
        GasReady(MushroomCombatRules.LumirumTraitId);
        object ally=Unit(0,Model(target).transform.position);Set(ally,"hp",1000f);
        GasCreate();GasCreate();GasTick(.5f);Assert.That(Hp(ally),Is.EqualTo(1013.125f));
        GasTick(3.5f);Assert.That(Hp(ally),Is.EqualTo(1105f));
    }

    [Test]
    public void GasSeedRetainsChildBurstDamageRadiusAndDelay()
    {
        GasReady(MushroomCombatRules.KingmoreTraitId);GasCreate();GasTick(.5f);
        Assert.That(Get(target,"mushroomSporeMarkSource"),Is.SameAs(source));
        Assert.That(Get(target,"mushroomSporeMarkTimer"),Is.EqualTo(4f));
        Assert.That(Get(target,"mushroomSporeChildDamage"),Is.EqualTo(27.5f));
        Assert.That(Get(target,"mushroomSporeChildDelay"),Is.EqualTo(1.2f));
    }

    [Test]
    public void GasDoesNotDamageOrSeedUntargetableEnemies()
    {
        GasReady(MushroomCombatRules.KingmoreTraitId);Set(target,"ninjaUntargetableTimer",1f);
        GasCreate();GasTick(.5f);Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(Get(target,"mushroomSporeMarkSource"),Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GasSurvivesCasterDeathAndCleansUpOnBattleGenerationChange(bool nextBattle)
    {
        GasReady();GasCreate();
        if(nextBattle)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else Set(source,"alive",false);
        GasTick(1f);Assert.That(Hp(target),Is.EqualTo(nextBattle?5000f:4960f));
        Assert.That(GasZones.Count,Is.EqualTo(nextBattle?0:1));
        GasTick(4f);Assert.That(GasZones.Count,Is.Zero);
    }

    [Test]
    public void GasResetRemovesCloudAndAllowsNewBattleDamage()
    {
        GasReady();GasCreate();GasTick(.5f);ReaperCall("ClearMushroomGasZones");
        Assert.That(GasZones.Count,Is.Zero);GasCreate();GasTick(.5f);
        Assert.That(Hp(target),Is.EqualTo(4960f));
    }

    [Test]
    public void GasParticlesDriftInsideRadiusAndZeroDeltaKeepsPoseAndDamage()
    {
        GasReady();GasCreate();
        var fx=(PixelMushroomGas)GasZones[0].GetType().GetField("effect").GetValue(GasZones[0]);
        // The fixture's battle host is inactive; native particles need an active hierarchy.
        fx.transform.SetParent(null,true);
        var ps=fx.GetComponentsInChildren<ParticleSystem>(true);
        foreach(var particles in ps)particles.Play();
        GasTick(.4f);
        Assert.That(ps.Length,Is.EqualTo(2));Assert.That(fx.GetComponentsInChildren<SpriteRenderer>().Length,Is.Zero);
        var first=new ParticleSystem.Particle[48];Assert.That(ps[0].GetParticles(first),Is.EqualTo(48));
        GasTick(0f);Assert.That(Hp(target),Is.EqualTo(5000f));
        var paused=new ParticleSystem.Particle[48];ps[0].GetParticles(paused);
        Assert.That(paused[0].position,Is.EqualTo(first[0].position));
        GasTick(1f);var moved=new ParticleSystem.Particle[48];ps[0].GetParticles(moved);
        Assert.That(moved[0].position,Is.Not.EqualTo(first[0].position));
        foreach(var p in moved)
        {
            Vector3 delta=p.position-Model(target).transform.position;delta.y=0f;
            Assert.That(delta.magnitude,Is.LessThanOrEqualTo(2f*MonsterAutoBattlePrototype.TileSize));
        }
        foreach(var renderer in fx.GetComponentsInChildren<ParticleSystemRenderer>())
            Assert.That(renderer.sharedMaterial.mainTexture.filterMode,Is.EqualTo(FilterMode.Point));
    }

    [Test]
    public void GasCastCreatesAreaOnlyAfterProjectileArrival()
    {
        GasReady();cast=(IEnumerator)ReaperCall("ResolveSporeColony",source,target,
            GameMasterData.Skill(MushroomCombatRules.SkillId),1f,false,0);
        Assert.That(cast.MoveNext(),Is.True);Assert.That(GasZones.Count,Is.Zero);
        var projectile=Object.FindObjectOfType<PixelStarProjectile>();
        Assert.That(projectile.transform.GetChild(0).localScale.x,
            Is.EqualTo(PixelStarProjectile.VisualScale(false)*1.25f*2f).Within(.0001f));
        var basic=(PixelStarProjectile)ReaperCall("EmitMushroomSpore",source,target,(System.Action)null);
        Assert.That(basic.transform.GetChild(0).localScale,Is.EqualTo(projectile.transform.GetChild(0).localScale));
        Assert.That(basic.FlightDuration,Is.EqualTo(projectile.FlightDuration));
        UnityEngine.Object.DestroyImmediate(basic.gameObject);
        Arrive(projectile);
        Assert.That(cast.MoveNext(),Is.False);Assert.That(GasZones.Count,Is.EqualTo(1));
        GasTick(.5f);Assert.That(Hp(target),Is.EqualTo(4980f));
    }
}
