using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MushroomDeathBloomVisualTests
{
    private const string SmokeShader="Hidden/PixelMushroomDeathBloomSmoke";
    private static readonly Bounds Arena=new Bounds(Vector3.zero,new Vector3(27f,1f,13.5f));
    private static ParticleSystem.Particle[] ReadParticles(ParticleSystem particles)
    {
        var values=new ParticleSystem.Particle[particles.main.maxParticles];int count=particles.GetParticles(values);
        Array.Resize(ref values,count);return values;
    }
    private static ParticleSystem Cloud(PixelMushroomDeathBloom effect)=>effect.GetComponentsInChildren<ParticleSystem>(true)
        .Single(system=>system.name=="Connected low poison billows");
    private static ParticleSystem[] Billows(PixelMushroomDeathBloom effect)=>effect.GetComponentsInChildren<ParticleSystem>(true)
        .Where(system=>system.GetComponent<Renderer>().sharedMaterial.shader.name==SmokeShader).ToArray();
    private static float ConnectedFraction(ParticleSystem.Particle[] particles)
    {
        var mass=particles.Where(p=>p.startColor.a>20).ToArray();var remaining=new HashSet<int>(Enumerable.Range(0,mass.Length));int largest=0;
        while(remaining.Count>0)
        {
            var queue=new Queue<int>();int first=remaining.First();remaining.Remove(first);queue.Enqueue(first);int count=0;
            while(queue.Count>0)
            {
                int i=queue.Dequeue();count++;var neighbors=new List<int>();
                foreach(int j in remaining)
                {
                    Vector3 delta=mass[i].position-mass[j].position;delta.y=0f;
                    if(delta.magnitude<=(mass[i].startSize3D.x+mass[j].startSize3D.x)*.40f)neighbors.Add(j);
                }
                foreach(int next in neighbors){remaining.Remove(next);queue.Enqueue(next);}
            }
            largest=Mathf.Max(largest,count);
        }
        return largest/(float)Mathf.Max(1,mass.Length);
    }

    [Test]public void SmokeUsesSupportedProceduralParticleVolumesWithoutAnyImageOrSpriteSheet()
    {
        var effect=PixelMushroomDeathBloom.Play(Vector3.zero,7f,8f,null,Color.white);
        try
        {
            var systems=effect.GetComponentsInChildren<ParticleSystem>(true);Assert.That(systems,Has.Length.EqualTo(3));
            Assert.That(effect.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty);
            Assert.That(systems.All(system=>!system.textureSheetAnimation.enabled),Is.True);
            var volumes=Billows(effect);Assert.That(volumes,Has.Length.EqualTo(2));
            foreach(var volume in volumes)
            {
                var renderer=volume.GetComponent<ParticleSystemRenderer>();Assert.That(renderer.renderMode,Is.EqualTo(ParticleSystemRenderMode.Billboard));
                Assert.That(renderer.sharedMaterial.shader.isSupported,Is.True);
                Assert.That(renderer.sharedMaterial.HasProperty("_MainTex"),Is.False,"The smoke must never sample an image silhouette.");
                Assert.That(renderer.sharedMaterial.HasProperty("_SmokeTime"),Is.True);
                var density=renderer.sharedMaterial.GetTexture("_DensityNoise") as Texture3D;Assert.That(density,Is.Not.Null);
                Assert.That(density.width,Is.EqualTo(32));Assert.That(density.height,Is.EqualTo(32));Assert.That(density.depth,Is.EqualTo(32));
                Assert.That(density.filterMode,Is.EqualTo(FilterMode.Trilinear));Assert.That(density.wrapMode,Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(density.hideFlags,Is.EqualTo(HideFlags.HideAndDontSave),"The density field is generated numerical data, never an authored smoke image.");
                Assert.That(volume.main.simulationSpeed,Is.Zero);
            }
            Assert.That(systems.Sum(system=>system.main.maxParticles),Is.LessThanOrEqualTo(104),"Volumetric samples should not inherit the old large sprite-puff budget.");
        }
        finally{effect.Cancel();}
    }

    [Test]public void AbsoluteSmokeClockIsPerInstanceAndRepeatedAdvanceSurvivesDisableWithoutDrift()
    {
        var first=PixelMushroomDeathBloom.Play(Vector3.zero,7f,8f,null,Color.white);
        var second=PixelMushroomDeathBloom.Play(Vector3.right*4f,7f,8f,null,Color.white);
        try
        {
            first.Advance(1.07f);var before=ReadParticles(Cloud(first));
            first.gameObject.SetActive(false);first.gameObject.SetActive(true);first.Advance(1.07f);var after=ReadParticles(Cloud(first));
            CollectionAssert.AreEqual(before.Select(p=>p.position),after.Select(p=>p.position));
            CollectionAssert.AreEqual(before.Select(p=>p.startSize3D),after.Select(p=>p.startSize3D));
            CollectionAssert.AreEqual(before.Select(p=>p.remainingLifetime),after.Select(p=>p.remainingLifetime));
            second.Advance(3.27f);
            Assert.That(Cloud(first).GetComponent<Renderer>().sharedMaterial,Is.SameAs(Cloud(second).GetComponent<Renderer>().sharedMaterial));
            foreach(var volume in Billows(first))
            {
                var block=new MaterialPropertyBlock();volume.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_SmokeTime"),Is.EqualTo(1.07f));
            }
            foreach(var volume in Billows(second))
            {
                var block=new MaterialPropertyBlock();volume.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_SmokeTime"),Is.EqualTo(3.27f));
            }
            first.Advance(1.27f);Assert.That(ReadParticles(Cloud(first)).Select(p=>p.position).SequenceEqual(after.Select(p=>p.position)),Is.False,"Billows must roll and flow instead of holding a scaled still image.");
        }
        finally{first.Cancel();second.Cancel();}
    }

    [TestCase(false)][TestCase(true)]
    public void NativeSmokeRemainsAConnectedThickMassAtTheCenterAndTheCorner(bool corner)
    {
        Vector3 center=corner?new Vector3(Arena.min.x+.1f,0f,Arena.min.z+.1f):Vector3.zero;float radius=corner?16f:7f;
        var effect=PixelMushroomDeathBloom.Play(center,radius,8.65f,null,Color.white,Arena);
        try
        {
            foreach(float age in new[]{.65f,1.07f,2.65f,4.65f,8.55f})
            {
                effect.Advance(age);var mass=ReadParticles(Cloud(effect));Assert.That(mass.Length,Is.GreaterThan(24));
                Assert.That(ConnectedFraction(mass),Is.GreaterThan(.88f),"Independent smoke rings must join a common cap-connected body at "+age);
                Assert.That(mass.All(p=>p.position.y>=1.2f&&p.position.y<=1.6f),Is.True,"The smoke volume must rise above the floor rather than being clipped to a flat ring.");
                Assert.That(mass.Count(p=>p.startSize3D.y>2f),Is.GreaterThan(mass.Length/2),"Most billows need a substantial vertical body.");
                Assert.That(mass.All(p=>p.startSize3D.y>=.95f&&p.startSize3D.y<=3.01f),Is.True);
                Assert.That(mass.All(p=>p.position.x>=Arena.min.x&&p.position.x<=Arena.max.x&&p.position.z>=Arena.min.z&&p.position.z<=Arena.max.z),Is.True);
                Assert.That(mass.All(p=>new Vector2(p.position.x-center.x,p.position.z-center.z).magnitude<=radius+.001f),Is.True);
                float min=mass.Min(p=>Vector2.Distance(new Vector2(p.position.x,p.position.z),new Vector2(center.x,center.z)));
                Assert.That(min,Is.LessThan(1f),"A hollow ring cannot replace emission from the cap.");
            }
            var block=new MaterialPropertyBlock();Cloud(effect).GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.That(block.GetFloat("_ClipBounds"),Is.EqualTo(1f));
            Assert.That(block.GetVector("_GroundBounds"),Is.EqualTo(new Vector4(Arena.min.x,Arena.max.x,Arena.min.z,Arena.max.z)));
        }
        finally{effect.Cancel();}
    }

    [Test]public void OrthographicPressureApproachExpandsIntoALargeOverlappingWallInsteadOfAThinColumn()
    {
        var cameraObject=new GameObject("Smoke curtain camera");var camera=cameraObject.AddComponent<Camera>();
        camera.transform.position=new Vector3(0f,2f,-20f);camera.orthographic=true;camera.orthographicSize=4.5f;camera.aspect=16f/9f;
        var effect=PixelMushroomDeathBloom.Play(Vector3.zero,8.5f,8f,null,Color.white);
        try
        {
            effect.SetPresentation(camera,1f,4.2f);effect.Advance(.65f);
            var wall=Billows(effect).SelectMany(ReadParticles).ToArray();Assert.That(wall.Length,Is.GreaterThan(24));
            Assert.That(wall.Count(p=>p.startSize3D.x>4f),Is.GreaterThan(12));
            Assert.That(wall.Min(p=>p.position.z),Is.LessThan(-4f));
            Assert.That(wall.Max(p=>p.position.z)-wall.Min(p=>p.position.z),Is.GreaterThan(4f));
            Assert.That(ConnectedFraction(wall),Is.GreaterThan(.88f));
            float width=wall.Max(p=>p.position.x+p.startSize3D.x*.5f)-wall.Min(p=>p.position.x-p.startSize3D.x*.5f);
            float height=wall.Max(p=>p.position.y+p.startSize3D.y*.5f)-wall.Min(p=>p.position.y-p.startSize3D.y*.5f);
            Assert.That(width,Is.GreaterThan(camera.orthographicSize*camera.aspect));
            Assert.That(height,Is.GreaterThan(camera.orthographicSize));
            float orthographicSize=wall.Average(p=>p.startSize3D.x);
            camera.orthographic=false;effect.Advance(.65f);var perspective=Billows(effect).SelectMany(ReadParticles).ToArray();
            Assert.That(orthographicSize,Is.GreaterThan(perspective.Average(p=>p.startSize3D.x)*1.10f),"Orthographic depth travel needs explicit perspective expansion.");
        }
        finally{effect.Cancel();Object.DestroyImmediate(cameraObject);}
    }

    [Test]public void NativeFlowerStaysInFrontOfTheDenseSmokeAndAllNativeAgesKeepSomeSupportingSpores()
    {
        var owner=new GameObject("Bloom flower renderer");var reference=owner.AddComponent<SpriteRenderer>();reference.sprite=PixelMycoking.DeathBloomFrames[4];reference.sortingOrder=23;
        var effect=PixelMushroomDeathBloom.Play(Vector3.zero,7f,8.65f,reference,new Color(.67f,.46f,.76f),Arena);
        try
        {
            var spores=effect.GetComponentsInChildren<ParticleSystem>().Single(system=>system.name=="Green and violet supporting spores");
            for(int step=0;step<=80;step++)
            {
                effect.Advance(.65f+step*.1f);var mass=ReadParticles(Cloud(effect));var dots=ReadParticles(spores);
                Assert.That(mass.Length,Is.GreaterThan(24));Assert.That(dots.Length,Is.InRange(1,8));
                Assert.That(mass.All(p=>p.startColor.r>=220&&p.startColor.g>=220&&p.startColor.b>=220),Is.True,"The procedural green-violet volume must not be muddied by a second dark tint.");
                Assert.That(mass.Count(p=>p.startColor.a>=100),Is.GreaterThan(mass.Length/2));
                Assert.That(mass.Length,Is.GreaterThan(dots.Length*2));
                Assert.That(Billows(effect).All(system=>system.GetComponent<Renderer>().sortingOrder<reference.sortingOrder),Is.True,"Native smoke cannot cover the only visible flower body.");
            }
        }
        finally{effect.Cancel();Object.DestroyImmediate(owner);}
    }

    [Test]public void DestroyedFlowerFinishesExistingBillowsWithoutAnyNewBirthAfterStopEmission()
    {
        var owner=new GameObject("Bloom flower renderer");var reference=owner.AddComponent<SpriteRenderer>();reference.sprite=PixelMycoking.DeathBloomFrames[4];
        var effect=PixelMushroomDeathBloom.Play(Vector3.zero,7f,8f,reference,Color.white,Arena);
        try
        {
            effect.Advance(.80f);Object.DestroyImmediate(owner);effect.StopEmission();Assert.DoesNotThrow(()=>effect.Advance(1.30f));
            var stillAlive=effect.GetComponentsInChildren<ParticleSystem>().SelectMany(ReadParticles).ToArray();Assert.That(stillAlive,Is.Not.Empty);
            Assert.That(stillAlive.All(p=>p.startLifetime<=PixelMushroomDeathBloom.TailDuration),Is.True);
            effect.Advance(.80f+PixelMushroomDeathBloom.TailDuration+.02f);Assert.That(effect.RemainingLifetime,Is.Zero);
            Assert.That(effect.GetComponentsInChildren<ParticleSystem>().All(system=>system.particleCount==0),Is.True);
        }
        finally{effect.Cancel();if(owner!=null)Object.DestroyImmediate(owner);}
        Assert.That(effect==null,Is.True);
    }
}

public sealed class MushroomFinalBloomCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private MushroomColonyRank4Stage stage;
    private Rank4StageContext context;
    private Transform root;
    private Camera camera;
    private T Read<T>(string field)=>(T)typeof(MushroomColonyRank4Stage).GetField(field,Hidden).GetValue(stage);
    private static FrogCatchCinematicStage.Actor Actor(string species,bool shiny=false)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        var prefab=Resources.Load<GameObject>(shiny?definition.shinyResourcePath:definition.resourcePath);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=species,shiny=shiny,sprite=driver.IdleFrames[0],frames=driver.DeathFrames,color=Color.white,size=1f};
    }
    [SetUp]public void Setup()
    {
        context=new Rank4StageContext{family="mushroom",source=Actor("versus"),returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f,returnBodyVisible=false};
        stage=new MushroomColonyRank4Stage(null);Assert.That(stage.Begin(context),Is.True);
        root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
        camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
    }
    [TearDown]public void Cleanup(){stage?.Dispose();}

    [TestCase("versus",false)][TestCase("doksirum",false)][TestCase("lumirum",false)]
    [TestCase("kingmore",false)][TestCase("doksirum",true)]
    public void FallenOriginalSpeciesAndActualShinyRemainRecognizableBeforeTheSharedFlower(string species,bool shiny)
    {
        context.source=Actor(species,shiny);Assert.That(stage.Begin(context),Is.True);stage.Tick(.20f,0f);
        var lead=Read<SpriteRenderer>("lead");Assert.That(lead.enabled,Is.True);
        Assert.That(context.source.frames.Contains(lead.sprite)||lead.sprite==context.source.sprite,Is.True);
        Assert.That(lead.flipX,Is.EqualTo(context.source.flipX));
        stage.Tick(MushroomColonyRank4Stage.FieldReturnTime-.03f,0f);Assert.That(Read<SpriteRenderer>("flower").enabled,Is.True);
        Assert.That(PixelMycoking.DeathBloomFrames.Contains(Read<SpriteRenderer>("flower").sprite),Is.True);
    }

    [Test]public void BloomUsesSeveralAuthoredUnfoldingDrawingsInsideAnOpaqueIndependentStage()
    {
        var heroSeen=new HashSet<Sprite>();
        for(float time=1.2f;time<3.5f;time+=.05f)
        {
            stage.Tick(time,0f);
            foreach(var renderer in new[]{Read<SpriteRenderer>("hero"),Read<SpriteRenderer>("heroBlend")})
                if(renderer.enabled&&renderer.color.a>.01f&&renderer.sprite!=null)heroSeen.Add(renderer.sprite);
        }
        Assert.That(heroSeen.Count,Is.GreaterThanOrEqualTo(4),"Changing one image's scale cannot substitute for authored unfolding frames.");
        Assert.That(heroSeen.All(sprite=>stage.ArtworkFrames.Contains(sprite)),Is.True);
        foreach(float time in new[]{.95f,1.7f,2.85f})
        {stage.Tick(time,0f);Assert.That(camera.backgroundColor.a,Is.EqualTo(1f),"The actual map must not show through the private stage at "+time);}
    }

    [Test]public void CurtainIsMadeOfAnimatedParticleSystemsAndReturnsToTheActualFlowerWithoutAnOverlayBody()
    {
        stage.Tick(3.65f,0f);var smoke=Read<PixelMushroomDeathBloom>("smoke");
        Assert.That(smoke.gameObject.activeSelf,Is.True);
        var systems=smoke.GetComponentsInChildren<ParticleSystem>(true);Assert.That(systems,Has.Length.EqualTo(3));
        Assert.That(systems.All(system=>!system.textureSheetAnimation.enabled),Is.True,"Smoke is procedural; no image sheet may return.");
        var lobes=systems.Where(system=>system.GetComponent<Renderer>().sharedMaterial.shader.name=="Hidden/PixelMushroomDeathBloomSmoke").SelectMany(system=>
        {
            var particles=new ParticleSystem.Particle[system.main.maxParticles];int count=system.GetParticles(particles);
            return particles.Take(count);
        }).Where(p=>p.startSize3D.x>1f&&p.startColor.a>10).ToArray();
        Assert.That(lobes.Length,Is.GreaterThanOrEqualTo(16),"Several overlapping four-lobe packets must form the curtain; decorative sparks cannot satisfy this check.");
        Assert.That(lobes.Count(p=>lobes.Any(other=>other.randomSeed!=p.randomSeed&&
            Vector3.Distance(p.position,other.position)<(p.startSize3D.x+other.startSize3D.x)*.45f)),
            Is.GreaterThanOrEqualTo(lobes.Length*.88f),"The billows must overlap into a connected smoke mass.");
        Assert.That(systems.All(system=>system.gameObject.layer==30),Is.True,"The private stage cannot leak its smoke into the real camera.");
        stage.Tick(MushroomColonyRank4Stage.FieldReturnTime-.03f,0f);var flower=Read<SpriteRenderer>("flower");
        Assert.That(flower.sprite,Is.SameAs(PixelMycoking.DeathBloomFrames[4]));
        Bounds visible=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{flower.sprite});
        Vector3 foot=camera.WorldToViewportPoint(flower.transform.TransformPoint(new Vector3(visible.center.x,visible.min.y,0f)));
        Vector3 top=camera.WorldToViewportPoint(flower.transform.TransformPoint(new Vector3(visible.center.x,visible.max.y,0f)));
        Assert.That(foot.x,Is.EqualTo(.5f+context.returnPosition.x/16f).Within(.002f));
        Assert.That(foot.y,Is.EqualTo(.5f+context.returnPosition.y/9f).Within(.002f));
        Assert.That(top.y-foot.y,Is.EqualTo(context.returnHeight/9f).Within(.002f));
        stage.Tick(MushroomColonyRank4Stage.FieldReturnTime,0f);
        Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer=>renderer.enabled),Is.False);
        Assert.That(camera.backgroundColor.a,Is.Zero);Assert.That(stage.ActionTime,Is.Zero);
        stage.Tick(MushroomColonyRank4Stage.FieldReturnTime+.42f,0f);
        Assert.That(stage.ActionTime,Is.EqualTo(.42f).Within(.0001f));
        stage.Tick(stage.Duration,0f);
        Assert.That(smoke.gameObject.activeSelf,Is.False);Assert.That(stage.Opacity,Is.Zero);
        Assert.That(stage.Begin(context),Is.True);stage.Tick(.95f,0f);
        Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));
        stage.Dispose();Assert.That(PixelMycoking.DeathBloomFrames[4],Is.Not.Null,"Disposing the movie cannot destroy shared native flower frames.");
    }
}
