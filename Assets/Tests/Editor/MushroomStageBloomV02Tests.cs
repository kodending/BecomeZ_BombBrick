using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MushroomStageBloomV02Tests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private const string ArtRoot="CombatCinematics/Rank4V3/mushroom/StageBloomV02/";
    private MushroomColonyRank4Stage stage;
    private Rank4StageContext context;
    private Transform root;
    private Camera camera;

    [SetUp] public void Setup()
    {
        context=new Rank4StageContext{family="mushroom",source=Actor("versus",false),
            returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f,returnBodyVisible=false};
        stage=new MushroomColonyRank4Stage(null);
        Assert.That(stage.Begin(context),Is.True);
        root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
        camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
    }

    [TearDown] public void Cleanup(){stage?.Dispose();}

    [TestCase("Hero8",8,4,2,true)]
    [TestCase("Chamber4",4,2,2,false)]
    [TestCase("SporeInterior4",4,2,2,false)]
    public void NewStageArtContainsIndependentDrawingsWithTheRequiredOpaqueAndCutoutMasks(
        string name,int count,int columns,int rows,bool cutout)
    {
        var frames=stage.ArtworkFrames.Where(s=>s.name.StartsWith(ArtRoot+name+" ",StringComparison.Ordinal)).ToArray();
        Assert.That(frames,Has.Length.EqualTo(count));
        var texture=Resources.Load<Texture2D>(ArtRoot+name);
        Assert.That(texture,Is.Not.Null);
        Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(texture.isReadable,Is.True);
        Assert.That(texture.width%columns,Is.Zero);Assert.That(texture.height%rows,Is.Zero);
        var signatures=new HashSet<uint>();
        foreach(var frame in frames)
        {
            Rect rect=frame.rect;
            var pixels=texture.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height);
            int clear=0,solid=0;uint hash=2166136261;
            foreach(Color pixel in pixels)
            {
                Color32 color=pixel;
                if(color.a<8)clear++;if(color.a>240)solid++;
                unchecked{hash=(hash^color.r)*16777619;hash=(hash^color.g)*16777619;
                    hash=(hash^color.b)*16777619;hash=(hash^color.a)*16777619;}
            }
            Assert.That(signatures.Add(hash),Is.True,name+" must not repeat the same drawing in different cells.");
            Assert.That(solid,Is.GreaterThan(pixels.Length*(cutout?.04f:.99f)),frame.name);
            if(cutout)Assert.That(clear,Is.GreaterThan(pixels.Length*.05f),"The stage flower needs a genuine alpha cutout.");
        }
    }

    [TestCase(.95f)] [TestCase(1.70f)] [TestCase(2.85f)] [TestCase(3.55f)]
    public void TheMicroscopicAndBloomSetIsOpaqueUntilTheSporeCurtainRevealsTheBattle(float time)
    {
        stage.Tick(time,0f);
        Assert.That(stage.Opacity,Is.EqualTo(1f));
        Assert.That(camera.backgroundColor.a,Is.EqualTo(1f),"The real map must not show through the new stage.");
        Assert.That(stage.ActionTime,Is.LessThan(0f),"The flower's damage clock cannot run behind its reveal.");
        Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy),Is.True);
    }

    [Test]
    public void RootRiseAndOpeningUseAtLeastFourAuthoredHeroDrawingsBeforeTheHeldFullBloom()
    {
        var seen=new HashSet<Sprite>();
        var hero=Read<SpriteRenderer>("hero");var blend=Read<SpriteRenderer>("heroBlend");
        foreach(float time in new[]{1.30f,1.55f,1.80f,2.05f,2.30f,2.55f,2.80f,3.05f})
        {
            stage.Tick(time,0f);
            foreach(var renderer in new[]{hero,blend})
                if(renderer.enabled&&renderer.color.a>.04f&&renderer.sprite!=null)seen.Add(renderer.sprite);
        }
        Assert.That(seen.Count,Is.GreaterThanOrEqualTo(4),"A transformed single picture cannot stand in for the unfolding caps.");
        Assert.That(seen.All(sprite=>sprite.name.StartsWith(ArtRoot+"Hero8 ",StringComparison.Ordinal)),Is.True);
        Assert.That(Read<SpriteRenderer>("flower").enabled,Is.False,"Native field art must not replace the hero before the curtain.");
    }

    [TestCase("versus",false)] [TestCase("doksirum",false)] [TestCase("lumirum",false)]
    [TestCase("kingmore",false)] [TestCase("doksirum",true)]
    public void TheOpeningUsesTheActualFallenSpeciesAndShinyDeathFrames(string species,bool shiny)
    {
        context.source=Actor(species,shiny);context.source.flipX=true;context.enemy=true;
        Assert.That(stage.Begin(context),Is.True);stage.Tick(.20f,0f);
        var lead=Read<SpriteRenderer>("lead");
        Assert.That(lead.enabled,Is.True);Assert.That(lead.flipX,Is.True);
        Assert.That(context.source.frames.Contains(lead.sprite),Is.True);
        Assert.That(stage.CurrentContext.source.species,Is.EqualTo(species));
        Assert.That(stage.CurrentContext.source.shiny,Is.EqualTo(shiny));
    }

    [Test]
    public void TheCurtainUsesProceduralBillowsAndHandsOffOnlyOneNativeFlowerAtTheRealPosition()
    {
        stage.Tick(3.65f,0f);
        var smoke=Read<PixelMushroomDeathBloom>("smoke");
        Assert.That(smoke.gameObject.activeInHierarchy,Is.True);
        var systems=smoke.GetComponentsInChildren<ParticleSystem>(true);
        Assert.That(systems,Has.Length.EqualTo(3));
        Assert.That(systems.All(p=>!p.textureSheetAnimation.enabled),Is.True);
        Assert.That(systems.Any(p=>p.particleCount>0),Is.True);
        Assert.That(systems.All(p=>p.gameObject.layer==30),Is.True);
        Assert.That(systems.Count(p=>p.GetComponent<Renderer>().sharedMaterial.shader.name=="Hidden/PixelMushroomDeathBloomSmoke"),Is.EqualTo(2));

        stage.Tick(MushroomColonyRank4Stage.FieldReturnTime,0f);
        Assert.That(stage.ActionTime,Is.Zero);
        Assert.That(camera.backgroundColor.a,Is.Zero);
        Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled),Is.False);
        stage.Tick(stage.Duration,0f);
        Assert.That(stage.Opacity,Is.Zero);
        Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).Any(p=>p.gameObject.activeInHierarchy&&p.particleCount>0),Is.False);
        Assert.That(stage.ActionTime,Is.EqualTo(stage.Duration-MushroomColonyRank4Stage.FieldReturnTime).Within(.00001f));
        Assert.That(context.returnPosition,Is.EqualTo(new Vector2(-2f,-2f)));
    }

    [Test]
    public void PlaybackAndReplayPreserveTheWorldClockRandomAndSharedNativeFlowerResources()
    {
        float speed=Time.timeScale;string random=JsonUtility.ToJson(UnityEngine.Random.state);
        var native=PixelMycoking.DeathBloomFrames.ToArray();
        foreach(float time in new[]{0f,.95f,1.70f,2.85f,3.65f,4.55f,float.NaN,float.PositiveInfinity})
        {
            stage.Tick(time,.016f);
            Assert.That(Time.timeScale,Is.EqualTo(speed));
            Assert.That(float.IsNaN(stage.Opacity)||float.IsInfinity(stage.Opacity),Is.False);
            Assert.That(stage.Opacity,Is.InRange(0f,1f));
            Assert.That(float.IsNaN(camera.orthographicSize)||float.IsInfinity(camera.orthographicSize),Is.False);
        }
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
        stage.Hide();Assert.That(stage.Active,Is.False);
        Assert.That(stage.Begin(context),Is.True);stage.Tick(.95f,0f);
        Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));Assert.That(stage.Opacity,Is.EqualTo(1f));
        stage.Dispose();stage.Dispose();Assert.That(stage.Active,Is.False);
        CollectionAssert.AreEqual(native,PixelMycoking.DeathBloomFrames);
        Assert.That(native.All(sprite=>sprite!=null),Is.True);
    }

    private T Read<T>(string name)=>(T)typeof(MushroomColonyRank4Stage).GetField(name,Hidden).GetValue(stage);

    private static FrogCatchCinematicStage.Actor Actor(string species,bool shiny)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        var prefab=Resources.Load<GameObject>(shiny?definition.shinyResourcePath:definition.resourcePath);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=species,shiny=shiny,sprite=driver.IdleFrames[0],
            frames=driver.DeathFrames,color=Color.white,size=1f};
    }
}
