using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private object RainCast=>Get(source,"wyvernBreath");
    private Vector3[] RainPoints(object rain)=>(Vector3[])rain.GetType().GetField("rainPoints").GetValue(rain);
    private void RainSetup(string id="vivre",string intrinsic=null)
    {Trait(WyvernRank3Rules.WingSweep);WyvernSetup(id,intrinsic);}

    [Test]
    public void WyvernRainDamagesOnlyOnSixLandingsAndNeverHurtsAllies()
    {
        RainSetup();object ally=Unit(0,Vector3.right*4),outside=Unit(1,Vector3.right*9);
        BreathActionTick(1.149f);Assert.That(Hp(target),Is.EqualTo(5000));
        BreathActionTick(.001f);Assert.That(Hp(target),Is.EqualTo(4930));
        BreathActionTick(.259f);Assert.That(Hp(target),Is.EqualTo(4930));
        BreathActionTick(.001f);Assert.That(Hp(target),Is.EqualTo(4860));
        BreathActionTick(1.04f);Assert.That(Hp(target),Is.EqualTo(4580));
        Assert.That(Hp(ally),Is.EqualTo(5000));Assert.That(Hp(outside),Is.EqualTo(5000));
        Assert.That(RainCast,Is.Null);BreathTick(2f);Assert.That(Hp(target),Is.EqualTo(4580));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));
    }

    [Test]
    public void WyvernRainDistributesRandomTargetsAndLocksAllMarkedGroundPositions()
    {
        object second=Unit(1,new Vector3(4.2f,0,5.8f)),third=Unit(1,new Vector3(4.2f,0,-5.8f));
        RainSetup();var rain=RainCast;var points=RainPoints(rain).ToArray();
        foreach(var enemy in new[]{target,second,third})
            Assert.That(points.Count(point=>Vector3.Distance(point,Model(enemy).transform.position)<1.11f),Is.EqualTo(2));
        Assert.That(points.All(point=>Vector3.Distance(point,Vector3.right*4.2f)<=4f*MonsterAutoBattlePrototype.TileSize+.0001f),Is.True);
        foreach(var enemy in new[]{target,second,third})Model(enemy).transform.position=Vector3.forward*30;
        object newcomer=Unit(1,points[0]);Set(target,"alive",false);BreathActionTick(2.45f);
        Assert.That(Hp(second),Is.EqualTo(5000));Assert.That(Hp(newcomer),Is.LessThan(5000));
        Assert.That(RainPoints(rain),Is.EqualTo(points));
    }

    [TestCase("vivre",WyvernCombatRules.VivreTraitId,CombatStatusKind.Poison,1)]
    [TestCase("frovire",WyvernCombatRules.FrovireTraitId,CombatStatusKind.ComboChilled,2)]
    public void WyvernRainAppliesElementStacksOnlyOnceDespiteSixOverlappingHits(string id,string intrinsic,CombatStatusKind kind,int stacks)
    {
        RainSetup(id,intrinsic);BreathActionTick(2.45f);
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(Hp(target),Is.EqualTo(4580));Assert.That(statuses.Get(kind).stacks,Is.EqualTo(stacks));
        Assert.That(statuses.Has(CombatStatusKind.Freeze),Is.False);
    }

    [Test]
    public void WyvernRainPauseFreezeInterruptionAndResetStopTheCorrectClock()
    {
        RainSetup();var statuses=(CombatStatusState)Get(source,"combatStatuses");
        statuses.ApplyTimed(CombatStatusKind.Freeze,2f,target);BreathTick(2f);Assert.That(Hp(target),Is.EqualTo(5000));
        statuses.Remove(CombatStatusKind.Freeze);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);
        BreathTick(2f);Assert.That(Hp(target),Is.EqualTo(5000));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);
        BreathActionTick(1.15f);Assert.That(Hp(target),Is.EqualTo(4930));
        GriffinCall("InterruptPositionalAction",source);BreathActionTick(2f);Assert.That(Hp(target),Is.EqualTo(4580));
        RainSetup();GriffinCall("ClearWyvernBreaths");BreathTick(2f);Assert.That(Hp(target),Is.EqualTo(4580));
    }

    [Test]
    public void WyvernRainFireDoesNotStackOverlappingPatchesAndEndsAfterFourTicks()
    {
        RainSetup("igvire",WyvernCombatRules.IgvireTraitId);BreathActionTick(2.45f);
        Assert.That(Hp(target),Is.EqualTo(4580));Assert.That(RainCast,Is.Null);
        BreathTick(4f);Assert.That(Hp(target),Is.EqualTo(4500));BreathTick(4f);Assert.That(Hp(target),Is.EqualTo(4500));
    }

    [Test]
    public void WyvernRainUsesItsCastRangeAndIsDeflectedByHostileWind()
    {
        RainSetup();var skill=GameMasterData.Skill(WyvernCombatRules.SkillId);
        Assert.That(GriffinCall("SkillActivationDistance",source,target,skill),Is.EqualTo(5.5f*MonsterAutoBattlePrototype.TileSize+.3f));
        Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_pone"));Set(target,"augmentTranscended",true);
        GriffinCall("BeginGriffinCrosswind",target);BreathActionTick(2.45f);
        Assert.That(Hp(target),Is.EqualTo(5000));Assert.That(RainCast,Is.Null);
    }

    [Test]
    public void WyvernRainShadowGrowsToDamageRadiusAndFlightFallsFromUpperRight()
    {
        var camera=new GameObject("Rain Camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0,12,-12);camera.transform.LookAt(Vector3.zero);camera.orthographic=true;camera.orthographicSize=8f;
        Vector3 point=Vector3.zero,offset=PixelWyvernRain.SkyOffset(point,camera);
        Vector3 start=PixelWyvernRain.FlightPoint(point,offset,0,.6f),end=PixelWyvernRain.FlightPoint(point,offset,0,1.15f);
        Vector3 halfway=PixelWyvernRain.FlightPoint(point,offset,0,.875f);
        Assert.That(Vector3.Distance(start,halfway),Is.LessThan(Vector3.Distance(halfway,end)));
        Assert.That(Vector3.Distance(start,halfway)/offset.magnitude,Is.EqualTo(.25f).Within(.001f));
        Assert.That(camera.WorldToViewportPoint(start).x,Is.GreaterThan(camera.WorldToViewportPoint(end).x));
        Assert.That(camera.WorldToViewportPoint(start).y,Is.GreaterThan(1f));
        Assert.That(end,Is.EqualTo(Vector3.up*.12f));
        Vector3[] points=Enumerable.Repeat(point,6).ToArray(),offsets=Enumerable.Repeat(offset,6).ToArray();
        var fx=PixelWyvernRain.Create(null,Vector3.zero,Vector3.right,points,offsets,new bool[6],1.7f,0,()=>0f,()=>true);
        var shadow=fx.GetComponentsInChildren<SpriteRenderer>()[0];fx.Pose(.36f);float early=shadow.transform.localScale.x;
        fx.Pose(1.1499f);Assert.That(shadow.transform.localScale.x,Is.EqualTo(3.4f).Within(.001f));
        Assert.That(shadow.transform.localScale.x,Is.GreaterThan(early));fx.Pose(1.15f);Assert.That(shadow.enabled,Is.False);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void WyvernRainAirborneBodiesAndTrailsAreLargeWhileLandingGeometryStaysUnchanged(int element)
    {
        Vector3 point=Vector3.right*4f,offset=Vector3.up*8f+Vector3.right*5.6f;
        const float radius=1.7f;
        var fx=PixelWyvernRain.Create(null,Vector3.zero,Vector3.right,new[]{point},new[]{offset},
            new bool[1],radius,element,()=>0f,()=>true);
        var body=fx.transform.Find("Falling Breath Clouds").GetComponent<ParticleSystem>();
        var sparks=fx.transform.Find("Falling Breath Fragments").GetComponent<ParticleSystem>();
        var particles=new ParticleSystem.Particle[400];
        fx.Pose(.5f);
        int count=body.GetParticles(particles);Assert.That(count,Is.EqualTo(32));
        Assert.That(particles.Take(count).Max(p=>p.startSize3D.x),Is.GreaterThan(.7f),
            "The upward breath must have a visible body rather than the former sub-.4m clouds.");
        Assert.That(particles.Take(count).Max(p=>p.position.z)-particles.Take(count).Min(p=>p.position.z),
            Is.GreaterThan(.5f));
        count=sparks.GetParticles(particles);
        Assert.That(particles.Take(count).Max(p=>p.startSize3D.x),Is.GreaterThan(.15f));

        float impact=WyvernRank3Rules.RainImpact(0),fallingTime=impact-.2f;
        Vector3 head=PixelWyvernRain.FlightPoint(point,offset,0,fallingTime);
        fx.Pose(fallingTime);
        count=body.GetParticles(particles);Assert.That(count,Is.EqualTo(48));
        Assert.That(particles.Take(count).Max(p=>p.startSize3D.x),Is.GreaterThan(1.1f));
        Assert.That(particles.Take(count).Max(p=>Vector3.Dot(p.position-head,offset.normalized)),
            Is.GreaterThan(5.1f),"The larger falling body needs its full-length matching trail.");
        count=sparks.GetParticles(particles);
        Assert.That(particles.Take(count).Max(p=>p.startSize3D.x),Is.GreaterThan(.2f));

        var shadow=fx.GetComponentsInChildren<SpriteRenderer>()[0];
        fx.Pose(impact-.0001f);
        Assert.That(shadow.transform.localScale.x,Is.EqualTo(radius*2f).Within(.001f));
        Assert.That(PixelWyvernRain.FlightPoint(point,offset,0,impact),Is.EqualTo(point+Vector3.up*.12f));
        fx.Pose(impact+.1f);count=body.GetParticles(particles);
        Assert.That(particles.Take(count).Max(p=>p.startSize3D.x),Is.LessThan(.58f),
            "Projectile enlargement must not enlarge the ground explosion or its damage footprint.");
    }
}
