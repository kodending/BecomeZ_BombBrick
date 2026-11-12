using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class HunterEconomyTelemetryEvent
{
    public int schemaVersion=1;
    public string eventType;
    public string utc;
    public string profileId;
    public string runId;
    public string regionId;
    public int completedWave;
    public int totalWaves;
    public bool cleared;
    public int tokenAmount;
    public int balanceBefore;
    public int balanceAfter;
    public int shopCycle;
    public string boxType;
    public string sourceId;
    public int price;
    public int familyPurchaseIndex;
    public string acquiredMonsterId;
    public string acquiredInstanceId;
    public bool speciesAlreadyOwned;
}

public sealed class HunterEconomyRegionSummary
{
    public string regionId;
    public string displayName;
    public int expeditions;
    public int clears;
    public int completedWaveTotal;
    public int tokensEarned;

    public float ClearRate=>
        expeditions<=0?0f:(float)clears/expeditions;
    public float AverageCompletedWave=>
        expeditions<=0?0f:(float)completedWaveTotal/expeditions;
}

public sealed class HunterEconomyTelemetrySummary
{
    public int expeditionCount;
    public int clearCount;
    public int completedWaveTotal;
    public int tokensEarned;
    public int tokensSpent;
    public int regionBoxesPurchased;
    public int familyBoxesPurchased;
    public int repeatSpeciesPurchases;
    public List<HunterEconomyRegionSummary> regions=
        new List<HunterEconomyRegionSummary>();

    public float ClearRate=>
        expeditionCount<=0?0f:(float)clearCount/expeditionCount;
    public float AverageCompletedWave=>
        expeditionCount<=0?0f:(float)completedWaveTotal/expeditionCount;
    public int NetTokens=>tokensEarned-tokensSpent;
}

public static class HunterEconomyTelemetry
{
    public const int SchemaVersion=1;
    private static readonly object FileLock=new object();
    private static string outputDirectoryOverride;

    public static string DirectoryPath=>
        string.IsNullOrWhiteSpace(outputDirectoryOverride)
            ?Path.Combine(
                Application.persistentDataPath,
                "HunterEconomyTelemetry")
            :outputDirectoryOverride;

    public static string CurrentFilePath=>Path.Combine(
        DirectoryPath,
        "hunter_economy_"+
        DateTime.UtcNow.ToString(
            "yyyyMMdd",CultureInfo.InvariantCulture)+
        ".jsonl");

    public static void RecordExpeditionSettlement(
        PlayerProfileSave profile,
        ExpeditionRunSave run,
        bool cleared,
        int reward,
        int balanceBefore)
    {
        if(profile==null||run==null)return;
        Append(new HunterEconomyTelemetryEvent
        {
            eventType="expedition_settlement",
            profileId=profile.profileId,
            runId=run.runId,
            regionId=run.regionId,
            completedWave=Math.Max(0,run.lastCompletedWave),
            totalWaves=12,
            cleared=cleared,
            tokenAmount=Math.Max(0,reward),
            balanceBefore=Math.Max(0,balanceBefore),
            balanceAfter=Math.Max(0,profile.hunterTokens),
            shopCycle=Math.Max(0,profile.hunterShopCycle)
        });
    }

    public static void RecordBoxPurchase(
        PlayerProfileSave profile,
        string boxType,
        string sourceId,
        int price,
        int balanceBefore,
        OwnedMonsterSave acquired,
        bool speciesAlreadyOwned)
    {
        if(profile==null||acquired==null)return;
        Append(new HunterEconomyTelemetryEvent
        {
            eventType="box_purchase",
            profileId=profile.profileId,
            boxType=boxType??"",
            sourceId=sourceId??"",
            price=Math.Max(0,price),
            tokenAmount=-Math.Max(0,price),
            balanceBefore=Math.Max(0,balanceBefore),
            balanceAfter=Math.Max(0,profile.hunterTokens),
            shopCycle=Math.Max(0,profile.hunterShopCycle),
            familyPurchaseIndex=Math.Max(
                0,profile.hunterFamilyPurchaseCount),
            acquiredMonsterId=acquired.monsterId,
            acquiredInstanceId=acquired.instanceId,
            speciesAlreadyOwned=speciesAlreadyOwned
        });
    }

