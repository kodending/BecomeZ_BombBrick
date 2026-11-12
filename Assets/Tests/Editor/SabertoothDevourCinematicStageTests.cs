using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SabertoothDevourCinematicStageTests
{
    private GameObject host;
    private SabertoothDevourCinematicStage stage;
    private Material[] preexistingMaterials;
    private readonly ParticleSystem.Particle[] before=new ParticleSystem.Particle[512];
    private readonly ParticleSystem.Particle[] after=new ParticleSystem.Particle[512];

    [SetUp]
    public void SetUp()
    {
        host=new GameObject("Sabertooth presentation test parent");
        preexistingMaterials=Resources.FindObjectsOfTypeAll<Material>();
        stage=new SabertoothDevourCinematicStage(host.transform);
    }

    [TearDown]
    public void TearDown()
    {
        stage?.Dispose();
        if(host!=null)Object.DestroyImmediate(host);
    }

    [TestCase("geomchi","SabertoothStage_Geomchi_Seola_8Frames",true)]
    [TestCase("seola","SabertoothStage_Geomchi_Seola_8Frames",false)]
    [TestCase("nox","SabertoothStage_Nox_Terrakan_8Frames",true)]
    [TestCase("terakan","SabertoothStage_Nox_Terrakan_8Frames",false)]
    public void EverySpeciesUsesItsOwnEightFullSizeStagePosesInsteadOfEnlargingCombatSprites(
        string species,string atlasName,bool topHalf)
    {
        SabertoothDevourCinematicStage.Warmup();
        Assert.That(SabertoothDevourCinematicStage.HasAssets(species),Is.True);
        Texture2D atlas=Resources.Load<Texture2D>(SabertoothDevourCinematicStage.ResourceRoot+atlasName);
        Assert.That(atlas,Is.Not.Null);
        Assert.That(atlas.width,Is.GreaterThanOrEqualTo(1024));
        Assert.That(atlas.height,Is.GreaterThanOrEqualTo(1024));
        Sprite[] frames=SabertoothDevourCinematicStage.Frames(species);
        Assert.That(frames,Has.Length.EqualTo(8));
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);
        Assert.That(prefab,Is.Not.Null);
        Sprite original=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true).GetIdleFrame(0f);
        for(int i=0;i<frames.Length;i++)
        {
            Sprite frame=frames[i];Assert.That(frame,Is.Not.Null);
            Assert.That(frame.texture,Is.SameAs(atlas));
            Assert.That(frame.texture,Is.Not.SameAs(original.texture),"The cinematic needs its own drawn poses.");
            int column=i%4,row=(topHalf?3:1)-i/4;
            int left=Mathf.FloorToInt(column*atlas.width/4f),right=Mathf.FloorToInt((column+1)*atlas.width/4f);
            int bottom=Mathf.FloorToInt(row*atlas.height/4f),top=Mathf.FloorToInt((row+1)*atlas.height/4f);
            Assert.That(frame.rect,Is.EqualTo(Rect.MinMaxRect(left,bottom,right,top)));
            Assert.That(frame.rect.width,Is.GreaterThan(128));
            for(int j=0;j<i;j++)Assert.That(frame.rect.Overlaps(frames[j].rect),Is.False);
        }
        Assert.That(stage.Begin(species,false),Is.True);
        Assert.That(stage.Actor.sprite,Is.SameAs(frames[0]));
        Assert.That(SabertoothDevourCinematicStage.HasAssets("not_a_sabertooth"),Is.False);
    }

    [TestCase("geomchi")] [TestCase("seola")] [TestCase("nox")] [TestCase("terakan")]
    public void OnlyTheRealNoxShinyUsesItsOwnPaletteAndTheNextNormalCastRestoresIt(string species)
    {
        Shader shader=Resources.Load<Shader>(SabertoothDevourCinematicStage.ResourceRoot+"StagePalette");
        Assert.That(shader,Is.Not.Null);Assert.That(shader.isSupported,Is.True);
        Assert.That(stage.Begin(species,false),Is.True);
        Material palette=stage.Actor.sharedMaterial;Texture2D atlas=stage.Actor.sprite.texture;
        Assert.That(palette.shader,Is.SameAs(shader));
        Assert.That(palette.HasProperty("_ShinyNox"),Is.True);
        Assert.That(palette.GetFloat("_ShinyNox"),Is.Zero);
        Assert.That(stage.Begin(species,true,true),Is.True);
        Assert.That(stage.Actor.sharedMaterial,Is.SameAs(palette));
        Assert.That(stage.Actor.sprite.texture,Is.SameAs(atlas),"The palette must not replace or rewrite the shared atlas.");
        Assert.That(palette.GetFloat("_ShinyNox"),Is.EqualTo(species=="nox"?1f:0f));
        Assert.That(stage.Begin(species,false,false),Is.True);
        Assert.That(palette.GetFloat("_ShinyNox"),Is.Zero);
    }

    [Test]
    public void ExplicitTicksAnimateTheDrawnPosesAndMintParticlesWhileTheWorldClockIsStopped()
    {
        float speed=Time.timeScale;bool combatClock=CombatSkillClock.Active;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin("geomchi",false),Is.True);
            Sprite opening=stage.Actor.sprite;
            stage.Tick(2.55f,.02f);
            Assert.That(stage.Actor.sprite,Is.Not.SameAs(opening));
            Assert.That(stage.FrameIndex,Is.GreaterThan(0));
            Assert.That(stage.Devour,Is.Not.Null);
            int count=stage.Devour.Particles.GetParticles(before);
            Assert.That(count,Is.GreaterThan(16));
            stage.Tick(2.65f,.1f);
            int next=stage.Devour.Particles.GetParticles(after);
            Assert.That(next,Is.GreaterThan(16));
            int moved=0;
            for(int i=0;i<Mathf.Min(count,next);i++)
                if(Vector3.SqrMagnitude(after[i].position-before[i].position)>.000001f)moved++;
            Assert.That(moved,Is.GreaterThan(Mathf.Min(count,next)/2));
            stage.Devour.Particles.GetParticles(before);
            stage.Tick(2.65f,0f);
            Assert.That(stage.Devour.Particles.GetParticles(after),Is.EqualTo(next));
            for(int i=0;i<next;i++)
                Assert.That(after[i].position,Is.EqualTo(before[i].position),"A zero-delta redraw must be stable.");
            Assert.That(stage.Devour.Particles.isPaused,Is.True);
            Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.EqualTo(combatClock));
        }
        finally{Time.timeScale=speed;}
    }

    [TestCase(false)] [TestCase(true)]
    public void TheFinalOpenMouthIsHeldForTheReturnAndTheStageNeverPlaysTheCombatBite(bool left)
    {
        Assert.That(stage.Begin("geomchi",left),Is.True);
        for(int sample=0;sample<=40;sample++)
        {
            stage.Tick(Mathf.Min(3.99f,sample*.1f),.1f);
            if(stage.Devour==null)continue;
            Assert.That(stage.Devour.Age,Is.LessThan(SabertoothDevourRules.HitTime),
                "The cinematic must not consume the one real bite reserved for the battlefield.");
            Assert.That(stage.Devour.UpperJaw.sharedMaterial.GetFloat("_Flash"),Is.LessThan(.01f));
        }
        foreach(float time in new[]{3.25f,3.65f,3.99f})
        {
            stage.Tick(time,.02f);
            Assert.That(stage.FrameIndex,Is.EqualTo(7));
            Assert.That(stage.Actor.flipX,Is.EqualTo(left));
            Assert.That(stage.Actor.color.a,Is.GreaterThan(.1f));
            Assert.That(stage.Devour,Is.Not.Null);
            Assert.That(stage.Devour.UpperJaw.enabled,Is.True);
            Assert.That(stage.Devour.LowerJaw.enabled,Is.True);
            Assert.That(stage.JawBodyWidthRatio,Is.GreaterThan(0f));
            Assert.That(Quaternion.Angle(stage.Devour.UpperJaw.transform.rotation,
                stage.Devour.LowerJaw.transform.rotation),Is.GreaterThan(20f));
        }
    }

    [TestCase("geomchi")] [TestCase("seola")] [TestCase("nox")] [TestCase("terakan")]
    public void ReturnFramingReportsTheActualBodyBoundsAndKeepsTheBodyAndMouthInsideTheViewport(string species)
    {
        foreach(bool left in new[]{false,true})
        {
            Assert.That(stage.Begin(species,left),Is.True);
            foreach(float time in new[]{3.25f,3.65f,3.99f})
            {
                stage.Tick(time,.02f);
                Camera camera=StageCamera();
                Rect actual=BodyViewport(stage.Actor,camera),reported=stage.ActorViewportRect;
                Assert.That(reported.xMin,Is.EqualTo(actual.xMin).Within(.015f));
                Assert.That(reported.xMax,Is.EqualTo(actual.xMax).Within(.015f));
                Assert.That(reported.yMin,Is.EqualTo(actual.yMin).Within(.015f));
                Assert.That(reported.yMax,Is.EqualTo(actual.yMax).Within(.015f));
                Assert.That(actual.xMin,Is.GreaterThanOrEqualTo(0f));
                Assert.That(actual.yMin,Is.GreaterThanOrEqualTo(0f));
                Assert.That(actual.xMax,Is.LessThanOrEqualTo(1f));
                Assert.That(actual.yMax,Is.LessThanOrEqualTo(1f));
                Assert.That(actual.height,Is.GreaterThan(.18f),"The final drawn body must remain readable.");
                Assert.That(stage.MouthViewport.x,Is.InRange(0f,1f));
                Assert.That(stage.MouthViewport.y,Is.InRange(0f,1f));
                Vector3 mouth=camera.WorldToViewportPoint(stage.Devour.Mouth);
                Assert.That(stage.MouthViewport.x,Is.EqualTo(mouth.x).Within(.015f));
                Assert.That(stage.MouthViewport.y,Is.EqualTo(mouth.y).Within(.015f));
            }
        }
    }

    [Test]
    public void ThePresentationCameraAndParticlesCannotChangeTheMainCameraRenderTargetOrCombatActors()
    {
        var mainHost=new GameObject("Unrelated gameplay camera");mainHost.transform.SetParent(host.transform,false);
        var main=mainHost.AddComponent<Camera>();main.tag="MainCamera";main.enabled=false;
        main.cullingMask=63;main.fieldOfView=37f;main.lensShift=new Vector2(.12f,-.08f);
        main.transform.SetPositionAndRotation(new Vector3(4f,8f,-11f),Quaternion.Euler(37f,13f,4f));
        Vector3 position=main.transform.position;Quaternion rotation=main.transform.rotation;
        Matrix4x4 projection=main.projectionMatrix;
        var sentinel=new RenderTexture(16,16,0);sentinel.Create();
        RenderTexture previous=RenderTexture.active;float speed=Time.timeScale;
        bool combatClock=CombatSkillClock.Active;
        try
        {
            RenderTexture.active=sentinel;
            Assert.That(stage.Begin("nox",true),Is.True);stage.Tick(2.8f,.03f);
            Assert.That(RenderTexture.active,Is.SameAs(sentinel));
            Camera camera=StageCamera();
            Assert.That(camera,Is.Not.SameAs(main));
            Assert.That(camera.cullingMask,Is.EqualTo(1<<30));
            Assert.That(camera.targetTexture,Is.SameAs(stage.CameraTarget));
            Assert.That(stage.CameraTarget.IsCreated(),Is.True);
            Assert.That(stage.Texture,Is.InstanceOf<RenderTexture>());
            Assert.That(((RenderTexture)stage.Texture).IsCreated(),Is.True);
            GameObject root=stage.Actor.transform.parent.gameObject;
            Assert.That(root.GetComponentsInChildren<SpriteMonsterAnimationDriver>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<Animator>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<MonsterAutoBattlePrototype>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.gameObject.layer,Is.EqualTo(30));
            Assert.That(main.cullingMask,Is.EqualTo(63));Assert.That(main.fieldOfView,Is.EqualTo(37f));
            Assert.That(main.lensShift,Is.EqualTo(new Vector2(.12f,-.08f)));
            Assert.That(main.transform.position,Is.EqualTo(position));Assert.That(main.transform.rotation,Is.EqualTo(rotation));
            Assert.That(main.targetTexture,Is.Null);Assert.That(main.enabled,Is.False);
            for(int i=0;i<16;i++)Assert.That(main.projectionMatrix[i],Is.EqualTo(projection[i]));
            Assert.That(Time.timeScale,Is.EqualTo(speed));Assert.That(CombatSkillClock.Active,Is.EqualTo(combatClock));
            stage.Hide();Assert.That(RenderTexture.active,Is.SameAs(sentinel));
        }
        finally
        {
            RenderTexture.active=previous;sentinel.Release();Object.DestroyImmediate(sentinel);
        }
    }

    [Test]
    public void HideClearsTheActualParticlesAndTheNextCastReusesTheOwnedCameraAndTargets()
    {
        Assert.That(stage.Begin("geomchi",false),Is.True);stage.Tick(2.65f,.1f);
        ParticleSystem particles=stage.Devour.Particles;
        Assert.That(particles.particleCount,Is.GreaterThan(0));
        SpriteRenderer actor=stage.Actor;Camera camera=StageCamera();
        Texture composite=stage.Texture;RenderTexture target=stage.CameraTarget;
        stage.Hide();
        Assert.That(stage.Active,Is.False);Assert.That(actor.enabled,Is.False);Assert.That(camera.enabled,Is.False);
        Assert.That(particles==null||particles.particleCount==0,Is.True);
        stage.Tick(2.9f,.5f);
        Assert.That(particles==null||particles.particleCount==0,Is.True);
        Assert.That(stage.Begin("terakan",true),Is.True);stage.Tick(2.65f,.1f);
        Assert.That(stage.Active,Is.True);Assert.That(stage.Actor,Is.SameAs(actor));
        Assert.That(StageCamera(),Is.SameAs(camera));Assert.That(stage.Texture,Is.SameAs(composite));
        Assert.That(stage.CameraTarget,Is.SameAs(target));Assert.That(stage.Devour.Particles.particleCount,Is.GreaterThan(0));
        Assert.That(stage.Actor.sprite.texture,Is.SameAs(SabertoothDevourCinematicStage.Frames("terakan")[0].texture));
    }

    [Test]
    public void TheBackgroundMovesOnlyWithExplicitDeltaAtZeroWorldTimeAndEveryBeginReproducesTheSameImage()
    {
        float speed=Time.timeScale;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin("geomchi",false),Is.True);
            Color32[] initial=ReadBackgroundSamples(stage.BackgroundTexture);
            stage.Tick(2.65f,0f);
            Color32[] paused=ReadBackgroundSamples(stage.BackgroundTexture);
            stage.Tick(2.65f,.75f);
            Color32[] moving=ReadBackgroundSamples(stage.BackgroundTexture);
            int changed=0;
            for(int i=0;i<moving.Length;i++)if(!moving[i].Equals(paused[i]))changed++;
            Assert.That(changed,Is.GreaterThan(16),"The held stage pose still needs visible, independently flowing scenery.");
            stage.Tick(2.65f,0f);
            CollectionAssert.AreEqual(moving,ReadBackgroundSamples(stage.BackgroundTexture),
                "A zero-delta redraw must not advance parallax, mist, shafts or embers.");
            Assert.That(stage.Begin("geomchi",false),Is.True);
            CollectionAssert.AreEqual(initial,ReadBackgroundSamples(stage.BackgroundTexture));
            stage.Tick(2.65f,0f);
            CollectionAssert.AreEqual(paused,ReadBackgroundSamples(stage.BackgroundTexture));
            stage.Tick(2.65f,.75f);
            CollectionAssert.AreEqual(moving,ReadBackgroundSamples(stage.BackgroundTexture),
                "Restarting the same presentation must reset its private background clock and random pattern.");
            Assert.That(Time.timeScale,Is.Zero);
        }
        finally{Time.timeScale=speed;}
    }

    [Test]
    public void TheOpaqueBackgroundUsesItsOwnTargetAndPreservesTheGraphicsTargetAcrossRedrawsAndRestart()
    {
        var sentinel=new RenderTexture(16,16,0);sentinel.Create();
        RenderTexture previous=RenderTexture.active;
        try
        {
            RenderTexture.active=sentinel;
            Assert.That(stage.Begin("nox",true),Is.True);
            Shader shader=Resources.Load<Shader>(SabertoothDevourCinematicStage.ResourceRoot+"StageBackdrop");
            Assert.That(shader,Is.Not.Null);Assert.That(shader.isSupported,Is.True);
            Texture2D source=Resources.Load<Texture2D>(SabertoothDevourCinematicStage.ResourceRoot+"PredatorDomain");
            Assert.That(source,Is.Not.Null);
            Assert.That(stage.BackgroundTexture,Is.InstanceOf<RenderTexture>());
            var background=(RenderTexture)stage.BackgroundTexture;
            Assert.That(background.IsCreated(),Is.True);
            Assert.That(background,Is.Not.SameAs(stage.Texture));
            Assert.That(background,Is.Not.SameAs(stage.CameraTarget));
            Assert.That(background,Is.Not.SameAs(source));
            Assert.That(background.width,Is.GreaterThan(32));Assert.That(background.height,Is.GreaterThan(32));
            Assert.That(background.width/(float)background.height,
                Is.EqualTo(stage.CameraTarget.width/(float)stage.CameraTarget.height).Within(.01f));
            Assert.That(RenderTexture.active,Is.SameAs(sentinel));
            stage.Tick(1.8f,.3f);
            Color32[] samples=ReadBackgroundSamples(background);
            int varied=0;
            foreach(Color32 sample in samples)
            {
                Assert.That(sample.a,Is.GreaterThanOrEqualTo(250),"The separate scenery pass must fully cover the stage.");
                if(!sample.Equals(samples[0]))varied++;
            }
            Assert.That(varied,Is.GreaterThan(16),"A valid render target cannot substitute a flat clear color for the domain art.");
            Assert.That(RenderTexture.active,Is.SameAs(sentinel));
            stage.Hide();Assert.That(RenderTexture.active,Is.SameAs(sentinel));
            Assert.That(stage.Begin("seola",false),Is.True);
            Assert.That(stage.BackgroundTexture,Is.SameAs(background),"A new cast should reuse its prepared scenery target.");
            Assert.That(RenderTexture.active,Is.SameAs(sentinel));
            stage.Dispose();stage=null;
            Assert.That(RenderTexture.active,Is.SameAs(sentinel));
            Assert.That(background==null,Is.True);Assert.That(source!=null,Is.True);
        }
        finally
        {
            RenderTexture.active=previous;sentinel.Release();Object.DestroyImmediate(sentinel);
        }
    }

    [Test]
    public void DisposeReleasesOnlyOwnedRenderingObjectsAndLeavesSharedAtlasesAndFramesUsable()
    {
        Assert.That(stage.Begin("nox",false,true),Is.True);stage.Tick(2.8f,.02f);
        Texture composite=stage.Texture,background=stage.BackgroundTexture;RenderTexture target=stage.CameraTarget;
        Material palette=stage.Actor.sharedMaterial;
        Shader backdropShader=Resources.Load<Shader>(SabertoothDevourCinematicStage.ResourceRoot+"StageBackdrop");
        Texture2D sharedBackground=Resources.Load<Texture2D>(SabertoothDevourCinematicStage.ResourceRoot+"PredatorDomain");
        Material backdropMaterial=null;
        foreach(Material material in Resources.FindObjectsOfTypeAll<Material>())
            if(material.shader==backdropShader&&System.Array.IndexOf(preexistingMaterials,material)<0)
            {
                Assert.That(backdropMaterial,Is.Null,"Each stage should own one backdrop material.");
                backdropMaterial=material;
            }
        Assert.That(backdropMaterial,Is.Not.Null);
        GameObject root=stage.Actor.transform.parent.gameObject;
        Camera camera=StageCamera();ParticleSystem particles=stage.Devour.Particles;
        Sprite sharedFrame=SabertoothDevourCinematicStage.Frames("nox")[7];
        Texture2D atlas=sharedFrame.texture;
        stage.Dispose();stage=null;
        Assert.That(composite==null,Is.True);Assert.That(target==null,Is.True);Assert.That(background==null,Is.True);
        Assert.That(backdropMaterial==null,Is.True);
        Assert.That(palette==null,Is.True);Assert.That(root==null,Is.True);
        Assert.That(camera==null,Is.True);Assert.That(particles==null,Is.True);
        Assert.That(sharedFrame!=null,Is.True);Assert.That(atlas!=null,Is.True);
        Assert.That(sharedBackground!=null,Is.True);Assert.That(backdropShader!=null,Is.True);
        Assert.That(Resources.Load<Texture2D>(SabertoothDevourCinematicStage.ResourceRoot+"PredatorDomain"),
            Is.SameAs(sharedBackground));
        Assert.That(SabertoothDevourCinematicStage.Frames("nox")[7],Is.SameAs(sharedFrame));
        Assert.That(host!=null,Is.True,"The game-owned parent must survive disposal.");
    }

    private Camera StageCamera()=>stage.Actor.transform.parent.GetComponentInChildren<Camera>(true);

    private static Color32[] ReadBackgroundSamples(Texture source)
    {
        Assert.That(source,Is.Not.Null);
        var sample=RenderTexture.GetTemporary(32,32,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        RenderTexture previous=RenderTexture.active;Texture2D readable=null;
        try
        {
            Graphics.Blit(source,sample);RenderTexture.active=sample;
            readable=new Texture2D(32,32,TextureFormat.RGBA32,false,true);
            readable.ReadPixels(new Rect(0f,0f,32f,32f),0,0);readable.Apply(false,false);
            return readable.GetPixels32();
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(sample);
            if(readable!=null)Object.DestroyImmediate(readable);
        }
    }

    private static Rect BodyViewport(SpriteRenderer actor,Camera camera)
    {
        var bounds=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{actor.sprite});
        Vector2 minimum=Vector2.one*float.PositiveInfinity,maximum=Vector2.one*float.NegativeInfinity;
        for(int i=0;i<4;i++)
        {
            Vector3 local=new Vector3(i%2==0?bounds.min.x:bounds.max.x,i<2?bounds.min.y:bounds.max.y,0f);
            if(actor.flipX)local.x=-local.x;
            Vector3 point=camera.WorldToViewportPoint(actor.transform.TransformPoint(local));
            minimum=Vector2.Min(minimum,point);maximum=Vector2.Max(maximum,point);
        }
        return Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
    }
}
