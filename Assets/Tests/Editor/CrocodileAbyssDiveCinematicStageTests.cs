using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CrocodileAbyssDiveCinematicStageTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private GameObject host;
    private CrocodileAbyssDiveCinematicStage stage;
    private Texture2D map,art;
    private Sprite sourceSprite,targetSprite,closedSprite;
    private readonly Dictionary<Texture2D,Color32[]> pixelsByTexture=new Dictionary<Texture2D,Color32[]>();
    private static readonly float[] PoseTimes={.06f,.3f,.9f,1.41f,1.63f,2.73f,3.05f,3.99f};
    [SetUp] public void Setup()
    {
        host=new GameObject("Crocodile private stage tests");stage=new CrocodileAbyssDiveCinematicStage(host.transform);
        map=new Texture2D(4,4);map.SetPixels(Enumerable.Repeat(new Color(.3f,.4f,.2f),16).ToArray());map.Apply();
        art=new Texture2D(16,8);art.SetPixels(Enumerable.Repeat(Color.white,128).ToArray());art.Apply();
        sourceSprite=Sprite.Create(art,new Rect(0,0,8,4),new Vector2(.5f,0f),4f);
        targetSprite=Sprite.Create(art,new Rect(8,0,4,8),new Vector2(.5f,0f),4f);
        closedSprite=Sprite.Create(art,new Rect(0,4,8,4),new Vector2(.5f,0f),4f);
    }
    [TearDown] public void Cleanup()
    {
        stage?.Dispose();Object.DestroyImmediate(host);Object.DestroyImmediate(sourceSprite);Object.DestroyImmediate(targetSprite);
        Object.DestroyImmediate(closedSprite);Object.DestroyImmediate(art);Object.DestroyImmediate(map);pixelsByTexture.Clear();
    }

    [Test]
    public void TheRealSourceTargetAndCurrentRegionAreRequiredAndSnapshotIdentityIsPreserved()
    {
        var source=Source("unpainted",true);var target=Target();var others=new[]{Target()};
        Assert.That(stage.Begin(null,source,target,others),Is.False);
        Assert.That(stage.Begin(map,new FrogCatchCinematicStage.Actor(),target,others),Is.False);
        Assert.That(stage.Begin(map,source,new FrogCatchCinematicStage.Actor(),others),Is.False);
        Assert.That(stage.Begin(map,source,target,others,"FrozenHighlands"),Is.True);
        others[0]=Source("mutated",false);
        Assert.That(stage.Background,Is.SameAs(map));Assert.That(stage.Source.species,Is.EqualTo("unpainted"));
        Assert.That(stage.Target.sprite,Is.SameAs(targetSprite));Assert.That(stage.Bystanders[0].species,Is.EqualTo("actual prey"));
        Assert.That(stage.SceneryRegion,Is.EqualTo("FrozenHighlands"));Assert.That(stage.Texture.width,Is.LessThanOrEqualTo(1600));
        Assert.That(stage.Texture.height,Is.LessThanOrEqualTo(900));
    }

    [TestCase("ager","AgerAction")]
    [TestCase("kimbul","KimbulAction")]
    [TestCase("krogal","KrogalAction")]
    [TestCase("tarvil","TarvilAction")]
    public void EveryNormalCrocodileUsesEightDifferentAuthoredActionFramesWithCompleteNonoverlappingAtlasCells(string species,string resource)
    {
        stage.Warmup(species);var source=Source(species,false);
        Assert.That(CrocodileAbyssDiveCinematicStage.PoseResource(species),Is.EqualTo(CrocodileAbyssDiveCinematicStage.ResourceRoot+resource));
        var atlas=Resources.Load<Texture2D>(CrocodileAbyssDiveCinematicStage.PoseResource(species));Assert.That(atlas,Is.Not.Null);
        Assert.That(stage.Begin(map,source,Target(),null),Is.True);var frames=new HashSet<Sprite>();
        var rectangles=new List<Rect>();float baseScale=0f;
        foreach(float time in PoseTimes)
        {
            stage.Tick(time,0f);var renderer=Renderer("Crocodile abyss source");frames.Add(renderer.sprite);
            Assert.That(renderer.sprite.texture,Is.SameAs(atlas));
            Assert.That(renderer.sprite.vertices.Length,Is.GreaterThan(80),"Multiple joints need a flexible sprite mesh, not one rotating quad.");
            Assert.That(renderer.sprite.triangles.Length,Is.GreaterThan(400));
            if(baseScale==0f)baseScale=renderer.transform.localScale.z;
            Assert.That(renderer.transform.localScale.z,Is.EqualTo(baseScale).Within(.0001f),"Folding the tail must not inflate a narrower drawing to the windup's full width.");
            Assert.That(renderer.transform.localScale.x/baseScale,Is.InRange(.75f,1.25f));
            Assert.That(renderer.transform.localScale.y/baseScale,Is.InRange(.75f,1.25f));
            Rect cell=renderer.sprite.rect;rectangles.Add(cell);
            Assert.That(cell.xMin,Is.GreaterThanOrEqualTo(0f));Assert.That(cell.yMin,Is.GreaterThanOrEqualTo(0f));
            Assert.That(cell.xMax,Is.LessThanOrEqualTo(atlas.width));Assert.That(cell.yMax,Is.LessThanOrEqualTo(atlas.height));
            Assert.That(cell.width,Is.EqualTo(atlas.width/4f).Within(.51f));Assert.That(cell.height,Is.EqualTo(atlas.height/2f).Within(.51f));
            Assert.That(renderer.color.r,Is.EqualTo(source.color.r));
        }
        Assert.That(frames.Count,Is.EqualTo(8),"Windup, lunge, dive, belly roll, back roll, coil, breach and crush must be different drawings.");
        float area=0f;for(int i=0;i<rectangles.Count;i++)
        {
            area+=rectangles[i].width*rectangles[i].height;
            for(int j=0;j<i;j++)Assert.That(rectangles[i].Overlaps(rectangles[j]),Is.False,"One action frame cannot borrow pixels from the next cell.");
        }
        Assert.That(area,Is.EqualTo(atlas.width*(float)atlas.height).Within(1f),"Fractional 4x2 cell boundaries must cover every source pixel exactly once.");
    }

    [TestCase("ager")][TestCase("kimbul")][TestCase("krogal")][TestCase("tarvil")]
    public void ShinySourcesAnimateTheirActualAuthoredFramesWithoutBorrowingNormalArtOrChangingTheOriginalSprites(string species)
    {
        var source=AuthoredSource(species,true);source.flipX=true;source.color=new Color(.95f,.5f,.7f);
        var vertices=source.frames.Select(frame=>frame.vertices).ToArray();var triangles=source.frames.Select(frame=>frame.triangles).ToArray();
        var textures=new HashSet<Texture2D>();var originals=new HashSet<Sprite>(source.frames);
        Assert.That(stage.Begin(map,source,Target(),null),Is.True);
        for(int i=0;i<=80;i++)
        {
            stage.Tick(i*.05f,.05f);var renderer=Renderer("Crocodile abyss source");
            Assert.That(originals.Contains(renderer.sprite),Is.False,"Flex geometry belongs to a private copy, never to the imported game sprite.");
            Assert.That(source.frames.Any(frame=>frame.texture==renderer.sprite.texture&&frame.rect==renderer.sprite.rect),Is.True);
            Assert.That(renderer.sprite.vertices.Length,Is.GreaterThan(80));Assert.That(renderer.flipX,Is.True);textures.Add(renderer.sprite.texture);
            Assert.That(renderer.color.r,Is.EqualTo(source.color.r));Assert.That(renderer.color.g,Is.EqualTo(source.color.g));
        }
        Assert.That(textures.Count,Is.GreaterThanOrEqualTo(5),"The shiny must actually flex and change its drawn roll poses rather than hold one frame.");
        for(int i=0;i<source.frames.Length;i++)
        {CollectionAssert.AreEqual(vertices[i],source.frames[i].vertices);CollectionAssert.AreEqual(triangles[i],source.frames[i].triangles);}
        stage.Dispose();foreach(Sprite original in originals)Assert.That(original!=null,Is.True,"Disposing cinematic copies must not destroy original character assets.");
    }

    [TestCase("ager",false,false)][TestCase("ager",true,false)]
    [TestCase("kimbul",false,false)][TestCase("kimbul",true,false)]
    [TestCase("krogal",false,false)][TestCase("krogal",true,false)]
    [TestCase("tarvil",false,false)][TestCase("tarvil",true,false)]
    [TestCase("ager",false,true)][TestCase("ager",true,true)]
    public void RegisteredBitesStayBetweenTheActualJawsForEverySpeciesPoseAndFacing(string species,bool flip,bool shiny)
    {
        var actor=AuthoredSource(species,shiny);actor.flipX=flip;
        Assert.That(stage.Begin(map,actor,Target(),null),Is.True);
        foreach(float time in PoseTimes)
        {
            stage.Tick(time,0f);var renderer=Renderer("Crocodile abyss source");Sprite frame=renderer.sprite;
            Vector3 mouth=(Vector3)typeof(CrocodileAbyssDiveCinematicStage).GetField("mouth",Hidden).GetValue(stage);
            Vector3 local=Quaternion.Inverse(renderer.transform.localRotation)*(mouth-renderer.transform.localPosition);
            local=new Vector3(local.x/renderer.transform.localScale.x,local.y/renderer.transform.localScale.y,local.z/renderer.transform.localScale.z);
            if(flip)local.x=-local.x;
            int x=Mathf.FloorToInt(frame.rect.x+frame.pivot.x+local.x*frame.pixelsPerUnit);
            int y=Mathf.FloorToInt(frame.rect.y+frame.pivot.y+local.y*frame.pixelsPerUnit);
            Assert.That(x,Is.InRange((int)frame.rect.xMin,(int)frame.rect.xMax-1));
            Assert.That(y,Is.InRange((int)frame.rect.yMin,(int)frame.rect.yMax-1));
            bool above=false,below=false;int reach=Mathf.CeilToInt(frame.rect.height*.25f),spread=Mathf.Max(1,Mathf.RoundToInt(frame.rect.width*.025f));
            for(int offset=1;offset<=reach;offset++)
            {
                for(int column=-1;column<=1;column++)
                {
                    int columnX=Mathf.Clamp(x+column*spread,(int)frame.rect.xMin,(int)frame.rect.xMax-1);
                    above|=ReadPixel(frame.texture,columnX,Mathf.Min((int)frame.rect.yMax-1,y+offset)).a>.5f;
                    below|=ReadPixel(frame.texture,columnX,Mathf.Max((int)frame.rect.yMin,y-offset)).a>.5f;
                }
                if(above&&below)break;
            }
            Assert.That(above&&below,Is.True,species+" frame at "+time+": the bite may be transparent, but painted upper and lower jaws must enclose it.");
        }
    }

    [TestCase(false)][TestCase(true)]
    public void TheSameVictimStaysInsideTheMouthThroughoutTheTwoWholeBodyRotations(bool flip)
    {
        var source=Source("ager",false);source.flipX=flip;
        Assert.That(stage.Begin(map,source,Target(),null),Is.True);
        float previousRoll=0f;var frames=new HashSet<Sprite>();
        for(int i=0;i<=160;i++)
        {
            float time=i*.025f;stage.Tick(time,.025f);var prey=Renderer("Actual abyss primary target");
            Bounds bounds=VisibleBounds(prey.sprite);
            Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,.5f),Mathf.Lerp(bounds.min.y,bounds.max.y,.58f),0f);
            if(prey.flipX)local.x=-local.x;
            Vector3 viewport=Camera().WorldToViewportPoint(prey.transform.TransformPoint(local));
            if(time>=.25f)Assert.That(Vector2.Distance(stage.MouthViewport,viewport),Is.LessThan(.0004f),"After the lunge connects, the same prey must stay registered to the jaws.");
            if(time==0f)Assert.That(Vector2.Distance(stage.MouthViewport,viewport),Is.GreaterThan(.025f),"Before contact the crocodile must drive into its prey rather than start already glued to it.");
            Assert.That(prey.sprite,Is.SameAs(targetSprite));
            var body=Renderer("Crocodile abyss source");frames.Add(body.sprite);
            Assert.That(Quaternion.Angle(Camera().transform.rotation,body.transform.rotation),Is.LessThan(30f),"A drawn body twist must not be replaced by spinning one flat image through the screen.");
            Assert.That(Quaternion.Angle(Camera().transform.rotation,prey.transform.rotation),Is.LessThan(30f));
            Assert.That(stage.RollDegrees,Is.GreaterThanOrEqualTo(previousRoll));previousRoll=stage.RollDegrees;
            AssertFinite(stage.SourceViewportRect);AssertFinite(stage.TargetViewportRect);
        }
        Assert.That(stage.RollDegrees,Is.EqualTo(720f).Within(.001f));
        Assert.That(frames.Count,Is.EqualTo(8));
    }

    [Test]
    public void TheCameraDivesAndReturnsToTheSameEntranceWithoutTouchingTheBattleCameraOrClock()
    {
        var cameraHost=new GameObject("Unrelated battle camera");var main=cameraHost.AddComponent<Camera>();main.enabled=false;
        main.transform.SetPositionAndRotation(new Vector3(2,5,-8),Quaternion.Euler(45,10,0));main.orthographic=true;
        var position=main.transform.position;var rotation=main.transform.rotation;float scale=Time.timeScale;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin(map,Source("unpainted",true),Target(),null),Is.True);
            var view=Camera();stage.Tick(.5f,0f);Vector3 entrance=view.transform.position;
            Assert.That(stage.Shot,Is.EqualTo(1));stage.Tick(1f,0f);Assert.That(stage.Shot,Is.EqualTo(2));
            stage.Tick(2.2f,0f);Assert.That(stage.Shot,Is.EqualTo(3));Assert.That(view.transform.position.y,Is.LessThan(entrance.y-2f));
            Assert.That(Renderer("Generated underwater depth").enabled,Is.True);
            stage.Tick(4f,0f);Assert.That(stage.Shot,Is.EqualTo(4));Assert.That(Vector3.Distance(view.transform.position,entrance),Is.LessThan(.001f));
            Assert.That(main.transform.position,Is.EqualTo(position));Assert.That(main.transform.rotation,Is.EqualTo(rotation));
            Assert.That(main.orthographic,Is.True);Assert.That(Time.timeScale,Is.Zero);
        }
        finally{Time.timeScale=scale;Object.DestroyImmediate(cameraHost);}
    }

    [Test]
    public void TheSurfaceEntranceAtTheTopOfTheUnderwaterPaintingIsNotCroppedByAWideCamera()
    {
        Assert.That(stage.Begin(map,Source("unpainted",true),Target(),null),Is.True);stage.Tick(2.2f,0f);
        var background=Renderer("Generated underwater depth");Bounds bounds=background.sprite.bounds;
        Vector3 top=background.transform.TransformPoint(new Vector3(bounds.center.x,Mathf.Lerp(bounds.min.y,bounds.max.y,.95f),0f));
        Vector3 viewport=Camera().WorldToViewportPoint(top);
        Assert.That(viewport.y,Is.InRange(.88f,1.02f));
    }

    [Test]
    public void OnlyTheFullViewUnderwaterTransitionIgnoresSceneryDepthWhileActorsKeepTheirNormalDepthTest()
    {
        Assert.That(stage.Begin(map,Source("unpainted",true),Target(),null,"FrozenHighlands"),Is.True);stage.Tick(3.35f,0f);
        var background=Renderer("Generated underwater depth");var actor=Renderer("Crocodile abyss source");
        Assert.That(background.enabled,Is.True);Assert.That(background.color.a,Is.InRange(.05f,.95f));
        Assert.That(background.sharedMaterial.GetFloat("_ZTest"),Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.Always));
        Assert.That(actor.sharedMaterial.GetFloat("_ZTest"),Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.LessEqual));
        Assert.That(background.sortingOrder,Is.LessThan(actor.sortingOrder));
    }

    [Test]
    public void DepthWritingGeometryInFrontOfTheUnderwaterBackdropCannotCutWedgesOutOfTheFullView()
    {
        Assert.That(stage.Begin(map,Source("unpainted",true),Target(),null),Is.True);stage.Tick(2.2f,0f);
        host.GetComponentInChildren<PixelCrocodileAbyssDive>(true).gameObject.SetActive(false);
        var view=Camera();view.Render();var points=new[]{new Vector2(.08f,.12f),new Vector2(.08f,.88f),new Vector2(.92f,.12f),new Vector2(.92f,.88f)};
        var expected=new Color[points.Length];for(int i=0;i<points.Length;i++)expected[i]=ReadStagePixel(points[i]);
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Quad);blocker.name="Opaque scenery depth regression";
        blocker.layer=30;blocker.transform.SetParent(view.transform,false);blocker.transform.localPosition=Vector3.forward*12f;
        blocker.transform.localRotation=Quaternion.identity;blocker.transform.localScale=Vector3.one*40f;
        var material=new Material(Shader.Find("Unlit/Color")){color=Color.red,renderQueue=2000};
        blocker.GetComponent<MeshRenderer>().sharedMaterial=material;
        Material backdrop=Renderer("Generated underwater depth").sharedMaterial;float depth=backdrop.GetFloat("_ZTest");
        try
        {
            backdrop.SetFloat("_ZTest",(float)UnityEngine.Rendering.CompareFunction.LessEqual);view.Render();bool reproduced=false;
            for(int i=0;i<points.Length;i++)
            {
                Color occluded=ReadStagePixel(points[i]);
                reproduced|=Vector3.Distance(new Vector3(occluded.r,occluded.g,occluded.b),new Vector3(expected[i].r,expected[i].g,expected[i].b))>.1f;
            }
            Assert.That(reproduced,Is.True,"The fixture must first reproduce the old depth occlusion.");
            backdrop.SetFloat("_ZTest",depth);
            view.Render();
            for(int i=0;i<points.Length;i++)
            {
                Color actual=ReadStagePixel(points[i]);
                Assert.That(Vector3.Distance(new Vector3(actual.r,actual.g,actual.b),new Vector3(expected[i].r,expected[i].g,expected[i].b)),Is.LessThan(.012f),
                    "Depth-writing terrain must not replace any part of the opaque underwater transition.");
            }
        }
        finally{backdrop.SetFloat("_ZTest",depth);Object.DestroyImmediate(blocker);Object.DestroyImmediate(material);}
    }

    [Test]
    public void TheAscendingCrocodileKeepsItsOpenGripUntilTheFinalCompression()
    {
        Assert.That(stage.Begin(map,Source("ager",false),Target(),null),Is.True);
        stage.Tick(.3f,0f);Sprite open=Renderer("Crocodile abyss source").sprite;
        stage.Tick(3.6f,0f);Assert.That(Renderer("Crocodile abyss source").sprite,Is.SameAs(open));
        stage.Tick(3.99f,0f);Assert.That(Renderer("Crocodile abyss source").sprite,Is.Not.SameAs(open));
    }

    [TestCase(false)][TestCase(true)]
    public void FlexibleBodyMeshesKeepTheHeadAnchoredAndReserveTheFlowWarpForWater(bool flip)
    {
        var source=Source("ager",false);source.flipX=flip;Assert.That(stage.Begin(map,source,Target(),null),Is.True);
        var properties=new MaterialPropertyBlock();
        foreach(float time in PoseTimes)
        {
            stage.Tick(time,0f);var renderer=Renderer("Crocodile abyss source");renderer.GetPropertyBlock(properties);
            Vector4 span=properties.GetVector("_BodySpan"),flex=properties.GetVector("_Flex");
            Bounds bounds=VisibleBounds(renderer.sprite);
            Assert.That(properties.GetTexture("_MainTex"),Is.SameAs(renderer.sprite.texture),"A private flex mesh must retain its source texture when setting the deformation properties.");
            Assert.That(span.x,Is.EqualTo(bounds.min.x).Within(.0001f));Assert.That(span.y,Is.EqualTo(bounds.max.x).Within(.0001f));
            Assert.That(flex.x,Is.GreaterThan(.01f),"The private subdivided body must actually receive its tail-flex animation.");
            Vector3 mouth=(Vector3)typeof(CrocodileAbyssDiveCinematicStage).GetField("mouth",Hidden).GetValue(stage);
            Vector3 local=Quaternion.Inverse(renderer.transform.localRotation)*(mouth-renderer.transform.localPosition);
            float headX=local.x/renderer.transform.localScale.x*(flip?-1f:1f);
            Assert.That((headX-span.x)/(span.y-span.x),Is.GreaterThan(.70f),"The registered head must stay outside the deforming tail region so its prey does not detach.");
            Assert.That(renderer.sharedMaterial.GetVector("_Flow"),Is.EqualTo(Vector4.zero),"Water UV flow must not warp character art.");
        }
        stage.Tick(.3f,0f);Renderer("Crocodile abyss source").GetPropertyBlock(properties);float restingFlex=properties.GetVector("_Flex").x;
        stage.Tick(1.8f,0f);Renderer("Crocodile abyss source").GetPropertyBlock(properties);
        Assert.That(properties.GetVector("_Flex").x,Is.GreaterThan(restingFlex*2f));
        Vector4 flow=Renderer("Generated underwater depth").sharedMaterial.GetVector("_Flow");
        Assert.That(flow.x,Is.EqualTo(1.8f));Assert.That(flow.y,Is.GreaterThan(.9f));Assert.That(flow.w,Is.GreaterThan(.5f));
    }

    [TestCase(false)][TestCase(true)]
    public void TheLastShotSettlesOnTheLiveActorsViewportAndDoesNotReuseThatReturnViewForTheNextCast(bool flip)
    {
        var source=Source("ager",false);source.flipX=flip;Assert.That(stage.Begin(map,source,Target(),null),Is.True);
        Vector2 foot=new Vector2(.31f,.22f);stage.SetReturnView(foot,.09f);stage.Tick(3.55f,0f);
        Rect start=stage.SourceViewportRect;float firstError=Vector2.Distance(new Vector2(start.center.x,start.yMin),foot);
        stage.Tick(3.95f,0f);Rect nearing=stage.SourceViewportRect;
        Assert.That(Vector2.Distance(new Vector2(nearing.center.x,nearing.yMin),foot),Is.LessThan(firstError*.5f));
        stage.Tick(4f,0f);Rect final=stage.SourceViewportRect;
        Assert.That(final.center.x,Is.EqualTo(foot.x).Within(.003f));Assert.That(final.yMin,Is.EqualTo(foot.y).Within(.003f));
        Assert.That(final.height,Is.EqualTo(.09f).Within(.003f));
        Assert.That(stage.Begin(map,source,Target(),null),Is.True);stage.Tick(4f,0f);
        Assert.That(stage.SourceViewportRect.height,Is.GreaterThan(.15f),"A fresh stage must not inherit the prior actor's tiny on-field return viewport.");
    }

    [Test]
    public void WaterFullyCoversTheTransitionWhileTheCameraIsBelowTheTerrain()
    {
        Assert.That(stage.Begin(map,Source("ager",false),Target(),null),Is.True);
        var scenery=(FrogCatchCinematicEnvironment)typeof(CrocodileAbyssDiveCinematicStage).GetField("scenery",Hidden).GetValue(stage);
        foreach(float time in new[]{1.1f,2.2f,2.85f})
        {
            stage.Tick(time,0f);
            Assert.That(Camera().transform.localPosition.y,Is.LessThan(0f));
            Assert.That(scenery.Active,Is.False,"The underside of the real terrain must never become a ceiling.");
            Assert.That(Renderer("Generated underwater depth").color.a,Is.EqualTo(1f));
        }
        stage.Tick(3.18f,0f);
        Assert.That(Camera().transform.localPosition.y,Is.GreaterThan(0f));
        Assert.That(scenery.Active,Is.True);
        Assert.That(Renderer("Generated underwater depth").color.a,Is.EqualTo(1f));
        stage.Tick(3.7f,0f);Assert.That(Renderer("Generated underwater depth").enabled,Is.False);
    }

    [TestCase("PrimordialMeadow")][TestCase("PoisonFogSwamp")][TestCase("CrimsonJungle")][TestCase("FrozenHighlands")][TestCase("SkySanctuary")]
    public void OnlyTheRealPrimaryDivesAndBystandersReturnToTheirUnchangedGroundInEveryMap(string region)
    {
        var source=Source("unpainted",true);source.ground=new Vector2(.5f,-.3f);
        var bystander=Target();bystander.ground=new Vector2(-.2f,.7f);
        Assert.That(stage.Begin(map,source,Target(),new[]{bystander},region),Is.True);
        stage.Tick(.3f,0f);var renderer=Renderer("Actual abyss bystander");Vector3 start=renderer.transform.position;
        stage.Tick(2.1f,0f);Assert.That(renderer.enabled,Is.False);
        stage.Tick(4f,0f);Assert.That(renderer.enabled,Is.True);Assert.That(renderer.transform.position,Is.EqualTo(start));
        Assert.That(stage.Source.ground,Is.EqualTo(source.ground));Assert.That(stage.Bystanders[0].ground,Is.EqualTo(bystander.ground));
    }

    [TestCase(30)][TestCase(60)][TestCase(120)]
    public void DirectAndSteppedClocksProduceTheSameAnimatedFrameAndBite(int fps)
    {
        Assert.That(stage.Begin(map,Source("unpainted",true),Target(),null),Is.True);
        for(int i=0;i/(float)fps<2.247f;i++)stage.Tick(i/(float)fps,1f/fps);
        stage.Tick(2.247f,0f);Vector3 source=Renderer("Crocodile abyss source").transform.position;
        Quaternion pose=Renderer("Crocodile abyss source").transform.rotation;Vector2 mouth=stage.MouthViewport;
        Assert.That(stage.Begin(map,Source("unpainted",true),Target(),null),Is.True);stage.Tick(2.247f,0f);
        Assert.That(Renderer("Crocodile abyss source").transform.position,Is.EqualTo(source));
        Assert.That(Quaternion.Angle(Renderer("Crocodile abyss source").transform.rotation,pose),Is.LessThan(.001f));
        Assert.That(stage.MouthViewport,Is.EqualTo(mouth));
    }

    [Test]
    public void ReuseHideAndDisposeDoNotLeaveOldActorsWaterOrRenderTargetsActive()
    {
        Assert.That(stage.Begin(map,Source("unpainted",true),Target(),new[]{Target(),Target()}),Is.True);
        Assert.That(stage.Begin(map,Source("unpainted",true),Target(),null),Is.True);stage.Tick(.2f,0f);
        Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Count(renderer=>renderer.name=="Actual abyss bystander"&&renderer.enabled),Is.Zero);
        var rt=(RenderTexture)stage.Texture;stage.Hide();Assert.That(stage.Active,Is.False);
        Assert.That(host.GetComponentsInChildren<PixelCrocodileAbyssDive>(true),Is.Empty);
        stage.Dispose();Assert.That(rt==null||!rt.IsCreated(),Is.True);stage.Dispose();
    }

    private FrogCatchCinematicStage.Actor Source(string species,bool shiny)=>new FrogCatchCinematicStage.Actor
    {species=species,shiny=shiny,sprite=sourceSprite,frames=new[]{sourceSprite,closedSprite,closedSprite,closedSprite},color=Color.white,size=1f};
    private FrogCatchCinematicStage.Actor AuthoredSource(string species,bool shiny)
    {
        string path=species=="ager"&&shiny?"Enemies/Shiny/ager":"Enemies/PixelMonsters/"+
            (species=="ager"?"013_ager":species=="kimbul"?"014_kimbul":species=="krogal"?"015_krogal":"016_tarvil");
        var prefab=Resources.Load<GameObject>(path);Assert.That(prefab,Is.Not.Null);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(driver,Is.Not.Null);
        var actor=Source(species,shiny);actor.frames=new Sprite[driver.SkillAttackFrameCount];
        for(int i=0;i<actor.frames.Length;i++)actor.frames[i]=driver.GetSkillAttackFrame(i);actor.sprite=actor.frames[0];return actor;
    }
    private FrogCatchCinematicStage.Actor Target()=>new FrogCatchCinematicStage.Actor
    {species="actual prey",sprite=targetSprite,color=new Color(.7f,.8f,1f),size=1f,ground=new Vector2(.3f,.2f)};
    private SpriteRenderer Renderer(string name)=>host.GetComponentsInChildren<SpriteRenderer>(true).First(renderer=>renderer.name==name);
    private Camera Camera()=>host.GetComponentsInChildren<Camera>(true).Single();
    private Color ReadStagePixel(Vector2 viewport)
    {
        var previous=RenderTexture.active;var target=(RenderTexture)stage.Texture;
        var pixel=new Texture2D(1,1,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=target;
            pixel.ReadPixels(new Rect(Mathf.FloorToInt(viewport.x*target.width),Mathf.FloorToInt(viewport.y*target.height),1f,1f),0,0);pixel.Apply();return pixel.GetPixel(0,0);
        }
        finally{RenderTexture.active=previous;Object.DestroyImmediate(pixel);}
    }
    private Color ReadPixel(Texture2D texture,int x,int y)
    {
        Color32[] pixels;
        if(!pixelsByTexture.TryGetValue(texture,out pixels))
        {
            if(texture.isReadable)pixels=texture.GetPixels32();
            else
            {
                var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                var readable=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
                try
                {
                    Graphics.Blit(texture,target);RenderTexture.active=target;
                    readable.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readable.Apply();pixels=readable.GetPixels32();
                }
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(readable);}
            }
            pixelsByTexture.Add(texture,pixels);
        }
        return pixels[Mathf.Clamp(y,0,texture.height-1)*texture.width+Mathf.Clamp(x,0,texture.width-1)];
    }
    private static Bounds VisibleBounds(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{sprite});
    private static void AssertFinite(Rect rect)
    {Assert.That(float.IsNaN(rect.x)||float.IsInfinity(rect.x)||float.IsNaN(rect.y)||float.IsInfinity(rect.y)||float.IsNaN(rect.width)||float.IsNaN(rect.height),Is.False);}
}
