using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public sealed class EnglishUiPresentationTests
{
    private string previousLanguage;

    [SetUp]
    public void UseEnglish()
    {
        previousLanguage=GameLocalization.CurrentLanguage;
        GameLocalization.SetLanguage(GameLocalization.English);
    }

    [TearDown]
    public void RestoreLanguage() => GameLocalization.SetLanguage(previousLanguage);

    [TestCase(1280,720)]
    [TestCase(1920,1080)]
    [TestCase(2560,1080)]
    public void LongLocalizedTooltipUsesItsFullBodyHeight(int width,int height)
    {
        var database=JsonUtility.FromJson<LocalizationDatabaseFile>(
            Resources.Load<TextAsset>(GameLocalization.ResourcePath).text);
        LocalizationEntry entry=database.entries.First(row=>row.enabled&&
            row.category=="TraitDescription"&&row.en.Length>300&&
            !row.ko.Contains("{")&&!row.en.Contains("{"));
        string translated=GameLocalization.Text(entry.ko);
        Assert.That(Regex.IsMatch(translated,"[가-힣]"),Is.False);
        var style=new GUIStyle
        {
            font=Resources.Load<Font>("UI/Fonts/Galmuri11-Bold"),
            fontSize=14,
            wordWrap=true
        };
        Assert.That(style.font,Is.Not.Null);
        Rect safe=BombBrickUiLayout.ForScreen(width,height,
            new Rect(0,0,width,height)).safeArea;
        var method=typeof(BombBrickUiTheme).GetMethod("HoverTooltipRect",
            BindingFlags.NonPublic|BindingFlags.Static);
        Assert.That(method,Is.Not.Null);
        Rect panel=(Rect)method.Invoke(null,new object[]{
            safe.max-Vector2.one,entry.ko,style,safe});
        float required=style.CalcHeight(new GUIContent(translated),panel.width-32f);
        Assert.That(required,Is.GreaterThan(108f),"Exercise a multi-paragraph tooltip.");
        Assert.That(panel.height-78f,Is.GreaterThanOrEqualTo(required));
        Assert.That(LocalizedGUI.FittedFontSize(new Rect(0,0,panel.width-32f,
            panel.height-78f),translated,style),Is.EqualTo(14));
        Assert.That(panel.xMin,Is.GreaterThanOrEqualTo(safe.xMin));
        Assert.That(panel.yMin,Is.GreaterThanOrEqualTo(safe.yMin));
        Assert.That(panel.xMax,Is.LessThanOrEqualTo(safe.xMax));
        Assert.That(panel.yMax,Is.LessThanOrEqualTo(safe.yMax));
        Assert.That(style.fontSize,Is.EqualTo(14));
    }

    [TestCase("몬스터를 드래그해 15칸 배치 구역에 전술을 구성하세요.")]
    [TestCase("좌측 전투 데이터 탭에서 기록을 확인할 수 있습니다.")]
    [TestCase("전투 진행")]
    [TestCase("로딩중...")]
    [TestCase("(임시)")]
    [TestCase("보유 효과")]
    [TestCase("신규 효과")]
    [TestCase("기초 강화")]
    [TestCase("스킬 전술")]
    [TestCase("생존 전술")]
    [TestCase("처치 연계")]
    [TestCase("공격 전술")]
    [TestCase("특성 발동")]
    public void ReleaseUiLabelsHaveEnglishText(string source)
    {
        Assert.That(Regex.IsMatch(GameLocalization.Text(source),"[가-힣]"),Is.False,source);
    }

    [TestCase(GameLocalization.Korean,"4등급 특성")]
    [TestCase(GameLocalization.English,"Rank 4 Trait")]
    public void TranscendenceTooltipsShowTheirActualRank(string language,string expected)
    {
        GameLocalization.SetLanguage(language);
        var traits=GameMasterData.Database.traits.Where(row=>row!=null&&row.enabled&&
            row.category=="Rank4Transcendence").ToArray();
        Assert.That(traits,Is.Not.Empty);
        foreach(TraitMasterRow trait in traits)
            Assert.That(GameLocalization.Text(MonsterAbilityTooltipContent.TraitCategory(trait)),
                Is.EqualTo(expected),trait.id);
        Assert.That(MonsterAbilityTooltipContent.TraitCategory(
            new TraitMasterRow{category="Rank2Enhancement"}),Is.EqualTo("2등급 특성"));
        Assert.That(MonsterAbilityTooltipContent.TraitCategory(
            new TraitMasterRow{category="Rank3Enhancement"}),Is.EqualTo("3등급 특성"));
    }
}
