using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class GriffinStormOrbCinematicTests
{
    private const string ArtRoot="CombatCinematics/GriffinStormOrbV1/";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private GameObject host;
    private GriffinCrosswindRank4Stage stage;
    private Rank4StageContext context;
    private Camera View=>host.GetComponentInChildren<Camera>(true);
    private SpriteRenderer Lead=>Renderer("Lead storm orb griffin");
    private SpriteRenderer Orb=>Renderer("Sovereign storm orb core");
    private SpriteRenderer Renderer(string name)=>host.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);

    [SetUp]public void Setup()
    {
        host=new GameObject("Storm orb cinematic checks");
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d!=null&&d.monsterId=="pone");
        var driver=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var actor=new FrogCatchCinematicStage.Actor{species="pone",sprite=driver.IdleFrames[0],frames=driver.IdleFrames,size=1f,color=Color.white};
        context=new Rank4StageContext{family="griffin",source=actor,returnSprite=actor.sprite,
            returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f};
        stage=new GriffinCrosswindRank4Stage(host.transform);Assert.That(stage.Begin(context),Is.True);
    }
    [TearDown]public void Cleanup(){stage?.Dispose();Object.DestroyImmediate(host);}

    [TestCase("pone",false,0)][TestCase("griphonas",false,1)][TestCase("holophonas",false,2)]
    [TestCase("entpone",false,3)][TestCase("holophonas",true,4)]
    public void EachSpeciesPlaysTwelveDrawnWingPosesAtFixedScaleWithinEachCameraGroup(string species,bool shiny,int row)
    {
        var actor=context.source;actor.species=species;actor.shiny=shiny;context.source=actor;Assert.That(stage.Begin(context),Is.True);
        var texture=Resources.Load<Texture2D>(ArtRoot+"Body60");var seen=new HashSet<int>();var scales=new Dictionary<int,Vector3>();
        for(float time=.26f;time<3.10f;time+=.012f)
        {
            stage.Tick(time,0f);Assert.That(Lead.enabled,Is.True);Assert.That(Lead.sprite.texture,Is.SameAs(texture));
            int frame=Cell(Lead.sprite,4,15);seen.Add(frame);Assert.That(frame,Is.InRange(row*12,row*12+11));
            int group=(frame-row*12)/4;
            if(scales.ContainsKey(group))Assert.That(Vector3.Distance(scales[group],Lead.transform.localScale),Is.LessThan(.00001f));
            else scales.Add(group,Lead.transform.localScale);
        }
        Assert.That(seen.Count,Is.EqualTo(12),"Every group needs four actual drawings, not one moving illustration.");
        Assert.That(scales.Count,Is.EqualTo(3));
    }

    [Test]
    public void BothEightFrameEffectsAnimateAndThereIsOnlyOneContinuousCoreRenderer()
    {
        var core=new HashSet<int>();var burst=new HashSet<int>();
        var released=Renderer("Released storm orb feather pressure");
        stage.Tick(.15f,0f);Assert.That(Orb.enabled,Is.False);
        for(float time=.45f;time<3.10f;time+=.006f)
        {
            stage.Tick(time,0f);if(Orb.enabled)core.Add(Cell(Orb.sprite,4,2));if(released.enabled)burst.Add(Cell(released.sprite,4,2));
            Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.name=="Sovereign storm orb core"),Is.EqualTo(1));
        }
        Assert.That(core.Count,Is.EqualTo(8));Assert.That(burst.Count,Is.EqualTo(8));
        Assert.That(stage.GetType().BaseType,Is.EqualTo(typeof(Rank4CinematicStage)));
    }

    [TestCase("PrimordialMeadow")][TestCase("PoisonFogSwamp")][TestCase("CrimsonJungle")]
    [TestCase("FrozenHighlands")][TestCase("SkySanctuary")]
    public void PrivateShotsUseTheirActualRegionalCurvedSceneryAndThreeDimensionalWind(string region)
    {
        context.sceneryRegion=region;stage.Begin(context);stage.Tick(1.85f,0f);
        var scenery=(FrogCatchCinematicEnvironment)typeof(GriffinCrosswindRank4Stage).GetField("scenery",Hidden).GetValue(stage);
        Assert.That(scenery.Region,Is.EqualTo(region));Assert.That(View.orthographic,Is.False);Assert.That(View.backgroundColor.a,Is.EqualTo(1f));
        var floor=host.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Generated continuous battle ground");
        Assert.That(floor.sharedMesh.vertices.Select(v=>v.x).Distinct().Count(),Is.GreaterThan(1));
        var wind=host.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("Three-dimensional feather wind")).ToArray();
        Assert.That(wind.Length,Is.EqualTo(3));
        foreach(var mesh in wind)
        {Assert.That(mesh.sharedMesh.bounds.size.z,Is.GreaterThan(.3f));Assert.That(mesh.sharedMesh.bounds.size.y,Is.GreaterThan(.3f));}
        Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&(r.name.Contains("victim")||r.name.Contains("witness"))),Is.False);
    }

    [TestCase(false)][TestCase(true)]
    public void ReturnMatchesTheNativeMenuGapAnchorSizeAndPhaseWithoutConsumingBattleTime(bool enemy)
    {
        context.enemy=enemy;context.griffinOrbViewport=new Vector2(enemy?.76f:.55f,.85f);context.griffinOrbHeight=.11f;
        stage.Begin(context);float original=Time.timeScale;
        try
        {
            Time.timeScale=0f;
            for(float time=0f;time<stage.Duration;time+=.02f)
            {stage.Tick(time,.02f);Assert.That(stage.ActionTime,Is.EqualTo(-1f));Assert.That(Time.timeScale,Is.Zero);}
            stage.Tick(GriffinCrosswindRank4Stage.FieldReturnTime,0f);
            Assert.That(View.orthographic,Is.True);Assert.That(View.backgroundColor.a,Is.Zero);Assert.That(Orb.enabled,Is.True);
            Rect rect=Projected(Orb);Assert.That(Vector2.Distance(rect.center,context.griffinOrbViewport),Is.LessThan(.004f));
            Assert.That(rect.height,Is.EqualTo(.11f).Within(.015f));Assert.That(Cell(Orb.sprite,4,2),Is.Zero);
            Rect source=Projected(Lead);Vector2 foot=new Vector2(source.center.x,source.yMin);
            Assert.That(Vector2.Distance(foot,new Vector2(.375f,.5f-2f/9f)),Is.LessThan(.003f));
            Assert.That(source.height,Is.EqualTo(1.3f/9f).Within(.003f));
            stage.Tick(GriffinCrosswindRank4Stage.FieldReturnTime-.001f,0f);Rect before=Projected(Orb);
            Assert.That(Vector2.Distance(before.center,rect.center),Is.LessThan(.005f));Assert.That(Mathf.Abs(before.height-rect.height),Is.LessThan(.025f));
        }
        finally{Time.timeScale=original;}
    }

    private Rect Projected(SpriteRenderer renderer)
    {
        Bounds body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{renderer.sprite});
        Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 point=new Vector3(x==0?body.min.x:body.max.x,y==0?body.min.y:body.max.y,0f);if(renderer.flipX)point.x=-point.x;
            Vector3 viewport=View.WorldToViewportPoint(renderer.transform.TransformPoint(point));
            Assert.That(viewport.z,Is.GreaterThan(0f));min=Vector2.Min(min,viewport);max=Vector2.Max(max,viewport);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    private static int Cell(Sprite sprite,int columns,int rows)
    {return Mathf.RoundToInt((sprite.texture.height-sprite.rect.yMax)/(sprite.texture.height/(float)rows))*columns+Mathf.RoundToInt(sprite.rect.x/(sprite.texture.width/(float)columns));}
}

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    [Test]
    public void UnavailableStormOrbPresentationFallsBackToTheLiveOrbWithoutLeavingItHiddenOrWaiting()
    {
        ReplaceSource("pone",0);Call("BeginGriffinCrosswind",source);
        object wind=((IList)BattleGet("griffinCrosswinds"))[0],request=Queue[0];
        ((HashSet<object>)BattleGet("rank4SkillPreludeCompleted")).Add(request);
        var context=(Rank4StageContext)Field(request,"context");var actor=context.source;actor.sprite=null;context.source=actor;
        Assert.That(Call("UpdateRank4Cinematic"),Is.False);Assert.That(Presentation,Is.Null);
        Assert.That(Field(wind,"waiting"),Is.False);Assert.That(Queue,Is.Empty);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);AssertNoDamage();
    }

    [TestCase(0)][TestCase(1)]
    public void StormOrbSummonFilmKeepsTheWholeEightSecondNativeWindowAndRestoresTheActualField(int team)
    {
        ReplaceSource("pone",team);Call("BeginGriffinCrosswind",source);
        object wind=((IList)BattleGet("griffinCrosswinds"))[0];Assert.That(Field(wind,"waiting"),Is.True);
        BeginQueued(source);object presentation=Presentation;float battleTime=(float)BattleGet("battleElapsed");
        for(float time=.05f;time<GriffinCrosswindRank4Stage.FieldReturnTime;time+=.05f)
        {
            SetField(presentation,"elapsed",time);Stage.Tick(time,.05f);Call("AdvanceRank4CinematicHandoff",presentation);
            Assert.That(Field(wind,"waiting"),Is.True);Assert.That(BattleGet("battleElapsed"),Is.EqualTo(battleTime));AssertNoDamage();
        }
        SetField(presentation,"elapsed",.75f);Call("UpdateGriffinStormOrbCinematicVisibility",presentation);
        Assert.That(Renderer(primary).forceRenderingOff,Is.True);Assert.That(Renderer(secondary).forceRenderingOff,Is.True);
        SetField(presentation,"elapsed",3.55f);Call("UpdateGriffinStormOrbCinematicVisibility",presentation);
        Assert.That(Renderer(primary).forceRenderingOff,Is.False);Assert.That(Renderer(secondary).forceRenderingOff,Is.False);
        SetField(presentation,"elapsed",GriffinCrosswindRank4Stage.FieldReturnTime);
        Stage.Tick(GriffinCrosswindRank4Stage.FieldReturnTime,0f);Call("AdvanceRank4CinematicHandoff",presentation);
        Assert.That(Field(wind,"waiting"),Is.False);Assert.That(Field(wind,"started"),Is.EqualTo(battleTime));
        Call("FinishRank4Cinematic");Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(QueueSource(),Is.False,"The same unit may not replay this summoning film in the same wave.");
        Assert.That(Renderer(source).forceRenderingOff,Is.False);AssertNoDamage();
    }

    [TestCase(false)][TestCase(true)]
    public void CommittedStormOrbFilmSurvivesCasterDeathAndCancellationReleasesItsNativeOrb(bool death)
    {
        ReplaceSource("pone",0);Call("BeginGriffinCrosswind",source);BeginQueued(source);
        object presentation=Presentation,wind=((IList)BattleGet("griffinCrosswinds"))[0];
        SetField(presentation,"elapsed",.75f);Call("UpdateGriffinStormOrbCinematicVisibility",presentation);
        if(death){Set(source,"hp",0f);Set(source,"alive",false);}
        Assert.That(Call("Rank4PresentationValid",Field(presentation,"request")),Is.True);
        Call("FinishRank4Cinematic");Assert.That(Field(wind,"waiting"),Is.False);
        Assert.That(Renderer(primary).forceRenderingOff,Is.False);Assert.That(Renderer(secondary).forceRenderingOff,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);AssertNoDamage();
    }
}
