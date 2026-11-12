using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MonkeyStormCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private GameObject host;
    private MonkeyCircusRank4Stage stage;
    private Rank4StageContext context;
    private Camera camera;
    private Transform root;
    private T Read<T>(string name)=>(T)typeof(MonkeyCircusRank4Stage).GetField(name,Hidden).GetValue(stage);

    [SetUp]public void Setup()
    {
        host=new GameObject("Monkey first-person thunderstorm cinematic contract");
        context=new Rank4StageContext{family="monkey",source=Actor("chiwoo"),
            returnPosition=new Vector2(-2f,-1.3f),returnHeight=1.1f,targets=new[]{Actor("toto")}};
        stage=new MonkeyCircusRank4Stage(host.transform);Assert.That(stage.Begin(context),Is.True);
        camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
        root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
    }

    [TearDown]public void Cleanup(){stage?.Dispose();if(host!=null)Object.DestroyImmediate(host);}

    private static FrogCatchCinematicStage.Actor Actor(string species,bool shiny=false)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        Assert.That(definition,Is.Not.Null,species);
        var prefab=Resources.Load<GameObject>(shiny?"Enemies/Shiny/"+species:definition.resourcePath);
        Assert.That(prefab,Is.Not.Null,"Use the actual species or shiny roster prefab.");
        var animation=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=species,shiny=shiny,sprite=animation.IdleFrames[0],size=1f,color=Color.white};
    }

    [TestCase("chiwoo",false)][TestCase("toto",false)][TestCase("uno",false)]
    [TestCase("maka",false)][TestCase("uno",true)]
    public void EverySpeciesAndActualUnoShinyUseCasterPovWithoutStagedCharacters(string species,bool shiny)
    {
        context.source=Actor(species,shiny);var sky=Read<SpriteRenderer>("sky");var banks=Read<SpriteRenderer[]>("banks");
        foreach(bool enemy in new[]{false,true})
        {
            context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
            foreach(float time in new[]{.92f,1.95f,2.79f})
            {
                stage.Tick(time,0f);Assert.That(sky.enabled,Is.True);
                foreach(var sprite in root.GetComponentsInChildren<SpriteRenderer>(true).Where(item=>item.enabled))
                    Assert.That(sprite==sky||banks.Contains(sprite),Is.True,"The POV shot contains sky and clouds, without a conductor or other staged monster.");
                Assert.That(Read<SpriteRenderer>("returned").enabled,Is.False);
            }
            Assert.That(stage.CurrentContext.source.shiny,Is.EqualTo(shiny));
            stage.Tick(MonkeyCircusRank4Stage.FieldReturnTime,0f);
            Assert.That(Read<SpriteRenderer>("returned").sprite,Is.SameAs(context.source.sprite));
        }
        if(shiny)Assert.That(context.source.sprite,Is.Not.SameAs(Actor("uno").sprite));
    }

    [Test]public void CasterEyeStaysInPlaceWhileLookingUpThenHoldsTheGatheredSky()
    {
        Vector3 eye=Vector3.zero;float previous=-1f;
        foreach(float time in new[]{.46f,.70f,1f,1.3f,1.6f})
        {
            stage.Tick(time,0f);Assert.That(camera.orthographic,Is.False);
            if(previous>=0f)Assert.That(camera.transform.localPosition,Is.EqualTo(eye),"A caster POV tilt must not orbit or cut to an external stage camera.");
            eye=camera.transform.localPosition;
            float up=(camera.transform.localRotation*Vector3.forward).y;
            Assert.That(up,Is.GreaterThan(previous));previous=up;
        }
        Assert.That(previous,Is.GreaterThan(.98f));Quaternion held=camera.transform.localRotation;
        stage.Tick(2.6f,0f);Assert.That(Quaternion.Angle(held,camera.transform.localRotation),Is.LessThan(.001f));
        stage.Tick(MonkeyCircusRank4Stage.FieldReturnTime,0f);Assert.That(camera.orthographic,Is.True);
    }

    [Test]public void EightLargeEdgeCloudBanksPlayFourExistingFramesAndLeaveTheSkyCenterOpen()
    {
        var banks=Read<SpriteRenderer[]>("banks");Assert.That(banks,Has.Length.EqualTo(12));
        var frames=Read<Sprite[]>("clouds");Assert.That(frames,Has.Length.EqualTo(4));
        Assert.That(frames.Select(Fingerprint).Distinct().Count(),Is.EqualTo(4));
        var shown=new HashSet<Sprite>();
        foreach(float time in new[]{.90f,1.07f,1.24f,1.41f}){stage.Tick(time,0f);shown.Add(banks[0].sprite);}
        Assert.That(shown.Count,Is.EqualTo(4));
        stage.Tick(2.52f,0f);var visible=banks.Where(bank=>bank.enabled).ToArray();
        Assert.That(visible,Has.Length.EqualTo(8));
        foreach(var bank in visible)
            Assert.That(new Vector2(bank.transform.localPosition.x,bank.transform.localPosition.z-2f).magnitude,
                Is.GreaterThan(10f),"Large clouds frame the edge instead of filling the center with repeated small tiles.");
        Assert.That(Read<Sprite>("background").texture,Is.SameAs(Resources.Load<Texture2D>(MonkeyCircusRank4Stage.SkyResourcePath)),
            "Use the existing sky without an illustrated lightning strike baked into it.");
    }

    [Test]public void ExistingLightningWaitsForItsCueAndItsFlashHasASharpRiseAndShortDecay()
    {
        var thunder=Read<PixelHaetaeThunder>("thunder");float cue=MonkeyCircusRank4Stage.ThunderTime;
        stage.Tick(cue-.001f,0f);Assert.That(thunder.gameObject.activeSelf,Is.False);
        stage.Tick(cue,0f);Assert.That(thunder.gameObject.activeSelf,Is.True);
        Assert.That(thunder.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty,"Use the existing Zap prefab without a generated sprite overlay.");
        Assert.That(MonkeyCircusRank4Stage.ThunderFlashAt(cue-.001f),Is.Zero);
        Assert.That(MonkeyCircusRank4Stage.ThunderFlashAt(cue),Is.Zero);
        Assert.That(MonkeyCircusRank4Stage.ThunderFlashAt(cue+.025f),Is.GreaterThan(.999f));
        float previous=1f;
        foreach(float age in new[]{.04f,.08f,.13f,.18f,.22f})
        {
            float flash=MonkeyCircusRank4Stage.ThunderFlashAt(cue+age);
            Assert.That(flash,Is.InRange(0f,previous+.00001f));previous=flash;
        }
        Assert.That(MonkeyCircusRank4Stage.ThunderFlashAt(cue+.221f),Is.Zero);
        Assert.That(stage.ActionTime,Is.EqualTo(-1f),"The theatrical sky strike does not trigger gameplay damage.");
    }

    [Test]public void NativeDamageStartsAfterFieldReturnAndKeepsOneContinuousFourSecondStormClock()
    {
        float originalScale=Time.timeScale,previous=-1f;int crossings=0;
        Assert.That(MonkeyCircusRank4Stage.EarliestImpactTime,Is.EqualTo(MonkeyCircusRank4Stage.FieldReturnTime+MonkeyStormRules.EarliestImpact).Within(.00001f));
        for(int frame=0;frame<=Mathf.CeilToInt(stage.Duration*120f);frame++)
        {
            float time=frame/120f,current=MonkeyCircusRank4Stage.CastTimeAt(time);
            Assert.That(current,Is.GreaterThanOrEqualTo(previous-.00001f));
            if(time<MonkeyCircusRank4Stage.FieldReturnTime)Assert.That(current,Is.EqualTo(-1f));
            if(time<MonkeyCircusRank4Stage.EarliestImpactTime-.00001f)Assert.That(current,Is.LessThan(MonkeyStormRules.EarliestImpact));
            if(previous<MonkeyStormRules.EarliestImpact&&current>=MonkeyStormRules.EarliestImpact)crossings++;
            previous=current;
        }
        Assert.That(crossings,Is.Zero,"Even the earliest possible random bolt lands after the sky shot finishes.");
        stage.Tick(MonkeyCircusRank4Stage.FieldReturnTime,0f);Assert.That(stage.ActionTime,Is.Zero);
        Assert.That(MonkeyCircusRank4Stage.EarliestImpactTime,Is.GreaterThan(stage.Duration));
        Assert.That(MonkeyCircusRank4Stage.CastTimeAt(MonkeyCircusRank4Stage.EarliestImpactTime),Is.EqualTo(MonkeyStormRules.EarliestImpact).Within(.00001f));
        stage.Tick(stage.Duration,0f);
        Assert.That(stage.ActionTime,Is.EqualTo(stage.Duration-MonkeyCircusRank4Stage.FieldReturnTime).Within(.00001f));
        Assert.That(stage.ActionTime,Is.LessThan(MonkeyStormRules.Duration),"The four-second gameplay storm continues after the brief sky shot.");
        Assert.That(Time.timeScale,Is.EqualTo(originalScale));
    }

    [Test]public void OpaqueSkyBecomesATransparentFieldWithoutADoubleCaster()
    {
        stage.Tick(.20f,0f);Assert.That(camera.backgroundColor.a,Is.Zero);
        stage.Tick(.80f,0f);Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));Assert.That(stage.Opacity,Is.EqualTo(1f));
        stage.Tick(3.49f,0f);Assert.That(stage.Opacity,Is.LessThan(.02f));
        context.returnBodyVisible=false;stage.Tick(MonkeyCircusRank4Stage.FieldReturnTime,0f);
        Assert.That(camera.backgroundColor.a,Is.Zero);Assert.That(Read<SpriteRenderer>("sky").enabled,Is.False);
        Assert.That(Read<SpriteRenderer>("returned").enabled,Is.False);
        Assert.That(Read<PixelHaetaeThunder>("thunder").gameObject.activeSelf,Is.False);
        context.returnBodyVisible=true;stage.Tick(MonkeyCircusRank4Stage.FieldReturnTime,0f);
        Assert.That(Read<SpriteRenderer>("returned").sprite,Is.SameAs(context.source.sprite));
        stage.Tick(stage.Duration,0f);Assert.That(stage.Opacity,Is.Zero);
    }

    [Test]public void NoGeneratedStageArtworkIsLoadedAndReentryAndDisposalReleaseOwnedResources()
    {
        foreach(var image in stage.ArtworkFrames)
        {
            Assert.That(image.name.StartsWith(MonkeyCircusRank4Stage.SkyResourcePath)||image.name.StartsWith(PixelMonkeyStorm.CloudResourcePath),Is.True,image.name);
            Assert.That(image.name,Does.Not.Contain("MonkeyStormV1"));Assert.That(image.name,Does.Not.Contain("Rank4V3/monkey/"));
        }
        Assert.That(root.GetComponentsInChildren<PixelMonkeyNimbus>(true),Is.Empty);
        Assert.That(root.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
        var owned=stage.ArtworkFrames.ToArray();var target=stage.Texture;Sprite roster=context.source.sprite;
        stage.Tick(2.85f,0f);stage.Hide();Assert.That(stage.Active,Is.False);Assert.That(root.gameObject.activeSelf,Is.False);
        Assert.That(stage.Begin(context),Is.True);Assert.That(Read<PixelHaetaeThunder>("thunder").gameObject.activeSelf,Is.False);
        stage.Dispose();stage.Dispose();
        Assert.That(stage.Active,Is.False);Assert.That(root==null&&camera==null&&target==null,Is.True);
        Assert.That(owned.All(image=>image==null),Is.True);Assert.That(roster,Is.Not.Null);
    }

    private static ulong Fingerprint(Sprite sprite)
    {
        int width=(int)sprite.rect.width,height=(int)sprite.rect.height;
        Color[] pixels=sprite.texture.GetPixels((int)sprite.rect.x,(int)sprite.rect.y,width,height);
        ulong hash=14695981039346656037UL;
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            Color32 pixel=pixels[Mathf.Clamp((int)((y+.5f)*height/64f),0,height-1)*width+Mathf.Clamp((int)((x+.5f)*width/64f),0,width-1)];
            uint value=(uint)(pixel.r|pixel.g<<8|pixel.b<<16|pixel.a<<24);hash=unchecked((hash^value)*1099511628211UL);
        }
        return hash;
    }
}
