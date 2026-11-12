using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityMushroomSkillDensityTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private MonkeyArcTests fixture;
    private bool active,enabled;
    private CombatReadabilityPreset preset;
    private UnityEngine.Random.State random;
    private CombatSkillClock stage;

    [SetUp] public void SetUp()
    {
        active=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;preset=CombatReadabilityPreview.Preset;
        random=UnityEngine.Random.state;
        fixture=new MonkeyArcTests();fixture.SetUp();
        CombatEffectTestConfiguration.Active=CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
    }
    [TearDown] public void TearDown()
    {
        stage?.End();fixture?.TearDown();
        CombatEffectTestConfiguration.Active=active;CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=preset;
        UnityEngine.Random.state=random;
    }

    [TestCase(CombatReadabilityPreset.Current,true,true,1f)]
    [TestCase(CombatReadabilityPreset.Compact,true,true,1f)]
    [TestCase(CombatReadabilityPreset.Local,true,true,.6f)]
    [TestCase(CombatReadabilityPreset.Local,false,true,1f)]
    [TestCase(CombatReadabilityPreset.Local,true,false,1f)]
    public void SustainedCloudLeavesItsAreaDriftSizeAndLifeIntact(CombatReadabilityPreset mode,bool test,bool selected,float opacity)
    {
        var fx=PixelMushroomGas.Play(new Vector3(2f,0f,1f),2f,4f,Color.magenta,null);
        CombatReadabilityPreview.Enabled=false;fx.Pose(.7f);
        var cloud=Get<ParticleSystem>(fx,"cloud");var motes=Get<ParticleSystem>(fx,"motes");
        var before=Read(cloud);var beforeMotes=Read(motes);
        CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=selected;CombatReadabilityPreview.Preset=mode;
        fx.Pose(.7f);
        AssertParticles(before,Read(cloud),opacity);AssertParticles(beforeMotes,Read(motes),opacity);
        Assert.That(Get<float>(fx,"radius"),Is.EqualTo(2f));Assert.That(Get<float>(fx,"duration"),Is.EqualTo(4f));
        Assert.That(fx.GetComponentsInChildren<SpriteRenderer>().Length,Is.Zero);
    }

    [TestCase(false,3,.2f)] [TestCase(true,11,.3f)]
    public void RepeatedTicksClearSoonerWhileStrongResonanceKeepsItsFullAccent(bool strong,int count,float lifetime)
    {
        PixelMushroomGas.Hit(Vector3.zero,null,Color.green,strong,recurringGas:true);
        var ps=Object.FindObjectsOfType<ParticleSystem>().Single(p=>p.name==(strong?"Spore Resonance Hit":"Mushroom Gas Tick"));
        var particles=Read(ps);Assert.That(particles.Length,Is.EqualTo(count));
        foreach(var particle in particles)Assert.That(particle.startLifetime,Is.EqualTo(lifetime).Within(.0001f));
    }

    [Test]
    public void SharedBreathContactKeepsItsOriginalParticles()
    {
        PixelWyvernBreath.Hit(Vector3.zero,null,0,false);
        var ps=Object.FindObjectsOfType<ParticleSystem>().Single(p=>p.name=="Mushroom Gas Tick");
        var particles=Read(ps);Assert.That(particles.Length,Is.EqualTo(5));
        foreach(var particle in particles)Assert.That(particle.startLifetime,Is.EqualTo(.3f).Within(.0001f));
    }

    [Test]
    public void ResonanceBurstAndExclusiveStageKeepTheirFullPresentation()
    {
        var fx=PixelMushroomGas.Play(Vector3.zero,2f,4f,Color.green,null);
        fx.Resonate(Vector3.zero,.5f,1f);
        float at=.5f+MushroomRank3Rules.ResonanceWindup+.08f;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;fx.Pose(at);
        var resonance=Get<ParticleSystem>(fx,"resonance");var before=Read(resonance);
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;fx.Pose(at);
        AssertParticles(before,Read(resonance),1f);Assert.That(before.Length,Is.EqualTo(28));
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;fx.Pose(.7f);var cloud=Get<ParticleSystem>(fx,"cloud");
        var baseline=Read(cloud);
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;stage=new CombatSkillClock(null);stage.Hold();fx.Pose(.7f);
        Assert.That(CombatReadabilityPreview.RefinedSkills,Is.False);AssertParticles(baseline,Read(cloud),1f);
    }

    [TestCase(CombatReadabilityPreset.Current)] [TestCase(CombatReadabilityPreset.Local)]
    public void CloudStillWaitsHalfASecondDealsEightTicksAndExpires(CombatReadabilityPreset mode)
    {
        CombatReadabilityPreview.Preset=mode;
        var battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");object source=Get<object>(fixture,"source"),target=Get<object>(fixture,"target");
        source.GetType().GetField("definition").SetValue(source,Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("versus"));
        Call(battle,"CreateMushroomGas",source,((GameObject)target.GetType().GetField("model").GetValue(target)).transform.position,
            GameMasterData.Skill(MushroomCombatRules.SkillId),1f,1f,false,0,null,null,0);
        Call(battle,"UpdateMushroomGasZones",.49f);Assert.That(Get<float>(target,"hp"),Is.EqualTo(5000f));
        Call(battle,"UpdateMushroomGasZones",.01f);Assert.That(Get<float>(target,"hp"),Is.EqualTo(4980f));
        Call(battle,"UpdateMushroomGasZones",3.5f);Assert.That(Get<float>(target,"hp"),Is.EqualTo(4840f));
        Assert.That(Get<int>(source,"skillHits"),Is.EqualTo(8));
        Assert.That(Get<IList>(battle,"mushroomGasZones").Count,Is.Zero);
    }

    private static ParticleSystem.Particle[] Read(ParticleSystem ps)
    {
        var particles=new ParticleSystem.Particle[ps.main.maxParticles];int count=ps.GetParticles(particles);
        Array.Resize(ref particles,count);return particles;
    }
    private static void AssertParticles(ParticleSystem.Particle[] before,ParticleSystem.Particle[] after,float opacity)
    {
        Assert.That(after.Length,Is.EqualTo(before.Length));Assert.That(before.Length,Is.GreaterThan(0));
        for(int i=0;i<before.Length;i++)
        {
            Assert.That(after[i].position,Is.EqualTo(before[i].position));
            Assert.That(Vector3.Distance(after[i].startSize3D,before[i].startSize3D*CombatReadabilityPreview.EffectVisualScale),Is.LessThan(.0001f));
            Assert.That(after[i].startLifetime,Is.EqualTo(before[i].startLifetime));
            Assert.That((float)after[i].startColor.a,Is.EqualTo(before[i].startColor.a*opacity).Within(2f));
        }
    }
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Fields).GetValue(target);
    private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Fields).Invoke(target,args);
}
