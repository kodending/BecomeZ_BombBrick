using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SkillFocusMirrorTests
{
    private GameObject sourceRoot;
    private GameObject mirrorRoot;
    private Texture2D texture;
    private Sprite firstSprite;
    private Sprite secondSprite;
    private SpriteRenderer source;
    private SkillFocusSpriteMirror mirror;

    [SetUp]
    public void SetUp()
    {
        texture=new Texture2D(4,2);
        firstSprite=Sprite.Create(texture,new Rect(0f,0f,2f,2f),Vector2.one*.5f);
        secondSprite=Sprite.Create(texture,new Rect(2f,0f,2f,2f),Vector2.one*.5f);
        sourceRoot=new GameObject("Focus source");
        source=sourceRoot.AddComponent<SpriteRenderer>();
        source.sprite=firstSprite;
        mirrorRoot=new GameObject("Independent focus mirror");
        mirror=mirrorRoot.AddComponent<SkillFocusSpriteMirror>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(mirrorRoot);
        Object.DestroyImmediate(sourceRoot);
        Object.DestroyImmediate(firstSprite);
        Object.DestroyImmediate(secondSprite);
        Object.DestroyImmediate(texture);
    }

    [Test]
    public void FollowDrawsInFrontWithoutChangingTheSourceSortingOrItsRendererHierarchy()
    {
        source.sortingOrder=37;
        int sourceLayer=source.sortingLayerID;
        mirror.Follow(source);

        var drawn=DrawnRenderer();
        Assert.That(drawn.sprite,Is.SameAs(firstSprite));
        Assert.That(drawn.enabled,Is.True);
        Assert.That(drawn.sortingOrder,Is.EqualTo(30004));
        Assert.That(drawn.sortingLayerID,Is.EqualTo(sourceLayer));
        Assert.That(source.sortingOrder,Is.EqualTo(37));
        Assert.That(source.sortingLayerID,Is.EqualTo(sourceLayer));
        Assert.That(source.GetComponentsInChildren<SpriteRenderer>(true),Has.Length.EqualTo(1));
        Assert.That(mirrorRoot.transform.IsChildOf(sourceRoot.transform),Is.False);
    }

    [Test]
    public void LateUpdateTracksTheCurrentSpritePoseFacingAndTintWithoutMovingTheSource()
    {
        mirror.Follow(source);
        source.sprite=secondSprite;
        source.flipX=true;
        source.flipY=true;
        source.color=new Color(.7f,.4f,.2f,.6f);
        source.transform.position=new Vector3(3f,1f,-4f);
        source.transform.rotation=Quaternion.Euler(55f,20f,7f);
        source.transform.localScale=new Vector3(1.4f,.8f,1.1f);
        source.sortingOrder=72;

        Synchronize();

        var drawn=DrawnRenderer();
        Assert.That(drawn.sprite,Is.SameAs(secondSprite));
        Assert.That(drawn.flipX,Is.True);
        Assert.That(drawn.flipY,Is.True);
        Assert.That(drawn.color,Is.EqualTo(source.color));
        Assert.That(Vector3.Distance(drawn.transform.position,source.transform.position),Is.LessThan(.00001f));
        Assert.That(Quaternion.Angle(drawn.transform.rotation,source.transform.rotation),Is.LessThan(.001f));
        Assert.That(Vector3.Distance(drawn.transform.lossyScale,source.transform.lossyScale),Is.LessThan(.00001f));
        Assert.That(source.transform.position,Is.EqualTo(new Vector3(3f,1f,-4f)));
        Assert.That(source.transform.localScale,Is.EqualTo(new Vector3(1.4f,.8f,1.1f)));
        Assert.That(source.sortingOrder,Is.EqualTo(72));
        Assert.That(drawn.sortingOrder,Is.EqualTo(30004));
    }

    [Test]
    public void PropertyBlockUpdatesCopyTheCurrentHitFlashAndRemoveOldShaderValues()
    {
        int flash=Shader.PropertyToID("_SkillFocusTestFlash");
        int tint=Shader.PropertyToID("_SkillFocusTestTint");
        var properties=new MaterialPropertyBlock();
        properties.SetFloat(flash,.65f);
        source.SetPropertyBlock(properties);
        mirror.Follow(source);
        var copied=new MaterialPropertyBlock();
        DrawnRenderer().GetPropertyBlock(copied);
        Assert.That(copied.GetFloat(flash),Is.EqualTo(.65f));

        properties.Clear();
        Color nextTint=new Color(.25f,.5f,.75f,.8f);
        properties.SetColor(tint,nextTint);
        source.SetPropertyBlock(properties);
        Synchronize();
        DrawnRenderer().GetPropertyBlock(copied);
        Assert.That(copied.GetFloat(flash),Is.Zero);
        source.GetPropertyBlock(properties);
        Color sourceTint=properties.GetColor(tint);
        Assert.That(Vector4.Distance(sourceTint,nextTint),Is.LessThan(.00001f),
            "Native property-block color storage must preserve the requested tint within float precision.");
        Assert.That(Vector4.Distance(copied.GetColor(tint),sourceTint),Is.LessThan(.00001f),
            "The mirror must copy the source's current native property-block tint.");
    }

    [Test]
    public void HiddenSourcesAndClearNeverLeaveAFloatingFocusSpriteAndFollowCanBeReused()
    {
        mirror.Follow(source);
        source.enabled=false;
        Synchronize();
        Assert.That(DrawnRenderer().enabled,Is.False);
        source.enabled=true;
        sourceRoot.SetActive(false);
        Synchronize();
        Assert.That(DrawnRenderer().enabled,Is.False);

        sourceRoot.SetActive(true);
        mirror.Follow(source);
        Assert.That(DrawnRenderer().enabled,Is.True);
        mirror.Clear();
        Assert.That(DrawnRenderer().enabled,Is.False);
        source.sprite=secondSprite;
        Synchronize();
        Assert.That(DrawnRenderer().enabled,Is.False);

        mirror.Follow(source);
        Assert.That(DrawnRenderer().enabled,Is.True);
        Assert.That(DrawnRenderer().sprite,Is.SameAs(secondSprite));
    }

    private SpriteRenderer DrawnRenderer()
    {
        var renderer=mirrorRoot.GetComponent<SpriteRenderer>();
        Assert.That(renderer,Is.Not.Null);
        return renderer;
    }

    private void Synchronize()=>typeof(SkillFocusSpriteMirror).GetMethod("LateUpdate",
        BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mirror,null);
}
