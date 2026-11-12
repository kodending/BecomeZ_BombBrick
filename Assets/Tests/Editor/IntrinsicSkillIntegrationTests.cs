using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [TestCase("")]
    [TestCase(BearCombatRules.MountainColossusTraitId)]
    [TestCase(BearCombatRules.RagingHundredStrikesTraitId)]
    [TestCase(BearCombatRules.KingOfBeastsRoarTraitId)]
    public void DamiEverySkillShieldsLowestHealthRatioAllyWithCastersMaxHealth(string rank3)
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_dami"));
        Set(source,"skillId",BearCombatRules.SkillId);Set(source,"hp",4000f);
        Trait(BearCombatRules.DamiTraitId);if(rank3.Length>0)Trait(rank3);
        var ally=Unit(0,Vector3.forward);Set(ally,"hp",200f);Set(ally,"maxHp",2000f);
        var higherRatio=Unit(0,Vector3.back);Set(higherRatio,"hp",100f);Set(higherRatio,"maxHp",400f);
        // This inactive fixture checks the synchronous intrinsic grant; roar wave timing has its own suite.
        if(rank3==BearCombatRules.KingOfBeastsRoarTraitId)
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                "Coroutine couldn't be started because the the game object 'Monkey Arc Test' is inactive!");
        Assert.That(MammothCall("ApplyBearFightingSpirit",source,GameMasterData.Skill(BearCombatRules.SkillId),1f,false,0),Is.True);
        var shield=((CombatStatusState)Get(ally,"combatStatuses")).Get(CombatStatusKind.Shield);
        Assert.That(shield,Is.Not.Null);
        Assert.That(shield.magnitude,Is.EqualTo(500f));
        Assert.That(shield.remaining,Is.EqualTo(6f));
        Assert.That(shield.source,Is.SameAs(source));
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).Has(CombatStatusKind.Shield),Is.False);
        Assert.That(((CombatStatusState)Get(higherRatio,"combatStatuses")).Has(CombatStatusKind.Shield),Is.False);
    }

    [Test]
    public void DamiCanProtectHerselfWhenAloneAndDoesNotSelectDeadOrTemporaryCopies()
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_dami"));
        Trait(BearCombatRules.DamiTraitId);
        var dead=Unit(0,Vector3.forward);Set(dead,"alive",false);Set(dead,"hp",0f);
        var copy=Unit(0,Vector3.back);Set(copy,"augmentShadowClone",true);Set(copy,"hp",1f);
        MammothCall("ApplyBearFightingSpirit",source,GameMasterData.Skill(BearCombatRules.SkillId),1f,false,0);
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(500f));
        Assert.That(((CombatStatusState)Get(copy,"combatStatuses")).Has(CombatStatusKind.Shield),Is.False);
    }
}
