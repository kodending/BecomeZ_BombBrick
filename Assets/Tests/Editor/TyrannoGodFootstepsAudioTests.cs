using System;
using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class TyrannoGodFootstepsAudioTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private GameObject host,voicesHost,battleHost;
    private GameAudio audio;
    private MonsterAutoBattlePrototype battle;
    private object previousAudio,previousFrontend,unit,presentation,request;
    private AudioSource[] voices;
    private FieldInfo singleton,frontend;
    private float previousSpeed;
    private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Hidden).SetValue(owner,value);

    [UnitySetUp]public IEnumerator Setup()
    {
        previousSpeed=Time.timeScale;
        singleton=typeof(GameAudio).GetField("instance",Hidden);previousAudio=singleton.GetValue(null);
        frontend=typeof(GameFrontend).GetField("instance",Hidden);previousFrontend=frontend.GetValue(null);frontend.SetValue(null,null);
        host=new GameObject("Inactive god footsteps audio owner");host.SetActive(false);audio=host.AddComponent<GameAudio>();
        voicesHost=new GameObject("God footsteps audio voices");
        voices=(AudioSource[])typeof(GameAudio).GetField("combatVoices",Hidden).GetValue(audio);
        var layers=(AudioSource[])typeof(GameAudio).GetField("combatLayers",Hidden).GetValue(audio);
        for(int i=0;i<voices.Length;i++)
        {
            voices[i]=voicesHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            layers[i]=voicesHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
        }
        Set(audio,"effects",voicesHost.AddComponent<AudioSource>());Set(audio,"previewEnabled",true);singleton.SetValue(null,audio);
        battleHost=new GameObject("Inactive god footsteps sound emitter");battleHost.SetActive(false);
        battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();Type type=typeof(MonsterAutoBattlePrototype);
        unit=Activator.CreateInstance(type.GetNestedType("Unit",Hidden),true);
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_tino"));Set(unit,"model",voicesHost);
        presentation=Activator.CreateInstance(type.GetNestedType("Rank4Presentation",Hidden),true);
        request=Activator.CreateInstance(type.GetNestedType("Rank4PresentationRequest",Hidden),true);
        Set(request,"context",new Rank4StageContext{family="tyranno"});Set(presentation,"request",request);
        foreach(string key in new[]{"step","prepare","reign"})
        {
            var clip=Resources.Load<AudioClip>("Audio/Combat/Tyranno/"+key);Assert.That(clip,Is.Not.Null);
            clip.LoadAudioData();while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded));
        }
        Time.timeScale=0f;
    }

    [TearDown]public void Cleanup()
    {
        if(battle!=null)Set(battle,"rank4Presentation",null);
        if(audio!=null)audio.StopCombatSounds();singleton.SetValue(null,previousAudio);frontend.SetValue(null,previousFrontend);
        Time.timeScale=previousSpeed;Object.DestroyImmediate(battleHost);Object.DestroyImmediate(host);Object.DestroyImmediate(voicesHost);
    }

    private void Emit(string cue)
    {
        var type=typeof(MonsterAutoBattlePrototype);
        typeof(MonsterAutoBattlePrototype).GetMethod("PlayTyrannoSound",Hidden).Invoke(battle,new[]{unit,
            Enum.Parse(type.GetNestedType("TyrannoSound",Hidden),cue),(object)Vector3.zero,1f});
    }

    [TestCase("Step")][TestCase("Prepare")][TestCase("Reign")]
    public void OnlyTheCurrentGodFootstepsCasterCanPlayItsPhasesWhileTheWorldIsHeld(string cue)
    {
        Emit(cue);Assert.That(audio.PlayedCombatSounds,Is.Zero);
        Set(battle,"rank4Presentation",presentation);
        Set(request,"source",Activator.CreateInstance(typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden),true));
        Emit(cue);Assert.That(audio.PlayedCombatSounds,Is.Zero);
        Set(request,"source",unit);Set(request,"context",new Rank4StageContext{family="ninja_cat"});
        Emit(cue);Assert.That(audio.PlayedCombatSounds,Is.Zero);
        Set(request,"context",new Rank4StageContext{family="tyranno"});Emit(cue);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));Assert.That(voices[0].pitch,Is.EqualTo(1f));
        Assert.That(voices[0].clip,Is.SameAs(Resources.Load<AudioClip>("Audio/Combat/Tyranno/"+cue.ToLowerInvariant())));
        Assert.That(voices[0].loop,Is.False);
        audio.StopCombatSounds();Set(battle,"rank4Presentation",null);Emit(cue);
        Assert.That(audio.PlayedCombatSounds,Is.Zero,"The sound exception ends with the presentation.");
    }

    [Test]public void UserPauseStillMutesTheCinematicFootstep()
    {
        Set(request,"source",unit);Set(battle,"rank4Presentation",presentation);audio.SetPaused(true);Emit("Step");
        Assert.That(audio.PlayedCombatSounds,Is.Zero);
    }

    [Test]public void EachFootstepIsAcceptedOnceAtItsUnscaledTimeAndTheFinalImpactIsADifferentCue()
    {
        var gate=new CombatSoundGate();
        int clip=Resources.Load<AudioClip>("Audio/Combat/Tyranno/step").GetInstanceID();
        for(int step=0;step<TyrannoGodFootstepsRules.StepCount;step++)
        {
            float time=TyrannoGodFootstepsRules.StepTimeAt(step);
            Assert.That(gate.TryAccept(17,CombatSoundCue.Impact,step+1,time,clip,true),Is.True);
            Assert.That(gate.TryAccept(17,CombatSoundCue.Impact,step+1,time,clip,true),Is.False);
        }
        int impact=Resources.Load<AudioClip>("Audio/Combat/Tyranno/reign").GetInstanceID();
        Assert.That(gate.TryAccept(17,CombatSoundCue.Impact,4,TyrannoReignRank4Stage.ImpactTime,impact,true),Is.True);
    }
}
