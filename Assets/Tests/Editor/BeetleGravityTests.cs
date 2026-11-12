using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void StartBeetleGravity(string species="jangpung")
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(source,"skillId",BeetleCombatRules.SkillId);Set(source,"defense",68f);
        Trait(BeetleRank3Rules.ZeroGravity);
        Assert.That(MammothCall("TryResolveBeetleRank3",source,target,
            GameMasterData.Skill(BeetleCombatRules.SkillId),1f,1f,0),Is.True);
    }

    [TestCase("jangpung",180f)] [TestCase("noepung",180f)]
    [TestCase("atlagon",48.96f)] [TestCase("solarbeetle",180f)]
    public void BeetleGravityCapturesInitialAreaAndOnlyDamagesOnceOnLanding(string species,float damage)
    {
        var near=Unit(1,Vector3.right*5);var far=Unit(1,Vector3.right*9);var ally=Unit(0,Vector3.right*5);
        StartBeetleGravity(species);var late=Unit(1,Vector3.right*5);
        Assert.That(Get(source,"beetleFlip"),Is.Null);Assert.That(Get(source,"beetleGravityHeld"),Is.Null);
        Assert.That(Get(target,"beetleGravityHeld"),Is.Not.Null);Assert.That(Get(near,"beetleGravityHeld"),Is.Not.Null);
        Assert.That(Get(far,"beetleGravityHeld"),Is.Null);Assert.That(Get(ally,"beetleGravityHeld"),Is.Null);
        MammothCall("UpdateBeetleGravityFields",BeetleRank3Rules.GravityLanding-.01f);
        Assert.That(Hp(target),Is.EqualTo(5000));Assert.That(Hp(near),Is.EqualTo(5000));
        MammothCall("UpdateBeetleGravityFields",.02f);
        Assert.That(Hp(target),Is.EqualTo(5000-damage).Within(.01f));Assert.That(Hp(near),Is.EqualTo(5000-damage).Within(.01f));
        Assert.That(Hp(far),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));Assert.That(Hp(late),Is.EqualTo(5000));
        Assert.That(Get(target,"beetleGravityHeld"),Is.Null);
        var status=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(status.Has(CombatStatusKind.Airborne),Is.False);Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False);
        MammothCall("UpdateBeetleGravityFields",1f);Assert.That(Hp(target),Is.EqualTo(5000-damage).Within(.01f));
    }

    [Test]
    public void BeetleGravityKeepsGroundPositionAndReleasesPoseOnForcedMovement()
    {
        var billboard=Model(target).AddComponent<SpriteMonsterBillboard>();Set(target,"spriteBillboard",billboard);
        Vector3 ground=Model(target).transform.position;StartBeetleGravity();
        MammothCall("UpdateBeetleGravityFields",.5f);
        var offset=typeof(SpriteMonsterBillboard).GetField("rollOffset",Private);
        Assert.That(((Vector3)offset.GetValue(billboard)).y,Is.GreaterThan(1.5f));
        Assert.That(Model(target).transform.position,Is.EqualTo(ground));
        MammothCall("InterruptPositionalAction",target);
        Assert.That(Get(target,"beetleGravityHeld"),Is.Null);Assert.That(offset.GetValue(billboard),Is.EqualTo(Vector3.zero));
        MammothCall("UpdateBeetleGravityFields",2f);Assert.That(Hp(target),Is.EqualTo(5000));
    }

    [Test]
    public void BeetleGravityImmuneVictimTakesDamageAndCleanseReleasesTheOtherVictim()
    {
        var immune=(CombatStatusState)Get(target,"combatStatuses");immune.ControlImmune=true;
        var other=Unit(1,Vector3.right*5);StartBeetleGravity();
        Assert.That(Get(target,"beetleGravityHeld"),Is.Null);
        ((CombatStatusState)Get(other,"combatStatuses")).Remove(CombatStatusKind.Airborne);
        MammothCall("UpdateBeetleGravityFields",.1f);Assert.That(Get(other,"beetleGravityHeld"),Is.Null);
        MammothCall("UpdateBeetleGravityFields",2f);
        Assert.That(Hp(target),Is.EqualTo(4820));Assert.That(immune.Has(CombatStatusKind.Knockdown),Is.False);
    }

    [Test]
    public void BeetleGravityCasterDeathKeepsReleasedFieldUntilLanding()
    {
        var other=Unit(1,Vector3.right*5);StartBeetleGravity();
        MammothCall("UpdateBeetleGravityFields",.5f);Set(source,"alive",false);
        MammothCall("CleanupRank3Unit",source);
        Assert.That(Get(target,"beetleGravityHeld"),Is.Not.Null);Assert.That(Get(other,"beetleGravityHeld"),Is.Not.Null);
        MammothCall("UpdateBeetleGravityFields",2f);Assert.That(Hp(target),Is.LessThan(5000));
        Assert.That(Get(target,"beetleGravityHeld"),Is.Null);Assert.That(Get(other,"beetleGravityHeld"),Is.Null);
    }

    [Test]
    public void BeetleGravitySecondFieldCannotStealHeldPose()
    {
        StartBeetleGravity();object owner=Get(target,"beetleGravityHeld");
        var second=Unit(0,Vector3.right);((IList)Get(second,"activeTraitIds")).Add(BeetleRank3Rules.ZeroGravity);
        MammothCall("TryResolveBeetleRank3",second,target,GameMasterData.Skill(BeetleCombatRules.SkillId),1f,1f,0);
        Assert.That(Get(target,"beetleGravityHeld"),Is.SameAs(owner));
        MammothCall("CleanupRank3Unit",second);Assert.That(Get(target,"beetleGravityHeld"),Is.SameAs(owner));
    }
}
