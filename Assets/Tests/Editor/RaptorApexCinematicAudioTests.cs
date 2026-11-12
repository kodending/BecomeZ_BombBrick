using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class RaptorApexCinematicAudioTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject host;
    private RaptorApexCinematicStage stage;
    private float previousScale;

    [SetUp]public void SetUp()
    {
        previousScale=Time.timeScale;
        host=new GameObject("Raptor continuous cinematic audio test");
        Rank4CinematicAudio.Warmup();
    }
    [TearDown]public void TearDown()
    {
        stage?.Dispose();if(host!=null)Object.DestroyImmediate(host);Time.timeScale=previousScale;
    }

    [Test]public void NativeLeapAndHitClipsRemainSeparateFromTheContinuousFilm()
    {
        AudioClip leap=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank4_start");
        AudioClip hit=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank4_impact");
        AudioClip timeline=Resources.Load<AudioClip>("Audio/Combat/Cinematics/raptor/timeline");
        Assert.That(leap,Is.Not.Null);Assert.That(hit,Is.Not.Null);Assert.That(timeline,Is.Not.Null);
        Assert.That(timeline,Is.Not.SameAs(leap).And.Not.SameAs(hit));
    }

    [Test]public void AllSpeciesAndShinyUseOneContinuousRaptorBankWithoutRetriggeringAtPoseChanges()
    {
        CreateStage();Time.timeScale=0f;object bank=null;
        foreach(string species in new[]{"velos","velok","raputo","wilo"})
        {
            Assert.That(stage.Begin(species,false,species=="velos"),Is.True,species);
            object selected=Field<object>(Audio,"bank");
            Assert.That(selected,Is.Not.Null);Assert.That(Field<string>(selected,"family"),Is.EqualTo("raptor"));
            if(bank!=null)Assert.That(selected,Is.SameAs(bank));bank=selected;
            AudioSource voice=Field<AudioSource>(Audio,"source");AudioClip clip=voice.clip;
            foreach(float time in new[]{1.05f,2.4f,3.25f,4.5f,5.25f,5.25f})
            {
                TickAudio(time);Assert.That(Field<object>(Audio,"bank"),Is.SameAs(bank));
                Assert.That(Field<AudioSource>(Audio,"source"),Is.SameAs(voice));
                Assert.That(voice.clip,Is.SameAs(clip));Assert.That(voice.isPlaying,Is.False);
            }
        }
        Assert.That(Time.timeScale,Is.Zero);
    }

    [Test]public void NormalReturnKeepsItsEndingWhileHideAndDisposalClearIt()
    {
        CreateStage();Assert.That(stage.Begin("velos",false),Is.True);
        AudioSource voice=Field<AudioSource>(Audio,"source");AudioClip clip=voice.clip;
        stage.CompletePresentation();
        Assert.That(stage.Active,Is.False);Assert.That(voice.clip,Is.SameAs(clip));
        Assert.That(Field<object>(Audio,"bank"),Is.Null);
        stage.Hide();Assert.That(voice.clip,Is.Null);Assert.That(voice.isPlaying,Is.False);
        Assert.That(Field<bool>(Audio,"focusHeld"),Is.False);
        Assert.That(stage.Begin("velos",true),Is.True);
        stage.Dispose();stage=null;Assert.That(voice==null,Is.True);
        Assert.That(clip,Is.Not.Null,"Stage disposal must preserve shared audio assets.");
    }

    [Test]public void LoadingWarmupIsSilentAndLeavesLaterAudiblePresentationsAvailable()
    {
        CreateStage();var warmup=stage.WarmupRender();
        Assert.That(warmup.MoveNext(),Is.True);Assert.That(Field<bool>(Audio,"playAudio"),Is.False);
        foreach(var voice in host.GetComponentsInChildren<AudioSource>(true))
        {Assert.That(voice.isPlaying,Is.False);Assert.That(voice.clip,Is.Null);}
        Assert.That(warmup.MoveNext(),Is.False);Assert.That(stage.Active,Is.False);
        Assert.That(Field<bool>(Audio,"focusHeld"),Is.False);
        stage.PlayPrelude(false);Assert.That(Field<bool>(Audio,"playAudio"),Is.False);
        Assert.That(stage.Begin("velok",false),Is.True);Assert.That(Field<bool>(Audio,"playAudio"),Is.True);
        Assert.That(host.GetComponentsInChildren<AudioSource>(true),Has.Length.EqualTo(3));
    }

    [TestCase(false)][TestCase(true)]
    public void EnemyTestMuteAppliesToBothThePreludeAndTheContinuousFilm(bool enemy)
    {
        var fixture=new RaptorApexCinematicTests();
        PropertyInfo mute=typeof(CombatEffectTestSceneController).GetProperty("MuteEnemySounds",BindingFlags.Static|BindingFlags.Public);
        bool previousMute=(bool)mute.GetValue(null);fixture.SetUp();
        try
        {
            mute.GetSetMethod(true).Invoke(null,new object[]{true});
            object battle=Field<object>(fixture,"battle"),actor=Field<object>(fixture,enemy?"target":"source");
            Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("TryBeginRank4SkillPrelude",Hidden)
                .Invoke(battle,new[]{actor,new object()}),Is.True);
            var runtimeStage=Field<RaptorApexCinematicStage>(battle,"raptorCinematicStage");
            var audio=Field<Rank4CinematicAudio>(runtimeStage,"continuousAudio");
            Assert.That(Field<bool>(audio,"playAudio"),Is.EqualTo(!enemy));
            typeof(MonsterAutoBattlePrototype).GetMethod("CancelRank4SkillPrelude",Hidden).Invoke(battle,null);
            Assert.That(Field<bool>(audio,"focusHeld"),Is.False);
            Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("BeginRaptorApexCinematic",Hidden).Invoke(battle,new[]{actor}),Is.True);
            Assert.That(Field<bool>(audio,"playAudio"),Is.EqualTo(!enemy));Assert.That(Time.timeScale,Is.Zero);
        }
        finally{mute.GetSetMethod(true).Invoke(null,new object[]{previousMute});fixture.TearDown();}
    }

    [Test]public void NaturalPreludeCompletionKeepsItsTailUntilTheMainShotCrossfade()
    {
        var fixture=new RaptorApexCinematicTests();fixture.SetUp();
        try
        {
            object battle=Field<object>(fixture,"battle"),actor=Field<object>(fixture,"source");
            Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("TryBeginRank4SkillPrelude",Hidden)
                .Invoke(battle,new[]{actor,new object()}),Is.True);
            var runtimeStage=Field<RaptorApexCinematicStage>(battle,"raptorCinematicStage");
            var audio=Field<Rank4CinematicAudio>(runtimeStage,"continuousAudio");
            AudioSource voice=Field<AudioSource>(audio,"preludeSource");AudioClip clip=voice.clip;
            object prelude=Field<object>(battle,"rank4SkillPrelude");
            prelude.GetType().GetField("elapsed",Hidden|BindingFlags.Public).SetValue(prelude,.451f);
            Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("UpdateRank4SkillPrelude",Hidden).Invoke(battle,null),Is.False);
            Assert.That(voice.clip,Is.SameAs(clip));
            Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("BeginRaptorApexCinematic",Hidden).Invoke(battle,new[]{actor}),Is.True);
            Assert.That(voice.clip,Is.SameAs(clip));audio.UpdateAudio(.1f);Assert.That(voice.clip,Is.Null);
        }
        finally{fixture.TearDown();}
    }

    private Rank4CinematicAudio Audio=>Field<Rank4CinematicAudio>(stage,"continuousAudio");
    private void CreateStage()=>stage=new RaptorApexCinematicStage(host.transform);
    private void TickAudio(float elapsed)=>typeof(RaptorApexCinematicStage).GetMethod("TickAudio",Hidden).Invoke(stage,new object[]{elapsed});
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden|BindingFlags.Public).GetValue(value);
}
