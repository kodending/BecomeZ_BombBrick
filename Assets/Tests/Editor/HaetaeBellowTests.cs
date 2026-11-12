using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private SkillMasterRow BellowSetup(string species="haeta",string intrinsic=null)
    {
        var skill=HaetaeSetup(species,intrinsic);Trait(HaetaeRank3Rules.JudgmentBellow);return skill;
    }

    [TestCase("haeta",280f)] [TestCase("yeomhae",280f)]
    [TestCase("geumgangsu",385f)] [TestCase("mukryeong",280f)]
    public void HaetaeBellowContactsTravelNearToFarInsideLockedConeOnly(string species,float damage)
    {
        var skill=BellowSetup(species);object near=Unit(1,Vector3.right),far=Unit(1,Vector3.right*6.5f);
        object behind=Unit(1,Vector3.left),side=Unit(1,new Vector3(2f,0,4f));
        object outside=Unit(1,Vector3.right*7.15f);
        Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        RoarActionTick(.5f);Assert.That(Hp(near),Is.EqualTo(5000f));
        RoarActionTick(.05f);Assert.That(5000f-Hp(near),Is.EqualTo(damage).Within(.01f));
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(far),Is.EqualTo(5000f));
        RoarActionTick(.25f);RoarActionTick(.2f);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(5000f-Hp(far),Is.EqualTo(damage).Within(.01f));
        Assert.That(Hp(behind),Is.EqualTo(5000f));Assert.That(Hp(side),Is.EqualTo(5000f));Assert.That(Hp(outside),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));Assert.That(Get(source,"haetaeRoar"),Is.Null);
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(10f));
        Assert.That(Get(target,"familyAttackReductionTimer"),Is.EqualTo(0f),"The old base-roar attack reduction must not be added.");
    }

    [Test]
    public void HaetaeBellowPushIsContinuousFastThenBrakesAndKnocksDownOnlyAtEnd()
    {
        var skill=BellowSetup();Model(target).transform.position=Vector3.right*2f;
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.8f);
        var status=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(Get(target,"displacing"),Is.True);Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(Model(target).transform.position.x,Is.EqualTo(2f),"Receiving the wave must not teleport a body.");
        GriffinCall("UpdateDisplacement",target,.19f);
        Assert.That(Model(target).transform.position.x,Is.EqualTo(2f+3.4f*.875f).Within(.01f));
        Assert.That(Model(target).transform.position.y,Is.EqualTo(0f));Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False);
        GriffinCall("UpdateDisplacement",target,.19f);
        Assert.That(Model(target).transform.position.x,Is.EqualTo(5.4f).Within(.01f));
        Assert.That(Get(target,"displacing"),Is.False);Assert.That(Get(target,"haetaeBellowPushedBy"),Is.Null);
        Assert.That(status.Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(.6f).Within(.001f));
    }

    [Test]
    public void HaetaeBellowKeepsFacingWhenTargetMovesAndSupportsOnlyThreeTiles()
    {
        var skill=BellowSetup("haeta",HaetaeCombatRules.HaetaTraitId);
        object ally=Unit(0,Vector3.left*5f),outside=Unit(0,Vector3.left*6f);
        Set(ally,"hp",4000f);Set(outside,"hp",4000f);
        var status=(CombatStatusState)Get(ally,"combatStatuses");status.ApplyTimed(CombatStatusKind.Poison,5f,target);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);Model(target).transform.position=Vector3.forward*4f;
        object newTarget=Unit(1,Vector3.right*4f);
        RoarActionTick(.8f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(newTarget),Is.EqualTo(4720f));
        Assert.That(Hp(ally),Is.EqualTo(4060f));Assert.That(status.HarmfulCount,Is.Zero);Assert.That(Hp(outside),Is.EqualTo(4000f));
        RoarActionTick(.5f);Assert.That(Hp(ally),Is.EqualTo(4060f));
    }

    [Test]
    public void HaetaeBellowControlImmunityStillTakesDamageAndOtherDisplacementCancelsPendingFall()
    {
        var skill=BellowSetup();var status=(CombatStatusState)Get(target,"combatStatuses");status.ControlImmune=true;
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.8f);
        Assert.That(Hp(target),Is.EqualTo(4720f));Assert.That(Get(target,"displacing"),Is.False);
        Assert.That(Get(target,"haetaeBellowPushedBy"),Is.Null);Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False);
        GriffinCall("ClearHaetaeRoars");status.ControlImmune=false;
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.8f);
        GriffinCall("DisplaceUnit",target,Vector3.forward,1f,true,false,false,false);
        GriffinCall("UpdateDisplacement",target,1f);
        Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False,"An unrelated push must not inherit the bellow's delayed knockdown.");
    }

    [Test]
    public void HaetaeBellowPauseCancellationAndFourTileSkillApproach()
    {
        BellowSetup();Model(target).transform.position=Vector3.right*6.5f;
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.True,"Use the redesigned four-tile range, not the base 2.5 tiles.");
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);
        RoarActionTick(1f);Assert.That(Hp(target),Is.EqualTo(5000f));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);
        Set(source,"positionRevision",1);RoarActionTick(1f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Get(source,"haetaeRoar"),Is.Null);
    }

    [Test]
    public void HaetaeBellowParticlesUseCombatClockAndCleanUpWithRoarAudioAvailable()
    {
        Assert.That(Resources.Load<AudioClip>("Audio/Combat/Haetae/bellow"),Is.Not.Null);
        var fx=PixelHaetaeBellow.Create(null,Vector3.zero,Vector3.right,6.8f,0);
        fx.Pose(.5f);var ps=fx.transform.Find("Bellow Dense Air").GetComponent<ParticleSystem>();
        var first=new ParticleSystem.Particle[224];var second=new ParticleSystem.Particle[224];
        fx.Pose(.65f);int count=ps.GetParticles(first);fx.Pose(.65f);Assert.That(ps.GetParticles(second),Is.EqualTo(count));
        for(int i=0;i<count;i++)Assert.That(second[i].position,Is.EqualTo(first[i].position));
        fx.Pose(.75f);ps.GetParticles(second);Assert.That(second[0].position.x,Is.GreaterThan(first[0].position.x));
        fx.Cancel();Assert.That(fx==null,Is.True);
    }
}
