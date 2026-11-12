using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class MonsterStatQualityTests
{
    [Test]
    public void StatGradeUsesEachStatsOwnConfiguredRange()
    {
        OwnedMonsterSave monster=AtMinimum();

        Assert.That(
            MonsterStatQuality.StatGrade(
                monster,MonsterIndividualStat.Health),
            Is.EqualTo("D"));

        monster.hpMultiplier=1f;
        Assert.That(
            MonsterStatQuality.StatGrade(
                monster,MonsterIndividualStat.Health),
            Is.EqualTo("B"));

        monster.hpMultiplier=1.15f;
        Assert.That(
            MonsterStatQuality.StatGrade(
                monster,MonsterIndividualStat.Health),
            Is.EqualTo("S"));

        monster.attackSpeedMultiplier=1f;
        Assert.That(
            MonsterStatQuality.QualityScore(
                monster,MonsterIndividualStat.AttackSpeed),
            Is.EqualTo(50f).Within(0.001f));

        monster.moveSpeedMultiplier=1f;
        Assert.That(
            MonsterStatQuality.QualityScore(
                monster,MonsterIndividualStat.MoveSpeed),
            Is.EqualTo(50f).Within(0.001f));
    }

    [Test]
    public void OverallGradeHasMinusAndPlusBandsWithoutPotentialCap()
    {
        OwnedMonsterSave minimum=AtMinimum();
        OwnedMonsterSave maximum=AtMaximum();
        OwnedMonsterSave baseline=AtMultiplier(1f);

        Assert.That(MonsterStatQuality.OverallScore(minimum),Is.EqualTo(0f));
        Assert.That(MonsterStatQuality.OverallGrade(minimum),Is.EqualTo("D-"));
        Assert.That(MonsterStatQuality.OverallScore(maximum),Is.EqualTo(100f));
        Assert.That(MonsterStatQuality.OverallGrade(maximum),Is.EqualTo("S+"));
        Assert.That(
            MonsterStatQuality.OverallScore(baseline),
            Is.EqualTo(50f).Within(0.001f));
        Assert.That(MonsterStatQuality.OverallGrade(baseline),Is.EqualTo("B"));
    }

    [TestCase(97f,"S+")]
    [TestCase(94f,"S")]
    [TestCase(90f,"S-")]
    [TestCase(84f,"A+")]
    [TestCase(77f,"A")]
    [TestCase(70f,"A-")]
    [TestCase(57f,"B+")]
    [TestCase(43f,"B")]
    [TestCase(30f,"B-")]
    [TestCase(23f,"C+")]
    [TestCase(17f,"C")]
    [TestCase(10f,"C-")]
    [TestCase(6f,"D+")]
    [TestCase(3f,"D")]
    [TestCase(0f,"D-")]
    public void OverallGradeMatchesApprovedThresholds(float score,string grade)
    {
        Assert.That(MonsterStatQuality.OverallGrade(score),Is.EqualTo(grade));
    }

    [Test]
    public void ResetRosterDefinitionsMatchApproved001To004BaseStats()
    {
        MonsterDatabase database=
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        Assert.That(MonsterBaseStatMasterSetup.Specs,Has.Length.EqualTo(4));

        foreach(MonsterBaseStatSpec spec in MonsterBaseStatMasterSetup.Specs)
        {
            MonsterDefinition definition=database.Get(spec.monsterId);
            Assert.That(definition,Is.Not.Null,spec.monsterId);
            Assert.That(definition.enabled,Is.True,spec.monsterId);
            Assert.That(definition.serialNumber,Is.EqualTo(spec.serialNumber));
            Assert.That(definition.maxHp,Is.EqualTo(spec.maxHp).Within(0.0001f));
            Assert.That(
                definition.attackDamage,
                Is.EqualTo(spec.attackDamage).Within(0.0001f));
            Assert.That(
                definition.attackInterval,
                Is.EqualTo(spec.AttackInterval).Within(0.0001f));
            Assert.That(definition.defense,Is.EqualTo(spec.defense).Within(0.0001f));
            Assert.That(
                definition.statusResistance,
                Is.EqualTo(spec.statusResistance).Within(0.0001f));
            Assert.That(
                definition.moveSpeedTilesPerSecond,
                Is.EqualTo(spec.moveSpeed).Within(0.0001f));
            Assert.That(definition.attackRange,Is.EqualTo(spec.attackRange));
        }
    }

    [Test]
    public void ShinyCatalogHasOneCompleteVariantPerFamily()
    {
        MonsterDatabase database=
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition[] shiny=database.monsters
            .Where(definition=>definition!=null&&definition.enabled&&
                !string.IsNullOrWhiteSpace(definition.shinyResourcePath))
            .ToArray();

        Assert.That(MonsterInstanceGenerator.ShinyOddsDenominator,
            Is.EqualTo(65536));
        int sourceCount=Directory.GetDirectories(
            Path.GetFullPath(PixelMonsterRosterSetup.SourceRoot),
            "Shiny",SearchOption.AllDirectories).Length;
        Assert.That(shiny,Has.Length.EqualTo(sourceCount));
        Assert.That(
            shiny.Select(definition=>definition.familyId).Distinct().Count(),
            Is.EqualTo(sourceCount));
        foreach(MonsterDefinition definition in shiny)
        {
            GameObject prefab=Resources.Load<GameObject>(
                definition.shinyResourcePath);
            SpriteMonsterAnimationDriver driver=prefab==null
                ?null
                :prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            Assert.That(driver,Is.Not.Null,definition.monsterId);
            Assert.That(driver.IdleFrameCount,Is.GreaterThan(0));
            Assert.That(driver.RunFrameCount,Is.GreaterThan(0));
            Assert.That(driver.AttackFrameCount,Is.GreaterThan(0));
            Assert.That(driver.SkillAttackFrameCount,Is.GreaterThan(0));
            Assert.That(driver.DeathFrameCount,Is.GreaterThan(0));
            Assert.That(driver.CaptureFrameCount,Is.GreaterThan(0));
            Assert.That(driver.StruggleFrameCount,Is.GreaterThan(0));
        }
    }

    [Test]
    public void ShinyStatsStayAtMaximumAndCapturePreservesShiny()
    {
        MonsterDatabase database=
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database.monsters.First(value=>
            value!=null&&!string.IsNullOrWhiteSpace(value.shinyResourcePath));
        OwnedMonsterSave enemy=new OwnedMonsterSave
        {
            instanceId="forced_shiny_enemy",
            monsterId=definition.monsterId,
            isShiny=true
        };

        MonsterInstanceGenerator.EnsureInitialized(enemy,definition);
        foreach(MonsterIndividualStat stat in
                System.Enum.GetValues(typeof(MonsterIndividualStat)))
            Assert.That(MonsterStatQuality.StatGrade(enemy,stat),Is.EqualTo("S"));
        Assert.That(MonsterStatQuality.OverallGrade(enemy),Is.EqualTo("S+"));

        OwnedMonsterSave captured=ExpeditionCaptureSystem.CreateCapturedMonster(
            enemy,definition,"captured_shiny");
        Assert.That(captured.isShiny,Is.True);
        Assert.That(MonsterStatQuality.OverallGrade(captured),Is.EqualTo("S+"));
        Assert.That(
            PlayerProfileStore.CreateMonster(definition,"temporary",false).isShiny,
            Is.False);
    }

    [Test]
    public void SelectedDifficultyUsesStatMultipliersAndCaptureQualityRerolls()
    {
        Assert.That(ExpeditionDifficultyRules.HealthMultiplier(
            ExpeditionDifficulty.Beginner),Is.EqualTo(0.65f));
        Assert.That(ExpeditionDifficultyRules.AttackMultiplier(
            ExpeditionDifficulty.Beginner),Is.EqualTo(1f));
        Assert.That(ExpeditionDifficultyRules.AttackMultiplier(
            ExpeditionDifficulty.Normal),Is.EqualTo(1f));
        Assert.That(ExpeditionDifficultyRules.HealthMultiplier(
            ExpeditionDifficulty.Normal),Is.EqualTo(0.80f));
        Assert.That(ExpeditionDifficultyRules.HealthMultiplier(
            ExpeditionDifficulty.Veteran),Is.EqualTo(1f));
        Assert.That(ExpeditionDifficultyRules.HealthMultiplier(
            ExpeditionDifficulty.Hell),Is.EqualTo(1.35f));
        Assert.That(ExpeditionDifficultyRules.AttackMultiplier(
            ExpeditionDifficulty.Hell),Is.EqualTo(1f));

        OwnedMonsterSave normal=AtMinimum();
        OwnedMonsterSave veteran=AtMinimum();
        OwnedMonsterSave hell=AtMinimum();
        MonsterInstanceGenerator.ApplyCaptureQualityRerolls(
            normal,ExpeditionDifficulty.Normal,9137);
        MonsterInstanceGenerator.ApplyCaptureQualityRerolls(
            veteran,ExpeditionDifficulty.Veteran,9137);
        MonsterInstanceGenerator.ApplyCaptureQualityRerolls(
            hell,ExpeditionDifficulty.Hell,9137);

        Assert.That(MonsterStatQuality.OverallScore(veteran),
            Is.GreaterThanOrEqualTo(MonsterStatQuality.OverallScore(normal)));
        Assert.That(MonsterStatQuality.OverallScore(hell),
            Is.GreaterThanOrEqualTo(MonsterStatQuality.OverallScore(veteran)));
    }

    [Test]
    public void CaptureRollsAreFreshAndChooseOneWholeStatSetWithoutEncounterBias()
    {
        var stats=(MonsterIndividualStat[])System.Enum.GetValues(typeof(MonsterIndividualStat));
        for(int seed=0;seed<100;seed++)
        {
            var beginner=AtMinimum();
            var normal=AtMaximum();
            var veteran=AtMinimum();
            var hell=AtMaximum();
            MonsterInstanceGenerator.ApplyCaptureQualityRerolls(beginner,ExpeditionDifficulty.Beginner,seed);
            MonsterInstanceGenerator.ApplyCaptureQualityRerolls(normal,ExpeditionDifficulty.Normal,seed);
            MonsterInstanceGenerator.ApplyCaptureQualityRerolls(veteran,ExpeditionDifficulty.Veteran,seed);
            MonsterInstanceGenerator.ApplyCaptureQualityRerolls(hell,ExpeditionDifficulty.Hell,seed);
            foreach(var stat in stats)
                Assert.That(MonsterStatQuality.Multiplier(normal,stat),
                    Is.EqualTo(MonsterStatQuality.Multiplier(beginner,stat)),"Encounter stats must not be candidates.");

            var random=new System.Random(seed);
            float bestScore=-1f;
            float[] best=null;
            for(int roll=0;roll<3;roll++)
            {
                var candidate=new float[stats.Length];
                float score=0f;
                for(int i=0;i<stats.Length;i++)
                {
                    candidate[i]=random.Next(MonsterStatQuality.MinimumPercent(stats[i]),
                        MonsterStatQuality.MaximumPercent(stats[i])+1)/100f;
                    score+=MonsterStatQuality.QualityScore(candidate[i],stats[i]);
                }
                score/=stats.Length;
                if(score>bestScore){bestScore=score;best=candidate;}
                var actual=roll==0?normal:roll==1?veteran:hell;
                for(int i=0;i<stats.Length;i++)
                    Assert.That(MonsterStatQuality.Multiplier(actual,stats[i]),Is.EqualTo(best[i]),
                        "Must select the best complete set from exactly "+(roll+1)+" fresh rolls.");
                Assert.That(MonsterInstanceGenerator.MultipliersAreValid(actual),Is.True);
            }
        }
    }

    private static OwnedMonsterSave AtMinimum()
    {
        return new OwnedMonsterSave
        {
            hpMultiplier=0.85f,
            damageMultiplier=0.85f,
            attackSpeedMultiplier=0.90f,
            defenseMultiplier=0.85f,
            statusResistanceMultiplier=0.85f,
            moveSpeedMultiplier=0.94f
        };
    }

    [TestCase(ExpeditionDifficulty.Beginner,0f,43f,"D-~B-")]
    [TestCase(ExpeditionDifficulty.Normal,10f,70f,"C-~B+")]
    [TestCase(ExpeditionDifficulty.Veteran,30f,84f,"B-~A")]
    [TestCase(ExpeditionDifficulty.Hell,57f,101f,"B+~S+")]
    public void EnemyQualityStaysInDifficultyRangeAndRetriesKeepTheSameStats(
        ExpeditionDifficulty difficulty,float minimum,float maximum,string grades)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        Assert.That(ExpeditionDifficultyRules.EnemyQualityGradeRange(difficulty),Is.EqualTo(grades));
        for(int i=0;i<256;i++)
        {
            var enemy=PlayerProfileStore.CreateMonster(definition,"difficulty_enemy_"+i,false);
            string personality=enemy.personalityId;
            MonsterInstanceGenerator.ApplyEnemyDifficultyQuality(enemy,difficulty);
            float score=MonsterStatQuality.OverallScore(enemy);
            Assert.That(score,Is.GreaterThanOrEqualTo(minimum).And.LessThan(maximum));
            Assert.That(MonsterInstanceGenerator.MultipliersAreValid(enemy),Is.True);
            Assert.That(enemy.personalityId,Is.EqualTo(personality));
            string first=JsonUtility.ToJson(enemy);
            MonsterInstanceGenerator.ApplyEnemyDifficultyQuality(enemy,difficulty);
            Assert.That(JsonUtility.ToJson(enemy),Is.EqualTo(first),"Retry must not reroll the enemy.");
        }
    }

    [Test]
    public void ShinyEnemiesAndCapturesKeepMaximumStatsAtEveryDifficulty()
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        foreach(ExpeditionDifficulty difficulty in System.Enum.GetValues(typeof(ExpeditionDifficulty)))
        {
            var enemy=PlayerProfileStore.CreateMonster(definition,"shiny_quality_enemy",false);
            enemy.isShiny=true;
            MonsterInstanceGenerator.ApplyEnemyDifficultyQuality(enemy,difficulty);
            Assert.That(MonsterStatQuality.OverallGrade(enemy),Is.EqualTo("S+"));
            var captured=ExpeditionCaptureSystem.CreateCapturedMonster(enemy,definition,"shiny_quality_capture",difficulty);
            Assert.That(captured.isShiny,Is.True);
            Assert.That(MonsterStatQuality.OverallGrade(captured),Is.EqualTo("S+"));
        }
    }

    private static OwnedMonsterSave AtMaximum()
    {
        return new OwnedMonsterSave
        {
            hpMultiplier=1.15f,
            damageMultiplier=1.15f,
            attackSpeedMultiplier=1.10f,
            defenseMultiplier=1.15f,
            statusResistanceMultiplier=1.15f,
            moveSpeedMultiplier=1.06f
        };
    }

    private static OwnedMonsterSave AtMultiplier(float value)
    {
        return new OwnedMonsterSave
        {
            hpMultiplier=value,
            damageMultiplier=value,
            attackSpeedMultiplier=value,
            defenseMultiplier=value,
            statusResistanceMultiplier=value,
            moveSpeedMultiplier=value
        };
    }
}
