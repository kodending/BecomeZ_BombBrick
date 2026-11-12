using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class Rank4CinematicAudioTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly Type AudioType=typeof(Rank4CinematicAudio);
    private GameObject parent,musicHost;
    private GameAudio music;
    private Rank4CinematicAudio audio;
    private object oldBanks,oldCurrent,oldMusic;
    private float oldSpeed;
    private readonly List<AudioClip> clips=new List<AudioClip>();

    [SetUp]public void Setup()
    {
        oldBanks=Field("banks").GetValue(null);oldCurrent=Field("current").GetValue(null);oldSpeed=Time.timeScale;
        oldMusic=MusicField("instance").GetValue(null);
        musicHost=new GameObject("Inactive continuous cinematic focus fixture");musicHost.SetActive(false);
        music=musicHost.AddComponent<GameAudio>();MusicField("instance").SetValue(null,music);
        parent=new GameObject("Continuous cinematic audio fixture");
        Type bankType=AudioType.GetNestedType("Bank",Hidden);
        var banks=(IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string),bankType));
        foreach(string family in new[]{"spider","beetle","raptor"})banks.Add(family,Bank(family));
        Field("banks").SetValue(null,banks);Field("current").SetValue(null,null);
        audio=new Rank4CinematicAudio(parent.transform);
    }

    [TearDown]public void Cleanup()
    {
        audio?.Dispose();
        if(parent!=null)Object.DestroyImmediate(parent);if(musicHost!=null)Object.DestroyImmediate(musicHost);
        foreach(var clip in clips)Object.DestroyImmediate(clip);clips.Clear();
        Field("banks").SetValue(null,oldBanks);Field("current").SetValue(null,oldCurrent);
        MusicField("instance").SetValue(null,oldMusic);Time.timeScale=oldSpeed;
    }

    [Test]public void ThreeFiniteTwoDimensionalVoicesHaveNoStartupAudioOrMusicFocus()
    {
        var voices=parent.GetComponentsInChildren<AudioSource>(true);
        Assert.That(voices,Has.Length.EqualTo(3));
        foreach(var voice in voices)
        {
            Assert.That(voice.spatialBlend,Is.Zero);Assert.That(voice.pitch,Is.EqualTo(1f));
            Assert.That(voice.playOnAwake,Is.False);Assert.That(voice.loop,Is.False);Assert.That(voice.isPlaying,Is.False);
        }
        Assert.That(Focused,Is.False);
        Assert.That(Rank4CinematicAudio.Supports("arachne"),Is.True);
        Assert.That(Rank4CinematicAudio.Supports(" SPIDER "),Is.True);
        Assert.That(Rank4CinematicAudio.Supports("raptor"),Is.True);
        Assert.That(Rank4CinematicAudio.Supports(null),Is.False);
    }

    [TestCase(false)][TestCase(true)]
    public void OneContinuousTrackSurvivesRepeatedBackwardAndStalledFrameTicksWithoutRestarting(bool enabled)
    {
        Time.timeScale=0f;audio.Begin("arachne",enabled);
        object bank=Get("bank");AudioSource voice=Voice;AudioClip clip=voice.clip;
        foreach(float time in new[]{.4f,1.3f,1.3f,.2f,1.9f})
        {
            audio.Tick(time);
            Assert.That(Get("bank"),Is.SameAs(bank));Assert.That(Voice,Is.SameAs(voice));
            Assert.That(voice.clip,Is.SameAs(clip));Assert.That(voice.timeSamples,Is.Zero);
        }
        Assert.That(clip!=null,Is.EqualTo(enabled));
        Assert.That(voice.isPlaying,Is.False,"EditMode must never produce audible playback.");
        Assert.That(Focused,Is.False);Assert.That(Time.timeScale,Is.Zero);
    }

    [Test]public void PreludeAndMainCrossfadeForOneTenthSecondInsteadOfCuttingAtTheShotBoundary()
    {
        audio.PlayPrelude("spider",true);
        AudioSource prelude=(AudioSource)Get("preludeSource");AudioClip preludeClip=prelude.clip;
        float fullPrelude=prelude.volume;
        audio.Begin("spider",true);AudioSource main=Voice;
        Assert.That(prelude.clip,Is.SameAs(preludeClip));Assert.That(main.clip,Is.Not.Null);
        Assert.That(main.volume,Is.Zero);Assert.That(Get("mainFade"),Is.EqualTo(.1f));
        audio.UpdateAudio(.05f);
        Assert.That(prelude.volume,Is.EqualTo(fullPrelude*.5f).Within(.0001f));
        Assert.That(main.volume,Is.EqualTo((float)Get("mainGain")*.5f).Within(.0001f));
        audio.UpdateAudio(.05f);
        Assert.That(prelude.clip,Is.Null);Assert.That(main.clip,Is.Not.Null);
        Assert.That(main.volume,Is.EqualTo((float)Get("mainGain")).Within(.0001f));
    }

    [TestCase(false)][TestCase(true)]
    public void NormalCompletionKeepsTheRecordedTailAndTheNextFilmUsesTheOtherVoice(bool finishAtDeclaredEnd)
    {
        audio.Begin("spider",true);AudioSource ending=Voice;AudioClip tail=ending.clip;
        if(finishAtDeclaredEnd)audio.Tick(2f);else audio.Finish();
        Assert.That(Get("bank"),Is.Null);Assert.That(ending.clip,Is.SameAs(tail));
        audio.Tick(30f);Assert.That(ending.clip,Is.SameAs(tail));
        audio.PlayPrelude("beetle",true);
        Assert.That(ending.clip,Is.SameAs(tail),"The next attention cue must leave the preceding natural tail intact.");
        audio.Begin("beetle",true);
        Assert.That(Voice,Is.Not.SameAs(ending));Assert.That(ending.clip,Is.SameAs(tail));
        Assert.That(Voice.clip,Is.Not.Null);Assert.That(parent.GetComponentsInChildren<AudioSource>(true),Has.Length.EqualTo(3));
    }

    [Test]public void CancellationDisablingAndDisposalClearEveryVoiceWithoutDestroyingSharedClips()
    {
        audio.Begin("spider",true);AudioClip shared=Voice.clip;audio.Finish();
        audio.PlayPrelude("beetle",true);audio.Begin("beetle",true);OwnFocus(audio);
        var driver=parent.GetComponentInChildren<Rank4CinematicAudioDriver>();driver.gameObject.SetActive(false);
        // EditMode does not dispatch normal MonoBehaviour lifecycle callbacks for this new driver.
        typeof(Rank4CinematicAudioDriver).GetMethod("OnDisable",Hidden).Invoke(driver,null);
        foreach(var voice in parent.GetComponentsInChildren<AudioSource>(true))Assert.That(voice.clip,Is.Null);
        Assert.That(Get("bank"),Is.Null);Assert.That(Focused,Is.False);
        audio.Stop();audio.Dispose();audio.Dispose();
        Assert.That(parent.GetComponentsInChildren<AudioSource>(true),Is.Empty);
        Assert.That(shared,Is.Not.Null);
    }

    [Test]public void TheRaptorTailCannotUnduckTheNextFamilysMainTrackWhenItEndsOrIsDestroyed()
    {
        var other=new Rank4CinematicAudio(parent.transform);
        try
        {
            audio.Begin("raptor",true);AudioSource ending=Voice;AudioClip tail=ending.clip;audio.Finish();
            AudioType.GetMethod("ClaimFocus",Hidden).Invoke(audio,null);
            other.Begin("spider",true);AudioType.GetMethod("ClaimFocus",Hidden).Invoke(other,null);
            Assert.That(ending.clip,Is.SameAs(tail));Assert.That(Get("focusHeld"),Is.False);
            audio.UpdateAudio(1f);audio.Dispose();
            Assert.That(Focused,Is.True);Assert.That(Field("current").GetValue(null),Is.SameAs(other));
            other.Stop();Assert.That(Focused,Is.False);Assert.That(Field("current").GetValue(null),Is.Null);
        }
        finally{other.Dispose();}
    }

    [Test]public void ACompletedAudibleTailReleasesMusicFocusEvenWhileTheSameOwnersNextFilmIsMuted()
    {
        audio.Begin("spider",true);AudioSource ending=Voice;
        ending.Play();Assert.That(ending.isPlaying,Is.True);OwnFocus(audio);audio.Finish();
        audio.PlayPrelude("beetle",false);audio.Begin("beetle",false);object mutedBank=Get("bank");
        Assert.That(mutedBank,Is.Not.Null);Assert.That(Voice,Is.Not.SameAs(ending));
        Assert.That(Voice.clip,Is.Null);audio.UpdateAudio(0f);
        Assert.That(Focused,Is.True,"The preceding audible tail still owns a short music duck.");
        ending.Stop();audio.UpdateAudio(0f);
        Assert.That(Focused,Is.False,"A silent active bank must not extend the previous film's music duck.");
        Assert.That(Get("bank"),Is.SameAs(mutedBank),"The muted presentation continues normally.");
        Assert.That(Field("current").GetValue(null),Is.Null);
    }

    [Test]public void GeneralPresentationCleanupKeepsCompletedTailsButCancelsOnlyTheActiveFilmAndPrelude()
    {
        audio.Begin("spider",true);AudioSource ending=Voice;AudioClip tail=ending.clip;audio.Finish();
        audio.CancelPresentation();Assert.That(ending.clip,Is.SameAs(tail));
        audio.PlayPrelude("beetle",true);audio.CancelPresentation();
        Assert.That(((AudioSource)Get("preludeSource")).clip,Is.Null);Assert.That(ending.clip,Is.SameAs(tail));
        audio.Begin("beetle",true);AudioSource active=Voice;audio.CancelPresentation();
        Assert.That(active.clip,Is.Null);Assert.That(ending.clip,Is.SameAs(tail));
        Rank4CinematicAudio.StopAll();Assert.That(ending.clip,Is.Null);
    }

    [Test]public void SilentWarmupAndMutedFilmsDoNotAcquireFocusOrCreateAdditionalVoices()
    {
        GameAudio.SetCinematicAudioFocus(true);Time.timeScale=0f;
        Rank4CinematicAudio.Warmup();var routine=Rank4CinematicAudio.WarmupClips();int waits=0;
        while(routine.MoveNext())Assert.That(++waits,Is.LessThan(16));
        audio.PlayPrelude("spider",false);audio.Begin("spider",false);audio.Tick(1f);audio.Finish();
        foreach(var voice in parent.GetComponentsInChildren<AudioSource>(true))
        {Assert.That(voice.isPlaying,Is.False);Assert.That(voice.clip,Is.Null);}
        Assert.That(parent.GetComponentsInChildren<AudioSource>(true),Has.Length.EqualTo(3));
        Assert.That(Focused,Is.True);Assert.That(Get("focusHeld"),Is.False);Assert.That(Time.timeScale,Is.Zero);
    }

    private object Bank(string family)
    {
        object bank=Activator.CreateInstance(AudioType.GetNestedType("Bank",Hidden),true);
        Set(bank,"family",family);Set(bank,"duration",2f);
        Set(bank,"prelude",Cue("prelude",.55f));Set(bank,"timeline",Cue("timeline",2.65f));return bank;
    }
    private object Cue(string key,float duration)
    {
        object cue=Activator.CreateInstance(AudioType.GetNestedType("Cue",Hidden),true);
        var clip=AudioClip.Create("Silent continuous "+key,Mathf.RoundToInt(duration*48000),2,48000,false);clips.Add(clip);
        Set(cue,"key",key);Set(cue,"duration",duration);Set(cue,"gain",1f);Set(cue,"clip",clip);return cue;
    }
    private void OwnFocus(Rank4CinematicAudio value)
    {Field("focusHeld").SetValue(value,true);Field("current").SetValue(null,value);GameAudio.SetCinematicAudioFocus(true);}
    private AudioSource Voice=>(AudioSource)Get("source");
    private bool Focused=>(bool)MusicField("cinematicAudioFocus").GetValue(music);
    private object Get(string name)=>Field(name).GetValue(audio);
    private static void Set(object value,string name,object content)=>value.GetType().GetField(name,Hidden).SetValue(value,content);
    private static FieldInfo Field(string name)=>AudioType.GetField(name,Hidden);
    private static FieldInfo MusicField(string name)=>typeof(GameAudio).GetField(name,Hidden);
}
