using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class DedicatedBasicAttackAudioTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private const BindingFlags Fields=Hidden|BindingFlags.Public;
    private const string Root="Audio/Combat/BasicFamilies/";
    private static readonly Dictionary<string,string[]> Dedicated=new Dictionary<string,string[]>
    {
        {"wyvern",new[]{Root+"Wyvern/attack",Root+"Wyvern/hit"}},
        {"monkey",new[]{Root+"Monkey/attack",Root+"Monkey/hit"}},
        {"wizard_mouse",new[]{"Audio/Combat/WizardMouse/launch","Audio/Combat/WizardMouse/hit"}},
        {"arachne",new[]{"Audio/Combat/Spider/launch","Audio/Combat/Spider/pierce"}},
        {"spider",new[]{"Audio/Combat/Spider/launch","Audio/Combat/Spider/pierce"}},
        {"frog",new[]{"Audio/Combat/Frog/launch","Audio/Combat/Frog/hit"}},
        {"mushroom",new[]{"Audio/Combat/Mushroom/launch","Audio/Combat/Mushroom/attach"}},
        {"spirit_deer",new[]{"Audio/Combat/SpiritDeer/spread","Audio/Combat/SpiritDeer/guard"}}
    };
    private GameObject host,voiceHost;
    private GameAudio audio;
    private AudioSource[] voices,layers;
    private object previousAudio,previousFrontend;
    private bool previousActive,previousEnabled,previousEnemyMute;
    private float previousScale;
    private UnityEngine.Random.State random;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object source,target;

    [UnitySetUp] public IEnumerator Setup()
    {
        previousAudio=Field(typeof(GameAudio),"instance").GetValue(null);
        previousFrontend=Field(typeof(GameFrontend),"instance").GetValue(null);
        previousActive=CombatEffectTestConfiguration.Active;previousEnabled=CombatReadabilityPreview.Enabled;
        previousEnemyMute=CombatEffectTestSceneController.MuteEnemySounds;
        previousScale=Time.timeScale;random=UnityEngine.Random.state;
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=false;
        Field(typeof(CombatEffectTestSceneController),"<MuteEnemySounds>k__BackingField").SetValue(null,false);Time.timeScale=1f;
        Field(typeof(GameFrontend),"instance").SetValue(null,null);
        foreach(string path in Dedicated.Values.SelectMany(x=>x).Distinct().Concat(new[]{
            "Audio/Combat/Asmr/attack_magic","Audio/Combat/Asmr/hit_energy","Audio/Combat/Asmr/attack_throw",
            "Audio/Combat/Asmr/hit_light","Audio/Combat/Asmr/critical_accent","Audio/Combat/Feedback/death_burst",
            "Audio/Combat/Feedback/basic_hit","Audio/Combat/Raptor/rank3_impact"}))
        {
            var clip=Resources.Load<AudioClip>(path);Assert.That(clip,Is.Not.Null,path);clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),path);
        }
        host=new GameObject("Inactive dedicated basic audio fixture");host.SetActive(false);
        voiceHost=new GameObject("Dedicated basic audio fixture voices");audio=host.AddComponent<GameAudio>();
        Call(audio,"InitializeCombatAudio");Field(typeof(GameAudio),"instance").SetValue(null,audio);
        Field(typeof(GameAudio),"previewEnabled").SetValue(audio,true);
        Field(typeof(GameAudio),"effects").SetValue(audio,voiceHost.AddComponent<AudioSource>());
        voices=Get<AudioSource[]>(audio,"combatVoices");layers=Get<AudioSource[]>(audio,"combatLayers");
        for(int i=0;i<voices.Length;i++)
        {
            voices[i]=voiceHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            layers[i]=voiceHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
        }
        fixture=new MonkeyArcTests();fixture.SetUp();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        source=Get<object>(fixture,"source");target=Get<object>(fixture,"target");
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            if(host!=null)Object.DestroyImmediate(host);if(voiceHost!=null)Object.DestroyImmediate(voiceHost);
            Field(typeof(GameAudio),"instance").SetValue(null,previousAudio);
            Field(typeof(GameFrontend),"instance").SetValue(null,previousFrontend);
            CombatEffectTestConfiguration.Active=previousActive;CombatReadabilityPreview.Enabled=previousEnabled;
            Field(typeof(CombatEffectTestSceneController),"<MuteEnemySounds>k__BackingField").SetValue(null,previousEnemyMute);
            Time.timeScale=previousScale;UnityEngine.Random.state=random;
        }
    }

    private static IEnumerable<TestCaseData> BasicCues()
    {
        foreach(string family in Dedicated.Keys)
        foreach(var cue in new[]{CombatSoundCue.Attack,CombatSoundCue.Hit})
            yield return new TestCaseData(family,CombatSoundBank.StyleForCue(family,true,cue),cue);
    }

    [TestCaseSource(nameof(BasicCues))]
    public void DedicatedBasicsUseTheExistingBasicChannelGainPriorityAndRandomContract(
        string family,CombatSoundStyle style,CombatSoundCue cue)
    {
        var clip=DedicatedClip(family,cue);
        Field(typeof(GameAudio),"authoredBasicHit").SetValue(audio,clip);
        string before=JsonUtility.ToJson(UnityEngine.Random.state);
        GameAudio.PlayCombat(style,cue,17,Vector3.zero,basicFamily:family);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));Assert.That(voices[0].clip,Is.SameAs(clip));
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(cue==CombatSoundCue.Attack?.055f:.22f).Within(.00001f));
        Assert.That(Get<int[]>(audio,"voicePriority")[0],Is.EqualTo(cue==CombatSoundCue.Attack?-1:0));
        Assert.That(layers[0].isPlaying,Is.False);Assert.That(voices[0].loop,Is.False);
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(before));
        Assert.That(Enum.GetValues(typeof(CombatSoundStyle)).Length,Is.EqualTo(7));
    }

    [Test] public void DedicatedClipsHaveExactlyOneValidMatchingPreparedCatalogEntry()
    {
        var asset=Resources.Load<TextAsset>(GameAudio.PreparedCombatSoundsPath);Assert.That(asset,Is.Not.Null);
        var catalog=JsonUtility.FromJson<GameAudio.PreparedCombatSounds>(asset.text);Assert.That(catalog.clips,Is.Not.Null);
        var paths=Dedicated.Values.SelectMany(x=>x).Distinct().ToArray();Assert.That(paths.Length,Is.EqualTo(14));
        foreach(string path in paths)
        {
            var matches=catalog.clips.Where(x=>x.path==path).ToArray();
            Assert.That(matches.Length,Is.EqualTo(1),path);var entry=matches[0];var clip=Resources.Load<AudioClip>(path);
            Assert.That(entry.samples,Is.EqualTo(clip.samples),path);Assert.That(entry.channels,Is.EqualTo(clip.channels),path);
            Assert.That(entry.frequency,Is.EqualTo(clip.frequency),path);
            Assert.That(entry.peak,Is.GreaterThan(0f).And.LessThanOrEqualTo(1f),path);
            Assert.That(entry.rms,Is.GreaterThan(0f).And.LessThanOrEqualTo(entry.peak),path);
        }
    }

    private static IEnumerable<TestCaseData> Variants()
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        foreach(var definition in database.monsters.Where(x=>Dedicated.ContainsKey(x.familyId)).OrderBy(x=>x.monsterId))
        foreach(var cue in new[]{CombatSoundCue.Attack,CombatSoundCue.Hit})
            yield return new TestCaseData(definition.monsterId,cue).SetName("DedicatedBasicFamilyForward_"+definition.monsterId+"_"+cue);
    }

    [Test] public void DedicatedRosterContainsSevenFamiliesWithFourVariantsAndNoDuplicateAliasSpecies()
    {
        var definitions=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.Where(x=>Dedicated.ContainsKey(x.familyId)).ToArray();
        Assert.That(definitions.Length,Is.EqualTo(28));Assert.That(definitions.Select(x=>x.monsterId).Distinct().Count(),Is.EqualTo(28));
        Assert.That(definitions.Select(x=>x.familyId).Distinct().Count(),Is.EqualTo(7));
        foreach(var family in definitions.GroupBy(x=>x.familyId))Assert.That(family.Count(),Is.EqualTo(4),family.Key);
    }

    [TestCaseSource(nameof(Variants))]
    public void EveryDedicatedFamilyVariantForwardsItsFamilyThroughTheActualBattleSoundHook(string species,CombatSoundCue cue)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(species);
        Set(source,"definition",definition);Set(source,"attackRange",definition.attackRange);
        Call(battle,"PlayCombatSound",source,cue,Vector3.zero,1f,false,false,false,null,false,false,false);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));
        Assert.That(voices[0].clip,Is.SameAs(DedicatedClip(definition.familyId,cue)));
    }

    [TestCase(null,CombatSoundStyle.Arcane,"attack_magic","hit_energy")]
    [TestCase("unknown_family",CombatSoundStyle.Projectile,"attack_throw","hit_light")]
    [TestCase("raptor",CombatSoundStyle.Bite,"attack_swing","hit_bite")]
    [TestCase("sabertooth",CombatSoundStyle.Slash,"attack_claw","hit_slash")]
    [TestCase("mantis",CombatSoundStyle.Slash,"attack_claw","hit_slash")]
    [TestCase("griffin",CombatSoundStyle.Slash,"attack_claw","hit_slash")]
    public void OtherFamiliesAndMissingNamesKeepTheExistingSharedPalette(string family,CombatSoundStyle style,string attack,string hit)
    {
        foreach(var cue in new[]{CombatSoundCue.Attack,CombatSoundCue.Hit})
        {
            audio.StopCombatSounds();GameAudio.PlayCombat(style,cue,18,Vector3.zero,basicFamily:family);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));Assert.That(voices[0].clip,
                Is.SameAs(Resources.Load<AudioClip>("Audio/Combat/Asmr/"+(cue==CombatSoundCue.Attack?attack:hit))));
        }
    }

    private static IEnumerable<TestCaseData> SkillCues()
    {
        foreach(string family in Dedicated.Keys.Where(x=>x!="spider"))
        foreach(var cue in new[]{CombatSoundCue.Cast,CombatSoundCue.Impact,CombatSoundCue.Periodic,CombatSoundCue.Death})
            yield return new TestCaseData(family,cue);
    }

    [TestCaseSource(nameof(SkillCues))]
    public void FamilyOverridesNeverReplaceSkillPeriodicOrDeathClips(string family,CombatSoundCue cue)
    {
        var clip=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank3_impact");
        GameAudio.PlayCombat(CombatSoundStyle.Arcane,cue,19,Vector3.zero,authoredSound:clip);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));var original=voices[0].clip;float gain=Get<float[]>(audio,"voiceGain")[0];
        audio.StopCombatSounds();GameAudio.PlayCombat(CombatSoundStyle.Arcane,cue,19,Vector3.zero,authoredSound:clip,basicFamily:family);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));Assert.That(voices[0].clip,Is.SameAs(original));
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(gain));Assert.That(layers[0].isPlaying,Is.False);
    }

    [TestCase("wyvern",CombatSoundStyle.Arcane)]
    [TestCase("monkey",CombatSoundStyle.Projectile)]
    [TestCase("wizard_mouse",CombatSoundStyle.Arcane)]
    [TestCase("spirit_deer",CombatSoundStyle.Spirit)]
    public void DedicatedCriticalHitsKeepTheirNormalizedCoreAndAddOnlyTheExistingAccent(string family,CombatSoundStyle style)
    {
        var clip=DedicatedClip(family,CombatSoundCue.Hit);
        float normalized=Mathf.Min(1f,(float)Call(audio,"MatchBasicHitLevel",clip,.24f));
        GameAudio.PlayCombat(style,CombatSoundCue.Hit,20,Vector3.zero,critical:true,basicFamily:family);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));Assert.That(voices[0].clip,Is.SameAs(clip));
        Assert.That(Get<float[]>(audio,"voiceGain")[0],Is.EqualTo(normalized).Within(.00001f));
        Assert.That(layers[0].clip,Is.SameAs(Resources.Load<AudioClip>("Audio/Combat/Asmr/critical_accent")));
        Assert.That(layers[0].volume,Is.EqualTo(voices[0].volume*.38f).Within(.00001f));Assert.That(layers[0].loop,Is.False);
        for(int i=0;i<8;i++)GameAudio.PlayCombat(style,CombatSoundCue.Hit,20,Vector3.zero,critical:true,basicFamily:family);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));
    }

    [Test] public void EndingStopsRepeatingWithoutMutingTheLastDeathBurst()
    {
        var clip=Resources.Load<AudioClip>("Audio/Combat/Raptor/rank3_impact");
        GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Cast,71,Vector3.zero,
            authoredSound:clip,loop:true);
        Assert.That(voices[0].isPlaying&&voices[0].loop,Is.True);
        GameAudio.FinishCombatLoops();
        Assert.That(voices[0].loop,Is.False);Assert.That(voices[0].isPlaying,Is.True);
        Assert.That(layers.All(x=>!x.loop),Is.True);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Death,72,Vector3.zero);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(2));
        GameAudio.SetBattlePhase(false);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Death,73,Vector3.zero);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(2));
    }

    [TestCase("pause")] [TestCase("end")] [TestCase("mute")] [TestCase("time")]
    public void DedicatedBasicsRespectTheExistingPauseEndMuteAndTimeStopGates(string gate)
    {
        if(gate=="pause")audio.SetPaused(true);
        if(gate=="end")GameAudio.SetBattlePhase(false);
        if(gate=="mute"){audio.ToggleCue(CombatSoundCue.Attack);audio.ToggleCue(CombatSoundCue.Hit);}
        if(gate=="time")Time.timeScale=0f;
        GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Attack,21,Vector3.zero,basicFamily:"wyvern");
        GameAudio.PlayCombat(CombatSoundStyle.Projectile,CombatSoundCue.Hit,22,Vector3.zero,basicFamily:"monkey");
        Assert.That(audio.PlayedCombatSounds,Is.Zero);
    }

    [TestCase("mlEpisodeActive")] [TestCase("battleEnding")]
    public void TheBattleHookStillSuppressesHeadlessTrainingAndEndedCombat(string blocker)
    {
        Set(battle,blocker,true);Call(battle,"PlayCombatSound",source,CombatSoundCue.Hit,Vector3.zero,1f,false,false,false,null,false,false,false);
        Assert.That(audio.PlayedCombatSounds,Is.Zero);
    }

    [Test] public void DedicatedReleasesKeepTheirSmallBudgetAndLeaveTheNormalHitBudgetAvailable()
    {
        GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Attack,100,Vector3.zero,basicFamily:"wyvern");
        GameAudio.PlayCombat(CombatSoundStyle.Projectile,CombatSoundCue.Attack,101,Vector3.zero,basicFamily:"monkey");
        GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Attack,102,Vector3.zero);
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(2));
        for(int i=0;i<6;i++)GameAudio.PlayCombat(i%2==0?CombatSoundStyle.Arcane:CombatSoundStyle.Projectile,
            CombatSoundCue.Hit,200+i,Vector3.zero,basicFamily:i%2==0?"wyvern":"monkey");
        Assert.That(audio.PlayedCombatSounds,Is.EqualTo(8));
    }

    [Test] public void ActualEightTargetWyvernPierceUsesOneDedicatedHitWithoutChangingPierceDamage()
    {
        Call(fixture,"BoltSetup");Set(battle,"mlEpisodeActive",false);
        Get<GameObject>(target,"model").transform.position=Vector3.right*.75f;
        var victims=new List<object>{target};
        for(int i=1;i<8;i++)victims.Add(Call(fixture,"Unit",1,Vector3.right*(.75f+i*.7f)));
        var before=victims.Select(x=>Get<float>(x,"hp")).ToArray();
        Call(battle,"BeginBasicAttackImpact",source,target,100f,false,1f);
        for(int i=0;i<victims.Count;i++)Assert.That(Get<float>(victims[i],"hp"),Is.LessThan(before[i]));
        Assert.That(audio.PlayedCount(CombatSoundStyle.Arcane,CombatSoundCue.Attack),Is.EqualTo(1));
        Assert.That(audio.PlayedCount(CombatSoundStyle.Arcane,CombatSoundCue.Hit),Is.EqualTo(1));
        Assert.That(audio.SuppressedCombatSounds,Is.GreaterThanOrEqualTo(7));
        Assert.That(voices.Any(x=>x.clip==Resources.Load<AudioClip>(Root+"Wyvern/hit")),Is.True);
    }

    private static AudioClip DedicatedClip(string family,CombatSoundCue cue)=>
        Resources.Load<AudioClip>(Dedicated[family][cue==CombatSoundCue.Attack?0:1]);
    private static FieldInfo Field(Type type,string field)=>type.GetField(field,Fields);
    private static T Get<T>(object value,string field)=>(T)Field(value.GetType(),field).GetValue(value);
    private static void Set(object value,string field,object data)=>Field(value.GetType(),field).SetValue(value,data);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Fields).Invoke(value,args);
}
