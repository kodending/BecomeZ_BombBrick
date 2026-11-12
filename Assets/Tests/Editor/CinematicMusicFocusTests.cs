using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CinematicMusicFocusTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject host,voices;
    private GameAudio audio;
    private AudioSource music,effects;
    private AudioClip calm,town,expedition;
    private object previous;
    private float previousTimeScale;

    [SetUp] public void Setup()
    {
        previous=Field("instance").GetValue(null);previousTimeScale=Time.timeScale;
        host=new GameObject("Inactive cinematic music focus tests");host.SetActive(false);audio=host.AddComponent<GameAudio>();
        Field("instance").SetValue(null,audio);
        voices=new GameObject("Cinematic music focus test voices");
        music=voices.AddComponent<AudioSource>();effects=voices.AddComponent<AudioSource>();
        music.playOnAwake=effects.playOnAwake=false;Set("music",music);Set("effects",effects);
        foreach(string name in new[]{"combatVoices","combatLayers"})
        {
            var sources=(AudioSource[])Get(name);
            for(int i=0;i<sources.Length;i++){sources[i]=host.AddComponent<AudioSource>();sources[i].playOnAwake=false;}
        }
        calm=AudioClip.Create("Silent camp focus test",64,1,44100,false);
        town=AudioClip.Create("Silent town focus test",64,1,44100,false);
        expedition=AudioClip.Create("Silent expedition focus test",64,1,44100,false);
        Set("calmLoop",calm);Set("authoredTownBgm",town);Set("authoredExpeditionBgm",expedition);
        music.clip=calm;audio.Apply(new GameUserSettings{musicVolume=.8f,effectsVolume=.42f});
    }

    [TearDown] public void Cleanup()
    {
        Set("calmLoop",null);Field("instance").SetValue(null,previous);
        if(host!=null)Object.DestroyImmediate(host);if(voices!=null)Object.DestroyImmediate(voices);
        Object.DestroyImmediate(calm);Object.DestroyImmediate(town);Object.DestroyImmediate(expedition);
        Time.timeScale=previousTimeScale;
    }

    [Test] public void MissingInstanceAndRepeatedFocusCallsAreHarmlessAndOnlyMusicChanges()
    {
        Field("instance").SetValue(null,null);
        Assert.DoesNotThrow(()=>GameAudio.SetCinematicAudioFocus(true));
        Assert.DoesNotThrow(()=>GameAudio.SetCinematicAudioFocus(false));Field("instance").SetValue(null,audio);
        float baseVolume=music.volume,effectVolume=effects.volume;
        var combat=(AudioSource[])Get("combatVoices");float combatVolume=combat[0].volume;
        GameAudio.SetCinematicAudioFocus(true);Step(.05f);
        Assert.That(music.volume,Is.InRange(baseVolume*.35f+.0001f,baseVolume-.0001f));
        GameAudio.SetCinematicAudioFocus(true);Step(.05f);Step(1f);
        Assert.That(music.volume,Is.EqualTo(baseVolume*.35f).Within(.00001f));
        Assert.That(effects.volume,Is.EqualTo(effectVolume));Assert.That(combat[0].volume,Is.EqualTo(combatVolume));
        Assert.That(Get("configuredMusicVolume"),Is.EqualTo(.8f));
        Assert.That(Get("musicTargetVolume"),Is.EqualTo(baseVolume));
    }

    [Test] public void ReleaseUsesUnscaledTimeAndRestoresTheSameMusicEvenDuringAHitStop()
    {
        float original=music.volume;GameAudio.SetCinematicAudioFocus(true);Step(.1f);
        Time.timeScale=0f;GameAudio.SetCinematicAudioFocus(false);Step(.11f);
        Assert.That(music.volume,Is.GreaterThan(original*.35f).And.LessThan(original));
        GameAudio.SetCinematicAudioFocus(false);Step(.11f);
        Assert.That(music.volume,Is.EqualTo(original).Within(.00001f));
        Assert.That(music.clip,Is.SameAs(calm));Assert.That(Time.timeScale,Is.Zero);
    }

    [TestCase(0f)] [TestCase(.47f)]
    public void SettingsAndMusicReplacementRespectTheUserGainWithoutSavingADuckedSetting(float volume)
    {
        GameAudio.SetCinematicAudioFocus(true);Step(.1f);
        var settings=new GameUserSettings{musicVolume=volume,effectsVolume=.27f};audio.Apply(settings);
        Assert.That(music.volume,Is.EqualTo(volume*.32f*.35f).Within(.00001f));
        Switch(town,0f);Assert.That(music.volume,Is.EqualTo(volume*.16f*.35f).Within(.00001f));
        Switch(expedition,0f);Assert.That(music.volume,Is.EqualTo(volume*.06f*.35f).Within(.00001f));
        GameAudio.SetCinematicAudioFocus(false);Step(.22f);
        Assert.That(music.volume,Is.EqualTo(volume*.06f).Within(.00001f));
        Assert.That(settings.musicVolume,Is.EqualTo(volume));Assert.That(Get("configuredMusicVolume"),Is.EqualTo(volume));
        Assert.That(effects.volume,Is.EqualTo(.27f));
    }

    [Test] public void CinematicFocusMultipliesTheExistingFadeAndReleasesWithoutRestartingIt()
    {
        Switch(expedition,.8f);GameAudio.SetCinematicAudioFocus(true);Step(.1f);
        Assert.That(music.volume,Is.EqualTo(.8f*.06f*.35f*Mathf.SmoothStep(0f,1f,.1f/.8f)).Within(.00001f));
        audio.Apply(new GameUserSettings{musicVolume=.6f,effectsVolume=.42f});
        Assert.That(music.volume,Is.EqualTo(.6f*.06f*.35f*Mathf.SmoothStep(0f,1f,.1f/.8f)).Within(.00001f));
        GameAudio.SetCinematicAudioFocus(false);Step(.22f);
        Assert.That(Get("musicFadeElapsed"),Is.EqualTo(.32f).Within(.00001f));
        Assert.That(Get("musicFadeDuration"),Is.EqualTo(.8f));
        Assert.That(music.volume,Is.EqualTo(.6f*.06f*Mathf.SmoothStep(0f,1f,.32f/.8f)).Within(.00001f));
        Step(1f);Assert.That(Get("musicFadeDuration"),Is.EqualTo(0f));
        Assert.That(music.volume,Is.EqualTo(.6f*.06f).Within(.00001f));
    }

    private static FieldInfo Field(string name)=>typeof(GameAudio).GetField(name,Hidden);
    private object Get(string name)=>Field(name).GetValue(audio);
    private void Set(string name,object value)=>Field(name).SetValue(audio,value);
    private void Step(float unscaledDeltaTime)=>typeof(GameAudio).GetMethod("AdvanceMusic",Hidden).Invoke(audio,new object[]{unscaledDeltaTime});
    private void Switch(AudioClip clip,float fade)=>typeof(GameAudio).GetMethod("SwitchMusic",Hidden).Invoke(audio,new object[]{clip,fade});
}
