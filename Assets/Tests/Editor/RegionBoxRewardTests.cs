using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class RegionBoxRewardTests
{
    private string root;
    private MonsterDatabase database;
    private PlayerProfileStore store;
    private PlayerProfileSave profile;

    [SetUp]
    public void SetUp()
    {
        root=Path.Combine(Path.GetTempPath(),"BombBrickRegionBoxRewardTests",Guid.NewGuid().ToString("N"));
        HunterEconomyTelemetry.SetOutputDirectoryForTests(Path.Combine(root,"Telemetry"));
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        store=new PlayerProfileStore(root);
        profile=store.LoadOrCreate(database);
        profile.starterSelectionPending=false;
    }

    [TearDown]
    public void TearDown()
    {
        HunterEconomyTelemetry.ResetOutputDirectoryForTests();
        if(Directory.Exists(root))Directory.Delete(root,true);
    }

    [TestCase(ExpeditionDifficulty.Beginner,1)]
    [TestCase(ExpeditionDifficulty.Normal,2)]
    [TestCase(ExpeditionDifficulty.Veteran,3)]
    [TestCase(ExpeditionDifficulty.Hell,4)]
    public void ClearQueuesOrdinaryRegionRollsWithoutGrantingCandidates(
        ExpeditionDifficulty difficulty,int expected)
    {
        ExpeditionRunSave run=CompletedRun(difficulty);
        int ownedBefore=profile.monsters.Count;
        HunterTokenRewardSystem.Settle(profile,store,database,run,true);

        Assert.That(profile.pendingRegionBoxRewards,Has.Count.EqualTo(1));
        RegionBoxRewardSave reward=profile.pendingRegionBoxRewards.Single();
        Assert.That(reward.candidates,Has.Count.EqualTo(expected));
        Assert.That(profile.monsters,Has.Count.EqualTo(ownedBefore));
        foreach(OwnedMonsterSave candidate in reward.candidates)
        {
            Assert.That(HunterRegionCatalog.Get(run.regionId).MonsterIds(),Does.Contain(candidate.monsterId));
            OwnedMonsterSave ordinary=PlayerProfileStore.CreateMonster(database.Get(candidate.monsterId),candidate.instanceId);
            ordinary.acquiredUtcTicks=candidate.acquiredUtcTicks;
            ordinary.isNew=true;
            Assert.That(JsonUtility.ToJson(candidate),Is.EqualTo(JsonUtility.ToJson(ordinary)),
                "Clear candidates use one ordinary roll including the ordinary shiny rule, not capture rerolls.");
        }
    }

    [Test]
    public void SavedChoicesSurviveRestartAndOnlySelectedMonsterIsGrantedOnce()
    {
        ExpeditionRunSave run=CompletedRun(ExpeditionDifficulty.Hell);
        int ownedBefore=profile.monsters.Count;
        HunterTokenRewardSystem.Settle(profile,store,database,run,true);
        string original=JsonUtility.ToJson(profile.pendingRegionBoxRewards.Single());
        store=new PlayerProfileStore(root);
        profile=store.LoadOrCreate(database);
        Assert.That(JsonUtility.ToJson(profile.pendingRegionBoxRewards.Single()),Is.EqualTo(original));
        HunterTokenRewardSystem.Settle(profile,store,database,run,true);
        Assert.That(profile.pendingRegionBoxRewards,Has.Count.EqualTo(1));
        RegionBoxRewardSave reward=profile.pendingRegionBoxRewards.Single();
        string selectedId=reward.candidates[2].instanceId;
        string[] rejectedIds=reward.candidates.Where((candidate,index)=>index!=2)
            .Select(candidate=>candidate.instanceId).ToArray();

        Assert.That(HunterShopSystem.TryClaimRegionBoxReward(profile,store,database,
            run.runId,2,out OwnedMonsterSave acquired,out string error),Is.True,error);
        Assert.That(acquired.instanceId,Is.EqualTo(selectedId));
        Assert.That(profile.monsters,Has.Count.EqualTo(ownedBefore+1));
        Assert.That(profile.monsters.Any(monster=>rejectedIds.Contains(monster.instanceId)),Is.False);
        Assert.That(profile.pendingRegionBoxRewards,Is.Empty);

        profile=new PlayerProfileStore(root).LoadOrCreate(database);
        HunterTokenRewardSystem.Settle(profile,store,database,run,true);
        Assert.That(HunterShopSystem.TryClaimRegionBoxReward(profile,store,database,
            run.runId,2,out _,out _),Is.False);
        Assert.That(profile.monsters.Count(monster=>monster.instanceId==selectedId),Is.EqualTo(1));
        Assert.That(profile.pendingRegionBoxRewards,Is.Empty);
    }

    [TestCase(false,12,ExpeditionRunStatus.Failed)]
    [TestCase(true,11,ExpeditionRunStatus.Active)]
    [TestCase(true,12,ExpeditionRunStatus.Abandoned)]
    public void FailedIncompleteOrAbandonedExpeditionsDoNotGiveABox(
        bool victory,int completedWaves,ExpeditionRunStatus status)
    {
        ExpeditionRunSave run=CompletedRun(ExpeditionDifficulty.Hell);
        run.lastCompletedWave=completedWaves;
        run.status=status;
        HunterTokenRewardSystem.Settle(profile,store,database,run,victory);
        Assert.That(profile.pendingRegionBoxRewards,Is.Empty);
    }

    [Test]
    public void InvalidChoiceAndFailedSaveLeaveRewardAndCollectionUnchanged()
    {
        ExpeditionRunSave run=CompletedRun(ExpeditionDifficulty.Normal);
        HunterTokenRewardSystem.Settle(profile,store,database,run,true);
        string before=JsonUtility.ToJson(profile);
        Assert.That(HunterShopSystem.TryClaimRegionBoxReward(profile,store,database,
            run.runId,4,out _,out _),Is.False);
        Assert.That(JsonUtility.ToJson(profile),Is.EqualTo(before));

        Directory.CreateDirectory(store.TemporaryPath);
        Assert.That(HunterShopSystem.TryClaimRegionBoxReward(profile,store,database,
            run.runId,1,out _,out string error),Is.False);
        Assert.That(error,Is.Not.Empty);
        Assert.That(JsonUtility.ToJson(profile),Is.EqualTo(before));
        Directory.Delete(store.TemporaryPath);
        Assert.That(HunterShopSystem.TryClaimRegionBoxReward(profile,store,database,
            run.runId,1,out _,out error),Is.True,error);
    }

    [Test]
    public void ProductionCompletionPersistsRewardBeforeRemovingActiveRun()
    {
        ExpeditionRunSave run=ExpeditionRunFactory.Create(profile,database,
            profile.monsters.Take(8).ToList(),HunterRegionCatalog.FirstRegionId,
            difficulty:ExpeditionDifficulty.Veteran);
        run.lastCompletedWave=12;
        run.wave=12;
        ExpeditionRunStore runs=new ExpeditionRunStore(profile.profileId,root);
        runs.SaveActive(run,profile,database);
        runs.SettleAndComplete(run,true,profile,store,database);

        PlayerProfileSave reloaded=new PlayerProfileStore(root).LoadOrCreate(database);
        Assert.That(reloaded.pendingRegionBoxRewards,Has.Count.EqualTo(1));
        Assert.That(reloaded.pendingRegionBoxRewards[0].candidates,Has.Count.EqualTo(3));
        Assert.That(File.Exists(runs.ActivePath),Is.False);
        Assert.That(run.status,Is.EqualTo(ExpeditionRunStatus.Completed));
    }

    private static ExpeditionRunSave CompletedRun(ExpeditionDifficulty difficulty)
    {
        return new ExpeditionRunSave
        {
            runId="region_reward_"+difficulty,
            regionId=HunterRegionCatalog.FirstRegionId,
            lastCompletedWave=12,
            difficulty=difficulty
        };
    }
}