    public static List<HunterEconomyTelemetryEvent> ReadAll(
        string directory=null)
    {
        string source=string.IsNullOrWhiteSpace(directory)
            ?DirectoryPath:directory;
        List<HunterEconomyTelemetryEvent> result=
            new List<HunterEconomyTelemetryEvent>();
        if(!Directory.Exists(source))return result;

        foreach(string path in Directory.GetFiles(
                    source,"hunter_economy_*.jsonl")
                .OrderBy(value=>value,StringComparer.Ordinal))
        {
            foreach(string line in File.ReadLines(path))
            {
                if(string.IsNullOrWhiteSpace(line))continue;
                try
                {
                    HunterEconomyTelemetryEvent item=
                        JsonUtility.FromJson<HunterEconomyTelemetryEvent>(
                            line);
                    if(item!=null&&item.schemaVersion==SchemaVersion&&
                       !string.IsNullOrWhiteSpace(item.eventType))
                        result.Add(item);
                }
                catch(Exception)
                {
                    // A truncated final line must not hide earlier play data.
                }
            }
        }
        return result;
    }

    public static HunterEconomyTelemetrySummary Analyze(
        IEnumerable<HunterEconomyTelemetryEvent> events)
    {
        List<HunterEconomyTelemetryEvent> source=
            (events??Enumerable.Empty<HunterEconomyTelemetryEvent>())
            .Where(item=>item!=null)
            .ToList();
        HunterEconomyTelemetrySummary summary=
            new HunterEconomyTelemetrySummary();

        foreach(HunterEconomyTelemetryEvent item in source)
        {
            if(string.Equals(
                item.eventType,
                "expedition_settlement",
                StringComparison.Ordinal))
            {
                summary.expeditionCount++;
                if(item.cleared)summary.clearCount++;
                summary.completedWaveTotal+=
                    Math.Max(0,item.completedWave);
                summary.tokensEarned+=Math.Max(0,item.tokenAmount);
            }
            else if(string.Equals(
                item.eventType,
                "box_purchase",
                StringComparison.Ordinal))
            {
                summary.tokensSpent+=Math.Max(0,item.price);
                if(string.Equals(
                    item.boxType,"region",StringComparison.Ordinal))
                    summary.regionBoxesPurchased++;
                else if(string.Equals(
                    item.boxType,"family",StringComparison.Ordinal))
                    summary.familyBoxesPurchased++;
                if(item.speciesAlreadyOwned)
                    summary.repeatSpeciesPurchases++;
            }
        }

        summary.regions=source
            .Where(item=>string.Equals(
                item.eventType,
                "expedition_settlement",
                StringComparison.Ordinal))
            .GroupBy(
                item=>item.regionId??"",
                StringComparer.Ordinal)
            .Select(group=>{
                HunterRegionDefinition region=
                    HunterRegionCatalog.Get(group.Key);
                return new HunterEconomyRegionSummary
                {
                    regionId=group.Key,
                    displayName=region.displayName,
                    expeditions=group.Count(),
                    clears=group.Count(item=>item.cleared),
                    completedWaveTotal=group.Sum(
                        item=>Math.Max(0,item.completedWave)),
                    tokensEarned=group.Sum(
                        item=>Math.Max(0,item.tokenAmount))
                };
            })
            .OrderBy(item=>HunterRegionCatalog.Get(item.regionId).order)
            .ToList();
        return summary;
    }

    public static void SetOutputDirectoryForTests(string directory)
    {
        outputDirectoryOverride=directory;
    }

    public static void ResetOutputDirectoryForTests()
    {
        outputDirectoryOverride=null;
    }

    private static void Append(HunterEconomyTelemetryEvent item)
    {
        try
        {
            item.schemaVersion=SchemaVersion;
            item.utc=DateTime.UtcNow.ToString(
                "O",CultureInfo.InvariantCulture);
            string json=JsonUtility.ToJson(item,false);
            lock(FileLock)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(
                    CurrentFilePath,
                    json+Environment.NewLine,
                    new UTF8Encoding(false));
            }
        }
        catch(Exception exception)
        {
            Debug.LogWarning(
                "[Hunter Economy] Telemetry write failed: "+
                exception.Message);
        }
    }
}
