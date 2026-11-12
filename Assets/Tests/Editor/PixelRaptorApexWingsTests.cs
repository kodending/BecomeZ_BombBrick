using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=NUnit.Framework.Is;
using Object=UnityEngine.Object;

public sealed class PixelRaptorApexWingsTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host,referenceHost;
    private SpriteRenderer source;
    private PixelRaptorApexWings effect;
    private Sprite sprite;
    private Texture2D texture;
    private Dictionary<Sprite,Bounds> bodyCache;
    private readonly ParticleSystem.Particle[] before=new ParticleSystem.Particle[PixelRaptorApexWings.ParticleCapacity];
    private readonly ParticleSystem.Particle[] after=new ParticleSystem.Particle[PixelRaptorApexWings.ParticleCapacity];
    private readonly Vector3[] beforeWorld=new Vector3[PixelRaptorApexWings.ParticleCapacity];
    private readonly Vector3[] afterWorld=new Vector3[PixelRaptorApexWings.ParticleCapacity];
    private static readonly Bounds Body=new Bounds(new Vector3(.2f,.35f,0f),new Vector3(1f,.8f,0f));

    [SetUp]
    public void SetUp()
    {
        host=new GameObject("Apex wing owner");source=host.AddComponent<SpriteRenderer>();
        texture=new Texture2D(16,16,TextureFormat.RGBA32,false);
        texture.SetPixels32(new Color32[16*16]);texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,16,16),Vector2.one*.5f,16f);source.sprite=sprite;
        bodyCache=(Dictionary<Sprite,Bounds>)typeof(PixelMantisReaper)
            .GetField("visibleBounds",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        bodyCache[sprite]=Body;effect=PixelRaptorApexWings.Attach(source);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        if(referenceHost!=null)Object.DestroyImmediate(referenceHost);
        bodyCache.Remove(sprite);
        Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
    }

    [Test]
    public void WingsContainOnlySquareGoldenPixelsWithoutAFlatWingOrContinuousLines()
    {
        Assert.That(Resources.Load<Shader>("CombatCinematics/RaptorApex/ApexAuraParticle"),Is.Not.Null);
        effect.SetAwakened(true);effect.Tick(1f);
        Assert.That(effect.IsVisible,Is.True);
        Assert.That(effect.WingParticles.particleCount,Is.EqualTo(PixelRaptorApexWings.ParticleCapacity));
        Assert.That(effect.WingParticles.isPaused,Is.True,"Only the explicit Tick may advance these particles.");
        Assert.That(effect.AuraRoot.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
        Assert.That(effect.WingParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name,
            Is.EqualTo("BecomeZ/Effects/Raptor Apex Golden Particle"));
        Assert.That(effect.AuraRoot.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty);
        Assert.That(effect.WingParticles.GetComponent<ParticleSystemRenderer>().alignment,Is.EqualTo(ParticleSystemRenderSpace.View));
        Assert.That(effect.WingParticles.GetParticles(before),Is.EqualTo(before.Length));
        foreach(ParticleSystem.Particle pixel in before)
        {
            Assert.That(pixel.startSize3D.x,Is.EqualTo(pixel.startSize3D.y));
            Assert.That(pixel.startSize3D.y,Is.EqualTo(pixel.startSize3D.z));
            Assert.That(pixel.rotation3D,Is.EqualTo(Vector3.zero),"Pixels must keep crisp square axes instead of becoming long rotating capsules.");
        }
    }

    [Test]
    public void GoldenWingsNeverEnterTheBodyTeamOutlinePass()
    {
        int outline=LayerMask.NameToLayer("ScreenSpaceOutline");
        Assert.That(outline,Is.GreaterThanOrEqualTo(0));
        source.gameObject.layer=outline;effect.SetAwakened(true);effect.Tick(1f);
        Assert.That(source.gameObject.layer,Is.EqualTo(outline),"Keep the body's team outline.");
        Assert.That(effect.AuraRoot.gameObject.layer,Is.Zero);
        foreach(Renderer renderer in effect.AuraRoot.GetComponentsInChildren<Renderer>())
            Assert.That(renderer.gameObject.layer,Is.Zero,"The outline override would replace gold with team cyan/red.");
        source.gameObject.layer=30;effect.Tick(.02f);
        foreach(Renderer renderer in effect.AuraRoot.GetComponentsInChildren<Renderer>())
            Assert.That(renderer.gameObject.layer,Is.EqualTo(30),"The isolated cinematic must keep its own camera layer.");
    }

    [Test]
    public void ActualRenderingProducesGoldPixelsWithTransparentCoverageAndClearRemovesThem()
    {
        GameObject view=null;RenderTexture target=null;Texture2D readback=null;
        RenderTexture previous=RenderTexture.active;
        try
        {
            host.layer=29;
            view=new GameObject("Isolated real wing pixel test camera");
            Camera camera=view.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=1.2f;
            camera.transform.position=new Vector3(0f,.8f,-10f);camera.cullingMask=1<<29;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.allowHDR=false;camera.allowMSAA=false;
            target=new RenderTexture(256,256,16,RenderTextureFormat.ARGB32);target.Create();
            camera.targetTexture=target;readback=new Texture2D(256,256,TextureFormat.RGBA32,false);
            effect.SetAwakened(true);effect.SetReveal(1f);effect.Tick(.12f);camera.Render();
            RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,256,256),0,0,false);readback.Apply();
            Assert.That(GoldCoveragePixels(readback.GetPixels32()),Is.GreaterThan(350),
                "A particle count alone is insufficient: the camera must actually render a substantial golden wing.");
            effect.Clear();camera.Render();
            RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,256,256),0,0,false);readback.Apply();
            Assert.That(GoldCoveragePixels(readback.GetPixels32()),Is.Zero);
        }
        finally
        {
            RenderTexture.active=previous;
            if(view!=null)Object.DestroyImmediate(view);
            if(target!=null){target.Release();Object.DestroyImmediate(target);}
            if(readback!=null)Object.DestroyImmediate(readback);
        }
    }

    private static int GoldCoveragePixels(Color32[] pixels)
    {
        int count=0;
        foreach(Color32 pixel in pixels)
            if(pixel.a>=20&&pixel.r>=pixel.g+12&&pixel.g>=pixel.b+16)count++;
        return count;
    }

    [Test]
    public void ReapplyingTheBuffReusesTheSameParticleSystemAndDoesNotRestartTheReveal()
    {
        Assert.That(PixelRaptorApexWings.Attach(source),Is.SameAs(effect));
        ParticleSystem retained=effect.WingParticles;
        effect.SetAwakened(true);effect.Tick(PixelRaptorApexWings.RevealDuration*.5f);
        Assert.That(effect.Reveal,Is.EqualTo(.5f).Within(.001f));
        effect.SetAwakened(true);Assert.That(effect.Reveal,Is.EqualTo(.5f).Within(.001f));
        effect.Tick(PixelRaptorApexWings.RevealDuration*.5f);Assert.That(effect.Reveal,Is.EqualTo(1f));
        Assert.That(effect.WingParticles,Is.SameAs(retained));
        Assert.That(host.GetComponentsInChildren<ParticleSystem>(true),Has.Length.EqualTo(1));
        Assert.That(host.GetComponents<PixelRaptorApexWings>(),Has.Length.EqualTo(1));
    }

    [Test]
    public void BuffExpiryFadesTheWholeFlowButDeathAndWaveClearImmediatelyAndReuseIt()
    {
        effect.SetAwakened(true);effect.Tick(1f);float alpha=effect.Opacity;
        effect.SetAwakened(false);effect.Tick(.15f);
        Assert.That(effect.IsVisible,Is.True);
        Assert.That(effect.Opacity,Is.GreaterThan(0f).And.LessThan(alpha));
        effect.Tick(.26f);Assert.That(effect.IsVisible,Is.False);
        Assert.That(effect.WingParticles.particleCount,Is.Zero);Assert.That(effect.Reveal,Is.Zero);
        ParticleSystem retained=effect.WingParticles;
        effect.SetAwakened(true);effect.Tick(.25f);effect.Clear();
        Assert.That(effect.IsVisible,Is.False);Assert.That(effect.IsAwakened,Is.False);
        Assert.That(effect.WingParticles.particleCount,Is.Zero);
        effect.SetAwakened(true);effect.Tick(.25f);
        Assert.That(effect.WingParticles,Is.SameAs(retained));Assert.That(effect.IsVisible,Is.True);
    }

    [Test]
    public void ExplicitStageTicksMoveTheLightAtZeroTimeScaleAndZeroDeltaPreservesEveryParticle()
    {
        float speed=Time.timeScale;
        try
        {
            Time.timeScale=0f;effect.SetAwakened(true);effect.SetReveal(1f);
            effect.WingParticles.GetParticles(before);
            effect.Tick(.1f);effect.WingParticles.GetParticles(after);
            Assert.That(Vector3.Distance(before[17].position,after[17].position),Is.GreaterThan(.01f));
            effect.WingParticles.GetParticles(before);effect.Tick(0f);effect.WingParticles.GetParticles(after);
            for(int i=0;i<before.Length;i++)
            {
                Assert.That(after[i].position,Is.EqualTo(before[i].position));
                Assert.That(after[i].remainingLifetime,Is.EqualTo(before[i].remainingLifetime));
            }
            Assert.That(effect.WingParticles.isPaused,Is.True);
            Assert.That(effect.AuraRoot.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty);
        }
        finally{Time.timeScale=speed;}
    }

    [Test]
    public void WorldParticlesTrailMovementReverseTheirLagOnATurnAndCatchUpAfterTheHistoryWindow()
    {
        PixelRaptorApexWings reference=CreateReference();
        effect.SetAwakened(true);effect.SetReveal(1f);
        for(int i=0;i<30;i++)
        {host.transform.position+=Vector3.right*.025f;effect.Tick(.02f);reference.Tick(.02f);}
        float rightLag=host.transform.position.x-MeanWorldOffset(reference);
        Assert.That(rightLag,Is.GreaterThan(.05f),"The drawn pixels must visibly remain behind the current owner, rather than moving as a rigid attached image.");
        for(int i=0;i<10;i++)
        {host.transform.position-=Vector3.right*.025f;effect.Tick(.02f);reference.Tick(.02f);}
        float leftLag=host.transform.position.x-MeanWorldOffset(reference);
        Assert.That(leftLag,Is.LessThan(-.02f),"After turning left, older pixels must remain on the right until their follow delay expires.");
        int settleSteps=Mathf.CeilToInt((PixelRaptorApexWings.MaximumFollowDelay+.1f)/.02f);
        for(int i=0;i<settleSteps;i++){effect.Tick(.02f);reference.Tick(.02f);}
        Assert.That(MeanWorldOffset(reference),Is.EqualTo(host.transform.position.x).Within(.002f),
            "Once the historical poses expire, a stationary owner's wing must settle onto its current back.");
    }

    [Test]
    public void LateFrameMovementDoesNotDragHistoricalPixelsForwardOrAdvanceTheirLife()
    {
        effect.SetAwakened(true);effect.SetReveal(1f);
        for(int i=0;i<30;i++)effect.Tick(.02f);
        CaptureWorld(effect.WingParticles,before,beforeWorld);
        host.transform.position+=Vector3.right*.3f;
        typeof(PixelRaptorApexWings).GetMethod("LateUpdate",Private).Invoke(effect,null);
        CaptureWorld(effect.WingParticles,after,afterWorld);
        float moved=0f;
        for(int i=0;i<before.Length;i++)
        {
            moved+=Vector3.Distance(beforeWorld[i],afterWorld[i]);
            Assert.That(after[i].remainingLifetime,Is.EqualTo(before[i].remainingLifetime));
        }
        Assert.That(moved/before.Length,Is.LessThan(.03f),
            "Moving after the combat Tick must update the root without pulling every old particle along with it.");
    }

    [Test]
    public void ClearingAndReawakeningAfterATeleportCannotReplayThePreviousWingHistory()
    {
        PixelRaptorApexWings reference=CreateReference();
        effect.SetAwakened(true);effect.SetReveal(1f);
        for(int i=0;i<30;i++)
        {host.transform.position+=Vector3.right*.025f;effect.Tick(.02f);reference.Tick(.02f);}
        effect.Clear();reference.Clear();host.transform.position+=Vector3.right*5f;
        effect.SetAwakened(true);effect.SetReveal(1f);
        reference.SetAwakened(true);reference.SetReveal(1f);
        effect.Tick(.02f);reference.Tick(.02f);
        Assert.That(MeanWorldOffset(reference),Is.EqualTo(host.transform.position.x).Within(.002f),
            "A new activation must start at its new owner position, not show pixels left in the previous battle or cast.");
    }

    private PixelRaptorApexWings CreateReference()
    {
        referenceHost=new GameObject("Stationary wing flow reference");
        var renderer=referenceHost.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        var reference=PixelRaptorApexWings.Attach(renderer);
        reference.SetAwakened(true);reference.SetReveal(1f);return reference;
    }

    private float MeanWorldOffset(PixelRaptorApexWings reference)
    {
        CaptureWorld(effect.WingParticles,before,beforeWorld);
        CaptureWorld(reference.WingParticles,after,afterWorld);
        float sum=0f;
        for(int i=0;i<before.Length;i++)sum+=beforeWorld[i].x-afterWorld[i].x;
        return sum/before.Length;
    }

    private static void CaptureWorld(ParticleSystem system,ParticleSystem.Particle[] buffer,Vector3[] world)
    {
        Assert.That(system.GetParticles(buffer),Is.EqualTo(buffer.Length));
        bool local=system.main.simulationSpace==ParticleSystemSimulationSpace.Local;
        Matrix4x4 matrix=system.transform.localToWorldMatrix;
        for(int i=0;i<buffer.Length;i++)world[i]=local?matrix.MultiplyPoint3x4(buffer[i].position):buffer[i].position;
    }

    [TestCase(false,false)]
    [TestCase(true,false)]
    [TestCase(false,true)]
    [TestCase(true,true)]
    public void BackRootTracksBillboardFacingScaleAndEveryStageLayerWithoutChangingTheBody(bool flipX,bool flipY)
    {
        source.flipX=flipX;source.flipY=flipY;source.sortingOrder=23;host.layer=30;
        source.color=new Color(.6f,.7f,.9f,.8f);
        host.transform.SetPositionAndRotation(new Vector3(2,3,4),Quaternion.Euler(35,8,12));
        host.transform.localScale=new Vector3(-2f,3f,1f);effect.SetAwakened(true);effect.Tick(1f);
        Vector3 back=Body.center+new Vector3(Body.size.x*.04f,Body.size.y*.13f,0f);
        if(flipX)back.x=-back.x;if(flipY)back.y=-back.y;
        Assert.That(Vector3.Distance(effect.AuraRoot.position,host.transform.TransformPoint(back)),Is.LessThan(.00001f));
        Assert.That(Quaternion.Angle(effect.AuraRoot.rotation,host.transform.rotation),Is.LessThan(.001f));
        Assert.That(Mathf.Sign(effect.AuraRoot.localScale.x),Is.EqualTo(flipX?-1f:1f));
        Assert.That(Mathf.Sign(effect.AuraRoot.localScale.y),Is.EqualTo(flipY?-1f:1f));
        Assert.That(effect.Opacity,Is.EqualTo(.8f));
        foreach(Renderer renderer in effect.AuraRoot.GetComponentsInChildren<Renderer>())
        {Assert.That(renderer.sortingOrder,Is.EqualTo(22));Assert.That(renderer.gameObject.layer,Is.EqualTo(30));}
        effect.SetForeground(true);
        foreach(Renderer renderer in effect.AuraRoot.GetComponentsInChildren<Renderer>())
            Assert.That(renderer.sortingOrder,Is.EqualTo(30000));
        effect.SetForeground(false);
        Assert.That(source.sprite,Is.SameAs(sprite));Assert.That(source.color,Is.EqualTo(new Color(.6f,.7f,.9f,.8f)));
    }

    [Test]
    public void HiddenOrDisabledOwnersCannotLeaveFloatingWingsOrParticlesBehind()
    {
        effect.SetAwakened(true);effect.Tick(1f);
        source.forceRenderingOff=true;effect.Tick(0f);Assert.That(effect.IsVisible,Is.False);
        Assert.That(effect.WingParticles.particleCount,Is.Zero);
        source.forceRenderingOff=false;effect.Tick(0f);Assert.That(effect.IsVisible,Is.True);
        source.enabled=false;effect.Tick(0f);Assert.That(effect.IsVisible,Is.False);
        source.enabled=true;host.SetActive(false);effect.Tick(0f);Assert.That(effect.IsVisible,Is.False);
        // EditMode fixtures do not receive MonoBehaviour lifecycle callbacks until Play Mode.
        typeof(PixelRaptorApexWings).GetMethod("OnDisable",Private).Invoke(effect,null);
        host.SetActive(true);effect.Tick(0f);
        Assert.That(effect.IsVisible,Is.False);Assert.That(effect.IsAwakened,Is.False);
    }

    [Test]
    public void CinematicModeFlaresTheSameActualWingsAndReturningTwiceRestoresTheCompactBattlePose()
    {
        source.color=new Color(1f,1f,1f,.7f);
        effect.SetAwakened(true);effect.SetReveal(1f);
        Vector3 normalScale=effect.AuraRoot.localScale;
        float normalOpacity=effect.Opacity;
        effect.WingParticles.GetParticles(before);
        ParticleSystem actualParticles=effect.WingParticles;
        effect.SetCinematic(true);
        Assert.That(effect.AuraRoot.localScale.x,Is.EqualTo(normalScale.x*1.35f).Within(.00001f));
        Assert.That(effect.AuraRoot.localScale.y,Is.EqualTo(normalScale.y*1.35f).Within(.00001f));
        Assert.That(effect.Opacity,Is.GreaterThan(normalOpacity));
        Assert.That(effect.AuraRoot.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
        Assert.That(effect.WingParticles,Is.SameAs(actualParticles));
        effect.SetCinematic(false);effect.SetCinematic(false);
        Assert.That(effect.AuraRoot.localScale,Is.EqualTo(normalScale));
        Assert.That(effect.Opacity,Is.EqualTo(normalOpacity));
        effect.WingParticles.GetParticles(after);
        for(int i=0;i<before.Length;i++)Assert.That(after[i].startSize3D,Is.EqualTo(before[i].startSize3D));
        TestDelegate work=()=>{for(int i=0;i<4;i++){effect.SetCinematic(true);effect.SetCinematic(false);}};
        Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
    }

    [Test]
    public void CinematicBackAnchorMirrorsOnceAndClearRestoresTheNaturalBattleBack()
    {
        source.flipX=true;source.flipY=true;
        effect.SetAwakened(true);effect.SetReveal(1f);
        Vector3 poseBack=new Vector3(.14f,.62f,0f);
        effect.SetBackAnchor(poseBack);
        Assert.That(effect.AuraRoot.localPosition,Is.EqualTo(new Vector3(-.14f,-.62f,0f)));
        effect.SetBackAnchor(null);
        Vector3 ordinary=effect.AuraRoot.localPosition;
        effect.SetBackAnchor(poseBack);effect.Clear();
        effect.SetAwakened(true);effect.SetReveal(1f);
        Assert.That(effect.AuraRoot.localPosition,Is.EqualTo(ordinary));
    }

    [Test]
    public void PreparedPixelHistoryTicksAndLateMovementDoNotAllocateManagedMemory()
    {
        effect.SetAwakened(true);
        var follow=(Action)Delegate.CreateDelegate(typeof(Action),effect,
            typeof(PixelRaptorApexWings).GetMethod("LateUpdate",Private));
        for(int i=0;i<240;i++){host.transform.position+=Vector3.right*.01f;effect.Tick(1f/120f);follow();}
        TestDelegate work=()=>
        {for(int i=0;i<16;i++){effect.Tick(1f/120f);host.transform.position+=Vector3.right*.01f;follow();}};
        Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
    }

    [TestCase("velos",false)]
    [TestCase("velos",true)]
    [TestCase("velok",false)]
    [TestCase("velok",true)]
    [TestCase("raputo",false)]
    [TestCase("raputo",true)]
    [TestCase("wilo",false)]
    [TestCase("wilo",true)]
    public void FourProfilesUseTheCatalogVisualWithAvailableShinyOrTheProductionFallback(string monsterId,bool shiny)
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.Get(monsterId);
        Assert.That(definition,Is.Not.Null,monsterId);
        string path=shiny?definition.shinyResourcePath:definition.resourcePath;
        if(string.IsNullOrWhiteSpace(path))path=definition.resourcePath;
        GameObject prefab=Resources.Load<GameObject>(path);
        Assert.That(prefab,Is.Not.Null,path);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        Assert.That(driver,Is.Not.Null,path);
        Sprite actual=driver.GetIdleFrame(0f);
        Assert.That(actual,Is.Not.Null,path);source.sprite=actual;source.flipX=shiny;
        effect.SetAwakened(true);effect.Tick(1f);Assert.That(effect.IsVisible,Is.True);
        Bounds visible=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actual});
        Vector3 back=visible.center+new Vector3(visible.size.x*.04f,visible.size.y*.13f,0f);
        if(shiny)back.x=-back.x;
        Assert.That(Vector3.Distance(effect.AuraRoot.localPosition,back),Is.LessThan(.00001f));
        Assert.That(source.sprite,Is.SameAs(actual));
        Assert.That(effect.AuraRoot.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty);
    }
}
