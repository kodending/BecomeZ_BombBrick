using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class AutoBattleMlAbTrialRecord
{
    public string policy="";
    public int scenario;
    public int wave;
    public int seed;
    public bool won;
    public float battleSeconds;
    public int playerDeaths;
    public int playerSurvivors;
    public int enemySurvivors;
    public float survivingPlayerHpRatio;
    public float enemyRemainingHpRatio;
}

[Serializable]
public sealed class AutoBattleMlAbPolicySummary
{
    public string policy="";
    public int runs;
    public int wins;
    public float winRate;
    public float averageSurvivingPlayerHpRatio;
    public float averageBattleSeconds;
    public float averagePlayerDeaths;
}

[Serializable]
public sealed class AutoBattleMlAbReport
{
    public int schemaVersion=1;
    public string createdUtc="";
    public string modelName="";
    public string rosterSignature="";
    public int baseSeed;
    public int requestedScenarios;
    public List<AutoBattleMlAbTrialRecord> trials=new List<AutoBattleMlAbTrialRecord>();
    public AutoBattleMlAbPolicySummary heuristic=new AutoBattleMlAbPolicySummary();
    public AutoBattleMlAbPolicySummary ml=new AutoBattleMlAbPolicySummary();
    public float mlMinusHeuristicWinRate;
    public float mlMinusHeuristicHpRatio;
    public float mlMinusHeuristicBattleSeconds;

    public void Rebuild()
    {
        heuristic=BuildSummary("Heuristic",trials);
        ml=BuildSummary("ML",trials);
        mlMinusHeuristicWinRate=ml.winRate-heuristic.winRate;
        mlMinusHeuristicHpRatio=ml.averageSurvivingPlayerHpRatio-
                                 heuristic.averageSurvivingPlayerHpRatio;
        mlMinusHeuristicBattleSeconds=ml.averageBattleSeconds-
                                       heuristic.averageBattleSeconds;
    }

    private static AutoBattleMlAbPolicySummary BuildSummary(
        string policy,IEnumerable<AutoBattleMlAbTrialRecord> source)
    {
        List<AutoBattleMlAbTrialRecord> rows=source
            .Where(row=>string.Equals(row.policy,policy,StringComparison.Ordinal)).ToList();
        return new AutoBattleMlAbPolicySummary
        {
            policy=policy,runs=rows.Count,wins=rows.Count(row=>row.won),
            winRate=rows.Count==0?0f:(float)rows.Count(row=>row.won)/rows.Count,
            averageSurvivingPlayerHpRatio=rows.Count==0?0f:
                rows.Average(row=>row.survivingPlayerHpRatio),
            averageBattleSeconds=rows.Count==0?0f:rows.Average(row=>row.battleSeconds),
            averagePlayerDeaths=rows.Count==0?0f:(float)rows.Average(row=>row.playerDeaths)
        };
    }
}

public static class AutoBattleMlAbReportWriter
{
    public static string Write(AutoBattleMlAbReport report,string directory=null)
    {
        if(report==null)throw new ArgumentNullException(nameof(report));
        report.Rebuild();
        report.createdUtc=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture);
        if(string.IsNullOrWhiteSpace(directory))
            directory=Path.Combine(Application.persistentDataPath,"BalanceReports");
        Directory.CreateDirectory(directory);
        string stamp=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture);
        string stem="ml_ab_seed_"+report.baseSeed+"_"+stamp;
        string json=Path.Combine(directory,stem+".json");
        string csv=Path.Combine(directory,stem+".csv");
        File.WriteAllText(json,JsonUtility.ToJson(report,true),Encoding.UTF8);
        File.WriteAllText(csv,BuildCsv(report),Encoding.UTF8);
        return json+"|"+csv;
    }

    public static string BuildCsv(AutoBattleMlAbReport report)
    {
        report.Rebuild();
        StringBuilder csv=new StringBuilder(4096);
        csv.AppendLine("policy,scenario,wave,seed,won,battle_seconds,player_deaths,player_survivors,enemy_survivors,player_hp_ratio,enemy_hp_ratio");
        foreach(AutoBattleMlAbTrialRecord row in report.trials
                    .OrderBy(item=>item.scenario).ThenBy(item=>item.policy,StringComparer.Ordinal))
        {
            csv.Append(row.policy).Append(',').Append(row.scenario).Append(',')
                .Append(row.wave).Append(',').Append(row.seed).Append(',')
                .Append(row.won?"1":"0").Append(',').Append(F(row.battleSeconds)).Append(',')
                .Append(row.playerDeaths).Append(',').Append(row.playerSurvivors).Append(',')
                .Append(row.enemySurvivors).Append(',').Append(F(row.survivingPlayerHpRatio))
                .Append(',').Append(F(row.enemyRemainingHpRatio)).AppendLine();
        }
        return csv.ToString();
    }

    private static string F(float value)
    {
        return value.ToString("0.#####",CultureInfo.InvariantCulture);
    }
}
