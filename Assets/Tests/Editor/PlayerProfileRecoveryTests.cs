using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerProfileRecoveryTests
{
    private string root;
    private PlayerProfileStore store;
    private MonsterDatabase database;

    [SetUp]
    public void SetUp()
    {
        root=Path.Combine(Path.GetTempPath(),"BombBrick.ProfileRecovery."+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        store=new PlayerProfileStore(root);
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        // Only the uniquely named temporary fixture is disposable, never the player's profile.
        Assert.That(Path.GetFullPath(root),Does.StartWith(
            Path.Combine(Path.GetTempPath(),"BombBrick.ProfileRecovery.")));
        if(Directory.Exists(root))Directory.Delete(root,true);
    }

    private static string SavedProfile(int tokens=321,int version=6)
    {
        return JsonUtility.ToJson(new PlayerProfileSave
        {
            profileId="recovery_test",version=version,hunterTokens=tokens,
            firstExpeditionGuideInitialized=true,firstExpeditionGuideVersion=2,
            firstExpeditionGuideStep=8
        });
    }

    [Test]
    public void ReadingAnUnchangedProfileDoesNotRotateItsBackupOrTimestamp()
    {
        var profile=store.LoadOrCreate(database);
        profile.hunterTokens=321;
        store.Save(profile);
        string primary=File.ReadAllText(store.ProfilePath),backup=File.ReadAllText(store.BackupPath);
        long ticks=profile.lastSavedUtcTicks;
        var loaded=store.LoadOrCreate(database);
        Assert.That(loaded.lastSavedUtcTicks,Is.EqualTo(ticks));
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo(primary));
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(backup));
    }

    [UnityTest]
    public IEnumerator DistributedLoadRecoversBackupAndPublishesOnlyTheCommittedProfile()
    {
        File.WriteAllText(store.ProfilePath,"{broken");
        string backup=SavedProfile();
        File.WriteAllText(store.BackupPath,backup);
        PlayerProfileSave loaded=null;
        int mainThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        var loading=store.LoadOrCreateAcrossFrames(database,value=>
        {
            Assert.That(System.Threading.Thread.CurrentThread.ManagedThreadId,Is.EqualTo(mainThread));
            Assert.That(JsonUtility.FromJson<PlayerProfileSave>(File.ReadAllText(store.ProfilePath)).hunterTokens,
                Is.EqualTo(321));
            loaded=value;
        });
        int frames=0;
        while(loading.MoveNext())
        {
            Assert.That(loaded,Is.Null);
            Assert.Throws<InvalidOperationException>(()=>store.Save(new PlayerProfileSave()));
            Assert.That(++frames,Is.LessThan(10000));
            yield return loading.Current;
        }
        Assert.That(frames,Is.GreaterThan(2));
        Assert.That(loaded.hunterTokens,Is.EqualTo(321));
        Assert.That(store.RecoveredFromBackup,Is.True);
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(backup));
        Assert.That(File.ReadAllText(store.RecoveryArchivePath),Is.EqualTo("{broken"));
        Assert.DoesNotThrow(()=>store.Save(loaded));
    }

    [UnityTest]
    public IEnumerator DistributedLoadPropagatesNewerVersionAndLeavesBothFilesIntact()
    {
        string newer=SavedProfile(999,7),backup=SavedProfile();
        File.WriteAllText(store.ProfilePath,newer);
        File.WriteAllText(store.BackupPath,backup);
        var loading=store.LoadOrCreateAcrossFrames(database,value=>Assert.Fail("Must not publish a failed load."));
        Exception failure=null;
        while(true)
        {
            bool moved=false;
            try {moved=loading.MoveNext();}
            catch(Exception exception) {failure=exception;}
            if(!moved||failure!=null)break;
            yield return loading.Current;
        }
        Assert.That(failure,Is.TypeOf<NotSupportedException>());
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo(newer));
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(backup));
        Assert.Throws<InvalidOperationException>(()=>store.Save(new PlayerProfileSave()));
    }

    [Test]
    public void FirstLaunchCreatesProfileAndNormalLoadKeepsProgress()
    {
        PlayerProfileSave profile=store.LoadOrCreate(database);
        Assert.That(profile.firstExpeditionGuideStep,Is.Zero);
        Assert.That(store.RecoveredFromBackup,Is.False);
        profile.hunterTokens=321;
        store.Save(profile);
        Assert.That(store.LoadOrCreate(database).hunterTokens,Is.EqualTo(321));
        Assert.That(store.RecoveredFromBackup,Is.False);
    }

    [TestCase("")]
    [TestCase("{broken")]
    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("{\"version\":6,\"profileId\":\"default\"}")]
    [TestCase("{\"version\":6,\"profileId\":\"default\",\"monsters\":null}")]
    [TestCase("{\"version\":6,\"profileId\":\"default\",\"monsters\":[{}]}")]
    [TestCase("{\"version\":6,\"version\":5,\"profileId\":\"default\",\"monsters\":[]}")]
    public void InvalidExistingProfileNeverBecomesANewGame(string damaged)
    {
        File.WriteAllText(store.ProfilePath,damaged);
        Assert.Throws<InvalidDataException>(()=>store.LoadOrCreate(database));
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo(damaged));
        Assert.That(File.Exists(store.TemporaryPath),Is.False);
        Assert.Throws<InvalidOperationException>(()=>store.Save(new PlayerProfileSave()));
    }

    [Test]
    public void RecoveryArchivesBadPrimaryAndNeverRotatesItOverGoodBackup()
    {
        const string damaged="{broken";
        string backup=SavedProfile();
        File.WriteAllText(store.ProfilePath,damaged);
        File.WriteAllText(store.BackupPath,backup);

        PlayerProfileSave loaded=store.LoadOrCreate(database);

        Assert.That(loaded.hunterTokens,Is.EqualTo(321));
        Assert.That(store.RecoveredFromBackup,Is.True);
        Assert.That(File.ReadAllText(store.RecoveryArchivePath),Is.EqualTo(damaged));
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(backup));
        Assert.That(JsonUtility.FromJson<PlayerProfileSave>(File.ReadAllText(store.ProfilePath))
            .profileId,Is.EqualTo("recovery_test"));
        Assert.That(new PlayerProfileStore(root).LoadOrCreate(database).hunterTokens,Is.EqualTo(321));
    }

    [Test]
    public void MissingPrimaryRecoversBackupWithoutChangingBackup()
    {
        string backup=SavedProfile();
        File.WriteAllText(store.BackupPath,backup);
        Assert.That(store.LoadOrCreate(database).hunterTokens,Is.EqualTo(321));
        Assert.That(store.RecoveredFromBackup,Is.True);
        Assert.That(store.RecoveryArchivePath,Is.Null);
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(backup));
    }

    [Test]
    public void BothDamagedFilesStayIntactAndARepairCanBeRetried()
    {
        File.WriteAllText(store.ProfilePath,"bad primary");
        File.WriteAllText(store.BackupPath,"bad backup");
        Assert.Throws<InvalidDataException>(()=>store.LoadOrCreate(database));
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo("bad primary"));
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo("bad backup"));
        File.WriteAllText(store.BackupPath,SavedProfile());
        PlayerProfileSave recovered=store.LoadOrCreate(database);
        Assert.That(recovered.hunterTokens,Is.EqualTo(321));
        Assert.DoesNotThrow(()=>store.Save(recovered));
    }

    [Test]
    public void NewerProfileNeverFallsBackToAnOlderBackup()
    {
        string newer=SavedProfile(999,7);
        string backup=SavedProfile();
        File.WriteAllText(store.ProfilePath,newer);
        File.WriteAllText(store.BackupPath,backup);
        Assert.Throws<NotSupportedException>(()=>store.LoadOrCreate(database));
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo(newer));
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(backup));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LockedFileIsNotTreatedAsMissingOrCorrupt(bool lockBackup)
    {
        string saved=SavedProfile();
        string lockedPath=lockBackup?store.BackupPath:store.ProfilePath;
        File.WriteAllText(lockedPath,saved);
        if(!lockBackup)File.WriteAllText(store.BackupPath,SavedProfile(123));
        using(new FileStream(lockedPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>store.LoadOrCreate(database));
        Assert.That(File.ReadAllText(lockedPath),Is.EqualTo(saved));
        Assert.That(File.Exists(store.TemporaryPath),Is.False);
        if(lockBackup)Assert.That(File.Exists(store.ProfilePath),Is.False);
        else Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(SavedProfile(123)));
    }

    [Test]
    public void InaccessibleFilePathDoesNotCauseDefaultProfileCreation()
    {
        Directory.CreateDirectory(store.ProfilePath);
        File.WriteAllText(store.BackupPath,SavedProfile());
        Assert.Throws<UnauthorizedAccessException>(()=>store.LoadOrCreate(database));
        Assert.That(File.Exists(store.TemporaryPath),Is.False);
        Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo(SavedProfile()));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrphanRecoveryFilesBlockSilentNewGame(bool migration)
    {
        string path=migration?store.MigrationBackupPath:store.TemporaryPath;
        string saved=SavedProfile();
        File.WriteAllText(path,saved);
        Assert.Throws<InvalidDataException>(()=>store.LoadOrCreate(database));
        Assert.That(File.Exists(store.ProfilePath),Is.False);
        Assert.That(File.ReadAllText(path),Is.EqualTo(saved));
    }

    [Test]
    public void Version5StillMigratesAndKeepsTheOriginal()
    {
        const string legacy="{\"version\":5,\"profileId\":\"legacy\",\"monsters\":[]}";
        File.WriteAllText(store.ProfilePath,legacy);
        Assert.That(store.LoadOrCreate(database).version,Is.EqualTo(6));
        Assert.That(File.ReadAllText(store.MigrationBackupPath),Is.EqualTo(legacy));
    }
    [UnityTest]
    public IEnumerator DistributedFrontendLoadReportsCorruptDataAndProtectsBothFiles()
    {
        File.WriteAllText(store.ProfilePath,"bad primary");
        File.WriteAllText(store.BackupPath,"bad backup");
        var host=new GameObject("Inactive profile load UI fixture");
        host.SetActive(false);
        var flow=host.AddComponent<BombBrick.Game.Core.GameFlowController>();
        var frontend=host.AddComponent<BombBrick.Game.Core.GameFrontend>();
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var instance=typeof(BombBrick.Game.Core.GameFlowController).GetField("instance",flags);
        object previous=instance.GetValue(null);
        instance.SetValue(null,flow);
        try
        {
            LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("\\[Profile Load\\] Invalid profile:"));
            yield return BombBrick.Game.Core.GameFrontend.LoadPlayerProfileAcrossFrames(store,database,
                value=>Assert.Fail("A failed load must not publish a profile."));
            var field=typeof(BombBrick.Game.Core.GameFrontend).GetField("profileLoadFailed",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(field.GetValue(frontend),Is.True);
            Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo("bad primary"));
            Assert.That(File.ReadAllText(store.BackupPath),Is.EqualTo("bad backup"));
            Assert.Throws<InvalidOperationException>(()=>store.Save(new PlayerProfileSave()));
        }
        finally
        {
            instance.SetValue(null,previous);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
