using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class PixelBeetleImperialHornTests
{
    private GameObject host;
    private SpriteRenderer reference;
    private PixelBeetleOverturn effect;

    [SetUp]public void Setup()
    {
        host=new GameObject("Native imperial horn effect tests");reference=host.AddComponent<SpriteRenderer>();
        reference.sprite=PixelBeetleOverturn.SourceFrames("jangpung",false)[7];
        reference.transform.rotation=Quaternion.Euler(30f,0f,0f);
    }

    [TearDown]public void Cleanup()
    {if(effect!=null)effect.Cancel();Object.DestroyImmediate(host);}

    private PixelBeetleOverturn Play(Vector3 direction)
    {
        effect=PixelBeetleOverturn.Play(new List<Bounds>{new Bounds(new Vector3(2f,0f,1f),new Vector3(5f,0f,3f))},reference,direction);
        Assert.That(effect,Is.Not.Null);return effect;
    }

    [Test]public void EachSpeciesHasEightOwnPosesWhileAllCallsShareTheSameAtlasAndCachedSprites()
    {
        string[] species={"jangpung","noepung","atlagon","solarbeetle","jangpung"};
        var rectangles=new HashSet<Rect>();Texture atlas=null;
        for(int row=0;row<species.Length;row++)
        {
            bool shiny=row==4;Sprite[] frames=PixelBeetleOverturn.SourceFrames(species[row],shiny);
            Assert.That(frames,Has.Length.EqualTo(8));Assert.That(PixelBeetleOverturn.SourceFrames(species[row],shiny),Is.SameAs(frames));
            foreach(Sprite frame in frames)
            {
                if(atlas==null)atlas=frame.texture;Assert.That(frame.texture,Is.SameAs(atlas));
                Assert.That(rectangles.Add(frame.rect),Is.True,"Species must not reuse another species' drawn pose.");
            }
        }
        Assert.That(rectangles.Count,Is.EqualTo(40));
        Assert.That(PixelBeetleOverturn.SourceFrames("noepung",true),Is.SameAs(PixelBeetleOverturn.SourceFrames("noepung",false)),
            "Only an actual existing shiny needs another source row.");
    }

    [Test]public void TheAuthoredHornHasTransparentGuttersAndCutoutCoverageInsteadOfAColoredRectangularBackdrop()
    {
        Texture2D atlas=Resources.Load<Texture2D>(PixelBeetleOverturn.ResourceRoot+"Horn8");Assert.That(atlas,Is.Not.Null);
        Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));var pixels=atlas.GetPixels32();
        int width=atlas.width/4,height=atlas.height/2;
        for(int frame=0;frame<8;frame++)
        {
            int x0=frame%4*width,y0=frame/4*height,drawn=0,samples=0;
            foreach(var corner in new[]{new Vector2Int(8,8),new Vector2Int(width-9,8),new Vector2Int(8,height-9),new Vector2Int(width-9,height-9)})
                Assert.That(pixels[(y0+corner.y)*atlas.width+x0+corner.x].a,Is.Zero);
            for(int y=8;y<height;y+=8)for(int x=8;x<width;x+=8)
            {samples++;if(pixels[(y0+y)*atlas.width+x0+x].a>64)drawn++;}
            Assert.That(drawn/(float)samples,Is.InRange(.01f,.70f),"A frame needs a visible horn and a clear background.");
        }
    }

    [TestCase(1f)][TestCase(-1f)]
    public void NativePlaybackUsesAllEightHornAndFourTurfDrawingsAndPointsTowardTheActualAttackDirection(float side)
    {
        Quaternion view=Camera.main==null?reference.transform.rotation:Camera.main.transform.rotation;
        Vector3 right=view*Vector3.right;right.y=0f;right.Normalize();Play(right*side);
        var horn=effect.GetComponentsInChildren<SpriteRenderer>(true).Single(renderer=>renderer.name=="Eight drawing imperial horn");
        var horns=new HashSet<Sprite>();var earth=new HashSet<Sprite>();
        for(float time=.01f;time<BeetleOverturnRules.End;time+=.005f)
        {
            effect.Pose(time);
            if(horn.enabled){horns.Add(horn.sprite);Assert.That(horn.flipX,Is.EqualTo(side<0f));}
            foreach(var renderer in effect.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer=>renderer.enabled&&renderer.name.StartsWith("Four drawing turf upheaval ",StringComparison.Ordinal)))
                earth.Add(renderer.sprite);
        }
        Assert.That(horns.Count,Is.EqualTo(8));Assert.That(earth.Count,Is.EqualTo(4));
        effect.Pose(BeetleOverturnRules.End+.001f);Assert.That(horn.enabled,Is.False);
    }

    [Test]public void LandingHitsItsLargeDrawnPeakPromptlyThenPlaysEveryFrameAndSettlesThreeDimensionalDebrisWithoutChangingRandomState()
    {
        Play(Vector3.right);Vector3 at=new Vector3(1.5f,0f,1f);float birth=BeetleOverturnRules.Landing;
        effect.Pose(birth);effect.Land(at);
        var dust=effect.GetComponentsInChildren<SpriteRenderer>(true).Single(renderer=>renderer.name=="Eight drawing ground impact 0");
        var fragments=effect.GetComponentInChildren<ParticleSystem>(true);
        Bounds first=VisibleBody(dust.sprite);var random=UnityEngine.Random.state;float speed=Time.timeScale;
        effect.Pose(birth+.06f);
        Assert.That(dust.color.a,Is.GreaterThan(.95f),"The landing peak must remain fully visible before the late dust fade.");
        Assert.That(VisibleBody(dust.sprite).size.y*dust.transform.lossyScale.y,Is.EqualTo(2.70f).Within(.01f),
            "Measure the visible burst, not the transparent cell, so it clears the monsters standing in front of it.");
        Vector3 drawingScale=dust.transform.localScale;
        Assert.That(VisibleBody(dust.sprite).size.y,Is.GreaterThan(first.size.y*2f),"The impact must reach its large burst before it reads as a slow cloud.");
        Assert.That(dust.transform.position,Is.EqualTo(at+Vector3.up*.025f));
        var particles=new ParticleSystem.Particle[768];int count=fragments.GetParticles(particles);Assert.That(count,Is.GreaterThan(24));
        Assert.That(particles.Take(count).Max(p=>p.position.z)-particles.Take(count).Min(p=>p.position.z),Is.GreaterThan(.3f));
        Assert.That(particles.Take(count).Max(p=>p.position.y)-particles.Take(count).Min(p=>p.position.y),Is.GreaterThan(.05f));
        float[] radius=new float[3];
        for(int i=0;i<3;i++)
        {
            effect.Pose(birth+.20f*(i+1));count=fragments.GetParticles(particles);
            radius[i]=particles.Take(count).Average(p=>new Vector2(p.position.x-at.x,p.position.z-at.z).magnitude);
        }
        Assert.That(radius[1],Is.GreaterThan(radius[0]));Assert.That(radius[2],Is.GreaterThan(radius[1]));
        Assert.That(radius[1]-radius[0],Is.GreaterThan(radius[2]-radius[1]),"Fragments spread quickly and slow while traveling farther.");
        var frames=new HashSet<Sprite>();
        for(float age=0f;age<PixelBeetleOverturn.Afterglow;age+=.005f)
        {effect.Pose(birth+age);if(dust.enabled){frames.Add(dust.sprite);Assert.That(dust.transform.localScale,Is.EqualTo(drawingScale));}}
        Assert.That(frames.Count,Is.EqualTo(8));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));Assert.That(Time.timeScale,Is.EqualTo(speed));
        effect.Pose(birth+PixelBeetleOverturn.Afterglow+.001f);Assert.That(dust.enabled,Is.False);
        Assert.That(fragments.GetParticles(particles),Is.Zero);
    }

    private static Bounds VisibleBody(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper)
        .GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{sprite});

    [Test]public void EditModeCancelDestroysOnlyThePrivateEffectAndKeepsSharedSourceFramesUsable()
    {
        var cached=PixelBeetleOverturn.SourceFrames("jangpung",false);Play(Vector3.right);effect.Pose(BeetleOverturnRules.Landing);
        effect.Land(Vector3.zero);var dust=effect.GetComponentsInChildren<SpriteRenderer>(true).Last();var particles=effect.GetComponentInChildren<ParticleSystem>(true);
        effect.Cancel();Assert.That(effect==null&&dust==null&&particles==null,Is.True);Assert.That(reference!=null,Is.True);
        Assert.That(PixelBeetleOverturn.SourceFrames("jangpung",false),Is.SameAs(cached));
        Assert.That(cached.All(frame=>frame!=null&&frame.texture!=null),Is.True);
    }

    [Test]public void ChargeUsesTheExistingParticleEmitterAroundTheHornBeforeTheLift()
    {
        Play(Vector3.right);effect.Pose(BeetleOverturnRules.Warning-.01f);
        var particles=new ParticleSystem.Particle[768];int count=effect.GetComponentInChildren<ParticleSystem>(true).GetParticles(particles);
        Vector3 joint=(Vector3)typeof(PixelBeetleOverturn).GetField("joint",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effect);
        Assert.That(count,Is.EqualTo(20));Assert.That(particles.Take(count).All(p=>Vector3.Distance(p.position,joint)<1f),Is.True);
        Assert.That(particles.Take(count).Any(p=>p.startColor.a>64),Is.True);
    }

    [Test]public void EnemiesInFrontOrBehindTheCasterCannotTurnTheRaisedHornSideways()
    {
        Quaternion view=Camera.main==null?reference.transform.rotation:Camera.main.transform.rotation;
        foreach(Vector3 direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
        {
            reference.flipX=direction.z>0f;Play(direction);effect.Pose(BeetleOverturnRules.RiseEnd);
            var horn=effect.GetComponentsInChildren<SpriteRenderer>(true).Single(renderer=>renderer.name=="Eight drawing imperial horn");
            Assert.That(Quaternion.Angle(horn.transform.rotation,view),Is.LessThan(.01f));
            if(Mathf.Abs((Quaternion.Inverse(view)*direction).x)<.08f)Assert.That(horn.flipX,Is.EqualTo(reference.flipX));
            effect.Cancel();
        }
    }
}
