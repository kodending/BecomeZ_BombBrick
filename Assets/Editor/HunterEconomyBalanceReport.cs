#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HunterEconomyBalanceReport
{
    private const int TotalWaves=12;
    private const string OutputFolder="Docs/BalanceReports";
    private static readonly int[] MilestoneCompletedWaves={2,5,8,11,12};

    [MenuItem("BombBrick/Balance/Generate Hunter Economy Report")]
    public static void Generate()
    {
        ValidateGuardrails();

        string projectRoot=Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory=Path.Combine(projectRoot,OutputFolder);
        Directory.CreateDirectory(outputDirectory);

        string markdownPath=Path.Combine(
            outputDirectory,"Hunter_Economy_Current.md");
        string csvPath=Path.Combine(
            outputDirectory,"Hunter_Economy_Current.csv");
        File.WriteAllText(
            markdownPath,BuildMarkdown(),new UTF8Encoding(false));
        File.WriteAllText(
            csvPath,BuildCsv(),new UTF8Encoding(false));
        AssetDatabase.Refresh();

        Debug.Log(
            "[Hunter Economy] Report generated: "+
            markdownPath+" / "+csvPath);
    }

    public static void ValidateGuardrails()
    {
        IReadOnlyList<HunterRegionDefinition> regions=
            HunterRegionCatalog.All;
        if(regions.Count!=5)
            throw new InvalidOperationException(
                "Hunter economy requires exactly five regions.");

        foreach(HunterRegionDefinition region in regions)
        {
            int clearReward=HunterTokenRewardSystem.Calculate(
                region.id,TotalWaves,TotalWaves,true);
            int midFailureReward=HunterTokenRewardSystem.Calculate(
                region.id,5,TotalWaves,false);
            if(clearReward<region.regionBoxCost)
                throw new InvalidOperationException(
                    region.id+
                    " clear reward must afford one region box.");
            if(clearReward>=region.regionBoxCost*2)
                throw new InvalidOperationException(
                    region.id+
                    " clear reward must not afford two region boxes.");

            float midRatio=clearReward<=0
                ?0f:(float)midFailureReward/clearReward;
            if(midRatio<0.25f||midRatio>0.45f)
                throw new InvalidOperationException(
                    region.id+
                    " Wave 6 failure reward ratio left the 25~45% band.");
        }

        int previous=0;
        for(int purchase=0;purchase<4;purchase++)
        {
            PlayerProfileSave profile=new PlayerProfileSave
            {
                hunterFamilyPurchaseCount=purchase
            };
            int cost=HunterShopSystem.CurrentFamilyBoxCost(profile);
            if(cost<=previous)
                throw new InvalidOperationException(
                    "Family box prices must strictly increase through "+
                    "the fourth purchase.");
            previous=cost;
        }
    }

    public static string BuildMarkdown()
    {
        IReadOnlyList<HunterRegionDefinition> regions=
            HunterRegionCatalog.All;
        StringBuilder builder=new StringBuilder();
        builder.AppendLine("# 사냥의 증표 경제 1차 진단");
        builder.AppendLine();
        builder.AppendLine("생성일: "+
            DateTime.Now.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("## 현재 규칙");
        builder.AppendLine();
        builder.AppendLine(
            "- 보상: `floor(완료 Wave × 지역 배율) + 완주 보너스`");
        builder.AppendLine("- 실패해도 완료한 Wave만큼 지급");
        builder.AppendLine("- 지역 상자: 10 / 13 / 16 / 20");
        builder.AppendLine("- 계열 상자: 18 → 22 → 27 → 33");
        builder.AppendLine();
        builder.AppendLine("## 진행 구간별 보상");
        builder.AppendLine();
        builder.AppendLine(
            "| 지역 | Wave 3 실패<br>(2 완료) | Wave 6 실패<br>(5 완료) | "+
            "Wave 9 실패<br>(8 완료) | Wave 12 실패<br>(11 완료) | 완주 | 지역 상자 |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach(HunterRegionDefinition region in regions)
        {
            List<int> rewards=MilestoneCompletedWaves
                .Select(completed=>
                    HunterTokenRewardSystem.Calculate(
                        region.id,
                        completed,
                        TotalWaves,
                        completed==TotalWaves))
                .ToList();
            builder.AppendLine(
                "| "+region.displayName+
                " | "+rewards[0]+
                " | "+rewards[1]+
                " | "+rewards[2]+
                " | "+rewards[3]+
                " | "+rewards[4]+
                " | "+region.regionBoxCost+" |");
        }

        int[] familyCosts=FamilyCumulativeCosts();
        builder.AppendLine();
        builder.AppendLine("## 계열 상자 누적 구매 주기");
        builder.AppendLine();
        builder.AppendLine(
            "| 지역 | 1개<br>(18) | 2개<br>(40) | 3개<br>(67) | 4개<br>(100) |");
        builder.AppendLine("|---|---:|---:|---:|---:|");
        foreach(HunterRegionDefinition region in regions)
        {
            int clearReward=HunterTokenRewardSystem.Calculate(
                region.id,TotalWaves,TotalWaves,true);
            builder.AppendLine(
                "| "+region.displayName+
                " | "+CeilDiv(familyCosts[0],clearReward)+"회"+
                " | "+CeilDiv(familyCosts[1],clearReward)+"회"+
                " | "+CeilDiv(familyCosts[2],clearReward)+"회"+
                " | "+CeilDiv(familyCosts[3],clearReward)+"회 |");
        }

        builder.AppendLine();
        builder.AppendLine("## 진단");
        builder.AppendLine();
        builder.AppendLine(
            "- 모든 지역에서 완주 1회로 지역 상자 1개를 구매할 수 있다.");
        builder.AppendLine(
            "- 어떤 지역도 완주 1회만으로 지역 상자 2개를 구매할 수 없다.");
        builder.AppendLine(
            "- Wave 6 실패 보상은 완주 보상의 약 30~33%라 실패 누적이 남는다.");
        builder.AppendLine(
            "- 계열 상자는 지역 상자보다 명확히 비싸며, 연속 구매 시 선택 압박이 커진다.");
        builder.AppendLine(
            "- 현재 값은 첫 플레이테스트 기준으로 유지하고 지역별 탐험 결과 20회 이상을 "+
            "모은 뒤 조정한다.");
        return builder.ToString();
    }

    public static string BuildCsv()
    {
        StringBuilder builder=new StringBuilder();
        builder.AppendLine(
            "region_id,display_name,completed_wave,cleared,reward,"+
            "region_box_cost,reward_to_box_ratio,can_buy_region_box");
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            for(int completed=0;completed<=TotalWaves;completed++)
            {
                bool cleared=completed==TotalWaves;
                int reward=HunterTokenRewardSystem.Calculate(
                    region.id,completed,TotalWaves,cleared);
                float ratio=region.regionBoxCost<=0
                    ?0f:(float)reward/region.regionBoxCost;
                builder.Append(region.id).Append(',')
                    .Append(EscapeCsv(region.displayName)).Append(',')
                    .Append(completed).Append(',')
                    .Append(cleared?"TRUE":"FALSE").Append(',')
                    .Append(reward).Append(',')
                    .Append(region.regionBoxCost).Append(',')
                    .Append(ratio.ToString(
                        "0.000",CultureInfo.InvariantCulture)).Append(',')
                    .Append(reward>=region.regionBoxCost?"TRUE":"FALSE")
                    .AppendLine();
            }
        }
        return builder.ToString();
    }

    private static int[] FamilyCumulativeCosts()
    {
        int[] result=new int[4];
        int total=0;
        for(int purchase=0;purchase<4;purchase++)
        {
            total+=HunterShopSystem.CurrentFamilyBoxCost(
                new PlayerProfileSave
                {
                    hunterFamilyPurchaseCount=purchase
                });
            result[purchase]=total;
        }
        return result;
    }

    private static int CeilDiv(int value,int divisor)
    {
        return divisor<=0?0:(value+divisor-1)/divisor;
    }

    private static string EscapeCsv(string value)
    {
        if(string.IsNullOrEmpty(value))return "";
        if(!value.Contains(",")&&!value.Contains("\"")&&
           !value.Contains("\n")&&!value.Contains("\r"))
            return value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
}
#endif
