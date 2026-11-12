using System.Text.RegularExpressions;
using BombBrick.Game.Core;
using NUnit.Framework;

public sealed class GameHelpTests
{
    [Test]
    public void AllHelpTopicsAndInstructionsHaveEnglishTranslations()
    {
        string previous = GameLocalization.CurrentLanguage;
        try
        {
            GameLocalization.Reload();
            GameLocalization.SetLanguage(GameLocalization.English);
            foreach (var page in GameHelp.Pages)
            {
                AssertTranslated(page.Title);
                foreach (string step in page.Steps) AssertTranslated(step);
            }
            AssertTranslated("게임 도움말");
            AssertTranslated("진행 기록을 바꾸지 않고 기본 조작과 탐험 흐름을 다시 확인하세요.");
            AssertTranslated("이전");
            AssertTranslated("다음");
        }
        finally { GameLocalization.SetLanguage(previous); }
    }

    [Test]
    public void StartingDrawIsTheFirstHelpTopic()
    {
        var page = GameHelp.Pages[0];
        Assert.That(page.Title, Is.EqualTo("시작 동료 뽑기"));
        Assert.That(page.Steps.Count, Is.EqualTo(3));
        Assert.That(page.Steps[0], Does.Contain("기본 동료 6마리").And.Contain("추가 동료 2마리"));
        Assert.That(page.Steps[1], Does.Contain("8마리").And.Contain("성장 특성").And.Contain("이로치가 나오지 않습니다"));
        Assert.That(page.Steps[2], Does.Contain("동료를 확정하면 다시 뽑기는 끝나고"));
    }

    [Test]
    public void StartingDrawGuideAndRerollButtonHaveEnglishTranslations()
    {
        string previous = GameLocalization.CurrentLanguage;
        try
        {
            GameLocalization.Reload();
            GameLocalization.SetLanguage(GameLocalization.Korean);
            Assert.That(GameLocalization.Text("전체 다시 뽑기"), Is.EqualTo("전체 다시 뽑기"));
            GameLocalization.SetLanguage(GameLocalization.English);
            Assert.That(GameLocalization.Text("전체 다시 뽑기"), Is.EqualTo("Reroll All"));
            AssertTranslated("탐험을 함께할 동료 8마리를 먼저 뽑습니다.\n기본 6마리는 평균 B−~B+·이로치 없음, 추가 2마리는 능력치·이로치가 무작위입니다.");
            AssertTranslated("전체 다시 뽑기를 누르면 8마리 모두 다시 뽑습니다.\n종류·능력치·성격·성장 특성이 바뀌니 원하는 조합을 찾아보세요.");
            AssertTranslated("원하는 동료를 골랐다면 동료 확정을 누르세요.\n확정한 성장 특성은 유지되며, 확정 후에는 다시 뽑을 수 없습니다.");
        }
        finally { GameLocalization.SetLanguage(previous); }
    }

    private static void AssertTranslated(string source)
    {
        string translated = GameLocalization.Text(source);
        Assert.That(translated, Is.Not.Empty);
        Assert.That(Regex.IsMatch(translated, "[가-힣]"), Is.False, source);
    }
}
