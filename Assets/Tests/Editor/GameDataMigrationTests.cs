using System;
using System.IO;
using BombBrick.Game.Core;
using NUnit.Framework;

public sealed class GameDataMigrationTests
{
    [Test]
    public void ExplicitQaProfileIsSeparateAndNeverImportsLegacyProgress()
    {
        Assert.That(GameDataPaths.QaProfileRoot(new[] { "game.exe" }, fixture), Is.Null);
        string qa = GameDataPaths.QaProfileRoot(new[] { "game.exe", "-qa-profile", "SR14_01" }, fixture);
        Assert.That(qa, Is.EqualTo(Path.Combine(fixture, "QAData", "SR14_01", "UserData")));
        WriteLegacy("Profiles/player_profile.json", "real progress");
        GameDataPaths.MigrateProfiles(null, qa);
        Assert.That(Directory.GetFileSystemEntries(qa), Is.Empty);
        Assert.That(File.ReadAllText(Path.Combine(legacy, "Profiles/player_profile.json")), Is.EqualTo("real progress"));
        Assert.Throws<ArgumentException>(() => GameDataPaths.QaProfileRoot(new[] { "-qa-profile" }, fixture));
        Assert.Throws<ArgumentException>(() => GameDataPaths.QaProfileRoot(new[] { "-qa-profile", "ok", "-qa-profile", "other" }, fixture));
        foreach (string invalid in new[] { "", "../Profiles", "..\\Profiles", "C:\\Profiles", "a/b", "a:b", new string('a', 49) })
            Assert.Throws<ArgumentException>(() => GameDataPaths.QaProfileRoot(new[] { "-qa-profile", invalid }, fixture));
    }

    private string fixture;
    private string legacy;
    private string destination;

    [SetUp]
    public void SetUp()
    {
        fixture = Path.Combine(Path.GetTempPath(), "WildCrewMigration-" + Guid.NewGuid().ToString("N"));
        legacy = Path.Combine(fixture, "Legacy");
        destination = Path.Combine(fixture, "New", "UserData");
        Directory.CreateDirectory(legacy);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(fixture, true);

    private void WriteLegacy(string relative, string text)
    {
        string path = Path.Combine(legacy, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }

    [Test]
    public void CopiesProfileExpeditionAndResetRecoveryTogetherLeavingOriginalsIntact()
    {
        string[] paths = {
            "Profiles/player_profile.json", "Profiles/player_profile.backup.json",
            "Profiles/Expeditions/default/active_expedition.json",
            "Profiles.backups/archive/Previous/player_profile.json",
            "Profiles.backups/archive/Fresh/player_profile.json", "Profiles.reset", "Profiles.reset.tmp",
            "Profiles.reset_backup_20260824/player_profile.json"
        };
        foreach (string path in paths) WriteLegacy(path, "preserve:" + path);
        WriteLegacy("Player.log", "not save data");
        GameDataPaths.MigrateProfiles(legacy, destination);
        foreach (string path in paths)
        {
            Assert.That(File.ReadAllText(Path.Combine(destination, path)), Is.EqualTo("preserve:" + path));
            Assert.That(File.ReadAllText(Path.Combine(legacy, path)), Is.EqualTo("preserve:" + path));
        }
        Assert.That(File.Exists(Path.Combine(destination, "Player.log")), Is.False);
    }

    [Test]
    public void ExistingNewDataWinsAndDoesNotReimportAfterNewGame()
    {
        WriteLegacy("Profiles/player_profile.json", "old progression");
        Directory.CreateDirectory(destination);
        GameDataPaths.MigrateProfiles(legacy, destination);
        Assert.That(Directory.GetFileSystemEntries(destination), Is.Empty);
        Directory.CreateDirectory(Path.Combine(destination, "Profiles"));
        File.WriteAllText(Path.Combine(destination, "Profiles/player_profile.json"), "new progression");
        GameDataPaths.MigrateProfiles(legacy, destination);
        Assert.That(File.ReadAllText(Path.Combine(destination, "Profiles/player_profile.json")), Is.EqualTo("new progression"));
    }

    [Test]
    public void MissingLegacyIsAValidFirstLaunch()
    {
        GameDataPaths.MigrateProfiles(Path.Combine(fixture, "Missing"), destination);
        Assert.That(Directory.Exists(destination), Is.True);
        Assert.That(Directory.GetFileSystemEntries(destination), Is.Empty);
    }

    [Test]
    public void InterruptedCopyCannotCommitPartialDataAndCanRetry()
    {
        WriteLegacy("Profiles/player_profile.json", "original");
        string source = Path.Combine(legacy, "Profiles/player_profile.json");
        using (File.Open(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => GameDataPaths.MigrateProfiles(legacy, destination));
        Assert.That(Directory.Exists(destination), Is.False);
        Assert.That(Directory.GetDirectories(Path.GetDirectoryName(destination), "*.migrating-*"), Is.Empty);
        GameDataPaths.MigrateProfiles(legacy, destination);
        Assert.That(File.ReadAllText(Path.Combine(destination, "Profiles/player_profile.json")), Is.EqualTo("original"));
    }

    [Test]
    public void BlockedLegacyDoesNotCreateEmptySave()
    {
        string blocked = Path.Combine(fixture, "Blocked");
        File.WriteAllText(blocked, "not a directory");
        Assert.Throws<IOException>(() => GameDataPaths.MigrateProfiles(blocked, destination));
        Assert.That(Directory.Exists(destination), Is.False);
    }

    [Test]
    public void BlockedDestinationIsNeverOverwritten()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.WriteAllText(destination, "keep");
        Assert.Throws<IOException>(() => GameDataPaths.MigrateProfiles(legacy, destination));
        Assert.That(File.ReadAllText(destination), Is.EqualTo("keep"));
    }

    [Test]
    public void InterruptedResetStillRecoversWholePreviousProfileAfterMigration()
    {
        string token = Guid.NewGuid().ToString("N");
        WriteLegacy("Profiles.backups/" + token + "/Previous/player_profile.json", "old");
        WriteLegacy("Profiles.backups/" + token + "/Fresh/player_profile.json", "fresh");
        WriteLegacy("Profiles.reset", token);
        GameDataPaths.MigrateProfiles(legacy, destination);
        new PlayerProgressReset(Path.Combine(destination, "Profiles")).RecoverPending();
        Assert.That(File.ReadAllText(Path.Combine(destination, "Profiles/player_profile.json")), Is.EqualTo("old"));
        Assert.That(File.ReadAllText(Path.Combine(destination, "Profiles.backups/" + token + "/Fresh/player_profile.json")), Is.EqualTo("fresh"));
        Assert.That(File.ReadAllText(Path.Combine(legacy, "Profiles.backups/" + token + "/Previous/player_profile.json")), Is.EqualTo("old"));
        Assert.That(File.Exists(Path.Combine(destination, "Profiles.reset")), Is.False);
    }
}
