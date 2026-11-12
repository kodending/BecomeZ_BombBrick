using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class BearAvatarCinematicStageTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host;
    private BearAvatarCinematicStage stage;
    private Texture2D texture;
    private Sprite sprite;

    [SetUp] public void Setup()
    {
        host=new GameObject("Bear transformation private stage test");stage=new BearAvatarCinematicStage(host.transform);
        texture=new Texture2D(8,8);texture.SetPixels(Enumerable.Repeat(Color.white,64).ToArray());texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0f,0f,8f,8f),Vector2.one*.5f,8f);
    }
    [TearDown] public void Cleanup()
    {stage?.Dispose();Object.DestroyImmediate(host);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);}

    [Test]
    public void ProductionAtlasesHaveTwentySeparateUncutCellsAndStablePerRowFraming()
    {
        stage.Warmup();
        var parts=(Sprite[])typeof(BearAvatarCinematicStage).GetField("parts",Private).GetValue(stage);
        var head=(Sprite[])typeof(BearAvatarCinematicStage).GetField("head",Private).GetValue(stage);
        Assert.That(parts,Has.Length.EqualTo(12));Assert.That(head,Has.Length.EqualTo(8));
        foreach(var frames in new[]{parts,head})
        {
            int rows=frames.Length/4;var atlas=frames[0].texture;
            Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));
            foreach(Sprite frame in frames)
            {
                Assert.That(frame.rect.width,Is.EqualTo(atlas.width/4f).Within(.51f));
                Assert.That(frame.rect.height,Is.EqualTo(atlas.height/(float)rows).Within(.51f));
                Assert.That(frame.rect.xMin,Is.GreaterThanOrEqualTo(0f));Assert.That(frame.rect.xMax,Is.LessThanOrEqualTo(atlas.width));
                Assert.That(frame.rect.yMin,Is.GreaterThanOrEqualTo(0f));Assert.That(frame.rect.yMax,Is.LessThanOrEqualTo(atlas.height));
            }
            for(int i=0;i<frames.Length;i++)for(int j=0;j<i;j++)Assert.That(frames[i].rect.Overlaps(frames[j].rect),Is.False);
        }
        Assert.That(stage.Begin(Source()),Is.True);
        foreach(float time in new[]{.15f,.30f,.50f,.63f,.80f,.94f,1.2f,1.4f,1.65f,1.8f,2.1f,2.4f})
        {
            stage.Tick(time,0f);AssertFinite(stage.SourceViewportRect);
            foreach(var renderer in host.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.name.Contains("part")))
            {var p=new MaterialPropertyBlock();renderer.GetPropertyBlock(p);Assert.That(p.GetTexture("_MainTex"),Is.Not.Null);}
        }
    }

    [TestCase(.2f,1)] [TestCase(.55f,2)] [TestCase(.85f,3)]
    [TestCase(1.2f,4)] [TestCase(1.7f,5)] [TestCase(2.3f,6)]
    public void SixDifferentBodyPartsFollowOnlyTheExplicitClock(float time,int shot)
    {
        float speed=Time.timeScale;
        try
        {
            Time.timeScale=0f;Assert.That(stage.Begin(Source()),Is.True);stage.Tick(time,0f);
            Assert.That(stage.Shot,Is.EqualTo(shot));Assert.That(Time.timeScale,Is.Zero);
            Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty,"No surrounding combat monsters belong in the private stage.");
        }
        finally {Time.timeScale=speed;}
    }

    [TestCase("woongi")] [TestCase("dami")] [TestCase("ironkuma")] [TestCase("berber")]
    public void EveryBearKeepsItsRealUntransformedSpriteAndShinyTintUnderTheIncomingSpirit(string id)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var actor=Source();actor.species=id;actor.sprite=driver.GetSkillAttackFrame(0);actor.shiny=true;actor.flipX=true;actor.color=new Color(.8f,.6f,.9f);
        Assert.That(stage.Begin(actor),Is.True);stage.Tick(.005f,0f);
        var renderer=Renderer("Bear actual species underlay");var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        Assert.That(renderer.enabled,Is.True);Assert.That(block.GetTexture("_MainTex"),Is.SameAs(actor.sprite.texture));
        Assert.That(block.GetColor("_Color").r,Is.EqualTo(.8f));Assert.That(renderer.transform.localScale.x,Is.LessThan(0f));
        Assert.That(stage.Source.species,Is.EqualTo(id));Assert.That(stage.Source.sprite,Is.SameAs(actor.sprite));
        stage.Tick(1.505f,0f);renderer.GetPropertyBlock(block);Assert.That(block.GetTexture("_MainTex"),Is.SameAs(actor.sprite.texture));
    }

    [TestCase(false)] [TestCase(true)]
    public void FinalPoseMatchesTheLiveSpiritSpriteFootHeightAndFlipAndReuseClearsThatTarget(bool flip)
    {
        var actor=Source();actor.flipX=flip;Assert.That(stage.Begin(actor),Is.True);
        stage.SetReturnSprite(sprite);stage.SetReturnView(new Vector2(.31f,.22f),.11f);stage.Tick(BearAvatarCinematicStage.Duration,0f);
        Rect result=stage.SourceViewportRect;
        Assert.That(result.center.x,Is.EqualTo(.31f).Within(.001f));Assert.That(result.yMin,Is.EqualTo(.22f).Within(.001f));
        Assert.That(result.height,Is.EqualTo(.11f).Within(.001f));
        var live=Renderer("Bear actual species underlay");var p=new MaterialPropertyBlock();live.GetPropertyBlock(p);
        Assert.That(p.GetTexture("_MainTex"),Is.SameAs(texture));Assert.That(p.GetColor("_Color").a,Is.EqualTo(1f));
        Assert.That(live.transform.localScale.x>0f,Is.EqualTo(!flip));
        Assert.That(stage.Begin(actor),Is.True);stage.Tick(BearAvatarCinematicStage.Duration,0f);
        Assert.That(stage.SourceViewportRect.height,Is.GreaterThan(.2f));
    }

    [Test]
    public void AllShotsKeepThePrivateRenderTextureCompletelyOpaque()
    {
        Assert.That(stage.Begin(Source()),Is.True);Camera camera=host.GetComponentsInChildren<Camera>(true).Single();
        var small=RenderTexture.GetTemporary(48,32,0,RenderTextureFormat.ARGB32);
        var readable=new Texture2D(48,32,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        try
        {
            foreach(float time in new[]{.2f,.41f,.55f,.72f,.85f,1.2f,1.7f,2.3f,2.6f,2.8f})
            {
                stage.Tick(time,0f);camera.Render();Graphics.Blit(stage.Texture,small);RenderTexture.active=small;
                readable.ReadPixels(new Rect(0f,0f,48f,32f),0,0);readable.Apply();
                foreach(Color32 pixel in readable.GetPixels32())Assert.That(pixel.a,Is.GreaterThanOrEqualTo(254),"Live-map alpha must never bleed through the private transformation.");
            }
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(small);Object.DestroyImmediate(readable);}
    }

    [Test]
    public void PrivateTargetPreservesTheGameViewAspectWithoutCroppingReturnCoordinates()
    {
        var target=(RenderTexture)stage.Texture;
        float aspect=Mathf.Max(1,Screen.width)/(float)Mathf.Max(1,Screen.height);
        Assert.That(target.width/(float)target.height,Is.EqualTo(aspect).Within(2f/target.height));
        Assert.That(target.width,Is.LessThanOrEqualTo(1600));Assert.That(target.height,Is.LessThanOrEqualTo(900));
    }

    [TestCase(30)] [TestCase(120)]
    public void PoseAndMovingParticlesAreIdenticalForDirectSteppedOrRewoundClocks(int fps)
    {
        Assert.That(stage.Begin(Source()),Is.True);float target=1.734f;
        for(int i=0;i/(float)fps<target;i++)stage.Tick(i/(float)fps,1f/fps);stage.Tick(target,0f);
        var renderer=Renderer("Bear current transforming part");Vector3 position=renderer.transform.localPosition,scale=renderer.transform.localScale;
        var particles=host.GetComponentInChildren<ParticleSystem>(true);var first=new ParticleSystem.Particle[1100];int count=particles.GetParticles(first);
        Rect viewport=stage.SourceViewportRect;
        stage.Tick(2.3f,0f);stage.Tick(target,0f);var second=new ParticleSystem.Particle[1100];int again=particles.GetParticles(second);
        Assert.That(renderer.transform.localPosition,Is.EqualTo(position));Assert.That(renderer.transform.localScale,Is.EqualTo(scale));
        Assert.That(stage.SourceViewportRect,Is.EqualTo(viewport));Assert.That(again,Is.EqualTo(count));
        for(int i=0;i<count;i++){Assert.That(second[i].position,Is.EqualTo(first[i].position));Assert.That(second[i].startColor,Is.EqualTo(first[i].startColor));}
    }

    [Test]
    public void SpiritMotesSurroundBothSidesInsteadOfCollapsingIntoOneGoldenPhaseLine()
    {
        Assert.That(stage.Begin(Source()),Is.True);stage.Tick(1.35f,0f);
        var particles=host.GetComponentInChildren<ParticleSystem>(true);var pixels=new ParticleSystem.Particle[1100];
        int count=particles.GetParticles(pixels),left=0,right=0;
        for(int i=0;i<count;i++)
        {if(pixels[i].position.x<-.15f)left++;if(pixels[i].position.x>.15f)right++;}
        Assert.That(left,Is.GreaterThan(20));Assert.That(right,Is.GreaterThan(20),
            "Particle lifetime and spiral angle must not cancel each other's golden-ratio phase.");
    }

    [Test]
    public void InvalidInputAndRepeatedHideDisposeCannotLeaveAStageCameraOrTargetAlive()
    {
        Assert.That(stage.Begin(new FrogCatchCinematicStage.Actor()),Is.False);Assert.That(stage.Active,Is.False);
        Assert.That(stage.Begin(Source()),Is.True);var rt=(RenderTexture)stage.Texture;
        stage.SetReturnView(new Vector2(float.NaN,0f),float.PositiveInfinity);stage.Tick(float.NaN,0f);
        stage.Hide();Assert.That(stage.Active,Is.False);stage.Dispose();stage.Dispose();
        Assert.That(rt==null||!rt.IsCreated(),Is.True);Assert.That(sprite,Is.Not.Null);
        Assert.That(stage.Begin(Source()),Is.False);
    }
    private FrogCatchCinematicStage.Actor Source()=>new FrogCatchCinematicStage.Actor
    {species="woongi",sprite=sprite,frames=new[]{sprite},color=Color.white,size=1f};
    private MeshRenderer Renderer(string name)=>host.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name==name);
    private static void AssertFinite(Rect rect)
    {
        Assert.That(float.IsNaN(rect.x+rect.y+rect.width+rect.height)||float.IsInfinity(rect.x+rect.y+rect.width+rect.height),Is.False);
        Assert.That(rect.width,Is.GreaterThan(0f));Assert.That(rect.height,Is.GreaterThan(0f));
    }
}
