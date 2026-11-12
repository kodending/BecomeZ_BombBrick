using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WolfStampedeCinematicStageTests
{
    private GameObject host;
    private WolfStampedeCinematicStage stage;
    private Texture2D art,map;
    private Sprite sprite;
    [SetUp] public void Setup()
    {
        host=new GameObject("Wolf stampede private stage tests");stage=new WolfStampedeCinematicStage(host.transform);
        art=new Texture2D(8,8);art.SetPixels(Enumerable.Repeat(Color.white,64).ToArray());art.Apply();
        sprite=Sprite.Create(art,new Rect(0f,0f,8f,8f),Vector2.one*.5f,8f);
        map=new Texture2D(8,8);map.SetPixels(Enumerable.Repeat(Color.green,64).ToArray());map.Apply();
    }
    [TearDown] public void Cleanup()
    {stage?.Dispose();Object.DestroyImmediate(host);Object.DestroyImmediate(sprite);Object.DestroyImmediate(art);Object.DestroyImmediate(map);}

    [TestCase(.3f,1)] [TestCase(1.1f,2)] [TestCase(2.2f,3)] [TestCase(3.2f,4)]
    public void PawContactsRisingPackLowRunAndLensWipeFollowTheExplicitClock(float time,int shot)
    {
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Tick(time,0f);
        Assert.That(stage.Shot,Is.EqualTo(shot));Assert.That(WolfStampedeCinematicStage.Duration,Is.EqualTo(3.6f));
        AssertFinite(View().transform.localPosition);AssertFinite(View().transform.localRotation.eulerAngles);
    }

    [TestCase("nukgu")] [TestCase("honga")] [TestCase("seollang")] [TestCase("wolyeong")]
    public void OriginalSpeciesAndShinyTintRemainOnTheHowlingCaster(string id)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var actor=Source();actor.species=id;actor.sprite=driver.GetSkillAttackFrame(0);actor.shiny=true;actor.flipX=true;
        actor.frames=new Sprite[driver.SkillAttackFrameCount];for(int i=0;i<actor.frames.Length;i++)actor.frames[i]=driver.GetSkillAttackFrame(i);
        actor.color=new Color(.8f,.6f,.9f,1f);
        Assert.That(stage.Begin(actor,null),Is.True);stage.Tick(1f,0f);
        var caster=Renderer("Wolf original caster");var block=new MaterialPropertyBlock();caster.GetPropertyBlock(block);
        Assert.That(caster.enabled,Is.True);Assert.That(actor.frames.Contains(caster.sprite),Is.True);Assert.That(caster.flipX,Is.True);
        Assert.That(caster.color.r,Is.EqualTo(.8f));Assert.That(caster.color.g,Is.EqualTo(.6f));
        Assert.That(block.GetFloat("_Spectral"),Is.Zero);Assert.That(stage.Source.species,Is.EqualTo(id));
    }

    [Test]
    public void CompressingAnOriginalPoseCannotEnlargeTheWholeCasterToKeepItsHeightConstant()
    {
        var poses=new Texture2D(32,16);var pixels=new Color[32*16];
        for(int y=2;y<14;y++)for(int x=4;x<12;x++)pixels[y*32+x]=Color.white;
        for(int y=4;y<8;y++)for(int x=17;x<31;x++)pixels[y*32+x]=Color.white;
        poses.SetPixels(pixels);poses.Apply();
        var tall=Sprite.Create(poses,new Rect(0f,0f,16f,16f),Vector2.one*.5f,8f);
        var compressed=Sprite.Create(poses,new Rect(16f,0f,16f,16f),Vector2.one*.5f,8f);
        try
        {
            var actor=Source();actor.sprite=tall;actor.frames=new[]{tall,compressed};
            Assert.That(stage.Begin(actor,null),Is.True);stage.Tick(.8f,0f);
            var caster=Renderer("Wolf original caster");Assert.That(caster.sprite,Is.SameAs(tall));Vector3 scale=caster.transform.localScale;
            stage.Tick(1.35f,0f);Assert.That(caster.sprite,Is.SameAs(compressed));
            Assert.That(caster.transform.localScale,Is.EqualTo(scale));
            Assert.That(VisibleBody(compressed).size.y*scale.y,Is.LessThan(VisibleBody(tall).size.y*scale.y*.5f));
        }
        finally{stage.Hide();Object.DestroyImmediate(tall);Object.DestroyImmediate(compressed);Object.DestroyImmediate(poses);}
    }

    [Test]
    public void TwentyFourSpiritsUseRealGallopFramesAndIrregularPositionsSpeedsAndSizes()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);var originals=PixelWolfStampede.Frames;
        Assert.That(originals,Has.Length.EqualTo(8));var seen=new System.Collections.Generic.HashSet<Sprite>();float initialScale=0f;
        for(int step=0;step<16;step++)
        {
            stage.Tick(1.5f+step*.031f,0f);var wolf=Visible(Renderer("Wolf running spirit 1"));seen.Add(wolf.sprite);
            Assert.That(originals.Contains(wolf.sprite),Is.True);
            if(initialScale==0f)initialScale=wolf.transform.localScale.z;
            Assert.That(wolf.transform.localScale.z,Is.EqualTo(initialScale).Within(.0001f));
        }
        Assert.That(seen.Count,Is.GreaterThanOrEqualTo(7),"The pack must animate real running poses.");
        stage.Tick(2f,0f);
        var primary=host.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith("Wolf running spirit ")&&
            !r.name.Contains("bridge")&&!r.name.Contains("shadow")).ToArray();
        Assert.That(primary,Has.Length.EqualTo(24));
        Assert.That(primary.Select(r=>Mathf.RoundToInt(Visible(r).transform.localPosition.z*10f)).Distinct().Count(),Is.GreaterThanOrEqualTo(18));
        Assert.That(primary.Select(r=>Mathf.RoundToInt(Visible(r).transform.localPosition.x*10f)).Distinct().Count(),Is.GreaterThanOrEqualTo(18));
        Assert.That(primary.Select(r=>Mathf.RoundToInt(Visible(r).transform.localScale.y*1000f)).Distinct().Count(),Is.GreaterThanOrEqualTo(18));
        foreach(var renderer in primary)Assert.That(Visible(renderer).enabled,Is.True);
        var early=primary.Select(r=>Visible(r).transform.localPosition).ToArray();stage.Tick(2.2f,0f);
        for(int i=0;i<primary.Length;i++)
        {var renderer=Visible(primary[i]);Assert.That(renderer.enabled,Is.True);Assert.That(renderer.transform.localPosition.x,Is.GreaterThan(early[i].x+.5f));}
        Assert.That(primary.Select((r,i)=>Mathf.RoundToInt((Visible(r).transform.localPosition.x-early[i].x)*100f)).Distinct().Count(),Is.GreaterThanOrEqualTo(12));
    }

    [TestCase(false)] [TestCase(true)]
    public void EveryRunnerAndForegroundUseTheSameSideDrawingsAlongASideOnlyCameraPath(bool flip)
    {
        var actor=Source();actor.flipX=flip;Assert.That(stage.Begin(actor,null),Is.True);var side=PixelWolfStampede.Frames;
        var seen=new System.Collections.Generic.HashSet<Sprite>();float scale=0f;
        for(int step=0;step<72;step++)
        {
            stage.Tick(1.5f+step*.027f,0f);var runner=Visible(Renderer("Wolf running spirit 0"));
            Assert.That(side.Contains(runner.sprite),Is.True);Assert.That(runner.flipX,Is.EqualTo(flip));
            var forward=View().transform.localRotation*Vector3.forward;
            Assert.That(Mathf.Abs(forward.x),Is.LessThan(.00001f),"A side drawing cannot be viewed from its front.");
            seen.Add(runner.sprite);if(scale==0f)scale=runner.transform.localScale.y;
            Assert.That(runner.transform.localScale.y,Is.EqualTo(scale).Within(.0001f));
        }
        Assert.That(seen.Count,Is.EqualTo(8));stage.Tick(3.15f,0f);
        Assert.That(side.Contains(Renderer("Wolf lens crossing spirit").sprite),Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void LiveMapIsMetadataOnlyAndTheLiveCameraAndClockStayUntouched(bool flip)
    {
        var live=new GameObject("Unrelated live camera");var camera=live.AddComponent<Camera>();camera.enabled=false;
        live.transform.SetPositionAndRotation(new Vector3(3f,5f,-8f),Quaternion.Euler(35f,12f,0f));
        Vector3 position=live.transform.position;Quaternion rotation=live.transform.rotation;float speed=Time.timeScale;
        var actor=Source();actor.flipX=flip;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin(actor,map),Is.True);stage.Tick(2.2f,0f);
            Assert.That(stage.Source.flipX,Is.EqualTo(flip));Assert.That(stage.Background,Is.SameAs(map));
            foreach(var renderer in host.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterial!=null)Assert.That(renderer.sharedMaterial.mainTexture,Is.Not.SameAs(map));
            Assert.That(Mesh("Wolf dedicated moon hunt panorama").GetComponent<MeshRenderer>().sharedMaterial.mainTexture,
                Is.SameAs(Resources.Load<Texture2D>(WolfStampedeCinematicStage.ResourceRoot+"MoonHuntPanorama")));
            Assert.That(Mesh("Wolf dedicated moon hunt ground").GetComponent<MeshRenderer>().sharedMaterial.mainTexture,
                Is.SameAs(Resources.Load<Texture2D>(WolfStampedeCinematicStage.ResourceRoot+"MoonHuntGround")));
            Assert.That(live.transform.position,Is.EqualTo(position));Assert.That(live.transform.rotation,Is.EqualTo(rotation));
            Assert.That(Time.timeScale,Is.Zero);Assert.That(camera.enabled,Is.False);
            Assert.That(stage.Begin(actor,null),Is.True);Assert.That(stage.Background,Is.Null);
        }
        finally{Time.timeScale=speed;Object.DestroyImmediate(live);}
    }

    [TestCase(false)] [TestCase(true)]
    public void AuthoredPanoramaKeepsItsAspectAndCoversTheFrameThroughBothTeamCameraPaths(bool flip)
    {
        var actor=Source();actor.flipX=flip;Assert.That(stage.Begin(actor,null),Is.True);
        Transform matte=Mesh("Wolf dedicated moon hunt panorama").transform;Camera view=View();
        var image=(Texture2D)matte.GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
        Assert.That(matte.parent,Is.SameAs(view.transform));Assert.That(Mesh("Wolf dedicated moon hunt panorama").sharedMesh.vertexCount,Is.EqualTo(4));
        foreach(float aspect in new[]{16f/9f,21f/9f,9f/16f})
        {
            view.aspect=aspect;
            foreach(float time in new[]{.2f,.75f,1.2f,1.8f,2.4f,3.2f,3.5f})
            {
                stage.Tick(time,0f);
                Assert.That(matte.localScale.x/matte.localScale.y,Is.EqualTo(image.width/(float)image.height).Within(.0001f));
                Assert.That(Quaternion.Angle(matte.localRotation,Quaternion.identity),Is.LessThan(.001f));
                var corners=Mesh("Wolf dedicated moon hunt panorama").sharedMesh.vertices.Select(v=>view.WorldToViewportPoint(matte.TransformPoint(v))).ToArray();
                Assert.That(corners.Min(p=>p.x),Is.LessThan(0f));Assert.That(corners.Max(p=>p.x),Is.GreaterThan(1f));
                Assert.That(corners.Min(p=>p.y),Is.LessThan(0f));Assert.That(corners.Max(p=>p.y),Is.GreaterThan(1f));
            }
        }
    }

    [Test]
    public void OpeningContactsUseTheDrawnPadAndFourToesAtlasAndAnimateDustAtTheGround()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Tick(.12f,0f);
        var impressions=Mesh("Wolf shaped paw impressions");Mesh paws=impressions.sharedMesh;
        Assert.That(impressions.GetComponent<MeshRenderer>().sharedMaterial.shader.name,Does.Contain("Drawn Ground Contacts"));
        Assert.That(impressions.GetComponent<MeshRenderer>().sharedMaterial.mainTexture,
            Is.SameAs(Resources.Load<Texture2D>(WolfStampedeCinematicStage.ResourceRoot+"HuntEffectsAction")));
        Assert.That(paws.colors32.Count(c=>c.a>0),Is.GreaterThanOrEqualTo(4));
        var padUV=paws.uv.Take(4).ToArray();Assert.That(padUV.Min(p=>p.y),Is.GreaterThan(.5f));
        Assert.That(padUV.Max(p=>p.x)-padUV.Min(p=>p.x),Is.LessThan(.24f),"Transparent atlas padding must not shrink the visible pad.");
        Assert.That(paws.vertices.Take(4).All(p=>Mathf.Abs(p.y-.024f)<.0001f),Is.True);
        Assert.That(Renderer("Wolf running spirit 0").enabled,Is.False);
        var dust=Mesh("Wolf foot contact dust").sharedMesh;Assert.That(dust.colors32.Count(c=>c.a>0),Is.GreaterThanOrEqualTo(4));
        Assert.That(dust.uv.Take(4).Max(p=>p.y),Is.LessThan(.5f));Assert.That(dust.vertices.Take(4).Min(p=>p.y),Is.InRange(.02f,.12f));
        stage.Tick(.22f,0f);Assert.That(paws.uv[0].x,Is.GreaterThan(padUV[0].x+.25f));
        stage.Tick(.52f,0f);Assert.That(paws.colors32.Count(c=>c.a>0),Is.GreaterThanOrEqualTo(12));
    }

    [Test]
    public void TheMoonIsRevealedOnlyAsTheGroundCloseupOpensIntoThePackShot()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);var material=Mesh("Wolf dedicated moon hunt panorama").GetComponent<MeshRenderer>().sharedMaterial;
        stage.Tick(.3f,0f);Assert.That(material.GetFloat("_PanoramaReveal"),Is.Zero);
        stage.Tick(.75f,0f);Assert.That(material.GetFloat("_PanoramaReveal"),Is.InRange(.1f,.9f));
        stage.Tick(1.1f,0f);Assert.That(material.GetFloat("_PanoramaReveal"),Is.EqualTo(1f));
    }

    [Test]
    public void HistoricalPixelWakesFollowBehindTheRunningPackInsteadOfReplacingThePawShape()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Tick(2.2f,0f);
        var wake=host.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="Wolf historical pixel wakes and wind");
        Assert.That(wake.GetComponent<ParticleSystemRenderer>().sortingOrder,Is.LessThan(Renderer("Wolf running spirit 0").sortingOrder),
            "The dense tail layer must stay behind readable bodies and faces.");
        var pixels=new ParticleSystem.Particle[2400];int count=wake.GetParticles(pixels);Assert.That(count,Is.GreaterThan(900));
        float lead=Visible(Renderer("Wolf running spirit 0")).transform.localPosition.x;
        Assert.That(pixels.Take(count).Count(p=>p.position.x<lead-.5f),Is.GreaterThan(50));
        Assert.That(host.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(1),"Ground contacts must use the authored atlas meshes.");
    }

    [Test]
    public void TailPixelsRemainAtTheirBirthPlaceAndSpreadWhileTheWolfKeepsRunning()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Tick(2.2f,0f);
        var wake=host.GetComponentsInChildren<ParticleSystem>(true).Single();
        var first=new ParticleSystem.Particle[2400];int count=wake.GetParticles(first);
        var tail=first.Take(count).Where(p=>p.randomSeed<100000).ToDictionary(p=>p.randomSeed,p=>p);
        Assert.That(tail.Count,Is.GreaterThan(30));
        Assert.That(tail.Values.Max(p=>p.position.y)-tail.Values.Min(p=>p.position.y),Is.GreaterThan(.5f));
        float body=Renderer("Wolf running spirit 0").transform.localPosition.x;
        stage.Tick(2.3f,0f);var later=new ParticleSystem.Particle[2400];int next=wake.GetParticles(later);
        var shared=later.Take(next).Where(p=>tail.ContainsKey(p.randomSeed)).ToArray();
        Assert.That(shared.Length,Is.GreaterThanOrEqualTo(Mathf.FloorToInt(tail.Count*.7f)),
            "Most existing pixels must persist through another .1 seconds of the .8-second tail lifetime.");
        Assert.That(Renderer("Wolf running spirit 0").transform.localPosition.x-body,Is.GreaterThan(.8f));
        foreach(var pixel in shared)
            Assert.That(Mathf.Abs(pixel.position.x-tail[pixel.randomSeed].position.x),Is.LessThan(.11f),"Old pixels cannot move along with the current body.");
        stage.Hide();Assert.That(wake.particleCount,Is.Zero);Assert.That(stage.Active,Is.False);
    }

    [Test]
    public void ThePackEntersInDifferentClustersRatherThanAppearingAsOneRectangularFormation()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Tick(.86f,0f);
        var bodies=host.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith("Wolf running spirit ")&&
            !r.name.Contains("bridge")&&!r.name.Contains("shadow")).ToArray();
        int early=bodies.Count(r=>r.enabled);Assert.That(early,Is.InRange(3,20));
        stage.Tick(1.46f,0f);Assert.That(bodies.Count(r=>r.enabled),Is.EqualTo(24));
        Assert.That(bodies.Select(r=>Mathf.RoundToInt(r.color.a*100f)).Distinct().Count(),Is.GreaterThan(10));
    }

    [Test]
    public void TheSideForegroundGallopCrossesTheLensAndDissolvesIntoTheLivePack()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Tick(2.7f,0f);
        var front=Renderer("Wolf lens crossing spirit");Assert.That(front.enabled,Is.False);
        stage.Tick(3.15f,0f);Assert.That(front.enabled,Is.True);Sprite frame=front.sprite;
        Assert.That(PixelWolfStampede.Frames.Contains(frame),Is.True);
        Camera view=View();Bounds bounds=VisibleBody(front.sprite);
        float low=view.WorldToViewportPoint(front.transform.TransformPoint(bounds.min)).y;
        float high=view.WorldToViewportPoint(front.transform.TransformPoint(bounds.max)).y;
        Assert.That(Mathf.Abs(high-low),Is.GreaterThan(1f));
        stage.Tick(3.42f,0f);var block=new MaterialPropertyBlock();front.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_Dissolve"),Is.GreaterThan(0f));Assert.That(front.sprite,Is.Not.SameAs(frame));
    }

    [Test]
    public void ContactAlphaTerrainAndParticlesCannotMakeThePrivateStageLeakTheLiveMap()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);Camera camera=View();
        var small=RenderTexture.GetTemporary(48,32,0,RenderTextureFormat.ARGB32);
        var readable=new Texture2D(48,32,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        try
        {
            foreach(float time in new[]{.12f,.42f,1.1f,1.65f,2.2f,3.15f,3.42f})
            {
                stage.Tick(time,0f);camera.Render();Graphics.Blit(stage.Texture,small);RenderTexture.active=small;
                readable.ReadPixels(new Rect(0f,0f,48f,32f),0,0);readable.Apply();
                foreach(Color32 pixel in readable.GetPixels32())Assert.That(pixel.a,Is.GreaterThanOrEqualTo(254));
            }
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(small);Object.DestroyImmediate(readable);}
    }

    [TestCase(30)] [TestCase(120)]
    public void SteppedAndRewoundClocksProduceTheSameGallopMarksCameraAndWakes(int fps)
    {
        Assert.That(stage.Begin(Source(),null),Is.True);float target=2.247f;
        for(int i=0;i/(float)fps<target;i++)stage.Tick(i/(float)fps,1f/fps);stage.Tick(target,0f);
        var wolf=Visible(Renderer("Wolf running spirit 0"));Sprite frame=wolf.sprite;Vector3 position=wolf.transform.localPosition;
        Vector3 cameraPosition=View().transform.localPosition;Quaternion cameraRotation=View().transform.localRotation;
        var pawMesh=Mesh("Wolf shaped paw impressions").sharedMesh;var pawVertices=pawMesh.vertices;var pawUV=pawMesh.uv;
        var system=host.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="Wolf historical pixel wakes and wind");
        var first=new ParticleSystem.Particle[2400];int count=system.GetParticles(first);
        stage.Tick(3.5f,0f);stage.Tick(target,0f);var second=new ParticleSystem.Particle[2400];int again=system.GetParticles(second);
        var same=Visible(Renderer("Wolf running spirit 0"));Assert.That(same.sprite,Is.SameAs(frame));Assert.That(same.transform.localPosition,Is.EqualTo(position));
        Assert.That(View().transform.localPosition,Is.EqualTo(cameraPosition));Assert.That(View().transform.localRotation,Is.EqualTo(cameraRotation));
        Assert.That(pawMesh.vertices,Is.EqualTo(pawVertices));Assert.That(pawMesh.uv,Is.EqualTo(pawUV));
        Assert.That(again,Is.EqualTo(count));for(int i=0;i<count;i++)Assert.That(second[i].position,Is.EqualTo(first[i].position));
    }

    [Test]
    public void TheSideTrackingCameraHasNoLargeVerticalJumpOrOneFrameSnap()
    {
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Tick(0f,0f);Vector3 previous=View().transform.localPosition;
        for(int i=1;i<=432;i++)
        {
            stage.Tick(i/120f,1f/120f);Vector3 current=View().transform.localPosition;
            Assert.That(Vector3.Distance(current,previous),Is.LessThan(.3f));Assert.That(current.y,Is.InRange(.84f,2.11f));
            previous=current;
        }
    }

    [Test]
    public void TextureAspectNullMapReuseAndDisposePreserveTheSharedSideAnimation()
    {
        var rt=(RenderTexture)stage.Texture;float screenAspect=Mathf.Max(1,Screen.width)/(float)Mathf.Max(1,Screen.height);
        Assert.That(rt.width/(float)rt.height,Is.EqualTo(screenAspect).Within(2f/rt.height));
        Assert.That(stage.Begin(new FrogCatchCinematicStage.Actor(),null),Is.False);
        Assert.That(stage.Begin(Source(),null),Is.True);Sprite shared=PixelWolfStampede.Frames[0];
        stage.Tick(float.NaN,0f);AssertFinite(View().transform.localPosition);stage.Hide();Assert.That(stage.Active,Is.False);
        Assert.That(stage.Begin(Source(),null),Is.True);stage.Dispose();stage.Dispose();
        Assert.That(rt==null||!rt.IsCreated(),Is.True);Assert.That(shared,Is.Not.Null);Assert.That(sprite,Is.Not.Null);
        Assert.That(stage.Begin(Source(),null),Is.False);
    }
    private FrogCatchCinematicStage.Actor Source()=>new FrogCatchCinematicStage.Actor
    {species="nukgu",sprite=sprite,frames=new[]{sprite},color=Color.white,size=1f};
    private SpriteRenderer Renderer(string name)=>host.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);
    private SpriteRenderer Visible(SpriteRenderer renderer)=>renderer.enabled?renderer:Renderer(renderer.name+" pose bridge");
    private MeshFilter Mesh(string name)=>host.GetComponentsInChildren<MeshFilter>(true).Single(r=>r.name==name);
    private Camera View()=>host.GetComponentsInChildren<Camera>(true).Single();
    private static Bounds VisibleBody(Sprite frame)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",
        System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{frame});
    private static void AssertFinite(Vector3 value)
    {Assert.That(float.IsNaN(value.sqrMagnitude)||float.IsInfinity(value.sqrMagnitude),Is.False);}
}
