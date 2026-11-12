using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=NUnit.Framework.Is;
using Object=UnityEngine.Object;

public sealed class PixelCrocodileAbyssDiveTests
{
    private readonly List<GameObject> hosts=new List<GameObject>();
    private readonly List<PixelCrocodileAbyssDive> effects=new List<PixelCrocodileAbyssDive>();
    private readonly ParticleSystem.Particle[] before=new ParticleSystem.Particle[1024],after=new ParticleSystem.Particle[1024];
    private SpriteRenderer source,target;
    private Texture2D art;
    private Sprite sprite;
    [SetUp] public void Setup()
    {
        art=new Texture2D(2,2);art.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});art.Apply();
        sprite=Sprite.Create(art,new Rect(0,0,2,2),Vector2.one*.5f,2f);
        source=Host("Actual crocodile").AddComponent<SpriteRenderer>();source.sprite=sprite;
        target=Host("Actual single prey").AddComponent<SpriteRenderer>();target.sprite=sprite;target.transform.position=Vector3.right;
    }
    [TearDown] public void Cleanup()
    {
        foreach(var effect in effects)if(effect!=null)effect.Cancel(true);effects.Clear();
        foreach(var host in hosts)if(host!=null)Object.DestroyImmediate(host);hosts.Clear();Object.DestroyImmediate(sprite);Object.DestroyImmediate(art);
    }
    [Test]
    public void InvalidSourcesCannotCreateAnOrphanPoolAndAnActiveVisualNeverMovesActors()
    {
        Assert.That(PixelCrocodileAbyssDive.Create(null,target,Vector3.zero,2f),Is.Null);
        Assert.That(PixelCrocodileAbyssDive.Create(source,null,Vector3.zero,2f),Is.Null);
        var effect=Create();Vector3 s=source.transform.position,t=target.transform.position;
        effect.Advance(.5f,Vector3.zero);Assert.That(effect.Particles.particleCount,Is.GreaterThan(300).And.LessThanOrEqualTo(PixelCrocodileAbyssDive.MaximumPixels));
        Assert.That(effect.Particles.isPaused,Is.True);Assert.That(effect.Particles.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(effect.Particles.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture.filterMode,Is.EqualTo(FilterMode.Point));
        typeof(PixelCrocodileAbyssDive).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(effect,null);
        Assert.That(effect.Age,Is.EqualTo(.5f));Assert.That(source.transform.position,Is.EqualTo(s));Assert.That(target.transform.position,Is.EqualTo(t));
    }
    [Test]
    public void TheLargePressureCrownAndGoldCrushAppearOnlyAtTheSingleGameplayHit()
    {
        var effect=Create();effect.Advance(CrocodileAbyssDiveRules.HitTime-.001f,Vector3.zero);
        Assert.That(GoldPixels(effect),Is.Zero);
        effect.Advance(CrocodileAbyssDiveRules.HitTime,Vector3.zero);
        Assert.That(effect.Particles.particleCount,Is.GreaterThan(500));Assert.That(GoldPixels(effect),Is.GreaterThan(0));
        effect.Advance(CrocodileAbyssDiveRules.HitTime+.02f,Vector3.zero);effect.Particles.GetParticles(before);
        effect.Advance(CrocodileAbyssDiveRules.HitTime+.15f,Vector3.zero);effect.Particles.GetParticles(after);
        Assert.That(Vector3.Distance(after[0].position,Vector3.zero),Is.GreaterThan(Vector3.Distance(before[0].position,Vector3.zero)));
        Assert.That(effect.Particles.particleCount,Is.LessThanOrEqualTo(PixelCrocodileAbyssDive.MaximumPixels));
        effect.Advance(CrocodileAbyssDiveRules.HitTime+.02f,Vector3.zero);float firstHeight=Height(effect);
        effect.Advance(CrocodileAbyssDiveRules.HitTime+.15f,Vector3.zero);
        Assert.That(Height(effect),Is.GreaterThan(firstHeight+1f),"The final bite must throw a pressure curtain upwards, not only expand a flat ring.");
    }
    [Test]
    public void TheCinematicHasMovingCoiledBubblesAndSideCurtainsWithoutPrematureImpact()
    {
        var effect=Create(true);effect.Advance(1.7f,Vector3.zero);effect.Particles.GetParticles(before);
        effect.Advance(2.1f,Vector3.zero);int count=effect.Particles.GetParticles(after);
        Assert.That(count,Is.GreaterThan(350).And.LessThanOrEqualTo(PixelCrocodileAbyssDive.MaximumPixels));Assert.That(GoldPixels(effect),Is.Zero);
        Assert.That(Vector3.Distance(before[130].position,after[130].position),Is.GreaterThan(.1f));
        effect.Advance(3.99f,Vector3.zero);
        Assert.That(GoldPixels(effect),Is.Zero,"The final gold bite belongs to the actual gameplay hit, never a second cinematic impact.");
    }

    [Test]
    public void UnderwaterRibbonsFollowTheirCurvedFlowAndTaperInsteadOfPointingOutLikeSpokes()
    {
        var effect=Create(true);effect.SetView(Quaternion.identity);effect.Advance(1.65f,Vector3.zero);effect.Particles.GetParticles(before);
        int flowing=0;float alignment=0f,thinnest=float.PositiveInfinity,thickest=0f;
        for(int i=3;i<350;i+=2)
        {
            Vector3 delta=before[i+2].position-before[i-2].position;
            if(delta.magnitude>.5f||delta.sqrMagnitude<.00001f)continue;
            Vector2 tangent=new Vector2(delta.x,delta.y).normalized;
            float angle=before[i].rotation*Mathf.Deg2Rad;
            Vector2 longAxis=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
            alignment+=Mathf.Abs(Vector2.Dot(tangent,longAxis));flowing++;
            thinnest=Mathf.Min(thinnest,before[i].startSize3D.x);thickest=Mathf.Max(thickest,before[i].startSize3D.x);
        }
        Assert.That(flowing,Is.GreaterThan(130));
        Assert.That(alignment/flowing,Is.GreaterThan(.97f),"Long pixels must follow the water's tangent, not form a radial saw around the victim.");
        Assert.That(thickest,Is.GreaterThan(thinnest*3f),"Water ribbons must shed thin tapered ends rather than repeat equal-width bars.");
    }

    [Test]
    public void TwoLoadedWhipsHaveSeparateAccelerationsAndKeepTheirPressureWavesInTheSameClock()
    {
        Assert.That(PixelCrocodileAbyssDive.CinematicRollTurns(1.35f),Is.Zero);
        Assert.That(PixelCrocodileAbyssDive.CinematicRollTurns(1.94f),Is.EqualTo(1f).Within(.00001f));
        Assert.That(PixelCrocodileAbyssDive.CinematicRollTurns(2f),Is.EqualTo(1f).Within(.00001f));
        Assert.That(PixelCrocodileAbyssDive.CinematicRollTurns(2.64f),Is.EqualTo(2f).Within(.00001f));
        float loading=PixelCrocodileAbyssDive.CinematicRollTurns(1.45f)-PixelCrocodileAbyssDive.CinematicRollTurns(1.35f);
        float whip=PixelCrocodileAbyssDive.CinematicRollTurns(1.7f)-PixelCrocodileAbyssDive.CinematicRollTurns(1.6f);
        Assert.That(whip,Is.GreaterThan(loading*4f),"The body and water share a forceful whip, with a brief loaded pause rather than constant spinning.");
        var effect=Create(true);effect.Advance(1.7f,Vector3.zero);int beforeWave=effect.Particles.particleCount;
        effect.Advance(1.84f,Vector3.zero);
        Assert.That(effect.Particles.particleCount,Is.GreaterThan(beforeWave+60));
        effect.Advance(2.54f,Vector3.zero);
        Assert.That(effect.Particles.particleCount,Is.GreaterThan(beforeWave+60));
        Assert.That(GoldPixels(effect),Is.Zero);
    }

    [Test]
    public void TheSurfaceTearsAndSwirlsThenDisappearsUnderwaterInsteadOfFloatingAroundTheActor()
    {
        var effect=Create(true);effect.Advance(.3f,Vector3.zero);
        var pool=(Transform)typeof(PixelCrocodileAbyssDive).GetField("pool",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(effect);
        var mesh=pool.GetComponent<MeshFilter>().sharedMesh;Vector3[] first=mesh.vertices;
        float shortest=float.PositiveInfinity,longest=0f;
        for(int i=1;i<first.Length;i++){float radius=first[i].magnitude;shortest=Mathf.Min(shortest,radius);longest=Mathf.Max(longest,radius);}
        Assert.That(longest-shortest,Is.GreaterThan(.12f),"The entrance must have broken moving crests instead of a perfect static ellipse.");
        effect.Advance(.5f,Vector3.zero);Vector3[] moved=mesh.vertices;bool changed=false;
        for(int i=1;i<first.Length;i++)changed|=Vector3.Distance(first[i],moved[i])>.03f;
        Assert.That(changed,Is.True);Assert.That(pool.gameObject.activeSelf,Is.True);
        effect.Advance(1.8f,Vector3.zero);Assert.That(pool.gameObject.activeSelf,Is.False);
        effect.Advance(3.3f,Vector3.zero);Assert.That(pool.gameObject.activeSelf,Is.True);
    }

    [Test]
    public void ResurfacingLaunchesTwoHighWaterCurtainsAndFallingDropletsBeforeTheFinalBite()
    {
        var effect=Create(true);effect.Advance(.9f,Vector3.zero);float divingHeight=Height(effect);
        effect.Advance(3.12f,Vector3.zero);float eruptingHeight=Height(effect);
        Assert.That(eruptingHeight,Is.GreaterThan(divingHeight+.5f));
        int count=effect.Particles.GetParticles(after),left=0,right=0;
        for(int i=0;i<count;i++)
            if(after[i].position.y>2f){if(after[i].position.x<-.4f)left++;if(after[i].position.x>.4f)right++;}
        Assert.That(left,Is.GreaterThan(10));Assert.That(right,Is.GreaterThan(10));
        Assert.That(GoldPixels(effect),Is.Zero);
        effect.Advance(3.58f,Vector3.zero);Assert.That(Height(effect),Is.LessThan(eruptingHeight),"The eruption must shed falling water rather than hold an unchanging column.");
    }

    [TestCase(30)][TestCase(60)][TestCase(120)]
    public void CinematicWaterIsFrameRateIndependentAndNeverOverflowsEvenThroughBothWhipsAndEruption(int fps)
    {
        var stepped=Create(true);var direct=Create(true);
        foreach(float time in new[]{.37f,.89f,1.65f,1.86f,2.54f,2.84f,3.12f,3.6f})
        {
            for(int frame=0;frame/(float)fps<time;frame++)stepped.Advance(frame/(float)fps,Vector3.zero);
            stepped.Advance(time,Vector3.zero);direct.Advance(time,Vector3.zero);
            int count=stepped.Particles.GetParticles(before);Assert.That(count,Is.InRange(1,PixelCrocodileAbyssDive.MaximumPixels));
            Assert.That(direct.Particles.GetParticles(after),Is.EqualTo(count));
            for(int i=0;i<count;i++)
            {Assert.That(after[i].position,Is.EqualTo(before[i].position));Assert.That(after[i].startSize3D,Is.EqualTo(before[i].startSize3D));Assert.That(after[i].startColor,Is.EqualTo(before[i].startColor));}
        }
    }
    [TestCase(30)][TestCase(60)][TestCase(120)]
    public void PixelPositionsAreDeterministicForAnyExplicitFrameRate(int fps)
    {
        var first=Create();var direct=Create();float time=CrocodileAbyssDiveRules.HitTime+.14f;
        for(int i=0;i/(float)fps<time;i++)first.Advance(i/(float)fps,Vector3.zero);
        first.Advance(time,Vector3.zero);direct.Advance(time,Vector3.zero);
        int count=first.Particles.GetParticles(before);Assert.That(direct.Particles.GetParticles(after),Is.EqualTo(count));
        for(int i=0;i<count;i++){Assert.That(after[i].position,Is.EqualTo(before[i].position));Assert.That(after[i].startColor,Is.EqualTo(before[i].startColor));}
    }
    [Test]
    public void CancelBeforeTheBiteFadesExistingWaterWithoutCreatingALaterCrush()
    {
        var effect=Create();effect.Advance(.5f,Vector3.zero);effect.Cancel();effect.Advance(1f,Vector3.zero);effect.AdvanceEnd(.1f);
        Assert.That(effect.Age,Is.EqualTo(.5f));Assert.That(GoldPixels(effect),Is.Zero);
        effect.AdvanceEnd(.2f);Assert.That(effect==null,Is.True);
    }
    [Test]
    public void ResultPlaybackBeforeImpactOnlyFadesTheExistingPoolAndRepeatedPreparationDoesNotRestartTheFade()
    {
        var effect=Create();effect.Advance(.5f,Vector3.zero);effect.PrepareEndPlayback();effect.AdvanceEnd(.1f);
        Assert.That(effect.Age,Is.EqualTo(.5f));Assert.That(GoldPixels(effect),Is.Zero);Assert.That(effect.Particles.particleCount,Is.GreaterThan(0));
        effect.PrepareEndPlayback();effect.AdvanceEnd(.15f);
        Assert.That(effect==null,Is.True,"The unfinished cast must fade within .24 seconds rather than advancing into a new impact.");
    }
    [Test]
    public void ExistingWaterFinishesAfterTheResultWhileItsBiteAnchorStaysFrozen()
    {
        var effect=Create();effect.Advance(CrocodileAbyssDiveRules.HitTime+.02f,Vector3.zero);effect.PrepareEndPlayback();
        source.transform.position=Vector3.one*100f;target.transform.position=Vector3.one*-100f;
        effect.AdvanceEnd(.05f);int count=effect.Particles.GetParticles(after);
        for(int i=0;i<count;i++)Assert.That(after[i].position.magnitude,Is.LessThan(10f));
        effect.AdvanceEnd(1f);Assert.That(effect==null,Is.True);
    }
    [Test]
    public void SceneDestroyedChildrenCanStillBeCancelledWithoutMissingReferenceErrors()
    {
        var effect=Create();var particles=effect.Particles;Object.DestroyImmediate(particles.gameObject);
        Assert.DoesNotThrow(()=>effect.Cancel(true));Assert.That(effect==null,Is.True);
    }
    [Test]
    public void WarmedPoolRollAndImpactTicksDoNotAllocateManagedMemory()
    {
        var effect=Create();TestDelegate work=()=>{effect.Advance(.4f,Vector3.zero);effect.Advance(.8f,Vector3.zero);effect.Advance(CrocodileAbyssDiveRules.HitTime,Vector3.zero);};
        work();Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
    }
    [Test]
    public void WarmedCinematicWhipsAndWaterColumnsDoNotAllocateManagedMemory()
    {
        var effect=Create(true);TestDelegate work=()=>{effect.Advance(.9f,Vector3.zero);effect.Advance(1.65f,Vector3.zero);effect.Advance(2.54f,Vector3.zero);effect.Advance(3.12f,Vector3.zero);};
        work();Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
    }
    [Test]
    public void TheActualHitPlaysEightDrawnWaterPhasesAndSixContactPhasesWithoutResizingEachDrawing()
    {
        var effect=Create();SpriteRenderer water=NamedSprite(effect,"Eight drawn abyss water crush"),contact=NamedSprite(effect,"Six drawn abyss bite contact");
        var crown=NamedMesh(effect,"Curled three dimensional pressure crown");
        effect.Advance(CrocodileAbyssDiveRules.HitTime-.001f,Vector3.zero);
        Assert.That(water.enabled||contact.enabled||crown.GetComponent<MeshRenderer>().enabled,Is.False);
        var waterFrames=new HashSet<Sprite>();var contactFrames=new HashSet<Sprite>();Vector3? scale=null;
        foreach(float elapsed in new[]{0f,.03f,.07f,.12f,.22f,.32f,.45f,.56f})
        {
            effect.Advance(CrocodileAbyssDiveRules.HitTime+elapsed,Vector3.zero);Assert.That(water.enabled,Is.True);
            Assert.That(water.sprite.texture,Is.SameAs(Resources.Load<Texture2D>(PixelCrocodileAbyssDive.CrushResource)));
            waterFrames.Add(water.sprite);if(scale.HasValue)Assert.That(water.transform.localScale,Is.EqualTo(scale.Value));else scale=water.transform.localScale;
        }
        foreach(float elapsed in new[]{.001f,.031f,.061f,.091f,.121f,.151f})
        {effect.Advance(CrocodileAbyssDiveRules.HitTime+elapsed,Vector3.zero);Assert.That(contact.enabled,Is.True);contactFrames.Add(contact.sprite);}
        Assert.That(waterFrames.Count,Is.EqualTo(8));Assert.That(contactFrames.Count,Is.GreaterThanOrEqualTo(4));
        effect.Advance(CrocodileAbyssDiveRules.HitTime+.2f,Vector3.zero);Assert.That(contact.enabled,Is.False);
        effect.Advance(.5f,Vector3.zero);Assert.That(water.enabled||contact.enabled||crown.GetComponent<MeshRenderer>().enabled,Is.False,
            "Repainting an earlier cast time must not leave a previous impact visible.");
    }

    [Test]
    public void ThePressureBurstHasRealDepthLeavesTheCentralBiteOpenAndUsesShortDropletsInsteadOfWhiteBars()
    {
        var effect=Create();effect.SetView(Quaternion.identity);effect.Advance(CrocodileAbyssDiveRules.HitTime+.12f,Vector3.zero);
        var crown=NamedMesh(effect,"Curled three dimensional pressure crown");Bounds bounds=crown.sharedMesh.bounds;
        Assert.That(bounds.size.z,Is.GreaterThan(1f));Assert.That(bounds.size.y,Is.GreaterThan(2f));
        Vector3[] vertices=crown.sharedMesh.vertices;Color[] colors=crown.sharedMesh.colors;int high=0;
        for(int i=0;i<vertices.Length;i++)if(vertices[i].y>.7f&&colors[i].a>.05f)
        {high++;Assert.That(Mathf.Abs(vertices[i].x),Is.GreaterThan(.5f),"The front of the actual bite must stay open between the two pressure fans.");}
        Assert.That(high,Is.GreaterThan(10));
        int count=effect.Particles.GetParticles(after),longBars=0;
        for(int i=0;i<count;i++)if(after[i].startSize3D.y>after[i].startSize3D.x*3f&&after[i].startSize3D.y>.25f)longBars++;
        Assert.That(longBars,Is.LessThan(8));Assert.That(source.transform.position,Is.EqualTo(Vector3.zero));Assert.That(target.transform.position,Is.EqualTo(Vector3.right));
    }

    [Test]
    public void ImpactMeshesAndDrawnFramesUseTheSameExplicitClockAndTheirOwnedResourcesAreReleased()
    {
        var stepped=Create();var direct=Create();float time=CrocodileAbyssDiveRules.HitTime+.12f;
        for(int frame=0;frame/120f<time;frame++)stepped.Advance(frame/120f,Vector3.zero);
        stepped.Advance(time,Vector3.zero);direct.Advance(time,Vector3.zero);
        foreach(string name in new[]{"Curled three dimensional pressure crown","Broken decelerating contact wakes"})
        {
            Mesh a=NamedMesh(stepped,name).sharedMesh,b=NamedMesh(direct,name).sharedMesh;
            Assert.That(a.vertices,Is.EqualTo(b.vertices));Assert.That(a.colors,Is.EqualTo(b.colors));
        }
        Assert.That(NamedSprite(stepped,"Eight drawn abyss water crush").sprite,Is.SameAs(NamedSprite(direct,"Eight drawn abyss water crush").sprite));
        var crown=NamedMesh(stepped,"Curled three dimensional pressure crown").sharedMesh;
        var ripples=NamedMesh(stepped,"Broken decelerating contact wakes").sharedMesh;
        var material=NamedSprite(stepped,"Eight drawn abyss water crush").sharedMaterial;
        stepped.Cancel(true);Assert.That(crown==null&&ripples==null&&material==null,Is.True);
    }

    private static SpriteRenderer NamedSprite(PixelCrocodileAbyssDive effect,string name)=>
        effect.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);
    private static MeshFilter NamedMesh(PixelCrocodileAbyssDive effect,string name)=>
        effect.GetComponentsInChildren<MeshFilter>(true).Single(r=>r.name==name);
    private float Height(PixelCrocodileAbyssDive effect)
    {
        int count=effect.Particles.GetParticles(after);float height=float.NegativeInfinity;
        for(int i=0;i<count;i++)height=Mathf.Max(height,after[i].position.y);return height;
    }
    private int GoldPixels(PixelCrocodileAbyssDive effect)
    {
        int count=effect.Particles.GetParticles(after),gold=0;
        for(int i=0;i<count;i++){Color color=after[i].startColor;if(color.r>.9f&&color.g>.65f&&color.b<.85f&&color.a>.01f)gold++;}return gold;
    }
    private PixelCrocodileAbyssDive Create(bool cinematic=false)
    {var effect=PixelCrocodileAbyssDive.Create(source,target,Vector3.zero,3f,cinematic);effects.Add(effect);return effect;}
    private GameObject Host(string name){var host=new GameObject(name);hosts.Add(host);return host;}
}
