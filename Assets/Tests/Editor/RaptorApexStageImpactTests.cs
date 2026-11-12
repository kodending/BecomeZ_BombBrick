using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class RaptorApexStageImpactTests
{
    private GameObject host;
    private RaptorApexStageImpact effect;

    [SetUp]
    public void SetUp(){host=new GameObject("Isolated stage impact test");effect=new RaptorApexStageImpact(host.transform);}
    [TearDown]
    public void TearDown(){effect.Dispose();Object.DestroyImmediate(host);}

    [Test]
    public void StageRoarShowsTwoSoftGroundWavesAndBoundedActualDustAndPebblesAtZeroWorldTime()
    {
        float speed=Time.timeScale;
        try
        {
            Time.timeScale=0f;effect.Tick(3.85f,.025f);
            var rings=host.GetComponentsInChildren<LineRenderer>(true);
            Assert.That(rings,Has.Length.EqualTo(2));
            foreach(var ring in rings)
            {Assert.That(ring.enabled,Is.True);Assert.That(ring.loop,Is.True);Assert.That(ring.positionCount,Is.GreaterThan(48));}
            int total=0;
            foreach(var system in host.GetComponentsInChildren<ParticleSystem>(true))
            {Assert.That(system.isPaused,Is.True);total+=system.particleCount;}
            Assert.That(total,Is.GreaterThan(16).And.LessThanOrEqualTo(112));
            foreach(var renderer in host.GetComponentsInChildren<Renderer>(true))
            {Assert.That(renderer.gameObject.layer,Is.EqualTo(30));Assert.That(renderer.sortingOrder,Is.LessThan(20));}
            Assert.That(Time.timeScale,Is.Zero);Assert.That(Resources.Load<Shader>("CombatCinematics/RaptorApex/ApexStageDust"),Is.Not.Null);
        }
        finally{Time.timeScale=speed;}
    }

    [Test]
    public void SkippingDirectlyIntoGatherRoarOrDashCannotLoseTheEffectAndAZeroDeltaRedrawIsStable()
    {
        foreach(float t in new[]{2.8f,3.31f,3.85f,4.78f})
        {
            effect.Clear();effect.Tick(t,.1f);
            var before=new List<Vector3>();
            var buffer=new ParticleSystem.Particle[112];
            foreach(var system in host.GetComponentsInChildren<ParticleSystem>(true))
            {int count=system.GetParticles(buffer);for(int i=0;i<count;i++)before.Add(buffer[i].position);}
            Assert.That(before,Is.Not.Empty,"Missing timeline effect at "+t);
            Vector2 offset=effect.CameraOffset;effect.Tick(t,0f);int index=0;
            foreach(var system in host.GetComponentsInChildren<ParticleSystem>(true))
            {int count=system.GetParticles(buffer);for(int i=0;i<count;i++)Assert.That(buffer[i].position,Is.EqualTo(before[index++]));}
            Assert.That(index,Is.EqualTo(before.Count));Assert.That(effect.CameraOffset,Is.EqualTo(offset));
        }
    }

    [Test]
    public void RoarQuakeIsVisibleBoundedAndOnlyExposesAnOffsetWithoutMovingTheStageOrGameCamera()
    {
        Vector3 position=host.transform.position;float peak=0f;
        for(int i=0;i<=100;i++)
        {
            effect.Tick(3.25f+i*.008f,.008f);peak=Mathf.Max(peak,effect.CameraOffset.magnitude);
            Assert.That(effect.CameraOffset.magnitude,Is.LessThanOrEqualTo(.08501f));
            Assert.That(host.transform.position,Is.EqualTo(position));
        }
        Assert.That(peak,Is.GreaterThan(.04f));
        effect.Tick(5.74f,.05f);Assert.That(effect.CameraOffset,Is.EqualTo(Vector2.zero));
        Assert.That(host.GetComponentsInChildren<Camera>(true),Is.Empty);
    }

    [Test]
    public void ClearAndDisposeRemoveOnlyTheOwnedStagePresentationAndAllowASecondCast()
    {
        var neighbour=new GameObject("Unrelated actor");neighbour.transform.SetParent(host.transform,false);
        effect.Tick(3.85f,.1f);var systems=host.GetComponentsInChildren<ParticleSystem>(true);
        effect.Clear();Assert.That(effect.CameraOffset,Is.EqualTo(Vector2.zero));
        foreach(var system in systems)Assert.That(system.particleCount,Is.Zero);
        foreach(var renderer in host.GetComponentsInChildren<Renderer>(true))Assert.That(renderer.enabled,Is.False);
        effect.Tick(3.85f,.1f);Assert.That(systems[0].particleCount,Is.GreaterThan(0));
        effect.Dispose();effect.Dispose();effect.Tick(3.85f,.1f);
        Assert.That(neighbour,Is.Not.Null);Assert.That(host.GetComponentsInChildren<Renderer>(true),Is.Empty);
    }
}
