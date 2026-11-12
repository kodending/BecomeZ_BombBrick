using System;
using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class BattleStartAudioTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host;
    private GameAudio audio;
    private AudioClip drum;
    private FieldInfo singleton;
    private object previous;

    [UnitySetUp] public IEnumerator Setup()
    {
        drum=Resources.Load<AudioClip>("Sounds/BattleEngage");
        Assert.That(drum,Is.Not.Null);
        drum.LoadAudioData();
        while(drum.loadState==AudioDataLoadState.Loading)yield return null;
        Assert.That(drum.loadState,Is.EqualTo(AudioDataLoadState.Loaded));
        singleton=typeof(GameAudio).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        previous=singleton.GetValue(null);
        host=new GameObject("Battle cue audio test");host.SetActive(false);
        audio=host.AddComponent<GameAudio>();
        singleton.SetValue(null,audio);
        Set("previewEnabled",true);
    }

    [TearDown] public void Cleanup()
    {
        if(singleton!=null)singleton.SetValue(null,previous);
        if(host!=null)Object.DestroyImmediate(host);
    }

    [Test] public void BattleStartDrumHasHeadroomAndItsExistingCueNumberStaysStable()
    {
        Assert.That((int)GameAudio.Cue.Start,Is.EqualTo(1));
        Assert.That((int)GameAudio.Cue.Logo,Is.EqualTo(10));
        Assert.That(Enum.GetValues(typeof(GameAudio.Cue)).Length,Is.EqualTo(11));
        Assert.That(drum.length,Is.InRange(1.6f,1.9f));
        float[] samples=new float[drum.samples*drum.channels];
        Assert.That(drum.GetData(samples,0),Is.True);
        double square=0;float peak=0;
        foreach(float sample in samples)
        {
            Assert.That(float.IsNaN(sample)||float.IsInfinity(sample),Is.False);
            peak=Mathf.Max(peak,Mathf.Abs(sample));square+=sample*sample;
        }
        Assert.That(peak,Is.InRange(.2f,.8f),drum.name+" retains clipping headroom.");
        Assert.That(Math.Sqrt(square/samples.Length),Is.GreaterThan(.025),drum.name);
        Assert.That(Mathf.Abs(samples[0]),Is.LessThan(.001f));
        Assert.That(Mathf.Abs(samples[samples.Length-1]),Is.LessThan(.001f));
    }

    [TestCase(-1)] [TestCase(int.MaxValue)]
    [TestCase((int)GameAudio.Cue.Start)] [TestCase(11)]
    public void InvalidCueOrShortClipTableDoesNotIndexOrPlayAnUnrelatedSound(int cue)
    {
        Set("effects",host.AddComponent<AudioSource>());
        Set("clips",new[]{drum});
        Assert.DoesNotThrow(()=>GameAudio.Play((GameAudio.Cue)cue));
    }

    [Test] public void DestroyingTheAudioOwnerKeepsTheImportedBattleDrumAvailable()
    {
        Set("authoredBattleStart",drum);
        Set("clips",new[]{drum});
        typeof(GameAudio).GetMethod("OnDestroy",Private).Invoke(audio,null);
        Assert.That(drum!=null,Is.True);
        Assert.That(Resources.Load<AudioClip>("Sounds/BattleEngage"),Is.SameAs(drum));
    }

    [TestCase(0f)] [TestCase(.37f)] [TestCase(1f)]
    public void BattleCuesUseTheExistingEffectsVolume(float volume)
    {
        var effects=host.AddComponent<AudioSource>();
        Set("effects",effects);Set("music",host.AddComponent<AudioSource>());
        foreach(string field in new[]{"combatVoices","combatLayers"})
        {
            var sources=(AudioSource[])typeof(GameAudio).GetField(field,Private).GetValue(audio);
            for(int i=0;i<sources.Length;i++)sources[i]=host.AddComponent<AudioSource>();
        }
        audio.Apply(new GameUserSettings{effectsVolume=volume});
        Assert.That(effects.volume,Is.EqualTo(volume).Within(.00001f));
    }

    private void Set(string field,object value)=>typeof(GameAudio).GetField(field,Private).SetValue(audio,value);
}
