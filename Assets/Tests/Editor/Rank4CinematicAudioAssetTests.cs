using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class Rank4CinematicAudioAssetTests
{
    [Serializable] private sealed class Catalog {public Family[] families;}
    [Serializable] private sealed class Family {public string family;public float duration;public Cue prelude,timeline;}
    [Serializable] private sealed class Cue {public string key;public float time,duration,gain;}
    private const string Root="Audio/Combat/Cinematics/";

    [UnitySetUp]
    public IEnumerator WaitForProductionAudioData()
    {
        var clips=new List<AudioClip>(Resources.LoadAll<AudioClip>(Root));
        clips.AddRange(Resources.LoadAll<AudioClip>("Audio/Combat/Native"));
        foreach(var clip in clips)
        {
            clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),clip.name);
        }
    }

    [Test]
    public void AllTwentyProductionBanksHaveOneContinuousTimelineAndAnOverlappingPrelude()
    {
        TextAsset asset=Resources.Load<TextAsset>(Root+"catalog");
        Assert.That(asset,Is.Not.Null);
        var catalog=JsonUtility.FromJson<Catalog>(asset.text);
        Assert.That(catalog.families,Has.Length.EqualTo(20));
        var families=new HashSet<string>();var paths=new HashSet<string>();var guids=new HashSet<string>();
        int count=0;
        foreach(var family in catalog.families)
        {
            Assert.That(families.Add(family.family),Is.True);
            Assert.That(Rank4CinematicAudio.Supports(family.family),Is.True,family.family);
            Assert.That(family.prelude.key,Is.EqualTo("prelude"));
            Assert.That(family.prelude.duration,Is.EqualTo(.55f).Within(.0001f));
            Assert.That(family.timeline,Is.Not.Null);
            Assert.That(family.timeline.key,Is.EqualTo("timeline"));
            Assert.That(family.timeline.duration,Is.EqualTo(family.duration+.65f).Within(.0001f));
            var cues=new[]{family.prelude,family.timeline};
            foreach(var cue in cues)
            {
                string path=Root+family.family+"/"+cue.key;
                Assert.That(paths.Add(path),Is.True,path);
                AudioClip clip=Resources.Load<AudioClip>(path);
                Assert.That(clip,Is.Not.Null,path);Assert.That(clip.channels,Is.EqualTo(2),path);
                Assert.That(clip.frequency,Is.EqualTo(48000),path);
                Assert.That(clip.length,Is.EqualTo(cue.duration).Within(.002f),path);
                Assert.That(cue.gain,Is.GreaterThan(0f),path);
                string source=AssetDatabase.GetAssetPath(clip);
                Assert.That(guids.Add(AssetDatabase.AssetPathToGUID(source)),Is.True,path);
                var importer=(AudioImporter)AssetImporter.GetAtPath(source);
                Assert.That(importer.forceToMono,Is.False,path);
                Assert.That(importer.loadInBackground,Is.True,path);
                Assert.That(importer.defaultSampleSettings.compressionFormat,Is.EqualTo(AudioCompressionFormat.PCM),path);
                Assert.That(importer.defaultSampleSettings.loadType,Is.EqualTo(AudioClipLoadType.DecompressOnLoad),path);
                Assert.That(Regex.IsMatch(File.ReadAllText(source+".meta"),@"(?m)^\s*normalize:\s*0\s*$"),Is.True,path);
                var samples=new float[clip.samples*clip.channels];
                Assert.That(clip.GetData(samples,0),Is.True,path);
                double energy=0;float peak=0f;bool finite=true;
                foreach(float sample in samples)
                {
                    finite&=!float.IsNaN(sample)&&!float.IsInfinity(sample);
                    peak=Mathf.Max(peak,Mathf.Abs(sample));
                    energy+=sample*sample;
                }
                Assert.That(finite,Is.True,path);Assert.That(peak,Is.LessThan(1f),path);
                Assert.That(energy,Is.GreaterThan(.000001d),path);
                Assert.That(cue.gain,Is.EqualTo(1f),path);
                Assert.That(Mathf.Abs(samples[samples.Length-1]),Is.LessThan(.001f),path+" tail edge");
                count++;
            }
        }
        Assert.That(count,Is.EqualTo(40));
        Assert.That(Rank4CinematicAudio.Supports("arachne"),Is.True);
        Assert.That(Rank4CinematicAudio.Supports("raptor"),Is.True,"Velos uses the same continuous playback lifecycle.");
    }

    [TestCase("sabertooth","devour_inhale",1.10f)]
    [TestCase("sabertooth","devour_snap",.32f)]
    [TestCase("ninja_cat","spacetime_charge",.65f)]
    [TestCase("ninja_cat","spacetime_collapse",.34f)]
    [TestCase("wizard_mouse","meteor_release",.28f)]
    [TestCase("wizard_mouse","meteor_hit",.18f)]
    [TestCase("wizard_mouse","meteor_finish",.38f)]
    [TestCase("wolf","pack_pass",.45f)]
    [TestCase("wolf","pack_hit",.24f)]
    [TestCase("mushroom","death_bloom",.70f)]
    [TestCase("mushroom","death_breath",.50f)]
    [TestCase("griffin","orb_absorb",.20f)]
    [TestCase("griffin","orb_contract",.35f)]
    [TestCase("griffin","orb_release",.42f)]
    public void NativeSkillReplacementIsFiniteDecodedAndPreparedBeforeCombat(string family,string key,float seconds)
    {
        string path="Audio/Combat/Native/"+family+"/"+key;
        AudioClip clip=Resources.Load<AudioClip>(path);
        Assert.That(clip,Is.Not.Null,path);
        Assert.That(clip.channels,Is.EqualTo(2),path);
        Assert.That(clip.frequency,Is.EqualTo(48000),path);
        Assert.That(clip.length,Is.EqualTo(seconds).Within(.002f),path);
        var samples=new float[clip.samples*clip.channels];
        Assert.That(clip.GetData(samples,0),Is.True,path);
        double energy=0d;float peak=0f;bool finite=true;
        foreach(float sample in samples)
        {finite&=!float.IsNaN(sample)&&!float.IsInfinity(sample);energy+=sample*sample;peak=Mathf.Max(peak,Mathf.Abs(sample));}
        Assert.That(finite,Is.True,path);Assert.That(energy,Is.GreaterThan(.000001d),path);
        Assert.That(peak,Is.LessThan(1f),path);
        string source=AssetDatabase.GetAssetPath(clip);
        var importer=(AudioImporter)AssetImporter.GetAtPath(source);
        Assert.That(importer.loadInBackground,Is.True,path);
        Assert.That(importer.defaultSampleSettings.compressionFormat,Is.EqualTo(AudioCompressionFormat.PCM),path);
        var prepared=JsonUtility.FromJson<BombBrick.Game.Core.GameAudio.PreparedCombatSounds>(
            Resources.Load<TextAsset>(BombBrick.Game.Core.GameAudio.PreparedCombatSoundsPath).text);
        var entry=Array.Find(prepared.clips,item=>item.path==path);
        Assert.That(entry,Is.Not.Null,path);
        Assert.That(entry.samples,Is.EqualTo(clip.samples),path);
        Assert.That(entry.rms,Is.GreaterThan(0f),path);
    }
}
