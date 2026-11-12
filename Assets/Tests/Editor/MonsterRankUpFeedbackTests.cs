using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public sealed class MonsterRankUpFeedbackTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=Hidden|BindingFlags.Public;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object unit;
    private ExpeditionRunSave run;
    private ExpeditionMonsterStateSave state;
    private SpriteRenderer renderer;
    private Sprite sprite;
    private Texture2D texture;
    private UnityEngine.Random.State random;
    private float timeScale;
    private object previousFrontend;
    private HashSet<MonsterRankUpFeedback> previousEffects;

    [SetUp] public void Setup()
    {
        random=UnityEngine.Random.state;timeScale=Time.timeScale;
        previousEffects=new HashSet<MonsterRankUpFeedback>(Resources.FindObjectsOfTypeAll<MonsterRankUpFeedback>());
        var frontend=typeof(GameFrontend).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        previousFrontend=frontend.GetValue(null);frontend.SetValue(null,null);
        fixture=new MonkeyArcTests();fixture.SetUp();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        unit=Get<object>(fixture,"source");run=Get<ExpeditionRunSave>(battle,"expeditionRun");
        run.runId="rank_up_feedback";run.wave=1;run.expeditionCurrency=100;
        state=new ExpeditionMonsterStateSave{instanceId=Get<string>(unit,"stableId"),monsterId="chiwoo",rank=ExpeditionMonsterRank.Rank1};
        run.monsters.Add(state);
        texture=new Texture2D(4,4);var pixels=new Color[16];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;
        texture.SetPixels(pixels);texture.Apply();sprite=Sprite.Create(texture,new Rect(0,0,4,4),Vector2.one*.5f,4f);
        renderer=Get<GameObject>(unit,"model").AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=8;
    }

    [TearDown] public void Cleanup()
    {
        try
        {
            foreach(var effect in Resources.FindObjectsOfTypeAll<MonsterRankUpFeedback>())
                if(effect!=null&&!previousEffects.Contains(effect)&&effect.gameObject.scene.IsValid())effect.Cancel();
            var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            if(sprite!=null)cache.Remove(sprite);
            fixture?.TearDown();
            if(sprite!=null)Object.DestroyImmediate(sprite);if(texture!=null)Object.DestroyImmediate(texture);
        }
        finally
        {
            typeof(GameFrontend).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,previousFrontend);
            UnityEngine.Random.state=random;Time.timeScale=timeScale;
        }
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void LoadedRanksAreBaselinesAndDisplayRefreshNeverReplaysEvolution(int rank)
    {
        state.rank=(ExpeditionMonsterRank)Mathf.Min(rank,3);state.transcended=rank==4;
        for(int i=0;i<4;i++)Observe();
        Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
        Assert.That(Pulses.Count,Is.Zero);
    }

    [Test] public void SuccessfulGrowthAndTranscendenceEachProduceOneEvolution()
    {
        state.growthTotal=2;Observe();
        Assert.That(ExpeditionGrowthSystem.TryPurchase(run,state.instanceId,out _,out _,out string error),Is.True,error);
        Assert.That(state.rank,Is.EqualTo(ExpeditionMonsterRank.Rank2));Observe();
        Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
        Assert.That(Get<float>(Pulses[state.instanceId],"startedAt"),Is.EqualTo(-1f));
        Present(10f);var rankTwo=OnlyWorldEffect(2);
        Assert.That(Get<float>(Pulses[state.instanceId],"startedAt"),Is.EqualTo(10f));
        state.growthTotal=8;run.wave++;run.expeditionCurrency=100;
        Assert.That(ExpeditionGrowthSystem.TryPurchase(run,state.instanceId,out _,out _,out error),Is.True,error);
        Assert.That(state.rank,Is.EqualTo(ExpeditionMonsterRank.Rank3));Observe();
        Assert.That(rankTwo==null||!rankTwo.gameObject.activeSelf,Is.True);
        Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
        Present(20f);var rankThree=OnlyWorldEffect(3);
        state.growthTotal=12;
        run.augments.Add(new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.TranscendenceGene,targetMonsterInstanceId=state.instanceId});
        Assert.That(ExpeditionAugmentRuntime.TryActivateTranscendence(run,state.instanceId),Is.True);Observe();
        Assert.That(rankThree==null||!rankThree.gameObject.activeSelf,Is.True);
        Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
        Present(30f);var rankFour=OnlyWorldEffect(4);
        Assert.That(Get<int>(Pulses[state.instanceId],"rank"),Is.EqualTo(4));
        Assert.That(ExpeditionAugmentRuntime.TryActivateTranscendence(run,state.instanceId),Is.False);
        for(int i=0;i<5;i++){Observe();Present(30f);}
        Assert.That(OnlyWorldEffect(4),Is.SameAs(rankFour));
    }

    [Test] public void FailedPurchaseAndEquipmentOnlyRefreshDoNotProduceEvolution()
    {
        Observe();run.growthShopFreePurchaseUsed=true;run.growthShopWave=run.wave;run.expeditionCurrency=0;
        Assert.That(ExpeditionGrowthSystem.TryPurchase(run,state.instanceId,out _,out _,out _),Is.False);
        for(int i=0;i<4;i++)Observe();
        Assert.That(Pulses.Count,Is.Zero);Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
    }

    [Test] public void StoredMonsterKeepsThePortraitPulseWithoutRequiringAnActiveWorldModel()
    {
        Observe();renderer.gameObject.SetActive(false);state.rank=ExpeditionMonsterRank.Rank2;
        string data=JsonUtility.ToJson(run);Observe();
        Assert.That(Pulses.Count,Is.EqualTo(1));Assert.That(Get<int>(Pulses[state.instanceId],"rank"),Is.EqualTo(2));
        Present(10f);Assert.That(Get<float>(Pulses[state.instanceId],"startedAt"),Is.EqualTo(-1f));
        Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
        Call(battle,"DrawMonsterRankUpFeedback",new Rect(0,0,64,64),state.instanceId);
        Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));Assert.That(Pulses.Count,Is.EqualTo(1));
        renderer.gameObject.SetActive(true);Present(1000f);OnlyWorldEffect(2);
        Assert.That(Get<float>(Pulses[state.instanceId],"startedAt"),Is.EqualTo(1000f));
    }

    [Test] public void ReloadResetAndNewRunDiscardOldRankNotifications()
    {
        Observe();state.rank=ExpeditionMonsterRank.Rank2;Observe();Present(10f);OnlyWorldEffect(2);
        Assert.That(Pulses.Count,Is.EqualTo(1));
        Call(battle,"ResetMonsterRankUpFeedback");Observe();Assert.That(Pulses.Count,Is.Zero);
        Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
        run.runId="new_rank_up_run";state.rank=ExpeditionMonsterRank.Rank3;Observe();
        Assert.That(Pulses.Count,Is.Zero);
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void AttachedEvolutionIsBoundedDeterministicAndDoesNotChangeTheMonster(int rank)
    {
        var root=renderer.transform;Vector3 position=root.position,scale=root.localScale;
        Quaternion rotation=root.localRotation;renderer.color=new Color(.4f,.6f,.8f,.75f);Color color=renderer.color;
        Material material=renderer.sharedMaterial;
        float hp=Get<float>(unit,"hp"),damage=Get<float>(unit,"damage"),radius=Get<float>(unit,"collisionRadius");
        int range=Get<int>(unit,"attackRange");string before=JsonUtility.ToJson(UnityEngine.Random.state);
        string data=JsonUtility.ToJson(run);object profile=Get<object>(battle,"playerProfile");
        string profileData=profile==null?null:JsonUtility.ToJson(profile);
        var effect=MonsterRankUpFeedback.Play(renderer,rank);Assert.That(effect,Is.Not.Null);
        Assert.That(renderer.forceRenderingOff,Is.True);Assert.That(effect.BodyRenderer,Is.Not.SameAs(renderer));
        Call(effect,"Advance",.08f);
        var ps=effect.GetComponent<ParticleSystem>();var samples=new ParticleSystem.Particle[ps.main.maxParticles];
        Assert.That(ps.main.maxParticles,Is.LessThanOrEqualTo(40));
        Assert.That(ps.GetParticles(samples),Is.InRange(1,samples.Length));Assert.That(ps.main.useUnscaledTime,Is.True);
        Assert.That(ps.GetComponent<ParticleSystemRenderer>().sortingOrder,Is.EqualTo(renderer.sortingOrder+25));
        Assert.That(effect.gameObject.scene,Is.EqualTo(renderer.gameObject.scene));
        Assert.That(renderer.sprite,Is.SameAs(sprite));Assert.That(root.position,Is.EqualTo(position));Assert.That(root.localScale,Is.EqualTo(scale));
        Assert.That(root.localRotation,Is.EqualTo(rotation));Assert.That(renderer.color,Is.EqualTo(color));
        Assert.That(renderer.sharedMaterial,Is.SameAs(material));
        Assert.That(renderer.sortingOrder,Is.EqualTo(8));
        Assert.That(Get<float>(unit,"hp"),Is.EqualTo(hp));Assert.That(Get<float>(unit,"damage"),Is.EqualTo(damage));
        Assert.That(Get<float>(unit,"collisionRadius"),Is.EqualTo(radius));Assert.That(Get<int>(unit,"attackRange"),Is.EqualTo(range));
        Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));
        Assert.That(profile==null?null:JsonUtility.ToJson(profile),Is.EqualTo(profileData));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(before));
        float impact=MonsterEvolutionCelebration.ImpactAt(rank);
        Assert.That(MonsterRankUpFeedback.Sample(8,impact*.25f,rank,out Vector2 first,out _,out _),Is.True);
        Assert.That(MonsterRankUpFeedback.Sample(8,impact*.9f,rank,out Vector2 gathered,out _,out _),Is.True);
        Assert.That(gathered.magnitude,Is.LessThan(first.magnitude),"Preparation gathers before the impact.");
        Assert.That(MonsterRankUpFeedback.Sample(8,impact+.02f,rank,out Vector2 contact,out _,out _),Is.True);
        Assert.That(MonsterRankUpFeedback.Sample(8,impact+.20f,rank,out Vector2 expanded,out float size,out Color tint),Is.True);
        Assert.That(expanded.magnitude,Is.GreaterThan(contact.magnitude),"The impact releases outward after preparation.");
        Assert.That(MonsterRankUpFeedback.Sample(8,impact+.20f,rank,out Vector2 repeat,out float repeatSize,out Color repeatTint),Is.True);
        Assert.That(repeat,Is.EqualTo(expanded));Assert.That(repeatSize,Is.EqualTo(size));Assert.That(repeatTint,Is.EqualTo(tint));
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void WorldEffectsPlayEveryAuthoredFrameAfterPreparation(int rank)
    {
        var effect=MonsterRankUpFeedback.Play(renderer,rank);var visuals=Get<SpriteRenderer[]>(effect,"sprites");
        var pulse=new HashSet<Sprite>();var lightning=new HashSet<Sprite>();var sparks=new HashSet<Sprite>();
        float impact=MonsterEvolutionCelebration.ImpactAt(rank);
        AdvanceTo(effect,impact-.001f);
        foreach(var visual in visuals)Assert.That(visual.enabled,Is.False,"Frame effects must wait for the impact.");
        var times=new List<float>();
        for(int i=0;i<8;i++)
        {
            times.Add(impact+(i+.25f)/24f);
            times.Add(impact+(i+.25f)/32f);
        }
        for(int i=0;i<6;i++)times.Add(impact+(i+.25f)/22f);
        times.Sort();
        foreach(float time in times)
        {
            AdvanceTo(effect,time);
            if(visuals[0].enabled)pulse.Add(visuals[0].sprite);
            if(visuals[1].enabled)lightning.Add(visuals[1].sprite);
            if(visuals[4].enabled)sparks.Add(visuals[4].sprite);
            if(rank==2)
                for(int i=1;i<=3;i++)Assert.That(visuals[i].enabled,Is.False,"Rank 2 has no lightning.");
        }
        Assert.That(pulse.Count,Is.EqualTo(8),"The shockwave must animate eight different frames.");
        Assert.That(sparks.Count,Is.EqualTo(6),"The sparkle must animate six different frames.");
        Assert.That(lightning.Count,Is.EqualTo(rank>=3?8:0));
        AdvanceTo(effect,impact+.45f);
        foreach(var visual in visuals)Assert.That(visual.enabled,Is.False,"Frame effects must finish before the afterglow.");
        var ps=effect.GetComponent<ParticleSystem>();
        Assert.That(ps.particleCount,Is.GreaterThan(0),"The local afterglow continues without replaying the impact.");
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void ParticlesAndAnimatedFramesFollowTheExistingMonsterWithoutMovingIt(int rank)
    {
        var effect=MonsterRankUpFeedback.Play(renderer,rank);
        AdvanceTo(effect,MonsterEvolutionCelebration.ImpactAt(rank)+.08f);
        var ps=effect.GetComponent<ParticleSystem>();var first=new ParticleSystem.Particle[ps.main.maxParticles];
        int count=ps.GetParticles(first);Assert.That(count,Is.GreaterThan(0));
        var visuals=Get<SpriteRenderer[]>(effect,"sprites");var positions=new Vector3[visuals.Length];
        for(int i=0;i<visuals.Length;i++)positions[i]=visuals[i].transform.position;
        Vector3 delta=new Vector3(2f,1f,-3f);renderer.transform.position+=delta;
        Vector3 moved=renderer.transform.position;Call(effect,"Advance",0f);
        var later=new ParticleSystem.Particle[first.Length];Assert.That(ps.GetParticles(later),Is.EqualTo(count));
        for(int i=0;i<count;i++)
            Assert.That(Vector3.Distance(later[i].position,first[i].position+delta),Is.LessThan(.0001f));
        for(int i=0;i<visuals.Length;i++)
            if(visuals[i].enabled)
                Assert.That(Vector3.Distance(visuals[i].transform.position,positions[i]+delta),Is.LessThan(.0001f));
        Assert.That(renderer.transform.position,Is.EqualTo(moved));Assert.That(renderer.sprite,Is.SameAs(sprite));
    }

    [TestCase(2,2f)] [TestCase(3,2.8f)] [TestCase(4,3.8f)]
    public void EvolutionLivesForItsRankDurationAndExpiresWhilePaused(int rank,float duration)
    {
        Time.timeScale=0f;var effect=MonsterRankUpFeedback.Play(renderer,rank);
        AdvanceTo(effect,duration-.001f);Assert.That(effect!=null&&effect.gameObject.activeSelf,Is.True);
        Call(effect,"Advance",.002f);Assert.That(effect==null||!effect.gameObject.activeSelf,Is.True);
        Assert.That(renderer.forceRenderingOff,Is.False);
        Assert.That(MonsterRankUpFeedback.Sample(8,duration,rank,out _,out _,out _),Is.False);
        Assert.That(Get<float>(unit,"hp"),Is.EqualTo(5000f));
    }

    [TestCase("inactive")] [TestCase("disabled")] [TestCase("missingSprite")] [TestCase("destroyed")]
    public void LosingTheSourceVisualCleansTheEntireEffect(string change)
    {
        var effect=MonsterRankUpFeedback.Play(renderer,4);AdvanceTo(effect,.4f);
        var visuals=Get<SpriteRenderer[]>(effect,"sprites");
        if(change=="inactive")renderer.gameObject.SetActive(false);
        else if(change=="disabled")renderer.enabled=false;
        else if(change=="missingSprite")renderer.sprite=null;
        else Object.DestroyImmediate(renderer.gameObject);
        Call(effect,"Advance",0f);
        Assert.That(effect==null||!effect.gameObject.activeSelf,Is.True);
        foreach(var visual in visuals)Assert.That(visual==null||!visual.gameObject.activeInHierarchy,Is.True);
        if(renderer!=null)
        {
            Assert.That(renderer.forceRenderingOff,Is.False);
            if(change=="inactive")Assert.That(renderer.gameObject.activeSelf,Is.False);
            if(change=="disabled")Assert.That(renderer.enabled,Is.False);
            if(change=="missingSprite")Assert.That(renderer.sprite,Is.Null);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CancellationRestoresThePreviousSourceVisibilityWithoutRevealingHiddenBodies(bool hidden)
    {
        renderer.forceRenderingOff=hidden;Material material=renderer.sharedMaterial;Color color=renderer.color;
        Vector3 scale=renderer.transform.localScale;
        var effect=MonsterRankUpFeedback.Play(renderer,4);AdvanceTo(effect,MonsterEvolutionCelebration.ImpactAt(4)*.8f);
        Assert.That(effect.BodyRenderer.enabled,Is.EqualTo(!hidden));
        effect.Cancel();
        Assert.That(renderer.forceRenderingOff,Is.EqualTo(hidden));Assert.That(renderer.enabled,Is.True);
        Assert.That(renderer.sprite,Is.SameAs(sprite));Assert.That(renderer.sharedMaterial,Is.SameAs(material));
        Assert.That(renderer.color,Is.EqualTo(color));Assert.That(renderer.transform.localScale,Is.EqualTo(scale));
    }

    [Test] public void ReplacingTheBodyEffectTransfersVisibilityOwnershipAndTheLastOwnerRestoresIt()
    {
        var first=MonsterRankUpFeedback.Play(renderer,2);AdvanceTo(first,.3f);
        var second=MonsterRankUpFeedback.Play(renderer,3);
        Assert.That(first==null||!first.gameObject.activeSelf,Is.True);
        Assert.That(second.BodyRenderer.enabled,Is.True);Assert.That(renderer.forceRenderingOff,Is.True);
        second.Cancel();Assert.That(renderer.forceRenderingOff,Is.False);
    }

    [TestCase("OnDisable",false)] [TestCase("OnDisable",true)]
    [TestCase("OnDestroy",false)] [TestCase("OnDestroy",true)]
    public void UnityLifecycleCallbacksRestoreVisibilityAndDestroyTheOwnedMaterial(string callback,bool hidden)
    {
        renderer.forceRenderingOff=hidden;Color color=renderer.color;Material original=renderer.sharedMaterial;
        var effect=MonsterRankUpFeedback.Play(renderer,4);AdvanceTo(effect,MonsterEvolutionCelebration.ImpactAt(4)*.8f);
        var material=Get<Material>(effect,"silhouetteMaterial");Assert.That(material,Is.Not.Null);
        // These EditMode-created components have never run in PlayMode; invoke Unity's callback explicitly.
        Call(effect,callback);
        Assert.That(renderer.forceRenderingOff,Is.EqualTo(hidden));Assert.That(effect.BodyRenderer.enabled,Is.False);
        Assert.That(effect.GetComponent<ParticleSystem>().particleCount,Is.Zero);
        foreach(var visual in effect.GetComponentsInChildren<SpriteRenderer>(true))Assert.That(visual.enabled,Is.False);
        if(callback=="OnDisable")Call(effect,"OnDestroy");
        Assert.That(material==null,Is.True);
        Object.DestroyImmediate(effect.gameObject);
        Assert.That(renderer.forceRenderingOff,Is.EqualTo(hidden));Assert.That(renderer.color,Is.EqualTo(color));
        Assert.That(renderer.sharedMaterial,Is.SameAs(original));Assert.That(renderer.sprite,Is.SameAs(sprite));
    }

    [Test] public void ClosingTheOwnerSceneRemovesTheWorldEffectAndItsAnimatedChildren()
    {
        Scene original=SceneManager.GetActiveScene(),temporary=default;
        try
        {
            temporary=EditorSceneManager.NewPreviewScene();
            var owner=new GameObject("Evolution source scene test");SceneManager.MoveGameObjectToScene(owner,temporary);
            var source=owner.AddComponent<SpriteRenderer>();source.sprite=sprite;
            var effect=MonsterRankUpFeedback.Play(source,4);AdvanceTo(effect,.4f);
            var visuals=Get<SpriteRenderer[]>(effect,"sprites");
            Assert.That(effect.gameObject.scene,Is.EqualTo(temporary));
            // Preview-scene objects are EditMode-only; model the callback while checking real scene-owned destruction below.
            Call(effect,"OnDestroy");Assert.That(source.forceRenderingOff,Is.False);
            SceneManager.SetActiveScene(original);
            Assert.That(EditorSceneManager.ClosePreviewScene(temporary),Is.True);
            Assert.That(effect==null,Is.True);Assert.That(source==null,Is.True);
            foreach(var visual in visuals)Assert.That(visual==null,Is.True);
        }
        finally
        {
            if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
            if(temporary.IsValid()&&temporary.isLoaded)EditorSceneManager.ClosePreviewScene(temporary);
        }
    }

    [Test] public void MissingOrInactiveSourcesDoNotCreateEffects()
    {
        Assert.That(MonsterRankUpFeedback.Play(null,3),Is.Null);
        renderer.enabled=false;Assert.That(MonsterRankUpFeedback.Play(renderer,3),Is.Null);
        renderer.enabled=true;renderer.sprite=null;Assert.That(MonsterRankUpFeedback.Play(renderer,3),Is.Null);
        renderer.sprite=sprite;renderer.gameObject.SetActive(false);
        Assert.That(MonsterRankUpFeedback.Play(renderer,3),Is.Null);
        Assert.That(Object.FindObjectsOfType<MonsterRankUpFeedback>(),Is.Empty);
    }

    [Test] public void InvalidRanksDoNotCreateEffects()
    {
        Assert.That(MonsterRankUpFeedback.Play(renderer,1),Is.Null);
        Assert.That(MonsterRankUpFeedback.Play(renderer,5),Is.Null);
        Assert.That(MonsterRankUpFeedback.Sample(0,.1f,1,out _,out _,out _),Is.False);
    }

    private IDictionary Pulses=>Get<IDictionary>(battle,"monsterRankUpPulses");
    private void Observe()=>Call(battle,"ObserveMonsterRankUpFeedback",unit);
    private void Present(float now)=>Call(battle,"PresentMonsterRankUpFeedback",now);
    private static MonsterRankUpFeedback OnlyWorldEffect(int rank)
    {
        var effects=Object.FindObjectsOfType<MonsterRankUpFeedback>();Assert.That(effects.Length,Is.EqualTo(1));
        Assert.That(Get<int>(effects[0],"rank"),Is.EqualTo(rank));return effects[0];
    }
    private static void AdvanceTo(MonsterRankUpFeedback effect,float age)=>Call(effect,"Advance",age-Get<float>(effect,"elapsed"));
    private static T Get<T>(object instance,string name)=>(T)instance.GetType().GetField(name,Fields).GetValue(instance);
    private static object Call(object instance,string name,params object[] args)=>instance.GetType().GetMethod(name,Hidden).Invoke(instance,args);
}
