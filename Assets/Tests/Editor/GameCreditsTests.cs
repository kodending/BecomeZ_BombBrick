using System.IO;
using System.Text.RegularExpressions;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class GameCreditsTests
{
    [Test]
    public void UnusedSamplesStayOutOfRuntimeResources()
    {
        Assert.That(TMPro.TMP_Settings.defaultSpriteAsset, Is.Null);
        Assert.That(Resources.Load<TMPro.TMP_SpriteAsset>("Sprite Assets/EmojiOne"), Is.Null);
        Assert.That(Resources.Load<AudioClip>("Audio/Combat/Bear/roarend"), Is.Null);
        Assert.That(Resources.Load<AudioClip>("Audio/Combat/Bear/departure"), Is.Null);
    }

    [Test]
    public void ShippingNoticesPreserveFontLicensesAndIndividualIconCredits()
    {
        for (int i = 0; i < GameCredits.NoticeFiles.Count; i++)
            Assert.That(GameCredits.ReadNotice(i).Length, Is.GreaterThan(200), GameCredits.NoticeFiles[i]);
        Assert.That(GameCredits.ReadNotice(3), Is.EqualTo(File.ReadAllText("Assets/Resources/UI/Fonts/Galmuri-OFL.txt")));
        Assert.That(GameCredits.ReadNotice(4), Is.EqualTo(File.ReadAllText("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt")));
        foreach (Match link in Regex.Matches(File.ReadAllText("Assets/Art/UI/CombatStatusIcons/ATTRIBUTION.md"), @"https://game-icons.net/1x1/[^)]+"))
            Assert.That(GameCredits.ReadNotice(0), Does.Contain(link.Value));
        Assert.That(GameCredits.ReadNotice(0), Does.Contain("Changes:"));
    }

    [Test]
    public void CreditsUiHasEnglishWithoutChangingTheSavedLanguage()
    {
        string language = GameLocalization.CurrentLanguage;
        string saved = PlayerPrefs.GetString(GameUserSettings.PreferenceKey, "");
        try
        {
            GameLocalization.Reload();
            GameLocalization.SetLanguage(GameLocalization.English);
            foreach (string text in new[] { "크레딧", "제작진", "함께 만든 사람들과 사용 자산", "개발 스튜디오",
                "기획 · 개발", "마스코트", "춘동이", "함께 모험해 주셔서 감사합니다.",
                GameCredits.ChundongiMemorialTitle, GameCredits.ChundongiMemorialBody,
                "춘동이가 동료가 되었습니다", "평균 S급 · 춘동이",
                "이제 새 게임을 시작해도 춘동이가 늘 함께합니다.", "함께해 줘서 고마워",
                "춘동이는 이미 당신과 함께하고 있습니다.",
                "고지 파일을 불러오지 못했습니다. 게임 파일 무결성을 확인해 주세요." })
                Assert.That(Regex.IsMatch(GameLocalization.Text(text), "[가-힣]"), Is.False, text);
            foreach (string title in GameCredits.NoticeTitles)
                Assert.That(Regex.IsMatch(GameLocalization.Text(title), "[가-힣]"), Is.False, title);
            Assert.That(PlayerPrefs.GetString(GameUserSettings.PreferenceKey, ""), Is.EqualTo(saved));
        }
        finally { GameLocalization.SetLanguage(language); }
    }

    [Test]
    public void MemorialPreservesArrivalYearAndFarewellDateInBothLanguages()
    {
        Assert.That(GameCredits.ChundongiMemorialBody, Does.Contain("2019년 저의 품에 들어온"));
        Assert.That(GameCredits.ChundongiMemorialBody, Does.Contain("2025년 12월 15일"));
        string language = GameLocalization.CurrentLanguage;
        try
        {
            GameLocalization.Reload();
            GameLocalization.SetLanguage(GameLocalization.English);
            string body = GameLocalization.Text(GameCredits.ChundongiMemorialBody);
            Assert.That(body, Does.Contain("came into my arms in 2019"));
            Assert.That(body, Does.Contain("December 15, 2025"));
            Assert.That(body, Does.Contain("both in this game and in heaven"));
        }
        finally { GameLocalization.SetLanguage(language); }
    }
}
