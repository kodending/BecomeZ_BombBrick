using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AcquisitionStatCueFlowTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject audioHost,voicesHost;
    private GameAudio audio;
    private object previousAudio;
    private AudioSource[] voices;
    private FieldInfo voiceCounter;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        previousAudio=typeof(GameAudio).GetField("instance",Hidden).GetValue(null);
        AudioClip clip=Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_medium_000");
        Assert.That(clip,Is.Not.Null);
        clip.LoadAudioData();
        while(clip.loadState==AudioDataLoadState.Loading)yield return null;
        Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded));
        audioHost=new GameObject("Inactive stat cue routing owner");audioHost.SetActive(false);
        voicesHost=new GameObject("Stat cue routing voices");
        audio=audioHost.AddComponent<GameAudio>();
        voices=(AudioSource[])typeof(GameAudio).GetField("chestVoices",Hidden).GetValue(audio);
        for(int i=0;i<voices.Length;i++)
        {voices[i]=voicesHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;}
        typeof(GameAudio).GetField("chestTension",Hidden).SetValue(audio,voicesHost.AddComponent<AudioSource>());
        typeof(GameAudio).GetField("authoredBasicHit",Hidden).SetValue(audio,clip);
        typeof(GameAudio).GetField("chestContactClip",Hidden).SetValue(audio,clip);
        typeof(GameAudio).GetField("previewEnabled",Hidden).SetValue(audio,true);
        typeof(GameAudio).GetField("instance",Hidden).SetValue(null,audio);
        voiceCounter=typeof(GameAudio).GetField("nextChestVoice",Hidden);
    }

    [TearDown]
    public void Cleanup()
    {
        typeof(GameAudio).GetField("instance",Hidden).SetValue(null,previousAudio);
        if(audioHost!=null)Object.DestroyImmediate(audioHost);
        if(voicesHost!=null)Object.DestroyImmediate(voicesHost);
    }

    [Test]
    public void ChestUsesEachActualStatGradeRatherThanTheAverageGrade()
    {
        var monster=MixedGrades();
        string before=JsonUtility.ToJson(monster);
        var view=new ChestRewardPresentation(new List<OwnedMonsterSave>{monster},false,false,"prototype_region",null);
        int[] qualities={0,4,1,3,2,0};
        for(int stat=0;stat<MonsterAcquisitionReveal.StatCount;stat++)
        {
            float expected=ExpectedPitch(stat,qualities[stat]);
            SetAge(view,view.RevealAt(0)+MonsterAcquisitionReveal.StatAt(stat)+.001f);
            view.Update();
            Assert.That(LastPitch(),Is.EqualTo(expected),"Stat "+stat+" must use its individual grade.");
            int count=(int)voiceCounter.GetValue(audio);
            view.Update();
            Assert.That(voiceCounter.GetValue(audio),Is.EqualTo(count));
        }
        Assert.That(JsonUtility.ToJson(monster),Is.EqualTo(before));
    }

    [Test]
    public void ADelayedFrameUsesTheNewestCardsFirstStatInsteadOfThePreviousCardsLastStat()
    {
        var high=ChestRewardPresentationTests.MonsterWithScore(95f);
        var low=ChestRewardPresentationTests.MonsterWithScore(0f);
        var view=new ChestRewardPresentation(new List<OwnedMonsterSave>{high,low},false,false,"prototype_region",null);
        float expected=ExpectedPitch(0,0);
        SetAge(view,view.RevealAt(1)+MonsterAcquisitionReveal.StatAt(0)+.001f);
        view.Update();
        Assert.That(LastPitch(),Is.EqualTo(expected));
        Assert.That(typeof(ChestRewardPresentation).GetField("statSoundMasks",Hidden).GetValue(view),
            Is.EqualTo(new[]{63,1}));
        int count=(int)voiceCounter.GetValue(audio);
        view.Update();
        Assert.That(voiceCounter.GetValue(audio),Is.EqualTo(count));
    }

    [Test]
    public void CaptureRoutesIndividualStatGradesAndIgnoresAnAbsentResult()
    {
        var host=new GameObject("Inactive capture stat cue routing");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var type=typeof(MonsterAutoBattlePrototype);
            var ownedField=type.GetField("capturePresentationMonster",Hidden);
            var update=type.GetMethod("UpdateCaptureAcquisitionAudio",Hidden);
            ownedField.SetValue(battle,MixedGrades());
            int[] qualities={0,4,1,3,2,0};
            for(int stat=0;stat<MonsterAcquisitionReveal.StatCount;stat++)
            {
                float expected=ExpectedPitch(stat,qualities[stat]);
                update.Invoke(battle,new object[]{MonsterAcquisitionReveal.StatAt(stat)+.001f});
                Assert.That(LastPitch(),Is.EqualTo(expected),"Captured stat "+stat+" must retain its own grade cue.");
                int count=(int)voiceCounter.GetValue(audio);
                update.Invoke(battle,new object[]{MonsterAcquisitionReveal.StatAt(stat)+.001f});
                Assert.That(voiceCounter.GetValue(audio),Is.EqualTo(count));
            }
            ownedField.SetValue(battle,null);
            int before=(int)voiceCounter.GetValue(audio);
            Assert.DoesNotThrow(()=>update.Invoke(battle,new object[]{MonsterAcquisitionReveal.StampAt}));
            Assert.That(voiceCounter.GetValue(audio),Is.EqualTo(before));
        }
        finally{Object.DestroyImmediate(host);}
    }

    private float ExpectedPitch(int stat,int quality)
    {
        GameAudio.PlayChest(GameAudio.ChestCue.Stat,false,stat,quality);
        float pitch=LastPitch();
        typeof(GameAudio).GetMethod("StopChestSounds",Hidden).Invoke(audio,new object[]{false});
        return pitch;
    }
    private float LastPitch()
    {
        int count=(int)voiceCounter.GetValue(audio);
        Assert.That(count,Is.GreaterThan(0));
        return voices[(count-1)%voices.Length].pitch;
    }
    private static void SetAge(ChestRewardPresentation view,float age)=>
        typeof(ChestRewardPresentation).GetField("startedAt",Hidden).SetValue(view,Time.unscaledTime-age);
    private static OwnedMonsterSave MixedGrades()
    {
        float[] scores={0f,95f,20f,80f,50f,0f},v=new float[6];
        for(int i=0;i<v.Length;i++)v[i]=Mathf.Lerp(MonsterStatQuality.MinimumPercent((MonsterIndividualStat)i),
            MonsterStatQuality.MaximumPercent((MonsterIndividualStat)i),scores[i]/100f)/100f;
        return new OwnedMonsterSave{hpMultiplier=v[0],damageMultiplier=v[1],attackSpeedMultiplier=v[2],
            defenseMultiplier=v[3],statusResistanceMultiplier=v[4],moveSpeedMultiplier=v[5]};
    }
}
