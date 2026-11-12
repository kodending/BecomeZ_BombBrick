using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityImpactHierarchyTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private bool active,enabled,reduced;
    private CombatReadabilityPreset preset;
    private CombatSkillClock stage;
    private readonly HashSet<PixelCombatEffect> effects=new HashSet<PixelCombatEffect>();

    [SetUp] public void Setup()
    {
        active=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;
        preset=CombatReadabilityPreview.Preset;
        reduced=CombatFlashPresentation.Reduced;CombatFlashPresentation.Apply(false);
        CombatEffectTestConfiguration.Active=CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
    }

    [TearDown] public void Cleanup()
    {
        stage?.End();stage=null;
        foreach(var effect in effects)if(effect!=null)Object.DestroyImmediate(effect.gameObject);
        effects.Clear();
        CombatEffectTestConfiguration.Active=active;CombatReadabilityPreview.Enabled=enabled;
        CombatReadabilityPreview.Preset=preset;
        CombatFlashPresentation.Apply(reduced);
    }

    [TestCase(CombatReadabilityPreset.Current,true,true,false,false)]
    [TestCase(CombatReadabilityPreset.Compact,true,true,false,false)]
    [TestCase(CombatReadabilityPreset.Local,true,true,false,true)]
    [TestCase(CombatReadabilityPreset.Local,false,true,false,false)]
    [TestCase(CombatReadabilityPreset.Local,true,false,false,false)]
    [TestCase(CombatReadabilityPreset.Local,true,true,true,false)]
    public void OnlyLocalOrdinaryCombatUsesTheEarlierPeakAndCenterClearingTail(
        CombatReadabilityPreset mode,bool test,bool selected,bool cinematic,bool adjusted)
    {
        CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=selected;
        CombatReadabilityPreview.Preset=mode;
        if(cinematic){stage=new CombatSkillClock(null);stage.Hold();}
        var effect=Play(true);
        var source=(Sprite[])typeof(PixelCombatEffect).GetField("monkeyFruitPopFrames",Hidden).GetValue(null);
        var expected=adjusted?new[]{source[0],source[2],source[3],source[3],source[4],source[5],source[6],source[7]}:source;
        CollectionAssert.AreEqual(expected,Frames(effect));
        Assert.That(Frames(effect).Length,Is.EqualTo(8));
        Assert.That(new HashSet<Sprite>(Frames(effect)).Count,Is.GreaterThanOrEqualTo(4));
        Assert.That(Get<float>(effect,"framesPerSecond"),Is.EqualTo(26f));
    }

    [TestCase(true,false,26f)]
    [TestCase(true,true,56f)]
    [TestCase(false,false,40f)]
    public void ActualPeakKeepsItsOpaqueCenterThenClearsToARingWithoutExtendingTheEffect(
        bool heavy,bool secondary,float fps)
    {
        var effect=Play(heavy,secondary);
        var renderer=effect.GetComponent<SpriteRenderer>();
        Sprite[] playback=Frames(effect);
        Assert.That(Get<float>(effect,"framesPerSecond"),Is.EqualTo(fps));
        Assert.That(effect.transform.localScale.x,Is.EqualTo((heavy?1.15f:.38f)*(secondary?.85f:1f)*CombatReadabilityPreview.EffectVisualScale).Within(.0001f));
        Assert.That(renderer.color.a,Is.EqualTo(secondary?.62f:1f).Within(.0001f));
        Advance(effect,1.001f/fps);
        Assert.That(renderer.sprite,Is.SameAs(playback[1]));
        Assert.That(Alpha(renderer.sprite,20,20),Is.GreaterThan(.99f));
        Advance(effect,1f/fps);
        Assert.That(renderer.sprite,Is.SameAs(playback[2]));
        Assert.That(Alpha(renderer.sprite,20,20),Is.LessThan(.01f));
        Assert.That(Alpha(renderer.sprite,33,20),Is.GreaterThan(.5f),"The expanding edge must remain visible.");
        Advance(effect,5.989f/fps);
        Assert.That(renderer.enabled,Is.True);Assert.That(renderer.sprite,Is.SameAs(playback[7]));
        Advance(effect,.02f/fps);
        Assert.That(renderer.enabled,Is.False);Assert.That(effect.gameObject.activeSelf,Is.False);
    }

    [Test]
    public void WarmedSourceAndCachedOrderRemainSeparateAcrossPoolReuseAndPresetChanges()
    {
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        var authored=Play(true);
        Sprite[] source=Frames(authored),snapshot=(Sprite[])Frames(authored).Clone();
        authored.ReleaseAfterBattleEndFade();
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        var first=Play(true);Sprite[] cached=Frames(first);
        Assert.That(first,Is.SameAs(authored));Assert.That(cached,Is.Not.SameAs(source));
        first.ReleaseAfterBattleEndFade();
        var repeated=Play(false,true);
        Assert.That(repeated,Is.SameAs(first));Assert.That(Frames(repeated),Is.SameAs(cached));
        repeated.ReleaseAfterBattleEndFade();
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        var legacy=Play(true);
        Assert.That(legacy,Is.SameAs(first));Assert.That(Frames(legacy),Is.SameAs(source));
        CollectionAssert.AreEqual(snapshot,source);
        Assert.That(Get<float>(legacy,"framesPerSecond"),Is.EqualTo(26f));
        Assert.That(legacy.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(1f));
    }

    private PixelCombatEffect Play(bool heavy,bool secondary=false)
    {
        var effect=PixelCombatEffect.PlayMonkeyFruitPop(Vector3.zero,null,Color.yellow,heavy,1f,secondary);
        effects.Add(effect);return effect;
    }
    private static Sprite[] Frames(PixelCombatEffect effect)=>Get<Sprite[]>(effect,"frames");
    private static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Hidden).GetValue(target);
    private static void Advance(PixelCombatEffect effect,float delta)=>typeof(PixelCombatEffect)
        .GetMethod("Advance",Hidden).Invoke(effect,new object[]{delta});
    private static float Alpha(Sprite sprite,int x,int y)
    {
        var previous=RenderTexture.active;
        var target=RenderTexture.GetTemporary(sprite.texture.width,sprite.texture.height,0,RenderTextureFormat.ARGB32);
        var pixel=new Texture2D(1,1,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(sprite.texture,target);RenderTexture.active=target;
            pixel.ReadPixels(new Rect(x,y,1,1),0,0);pixel.Apply();return pixel.GetPixel(0,0).a;
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(pixel);}
    }
}
