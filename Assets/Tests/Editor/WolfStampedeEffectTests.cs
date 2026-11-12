using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WolfStampedeEffectTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly List<PixelWolfStampede> effects=new List<PixelWolfStampede>();
    private readonly Bounds field=new Bounds(Vector3.zero,new Vector3(20f,2f,14f));
    [TearDown] public void Cleanup(){foreach(var effect in effects)if(effect!=null)effect.Cancel();effects.Clear();}
    private PixelWolfStampede Create(Vector3 direction)
    {
        var effect=PixelWolfStampede.Create(field,direction,null);Assert.That(effect,Is.Not.Null);effects.Add(effect);return effect;
    }
    private static T Read<T>(PixelWolfStampede effect,string name)=>(T)typeof(PixelWolfStampede).GetField(name,Hidden).GetValue(effect);
    private static Vector3 Ground(PixelWolfStampede effect,int i,float t)=>(Vector3)typeof(PixelWolfStampede).GetMethod("Ground",Hidden).Invoke(effect,new object[]{i,t});
    private static ParticleSystem.Particle[] Particles(PixelWolfStampede effect)=>Read<ParticleSystem.Particle[]>(effect,"pixels").Take(Read<int>(effect,"pixelCount")).ToArray();

    [Test]
    public void BothDrawnRowsShareTheirLowestContactBaselineWithoutRescalingIndividualPoses()
    {
        var frames=PixelWolfStampede.Frames;Assert.That(frames,Has.Length.EqualTo(8));
        for(int row=0;row<2;row++)
        {
            float contact=frames.Skip(row*4).Take(4).Min(OpaqueBottom);
            Assert.That(contact,Is.EqualTo(0f).Within(1f/128f));
            Assert.That(frames.Skip(row*4).Take(4).Select(frame=>frame.pivot.y).Distinct().Count(),Is.EqualTo(1));
        }
        Assert.That(frames.Skip(4).Min(OpaqueBottom),Is.EqualTo(frames.Take(4).Min(OpaqueBottom)).Within(1f/128f));
    }
    private static float OpaqueBottom(Sprite frame)
    {
        var pixels=frame.texture.GetPixels32();Rect cell=frame.rect;
        for(int y=(int)cell.yMin;y<(int)cell.yMax;y++)for(int x=(int)cell.xMin;x<(int)cell.xMax;x++)
            if(pixels[y*frame.texture.width+x].a>=32)return (y-cell.yMin-frame.pivot.y)/frame.pixelsPerUnit;
        Assert.Fail("Each running pose must contain opaque pixels.");return 0f;
    }

    [Test]
    public void ThreeWavesHaveTwentyFourUnevenRunnersCoveringBothEdgesWithoutAParadeGrid()
    {
        var effect=Create(Vector3.right);effect.Advance(.8f);var wolves=Read<SpriteRenderer[]>(effect,"wolves");
        Assert.That(wolves,Has.Length.EqualTo(24));
        Assert.That(wolves.All(w=>w.sharedMaterial.shader.name=="BecomeZ/Effects/Wolf Moonlit Stampede"&&
            Mathf.Approximately(w.sharedMaterial.GetFloat("_Spectral"),1f)),Is.True);
        foreach(var wolf in wolves.Where(w=>w.enabled))
        {
            var block=new MaterialPropertyBlock();wolf.GetPropertyBlock(block);
            Assert.That(block.GetTexture("_MainTex"),Is.SameAs(wolf.sprite.texture),
                "Each explicit sprite block must carry its atlas, otherwise spectral bodies render as white rectangles.");
        }
        for(int wave=0;wave<3;wave++)
        {
            var feet=Enumerable.Range(wave*8,8).Select(i=>Ground(effect,i,.8f)).ToArray();
            Assert.That(feet.Max(p=>p.z)-feet.Min(p=>p.z),Is.GreaterThan(10f));
            Assert.That(feet.Max(p=>p.x)-feet.Min(p=>p.x),Is.GreaterThan(2.5f),"Each wave must overlap its neighbors rather than forming a straight column.");
            var depths=feet.Select(p=>p.z).OrderBy(z=>z).ToArray();
            var gaps=Enumerable.Range(1,7).Select(i=>depths[i]-depths[i-1]).ToArray();
            Assert.That(gaps.Max()-gaps.Min(),Is.GreaterThan(.35f),"No equally spaced lanes.");
            Assert.That(wolves.Skip(wave*8).Take(8).Select(w=>w.transform.localScale.x).Distinct().Count(),Is.GreaterThan(5));
        }
    }

    [Test]
    public void ScatteredVisualBodiesStayWithinTheirBroadDamageWave()
    {
        var effect=Create(Vector3.right);
        for(int i=0;i<24;i++)for(int step=1;step<10;step++)
        {
            float progress=step/10f;
            float contact=(float)typeof(PixelWolfStampede).GetMethod("ContactTime",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{i,progress});
            Assert.That(Mathf.Abs(contact-WolfStampedeRules.HitTime(i/8,progress)),Is.LessThan(.18f));
            Assert.That(Ground(effect,i,contact).x,Is.EqualTo(Mathf.Lerp(-10f,10f,progress)).Within(.002f));
        }
    }

    [Test]
    public void BornTailPixelsRemainInSpaceSpreadAndFadeInsteadOfFollowingTheBodyAsOneRigidLine()
    {
        var effect=Create(Vector3.right);effect.Advance(.72f);
        var system=Read<ParticleSystem>(effect,"particles");
        Assert.That(system.isPaused,Is.True,"The manual particle clock must be active for renderer output.");
        Assert.That(system.particleCount,Is.GreaterThan(500));
        var before=Particles(effect).Where(p=>p.remainingLifetime>.24f&&p.remainingLifetime<.35f).ToDictionary(p=>p.randomSeed);
        Assert.That(before.Count,Is.GreaterThan(80));effect.Advance(.82f);
        var after=Particles(effect).Where(p=>before.ContainsKey(p.randomSeed)).ToArray();
        Assert.That(after.Length,Is.GreaterThan(40));
        foreach(var particle in after)
        {
            var old=before[particle.randomSeed];
            Assert.That(particle.position.x,Is.LessThan(old.position.x),"Old pixels drift back while the wolf races forward.");
            Assert.That(particle.remainingLifetime,Is.EqualTo(old.remainingLifetime-.1f).Within(.0001f));
            Assert.That(particle.startSize,Is.LessThan(old.startSize));
            Assert.That(particle.startColor.a,Is.LessThanOrEqualTo(old.startColor.a));
        }
        Assert.That(after.Max(p=>p.position.y)-after.Min(p=>p.position.y),Is.GreaterThan(.3f));
    }

    [Test]
    public void HistoricalTailsAndRunnerPositionsAreIdenticalAtThirtyAndOneHundredTwentyFps()
    {
        var slow=Create(Vector3.right);var fast=Create(Vector3.right);
        for(int frame=1;frame<=24;frame++)slow.Advance(frame/30f);
        for(int frame=1;frame<=96;frame++)fast.Advance(frame/120f);
        var a=Particles(slow);var b=Particles(fast);Assert.That(a.Length,Is.EqualTo(b.Length));
        Assert.That(a.Length,Is.GreaterThan(500));Assert.That(a.Length,Is.LessThanOrEqualTo(3200));
        for(int i=0;i<a.Length;i++)
        {
            Assert.That(a[i].randomSeed,Is.EqualTo(b[i].randomSeed));Assert.That(a[i].position,Is.EqualTo(b[i].position));
            Assert.That(a[i].startColor,Is.EqualTo(b[i].startColor));Assert.That(a[i].startSize,Is.EqualTo(b[i].startSize));
        }
        var aw=Read<SpriteRenderer[]>(slow,"wolves");var bw=Read<SpriteRenderer[]>(fast,"wolves");
        for(int i=0;i<aw.Length;i++){Assert.That(aw[i].transform.position,Is.EqualTo(bw[i].transform.position));Assert.That(aw[i].sprite,Is.SameAs(bw[i].sprite));}
    }

    [Test]
    public void TheOtherTeamMirrorsTheRushAndTailPositions()
    {
        var right=Create(Vector3.right);var left=Create(Vector3.left);right.Advance(.8f);left.Advance(.8f);
        var a=Particles(right);var b=Particles(left);Assert.That(a.Length,Is.EqualTo(b.Length));
        for(int i=0;i<a.Length;i++)
        {Assert.That(b[i].position.x,Is.EqualTo(-a[i].position.x).Within(.0001f));Assert.That(b[i].position.z,Is.EqualTo(-a[i].position.z).Within(.0001f));}
    }

    [Test]
    public void ExpiredPackClearsAllBodyTailAndOwnedDrawingResources()
    {
        var effect=Create(Vector3.right);effect.Advance(.8f);
        var mesh=Read<Mesh>(effect,"contactMesh");var material=Read<Material>(effect,"pixelMaterial");
        var bodyMaterial=Read<Material>(effect,"wolfMaterial");
        effect.Advance(WolfStampedeRules.Duration);
        Assert.That(Read<SpriteRenderer[]>(effect,"wolves").All(w=>!w.enabled),Is.True);
        Assert.That(Read<ParticleSystem>(effect,"particles").particleCount,Is.Zero);
        Assert.That(mesh==null,Is.True);Assert.That(material==null,Is.True);
        Assert.That(bodyMaterial==null,Is.True);
    }
}
