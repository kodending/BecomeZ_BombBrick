using System;
using System.Collections;
using System.Threading.Tasks;
using System.Reflection;
using BombBrick.Game.Core;
using UnityEngine.TestTools;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ExpeditionSaveRetryTests
{
    private string root;
    private MonsterDatabase database;
    private PlayerProfileStore profiles;
    private PlayerProfileSave profile;
    private ExpeditionRunStore runs;
    private ExpeditionRunSave run;

    [SetUp]
    public void SetUp()
    {
        root=Path.Combine(Path.GetTempPath(),"BombBrick.SaveRetry."+Guid.NewGuid().ToString("N"));
        HunterEconomyTelemetry.SetOutputDirectoryForTests(Path.Combine(root,"Telemetry"));
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        profiles=new PlayerProfileStore(root);
        profile=profiles.LoadOrCreate(database);
        var owned=PlayerProfileStore.CreateMonster(database.Get("raptor_reference_palette_v2"),"retry_raptor");
        profile.monsters.Add(owned);
        profiles.Save(profile);
        runs=new ExpeditionRunStore(profile.profileId,root);
        run=ExpeditionRunFactory.Create(profile,database,new[]{owned},HunterRegionCatalog.FirstRegionId,
            3,3,ExpeditionDifficulty.Beginner);
        run.lastCompletedWave=12;
        run.wave=12;
        runs.SaveActive(run,profile,database);
    }

    [TearDown]
    public void TearDown()
    {
        HunterEconomyTelemetry.ResetOutputDirectoryForTests();
        Assert.That(Path.GetFullPath(root),Does.StartWith(Path.Combine(Path.GetTempPath(),"BombBrick.SaveRetry.")));
        if(Directory.Exists(root))Directory.Delete(root,true);
    }

    [UnityTest]
    public IEnumerator DistributedRunReadKeepsAccessFailuresAndCanResumeAfterUnlock()
    {
        run.expeditionCurrency=73;runs.SaveActive(run,profile,database);
        byte[] primary=File.ReadAllBytes(runs.ActivePath),backup=File.ReadAllBytes(runs.BackupPath);
        Exception failure=null;
        using(new FileStream(runs.ActivePath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            var loading=runs.LoadActiveAcrossFrames(profile,database,value=>Assert.Fail("Locked primary must not fall back."));
            while(true)
            {
                bool moved=false;
                try{moved=loading.MoveNext();}catch(Exception exception){failure=exception;}
                if(!moved||failure!=null)break;
                yield return loading.Current;
            }
        }
        Assert.That(failure,Is.InstanceOf<IOException>());
        ExpeditionRunSave restored=null;
        yield return runs.LoadActiveAcrossFrames(profile,database,value=>restored=value);
        Assert.That(restored.expeditionCurrency,Is.EqualTo(73));
        Assert.That(File.ReadAllBytes(runs.ActivePath),Is.EqualTo(primary));
        Assert.That(File.ReadAllBytes(runs.BackupPath),Is.EqualTo(backup));
    }

    [UnityTest]
    public IEnumerator WorkerCheckpointRetriesTheSameSnapshotBeforeLaterSaves()
    {
        runs.SaveActive(run,profile,database);
        byte[] primary=File.ReadAllBytes(runs.ActivePath),backup=File.ReadAllBytes(runs.BackupPath);
        var host=new GameObject("Inactive asynchronous save fixture");host.SetActive(false);
        var flow=host.AddComponent<GameFlowController>();var frontend=host.AddComponent<GameFrontend>();
        const BindingFlags statics=BindingFlags.Static|BindingFlags.NonPublic;
        const BindingFlags members=BindingFlags.Instance|BindingFlags.NonPublic;
        var flowInstance=typeof(GameFlowController).GetField("instance",statics);
        var frontendInstance=typeof(GameFrontend).GetField("instance",statics);
        var taskField=typeof(GameFrontend).GetField("pendingSaveTask",members);
        object oldFlow=flowInstance.GetValue(null),oldFrontend=frontendInstance.GetValue(null);
        float speed=Time.timeScale;
        flowInstance.SetValue(null,flow);frontendInstance.SetValue(null,frontend);
        int prepared=0,callbackThread=0,workerThread=0,mainThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        var order=new System.Collections.Generic.List<string>();
        try
        {
            profile.hunterTokens=123;run.expeditionCurrency=73;
            using(new FileStream(runs.ActivePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Assert.That(GameFrontend.TrySaveProgressAcrossFrames(()=>
                {
                    prepared++;
                    var saveProfile=profiles.PrepareSaveFile(profile);
                    var saveRun=runs.PrepareSaveActive(run,profile,database);
                    return ()=>{workerThread=System.Threading.Thread.CurrentThread.ManagedThreadId;saveProfile();saveRun();};
                },root,()=>{callbackThread=System.Threading.Thread.CurrentThread.ManagedThreadId;order.Add("committed");}),Is.False);
                profile.hunterTokens=999;run.expeditionCurrency=777;
                Assert.That(GameFrontend.TrySaveProgress(()=>order.Add("later"),root),Is.False);
                var task=(Task)taskField.GetValue(frontend);
                while(!task.IsCompleted)yield return null;
                LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("\\[Save Retry\\]"));
                Assert.That(frontend.RetryPendingSave(),Is.False);
                Assert.That(GameFrontend.HasPendingSave,Is.True);
                Assert.That(order,Is.Empty);
                Assert.That(File.ReadAllBytes(runs.ActivePath),Is.EqualTo(primary));
                Assert.That(File.ReadAllBytes(runs.BackupPath),Is.EqualTo(backup));
                Assert.That(Time.timeScale,Is.Zero);
            }
            Assert.That(frontend.RetryPendingSave(),Is.False);
            var retry=(Task)taskField.GetValue(frontend);
            while(!retry.IsCompleted)yield return null;
            Assert.That(frontend.RetryPendingSave(),Is.True);
            Assert.That(GameFrontend.HasPendingSave,Is.False);
            Assert.That(prepared,Is.EqualTo(1));
            Assert.That(workerThread,Is.Not.EqualTo(mainThread));
            Assert.That(callbackThread,Is.EqualTo(mainThread));
            Assert.That(order,Is.EqualTo(new[]{"committed","later"}));
            Assert.That(JsonUtility.FromJson<PlayerProfileSave>(File.ReadAllText(profiles.ProfilePath)).hunterTokens,Is.EqualTo(123));
            Assert.That(runs.LoadActive(profile,database).expeditionCurrency,Is.EqualTo(73));
            Assert.That(profile.hunterTokens,Is.EqualTo(999));
        }
        finally
        {
            var pending=(Task)taskField.GetValue(frontend);
            if(pending!=null)try{pending.Wait(5000);}catch(AggregateException){}
            flowInstance.SetValue(null,oldFlow);frontendInstance.SetValue(null,oldFrontend);
            Time.timeScale=speed;UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void EncounterRegistrationSavesSynchronouslyByDefaultAndNotifiesOnce()
    {
        string id=database.monsters.First(definition=>definition!=null&&definition.enabled&&
            !profile.encounteredMonsterIds.Contains(definition.monsterId)&&
            !profile.monsters.Any(owned=>owned!=null&&owned.monsterId==definition.monsterId)).monsterId;
        int notifications=0,callbackThread=0,mainThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        profiles.Changed+=saved=>
        {
            Assert.That(saved,Is.SameAs(profile));
            Assert.That(JsonUtility.FromJson<PlayerProfileSave>(File.ReadAllText(profiles.ProfilePath))
                .encounteredMonsterIds,Does.Contain(id));
            notifications++;callbackThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        };
        Assert.That(HunterShopSystem.RegisterEncounters(profile,profiles,database,new[]{id,id,"","missing"}),Is.True);
        Assert.That(notifications,Is.EqualTo(1));
        Assert.That(callbackThread,Is.EqualTo(mainThread));
        Assert.That(profile.encounteredMonsterIds.Count(value=>value==id),Is.EqualTo(1));
        byte[] primary=File.ReadAllBytes(profiles.ProfilePath),backup=File.ReadAllBytes(profiles.BackupPath);
        Assert.That(HunterShopSystem.RegisterEncounters(profile,profiles,database,new[]{id}),Is.False);
        Assert.That(notifications,Is.EqualTo(1));
        Assert.That(File.ReadAllBytes(profiles.ProfilePath),Is.EqualTo(primary));
        Assert.That(File.ReadAllBytes(profiles.BackupPath),Is.EqualTo(backup));
    }

    [UnityTest]
    public IEnumerator EncounterSaveRetriesTheSameSnapshotAndNotifiesOnMainThreadAfterCommit()
    {
        string id=database.monsters.First(definition=>definition!=null&&definition.enabled&&
            !profile.encounteredMonsterIds.Contains(definition.monsterId)&&
            !profile.monsters.Any(owned=>owned!=null&&owned.monsterId==definition.monsterId)).monsterId;
        byte[] primary=File.ReadAllBytes(profiles.ProfilePath),backup=File.ReadAllBytes(profiles.BackupPath);
        int tokens=profile.hunterTokens;
        var host=new GameObject("Inactive encounter save fixture");host.SetActive(false);
        var flow=host.AddComponent<GameFlowController>();var frontend=host.AddComponent<GameFrontend>();
        const BindingFlags statics=BindingFlags.Static|BindingFlags.NonPublic;
        const BindingFlags members=BindingFlags.Instance|BindingFlags.NonPublic;
        var flowInstance=typeof(GameFlowController).GetField("instance",statics);
        var frontendInstance=typeof(GameFrontend).GetField("instance",statics);
        var taskField=typeof(GameFrontend).GetField("pendingSaveTask",members);
        object oldFlow=flowInstance.GetValue(null),oldFrontend=frontendInstance.GetValue(null);
        float speed=Time.timeScale;
        flowInstance.SetValue(null,flow);frontendInstance.SetValue(null,frontend);
        int notifications=0,callbackThread=0,mainThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        var order=new System.Collections.Generic.List<string>();
        profiles.Changed+=saved=>
        {
            Assert.That(saved,Is.SameAs(profile));
            Assert.That(JsonUtility.FromJson<PlayerProfileSave>(File.ReadAllText(profiles.ProfilePath))
                .encounteredMonsterIds,Does.Contain(id));
            notifications++;callbackThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
            order.Add("encounter");
        };
        try
        {
            using(new FileStream(profiles.ProfilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Assert.That(HunterShopSystem.RegisterEncounters(profile,profiles,database,new[]{id},true),Is.True);
                Assert.That(GameFrontend.HasPendingSave,Is.True);
                Assert.That(notifications,Is.Zero);
                profile.hunterTokens=tokens+999;
                Assert.That(GameFrontend.TrySaveProgress(()=>order.Add("later"),root),Is.False);
                var task=(Task)taskField.GetValue(frontend);
                while(!task.IsCompleted)yield return null;
                LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("\\[Save Retry\\]"));
                Assert.That(frontend.RetryPendingSave(),Is.False);
                Assert.That(notifications,Is.Zero);
                Assert.That(GameFrontend.HasPendingSave,Is.True);
                Assert.That(File.ReadAllBytes(profiles.ProfilePath),Is.EqualTo(primary));
                Assert.That(File.ReadAllBytes(profiles.BackupPath),Is.EqualTo(backup));
                Assert.That(Time.timeScale,Is.Zero);
            }
            Assert.That(frontend.RetryPendingSave(),Is.False);
            var retry=(Task)taskField.GetValue(frontend);
            while(!retry.IsCompleted)yield return null;
            Assert.That(frontend.RetryPendingSave(),Is.True);
            Assert.That(GameFrontend.HasPendingSave,Is.False);
            Assert.That(notifications,Is.EqualTo(1));
            Assert.That(callbackThread,Is.EqualTo(mainThread));
            Assert.That(order,Is.EqualTo(new[]{"encounter","later"}));
            var savedProfile=JsonUtility.FromJson<PlayerProfileSave>(File.ReadAllText(profiles.ProfilePath));
            Assert.That(savedProfile.hunterTokens,Is.EqualTo(tokens));
            Assert.That(savedProfile.encounteredMonsterIds.Count(value=>value==id),Is.EqualTo(1));
            Assert.That(profile.hunterTokens,Is.EqualTo(tokens+999));
            Assert.That(File.ReadAllBytes(profiles.BackupPath),Is.EqualTo(primary));
        }
        finally
        {
            var pending=(Task)taskField.GetValue(frontend);
            if(pending!=null)try{pending.Wait(5000);}catch(AggregateException){}
            flowInstance.SetValue(null,oldFlow);frontendInstance.SetValue(null,oldFrontend);
            Time.timeScale=speed;UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void LockedCheckpointKeepsPrimaryAndBackupAndCanRetry()
    {
        runs.SaveActive(run,profile,database);
        byte[] primary=File.ReadAllBytes(runs.ActivePath),backup=File.ReadAllBytes(runs.BackupPath);
        run.expeditionCurrency=73;
        using(new FileStream(runs.ActivePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>runs.SaveActive(run,profile,database));
        Assert.That(File.ReadAllBytes(runs.ActivePath),Is.EqualTo(primary));
        Assert.That(File.ReadAllBytes(runs.BackupPath),Is.EqualTo(backup));
        runs.SaveActive(run,profile,database);
        Assert.That(runs.LoadActive(profile,database).expeditionCurrency,Is.EqualTo(73));
    }

    [Test]
    public void LockedProfileDoesNotOverwriteBackup()
    {
        byte[] primary=File.ReadAllBytes(profiles.ProfilePath),backup=File.ReadAllBytes(profiles.BackupPath);
        profile.hunterTokens+=31;
        using(new FileStream(profiles.ProfilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>profiles.Save(profile));
        Assert.That(File.ReadAllBytes(profiles.ProfilePath),Is.EqualTo(primary));
        Assert.That(File.ReadAllBytes(profiles.BackupPath),Is.EqualTo(backup));
        profiles.Save(profile);
        Assert.That(profiles.LoadOrCreate(database).hunterTokens,Is.EqualTo(profile.hunterTokens));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SettlementProfileFailureRetainsIntentWithoutPayingAndRestartsSafely(bool restart)
    {
        int tokens=profile.hunterTokens,xp=profile.permanentResearchExperience;
        using(new FileStream(profiles.ProfilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>runs.SettleAndComplete(run,true,profile,profiles,database));
        Assert.That(run.status,Is.EqualTo(ExpeditionRunStatus.Active));
        Assert.That(profile.hunterTokens,Is.EqualTo(tokens));
        Assert.That(profile.permanentResearchExperience,Is.EqualTo(xp));
        Assert.That(profile.settledExpeditionRunIds,Does.Not.Contain(run.runId));
        if(restart)
        {
            profile=profiles.LoadOrCreate(database);
            run=runs.LoadActive(profile,database);
        }
        Assert.That(run.completion,Is.Not.Null);
        var receipt=runs.SettleAndComplete(run,true,profile,profiles,database);
        AssertPaidOnce(tokens,xp,receipt);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResultFailureAfterRewardsRetainsActiveAndRetryNeverPaysTwice(bool restart)
    {
        int tokens=profile.hunterTokens,xp=profile.permanentResearchExperience;
        Directory.CreateDirectory(runs.LastResultPath+".tmp");
        Assert.Throws<UnauthorizedAccessException>(()=>runs.SettleAndComplete(run,true,profile,profiles,database));
        Assert.That(run.status,Is.EqualTo(ExpeditionRunStatus.Active));
        Assert.That(File.Exists(runs.ActivePath),Is.True);
        var receipt=run.completion;
        Assert.That(profile.hunterTokens,Is.EqualTo(tokens+receipt.hunterTokens));
        Directory.Delete(runs.LastResultPath+".tmp");
        if(restart)
        {
            profile=profiles.LoadOrCreate(database);
            run=runs.LoadActive(profile,database);
        }
        var retried=runs.SettleAndComplete(run,true,profile,profiles,database);
        Assert.That(retried.research.experience,Is.EqualTo(receipt.research.experience));
        AssertPaidOnce(tokens,xp,retried);
    }

    [Test]
    public void IntentFailureDoesNotGrantRewards()
    {
        int tokens=profile.hunterTokens;
        using(new FileStream(runs.ActivePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>runs.SettleAndComplete(run,true,profile,profiles,database));
        Assert.That(profile.hunterTokens,Is.EqualTo(tokens));
        Assert.That(profile.settledExpeditionRunIds,Does.Not.Contain(run.runId));
        Assert.That(File.Exists(runs.LastResultPath),Is.False);
    }

    [Test]
    public void CleanupFailureKeepsActiveUntilRetryAndResultIsAtomic()
    {
        runs.SaveActive(run,profile,database);
        using(new FileStream(runs.BackupPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>runs.Complete(run,true,profile,database));
        Assert.That(run.status,Is.EqualTo(ExpeditionRunStatus.Active));
        Assert.That(File.Exists(runs.ActivePath),Is.True);
        Assert.That(JsonUtility.FromJson<ExpeditionRunSave>(File.ReadAllText(runs.LastResultPath)).status,
            Is.EqualTo(ExpeditionRunStatus.Completed));
        runs.Complete(run,true,profile,database);
        Assert.That(run.status,Is.EqualTo(ExpeditionRunStatus.Completed));
        Assert.That(runs.LoadActive(profile,database),Is.Null);
    }

    [Test]
    public void LockedActiveRunNeverFallsBackToOlderProgress()
    {
        runs.SaveActive(run,profile,database);
        using(new FileStream(runs.ActivePath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>runs.LoadActive(profile,database));
    }

    [Test]
    public void OldCheckpointHasNoPendingSettlementAndLoadingDoesNotWrite()
    {
        var legacy=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(runs.ActivePath));
        legacy["version"]=14;
        legacy.Remove("completionPending");
        legacy.Remove("completion");
        File.WriteAllText(runs.ActivePath,legacy.ToString());
        string before=File.ReadAllText(runs.ActivePath);
        using(new FileStream(runs.ActivePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.That(runs.LoadActive(profile,database).completionPending,Is.False);
        Assert.That(File.ReadAllText(runs.ActivePath),Is.EqualTo(before));
    }

    private void AssertPaidOnce(int tokens,int xp,ExpeditionCompletionSave receipt)
    {
        Assert.That(receipt.hunterTokens,Is.GreaterThan(0));
        Assert.That(receipt.research.experience,Is.GreaterThan(0));
        Assert.That(profile.hunterTokens,Is.EqualTo(tokens+receipt.hunterTokens));
        Assert.That(profile.permanentResearchExperience,Is.EqualTo(xp+receipt.research.experience));
        Assert.That(profile.settledExpeditionRunIds.Count(id=>id==run.runId),Is.EqualTo(1));
        Assert.That(profile.settledPermanentResearchRunIds.Count(id=>id==run.runId),Is.EqualTo(1));
        Assert.That(run.status,Is.EqualTo(ExpeditionRunStatus.Completed));
        Assert.That(runs.LoadActive(profile,database),Is.Null);
        Assert.That(runs.SettleAndComplete(run,true,profile,profiles,database),Is.SameAs(receipt));
        Assert.That(profiles.LoadOrCreate(database).hunterTokens,Is.EqualTo(profile.hunterTokens));
    }
}
