using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using BombBrick.Game.Scenes;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerProgressResetTests
{
    private string fixture;
    private PlayerProgressReset reset;
    private PlayerProfileStore profiles;
    private MonsterDatabase database;

    [SetUp]
    public void SetUp()
    {
        fixture = Path.Combine(Path.GetTempPath(), "BombBrick.NewGame." + Guid.NewGuid().ToString("N"));
        reset = new PlayerProgressReset(Path.Combine(fixture, "Profiles"));
        profiles = new PlayerProfileStore(reset.RootPath);
        database = Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var profile = profiles.LoadOrCreate(database);
        profile.hunterTokens = 777;
        profile.firstExpeditionGuideStep = profile.firstExpeditionCaptureGuideStep = 8;
        profile.permanentResearchExperience = 600;
        profile.clearedRegionDifficultyKeys.Add("old_clear");
        profiles.Save(profile);
        string runs = new ExpeditionRunStore(profile.profileId, reset.RootPath).DirectoryPath;
        Directory.CreateDirectory(runs);
        foreach (var name in new[] { "active_expedition.json", "active_expedition.backup.json", "last_expedition.json", "last_expedition.json.tmp" })
            File.WriteAllText(Path.Combine(runs, name), "old expedition " + name);
        Directory.CreateDirectory(Path.Combine(reset.RootPath, "EarlierArchive"));
        File.WriteAllText(Path.Combine(reset.RootPath, "EarlierArchive", "old.json"), "prior recovery data");
    }

    [TearDown]
    public void TearDown()
    {
        Assert.That(Path.GetFullPath(fixture), Does.StartWith(Path.Combine(Path.GetTempPath(), "BombBrick.NewGame.")));
        if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
    }

    private static Dictionary<string, byte[]> Snapshot(string path) => Directory.GetFiles(path, "*", SearchOption.AllDirectories)
        .ToDictionary(file => file.Substring(path.Length + 1), File.ReadAllBytes);

    [Test]
    public void ContinueAvailabilityDoesNotCreateSavesAndIncludesRecoveryFiles()
    {
        var empty = new PlayerProfileStore(Path.Combine(fixture, "EmptyProfiles"));
        Assert.That(empty.HasSavedProgress, Is.False);
        Assert.That(Directory.Exists(empty.DirectoryPath), Is.False);
        Directory.CreateDirectory(empty.DirectoryPath);
        Assert.That(empty.HasSavedProgress, Is.False);
        foreach (string path in new[] { empty.ProfilePath, empty.BackupPath, empty.TemporaryPath,
            empty.MigrationBackupPath, new PlayerProgressReset(empty.DirectoryPath).PendingPath })
        {
            File.WriteAllText(path, "recovery data must remain untouched");
            Assert.That(empty.HasSavedProgress, Is.True, path);
            Assert.That(File.ReadAllText(path), Is.EqualTo("recovery data must remain untouched"));
            File.Delete(path);
            Assert.That(empty.HasSavedProgress, Is.False);
        }
        var before = Snapshot(reset.RootPath);
        Assert.That(profiles.HasSavedProgress, Is.True);
        AssertSnapshot(reset.RootPath, before);
    }

    private static void AssertSnapshot(string path, Dictionary<string, byte[]> expected)
    {
        Assert.That(Snapshot(path).Keys, Is.EquivalentTo(expected.Keys));
        foreach (var file in expected) Assert.That(File.ReadAllBytes(Path.Combine(path, file.Key)), Is.EqualTo(file.Value), file.Key);
    }

    private void AssertFresh()
    {
        var profile = new PlayerProfileStore(reset.RootPath).LoadOrCreate(database);
        Assert.That(profile.hunterTokens, Is.Zero);
        Assert.That(profile.permanentResearchExperience, Is.Zero);
        Assert.That(profile.firstExpeditionGuideStep, Is.Zero);
        Assert.That(profile.firstExpeditionCaptureGuideStep, Is.Zero);
        Assert.That(profile.firstExpeditionGuideInitialized, Is.True);
        Assert.That(profile.clearedRegionDifficultyKeys, Is.Empty);
        Assert.That(profile.expeditionPreset.RosterCount, Is.Zero);
        Assert.That(profile.monsters.Count, Is.GreaterThanOrEqualTo(PlayerProfileStore.ExpeditionRosterSize));
        Assert.That(new ExpeditionRunStore(profile.profileId, reset.RootPath).LoadActive(profile, database), Is.Null);
        Assert.That(Directory.Exists(Path.Combine(reset.RootPath, "Expeditions")), Is.False);
    }

    private string StageInterruptedReset(bool archived, bool committed)
    {
        string token = Guid.NewGuid().ToString("N");
        string archive = Path.Combine(reset.BackupDirectory, token);
        var fresh = new PlayerProfileStore(Path.Combine(archive, "Fresh"));
        fresh.Save(fresh.LoadOrCreate(database));
        File.WriteAllText(reset.PendingPath, token);
        if (archived) Directory.Move(reset.RootPath, Path.Combine(archive, "Previous"));
        if (committed) Directory.Move(Path.Combine(archive, "Fresh"), reset.RootPath);
        return archive;
    }

    [Test]
    public void ResetArchivesEveryByteAndStartsFirstTutorialWithoutChangingSettings()
    {
        var before = Snapshot(reset.RootPath);
        string settings = PlayerPrefs.GetString(GameUserSettings.PreferenceKey, "");
        string archive = reset.StartNewGame(database);
        AssertSnapshot(Path.Combine(archive, "Previous"), before);
        Assert.That(File.Exists(Path.Combine(archive, "RESTORE.txt")), Is.True);
        Assert.That(File.Exists(reset.PendingPath), Is.False);
        Assert.That(PlayerPrefs.GetString(GameUserSettings.PreferenceKey, ""), Is.EqualTo(settings));
        AssertFresh();
        // Corrupting the new primary must never resurrect the previous game's fallback.
        File.WriteAllText(profiles.ProfilePath, "damaged new primary");
        AssertFresh();
        AssertSnapshot(Path.Combine(archive, "Previous"), before);
    }

    [Test]
    public void BackupFailureLeavesTheEntireOldTreeUntouched()
    {
        var before = Snapshot(reset.RootPath);
        File.WriteAllText(reset.BackupDirectory, "blocked backup destination");
        Assert.Throws<IOException>(() => reset.StartNewGame(database));
        AssertSnapshot(reset.RootPath, before);
        Assert.That(File.Exists(reset.PendingPath), Is.False);
    }

    [Test]
    public void NewGamesRandomizeBasicSpeciesAndKeepTheirQualityAndLoadedStats()
    {
        var species = new HashSet<string>();
        for (int game = 0; game < 4; game++)
        {
            reset.StartNewGame(database, false);
            var profile = profiles.LoadOrCreate(database);
            Assert.That(profile.monsters, Has.Count.EqualTo(8));
            Assert.That(profile.monsters.Select(m => m.monsterId), Is.Unique);
            foreach (var basic in profile.monsters.Take(6))
            {
                species.Add(basic.monsterId);
                Assert.That(basic.isShiny, Is.False);
                Assert.That(MonsterStatQuality.OverallScore(basic),
                    Is.GreaterThanOrEqualTo(30f).And.LessThan(70f));
            }
            string[] before = profile.monsters.Select(m => JsonUtility.ToJson(m)).ToArray();
            Assert.That(profiles.LoadOrCreate(database).monsters.Select(m => JsonUtility.ToJson(m)),
                Is.EqualTo(before));
        }
        Assert.That(species.Count, Is.GreaterThan(6));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TutorialChoiceSurvivesBackupRecoveryAndStarterConfirmation(bool playTutorial)
    {
        var before = Snapshot(reset.RootPath);
        string archive = reset.StartNewGame(database, playTutorial);
        AssertSnapshot(Path.Combine(archive, "Previous"), before);
        File.WriteAllText(profiles.ProfilePath, "damaged new primary");
        var profile = profiles.LoadOrCreate(database);
        Assert.That(profiles.RecoveredFromBackup, Is.True);
        Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(playTutorial ? 0 : 8));
        Assert.That(profile.firstExpeditionCaptureGuideStep, Is.EqualTo(playTutorial ? 0 : 8));
        Assert.That(profile.starterSelectionPending, Is.True);
        profiles.RerollStarters(profile, database);
        profiles.ConfirmStarters(profile);
        profile = profiles.LoadOrCreate(database);
        Assert.That(profile.starterSelectionPending, Is.False);
        Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(playTutorial ? 0 : 8));
        Assert.That(profile.firstExpeditionCaptureGuideStep, Is.EqualTo(playTutorial ? 0 : 8));
        Assert.That(profile.firstExpeditionGuideInitialized, Is.True);
        Assert.That(profile.monsters, Has.Count.EqualTo(8));
        Assert.That(profile.expeditionPreset.RosterCount, Is.Zero);
    }

    [Test]
    public void SkippingTutorialDisablesEveryBattleGuideAndStaysCompleteAfterReturningToTown()
    {
        reset.StartNewGame(database, false);
        var profile = profiles.LoadOrCreate(database);
        var host = new GameObject("All tutorial gates regression");
        host.SetActive(false);
        try
        {
            var battle = host.AddComponent<MonsterAutoBattlePrototype>();
            var type = typeof(MonsterAutoBattlePrototype);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var run = new ExpeditionRunSave();
            type.GetField("playerProfile", flags).SetValue(battle, profile);
            type.GetField("expeditionRun", flags).SetValue(battle, run);
            var guides = new[] { "FirstAugmentGuideVisible", "FirstGrowthShopGuideVisible",
                "FirstCaptureGuideVisible", "FirstDeploymentGuideVisible", "FirstBattleStartGuideVisible",
                "FirstItemCombineGuideVisible", "FirstItemEquipGuideVisible", "FirstItemTransferGuideVisible" };
            int[] steps = { 0, 0, 1, 2, 3, 5, 6, 7 };
            for (int i = 0; i < guides.Length; i++)
            {
                run.wave = i < 5 ? 1 : 2;
                run.growthShopCompletedWave = run.wave;
                run.augmentDraftWave = run.wave;
                run.augmentDraftIds.Clear();
                if (i == 0) run.augmentDraftIds.AddRange(new[] { "a", "b", "c" });
                type.GetField("growthShopVisible", flags).SetValue(battle, i == 1);
                var gate = type.GetProperty(guides[i], flags);
                profile.firstExpeditionGuideStep = PlayerProfileStore.FirstExpeditionGuideBattleStep;
                profile.firstExpeditionCaptureGuideStep = steps[i];
                Assert.That(gate.GetValue(battle), Is.True, "Control: " + guides[i]);
                profile.firstExpeditionGuideStep = profile.firstExpeditionCaptureGuideStep = 8;
                Assert.That(gate.GetValue(battle), Is.False, "Skipped: " + guides[i]);
                Assert.That(type.GetProperty("FirstExpeditionBattleTutorialActive", flags).GetValue(battle), Is.False);
                Assert.That(type.GetProperty("FirstExpeditionCaptureGuaranteed", flags).GetValue(battle), Is.False);
                Assert.That(type.GetMethod("TryBeginFirstCaptureGuide", flags).Invoke(battle, null), Is.False);
                Assert.That(type.GetMethod("TryBeginFirstItemGuide", flags).Invoke(battle, null), Is.False);
            }
            for (int wave = 1; wave <= 12; wave++)
            {
                run.wave = wave;
                Assert.That(type.GetProperty("FirstExpeditionBattleTutorialStep", flags).GetValue(battle), Is.EqualTo(-1));
                Assert.That(MonsterAutoBattlePrototype.ShouldGuaranteeFirstTutorialItemDrop(
                    profile.firstExpeditionCaptureGuideStep, wave, 0), Is.False);
            }
            var town = host.AddComponent<TownSceneController>();
            typeof(TownSceneController).GetField("profile", flags).SetValue(town, profile);
            typeof(TownSceneController).GetField("profileStore", flags).SetValue(town, profiles);
            var reconcile = typeof(TownSceneController).GetMethod("ReconcileFirstExpeditionGuide", flags);
            reconcile.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(8));
            profile.settledExpeditionRunIds.Add("finished_without_tutorial");
            reconcile.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(8), "Town reward/shop guides must stay skipped.");
            Assert.That(profile.firstExpeditionCaptureGuideStep, Is.EqualTo(8));
            Assert.That(profile.captureNets, Is.Empty, "No tutorial-only net grant.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(8)]
    public void RewardAndShopTutorialWaitForSettlementAndPersistUntilShopOpened(int battleStep)
    {
        reset.StartNewGame(database, true);
        var profile = profiles.LoadOrCreate(database);
        profiles.ConfirmStarters(profile);
        profile.firstExpeditionGuideStep = 5;
        profile.firstExpeditionCaptureGuideStep = battleStep;
        var run = new ExpeditionRunSave { runId = "tutorial_return", regionId = HunterRegionCatalog.All[0].id,
            wave = 3, lastCompletedWave = battleStep == 0 ? 0 : 2 };
        var host = new GameObject("Post expedition tutorial regression");
        host.SetActive(false);
        try
        {
            var town = host.AddComponent<TownSceneController>();
            var type = typeof(TownSceneController);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var profileField = type.GetField("profile", flags);
            var activeField = type.GetField("activeRun", flags);
            profileField.SetValue(town, profile);
            type.GetField("profileStore", flags).SetValue(town, profiles);
            type.GetField("database", flags).SetValue(town, database);
            var reconcile = type.GetMethod("ReconcileFirstExpeditionGuide", flags);
            var setStep = type.GetMethod("SetFirstExpeditionGuideStep", flags);
            var openShop = type.GetMethod("OpenHunterShop", flags);

            activeField.SetValue(town, run);
            reconcile.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(5), "Returning during a run must not finish the tutorial.");
            activeField.SetValue(town, null);
            reconcile.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(4), "Abandoning without settlement returns to exploration start.");
            Assert.That(profile.firstExpeditionCaptureGuideStep, Is.EqualTo(battleStep));

            int earned = HunterTokenRewardSystem.Settle(profile, profiles, database, run, false);
            reconcile.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(6), "Settlement leads to tokens, not overall completion.");
            Assert.That(profile.firstExpeditionCaptureGuideStep, Is.EqualTo(8));
            profile = profiles.LoadOrCreate(database);
            profileField.SetValue(town, profile);
            reconcile.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(6), "Reload must retain the token explanation.");
            Assert.That(profile.hunterTokens, Is.EqualTo(earned));

            setStep.Invoke(town, new object[] { 7 });
            profile = profiles.LoadOrCreate(database);
            profileField.SetValue(town, profile);
            reconcile.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(7), "The shop explanation must survive reconciliation.");
            activeField.SetValue(town, run);
            openShop.Invoke(town, null);
            Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(7));
            Assert.That(type.GetField("hunterShopOpen", flags).GetValue(town), Is.False);
            activeField.SetValue(town, null);
            openShop.Invoke(town, null);
            Assert.That(type.GetField("hunterShopOpen", flags).GetValue(town), Is.True);
            Assert.That(profiles.LoadOrCreate(database).firstExpeditionGuideStep, Is.EqualTo(8));
            Assert.That(profile.hunterTokens, Is.EqualTo(earned), "Opening the shop never forces a purchase.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InterruptionBeforeCommitRestoresOldProfileAndExpeditionTogether(bool archived)
    {
        var before = Snapshot(reset.RootPath);
        StageInterruptedReset(archived, false);
        reset.RecoverPending();
        AssertSnapshot(reset.RootPath, before);
        Assert.That(profiles.LoadOrCreate(database).hunterTokens, Is.EqualTo(777));
        Assert.That(File.Exists(reset.PendingPath), Is.False);
    }

    [Test]
    public void ProfileLoadRecoversInterruptedDirectorySwapBeforeOpeningAnyProfile()
    {
        StageInterruptedReset(true, false);
        var profile = profiles.LoadOrCreate(database);
        Assert.That(profile.hunterTokens, Is.EqualTo(777));
        Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(8));
        Assert.That(File.ReadAllText(new ExpeditionRunStore(profile.profileId, reset.RootPath).ActivePath),
            Is.EqualTo("old expedition active_expedition.json"));
    }

    [Test]
    public void InterruptionAfterCommitKeepsNewGameAndPreservesOldArchive()
    {
        var before = Snapshot(reset.RootPath);
        string archive = StageInterruptedReset(true, true);
        AssertFresh();
        AssertSnapshot(Path.Combine(archive, "Previous"), before);
        Assert.That(File.Exists(reset.PendingPath), Is.False);
    }

    [Test]
    public void LockedCompletedMarkerCanBeRetriedWithoutRollingBackOrReplacingIt()
    {
        StageInterruptedReset(true, true);
        using (new FileStream(reset.PendingPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            AssertFresh();
            Assert.That(File.Exists(reset.PendingPath), Is.True);
            Assert.Throws<IOException>(() => reset.StartNewGame(database));
        }
        AssertFresh();
        Assert.That(File.Exists(reset.PendingPath), Is.False);
    }

    [Test]
    public void RepeatedResetDoesNotOverwriteEarlierRecoveryBackups()
    {
        string first = reset.StartNewGame(database);
        var before = Snapshot(first);
        string second = reset.StartNewGame(database);
        Assert.That(second, Is.Not.EqualTo(first));
        AssertSnapshot(first, before);
        AssertFresh();
    }

    [Test]
    public void InvalidMarkerFailsClosedWithoutReadingOrReplacingExistingProgress()
    {
        var before = Snapshot(reset.RootPath);
        File.WriteAllText(reset.PendingPath, "../outside");
        Assert.Throws<InvalidDataException>(() => profiles.LoadOrCreate(database));
        Assert.Throws<InvalidOperationException>(() => profiles.Save(new PlayerProfileSave()));
        AssertSnapshot(reset.RootPath, before);
    }

    [Test]
    public void ConflictingFoldersArePreservedInsteadOfGuessingWhichToReplace()
    {
        string archive = StageInterruptedReset(true, false);
        Directory.CreateDirectory(reset.RootPath);
        File.WriteAllText(Path.Combine(reset.RootPath, "unexpected.json"), "preserve me");
        var before = Snapshot(archive);
        Assert.Throws<InvalidDataException>(() => reset.RecoverPending());
        AssertSnapshot(archive, before);
        Assert.That(File.ReadAllText(Path.Combine(reset.RootPath, "unexpected.json")), Is.EqualTo("preserve me"));
    }
}
