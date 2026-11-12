using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class AutoBattleBalanceTrialRecord
{
    public int runIndex;
    public int wave;
    public int seed;
    public bool won;
    public float battleSeconds;
    public int playerDeaths;
    public int playerSurvivors;
    public int enemySurvivors;
    public float survivingPlayerHpRatio;
    public bool timedOut;
}

[Serializable]
public sealed class AutoBattleBalanceUnitRecord
{
    public int runIndex;
    public int wave;
    public int seed;
    public string monsterId;
    public string displayName;
    public string team;
    public bool deployed;
    public bool survived;
    public bool won;
    public int attackRange;
    public float damageDealt;
    public float damageTaken;
    public float skillDamage;
    public float preventedDamage;
    public int skillUses;
    public int skillHits;
    public int kills;
    public int traitActivations;
}

[Serializable]
public sealed class AutoBattleMonsterBalanceAggregate
{
    public string monsterId;
    public string displayName;
    public string team;
    public int appearances;
    public int wins;
    public int survivals;
    public float totalDamageDealt;
    public float totalDamageTaken;
    public float totalSkillDamage;
    public float totalPreventedDamage;
    public int totalKills;
    public int totalSkillUses;
    public int totalTraitActivations;
    public float winRate;
    public float survivalRate;
    public float averageDamageDealt;
    public float averageDamageTaken;
}

[Serializable]
public sealed class AutoBattleBalanceReport
{
    public int schemaVersion=1;
    public string createdUtc;
    public string agent="HeuristicFormationV1";
    public string regionId;
    public string difficulty;
    public string rosterSignature;
    public int baseSeed;
    public int wave;
    public int requestedRuns;
    public int completedRuns;
    public int wins;
    public int timedOutRuns;
    public float winRate;
    public float averageBattleSeconds;
    public float averagePlayerDeaths;
    public List<AutoBattleBalanceTrialRecord> trials=new List<AutoBattleBalanceTrialRecord>();
    public List<AutoBattleBalanceUnitRecord> units=new List<AutoBattleBalanceUnitRecord>();
    public List<AutoBattleMonsterBalanceAggregate> monsters=
        new List<AutoBattleMonsterBalanceAggregate>();

    public void RebuildAggregates()
    {
        completedRuns=trials.Count;
        wins=trials.Count(row=>row.won);
        timedOutRuns=trials.Count(row=>row.timedOut);
        winRate=completedRuns==0?0f:(float)wins/completedRuns;
        averageBattleSeconds=completedRuns==0?0f:trials.Average(row=>row.battleSeconds);
        averagePlayerDeaths=completedRuns==0?0f:(float)trials.Average(row=>row.playerDeaths);
        monsters=units.Where(row=>row.deployed)
            .GroupBy(row=>row.team+"\n"+row.monsterId,StringComparer.Ordinal)
            .Select(group=>
            {
                AutoBattleBalanceUnitRecord first=group.First();
                int count=group.Count();
                return new AutoBattleMonsterBalanceAggregate
                {
                    monsterId=first.monsterId,displayName=first.displayName,team=first.team,
                    appearances=count,wins=group.Count(row=>row.won),
                    survivals=group.Count(row=>row.survived),
                    totalDamageDealt=group.Sum(row=>row.damageDealt),
                    totalDamageTaken=group.Sum(row=>row.damageTaken),
                    totalSkillDamage=group.Sum(row=>row.skillDamage),
                    totalPreventedDamage=group.Sum(row=>row.preventedDamage),
                    totalKills=group.Sum(row=>row.kills),
                    totalSkillUses=group.Sum(row=>row.skillUses),
                    totalTraitActivations=group.Sum(row=>row.traitActivations),
                    winRate=(float)group.Count(row=>row.won)/count,
                    survivalRate=(float)group.Count(row=>row.survived)/count,
                    averageDamageDealt=group.Average(row=>row.damageDealt),
                    averageDamageTaken=group.Average(row=>row.damageTaken)
                };
            })
            .OrderBy(row=>row.team,StringComparer.Ordinal)
            .ThenByDescending(row=>row.winRate)
            .ThenByDescending(row=>row.averageDamageDealt)
            .ToList();
    }
}

