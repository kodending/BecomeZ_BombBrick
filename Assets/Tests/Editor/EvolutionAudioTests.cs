using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EvolutionAudioTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private GameObject host, voicesHost;
    private GameAudio audio;
    private object previousSingleton;
    private AudioSource[] voices;
    private AudioClip[] clips;
    private readonly List<AudioClip> generated = new List<AudioClip>();

    private static FieldInfo Field(string name) => typeof(GameAudio).GetField(name, Hidden);
    private static int VoiceCount(GameAudio owner) => (int)Field("nextChestVoice").GetValue(owner);

    [SetUp]
    public void SetUp()
    {
        previousSingleton = Field("instance").GetValue(null);
        host = new GameObject("Evolution audio test owner");host.SetActive(false);
        voicesHost = new GameObject("Evolution reward voice pool");
        audio = host.AddComponent<GameAudio>();
        voices = (AudioSource[])Field("chestVoices").GetValue(audio);
        for (int i = 0; i < voices.Length; i++)
        { voices[i] = voicesHost.AddComponent<AudioSource>();voices[i].playOnAwake = false; }
        clips = (AudioClip[])Field("evolutionClips").GetValue(audio);
        for (int i = 0; i < clips.Length; i++) clips[i] = Clip("Evolution " + i, .2f);
        Field("authoredBasicHit").SetValue(audio, Clip("RMS reference", .1f));
        Field("effectsVolume").SetValue(audio, .35f);
        Field("previewEnabled").SetValue(audio, true);
        Field("instance").SetValue(null, audio);
    }

    [TearDown]
    public void TearDown()
    {
        Field("instance").SetValue(null, previousSingleton);
        Object.DestroyImmediate(voicesHost);Object.DestroyImmediate(host);
        foreach (var clip in generated) Object.DestroyImmediate(clip);
        generated.Clear();
    }

    private AudioClip Clip(string name, float amplitude)
    {
        var clip = AudioClip.Create(name, 200, 1, 1000, false);
        var data = new float[200];
        for (int i = 0; i < data.Length; i++) data[i] = i % 2 == 0 ? amplitude : -amplitude;
        clip.SetData(data, 0);generated.Add(clip);return clip;
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void MonsterTimelinePlaysEachAuthoredStageOnceWithoutTheOldRewardSounds(int rank)
    {
        int mask=0;
        MonsterRankUpFeedback.PlaySounds(rank,0f,ref mask);
        Assert.That(VoiceCount(audio),Is.EqualTo(1));
        Assert.That(voices[0].clip,Is.SameAs(clips[0]));
        float impact=MonsterEvolutionCelebration.ImpactAt(rank);
        MonsterRankUpFeedback.PlaySounds(rank,impact-.001f,ref mask);
        Assert.That(VoiceCount(audio),Is.EqualTo(1));
        MonsterRankUpFeedback.PlaySounds(rank,impact,ref mask);
        Assert.That(VoiceCount(audio),Is.EqualTo(3));
        Assert.That(voices[1].clip,Is.SameAs(clips[1]));
        Assert.That(voices[2].clip,Is.SameAs(clips[rank+2]));
        MonsterRankUpFeedback.PlaySounds(rank,MonsterEvolutionCelebration.StampAt(rank),ref mask);
        Assert.That(VoiceCount(audio),Is.EqualTo(4));
        Assert.That(voices[3].clip,Is.SameAs(clips[2]));
        float afterglow=MonsterEvolutionCelebration.StampAt(rank)+.201f;
        for(int i=0;i<4;i++)MonsterRankUpFeedback.PlaySounds(rank,afterglow,ref mask);
        Assert.That(VoiceCount(audio),Is.EqualTo(5));
        Assert.That(voices[4].clip,Is.SameAs(clips[3]));
        Assert.That(mask,Is.EqualTo(31));
    }

    [TestCase(GameAudio.EvolutionCue.Preparation, 2, 0, .065f)]
    [TestCase(GameAudio.EvolutionCue.Preparation, 3, 0, .065f)]
    [TestCase(GameAudio.EvolutionCue.Preparation, 4, 0, .065f)]
    [TestCase(GameAudio.EvolutionCue.Impact, 2, 1, .10f)]
    [TestCase(GameAudio.EvolutionCue.Impact, 3, 1, .10f)]
    [TestCase(GameAudio.EvolutionCue.Impact, 4, 1, .10f)]
    [TestCase(GameAudio.EvolutionCue.Stamp, 2, 2, .12f)]
    [TestCase(GameAudio.EvolutionCue.Stamp, 3, 2, .12f)]
    [TestCase(GameAudio.EvolutionCue.Stamp, 4, 2, .12f)]
    [TestCase(GameAudio.EvolutionCue.Afterglow, 2, 3, .05f)]
    [TestCase(GameAudio.EvolutionCue.Afterglow, 3, 3, .05f)]
    [TestCase(GameAudio.EvolutionCue.Afterglow, 4, 3, .05f)]
    [TestCase(GameAudio.EvolutionCue.RankAccent, 2, 4, .032f)]
    [TestCase(GameAudio.EvolutionCue.RankAccent, 3, 5, .037f)]
    [TestCase(GameAudio.EvolutionCue.RankAccent, 4, 6, .042f)]
    public void CueUsesOneAuthoredClipAtOriginalPitchThroughTheExistingNormalizedPool(
        GameAudio.EvolutionCue cue, int rank, int expected, float gain)
    {
        voices[0].loop = true;
        GameAudio.PlayEvolution(cue, rank);
        Assert.That(VoiceCount(audio), Is.EqualTo(1));
        Assert.That(voices[0].clip, Is.SameAs(clips[expected]));
        Assert.That(voices[0].pitch, Is.EqualTo(1f));
        Assert.That(voices[0].loop, Is.False);
        var gains = (float[])Field("chestGains").GetValue(audio);
        Assert.That(gains[0], Is.EqualTo(gain * .5f).Within(.0001f), "RMS matching uses the ordinary SFX reference.");
        Assert.That(voices[0].volume, Is.EqualTo(.35f * gains[0]).Within(.0001f));
        Assert.That(Field("effectsVolume").GetValue(audio), Is.EqualTo(.35f));
        Assert.That(voicesHost.GetComponents<AudioSource>().Length, Is.EqualTo(8));
    }

    [Test]
    public void QuietPreparationIsLevelMatchedAndRankAccentRemainsQuieterThanTheCommonImpact()
    {
        clips[0] = Clip("Quiet preparation", .02f);
        GameAudio.PlayEvolution(GameAudio.EvolutionCue.Preparation, 4);
        GameAudio.PlayEvolution(GameAudio.EvolutionCue.Impact, 4);
        GameAudio.PlayEvolution(GameAudio.EvolutionCue.RankAccent, 4);
        var gains = (float[])Field("chestGains").GetValue(audio);
        Assert.That(gains[0] * .02f, Is.EqualTo(.065f * .1f).Within(.0001f));
        Assert.That(gains[1] * .2f, Is.EqualTo(.10f * .1f).Within(.0001f));
        Assert.That(gains[2], Is.LessThan(gains[1] * .5f), "The rank color stays underneath the impact.");
    }

    [Test]
    public void RepeatedCuesReuseEightVoicesAndMuteUsesTheExistingEffectsOption()
    {
        Field("effectsVolume").SetValue(audio, 0f);
        for (int i = 0; i < 20; i++) GameAudio.PlayEvolution(GameAudio.EvolutionCue.Stamp, 3);
        Assert.That(VoiceCount(audio), Is.EqualTo(20));
        Assert.That(voicesHost.GetComponents<AudioSource>().Length, Is.EqualTo(8));
        foreach (var voice in voices)
        { Assert.That(voice.volume, Is.Zero);Assert.That(voice.loop, Is.False); }
    }

    [TestCase(1)]
    [TestCase(5)]
    public void InvalidRankDoesNotAllocateARewardVoice(int rank)
    {
        GameAudio.PlayEvolution(GameAudio.EvolutionCue.Impact, rank);
        Assert.That(VoiceCount(audio), Is.Zero);
    }

    [Test]
    public void InvalidCuePausedAudioAndMissingOwnerAreSilent()
    {
        GameAudio.PlayEvolution((GameAudio.EvolutionCue)99, 3);
        Assert.That(VoiceCount(audio), Is.Zero);
        Field("paused").SetValue(audio, true);
        GameAudio.PlayEvolution(GameAudio.EvolutionCue.Impact, 3);
        Assert.That(VoiceCount(audio), Is.Zero);
        Field("instance").SetValue(null, null);
        Assert.DoesNotThrow(() => GameAudio.PlayEvolution(GameAudio.EvolutionCue.Impact, 3));
    }

    [Test]
    public void MissingClipSkipsTheCueWithoutAnUnrelatedFallbackSound()
    {
        var paths = (string[])Field("EvolutionPaths").GetValue(null);
        string previous = paths[0];
        try
        {
            paths[0] = "Audio/Evolution/__MissingEvolutionAudioTest";clips[0] = null;
            Assert.DoesNotThrow(() => GameAudio.PlayEvolution(GameAudio.EvolutionCue.Preparation, 2));
            Assert.That(VoiceCount(audio), Is.Zero);
        }
        finally { paths[0] = previous; }
    }

    [UnityTest]
    public IEnumerator AllSevenUserClipsExistAndExposeNonSilentAudioData()
    {
        string[] names = { "prepare", "impact", "stamp", "afterglow", "rank2", "rank3", "rank4" };
        foreach (string name in names)
        {
            var clip = Resources.Load<AudioClip>("Audio/Evolution/" + name);
            Assert.That(clip, Is.Not.Null, name);
            clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading) yield return null;
            Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded), name);
            var data = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(data, 0), Is.True, name);
            float peak = 0f;
            foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
            Assert.That(peak, Is.InRange(.001f, 1f), name);
        }
    }
}
