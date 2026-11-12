using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void RoarActionTick(float seconds)=>GriffinCall("UpdateHaetaeRoars",CombatActionSeconds(seconds));
    private SkillMasterRow HaetaeSetup(string species="haeta",string intrinsic=HaetaeCombatRules.HaetaTraitId)
    {
        GriffinSetup(species);Set(source,"skillId",HaetaeCombatRules.SkillId);
        Set(source,"skillCooldownDuration",10f);if(intrinsic!=null)Trait(intrinsic);
        return GameMasterData.Skill(HaetaeCombatRules.SkillId);
    }

    [TestCase("haeta",120f)] [TestCase("yeomhae",120f)]
    [TestCase("geumgangsu",165f)] [TestCase("mukryeong",120f)]
    public void HaetaeWaveHitsOnceAtContactUsingSpeciesPower(string species,float damage)
    {
        var skill=HaetaeSetup(species,null);object near=Unit(1,Vector3.right),outside=Unit(1,Vector3.right*4.6f);
        Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        RoarActionTick(.35f);Assert.That(Hp(near),Is.EqualTo(5000f));
        RoarActionTick(.04f);Assert.That(5000f-Hp(near),Is.EqualTo(damage).Within(.01f));
        Assert.That(Hp(target),Is.EqualTo(5000f));
        RoarActionTick(.21f);RoarActionTick(.2f);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(5000f-Hp(near),Is.EqualTo(damage).Within(.01f));Assert.That(Hp(outside),Is.EqualTo(5000f));
        Assert.That(Get(source,"haetaeRoar"),Is.Null);Assert.That(Get(source,"skillTimer"),Is.EqualTo(10f));
        Assert.That(Get(target,"familyAttackReductionRatio"),Is.EqualTo(.15f));
    }

    [Test]
    public void HaetaeCanHealWithoutEnemyOrDebuffAndDoesNotRecastForHealthyAllies()
    {
        HaetaeSetup();Set(source,"target",null);Model(target).transform.position=Vector3.right*20f;
        object ally=Unit(0,Vector3.right);Set(ally,"hp",4000f);
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.True);
        RoarActionTick(.6f);
        Assert.That(Hp(ally),Is.EqualTo(4060f).Within(.01f));
        Set(source,"skillTimer",0f);Assert.That(GriffinCall("TryUseSkill",source,false),Is.False);
    }

    [TestCase("cooldown")] [TestCase("rank3_lock")]
    [TestCase("silence")] [TestCase("fear")]
    public void HaetaeSupportDoesNotBypassSkillGuards(string guard)
    {
        HaetaeSetup();Set(source,"target",null);Model(target).transform.position=Vector3.right*20f;
        object ally=Unit(0,Vector3.right);Set(ally,"hp",4000f);Set(ally,"rank3ActionSlowTimer",3f);
        if(guard=="cooldown")Set(source,"skillTimer",2f);
        else if(guard=="rank3_lock")Set(source,"rank3SkillLockTimer",2f);
        else ((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(
            guard=="silence"?CombatStatusKind.Silence:CombatStatusKind.Fear,2f);
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.False);
        Assert.That(Get(source,"haetaeRoar"),Is.Null);Assert.That(Get(source,"skillUses"),Is.EqualTo(0));
        Assert.That(Hp(ally),Is.EqualTo(4000f));Assert.That(Get(ally,"rank3ActionSlowTimer"),Is.EqualTo(3f));
    }

    [Test]
    public void CombinedHaetaeCastKeepsSilenceAndInterruptionOrderWhileBypassingCooldown()
    {
        HaetaeSetup();Set(source,"target",null);Model(target).transform.position=Vector3.right*20f;
        object ally=Unit(0,Vector3.right);Set(ally,"hp",4000f);Set(source,"skillTimer",2f);
        var driver=Model(source).AddComponent<SpriteMonsterAnimationDriver>();Set(source,"spriteAnimation",driver);
        var oneShot=typeof(SpriteMonsterAnimationDriver).GetField("oneShot",Private);
        oneShot.SetValue(driver,true);
        var status=(CombatStatusState)Get(source,"combatStatuses");status.ApplyTimed(CombatStatusKind.Silence,2f);
        Assert.That(GriffinCall("TryUseSkill",source,true),Is.False);
        Assert.That(oneShot.GetValue(driver),Is.True,"Silence must return before interrupting the current action.");
        status.Remove(CombatStatusKind.Silence);Set(source,"rank3SkillLockTimer",2f);
        Assert.That(GriffinCall("TryUseSkill",source,true),Is.False);
        Assert.That(oneShot.GetValue(driver),Is.False,"The existing combined-cast interruption still precedes the rank3 lock check.");
        Set(source,"rank3SkillLockTimer",0f);
        Assert.That(GriffinCall("TryUseSkill",source,true),Is.True);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillTimer"),Is.EqualTo(10f));
    }

    [Test]
    public void HaetaeWithEnemyCanCastWithoutSupportTargets()
    {
        var skill=HaetaeSetup();
        Assert.That(GriffinCall("HaetaeSupportTargets",source,skill),Is.EqualTo(0));
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.True);
        Assert.That(Get(source,"haetaeRoar"),Is.Not.Null);
    }

    [Test]
    public void HaetaeCommittedRoarFinishesAfterItsTargetLeavesWithoutExpandingTheWave()
    {
        var skill=HaetaeSetup();Model(target).transform.position=Vector3.right*20f;
        Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.False);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0));
        var requestType=typeof(MonsterAutoBattlePrototype).GetNestedType("PendingSkillFocus",System.Reflection.BindingFlags.NonPublic);
        object request=System.Activator.CreateInstance(requestType,true);
        requestType.GetField("unit").SetValue(request,source);requestType.GetField("castTarget").SetValue(request,target);
        var context=typeof(MonsterAutoBattlePrototype).GetField("committingSkill",Private);
        object previous=context.GetValue(battle);
        try
        {
            context.SetValue(battle,request);
            Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        }
        finally{context.SetValue(battle,previous);}
        Assert.That(Get(source,"haetaeRoar"),Is.Not.Null);Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        RoarActionTick(1f);
        Assert.That(Get(source,"haetaeRoar"),Is.Null);
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(10f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(0));Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [Test]
    public void HaetaeSupportQueryDoesNotCleanseAndWavePreservesBeneficialShield()
    {
        var skill=HaetaeSetup("geumgangsu",HaetaeCombatRules.GeumgangsuTraitId);
        Set(source,"target",null);Model(target).transform.position=Vector3.right*20f;
        object ally=Unit(0,Vector3.right);Set(ally,"maxHp",1000f);Set(ally,"hp",1000f);
        var status=(CombatStatusState)Get(ally,"combatStatuses");status.ApplyStacks(CombatStatusKind.Poison,4f,1,5,target,10f,1f);
        Set(ally,"familyAttackReductionTimer",4f);Set(ally,"familyAttackReductionRatio",.15f);
        Set(ally,"rank3ActionSlowTimer",3f);Set(ally,"rank3ActionSlowRatio",.25f);Set(ally,"rank3SkillLockTimer",1f);
        object dead=Unit(0,Vector3.right),clone=Unit(0,Vector3.right),far=Unit(0,Vector3.right*20f);
        Set(dead,"alive",false);Set(clone,"augmentShadowClone",true);
        foreach(var excluded in new[]{dead,clone,far,target})Set(excluded,"rank3ActionSlowTimer",3f);
        Assert.That(GriffinCall("HaetaeSupportTargets",source,null),Is.EqualTo(0));
        var otherSkill=GameMasterData.Skill(GriffinCombatRules.SkillId);
        Assert.That(GriffinCall("HaetaeSupportTargets",source,otherSkill),Is.EqualTo(0));
        Set(source,"skillId",GriffinCombatRules.SkillId);
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.False,"Non-Haetae skills cannot use ally support to replace a missing enemy.");
        Set(source,"skillId",HaetaeCombatRules.SkillId);
        Assert.That(GriffinCall("HaetaeSupportTargets",source,skill),Is.EqualTo(1));
        foreach(var excluded in new[]{dead,clone,far,target})
            Assert.That(Get(excluded,"rank3ActionSlowTimer"),Is.EqualTo(3f));
        Assert.That(status.Has(CombatStatusKind.Poison),Is.True);Assert.That(Get(ally,"familyAttackReductionTimer"),Is.EqualTo(4f));
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.True);RoarActionTick(.6f);
        Assert.That(status.HarmfulCount,Is.Zero);Assert.That(Get(ally,"familyAttackReductionTimer"),Is.EqualTo(0f));
        Assert.That(Get(ally,"rank3ActionSlowTimer"),Is.EqualTo(0f));Assert.That(Get(ally,"rank3SkillLockTimer"),Is.EqualTo(0f));
        Assert.That(status.Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(400f).Within(.01f));
        GriffinCall("CleanseAllHarmfulEffects",ally);Assert.That(status.Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(400f));
    }

    [Test]
    public void HaetaeFlameAndKarmaApplyExactDurationAndDoNotMultiplyRecoveryPenalties()
    {
        var skill=HaetaeSetup("yeomhae",HaetaeCombatRules.YeomhaeTraitId);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        var status=(CombatStatusState)Get(target,"combatStatuses");var burn=status.Get(CombatStatusKind.Burning);
        Assert.That(burn.magnitude,Is.EqualTo(25f));Assert.That(burn.remaining,Is.EqualTo(4f));Assert.That(burn.tickInterval,Is.EqualTo(1f));
        ((System.Collections.IList)Get(source,"activeTraitIds")).Clear();Trait(HaetaeCombatRules.MukryeongTraitId);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        float before=Hp(target);GriffinCall("Heal",target,100f);
        Assert.That(Hp(target)-before,Is.EqualTo(60f).Within(.01f));
        status.ApplyShield(5f,100f,source);Assert.That(status.Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(75f));
    }

    [Test]
    public void HaetaeWavePausesAndDisplacementCancelsUnreleasedCast()
    {
        var skill=HaetaeSetup();GriffinCall("BeginHaetaeJusticeRoar",source,skill);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);
        RoarActionTick(1f);Assert.That(Hp(target),Is.EqualTo(5000f));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);
        Set(source,"positionRevision",1);RoarActionTick(1f);
        Assert.That(Get(source,"haetaeRoar"),Is.Null);Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [Test]
    public void HaetaeEchoUsesReducedPowerWithoutRepeatingIntrinsicHeal()
    {
        var skill=HaetaeSetup();Set(source,"hp",4000f);
        int generation=(int)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        GriffinCall("ResolveHaetaeJusticeRoar",source,skill,.7f,true,generation);RoarActionTick(.25f);
        Assert.That(5000f-Hp(target),Is.EqualTo(84f).Within(.01f));Assert.That(Hp(source),Is.EqualTo(4000f));
    }
}
