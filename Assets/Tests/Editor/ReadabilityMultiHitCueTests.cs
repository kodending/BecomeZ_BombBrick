using System;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
using Random=UnityEngine.Random;

public sealed class ReadabilityMultiHitCueTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private BearRagingStrikesTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object bear,prey,other,previousFrontend,previousAudio;
    private SpriteMonsterAnimationDriver driver;
    private bool active,enabled;
    private CombatReadabilityPreset preset;
    private float speed;
    private Random.State random;

    [SetUp] public void Setup()
    {
        active=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;preset=CombatReadabilityPreview.Preset;
        speed=Time.timeScale;random=Random.state;
        previousFrontend=Field(typeof(GameFrontend),"instance").GetValue(null);
        previousAudio=Field(typeof(GameAudio),"instance").GetValue(null);
        Field(typeof(GameFrontend),"instance").SetValue(null,null);Field(typeof(GameAudio),"instance").SetValue(null,null);
        CombatEffectTestConfiguration.Active=CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;Time.timeScale=1f;
        fixture=new BearRagingStrikesTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");bear=Get<object>(fixture,"bear");
        prey=Get<object>(fixture,"prey");other=Get<object>(fixture,"nearby");
        Model(other).transform.position=Vector3.left*4f;
        var definition=Get<MonsterDefinition>(bear,"definition");
        var model=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        model.transform.position=Model(bear).transform.position;
        driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();driver.ResetForReuse();
        Set(bear,"model",model);Set(bear,"spriteAnimation",driver);Set(battle,"battleStarted",true);
        Step(0f);
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            CombatEffectTestConfiguration.Active=active;CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=preset;
            Time.timeScale=speed;Random.state=random;
            Field(typeof(GameFrontend),"instance").SetValue(null,previousFrontend);Field(typeof(GameAudio),"instance").SetValue(null,previousAudio);
        }
    }

    [Test] public void ActualBearComboKeepsItsVictimThroughAllContactsAfterTheAiTargetChanges()
    {
        Attack();Set(bear,"target",other);
        Assert.That(driver.HasPendingBasicAttackHit,Is.False,"The combo owns its three callbacks rather than one basic-hit callback.");
        Assert.That(driver.IsBearRagingAttackActive,Is.True);
        float windup=Get<float>(driver,"bearRagingWindup");
        Step(windup+.001f);Assert.That(Hp(prey),Is.LessThan(5000f));
        Step(BearCombatRules.RagingThirdHitTime+BearCombatRules.RagingRecoveryDuration+.02f);
        Assert.That(driver.IsBearRagingAttackActive,Is.False);Assert.That(Hp(other),Is.EqualTo(5000f));
    }

    [Test] public void BearRetargetsOnlyWhenTheNextRealStrikeSelectsANewVictim()
    {
        Attack();Step(Get<float>(driver,"bearRagingWindup")+.001f);
        Model(other).transform.position=Vector3.left*.7f;Set(prey,"alive",false);
        Step(BearCombatRules.RagingSecondHitTime+.001f);
        Assert.That(Hp(other),Is.LessThan(5000f));
        Set(prey,"alive",true);Set(bear,"target",prey);
        driver.InterruptCurrentAction(true);float hp=Hp(other);Step(1f);
        Assert.That(Hp(other),Is.EqualTo(hp));
    }

    [TestCase(false)] [TestCase(true)]
    public void CancelOrDeathEndsTheBearSequenceWithoutExtraDamage(bool death)
    {
        Attack();Set(bear,"target",other);
        if(death){Set(bear,"alive",false);driver.PlayDeath();}else driver.InterruptCurrentAction(true);
        Step(1f);
        Assert.That(driver.IsBearRagingAttackActive,Is.False);Assert.That(Hp(prey),Is.EqualTo(5000f));
    }

    [Test] public void FreezePausesContactsWithoutRedirectingTheAcceptedCombo()
    {
        Attack();Set(bear,"target",other);driver.SetFrozen(true);Step(2f);
        Assert.That(Hp(prey),Is.EqualTo(5000f));
        driver.SetFrozen(false);Step(Get<float>(driver,"bearRagingWindup")+.001f);
        Assert.That(Hp(prey),Is.LessThan(5000f));Assert.That(Hp(other),Is.EqualTo(5000f));
    }

    [TestCase(CombatReadabilityPreset.Current,true,true)]
    [TestCase(CombatReadabilityPreset.Compact,true,true)]
    [TestCase(CombatReadabilityPreset.Local,false,true)]
    [TestCase(CombatReadabilityPreset.Local,true,false)]
    public void OtherModesKeepTheAcceptedComboVictimAfterTheAiTargetChanges(CombatReadabilityPreset mode,bool selected,bool test)
    {
        CombatReadabilityPreview.Preset=mode;CombatReadabilityPreview.Enabled=selected;CombatEffectTestConfiguration.Active=test;
        Attack();Set(bear,"target",other);
        Assert.That(driver.IsBearRagingAttackActive,Is.True);
        Step(Get<float>(driver,"bearRagingWindup")+.001f);
        Assert.That(Hp(prey),Is.LessThan(5000f));Assert.That(Hp(other),Is.EqualTo(5000f));
    }

    [TestCase(false)] [TestCase(true)]
    public void ActualMantisPairUsesItsActionVictimAndReleasesOnEndOrCancel(bool cancel)
    {
        var mantis=new MonkeyArcTests();mantis.SetUp();
        try
        {
            var mb=Get<MonsterAutoBattlePrototype>(mantis,"battle");object source=Get<object>(mantis,"source"),victim=Get<object>(mantis,"target");
            object alternative=Call(mantis,"Unit",1,Vector3.left*4f);Model(victim).transform.position=Vector3.right*2f;
            Call(mantis,"ReaperReady",new object[]{null});
            Call(mb,"StartMantisReaperBasic",source,victim,100f,false,1f);Set(source,"target",alternative);
            Call(mantis,"ReaperTick",.12f);Assert.That(Hp(victim),Is.LessThan(5000f));
            if(cancel)Call(mb,"EndMantisReaperAction",source,false);
            else
            {
                float before=Hp(victim);Call(mantis,"ReaperTick",.1f);Assert.That(Hp(victim),Is.LessThan(before));
            }
            float after=Hp(victim);Call(mantis,"ReaperTick",.4f);
            Assert.That(Get<object>(source,"mantisReaperAction"),Is.Null);Assert.That(Hp(victim),Is.EqualTo(after));
            Assert.That(Hp(alternative),Is.EqualTo(5000f));
        }
        finally{mantis.TearDown();}
    }

    private void Attack()=>Call(battle,"Attack",bear,prey);
    private void Step(float dt){Call(driver,"Advance",dt);Call(driver,"LateUpdate");}
    private static float Hp(object unit)=>Get<float>(unit,"hp");
    private static GameObject Model(object unit)=>Get<GameObject>(unit,"model");
    private static FieldInfo Field(Type type,string field)=>type.GetField(field,Fields);
    private static T Get<T>(object target,string field)=>(T)Field(target.GetType(),field).GetValue(target);
    private static void Set(object target,string field,object value)=>Field(target.GetType(),field).SetValue(target,value);
    private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Fields).Invoke(target,args);
}
