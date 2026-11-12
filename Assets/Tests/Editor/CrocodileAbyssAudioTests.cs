using System;
using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class CrocodileAbyssAudioTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private GameObject host,voicesHost,battleHost;
    private GameAudio audio;
    private MonsterAutoBattlePrototype battle;
    private object previousAudio,previousFrontend;
    private float previousSpeed;
    private FieldInfo singleton,frontend;
    private AudioSource[] voices;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        previousSpeed=Time.timeScale;
        singleton=typeof(GameAudio).GetField("instance",Hidden);previousAudio=singleton.GetValue(null);
        frontend=typeof(GameFrontend).GetField("instance",Hidden);previousFrontend=frontend.GetValue(null);frontend.SetValue(null,null);
        host=new GameObject("Inactive abyss audio owner");host.SetActive(false);audio=host.AddComponent<GameAudio>();
        voicesHost=new GameObject("Active abyss audio voices");
        voices=(AudioSource[])typeof(GameAudio).GetField("combatVoices",Hidden).GetValue(audio);
        var layers=(AudioSource[])typeof(GameAudio).GetField("combatLayers",Hidden).GetValue(audio);
        for(int i=0;i<voices.Length;i++)
        {
            voices[i]=voicesHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            layers[i]=voicesHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
        }
        typeof(GameAudio).GetField("effects",Hidden).SetValue(audio,voicesHost.AddComponent<AudioSource>());
        typeof(GameAudio).GetField("previewEnabled",Hidden).SetValue(audio,true);singleton.SetValue(null,audio);
        foreach(string key in new[]{"abyss_enter","abyss_roll","abyss_surge","abyss_crush","spin"})
        {
            var clip=Resources.Load<AudioClip>("Audio/Combat/Crocodile/"+key);Assert.That(clip,Is.Not.Null);
            clip.LoadAudioData();while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded));
        }
        Time.timeScale=0f;
    }

    [TearDown]
    public void Cleanup()
    {
        if(battle!=null)typeof(MonsterAutoBattlePrototype).GetField("crocodileAbyssCinematic",Hidden).SetValue(battle,null);
        if(audio!=null)audio.StopCombatSounds();
        singleton.SetValue(null,previousAudio);frontend.SetValue(null,previousFrontend);Time.timeScale=previousSpeed;
        if(battleHost!=null)Object.DestroyImmediate(battleHost);
        Object.DestroyImmediate(host);Object.DestroyImmediate(voicesHost);
    }

    [TestCase("abyss_enter",CombatSoundCue.Cast)]
    [TestCase("abyss_roll",CombatSoundCue.Periodic)]
    [TestCase("abyss_surge",CombatSoundCue.Cast)]
    public void OnlyAnExplicitAuthoredOneShotCanPlayDuringTheWorldFreezeAtNormalPitch(string name,CombatSoundCue cue)
    {
        var clip=Resources.Load<AudioClip>("Audio/Combat/Crocodile/"+name);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,cue,17,Vector3.zero,authoredSound:clip);
        Assert.That(audio.PlayedCombatSounds,Is.Zero,"The existing stopped-world policy remains the default.");
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,cue,17,Vector3.zero,authoredSound:clip,allowDuringTimeStop:true);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));Assert.That(voices[0].clip,Is.SameAs(clip));
        Assert.That(voices[0].pitch,Is.EqualTo(1f));Assert.That(voices[0].volume,Is.GreaterThan(0f));
        Assert.That(voices[0].loop,Is.False);Assert.That(Time.timeScale,Is.Zero);
        float until=((float[])typeof(GameAudio).GetField("voiceUntil",Hidden).GetValue(audio))[0];
        Assert.That(until,Is.EqualTo(Time.unscaledTime+clip.length).Within(.001f));
        audio.StopCombatSounds();Assert.That(voices[0].isPlaying,Is.False);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,cue,17,Vector3.zero,authoredSound:clip,loop:true,allowDuringTimeStop:true);
        Assert.That(audio.PlayedCombatSounds,Is.Zero,"The opt-in cannot create a frozen-world loop.");
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Death,17,Vector3.zero,allowDuringTimeStop:true);
        Assert.That(audio.PlayedCombatSounds,Is.Zero,"Ordinary and unauthored combat remains frozen.");
    }

    [TestCase("pause")][TestCase("cue mute")][TestCase("battle result")][TestCase("zero volume")]
    public void TheOptInStillHonorsMenuPauseUserMuteVolumeAndTheBattleResult(string state)
    {
        var clip=Resources.Load<AudioClip>("Audio/Combat/Crocodile/abyss_surge");
        if(state=="pause")audio.SetPaused(true);
        else if(state=="cue mute")audio.ToggleCue(CombatSoundCue.Cast);
        else if(state=="battle result")GameAudio.SetBattlePhase(false);
        else typeof(GameAudio).GetField("effectsVolume",Hidden).SetValue(audio,0f);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Cast,17,Vector3.zero,authoredSound:clip,allowDuringTimeStop:true);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(state=="zero volume"?1:0));
        if(state=="zero volume")Assert.That(voices[0].volume,Is.Zero);
    }

    [TestCase("AbyssEnter")][TestCase("AbyssRoll")][TestCase("AbyssSurge")]
    public void TheCrocodileEmitterGrantsTheOptInOnlyToTheCurrentCinematicActorAndNewPhaseCues(string cue)
    {
        battleHost=new GameObject("Inactive abyss emitter test");battleHost.SetActive(false);battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();
        Type battleType=typeof(MonsterAutoBattlePrototype),unitType=battleType.GetNestedType("Unit",Hidden);
        object unit=Activator.CreateInstance(unitType,true),other=Activator.CreateInstance(unitType,true);
        unitType.GetField("definition",Hidden).SetValue(unit,Resources.Load<MonsterDefinition>("MonsterData/Monster_ager"));
        unitType.GetField("model",Hidden).SetValue(unit,voicesHost);
        Type sound=battleType.GetNestedType("CrocodileSound",Hidden);
        MethodInfo play=battleType.GetMethod("PlayCrocodileSound",Hidden);
        Action<string> emit=value=>play.Invoke(battle,new object[]{unit,Enum.Parse(sound,value),Vector3.zero,1f});
        emit(cue);Assert.That(audio.PlayedCombatSounds,Is.Zero);
        object stage=Activator.CreateInstance(battleType.GetNestedType("CrocodileAbyssDiveCinematic",Hidden),true);
        stage.GetType().GetField("unit",Hidden).SetValue(stage,other);
        battleType.GetField("crocodileAbyssCinematic",Hidden).SetValue(battle,stage);
        emit(cue);Assert.That(audio.PlayedCombatSounds,Is.Zero);
        stage.GetType().GetField("unit",Hidden).SetValue(stage,unit);
        emit("Spin");Assert.That(audio.PlayedCombatSounds,Is.Zero,"Existing normal skill cues keep the stopped-world policy.");
        emit(cue);Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));
        audio.StopCombatSounds();battleType.GetField("crocodileAbyssCinematic",Hidden).SetValue(battle,null);
        emit(cue);Assert.That(audio.PlayedCombatSounds,Is.Zero,"No opt-in survives presentation teardown.");
    }

    [Test]
    public void AStoppedWorldPeriodicTimestampCannotSuppressLaterResumedWorldSounds()
    {
        var gate=new CombatSoundGate();
        Assert.That(gate.TryAccept(17,CombatSoundCue.Periodic,1,1000f,9,true),Is.True);
        Assert.That(gate.TryAccept(17,CombatSoundCue.Periodic,2,10f,9),Is.True);
        Assert.That(gate.TryAccept(17,CombatSoundCue.Periodic,3,1000.2f,9,true),Is.False);
        Assert.That(gate.TryAccept(17,CombatSoundCue.Periodic,4,1000.8f,9,true),Is.True);
        Assert.That(gate.TryAccept(17,CombatSoundCue.Periodic,5,10.2f,9),Is.False);
        Assert.That(gate.TryAccept(17,CombatSoundCue.Periodic,6,10.8f,9),Is.True);
    }
}
