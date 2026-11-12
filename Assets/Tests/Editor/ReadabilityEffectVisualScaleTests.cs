using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityEffectVisualScaleTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly List<GameObject> roots=new List<GameObject>();
    private SpriteRenderer source,target;
    private Texture2D texture;
    private Sprite sprite;
    private bool previousActive,previousEnabled;
    private CombatReadabilityPreset previousPreset;

    [SetUp] public void Setup()
    {
        previousActive=CombatEffectTestConfiguration.Active;previousEnabled=CombatReadabilityPreview.Enabled;
        previousPreset=CombatReadabilityPreview.Preset;
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        texture=new Texture2D(4,4);texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white,
            Color.white,Color.white,Color.white,Color.white,Color.white,Color.white,Color.white,Color.white,
            Color.white,Color.white,Color.white,Color.white});texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,4,4),Vector2.one*.5f,4f);
        source=Root("scale source").AddComponent<SpriteRenderer>();source.sprite=sprite;
        target=Root("scale target").AddComponent<SpriteRenderer>();target.sprite=sprite;
        target.transform.position=Vector3.right*4f;
    }

    [TearDown] public void Cleanup()
    {
        foreach(var root in roots)if(root!=null)Object.DestroyImmediate(root);roots.Clear();
        if(sprite!=null)Object.DestroyImmediate(sprite);if(texture!=null)Object.DestroyImmediate(texture);
        CombatEffectTestConfiguration.Active=previousActive;CombatReadabilityPreview.Enabled=previousEnabled;
        CombatReadabilityPreview.Preset=previousPreset;
    }

    [TestCase(CombatReadabilityPreset.Current,1f)]
    [TestCase(CombatReadabilityPreset.Compact,1f)]
    [TestCase(CombatReadabilityPreset.Local,1.215f)]
    public void CommonSpriteAnimationChangesOnlyItsSizeAndKeepsPlayback(CombatReadabilityPreset preset,float scale)
    {
        var baseline=PixelCombatEffect.PlayRaptorBite(target.transform,target,1f);Track(baseline.gameObject);
        Vector3 original=baseline.transform.localScale;float fps=Get<float>(baseline,"framesPerSecond");
        CombatReadabilityPreview.Preset=preset;
        var adjusted=PixelCombatEffect.PlayRaptorBite(target.transform,target,1f);Track(adjusted.gameObject);
        AssertVector(adjusted.transform.localScale,original*scale);
        Assert.That(Get<float>(adjusted,"framesPerSecond"),Is.EqualTo(fps));
        Assert.That(Get<Sprite[]>(adjusted,"frames").Length,Is.EqualTo(Get<Sprite[]>(baseline,"frames").Length));
        Assert.That(adjusted.transform.position,Is.EqualTo(baseline.transform.position));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void BasicProjectileArtAndTrailsScaleWithoutChangingFlight(int kind)
    {
        var baseline=Shot(kind);var oldHead=Get<SpriteRenderer>(baseline,"head");
        var oldTrail=Get<SpriteRenderer[]>(baseline,"trail");
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        var adjusted=Shot(kind);var head=Get<SpriteRenderer>(adjusted,"head");
        var trail=Get<SpriteRenderer[]>(adjusted,"trail");
        AssertVector(head.transform.localScale,oldHead.transform.localScale*1.215f);
        for(int i=0;i<Get<int>(baseline,"activeTrailCount");i++)
            AssertVector(trail[i].transform.localScale,oldTrail[i].transform.localScale*1.215f);
        Assert.That(adjusted.FlightDuration,Is.EqualTo(baseline.FlightDuration));
        Assert.That(Get<Vector3>(adjusted,"start"),Is.EqualTo(Get<Vector3>(baseline,"start")));
        Assert.That(Get<Vector3>(adjusted,"lastTarget"),Is.EqualTo(Get<Vector3>(baseline,"lastTarget")));
        typeof(PixelStarProjectile).GetField("curveSide",Hidden).SetValue(adjusted,Get<float>(baseline,"curveSide"));
        Call(baseline,"UpdateVisual",.4f);Call(adjusted,"UpdateVisual",.4f);
        AssertVector(head.transform.position,oldHead.transform.position);
    }

    [Test] public void MilkyWayLineAndGrowingHeadKeepTheirNewSizeAcrossRepeatedUpdates()
    {
        var baseline=Shot(0);baseline.SetMilkyWayArc(1);Call(baseline,"UpdateVisual",.4f);
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        var adjusted=Shot(0);adjusted.SetMilkyWayArc(1);
        var line=Get<LineRenderer>(adjusted,"milkyWayTrail");var original=Get<LineRenderer>(baseline,"milkyWayTrail");
        Assert.That(line.startWidth,Is.EqualTo(original.startWidth*1.215f).Within(.00001f));
        Assert.That(line.endWidth,Is.EqualTo(original.endWidth*1.215f).Within(.00001f));
        for(int i=0;i<3;i++)Call(adjusted,"UpdateVisual",.4f);
        AssertVector(Get<SpriteRenderer>(adjusted,"head").transform.localScale,
            Get<SpriteRenderer>(baseline,"head").transform.localScale*1.215f);
    }

    [Test] public void GasPuffsAndSporesScaleWhileTheirAreaPositionsAndLifetimeStayFixed()
    {
        var baseline=PixelMushroomGas.Play(Vector3.zero,2f,3f,Color.green,target);Track(baseline.gameObject);baseline.Pose(.5f);
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        var adjusted=PixelMushroomGas.Play(Vector3.zero,2f,3f,Color.green,target);Track(adjusted.gameObject);adjusted.Pose(.5f);
        foreach(string name in new[]{"puffs","specks"})
        {
            var original=Get<ParticleSystem.Particle[]>(baseline,name);var particles=Get<ParticleSystem.Particle[]>(adjusted,name);
            for(int i=0;i<particles.Length;i++)
            {
                AssertVector(particles[i].startSize3D,original[i].startSize3D*1.215f);
                Assert.That(particles[i].position,Is.EqualTo(original[i].position));
                Assert.That(particles[i].remainingLifetime,Is.EqualTo(original[i].remainingLifetime));
            }
        }
        Assert.That(Get<float>(adjusted,"radius"),Is.EqualTo(2f));
        Assert.That(Get<float>(adjusted,"duration"),Is.EqualTo(3f));
    }

    [Test] public void CommonParticlesScaleBothEndsOfTheSizeRange()
    {
        var baseline=PixelCombatEffect.PlayCrocodileCrushingRollChips(Vector3.zero,1f,source);
        Track(baseline.gameObject);var original=baseline.main.startSize;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        var adjusted=PixelCombatEffect.PlayCrocodileCrushingRollChips(Vector3.zero,1f,source);
        Track(adjusted.gameObject);var size=adjusted.main.startSize;
        Assert.That(size.constantMin,Is.EqualTo(original.constantMin*1.215f).Within(.00001f));
        Assert.That(size.constantMax,Is.EqualTo(original.constantMax*1.215f).Within(.00001f));
    }

    private PixelStarProjectile Shot(int kind)
    {
        var shot=kind==0?PixelStarProjectile.Play(source.transform,source,target.transform,target,Color.white,false):
            kind==1?PixelStarProjectile.PlayStone(source.transform,source,target.transform,target,Color.white,null):
            kind==2?PixelStarProjectile.PlaySporeShot(source.transform,source,target.transform,target,Color.white,null):
            PixelStarProjectile.PlaySpiritLight(source.transform,source,target.transform,target,Color.white,null);
        Track(shot.gameObject);return shot;
    }
    private GameObject Root(string name){var root=new GameObject(name);Track(root);return root;}
    private void Track(GameObject root){roots.Add(root);}
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden).GetValue(value);
    private static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden).Invoke(value,args);
    private static void AssertVector(Vector3 actual,Vector3 expected)=>Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.00001f));
}
