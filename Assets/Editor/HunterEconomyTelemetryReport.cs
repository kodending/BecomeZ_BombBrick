#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HunterEconomyTelemetryReport
{
    private const string OutputFolder="Docs/BalanceReports";

    [MenuItem(
        "BombBrick/Balance/Generate Hunter Economy Telemetry Summary")]
    public static void Generate()
    {
        HunterEconomyTelemetrySummary summary=
            HunterEconomyTelemetry.Analyze(
                HunterEconomyTelemetry.ReadAll());
        string projectRoot=Directory.GetParent(
            Application.dataPath).FullName;
        string outputDirectory=Path.Combine(projectRoot,OutputFolder);
        Directory.CreateDirectory(outputDirectory);
        string markdownPath=Path.Combine(
            outputDirectory,
            "Hunter_Economy_Playtest_Summary.md");
        string csvPath=Path.Combine(
            outputDirectory,
            "Hunter_Economy_Playtest_Summary.csv");
        File.WriteAllText(
            markdownPath,
            BuildMarkdown(summary),
            new UTF8Encoding(false));
        File.WriteAllText(
            csvPath,
            BuildCsv(summary),
            new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log(
            "[Hunter Economy] Telemetry summary generated. events="+
            summary.expeditionCount+
            " path="+markdownPath);
    }

    public static string BuildMarkdown(
        HunterEconomyTelemetrySummary summary)
    {
        if(summary==null)
            summary=new HunterEconomyTelemetrySummary();
        StringBuilder builder=new StringBuilder();
        builder.AppendLine("# 사냥의 증표 실전 플레이 요약");
        builder.AppendLine();
        builder.AppendLine(
            "생성일: "+
            DateTime.Now.ToString(
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("## 전체");
        builder.AppendLine();
        builder.AppendLine("| 항목 | 값 |");
        builder.AppendLine("|---|---:|");
        builder.AppendLine(
            "| 완료 탐험 | "+summary.expeditionCount+" |");
        builder.AppendLine(
            "| 완주율 | "+Percent(summary.ClearRate)+" |");
        builder.AppendLine(
            "| 평균 완료 Wave | "+
            summary.AverageCompletedWave.ToString(
                "0.00",CultureInfo.InvariantCulture)+" |");
        builder.AppendLine(
            "| 증표 획득 | "+summary.tokensEarned+" |");
        builder.AppendLine(
            "| 증표 소비 | "+summary.tokensSpent+" |");
        builder.AppendLine(
            "| 순증감 | "+summary.NetTokens+" |");
        builder.AppendLine(
            "| 지역 상자 구매 | "+
            summary.regionBoxesPurchased+" |");
        builder.AppendLine(
            "| 계열 상자 구매 | "+
            summary.familyBoxesPurchased+" |");
        builder.AppendLine(
            "| 중복 종 획득 | "+
            summary.repeatSpeciesPurchases+" |");

        builder.AppendLine();
        builder.AppendLine("## 지역별");
        builder.AppendLine();
        builder.AppendLine(
            "| 지역 | 탐험 | 완주율 | 평균 완료 Wave | 증표 획득 | 판정 |");
        builder.AppendLine("|---|---:|---:|---:|---:|---|");
        foreach(HunterEconomyRegionSummary region in summary.regions)
        {
            builder.AppendLine(
                "| "+region.displayName+
                " | "+region.expeditions+
                " | "+Percent(region.ClearRate)+
                " | "+region.AverageCompletedWave.ToString(
                    "0.00",CultureInfo.InvariantCulture)+
                " | "+region.tokensEarned+
                " | "+(region.expeditions>=20
                    ?"조정 검토 가능"
                    :"표본 부족 ("+region.expeditions+"/20)")+
                " |");
        }
        if(summary.regions.Count==0)
            builder.AppendLine(
                "| 기록 없음 | 0 | 0.0% | 0.00 | 0 | 표본 부족 (0/20) |");

        builder.AppendLine();
        builder.AppendLine("## 해석 기준");
        builder.AppendLine();
        builder.AppendLine(
            "- 지역별 탐험 결과가 20회 미만이면 가격과 보상 수치를 변경하지 않는다.");
        builder.AppendLine(
            "- 완주율, 평균 완료 Wave, 증표 순증감과 상자 구매 빈도를 함께 본다.");
        builder.AppendLine(
            "- 중복 종 획득률이 높아지면 상자 가격보다 중복 보상 정책을 먼저 검토한다.");
        return builder.ToString();
    }

    public static string BuildCsv(
        HunterEconomyTelemetrySummary summary)
    {
        if(summary==null)
            summary=new HunterEconomyTelemetrySummary();
        StringBuilder builder=new StringBuilder();
        builder.AppendLine(
            "region_id,display_name,expeditions,clears,clear_rate,"+
            "average_completed_wave,tokens_earned,sample_ready");
        foreach(HunterEconomyRegionSummary region in summary.regions)
        {
            builder.Append(region.regionId).Append(',')
                .Append(EscapeCsv(region.displayName)).Append(',')
                .Append(region.expeditions).Append(',')
                .Append(region.clears).Append(',')
                .Append(region.ClearRate.ToString(
                    "0.000",CultureInfo.InvariantCulture)).Append(',')
                .Append(region.AverageCompletedWave.ToString(
                    "0.000",CultureInfo.InvariantCulture)).Append(',')
                .Append(region.tokensEarned).Append(',')
                .Append(region.expeditions>=20?"TRUE":"FALSE")
                .AppendLine();
        }
        return builder.ToString();
    }

    private static string Percent(float value)
    {
        return (Mathf.Clamp01(value)*100f).ToString(
            "0.0",CultureInfo.InvariantCulture)+"%";
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
