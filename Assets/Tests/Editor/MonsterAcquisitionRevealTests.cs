using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class MonsterAcquisitionRevealTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private bool reduced;
    [SetUp] public void Setup(){reduced=CombatFlashPresentation.Reduced;CombatFlashPresentation.Apply(false);}
    [TearDown] public void Cleanup()=>CombatFlashPresentation.Apply(reduced);

    [Test]
    public void SixStatsAccelerateThenRestForTheInitialSlowBeatBeforeTheGradeStamp()
    {
        Assert.That(MonsterAcquisitionReveal.PortraitAt,Is.LessThan(MonsterAcquisitionReveal.StatsAt));
        float previousInterval=float.PositiveInfinity;
        for(int stat=0;stat<MonsterAcquisitionReveal.StatCount;stat++)
        {
            float start=MonsterAcquisitionReveal.StatAt(stat);
            if(stat>0)
            {
                float interval=start-MonsterAcquisitionReveal.StatAt(stat-1);
                Assert.That(interval,Is.LessThan(previousInterval),"Each measurement beat must arrive sooner than the previous one.");
                Assert.That(interval,Is.GreaterThanOrEqualTo(MonsterAcquisitionReveal.CountDuration));
                previousInterval=interval;
            }
            Assert.That(MonsterAcquisitionReveal.StatProgress(start-.001f,stat),Is.Zero);
            Assert.That(MonsterAcquisitionReveal.StatProgress(start+MonsterAcquisitionReveal.CountDuration*.5f,stat),Is.InRange(.49f,.51f));
            Assert.That(MonsterAcquisitionReveal.StatProgress(start+MonsterAcquisitionReveal.CountDuration+.001f,stat),Is.EqualTo(1f));
            Assert.That(MonsterAcquisitionReveal.GradeVisible(start),Is.False);
        }
        Assert.That(MonsterAcquisitionReveal.StatAt(0),Is.EqualTo(MonsterAcquisitionReveal.StatsAt));
        float firstInterval=MonsterAcquisitionReveal.StatAt(1)-MonsterAcquisitionReveal.StatAt(0);
        Assert.That(firstInterval,Is.EqualTo(MonsterAcquisitionReveal.FirstStatInterval).Within(.001f));
        float finishedLast=MonsterAcquisitionReveal.StatAt(MonsterAcquisitionReveal.StatCount-1)+MonsterAcquisitionReveal.CountDuration;
        Assert.That(MonsterAcquisitionReveal.StampAt-finishedLast,Is.EqualTo(firstInterval).Within(.001f),
            "After the fast final number, leave the original slow beat completely empty before stamping.");
        Assert.That(MonsterAcquisitionReveal.GradeVisible(MonsterAcquisitionReveal.StampAt-.001f),Is.False);
        Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.StampAt),Is.InRange(1.25f,1.35f),
            "The stamp should land firmly without covering the adjacent stats or confirmation hint.");
        Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.ReadyAt),Is.EqualTo(1f));
    }

    [Test]
    public void FirstMeasurementStartsSoonerAndUsesEachStatsOwnGrade()
    {
        Assert.That(MonsterAcquisitionReveal.StatsAt-MonsterAcquisitionReveal.PortraitAt,Is.LessThanOrEqualTo(.35f));
        Assert.That(MonsterAcquisitionReveal.FirstStatInterval,Is.LessThanOrEqualTo(.40f));
        var monster=ChestRewardPresentationTests.MonsterWithScore(50f);
        monster.hpMultiplier=MonsterStatQuality.MinimumPercent(MonsterIndividualStat.Health)/100f;
        monster.damageMultiplier=MonsterStatQuality.MaximumPercent(MonsterIndividualStat.AttackDamage)/100f;
        string before=JsonUtility.ToJson(monster);
        Assert.That(MonsterAcquisitionReveal.QualityTier(monster),Is.EqualTo(2));
        Assert.That(MonsterAcquisitionReveal.StatQualityTier(monster,0),Is.EqualTo(0));
        Assert.That(MonsterAcquisitionReveal.StatQualityTier(monster,1),Is.EqualTo(4));
        Assert.That(MonsterAcquisitionReveal.StatQualityTier(monster,2),Is.EqualTo(2));
        Assert.That(JsonUtility.ToJson(monster),Is.EqualTo(before),"Revealing a high stat must never reroll the owned result.");
    }

    [Test]
    public void DisplayedValuesCountUpOnlyToTheAlreadyRolledValuesAndNeverMutateTheMonster()
    {
        var definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        var monster=ChestRewardPresentationTests.MonsterWithScore(75f);
        definition.maxHp=123f;definition.attackDamage=25f;definition.attackInterval=1.4f;
        definition.defense=17f;definition.statusResistance=24f;definition.moveSpeedTilesPerSecond=2.3f;
        string before=JsonUtility.ToJson(monster);
        try
        {
            for(int stat=0;stat<6;stat++)
            {
                float actual=MonsterAcquisitionReveal.Value(definition,monster,stat),previous=0f;
                for(float age=0f;age<MonsterAcquisitionReveal.ReadyAt;age+=1f/120f)
                {
                    float value=MonsterAcquisitionReveal.DisplayValue(definition,monster,age,stat);
                    Assert.That(value,Is.InRange(previous-.00001f,actual+.00001f));previous=value;
                }
                Assert.That(MonsterAcquisitionReveal.DisplayValue(definition,monster,MonsterAcquisitionReveal.ReadyAt,stat),Is.EqualTo(actual));
            }
            Assert.That(JsonUtility.ToJson(monster),Is.EqualTo(before));
        }
        finally{Object.DestroyImmediate(definition);}
    }

    [Test]
    public void ReducedAccentsKeepTheBeatAndGradeButRemoveTheStampZoom()
    {
        CombatFlashPresentation.Apply(true);
        Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.StampAt),Is.EqualTo(1f));
        Assert.That(MonsterAcquisitionReveal.GradeVisible(MonsterAcquisitionReveal.StampAt),Is.True);
        Assert.That(MonsterAcquisitionReveal.StatProgress(MonsterAcquisitionReveal.StatsAt+MonsterAcquisitionReveal.CountDuration*.5f,0),Is.InRange(.49f,.51f));
        Assert.That(MonsterAcquisitionReveal.StatImpact(MonsterAcquisitionReveal.StatsAt,0),Is.Zero);
        Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.StampAt,4,true),Is.EqualTo(1f));
        Assert.That(MonsterAcquisitionReveal.GradeImpact(MonsterAcquisitionReveal.StampAt,4,true),Is.Zero);
    }

    [Test]
    public void StatImpactsAreDistinctAndSettledCardsNeverReplayTheirMeasurement()
    {
        for(int stat=0;stat<MonsterAcquisitionReveal.StatCount;stat++)
        {
            float beat=MonsterAcquisitionReveal.StatAt(stat);
            Assert.That(MonsterAcquisitionReveal.StatImpact(beat-.01f,stat),Is.Zero);
            Assert.That(MonsterAcquisitionReveal.StatImpact(beat+.01f,stat),Is.GreaterThan(.75f));
            float settled=stat+1<MonsterAcquisitionReveal.StatCount?MonsterAcquisitionReveal.StatAt(stat+1):
                beat+MonsterAcquisitionReveal.CountDuration;
            Assert.That(MonsterAcquisitionReveal.StatImpact(settled,stat),Is.EqualTo(0f).Within(.00001f),
                "Only the current beat should move, and the final pause must be completely still.");
            Assert.That(MonsterAcquisitionReveal.StatImpact(MonsterAcquisitionReveal.ReadyAt,stat),Is.Zero);
        }
        Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.StampAt+MonsterAcquisitionReveal.StampDuration*.30f),Is.LessThan(1f),
            "The fast landing should compress before its short rebound.");
        Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.StampAt+MonsterAcquisitionReveal.StampDuration*.50f),Is.GreaterThan(1f));
        Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.ReadyAt),Is.EqualTo(1f));
    }

    [Test]
    public void BetterGradesHaveDistinctColorsAndStrongerStampsWithoutLingeringIntoTheNextCard()
    {
        float[] scores={0f,15f,50f,75f,95f};
        var colors=new HashSet<Color>();
        float previousStrength=0f,previousScale=1f,previousImpact=0f;
        for(int quality=0;quality<scores.Length;quality++)
        {
            var monster=ChestRewardPresentationTests.MonsterWithScore(scores[quality]);
            colors.Add(MonsterAcquisitionReveal.GradeColor(monster));
            float strength=MonsterAcquisitionReveal.GradeStrength(quality);
            float scale=MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.StampAt,quality);
            float impact=MonsterAcquisitionReveal.GradeImpact(MonsterAcquisitionReveal.StampAt+.01f,quality);
            Assert.That(strength,Is.GreaterThan(previousStrength));
            Assert.That(scale,Is.GreaterThan(previousScale));
            Assert.That(impact,Is.GreaterThan(previousImpact));
            Assert.That(MonsterAcquisitionReveal.GradeImpact(MonsterAcquisitionReveal.ReadyAt,quality),Is.Zero);
            Assert.That(MonsterAcquisitionReveal.StampScale(MonsterAcquisitionReveal.ReadyAt,quality),Is.EqualTo(1f));
            previousStrength=strength;previousScale=scale;previousImpact=impact;
        }
        Assert.That(colors,Has.Count.EqualTo(5),"D, C, B, A and S need visibly different grade inks.");
        Assert.That(MonsterAcquisitionReveal.GradeStrength(4,true),Is.GreaterThan(previousStrength));
        Assert.That(MonsterAcquisitionReveal.GradeImpact(MonsterAcquisitionReveal.ReadyAt,4,true),Is.Zero);
    }

    [Test]
    public void SkippingImmediatelyShowsActualResultsWithoutConfirmingOrChangingCandidates()
    {
        var monster=ChestRewardPresentationTests.MonsterWithScore(95f);
        string before=JsonUtility.ToJson(monster);
        var reveal=new ChestRewardPresentation(new List<OwnedMonsterSave>{monster,monster},true,false,"prototype_region",null);
        reveal.CompleteReveal();
        Assert.That(reveal.Elapsed,Is.GreaterThanOrEqualTo(reveal.ReadyAt));
        Assert.That(reveal.Finished,Is.False);
        Assert.That((float)typeof(ChestRewardPresentation).GetField("confirmedAt",Private).GetValue(reveal),Is.EqualTo(-1f));
        Assert.That(JsonUtility.ToJson(monster),Is.EqualTo(before));
        int mask=(int)typeof(ChestRewardPresentation).GetField("soundMask",Private).GetValue(reveal);
        reveal.Update();reveal.Update();
        Assert.That(typeof(ChestRewardPresentation).GetField("soundMask",Private).GetValue(reveal),Is.EqualTo(mask),"Repaint/update cannot replay skipped grade sounds.");
    }

    [Test]
    public void SequentialCardsOnlyMeasureTheActiveCardAndNeverReplayItsStatCues()
    {
        var monster=ChestRewardPresentationTests.MonsterWithScore(50f);
        var reveal=new ChestRewardPresentation(new List<OwnedMonsterSave>{monster,monster,monster,monster},true,false,"prototype_region",null);
        float age=ChestRewardPresentation.FirstOpenAt+MonsterAcquisitionReveal.StatAt(3)+.001f;
        typeof(ChestRewardPresentation).GetField("startedAt",Private).SetValue(reveal,Time.unscaledTime-age);
        reveal.Update();
        var masks=(int[])typeof(ChestRewardPresentation).GetField("statSoundMasks",Private).GetValue(reveal);
        Assert.That(masks,Is.EqualTo(new[]{15,0,0,0}));
        int soundMask=(int)typeof(ChestRewardPresentation).GetField("soundMask",Private).GetValue(reveal);
        reveal.Update();
        Assert.That(typeof(ChestRewardPresentation).GetField("soundMask",Private).GetValue(reveal),Is.EqualTo(soundMask));
        Assert.That(masks,Is.EqualTo(new[]{15,0,0,0}));
    }
}
