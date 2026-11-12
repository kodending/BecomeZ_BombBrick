using System.Reflection;
using System.Text.RegularExpressions;
using BombBrick.Game.Scenes;
using NUnit.Framework;

public sealed class RegionAffinityTooltipTests
{
    private string previousLanguage;

    [SetUp]
    public void SetUp()
    {
        previousLanguage = GameLocalization.CurrentLanguage;
        GameLocalization.Reload();
    }

    [TearDown]
    public void TearDown() => GameLocalization.SetLanguage(previousLanguage);

    [TestCase("ko", 1.2f, "현재 지역에서 주는 피해가 20% 증가합니다.")]
    [TestCase("ko", 1f, "현재 지역에서 주는 피해는 변하지 않습니다.")]
    [TestCase("ko", 0.8f, "현재 지역에서 주는 피해가 20% 감소합니다.")]
    [TestCase("ko", 1.35f, "현재 지역에서 주는 피해가 35% 증가합니다.")]
    [TestCase("ko", 0.95f, "현재 지역에서 주는 피해가 5% 감소합니다.")]
    [TestCase("en", 1.2f, "Deals 20% more damage in the current region.")]
    [TestCase("en", 1f, "Damage dealt is unchanged in the current region.")]
    [TestCase("en", 0.8f, "Deals 20% less damage in the current region.")]
    [TestCase("en", 1.35f, "Deals 35% more damage in the current region.")]
    [TestCase("en", 0.95f, "Deals 5% less damage in the current region.")]
    public void AffinityDescriptionExplainsOutgoingDamageInSelectedLanguage(
        string language, float multiplier, string expected)
    {
        GameLocalization.SetLanguage(language);
        MethodInfo method = typeof(TownSceneController).GetMethod(
            "RegionAffinityTooltipDescription", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { multiplier }), Is.EqualTo(expected));
    }

    [Test]
    public void AffinityCategoryHasEnglishTranslation()
    {
        GameLocalization.SetLanguage(GameLocalization.English);
        string title = GameLocalization.Text("지역 상성");
        Assert.That(title, Is.EqualTo("Region Affinity"));
        Assert.That(Regex.IsMatch(title, "[가-힣]"), Is.False);
    }
}
