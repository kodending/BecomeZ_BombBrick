using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [TestCase("haeta",240f)] [TestCase("yeomhae",240f)]
    [TestCase("geumgangsu",330f)] [TestCase("mukryeong",240f)]
    public void HaetaeThunderWaitsForWarningThenUsesSpeciesPowerOnce(string species,float damage)
    {
        var skill=HaetaeSetup(species,null);Trait(HaetaeRank3Rules.HeavenlyJudgment);
        object overlap=Unit(1,Vector3.right*4.45f);
        Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        RoarActionTick(.7f);Assert.That(Get(source,"haetaeRoar"),Is.Null);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(overlap),Is.EqualTo(5000f));
        RoarActionTick(.18f);Assert.That(Hp(target),Is.EqualTo(5000f));
        RoarActionTick(.25f);RoarActionTick(.6f);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(5000f-Hp(overlap),Is.EqualTo(damage).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
        Assert.That(Get(target,"familyAttackReductionRatio"),Is.EqualTo(.2f));
        Assert.That(Get(target,"familyAttackReductionTimer"),Is.EqualTo(4f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Get(CombatStatusKind.Silence).remaining,Is.EqualTo(1f));
    }

    [Test]
    public void HaetaeThunderStaysAtMarkedGroundAndHitsNewEntrantsWithoutChasing()
    {
        var skill=HaetaeSetup();Trait(HaetaeRank3Rules.HeavenlyJudgment);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.7f);
        Vector3 point=Model(target).transform.position;Model(target).transform.position=Vector3.right*15f;
        object entrant=Unit(1,point),outside=Unit(1,point+Vector3.forward*1.4f);
        RoarActionTick(.4f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(entrant),Is.EqualTo(4760f));Assert.That(Hp(outside),Is.EqualTo(5000f));
    }

    [Test]
    public void HaetaeThunderExpandedRangeCastsForInjuredAllyWithoutEnemy()
    {
        HaetaeSetup();Trait(HaetaeRank3Rules.HeavenlyJudgment);Set(source,"target",null);
        Model(target).transform.position=Vector3.right*20f;object ally=Unit(0,Vector3.right*5.8f);Set(ally,"hp",4000f);
        var status=(CombatStatusState)Get(ally,"combatStatuses");status.ApplyStacks(CombatStatusKind.Poison,3f,1,5,target,10f,1f);
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.True);RoarActionTick(1.1f);
        Assert.That(Hp(ally),Is.EqualTo(4060f));Assert.That(status.HarmfulCount,Is.Zero);
    }

    [Test]
    public void HaetaeThunderExpandedEnemyRangeMatchesAiAndLargeFrameStillResolves()
    {
        HaetaeSetup("geumgangsu",HaetaeCombatRules.GeumgangsuTraitId);Trait(HaetaeRank3Rules.HeavenlyJudgment);
        Model(target).transform.position=Vector3.right*5.8f;
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.True);RoarActionTick(1.1f);
        Assert.That(Hp(target),Is.EqualTo(4670f).Within(.01f));
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(400f));
    }

    [Test]
    public void HaetaeThunderPauseAndBattleResetClearPendingDamage()
    {
        var skill=HaetaeSetup();Trait(HaetaeRank3Rules.HeavenlyJudgment);
        GriffinCall("BeginHaetaeJusticeRoar",source,skill);RoarActionTick(.7f);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);
        RoarActionTick(1f);Assert.That(Hp(target),Is.EqualTo(5000f));
        GriffinCall("ClearHaetaeRoars");typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);
        RoarActionTick(1f);Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That((IList)typeof(MonsterAutoBattlePrototype).GetField("haetaeRoars",Private).GetValue(battle),Is.Empty);
    }

    [Test]
    public void HaetaeThunderImportedAssetsAreBuildReferencedAndWaitForImpact()
    {
        var template=Resources.Load<PixelHaetaeThunder>(PixelHaetaeThunder.ResourcePath);
        Assert.That(template,Is.Not.Null);Assert.That(template.variants.Length,Is.EqualTo(4));
        foreach(var prefab in template.variants)Assert.That(prefab,Is.Not.Null);
        Assert.That(PixelHaetaeThunder.ImpactClip.name,Is.EqualTo("SFX_Vefects_Zap_Big_02"));
        var fx=PixelHaetaeThunder.Create(Vector3.zero,0,null);fx.Pose(.34f);
        Assert.That(fx.GetComponentsInChildren<ParticleSystem>().Length,Is.EqualTo(1));
        fx.Pose(.39f);Assert.That(fx.GetComponentsInChildren<ParticleSystem>().Length,Is.GreaterThan(3));
        Assert.That(fx.transform.Find("VFX_Zap_06_White(Clone)").position.y,Is.EqualTo(.04f));
        fx.Cancel();Assert.That(fx==null,Is.True);
    }
}
