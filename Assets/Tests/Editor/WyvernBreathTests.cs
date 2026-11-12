using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void WyvernSetup(string id="vivre",string intrinsic=null)
    {
        GriffinSetup(id);Set(source,"skillId",WyvernCombatRules.SkillId);
        Set(source,"skillCooldownDuration",8.5f);
        if(intrinsic!=null)Trait(intrinsic);
        Assert.That(GriffinCall("BeginWyvernBreath",source,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.True);
    }
    private void BreathTick(float dt)=>GriffinCall("UpdateWyvernBreaths",dt);
    private void BreathActionTick(float seconds)=>BreathTick(CombatActionSeconds(seconds));

    [Test]
    public void WyvernBreathWaitsForItsFrontThenDealsFourHitsAndLocksDirection()
    {
        WyvernSetup();object second=Unit(1,new Vector3(6,0,.4f));
        BreathActionTick(.35f);Assert.That(Hp(target),Is.EqualTo(5000));
        BreathActionTick(.15f);Assert.That(Hp(target),Is.EqualTo(5000));
        BreathActionTick(.01f);Assert.That(Hp(target),Is.EqualTo(4950));
        Assert.That(Get(source,"wyvernBreath"),Is.Not.Null);
        Model(target).transform.position=new Vector3(0,0,5);
        Set(source,"target",Unit(1,new Vector3(0,0,6)));
        BreathActionTick(.49f);
        Assert.That(Hp(target),Is.EqualTo(4950));Assert.That(Hp(second),Is.EqualTo(4800));
        Assert.That(Get(source,"wyvernBreath"),Is.Null);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));
    }

    [Test]
    public void WyvernBreathRechecksVictimsAtEachTickAndContinuesAfterPrimaryDies()
    {
        WyvernSetup();object late=Unit(1,new Vector3(4,0,5));
        object side=Unit(1,new Vector3(4,0,2f));object behind=Unit(1,Vector3.left);
        BreathActionTick(.52f);Set(target,"alive",false);
        Model(late).transform.position=new Vector3(4,0,0);
        BreathActionTick(.48f);
        Assert.That(Hp(late),Is.EqualTo(4850));
        Assert.That(Hp(side),Is.EqualTo(5000));Assert.That(Hp(behind),Is.EqualTo(5000));
    }

    [TestCase("vivre",WyvernCombatRules.VivreTraitId,CombatStatusKind.Poison,1)]
    [TestCase("frovire",WyvernCombatRules.FrovireTraitId,CombatStatusKind.ComboChilled,2)]
    public void WyvernBreathAppliesSpeciesStacksOnlyOncePerVictim(string id,string intrinsic,CombatStatusKind status,int stacks)
    {
        WyvernSetup(id,intrinsic);BreathActionTick(1f);
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(Hp(target),Is.EqualTo(4800));Assert.That(statuses.Get(status).stacks,Is.EqualTo(stacks));
        Assert.That(statuses.Has(CombatStatusKind.Freeze),Is.False);
        Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
    }

    [Test]
    public void WyvernBreathChainsLightningOnlyOnFirstContact()
    {
        WyvernSetup("temvire",WyvernCombatRules.TemvireTraitId);
        object chain=Unit(1,new Vector3(4,0,4));BreathActionTick(1f);
        Assert.That(Hp(target),Is.EqualTo(4800));Assert.That(Hp(chain),Is.EqualTo(4950));
    }

    [Test]
    public void WyvernBreathFreezeAndPauseSuspendDamageThenResume()
    {
        WyvernSetup();var statuses=(CombatStatusState)Get(source,"combatStatuses");
        statuses.ApplyTimed(CombatStatusKind.Freeze,2f,target);BreathTick(1f);
        Assert.That(Hp(target),Is.EqualTo(5000));
        statuses.Remove(CombatStatusKind.Freeze);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);BreathTick(1f);
        Assert.That(Hp(target),Is.EqualTo(5000));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);BreathActionTick(1f);
        Assert.That(Hp(target),Is.EqualTo(4800));
    }

    [TestCase(CombatStatusKind.Knockdown)]
    [TestCase(CombatStatusKind.Fear)]
    [TestCase(CombatStatusKind.Airborne)]
    public void WyvernReleasedBreathContinuesThroughControl(CombatStatusKind status)
    {
        WyvernSetup();BreathActionTick(.52f);
        ((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(status,2f,target);
        BreathActionTick(.5f);Assert.That(Hp(target),Is.EqualTo(4800));Assert.That(Get(source,"wyvernBreath"),Is.Null);
    }

    [Test]
    public void WyvernBreathDisplacementDeathResetAndRecastCannotLeaveAChannelBehind()
    {
        WyvernSetup();Assert.That(GriffinCall("TryUseSkill",source,false),Is.False);
        GriffinCall("InterruptPositionalAction",source);BreathTick(1f);
        Assert.That(Hp(target),Is.EqualTo(5000));Assert.That(Get(source,"wyvernBreath"),Is.Null);
        WyvernSetup();Set(source,"alive",false);BreathTick(1f);
        Assert.That(Get(source,"wyvernBreath"),Is.Null);Set(source,"alive",true);
        WyvernSetup();GriffinCall("ClearWyvernBreaths");BreathTick(1f);
        Assert.That(Hp(target),Is.EqualTo(5000));Assert.That(Get(source,"wyvernBreath"),Is.Null);
    }

    [Test]
    public void WyvernBreathEchoUsesFourScaledHitsWithoutDuplicatingIntrinsicsOrReplacingCast()
    {
        WyvernSetup("vivre",WyvernCombatRules.VivreTraitId);object original=Get(source,"wyvernBreath");
        int generation=(int)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        GriffinCall("ResolveWyvernBreath",source,target,GameMasterData.Skill(WyvernCombatRules.SkillId),.5f,true,generation);
        Assert.That(Get(source,"wyvernBreath"),Is.SameAs(original));BreathActionTick(1f);
        Assert.That(Hp(target),Is.EqualTo(4700));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Get(CombatStatusKind.Poison).stacks,Is.EqualTo(1));
    }
}
