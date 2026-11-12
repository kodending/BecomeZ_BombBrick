using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PreparationShopEscapeTests
{
    private GameObject host;
    private MonsterAutoBattlePrototype battle;
    private ExpeditionRunSave run;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Shop Escape Test");
        host.SetActive(false);
        battle = host.AddComponent<MonsterAutoBattlePrototype>();
        run = new ExpeditionRunSave { wave = 1, expeditionCurrency = 20 };
        run.monsters.Add(new ExpeditionMonsterStateSave { instanceId = "escape_test", monsterId = "velos" });
        Set("expeditionRun", run);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [Test]
    public void NoOpenShopLeavesEscapeForTheMenu()
    {
        Assert.That(battle.TryClosePreparationShop(), Is.False);
    }

    [TestCase("shopVisible", "shopClosing")]
    [TestCase("growthShopVisible", "growthShopClosing")]
    public void ClosingShopStillConsumesEscapeWithoutDoubleClosing(string visible, string closing)
    {
        Set(visible, true);
        Set(closing, true);
        Set("draggingItem", true);
        Assert.That(battle.TryClosePreparationShop(), Is.True);
        Assert.That(battle.TryClosePreparationShop(), Is.True);
        Assert.That(Get<bool>("draggingItem"), Is.False);
        Assert.That(Get<bool>(closing), Is.True);
        Assert.That(run.expeditionCurrency, Is.EqualTo(20));
    }

    [Test]
    public void RequiredFreeGrowthConsumesEscapeWithoutSkippingTutorial()
    {
        Set("growthShopVisible", true);
        Set("playerProfile", new PlayerProfileSave {
            firstExpeditionGuideStep = PlayerProfileStore.FirstExpeditionGuideBattleStep,
            firstExpeditionCaptureGuideStep = 0
        });
        Assert.That(battle.TryClosePreparationShop(), Is.True);
        Assert.That(Get<bool>("growthShopClosing"), Is.False);
        Assert.That(Get<string>("itemUiMessage"), Is.EqualTo("무료 성장을 먼저 선택하세요."));
        Assert.That(run.growthShopCompletedWave, Is.Not.EqualTo(run.wave));
        Assert.That(run.growthShopFreePurchaseUsed, Is.False);
    }

    private void Set(string name, object value) => typeof(MonsterAutoBattlePrototype).GetField(name, Private).SetValue(battle, value);
    private T Get<T>(string name) => (T)typeof(MonsterAutoBattlePrototype).GetField(name, Private).GetValue(battle);
}
