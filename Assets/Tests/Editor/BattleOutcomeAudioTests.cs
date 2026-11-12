using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BattleOutcomeAudioTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameAudio audio;
    private AudioClip victory, defeat;
    private FieldInfo singleton;
    private object previous;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        victory = Resources.Load<AudioClip>("Sounds/BattleVictory");
        defeat = Resources.Load<AudioClip>("Sounds/BattleDefeat");
        foreach (AudioClip clip in new[] { victory, defeat })
        {
            Assert.That(clip, Is.Not.Null);
            clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading) yield return null;
            Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
        }
        singleton = typeof(GameAudio).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        previous = singleton.GetValue(null);
        host = new GameObject("Battle outcome audio test");
        host.SetActive(false);
        audio = host.AddComponent<GameAudio>();
    }

    [TearDown]
    public void Cleanup()
    {
        // The real Awake creates fallback clips; release those immediately in the editor fixture.
        var generated = new HashSet<AudioClip>();
        foreach (string field in new[] { "clips", "combatClips" })
        {
            var table = Get(field) as Array;
            if (table != null)
                foreach (AudioClip clip in table)
                    if (clip != null && !AssetDatabase.Contains(clip)) generated.Add(clip);
            Set(field, null);
        }
        var calm = Get("calmLoop") as AudioClip;
        if (calm != null) generated.Add(calm);
        Set("calmLoop", null);
        foreach (AudioClip clip in generated) Object.DestroyImmediate(clip);
        if (singleton != null) singleton.SetValue(null, previous);
        if (host != null) Object.DestroyImmediate(host);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ResultJingleHasHeadroomSilentEdgesAndAFiniteLength(bool won)
    {
        AudioClip clip = won ? victory : defeat;
        Assert.That(clip.length, Is.InRange(won ? .85f : 1.5f, won ? 1.1f : 2.2f));
        var samples = new float[clip.samples * clip.channels];
        Assert.That(clip.GetData(samples, 0), Is.True);
        float peak = 0;
        double square = 0, sum = 0;
        foreach (float sample in samples)
        {
            Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
            peak = Mathf.Max(peak, Mathf.Abs(sample));
            square += sample * sample;
            sum += sample;
        }
        Assert.That(peak, Is.InRange(.2f, .7f));
        Assert.That(Math.Sqrt(square / samples.Length), Is.GreaterThan(.025));
        Assert.That(Math.Abs(sum / samples.Length), Is.LessThan(.0001));
        Assert.That(samples[0], Is.Zero);
        Assert.That(samples[samples.Length - 1], Is.Zero);
    }

    [Test]
    public void ExistingOutcomeCueNumbersSelectDistinctImportedJingles()
    {
        typeof(GameAudio).GetMethod("Awake", Private).Invoke(audio, null);
        var clips = (AudioClip[])Get("clips");
        Assert.That((int)GameAudio.Cue.Victory, Is.EqualTo(5));
        Assert.That((int)GameAudio.Cue.Defeat, Is.EqualTo(6));
        Assert.That(clips[(int)GameAudio.Cue.Victory], Is.SameAs(victory));
        Assert.That(clips[(int)GameAudio.Cue.Defeat], Is.SameAs(defeat));
        Assert.That(victory, Is.Not.SameAs(defeat));
        Assert.That(((AudioSource)Get("effects")).loop, Is.False);
    }

    [Test]
    public void DestroyingTheAudioOwnerKeepsBothImportedResultJingles()
    {
        Set("authoredBattleVictory", victory);
        Set("authoredBattleDefeat", defeat);
        Set("clips", new[] { victory, defeat });
        typeof(GameAudio).GetMethod("OnDestroy", Private).Invoke(audio, null);
        Assert.That(victory != null && defeat != null, Is.True);
        Assert.That(Resources.Load<AudioClip>("Sounds/BattleVictory"), Is.SameAs(victory));
        Assert.That(Resources.Load<AudioClip>("Sounds/BattleDefeat"), Is.SameAs(defeat));
    }

    private object Get(string field) => typeof(GameAudio).GetField(field, Private).GetValue(audio);
    private void Set(string field, object value) => typeof(GameAudio).GetField(field, Private).SetValue(audio, value);
}
