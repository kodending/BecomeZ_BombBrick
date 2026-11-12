using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ContactImpactPresentationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly List<GameObject> created=new List<GameObject>();
    private bool reduced;

    [SetUp] public void Remember()
    {
        reduced=CombatFlashPresentation.Reduced;
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.scene.IsValid()&&effect.name=="Contact impact")
                effect.ReleaseAfterBattleEndFade();
    }

    [TearDown] public void Cleanup()
    {
        foreach(var instance in created)if(instance!=null)Object.DestroyImmediate(instance);
        created.Clear();
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.scene.IsValid()&&
                (effect.name=="Contact impact"||effect.name=="Contact impact pool"))
                Object.DestroyImmediate(effect.gameObject);
        CombatFlashPresentation.Apply(reduced);
    }

    [Test]
    public void ContactImpactsAreBoundedFinishAndReuseWithoutMovingTheVictimOrUsingCombatRandom()
    {
        var reference=Spawn().GetComponentInChildren<SpriteRenderer>();
        Vector3 body=reference.transform.root.position;
        var random=Random.state;
        string before=JsonUtility.ToJson(random);
        PixelCombatEffect.PrepareContactImpacts();
        var effects=new List<PixelCombatEffect>();
        for(int i=0;i<8;i++)
        {
            var effect=PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.right,false);
            Assert.That(effect,Is.Not.Null);
            effects.Add(effect);
        }
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.EqualTo(8));
        Assert.That(PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.right,true),Is.Null);
        Vector3 impactPosition=effects[0].transform.position;
        reference.transform.root.position+=Vector3.right*2f;
        Call(effects[0],"LateUpdate");
        Assert.That(effects[0].transform.position,Is.EqualTo(impactPosition),"Contact stays at the actual point of impact.");
        reference.transform.root.position=body;
        foreach(var effect in effects)Call(effect,"Advance",.11f);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
        var reused=PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.right,false);
        Assert.That(effects.Contains(reused),Is.True);
        Assert.That(reference.transform.root.position,Is.EqualTo(body));
        Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(before));
        reused.ReleaseAfterBattleEndFade();
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
    }

    [Test]
    public void DisableDestroyAndGenericPoolReuseDoNotLeakTheContactLimitOrReducedFlashMode()
    {
        var reference=Spawn().GetComponentInChildren<SpriteRenderer>();
        var effect=PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.right,false);
        effect.gameObject.SetActive(false);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
        Object.DestroyImmediate(effect.gameObject);
        effect=PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.right,false);
        Object.DestroyImmediate(effect.gameObject);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
        effect=PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.right,false);
        effect.ReleaseAfterBattleEndFade();
        var generic=PixelCombatEffect.PlayCritical(null,null);
        created.Add(generic.gameObject);
        CombatFlashPresentation.Apply(false);
        Call(generic,"LateUpdate");
        float alpha=generic.GetComponent<SpriteRenderer>().color.a;
        CombatFlashPresentation.Apply(true);
        Call(generic,"LateUpdate");
        Assert.That(generic.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(alpha));
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
    }

    [Test]
    public void ContactFlashReductionChangesOnlyAccentAndRestoresOnToggle()
    {
        CombatFlashPresentation.Apply(false);
        var reference=Spawn().GetComponentInChildren<SpriteRenderer>();
        var effect=PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.forward,true);
        var renderer=effect.GetComponent<SpriteRenderer>();
        Color color=renderer.color;Sprite frame=renderer.sprite;
        Vector3 scale=effect.transform.localScale;Vector3 position=effect.transform.position;
        CombatFlashPresentation.Apply(true);Call(effect,"LateUpdate");
        Assert.That(renderer.color.a,Is.EqualTo(color.a*.25f).Within(.0001f));
        Assert.That(renderer.color.r,Is.LessThan(color.r));
        Assert.That(renderer.sprite,Is.SameAs(frame));
        Assert.That(effect.transform.localScale,Is.EqualTo(scale));
        Assert.That(effect.transform.position,Is.EqualTo(position));
        CombatFlashPresentation.Apply(false);Call(effect,"LateUpdate");
        Assert.That(renderer.color,Is.EqualTo(color));
    }

    [Test]
    public void BattleEndPlaybackKeepsTheReducedContactAccentUntilNaturalCompletion()
    {
        CombatFlashPresentation.Apply(true);
        var reference=Spawn().GetComponentInChildren<SpriteRenderer>();
        var effect=PixelCombatEffect.PlayContactImpact(reference.transform,reference,Vector3.right,true);
        var renderer=effect.GetComponent<SpriteRenderer>();
        Color color=renderer.color;
        CombatEffectEndPlayback.Begin(effect.gameObject,new HashSet<System.Type>{typeof(PixelCombatEffect)});
        Assert.That(effect.enabled,Is.False);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.EqualTo(1));
        Call(effect,"LateUpdate");
        Assert.That(renderer.color,Is.EqualTo(color));
        Call(effect.GetComponent<CombatEffectEndPlayback>(),"Advance",.11f);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
    }

    [Test]
    public void RapidHitsDoNotExtendFlashAndDamageTicksCannotTakeTheContactAccent()
    {
        var instance=Spawn();
        var reaction=instance.GetComponent<VoxelEnemyReaction>()??instance.AddComponent<VoxelEnemyReaction>();
        reaction.PlayHit(Vector3.right,false,false);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
        Assert.That(Field<float>(reaction,"contactAccentCooldown"),Is.Zero);
        reaction.PlayHit(Vector3.right,false,true);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.EqualTo(1));
        Call(reaction,"UpdateFlash",.03f);
        float remaining=Field<float>(reaction,"flashTimer");
        Color tint=Field<Color>(reaction,"flashColor");
        reaction.PlayHit(Vector3.left,true,true);
        reaction.PlayHit(Vector3.left,false,false);
        Assert.That(Field<float>(reaction,"flashTimer"),Is.EqualTo(remaining));
        Assert.That(Field<Color>(reaction,"flashColor"),Is.EqualTo(tint));
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.EqualTo(1));
        Call(reaction,"UpdateFlash",.09f);
        Assert.That(Field<float>(reaction,"flashTimer"),Is.Zero);
        reaction.PrepareForReuse();
        Assert.That(Field<float>(reaction,"contactAccentCooldown"),Is.Zero);
        Assert.That(Field<float>(reaction,"flashTimer"),Is.Zero);
    }

    [TestCase(false)] [TestCase(true)]
    public void RedSilhouetteFollowsTheCurrentPoseAndFocusThenRestoresStatusTint(bool strong)
    {
        CombatFlashPresentation.Apply(false);
        var instance=Spawn();
        var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var renderer=driver.GetComponent<SpriteRenderer>();
        var reaction=instance.GetComponent<VoxelEnemyReaction>()??instance.AddComponent<VoxelEnemyReaction>();
        reaction.Initialize();
        Material original=renderer.sharedMaterial;
        Color status=new Color(.45f,.8f,1f);
        reaction.SetStatusTint(status,.3f);
        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        Color resting=block.GetColor("_Color");
        Texture texture=renderer.sprite.texture;
        block.SetTexture("_MainTex",texture);renderer.SetPropertyBlock(block);
        renderer.flipX=true;renderer.color=new Color(.9f,.8f,.7f,.6f);
        Color rendererColor=renderer.color;
        var focus=new GameObject("Hit flash focused body");created.Add(focus);
        var mirror=focus.AddComponent<SkillFocusSpriteMirror>();mirror.Follow(renderer,0f);

        reaction.PlayHit(Vector3.right,strong);
        Call(mirror,"LateUpdate");
        renderer.GetPropertyBlock(block);
        Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("BecomeZ/Effects/Pixel Hit Flash"));
        Assert.That(block.GetFloat("_HitAmount"),Is.EqualTo(strong?.7f:.55f).Within(.0001f));
        Assert.That(block.GetColor("_HitColor").r,Is.GreaterThan(.95f));
        Assert.That(block.GetColor("_HitColor").g,Is.LessThan(.1f));
        Assert.That(block.GetColor("_Color"),Is.EqualTo(resting));
        Assert.That(block.GetTexture("_MainTex"),Is.SameAs(texture));
        Assert.That(renderer.color,Is.EqualTo(rendererColor));
        Assert.That(renderer.flipX,Is.True);
        SpriteRenderer foreground=focus.GetComponent<SpriteRenderer>();
        Assert.That(foreground.sharedMaterial,Is.SameAs(renderer.sharedMaterial));
        Assert.That(foreground.sprite,Is.SameAs(renderer.sprite));
        Assert.That(foreground.sortingOrder,Is.EqualTo(30004));
        var copied=new MaterialPropertyBlock();foreground.GetPropertyBlock(copied);
        Assert.That(copied.GetFloat("_HitAmount"),Is.EqualTo(block.GetFloat("_HitAmount")));
        reaction.PlayTintHit(Color.white);
        Assert.That(Field<float>(reaction,"activeFlashDuration"),Is.EqualTo(strong?.11f:.09f));
        reaction.SetStatusTint(status,.5f);
        Call(reaction,"UpdateFlash",.12f);Call(mirror,"LateUpdate");
        renderer.GetPropertyBlock(block);
        Assert.That(renderer.sharedMaterial,Is.SameAs(original));
        Assert.That(foreground.sharedMaterial,Is.SameAs(original));
        Assert.That(block.GetFloat("_HitAmount"),Is.Zero);
        Assert.That(block.GetColor("_Color"),Is.EqualTo(Color.Lerp(Color.white,status,.5f)));
        Assert.That(block.GetTexture("_MainTex"),Is.SameAs(texture));
    }

    [TestCase("disable")] [TestCase("reuse")] [TestCase("death")]
    public void RedSilhouetteCannotLeakItsTemporaryMaterialAcrossLifecycleChanges(string transition)
    {
        var instance=Spawn();
        var renderer=instance.GetComponentInChildren<SpriteRenderer>();
        var reaction=instance.GetComponent<VoxelEnemyReaction>()??instance.AddComponent<VoxelEnemyReaction>();
        Material original=renderer.sharedMaterial;
        reaction.PlayHit(Vector3.right,true);
        Assert.That(renderer.sharedMaterial,Is.Not.SameAs(original));
        if(transition=="disable")Call(reaction,"OnDisable");
        else if(transition=="reuse")reaction.PrepareForReuse();
        else reaction.PlayCaptureHold();
        Assert.That(renderer.sharedMaterial,Is.SameAs(original));
        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_HitAmount"),Is.Zero);
        Assert.That(Field<float>(reaction,"flashTimer"),Is.Zero);
    }

    [Test]
    public void ReducedFlashesDimTheSilhouetteWithoutChangingItsTimingOrMaterialRecovery()
    {
        var instance=Spawn();
        var renderer=instance.GetComponentInChildren<SpriteRenderer>();
        var reaction=instance.GetComponent<VoxelEnemyReaction>()??instance.AddComponent<VoxelEnemyReaction>();
        Material original=renderer.sharedMaterial;
        CombatFlashPresentation.Apply(false);reaction.PlayHit(Vector3.right,true);
        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        float amount=block.GetFloat("_HitAmount"),duration=Field<float>(reaction,"flashTimer");
        CombatFlashPresentation.Apply(true);Call(reaction,"UpdateFlash",0f);
        renderer.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_HitAmount"),Is.EqualTo(amount*.25f).Within(.0001f));
        Assert.That(Field<float>(reaction,"flashTimer"),Is.EqualTo(duration));
        Call(reaction,"UpdateFlash",.12f);
        Assert.That(renderer.sharedMaterial,Is.SameAs(original));
    }

    [TestCase(false)] [TestCase(true)]
    public void HitShaderColorsDarkPixelsWhileKeepingTransparencyRendererAlphaAndFlip(bool externalAlpha)
    {
        var texture=new Texture2D(4,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        texture.SetPixels(new[]{Color.black,Color.white,Color.white,externalAlpha?Color.white:Color.clear});
        texture.Apply();
        var alpha=new Texture2D(4,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        alpha.SetPixels(new[]{Color.white,Color.white,Color.white,Color.black});alpha.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,4,1),new Vector2(.5f,.5f),1f);
        var material=new Material(Resources.Load<Shader>("CombatEffects/SpriteHitFlash"));
        var pixels=new Texture2D(128,32,TextureFormat.RGBA32,false);
        var target=new RenderTexture(128,32,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        RenderTexture previous=RenderTexture.active;
        var host=new GameObject("Hit silhouette alpha test");created.Add(host);host.layer=31;
        var renderer=host.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;
        renderer.color=new Color(.8f,.5f,.5f,.6f);
        var cameraHost=new GameObject("Hit silhouette test camera");created.Add(cameraHost);
        var camera=cameraHost.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.5f;
        camera.aspect=4f;camera.transform.position=Vector3.back*10f;camera.cullingMask=1<<31;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        material.SetFloat("_HitAmount",.55f);material.SetColor("_HitColor",new Color(1f,.08f,.045f));
        if(externalAlpha)
        {
            material.EnableKeyword("ETC1_EXTERNAL_ALPHA");
            // UnityPerDrawSprite owns these values; material defaults do not override the renderer.
            var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
            properties.SetTexture("_AlphaTex",alpha);properties.SetFloat("_EnableExternalAlpha",1f);
            renderer.SetPropertyBlock(properties);
        }
        try
        {
            camera.targetTexture=target;
            foreach(bool flip in new[]{false,true})
            {
                renderer.flipX=flip;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,128,32),0,0);pixels.Apply();
                Color dark=pixels.GetPixel(flip?112:16,16);
                Color light=pixels.GetPixel(flip?80:48,16);
                Color transparent=pixels.GetPixel(flip?16:112,16);
                Assert.That(dark.r,Is.GreaterThan(.25f),"Black source pixels must still show the red silhouette.");
                Assert.That(dark.g,Is.LessThan(dark.r*.35f));
                Assert.That(light.r,Is.GreaterThan(dark.r),"The brief accent must retain some original detail.");
                Assert.That(dark.a,Is.EqualTo(.6f).Within(.02f));
                Assert.That(transparent.a,Is.LessThan(.01f));
            }
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;target.Release();
            Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(material);
            Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);Object.DestroyImmediate(alpha);
        }
    }

    private GameObject Spawn()
    {
        var instance=PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
        created.Add(instance);
        return instance;
    }

    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
    private static void Call(object value,string method,params object[] args)=>
        value.GetType().GetMethod(method,Private).Invoke(value,args.Length==0?null:args);
}
