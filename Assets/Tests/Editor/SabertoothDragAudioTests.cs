using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class SabertoothDragAudioTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private SabertoothDevourCinematicTests fixture;
    private MonsterAutoBattlePrototype battle;
    private GameAudio audio;
    private GameObject audioHost,voiceHost;
    private object source,target,previousAudio;
    private bool previousSkillAudio;
    private AudioSource[] voices;
    private AudioClip drag,finish,inhale;

    [UnitySetUp] public IEnumerator Setup()
    {
        previousAudio=typeof(GameAudio).GetField("instance",Fields).GetValue(null);
        previousSkillAudio=CombatReadabilityPreview.SkillAudio;
        fixture=new SabertoothDevourCinematicTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        source=Get<object>(fixture,"source");target=Get<object>(fixture,"target");
        Set(source,"id",17);Set(target,"id",18);
        audioHost=new GameObject("Inactive sabertooth drag audio owner");audioHost.SetActive(false);
        audio=audioHost.AddComponent<GameAudio>();
        Call(audio,"InitializeCombatAudio");
        voiceHost=new GameObject("Active sabertooth drag audio voices");
        voices=Get<AudioSource[]>(audio,"combatVoices");
        var layers=Get<AudioSource[]>(audio,"combatLayers");
        for(int i=0;i<voices.Length;i++)
        {
            voices[i]=voiceHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            layers[i]=voiceHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
        }
        Set(audio,"effects",voiceHost.AddComponent<AudioSource>());Set(audio,"previewEnabled",true);
        typeof(GameAudio).GetField("instance",Fields).SetValue(null,audio);
        CombatReadabilityPreview.SkillAudio=true;
        foreach(string name in new[]{"drag","start","finish"})
        {
            var clip=Resources.Load<AudioClip>("Audio/Combat/Sabertooth/"+name);
            Assert.That(clip,Is.Not.Null,name);clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),name);
        }
        drag=Resources.Load<AudioClip>("Audio/Combat/Sabertooth/drag");
        finish=Resources.Load<AudioClip>("Audio/Combat/Sabertooth/finish");
        inhale=Resources.Load<AudioClip>("Audio/Combat/Native/sabertooth/devour_inhale")??drag;
        inhale.LoadAudioData();
        while(inhale.loadState==AudioDataLoadState.Loading)yield return null;
        Get<AudioClip[]>(battle,"sabertoothSkillSounds")[2]=drag;
    }

    [TearDown] public void Cleanup()
    {
        try
        {
            if(audio!=null)audio.StopCombatSounds();
            fixture?.TearDown();
        }
        finally
        {
            typeof(GameAudio).GetField("instance",Fields).SetValue(null,previousAudio);
            CombatReadabilityPreview.SkillAudio=previousSkillAudio;
            if(audioHost!=null)Object.DestroyImmediate(audioHost);
            if(voiceHost!=null)Object.DestroyImmediate(voiceHost);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void ReleasingOwnedDragIgnoresTheLegacyFlagAndPreservesOtherOwnersClipsAndOneShots(bool legacyFlag)
    {
        Set(source,"preyDragSoundStarted",legacyFlag);
        StartVoice(0,17,drag,true);StartVoice(1,18,drag,true);
        StartVoice(2,17,finish,true);StartVoice(3,17,drag,false);
        bool playing=voices[0].isPlaying,oneShotPlaying=voices[3].isPlaying;
        int position=voices[0].timeSamples,oneShotPosition=voices[3].timeSamples;
        float oneShotUntil=Get<float[]>(audio,"voiceUntil")[3];
        Call(battle,"StopSabertoothDragSound",source);
        AssertReleased(voices[0],playing,position);
        Assert.That(Get<bool>(source,"preyDragSoundStarted"),Is.False);
        Assert.That(voices[1].loop,Is.True,"Another monster owns this drag.");
        Assert.That(voices[2].loop,Is.True,"Another clip on the caster must remain intact.");
        Assert.That(voices[3].loop,Is.False);Assert.That(voices[3].isPlaying,Is.EqualTo(oneShotPlaying));
        Assert.That(voices[3].timeSamples,Is.GreaterThanOrEqualTo(oneShotPosition));
        Assert.That(Get<float[]>(audio,"voiceUntil")[3],Is.EqualTo(oneShotUntil));
        float until=Get<float[]>(audio,"voiceUntil")[0];
        Call(battle,"StopSabertoothDragSound",source);
        Assert.That(Get<float[]>(audio,"voiceUntil")[0],Is.EqualTo(until),"Repeated cleanup must not restart or extend the tail.");
        Assert.That(voices[0].isPlaying,Is.EqualTo(playing));
    }

    [TestCase(0)] [TestCase(1)]
    public void RankFourPullUsesOneFiniteIntakeAndDoesNotRestartItAtPullEndOnEitherTeam(int team)
    {
        object actor=team==0?source:target;
        var voice=BeginPull(actor);
        bool playing=voice.isPlaying;int position=voice.timeSamples;
        object state=Get<object>(actor,"sabertoothDevour");
        Call(battle,"AdvanceSabertoothDevour",actor,SabertoothDevourRules.PullEnd-Get<float>(state,"time")+.001f);
        Assert.That(Get<bool>(state,"pullSound"),Is.False);
        AssertFiniteIntakeContinues(voice,playing,position);
        Assert.That(Get<object>(actor,"sabertoothDevour"),Is.SameAs(state),"The drag ends before the bite, not only after the complete skill.");
        Call(battle,"AdvanceSabertoothDevour",actor,SabertoothDevourRules.Duration);
        Assert.That(Get<object>(actor,"sabertoothDevour"),Is.Null);
        Assert.That(voices.Where(v=>v.clip==drag&&v.loop),Is.Empty);
    }

    [TestCase("cancel")] [TestCase("death")] [TestCase("battle end")] [TestCase("new wave")]
    public void InterruptedRankFourPullAlsoFinishesItsCurrentClipWithoutRepeating(string reason)
    {
        var voice=BeginPull(source);
        bool playing=voice.isPlaying;int position=voice.timeSamples;
        if(reason=="cancel")Call(battle,"EndSabertoothDevour",source,false);
        else
        {
            if(reason=="death")Set(source,"hp",0f);
            else if(reason=="battle end")Set(battle,"battleEnding",true);
            else Set(battle,"combatGeneration",Get<int>(battle,"combatGeneration")+1);
            Call(battle,"AdvanceSabertoothDevour",source,.02f);
        }
        Assert.That(Get<object>(source,"sabertoothDevour"),Is.Null);
        AssertFiniteIntakeContinues(voice,playing,position);
    }

    private AudioSource BeginPull(object actor)
    {
        Assert.That(Call(battle,"BeginSabertoothDevour",actor,GameMasterData.Skill(SabertoothCombatRules.SkillId)),Is.True);
        Call(battle,"AdvanceSabertoothDevour",actor,SabertoothDevourRules.PullStart+.001f);
        Assert.That(Get<bool>(Get<object>(actor,"sabertoothDevour"),"pullSound"),Is.True);
        Assert.That(Get<bool>(actor,"preyDragSoundStarted"),Is.False,"Rank four does not use the rank-three prey-drag flag.");
        int owner=Get<int>(actor,"id");var owners=Get<int[]>(audio,"voiceOwner");
        var voice=voices.Where((v,i)=>owners[i]==owner&&v.clip==inhale).Single();
        Assert.That(voice.loop,Is.False,"A fixed-length intake must not become another persistent drag loop.");
        voice.timeSamples=128;return voice;
    }

    private void StartVoice(int slot,int owner,AudioClip clip,bool loop)
    {
        Get<int[]>(audio,"voiceOwner")[slot]=owner;
        Get<float[]>(audio,"voiceUntil")[slot]=loop?float.PositiveInfinity:Time.unscaledTime+clip.length;
        var voice=voices[slot];voice.clip=clip;voice.loop=loop;voice.pitch=1f;voice.Play();voice.timeSamples=128;
    }

    private void AssertReleased(AudioSource voice,bool playing,int position)
    {
        Assert.That(voice.loop,Is.False);Assert.That(voice.clip,Is.SameAs(drag));
        Assert.That(voice.isPlaying,Is.EqualTo(playing),"The playing tail must finish naturally.");
        Assert.That(voice.timeSamples,Is.GreaterThanOrEqualTo(position));
        float until=Get<float[]>(audio,"voiceUntil")[Array.IndexOf(voices,voice)];
        Assert.That(float.IsInfinity(until)||float.IsNaN(until),Is.False);
        Assert.That(until,Is.InRange(Time.unscaledTime,Time.unscaledTime+drag.length+.001f));
    }

    private void AssertFiniteIntakeContinues(AudioSource voice,bool playing,int position)
    {
        Assert.That(voice.loop,Is.False);Assert.That(voice.clip,Is.SameAs(inhale));
        Assert.That(voice.isPlaying,Is.EqualTo(playing),"Cleanup lets the already playing finite intake finish.");
        Assert.That(voice.timeSamples,Is.GreaterThanOrEqualTo(position));
        Assert.That(float.IsInfinity(Get<float[]>(audio,"voiceUntil")[Array.IndexOf(voices,voice)]),Is.False);
    }

    private static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Fields).Invoke(owner,args);
    private static T Get<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Fields).GetValue(owner);
    private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Fields).SetValue(owner,value);
}
