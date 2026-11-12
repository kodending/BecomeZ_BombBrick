using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MammothChainVisualTests
{
    private static ParticleSystem.Particle[] Particles(ParticleSystem system)
    {
        var values=new ParticleSystem.Particle[system.main.maxParticles];int count=system.GetParticles(values);
        Array.Resize(ref values,count);return values;
    }
    private static uint Pixels(Sprite sprite)
    {
        var pixels=sprite.texture.GetPixels32();Rect rect=sprite.rect;uint hash=2166136261u;
        for(int y=(int)rect.yMin;y<(int)rect.yMax;y++)for(int x=(int)rect.xMin;x<(int)rect.xMax;x++)
        {
            Color32 color=pixels[y*sprite.texture.width+x];
            unchecked{hash=(hash^color.r)*16777619u;hash=(hash^color.g)*16777619u;hash=(hash^color.b)*16777619u;hash=(hash^color.a)*16777619u;}
        }
        return hash;
    }
    [Test]public void ContactAndDustHaveFourAndEightGenuinelyDifferentDrawings()
    {
        Assert.That(PixelMammothChainImpact.ImpactFrames,Has.Length.EqualTo(4));
        Assert.That(PixelMammothChainImpact.DustFrames,Has.Length.EqualTo(8));
        Assert.That(PixelMammothChainImpact.ImpactFrames.Select(Pixels).Distinct().Count(),Is.EqualTo(4));
        Assert.That(PixelMammothChainImpact.DustFrames.Select(Pixels).Distinct().Count(),Is.EqualTo(8));
        Assert.That(PixelMammothChainImpact.ImpactFrames.Concat(PixelMammothChainImpact.DustFrames).All(sprite=>sprite.texture.filterMode==FilterMode.Point),Is.True);
    }
    [Test]public void CollisionPlaysEveryContactPoseThenFinishesItsAnimatedDustAndPhysicalFragments()
    {
        var fx=PixelMammothExpulsion.Contact(new Vector3(2f,0f,3f),null,Color.white,true,true);
        try
        {
            var renderer=fx.GetComponentInChildren<SpriteRenderer>();var seen=new HashSet<Sprite>();
            for(int i=0;i<4;i++)
            {
                fx.Pose((i+.2f)*PixelMammothChainImpact.ImpactFrameSeconds);seen.Add(renderer.sprite);
                Assert.That(renderer.sprite,Is.SameAs(PixelMammothChainImpact.ImpactFrames[i]));
            }
            Assert.That(seen.Count,Is.EqualTo(4));fx.Pose(.22f);Assert.That(renderer.enabled,Is.False);
            var dust=fx.GetComponentsInChildren<ParticleSystem>().Single(system=>system.textureSheetAnimation.enabled);
            var sheet=dust.textureSheetAnimation;Assert.That(sheet.spriteCount,Is.EqualTo(8));
            Assert.That(sheet.mode,Is.EqualTo(ParticleSystemAnimationMode.Sprites));Assert.That(sheet.timeMode,Is.EqualTo(ParticleSystemAnimationTimeMode.Lifetime));
            var frames=new HashSet<int>();
            for(int sample=0;sample<70;sample++)
            {
                fx.Pose(sample*.01f);var particle=Particles(dust).SingleOrDefault(p=>p.randomSeed==101);
                if(particle.randomSeed!=101)continue;
                float t=1f-particle.remainingLifetime/particle.startLifetime;
                frames.Add(Mathf.Clamp(Mathf.FloorToInt(sheet.frameOverTime.curve.Evaluate(t)*8f),0,7));
            }
            Assert.That(frames.Count,Is.EqualTo(8),"Dust must actually advance all eight drawings from its collision clock.");
            fx.Pose(.30f);Assert.That(fx.GetComponentsInChildren<ParticleSystem>().All(system=>system.particleCount>0),Is.True);
            fx.Pose(PixelMammothChainImpact.Duration+.01f);Assert.That(fx.RemainingLifetime,Is.Zero);
            Assert.That(fx.GetComponentsInChildren<ParticleSystem>().All(system=>system.particleCount==0),Is.True);
        }
        finally{fx.Cancel();}
    }
    [Test]public void OneCastTracksEightGroundedSkidsAndRepeatedCallsAtTheSameAgeDoNotEmitAgain()
    {
        var bodies=Enumerable.Range(0,8).Select(i=>new GameObject("Grounded victim "+i).AddComponent<SpriteRenderer>()).ToArray();
        var fx=PixelMammothExpulsion.Prepare(null,Vector3.zero,Vector3.right,Color.white);
        try
        {
            fx.Charge(Vector3.right*.8f,Vector3.right,.10f);
            for(int i=0;i<bodies.Length;i++)fx.Slide(new Vector3(i*.8f,0f,i%2),Vector3.right*12f,.10f,bodies[i]);fx.Pose(.10f);
            var systems=fx.GetComponentsInChildren<ParticleSystem>();Assert.That(systems,Has.Length.EqualTo(2));
            var skids=systems.Single(system=>system.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.HorizontalBillboard);
            Assert.That(skids.particleCount,Is.GreaterThanOrEqualTo(8));Assert.That(fx.GetComponentsInChildren<SpriteRenderer>(),Is.Empty,"Victims stay real bodies, never scaled or spinning copies.");
            var before=Particles(skids);
            for(int i=0;i<bodies.Length;i++)fx.Slide(new Vector3(i*.8f,0f,i%2),Vector3.right*12f,.10f,bodies[i]);fx.Pose(.10f);
            var after=Particles(skids);CollectionAssert.AreEqual(before.Select(p=>p.position),after.Select(p=>p.position));
            CollectionAssert.AreEqual(before.Select(p=>p.randomSeed),after.Select(p=>p.randomSeed));
            Assert.That(after.All(p=>p.position.y>=.03f&&p.position.y<.08f),Is.True,"Skid marks belong to the floor.");
            for(int i=0;i<bodies.Length;i++)fx.Slide(new Vector3(i*.8f+.8f,0f,i%2),Vector3.right*12f,.20f,bodies[i]);fx.Pose(.20f);
            Assert.That(skids.particleCount,Is.GreaterThan(before.Length));
            var dust=systems.Single(system=>system.textureSheetAnimation.enabled);Assert.That(dust.textureSheetAnimation.spriteCount,Is.EqualTo(8));
            Assert.That(Particles(dust).All(p=>p.position.y>=0f),Is.True);
        }
        finally{fx.Cancel();foreach(var body in bodies)Object.DestroyImmediate(body.gameObject);}
    }
    [Test]public void DetachedTrailsEndNaturallyAndCancelPreservesSharedFrames()
    {
        var source=new GameObject("Mammoth caster").AddComponent<SpriteRenderer>();source.gameObject.layer=30;
        var fx=PixelMammothExpulsion.Prepare(source,Vector3.zero,Vector3.right,Color.white);
        var contact=PixelMammothExpulsion.Contact(Vector3.zero,source,Color.white,false,true);
        try
        {
            fx.Charge(Vector3.right,.2f*Vector3.right,.2f);fx.Pose(.2f);fx.StopEmission();Object.DestroyImmediate(source.gameObject);
            Assert.DoesNotThrow(()=>fx.Pose(.3f));Assert.That(fx.GetComponentsInChildren<ParticleSystem>().Any(system=>system.particleCount>0),Is.True);
            fx.Pose(.2f+PixelMammothExpulsion.TailDuration+.01f);Assert.That(fx.RemainingLifetime,Is.Zero);
            Assert.That(fx.GetComponentsInChildren<ParticleSystem>().All(system=>system.particleCount==0),Is.True);
            Assert.That(fx.GetComponentsInChildren<Transform>(true).All(child=>child.gameObject.layer==30),Is.True);
            Assert.That(contact.GetComponentsInChildren<Transform>(true).All(child=>child.gameObject.layer==30),Is.True);
        }
        finally{fx.Cancel();contact.Cancel();if(source!=null)Object.DestroyImmediate(source.gameObject);}
        Assert.That(PixelMammothChainImpact.ImpactFrames.All(sprite=>sprite!=null),Is.True);
        Assert.That(PixelMammothChainImpact.DustFrames.All(sprite=>sprite!=null),Is.True);
    }
    [Test]public void EightFastVictimsDoNotOverwriteDustBeforeItsEightDrawingsCanFinish()
    {
        var bodies=Enumerable.Range(0,8).Select(i=>new GameObject("Fast skid victim "+i).AddComponent<SpriteRenderer>()).ToArray();
        var fx=PixelMammothExpulsion.Prepare(null,Vector3.zero,Vector3.right,Color.white);
        try
        {
            var dust=fx.GetComponentsInChildren<ParticleSystem>().Single(system=>system.textureSheetAnimation.enabled);
            var frames=new HashSet<int>();
            for(int tick=1;tick<=90;tick++)
            {
                float age=tick/60f;fx.Charge(Vector3.right*age*10f,Vector3.right,age);
                for(int i=0;i<bodies.Length;i++)fx.Slide(new Vector3(age*17f,0f,i),Vector3.right*17f,age,bodies[i]);fx.Pose(age);
                foreach(var particle in Particles(dust))
                {
                    float progress=1f-particle.remainingLifetime/particle.startLifetime;
                    frames.Add(Mathf.Clamp(Mathf.FloorToInt(dust.textureSheetAnimation.frameOverTime.curve.Evaluate(progress)*8f),0,7));
                }
            }
            Assert.That(frames.Count,Is.EqualTo(8),"A bounded cast buffer must still leave each trail enough time to animate.");
        }
        finally{fx.Cancel();foreach(var body in bodies)Object.DestroyImmediate(body.gameObject);}
    }
    [Test]public void ReducedFlashesDimOnlyTheContactAccentAndKeepTheDrawnAnimation()
    {
        bool reduced=CombatFlashPresentation.Reduced;var fx=PixelMammothExpulsion.Contact(Vector3.zero,null,Color.white,false,false);
        try
        {
            CombatFlashPresentation.Apply(false);fx.Pose(.055f);var renderer=fx.GetComponentInChildren<SpriteRenderer>();Color normal=renderer.color;Sprite frame=renderer.sprite;
            CombatFlashPresentation.Apply(true);fx.Pose(.055f);Assert.That(renderer.sprite,Is.SameAs(frame));
            Assert.That(renderer.color.a,Is.LessThan(normal.a));Assert.That(renderer.enabled,Is.True);
        }
        finally{CombatFlashPresentation.Apply(reduced);fx.Cancel();}
    }
    [Test]public void NativeContactAdvancesItselfAndTheStageCanUseAnIndependentManualClock()
    {
        var advance=typeof(PixelMammothChainImpact).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);
        var fx=PixelMammothExpulsion.Contact(Vector3.zero,null,Color.white,false,true);
        var cameraObject=new GameObject("Private contact stage view");var camera=cameraObject.AddComponent<Camera>();
        try
        {
            advance.Invoke(fx,new object[]{.055f});Assert.That(fx.Elapsed,Is.EqualTo(.055f));
            Assert.That(fx.GetComponentInChildren<SpriteRenderer>().sprite,Is.SameAs(PixelMammothChainImpact.ImpactFrames[1]));
            fx.SetPresentation(camera);advance.Invoke(fx,new object[]{.20f});Assert.That(fx.Elapsed,Is.EqualTo(.055f));
            fx.Pose(.10f,camera);Assert.That(fx.GetComponentInChildren<SpriteRenderer>().sprite,Is.SameAs(PixelMammothChainImpact.ImpactFrames[2]));
        }
        finally{fx.Cancel();Object.DestroyImmediate(cameraObject);}
        var automatic=PixelMammothExpulsion.Contact(Vector3.zero,null,Color.white,false,true);
        advance.Invoke(automatic,new object[]{PixelMammothChainImpact.Duration+.01f});Assert.That(automatic==null,Is.True);
    }
    [Test]public void EndingPlaybackAloneOwnsTheContactClockAndDisposesItsWholeTail()
    {
        var fx=PixelMammothExpulsion.Contact(Vector3.zero,null,Color.white,true,true);fx.Pose(.10f);
        var advance=typeof(PixelMammothChainImpact).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);
        var playbackAdvance=typeof(CombatEffectEndPlayback).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);
        try
        {
            CombatEffectEndPlayback.Begin(fx.gameObject,new HashSet<Type>{typeof(PixelMammothChainImpact)});
            var playback=fx.GetComponent<CombatEffectEndPlayback>();Assert.That(playback,Is.Not.Null);
            advance.Invoke(fx,new object[]{.05f});Assert.That(fx.Elapsed,Is.EqualTo(.10f),"Native LateUpdate must not advance a clock already owned by ending playback.");
            playbackAdvance.Invoke(playback,new object[]{.05f});Assert.That(fx.Elapsed,Is.EqualTo(.15f).Within(.0001f));
            for(int i=0;i<16&&playback!=null;i++)playbackAdvance.Invoke(playback,new object[]{.05f});
            Assert.That(fx==null,Is.True);
        }
        finally{if(fx!=null)Object.DestroyImmediate(fx.gameObject);}
    }
}
