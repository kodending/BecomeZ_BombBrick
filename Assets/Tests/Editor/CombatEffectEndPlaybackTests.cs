using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public sealed class CombatEffectEndPlaybackTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly ISet<Type> ProjectileTypes=new HashSet<Type>{typeof(PixelStarProjectile)};
    private HashSet<GameObject> existing;
    private Scene scene,otherScene;
    private UnityEngine.Random.State randomState;
    private Stack<PixelCombatEffect> pool;
    private PixelCombatEffect[] savedPool;

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        randomState=UnityEngine.Random.state;
        scene=EditorSceneManager.NewPreviewScene();
        otherScene=EditorSceneManager.NewPreviewScene();
        pool=(Stack<PixelCombatEffect>)typeof(PixelCombatEffect)
            .GetField("AvailableEffects",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        savedPool=pool.ToArray();pool.Clear();
    }

    [TearDown] public void Cleanup()
    {
        CombatEffectEndPlayback.Clear(scene);CombatEffectEndPlayback.Clear(otherScene);
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)
                Object.DestroyImmediate(go);
        pool.Clear();
        for(int i=savedPool.Length-1;i>=0;i--)pool.Push(savedPool[i]);
        if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.ClosePreviewScene(scene);
        if(otherScene.IsValid()&&otherScene.isLoaded)EditorSceneManager.ClosePreviewScene(otherScene);
        UnityEngine.Random.state=randomState;
    }

    [Test] public void ReleasedProjectileCompletesItsPathWithoutCombatCallbacksOrOriginalValidity()
    {
        int callbacks=0,finished=0;bool completed=false;
        var projectile=Projectile(scene,()=>callbacks++);
        var flight=new CombatProjectileSource.Flight
        {
            clock=()=>7f,valid=()=>false,
            deflect=(from,to,body)=>{callbacks++;return true;},
            finished=()=>{if(!completed){completed=true;finished++;}}
        };
        typeof(PixelStarProjectile).GetField("flight",Private).SetValue(projectile,flight);
        typeof(PixelStarProjectile).GetField("flightClock",Private).SetValue(projectile,7f);
        typeof(PixelStarProjectile).GetField("concertValid",Private).SetValue(projectile,(Func<bool>)(()=>false));
        projectile.OnDeflected=()=>callbacks++;
        Vector3 start=projectile.transform.GetChild(0).position;
        CombatEffectEndPlayback.Begin(projectile.gameObject,ProjectileTypes);
        projectile.Cancel();

        Assert.That(projectile!=null&&projectile.gameObject.activeSelf,Is.True,
            "Battle cleanup must not cut off a preserved visual.");
        Assert.That(projectile.enabled,Is.False);
        Advance(projectile.gameObject,.1f);
        Assert.That(projectile!=null,Is.True);
        Assert.That(projectile.transform.GetChild(0).position,Is.Not.EqualTo(start));
        Assert.That(callbacks,Is.Zero);
        Advance(projectile.gameObject,1f);
        Assert.That(projectile==null||!projectile.gameObject.activeSelf,Is.True);
        Assert.That(callbacks,Is.Zero,"Arrival and deflection damage callbacks must stay disarmed.");
        Assert.That(finished,Is.EqualTo(1),"Bookkeeping still receives one completion.");
    }

    [Test] public void BeginIsIdempotentAndDoesNotAddAFade()
    {
        var projectile=Projectile(scene);
        CombatEffectEndPlayback.Begin(projectile.gameObject,ProjectileTypes);
        CombatEffectEndPlayback.Begin(projectile.gameObject,ProjectileTypes);
        Assert.That(projectile.GetComponents<CombatEffectEndPlayback>().Length,Is.EqualTo(1));
        Assert.That(projectile.GetComponent<CombatEffectEndFade>(),Is.Null);
        Assert.That(CombatEffectEndPlayback.IsPreserving(projectile.transform.GetChild(0)),Is.True);
    }

    [Test] public void PreservedLocalSpaceDoesNotFollowAnActorsLaterMovement()
    {
        var ancestor=Go("Scaled actor root",scene).transform;
        ancestor.position=new Vector3(7f,2f,-3f);
        ancestor.rotation=Quaternion.Euler(15f,28f,9f);
        ancestor.localScale=new Vector3(2f,1.4f,.8f);
        var parent=Go("Actor effect attachment",scene).transform;parent.SetParent(ancestor,false);
        parent.localPosition=new Vector3(1f,.5f,-.4f);
        parent.localRotation=Quaternion.Euler(13f,22f,4f);
        parent.localScale=new Vector3(.9f,1.1f,.7f);
        var projectile=Projectile(scene);
        projectile.transform.SetParent(parent,false);
        projectile.transform.localPosition=new Vector3(.7f,1.3f,-.2f);
        projectile.transform.localRotation=Quaternion.Euler(5f,17f,3f);
        projectile.transform.localScale=new Vector3(.7f,.8f,.9f);
        Vector3 local=projectile.transform.localPosition,world=projectile.transform.position;
        Quaternion rotation=projectile.transform.rotation;
        Vector3 scale=projectile.transform.lossyScale;

        CombatEffectEndPlayback.Begin(projectile.gameObject,ProjectileTypes);
        Assert.That(projectile.transform.parent,Is.Not.SameAs(parent));
        Assert.That(Vector3.Distance(projectile.transform.localPosition,local),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(projectile.transform.position,world),Is.LessThan(.0001f));
        Assert.That(Quaternion.Angle(projectile.transform.rotation,rotation),Is.LessThan(.001f));
        Assert.That(Vector3.Distance(projectile.transform.lossyScale,scale),Is.LessThan(.0001f));
        ancestor.position+=Vector3.right*20f;parent.localScale=Vector3.one*4f;
        Assert.That(Vector3.Distance(projectile.transform.position,world),Is.LessThan(.0001f));
    }

    [Test] public void ClearOnlyRemovesPlaybackOwnedByTheRequestedScene()
    {
        var first=Projectile(scene);var second=Projectile(otherScene);
        CombatEffectEndPlayback.Begin(first.gameObject,ProjectileTypes);
        CombatEffectEndPlayback.Begin(second.gameObject,ProjectileTypes);
        CombatEffectEndPlayback.Clear(scene);
        Assert.That(first==null||!first.gameObject.activeSelf,Is.True);
        Assert.That(second!=null&&second.gameObject.activeSelf,Is.True);
        Assert.That(CombatEffectEndPlayback.IsPreserving(second),Is.True);
        CombatEffectEndPlayback.Clear(otherScene);
        Assert.That(second==null||!second.gameObject.activeSelf,Is.True);
    }

    [Test] public void ExplicitClearReturnsAPooledEffectReadyForTheNextBattle()
    {
        var source=Go("Impact target",scene).AddComponent<SpriteRenderer>();
        var effect=PixelCombatEffect.PlayWolfHuntImpact(source.transform,source,Vector3.right,Color.white,false);
        SceneManager.MoveGameObjectToScene(effect.gameObject,scene);
        CombatEffectEndPlayback.Begin(effect.gameObject,new HashSet<Type>{typeof(PixelCombatEffect)});
        Assert.That(effect.enabled,Is.False);
        CombatEffectEndPlayback.Clear(scene);
        Assert.That(effect!=null,Is.True);
        Assert.That(effect.gameObject.activeSelf,Is.False);
        Assert.That(pool.Contains(effect),Is.True);

        var reused=PixelCombatEffect.PlayWolfHuntImpact(source.transform,source,Vector3.right,Color.white,false);
        Assert.That(reused,Is.SameAs(effect));
        Assert.That(reused.gameObject.activeSelf,Is.True);
        Assert.That(reused.enabled,Is.True);
        Assert.That(CombatEffectEndPlayback.IsPreserving(reused),Is.False);
        Assert.That(reused.GetComponent<CombatEffectEndPlayback>(),Is.Null);
        Assert.That(reused.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(1f).Within(.001f));
    }

    [Test] public void NaturalSpriteCompletionReturnsAPooledEffectWithoutAWaitOrFade()
    {
        var source=Go("Impact target",scene).AddComponent<SpriteRenderer>();
        var effect=PixelCombatEffect.PlayWolfHuntImpact(source.transform,source,Vector3.right,Color.white,false);
        SceneManager.MoveGameObjectToScene(effect.gameObject,scene);
        CombatEffectEndPlayback.Begin(effect.gameObject,new HashSet<Type>{typeof(PixelCombatEffect)});
        Advance(effect.gameObject,2f);
        Assert.That(effect!=null,Is.True);
        Assert.That(effect.gameObject.activeSelf,Is.False);
        Assert.That(pool.Contains(effect),Is.True);
    }

    [Test] public void NestedPooledImpactReturnsEnabledAndCannotBeDrivenByItsFormerParent()
    {
        var root=Go("Parent impact particles",scene);root.AddComponent<CombatEffectEndMarker>();
        var source=Go("Impact target",scene).AddComponent<SpriteRenderer>();
        var effect=PixelCombatEffect.PlayWolfHuntImpact(source.transform,source,Vector3.right,Color.white,false);
        SceneManager.MoveGameObjectToScene(effect.gameObject,scene);effect.transform.SetParent(root.transform,true);
        CombatEffectEndPlayback.Begin(root,new HashSet<Type>{typeof(CombatEffectEndMarker),typeof(PixelCombatEffect)});
        Assert.That(effect.enabled,Is.False);
        Advance(root,2f);
        Assert.That(effect!=null,Is.True);
        Assert.That(effect.gameObject.activeSelf,Is.False);
        Assert.That(pool.Contains(effect),Is.True);
        Assert.That(effect.transform.parent,Is.Null);

        var reused=PixelCombatEffect.PlayWolfHuntImpact(source.transform,source,Vector3.right,Color.white,false);
        Assert.That(reused,Is.SameAs(effect));
        Assert.That(reused.enabled,Is.True);
        Assert.That(CombatEffectEndPlayback.IsPreserving(reused),Is.False);
        if(root!=null&&root.GetComponent<CombatEffectEndPlayback>()!=null)Advance(root,2f);
        Assert.That(reused.gameObject.activeSelf,Is.True,"An old playback cannot consume a reused pooled impact.");
        Assert.That(reused.enabled,Is.True);
    }

    [TestCase(0)] [TestCase(1)]
    public void IntermediateKingFruitFinishesLandingWithoutCallingTheOldVolley(int stage)
    {
        var source=Go("King fruit source",scene).AddComponent<SpriteRenderer>();
        source.sprite=PixelStarProjectile.BeanSprite;
        int arrivals=0;
        var fruit=PixelStarProjectile.PlayKingFruit(Vector3.up*3f,Vector3.right*4f,
            source,Color.yellow,stage,()=>arrivals++);
        SceneManager.MoveGameObjectToScene(fruit.gameObject,scene);
        CombatEffectEndPlayback.Begin(fruit.gameObject,ProjectileTypes);
        source.gameObject.SetActive(false);
        Advance(fruit.gameObject,.1f);
        Assert.That(fruit!=null&&fruit.gameObject.activeSelf,Is.True);
        for(int i=0;i<30&&fruit!=null&&fruit.gameObject.activeSelf;i++)Advance(fruit.gameObject,.1f);
        Assert.That(arrivals,Is.Zero,"The visual cannot resume the cancelled barrage or its damage.");
        Assert.That(fruit==null||!fruit.gameObject.activeSelf,Is.True,
            "An intermediate fruit cannot wait forever for its original coroutine to launch the next bounce.");
    }

    [TestCase(false)] [TestCase(true)]
    public void WolfCompanionAndRushFinishAfterTheirOwnerReturnsToIdle(bool rushing)
    {
        var model=Monster(scene,"nukgu");
        var source=model.GetComponentInChildren<SpriteRenderer>(true);
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(source,Vector3.left,Vector3.right,Color.cyan,.18f);
        SceneManager.MoveGameObjectToScene(ghost.gameObject,scene);
        var effect=ghost.GetComponent<PixelWolfHuntAfterimage>();
        if(rushing)
        {
            effect.PrepareAmbush(source,()=>true,driver.GetSkillAttackFrame(2),driver);
            effect.LaunchRush(Vector3.right);
        }
        else
        {
            effect.PrepareCompanion(source,driver,()=>Vector3.left,()=>true,Color.cyan);
            driver.PlayBasicAttack(1f,null);effect.StrikeCompanion(source,0f,1f);
        }
        CombatEffectEndPlayback.Begin(ghost.gameObject,new HashSet<Type>{typeof(PixelWolfHuntAfterimage)});
        driver.ResetForReuse();model.SetActive(false);
        Advance(ghost.gameObject,.05f);
        Assert.That(effect!=null&&effect.gameObject.activeSelf,Is.True,
            "Resetting the owner must not cut off its already-visible spirit.");
        for(int i=0;i<60&&effect!=null&&effect.gameObject.activeSelf;i++)Advance(effect.gameObject,.1f);
        Assert.That(effect==null||!effect.gameObject.activeSelf,Is.True,
            "A companion or rush must leave its sustained phase when combat has ended.");
    }

    [Test] public void PuppetUnravelsAfterItsActorDriverIsReset()
    {
        var owner=Go("Summoned actor owner",scene);
        var model=Monster(scene,"aranea");model.transform.SetParent(owner.transform,true);
        var body=model.GetComponentInChildren<SpriteRenderer>(true);
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var puppet=PixelSilkPuppet.Create(body,()=>true);
        CombatEffectEndPlayback.Begin(puppet.gameObject,
            new HashSet<Type>{typeof(PixelSilkPuppet),typeof(PixelBearAvatarDeparture)});
        driver.ResetForReuse();owner.SetActive(false);
        foreach(var playback in Resources.FindObjectsOfTypeAll<CombatEffectEndPlayback>())
            if(playback.gameObject.scene==scene)Advance(playback.gameObject,.05f);
        bool visiblePuppet=puppet!=null&&puppet.gameObject.activeInHierarchy;
        bool visibleSoul=Array.Exists(Resources.FindObjectsOfTypeAll<PixelBearAvatarDeparture>(),
            departure=>departure.gameObject.scene==scene);
        Assert.That(visiblePuppet||visibleSoul,Is.True,"Actor cleanup must retain the puppet's unravelling visual.");
        for(int i=0;i<60;i++)
        foreach(var playback in Resources.FindObjectsOfTypeAll<CombatEffectEndPlayback>())
            if(playback.gameObject.scene==scene)Advance(playback.gameObject,.1f);
        Assert.That(puppet==null||!puppet.gameObject.activeSelf,Is.True);
        Assert.That(Array.FindAll(Resources.FindObjectsOfTypeAll<PixelBearAvatarDeparture>(),
            departure=>departure.gameObject.scene==scene),Is.Empty,"Unravelling must also finish its detached soul.");
    }

    [Test] public void BearRoarSpiritCompletesAfterTheSourceSkillAnimationIsReset()
    {
        var model=Monster(scene,"berber");
        var source=model.GetComponentInChildren<SpriteRenderer>(true);
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        driver.PlaySkillAttack(1f,null);
        var spirit=PixelCombatEffect.PlayBearKingRoarSpirit(model.transform,source);
        Assert.That(spirit,Is.Not.Null);
        var effect=spirit.GetComponent<PixelBearKingRoarSpirit>();
        typeof(PixelBearKingRoarSpirit).GetMethod("LateUpdate",Private).Invoke(effect,null);
        CombatEffectEndPlayback.Begin(spirit.gameObject,new HashSet<Type>{typeof(PixelBearKingRoarSpirit)});
        driver.ResetForReuse();model.SetActive(false);
        Advance(spirit.gameObject,.05f);
        Assert.That(effect!=null&&effect.gameObject.activeInHierarchy,Is.True,
            "The source returning to Idle must not skip the remaining roar spirit frames.");
        for(int i=0;i<60&&effect!=null&&effect.gameObject.activeSelf;i++)Advance(effect.gameObject,.1f);
        Assert.That(effect==null||!effect.gameObject.activeSelf,Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void StoppedWyvernEmissionDrainsFromTheMouthAndKeepsItsLastAnchor(bool overheat)
    {
        SpriteRenderer source;
        var breath=ReleasedBreath(overheat,out source);
        var before=VisibleBreathParticles(breath).Select(p=>p.position).ToArray();
        Assert.That(before.Length,Is.GreaterThan(0));
        float transit=overheat?.22f:.16f;
        breath.StopEmission();float endAge=breath.EndAge;
        Assert.That(endAge,Is.EqualTo(.7f+transit+.01f).Within(.00001f));

        source.transform.position+=Vector3.up*100f;source.gameObject.SetActive(false);
        breath.Pose(.7f,Vector3.right);
        CollectionAssert.AreEqual(before,VisibleBreathParticles(breath).Select(p=>p.position).ToArray(),
            "The dying caster's changed bounds cannot drag the emitted stream away from its last mouth position.");
        breath.Pose(.7f+transit*.5f,Vector3.right);
        var remaining=VisibleBreathParticles(breath);
        Assert.That(remaining.Length,Is.GreaterThan(0),"The end of the stream must remain while the mouth clears.");
        Assert.That(remaining.Min(p=>p.position.x),Is.GreaterThan(before.Min(p=>p.x)+1f),
            "No new particles may fill the mouth; the trailing edge must travel toward the far end.");
        breath.StopEmission();
        Assert.That(breath.EndAge,Is.EqualTo(endAge),"Repeated death cleanup must not restart the tail.");
        breath.Pose(endAge+.01f,Vector3.right);
        Assert.That(breath==null||!breath.gameObject.activeSelf,Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void InterruptedWyvernPlaybackHonorsItsTailEndInsteadOfTheFullChannel(bool overheat)
    {
        SpriteRenderer source;
        var breath=ReleasedBreath(overheat,out source);
        breath.StopEmission();float transit=overheat?.22f:.16f;
        CombatEffectEndPlayback.Begin(breath.gameObject,new HashSet<Type>{typeof(PixelWyvernBreath)});
        source.gameObject.SetActive(false);
        Advance(breath.gameObject,transit*.5f);
        Assert.That(breath!=null&&breath.gameObject.activeSelf,Is.True);
        Assert.That(VisibleBreathParticles(breath).Length,Is.GreaterThan(0));
        Advance(breath.gameObject,transit+.05f);
        Assert.That(breath==null||!breath.gameObject.activeSelf,Is.True,
            "Result playback must respect the stopped emission's short remaining lifetime.");
    }

    [Test] public void WaveResetCanClearAnInterruptedWyvernTailImmediately()
    {
        SpriteRenderer source;
        var breath=ReleasedBreath(false,out source);breath.StopEmission();
        CombatEffectEndPlayback.Begin(breath.gameObject,new HashSet<Type>{typeof(PixelWyvernBreath)});
        Advance(breath.gameObject,.02f);
        Assert.That(breath!=null&&breath.gameObject.activeSelf,Is.True);
        CombatEffectEndPlayback.Clear(scene);
        Assert.That(breath==null||!breath.gameObject.activeSelf,Is.True);
        Assert.That(Array.FindAll(Resources.FindObjectsOfTypeAll<CombatEffectEndPlayback>(),
            playback=>playback.gameObject.scene==scene),Is.Empty);
    }

    [Test] public void StoppingRainMouthEmissionKeepsTheFallingBreathParticles()
    {
        var source=Go("Rain caster",scene).AddComponent<SpriteRenderer>();
        source.sprite=PixelStarProjectile.BeanSprite;
        var rain=PixelWyvernRain.Create(source,Vector3.zero,Vector3.right,
            new[]{Vector3.right*4f},new[]{Vector3.up*8f},new[]{false},1f,0,()=>0f,()=>true);
        SceneManager.MoveGameObjectToScene(rain.gameObject,scene);
        foreach(var system in rain.GetComponentsInChildren<ParticleSystem>())system.Play();
        var body=rain.GetComponentsInChildren<ParticleSystem>(true)
            .Single(system=>system.name=="Falling Breath Clouds");
        var particles=new ParticleSystem.Particle[body.main.maxParticles];

        rain.Pose(.45f);
        int count=body.GetParticles(particles);
        Assert.That(particles.Take(count).Any(particle=>particle.startColor.a>0),Is.True);
        rain.StopMouthEmission();
        Assert.That(body.GetParticles(particles),Is.Zero,
            "Stopping the mouth must immediately remove the upward exhale before any rain has fallen.");

        rain.Pose(WyvernRank3Rules.RainImpact(0)-WyvernRank3Rules.RainFall*.5f);
        count=body.GetParticles(particles);
        Assert.That(particles.Take(count).Any(particle=>particle.startColor.a>0),Is.True,
            "Stopping the mouth must not prevent the already-launched rain from falling.");
        var positions=particles.Take(count).Select(particle=>particle.position).ToArray();
        var colors=particles.Take(count).Select(particle=>particle.startColor).ToArray();
        rain.StopMouthEmission();
        Assert.That(body.GetParticles(particles),Is.EqualTo(count));
        CollectionAssert.AreEqual(positions,particles.Take(count).Select(particle=>particle.position).ToArray());
        CollectionAssert.AreEqual(colors,particles.Take(count).Select(particle=>particle.startColor).ToArray());
    }

    private PixelWyvernBreath ReleasedBreath(bool overheat,out SpriteRenderer source)
    {
        source=Go("Breath caster",scene).AddComponent<SpriteRenderer>();
        source.sprite=PixelStarProjectile.BeanSprite;
        var breath=PixelWyvernBreath.Create(source,Vector3.zero,Vector3.right,6.8f,1f,.4f,0,
            ()=>0f,()=>false,overheat);
        SceneManager.MoveGameObjectToScene(breath.gameObject,scene);
        foreach(var system in breath.GetComponentsInChildren<ParticleSystem>())system.Play();
        breath.Pose(.7f,Vector3.right);return breath;
    }

    private static ParticleSystem.Particle[] VisibleBreathParticles(PixelWyvernBreath breath)
    {
        var body=breath.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="Breath Body");
        var particles=new ParticleSystem.Particle[body.main.maxParticles];
        int count=body.GetParticles(particles);
        return particles.Take(count).Where(p=>p.startColor.a>0).ToArray();
    }

    private static GameObject Monster(Scene owner,string id)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        Assert.That(definition,Is.Not.Null);
        var model=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        SceneManager.MoveGameObjectToScene(model,owner);return model;
    }

    private static void Advance(GameObject root,float dt)
    {
        var playback=root.GetComponent<CombatEffectEndPlayback>();
        Assert.That(playback,Is.Not.Null);
        typeof(CombatEffectEndPlayback).GetMethod("Advance",Private).Invoke(playback,new object[]{dt});
    }
    private static GameObject Go(string name,Scene owner)
    {
        var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,owner);return go;
    }
    private static PixelStarProjectile Projectile(Scene owner,Action arrived=null)
    {
        var source=Go("Visual projectile source",owner);
        var target=Go("Visual projectile target",owner);target.transform.position=Vector3.right*4f;
        var projectile=PixelStarProjectile.PlayBean(source.transform,null,target.transform,null,Color.white,false,1f,arrived);
        SceneManager.MoveGameObjectToScene(projectile.gameObject,owner);
        return projectile;
    }
}
