using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public sealed class CombatEffectEndFadeTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly List<GameObject> objects=new List<GameObject>();
    private Scene scene;
    private UnityEngine.Random.State randomState;
    private float timeScale,captureDeltaTime;
    private Stack<PixelCombatEffect> pool;
    private PixelCombatEffect[] savedPool;

    [SetUp] public void Setup()
    {
        randomState=UnityEngine.Random.state;
        timeScale=Time.timeScale;captureDeltaTime=Time.captureDeltaTime;
        scene=EditorSceneManager.NewPreviewScene();
        pool=(Stack<PixelCombatEffect>)typeof(PixelCombatEffect)
            .GetField("AvailableEffects",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        savedPool=pool.ToArray();pool.Clear();
    }

    [TearDown] public void Cleanup()
    {
        foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();
        if(pool!=null&&savedPool!=null)
        {
            pool.Clear();
            for(int i=savedPool.Length-1;i>=0;i--)pool.Push(savedPool[i]);
        }
        if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.ClosePreviewScene(scene);
        UnityEngine.Random.state=randomState;
        Time.timeScale=timeScale;Time.captureDeltaTime=captureDeltaTime;
    }

    [Test] public void FadePreservesCurrentOpacityAndStopsEffectUpdates()
    {
        var root=Go("Skill visual");
        var sprite=root.AddComponent<SpriteRenderer>();sprite.color=new Color(.2f,.4f,.6f,.8f);
        var effect=root.AddComponent<PixelStarProjectile>();
        var line=Go("Skill line").AddComponent<LineRenderer>();line.transform.SetParent(root.transform);
        line.startColor=new Color(1f,1f,1f,.6f);line.endColor=new Color(1f,1f,1f,.2f);

        CombatEffectEndFade.Begin(root);
        CombatEffectEndFade.Begin(root);
        Assert.That(root.GetComponents<CombatEffectEndFade>().Length,Is.EqualTo(1));
        Assert.That(effect.enabled,Is.False,"Combat callbacks must not continue during a visual tail.");
        Assert.That(sprite.color.a,Is.EqualTo(.8f).Within(.001f));
        Time.timeScale=0f;
        Advance(root,CombatEffectEndFade.Duration*.5f);

        Assert.That(sprite.color.a,Is.EqualTo(.4f).Within(.001f));
        Assert.That(sprite.color.r,Is.EqualTo(.2f).Within(.001f));
        Assert.That(line.startColor.a,Is.EqualTo(.3f).Within(1f/255f));
        Assert.That(line.endColor.a,Is.EqualTo(.1f).Within(1f/255f));
    }

    [Test] public void StoppedManualParticlesRemainVisibleThenFade()
    {
        var root=Go("Manual gas");var particles=root.AddComponent<ParticleSystem>();
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.simulationSpeed=0f;main.startLifetime=10f;
        main.stopAction=ParticleSystemStopAction.Destroy;
        particles.Play();particles.Emit(new ParticleSystem.EmitParams{startColor=Color.white},3);
        var live=new ParticleSystem.Particle[3];

        CombatEffectEndFade.Begin(root);
        Assert.That(particles.isEmitting,Is.False);
        Assert.That(particles.particleCount,Is.EqualTo(3),"Stopping emission must not clear live particles.");
        Assert.That(particles.main.stopAction,Is.EqualTo(ParticleSystemStopAction.None));
        Advance(root,CombatEffectEndFade.Duration*.5f);

        Assert.That(particles.GetParticles(live),Is.EqualTo(3));
        Assert.That((int)live[0].startColor.a,Is.InRange(126,129));
        Assert.That(live[0].remainingLifetime,Is.GreaterThan(0f));
    }

    [Test] public void GasCleanupKeepsItsLiveCloudUntilFadeCompletes()
    {
        var gas=PixelMushroomGas.Play(Vector3.zero,2f,5f,Color.green,null);
        objects.Add(gas.gameObject);gas.Pose(1f);
        var cloud=gas.GetComponentInChildren<ParticleSystem>();
        int count=cloud.particleCount;
        CombatEffectEndFade.Begin(gas.gameObject);gas.Cancel();

        Assert.That(gas!=null,Is.True);
        Assert.That(gas.enabled,Is.False);
        Assert.That(cloud.particleCount,Is.EqualTo(count));
        Advance(gas.gameObject,CombatEffectEndFade.Duration);
        Assert.That(gas==null,Is.True);
    }

    [Test] public void GuardianCleanupCannotCutOffTheFade()
    {
        var source=Go("Caster").AddComponent<SpriteRenderer>();
        float clock=0f;bool valid=true;int blessings=0;
        var guardian=PixelSpiritGuardian.Create(source,new Bounds(Vector3.zero,Vector3.one*10f),true,
            ()=>clock,()=>valid,0f,()=>blessings++);
        Assert.That(guardian,Is.Not.Null);objects.Add(guardian.gameObject);
        typeof(PixelSpiritGuardian).GetMethod("Draw",Private).Invoke(guardian,new object[]{2f});
        var body=guardian.GetComponentInChildren<SpriteRenderer>();float opacity=body.color.a;
        CombatEffectEndFade.Begin(guardian.gameObject);
        clock=10f;valid=false;guardian.Cancel();
        Advance(guardian.gameObject,CombatEffectEndFade.Duration*.5f);

        Assert.That(guardian!=null,Is.True);
        Assert.That(guardian.enabled,Is.False);
        Assert.That(blessings,Is.EqualTo(0));
        Assert.That(body.color.a,Is.EqualTo(opacity*.5f).Within(.001f));
    }

    [Test] public void CompletedFadeDestroysNonPooledVisuals()
    {
        var root=Go("Temporary visual");root.AddComponent<SpriteRenderer>();
        CombatEffectEndFade.Begin(root);
        Advance(root,CombatEffectEndFade.Duration);
        Assert.That(root==null,Is.True);
    }

    [Test] public void MeshFadePreservesSharedMaterialAndExistingShaderProperties()
    {
        var root=Go("Mesh shield");var renderer=root.AddComponent<MeshRenderer>();
        var material=new Material(Shader.Find("Sprites/Default"));
        try
        {
            renderer.sharedMaterial=material;
            var properties=new MaterialPropertyBlock();
            properties.SetColor("_Color",new Color(.2f,.4f,.6f,.8f));
            properties.SetFloat("_TestExistingProperty",.73f);renderer.SetPropertyBlock(properties);
            CombatEffectEndFade.Begin(root);Advance(root,CombatEffectEndFade.Duration*.5f);
            renderer.GetPropertyBlock(properties);

            Assert.That(properties.GetColor("_Color").a,Is.EqualTo(.4f).Within(.001f));
            Assert.That(properties.GetFloat("_TestExistingProperty"),Is.EqualTo(.73f).Within(.001f));
            Assert.That(renderer.sharedMaterial,Is.SameAs(material));
            Assert.That(material.color.a,Is.EqualTo(1f).Within(.001f));
        }
        finally{Object.DestroyImmediate(material);}
    }

    [Test] public void CompletedFadeReturnsPooledEffectReadyForAnotherHit()
    {
        var source=Go("Hit target").AddComponent<SpriteRenderer>();
        var effect=PixelCombatEffect.PlayWolfHuntImpact(source.transform,source,Vector3.right,Color.white,false);
        objects.Add(effect.gameObject);
        CombatEffectEndFade.Begin(effect.gameObject);
        Advance(effect.gameObject,CombatEffectEndFade.Duration);
        Assert.That(effect!=null,Is.True);
        Assert.That(effect.gameObject.activeSelf,Is.False);

        var reused=PixelCombatEffect.PlayWolfHuntImpact(source.transform,source,Vector3.right,Color.white,false);
        Assert.That(reused,Is.SameAs(effect));
        Assert.That(reused.enabled,Is.True);
        Assert.That(reused.gameObject.activeSelf,Is.True);
        Assert.That(CombatEffectEndFade.IsFading(reused),Is.False);
        Assert.That(reused.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(1f).Within(.001f));
    }

    [Test] public void BattleEndSelectsCombatVisualsWithoutFadingActorsOrEnvironment()
    {
        var host=Go("Battle host");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var body=Go("Actor").AddComponent<SpriteRenderer>();
        var temporaryModel=Go("Temporary summoned actor");
        var spirit=Go("Summoned spirit visual").AddComponent<SpriteRenderer>();
        spirit.transform.SetParent(temporaryModel.transform);
        spirit.gameObject.AddComponent<PixelSpiritConcert>();
        var environment=Go("Environment").AddComponent<SpriteRenderer>();
        var aura=Go("Permanent rank aura");aura.AddComponent<PixelTranscendenceFlameOutline>();
        var climax=Go("Temporary climax flames");climax.AddComponent<CombatEffectEndMarker>();
        climax.AddComponent<PixelTranscendenceFlameOutline>();
        var rankParticles=Go("Permanent rank particles").AddComponent<ParticleSystem>();
        rankParticles.gameObject.AddComponent<CombatEffectEndMarker>();
        var returnParticles=Go("Equipped return mark").AddComponent<ParticleSystem>();
        returnParticles.gameObject.AddComponent<CombatEffectEndMarker>();
        var unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        var unit=Activator.CreateInstance(unitType,true);
        unitType.GetField("transcendenceFlameParticles").SetValue(unit,rankParticles);
        unitType.GetField("returnMarkAura").SetValue(unit,returnParticles);
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(unit);
        var effect=Go("Combat particle");effect.AddComponent<CombatEffectEndMarker>();
        effect.transform.SetParent(body.transform);
        var nested=Go("Nested visual");nested.transform.SetParent(effect.transform);
        nested.AddComponent<CombatEffectEndMarker>();

        typeof(MonsterAutoBattlePrototype).GetMethod("FadeBattleEffects",Private).Invoke(battle,null);

        Assert.That(CombatEffectEndFade.IsFading(effect.transform),Is.True);
        Assert.That(CombatEffectEndFade.IsFading(nested.transform),Is.True);
        Assert.That(nested.GetComponent<CombatEffectEndFade>(),Is.Null,"Nested visuals share their parent's fade.");
        Assert.That(effect.transform.parent,Is.Null,"Actor cleanup must not destroy an effect's visual tail.");
        Assert.That(CombatEffectEndFade.IsFading(spirit),Is.True);
        Assert.That(spirit.transform.parent,Is.Null,"Summon model removal must preserve its fading visual.");
        Assert.That(CombatEffectEndFade.IsFading(body),Is.False);
        Assert.That(CombatEffectEndFade.IsFading(environment),Is.False);
        Assert.That(CombatEffectEndFade.IsFading(aura.transform),Is.False);
        Assert.That(CombatEffectEndFade.IsFading(climax.transform),Is.True);
        Assert.That(CombatEffectEndFade.IsFading(rankParticles),Is.False);
        Assert.That(CombatEffectEndFade.IsFading(returnParticles),Is.False);
    }

    private GameObject Go(string name)
    {
        var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);objects.Add(go);return go;
    }
    private static void Advance(GameObject root,float delta)
    {
        var fade=root.GetComponent<CombatEffectEndFade>();
        typeof(CombatEffectEndFade).GetMethod("Advance",Private).Invoke(fade,new object[]{delta});
    }
}
