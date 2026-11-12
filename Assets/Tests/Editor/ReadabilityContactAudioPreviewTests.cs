using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class ReadabilityContactAudioPreviewTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> created=new List<GameObject>();
    private bool previousActive,previousEnabled,previousContact,previousSkillAudio,previousBasicOnly,previousReduced;
    private CombatReadabilityPreset previousPreset;
    private float previousScale;
    private int previousSeed;
    private object previousAudio,previousFrontend;
    private GameAudio audio;
    private AudioSource[] voices;
    private CombatSkillClock stageClock;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        previousActive=CombatEffectTestConfiguration.Active;
        previousEnabled=CombatReadabilityPreview.Enabled;
        previousPreset=CombatReadabilityPreview.Preset;
        previousContact=CombatReadabilityPreview.ContactFeedback;
        previousSkillAudio=CombatReadabilityPreview.SkillAudio;
        previousBasicOnly=CombatReadabilityPreview.BasicOnly;
        previousSeed=CombatReadabilityPreview.Seed;
        previousReduced=CombatFlashPresentation.Reduced;
        previousScale=Time.timeScale;
        previousAudio=Field(typeof(GameAudio),"instance").GetValue(null);
        previousFrontend=Field(typeof(GameFrontend),"instance").GetValue(null);
        CombatReadabilityPreview.Reset();
        CombatEffectTestConfiguration.Active=true;
        CombatReadabilityPreview.Enabled=true;
        CombatFlashPresentation.Apply(false);
        Time.timeScale=1f;
        Field(typeof(GameFrontend),"instance").SetValue(null,null);
        foreach(string key in new[]{"Feedback/basic_hit","Feedback/death_burst","Raptor/rank3_impact",
                    "Wyvern/breath","Asmr/attack_swing","Asmr/hit_body"})
        {
            var clip=Resources.Load<AudioClip>("Audio/Combat/"+key);
            Assert.That(clip,Is.Not.Null,key);clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
        }
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.scene.IsValid()&&effect.name=="Contact impact")effect.ReleaseAfterBattleEndFade();
    }

    [TearDown]
    public void Cleanup()
    {
        stageClock?.End();
        foreach(var go in created)if(go!=null)Object.DestroyImmediate(go);
        created.Clear();
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.scene.IsValid()&&
               (effect.name=="Contact impact"||effect.name=="Contact impact pool"))Object.DestroyImmediate(effect.gameObject);
        Field(typeof(GameAudio),"instance").SetValue(null,previousAudio);
        Field(typeof(GameFrontend),"instance").SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousActive;
        CombatReadabilityPreview.Enabled=previousEnabled;
        CombatReadabilityPreview.Preset=previousPreset;
        CombatReadabilityPreview.ContactFeedback=previousContact;
        CombatReadabilityPreview.SkillAudio=previousSkillAudio;
        CombatReadabilityPreview.BasicOnly=previousBasicOnly;
        CombatReadabilityPreview.Seed=previousSeed;
        CombatFlashPresentation.Apply(previousReduced);
        Time.timeScale=previousScale;
    }

    [TestCase(false,true,CombatReadabilityPreset.Local,1f)]
    [TestCase(true,false,CombatReadabilityPreset.Local,1f)]
    [TestCase(true,true,CombatReadabilityPreset.Current,1f)]
    [TestCase(true,true,CombatReadabilityPreset.Compact,.55f)]
    [TestCase(true,true,CombatReadabilityPreset.Local,.55f)]
    public void ContactFlashChangesOnlyInsideAnAdjustedTestComparison(bool testMode,bool enabled,CombatReadabilityPreset preset,float strength)
    {
        CombatEffectTestConfiguration.Active=testMode;CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=preset;
        var go=Spawn();var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var reaction=go.GetComponent<VoxelEnemyReaction>()??go.AddComponent<VoxelEnemyReaction>();
        Vector3 position=go.transform.position;string motion=driver.CurrentMotionName;
        Action callback=()=>{};Field(typeof(SpriteMonsterAnimationDriver),"pendingBasicAttackHit").SetValue(driver,callback);
        string random=JsonUtility.ToJson(UnityEngine.Random.state);
        reaction.PlayHit(Vector3.right);
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.55f*strength).Within(.0001f));
        Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(.09f));
        Assert.That(Get<float>(reaction,"contactAccentCooldown"),Is.EqualTo(.12f));
        Assert.That(go.transform.position,Is.EqualTo(position));
        Assert.That(driver.CurrentMotionName,Is.EqualTo(motion));
        Assert.That(Field(typeof(SpriteMonsterAnimationDriver),"pendingBasicAttackHit").GetValue(driver),Is.SameAs(callback));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
    }

    [Test]
    public void ContactToggleSuppressesOnlyNewVisualFeedback()
    {
        var go=Spawn();var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var renderer=driver.GetComponent<SpriteRenderer>();
        var reaction=go.GetComponent<VoxelEnemyReaction>()??go.AddComponent<VoxelEnemyReaction>();
        CombatReadabilityPreview.ContactFeedback=false;
        Vector3 position=go.transform.position;
        reaction.PlayHit(Vector3.right,true);
        Assert.That(Get<float>(reaction,"flashTimer"),Is.Zero);
        Assert.That(Get<float>(reaction,"contactAccentCooldown"),Is.Zero);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
        Assert.That(PixelCombatEffect.PlayContactImpact(go.transform,renderer,Vector3.right,true),Is.Null);
        Assert.That(go.transform.position,Is.EqualTo(position));
        Assert.That(reaction.IsDying,Is.False);
    }

    [TestCase(false,true)]
    [TestCase(true,false)]
    public void NonComparisonScenesIgnoreTheContactToggle(bool testMode,bool enabled)
    {
        CombatEffectTestConfiguration.Active=testMode;CombatReadabilityPreview.Enabled=enabled;
        CombatReadabilityPreview.ContactFeedback=false;
        var go=Spawn();var reaction=go.GetComponent<VoxelEnemyReaction>()??go.AddComponent<VoxelEnemyReaction>();
        reaction.PlayHit(Vector3.right);
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.55f));
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.EqualTo(1));
    }

    [Test]
    public void SmallerContactPreservesFourFramesDurationAndFlashWhileFollowingTheVisibleBody()
    {
        var renderer=Spawn().GetComponentInChildren<SpriteRenderer>();
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        Call(renderer.GetComponent<SpriteMonsterAnimationDriver>(),"LateUpdate");
        var original=PixelCombatEffect.PlayContactImpact(renderer.transform,renderer,Vector3.right,false);
        Vector3 size=original.transform.localScale,position=original.transform.position;
        float alpha=original.GetComponent<SpriteRenderer>().color.a;
        original.ReleaseAfterBattleEndFade();
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        Call(renderer.GetComponent<SpriteMonsterAnimationDriver>(),"LateUpdate");
        var adjusted=PixelCombatEffect.PlayContactImpact(renderer.transform,renderer,Vector3.right,false);
        Assert.That(adjusted.transform.localScale.x,Is.EqualTo(size.x*.75f*CombatReadabilityPreview.EffectVisualScale).Within(.0001f));
        Vector3 expected=CombatReadabilityPreview.VisibleBodyCenter(renderer)-Vector3.right*Mathf.Min(.18f,renderer.bounds.size.y*.18f);
        Assert.That(Vector3.Distance(adjusted.transform.position,expected),Is.LessThan(.0001f));
        Assert.That(adjusted.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(alpha*.775f).Within(.0001f));
        var frames=(Sprite[])Field(typeof(PixelCombatEffect),"contactImpactFrames").GetValue(null);
        Assert.That(frames.Length,Is.EqualTo(4));
        CombatFlashPresentation.Apply(true);
        Call(adjusted,"LateUpdate");
        Assert.That(adjusted.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(alpha*.775f*.25f).Within(.0001f));
        Call(adjusted,"Advance",.11f);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
    }

    [TestCase(CombatSoundCue.Cast,.2f)]
    [TestCase(CombatSoundCue.Impact,.3f)]
    [TestCase(CombatSoundCue.Periodic,.12f)]
    public void OnlyOrdinaryAuthoredSkillGainIsLoweredInTheComparison(CombatSoundCue cue,float gain)
    {
        CreateAudio();var clip=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank3_impact");
        Field(typeof(GameAudio),"authoredBasicHit").SetValue(audio,clip);
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        GameAudio.PlayCombat(CombatSoundStyle.Bite,cue,1,Vector3.zero,authoredSound:clip);
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(gain).Within(.0001f));
        audio.StopCombatSounds();CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        string random=JsonUtility.ToJson(UnityEngine.Random.state);
        GameAudio.PlayCombat(CombatSoundStyle.Bite,cue,2,Vector3.zero,authoredSound:clip);
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(gain*.7f).Within(.0001f));
        Assert.That(voices[0].clip,Is.SameAs(clip));
        Assert.That(voices[0].pitch,Is.EqualTo(1f));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
    }

    [TestCase(false,true)]
    [TestCase(true,false)]
    public void NonComparisonScenesIgnoreTheSkillMuteAndGainSettings(bool testMode,bool enabled)
    {
        CreateAudio();CombatEffectTestConfiguration.Active=testMode;CombatReadabilityPreview.Enabled=enabled;
        CombatReadabilityPreview.SkillAudio=false;
        var clip=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank3_impact");
        Field(typeof(GameAudio),"authoredBasicHit").SetValue(audio,clip);
        GameAudio.PlayCombat(CombatSoundStyle.Bite,CombatSoundCue.Impact,1,Vector3.zero,authoredSound:clip);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(.3f).Within(.0001f));
    }

    [Test]
    public void SkillMuteLeavesBasicMotionContactAndDeathEventsAvailable()
    {
        CreateAudio();CombatReadabilityPreview.SkillAudio=false;
        var clip=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank3_impact");
        foreach(var cue in new[]{CombatSoundCue.Cast,CombatSoundCue.Impact,CombatSoundCue.Periodic})
            GameAudio.PlayCombat(CombatSoundStyle.Bite,cue,1,Vector3.zero,authoredSound:clip);
        Assert.That(audio.PlayedCombatSounds,Is.Zero);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Attack,2,Vector3.zero);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Hit,2,Vector3.zero);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Death,2,Vector3.zero);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(3));
    }

    [Test]
    public void ExplicitCinematicAndExclusiveClockSoundsKeepTheirAuthoredGain()
    {
        CreateAudio();var clip=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank3_impact");
        Field(typeof(GameAudio),"authoredBasicHit").SetValue(audio,clip);
        Time.timeScale=0f;
        GameAudio.PlayCombat(CombatSoundStyle.Bite,CombatSoundCue.Impact,1,Vector3.zero,authoredSound:clip,allowDuringTimeStop:true);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(.3f).Within(.0001f));
        audio.StopCombatSounds();Time.timeScale=1f;
        stageClock=new CombatSkillClock(null);stageClock.Hold();
        GameAudio.PlayCombat(CombatSoundStyle.Bite,CombatSoundCue.Impact,2,Vector3.zero,authoredSound:clip);
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(.3f).Within(.0001f));
    }

    [Test]
    public void QuieterBreathStillFinishesItsCurrentLoopNormally()
    {
        CreateAudio();var clip=Resources.Load<AudioClip>("Audio/Combat/Wyvern/breath");
        Field(typeof(GameAudio),"authoredBasicHit").SetValue(audio,clip);
        GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Cast,1,Vector3.zero,authoredSound:clip,loop:true);
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(.11f*.7f).Within(.0001f));
        Assert.That(voices[0].loop,Is.True);
        bool playing=voices[0].isPlaying;int sample=voices[0].timeSamples;
        GameAudio.StopCombatLoop(1,clip,true);
        Assert.That(voices[0].loop,Is.False);
        Assert.That(voices[0].isPlaying,Is.EqualTo(playing));
        Assert.That(voices[0].timeSamples,Is.GreaterThanOrEqualTo(sample));
    }

    private GameObject Spawn()
    {
        var go=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
        created.Add(go);return go;
    }

    private void CreateAudio()
    {
        var host=new GameObject("Inactive readability audio fixture");host.SetActive(false);created.Add(host);
        var voiceHost=new GameObject("Readability audio fixture voices");created.Add(voiceHost);
        audio=host.AddComponent<GameAudio>();Call(audio,"InitializeCombatAudio");
        Field(typeof(GameAudio),"instance").SetValue(null,audio);Field(typeof(GameAudio),"previewEnabled").SetValue(audio,true);
        voices=Get<AudioSource[]>(audio,"combatVoices");var layers=Get<AudioSource[]>(audio,"combatLayers");
        for(int i=0;i<voices.Length;i++)
        {
            voices[i]=voiceHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            layers[i]=voiceHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
        }
    }

    private static FieldInfo Field(Type type,string name)=>type.GetField(name,Hidden);
    private static T Get<T>(object value,string field)=>(T)Field(value.GetType(),field).GetValue(value);
    private static void Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Hidden).Invoke(value,args.Length==0?null:args);
}
