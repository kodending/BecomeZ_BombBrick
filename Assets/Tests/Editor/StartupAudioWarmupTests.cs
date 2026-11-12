using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class StartupAudioWarmupTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private FieldInfo singleton;
    private object previous;
    private GameObject host;
    private GameAudio audio;
    private GameAudio.PreparedCombatSounds prepared;
    private PreparedEffectSoundLevels effects;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        singleton=typeof(GameAudio).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        previous=singleton.GetValue(null);
        host=new GameObject("Prepared audio batch test");host.SetActive(false);
        audio=host.AddComponent<GameAudio>();singleton.SetValue(null,audio);
        prepared=JsonUtility.FromJson<GameAudio.PreparedCombatSounds>(
            Resources.Load<TextAsset>(GameAudio.PreparedCombatSoundsPath).text);
        effects=Resources.Load<PreparedEffectSoundLevels>("Audio/PreparedEffectSoundLevels");
        var clips=new HashSet<AudioClip>();
        foreach(var entry in prepared.clips)
        {
            var clip=Resources.Load<AudioClip>(entry.path);
            Assert.That(clip,Is.Not.Null,entry.path);clips.Add(clip);
        }
        foreach(var entry in effects.clips)clips.Add(entry.clip);
        foreach(var clip in clips)
        {
            Assert.That(clip,Is.Not.Null);clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),clip.name);
        }
    }

    [TearDown]
    public void Cleanup()
    {
        singleton.SetValue(null,previous);
        Object.DestroyImmediate(host);
    }

    private bool Flag(string name)=>(bool)typeof(GameAudio).GetField(name,Private).GetValue(audio);

    [UnityTest]
    public IEnumerator ReadyAudioWarmupDoesNotWaitOneFramePerClipAndPreservesPreparedLevels()
    {
        string random=JsonUtility.ToJson(UnityEngine.Random.state);
        var routine=GameAudio.WarmupCombatSounds();int waits=0;
        try
        {
            while(routine.MoveNext()){waits++;yield return routine.Current;}
            Assert.That(waits,Is.LessThan(prepared.clips.Length),
                "Already loaded audio must not require an unconditional frame for every catalog entry.");
            Assert.That(Flag("combatAudioWarmupComplete"),Is.True);
            Assert.That(Flag("combatAudioWarmupRunning"),Is.False);
            var levels=(IDictionary)typeof(GameAudio).GetField("combatLevels",Private).GetValue(audio);
            foreach(var entry in prepared.clips)
            {
                var clip=Resources.Load<AudioClip>(entry.path);
                Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),entry.path);
                Assert.That(levels.Contains(clip),Is.True,entry.path);
                object level=levels[clip];
                Assert.That(level.GetType().GetField("peak").GetValue(level),Is.EqualTo(entry.peak));
                Assert.That(level.GetType().GetField("rms").GetValue(level),Is.EqualTo(entry.rms));
            }
            foreach(var entry in effects.clips)
                Assert.That(entry.clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),entry.clip.name);
            Assert.That(GameAudio.WarmupCombatSounds().MoveNext(),Is.False);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
            Assert.That(host.GetComponents<AudioSource>(),Is.Empty,"Preparation must not start voices or initialize another audio owner.");
        }
        finally{(routine as IDisposable)?.Dispose();}
    }

    [UnityTest]
    public IEnumerator InterruptedAudioWarmupReleasesItsRunningFlagAndCanRetry()
    {
        var routine=GameAudio.WarmupCombatSounds();
        try
        {
            if(routine.MoveNext())
            {
                Assert.That(Flag("combatAudioWarmupRunning"),Is.True);
                Assert.That(Flag("combatAudioWarmupComplete"),Is.False);
                var waiter=GameAudio.WarmupCombatSounds();
                Assert.That(waiter.MoveNext(),Is.True);
                Assert.That(waiter.Current,Is.Null);
                (routine as IDisposable)?.Dispose();
                Assert.That(Flag("combatAudioWarmupRunning"),Is.False);
                Assert.That(Flag("combatAudioWarmupComplete"),Is.False);
                yield return waiter;
            }
            Assert.That(Flag("combatAudioWarmupRunning"),Is.False);
            Assert.That(Flag("combatAudioWarmupComplete"),Is.True);
            Assert.That(GameAudio.WarmupCombatSounds().MoveNext(),Is.False);
        }
        finally{(routine as IDisposable)?.Dispose();}
    }
}
