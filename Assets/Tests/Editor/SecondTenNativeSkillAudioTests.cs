using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SecondTenNativeSkillAudioTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object source,previousAudio,previousFrontend;
    private GameAudio audio;
    private float previousScale;
    private bool previousTestMode;
    private readonly List<AudioClip> carriers=new List<AudioClip>();

    [SetUp]
    public void Setup()
    {
        previousScale=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousAudio=Field(typeof(GameAudio),"instance").GetValue(null);
        previousFrontend=Field(typeof(GameFrontend),"instance").GetValue(null);
        fixture=new MonkeyArcTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");source=Get<object>(fixture,"source");
        Field(source.GetType(),"id").SetValue(source,17);
        Field(typeof(GameFrontend),"instance").SetValue(null,null);
        CombatEffectTestConfiguration.Active=false;Time.timeScale=1f;
        var owner=new GameObject("Inactive native second-ten audio test");owner.SetActive(false);
        audio=owner.AddComponent<GameAudio>();
        var voiceHost=new GameObject("Native second-ten audio voices");
        var voices=Get<AudioSource[]>(audio,"combatVoices");var layers=Get<AudioSource[]>(audio,"combatLayers");
        for(int i=0;i<voices.Length;i++)
        {
            voices[i]=voiceHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            layers[i]=voiceHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
        }
        Field(typeof(GameAudio),"effects").SetValue(audio,voiceHost.AddComponent<AudioSource>());
        Field(typeof(GameAudio),"previewEnabled").SetValue(audio,true);
        Field(typeof(GameAudio),"instance").SetValue(null,audio);
        Inject("Mushroom","DeathBloom");Inject("Mushroom","DeathBreath");
        Inject("Griffin","OrbAbsorb");Inject("Griffin","OrbContract");Inject("Griffin","OrbRelease");
    }

    [TearDown]
    public void Cleanup()
    {
        try {if(audio!=null)audio.StopCombatSounds();fixture?.TearDown();}
        finally
        {
            Field(typeof(GameAudio),"instance").SetValue(null,previousAudio);
            Field(typeof(GameFrontend),"instance").SetValue(null,previousFrontend);
            Time.timeScale=previousScale;CombatEffectTestConfiguration.Active=previousTestMode;
            foreach(var clip in carriers)Object.DestroyImmediate(clip);carriers.Clear();
        }
    }

    private void Inject(string family,string sound)
    {
        var clip=AudioClip.Create("Native test "+sound,4800,1,48000,false);
        var samples=new float[4800];for(int i=0;i<samples.Length;i++)samples[i]=.1f*Mathf.Sin(i*.1f);
        clip.SetData(samples,0);carriers.Add(clip);
        var kind=typeof(MonsterAutoBattlePrototype).GetNestedType(family+"Sound",Hidden);
        var cache=Get<AudioClip[]>(battle,char.ToLowerInvariant(family[0])+family.Substring(1)+"SkillSounds");
        cache[Convert.ToInt32(Enum.Parse(kind,sound))]=clip;
    }

    private object Flower(bool audible)
    {
        Call(fixture,"DeathBloomReady",source);
        var definition=Get<MonsterDefinition>(source,"definition");
        var prefab=Resources.Load<GameObject>(definition.resourcePath);
        Assert.That(prefab,Is.Not.Null,"The fixture must provide the same source sprite as a spawned mushroom.");
        var original=prefab.GetComponentInChildren<SpriteRenderer>(true);
        Assert.That(original?.sprite,Is.Not.Null);
        Get<GameObject>(source,"model").AddComponent<SpriteRenderer>().sprite=original.sprite;
        Field(source.GetType(),"displayName").SetValue(source,definition.displayName);
        Field(source.GetType(),"hp").SetValue(source,0f);
        Field(source.GetType(),"alive").SetValue(source,false);
        Field(typeof(MonsterAutoBattlePrototype),"mlEpisodeActive").SetValue(battle,!audible);
        Call(battle,"TryBeginMushroomDeathBloom",source);
        var blooms=(IList)Field(typeof(MonsterAutoBattlePrototype),"mushroomDeathBlooms").GetValue(battle);
        Assert.That(blooms.Count,Is.EqualTo(1));
        var bloom=blooms[0];var flower=Get<object>(bloom,"flower");
        // The borrowed host is deliberately inactive; match KillForDeathBloom's live-actor setup.
        var model=Get<GameObject>(flower,"model");model.transform.SetParent(null,true);
        Get<GameObject>(flower,"ring").transform.SetParent(null,true);
        Assert.That(model.activeInHierarchy&&Get<bool>(flower,"alive"),Is.True);
        return bloom;
    }

    private void EnableNative()=>Field(typeof(MonsterAutoBattlePrototype),"mlEpisodeActive").SetValue(battle,false);
    private static FieldInfo Field(Type type,string name)=>type.GetField(name,Hidden);
    private static T Get<T>(object owner,string name)=>(T)Field(owner.GetType(),name).GetValue(owner);
    private static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Hidden).Invoke(owner,args);

    [Test]
    public void NativeFlowerWithoutFilmPlaysOneOpeningAndCannotReplayOnRepeatedDeathCallback()
    {
        Flower(true);Assert.That(audio.PlayedCombatSounds,Is.EqualTo(2),"One bloom and one cloud departure.");
        audio.StopCombatSounds();Call(battle,"TryBeginMushroomDeathBloom",source);
        Assert.That(audio.PlayedCombatSounds,Is.Zero,"Cleared audio gate must not hide repeated birth requests.");
    }

    [Test]
    public void NativeFlowerFilmHandoffNeverDuplicatesBirthEvenAfterTimeScaleHasResumed()
    {
        object bloom=Flower(false);Field(bloom.GetType(),"awaitingPresentation").SetValue(bloom,true);EnableNative();
        Call(battle,"ReleaseMushroomDeathBloom",source);Call(battle,"ReleaseMushroomDeathBloom",source);
        Assert.That(Get<bool>(bloom,"awaitingPresentation"),Is.False);
        Assert.That(audio.PlayedCombatSounds,Is.Zero,"The continuous film owns birth, independent of the global freeze gate.");
    }

    [Test]
    public void NativeFlowerBreathsOnceAtTwoFourSixSecondsWithoutPoisonTickAccents()
    {
        Flower(false);EnableNative();
        var steps=new[]{1f,1f,0f,1f,1f,0f,1f,1f,0f};var counts=new[]{0,1,0,0,1,0,0,1,0};
        for(int i=0;i<steps.Length;i++)
        {
            float previous=Get<float>(((IList)Get<object>(battle,"mushroomDeathBlooms"))[0],"age");
            audio.StopCombatSounds();Call(fixture,"DeathBloomTick",steps[i]);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(counts[i]),"Flower age "+previous+" + "+steps[i]);
        }
        Assert.That(Get<int>(source,"skillHits"),Is.EqualTo(12),"Audio spacing must not remove .5 second poison ticks.");
    }

    [Test]
    public void NativeFlowerLongFrameEmitsOnlyOneCurrentBreathWhileAllDamageTicksRemain()
    {
        Flower(false);EnableNative();Call(fixture,"DeathBloomTick",6.1f);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1),"No backlog of 2/4/6 second exhalations.");
        Assert.That(Get<int>(source,"skillHits"),Is.EqualTo(12));
    }

    [Test]
    public void NativeStormOrbAudioThrottleNeverDropsCapturedProjectiles()
    {
        Call(fixture,"WindSetup");EnableNative();
        var winds=(IList)Get<object>(battle,"griffinCrosswinds");var shots=(IList)Get<object>(winds[0],"shots");
        int before=shots.Count;
        var times=new[]{0f,.119f,.121f};var counts=new[]{1,0,1};
        for(int i=0;i<times.Length;i++)
        {
            audio.StopCombatSounds();Call(fixture,"WindTime",times[i]);
            Assert.That(Call(fixture,"WindBlocks",Vector3.forward*10f,Vector3.back*10f,null),Is.True);
            Assert.That(shots.Count,Is.EqualTo(before+i+1));
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(counts[i]),"Captured at "+times[i]);
        }
    }

    [Test]
    public void NativeStormOrbContractionAndReleaseEachPlayExactlyOnce()
    {
        Call(fixture,"WindSetup");EnableNative();
        var times=new[]{7.99f,8f,8.1f,8.35f,8.36f};var counts=new[]{0,1,0,1,0};
        for(int i=0;i<times.Length;i++)
        {
            audio.StopCombatSounds();Call(fixture,"WindTick",times[i]);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(counts[i]),"Orb age "+times[i]);
        }
    }

    [Test]
    public void NativeNewFlowerAndOrbSoundsKeepTheDefaultStoppedWorldMute()
    {
        Time.timeScale=0f;
        foreach(string family in new[]{"Mushroom","Griffin"})
        {
            string monster=family=="Mushroom"?"versus":"pone";
            Field(source.GetType(),"definition").SetValue(source,Resources.Load<MonsterDefinition>("MonsterData/Monster_"+monster));
            var kind=typeof(MonsterAutoBattlePrototype).GetNestedType(family+"Sound",Hidden);
            foreach(string sound in family=="Mushroom"?new[]{"DeathBloom","DeathBreath"}:new[]{"OrbAbsorb","OrbContract","OrbRelease"})
                Call(battle,"Play"+family+"Sound",source,Enum.Parse(kind,sound),Vector3.zero,1f);
        }
        Assert.That(audio.PlayedCombatSounds,Is.Zero);
    }
}
