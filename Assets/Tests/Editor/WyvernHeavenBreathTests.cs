using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private IList HeavenBreaths=>(IList)typeof(MonsterAutoBattlePrototype).GetField("wyvernHeavenBreaths",Private).GetValue(battle);
    private void HeavenTick(float dt)=>GriffinCall("UpdateWyvernHeavenBreaths",dt);

    [TestCase(null)]
    [TestCase(WyvernRank3Rules.CataclysmBreath)]
    [TestCase(WyvernRank3Rules.WingSweep)]
    [TestCase(WyvernRank3Rules.DragonCore)]
    public void WyvernHeavenReplacesEveryRankThreeVariantAndRecastsAfterItsCooldown(string trait)
    {
        if(trait!=null)Trait(trait);Set(source,"augmentTranscended",true);WyvernSetup();
        Assert.That(Get(source,"wyvernBreath"),Is.Null);Assert.That(HeavenBreaths.Count,Is.EqualTo(1));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));
        Assert.That((IList)typeof(MonsterAutoBattlePrototype).GetField("wyvernBreaths",Private).GetValue(battle),Is.Empty);
        object distant=Unit(1,new Vector3(-18,0,-18)),friend=Unit(0,Vector3.zero);
        object outside=Unit(1,new Vector3(21,0,0)),stored=Unit(1,new Vector3(4,0,0));Set(stored,"inStorage",true);
        object dead=Unit(1,Vector3.zero);Set(dead,"alive",false);
        HeavenTick(0);HeavenTick(WyvernHeavenBreathRules.ImpactTime-.001f);Assert.That(Hp(distant),Is.EqualTo(5000));
        HeavenTick(.001f);Assert.That(Hp(distant),Is.EqualTo(4700));Assert.That(Hp(friend),Is.EqualTo(5000));
        Assert.That(Hp(outside),Is.EqualTo(5000));Assert.That(Hp(stored),Is.EqualTo(5000));Assert.That(Hp(dead),Is.EqualTo(5000));
        Assert.That(((CombatStatusState)Get(distant,"combatStatuses")).Count,Is.Zero,"No extra control or elemental damage.");
        HeavenTick(2);Assert.That(Hp(distant),Is.EqualTo(4700));Assert.That(HeavenBreaths.Count,Is.Zero);
        Set(source,"skillTimer",0f);
        Assert.That(GriffinCall("BeginWyvernBreath",source,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.True);
        Assert.That(Get(source,"wyvernBreath"),Is.Null);Assert.That(HeavenBreaths.Count,Is.EqualTo(1));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(2));Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));
        HeavenTick(WyvernHeavenBreathRules.ImpactTime);Assert.That(Hp(distant),Is.EqualTo(4400f));
        HeavenTick(WyvernHeavenBreathRules.EndTime);Assert.That(HeavenBreaths,Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void InterruptedRankThreeCastsAndRankFourEchoesCannotCreateAHeavenCast(bool echo)
    {
        GriffinSetup("vivre");
        if(echo)
        {
            Set(source,"augmentTranscended",true);
            Assert.That(GriffinCall("StartWyvernBreath",source,target,GameMasterData.Skill(WyvernCombatRules.SkillId),1f,true,0),Is.False);
            Assert.That(Get(source,"wyvernBreath"),Is.Null);
        }
        else{WyvernSetup();((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Knockdown,1f,target);BreathTick(2);}
        Assert.That(HeavenBreaths.Count,Is.Zero);
    }

    [TestCase("death")] [TestCase("end")] [TestCase("reset")] [TestCase("generation")]
    public void WyvernHeavenSurvivesOwnerDeathAndCancelsWhenItsBattleEnds(string reason)
    {
        Set(source,"augmentTranscended",true);WyvernSetup();HeavenTick(.3f);
        float before=Hp(target);
        if(reason=="death")Set(source,"alive",false);
        else if(reason=="end")typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
        else if(reason=="generation")typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else GriffinCall("ResetFamilyTranscendenceStates");
        HeavenTick(3);Assert.That(Hp(target),Is.EqualTo(reason=="death"?before-300f:before));
        Assert.That(HeavenBreaths.Count,Is.Zero);
    }

    [Test]
    public void WyvernHeavenPauseAndTeamSelectionDoNotChangeContactTiming()
    {
        Set(source,"augmentTranscended",true);Set(source,"team",Get(target,"team"));
        object opponent=Unit(0,new Vector3(-3,0,3));Set(source,"target",opponent);WyvernSetup();
        Set(opponent,"hp",5000f);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);HeavenTick(10);
        Assert.That(Hp(opponent),Is.EqualTo(5000));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);HeavenTick(WyvernHeavenBreathRules.ImpactTime);
        Assert.That(Hp(opponent),Is.EqualTo(4700));Assert.That(Hp(target),Is.EqualTo(5000));
    }

    [Test]
    public void WyvernHeavenShadowPassesUpwardBeforeTheDescendingMassAndCleansUp()
    {
        var fx=PixelWyvernHeavenBreath.Create(new Bounds(Vector3.zero,new Vector3(20,1,12)),null,100,1);
        var shadow=fx.transform.Find("Passing Dragon Shadow").GetComponent<SpriteRenderer>();
        Assert.That(shadow.sprite,Is.Not.Null);fx.Advance(.1f);var before=shadow.transform.position;
        fx.Advance(.5f);Assert.That(Vector3.Dot(shadow.transform.position-before,shadow.transform.up),Is.GreaterThan(0));
        var body=fx.GetComponentsInChildren<ParticleSystem>().First(p=>p.name=="Heaven Breath Mass");
        float start=WyvernHeavenBreathRules.ShadowDuration+WyvernHeavenBreathRules.PauseDuration;
        fx.Advance(start-.1f);Assert.That(shadow.enabled,Is.False);Assert.That(body.particleCount,Is.Zero);
        var landing=fx.transform.Find("Heaven Breath Landing Shadow").GetComponent<SpriteRenderer>();
        var particles=new ParticleSystem.Particle[288];fx.Advance(start+.1f);body.GetParticles(particles);float high=particles.Average(p=>p.position.y);
        float small=landing.transform.localScale.x;
        fx.Advance(WyvernHeavenBreathRules.ImpactTime-.001f);
        Assert.That(landing.enabled,Is.True);Assert.That(landing.transform.localScale.x,Is.GreaterThan(small*3f));
        Assert.That(landing.transform.localScale.x,Is.GreaterThan(20f*1.5f),"Shadow covers arena corners before contact.");
        body.GetParticles(particles);Assert.That(particles.Max(p=>p.position.x)-particles.Min(p=>p.position.x),Is.LessThan(12f),"The descending mass stays connected around one core.");
        fx.Advance(WyvernHeavenBreathRules.ImpactTime);body.GetParticles(particles);Assert.That(particles.Average(p=>p.position.y),Is.LessThan(high-5f));
        Assert.That(landing.enabled,Is.False);
        Assert.That(particles.All(p=>p.startColor.a>0),Is.True);
        fx.Advance(WyvernHeavenBreathRules.ImpactTime+.35f);body.GetParticles(particles);Assert.That(particles.All(p=>p.startColor.a==0),Is.True);
        fx.Cancel();fx.Cancel();Assert.That(fx==null,Is.True);
    }
}
