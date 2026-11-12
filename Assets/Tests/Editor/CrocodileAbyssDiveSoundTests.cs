using System;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class CrocodileAbyssDiveSoundTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;

    [TestCase("AbyssEnter","abyss_enter",16320)]
    [TestCase("AbyssRoll","abyss_roll",14400)]
    [TestCase("AbyssSurge","abyss_surge",20160)]
    [TestCase("AbyssCrush","abyss_crush",14400)]
    public void EachNewCueResolvesItsPreparedShortRecordedMixWithoutChangingTheOldCueNames(string cue,string resource,int samples)
    {
        Type sound=typeof(MonsterAutoBattlePrototype).GetNestedType("CrocodileSound",BindingFlags.NonPublic);
        MethodInfo path=typeof(MonsterAutoBattlePrototype).GetMethod("CrocodileSoundResource",Hidden);
        Assert.That(path,Is.Not.Null);
        Assert.That(path.Invoke(null,new[]{Enum.Parse(sound,cue)}),Is.EqualTo(resource));
        foreach(string name in new[]{"Bite","Spin","Crush","Throw","Collision","Sweep","Jaw","Resonance"})
            Assert.That(path.Invoke(null,new[]{Enum.Parse(sound,name)}),Is.EqualTo(name.ToLowerInvariant()));
        string key="Audio/Combat/Crocodile/"+resource;
        AudioClip clip=Resources.Load<AudioClip>(key);Assert.That(clip,Is.Not.Null,key);
        Assert.That(clip.samples,Is.EqualTo(samples));Assert.That(clip.channels,Is.EqualTo(2));
        Assert.That(clip.frequency,Is.EqualTo(48000));Assert.That(clip.length,Is.LessThan(.45f));
        var catalog=JsonUtility.FromJson<GameAudio.PreparedCombatSounds>(Resources.Load<TextAsset>(GameAudio.PreparedCombatSoundsPath).text);
        var entry=catalog.clips.Single(value=>value.path==key);
        Assert.That(entry.samples,Is.EqualTo(samples));Assert.That(entry.channels,Is.EqualTo(2));
        Assert.That(entry.frequency,Is.EqualTo(48000));
        Assert.That(entry.peak,Is.InRange(.1f,Mathf.Pow(10f,-.8f/20f)));
        Assert.That(entry.rms,Is.GreaterThan(.01f).And.LessThan(entry.peak));
    }
}
