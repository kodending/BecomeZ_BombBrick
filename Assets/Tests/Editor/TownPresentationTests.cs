using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using BombBrick.Game.Scenes;
using NUnit.Framework;
using UnityEngine;

public sealed class TownPresentationTests
{
    [TestCase(0f, 360f, 24, 6, 0, 12)]
    [TestCase(180f, 360f, 24, 6, 0, 18)]
    [TestCase(186f, 360f, 24, 6, 6, 18)]
    [TestCase(558f, 360f, 25, 6, 18, 25)]
    [TestCase(0f, 360f, 10000, 6, 0, 12)]
    [TestCase(0f, 360f, 0, 6, 0, 0)]
    public void CollectionProcessesOnlyVisibleRows(float scroll, float height, int count,
        int columns, int first, int end)
    {
        Assert.That(TownSceneController.VisibleCollectionRange(scroll, height, count, columns, 186f),
            Is.EqualTo(new Vector2Int(first, end)));
    }

    [Test]
    public void CollectionCacheRefreshesAfterPurchaseFormationExchangeAndProfileReload()
    {
        var go = new GameObject("Town cache test");
        var db = ScriptableObject.CreateInstance<MonsterDatabase>();
        var monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        try
        {
            monster.monsterId = "velos";
            db.monsters.Add(monster);
            var town = go.AddComponent<TownSceneController>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(TownSceneController);
            type.GetField("database", flags).SetValue(town, db);
            var profile = new PlayerProfileSave { lastSavedUtcTicks = 1,
                monsters = new List<OwnedMonsterSave> { new OwnedMonsterSave { instanceId = "a", monsterId = "velos" } },
                expeditionPreset = new ExpeditionPresetSave() };
            type.GetField("profile", flags).SetValue(town, profile);
            var method = type.GetMethod("CollectionMonsters", flags);
            var first = (List<OwnedMonsterSave>)method.Invoke(town, null);
            Assert.That(method.Invoke(town, null), Is.SameAs(first));
            profile.monsters.Add(new OwnedMonsterSave { instanceId = "b", monsterId = "velos" });
            profile.lastSavedUtcTicks++;
            Assert.That((List<OwnedMonsterSave>)method.Invoke(town, null), Has.Count.EqualTo(2));
            profile.expeditionPreset.rosterInstanceIds.Add("a");
            profile.lastSavedUtcTicks++;
            Assert.That((List<OwnedMonsterSave>)method.Invoke(town, null), Has.Count.EqualTo(1));
            profile.monsters.RemoveAt(1);
            profile.lastSavedUtcTicks++;
            Assert.That((List<OwnedMonsterSave>)method.Invoke(town, null), Is.Empty);
            type.GetField("profile", flags).SetValue(town, new PlayerProfileSave {
                lastSavedUtcTicks = 4, monsters = first, expeditionPreset = new ExpeditionPresetSave() });
            Assert.That((List<OwnedMonsterSave>)method.Invoke(town, null), Has.Count.EqualTo(1));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(db); Object.DestroyImmediate(monster); }
    }

    [Test]
    public void StarterRevealFinishesLeftBeforeRightAndRerollCreatesFreshPresentation()
    {
        var go = new GameObject("Starter reveal test");
        try
        {
            var town = go.AddComponent<TownSceneController>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(TownSceneController);
            var a = ChestRewardPresentationTests.MonsterWithScore(95f); a.instanceId = "a";
            var b = ChestRewardPresentationTests.MonsterWithScore(50f); b.instanceId = "b";
            var profile = new PlayerProfileSave { monsters = new List<OwnedMonsterSave> { a, b },
                starterRerollInstanceIds = new List<string> { "a", "b" } };
            type.GetField("profile", flags).SetValue(town, profile);
            var begin = type.GetMethod("BeginStarterReveal", flags);
            var field = type.GetField("starterReveal", flags);
            begin.Invoke(town, null);
            var reveal = (ChestRewardPresentation)field.GetValue(town);
            Assert.That(reveal.RevealAt(1) - reveal.RevealAt(0),
                Is.EqualTo(MonsterAcquisitionReveal.ReadyAt+.25f+ChestRewardPresentation.RevealSpacing).Within(.001f));
            var replacement = ChestRewardPresentationTests.MonsterWithScore(50f); replacement.instanceId = "c";
            profile.monsters[0] = replacement;
            profile.starterRerollInstanceIds[0] = "c";
            begin.Invoke(town, null);
            var reroll = (ChestRewardPresentation)field.GetValue(town);
            Assert.That(reroll, Is.Not.SameAs(reveal));
            Assert.That(reroll.RevealAt(1) - reroll.RevealAt(0),
                Is.EqualTo(MonsterAcquisitionReveal.ReadyAt+ChestRewardPresentation.RevealSpacing).Within(.001f));
        }
        finally { Object.DestroyImmediate(go); }
    }
}
