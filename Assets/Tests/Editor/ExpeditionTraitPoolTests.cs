using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ExpeditionTraitPoolTests
{
    [Test]
    public void RepeatedTraitLookupsReuseDefinitionsUntilMasterDataIsRefreshed()
    {
        GameMasterData.ClearCache();
        var first=MonsterTraitCatalog.All;
        var trait=first[0];
        for(int frame=0;frame<100;frame++)
        {
            Assert.That(MonsterTraitCatalog.All,Is.SameAs(first));
            Assert.That(MonsterTraitCatalog.Get(trait.id),Is.SameAs(trait));
        }
        GameMasterData.ClearCache();
        Assert.That(MonsterTraitCatalog.All,Is.Not.SameAs(first));
        Assert.That(MonsterTraitCatalog.Get(trait.id).displayName,Is.EqualTo(trait.displayName));
    }

    [Test]
    public void MasterRefreshRebuildsTraitsAndPreservesFallbackAndIntrinsicLookup()
    {
        var source=GameMasterData.Database;
        var copy=Object.Instantiate(source);
        var field=typeof(GameMasterData).GetField("cached",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        try
        {
            var original=MonsterTraitCatalog.All;
            field.SetValue(null,copy);
            Assert.That(MonsterTraitCatalog.All,Is.Not.SameAs(original));
            var row=copy.traits.First(r=>r.enabled&&r.category=="Rank2Enhancement");
            row.displayName="Updated trait";
            GameMasterData.ClearCache();
            field.SetValue(null,copy);
            Assert.That(MonsterTraitCatalog.Get(row.id).displayName,Is.EqualTo(row.displayName));
            var intrinsic=copy.traits.First(r=>r.enabled&&r.category=="Intrinsic");
            Assert.That(MonsterTraitCatalog.Get(intrinsic.id).displayName,Is.EqualTo(intrinsic.displayName));
            row.enabled=false;
            GameMasterData.ClearCache();
            field.SetValue(null,copy);
            Assert.That(MonsterTraitCatalog.All.Select(t=>t.displayName),Does.Not.Contain("Updated trait"));
            Assert.That(MonsterTraitCatalog.Rank2,Has.Count.EqualTo(16));
            Assert.That(MonsterTraitCatalog.Get("missing_trait"),Is.Null);
            Assert.That(MonsterTraitCatalog.Get(null),Is.Null);
        }
        finally
        {
            GameMasterData.ClearCache();
            Object.DestroyImmediate(copy);
        }
    }

    [Test]
    public void MasterDatabaseContainsLatestFamilyRankThreeAndTranscendenceTraits()
    {
        TraitMasterRow[] active=GameMasterData.Database.traits
            .Where(row=>row!=null&&row.enabled&&row.category!="Intrinsic").ToArray();
        TraitMasterRow[] rank3=active.Where(row=>
            row.category!="Rank2Enhancement"&&
            row.category!="Rank4Transcendence").ToArray();
        TraitMasterRow[] rank4=active.Where(row=>
            row.category=="Rank4Transcendence").ToArray();

        Assert.That(active,Has.Length.EqualTo(96));
        Assert.That(active.Count(row=>row.category=="Rank2Enhancement"),Is.EqualTo(16));
        Assert.That(rank3,Has.Length.EqualTo(60));
        Assert.That(rank4,Has.Length.EqualTo(20));
        Assert.That(rank3,
            Has.All.Matches<TraitMasterRow>(row=>row.dataVersion>=5));
        Assert.That(rank4,
            Has.All.Matches<TraitMasterRow>(row=>row.dataVersion>=1));
        Assert.That(active.Select(row=>row.id),Is.Unique);
        Assert.That(MonsterTraitCatalog.Rank2,Has.Count.EqualTo(16));
        Assert.That(MonsterTraitCatalog.Rank3,Has.Count.EqualTo(60));
        Assert.That(MonsterTraitCatalog.Rank4,Has.Count.EqualTo(20));
        Assert.That(MonsterTraitCatalog.Rank3.Select(row=>row.id),
            Does.Not.Contain("trait_skill_condense"));
    }

    [Test]
    public void EveryEnabledMonsterHasExactlyThreeFamilyRankThreeCandidates()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        foreach(MonsterDefinition definition in database.monsters.Where(item=>item!=null&&item.enabled))
        {
            int count=MonsterTraitCatalog.Rank3.Count(trait=>trait.IsCompatible(definition));
            Assert.That(count,Is.EqualTo(3),definition.monsterId);
            Assert.That(MonsterTraitCatalog.Rank4.Count(
                trait=>trait.IsCompatible(definition)),Is.EqualTo(1),
                definition.monsterId);
            Assert.That(MonsterTraitCatalog.Rank2.Count(trait=>trait.IsCompatible(definition)),
                Is.GreaterThanOrEqualTo(12),definition.monsterId);
        }
    }

    [Test]
    public void PreviewAssignmentIsDeterministicAndKeepsTiersSeparate()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.monsters.First(item=>item!=null&&item.enabled);
        ExpeditionMonsterStateSave first=new ExpeditionMonsterStateSave();
        ExpeditionMonsterStateSave second=new ExpeditionMonsterStateSave();
        ExpeditionTraitProgression.AssignPreviewTraits(first,definition,new System.Random(4721));
        ExpeditionTraitProgression.AssignPreviewTraits(second,definition,new System.Random(4721));

        Assert.That(first.rank2TraitId,Is.EqualTo(second.rank2TraitId));
        Assert.That(first.rank3TraitId,Is.EqualTo(second.rank3TraitId));
        Assert.That(MonsterTraitCatalog.Get(first.rank2TraitId).IsRank2,Is.True);
        Assert.That(MonsterTraitCatalog.Get(first.rank3TraitId).IsRank3,Is.True);
    }

    [Test]
    public void EnemyProgressionActivatesEveryTraitUpToItsRank()
    {
        MonsterDefinition definition=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase").Get("velos");
        var factory=typeof(MonsterAutoBattlePrototype).GetMethod(
            "BuildEnemyProgression",
            System.Reflection.BindingFlags.Static|
            System.Reflection.BindingFlags.NonPublic);
        Assert.That(factory,Is.Not.Null);

        ExpeditionMonsterStateSave rank1=(ExpeditionMonsterStateSave)factory.Invoke(
            null,new object[]{definition,ExpeditionMonsterRank.Rank1,101,new[]{definition}});
        ExpeditionMonsterStateSave rank2=(ExpeditionMonsterStateSave)factory.Invoke(
            null,new object[]{definition,ExpeditionMonsterRank.Rank2,102,new[]{definition}});
        ExpeditionMonsterStateSave rank3=(ExpeditionMonsterStateSave)factory.Invoke(
            null,new object[]{definition,ExpeditionMonsterRank.Rank3,103,new[]{definition}});

        Assert.That(ExpeditionTraitProgression.ActiveTraitIds(rank1),Is.Empty);
        Assert.That(ExpeditionTraitProgression.ActiveTraitIds(rank2),Has.Count.EqualTo(1));
        Assert.That(ExpeditionTraitProgression.ActiveTraitIds(rank3),Has.Count.EqualTo(2));
        Assert.That(MonsterTraitCatalog.Get(rank2.rank2TraitId).IsRank2,Is.True);
        Assert.That(MonsterTraitCatalog.Get(rank3.rank2TraitId).IsRank2,Is.True);
        Assert.That(MonsterTraitCatalog.Get(rank3.rank3TraitId).IsRank3,Is.True);
    }

    [Test]
    public void LegacyRankThreeTraitMigratesWithoutReplacingRankTwoTrait()
    {
        MonsterDefinition definition=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase").Get("velos");
        ExpeditionMonsterStateSave state=new ExpeditionMonsterStateSave
        {
            rank2TraitId="trait_rank2_might",
            rank3TraitId="trait_skill_condense"
        };

        ExpeditionTraitProgression.AssignPreviewTraits(
            state,definition,new System.Random(4721));

        Assert.That(state.rank2TraitId,Is.EqualTo("trait_rank2_might"));
        Assert.That(MonsterTraitCatalog.Rank3
            .Where(trait=>trait.IsCompatible(definition))
            .Select(trait=>trait.id),Does.Contain(state.rank3TraitId));
    }

    [Test]
    public void LegacySpiritDeerSoulSeparationBecomesInviolableSanctuary()
    {
        MonsterDefinition definition=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase").Get("holydeer");
        ExpeditionMonsterStateSave state=new ExpeditionMonsterStateSave
        {
            rank2TraitId="trait_rank2_might",
            rank3TraitId=SpiritDeerRank3Rules.LegacySoulSeparation
        };

        ExpeditionTraitProgression.AssignPreviewTraits(
            state,definition,new System.Random(4721));

        Assert.That(state.rank2TraitId,Is.EqualTo("trait_rank2_might"));
        Assert.That(state.rank3TraitId,
            Is.EqualTo(SpiritDeerRank3Rules.InviolableSanctuary));
    }

    [Test]
    public void RankTwoPursuitAndRankThreeSkillNumbersMatchDesign()
    {
        MonsterTraitCombatState state=new MonsterTraitCombatState();
        BasicAttackTraitResult hit=default;
        for(int index=0;index<3;index++)
            hit=MonsterTraitRuntime.ResolveBasicAttack(
                new[]{"trait_rank2_pursuit"},state,"target",false,false);
        Assert.That(hit.bonusPrimaryDamage,Is.EqualTo(0.3f).Within(0.001f));

        SkillUseTraitResult result=MonsterTraitRuntime.PreviewSkillUse(
            new[]{"trait_skill_condense","trait_skill_overcharge"},
            new MonsterTraitCombatState());
        Assert.That(result.damageMultiplier,Is.EqualTo(1.8f*1.7f).Within(0.001f));
        Assert.That(result.radiusMultiplier,Is.EqualTo(0.6f*1.3f).Within(0.001f));
    }
}
