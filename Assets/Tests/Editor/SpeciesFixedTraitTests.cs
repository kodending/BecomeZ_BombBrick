using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class SpeciesFixedTraitTests
{
    [Test]
    public void MasterImportRejectsMissingAndIncompatibleFixedTraitsBeforeSaving()
    {
        var copy=UnityEngine.Object.Instantiate(GameMasterData.Database);
        var validate=typeof(GameMasterDatabaseSync).GetMethod("ValidateFixedTraits",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        try
        {
            Assert.DoesNotThrow(()=>validate.Invoke(null,new object[]{copy}));
            var growth=copy.monsterGrowths.First(r=>r.monsterId=="velos");
            string original=growth.fixedRank3TraitId;
            growth.fixedRank3TraitId="missing_fixed_trait";
            Assert.That(Assert.Throws<System.Reflection.TargetInvocationException>(
                ()=>validate.Invoke(null,new object[]{copy})).InnerException,Is.TypeOf<InvalidDataException>());
            growth.fixedRank3TraitId=original;
            copy.traits.First(r=>r.id==original).requiredTags="HaetaeFamily";
            Assert.That(Assert.Throws<System.Reflection.TargetInvocationException>(
                ()=>validate.Invoke(null,new object[]{copy})).InnerException,Is.TypeOf<InvalidDataException>());
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }

    [Test]
    public void EverySpeciesKeepsItsApprovedPairAcrossSeedsAndAllSixtySkillsAreUsed()
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var species=database.monsters.Where(m=>m!=null&&m.enabled).ToArray();
        Assert.That(species,Has.Length.EqualTo(80));
        foreach(var definition in species)
        {
            var row=GameMasterData.MonsterGrowth(definition.monsterId);
            Assert.That(row.fixedTraitVersion,Is.EqualTo(1),definition.monsterId);
            for(int seed=0;seed<3;seed++)
            {
                var state=new ExpeditionMonsterStateSave();
                ExpeditionTraitProgression.AssignPreviewTraits(state,definition,new System.Random(seed));
                Assert.That(state.rank2TraitId,Is.EqualTo(row.fixedRank2TraitId));
                Assert.That(state.rank3TraitId,Is.EqualTo(row.fixedRank3TraitId));
                Assert.That(ExpeditionTraitProgression.ActiveTraitIds(state),Is.Empty);
                state.rank=ExpeditionMonsterRank.Rank2;
                Assert.That(ExpeditionTraitProgression.ActiveTraitIds(state),Is.EqualTo(new[]{row.fixedRank2TraitId}));
                state.rank=ExpeditionMonsterRank.Rank3;
                Assert.That(ExpeditionTraitProgression.ActiveTraitIds(state),Is.EqualTo(new[]{row.fixedRank2TraitId,row.fixedRank3TraitId}));
            }
        }
        Assert.That(species.Select(m=>ExpeditionTraitProgression.FixedTrait(m,3).id).Distinct().Count(),Is.EqualTo(60));
        foreach(var family in species.GroupBy(m=>m.familyId))
            Assert.That(family.Select(m=>GameMasterData.MonsterGrowth(m.monsterId))
                .Select(r=>r.fixedRank2TraitId+"/"+r.fixedRank3TraitId),Is.Unique,family.Key);
    }

    [Test]
    public void WholeCrewRerollRandomizesAllSpeciesAndPreservesBasicQualityAndSavedRolls()
    {
        string root=Path.Combine(Path.GetTempPath(),"BombBrickStarterTests",Guid.NewGuid().ToString("N"));
        try
        {
            var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
            var store=new PlayerProfileStore(root);
            var profile=store.LoadOrCreate(database);
            var basicSpecies=new System.Collections.Generic.HashSet<string>(
                profile.monsters.Take(6).Select(m=>m.monsterId));
            var rank2=new System.Collections.Generic.HashSet<string>();
            var rank3=new System.Collections.Generic.HashSet<string>();
            var personalities=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<15;i++)
            {
                string[] oldIds=profile.monsters.Select(m=>m.instanceId).ToArray();
                store.RerollStarters(profile,database);
                string[] rolled=profile.monsters.Select(m=>JsonUtility.ToJson(m)).ToArray();
                profile=store.LoadOrCreate(database);
                Assert.That(profile.monsters.Select(m=>JsonUtility.ToJson(m)),Is.EqualTo(rolled));
                Assert.That(profile.monsters.Select(m=>m.instanceId).Intersect(oldIds),Is.Empty);
                foreach(var basic in profile.monsters.Take(6))
                {
                    basicSpecies.Add(basic.monsterId);
                    Assert.That(MonsterStatQuality.OverallScore(basic),Is.GreaterThanOrEqualTo(30f).And.LessThan(70f));
                    Assert.That(basic.isShiny,Is.False);
                }
                foreach(var monster in profile.monsters)
                {
                    Assert.That(MonsterTraitCatalog.Get(monster.rank2TraitId).IsRank2,Is.True);
                    Assert.That(MonsterTraitCatalog.Get(monster.rank3TraitId).IsRank3,Is.True);
                    Assert.That(MonsterTraitCatalog.IsCompatible(monster.rank2TraitId,database.Get(monster.monsterId)),Is.True);
                    Assert.That(MonsterTraitCatalog.IsCompatible(monster.rank3TraitId,database.Get(monster.monsterId)),Is.True);
                }
                rank2.Add(profile.monsters[0].rank2TraitId);
                rank3.Add(profile.monsters[0].rank3TraitId);
                personalities.Add(profile.monsters[0].personalityId);
                Assert.That(profile.monsters.Select(m=>m.monsterId),Is.Unique);
                Assert.That(profile.monsters.Count,Is.EqualTo(8));
                Assert.That(profile.encounteredMonsterIds,Is.Empty);
            }
            Assert.That(basicSpecies.Count,Is.GreaterThan(6));
            Assert.That(rank2.Count,Is.GreaterThan(1));
            Assert.That(rank3.Count,Is.GreaterThan(1));
            Assert.That(personalities.Count,Is.GreaterThan(1));
            store.ConfirmStarters(profile);
            profile=store.LoadOrCreate(database);
            Assert.That(profile.starterSelectionPending,Is.False);
            Assert.That(profile.encounteredMonsterIds,Is.EquivalentTo(profile.monsters.Select(m=>m.monsterId)));
            Assert.Throws<InvalidOperationException>(()=>store.RerollStarters(profile,database));
            Assert.That(profile.firstExpeditionGuideStep,Is.Zero);
            for(int attempt=0;attempt<3;attempt++)
            {
                var run=ExpeditionRunFactory.Create(profile,database,profile.monsters,"prototype_region");
                Assert.That(ExpeditionRunFactory.Normalize(run,profile,database),Is.True);
                foreach(var monster in profile.monsters)
                {
                    var state=run.monsters.Single(m=>m.instanceId==monster.instanceId);
                    Assert.That(state.rank2TraitId,Is.EqualTo(monster.rank2TraitId));
                    Assert.That(state.rank3TraitId,Is.EqualTo(monster.rank3TraitId));
                    state.rank=ExpeditionMonsterRank.Rank3;
                    Assert.That(ExpeditionTraitProgression.ActiveTraitIds(state),
                        Is.EqualTo(new[]{monster.rank2TraitId,monster.rank3TraitId}));
                }
            }
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Test]
    public void PendingLegacyDraftGetsBoundedNonShinyBasicsAndPersistentTraitsOnlyOnce()
    {
        string root=Path.Combine(Path.GetTempPath(),"BombBrickStarterTests",Guid.NewGuid().ToString("N"));
        try
        {
            var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
            var store=new PlayerProfileStore(root);
            var profile=store.LoadOrCreate(database);
            string[] ids=profile.monsters.Select(m=>m.instanceId).ToArray();
            foreach(var monster in profile.monsters)
            {
                monster.rank2TraitId=monster.rank3TraitId=null;
                monster.isShiny=true;
            }
            store.Save(profile);
            profile=store.LoadOrCreate(database);
            Assert.That(profile.monsters.Select(m=>m.instanceId),Is.EqualTo(ids));
            Assert.That(profile.monsters.Take(6).All(m=>!m.isShiny&&MonsterStatQuality.OverallScore(m)>=30f&&
                MonsterStatQuality.OverallScore(m)<70f),Is.True);
            Assert.That(profile.monsters.Skip(6).All(m=>m.isShiny&&MonsterStatQuality.OverallGrade(m)=="S+"),Is.True);
            string[] upgraded=profile.monsters.Select(m=>JsonUtility.ToJson(m)).ToArray();
            profile=store.LoadOrCreate(database);
            Assert.That(profile.monsters.Select(m=>JsonUtility.ToJson(m)),Is.EqualTo(upgraded));
            store.ConfirmStarters(profile);
            File.WriteAllText(store.ProfilePath,"damaged primary");
            profile=store.LoadOrCreate(database);
            Assert.That(store.RecoveredFromBackup,Is.True);
            Assert.That(profile.monsters.Select(m=>JsonUtility.ToJson(m)),Is.EqualTo(upgraded));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Test]
    public void ExistingProfilesNeverGainOldStartersOrEnterDraft()
    {
        string root=Path.Combine(Path.GetTempPath(),"BombBrickStarterTests",Guid.NewGuid().ToString("N"));
        try
        {
            var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
            var store=new PlayerProfileStore(root);
            var profile=new PlayerProfileSave {firstExpeditionGuideInitialized=true,firstExpeditionGuideVersion=2,firstExpeditionGuideStep=8};
            profile.monsters.Add(PlayerProfileStore.CreateMonster(database.Get("velos"),"legacy_velos"));
            store.Save(profile);
            var loaded=store.LoadOrCreate(database);
            Assert.That(loaded.monsters,Has.Count.EqualTo(1));
            Assert.That(loaded.monsters[0].instanceId,Is.EqualTo("legacy_velos"));
            Assert.That(loaded.starterSelectionPending,Is.False);
            Assert.That(loaded.firstExpeditionGuideStep,Is.EqualTo(8));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Test]
    public void FailedSaveRestoresRerollAndLeavesConfirmationPending()
    {
        string root=Path.Combine(Path.GetTempPath(),"BombBrickStarterTests",Guid.NewGuid().ToString("N"));
        try
        {
            var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
            var store=new PlayerProfileStore(root);var profile=store.LoadOrCreate(database);
            string[] before=profile.monsters.Select(m=>JsonUtility.ToJson(m)).ToArray();
            string[] previousIds=profile.starterRerollInstanceIds.ToArray();
            Directory.CreateDirectory(store.TemporaryPath);
            Assert.Throws<UnauthorizedAccessException>(()=>store.RerollStarters(profile,database));
            Assert.That(profile.monsters.Select(m=>JsonUtility.ToJson(m)),Is.EqualTo(before));
            Assert.That(profile.starterRerollInstanceIds,Is.EqualTo(previousIds));
            Assert.Throws<UnauthorizedAccessException>(()=>store.ConfirmStarters(profile));
            Assert.That(profile.starterSelectionPending,Is.True);
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}

public sealed partial class MonkeyArcTests
{
    [Test]
    public void GuardianAmplifiesCasterShieldButNotRecipientTraitAndKeepsDuration()
    {
        Trait("trait_rank2_guardian");
        var shield=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(shield.ApplyShield(6f,100f,source).magnitude,Is.EqualTo(140f).Within(.001f));
        Assert.That(shield.Get(CombatStatusKind.Shield).remaining,Is.EqualTo(6f));
        var received=(CombatStatusState)Get(source,"combatStatuses");
        Assert.That(received.ApplyShield(6f,100f,target).magnitude,Is.EqualTo(100f));
    }

    [Test]
    public void RankTwoSupportScalesOnceAndNeverChangesDuration()
    {
        Trait("trait_rank2_technique");
        var method=typeof(MonsterAutoBattlePrototype).GetMethod("SkillSupportAmount",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        Set(source,"augmentSkillPowerMultiplier",1.2f);Set(source,"spiritConcertScale",.5f);
        Assert.That((float)method.Invoke(null,new[]{source,(object)100f}),Is.EqualTo(78f).Within(.001f));
        Trait("trait_rank2_guardian");
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        var rally=statuses.ApplyTimed(CombatStatusKind.ComboRally,6f,source,.2f);
        Assert.That(rally.magnitude,Is.EqualTo(.2f*1.3f*1.3f).Within(.001f));
        Assert.That(rally.remaining,Is.EqualTo(6f));
    }
}
