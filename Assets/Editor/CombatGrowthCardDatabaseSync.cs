using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class CombatGrowthCardDatabaseSync
{
    private const string SpreadsheetId="YOUR_SPREADSHEET_ID";
    private const string SheetName="Growth_Card_Master";
    private static string CsvUrl=>GameMasterDatabaseSync.CsvUrlForSheet(SheetName);
    private const string DatabaseFolder="Assets/Resources/CombatData";
    private const string DatabasePath=DatabaseFolder+"/CombatGrowthCardDatabase.asset";
    private const string SnapshotFolder="Assets/CombatData/Generated";
    private const string SnapshotPath=SnapshotFolder+"/GrowthCardMasterSnapshot.json";

    private static readonly string[] ExpectedHeaders=
    {
        "card_id","display_name","effect","common_value","rare_value","epic_value",
        "legendary_value","common_secondary_value","rare_secondary_value",
        "epic_secondary_value","legendary_secondary_value","trigger_rule","stacking_rule",
        "reset_rule","enabled","data_version"
    };

    private static UnityWebRequest request;

    [MenuItem("Tools/Game Database/Sync Growth Cards from Google Sheet")]
    public static void SyncFromGoogleSheet()
    {
        if(request!=null)
        {
            Debug.LogWarning("[Growth Card DB Sync] 이미 동기화 중입니다.");
            return;
        }

        request=UnityWebRequest.Get(CsvUrl);
        request.timeout=20;
        request.SendWebRequest();
        EditorApplication.update-=Poll;
        EditorApplication.update+=Poll;
        Debug.Log("[Growth Card DB Sync] Growth_Card_Master 다운로드를 시작했습니다.");
    }

    public static int ImportCsv(string csv)
    {
        List<List<string>> table=ParseCsv(csv);
        if(table.Count<2)throw new InvalidDataException("Growth_Card_Master에 데이터 행이 없습니다.");

        Dictionary<string,int> columns=BuildColumnMap(table[0]);
        List<string> missing=ExpectedHeaders.Where(header=>!columns.ContainsKey(header)).ToList();
        if(missing.Count>0)throw new InvalidDataException("필수 헤더 누락: "+string.Join(", ",missing));

        List<CombatGrowthCardDatabaseRow> rows=new List<CombatGrowthCardDatabaseRow>();
        HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        for(int rowIndex=1;rowIndex<table.Count;rowIndex++)
        {
            List<string> cells=table[rowIndex];
            string cardId=Cell(cells,columns,"card_id").Trim();
            if(string.IsNullOrWhiteSpace(cardId))continue;
            int sheetRow=rowIndex+1;
            if(!Regex.IsMatch(cardId,"^[a-z0-9_]+$"))
                throw new InvalidDataException("행 "+sheetRow+": card_id 형식 오류: "+cardId);
            if(!ids.Add(cardId))
                throw new InvalidDataException("행 "+sheetRow+": 중복 card_id: "+cardId);

            string displayName=Cell(cells,columns,"display_name").Trim();
            if(string.IsNullOrWhiteSpace(displayName))
                throw new InvalidDataException("행 "+sheetRow+": display_name이 비어 있습니다.");
            if(!Enum.TryParse(Cell(cells,columns,"effect").Trim(),true,out CombatGrowthCardEffect effect))
                throw new InvalidDataException("행 "+sheetRow+": 알 수 없는 effect입니다.");

            CombatGrowthCardDatabaseRow row=new CombatGrowthCardDatabaseRow
            {
                cardId=cardId,
                displayName=displayName,
                effect=effect,
                commonValue=ParseFloat(cells,columns,"common_value",sheetRow),
                rareValue=ParseFloat(cells,columns,"rare_value",sheetRow),
                epicValue=ParseFloat(cells,columns,"epic_value",sheetRow),
                legendaryValue=ParseFloat(cells,columns,"legendary_value",sheetRow),
                commonSecondaryValue=ParseFloat(cells,columns,"common_secondary_value",sheetRow),
                rareSecondaryValue=ParseFloat(cells,columns,"rare_secondary_value",sheetRow),
                epicSecondaryValue=ParseFloat(cells,columns,"epic_secondary_value",sheetRow),
                legendarySecondaryValue=ParseFloat(cells,columns,"legendary_secondary_value",sheetRow),
                triggerRule=Cell(cells,columns,"trigger_rule").Trim(),
                stackingRule=Cell(cells,columns,"stacking_rule").Trim(),
                resetRule=Cell(cells,columns,"reset_rule").Trim(),
                enabled=ParseBool(cells,columns,"enabled",sheetRow),
                dataVersion=ParseInt(cells,columns,"data_version",sheetRow)
            };
            if(row.commonValue<0f||row.rareValue<row.commonValue||
               row.epicValue<row.rareValue||row.legendaryValue<row.epicValue)
                throw new InvalidDataException("행 "+sheetRow+": 희귀도 수치는 0 이상 오름차순이어야 합니다.");
            if(row.commonSecondaryValue<0f||row.rareSecondaryValue<row.commonSecondaryValue||
               row.epicSecondaryValue<row.rareSecondaryValue||
               row.legendarySecondaryValue<row.epicSecondaryValue)
                throw new InvalidDataException("행 "+sheetRow+": 보조 수치는 0 이상 오름차순이어야 합니다.");
            if(row.dataVersion<1)
                throw new InvalidDataException("행 "+sheetRow+": data_version은 1 이상이어야 합니다.");
            rows.Add(row);
        }

        if(rows.Count<CombatGrowthCardDraftService.DraftSize)
            throw new InvalidDataException("활성 여부와 관계없이 카드가 3개 미만입니다.");

        Undo.IncrementCurrentGroup();
        EnsureFolder("Assets/Resources","CombatData");
        CombatGrowthCardDatabase database=
            AssetDatabase.LoadAssetAtPath<CombatGrowthCardDatabase>(DatabasePath);
        if(database==null)
        {
            database=ScriptableObject.CreateInstance<CombatGrowthCardDatabase>();
            AssetDatabase.CreateAsset(database,DatabasePath);
        }
        else
        {
            Undo.RecordObject(database,"Sync Combat Growth Card Database");
        }

        database.cards=rows;
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        // Keep later test-runner Undo cleanup outside this import.
        Undo.IncrementCurrentGroup();

        EnsureFolder("Assets","CombatData");
        EnsureFolder("Assets/CombatData","Generated");
        GrowthCardSnapshot snapshot=new GrowthCardSnapshot
        {
            spreadsheetId=SpreadsheetId,
            sheetName=SheetName,
            downloadedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture),
            rows=rows
        };
        File.WriteAllText(SnapshotPath,JsonUtility.ToJson(snapshot,true),new UTF8Encoding(false));
        AssetDatabase.ImportAsset(SnapshotPath,ImportAssetOptions.ForceUpdate);
        CombatGrowthCardCatalog.ReloadFromResources();
        Debug.Log("[Growth Card DB Sync] 적용 완료: "+rows.Count+"개 카드");
        return rows.Count;
    }

    private static void Poll()
    {
        if(request==null||!request.isDone)return;
        EditorApplication.update-=Poll;
        UnityWebRequest completed=request;
        request=null;
        try
        {
            if(completed.result!=UnityWebRequest.Result.Success)
                throw new InvalidOperationException("다운로드 실패: "+completed.error);
            string csv=Encoding.UTF8.GetString(completed.downloadHandler.data);
            ImportCsv(csv);
        }
        catch(Exception exception)
        {
            Debug.LogError("[Growth Card DB Sync] "+exception.Message);
        }
        finally
        {
            completed.Dispose();
        }
    }

    private static void EnsureFolder(string parent,string child)
    {
        string path=parent+"/"+child;
        if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,child);
    }

    private static Dictionary<string,int> BuildColumnMap(List<string> header)
    {
        Dictionary<string,int> result=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<header.Count;i++)
        {
            string key=(header[i]??string.Empty).Trim();
            if(!string.IsNullOrEmpty(key)&&!result.ContainsKey(key))result.Add(key,i);
        }
        return result;
    }

    private static string Cell(List<string> cells,Dictionary<string,int> columns,string key)
    {
        return columns.TryGetValue(key,out int index)&&index>=0&&index<cells.Count
            ?cells[index]
            :string.Empty;
    }

    private static float ParseFloat(
        List<string> cells,Dictionary<string,int> columns,string key,int row)
    {
        string raw=Cell(cells,columns,key).Trim();
        if(float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out float value))
            return value;
        throw new InvalidDataException("행 "+row+": "+key+" 숫자 형식 오류: "+raw);
    }

    private static int ParseInt(
        List<string> cells,Dictionary<string,int> columns,string key,int row)
    {
        string raw=Cell(cells,columns,key).Trim();
        if(int.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value))
            return value;
        throw new InvalidDataException("행 "+row+": "+key+" 정수 형식 오류: "+raw);
    }

    private static bool ParseBool(
        List<string> cells,Dictionary<string,int> columns,string key,int row)
    {
        string raw=Cell(cells,columns,key).Trim();
        if(bool.TryParse(raw,out bool value))return value;
        if(raw=="1")return true;
        if(raw=="0")return false;
        throw new InvalidDataException("행 "+row+": "+key+"는 TRUE/FALSE여야 합니다.");
    }

    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows=new List<List<string>>();
        List<string> row=new List<string>();
        StringBuilder field=new StringBuilder();
        bool quoted=false;
        for(int i=0;i<text.Length;i++)
        {
            char character=text[i];
            if(character=='"')
            {
                if(quoted&&i+1<text.Length&&text[i+1]=='"'){field.Append('"');i++;}
                else quoted=!quoted;
            }
            else if(character==','&&!quoted){row.Add(field.ToString());field.Length=0;}
            else if((character=='\n'||character=='\r')&&!quoted)
            {
                if(character=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;
                row.Add(field.ToString());field.Length=0;
                if(row.Any(value=>!string.IsNullOrEmpty(value)))rows.Add(row);
                row=new List<string>();
            }
            else field.Append(character);
        }
        row.Add(field.ToString());
        if(row.Any(value=>!string.IsNullOrEmpty(value)))rows.Add(row);
        return rows;
    }

    [Serializable]
    private sealed class GrowthCardSnapshot
    {
        public string spreadsheetId;
        public string sheetName;
        public string downloadedUtc;
        public List<CombatGrowthCardDatabaseRow> rows;
    }
}
