using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SpiritDeerWaterMantleV02Tests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private const string ArtRoot="CombatCinematics/Rank4V3/spirit_deer/WaterMantleV02/";
    private SpiritGuardianRank4Stage stage;
    private Rank4StageContext context;
    private Transform root;
    private Camera camera;
    private Sprite nativeGuardian;

    [SetUp] public void Setup()
    {
        var texture=Resources.Load<Texture2D>("CombatEffects/SpiritDeer/GuardianGoddess16");
        Assert.That(texture,Is.Not.Null);
        nativeGuardian=Sprite.Create(texture,new Rect(0,0,texture.width/4f,texture.height/4f),Vector2.one*.5f,64);
        context=new Rank4StageContext{family="spirit_deer",source=Actor("holydeer"),
            allies=new[]{Actor("holydeer"),Actor("golgaebi"),Actor("geomchi"),Actor("woongi")},
            returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f,returnBodyVisible=true,
            guardianReturnSprite=nativeGuardian,guardianReturnPosition=new Vector2(.8f,1.2f),
            guardianReturnHeight=3.6f,guardianReturnColor=new Color(.84f,.79f,1f,.46f),guardianReturnVisible=true,
            guardianReturnRearEdge=.76f};
        stage=new SpiritGuardianRank4Stage(null);
        Assert.That(stage.Begin(context),Is.True);
        root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
        camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
    }

    [TearDown] public void Cleanup(){stage?.Dispose();Object.DestroyImmediate(nativeGuardian);}

    [TestCase("Guardian8",4,2,true)]
    [TestCase("Sleeve4",2,2,true)]
    [TestCase("WaterShots12",4,3,false)]
    [TestCase("Hoof20",4,5,true)]
    public void EachStageSequenceContainsAtLeastFourDistinctAuthoredFramesAndTheCorrectAlphaMask(
        string name,int columns,int rows,bool cutout)
    {
        var texture=Resources.Load<Texture2D>(ArtRoot+name);
        Assert.That(texture,Is.Not.Null);Assert.That(texture.isReadable,Is.True);
        Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(texture.width%columns,Is.Zero);Assert.That(texture.height%rows,Is.Zero);
        var frames=stage.ArtworkFrames.Where(s=>s.name.StartsWith(ArtRoot+name+" ",StringComparison.Ordinal)).ToArray();
        Assert.That(frames,Has.Length.EqualTo(columns*rows));
        var signatures=new HashSet<uint>();
        foreach(var frame in frames)
        {
            Rect rect=frame.rect;
            var pixels=texture.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height);
            int clear=0,solid=0;uint hash=2166136261;
            foreach(Color pixel in pixels)
            {
                Color32 color=pixel;if(color.a<8)clear++;if(color.a>240)solid++;
                unchecked{hash=(hash^color.r)*16777619;hash=(hash^color.g)*16777619;
                    hash=(hash^color.b)*16777619;hash=(hash^color.a)*16777619;}
            }
            Assert.That(signatures.Add(hash),Is.True,frame.name+": duplicated pictures cannot supply animation.");
            Assert.That(solid,Is.GreaterThan(pixels.Length*(cutout?.015f:.99f)),frame.name);
            if(cutout)Assert.That(clear,Is.GreaterThan(pixels.Length*.05f),frame.name+": a real alpha cutout is required.");
        }
    }

    [TestCase(.80f)] [TestCase(1.60f)] [TestCase(2.50f)]
    public void UnderwaterDescentAndMantleStageRemainOpaqueUntilTheFieldReveal(float time)
    {
        stage.Tick(time,0f);
        Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));
        Assert.That(stage.Opacity,Is.EqualTo(1f));
        Assert.That(stage.ActionTime,Is.EqualTo(-1f),"The battle-start protection must never be reapplied by a film cue.");
    }

    [Test]
    public void TheFilmActuallyShowsFourDifferentDrawingsForEachPrimarySpriteAnimation()
    {
        var names=new[]{"Guardian8","Sleeve4","WaterShots12","Hoof20"};
        var seen=names.ToDictionary(name=>name,name=>new HashSet<Sprite>());
        for(float time=0f;time<SpiritGuardianRank4Stage.FieldRevealTime;time+=.015f)
        {
            stage.Tick(time,0f);
            foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if(!renderer.enabled||renderer.color.a<=.04f||renderer.sprite==null)continue;
                foreach(string name in names)
                    if(renderer.sprite.name.StartsWith(ArtRoot+name+" ",StringComparison.Ordinal))seen[name].Add(renderer.sprite);
            }
        }
        foreach(string name in names)Assert.That(seen[name].Count,Is.GreaterThanOrEqualTo(4),name+": imported frames must be used in playback.");
    }

    [Test]
    public void FineRipplesDropletsAndStatusParticlesSurviveViewportToWorldPrecisionAtEveryStageShot()
    {
        var system=(ParticleSystem)typeof(Rank4CinematicStage).GetField("pixels",Hidden).GetValue(stage);
        var particles=new ParticleSystem.Particle[system.main.maxParticles];
        foreach(float time in new[]{.28f,.80f,1.60f,2.50f,3.10f})
        {
            stage.Tick(time,0f);
            int count=system.GetParticles(particles);
            Assert.That(count,Is.GreaterThan(40),"Shot at "+time+": small viewport spans must not collapse to zero and discard the particles.");
            for(int i=0;i<count;i++)
            {
                Vector3 position=particles[i].position,size=particles[i].startSize3D;
                foreach(float value in new[]{position.x,position.y,position.z,size.x,size.y,size.z})
                    Assert.That(float.IsNaN(value)||float.IsInfinity(value),Is.False,"Shot at "+time+", particle "+i);
                Assert.That(size.x,Is.GreaterThan(0f));Assert.That(size.y,Is.GreaterThan(0f));
            }
        }
    }

    [Test]
    public void UnderwaterAndOverShoulderShotsUseDifferentPhysicalCameraDirectionsAndForegroundDepth()
    {
        stage.Tick(.80f,0f);
        Assert.That(camera.orthographic,Is.False);
        Assert.That(Vector3.Dot(camera.transform.forward,Vector3.up),Is.GreaterThan(.45f));
        Quaternion underwater=camera.transform.localRotation;
        stage.Tick(2.50f,0f);
        Assert.That(camera.orthographic,Is.False);
        Assert.That(camera.transform.forward.y,Is.LessThan(-.2f));
        Assert.That(Quaternion.Angle(underwater,camera.transform.localRotation),Is.GreaterThan(45f));
        var shoulder=Renderer("Near shoulder and flowing sleeve");var ally=Renderer("Protected ally 0");
        Assert.That(shoulder.enabled,Is.True);Assert.That(ally.enabled,Is.True);
        Assert.That(shoulder.sprite.name,Does.StartWith(ArtRoot+"Sleeve4 "));
        float shoulderDepth=camera.WorldToViewportPoint(shoulder.bounds.center).z;
        float allyDepth=camera.WorldToViewportPoint(ally.bounds.center).z;
        Assert.That(shoulderDepth,Is.GreaterThan(camera.nearClipPlane));
        Assert.That(shoulderDepth,Is.LessThan(allyDepth-.1f),"The near sleeve must frame actual allies behind it.");
    }

    [Test]
    public void TheMantleProtectsTheActualPartySnapshotsWithTheirFacingAndColors()
    {
        for(int i=0;i<context.allies.Length;i++)
        {
            var ally=context.allies[i];ally.flipX=i%2==1;ally.color=new Color(.6f+i*.1f,.9f,.8f,1f);context.allies[i]=ally;
        }
        Assert.That(stage.Begin(context),Is.True);stage.Tick(2.50f,0f);
        for(int i=0;i<context.allies.Length;i++)
        {
            var renderer=Renderer("Protected ally "+i);var ally=context.allies[i];
            Assert.That(renderer.enabled,Is.True);Assert.That(renderer.sprite,Is.SameAs(ally.sprite));
            Assert.That(renderer.flipX,Is.EqualTo(ally.flipX));Assert.That(renderer.color,Is.EqualTo(ally.color));
        }
        Assert.That(Renderer("Protected ally 4").enabled,Is.False,"No imaginary party members may fill unoccupied places.");
    }

    [Test]
    public void ReturnMatchesTheActualNativePrayingGuardianPoseFeetHeightAndTintWithoutRevivingTheCaster()
    {
        context.returnBodyVisible=false;stage.Tick(3.45f,0f);
        Assert.That(camera.backgroundColor.a,Is.Zero);
        var guardian=Renderer("Native praying guardian handoff");
        Assert.That(guardian.enabled,Is.True);Assert.That(guardian.sprite,Is.SameAs(nativeGuardian));
        Bounds visible=VisibleBody(nativeGuardian);
        Vector3 feet=guardian.transform.localPosition+guardian.transform.localRotation*
            (new Vector3(visible.center.x,visible.min.y,0f)*guardian.transform.localScale.y);
        Assert.That(Vector2.Distance(feet,context.guardianReturnPosition),Is.LessThan(.001f));
        Assert.That(visible.size.y*guardian.transform.localScale.y,Is.EqualTo(context.guardianReturnHeight).Within(.001f));
        Assert.That(guardian.color,Is.EqualTo(context.guardianReturnColor));
        Assert.That(guardian.sharedMaterial.shader,Is.SameAs(Resources.Load<Shader>("CombatEffects/SpiritDeer/GuardianVeil")));
        Assert.That(guardian.sharedMaterial.GetFloat("_RearEdge"),Is.EqualTo(context.guardianReturnRearEdge));
        Assert.That(Renderer("Lead spirit_deer").enabled,Is.False);
        context.guardianReturnVisible=false;stage.Tick(3.45f,0f);
        Assert.That(guardian.enabled,Is.False,"A removed native guardian must not be redrawn by the overlay.");
        context.guardianReturnVisible=true;stage.Tick(3.85f,0f);
        Assert.That(guardian.enabled,Is.False,"The native field guardian has returned; a second overlay would double its opacity.");
    }

    [Test]
    public void PassivePlaybackReplayAndCleanupPreserveBattleTimeRandomAndSharedNativeArtwork()
    {
        float speed=Time.timeScale;string random=JsonUtility.ToJson(UnityEngine.Random.state);
        Texture nativeTexture=nativeGuardian.texture;
        var privateFrames=stage.ArtworkFrames.ToArray();var privateTarget=stage.Texture;
        foreach(float time in new[]{0f,.80f,1.60f,2.50f,3.10f,3.85f,stage.Duration,float.NaN,float.PositiveInfinity})
        {
            stage.Tick(time,.016f);
            Assert.That(stage.ActionTime,Is.EqualTo(-1f));Assert.That(Time.timeScale,Is.EqualTo(speed));
            Assert.That(float.IsNaN(stage.Opacity)||float.IsInfinity(stage.Opacity),Is.False);
            Assert.That(stage.Opacity,Is.InRange(0f,1f));
        }
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
        stage.Tick(stage.Duration,0f);Assert.That(stage.Opacity,Is.Zero);
        stage.Hide();Assert.That(stage.Active,Is.False);
        Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.particleCount==0),Is.True);
        Assert.That(stage.Begin(context),Is.True);stage.Tick(.80f,0f);
        Assert.That(stage.Opacity,Is.EqualTo(1f));Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));
        stage.Dispose();stage.Dispose();Assert.That(stage.Active,Is.False);
        Assert.That(privateFrames.All(frame=>frame==null),Is.True);
        Assert.That(privateTarget==null,Is.True);
        Assert.That(nativeGuardian!=null&&nativeTexture!=null,Is.True,"Disposal owns stage copies, not the native field resources.");
    }

    private SpriteRenderer Renderer(string name)=>root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);
    private static Bounds VisibleBody(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{sprite});
    private static FrogCatchCinematicStage.Actor Actor(string species)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        Assert.That(definition,Is.Not.Null,species);
        var driver=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=species,sprite=driver.IdleFrames[0],frames=driver.IdleFrames,color=Color.white,size=1f};
    }
}
