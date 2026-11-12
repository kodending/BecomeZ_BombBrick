using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ChestAudioTests
{
    [UnitySetUp]
    public IEnumerator LoadImportedRewardAudioData()
    {
        var clips=new System.Collections.Generic.List<AudioClip>(Resources.LoadAll<AudioClip>("Audio/Rewards"))
        {
            Resources.Load<AudioClip>("Audio/Combat/Feedback/basic_hit"),
            Resources.Load<AudioClip>("Audio/Combat/Feedback/critical_hit"),
            Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_heavy_000"),
            Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_medium_000"),
            Resources.Load<AudioClip>("Audio/Combat/Samples/impactGeneric_light_000")
        };
        foreach(var clip in clips)
        {
            clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),clip.name);
        }
    }

    [TestCase("chest_open", 2f, .15f)]
    [TestCase("chest_tension", 1.5f, .10f)]
    [TestCase("chest_reveal", 1.132f, .07f)]
    [TestCase("capture_beat", .26f, 0f)]
    [TestCase("capture_failure", .60f, 0f)]
    [TestCase("chest_grade_d", .4f, 0f)]
    [TestCase("chest_grade_c", .65f, 0f)]
    [TestCase("chest_grade_b", 1f, 0f)]
    [TestCase("chest_grade_a", 1.45f, 0f)]
    [TestCase("chest_grade_s", 1.85f, 0f)]
    [TestCase("chest_shiny", 2f, 0f)]
    public void RewardCuesHaveReadableAuthoredAudioAndValidPickups(string name, float duration, float pickup)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/Rewards/" + name);
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.EqualTo(duration).Within(.01f));
        Assert.That(pickup, Is.LessThan(clip.length));
        clip.LoadAudioData();
        float[] data = new float[clip.samples * clip.channels];
        Assert.That(clip.GetData(data, 0), Is.True);
        float peak = 0f;
        for (int i = Mathf.RoundToInt(pickup * clip.frequency) * clip.channels; i < data.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        Assert.That(peak, Is.InRange(.1f, 1f));
    }

    [Test]
    public void HeavyImpactDucksOnlyOrdinaryHitsAndRestoresWithinNinetyMilliseconds()
    {
        MethodInfo method = typeof(GameAudio).GetMethod("ImpactDuckGain", BindingFlags.Static | BindingFlags.NonPublic);
        System.Func<bool, float, float> gain = (ordinaryHit, elapsed) =>
            (float)method.Invoke(null, new object[] { ordinaryHit, elapsed });
        Assert.That(gain(true, 0f), Is.EqualTo(.55f).Within(.001f));
        Assert.That(gain(true, .025f), Is.EqualTo(.55f).Within(.001f));
        Assert.That(gain(true, .0575f), Is.EqualTo(.775f).Within(.001f));
        Assert.That(gain(true, .09f), Is.EqualTo(1f));
        Assert.That(gain(true, 1f), Is.EqualTo(1f));
        Assert.That(gain(true, -.01f), Is.EqualTo(1f));
        Assert.That(gain(false, .01f), Is.EqualTo(1f), "Skill, critical and death voices keep their gain.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SkippingPlaysTheFinalGradeOnlyIfItsStampHasNotAlreadySounded(bool stamped)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
        FieldInfo singleton=typeof(GameAudio).GetField("instance",hidden);
        object previous=singleton.GetValue(null);
        var host=new GameObject("Inactive reward audio owner");host.SetActive(false);
        var voicesHost=new GameObject("Active reward audio voices");
        try
        {
            var audio=host.AddComponent<GameAudio>();
            var voices=(AudioSource[])typeof(GameAudio).GetField("chestVoices",hidden).GetValue(audio);
            for(int i=0;i<voices.Length;i++)
            {
                voices[i]=voicesHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            }
            typeof(GameAudio).GetField("chestTension",hidden).SetValue(audio,voicesHost.AddComponent<AudioSource>());
            var clip=Resources.Load<AudioClip>("Audio/Rewards/chest_grade_b");
            typeof(GameAudio).GetField("chestOpenClip",hidden).SetValue(audio,clip);
            typeof(GameAudio).GetField("chestStampClip",hidden).SetValue(audio,Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_heavy_000"));
            typeof(GameAudio).GetField("chestContactClip",hidden).SetValue(audio,Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_medium_000"));
            typeof(GameAudio).GetField("previewEnabled",hidden).SetValue(audio,true);
            singleton.SetValue(null,audio);
            var monster=ChestRewardPresentationTests.MonsterWithScore(50f);
            var reveal=new ChestRewardPresentation(new System.Collections.Generic.List<OwnedMonsterSave>{monster},false,false,"prototype_region",null);
            float age=reveal.RevealAt(0)+(stamped?MonsterAcquisitionReveal.StampAt:MonsterAcquisitionReveal.StatsAt)+.03f;
            typeof(ChestRewardPresentation).GetField("startedAt",hidden).SetValue(reveal,Time.unscaledTime-age);
            reveal.Update();
            FieldInfo counter=typeof(GameAudio).GetField("nextChestVoice",hidden);
            int before=(int)counter.GetValue(audio);
            reveal.CompleteReveal();
            Assert.That(counter.GetValue(audio),Is.EqualTo(before+(stamped?0:2)),"Skipping after the grade beat must not replay its two stamp layers.");
            reveal.CompleteReveal();reveal.Update();
            Assert.That(counter.GetValue(audio),Is.EqualTo(before+(stamped?0:2)),"Repeated clicks and updates cannot add another grade voice.");
        }
        finally
        {
            singleton.SetValue(null,previous);
            Object.DestroyImmediate(host);Object.DestroyImmediate(voicesHost);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void IndividualStatGradesRiseInPitchAndFitEvenTheFastestBeat(int stat)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
        var host=new GameObject("Inactive acquisition impact audio");host.SetActive(false);
        var voicesHost=new GameObject("Acquisition impact voices");
        try
        {
            var audio=host.AddComponent<GameAudio>();
            var voices=(AudioSource[])typeof(GameAudio).GetField("chestVoices",hidden).GetValue(audio);
            for(int i=0;i<voices.Length;i++)
            { voices[i]=voicesHost.AddComponent<AudioSource>();voices[i].playOnAwake=false; }
            var contact=Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_medium_000");
            var accent=Resources.Load<AudioClip>("Audio/Combat/Samples/impactGeneric_light_000");
            Assert.That(contact,Is.Not.Null);Assert.That(accent,Is.Not.Null);
            typeof(GameAudio).GetField("chestContactClip",hidden).SetValue(audio,contact);
            typeof(GameAudio).GetField("chestStatAccentClip",hidden).SetValue(audio,accent);
            MethodInfo cue=typeof(GameAudio).GetMethod("PlayChestCue",hidden);
            float interval=stat+1<MonsterAcquisitionReveal.StatCount
                ?MonsterAcquisitionReveal.StatAt(stat+1)-MonsterAcquisitionReveal.StatAt(stat)
                :MonsterAcquisitionReveal.StatAt(stat)-MonsterAcquisitionReveal.StatAt(stat-1);
            FieldInfo counter=typeof(GameAudio).GetField("nextChestVoice",hidden);
            float previousPitch=0f,firstPitch=0f;
            for(int quality=0;quality<5;quality++)
            {
                foreach(var voice in voices)voice.Stop();
                counter.SetValue(audio,0);
                cue.Invoke(audio,new object[]{GameAudio.ChestCue.Stat,false,stat,quality});
                Assert.That(voices[0].clip,Is.SameAs(contact));
                Assert.That(voices[0].pitch,Is.GreaterThan(previousPitch),"A higher individual grade needs a higher note at the same stat position.");
                previousPitch=voices[0].pitch;
                if(quality==0)firstPitch=previousPitch;
                Assert.That(contact.length/voices[0].pitch,Is.LessThan(interval));
                Assert.That(counter.GetValue(audio),Is.EqualTo(quality>=3?2:1));
                if(quality>=3)
                {
                    Assert.That(voices[1].clip,Is.SameAs(accent));
                    Assert.That(accent.length/voices[1].pitch,Is.LessThan(interval));
                }
            }
            Assert.That(previousPitch-firstPitch,Is.GreaterThan(.6f),"The grade notes should be clearly different, not a tiny pitch jitter.");
        }
        finally{Object.DestroyImmediate(host);Object.DestroyImmediate(voicesHost);}
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FinalGradeIsAShortPhysicalStampWithNoLongMelodyAndPreservesEffectsVolume(bool shiny)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
        var host=new GameObject("Inactive graded reward audio");host.SetActive(false);
        var voicesHost=new GameObject("Graded reward voices");
        try
        {
            var audio=host.AddComponent<GameAudio>();
            var voices=(AudioSource[])typeof(GameAudio).GetField("chestVoices",hidden).GetValue(audio);
            for(int i=0;i<voices.Length;i++)
            { voices[i]=voicesHost.AddComponent<AudioSource>();voices[i].playOnAwake=false; }
            var contact=Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_medium_000");
            var stamp=Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_heavy_000");
            var accent=Resources.Load<AudioClip>("Audio/Combat/Samples/impactGeneric_light_000");
            typeof(GameAudio).GetField("chestContactClip",hidden).SetValue(audio,contact);
            typeof(GameAudio).GetField("chestStampClip",hidden).SetValue(audio,stamp);
            typeof(GameAudio).GetField("chestStatAccentClip",hidden).SetValue(audio,accent);
            FieldInfo counter=typeof(GameAudio).GetField("nextChestVoice",hidden);
            MethodInfo cue=typeof(GameAudio).GetMethod("PlayChestCue",hidden);
            var gains=(float[])typeof(GameAudio).GetField("chestGains",hidden).GetValue(audio);
            FieldInfo volume=typeof(GameAudio).GetField("effectsVolume",hidden);
            float effects=(float)volume.GetValue(audio),previousPitch=float.MaxValue,previousGain=0f;
            for(int grade=0;grade<5;grade++)
            {
                for(int i=0;i<voices.Length;i++)voices[i].Stop();
                counter.SetValue(audio,0);
                cue.Invoke(audio,new object[]{GameAudio.ChestCue.Reveal,shiny,0,grade});
                int count=(int)counter.GetValue(audio);
                Assert.That(count,Is.EqualTo(grade>=3||shiny?3:2));
                Assert.That(voices[0].clip,Is.SameAs(contact));
                Assert.That(voices[1].clip,Is.SameAs(stamp));
                Assert.That(voices[1].pitch,Is.LessThan(previousPitch));previousPitch=voices[1].pitch;
                Assert.That(gains[1],Is.GreaterThan(previousGain));previousGain=gains[1];
                for(int i=0;i<count;i++)
                {
                    Assert.That(voices[i].clip.length/voices[i].pitch,Is.LessThan(.65f),"The stamp must end as a short thump instead of playing a grade melody.");
                    Assert.That(voices[i].timeSamples,Is.LessThan(voices[i].clip.frequency*.03f),"The impact starts at the stamp, with no long pickup.");
                }
                if(grade>=3||shiny)
                {
                    Assert.That(voices[2].clip,Is.SameAs(accent));
                }
                Assert.That((float)volume.GetValue(audio),Is.EqualTo(effects));
            }
        }
        finally{Object.DestroyImmediate(host);Object.DestroyImmediate(voicesHost);}
    }
}
