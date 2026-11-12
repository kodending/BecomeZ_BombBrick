using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private object StartGuillotine(string intrinsic=null)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_mantius"));
        Set(source,"skillId",MantisCombatRules.SkillId);
        Trait(MantisRank3Rules.DivineGuillotine);if(intrinsic!=null)Trait(intrinsic);
        typeof(MonsterAutoBattlePrototype).GetMethod("StartMantisGuillotine",Private)
            .Invoke(battle,new[]{source,target,1f,1f,0});
        return Get(source,"mantisGuillotine");
    }
    private void AdvanceGuillotine(object state,float time)=>typeof(MonsterAutoBattlePrototype)
        .GetMethod("AdvanceMantisGuillotine",Private).Invoke(battle,new[]{source,state,time});
    private void EndGuillotine(object state)=>typeof(MonsterAutoBattlePrototype)
        .GetMethod("EndMantisGuillotine",Private).Invoke(battle,new[]{source,state});

    [Test]
    public void MantisGuillotineHitsOnlyTheTwoGroundStripsAtContact()
    {
        Vector3 center=Model(target).transform.position;
        var arm=Unit(1,center+MantisRank3Rules.GuillotineDirection(Vector3.right,0)*MonsterAutoBattlePrototype.TileSize*2f);
        var gap=Unit(1,center+Vector3.forward*MonsterAutoBattlePrototype.TileSize*2.3f);
        var ally=Unit(0,center);
        object state=StartGuillotine();AdvanceGuillotine(state,.49f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(arm),Is.EqualTo(5000f));
        AdvanceGuillotine(state,.5f);
        Assert.That(Hp(target),Is.EqualTo(4820f));Assert.That(Hp(arm),Is.EqualTo(4820f));
        AdvanceGuillotine(state,.61f);Assert.That(Hp(target),Is.EqualTo(4820f));
        AdvanceGuillotine(state,.63f);AdvanceGuillotine(state,.7f);
        Assert.That(Hp(target),Is.EqualTo(4640f));Assert.That(Hp(arm),Is.EqualTo(4820f));
        Assert.That(Hp(gap),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(statuses.Has(CombatStatusKind.Bleed),Is.True);
        Assert.That(statuses.Get(CombatStatusKind.Bleed).remaining,Is.EqualTo(3f));
        Assert.That(statuses.Has(CombatStatusKind.Root),Is.False);
        Assert.That(((CombatStatusState)Get(arm,"combatStatuses")).Has(CombatStatusKind.Bleed),Is.False);
        AdvanceGuillotine(state,.94f);EndGuillotine(state);
        Assert.That(Get(source,"mantisGuillotine"),Is.Null);
    }

    [TestCase(MantisCombatRules.MantiusTraitId,432f,false)]
    [TestCase(MantisCombatRules.FlotisTraitId,360f,true)]
    [TestCase(MantisCombatRules.SantiusTraitId,360f,false)]
    public void MantisGuillotineRetainsIntrinsicsAndLongFrames(string trait,float damage,bool pollen)
    {
        object state=StartGuillotine(trait);AdvanceGuillotine(state,.94f);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.HallucinatoryPollen),Is.EqualTo(pollen));
        EndGuillotine(state);
    }

    [Test]
    public void MantisGuillotineRechecksPositionsAndDoesNotFollowTheTarget()
    {
        object state=StartGuillotine();AdvanceGuillotine(state,.5f);
        Model(target).transform.position+=Vector3.forward*20f;
        AdvanceGuillotine(state,.63f);
        Assert.That(Hp(target),Is.EqualTo(4820f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Bleed),Is.False);
        EndGuillotine(state);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReleasedMantisGuillotineSurvivesDeathButBattleResetCancelsIt(bool battleReset)
    {
        object state=StartGuillotine();AdvanceGuillotine(state,.4f);
        if(battleReset)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else Set(source,"alive",false);
        AdvanceGuillotine(state,1f);EndGuillotine(state);
        Assert.That(Hp(target),Is.EqualTo(battleReset?5000f:4640f));
        Assert.That(Get(source,"mantisGuillotine"),Is.Null);
        if(battleReset)Assert.That(Object.FindObjectsOfType<PixelMantisGroundCut>(),Is.Empty);
    }

    [TestCase("death")] [TestCase("stun")] [TestCase("freeze")]
    public void ReleasedGuillotineCompletesAfterCasterIsDisabled(string state)
    {
        object attack=StartGuillotine();AdvanceGuillotine(attack,.4f);
        DisableReleasedCaster(state);EndGuillotine(attack);
        AdvanceGuillotine(attack,1f);EndGuillotine(attack);
        Assert.That(Hp(target),Is.EqualTo(4640f));
        Assert.That(Get(source,"mantisGuillotine"),Is.Null);
    }

    [Test]
    public void GuillotineInterruptedBeforeReleaseDoesNotDealDamage()
    {
        object attack=StartGuillotine();AdvanceGuillotine(attack,.2f);
        DisableReleasedCaster("stun");EndGuillotine(attack);
        AdvanceGuillotine(attack,1f);Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [Test]
    public void MantisGuillotineAnimationFreezesAndRestoresItsPose()
    {
        var driver=Model(source).AddComponent<SpriteMonsterAnimationDriver>();
        var frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        typeof(SpriteMonsterAnimationDriver).GetField("skillAttackFrames",Private).SetValue(driver,frames);
        typeof(SpriteMonsterAnimationDriver).GetField("idleFrames",Private).SetValue(driver,new[]{frames[0]});
        float time=0f;int ended=0;
        driver.PlayMantisGuillotine(()=>true,t=>time=t,()=>ended++);
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        advance.Invoke(driver,new object[]{.35f});Assert.That(time,Is.EqualTo(.35f).Within(.001f));
        typeof(SpriteMonsterAnimationDriver).GetField("statusFrozen",Private).SetValue(driver,true);
        advance.Invoke(driver,new object[]{.5f});Assert.That(time,Is.EqualTo(.35f).Within(.001f));
        typeof(SpriteMonsterAnimationDriver).GetField("statusFrozen",Private).SetValue(driver,false);
        advance.Invoke(driver,new object[]{.7f});
        Assert.That(ended,Is.EqualTo(1));Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
        Assert.That(typeof(SpriteMonsterAnimationDriver).GetField("mantisGuillotineAttack",Private).GetValue(driver),Is.False);
    }
}
