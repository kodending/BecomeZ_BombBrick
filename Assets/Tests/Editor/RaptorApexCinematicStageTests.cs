using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class RaptorApexCinematicStageTests
{
    private GameObject host;
    private RaptorApexCinematicStage stage;
    private readonly ParticleSystem.Particle[] before=new ParticleSystem.Particle[PixelRaptorApexWings.ParticleCapacity];
    private readonly ParticleSystem.Particle[] after=new ParticleSystem.Particle[PixelRaptorApexWings.ParticleCapacity];

    [SetUp]
    public void SetUp()
    {
        host=new GameObject("Raptor cinematic stage test parent");
        stage=new RaptorApexCinematicStage(host.transform);
    }

    [TearDown]
    public void TearDown()
    {
        if(stage!=null)stage.Dispose();
        if(host!=null)Object.DestroyImmediate(host);
    }

    [TestCase("velos")]
    [TestCase("velok")]
    [TestCase("raputo")]
    [TestCase("wilo")]
    public void EachSpeciesUsesTwelveRetouchedPosesItsOwnEyeAndOnlyTheAvailableShinyPalette(string species)
    {
        string prefix=char.ToUpperInvariant(species[0])+species.Substring(1);
        Sprite[] frames=RaptorApexCinematicStage.Frames(species);
        Assert.That(frames,Has.Length.EqualTo(12),species);
        Texture2D atlas=Resources.Load<Texture2D>(RaptorApexCinematicStage.ResourceRoot+prefix+"Awakening_12Frames");
        Texture2D eye=RaptorApexCinematicStage.Portrait(species);
        Assert.That(atlas,Is.Not.Null);Assert.That(eye,Is.Not.Null);
        Assert.That(eye.name,Is.EqualTo(prefix+"PredatorEye"));
        Assert.That(eye.width,Is.GreaterThanOrEqualTo(512));Assert.That(eye.height,Is.GreaterThanOrEqualTo(512));
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.Get(species);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);
        Sprite original=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true).GetIdleFrame(0f);
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].texture,Is.SameAs(atlas));
            Assert.That(frames[i].texture,Is.Not.SameAs(original.texture),"The movie must not enlarge a combat sprite sheet.");
            Assert.That(frames[i].rect.width,Is.GreaterThan(128));
            Assert.That(frames[i].rect.width,Is.EqualTo(atlas.width/4f));
            Assert.That(frames[i].rect.height,Is.EqualTo(atlas.height/3f));
            Assert.That(frames[i].name,Is.EqualTo(prefix+" Retouched Awakening "+i));
        }
        Assert.That(stage.Begin(species,false),Is.True);
        Assert.That(stage.EyeTexture,Is.SameAs(eye));
        Assert.That(stage.Actor.sharedMaterial.GetFloat("_HueShift"),Is.Zero);
        Assert.That(stage.Begin(species,true,true),Is.True);
        Assert.That(RenderTexture.active,Is.Null.Or.Not.SameAs(stage.EyeTexture),
            "Preparing the shiny portrait must release its render target before scene rendering resumes.");
        float hue=stage.Actor.sharedMaterial.GetFloat("_HueShift");
        if(species=="velos")
        {
            Assert.That(hue,Is.GreaterThan(0f));
            Assert.That(stage.EyeTexture,Is.InstanceOf<RenderTexture>());
            Assert.That(((RenderTexture)stage.EyeTexture).IsCreated(),Is.True);
            Assert.That(stage.EyeTexture.width,Is.EqualTo(eye.width));
        }
        else
        {
            Assert.That(hue,Is.Zero,"Only Velos currently has a real shiny variant in the game catalog.");
            Assert.That(stage.EyeTexture,Is.SameAs(eye));
        }
        Assert.That(stage.Begin(species,false,false),Is.True);
        Assert.That(stage.Actor.sharedMaterial.GetFloat("_HueShift"),Is.Zero);
        Assert.That(stage.EyeTexture,Is.SameAs(eye));
    }

    [Test]
    public void ZeroTimeScaleStillPlaysRetouchedRoarPosesAndActualWingParticlesOnlyThroughExplicitTicks()
    {
        float speed=Time.timeScale;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin("velos",false),Is.True);
            stage.Tick(3.05f,.02f);Assert.That(stage.FrameIndex,Is.EqualTo(4));
            Sprite initial=stage.Actor.sprite;
            stage.Tick(3.62f,.02f);Assert.That(stage.FrameIndex,Is.EqualTo(6));
            Assert.That(stage.Actor.sprite,Is.Not.SameAs(initial));
            Assert.That(stage.Wings.IsVisible,Is.True);
            Assert.That(stage.Wings.WingParticles.GetParticles(before),Is.EqualTo(before.Length));
            stage.Tick(3.62f,.07f);stage.Wings.WingParticles.GetParticles(after);
            Assert.That(Vector3.Distance(before[17].position,after[17].position),Is.GreaterThan(.0001f),
                "Light must flow even while the drawn roar pose and world clock are held.");
            stage.Wings.WingParticles.GetParticles(before);
            stage.Tick(3.62f,0f);stage.Wings.WingParticles.GetParticles(after);
            for(int i=0;i<before.Length;i++)
            {
                Assert.That(after[i].position,Is.EqualTo(before[i].position));
                Assert.That(after[i].remainingLifetime,Is.EqualTo(before[i].remainingLifetime));
            }
            Assert.That(stage.Wings.WingParticles.isPaused,Is.True);
            Assert.That(Time.timeScale,Is.Zero);
        }
        finally{Time.timeScale=speed;}
    }

    [Test]
    public void StageHasNoCombatActorAndItsLayerThirtyCameraCannotChangeTheMainCamera()
    {
        var view=new GameObject("Unrelated main gameplay camera");view.transform.SetParent(host.transform,false);
        Camera main=view.AddComponent<Camera>();main.enabled=false;main.cullingMask=63;
        main.fieldOfView=37f;main.lensShift=new Vector2(.12f,-.08f);
        main.transform.SetPositionAndRotation(new Vector3(4,8,-11),Quaternion.Euler(30,5,0));
        Matrix4x4 projection=main.projectionMatrix;
        Vector3 position=main.transform.position;Quaternion rotation=main.transform.rotation;
        Assert.That(stage.Begin("raputo",true),Is.True);stage.Tick(3.2f,.02f);
        Camera presentation=stage.Actor.transform.parent.GetComponentInChildren<Camera>(true);
        Assert.That(presentation,Is.Not.SameAs(main));
        Assert.That(presentation.cullingMask,Is.EqualTo(1<<30));
        Assert.That(presentation.targetTexture,Is.SameAs(stage.CameraTarget));
        Assert.That(((RenderTexture)stage.Texture).IsCreated(),Is.True);
        Assert.That(stage.Actor.GetComponent<SpriteMonsterAnimationDriver>(),Is.Null);
        Assert.That(stage.Actor.GetComponent<Animator>(),Is.Null);
        MonoBehaviour[] behaviours=stage.Actor.GetComponents<MonoBehaviour>();
        Assert.That(behaviours,Has.Length.EqualTo(1));Assert.That(behaviours[0],Is.SameAs(stage.Wings));
        foreach(Renderer renderer in stage.Actor.GetComponentsInChildren<Renderer>(true))
            Assert.That(renderer.gameObject.layer,Is.EqualTo(30));
        Assert.That(main.cullingMask,Is.EqualTo(63));Assert.That(main.fieldOfView,Is.EqualTo(37f));
        Assert.That(main.lensShift,Is.EqualTo(new Vector2(.12f,-.08f)));
        Assert.That(main.transform.position,Is.EqualTo(position));Assert.That(main.transform.rotation,Is.EqualTo(rotation));
        Assert.That(main.targetTexture,Is.Null);
        for(int i=0;i<16;i++)Assert.That(main.projectionMatrix[i],Is.EqualTo(projection[i]));
    }

    [Test]
    public void LoadingWarmupRendersSilentlyAndLeavesNoActiveStageOrParticles()
    {
        float speed=Time.timeScale;
        var warmup=stage.WarmupRender();
        Assert.That(warmup.MoveNext(),Is.True,"URP must receive a normal loading frame to render the stage.");
        Assert.That(stage.Active,Is.True);Assert.That(stage.EyeTexture,Is.InstanceOf<RenderTexture>());
        foreach(var voice in stage.Actor.transform.parent.GetComponentsInChildren<AudioSource>(true))
            Assert.That(voice.isPlaying,Is.False);
        Assert.That(warmup.MoveNext(),Is.False);
        Assert.That(stage.Active,Is.False);Assert.That(stage.Wings.WingParticles.particleCount,Is.Zero);
        Assert.That(((RenderTexture)stage.Texture).IsCreated(),Is.True);
        foreach(var voice in stage.Actor.transform.parent.GetComponentsInChildren<AudioSource>(true))
            Assert.That(voice.isPlaying,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(speed));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(stage.Begin("velok",true),Is.True,"The next real cast must reuse the prepared stage.");
    }

    [TestCase("velos")] [TestCase("velok")] [TestCase("raputo")] [TestCase("wilo")]
    public void FinalPounceKeepsTheWholeBodyInsideTheLetterboxAndRemainsVisibleThroughTheReturn(string species)
    {
        Assert.That(stage.Begin(species,true),Is.True);
        Camera view=stage.Actor.transform.parent.GetComponentInChildren<Camera>(true);
        foreach(float time in new[]{5.26f,5.4f,5.5f})
        {
            stage.Tick(time,.02f);
            Bounds b=VisibleBody(stage.Actor.sprite);
            Vector3 bottom=view.WorldToViewportPoint(stage.Actor.transform.TransformPoint(b.min));
            Vector3 top=view.WorldToViewportPoint(stage.Actor.transform.TransformPoint(b.max));
            Assert.That(bottom.y,Is.GreaterThan(.09f),"Feet at "+time);
            Assert.That(top.y,Is.LessThan(.91f),"Head at "+time);
            Assert.That(stage.Actor.color.a,Is.GreaterThan(0f),"The forest must not be left empty before combat fades in.");
        }
    }

    [Test]
    public void EyeToStagePullbackUsesTheWholeActorAndFramesItsBodyBeforeTheRoar()
    {
        Assert.That(stage.Begin("velos",false),Is.True);
        Camera view=stage.Actor.transform.parent.GetComponentInChildren<Camera>(true);
        stage.Tick(1.95f,0f);
        float close=view.orthographicSize;
        Bounds b=VisibleBody(stage.Actor.sprite);
        Vector3 head=b.center+new Vector3(b.size.x*.24f,b.size.y*.25f,0f);
        Vector3 focus=view.WorldToViewportPoint(stage.Actor.transform.TransformPoint(head));
        Assert.That(focus.x,Is.EqualTo(.5f).Within(.01f));
        Assert.That(focus.y,Is.EqualTo(.5f).Within(.01f));
        stage.Tick(2.85f,.02f);
        Assert.That(view.orthographicSize,Is.GreaterThan(close*3f));
        b=VisibleBody(stage.Actor.sprite);
        Vector3 bottom=view.WorldToViewportPoint(stage.Actor.transform.TransformPoint(b.min));
        Vector3 top=view.WorldToViewportPoint(stage.Actor.transform.TransformPoint(b.max));
        Assert.That(bottom.y,Is.GreaterThan(.09f));Assert.That(top.y,Is.LessThan(.91f));
    }

    [Test]
    public void HideStopsRenderingAndActualParticlesAndTheNextPresentationReusesThem()
    {
        Assert.That(stage.Begin("velos",false),Is.True);stage.Tick(3.62f,.02f);
        ParticleSystem retained=stage.Wings.WingParticles;
        Assert.That(retained.particleCount,Is.GreaterThan(0));
        stage.Hide();Assert.That(stage.Active,Is.False);Assert.That(stage.Actor.enabled,Is.False);
        Assert.That(stage.Wings.IsVisible,Is.False);Assert.That(retained.particleCount,Is.Zero);
        stage.Tick(4f,.5f);Assert.That(retained.particleCount,Is.Zero);
        Assert.That(stage.Begin("wilo",true),Is.True);stage.Tick(3.62f,.02f);
        Assert.That(stage.Active,Is.True);Assert.That(stage.Wings.WingParticles,Is.SameAs(retained));
        Assert.That(stage.Wings.IsVisible,Is.True);Assert.That(retained.particleCount,Is.GreaterThan(0));
    }

    [Test]
    public void DisposeDestroysOnlyTheOwnedStageRenderTargetsPaletteAndPresentationObjects()
    {
        Assert.That(stage.Begin("velos",false,true),Is.True);stage.Tick(3.62f,.02f);
        var target=(RenderTexture)stage.Texture;var shinyEye=(RenderTexture)stage.EyeTexture;
        Material palette=stage.Actor.sharedMaterial;
        GameObject stageRoot=stage.Actor.transform.parent.gameObject;
        Camera stageCamera=stageRoot.GetComponentInChildren<Camera>(true);
        ParticleSystem particles=stage.Wings.WingParticles;
        Texture2D sharedEye=RaptorApexCinematicStage.Portrait("velos");
        Texture2D sharedAtlas=RaptorApexCinematicStage.Frames("velos")[0].texture;
        stage.Dispose();stage=null;
        Assert.That(target==null,Is.True);Assert.That(shinyEye==null,Is.True);Assert.That(palette==null,Is.True);
        Assert.That(stageRoot==null,Is.True);Assert.That(stageCamera==null,Is.True);Assert.That(particles==null,Is.True);
        Assert.That(sharedEye!=null,Is.True);Assert.That(sharedAtlas!=null,Is.True);
        Assert.That(host!=null,Is.True,"The game-owned parent must survive presentation disposal.");
    }

    private static Bounds VisibleBody(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper)
        .GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{sprite});
}
