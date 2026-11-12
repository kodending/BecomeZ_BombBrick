using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=NUnit.Framework.Is;
using Object=UnityEngine.Object;

public sealed class PixelFrogGrandSlamTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly List<PixelFrogGrandSlam> effects=new List<PixelFrogGrandSlam>();
    private readonly List<GameObject> hosts=new List<GameObject>();
    private readonly List<Transform> targets=new List<Transform>();
    private readonly ParticleSystem.Particle[] first=new ParticleSystem.Particle[4096],second=new ParticleSystem.Particle[4096];
    private SpriteRenderer source;
    private Texture2D texture;
    private Sprite sprite;
    private static readonly Vector3 Center=Vector3.right*2f;

    [SetUp] public void Setup()
    {
        var camera=Host("Frog VFX test camera").AddComponent<Camera>();
        camera.transform.rotation=Quaternion.Euler(58f,0f,0f);
        source=Host("Frog VFX caster").AddComponent<SpriteRenderer>();
        texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2f);source.sprite=sprite;
        source.transform.rotation=camera.transform.rotation;source.transform.position=Vector3.left;
        for(int i=0;i<8;i++){var target=Host("Frog captured target "+i).transform;target.position=new Vector3(2f+i%2,0f,i/2*.5f);targets.Add(target);}
    }

    [TearDown] public void Cleanup()
    {
        foreach(var fx in effects)if(fx!=null)fx.Cancel(true);effects.Clear();
        foreach(var host in hosts)if(host!=null)Object.DestroyImmediate(host);hosts.Clear();targets.Clear();
        Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
    }

    [Test]
    public void EveryCaughtEnemyGetsACrispTetherWithoutTheEffectMovingAnyActors()
    {
        var positions=new List<Vector3>();foreach(var target in targets)positions.Add(target.position);
        var fx=Create();fx.Advance(FrogGrandSlamRules.CastContact,Center);
        Assert.That(fx.Particles.particleCount,Is.EqualTo(8*28*2));
        var material=fx.Particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        Assert.That(material.mainTexture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(fx.Particles.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(fx.Particles.isPaused,Is.True);
        fx.Advance(FrogGrandSlamRules.LiftEnd,Center);
        Assert.That(fx.Particles.particleCount,Is.EqualTo(8*(28*2+12)+96));
        for(int i=0;i<targets.Count;i++)Assert.That(targets[i].position,Is.EqualTo(positions[i]));
        Assert.That(source.transform.position,Is.EqualTo(Vector3.left));
        typeof(PixelFrogGrandSlam).GetMethod("Update",Private).Invoke(fx,null);
        Assert.That(fx.Age,Is.EqualTo(FrogGrandSlamRules.LiftEnd),"Only combat advances an active cast's visual time.");
    }

    [Test]
    public void TheLandingCreatesOneExpandingBurstAndItsFragmentsBrakeAfterTheHit()
    {
        var fx=Create();fx.Advance(FrogGrandSlamRules.HitTime,Center);
        int count=fx.Particles.GetParticles(first);
        Assert.That(count,Is.GreaterThan(500).And.LessThanOrEqualTo(fx.Particles.main.maxParticles));
        fx.Advance(FrogGrandSlamRules.HitTime+.02f,Center);fx.Particles.GetParticles(first);
        fx.Advance(FrogGrandSlamRules.HitTime+.04f,Center);fx.Particles.GetParticles(second);
        float early=Vector3.Distance(first[0].position,second[0].position);
        float ring=Vector3.Distance(first[112].position,Center);
        fx.Advance(FrogGrandSlamRules.HitTime+.14f,Center);fx.Particles.GetParticles(first);
        fx.Advance(FrogGrandSlamRules.HitTime+.16f,Center);fx.Particles.GetParticles(second);
        Assert.That(Vector3.Distance(first[0].position,second[0].position),Is.LessThan(early*.65f));
        Assert.That(Vector3.Distance(second[112].position,Center),Is.GreaterThan(ring));
        fx.Cancel();fx.AdvanceEnd(.5f);Assert.That(fx==null,Is.True);
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void TheSameCastClockGivesTheSameLiftAndImpactAtEveryFrameRate(int fps)
    {
        var stepped=Create();var direct=Create();
        foreach(float end in new[]{FrogGrandSlamRules.LiftEnd,FrogGrandSlamRules.HitTime+.08f})
        {
            for(int frame=0;frame/(float)fps<end;frame++)stepped.Advance(frame/(float)fps,Center);
            stepped.Advance(end,Center);direct.Advance(end,Center);
            int count=stepped.Particles.GetParticles(first);Assert.That(direct.Particles.GetParticles(second),Is.EqualTo(count));
            for(int i=0;i<count;i++)
            {Assert.That(first[i].position,Is.EqualTo(second[i].position));Assert.That(first[i].startSize3D,Is.EqualTo(second[i].startSize3D));Assert.That(first[i].startColor,Is.EqualTo(second[i].startColor));}
        }
    }

    [Test]
    public void ExtraSummonedEnemiesAreNotCutOffAtEightOrAllowedToOverflowTheParticleBuffer()
    {
        for(int i=8;i<32;i++)targets.Add(Host("Extra captured enemy "+i).transform);
        var fx=Create();fx.Advance(FrogGrandSlamRules.LiftEnd,Center);
        Assert.That(fx.Particles.particleCount,Is.EqualTo(32*(28*2+12)+96));
        Assert.That(fx.Particles.particleCount,Is.LessThanOrEqualTo(fx.Particles.main.maxParticles));
        fx.Advance(FrogGrandSlamRules.SlamStart+.08f,Center);
        Assert.That(fx.Particles.particleCount,Is.EqualTo(32*(28*2+12+24)+24));
        Assert.That(fx.Particles.particleCount,Is.LessThanOrEqualTo(fx.Particles.main.maxParticles));
    }

    [Test]
    public void ThrownEnemiesLeaveOutwardDownwardStreaksAndEachLandingBurstStaysAtItsImpactPoint()
    {
        targets[0].position=new Vector3(6f,0f,0f);
        var fx=Create();fx.Advance(FrogGrandSlamRules.SlamStart+.08f,Center);
        int count=fx.Particles.GetParticles(first),trailStart=targets.Count*(28*2+12)+24;
        Assert.That(count,Is.EqualTo(trailStart+targets.Count*24));
        Vector3 motion=first[trailStart].position-first[trailStart+22].position;
        Assert.That(motion.x,Is.GreaterThan(.1f));Assert.That(motion.y,Is.LessThan(-.1f),
            "The trailing pixels must show a throw outwards and down, rather than only vertical falling.");
        fx.Advance(FrogGrandSlamRules.HitTime+.04f,Center);
        count=fx.Particles.GetParticles(first);
        Assert.That(count,Is.EqualTo(516+targets.Count*16));
        Vector3 landing=targets[0].position;
        foreach(var target in targets)target.position+=Vector3.right*100f;
        fx.Advance(FrogGrandSlamRules.HitTime+.1f,Center);fx.Particles.GetParticles(second);
        for(int i=516;i<532;i++)Assert.That(Vector3.Distance(second[i].position,landing),Is.LessThan(2.5f),
            "Thrown-target impact fragments must remain at their original landing points after actors resume moving.");
    }

    [Test]
    public void CancellingBeforeContactOnlyFadesAndNeverStartsALandingBurst()
    {
        var fx=Create();fx.Advance(.1f,Center);float age=fx.Age;fx.Cancel();
        fx.Advance(FrogGrandSlamRules.HitTime,Center);fx.AdvanceEnd(.1f);
        Assert.That(fx.Age,Is.EqualTo(age));Assert.That(fx.Particles.particleCount,Is.EqualTo(8*28*2));
        fx.AdvanceEnd(.2f);Assert.That(fx==null,Is.True);
    }

    [Test]
    public void ResultWindowPlaybackFinishesIndependentlyWithFrozenAnchorsAndNoGameplayCallbacks()
    {
        var fx=Create();fx.Advance(FrogGrandSlamRules.LiftEnd,Center);fx.Particles.GetParticles(first);
        var types=new HashSet<System.Type>{typeof(PixelFrogGrandSlam),typeof(CombatEffectEndMarker)};
        CombatEffectEndPlayback.Begin(fx.gameObject,types);fx.Cancel();
        var playback=fx.GetComponent<CombatEffectEndPlayback>();Assert.That(playback,Is.Not.Null);
        source.transform.position+=Vector3.right*100f;foreach(var target in targets)target.position+=Vector3.right*100f;
        typeof(CombatEffectEndPlayback).GetMethod("Advance",Private).Invoke(playback,new object[]{0f});
        int count=fx.Particles.GetParticles(second);
        for(int i=0;i<count;i++)Assert.That(second[i].position,Is.EqualTo(first[i].position));
        typeof(CombatEffectEndPlayback).GetMethod("Advance",Private).Invoke(playback,new object[]{1f});
        Assert.That(fx==null,Is.True,"The result must finish existing pixels without waiting on any living target.");
    }

    [Test]
    public void WarmedCastAndLandingUpdatesDoNotAllocateManagedMemory()
    {
        var fx=Create();TestDelegate work=()=>{fx.Advance(.4f,Center);fx.Advance(FrogGrandSlamRules.LiftEnd,Center);fx.Advance(FrogGrandSlamRules.SlamStart+.08f,Center);fx.Advance(FrogGrandSlamRules.HitTime,Center);};
        work();Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
    }

    private GameObject Host(string name){var host=new GameObject(name);hosts.Add(host);return host;}
    private PixelFrogGrandSlam Create(){var fx=PixelFrogGrandSlam.Create(source,Center,3.2f,targets);effects.Add(fx);return fx;}
}
