using System.Reflection;
using BombBrick.Game.Scenes;
using NUnit.Framework;
using UnityEngine;

public sealed class StarterSelectionTutorialTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private TownSceneController town;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Starter tutorial test");
        host.SetActive(false);
        town = host.AddComponent<TownSceneController>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [TestCase(true, 0, 0, true, false)]
    [TestCase(true, 0, 1, true, false)]
    [TestCase(true, 0, 2, true, false)]
    [TestCase(true, 0, 3, false, true)]
    [TestCase(false, 0, 0, false, true)]
    [TestCase(true, 8, 0, false, true)]
    [TestCase(true, 1, 0, false, true)]
    public void StarterGuideOnlyGatesNewTutorialBeforeConfirmation(
        bool pending, int savedStep, int introductionStep, bool guideActive, bool buttonsEnabled)
    {
        var profile = new PlayerProfileSave { starterSelectionPending = pending, firstExpeditionGuideStep = savedStep };
        Set("profile", profile);
        Set("starterGuideStep", introductionStep);
        Assert.That(GetProperty("StarterGuideActive"), Is.EqualTo(guideActive));
        Assert.That(GetProperty("StarterSelectionButtonsEnabled"), Is.EqualTo(buttonsEnabled));
        Assert.That(profile.firstExpeditionGuideStep, Is.EqualTo(savedStep));
    }

    [Test]
    public void MissingProfileDoesNotShowOrBlockStarterGuide()
    {
        Assert.That(GetProperty("StarterGuideActive"), Is.False);
        Assert.That(GetProperty("StarterSelectionButtonsEnabled"), Is.True);
    }

    [Test]
    public void RerollRevealKeepsCompletedStarterIntroduction()
    {
        var profile = new PlayerProfileSave { starterSelectionPending = true, firstExpeditionGuideStep = 0 };
        for (int i = 0; i < 8; i++)
        {
            string id = "starter_" + i;
            profile.monsters.Add(new OwnedMonsterSave { instanceId = id, monsterId = "velos" });
            if (i >= 6) profile.starterRerollInstanceIds.Add(id);
        }
        Set("profile", profile);
        Set("starterGuideStep", 3);
        typeof(TownSceneController).GetMethod("BeginStarterReveal", Private).Invoke(town, null);
        Assert.That(typeof(TownSceneController).GetField("starterGuideStep", Private).GetValue(town), Is.EqualTo(3));
        Assert.That(GetProperty("StarterGuideActive"), Is.False);
        Assert.That(GetProperty("StarterSelectionButtonsEnabled"), Is.True);
        Assert.That(profile.firstExpeditionGuideStep, Is.Zero);
    }

    [Test]
    public void ThirdNextClosesOnlyTheIntroductionAndLeavesTheCrewUnconfirmed()
    {
        var profile = new PlayerProfileSave { starterSelectionPending = true, firstExpeditionGuideStep = 0 };
        profile.monsters.Add(new OwnedMonsterSave { instanceId = "chosen_starter", monsterId = "velos" });
        string draft = JsonUtility.ToJson(profile);
        Set("profile", profile);
        var next = typeof(TownSceneController).GetMethod("AdvanceStarterGuide", Private);
        for (int step = 0; step < 3; step++)
        {
            Assert.That(GetProperty("StarterGuideActive"), Is.True);
            Assert.That(GetProperty("StarterSelectionButtonsEnabled"), Is.False);
            next.Invoke(town, null);
            Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(draft));
        }
        Assert.That(GetProperty("StarterGuideActive"), Is.False);
        Assert.That(GetProperty("StarterSelectionButtonsEnabled"), Is.True);
        Assert.That(profile.starterSelectionPending, Is.True);
        Assert.That(profile.firstExpeditionGuideStep, Is.Zero);
        next.Invoke(town, null);
        Assert.That(typeof(TownSceneController).GetField("starterGuideStep", Private).GetValue(town), Is.EqualTo(3));
    }

    private void Set(string name, object value) => typeof(TownSceneController).GetField(name, Private).SetValue(town, value);
    private bool GetProperty(string name) => (bool)typeof(TownSceneController).GetProperty(name, Private).GetValue(town, null);
}
