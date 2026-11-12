using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class KingKongLeylineCinematicStageTests
{
    private GameObject host;
    private KingKongLeylineCinematicStage stage;
    private Texture2D map,art;
    private Sprite sourceSprite,otherSprite,secondFrame;
    private static readonly float[] PoseTimes={.20f,.59f,.74f,.91f,1.12f,1.40f,1.85f,2.75f};

    [SetUp] public void Setup()
    {
        host=new GameObject("KingKong private stage tests");stage=new KingKongLeylineCinematicStage(host.transform);
        map=new Texture2D(4,4);map.SetPixels(Enumerable.Repeat(new Color(.3f,.4f,.2f),16).ToArray());map.Apply();
        art=new Texture2D(16,8);art.SetPixels(Enumerable.Repeat(Color.white,128).ToArray());art.Apply();
        sourceSprite=Sprite.Create(art,new Rect(0,0,8,4),new Vector2(.5f,0f),4f);
        otherSprite=Sprite.Create(art,new Rect(8,0,4,8),new Vector2(.5f,0f),4f);
        secondFrame=Sprite.Create(art,new Rect(0,4,8,4),new Vector2(.5f,0f),4f);
    }

    [TearDown] public void Cleanup()
    {
        stage?.Dispose();Object.DestroyImmediate(host);Object.DestroyImmediate(sourceSprite);Object.DestroyImmediate(otherSprite);
        Object.DestroyImmediate(secondFrame);Object.DestroyImmediate(art);Object.DestroyImmediate(map);
    }

    [TestCase("lilas","LilasAction")][TestCase("magril","MagrilAction")]
    [TestCase("bingareu","BingareuAction")][TestCase("moska","MoskaAction")]
    public void EverySpeciesUsesEightSeparateDrawingsWithCompleteAtlasCellsAndAStableLogicalBodyScale(string species,string resource)
    {
        stage.Warmup(species);
        Assert.That(KingKongLeylineCinematicStage.PoseResource(species),Is.EqualTo(KingKongLeylineCinematicStage.ResourceRoot+resource));
        var atlas=Resources.Load<Texture2D>(KingKongLeylineCinematicStage.PoseResource(species));Assert.That(atlas,Is.Not.Null);
        Assert.That(stage.Begin(map,Source(species,false),"PrimordialMeadow"),Is.True);
        var frames=new HashSet<Sprite>();var rectangles=new List<Rect>();var properties=new MaterialPropertyBlock();float scale=0f;
        foreach(float time in PoseTimes)
        {
            stage.Tick(time,0f);var body=SourceRenderer();frames.Add(body.sprite);
            Assert.That(body.sprite.texture,Is.SameAs(atlas));
            body.GetPropertyBlock(properties);Assert.That(properties.GetTexture("_MainTex"),Is.SameAs(atlas),"The actor glow properties must retain the actual pose texture.");
            if(scale==0f)scale=body.transform.localScale.z;
            Assert.That(body.transform.localScale.z,Is.EqualTo(scale).Within(.0001f),"Narrower arm poses must not be inflated to the windup's full width.");
            Assert.That(body.transform.localScale.x/scale,Is.InRange(.75f,1.25f));
            Assert.That(body.transform.localScale.y/scale,Is.InRange(.75f,1.25f));
            Rect cell=body.sprite.rect;rectangles.Add(cell);
            Assert.That(cell.xMin,Is.GreaterThanOrEqualTo(0f));Assert.That(cell.yMin,Is.GreaterThanOrEqualTo(0f));
            Assert.That(cell.xMax,Is.LessThanOrEqualTo(atlas.width));Assert.That(cell.yMax,Is.LessThanOrEqualTo(atlas.height));
            Assert.That(cell.width,Is.EqualTo(atlas.width/4f).Within(.51f));Assert.That(cell.height,Is.EqualTo(atlas.height/2f).Within(.51f));
            AssertFinite(stage.SourceViewportRect);
        }
        Assert.That(frames.Count,Is.EqualTo(8),"Brace, alternating chest beats, roar and settlement need different drawings.");
        float area=0f;
        for(int i=0;i<rectangles.Count;i++)
        {
            area+=rectangles[i].width*rectangles[i].height;
            for(int j=0;j<i;j++)Assert.That(rectangles[i].Overlaps(rectangles[j]),Is.False);
        }
        Assert.That(area,Is.EqualTo(atlas.width*(float)atlas.height).Within(1f));
    }

    [TestCase("lilas")][TestCase("magril")][TestCase("bingareu")][TestCase("moska")]
    public void ShinySourcesKeepTheirOriginalFramesColorsAndGeometryInsteadOfBorrowingNormalColoredArt(string species)
    {
        var source=AuthoredSource(species);source.shiny=true;source.flipX=true;source.color=new Color(.95f,.5f,.7f);
        var vertices=source.frames.Select(frame=>frame.vertices).ToArray();var triangles=source.frames.Select(frame=>frame.triangles).ToArray();
        var textures=new HashSet<Texture2D>(source.frames.Select(frame=>frame.texture));var seen=new HashSet<Sprite>();
        Assert.That(stage.Begin(map,source,"FrozenHighlands"),Is.True);
        for(int i=0;i<=60;i++)
        {
            stage.Tick(i*.05f,.05f);var body=SourceRenderer();seen.Add(body.sprite);
            Assert.That(textures.Contains(body.sprite.texture),Is.True,"Shiny colors must come from the actual owned actor.");
            Assert.That(body.flipX,Is.True);Assert.That(body.color.r,Is.EqualTo(source.color.r));
        }
        Assert.That(seen.Count,Is.GreaterThan(1),"The fallback must animate the authored actor, rather than freeze one sprite.");
        for(int i=0;i<source.frames.Length;i++)
        {Assert.That(source.frames[i].vertices,Is.EqualTo(vertices[i]));Assert.That(source.frames[i].triangles,Is.EqualTo(triangles[i]));}
        stage.Dispose();
        foreach(Sprite sprite in source.frames)Assert.That(sprite,Is.Not.Null,"Disposing a private stage cannot destroy shared monster frames.");
    }

    [TestCase("PrimordialMeadow")][TestCase("PoisonFogSwamp")][TestCase("CrimsonJungle")][TestCase("FrozenHighlands")][TestCase("SkySanctuary")]
    public void TheStageShowsOnlyItsCasterInAllFiveMapsWithoutHidingOtherLiveUnits(string region)
    {
        var source=Source("unpainted",true);source.ground=new Vector2(.5f,-.3f);source.flipX=true;
        var live=new GameObject("Unrelated live actor");live.transform.position=new Vector3(4f,0,-2f);
        var liveRenderer=live.AddComponent<SpriteRenderer>();liveRenderer.sprite=otherSprite;
        var view=new GameObject("Unrelated live camera").AddComponent<Camera>();view.enabled=false;
        view.transform.SetPositionAndRotation(new Vector3(2,5,-8),Quaternion.Euler(45,10,0));view.fieldOfView=43f;
        Vector3 cameraPosition=view.transform.position;Quaternion cameraRotation=view.transform.rotation;float speed=Time.timeScale;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin(map,source,region),Is.True);
            foreach(float time in PoseTimes)
            {
                stage.Tick(time,.1f);AssertFinite(stage.SourceViewportRect);
                Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).All(renderer=>
                    renderer.name=="Leyline actual caster"||renderer.name=="Leyline pose bridge"),Is.True);
            }
            Assert.That(stage.Source.species,Is.EqualTo("unpainted"));Assert.That(stage.Source.sprite,Is.SameAs(sourceSprite));
            Assert.That(stage.Source.ground,Is.EqualTo(source.ground));Assert.That(stage.Source.flipX,Is.True);
            Assert.That(liveRenderer.enabled,Is.True);Assert.That(liveRenderer.forceRenderingOff,Is.False);
            stage.Hide();Assert.That(liveRenderer.enabled,Is.True);Assert.That(liveRenderer.forceRenderingOff,Is.False);
            Assert.That(stage.SceneryRegion,Is.EqualTo(region));Assert.That(live.transform.position,Is.EqualTo(new Vector3(4f,0,-2f)));
            Assert.That(view.transform.position,Is.EqualTo(cameraPosition));Assert.That(view.transform.rotation,Is.EqualTo(cameraRotation));
            Assert.That(view.fieldOfView,Is.EqualTo(43f));Assert.That(Time.timeScale,Is.Zero);
        }
        finally{Time.timeScale=speed;Object.DestroyImmediate(live);Object.DestroyImmediate(view.gameObject);}
    }

    [TestCase(.2f,1)][TestCase(.8f,2)][TestCase(1.8f,3)][TestCase(2.8f,4)]
    public void TheFourConnectedShotsFollowTheExplicitClock(float time,int shot)
    {
        Assert.That(stage.Begin(map,Source("unpainted",true),"PrimordialMeadow"),Is.True);
        stage.Tick(time,0f);Assert.That(stage.Shot,Is.EqualTo(shot));Assert.That(KingKongLeylineCinematicStage.Duration,Is.EqualTo(3f));
    }

    [TestCase(30)][TestCase(60)][TestCase(120)]
    public void DirectAndSteppedClocksReachTheSamePoseAndViewport(int fps)
    {
        Assert.That(stage.Begin(map,Source("unpainted",true),"PrimordialMeadow"),Is.True);
        for(int i=0;i/(float)fps<1.847f;i++)stage.Tick(i/(float)fps,1f/fps);
        stage.Tick(1.847f,0f);var body=SourceRenderer();Vector3 position=body.transform.position;
        Quaternion rotation=body.transform.rotation;Sprite sprite=body.sprite;Rect viewport=stage.SourceViewportRect;
        Assert.That(stage.Begin(map,Source("unpainted",true),"PrimordialMeadow"),Is.True);stage.Tick(1.847f,0f);
        Assert.That(SourceRenderer().transform.position,Is.EqualTo(position));Assert.That(SourceRenderer().sprite,Is.SameAs(sprite));
        Assert.That(Quaternion.Angle(SourceRenderer().transform.rotation,rotation),Is.LessThan(.001f));
        Assert.That(stage.SourceViewportRect,Is.EqualTo(viewport));
    }

    [TestCase(false)][TestCase(true)]
    public void TheLastShotMatchesTheLiveFootAndHeightAndDoesNotLeakThatReturnViewIntoTheNextCast(bool flip)
    {
        var source=Source("lilas",false);source.flipX=flip;Assert.That(stage.Begin(map,source,"PrimordialMeadow"),Is.True);
        Vector2 foot=new Vector2(.31f,.22f);stage.SetReturnView(foot,.09f);stage.Tick(2.35f,0f);
        Rect initial=stage.SourceViewportRect;float initialError=Vector2.Distance(new Vector2(initial.center.x,initial.yMin),foot);
        stage.Tick(2.95f,0f);Rect nearing=stage.SourceViewportRect;
        Assert.That(Vector2.Distance(new Vector2(nearing.center.x,nearing.yMin),foot),Is.LessThan(initialError*.5f));
        stage.Tick(3f,0f);Rect final=stage.SourceViewportRect;
        Assert.That(final.center.x,Is.EqualTo(foot.x).Within(.003f));Assert.That(final.yMin,Is.EqualTo(foot.y).Within(.003f));
        Assert.That(final.height,Is.EqualTo(.09f).Within(.003f));
        Assert.That(stage.Begin(map,source,"PrimordialMeadow"),Is.True);stage.Tick(3f,0f);
        Assert.That(stage.SourceViewportRect.height,Is.GreaterThan(.15f));
    }

    [Test]
    public void GroundFissuresUseTransparentPixelArtworkWithProgressiveRevealAndBeatResponse()
    {
        var texture=Resources.Load<Texture2D>(KingKongLeylineCinematicStage.ResourceRoot+"GroundFissures");
        Assert.That(texture,Is.Not.Null);Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(texture.mipmapCount,Is.EqualTo(1));Assert.That(texture.width,Is.GreaterThan(128));
        Assert.That(texture.GetPixel(0,0).a,Is.LessThan(.035f));
        Assert.That(stage.Begin(map,Source("lilas",false),"PrimordialMeadow"),Is.True);
        Assert.That(host.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
        var ground=host.GetComponentsInChildren<MeshRenderer>(true).First(renderer=>renderer.name=="Leyline pixel ground fissures");
        Assert.That(ground.sharedMaterial.mainTexture,Is.SameAs(texture));
        stage.Tick(.20f,0f);var properties=new MaterialPropertyBlock();ground.GetPropertyBlock(properties);
        float opening=properties.GetFloat("_Reveal"),quiet=properties.GetFloat("_Pulse");
        stage.Tick(.72f,0f);ground.GetPropertyBlock(properties);
        Assert.That(properties.GetFloat("_Pulse"),Is.GreaterThan(quiet));
        stage.Tick(1.85f,0f);ground.GetPropertyBlock(properties);
        Assert.That(properties.GetFloat("_Reveal"),Is.GreaterThan(opening));
        Assert.That(properties.GetFloat("_Opacity"),Is.GreaterThan(.9f));
    }

    [Test]
    public void InvalidInputAndReuseHideAndDisposeLeaveNoActiveOldStageOrRenderTarget()
    {
        Assert.That(stage.Begin(null,Source("unpainted",true),"PrimordialMeadow"),Is.False);
        Assert.That(stage.Begin(map,new FrogCatchCinematicStage.Actor(),"PrimordialMeadow"),Is.False);
        Assert.That(stage.Begin(map,Source("unpainted",true),"PrimordialMeadow"),Is.True);
        Assert.That(stage.Begin(map,Source("unpainted",true),"SkySanctuary"),Is.True);
        Assert.That(stage.Active,Is.True);
        var target=(RenderTexture)stage.Texture;Assert.That(target,Is.Not.Null);Assert.That(target.IsCreated(),Is.True);
        stage.Hide();Assert.That(stage.Active,Is.False);stage.Dispose();Assert.That(target==null||!target.IsCreated(),Is.True);
        Assert.DoesNotThrow(()=>stage.Dispose());
    }

    private FrogCatchCinematicStage.Actor Source(string species,bool shiny)=>new FrogCatchCinematicStage.Actor
    {species=species,shiny=shiny,sprite=sourceSprite,frames=new[]{sourceSprite,secondFrame,sourceSprite,secondFrame},color=Color.white,size=1f};
    private FrogCatchCinematicStage.Actor AuthoredSource(string species)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(species=="lilas"?"Enemies/Shiny/lilas":definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(driver,Is.Not.Null);
        var actor=Source(species,true);actor.frames=new Sprite[driver.SkillAttackFrameCount];
        for(int i=0;i<actor.frames.Length;i++)actor.frames[i]=driver.GetSkillAttackFrame(i);actor.sprite=actor.frames[0];return actor;
    }
    private SpriteRenderer SourceRenderer()=>host.GetComponentsInChildren<SpriteRenderer>(true).First(renderer=>renderer.name=="Leyline actual caster");
    private static void AssertFinite(Rect rect)
    {
        Assert.That(float.IsNaN(rect.x)||float.IsInfinity(rect.x)||float.IsNaN(rect.y)||float.IsInfinity(rect.y)||
            float.IsNaN(rect.width)||float.IsInfinity(rect.width)||float.IsNaN(rect.height)||float.IsInfinity(rect.height),Is.False);
        Assert.That(rect.width,Is.GreaterThan(0f));Assert.That(rect.height,Is.GreaterThan(0f));
    }
}
