using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private PixelUnitShield ShieldFx(object unit)=>(PixelUnitShield)Get(unit,"shieldVisual");
    private void ShieldVisualSetup()
    {
        typeof(MonsterAutoBattlePrototype).GetField("battleStarted",Private).SetValue(battle,true);
        foreach(var unit in new[]{source,target})Model(unit).AddComponent<SpriteRenderer>().sprite=PixelStarProjectile.BeanSprite;
    }

    [TestCase("crocodileShield")]
    [TestCase("griffinShield")]
    [TestCase("raptorShield")]
    [TestCase("kingKongShield")]
    [TestCase("bearMountainShield")]
    [TestCase("spiritLifeShield")]
    [TestCase("itemShield")]
    public void UnitShieldVisualCoversEveryAbsorbPoolWithoutChangingItsHealth(string field)
    {
        ShieldVisualSetup();float hp=Hp(target);Set(target,field,100f);
        GriffinCall("UpdateUnitShieldVisual",target);var fx=ShieldFx(target);
        Assert.That(fx,Is.Not.Null);Assert.That(Hp(target),Is.EqualTo(hp));Assert.That(Get(target,field),Is.EqualTo(100f));
        ((CombatStatusState)Get(target,"combatStatuses")).ApplyShield(3f,30f);
        GriffinCall("UpdateUnitShieldVisual",target);Assert.That(ShieldFx(target),Is.SameAs(fx));
        Set(target,field,0f);GriffinCall("UpdateUnitShieldVisual",target);Assert.That(ShieldFx(target),Is.SameAs(fx));
        ((CombatStatusState)Get(target,"combatStatuses")).Tick(4f);
        GriffinCall("UpdateUnitShieldVisual",target);
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,.21f);
        GriffinCall("UpdateUnitShieldVisual",target);Assert.That(ShieldFx(target)==null,Is.True);
    }

    [Test]
    public void ShieldHidesAfterCreationAndOnlyWakesForFreshDirectionalHits()
    {
        ShieldVisualSetup();Set(target,"itemShield",100f);
        GriffinCall("UpdateUnitShieldVisual",target);var fx=ShieldFx(target);
        var shell=fx.GetComponentInChildren<MeshRenderer>();
        Assert.That(shell.enabled,Is.True);
        fx.Pose(.51f,true);
        Assert.That(shell.enabled,Is.False,"An idle shield must stop drawing, while its health remains.");
        Assert.That(Get(target,"itemShield"),Is.EqualTo(100f));
        Vector3 right=shell.transform.right;
        fx.Impact(fx.Center-right*3f,.6f);
        var left=shell.sharedMaterial.GetVectorArray("_Hits")[0];
        Assert.That(shell.enabled,Is.True);Assert.That(left.x,Is.LessThan(0));
        fx.Impact(fx.Center+right*3f,.7f);
        var hits=shell.sharedMaterial.GetVectorArray("_Hits");
        Assert.That(hits[0],Is.EqualTo(left),"Opposite hits may overlap without moving the earlier patch.");
        Assert.That(hits[1].x,Is.GreaterThan(0));
        fx.Pose(.7f,true);Assert.That(shell.enabled,Is.True);
        fx.Pose(1.11f,true);Assert.That(shell.enabled,Is.False);
        fx.Pose(1.2f,false);fx.Pose(1.25f,true);
        Assert.That(shell.enabled,Is.True,"A new shield during the old shield's fade gets a fresh reveal.");
        Assert.That(shell.sharedMaterial.GetFloat("_Age"),Is.Zero);
        foreach(var hit in shell.sharedMaterial.GetVectorArray("_Hits"))Assert.That(hit.w,Is.Zero);
        fx.Pose(1.8f,true);fx.Pose(1.9f,false);
        fx.Impact(fx.Center-right*3f,2f);fx.Pose(2f,true);
        Assert.That(shell.sharedMaterial.GetVectorArray("_Hits")[0].w,Is.EqualTo(1),
            "A hit on a newly granted shield must survive the same frame's shield update.");
        Assert.That(shell.sharedMaterial.GetFloat("_Age"),Is.Zero);
    }

    [Test]
    public void ShieldDiscoveredOnImpactSkipsWholeBodyReveal()
    {
        ShieldVisualSetup();Set(target,"itemShield",100f);
        GriffinCall("ShowUnitShieldHit",source,target);var fx=ShieldFx(target);
        var shell=fx.GetComponentInChildren<MeshRenderer>();
        Assert.That(shell.enabled,Is.True);
        Assert.That(shell.sharedMaterial.GetFloat("_Age"),Is.GreaterThanOrEqualTo(.5f));
        fx.Pose(.41f,true);Assert.That(shell.enabled,Is.False);
    }

    [Test]
    public void UnitShieldHitReactsOnlyToAbsorbedDamageAndKeepsExistingDamageMath()
    {
        ShieldVisualSetup();Set(target,"defense",0f);Set(target,"itemShield",30f);
        float hp=Hp(target);
        GriffinCall("ApplyDamage",source,target,50f,false,false,false,false,0f,0f,1f,false,false,false);
        Assert.That(Hp(target),Is.EqualTo(hp-20f).Within(.01f));Assert.That(Get(target,"itemShield"),Is.EqualTo(0));
        Assert.That(ShieldFx(target),Is.Not.Null);
        GriffinCall("ClearUnitShieldVisual",target);Set(target,"itemShield",100f);
        GriffinCall("ApplyDamage",source,target,10f,false,false,false,false,0f,0f,1f,true,false,false);
        Assert.That(ShieldFx(target),Is.Null);Assert.That(Get(target,"itemShield"),Is.EqualTo(100f));
    }

    [Test]
    public void UnitShieldFollowsItsOwnerPausesAndClearsOnDeathOrBattleReset()
    {
        ShieldVisualSetup();((CombatStatusState)Get(source,"combatStatuses")).ApplyShield(5f,100f);
        GriffinCall("UpdateUnitShieldVisual",source);var fx=ShieldFx(source);Vector3 center=fx.Center;
        Model(source).transform.position+=new Vector3(3,2,-1);
        typeof(PixelUnitShield).GetMethod("LateUpdate",Private).Invoke(fx,null);
        Assert.That(fx.Center-center,Is.EqualTo(new Vector3(3,2,-1)));
        GriffinCall("UpdateUnitShieldVisual",source);Assert.That(ShieldFx(source),Is.SameAs(fx));
        Set(source,"alive",false);GriffinCall("UpdateUnitShieldVisual",source);Assert.That(ShieldFx(source),Is.Null);
        Set(source,"alive",true);GriffinCall("UpdateUnitShieldVisual",source);Assert.That(ShieldFx(source),Is.Not.Null);
        GriffinCall("ResetFamilyTranscendenceStates");Assert.That(ShieldFx(source),Is.Null);
    }

    [TestCase(GriffinRank3Rules.TyphoonDive)]
    [TestCase(GriffinRank3Rules.RaptorHunt)]
    [TestCase(GriffinRank3Rules.RoyalDescent)]
    public void GriffinStormOrbDoesNotGrantAbsorbHealthOrHexShield(string trait)
    {
        GriffinSetup();Set(source,"augmentTranscended",true);Trait(trait);
        Set(source,"hp",3000f);
        WindWaveStart();
        Assert.That(Get(source,"griffinSkyfalling"),Is.False);
        WindTick(GriffinStormOrbRules.Burst-.01f);
        Assert.That(Get(source,"skillHits"),Is.Zero);
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));Assert.That(Hp(source),Is.EqualTo(3000f));
        Assert.That(ShieldFx(source),Is.Null);Assert.That((float)GriffinCall("TotalVisibleShield",source),Is.Zero);
        Assert.That(FamilyTranscendenceRules.CanActivate("griffin",true),Is.True);
    }
}
