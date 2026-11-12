using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private SkillMasterRow OppressionSetup(string species="haeta",string intrinsic=null)
    {
        var skill=HaetaeSetup(species,intrinsic);Trait(HaetaeRank3Rules.DivineOppression);return skill;
    }

    [TestCase("haeta",250f)] [TestCase("yeomhae",250f)]
    [TestCase("geumgangsu",343.75f)] [TestCase("mukryeong",250f)]
    public void HaetaeOppressionFiveContactPulsesUseSpeciesPowerAndReleaseCaster(string species,float damage)
    {
        var skill=OppressionSetup(species);object outside=Unit(1,Vector3.right*(MonsterAutoBattlePrototype.TileSize*3f+.31f));
        Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        RoarActionTick(.35f);Assert.That(Hp(target),Is.EqualTo(5000f));
        // The released aura uses world seconds, including its first expanding front and one-second pulses.
        GriffinCall("UpdateHaetaeRoars",.25f);Assert.That(5000f-Hp(target),Is.EqualTo(damage/5f).Within(.01f));
        Assert.That(Get(source,"haetaeRoar"),Is.Null,"Movement and basic attacks must resume after the first front.");
        for(int i=0;i<4;i++)GriffinCall("UpdateHaetaeRoars",1f);
        GriffinCall("UpdateHaetaeRoars",.75f);GriffinCall("UpdateHaetaeRoars",2f);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));Assert.That(Hp(outside),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));Assert.That(Get(source,"skillTimer"),Is.EqualTo(10f));
        Assert.That((IList)typeof(MonsterAutoBattlePrototype).GetField("haetaeRoars",Private).GetValue(battle),Is.Empty);
    }

    [Test]
    public void HaetaeOppressionFollowsCasterAndRangeEffectsRespectExitImmunityAndOtherBuffs()
    {
        var skill=OppressionSetup();object ally=Unit(0,Vector3.right);Set(target,"speedTilesPerSecond",1f);
        float baseMove=(float)GriffinCall("CurrentMovementSpeed",target),baseAttack=(float)GriffinCall("CurrentAttackInterval",target);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        Assert.That((float)GriffinCall("CurrentMovementSpeed",target),Is.EqualTo(baseMove*.75f).Within(.001f));
        Assert.That((float)GriffinCall("CurrentAttackInterval",target),Is.EqualTo(baseAttack/.75f).Within(.001f));
        var status=(CombatStatusState)Get(target,"combatStatuses");status.DebuffImmune=true;
        Assert.That(GriffinCall("CurrentMovementSpeed",target),Is.EqualTo(baseMove));status.DebuffImmune=false;
        float before=Hp(ally);GriffinCall("Rank3Hit",target,ally,1f,0f,0f);
        Assert.That(before-Hp(ally),Is.EqualTo(80f).Within(.01f));
        Set(ally,"familyDamageReductionTimer",5f);Set(ally,"familyDamageReductionRatio",.35f);
        before=Hp(ally);GriffinCall("Rank3Hit",target,ally,1f,0f,0f);
        Assert.That(before-Hp(ally),Is.EqualTo(65f).Within(.01f),"Use the stronger reduction, not another multiplicative layer.");
        Model(source).transform.position=Vector3.left*10f;
        Assert.That(GriffinCall("HaetaeOppressionAffects",ally,true),Is.False);
        Assert.That(GriffinCall("CurrentMovementSpeed",target),Is.EqualTo(baseMove));
        Assert.That(Get(ally,"familyDamageReductionRatio"),Is.EqualTo(.35f));
        object entrant=Unit(1,Vector3.left*9f);GriffinCall("UpdateHaetaeRoars",1f);
        Assert.That(Hp(entrant),Is.EqualTo(4950f));Assert.That(Hp(target),Is.EqualTo(4950f));
        Assert.That(GriffinCall("HaetaeOppressionAffects",entrant,false),Is.True);
    }

    [Test]
    public void HaetaeOppressionCleansesAndHealsOnlyAtCreationNotOnTicksOrEntry()
    {
        var skill=OppressionSetup("haeta",HaetaeCombatRules.HaetaTraitId);
        object ally=Unit(0,Vector3.right),late=Unit(0,Vector3.left*8f);Set(ally,"hp",4000f);Set(late,"hp",4000f);
        var status=(CombatStatusState)Get(ally,"combatStatuses");status.ApplyTimed(CombatStatusKind.Poison,5f,target);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        Assert.That(Hp(ally),Is.EqualTo(4060f));Assert.That(status.HarmfulCount,Is.Zero);
        status.ApplyTimed(CombatStatusKind.Poison,5f,target);Model(late).transform.position=Vector3.right;
        GriffinCall("UpdateHaetaeRoars",3f);
        Assert.That(Hp(ally),Is.EqualTo(4060f));Assert.That(status.Has(CombatStatusKind.Poison),Is.True);
        Assert.That(Hp(late),Is.EqualTo(4000f));Assert.That(GriffinCall("HaetaeOppressionAffects",late,true),Is.True);
    }

    [Test]
    public void HaetaeOppressionShieldUsesCasterHpOnceAndEnemyIntrinsicsRefresh()
    {
        var skill=OppressionSetup("geumgangsu",HaetaeCombatRules.GeumgangsuTraitId);
        object ally=Unit(0,Vector3.right);Set(ally,"maxHp",1000f);Set(ally,"hp",1000f);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        var status=(CombatStatusState)Get(ally,"combatStatuses");Assert.That(status.Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(400f));
        status.Clear();GriffinCall("UpdateHaetaeRoars",1f);Assert.That(status.Has(CombatStatusKind.Shield),Is.False);
        GriffinCall("ClearHaetaeRoars");((IList)Get(source,"activeTraitIds")).Clear();
        skill=OppressionSetup("mukryeong",HaetaeCombatRules.MukryeongTraitId);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        Assert.That(Get(target,"familyHealingReductionRatio"),Is.EqualTo(.4f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).shieldGainMultiplier,Is.EqualTo(.75f));
        Set(target,"familyHealingReductionTimer",.1f);GriffinCall("UpdateHaetaeRoars",1f);
        Assert.That(Get(target,"familyHealingReductionTimer"),Is.EqualTo(4f));
    }

    [Test]
    public void HaetaeOppressionPauseDeathRecastAndLargeFramesCannotLeaveOrDuplicateAura()
    {
        var skill=OppressionSetup();GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);
        GriffinCall("UpdateHaetaeRoars",10f);Assert.That(Hp(target),Is.EqualTo(4950f));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        Assert.That(Hp(target),Is.EqualTo(4900f));
        GriffinCall("UpdateHaetaeRoars",5f);Assert.That(Hp(target),Is.EqualTo(4700f));
        Assert.That(GriffinCall("HaetaeOppressionAffects",source,true),Is.False);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.6f);
        Set(source,"alive",false);Assert.That(GriffinCall("HaetaeOppressionAffects",target,false),Is.True);
        float before=Hp(target);GriffinCall("UpdateHaetaeRoars",10f);Assert.That(Hp(target),Is.EqualTo(before-200f));
        Assert.That(GriffinCall("HaetaeOppressionAffects",target,false),Is.False);
    }
}
