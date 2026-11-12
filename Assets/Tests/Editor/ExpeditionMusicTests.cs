using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ExpeditionMusicTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string MusicPath = "Sounds/ExpeditionPredatorGround";
    private GameObject host, voiceHost;
    private GameAudio audio;
    private AudioSource music;
    private AudioClip expedition, town, calm;
    private FieldInfo singleton;
    private object previous;

    [SetUp]
    public void Setup()
    {
        expedition = Resources.Load<AudioClip>(MusicPath);
        town = Resources.Load<AudioClip>("Sounds/TownAfternoonBGM");
        Assert.That(expedition, Is.Not.Null);
        Assert.That(town, Is.Not.Null);
        singleton = typeof(GameAudio).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        previous = singleton.GetValue(null);
        host = new GameObject("Expedition music test");
        host.SetActive(false);
        audio = host.AddComponent<GameAudio>();
        singleton.SetValue(null, audio);
        voiceHost = new GameObject("Active expedition music test voices");
        music = voiceHost.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        Set("music", music);
        var effects = voiceHost.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        Set("effects", effects);
        foreach (string field in new[] { "combatVoices", "combatLayers" })
        {
            var sources = (AudioSource[])Get(field);
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = host.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
        }
        calm = AudioClip.Create("Expedition test camp theme", 48000, 1, 48000, false);
        Set("calmLoop", calm);
        Set("authoredTownBgm", town);
    }

    [TearDown]
    public void Cleanup()
    {
        if (audio != null) Set("calmLoop", null);
        if (singleton != null) singleton.SetValue(null, previous);
        if (host != null) Object.DestroyImmediate(host);
        if (voiceHost != null) Object.DestroyImmediate(voiceHost);
        if (calm != null) Object.DestroyImmediate(calm);
    }

    [Test]
    public void SuppliedTwoMinuteMusicStreamsInStereoWithoutPreloadingTheWholeTrack()
    {
        Assert.That(expedition.length, Is.EqualTo(120f).Within(.02f));
        Assert.That(expedition.frequency, Is.EqualTo(48000));
        Assert.That(expedition.channels, Is.EqualTo(2));
        Assert.That(expedition.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
        Assert.That(expedition.preloadAudioData, Is.False);
        Assert.That(expedition.loadInBackground, Is.True);
        var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(expedition));
        Assert.That(importer.forceToMono, Is.False);
    }

    [TestCase(0f)]
    [TestCase(.37f)]
    [TestCase(1f)]
    public void ExpeditionMusicUsesTheMusicSliderAtAQuietGainAndFadesIn(float volume)
    {
        audio.Apply(new GameUserSettings { musicVolume = volume, effectsVolume = .4f });
        Assert.That(Get("authoredExpeditionBgm"), Is.Null, "The track is requested when entering the expedition.");
        audio.SetScene(GameSceneCatalog.ExpeditionBattle);
        Assert.That(music.clip, Is.SameAs(expedition));
        Assert.That(music.loop, Is.True);
        Assert.That(Get("musicFadeDuration"), Is.EqualTo(.8f));
        Assert.That(music.volume, Is.Zero);
        Assert.That((float)Get("musicTargetVolume"), Is.EqualTo(volume * .06f).Within(.00001f));
        Set("musicFadeDuration", 0f);
        audio.Apply(new GameUserSettings { musicVolume = volume, effectsVolume = 0f });
        Assert.That(music.volume, Is.EqualTo(volume * .06f).Within(.00001f), "Effects volume cannot mute the BGM.");
    }

    [Test]
    public void RepeatedSceneNotificationsAndCombatTransitionsKeepTheSameMusicPlayback()
    {
        // Use a seekable in-memory clip to verify continuity without waiting for disk streaming.
        Set("authoredExpeditionBgm", calm);
        audio.SetScene(GameSceneCatalog.ExpeditionBattle);
        music.timeSamples = 24000;
        int sample = music.timeSamples;
        Assert.That(sample, Is.GreaterThan(0));
        Set("musicFadeElapsed", .3f);
        music.volume = .02f;
        int sources = voiceHost.GetComponents<AudioSource>().Length;
        bool playing = music.isPlaying;
        audio.SetScene(GameSceneCatalog.ExpeditionBattle);
        GameAudio.SetBattlePhase(true);
        GameAudio.ResetCombatSounds();
        GameAudio.SetBattlePhase(false);
        GameAudio.ResetCombatSounds();
        Assert.That(music.clip, Is.SameAs(calm));
        Assert.That(music.loop, Is.True);
        Assert.That(music.isPlaying, Is.EqualTo(playing));
        Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(sample), "Combat cleanup must not rewind the BGM.");
        Assert.That(Get("musicFadeElapsed"), Is.EqualTo(.3f), "No Wave, result or repeated scene event may restart the fade.");
        Assert.That(music.volume, Is.EqualTo(.02f));
        Assert.That(voiceHost.GetComponents<AudioSource>().Length, Is.EqualTo(sources));
    }

    [TestCase(GameSceneCatalog.Boot)]
    [TestCase(GameSceneCatalog.Town)]
    [TestCase(GameSceneCatalog.Battle)]
    [TestCase(GameSceneCatalog.Result)]
    public void LeavingTheExpeditionRestoresTheExistingSceneMusic(string scene)
    {
        audio.SetScene(GameSceneCatalog.ExpeditionBattle);
        audio.SetScene(scene);
        AudioClip expected = scene == GameSceneCatalog.Boot ? null : scene == GameSceneCatalog.Town ? town : calm;
        Assert.That(music.clip, Is.SameAs(expected));
        Assert.That(Get("musicFadeDuration"), Is.Zero);
        Assert.That(music.volume, Is.EqualTo(expected == null ? 0f : expected == town ? .16f : .32f).Within(.00001f));
    }

    [Test]
    public void ReturningToTheMainMenuKeepsItsExistingCampThemeFade()
    {
        audio.SetScene(GameSceneCatalog.ExpeditionBattle);
        audio.StartMainMenuMusic();
        Assert.That(music.clip, Is.SameAs(calm));
        Assert.That(Get("musicFadeDuration"), Is.EqualTo(3.4f));
        Assert.That(music.volume, Is.Zero);
        Assert.That(Get("musicTargetVolume"), Is.EqualTo(.32f));
    }

    private object Get(string field) => typeof(GameAudio).GetField(field, Private).GetValue(audio);
    private void Set(string field, object value) => typeof(GameAudio).GetField(field, Private).SetValue(audio, value);
}
