using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ChundongiMemorialTests
{
    private string fixture;
    private PlayerProfileStore store;
    private MonsterDatabase database;

    [SetUp]
    public void SetUp()
    {
        fixture=Path.Combine(Path.GetTempPath(),"BombBrick.Chundongi."+Guid.NewGuid().ToString("N"));
        store=new PlayerProfileStore(Path.Combine(fixture,"Profiles"));
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
    }

    [TearDown]
    public void TearDown()
    {
        Assert.That(Path.GetFullPath(fixture),Does.StartWith(Path.Combine(Path.GetTempPath(),"BombBrick.Chundongi.")));
        if(Directory.Exists(fixture))Directory.Delete(fixture,true);
    }

    private static OwnedMonsterSave Memorial(PlayerProfileSave profile)=>profile.monsters
        .Single(m=>m.instanceId==PlayerProfileStore.MemorialChundongiInstanceId);

    [Test]
    public void FirstDiscoveryPersistsWithoutCreatingContinueAndDuplicateClicksGrantOnlyOnce()
    {
        Assert.That(store.HasMemorialChundongi,Is.False);
        Assert.That(store.UnlockMemorialChundongi(database),Is.True);
        Assert.That(store.HasMemorialChundongi,Is.True);
        Assert.That(store.HasSavedProgress,Is.False);
        Assert.That(store.UnlockMemorialChundongi(database),Is.False);
        var profile=store.LoadOrCreate(database);
        var memorial=Memorial(profile);
        Assert.That(profile.monsters,Has.Count.EqualTo(9));
        Assert.That(MonsterStatQuality.OverallGrade(memorial),Is.EqualTo("S"));
        Assert.That(memorial.isShiny,Is.False);
        Assert.That(memorial.locked,Is.True);
        Assert.That(MonsterTraitCatalog.IsCompatible(memorial.rank2TraitId,database.Get("chundongi")),Is.True);
        Assert.That(MonsterTraitCatalog.IsCompatible(memorial.rank3TraitId,database.Get("chundongi")),Is.True);
        string before=JsonUtility.ToJson(memorial);
        store.UnlockMemorialChundongi(database);
        Assert.That(JsonUtility.ToJson(Memorial(store.LoadOrCreate(database))),Is.EqualTo(before));
    }

    [Test]
    public void ExistingCapturedChundongiAndOwnedPreferencesAreUntouched()
    {
        var profile=store.LoadOrCreate(database);
        store.ConfirmStarters(profile);
        var captured=store.AcquireMonster(profile,database,"chundongi");
        string capturedBefore=JsonUtility.ToJson(captured);
        store.UnlockMemorialChundongi(database);
        profile=store.LoadOrCreate(database);
        Assert.That(JsonUtility.ToJson(profile.monsters.Single(m=>m.instanceId==captured.instanceId)),Is.EqualTo(capturedBefore));
        var memorial=Memorial(profile);
        memorial.customName="우리 춘동이";
        memorial.favorite=false;
        memorial.isNew=false;
        store.Save(profile);
        memorial=Memorial(store.LoadOrCreate(database));
        Assert.That(memorial.customName,Is.EqualTo("우리 춘동이"));
        Assert.That(memorial.favorite,Is.False);
        Assert.That(memorial.isNew,Is.False);
        Assert.That(store.RemoveMonster(profile,database,memorial.instanceId),Is.False);
        Assert.That(store.RemoveMonster(profile,database,captured.instanceId),Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NewGameRerollsBonusButLoadingAndWholeCrewRerollsKeepIt(bool playTutorial)
    {
        store.UnlockMemorialChundongi(database);
        int previousSeed=Memorial(store.LoadOrCreate(database)).personalitySeed;
        string unlockRecord=File.ReadAllText(store.MemorialChundongiPath);
        new PlayerProgressReset(store.DirectoryPath).StartNewGame(database,playTutorial);
        var profile=store.LoadOrCreate(database);
        var bonus=Memorial(profile);
        Assert.That(bonus.personalitySeed,Is.Not.EqualTo(previousSeed));
        Assert.That(MonsterStatQuality.OverallGrade(bonus),Is.EqualTo("S"));
        Assert.That(bonus.locked,Is.True);
        Assert.That(MonsterTraitCatalog.IsCompatible(bonus.rank2TraitId,database.Get("chundongi")),Is.True);
        Assert.That(MonsterTraitCatalog.IsCompatible(bonus.rank3TraitId,database.Get("chundongi")),Is.True);
        Assert.That(File.ReadAllText(store.MemorialChundongiPath),Is.EqualTo(unlockRecord));
        string original=JsonUtility.ToJson(bonus);
        Assert.That(profile.firstExpeditionGuideStep,Is.EqualTo(playTutorial?0:8));
        for(int i=0;i<3;i++)
        {
            string[] starters=profile.monsters.Where(m=>m.instanceId!=PlayerProfileStore.MemorialChundongiInstanceId)
                .Select(m=>m.instanceId).ToArray();
            store.RerollStarters(profile,database);
            profile=store.LoadOrCreate(database);
            Assert.That(profile.monsters,Has.Count.EqualTo(9));
            Assert.That(profile.monsters.Select(m=>m.instanceId).Intersect(starters),Is.Empty);
            Assert.That(JsonUtility.ToJson(Memorial(profile)),Is.EqualTo(original));
            Assert.That(profile.monsters.Take(6).All(m=>MonsterStatQuality.OverallScore(m)>=30f&&MonsterStatQuality.OverallScore(m)<70f),Is.True);
        }
        store.ConfirmStarters(profile);
        profile=store.LoadOrCreate(database);
        Assert.That(profile.starterSelectionPending,Is.False);
        Assert.That(profile.expeditionPreset.RosterCount,Is.Zero);
        Assert.That(profile.encounteredMonsterIds,Does.Contain("chundongi"));
        Assert.That(JsonUtility.ToJson(Memorial(profile)),Is.EqualTo(original));
        File.WriteAllText(store.ProfilePath,"broken new primary");
        Assert.That(JsonUtility.ToJson(Memorial(store.LoadOrCreate(database))),Is.EqualTo(original));
    }

    [Test]
    public void EachNewGameDrawsPersonalityAndBothTraitsAgain()
    {
        store.UnlockMemorialChundongi(database);
        var personalities=new System.Collections.Generic.HashSet<string>();
        var rank2=new System.Collections.Generic.HashSet<string>();
        var rank3=new System.Collections.Generic.HashSet<string>();
        for(int i=0;i<24;i++)
        {
            new PlayerProgressReset(store.DirectoryPath).StartNewGame(database,false);
            var profile=store.LoadOrCreate(database);
            var companion=Memorial(profile);
            personalities.Add(companion.personalityId);
            rank2.Add(companion.rank2TraitId);
            rank3.Add(companion.rank3TraitId);
            Assert.That(MonsterStatQuality.OverallGrade(companion),Is.EqualTo("S"));
            Assert.That(companion.isShiny,Is.False);
        }
        Assert.That(personalities.Count,Is.GreaterThan(1));
        Assert.That(rank2.Count,Is.GreaterThan(1));
        Assert.That(rank3.Count,Is.GreaterThan(1));
    }

    [Test]
    public void BothProfileRecoveryAndMemorialBackupPreserveTheUnlock()
    {
        var profile=store.LoadOrCreate(database);
        store.ConfirmStarters(profile);
        store.UnlockMemorialChundongi(database);
        string expected=JsonUtility.ToJson(Memorial(store.LoadOrCreate(database)));
        File.WriteAllText(store.ProfilePath,"broken primary");
        File.WriteAllText(store.MemorialChundongiPath,"broken unlock primary");
        profile=store.LoadOrCreate(database);
        Assert.That(store.RecoveredFromBackup,Is.True);
        Assert.That(JsonUtility.ToJson(Memorial(profile)),Is.EqualTo(expected));
        Assert.That(store.UnlockMemorialChundongi(database),Is.False);
    }

    [Test]
    public void FailedProfileWriteKeepsDurableUnlockAndRetriesWithoutDuplicates()
    {
        var profile=store.LoadOrCreate(database);
        string original=File.ReadAllText(store.ProfilePath);
        Directory.CreateDirectory(store.TemporaryPath);
        Assert.Throws<UnauthorizedAccessException>(()=>store.UnlockMemorialChundongi(database));
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo(original));
        Assert.That(store.HasMemorialChundongi,Is.True);
        Assert.That(profile.monsters.Any(m=>m.instanceId==PlayerProfileStore.MemorialChundongiInstanceId),Is.False);
        Directory.Delete(store.TemporaryPath);
        Assert.That(store.UnlockMemorialChundongi(database),Is.False);
        Assert.That(store.LoadOrCreate(database).monsters,Has.Count.EqualTo(9));
    }

    [Test]
    public void FailedRerollRestoresTheOriginalDraftIncludingTheBonus()
    {
        store.UnlockMemorialChundongi(database);
        var profile=store.LoadOrCreate(database);
        string[] original=profile.monsters.Select(m=>JsonUtility.ToJson(m)).ToArray();
        Directory.CreateDirectory(store.TemporaryPath);
        Assert.Throws<UnauthorizedAccessException>(()=>store.RerollStarters(profile,database));
        Assert.That(profile.monsters.Select(m=>JsonUtility.ToJson(m)),Is.EqualTo(original));
        Assert.Throws<UnauthorizedAccessException>(()=>store.ConfirmStarters(profile));
        Assert.That(profile.starterSelectionPending,Is.True);
    }

    [Test]
    public void UnreadableUnlockNeverReplacesTheExistingProfileOrStartsANewGame()
    {
        var profile=store.LoadOrCreate(database);
        string original=File.ReadAllText(store.ProfilePath);
        File.WriteAllText(store.MemorialChundongiPath,"broken unlock");
        Assert.Throws<InvalidDataException>(()=>store.UnlockMemorialChundongi(database));
        Assert.Throws<InvalidDataException>(()=>new PlayerProgressReset(store.DirectoryPath).StartNewGame(database));
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo(original));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyOrInvalidRosterStillRecoversEightStartersBeforeTheMemorial(bool invalidEntry)
    {
        store.UnlockMemorialChundongi(database);
        var profile=store.LoadOrCreate(database);
        string original=JsonUtility.ToJson(Memorial(profile));
        profile.monsters.Clear();
        profile.starterSelectionPending=false;
        if(invalidEntry)profile.monsters.Add(new OwnedMonsterSave {instanceId="obsolete",monsterId="obsolete_species"});
        store.Save(profile);
        profile=store.LoadOrCreate(database);
        Assert.That(profile.monsters,Has.Count.EqualTo(9));
        Assert.That(profile.monsters.Take(6).Select(m=>m.monsterId),
            Is.Unique);
        Assert.That(profile.monsters.Take(6).All(m=>!m.isShiny&&
            MonsterStatQuality.OverallScore(m)>=30f&&MonsterStatQuality.OverallScore(m)<70f),Is.True);
        Assert.That(profile.monsters.Last().instanceId,Is.EqualTo(PlayerProfileStore.MemorialChundongiInstanceId));
        Assert.That(JsonUtility.ToJson(Memorial(profile)),Is.EqualTo(original));
        Assert.That(profile.starterSelectionPending,Is.True);
        Assert.That(profile.starterRerollInstanceIds,Has.Count.EqualTo(2));
        Assert.DoesNotThrow(()=>store.ConfirmStarters(profile));
    }

    [Test]
    public void InterruptedFirstUnlockPromotesValidatedTemporaryRecordAndCreatesFallback()
    {
        store.UnlockMemorialChundongi(database);
        string path=store.MemorialChundongiPath;
        string saved=File.ReadAllText(path);
        File.Move(path,path+".tmp");
        File.Delete(path+".backup");
        Assert.That(store.HasMemorialChundongi,Is.True);
        Assert.That(File.ReadAllText(path),Is.EqualTo(saved));
        Assert.That(File.ReadAllText(path+".backup"),Is.EqualTo(saved));
        Assert.That(File.Exists(path+".tmp"),Is.False);
        Assert.That(store.HasSavedProgress,Is.False);
        new PlayerProgressReset(store.DirectoryPath).StartNewGame(database);
        Assert.That(store.LoadOrCreate(database).monsters,Has.Count.EqualTo(9));
    }

    [Test]
    public void InterruptedFallbackCopyIsCompletedWithoutGrantingAnotherCompanion()
    {
        store.UnlockMemorialChundongi(database);
        string path=store.MemorialChundongiPath;
        File.Delete(path+".backup");
        File.WriteAllText(path+".backup.tmp","unfinished copy");
        Assert.That(store.UnlockMemorialChundongi(database),Is.False);
        Assert.That(File.ReadAllText(path+".backup"),Is.EqualTo(File.ReadAllText(path)));
        Assert.That(File.Exists(path+".backup.tmp"),Is.False);
        Assert.That(store.LoadOrCreate(database).monsters,Has.Count.EqualTo(9));
    }

    [Test]
    public void InvalidInterruptedUnlockPreservesTheTemporaryFileAndProfile()
    {
        store.LoadOrCreate(database);
        string original=File.ReadAllText(store.ProfilePath);
        File.WriteAllText(store.MemorialChundongiPath+".tmp","unfinished unlock");
        Assert.Throws<InvalidDataException>(()=>store.LoadOrCreate(database));
        Assert.Throws<InvalidDataException>(()=>new PlayerProgressReset(store.DirectoryPath).StartNewGame(database));
        Assert.That(File.ReadAllText(store.ProfilePath),Is.EqualTo(original));
        Assert.That(File.ReadAllText(store.MemorialChundongiPath+".tmp"),Is.EqualTo("unfinished unlock"));
        Assert.That(File.Exists(store.MemorialChundongiPath),Is.False);
    }
}
