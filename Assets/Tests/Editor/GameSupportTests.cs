using System;
using System.IO;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class GameSupportTests
{
    [Test]
    public void ContactUsesConfirmedAddressAndOnlyAnEscapedSubject()
    {
        Assert.That(GameSupport.ContactEmail, Is.EqualTo("codending@gmail.com"));
        Assert.That(GameSupport.EmailUrl, Does.StartWith("mailto:codending@gmail.com?subject="));
        Assert.That(Uri.UnescapeDataString(GameSupport.EmailUrl), Does.EndWith("BecomeZ: Wild Crew v" + Application.version));
        Assert.That(GameSupport.EmailUrl, Does.Not.Contain("&body="));
    }

    [Test]
    public void DiagnosticsUseCurrentVersionWithoutPrivateIdentifiersOrFiles()
    {
        string saved = PlayerPrefs.GetString(GameUserSettings.PreferenceKey, "");
        string report = GameSupport.DiagnosticInformation();
        Assert.That(report, Does.Contain("Version: " + Application.version));
        Assert.That(report, Does.Contain("Display: " + Screen.width + "x" + Screen.height));
        Assert.That(report, Does.Contain("Language: " + GameLocalization.CurrentLanguage));
        Assert.That(report.Split('\n').Length, Is.EqualTo(9));
        Assert.That(report, Does.Not.Contain(Application.persistentDataPath));
        Assert.That(report, Does.Not.Contain(GameDataPaths.Root));
        if (!string.IsNullOrEmpty(Application.consoleLogPath))
            Assert.That(report, Does.Not.Contain(Application.consoleLogPath));
        Assert.That(report, Does.Not.Contain(SystemInfo.deviceUniqueIdentifier));
        Assert.That(PlayerPrefs.GetString(GameUserSettings.PreferenceKey, ""), Is.EqualTo(saved));
    }

    [Test]
    public void LogPathHandlesDisabledAndCustomLogsWithoutCreatingFolders()
    {
        Assert.That(GameSupport.LogDirectory(null), Is.Empty);
        Assert.That(GameSupport.LogDirectory("  "), Is.Empty);
        Assert.That(GameSupport.LogDirectory("bad\0path"), Is.Empty);
        string directory = Path.Combine(Path.GetTempPath(), "지원 로그 " + Guid.NewGuid().ToString("N"));
        Assert.That(GameSupport.LogDirectory(Path.Combine(directory, "Player.log")), Is.EqualTo(directory));
        Assert.That(Directory.Exists(directory), Is.False);
        Assert.That(GameSupport.LogDirectory("Player.log"), Is.EqualTo(Directory.GetCurrentDirectory()));
    }
}
