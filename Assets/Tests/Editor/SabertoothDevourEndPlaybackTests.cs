using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public sealed class SabertoothDevourEndPlaybackTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string HeadResource="CombatEffects/SabertoothDevour/DevourHeadParts";
    private static readonly ISet<Type> EffectTypes=new HashSet<Type>{typeof(PixelSabertoothDevour)};
    private Scene scene,previousScene;
    private bool ownsScene;
    private GameObject caster,victim;
    private Texture2D texture;
    private Sprite sprite;
    private PixelSabertoothDevour effect;
    private readonly Vector3 mouth=new Vector3(-1.5f,.6f,0f);

    [SetUp] public void Setup()
    {
        previousScene=SceneManager.GetActiveScene();scene=default(Scene);ownsScene=false;
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            Scene loaded=SceneManager.GetSceneAt(i);
            if(loaded.isLoaded&&string.IsNullOrEmpty(loaded.path)){scene=loaded;break;}
        }
        // Preview scenes skip normal component destruction and reset native particle state.
        if(!scene.IsValid())
        {
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);ownsScene=true;
        }
        SceneManager.SetActiveScene(scene);
        caster=Go("Devour playback caster");victim=Go("Devour playback victim");
        caster.transform.position=Vector3.left*2f;victim.transform.position=Vector3.right;
        var source=caster.AddComponent<SpriteRenderer>();
        texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0f,0f,2f,2f),Vector2.one*.5f,2f);source.sprite=sprite;
        effect=PixelSabertoothDevour.Create(source,Vector3.right,Vector3.right,3f,1.8f,
            new List<Transform>{victim.transform});
        effect.Particles.Play(false);effect.Particles.Pause(false);
    }

    [TearDown] public void Cleanup()
    {
        CombatEffectEndPlayback.Clear(scene);
        if(effect!=null)effect.Cancel(true);
        Object.DestroyImmediate(caster);Object.DestroyImmediate(victim);
        if(previousScene.IsValid()&&previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);
        if(ownsScene&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
        Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
    }

    [TestCase(.3f,false)] [TestCase(1.85f,false)] [TestCase(2.3f,false)] [TestCase(.3f,true)]
    public void BattleEndCompletesTheExistingBiteAndTailWithoutItsCasterOrGameplayState(float age,bool deleteCaster)
    {
        effect.Advance(age,mouth);
        Vector3 casterPosition=caster.transform.position,victimPosition=victim.transform.position;
        Object[] owned=OwnedGraphics();
        Texture atlas=effect.UpperJaw.sprite.texture;
        Assert.That(atlas,Is.SameAs(Resources.Load<Texture2D>(HeadResource)));
        CombatEffectEndPlayback.Begin(effect.gameObject,EffectTypes);effect.Cancel(false);
        Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True);
        Assert.That(effect.enabled,Is.False);Assert.That(effect.gameObject.activeSelf,Is.True);
        if(deleteCaster)Object.DestroyImmediate(caster);

        float afterSnap=Mathf.Max(age+.02f,SabertoothDevourRules.HitTime+.02f);
        Advance(afterSnap-age);
        Assert.That(effect!=null&&effect.gameObject.activeSelf,Is.True);
        Assert.That(effect.Age,Is.EqualTo(afterSnap).Within(.0001f));
        Assert.That(effect.Mouth,Is.EqualTo(mouth));
        Assert.That(effect.UpperJaw.enabled,Is.True);Assert.That(effect.LowerJaw.enabled,Is.True);
        var particles=new ParticleSystem.Particle[512];
        int count=effect.Particles.GetParticles(particles);
        Assert.That(count,Is.GreaterThan(0),"The preserved visual must still reach the final bite burst.");
        int gold=0;
        for(int i=0;i<count;i++)
            if(particles[i].startColor.a>0&&particles[i].startColor.r>particles[i].startColor.b+30)gold++;
        Assert.That(gold,Is.GreaterThan(count/2));
        Assert.That(victim.transform.position,Is.EqualTo(victimPosition));
        if(!deleteCaster)Assert.That(caster.transform.position,Is.EqualTo(casterPosition));

        float beforeEnd=SabertoothDevourRules.Duration-.03f;
        Advance(beforeEnd-afterSnap);
        Assert.That(effect!=null&&effect.gameObject.activeSelf,Is.True,
            "The remaining recovery must finish on its own clock instead of being cut short by cancellation.");
        Assert.That(effect.Age,Is.EqualTo(beforeEnd).Within(.0001f));
        Assert.That(effect.UpperJaw.enabled,Is.True);
        Advance(.04f);
        Assert.That(effect==null,Is.True);
        foreach(Object asset in owned)Assert.That(asset==null,Is.True);
        Assert.That(atlas!=null,Is.True,"Finishing one cast must not destroy its shared authored head atlas.");
        Assert.That(sprite!=null&&texture!=null,Is.True,"Only the effect's own graphics may be released.");
        CombatEffectEndPlayback.Clear(scene);AssertSceneHasNoPlayback();
    }

    [Test]
    public void TheNextWaveCanClearAnUnfinishedPreservedDevourAndReleaseItsGraphics()
    {
        effect.Advance(1.85f,mouth);Object[] owned=OwnedGraphics();
        Texture atlas=effect.UpperJaw.sprite.texture;
        CombatEffectEndPlayback.Begin(effect.gameObject,EffectTypes);effect.Cancel(false);
        Advance(.03f);Assert.That(effect!=null&&effect.gameObject.activeSelf,Is.True);
        CombatEffectEndPlayback.Clear(scene);
        Assert.That(effect==null,Is.True);
        foreach(Object asset in owned)Assert.That(asset==null,Is.True);
        Assert.That(atlas!=null,Is.True,"Wave reset must release cast-owned graphics while preserving the shared atlas.");
        Assert.That(sprite!=null&&texture!=null,Is.True);AssertSceneHasNoPlayback();
    }

    private Object[] OwnedGraphics()
    {
        Material headMaterial=effect.UpperJaw.sharedMaterial;
        Material particleMaterial=effect.Particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        return new Object[]{effect.UpperJaw.sprite,effect.LowerJaw.sprite,
            headMaterial,particleMaterial,particleMaterial.mainTexture};
    }

    private void Advance(float dt)
    {
        var playback=effect.GetComponent<CombatEffectEndPlayback>();Assert.That(playback,Is.Not.Null);
        typeof(CombatEffectEndPlayback).GetMethod("Advance",Private).Invoke(playback,new object[]{dt});
    }

    private void AssertSceneHasNoPlayback()
    {
        foreach(var root in scene.GetRootGameObjects())
        {
            Assert.That(root.GetComponentsInChildren<CombatEffectEndPlayback>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<PixelSabertoothDevour>(true),Is.Empty);
        }
    }

    private GameObject Go(string name)
    {
        var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;
    }
}

