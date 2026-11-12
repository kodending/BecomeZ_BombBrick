using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class ReadabilityMonkeySkillDensityTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private bool active,enabled,contact,skillAudio,basic,reduced;
    private CombatReadabilityPreset preset;
    private float speed;
    private HashSet<GameObject> existing;

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        active=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;
        contact=CombatReadabilityPreview.ContactFeedback;skillAudio=CombatReadabilityPreview.SkillAudio;
        basic=CombatReadabilityPreview.BasicOnly;preset=CombatReadabilityPreview.Preset;
        reduced=CombatFlashPresentation.Reduced;speed=Time.timeScale;
        CombatEffectTestConfiguration.Active=CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        CombatReadabilityPreview.ContactFeedback=CombatReadabilityPreview.SkillAudio=true;
        CombatReadabilityPreview.BasicOnly=false;CombatFlashPresentation.Apply(false);Time.timeScale=1f;
    }

    [TearDown] public void TearDown()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
        CombatEffectTestConfiguration.Active=active;CombatReadabilityPreview.Enabled=enabled;
        CombatReadabilityPreview.ContactFeedback=contact;CombatReadabilityPreview.SkillAudio=skillAudio;
        CombatReadabilityPreview.BasicOnly=basic;CombatReadabilityPreview.Preset=preset;
        CombatFlashPresentation.Apply(reduced);Time.timeScale=speed;
    }

    [Test]
    public void SecondaryMortarClearsFasterWithoutShrinkingItsDamageAreaCueOrRemovingAnimationFrames()
    {
        string random=JsonUtility.ToJson(UnityEngine.Random.state);
        var ps=PixelStarProjectile.PlayBeanImpact(Vector3.zero,null,Color.yellow,true,2f,.7f,secondary:true);
        var particles=new ParticleSystem.Particle[32];int count=ps.GetParticles(particles);
        Assert.That(count,Is.EqualTo(12));float maximumReach=0f;
        for(int i=0;i<count;i++)
        {
            Assert.That(particles[i].startLifetime,Is.EqualTo(.16f).Within(.0001f));
            Vector3 velocity=particles[i].velocity;velocity.y=0f;
            maximumReach=Mathf.Max(maximumReach,velocity.magnitude*particles[i].startLifetime);
        }
        Assert.That(maximumReach,Is.EqualTo(2f).Within(.0001f));
        var pop=FindPop("Monkey Heavy Fruit Pop");
        Assert.That(Get<Sprite[]>(pop,"frames").Length,Is.EqualTo(8));
        Assert.That(Get<float>(pop,"framesPerSecond"),Is.EqualTo(56f));
        Assert.That(pop.transform.localScale.x,Is.EqualTo(1.15f*.7f*.85f*1.215f).Within(.0001f));
        Assert.That(pop.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.62f).Within(.0001f));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
    }

    [Test]
    public void FinishingMortarKeepsItsFullBurstAndLongerHeavyPop()
    {
        var ps=PixelStarProjectile.PlayBeanImpact(Vector3.zero,null,Color.yellow,true,2f,1.35f);
        var particles=new ParticleSystem.Particle[32];Assert.That(ps.GetParticles(particles),Is.EqualTo(28));
        foreach(var particle in particles)if(particle.startLifetime>0f)
            Assert.That(particle.startLifetime,Is.EqualTo(.24f).Within(.0001f));
        var pop=FindPop("Monkey Heavy Fruit Pop");
        Assert.That(Get<float>(pop,"framesPerSecond"),Is.EqualTo(26f));
        Assert.That(pop.transform.localScale.x,Is.EqualTo(1.15f*1.35f*1.215f).Within(.0001f));
        Assert.That(pop.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(1f));
    }

    [TestCase(false,true,CombatReadabilityPreset.Local)]
    [TestCase(true,false,CombatReadabilityPreset.Local)]
    [TestCase(true,true,CombatReadabilityPreset.Current)]
    [TestCase(true,true,CombatReadabilityPreset.Compact)]
    public void ProductionOffAndOtherComparisonsKeepSecondaryBurstsUnchanged(bool test,bool selected,CombatReadabilityPreset mode)
    {
        CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=selected;CombatReadabilityPreview.Preset=mode;
        AssertLegacyBurst();
    }

    [Test]
    public void ExclusiveCinematicClockKeepsTheAuthoredBurst()
    {
        var clock=new CombatSkillClock(null);clock.Hold();
        try{AssertLegacyBurst();}finally{clock.End();}
    }

    [Test]
    public void ReturningFinalContactRetainsItsExplicitLargePopEvenThoughItIsNotHeavy()
    {
        var pop=PixelCombatEffect.PlayMonkeyFruitPop(Vector3.zero,null,Color.yellow,false,1.9f,secondary:false);
        Assert.That(Get<float>(pop,"framesPerSecond"),Is.EqualTo(40f));
        Assert.That(pop.transform.localScale.x,Is.EqualTo(.38f*1.9f*1.215f).Within(.0001f));
        Assert.That(pop.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(1f));
        Assert.That(Get<Sprite[]>(pop,"frames").Length,Is.EqualTo(8));
    }

    [Test]
    public void IntermediatePelletKeepsItsContactWithOnlyTwoShortLivedFragments()
    {
        var ps=PixelStarProjectile.PlayBeanImpact(Vector3.zero,null,Color.yellow,false,1f,secondary:true);
        var particles=new ParticleSystem.Particle[8];int count=ps.GetParticles(particles);
        Assert.That(count,Is.EqualTo(2));
        for(int i=0;i<count;i++)Assert.That(particles[i].startLifetime,Is.InRange(.1f,.18f));
        Assert.That(Get<Sprite[]>(FindPop("Monkey Pellet Pop"),"frames").Length,Is.EqualTo(8));
    }

    [TestCase(MonkeyRank3Rules.MyriadReturn,.008333333f,350f)]
    [TestCase(MonkeyRank3Rules.MyriadReturn,.23f,350f)]
    [TestCase(MonkeyRank3Rules.FiveOrbJuggling,.008333333f,450f)]
    [TestCase(MonkeyRank3Rules.FiveOrbJuggling,.23f,450f)]
    public void RefinementPreservesEveryStationaryFruitContactAndItsDamage(string trait,float delta,float damage)
    {
        var fixture=new MonkeyArcTests();fixture.SetUp();
        try{fixture.RealBeginAndPacedReleaseReachAllStationaryFruitContacts(trait,delta,damage);}
        finally{fixture.TearDown();}
    }

    [UnityTest]
    public IEnumerator OnlyExplicitIntermediateThrowAndHitSoundsLoseGain()
    {
        object previousAudio=Field(typeof(GameAudio),"instance").GetValue(null);
        object previousFrontend=Field(typeof(GameFrontend),"instance").GetValue(null);
        var fixture=new MonkeyArcTests();fixture.SetUp();
        try
        {
            Field(typeof(GameFrontend),"instance").SetValue(null,null);
            var host=new GameObject("Inactive monkey density audio");host.SetActive(false);
            var audio=host.AddComponent<GameAudio>();Call(audio,"InitializeCombatAudio");
            Field(typeof(GameAudio),"instance").SetValue(null,audio);Field(typeof(GameAudio),"previewEnabled").SetValue(audio,true);
            var voices=Get<AudioSource[]>(audio,"combatVoices");var layers=Get<AudioSource[]>(audio,"combatLayers");
            var voiceHost=new GameObject("Monkey density audio voices");
            for(int i=0;i<voices.Length;i++)
            {voices[i]=voiceHost.AddComponent<AudioSource>();layers[i]=voiceHost.AddComponent<AudioSource>();}
            var battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");var source=Get<object>(fixture,"source");
            var soundType=typeof(MonsterAutoBattlePrototype).GetNestedType("MonkeySound",BindingFlags.NonPublic);
            foreach(string name in new[]{"Throw","Hit","Finish"})
            {
                var clip=Resources.Load<AudioClip>("Audio/Combat/Monkey/"+name.ToLowerInvariant());
                Assert.That(clip,Is.Not.Null);clip.LoadAudioData();
                while(clip.loadState==AudioDataLoadState.Loading)yield return null;
                Field(typeof(GameAudio),"authoredBasicHit").SetValue(audio,clip);
                object cue=Enum.Parse(soundType,name);
                audio.StopCombatSounds();Call(battle,"PlayMonkeySound",source,cue,Vector3.zero,1f,false);
                Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));float normal=Get<float[]>(audio,"voiceGain")[0];
                audio.StopCombatSounds();Call(battle,"PlayMonkeySound",source,cue,Vector3.zero,1f,true);
                Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));
                Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(normal*(name=="Finish"?1f:.75f)).Within(.0001f));
            }
        }
        finally
        {
            fixture.TearDown();Field(typeof(GameAudio),"instance").SetValue(null,previousAudio);
            Field(typeof(GameFrontend),"instance").SetValue(null,previousFrontend);
        }
    }

    private static void AssertLegacyBurst()
    {
        var ps=PixelStarProjectile.PlayBeanImpact(Vector3.zero,null,Color.yellow,true,2f,.7f,secondary:true);
        Assert.That(ps.GetParticles(new ParticleSystem.Particle[32]),Is.EqualTo(28));
        Assert.That(Get<float>(FindPop("Monkey Heavy Fruit Pop"),"framesPerSecond"),Is.EqualTo(26f));
    }
    private static PixelCombatEffect FindPop(string name)
    {
        foreach(var effect in Object.FindObjectsOfType<PixelCombatEffect>())if(effect.name==name)return effect;
        Assert.Fail("Missing "+name);return null;
    }
    private static FieldInfo Field(Type type,string field)=>type.GetField(field,Hidden);
    private static T Get<T>(object target,string field)=>(T)Field(target.GetType(),field).GetValue(target);
    private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Hidden).Invoke(target,args);
}
