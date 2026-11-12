using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private static IEnumerable<TestCaseData> AllSpeciesApproachCases()
    {
        foreach(var definition in Resources.LoadAll<MonsterDefinition>("MonsterData").Where(d=>d.enabled))
            foreach(bool enemy in new[]{false,true})
                yield return new TestCaseData(definition.monsterId,enemy);
    }

    [TestCaseSource(nameof(AllSpeciesApproachCases))]
    public void EverySpeciesUsesReadySkillReachForBothTeams(string monsterId,bool enemyTeam)
    {
        var definition=Resources.LoadAll<MonsterDefinition>("MonsterData").First(d=>d.monsterId==monsterId);
        Set(source,"definition",definition);Set(source,"skillId",definition.skillId);
        Set(source,"attackRange",definition.attackRange);Set(source,"speedTilesPerSecond",2f);
        Set(source,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,enemyTeam?1:0));
        Set(target,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,enemyTeam?0:1));
        float basic=MonsterAutoBattlePrototype.AttackDistanceForRange(definition.attackRange,.3f,.3f);
        var approach=typeof(MonsterAutoBattlePrototype).GetMethod("CombatApproachDistance",Private);
        Set(source,"skillTimer",1f);
        Assert.That(approach.Invoke(battle,new[]{source,target}),Is.EqualTo(basic));
        Set(source,"skillTimer",0f);
        float ready=(float)approach.Invoke(battle,new[]{source,target});
        Assert.That(ready,Is.GreaterThan(0f).And.LessThanOrEqualTo(basic));
        var skill=GameMasterData.Skill(definition.skillId);
        Assert.That(SkillUseDecision.SupportsBehavior(skill.behavior),Is.True);
        if(skill.behavior=="FightingSpirit"||skill.behavior=="SpiritSanctuary")
        {
            Assert.That(ready,Is.EqualTo(basic),"Self buffs and healing must not pull the caster toward an enemy.");
            return;
        }
        Model(target).transform.position=Vector3.right*(ready*.99f);
        var snapshot=(SkillUseSnapshot)typeof(MonsterAutoBattlePrototype).GetMethod("BuildSkillSnapshot",Private)
            .Invoke(battle,new[]{source});
        Assert.That(SkillUseDecision.ShouldUse(snapshot),Is.True,"The stopping point must satisfy the skill's opportunity check.");
        if(ready>=basic-.01f)return;
        Model(target).transform.position=Vector3.right*(basic-.01f);Set(source,"cooldown",100f);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        typeof(MonsterAutoBattlePrototype).GetMethod("UpdateUnit",Private).Invoke(battle,new[]{source,(object).1f});
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(.01f));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0),"Out-of-range approach cannot force a cast.");
    }

    [TestCase("guardian_taunt")]
    [TestCase("cluster_breath")]
    public void AllActiveSkillsAreConsideredBeforeSettlingForBasicAttacks(string skillId)
    {
        Set(source,"skillId",skillId);Set(source,"skillTimer",0f);Set(source,"cooldown",100f);
        Model(target).transform.position=Vector3.right*1.5f;
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        typeof(MonsterAutoBattlePrototype).GetMethod("UpdateUnit",Private).Invoke(battle,new[]{source,(object).1f});
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
    }

    [Test]
    public void HealingKeepsItsInjuredAllyRequirementAndPassiveSkillsDoNotActivelyCast()
    {
        Set(source,"skillId","spirit_deer_sanctuary");
        Model(target).transform.position=Vector3.right*9.7f;
        var build=typeof(MonsterAutoBattlePrototype).GetMethod("BuildSkillSnapshot",Private);
        Assert.That(SkillUseDecision.ShouldUse((SkillUseSnapshot)build.Invoke(battle,new[]{source})),Is.False);
        Set(source,"hp",4000f);
        Assert.That(SkillUseDecision.ShouldUse((SkillUseSnapshot)build.Invoke(battle,new[]{source})),Is.True);
        Set(source,"skillId","focus_chain");
        Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("TryUseSkill",Private)
            .Invoke(battle,new[]{source,(object)false}),Is.False);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0));
    }

    [TestCase(MonkeyCombatRules.SkillId,false)]
    [TestCase(SpiderCombatRules.SkillId,false)]
    [TestCase(MonkeyCombatRules.SkillId,true)]
    [TestCase(SpiderCombatRules.SkillId,true)]
    public void ReadyShorterRangeSkillApproachesInsteadOfRepeatingBasicAttacks(string skillId,bool enemyTeam)
    {
        Set(source,"skillId",skillId);
        Set(source,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,enemyTeam?1:0));
        Set(target,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,enemyTeam?0:1));
        Set(source,"skillTimer",0f);Set(source,"cooldown",100f);Set(source,"speedTilesPerSecond",2f);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        float basicReach=MonsterAutoBattlePrototype.AttackDistanceForRange(3,.3f,.3f);
        Model(target).transform.position=Vector3.right*(basicReach-.01f);
        var immediate=typeof(MonsterAutoBattlePrototype).GetMethod("FindImmediateAttackTarget",Private);
        Set(source,"skillTimer",2f);
        Assert.That(immediate.Invoke(battle,new[]{source}),Is.SameAs(target));
        Set(source,"skillTimer",0f);
        Assert.That(immediate.Invoke(battle,new[]{source}),Is.Null);
        typeof(MonsterAutoBattlePrototype).GetMethod("UpdateUnit",Private)
            .Invoke(battle,new[]{source,(object).1f});
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(.01f),
            "The normal AI update must advance into skill range, not remain in basic-attack recovery.");
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0),"Approaching must not bypass the cast range.");
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
    }

    [TestCase(MonkeyCombatRules.SkillId,CombatStatusKind.Silence)]
    [TestCase(SpiderCombatRules.SkillId,CombatStatusKind.Silence)]
    [TestCase(MonkeyCombatRules.SkillId,CombatStatusKind.Root)]
    [TestCase(SpiderCombatRules.SkillId,CombatStatusKind.Root)]
    public void BlockedSkillStillAllowsAnInRangeBasicAttack(string skillId,CombatStatusKind status)
    {
        Set(source,"skillId",skillId);Set(source,"skillTimer",0f);
        Model(target).transform.position=Vector3.right*
            (MonsterAutoBattlePrototype.AttackDistanceForRange(3,.3f,.3f)-.01f);
        ((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(status,2f);
        Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("FindImmediateAttackTarget",Private)
            .Invoke(battle,new[]{source}),Is.SameAs(target));
    }
}
