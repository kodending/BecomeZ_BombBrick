using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class Rank4SkillPreludeVisualTests
{
    private GameObject parent,sourceRoot;
    private SpriteRenderer source;
    private Texture2D texture;
    private Sprite sprite,secondSprite;
    private Material sharedMaterial;
    private Rank4SkillPreludeVisual visual;

    [SetUp] public void Setup()
    {
        parent=new GameObject("Rank 4 anticipation visual fixture");
        sourceRoot=new GameObject("Unchanged battle caster");sourceRoot.transform.SetParent(parent.transform,false);
        source=sourceRoot.AddComponent<SpriteRenderer>();
        texture=new Texture2D(4,6,TextureFormat.RGBA32,false);
        var pixels=new Color[24];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;
        texture.SetPixels(pixels);texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,4,6),new Vector2(.25f,.2f),10f,0,SpriteMeshType.FullRect);
        secondSprite=Sprite.Create(texture,new Rect(0,0,4,6),new Vector2(.75f,.8f),10f,0,SpriteMeshType.FullRect);
        sharedMaterial=new Material(Resources.Load<Shader>("CombatEffects/SpriteHitFlash"));
        source.sprite=sprite;source.sharedMaterial=sharedMaterial;
        visual=new Rank4SkillPreludeVisual(parent.transform);
    }

    [TearDown] public void Cleanup()
    {
        visual?.Dispose();
        typeof(PixelMantisReaper).GetMethod("ReleaseVisibleBodies",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{new[]{sprite,secondSprite}});
        Object.DestroyImmediate(parent);Object.DestroyImmediate(sprite);Object.DestroyImmediate(secondSprite);
        Object.DestroyImmediate(texture);Object.DestroyImmediate(sharedMaterial);
    }

    [Test]
    public void FrozenCasterKeepsItsActualPoseFacingTintAndUnrelatedShaderProperties()
    {
        source.flipX=true;source.flipY=true;source.color=new Color(.63f,.74f,.85f,.8f);
        source.sortingOrder=63;source.sharedMaterial.SetColor("_Color",new Color(.9f,.8f,.7f,1f));
        var original=new MaterialPropertyBlock();original.SetFloat("_PreludeFixtureValue",.73f);
        original.SetVector("_GroundClip",new Vector4(.28f,1f,0f,0f));source.SetPropertyBlock(original);
        visual.Begin(source);visual.Draw(.65f);
        var body=visual.Body;var copied=new MaterialPropertyBlock();body.GetPropertyBlock(copied);
        Assert.That(body.sprite,Is.SameAs(sprite));Assert.That(body.flipX,Is.True);Assert.That(body.flipY,Is.True);
        Assert.That(body.color,Is.EqualTo(source.color));Assert.That(body.sortingLayerID,Is.EqualTo(source.sortingLayerID));
        Assert.That(body.sortingOrder,Is.GreaterThan(30004));Assert.That(source.sortingOrder,Is.EqualTo(63));
        Assert.That(copied.GetFloat("_PreludeFixtureValue"),Is.EqualTo(.73f));
        Assert.That(copied.GetVector("_GroundClip"),Is.EqualTo(original.GetVector("_GroundClip")));
        Assert.That(copied.GetFloat("_HitAmount"),Is.GreaterThan(0f).And.LessThan(.4f));
        Assert.That(copied.GetColor("_HitColor").g,Is.GreaterThan(.8f));
        Assert.That(body.sharedMaterial,Is.Not.SameAs(sharedMaterial));
        Assert.That(body.sharedMaterial.shader,Is.SameAs(sharedMaterial.shader));
        Assert.That(body.sharedMaterial.GetColor("_Color"),Is.EqualTo(sharedMaterial.GetColor("_Color")));
        source.GetPropertyBlock(copied);
        Assert.That(copied.GetFloat("_HitAmount"),Is.Zero,"The source hit color and material block remain owned by combat.");
    }

    [Test]
    public void EnlargementHoldsTheVisibleFeetAndDoesNotResizeMoveOrHideTheRealCaster()
    {
        source.flipX=source.flipY=true;
        source.transform.position=new Vector3(3f,1f,-4f);source.transform.rotation=Quaternion.Euler(55f,20f,7f);
        source.transform.localScale=new Vector3(1.4f,.8f,1.1f);
        Vector3 position=source.transform.position,scale=source.transform.localScale,feet=Feet(source);
        Quaternion rotation=source.transform.rotation;
        visual.Begin(source);visual.Draw(.65f);
        Assert.That(Vector3.Distance(visual.Body.transform.lossyScale,source.transform.lossyScale*1.18f),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(Feet(visual.Body),feet),Is.LessThan(.0001f));
        Assert.That(source.transform.position,Is.EqualTo(position));Assert.That(source.transform.localScale,Is.EqualTo(scale));
        Assert.That(Quaternion.Angle(source.transform.rotation,rotation),Is.LessThan(.001f));
        Assert.That(source.enabled,Is.True);Assert.That(source.forceRenderingOff,Is.False);
    }

    [Test]
    public void TheFrozenProxyRemainsVisibleWhenTheBattleOwnsTheHiddenSource()
    {
        visual.Begin(source);source.enabled=false;source.forceRenderingOff=true;
        sourceRoot.SetActive(false);visual.Draw(.7f);
        Assert.That(visual.Body.enabled,Is.True);Assert.That(visual.Body.forceRenderingOff,Is.False);
        Assert.That(visual.Body.gameObject.activeInHierarchy,Is.True);
        Assert.That(source.enabled,Is.False);Assert.That(source.forceRenderingOff,Is.True);
        Assert.That(sourceRoot.activeSelf,Is.False);
    }

    [Test]
    public void ClearCanBeReusedAndDisposalDestroysOnlyTheOwnedProxyAndMaterial()
    {
        visual.Begin(source);SpriteRenderer proxy=visual.Body;Material owned=proxy.sharedMaterial;
        Material pixelMaterial=parent.GetComponentInChildren<ParticleSystemRenderer>(true).sharedMaterial;
        Assert.That(pixelMaterial,Is.Not.SameAs(owned));Assert.That(pixelMaterial.mainTexture,Is.SameAs(Texture2D.whiteTexture));
        visual.Clear();visual.Draw(.5f);Assert.That(proxy.enabled,Is.False);
        source.sprite=secondSprite;visual.Begin(source);visual.Draw(.2f);
        Assert.That(proxy.enabled,Is.True);Assert.That(proxy.sprite,Is.SameAs(secondSprite));
        Assert.That(proxy.sharedMaterial,Is.SameAs(owned));
        visual.Dispose();visual.Dispose();
        Assert.That(proxy==null,Is.True);Assert.That(owned==null,Is.True);Assert.That(pixelMaterial==null,Is.True);
        Assert.That(source!=null&&sprite!=null&&secondSprite!=null&&texture!=null&&sharedMaterial!=null,Is.True);
        Assert.That(source.sprite,Is.SameAs(secondSprite));Assert.That(source.sharedMaterial,Is.SameAs(sharedMaterial));
    }

    [Test]
    public void InvalidOrOutOfRangeProgressNeverProducesAnInvalidTransform()
    {
        visual.Begin(source);
        foreach(float progress in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f,2f})
        {
            visual.Draw(progress);Vector3 position=visual.Body.transform.position,scale=visual.Body.transform.lossyScale;
            Assert.That(Finite(position.x)&&Finite(position.y)&&Finite(position.z),Is.True);
            Assert.That(Finite(scale.x)&&Finite(scale.y)&&Finite(scale.z),Is.True);
            Assert.That(scale.x,Is.InRange(source.transform.lossyScale.x,source.transform.lossyScale.x*1.236f));
        }
    }

    [Test]
    public void TheEntryOvershootsOnceThenSettlesBeforeTheCutsceneWithoutMovingTheFeet()
    {
        Vector3 feet=Feet(source);visual.Begin(source);
        visual.Draw(.16f);float peak=visual.Body.transform.lossyScale.y/source.transform.lossyScale.y;
        Assert.That(peak,Is.EqualTo(1.235f).Within(.0001f));
        Assert.That(Vector3.Distance(Feet(visual.Body),feet),Is.LessThan(.0001f));
        foreach(float progress in new[]{.40f,.65f,.89f,1f})
        {
            visual.Draw(progress);
            Assert.That(visual.Body.transform.lossyScale.y/source.transform.lossyScale.y,Is.EqualTo(1.18f).Within(.0001f));
            Assert.That(Vector3.Distance(Feet(visual.Body),feet),Is.LessThan(.0001f));
        }
        Assert.That(source.transform.localScale,Is.EqualTo(Vector3.one));
    }

    [Test]
    public void ChargePixelsAdvanceDuringTheFrozenPoseAndClearBeforeReuse()
    {
        visual.Begin(source);var particles=parent.GetComponentInChildren<ParticleSystem>(true);
        var buffer=new ParticleSystem.Particle[20];
        visual.Draw(.50f);int chargeCount=particles.GetParticles(buffer);
        Assert.That(chargeCount,Is.InRange(1,20));
        float chargeWidth=0f;
        for(int i=0;i<chargeCount;i++)
        {
            Assert.That(Finite(buffer[i].position.x)&&Finite(buffer[i].position.y),Is.True);
            Assert.That(buffer[i].startSize,Is.GreaterThan(0f));chargeWidth=Mathf.Max(chargeWidth,Mathf.Abs(buffer[i].position.x));
        }
        visual.Draw(.89f);int releaseCount=particles.GetParticles(buffer);
        Assert.That(releaseCount,Is.InRange(1,20));float releaseWidth=0f;
        for(int i=0;i<releaseCount;i++)releaseWidth=Mathf.Max(releaseWidth,Mathf.Abs(buffer[i].position.x));
        Assert.That(releaseWidth,Is.GreaterThan(chargeWidth));
        visual.Clear();Assert.That(particles.particleCount,Is.Zero);
        visual.Begin(source);Assert.That(particles.particleCount,Is.Zero);
    }

    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private static Vector3 Feet(SpriteRenderer renderer)
    {
        Bounds bounds=renderer.sprite.bounds;
        return renderer.transform.TransformPoint(new Vector3(renderer.flipX?-bounds.center.x:bounds.center.x,
            renderer.flipY?-bounds.max.y:bounds.min.y,0f));
    }
}
