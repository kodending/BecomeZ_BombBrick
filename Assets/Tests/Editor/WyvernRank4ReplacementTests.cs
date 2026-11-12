using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private IList LowerRankWyvernCasts=>(IList)typeof(MonsterAutoBattlePrototype)
        .GetField("wyvernBreaths",Private).GetValue(battle);

    private void Rank4WyvernSetup(string species="vivre",int team=0)
    {
        GriffinSetup(species);Set(source,"skillId",WyvernCombatRules.SkillId);
        Set(source,"skillCooldownDuration",8.5f);Set(source,"augmentTranscended",true);
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
    }

    [TestCase("vivre",0)] [TestCase("vivre",1)]
    [TestCase("igvire",0)] [TestCase("igvire",1)]
    [TestCase("frovire",0)] [TestCase("frovire",1)]
    [TestCase("temvire",0)] [TestCase("temvire",1)]
    public void Rank4WyvernReplacesEveryLowerRankBranchAndHitsTheWholeArenaOnce(string species,int team)
    {
        Rank4WyvernSetup(species,team);
        Trait(WyvernRank3Rules.CataclysmBreath);Trait(WyvernRank3Rules.WingSweep);Trait(WyvernRank3Rules.DragonCore);
        Trait(WyvernCombatRules.VivreTraitId);Trait(WyvernCombatRules.IgvireTraitId);
        Trait(WyvernCombatRules.FrovireTraitId);Trait(WyvernCombatRules.TemvireTraitId);
        Model(target).transform.position=new Vector3(18f,0f,-18f);
        var skill=GameMasterData.Skill(WyvernCombatRules.SkillId);
        var decision=(SkillUseSnapshot)GriffinCall("BuildSkillSnapshot",source);
        Assert.That(decision.enemiesInEffect,Is.EqualTo(1));
        Assert.That(SkillUseDecision.ShouldUse(decision),Is.True);
        Assert.That(GriffinCall("FocusedSkillReady",source,target,false),Is.True);
        Assert.That(float.IsPositiveInfinity((float)GriffinCall("SkillActivationDistance",source,target,skill)),Is.True);
        Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.True);
        Assert.That(HeavenBreaths.Count,Is.EqualTo(1));Assert.That(LowerRankWyvernCasts,Is.Empty);
        Assert.That(Get(source,"wyvernBreath"),Is.Null);Assert.That(Get(source,"wyvernBreathPending"),Is.False);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));

        object ally=Unit(team,Vector3.left*3f),outside=Unit(1-team,Vector3.right*25f);
        object stored=Unit(1-team,Vector3.right*2f);Set(stored,"inStorage",true);
        HeavenTick(WyvernHeavenBreathRules.ImpactTime-.001f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.EqualTo(0));
        HeavenTick(.002f);
        Assert.That(Hp(target),Is.EqualTo(4700f).Within(.001f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Hp(outside),Is.EqualTo(5000f));Assert.That(Hp(stored),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Count,Is.Zero,
            "Rank-4 replaces poison, fire fields, chill/freeze and lightning-chain attacks from the lower-rank breath.");
        HeavenTick(.15f);Assert.That(Hp(target),Is.EqualTo(4700f).Within(.001f));
        HeavenTick(1f);
        Assert.That(HeavenBreaths,Is.Empty);Assert.That(LowerRankWyvernCasts,Is.Empty);
        Assert.That(Hp(target),Is.EqualTo(4700f).Within(.001f));
    }

    [TestCase(0)] [TestCase(1)]
    public void Rank4WyvernRecastsOnItsNextCooldownWithoutStackingAnActiveCast(int team)
    {
        Rank4WyvernSetup("vivre",team);var skill=GameMasterData.Skill(WyvernCombatRules.SkillId);
        Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.True);
        Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.False);
        Assert.That(HeavenBreaths.Count,Is.EqualTo(1));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));
        HeavenTick(WyvernHeavenBreathRules.EndTime+.01f);Assert.That(HeavenBreaths,Is.Empty);
        Assert.That(Hp(target),Is.EqualTo(4700f).Within(.001f));

        Set(source,"skillTimer",0f);
        Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.True);
        Assert.That(HeavenBreaths.Count,Is.EqualTo(1));Assert.That(Get(source,"skillUses"),Is.EqualTo(2));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));Assert.That(LowerRankWyvernCasts,Is.Empty);
        HeavenTick(WyvernHeavenBreathRules.ImpactTime);
        Assert.That(Hp(target),Is.EqualTo(4400f).Within(.001f));Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
    }

    [TestCase("vivre")] [TestCase("igvire")] [TestCase("frovire")] [TestCase("temvire")]
    public void Rank4WyvernCannotReintroduceLowerRankEffectsThroughTheDirectOrEchoEntry(string species)
    {
        Rank4WyvernSetup(species);var skill=GameMasterData.Skill(WyvernCombatRules.SkillId);
        Assert.That(GriffinCall("StartWyvernBreath",source,target,skill,.5f,true,0),Is.False);
        Assert.That(LowerRankWyvernCasts,Is.Empty);Assert.That(HeavenBreaths,Is.Empty);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0));Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(GriffinCall("StartWyvernBreath",source,target,skill,1f,false,0),Is.True);
        Assert.That(LowerRankWyvernCasts,Is.Empty);Assert.That(HeavenBreaths.Count,Is.EqualTo(1));
        Assert.That(Get(source,"wyvernBreath"),Is.Null);
    }

    [TestCase("death")] [TestCase("knockdown")] [TestCase("freeze")]
    public void AReleasedRank4WyvernContinuesAfterItsCasterDiesOrLosesControl(string interruption)
    {
        Rank4WyvernSetup();Assert.That(GriffinCall("BeginWyvernBreath",source,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.True);
        HeavenTick(.2f);
        if(interruption=="death"){Set(source,"alive",false);Set(source,"hp",0f);}
        else ((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(
            interruption=="freeze"?CombatStatusKind.Freeze:CombatStatusKind.Knockdown,10f,target);
        HeavenTick(WyvernHeavenBreathRules.ImpactTime-.2f+.001f);
        Assert.That(Hp(target),Is.EqualTo(4700f).Within(.001f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        HeavenTick(1f);Assert.That(HeavenBreaths,Is.Empty);Assert.That(LowerRankWyvernCasts,Is.Empty);
    }

    [Test]
    public void Rank4WyvernDoesNotSpendItsCooldownOrCreateEffectsWhenThereAreNoEnemies()
    {
        Rank4WyvernSetup();Set(target,"alive",false);Set(target,"hp",0f);
        Assert.That(SkillUseDecision.ShouldUse((SkillUseSnapshot)GriffinCall("BuildSkillSnapshot",source)),Is.False);
        Assert.That(GriffinCall("BeginWyvernBreath",source,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.False);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0));Assert.That(Get(source,"skillTimer"),Is.EqualTo(0f));
        Assert.That(HeavenBreaths,Is.Empty);Assert.That(LowerRankWyvernCasts,Is.Empty);
    }

    [TestCase(null,1f,200f)]
    [TestCase(WyvernRank3Rules.CataclysmBreath,.7f,300f)]
    [TestCase(WyvernRank3Rules.WingSweep,2.46f,420f)]
    [TestCase(WyvernRank3Rules.DragonCore,2.41f,360f)]
    public void NonTranscendedWyvernsKeepTheirExistingBreathInsteadOfTheRank4Replacement(string trait,float duration,float damage)
    {
        GriffinSetup("vivre");Set(source,"skillId",WyvernCombatRules.SkillId);Set(source,"skillCooldownDuration",8.5f);
        if(trait!=null)Trait(trait);
        Assert.That(GriffinCall("BeginWyvernBreath",source,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.True);
        Assert.That(Get(source,"wyvernBreath"),Is.Not.Null);Assert.That(LowerRankWyvernCasts.Count,Is.EqualTo(1));
        Assert.That(HeavenBreaths,Is.Empty);BreathActionTick(duration+.01f);
        Assert.That(Hp(target),Is.EqualTo(5000f-damage).Within(.01f));
        Assert.That(HeavenBreaths,Is.Empty);
    }
}
