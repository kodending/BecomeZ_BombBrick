using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class AutoBattleMlContractTests
{
    [Test]
    public void ObservationContract_HasExpectedFixedSize()
    {
        int calculated=6+
                       5+4+
                       BombBrickBalanceAgent.MaximumRosterSize*
                           BombBrickBalanceAgent.UnitObservationSize+
                       BombBrickBalanceAgent.MaximumTileCount*6+
                       BombBrickBalanceAgent.MaximumEnemyCount*
                           BombBrickBalanceAgent.UnitObservationSize;
        Assert.AreEqual(1505,calculated);
        Assert.AreEqual(calculated,BombBrickBalanceAgent.ObservationSize);
    }

    [Test]
    public void TerminalReward_WinIsStrongerThanLoss()
    {
        AutoBattleMlOutcome win=new AutoBattleMlOutcome
        {
            won=true,battleSeconds=30f,survivingPlayerHpRatio=0.5f,
            enemyRemainingHpRatio=0f
        };
        AutoBattleMlOutcome loss=new AutoBattleMlOutcome
        {
            won=false,battleSeconds=30f,survivingPlayerHpRatio=0.5f,
            enemyRemainingHpRatio=0.5f
        };
        Assert.Greater(AutoBattleMlReward.Terminal(win),AutoBattleMlReward.Terminal(loss));
        Assert.Greater(AutoBattleMlReward.Terminal(win),1f);
        Assert.Less(AutoBattleMlReward.Terminal(loss),0f);
    }

    [Test]
    public void TrainingRoster_IsDeterministicAndSupportsFamilyCohorts()
    {
        MonsterDatabase database=ScriptableObject.CreateInstance<MonsterDatabase>();
        List<MonsterDefinition> definitions=new List<MonsterDefinition>();
        try
        {
            for(int family=0;family<4;family++)
            for(int member=0;member<4;member++)
            {
                int serial=family*4+member+1;
                MonsterDefinition definition=
                    ScriptableObject.CreateInstance<MonsterDefinition>();
                definition.monsterId="monster_"+serial;
                definition.familyId="family_"+family;
                definition.serialNumber=serial.ToString("000");
                definition.resourcePath="dummy";
                definition.enabled=true;
                definitions.Add(definition);
                database.monsters.Add(definition);
            }

            List<MonsterDefinition> first=
                MonsterAutoBattlePrototype.BuildMlTrainingRoster(database,1,8);
            List<MonsterDefinition> second=
                MonsterAutoBattlePrototype.BuildMlTrainingRoster(database,1,8);

            CollectionAssert.AreEqual(
                first.Select(item=>item.monsterId),
                second.Select(item=>item.monsterId));
            Assert.AreEqual(8,first.Count);
            Assert.AreEqual(8,first.Select(item=>item.monsterId).Distinct().Count());
            Assert.AreEqual(2,first.Select(item=>item.familyId).Distinct().Count());
        }
        finally
        {
            foreach(MonsterDefinition definition in definitions)
                Object.DestroyImmediate(definition);
            Object.DestroyImmediate(database);
        }
    }

    [Test]
    public void TerminalReward_IsBoundedForExtremeTelemetry()
    {
        AutoBattleMlOutcome outcome=new AutoBattleMlOutcome
        {
            won=true,battleSeconds=-100f,survivingPlayerHpRatio=5f,
            enemyRemainingHpRatio=-2f
        };
        Assert.That(AutoBattleMlReward.Terminal(outcome),Is.InRange(1f,1.75f));
    }

    [Test]
    public void MissingConditionSchedule_CoversOnlyTheLegacyGaps()
    {
        const int seed=260723;
        HashSet<Vector3Int> legacy=new HashSet<Vector3Int>();
        for(int scenario=0;scenario<60;scenario++)
        {
            int episodeSeed=unchecked(seed+scenario*7919);
            legacy.Add(new Vector3Int(
                scenario%12+1,PositiveModulo(episodeSeed,5),
                PositiveModulo(episodeSeed/5,4)));
        }
        HashSet<Vector3Int> missing=new HashSet<Vector3Int>();
        for(int scenario=0;scenario<180;scenario++)
            missing.Add(BombBrickBalanceAgent.ResolveTrainingScenario(
                scenario,seed,1,12,5,true));

        Assert.AreEqual(60,legacy.Count);
        Assert.AreEqual(180,missing.Count);
        Assert.IsFalse(missing.Overlaps(legacy));
        Assert.AreEqual(240,legacy.Union(missing).Count());
    }

    [Test]
    public void AbReport_RebuildsPairedPolicyDeltas()
    {
        AutoBattleMlAbReport report=new AutoBattleMlAbReport();
        report.trials.Add(new AutoBattleMlAbTrialRecord
        {
            policy="Heuristic",scenario=0,won=false,battleSeconds=20f,
            survivingPlayerHpRatio=0.2f,playerDeaths=3
        });
        report.trials.Add(new AutoBattleMlAbTrialRecord
        {
            policy="ML",scenario=0,won=true,battleSeconds=15f,
            survivingPlayerHpRatio=0.6f,playerDeaths=1
        });

        report.Rebuild();

        Assert.AreEqual(0f,report.heuristic.winRate);
        Assert.AreEqual(1f,report.ml.winRate);
        Assert.AreEqual(1f,report.mlMinusHeuristicWinRate);
        Assert.AreEqual(0.4f,report.mlMinusHeuristicHpRatio,0.0001f);
        Assert.AreEqual(-5f,report.mlMinusHeuristicBattleSeconds,0.0001f);
    }

    private static int PositiveModulo(int value,int divisor)
    {
        int result=value%divisor;
        return result<0?result+divisor:result;
    }
}
