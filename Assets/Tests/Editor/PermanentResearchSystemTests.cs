using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class PermanentResearchSystemTests
{
    private string root;
    private MonsterDatabase database;

    [SetUp]
    public void SetUp()
    {
        root=Path.Combine(Path.GetTempPath(),
            "bombbrick_research_tests_"+Guid.NewGuid().ToString("N"));
        database=ScriptableObject.CreateInstance<MonsterDatabase>();
    }

    [TearDown]
    public void TearDown()
    {
        if(database!=null)UnityEngine.Object.DestroyImmediate(database);
        if(Directory.Exists(root))Directory.Delete(root,true);
    }

    [Test]
    public void Catalog_RetiresPaidScoutingAndKeepsTheThirtyPointCap()
    {
        Assert.That(PermanentResearchSystem.All.Sum(row=>row.maxRank),
            Is.EqualTo(49));
        Assert.That(PermanentResearchSystem.All.Any(row=>row.id==
            PermanentResearchSystem.AdvanceScouting),Is.False);
        Assert.That(PermanentResearchSystem.ExperienceForLevel(1),
            Is.EqualTo(40));
        Assert.That(PermanentResearchSystem.ExperienceForLevel(2),
            Is.EqualTo(90));
        Assert.That(PermanentResearchSystem.ExperienceForLevel(30),
            Is.EqualTo(5550));
        Assert.That(PermanentResearchSystem.LevelForExperience(39),Is.Zero);
        Assert.That(PermanentResearchSystem.LevelForExperience(40),Is.EqualTo(1));
        Assert.That(PermanentResearchSystem.LevelForExperience(5550),Is.EqualTo(30));
    }

    [Test]
    public void RetiredScoutingReturnsItsPointWithoutChangingProgressOrOtherResearch()
    {
        PlayerProfileSave profile=ProfileAtLevel(12);
        profile.hunterTokens=51;
        profile.permanentResearchAllocations.Add(
            new PermanentResearchAllocationSave{researchId=PermanentResearchSystem.Health,rank=3});
        profile.permanentResearchAllocations.Add(
            new PermanentResearchAllocationSave{researchId=PermanentResearchSystem.AdvanceScouting,rank=1});
        profile.permanentResearchRoutes.Add(
            new PermanentResearchRouteProgressSave{routeKey="meadow_beginner",bestCompletedWave=8,lastFailedBracket=2});
        profile.settledPermanentResearchRunIds.Add("settled_before_intel_patch");
        int available=PermanentResearchSystem.AvailablePoints(profile),experience=profile.permanentResearchExperience;
        PermanentResearchSystem.Normalize(profile);
        Assert.That(PermanentResearchSystem.AvailablePoints(profile),Is.EqualTo(available+1));
        Assert.That(PermanentResearchSystem.Rank(profile,PermanentResearchSystem.Health),Is.EqualTo(3));
        Assert.That(PermanentResearchSystem.Rank(profile,PermanentResearchSystem.AdvanceScouting),Is.Zero);
        Assert.That(PermanentResearchSystem.Value(profile,PermanentResearchSystem.AdvanceScouting),Is.Zero);
        Assert.That(profile.permanentResearchExperience,Is.EqualTo(experience));
        Assert.That(profile.hunterTokens,Is.EqualTo(51));
        Assert.That(profile.permanentResearchRoutes.Single().bestCompletedWave,Is.EqualTo(8));
        Assert.That(profile.permanentResearchRoutes.Single().lastFailedBracket,Is.EqualTo(2));
        Assert.That(profile.settledPermanentResearchRunIds,Is.EqualTo(new[]{"settled_before_intel_patch"}));
        Assert.That(PermanentResearchSystem.TryChangeRank(
            profile,PermanentResearchSystem.AdvanceScouting,1),Is.False);
        PermanentResearchSystem.Normalize(profile);
        Assert.That(PermanentResearchSystem.AvailablePoints(profile),Is.EqualTo(available+1),
            "Loading again must not grant an extra research level or point.");
    }

    [Test]
    public void AnOlderMasterImportCannotRestorePaidEnemyInformation()
    {
        var row=GameMasterData.Database.permanentResearch.Single(
            value=>value.id==PermanentResearchSystem.AdvanceScouting);
        bool enabled=row.enabled;
        try
        {
            row.enabled=true;
            PlayerProfileSave profile=ProfileAtLevel(30);
            profile.permanentResearchAllocations.Add(
                new PermanentResearchAllocationSave{researchId=row.id,rank=1});
            Assert.That(PermanentResearchSystem.All.Any(value=>value.id==row.id),Is.False);
            Assert.That(GameMasterData.Research(row.id),Is.Null);
            Assert.That(PermanentResearchSystem.TryChangeRank(profile,row.id,1),Is.False);
            Assert.That(PermanentResearchSystem.Value(profile,row.id),Is.Zero);
            PermanentResearchSystem.Normalize(profile);
            Assert.That(profile.permanentResearchAllocations,Is.Empty);
            Assert.That(PermanentResearchSystem.AvailablePoints(profile),Is.EqualTo(30));
        }
        finally{row.enabled=enabled;}
    }

    [Test]
    public void LoadingAnOldSavePersistsTheRefundAndBackupRecoveryCannotDoubleIt()
    {
        var store=new PlayerProfileStore(root);
        var realDatabase=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        PlayerProfileSave profile=store.LoadOrCreate(realDatabase);
        store.ConfirmStarters(profile);
        profile.permanentResearchExperience=PermanentResearchSystem.ExperienceForLevel(12);
        profile.hunterTokens=51;
        profile.firstExpeditionGuideStep=4;
        profile.firstExpeditionCaptureGuideStep=2;
        profile.permanentResearchAllocations.Add(
            new PermanentResearchAllocationSave{researchId=PermanentResearchSystem.Health,rank=3});
        profile.permanentResearchAllocations.Add(
            new PermanentResearchAllocationSave{researchId=PermanentResearchSystem.AdvanceScouting,rank=1});
        string[] monsters=profile.monsters.Select(value=>value.instanceId).ToArray();
        store.Save(profile);
        PlayerProfileSave restored=store.LoadOrCreate(realDatabase);
        Assert.That(PermanentResearchSystem.AvailablePoints(restored),Is.EqualTo(9));
        Assert.That(restored.permanentResearchAllocations.Single().researchId,Is.EqualTo(PermanentResearchSystem.Health));
        Assert.That(restored.monsters.Select(value=>value.instanceId),Is.EqualTo(monsters));
        Assert.That(restored.hunterTokens,Is.EqualTo(51));
        Assert.That(restored.firstExpeditionGuideStep,Is.EqualTo(4));
        Assert.That(restored.firstExpeditionCaptureGuideStep,Is.EqualTo(2));
        Assert.That(File.ReadAllText(store.ProfilePath),Does.Not.Contain(PermanentResearchSystem.AdvanceScouting));
        File.WriteAllText(store.ProfilePath,"broken primary");
        restored=store.LoadOrCreate(realDatabase);
        Assert.That(store.RecoveredFromBackup,Is.True);
        Assert.That(PermanentResearchSystem.AvailablePoints(restored),Is.EqualTo(9));
        Assert.That(restored.permanentResearchExperience,Is.EqualTo(profile.permanentResearchExperience));
        Assert.That(restored.monsters.Select(value=>value.instanceId),Is.EqualTo(monsters));
    }

    [Test]
    public void Capstone_RequiresEveryPriorBranchRank()
    {
        PlayerProfileSave profile=ProfileAtLevel(30);
        Assert.That(PermanentResearchSystem.TryChangeRank(
            profile,PermanentResearchSystem.FrontlinePreparation,1),Is.False);
        Max(profile,PermanentResearchSystem.Health);
        Max(profile,PermanentResearchSystem.Defense);
        Max(profile,PermanentResearchSystem.Resistance);
        Max(profile,PermanentResearchSystem.Recovery);
        Assert.That(PermanentResearchSystem.TryChangeRank(
            profile,PermanentResearchSystem.FrontlinePreparation,1),Is.True);
        Assert.That(PermanentResearchSystem.TryChangeRank(
            profile,PermanentResearchSystem.Health,-1),Is.False);
    }

    [Test]
    public void Settlement_GrantsProgressBonusesThenRepeatMinimum()
    {
        PlayerProfileSave profile=ProfileAtLevel(0);
        PlayerProfileStore store=new PlayerProfileStore(root);
        ExpeditionRunSave first=Run("run_first",3);
        PermanentResearchSettlement firstResult=PermanentResearchSystem.Settle(
            profile,store,database,first,false);
        Assert.That(firstResult.experience,Is.EqualTo(42));
        Assert.That(firstResult.experienceBefore,Is.Zero);
        Assert.That(firstResult.experienceAfter,Is.EqualTo(42));
        Assert.That(firstResult.levelAfter,Is.EqualTo(1));
        Assert.That(firstResult.improvedWaves,Is.EqualTo(3));
        Assert.That(firstResult.newMilestones,Is.EqualTo(1));

        ExpeditionRunSave repeat=Run("run_repeat",3);
        PermanentResearchSettlement repeatResult=PermanentResearchSystem.Settle(
            profile,store,database,repeat,false);
        Assert.That(repeatResult.experience,Is.EqualTo(8));
        Assert.That(repeatResult.repeatedBracketPenalty,Is.True);
    }

    [TestCase(1,1)]
    [TestCase(3,0)]
    public void ShopNegotiation_DiscountsOnlyTheFirstPaidRefreshEvenWhenFree(
        int researchRank,int firstGrowthCost)
    {
        PlayerProfileSave profile=ProfileAtLevel(30);
        for(int rank=0;rank<researchRank;rank++)
            Assert.That(PermanentResearchSystem.TryChangeRank(
                profile,PermanentResearchSystem.ShopNegotiation,1),Is.True);
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="discount_test",wave=3,expeditionCurrency=10
        };
        for(int index=0;index<8;index++)
            run.monsters.Add(new ExpeditionMonsterStateSave
            {
                instanceId="monster_"+index,monsterId="missing"
            });

        Assert.That(ExpeditionGrowthSystem.RerollCostFor(run,profile),
            Is.EqualTo(firstGrowthCost));
        Assert.That(ExpeditionGrowthSystem.TryReroll(
            run,null,profile,out string growthError),Is.True,growthError);
        Assert.That(run.expeditionCurrency,Is.EqualTo(10-firstGrowthCost));
        Assert.That(ExpeditionGrowthSystem.RerollCostFor(run,profile),Is.EqualTo(2));

        ExpeditionShopSystem.EnsureStock(run,11);
        for(int refresh=0;refresh<2;refresh++)
        {
            Assert.That(ExpeditionShopSystem.RefreshCost(run,profile),Is.Zero);
            Assert.That(ExpeditionShopSystem.TryRefresh(
                run,12+refresh,profile,out string shopError),Is.True,shopError);
        }
        Assert.That(run.expeditionCurrency,Is.EqualTo(10-firstGrowthCost));
        Assert.That(ExpeditionShopSystem.RefreshCost(run,profile),Is.EqualTo(2));
    }

    [Test]
    public void Settlement_PaysTutorialCompletionOnce()
    {
        PlayerProfileSave profile=ProfileAtLevel(0);
        profile.firstExpeditionGuideStep=0;
        PlayerProfileStore store=new PlayerProfileStore(root);
        ExpeditionRunSave run=Run("run_tutorial",12);
        PermanentResearchSettlement result=PermanentResearchSystem.Settle(
            profile,store,database,run,true);
        Assert.That(result.experience,Is.EqualTo(204));
        Assert.That(result.levelAfter,Is.EqualTo(3));
        profile.firstExpeditionGuideStep=8;
        Assert.That(PermanentResearchSystem.Settle(
            profile,store,database,run,true).experience,Is.Zero);
    }

    private static PlayerProfileSave ProfileAtLevel(int level)
    {
        return new PlayerProfileSave
        {
            firstExpeditionGuideInitialized=true,
            firstExpeditionGuideVersion=2,
            firstExpeditionGuideStep=8,
            permanentResearchExperience=
                PermanentResearchSystem.ExperienceForLevel(level)
        };
    }

    private static ExpeditionRunSave Run(string id,int completed)
    {
        return new ExpeditionRunSave
        {
            runId=id,
            regionId="prototype_region",
            difficulty=ExpeditionDifficulty.Beginner,
            lastCompletedWave=completed
        };
    }

    private static void Max(PlayerProfileSave profile,string id)
    {
        PermanentResearchMasterRow row=PermanentResearchSystem.All
            .First(value=>value.id==id);
        for(int rank=0;rank<row.maxRank;rank++)
            Assert.That(PermanentResearchSystem.TryChangeRank(profile,id,1),
                Is.True);
    }
}
