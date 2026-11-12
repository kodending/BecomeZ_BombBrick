using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
using Random=UnityEngine.Random;

public sealed class ReadabilityBasicImpactTimingTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object source,target,previousAudio,previousFrontend;
    private GameAudio audio;
    private AudioSource[] voices;
    private SpriteMonsterAnimationDriver victimDriver;
    private VoxelEnemyReaction victimReaction;
    private Random.State previousRandom;
    private bool previousActive,previousEnabled,previousContact,previousSkillAudio,previousBasic,previousReduced;
    private CombatReadabilityPreset previousPreset;
    private int previousSeed;
    private float previousScale;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        previousRandom=Random.state;previousScale=Time.timeScale;
        previousAudio=Field(typeof(GameAudio),"instance").GetValue(null);
        previousFrontend=Field(typeof(GameFrontend),"instance").GetValue(null);
        previousActive=CombatEffectTestConfiguration.Active;previousEnabled=CombatReadabilityPreview.Enabled;
        previousPreset=CombatReadabilityPreview.Preset;previousSeed=CombatReadabilityPreview.Seed;
        previousContact=CombatReadabilityPreview.ContactFeedback;previousSkillAudio=CombatReadabilityPreview.SkillAudio;
        previousBasic=CombatReadabilityPreview.BasicOnly;previousReduced=CombatFlashPresentation.Reduced;
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Reset();CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;CombatFlashPresentation.Apply(false);Time.timeScale=1f;
        Field(typeof(GameFrontend),"instance").SetValue(null,null);
        foreach(string key in new[]{"Asmr/hit_bite","Asmr/attack_swing",
            "BasicFamilies/Monkey/hit","BasicFamilies/Monkey/attack","Mushroom/attach","Mushroom/launch"})
        {
            var clip=Resources.Load<AudioClip>("Audio/Combat/"+key);
            Assert.That(clip,Is.Not.Null,key);clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded));
        }
        fixture=new MonkeyArcTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");source=Get<object>(fixture,"source");target=Get<object>(fixture,"target");
        var host=new GameObject("Inactive contact timing audio");host.SetActive(false);
        audio=host.AddComponent<GameAudio>();Call(audio,"InitializeCombatAudio");
        Field(typeof(GameAudio),"instance").SetValue(null,audio);Field(typeof(GameAudio),"previewEnabled").SetValue(audio,true);
        voices=Get<AudioSource[]>(audio,"combatVoices");var layers=Get<AudioSource[]>(audio,"combatLayers");
        var voiceHost=new GameObject("Contact timing audio voices");
        for(int i=0;i<voices.Length;i++)
        {
            voices[i]=voiceHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
            layers[i]=voiceHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
        }
        AttachVisual(target,"velos");victimDriver=(SpriteMonsterAnimationDriver)UnitGet(target,"spriteAnimation");
        victimReaction=victimDriver.transform.root.GetComponent<VoxelEnemyReaction>()??victimDriver.transform.root.gameObject.AddComponent<VoxelEnemyReaction>();
        victimReaction.Initialize();UnitSet(target,"reaction",victimReaction);
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.scene.IsValid()&&effect.name=="Contact impact")effect.ReleaseAfterBattleEndFade();
    }

    [TearDown]
    public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            Field(typeof(GameAudio),"instance").SetValue(null,previousAudio);
            Field(typeof(GameFrontend),"instance").SetValue(null,previousFrontend);
            CombatEffectTestConfiguration.Active=previousActive;CombatReadabilityPreview.Enabled=previousEnabled;
            CombatReadabilityPreview.Preset=previousPreset;CombatReadabilityPreview.Seed=previousSeed;
            CombatReadabilityPreview.ContactFeedback=previousContact;CombatReadabilityPreview.SkillAudio=previousSkillAudio;
            CombatReadabilityPreview.BasicOnly=previousBasic;CombatFlashPresentation.Apply(previousReduced);
            Time.timeScale=previousScale;Random.state=previousRandom;
        }
    }

    [TestCase("velos",CombatSoundStyle.Bite,"Asmr/hit_bite")]
    [TestCase("chiwoo",CombatSoundStyle.Projectile,"BasicFamilies/Monkey/hit")]
    [TestCase("kingmore",CombatSoundStyle.Spore,"Mushroom/attach")]
    public void ResolvedBasicDamageStartsItsFamilySoundAndVictimFeedbackInTheSameFrame(
        string monster,CombatSoundStyle style,string sound)
    {
        AttachVisual(source,monster);int frame=Time.frameCount;
        PixelCombatEffect bite=null;
        Sprite[] biteFrames=null;
        if(monster=="velos")
        {
            var renderer=victimDriver.GetComponent<SpriteRenderer>();
            bite=PixelCombatEffect.PlayRaptorBite(victimDriver.transform,renderer,1f);
            Assert.That(bite,Is.Not.Null);
            biteFrames=Get<Sprite[]>(bite,"frames");
            Call(bite,"Advance",1.25f/Get<float>(bite,"framesPerSecond"));
            Assert.That(bite.GetComponent<SpriteRenderer>().sprite,Is.SameAs(biteFrames[1]));
            UnitSet(source,"readabilityBiteEffect",bite);
            UnitSet(source,"readabilityBiteVersion",bite.ReadabilityBiteVersion);
        }
        AssertNoContact(style);
        Call(battle,"ResolveBasicAttackImpact",source,target,100f);
        AssertResolvedContact(style,sound);
        Assert.That(Time.frameCount,Is.EqualTo(frame),"Contact audio and visual feedback must start synchronously with damage.");
        if(bite!=null)
        {
            Assert.That(bite.GetComponent<SpriteRenderer>().sprite,Is.SameAs(biteFrames[2]),
                "The real damage callback must display the closed bite on its contact frame.");
            Assert.That(UnitGet(source,"readabilityBiteEffect"),Is.Null,"The consumed binding must not affect another attack.");
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void ADelayedBiteCannotAppearAfterItsAttackIsCancelledOrReplaced(bool replaced)
    {
        AttachVisual(source,"velos");
        var driver=(SpriteMonsterAnimationDriver)UnitGet(source,"spriteAnimation");
        UnitSet(source,"basicAttackCount",1);
        driver.PlayBasicAttack(1f,()=>Call(battle,"BeginBasicAttackImpact",source,target,100f,false,1f));
        int before=Object.FindObjectsOfType<PixelCombatEffect>().Count(effect=>effect.name=="Raptor Bite Effect");
        var routine=(IEnumerator)Call(battle,"PlayRaptorBiteEffect",source,target,1f,Get<int>(battle,"combatGeneration"),false);
        try
        {
            Assert.That(routine.MoveNext(),Is.True,"The authored bite must first wait for its lead-in.");
            Assert.That(driver.HasPendingBasicAttackHit,Is.True);
            if(replaced)UnitSet(source,"basicAttackCount",2);
            else driver.InterruptCurrentAction();
            Assert.That(routine.MoveNext(),Is.False);
            Assert.That(Object.FindObjectsOfType<PixelCombatEffect>().Count(effect=>effect.name=="Raptor Bite Effect"),Is.EqualTo(before));
            Assert.That(UnitGet(source,"readabilityBiteEffect"),Is.Null);
            AssertNoContact(CombatSoundStyle.Bite);
        }
        finally{(routine as IDisposable)?.Dispose();}
    }

    [TestCase("chiwoo",CombatSoundStyle.Projectile,"BasicFamilies/Monkey/hit","BasicFamilies/Monkey/attack")]
    [TestCase("kingmore",CombatSoundStyle.Spore,"Mushroom/attach","Mushroom/launch")]
    public void ActualProjectileArrivalStartsDamageAndHitSoundOnlyAtContact(
        string monster,CombatSoundStyle style,string hit,string release)
    {
        AttachVisual(source,monster);
        Call(battle,"BeginBasicAttackImpact",source,target,100f,false,1f);
        var shot=Object.FindObjectsOfType<PixelStarProjectile>().Single();
        Assert.That(audio.PlayedCount(style,CombatSoundCue.Attack),Is.EqualTo(1));
        Assert.That(voices.Any(v=>v.clip==Resources.Load<AudioClip>("Audio/Combat/"+release)),Is.True);
        AssertNoContact(style);
        Call(shot,"Advance",shot.FlightDuration-.005f);
        AssertNoContact(style);
        int frame=Time.frameCount;Call(shot,"Advance",.006f);
        AssertResolvedContact(style,hit);
        Assert.That(shot==null,Is.True,"The production arrival callback completes and releases its projectile.");
        Assert.That(Time.frameCount,Is.EqualTo(frame));
    }

    [TestCase(false)] [TestCase(true)]
    public void ZeroOrInvulnerableDamageCannotEmitAMisleadingHitSoundOrContact(bool invulnerable)
    {
        AttachVisual(source,"velos");if(invulnerable)UnitSet(target,"augmentReturnInvulnerabilityTimer",1f);
        float dealt=(float)Call(battle,"ApplyDamage",source,target,invulnerable?100f:0f,false,true,true,false,
            0f,0f,1f,false,false,false);
        Assert.That(dealt,Is.Zero);AssertNoContact(CombatSoundStyle.Bite);
        Assert.That(audio.PlayedCombatSounds,Is.Zero);
    }

    private void AssertNoContact(CombatSoundStyle style)
    {
        Assert.That((float)UnitGet(target,"hp"),Is.EqualTo(5000f));
        Assert.That(audio.PlayedCount(style,CombatSoundCue.Hit),Is.Zero);
        Assert.That(Get<float>(victimReaction,"flashTimer"),Is.Zero);
        Assert.That(Get<float>(victimDriver,"contactShakeRemaining"),Is.Zero);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
    }

    private void AssertResolvedContact(CombatSoundStyle style,string sound)
    {
        Assert.That((float)UnitGet(target,"hp"),Is.LessThan(5000f));
        Assert.That(audio.PlayedCount(style,CombatSoundCue.Hit),Is.EqualTo(1));
        var clip=Resources.Load<AudioClip>("Audio/Combat/"+sound);
        Assert.That(voices.Any(v=>v.clip==clip&&v.isPlaying),Is.True,"The real family contact voice must already be playing.");
        Assert.That(Get<float>(victimReaction,"flashTimer"),Is.GreaterThan(0f));
        Assert.That(Get<float>(victimDriver,"contactShakeRemaining"),Is.GreaterThan(0f));
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.EqualTo(1));
    }

    private void AttachVisual(object unit,string monster)
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        var definition=database.Get(monster);
        Assert.That(definition,Is.Not.Null,monster);
        var instance=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        instance.transform.position=((GameObject)UnitGet(unit,"model")).transform.position;
        var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Assert.That(driver,Is.Not.Null,monster);
        UnitSet(unit,"model",instance);UnitSet(unit,"definition",definition);UnitSet(unit,"spriteAnimation",driver);
        UnitSet(unit,"attackRange",definition.attackRange);UnitSet(unit,"skillId","");
        if(unit==source&&definition.attackRange<3)
            ((GameObject)UnitGet(target,"model")).transform.position=Vector3.right*.8f;
    }

    private static FieldInfo Field(Type type,string field)=>type.GetField(field,Hidden);
    private static T Get<T>(object instance,string field)=>(T)Field(instance.GetType(),field).GetValue(instance);
    private static object UnitGet(object unit,string field)=>unit.GetType().GetField(field).GetValue(unit);
    private static void UnitSet(object unit,string field,object value)=>unit.GetType().GetField(field).SetValue(unit,value);
    private static object Call(object instance,string method,params object[] args)=>instance.GetType().GetMethod(method,Hidden).Invoke(instance,args);
}