public static class AutoBattleBalanceReportWriter
{
    public static string Write(AutoBattleBalanceReport report,string directory=null)
    {
        if(report==null)throw new ArgumentNullException(nameof(report));
        report.RebuildAggregates();
        if(string.IsNullOrWhiteSpace(report.createdUtc))
            report.createdUtc=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture);
        if(string.IsNullOrWhiteSpace(directory))
            directory=Path.Combine(Application.persistentDataPath,"BalanceReports");
        Directory.CreateDirectory(directory);
        string stamp=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture);
        string stem="balance_wave_"+report.wave+"_seed_"+report.baseSeed+"_"+stamp;
        string jsonPath=Path.Combine(directory,stem+".json");
        string csvPath=Path.Combine(directory,stem+".csv");
        File.WriteAllText(jsonPath,JsonUtility.ToJson(report,true),Encoding.UTF8);
        File.WriteAllText(csvPath,BuildCsv(report),Encoding.UTF8);
        return jsonPath+"|"+csvPath;
    }

    public static string BuildCsv(AutoBattleBalanceReport report)
    {
        report.RebuildAggregates();
        StringBuilder csv=new StringBuilder(4096);
        csv.AppendLine("record_type,wave,run,seed,won,timed_out,battle_seconds,player_deaths,player_survivors,enemy_survivors,monster_id,display_name,team,survived,damage_dealt,damage_taken,skill_damage,prevented_damage,kills,skill_uses,trait_activations,region_id,difficulty");
        for(int i=0;i<report.trials.Count;i++)
        {
            AutoBattleBalanceTrialRecord row=report.trials[i];
            csv.Append("trial,").Append(row.wave).Append(',').Append(row.runIndex).Append(',')
                .Append(row.seed).Append(',').Append(row.won?"1":"0").Append(',')
                .Append(row.timedOut?"1":"0").Append(',')
                .Append(F(row.battleSeconds)).Append(',').Append(row.playerDeaths).Append(',')
                .Append(row.playerSurvivors).Append(',').Append(row.enemySurvivors)
                .Append(",,,,,,,,,,,,").Append(Q(report.regionId)).Append(',')
                .Append(Q(report.difficulty)).AppendLine();
        }
        for(int i=0;i<report.units.Count;i++)
        {
            AutoBattleBalanceUnitRecord row=report.units[i];
            if(!row.deployed)continue;
            csv.Append("unit,").Append(row.wave).Append(',').Append(row.runIndex).Append(',')
                .Append(row.seed).Append(',').Append(row.won?"1":"0").Append(",,")
                .Append(",,,,")
                .Append(Q(row.monsterId)).Append(',').Append(Q(row.displayName)).Append(',')
                .Append(Q(row.team)).Append(',').Append(row.survived?"1":"0").Append(',')
                .Append(F(row.damageDealt)).Append(',').Append(F(row.damageTaken)).Append(',')
                .Append(F(row.skillDamage)).Append(',').Append(F(row.preventedDamage)).Append(',')
                .Append(row.kills).Append(',').Append(row.skillUses).Append(',')
                .Append(row.traitActivations).Append(',').Append(Q(report.regionId)).Append(',')
                .Append(Q(report.difficulty)).AppendLine();
        }
        return csv.ToString();
    }

    private static string F(float value)
    {
        return value.ToString("0.#####",CultureInfo.InvariantCulture);
    }

    private static string Q(string value)
    {
        string safe=value??string.Empty;
        return "\""+safe.Replace("\"","\"\"")+"\"";
    }
}

public sealed class AutoBattleFormationCandidate
{
    public string stableId;
    public float maxHp;
    public float damage;
    public float attackInterval;
    public int attackRange;
    public int rank;
    public int traitCount;
}

public static class AutoBattleHeuristicPolicy
{
    public static List<string> ApplyGrowthShopProgression(
        ExpeditionRunSave run,int targetWave,int baseSeed)
    {
        List<string> selectedIds=new List<string>();
        if(run==null||run.monsters==null)return selectedIds;
        if(run.growthCards==null)
            run.growthCards=new List<CombatGrowthCardSelectionSave>();
        run.growthCards.Clear();
        foreach(ExpeditionMonsterStateSave monster in run.monsters.Where(
                    value=>value!=null))
        {
            monster.growthTotal=0;
            monster.rank=ExpeditionMonsterRank.Rank1;
            monster.transcended=false;
            monster.transcendenceFailureBonus=0;
        }
        if(run.growthShopCandidateMonsterIds==null)
            run.growthShopCandidateMonsterIds=new List<string>();
        if(run.growthShopPurchasedMonsterIds==null)
            run.growthShopPurchasedMonsterIds=new List<string>();
        run.growthShopWave=0;
        run.growthShopCandidateMonsterIds.Clear();
        run.growthShopPurchasedMonsterIds.Clear();

        int finalWave=Mathf.Max(1,targetWave);
        for(int wave=1;wave<=finalWave;wave++)
        {
            run.wave=wave;
            ExpeditionGrowthSystem.PrepareWave(run);
            ExpeditionMonsterStateSave selected=
                ExpeditionGrowthSystem.ShopCandidates(run)
                .OrderByDescending(monster=>
                    ExpeditionGrowthSystem.OfferAmount(run,monster.instanceId))
                .ThenByDescending(monster=>monster.growthTotal)
                .ThenBy(monster=>MonsterInstanceGenerator.StableSeed(
                    baseSeed+"_growth_shop_"+wave+"_"+monster.instanceId))
                .FirstOrDefault();
            if(selected==null)continue;
            if(ExpeditionGrowthSystem.TryPurchase(run,selected.instanceId,
               out _,out _,out _))selectedIds.Add(selected.instanceId);
        }
        run.wave=finalWave;
        return selectedIds;
    }

    public static List<string> SelectDeployment(
        IEnumerable<AutoBattleFormationCandidate> candidates,int maximumCount,int seed)
    {
        if(candidates==null||maximumCount<=0)return new List<string>();
        return candidates.Where(row=>row!=null&&!string.IsNullOrWhiteSpace(row.stableId))
            .OrderByDescending(Score)
            .ThenBy(row=>StableTie(row.stableId,seed))
            .Take(maximumCount)
            .Select(row=>row.stableId).ToList();
    }

    public static int PreferredDeploymentRow(int attackRange,int minimumRow,int maximumRow)
    {
        if(attackRange>=3)return minimumRow;
        if(attackRange==2)return Mathf.RoundToInt((minimumRow+maximumRow)*0.5f);
        return maximumRow;
    }

    private static float Score(AutoBattleFormationCandidate row)
    {
        float dps=Mathf.Max(0f,row.damage)/Mathf.Max(0.1f,row.attackInterval);
        return dps*1.15f+Mathf.Max(0f,row.maxHp)*0.08f+
               Mathf.Max(0,row.attackRange)*1.8f+Mathf.Max(1,row.rank)*3f+
               Mathf.Max(0,row.traitCount)*2f;
    }

    private static int StableTie(string stableId,int seed)
    {
        unchecked
        {
            int hash=seed;
            for(int i=0;i<stableId.Length;i++)hash=hash*31+stableId[i];
            return hash&int.MaxValue;
        }
    }
}
