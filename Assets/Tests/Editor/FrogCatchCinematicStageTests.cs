using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class FrogCatchCinematicStageTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private GameObject host;
    private FrogCatchCinematicStage stage;
    private Texture2D map,art;
    private Sprite first,second;

    [SetUp]
    public void SetUp()
    {
        host=new GameObject("Frog private cinematic tests");stage=new FrogCatchCinematicStage(host.transform);
        map=new Texture2D(8,8);map.SetPixels(Enumerable.Repeat(new Color(.22f,.35f,.58f),64).ToArray());map.Apply();
        art=new Texture2D(8,8);art.SetPixels(Enumerable.Repeat(Color.white,64).ToArray());art.Apply();
        first=Sprite.Create(art,new Rect(0,0,4,8),new Vector2(.5f,0f),8f);
        second=Sprite.Create(art,new Rect(4,0,4,8),new Vector2(.5f,0f),8f);
    }
    [TearDown]
    public void TearDown()
    {
        stage?.Dispose();Object.DestroyImmediate(host);Object.DestroyImmediate(first);Object.DestroyImmediate(second);
        Object.DestroyImmediate(art);Object.DestroyImmediate(map);
    }

    [Test]
    public void OnlyRealCapturedActorsCanStartAndTheCurrentMapAndActorSnapshotsAreRetained()
    {
        var source=Actor("golgaebi",Vector2.left);var enemies=new[]{Actor("geomchi",Vector2.right),Actor("nox",Vector2.up)};
        Assert.That(stage.Begin(map,source,new FrogCatchCinematicStage.Actor[0],enemies),Is.False);
        Assert.That(stage.Begin(null,source,enemies,null),Is.False);
        Assert.That(stage.Begin(map,source,enemies,new[]{Actor("frosk",Vector2.down)}),Is.True);
        enemies[0]=Actor("changed",Vector2.zero);
        Assert.That(stage.Background,Is.SameAs(map));Assert.That(stage.Source.species,Is.EqualTo("golgaebi"));
        Assert.That(stage.Captured[0].species,Is.EqualTo("geomchi"));Assert.That(stage.Bystanders[0].species,Is.EqualTo("frosk"));
        Assert.That(stage.Texture.width,Is.LessThanOrEqualTo(1600));Assert.That(stage.Texture.height,Is.LessThanOrEqualTo(900));
    }

    [TestCase("golgaebi",true)]
    [TestCase("frosk",true)]
    [TestCase("kingpa",true)]
    [TestCase("rokrok",true)]
    [TestCase("unretouched",false)]
    public void OtherSpeciesAndShinySourcesKeepTheirActualFramesColorAndFacing(string species,bool shiny)
    {
        var source=Actor(species,Vector2.zero);source.shiny=shiny;source.flipX=true;source.color=new Color(.62f,.8f,1f);
        Assert.That(stage.Begin(map,source,new[]{Actor("nox",Vector2.right)},null),Is.True);
        foreach(float time in new[]{.2f,1.4f,2.5f,3.5f})
        {
            stage.Tick(time,.1f);var renderer=SourceRenderer();
            Assert.That(renderer.sprite.texture,Is.SameAs(art));Assert.That(renderer.flipX,Is.True);
            Assert.That(renderer.color,Is.EqualTo(source.color));
        }
    }

    [TestCase("kingpa",false,"KingpaPoses")]
    [TestCase("kingpa",true,"KingpaPoses")]
    [TestCase("rokrok",false,"RokrokPoses")]
    [TestCase("rokrok",true,"RokrokPoses")]
    [TestCase("frosk",false,"FroskPoses")]
    [TestCase("frosk",true,"FroskPoses")]
    public void EachOtherFrogHasItsOwnFourOrderedPosesWithGroundedFeetAndContinuousWidth(string species,bool flipped,string expectedAtlas)
    {
        var authored=AuthoredSource(species);authored.flipX=flipped;stage.Warmup(species);
        Assert.That(stage.Begin(map,authored,Enemies(8),null),Is.True);
        Texture2D atlas=Resources.Load<Texture2D>(FrogCatchCinematicStage.ResourceRoot+expectedAtlas);
        Assert.That(atlas,Is.Not.Null);Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));
        var frames=new HashSet<Sprite>();
        foreach(float time in new[]{.35f,2.18f,2.75f,3.5f})
        {
            stage.Tick(time,0f);var renderer=SourceRenderer();frames.Add(renderer.sprite);
            Assert.That(renderer.sprite.texture,Is.SameAs(atlas));Assert.That(renderer.flipX,Is.EqualTo(flipped));
            Bounds bounds=VisibleBounds(renderer.sprite);Vector3 foot=SpritePoint(renderer,new Vector2(.5f,0f));
            var body=typeof(FrogCatchCinematicStage).GetField("sourceBody",Hidden).GetValue(stage);
            Vector3 ground=(Vector3)body.GetType().GetField("position").GetValue(body);
            Assert.That(Vector3.Distance(foot,renderer.transform.parent.TransformPoint(ground)),Is.LessThan(.002f));
            Assert.That(bounds.size.x,Is.GreaterThan(1f));Assert.That(bounds.size.y,Is.GreaterThan(1f));
        }
        Assert.That(frames.Count,Is.EqualTo(4));
        stage.Tick(0f,0f);float previous=VisibleWidth(SourceRenderer());
        for(int i=1;i<=400;i++)
        {
            stage.Tick(i*.01f,.01f);float width=VisibleWidth(SourceRenderer());
            Assert.That(Mathf.Abs(width-previous),Is.LessThan(.08f),"A new pose must not suddenly shrink the body.");previous=width;
        }
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),Enemies(1),null),Is.True);
        Assert.That(stage.Begin(map,authored,Enemies(1),null),Is.True);stage.Tick(2.75f,0f);
        Assert.That(SourceRenderer().sprite.texture,Is.SameAs(atlas),"Reusing the stage must select this frog's own poses again.");
    }

    [TestCase("golgaebi")]
    [TestCase("kingpa")]
    [TestCase("rokrok")]
    [TestCase("frosk")]
    public void ShinySourcesHoldTheirAuthoredOpenMouthInsteadOfLoopingBackToIdleDuringTheCatch(string species)
    {
        var authored=AuthoredSource(species,true);authored.shiny=true;
        Assert.That(stage.Begin(map,authored,Enemies(8),null),Is.True);
        for(int i=0;i<38;i++)
        {
            stage.Tick(i*.05f,.05f);
            Assert.That(SourceRenderer().sprite,Is.SameAs(authored.frames[3]));
        }
        stage.Tick(3.5f,0f);Assert.That(SourceRenderer().sprite,Is.SameAs(authored.frames[3]),"A closed-mouth return frame cannot throw a tongue-bound victim.");
    }

    [TestCase("golgaebi",false,false)]
    [TestCase("golgaebi",true,false)]
    [TestCase("kingpa",false,false)]
    [TestCase("kingpa",true,false)]
    [TestCase("rokrok",false,false)]
    [TestCase("rokrok",true,false)]
    [TestCase("frosk",false,false)]
    [TestCase("frosk",true,false)]
    [TestCase("frosk",false,true)]
    [TestCase("frosk",true,true)]
    public void EveryPaintedPoseAndShinyFroskStartTheTongueInsideTheActualDarkOpenMouthOnEitherFacing(string species,bool flipped,bool shiny)
    {
        var authored=AuthoredSource(species,shiny);authored.flipX=flipped;authored.shiny=shiny;
        Assert.That(stage.Begin(map,authored,Enemies(8),null),Is.True);
        foreach(float time in new[]{.35f,2.18f,2.75f,3.5f})
        {
            stage.Tick(time,0f);var renderer=SourceRenderer();Sprite frame=renderer.sprite;
            var tongues=host.GetComponentsInChildren<MeshFilter>(true).Where(filter=>filter.name=="Captured enemy pink branch tongue").ToArray();
            var tongue=tongues[0];
            Vector3[] vertices=tongue.sharedMesh.vertices;
            Vector3 root=(vertices[0]+vertices[1]+vertices[2]+vertices[3])*.25f;
            foreach(var branch in tongues)
            {
                var points=branch.sharedMesh.vertices;
                Assert.That(Vector3.Distance(root,(points[0]+points[1]+points[2]+points[3])*.25f),Is.LessThan(.001f));
            }
            Vector3 local=Quaternion.Inverse(renderer.transform.localRotation)*(root-renderer.transform.localPosition);
            Vector3 scale=renderer.transform.localScale;
            local=new Vector3(local.x/scale.x,local.y/scale.y,local.z/scale.z);
            if(renderer.flipX)local.x=-local.x;
            int x=Mathf.FloorToInt(frame.rect.x+frame.pivot.x+local.x*frame.pixelsPerUnit);
            int y=Mathf.FloorToInt(frame.rect.y+frame.pivot.y+local.y*frame.pixelsPerUnit);
            Color paintedMouth=ReadPixel(frame.texture,x,y);
            Assert.That(paintedMouth.a,Is.GreaterThan(.9f),"The tongue must not emerge from transparent space.");
            Assert.That(Mathf.Max(paintedMouth.r,paintedMouth.g,paintedMouth.b),Is.LessThan(.7f),
                "The attachment belongs inside the painted mouth, away from bright cheeks, crowns and toes.");
        }
    }

    [Test]
    public void ExplicitTicksAnimatePerspectiveBodyTensionTonguesAndForegroundWithoutChangingTheGameplayCameraOrClock()
    {
        var gameCamera=new GameObject("Unrelated gameplay camera");gameCamera.transform.SetParent(host.transform,false);
        var main=gameCamera.AddComponent<Camera>();main.enabled=false;main.orthographic=true;main.fieldOfView=37f;
        main.transform.SetPositionAndRotation(new Vector3(2,5,-8),Quaternion.Euler(25,11,3));
        Vector3 position=main.transform.position;Quaternion rotation=main.transform.rotation;float scale=Time.timeScale;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin(map,Actor("frosk",Vector2.zero),Enemies(8),null),Is.True);
            Camera view=Camera();Assert.That(view.orthographic,Is.False);Assert.That(view.targetTexture,Is.SameAs(stage.Texture));
            stage.Tick(.3f,.1f);Quaternion enemyEye=view.transform.rotation;Assert.That(stage.Shot,Is.EqualTo(1));
            stage.Tick(1.3f,.1f);Assert.That(stage.Shot,Is.EqualTo(2));Assert.That(Quaternion.Angle(view.transform.rotation,enemyEye),Is.GreaterThan(35f));
            stage.Tick(2.50f,.1f);Assert.That(stage.Shot,Is.EqualTo(3));Vector3 body=SourceRenderer().transform.localScale;
            Vector3[] rope=host.GetComponentsInChildren<MeshFilter>(true).First(filter=>filter.name=="Captured enemy pink branch tongue").sharedMesh.vertices;
            stage.Tick(2.60f,.1f);Assert.That(SourceRenderer().transform.localScale,Is.Not.EqualTo(body));
            Vector3[] next=host.GetComponentsInChildren<MeshFilter>(true).First(filter=>filter.name=="Captured enemy pink branch tongue").sharedMesh.vertices;
            Assert.That(next.Where((point,index)=>(point-rope[index]).sqrMagnitude>.000001f).Count(),Is.GreaterThan(20));
            stage.Tick(3.8f,.1f);Assert.That(stage.Shot,Is.EqualTo(4));
            SpriteRenderer foreground=host.GetComponentsInChildren<SpriteRenderer>(true).First(renderer=>renderer.name=="Actual enemy lens wipe");
            Assert.That(foreground.enabled,Is.True);Assert.That(foreground.sprite.texture,Is.SameAs(art));
            Vector3 last=view.transform.position;stage.Tick(3.8f,0f);Assert.That(view.transform.position,Is.EqualTo(last));
            Assert.That(main.transform.position,Is.EqualTo(position));Assert.That(main.transform.rotation,Is.EqualTo(rotation));
            Assert.That(main.orthographic,Is.True);Assert.That(main.fieldOfView,Is.EqualTo(37f));Assert.That(Time.timeScale,Is.Zero);
        }
        finally{Time.timeScale=scale;}
    }

    [Test]
    public void ReusingTheStageWithOneEnemyDoesNotResurrectSevenPreviousActorsOrTongues()
    {
        Assert.That(stage.Begin(map,Actor("frosk",Vector2.zero),Enemies(8),Enemies(3)),Is.True);stage.Tick(1.4f,.1f);
        Assert.That(stage.Begin(map,Actor("kingpa",Vector2.zero),Enemies(1),null),Is.True);stage.Tick(1.4f,.1f);
        Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Count(renderer=>renderer.name=="Captured cinematic enemy"&&renderer.enabled),Is.EqualTo(1));
        Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Count(renderer=>renderer.name=="Bystander cinematic actor"&&renderer.enabled),Is.Zero);
        Assert.That(host.GetComponentsInChildren<MeshRenderer>(true).Count(renderer=>renderer.name=="Captured enemy pink branch tongue"&&renderer.enabled),Is.EqualTo(1));
        stage.Hide();Assert.That(stage.Active,Is.False);stage.Tick(2f,.5f);Assert.That(stage.Active,Is.False);
        var texture=(RenderTexture)stage.Texture;stage.Dispose();Assert.That(texture==null||!texture.IsCreated(),Is.True);stage.Dispose();
    }

    [Test]
    public void NormalGolgaebiLoadsFourFootAnchoredRetouchedPosesAndNormalizesTheirVisibleWidth()
    {
        Assert.That(Resources.Load<Texture2D>(FrogCatchCinematicStage.ResourceRoot+"GolgaebiPoses"),Is.Not.Null);
        stage.Warmup();Assert.That(stage.Active,Is.False);
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),Enemies(8),null),Is.True);
        var frames=new HashSet<Sprite>();
        foreach(float time in new[]{.4f,2.18f,2.75f,3.5f})
        {
            stage.Tick(time,.1f);SpriteRenderer renderer=SourceRenderer();frames.Add(renderer.sprite);
            Assert.That(renderer.sprite.texture,Is.Not.SameAs(art));Assert.That(renderer.sprite.rect.width,Is.EqualTo(627f));
            Assert.That(renderer.sprite.pivot.y,Is.GreaterThan(20f));
        }
        Assert.That(frames.Count,Is.EqualTo(4));
        stage.Tick(2.18f,.1f);float firstWidth=VisibleWidth(SourceRenderer());
        stage.Tick(2.75f,.1f);Assert.That(VisibleWidth(SourceRenderer()),Is.EqualTo(firstWidth).Within(.03f));
    }

    [Test]
    public void TheHighCameraRollAndTheLowAngleRemainContinuousAcrossEveryShotBoundary()
    {
        Assert.That(stage.Begin(map,Actor("frosk",Vector2.zero),Enemies(8),null),Is.True);
        stage.Tick(0f,0f);Quaternion previous=Camera().transform.rotation;
        for(int frame=1;frame<=400;frame++)
        {
            stage.Tick(frame*.01f,.01f);Quaternion next=Camera().transform.rotation;
            Assert.That(Quaternion.Angle(previous,next),Is.LessThan(12f),"No up-vector switch may flip the overhead shot.");
            previous=next;
        }
    }

    [TestCase(-.8f,.6f)]
    [TestCase(.7f,-.5f)]
    public void AuthoredSceneryUsesTheSelectedMapWhileActualActorsKeepTheirSharedGroundCoordinates(float x,float y)
    {
        var source=Actor("frosk",new Vector2(x,y));var enemies=Enemies(3);
        Assert.That(stage.Begin(map,source,enemies,null,"Environment/FrozenHighlands"),Is.True);
        Assert.That(stage.SceneryRegion,Is.EqualTo("FrozenHighlands"));
        Assert.That(stage.SceneryKit,Is.SameAs(Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.FloorResourceRoot+"FrozenHighlands")));
        Assert.That(stage.SceneryKit,Is.Not.SameAs(map),"The gameplay image must not be projected into a different camera angle.");
        var bodies=(System.Collections.IList)typeof(FrogCatchCinematicStage).GetField("capturedBodies",Hidden).GetValue(stage);
        for(int i=0;i<enemies.Length;i++)
        {
            Vector3 ground=(Vector3)bodies[i].GetType().GetField("ground").GetValue(bodies[i]);
            Assert.That(ground.x,Is.EqualTo((enemies[i].ground.x-x)*11f).Within(.001f));
            Assert.That(ground.z,Is.EqualTo((enemies[i].ground.y-y)*8f).Within(.001f));
        }
    }

    [Test]
    public void NewTonguesUseThePaintedPixelTextureAndCapturedBodiesHaveVisibleStickyWraps()
    {
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),Enemies(8),null),Is.True);stage.Tick(1.3f,.1f);
        var filters=host.GetComponentsInChildren<MeshRenderer>(true);
        var tongue=filters.First(value=>value.name=="Captured enemy pink branch tongue");
        Assert.That(tongue.sharedMaterial.mainTexture,Is.SameAs(Resources.Load<Texture2D>(FrogCatchCinematicStage.ResourceRoot+"TongueRibbonV2")));
        Vector4 uv=tongue.sharedMaterial.GetVector("_UvRect");
        Assert.That(uv.z,Is.GreaterThan(.8f));Assert.That(uv.w,Is.InRange(.05f,.4f));
        Assert.That(filters.Count(value=>value.name=="Sticky tongue wrap around captured enemy"&&value.enabled),Is.EqualTo(8));
        var branches=host.GetComponentsInChildren<MeshFilter>(true).Where(value=>value.name=="Captured enemy pink branch tongue").ToArray();
        var wraps=host.GetComponentsInChildren<MeshFilter>(true).Where(value=>value.name=="Sticky tongue wrap around captured enemy").ToArray();
        for(int i=0;i<8;i++)
        {
            var line=branches[i].sharedMesh.vertices;var coil=wraps[i].sharedMesh.vertices;
            Vector3 end=(line[128]+line[129]+line[130]+line[131])*.25f;
            Vector3 start=(coil[0]+coil[1]+coil[2]+coil[3])*.25f;
            Assert.That(Vector3.Distance(end,start),Is.LessThan(.002f),"The wrapping tongue must stay joined while the captured body rolls.");
        }
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),Enemies(1),null),Is.True);stage.Tick(1.3f,.1f);
        Assert.That(filters.Count(value=>value.name=="Sticky tongue wrap around captured enemy"&&value.enabled),Is.EqualTo(1));
        stage.Hide();Assert.That(filters.Count(value=>value.name=="Sticky tongue wrap around captured enemy"&&value.enabled),Is.Zero);
    }

    [Test]
    public void OpeningShowsOnlyLowerForegroundAndAnEdgeTongueLeavingTheFrogVisible()
    {
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),Enemies(8),new[]{Actor("bystander",Vector2.down)}),Is.True);
        var foreground=Renderer("Actual enemy lens wipe");
        foreach(float time in new[]{.08f,.20f,.35f,.50f})
        {
            stage.Tick(time,.05f);Rect paws=ViewportRect(foreground,true);
            Assert.That(paws.yMax,Is.LessThan(.27f));Assert.That(paws.yMax,Is.GreaterThan(.10f));
            Assert.That(ViewportRect(SourceRenderer()).center.y,Is.InRange(.28f,.80f));
            Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Count(value=>value.name=="Captured cinematic enemy"&&value.enabled),Is.Zero);
            Assert.That(Renderer("Bystander cinematic actor").enabled,Is.True);
            Assert.That(host.GetComponentsInChildren<MeshRenderer>(true).Count(value=>value.name=="Captured enemy pink branch tongue"&&value.enabled),Is.EqualTo(8));
            var ribbon=host.GetComponentsInChildren<MeshFilter>(true).First(value=>value.name=="Tongue wrapped around the lens");
            foreach(Vector3 point in ribbon.sharedMesh.vertices)
            {
                Vector3 viewport=Camera().WorldToViewportPoint(ribbon.transform.TransformPoint(point));
                Assert.That(viewport.x>.20f&&viewport.x<.80f&&viewport.y>.27f&&viewport.y<.78f,Is.False,
                    "The lens sweep must stay at the upper/left edge, away from the frog in the center.");
            }
        }
        stage.Tick(.58f,.05f);
        Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Count(value=>value.name=="Captured cinematic enemy"&&value.enabled),Is.Zero);
        stage.Tick(.715f,.05f);
        var captured=host.GetComponentsInChildren<SpriteRenderer>(true).Where(value=>value.name=="Captured cinematic enemy").ToArray();
        Assert.That(captured.All(value=>value.enabled&&Mathf.Abs(value.color.a-.5f)<.001f),Is.True);
        stage.Tick(.85f,.05f);Assert.That(captured.All(value=>value.enabled&&Mathf.Abs(value.color.a-1f)<.001f),Is.True);
    }

    [Test]
    public void TheOverheadShotSpreadsEightCapturedIdentitiesAcrossWidthAndDepthWithClearBodySpacing()
    {
        // This recreates the failed QA case: enemies were already almost together when the cinematic began.
        var enemies=Enemies(8);for(int i=0;i<enemies.Length;i++)enemies[i].ground*=.05f;
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),enemies,null),Is.True);stage.Tick(1.30f,.1f);
        var bodies=host.GetComponentsInChildren<SpriteRenderer>(true).Where(value=>value.name=="Captured cinematic enemy"&&value.enabled).ToArray();
        Assert.That(bodies.Length,Is.EqualTo(8));var frames=bodies.Select(value=>ViewportRect(value)).ToArray();
        float minX=frames.Min(value=>value.xMin),maxX=frames.Max(value=>value.xMax);
        float minY=frames.Min(value=>value.yMin),maxY=frames.Max(value=>value.yMax);
        Assert.That(maxX-minX,Is.GreaterThan(.55f));Assert.That(maxY-minY,Is.GreaterThan(.45f));
        foreach(Rect frame in frames)
        {
            Assert.That(frame.center.x,Is.InRange(.04f,.96f));Assert.That(frame.center.y,Is.InRange(.04f,.96f));
        }
        for(int a=0;a<frames.Length;a++)for(int b=a+1;b<frames.Length;b++)
        {
            float width=Mathf.Max(0f,Mathf.Min(frames[a].xMax,frames[b].xMax)-Mathf.Max(frames[a].xMin,frames[b].xMin));
            float height=Mathf.Max(0f,Mathf.Min(frames[a].yMax,frames[b].yMax)-Mathf.Max(frames[a].yMin,frames[b].yMin));
            Assert.That(width*height,Is.LessThan(frames[a].width*frames[a].height*.15f),"Two captured bodies must not form one unreadable stack.");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TheFinalShotShowsARealEnemyFaceAndTheBattlefieldUntilTheLastTenthOfASecond(bool flipped)
    {
        var enemy=Actor("geomchi",Vector2.right);enemy.flipX=flipped;enemy.size=2.7f;
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),new[]{enemy},null),Is.True);
        var foreground=Renderer("Actual enemy lens wipe");stage.Tick(3.68f,.1f);Rect readable=ViewportRect(foreground);
        Vector3 face=Camera().WorldToViewportPoint(SpritePoint(foreground,new Vector2(.72f,.72f)));
        Assert.That(face.x,Is.InRange(.15f,.35f));Assert.That(face.y,Is.InRange(.35f,.65f));
        Assert.That(readable.height,Is.LessThan(.70f));Assert.That(readable.xMax,Is.LessThan(.75f));
        stage.Tick(3.89f,.1f);Assert.That(ViewportRect(foreground).height,Is.LessThan(.85f));
        stage.Tick(3.96f,.1f);Rect cover=ViewportRect(foreground);
        Assert.That(cover.height,Is.GreaterThan(3f));Assert.That(cover.width,Is.GreaterThan(1f));
        Assert.That(foreground.sprite.texture,Is.SameAs(art));
    }

    [Test]
    public void ForceDustPixelsAreHalfThePreviousMaximumSize()
    {
        Assert.That(stage.Begin(map,Actor("golgaebi",Vector2.zero),Enemies(8),null),Is.True);stage.Tick(2.65f,.1f);
        var particles=host.GetComponentsInChildren<ParticleSystem>(true).Single();var buffer=new ParticleSystem.Particle[144];
        int count=particles.GetParticles(buffer);Assert.That(count,Is.GreaterThan(0));
        for(int i=0;i<count;i++)Assert.That(buffer[i].startSize,Is.LessThanOrEqualTo(.0475f));
    }

    [Test]
    public void AdditionalSummonedActorsAreNeverSilentlyTruncatedFromTheSnapshot()
    {
        Assert.That(stage.Begin(map,Actor("frosk",Vector2.zero),Enemies(40),Enemies(35)),Is.True);
        Assert.That(stage.Captured.Count,Is.EqualTo(40));Assert.That(stage.Captured[39].species,Is.EqualTo("enemy39"));
        Assert.That(stage.Bystanders.Count,Is.EqualTo(35));
    }

    private FrogCatchCinematicStage.Actor Actor(string species,Vector2 ground)=>new FrogCatchCinematicStage.Actor
    {species=species,sprite=first,frames=new[]{first,second},ground=ground,size=1f,color=Color.white};
    private FrogCatchCinematicStage.Actor AuthoredSource(string species,bool shiny=false)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        string resource=shiny&&!string.IsNullOrWhiteSpace(definition.shinyResourcePath)?definition.shinyResourcePath:definition.resourcePath;
        var driver=Resources.Load<GameObject>(resource).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var result=Actor(species,Vector2.zero);result.sprite=driver.IdleFrames[0];
        result.frames=new Sprite[driver.SkillAttackFrameCount];
        for(int i=0;i<result.frames.Length;i++)result.frames[i]=driver.GetSkillAttackFrame(i);
        return result;
    }
    private Color ReadPixel(Texture2D texture,int x,int y)
    {
        if(texture.isReadable)return texture.GetPixel(x,y);
        var previous=RenderTexture.active;
        var target=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var pixel=new Texture2D(1,1,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(texture,target,new Vector2(1f/texture.width,1f/texture.height),new Vector2(x/(float)texture.width,y/(float)texture.height));
            RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();
            return pixel.GetPixel(0,0);
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(pixel);}
    }
    private FrogCatchCinematicStage.Actor[] Enemies(int count)
    {
        var result=new FrogCatchCinematicStage.Actor[count];
        for(int i=0;i<count;i++)result[i]=Actor("enemy"+i,new Vector2(Mathf.Cos(i*2.4f)*.6f,Mathf.Sin(i*2.4f)*.6f));return result;
    }
    private Camera Camera()=>(Camera)typeof(FrogCatchCinematicStage).GetField("camera",Hidden).GetValue(stage);
    private SpriteRenderer SourceRenderer()=>host.GetComponentsInChildren<SpriteRenderer>(true).First(renderer=>renderer.name=="Frog cinematic source");
    private SpriteRenderer Renderer(string name)=>host.GetComponentsInChildren<SpriteRenderer>(true).First(renderer=>renderer.name==name);
    private Bounds VisibleBounds(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{sprite});
    private Vector3 SpritePoint(SpriteRenderer renderer,Vector2 point)
    {
        Bounds bounds=VisibleBounds(renderer.sprite);
        Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,point.x),Mathf.Lerp(bounds.min.y,bounds.max.y,point.y),0f);
        if(renderer.flipX)local.x=-local.x;
        return renderer.transform.TransformPoint(local);
    }
    private Rect ViewportRect(SpriteRenderer renderer,bool lowerBody=false)
    {
        Bounds bounds=VisibleBounds(renderer.sprite);Vector4 crop=renderer.sharedMaterial.GetVector("_BodyClip");
        if(lowerBody&&crop.w>.5f){Vector3 max=bounds.max;max.y=Mathf.Min(max.y,crop.y);bounds.SetMinMax(bounds.min,max);}
        Vector2 min=Vector2.one*float.MaxValue,maxPoint=Vector2.one*float.MinValue;
        for(int i=0;i<4;i++)
        {
            Vector3 local=new Vector3(i%2==0?bounds.min.x:bounds.max.x,i<2?bounds.min.y:bounds.max.y,0f);if(renderer.flipX)local.x=-local.x;
            Vector3 point=Camera().WorldToViewportPoint(renderer.transform.TransformPoint(local));
            min=Vector2.Min(min,point);maxPoint=Vector2.Max(maxPoint,point);
        }
        return Rect.MinMaxRect(min.x,min.y,maxPoint.x,maxPoint.y);
    }
    private float VisibleWidth(SpriteRenderer renderer)
    {
        var method=typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.NonPublic|BindingFlags.Static);
        return ((Bounds)method.Invoke(null,new object[]{renderer.sprite})).size.x*Mathf.Abs(renderer.transform.localScale.x);
    }
}
