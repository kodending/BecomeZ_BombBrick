using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnvironmentTerrainTests
{
    [TestCase("prototype_region")]
    [TestCase("poison_fog_swamp")]
    [TestCase("crimson_jungle")]
    [TestCase("frozen_highlands")]
    [TestCase("sky_sanctuary")]
    public void SkillLandingUsesCurrentMapButSolarMagicKeepsItsColour(string region)
    {
        var host=new GameObject("Terrain Integration Test");
        MeadowAmbientParticles environment=null;
        ParticleSystem landing=null,solar=null;
        try
        {
            var camera=host.AddComponent<Camera>();
            var reference=host.AddComponent<SpriteRenderer>();
            string randomBefore=JsonUtility.ToJson(Random.state);
            environment=MeadowAmbientParticles.Create(host.transform,camera,
                new Bounds(Vector3.zero,new Vector3(20f,.1f,12f)),region);
            environment.Impact(Vector3.zero,3f,reference);
            Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(randomBefore));
            Assert.That(environment.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(58));
            Color original=new Color(.42f,.34f,.23f,.37f);
            Color terrain=MeadowAmbientParticles.TerrainColor(original);
            Assert.That(terrain.a,Is.EqualTo(original.a));
            if(region=="prototype_region")Assert.That(terrain,Is.EqualTo(original));
            else Assert.That(terrain,Is.Not.EqualTo(original));
            if(region=="frozen_highlands")Assert.That(terrain.b,Is.GreaterThan(terrain.r));
            if(region=="poison_fog_swamp")Assert.That(terrain.g,Is.GreaterThan(terrain.r));
            if(region=="crimson_jungle"||region=="sky_sanctuary")
                Assert.That(terrain.r,Is.GreaterThan(terrain.b));

            landing=PixelCombatEffect.PlayFrogTongueSlamImpact(Vector3.zero,reference);
            Assert.That(landing.main.startColor.colorMin,
                Is.EqualTo(MeadowAmbientParticles.TerrainColor(new Color(.42f,.34f,.23f))));
            solar=PixelCombatEffect.PlayBeetleSolarBurst(Vector3.zero,1f,reference);
            Assert.That(solar.main.startColor.colorMin,Is.EqualTo(new Color(1f,.28f,.025f)));
        }
        finally
        {
            if(landing!=null)Object.DestroyImmediate(landing.gameObject);
            if(solar!=null)Object.DestroyImmediate(solar.gameObject);
            Release(environment);
            Object.DestroyImmediate(host);
        }
        Color fallback=new Color(.42f,.34f,.23f,.37f);
        Assert.That(MeadowAmbientParticles.TerrainColor(fallback),Is.EqualTo(fallback));
    }

    [Test]
    public void ReplacingMapDoesNotClearTheNewTerrainWhenOldParticlesAreDestroyed()
    {
        var host=new GameObject("Region Transition Test");
        var camera=host.AddComponent<Camera>();
        var old=MeadowAmbientParticles.Create(host.transform,camera,new Bounds(),"crimson_jungle");
        var current=MeadowAmbientParticles.Create(host.transform,camera,new Bounds(),"frozen_highlands");
        try
        {
            Release(old);Object.DestroyImmediate(old.gameObject);
            Color frozen=MeadowAmbientParticles.TerrainColor(new Color(.5f,.3f,.2f));
            Assert.That(frozen.b,Is.GreaterThan(frozen.r));
        }
        finally{Release(current);Object.DestroyImmediate(host);}
    }

    private static void Release(MeadowAmbientParticles effect)
    {
        if(effect!=null)typeof(MeadowAmbientParticles).GetMethod("OnDestroy",
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);
    }
}
