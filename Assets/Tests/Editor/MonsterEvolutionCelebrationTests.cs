using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MonsterEvolutionCelebrationTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonsterRankUpFeedbackTests fixture;
    private MonsterAutoBattlePrototype battle;
    private ExpeditionRunSave run;
    private ExpeditionMonsterStateSave state;
    private object unit,previousFrontend;
    private SpriteRenderer renderer;
    private bool reduced,effectTest;

    [SetUp] public void Setup()
    {
        reduced=CombatFlashPresentation.Reduced;effectTest=CombatEffectTestConfiguration.Active;CombatFlashPresentation.Apply(false);
        previousFrontend=typeof(BombBrick.Game.Core.GameFrontend).GetField("instance",Fields).GetValue(null);
        typeof(BombBrick.Game.Core.GameFrontend).GetField("instance",Fields).SetValue(null,null);
        fixture=new MonsterRankUpFeedbackTests();fixture.Setup();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        run=Get<ExpeditionRunSave>(fixture,"run");state=Get<ExpeditionMonsterStateSave>(fixture,"state");
        unit=Get<object>(fixture,"unit");renderer=Get<SpriteRenderer>(fixture,"renderer");
    }

    [TearDown] public void Cleanup()
    {
        try{if(battle!=null)Call(battle,"ResetMonsterRankUpFeedback");fixture?.Cleanup();}
        finally
        {
            CombatFlashPresentation.Apply(reduced);CombatEffectTestConfiguration.Active=effectTest;
            typeof(BombBrick.Game.Core.GameFrontend).GetField("instance",Fields).SetValue(null,previousFrontend);
        }
    }

    [TestCase(2,2f,.65f,.83f,1.25f,1.75f,.16f)]
    [TestCase(3,2.8f,1.10f,1.32f,1.40f,2f,.20f)]
    [TestCase(4,3.8f,1.65f,1.93f,1.60f,2.35f,.24f)]
    public void RankProfilesSeparateChargeBodyImpactStampAndRecovery(
        int rank,float duration,float impact,float stamp,float bodyPeak,float stampPeak,float whiteEnd)
    {
        Assert.That(MonsterRankUpFeedback.DurationFor(rank),Is.EqualTo(duration));
        Assert.That(MonsterEvolutionCelebration.ImpactAt(rank),Is.EqualTo(impact));
        Assert.That(MonsterEvolutionCelebration.StampAt(rank),Is.EqualTo(stamp).Within(.00001f));
        Assert.That(MonsterEvolutionCelebration.ChargeProgress(rank,0f),Is.Zero);
        float previous=0f;
        for(int i=1;i<=4;i++)
        {
            float charge=MonsterEvolutionCelebration.ChargeProgress(rank,impact*i*.25f);
            Assert.That(charge,Is.InRange(previous,1f));previous=charge;
        }
        Assert.That(previous,Is.EqualTo(1f));
        Assert.That(MonsterEvolutionCelebration.GrowthProgress(rank,impact-.001f),Is.Zero);
        Assert.That(MonsterEvolutionCelebration.GrowthProgress(rank,impact+.101f),Is.EqualTo(1f));
        Assert.That(MonsterEvolutionCelebration.BodyScale(rank,impact*.8f),Is.InRange(.9f,1.1f));
        Assert.That(MonsterEvolutionCelebration.BodyScale(rank,impact+.07f),Is.EqualTo(bodyPeak).Within(.00001f));
        Assert.That(MonsterEvolutionCelebration.BodyScale(rank,impact+.75f),Is.EqualTo(1f).Within(.00001f));
        Assert.That(MonsterEvolutionCelebration.SilhouetteAlpha(rank,0f),Is.Zero);
        Assert.That(MonsterEvolutionCelebration.SilhouetteAlpha(rank,impact-.001f),Is.GreaterThan(.99f));
        Assert.That(MonsterEvolutionCelebration.SilhouetteAlpha(rank,impact+whiteEnd+.001f),Is.Zero);
        Assert.That(MonsterEvolutionCelebration.StampScale(rank,stamp-.001f),Is.Zero);
        Assert.That(MonsterEvolutionCelebration.StampScale(rank,stamp),Is.EqualTo(stampPeak).Within(.00001f));
        Assert.That(MonsterEvolutionCelebration.StampScale(rank,stamp+.351f),Is.EqualTo(1f).Within(.00001f));
        Assert.That(MonsterRankUpFeedback.Sample(0,impact,rank,out _,out _,out Color white),Is.True);
        Assert.That(white,Is.EqualTo(Color.white));
        Assert.That(MonsterRankUpFeedback.Sample(0,impact+.121f,rank,out _,out _,out _),Is.False);
        Assert.That(MonsterRankUpFeedback.Sample(8,duration,rank,out _,out _,out _),Is.False);
    }

    [Test] public void HigherRanksHaveTheirOwnChargeCurveInsteadOfOnlyWaitingLonger()
    {
        float rankTwo=MonsterEvolutionCelebration.ChargeProgress(2,MonsterEvolutionCelebration.ImpactAt(2)*.5f);
        float rankThree=MonsterEvolutionCelebration.ChargeProgress(3,MonsterEvolutionCelebration.ImpactAt(3)*.5f);
        float rankFour=MonsterEvolutionCelebration.ChargeProgress(4,MonsterEvolutionCelebration.ImpactAt(4)*.5f);
        Assert.That(Mathf.Abs(rankTwo-rankThree),Is.GreaterThan(.001f));
        Assert.That(Mathf.Abs(rankThree-rankFour),Is.GreaterThan(.001f));
        Assert.That(Mathf.Abs(rankTwo-rankFour),Is.GreaterThan(.001f));
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void ReducedModePreservesChargeAndStampTimingWithoutTheStampOvershoot(int rank)
    {
        float impact=MonsterEvolutionCelebration.ImpactAt(rank),stamp=MonsterEvolutionCelebration.StampAt(rank);
        float charge=MonsterEvolutionCelebration.ChargeProgress(rank,impact*.75f);
        CombatFlashPresentation.Apply(true);
        Assert.That(MonsterEvolutionCelebration.ImpactAt(rank),Is.EqualTo(impact));
        Assert.That(MonsterEvolutionCelebration.StampAt(rank),Is.EqualTo(stamp));
        Assert.That(MonsterEvolutionCelebration.ChargeProgress(rank,impact*.75f),Is.EqualTo(charge));
        Assert.That(MonsterEvolutionCelebration.StampScale(rank,stamp-.001f),Is.Zero);
        Assert.That(MonsterEvolutionCelebration.StampScale(rank,stamp),Is.EqualTo(1f));
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void WorldEffectsFollowTheActualMonsterWithoutChangingItsBodyDataCameraOrRandom(int rank)
    {
        var effectOrder=(DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(MonsterRankUpFeedback),typeof(DefaultExecutionOrder));
        var bodyOrder=(DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(SpriteMonsterAnimationDriver),typeof(DefaultExecutionOrder));
        Assert.That(effectOrder.order,Is.GreaterThan(bodyOrder.order));
        Assert.That(typeof(MonsterRankUpFeedback).GetMethod("LateUpdate",Fields),Is.Not.Null);
        var camera=new GameObject("Local evolution camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-12f),Quaternion.Euler(35f,0f,0f));
        Vector3 cameraPosition=camera.transform.position;Quaternion cameraRotation=camera.transform.rotation;float fov=camera.fieldOfView;
        Vector3 originalScale=renderer.transform.localScale;Color originalColor=renderer.color;Sprite originalSprite=renderer.sprite;
        Material originalMaterial=renderer.sharedMaterial;bool originalRenderingOff=renderer.forceRenderingOff;
        string data=JsonUtility.ToJson(run),random=JsonUtility.ToJson(UnityEngine.Random.state);float hp=Get<float>(unit,"hp"),radius=Get<float>(unit,"collisionRadius"),time=Time.timeScale;
        var effect=MonsterRankUpFeedback.Play(renderer,rank);Assert.That(effect,Is.Not.Null);
        Assert.That(renderer.forceRenderingOff,Is.True);Assert.That(effect.BodyRenderer,Is.Not.SameAs(renderer));
        var particles=effect.GetComponent<ParticleSystem>();Assert.That(particles.main.useUnscaledTime,Is.True);
        Call(effect,"Advance",MonsterEvolutionCelebration.ImpactAt(rank)+.08f);
        var sprites=Get<SpriteRenderer[]>(effect,"sprites");Assert.That(sprites.Length,Is.EqualTo(7));
        Assert.That(sprites[0].enabled,Is.True);Assert.That(sprites[1].enabled,Is.EqualTo(rank>=3));
        Assert.That(sprites[2].enabled,Is.EqualTo(rank==4));Assert.That(sprites[3].enabled,Is.EqualTo(rank==4));
        Assert.That(sprites[4].enabled,Is.True);
        for(int i=0;i<sprites.Length;i++)Assert.That(sprites[i].sortingOrder,Is.EqualTo(renderer.sortingOrder+26+i));
        var samples=new ParticleSystem.Particle[40];Assert.That(particles.GetParticles(samples),Is.InRange(1,40));
        renderer.transform.position=new Vector3(3f,1f,-2f);Call(effect,"Pose");
        Assert.That(Vector3.Distance(Get<Vector3>(effect,"center"),CombatReadabilityPresentation.VisibleBodyCenter(effect.BodyRenderer)),Is.LessThan(.00001f));
        Assert.That(renderer.sprite,Is.SameAs(originalSprite));Assert.That(renderer.color,Is.EqualTo(originalColor));Assert.That(renderer.transform.localScale,Is.EqualTo(originalScale));
        Assert.That(renderer.sharedMaterial,Is.SameAs(originalMaterial));
        float span=Get<float>(effect,"span");renderer.transform.localScale=originalScale*1.5f;Call(effect,"Pose");
        Assert.That(Get<float>(effect,"span"),Is.EqualTo(span*1.5f).Within(.00001f));Assert.That(renderer.transform.localScale,Is.EqualTo(originalScale*1.5f));
        Assert.That(Get<float>(unit,"hp"),Is.EqualTo(hp));Assert.That(Get<float>(unit,"collisionRadius"),Is.EqualTo(radius));
        Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));Assert.That(Time.timeScale,Is.EqualTo(time));
        Assert.That(camera.transform.position,Is.EqualTo(cameraPosition));Assert.That(camera.transform.rotation,Is.EqualTo(cameraRotation));Assert.That(camera.fieldOfView,Is.EqualTo(fov));
        Time.timeScale=0f;Call(effect,"Advance",MonsterRankUpFeedback.DurationFor(rank)+.01f);Assert.That(effect==null||!effect.gameObject.activeSelf,Is.True);
        Assert.That(renderer.forceRenderingOff,Is.EqualTo(originalRenderingOff));
        Assert.That(renderer.sharedMaterial,Is.SameAs(originalMaterial));Assert.That(renderer.color,Is.EqualTo(originalColor));
        Assert.That(renderer.transform.localScale,Is.EqualTo(originalScale*1.5f));
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void BodyProxyUsesTheLiveFrameAndOpaqueChargeThenRestoresTheOriginalLook(int rank)
    {
        renderer.color=new Color(.2f,.4f,.6f,1f);renderer.transform.localScale=new Vector3(1.2f,.8f,1f);
        Sprite originalSprite=renderer.sprite;Material material=renderer.sharedMaterial;Color tint=renderer.color;
        var alternate=Sprite.Create(originalSprite.texture,new Rect(0f,0f,2f,4f),Vector2.one*.5f,4f);
        MonsterRankUpFeedback effect=null;
        try
        {
            effect=MonsterRankUpFeedback.Play(renderer,rank);effect.StartingScale=.8f;
            float impact=MonsterEvolutionCelebration.ImpactAt(rank);
            AdvanceTo(effect,impact*.8f);
            var body=effect.BodyRenderer;var silhouette=Get<SpriteRenderer>(effect,"silhouetteProxy");
            Assert.That(body.enabled,Is.True);Assert.That(renderer.forceRenderingOff,Is.True);
            Assert.That(body.sprite,Is.SameAs(originalSprite));Assert.That(body.sharedMaterial,Is.SameAs(material));
            Assert.That(silhouette,Is.Not.Null);Assert.That(silhouette.enabled,Is.True);
            Color charge=CombatFlashPresentation.Peak(MonsterRankUpFeedback.RankColor(rank));charge.a=1f;
            Assert.That(silhouette.color,Is.EqualTo(charge));
            Assert.That(silhouette.sharedMaterial.GetFloat("_KeepSpriteDetail"),Is.Zero);
            Assert.That(silhouette.sharedMaterial.GetFloat("_OutlineOnly"),Is.Zero);
            float expected=.8f*MonsterEvolutionCelebration.BodyScale(rank,impact*.8f);
            Assert.That(Vector3.Distance(body.transform.lossyScale,renderer.transform.lossyScale*expected),Is.LessThan(.0001f));

            renderer.sprite=alternate;renderer.flipX=true;renderer.flipY=true;
            renderer.transform.SetPositionAndRotation(new Vector3(2f,1f,-3f),Quaternion.Euler(0f,0f,12f));
            Call(effect,"Pose");
            Assert.That(body.sprite,Is.SameAs(alternate));Assert.That(silhouette.sprite,Is.SameAs(alternate));
            Assert.That(body.flipX,Is.True);Assert.That(body.flipY,Is.True);
            Assert.That(silhouette.flipX,Is.True);Assert.That(silhouette.flipY,Is.True);
            Assert.That(body.transform.rotation,Is.EqualTo(renderer.transform.rotation));
            Bounds bounds=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{alternate});
            Vector3 foot=new Vector3(-bounds.center.x,-bounds.max.y,0f);
            Assert.That(Vector3.Distance(body.transform.TransformPoint(foot),renderer.transform.TransformPoint(foot)),Is.LessThan(.0001f));

            AdvanceTo(effect,impact+.01f);
            Color flash=CombatFlashPresentation.Peak(Color.Lerp(Color.white,MonsterRankUpFeedback.RankColor(rank),.01f/.12f));
            Assert.That(silhouette.color.r,Is.EqualTo(flash.r).Within(.0001f));
            Assert.That(silhouette.color.g,Is.EqualTo(flash.g).Within(.0001f));
            Assert.That(silhouette.color.b,Is.EqualTo(flash.b).Within(.0001f));
            AdvanceTo(effect,impact+.101f);
            expected=MonsterEvolutionCelebration.BodyScale(rank,impact+.101f);
            Assert.That(Vector3.Distance(body.transform.lossyScale,renderer.transform.lossyScale*expected),Is.LessThan(.0001f),"The old-rank starting size is gone after growth completes.");
            AdvanceTo(effect,impact+.30f);Assert.That(silhouette.enabled,Is.False);
            Assert.That(body.enabled,Is.True);Assert.That(body.color,Is.EqualTo(tint));Assert.That(body.sharedMaterial,Is.SameAs(material));
            Vector3 position=renderer.transform.position,scale=renderer.transform.localScale;Quaternion rotation=renderer.transform.rotation;
            AdvanceTo(effect,MonsterRankUpFeedback.DurationFor(rank)+.01f);
            Assert.That(renderer.forceRenderingOff,Is.False);Assert.That(renderer.sprite,Is.SameAs(alternate));
            Assert.That(renderer.color,Is.EqualTo(tint));Assert.That(renderer.sharedMaterial,Is.SameAs(material));
            Assert.That(renderer.transform.position,Is.EqualTo(position));Assert.That(renderer.transform.localScale,Is.EqualTo(scale));
            Assert.That(renderer.transform.rotation,Is.EqualTo(rotation));Assert.That(renderer.flipX&&renderer.flipY,Is.True);
        }
        finally
        {
            if(effect!=null)effect.Cancel();
            if(renderer!=null)renderer.sprite=originalSprite;
            var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",Fields).GetValue(null);
            cache.Remove(alternate);UnityEngine.Object.DestroyImmediate(alternate);
        }
    }

    [Test] public void HigherRanksUseAllEightLightningFramesAndReducedModeOnlyAttenuatesAccents()
    {
        var atlas=Resources.Load<Texture2D>(MonsterEvolutionCelebration.LightningResource);Assert.That(atlas,Is.Not.Null);
        Assert.That(atlas.width,Is.EqualTo(512));Assert.That(atlas.height,Is.EqualTo(512));Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(PixelCombatEffect.LoadFrames(PixelCombatEffect.KingKongEarthPulseResourcePath).Length,Is.EqualTo(8));
        Assert.That(PixelCombatEffect.LoadFrames(PixelCombatEffect.CriticalResourcePath).Length,Is.EqualTo(6));
        for(int rank=3;rank<=4;rank++)
        {
            float impact=MonsterEvolutionCelebration.ImpactAt(rank);
            for(int frame=0;frame<8;frame++)Assert.That(MonsterEvolutionCelebration.LightningFrame(rank,impact+(frame+.1f)/32f),Is.EqualTo(frame));
            Assert.That(MonsterEvolutionCelebration.LightningFrame(rank,impact+.251f),Is.EqualTo(-1));
        }
        Assert.That(MonsterEvolutionCelebration.LightningFrame(2,.2f),Is.EqualTo(-1));
        MonsterRankUpFeedback.Sample(24,.4f,4,out Vector2 offset,out float size,out Color color);
        CombatFlashPresentation.Apply(true);MonsterRankUpFeedback.Sample(24,.4f,4,out Vector2 reducedOffset,out float reducedSize,out Color reducedColor);
        Assert.That(reducedOffset,Is.EqualTo(offset));Assert.That(reducedSize,Is.EqualTo(size));Assert.That(reducedColor,Is.EqualTo(color));
        Assert.That(CombatFlashPresentation.Flash(color).a,Is.EqualTo(color.a*.25f).Within(.00001f));
    }

    [Test] public void ARealRankIncreaseWaitsForPresentationThenStartsExactlyOneWorldEffect()
    {
        RankTwo();object pulse=Pulse;Assert.That(Get<float>(pulse,"startedAt"),Is.EqualTo(-1f));Assert.That(Get<MonsterRankUpFeedback>(pulse,"worldEffect"),Is.Null);
        for(int i=0;i<4;i++)Observe();Assert.That(Pulses.Count,Is.EqualTo(1));
        Present(10f);var effect=Get<MonsterRankUpFeedback>(pulse,"worldEffect");Assert.That(effect,Is.Not.Null);
        Assert.That(Get<float>(pulse,"startedAt"),Is.EqualTo(10f));Assert.That(Get<object>(pulse,"unit"),Is.SameAs(unit));
        Present(10.1f);Assert.That(Get<MonsterRankUpFeedback>(pulse,"worldEffect"),Is.SameAs(effect));
        Call(effect,"Advance",MonsterRankUpFeedback.DurationFor(2)+.01f);
        Present(10f+MonsterRankUpFeedback.DurationFor(2)+.01f);Assert.That(Pulses.Count,Is.Zero);
    }

    [TestCase("growthShopVisible")] [TestCase("shopVisible")] [TestCase("augmentDraft")] [TestCase("mlEpisodeActive")]
    [TestCase("battleStarted")] [TestCase("wavePreparationAcrossFrames")]
    public void CoveredOrUnavailableWorldViewsDoNotConsumeThePendingEffect(string blocker)
    {
        RankTwo();object pulse=Pulse;
        if(blocker=="augmentDraft")
        {run.augmentDraftWave=run.wave;for(int i=0;i<ExpeditionAugmentSystem.ChoiceCount;i++)run.augmentDraftIds.Add("preview_"+i);}
        else Set(battle,blocker,true);
        Present(100f);
        Assert.That(Get<float>(pulse,"startedAt"),Is.EqualTo(-1f));Assert.That(Get<MonsterRankUpFeedback>(pulse,"worldEffect"),Is.Null);
        if(blocker=="augmentDraft")run.augmentDraftIds.Clear();else Set(battle,blocker,false);
        Present(200f);Assert.That(Get<float>(pulse,"startedAt"),Is.EqualTo(200f));
    }

    [Test] public void InactiveWorldModelsWaitAndReloadOrNewRunsDiscardOldFeedback()
    {
        RankTwo();renderer.gameObject.SetActive(false);Present(100f);Assert.That(Get<float>(Pulse,"startedAt"),Is.EqualTo(-1f));
        renderer.gameObject.SetActive(true);Present(200f);var effect=Get<MonsterRankUpFeedback>(Pulse,"worldEffect");
        Call(battle,"ResetMonsterRankUpFeedback");Assert.That(Pulses.Count,Is.Zero);Assert.That(effect==null,Is.True);
        Observe();run.runId="another_local_rankup_run";state.transcended=true;Observe();Assert.That(Pulses.Count,Is.Zero);
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void TestPreviewTargetsTheExistingSelectedMonsterAndReplayDoesNotChangeProgression(int rank)
    {
        CombatEffectTestConfiguration.Active=true;var definition=Get<MonsterDefinition>(unit,"definition");
        string data=JsonUtility.ToJson(run),config=Configuration(),random=JsonUtility.ToJson(UnityEngine.Random.state);
        float hp=Get<float>(unit,"hp"),damage=Get<float>(unit,"damage"),radius=Get<float>(unit,"collisionRadius");int range=Get<int>(unit,"attackRange");
        Vector3 position=renderer.transform.position,scale=renderer.transform.localScale;
        Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,rank),Is.True);
        Assert.That(Pulses.Count,Is.EqualTo(1));object first=Pulse;Assert.That(Get<object>(first,"unit"),Is.SameAs(unit));Assert.That(Get<int>(first,"rank"),Is.EqualTo(rank));
        Present(10f);var effect=Get<MonsterRankUpFeedback>(first,"worldEffect");Assert.That(effect,Is.Not.Null);
        Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,rank),Is.True);
        Assert.That(Pulses.Count,Is.EqualTo(1));Assert.That(Pulse,Is.Not.SameAs(first));Assert.That(Get<float>(Pulse,"startedAt"),Is.EqualTo(-1f));Assert.That(effect==null,Is.True);
        Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));Assert.That(Configuration(),Is.EqualTo(config));Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
        Assert.That(Get<float>(unit,"hp"),Is.EqualTo(hp));Assert.That(Get<float>(unit,"damage"),Is.EqualTo(damage));Assert.That(Get<float>(unit,"collisionRadius"),Is.EqualTo(radius));Assert.That(Get<int>(unit,"attackRange"),Is.EqualTo(range));
        Assert.That(renderer.transform.position,Is.EqualTo(position));Assert.That(renderer.transform.localScale,Is.EqualTo(scale));
    }

    [Test] public void InvalidPreviewsNeverCreateAFakeMonsterOrModifyAnExistingPulse()
    {
        CombatEffectTestConfiguration.Active=true;var definition=Get<MonsterDefinition>(unit,"definition");
        Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,2),Is.True);object pending=Pulse;string data=JsonUtility.ToJson(run);
        foreach(int rank in new[]{0,1,5})Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,rank),Is.False);
        Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",null,2),Is.False);
        var absent=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("woongi");
        Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",absent,2),Is.False);
        renderer.gameObject.SetActive(false);Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,2),Is.False);renderer.gameObject.SetActive(true);
        Set(unit,"alive",false);Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,2),Is.False);Set(unit,"alive",true);
        Set(battle,"battleStarted",true);Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,2),Is.False);Set(battle,"battleStarted",false);
        CombatEffectTestConfiguration.Active=false;Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,2),Is.False);
        Assert.That(Pulse,Is.SameAs(pending));Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void HudStarsWaitForTheStampAndKeepTheActualEarnedRankAfterTheEffect(int rank)
    {
        Observe();state.rank=(ExpeditionMonsterRank)Mathf.Min(rank,3);state.transcended=rank==4;Observe();
        object pulse=Pulse;string data=JsonUtility.ToJson(run);
        Assert.That(HudStars(out float scale),Is.Zero);Assert.That(scale,Is.Zero);
        Present(Time.unscaledTime);
        Set(pulse,"startedAt",Time.unscaledTime-MonsterEvolutionCelebration.StampAt(rank)+.02f);
        Assert.That(HudStars(out scale),Is.Zero);Assert.That(scale,Is.Zero);
        Set(pulse,"startedAt",Time.unscaledTime-MonsterEvolutionCelebration.StampAt(rank)-.02f);
        Assert.That(HudStars(out scale),Is.EqualTo(rank));Assert.That(scale,Is.GreaterThan(1f));
        Set(pulse,"startedAt",Time.unscaledTime-MonsterEvolutionCelebration.DurationFor(rank)-.02f);
        Assert.That(HudStars(out scale),Is.EqualTo(rank));Assert.That(scale,Is.EqualTo(1f));
        Present(Time.unscaledTime);Assert.That(Pulses.Count,Is.Zero);
        Assert.That(HudStars(out scale),Is.EqualTo(rank));Assert.That(scale,Is.EqualTo(1f));
        Assert.That(renderer.forceRenderingOff,Is.False);Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));
    }

    [Test] public void RankFourPreviewReturnsToTheRealRankThreeStarsAfterItsStampFinishes()
    {
        state.rank=ExpeditionMonsterRank.Rank3;Observe();CombatEffectTestConfiguration.Active=true;
        var definition=Get<MonsterDefinition>(unit,"definition");string data=JsonUtility.ToJson(run);
        Assert.That(Call(battle,"PreviewCombatEffectTestEvolution",definition,4),Is.True);Present(Time.unscaledTime);
        Set(Pulse,"startedAt",Time.unscaledTime-MonsterEvolutionCelebration.StampAt(4)-.02f);
        Assert.That(HudStars(out float scale),Is.EqualTo(4));Assert.That(scale,Is.GreaterThan(1f));
        Set(Pulse,"startedAt",Time.unscaledTime-MonsterEvolutionCelebration.DurationFor(4)-.02f);
        Present(Time.unscaledTime);Assert.That(Pulses.Count,Is.Zero);
        Assert.That(HudStars(out scale),Is.EqualTo(3));Assert.That(scale,Is.EqualTo(1f));
        Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));
    }

    [Test] public void GrowthStartsFromTheRealPreviousBodyScaleRatherThanOnlyTheRankBaseRatio()
    {
        Observe();state.rank=ExpeditionMonsterRank.Rank2;Set(unit,"combatBodyScale",1.32f);
        Call(battle,"ObserveMonsterRankUpWithScale",unit,.86f);
        Assert.That(Get<float>(Pulse,"fromScale"),Is.EqualTo(.86f/1.32f).Within(.00001f));
        Present(Time.unscaledTime);var effect=Get<MonsterRankUpFeedback>(Pulse,"worldEffect");
        Assert.That(effect.StartingScale,Is.EqualTo(.86f/1.32f).Within(.00001f));
        Assert.That(Vector3.Distance(effect.BodyRenderer.transform.lossyScale,renderer.transform.lossyScale*effect.StartingScale),Is.LessThan(.0001f));
    }

    public sealed class LocalPortraitHarness:EditorWindow
    {
        public MonsterAutoBattlePrototype battle;
        public string instanceId;
        public Sprite portrait;
        public bool restored=true,inputPreserved=true,startedBeforePaint;
        public bool drawBody,renderTargetRestored=true;
        public int paints;
        private void OnGUI()
        {
            EventType type=Event.current.type;Color color=GUI.color;Matrix4x4 matrix=GUI.matrix;int depth=GUI.depth,hot=GUIUtility.hotControl;
            RenderTexture renderTarget=RenderTexture.active;
            if(drawBody)Call(battle,"DrawEvolutionPortrait",new Rect(100f,80f,64f,64f),portrait,instanceId);
            Call(battle,"DrawMonsterRankUpFeedback",new Rect(100f,80f,64f,64f),instanceId);
            var pulses=Get<IDictionary>(battle,"monsterRankUpPulses");
            if(paints==0&&type!=EventType.Repaint&&Get<float>(pulses[instanceId],"startedAt")>=0f)startedBeforePaint=true;
            if(type==EventType.Repaint)paints++;
            restored&=GUI.color==color&&GUI.matrix==matrix&&GUI.depth==depth;
            inputPreserved&=Event.current.type==type&&GUIUtility.hotControl==hot;
            renderTargetRestored&=RenderTexture.active==renderTarget;
        }
    }

    [UnityTest] public IEnumerator TheExistingCardPortraitStartsHiddenFeedbackOnRepaintWithoutAModalOrInputCapture()
    {
        RankTwo();renderer.gameObject.SetActive(false);Set(battle,"growthShopVisible",true);Present(100f);Assert.That(Get<float>(Pulse,"startedAt"),Is.EqualTo(-1f));
        string data=JsonUtility.ToJson(run),random=JsonUtility.ToJson(UnityEngine.Random.state);float time=Time.timeScale;
        var window=ScriptableObject.CreateInstance<LocalPortraitHarness>();window.battle=battle;window.instanceId=state.instanceId;
        try
        {
            window.position=new Rect(80f,80f,400f,300f);window.Show();window.Focus();yield return null;
            window.Repaint();yield return null;Assert.That(window.paints,Is.GreaterThan(0));Assert.That(window.startedBeforePaint,Is.False);
            Assert.That(Get<float>(Pulse,"startedAt"),Is.GreaterThanOrEqualTo(0f));Assert.That(Get<MonsterRankUpFeedback>(Pulse,"worldEffect"),Is.Null);
            window.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(120f,100f)});
            window.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=new Vector2(120f,100f)});
            window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Space});
            Assert.That(window.restored,Is.True);Assert.That(window.inputPreserved,Is.True);
            Assert.That(JsonUtility.ToJson(run),Is.EqualTo(data));Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));Assert.That(Time.timeScale,Is.EqualTo(time));
            Assert.That(typeof(MonsterEvolutionCelebration).GetMethod("Draw",Fields),Is.Null);
            Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("HandleRankUpCelebrationInput",Fields),Is.Null);
        }
        finally{window.Close();}
    }

    [UnityTest] public IEnumerator CardSilhouetteTexturesAreBoundedAndReleasedWithTheRankPulse()
    {
        RankTwo();renderer.gameObject.SetActive(false);Set(battle,"growthShopVisible",true);
        var window=ScriptableObject.CreateInstance<LocalPortraitHarness>();
        window.battle=battle;window.instanceId=state.instanceId;window.portrait=renderer.sprite;window.drawBody=true;
        try
        {
            window.position=new Rect(80f,80f,400f,300f);window.Show();window.Focus();yield return null;
            Set(Pulse,"startedAt",Time.unscaledTime-.3f);window.Repaint();yield return null;
            var source=Get<RenderTexture>(Pulse,"cardSource");var mask=Get<RenderTexture>(Pulse,"cardSilhouette");
            var material=Get<Material>(Pulse,"cardMaterial");
            Assert.That(source,Is.Not.Null);Assert.That(mask,Is.Not.Null);Assert.That(material,Is.Not.Null);
            foreach(var texture in new[]{source,mask})
            {
                Assert.That(texture.IsCreated(),Is.True);Assert.That(texture.width,Is.EqualTo(128));
                Assert.That(texture.height,Is.EqualTo(128));Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point));
            }
            Assert.That(window.restored&&window.inputPreserved&&window.renderTargetRestored,Is.True);
            window.Close();window=null;Call(battle,"ResetMonsterRankUpFeedback");
            Assert.That(Pulses.Count,Is.Zero);Assert.That(source==null&&mask==null&&material==null,Is.True);
        }
        finally{if(window!=null)window.Close();}
    }

    private IDictionary Pulses=>Get<IDictionary>(battle,"monsterRankUpPulses");

    [Test] public void AVisibleBenchEvolutionTemporarilyGetsItsHeadHudWithoutRequiringHover()
    {
        var camera=new GameObject("Bench evolution HUD camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0f,0f,-10f);
        Set(battle,"mainCamera",camera);Set(unit,"inStorage",true);Set(battle,"hoveredUnit",null);
        object[] bounds={unit,new Rect()};
        Assert.That((bool)typeof(MonsterAutoBattlePrototype).GetMethod("TryGetUnitWorldHudRect",Fields).Invoke(battle,bounds),Is.False);
        RankTwo();Present(Time.unscaledTime);
        var effect=Get<MonsterRankUpFeedback>(Pulse,"worldEffect");
        Assert.That(effect,Is.Not.Null);
        Assert.That((bool)typeof(MonsterAutoBattlePrototype).GetMethod("TryGetUnitWorldHudRect",Fields).Invoke(battle,bounds),Is.True);
        effect.Cancel();
        Assert.That((bool)typeof(MonsterAutoBattlePrototype).GetMethod("TryGetUnitWorldHudRect",Fields).Invoke(battle,bounds),Is.False);
    }

    private object Pulse=>Pulses[state.instanceId];
    private void Observe()=>Call(battle,"ObserveMonsterRankUpFeedback",unit);
    private void RankTwo(){Observe();state.rank=ExpeditionMonsterRank.Rank2;Observe();Assert.That(Pulses.Count,Is.EqualTo(1));}
    private void Present(float now)=>Call(battle,"PresentMonsterRankUpFeedback",now);
    private static void AdvanceTo(MonsterRankUpFeedback effect,float age)=>Call(effect,"Advance",age-effect.Elapsed);
    private int HudStars(out float scale)
    {
        object[] args={unit,0f};
        int count=(int)typeof(MonsterAutoBattlePrototype).GetMethod("RankUpPresentationStarCount",Fields).Invoke(battle,args);
        scale=(float)args[1];return count;
    }
    private static string Configuration()
    {
        return string.Join("\n",typeof(CombatEffectTestConfiguration).GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .OrderBy(x=>x.Name).Select(field=>
            {
                object value=field.GetValue(null);
                return field.Name+"="+(value is System.Collections.Generic.IEnumerable<string> list?string.Join(",",list):Convert.ToString(value));
            }));
    }
    private static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field,Fields).GetValue(value);
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Fields).SetValue(value,data);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Fields).Invoke(value,args);
}
