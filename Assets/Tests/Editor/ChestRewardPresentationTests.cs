using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public sealed class ChestRewardPresentationTests
{
    [Test]
    public void HiddenRewardKeepsEveryBeatUntilItsFirstPresentationAndStartsOnlyOnce()
    {
        var monster = MonsterWithScore(95f);
        string before = JsonUtility.ToJson(monster);
        var view = new ChestRewardPresentation(new List<OwnedMonsterSave> { monster, monster }, true, false, "prototype_region", null);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var start = typeof(ChestRewardPresentation).GetField("startedAt", hidden);
        for (int i = 0; i < 3; i++) view.Update();
        Assert.That(float.IsPositiveInfinity((float)start.GetValue(view)), Is.True);
        Assert.That(view.Elapsed, Is.Zero);
        Assert.That(typeof(ChestRewardPresentation).GetField("soundMask", hidden).GetValue(view), Is.EqualTo(0));
        Assert.That(typeof(ChestRewardPresentation).GetField("statSoundMasks", hidden).GetValue(view), Is.EqualTo(new[] { 0, 0 }));
        view.BeginPresentation();
        view.Update();
        Assert.That(view.Elapsed, Is.Zero);
        Assert.That(float.IsPositiveInfinity((float)start.GetValue(view)), Is.True, "The first paint must complete before timing begins.");
        if (Time.frameCount > 0)
        {
            typeof(ChestRewardPresentation).GetField("presentedFrame", hidden).SetValue(view, Time.frameCount - 1);
            view.Update();
            Assert.That(start.GetValue(view), Is.EqualTo(Time.unscaledTime));
            Assert.That(view.Elapsed, Is.Zero);
        }
        start.SetValue(view, Time.unscaledTime - .5f);
        view.BeginPresentation();
        Assert.That(view.Elapsed, Is.EqualTo(.5f).Within(.001f), "Later repaints must not restart an active presentation.");
        view.Update();
        Assert.That((int)typeof(ChestRewardPresentation).GetField("soundMask", hidden).GetValue(view), Is.EqualTo(3));
        Assert.That(JsonUtility.ToJson(monster), Is.EqualTo(before));
    }

    [Test]
    public void RevealImpulseMatchesSoundAndSettlesBeforeSelection()
    {
        Assert.That(ChestRewardPresentation.RevealKick(.17f),Is.Zero);
        Assert.That(ChestRewardPresentation.RevealKick(ChestRewardPresentation.RevealImpactAt),Is.EqualTo(1f));
        Assert.That(ChestRewardPresentation.RevealKick(.3f),Is.InRange(0f,1f));
        Assert.That(ChestRewardPresentation.RevealKick(.65f),Is.Zero);
        Assert.That(ChestRewardPresentation.RevealKick(10f),Is.Zero);
    }

    [Test]
    public void OpeningAtlasKeepsSixtyFourPixelsPerFrame()
    {
        const string path = "Assets/Resources/UI/HunterShop/chest_opening_sheet.png";
        Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.That(sheet, Is.Not.Null);
        Assert.That(sheet.width, Is.EqualTo(256));
        Assert.That(sheet.height, Is.EqualTo(256));
        Assert.That(sheet.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(sheet.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
    }

    [TestCase(1)]
    [TestCase(4)]
    public void SelectionWaitsForLastMonsterToFinishLanding(int count)
    {
        float lastOpen = ChestRewardPresentation.FirstOpenAt +
            (count - 1) * (MonsterAcquisitionReveal.ReadyAt + ChestRewardPresentation.RevealSpacing);
        var candidates = new List<OwnedMonsterSave>();
        for(int i=0;i<count;i++)candidates.Add(MonsterWithScore(50f));
        var presentation = new ChestRewardPresentation(candidates,true,false,"prototype_region",null);
        Assert.That(presentation.RevealAt(count-1),Is.EqualTo(lastOpen).Within(.001f));
        Assert.That(presentation.ReadyAt,Is.EqualTo(lastOpen+MonsterAcquisitionReveal.ReadyAt).Within(.001f));
        Assert.That(ChestRewardPresentation.OpeningFrame(.4f), Is.EqualTo(7));
    }

    [TestCase(0f,0)] [TestCase(15f,1)] [TestCase(50f,2)]
    [TestCase(70f,3)] [TestCase(85f,3)] [TestCase(90f,4)] [TestCase(100f,4)]
    public void RevealTierUsesTheDisplayedAverageGrade(float score,int expected)
    {
        var monster=MonsterWithScore(score);
        Assert.That(ChestRewardPresentation.QualityTier(monster),Is.EqualTo(expected));
    }

    [TestCase(false)] [TestCase(true)]
    public void RareRevealGetsTimeBeforeNextBoxAndBeforeConfirmation(bool shiny)
    {
        var rare=MonsterWithScore(95f);rare.isShiny=shiny;
        var view=new ChestRewardPresentation(new List<OwnedMonsterSave>{rare,rare},true,false,"prototype_region",null);
        float hold=MonsterAcquisitionReveal.ReadyAt+(shiny?.4f:.25f);
        Assert.That(view.RevealAt(1)-view.RevealAt(0),Is.EqualTo(hold+ChestRewardPresentation.RevealSpacing).Within(.001f));
        Assert.That(view.ReadyAt-view.RevealAt(1),Is.EqualTo(hold).Within(.001f));
    }

    [Test]
    public void EveryRewardChoiceWaitsForThePreviousBoxToFinishItsMeasurementAndStamp()
    {
        var ordinary=MonsterWithScore(50f);
        var view=new ChestRewardPresentation(new List<OwnedMonsterSave>{ordinary,ordinary,ordinary,ordinary},true,false,"prototype_region",null);
        for(int i=1;i<4;i++)
            Assert.That(view.RevealAt(i)-view.RevealAt(i-1),
                Is.EqualTo(MonsterAcquisitionReveal.ReadyAt+ChestRewardPresentation.RevealSpacing).Within(.001f));
    }

    [TestCase(20f, false)] [TestCase(50f, false)] [TestCase(95f, false)] [TestCase(20f, true)]
    public void SequentialCandidatesFinishEveryLeftBeatAndStampBeforeTheRightStarts(float score, bool shiny)
    {
        var left=MonsterWithScore(score);left.isShiny=shiny;
        var right=MonsterWithScore(50f);
        string before=JsonUtility.ToJson(left)+JsonUtility.ToJson(right);
        var view=new ChestRewardPresentation(new List<OwnedMonsterSave>{left,right},false,false,"prototype_region",null);
        float leftFinished=view.RevealAt(0)+MonsterAcquisitionReveal.ReadyAt;
        Assert.That(view.RevealAt(1),Is.GreaterThanOrEqualTo(leftFinished+ChestRewardPresentation.RevealSpacing-.001f));
        Assert.That(view.ReadyAt,Is.GreaterThanOrEqualTo(view.RevealAt(1)+MonsterAcquisitionReveal.ReadyAt));
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var started=typeof(ChestRewardPresentation).GetField("startedAt",hidden);
        var masks=(int[])typeof(ChestRewardPresentation).GetField("statSoundMasks",hidden).GetValue(view);
        for(int stat=0;stat<MonsterAcquisitionReveal.StatCount;stat++)
        {
            started.SetValue(view,Time.unscaledTime-view.RevealAt(0)-MonsterAcquisitionReveal.StatAt(stat)-.001f);
            view.Update();
            Assert.That(masks[0],Is.EqualTo((1<<(stat+1))-1));
            Assert.That(masks[1],Is.Zero,"The right card must not measure during the left card's sequence.");
        }
        started.SetValue(view,Time.unscaledTime-leftFinished);
        view.Update();
        Assert.That(masks[0],Is.EqualTo(63));Assert.That(masks[1],Is.Zero);
        started.SetValue(view,Time.unscaledTime-view.RevealAt(1)-MonsterAcquisitionReveal.StatAt(0)-.001f);
        view.Update();
        Assert.That(masks[0],Is.EqualTo(63));Assert.That(masks[1],Is.EqualTo(1));
        view.Update();Assert.That(masks[1],Is.EqualTo(1),"Repeated frames cannot restart a stat beat.");
        Assert.That(JsonUtility.ToJson(left)+JsonUtility.ToJson(right),Is.EqualTo(before));
    }

    [Test]
    public void AWaitingSequentialCardHasNoPrechargeMovementAndSkipShowsBothResults()
    {
        var monster=MonsterWithScore(50f);
        var view=new ChestRewardPresentation(new List<OwnedMonsterSave>{monster,monster},false,false,"prototype_region",null);
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(ChestRewardPresentation).GetField("startedAt",hidden).SetValue(view,Time.unscaledTime-view.RevealAt(1)+.10f);
        var card=new Rect(20f,40f,800f,360f);
        Assert.That(view.AnimateCard(card,1),Is.EqualTo(card),"Waiting must not apply the old precharge shake or GUI transform.");
        Assert.DoesNotThrow(()=>view.DrawCandidateReveal(card,1));
        Assert.DoesNotThrow(()=>view.DrawScreenReveal(card,card.center,1));
        view.CompleteReveal();
        Assert.That(view.Elapsed,Is.GreaterThanOrEqualTo(view.ReadyAt));Assert.That(view.Finished,Is.False);
        var masks=(int[])typeof(ChestRewardPresentation).GetField("statSoundMasks",hidden).GetValue(view);
        Assert.That(masks,Is.EqualTo(new[]{63,63}));
    }

    internal static OwnedMonsterSave MonsterWithScore(float score)
    {
        float[] values=new float[6];
        for(int i=0;i<6;i++)values[i]=Mathf.Lerp(MonsterStatQuality.MinimumPercent((MonsterIndividualStat)i),
            MonsterStatQuality.MaximumPercent((MonsterIndividualStat)i),score/100f)/100f;
        return new OwnedMonsterSave{hpMultiplier=values[0],damageMultiplier=values[1],attackSpeedMultiplier=values[2],
            defenseMultiplier=values[3],statusResistanceMultiplier=values[4],moveSpeedMultiplier=values[5]};
    }
}
