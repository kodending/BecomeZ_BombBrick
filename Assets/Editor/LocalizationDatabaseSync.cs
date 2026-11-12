#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class LocalizationDatabaseSync
{
    private const string SpreadsheetId = "YOUR_SPREADSHEET_ID";
    private const string SheetName = "Localization_Master";
    private const string OutputPath = "Assets/Resources/Localization/LocalizationDatabase.json";
    private static UnityWebRequest request;

    [MenuItem("Tools/Game Database/Sync Localization from Google Sheet")]
    public static void Sync()
    {
        if (request != null)
        {
            Debug.LogWarning("[Localization Sync] 이미 동기화 중입니다.");
            return;
        }
        string url = GameMasterDatabaseSync.CsvUrlForSheet(SheetName);
        request = UnityWebRequest.Get(url);
        request.timeout = 25;
        request.SendWebRequest();
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    [MenuItem("Tools/Game Database/Open Localization Master Sheet")]
    public static void OpenSheet()
    {
        Application.OpenURL("https://docs.google.com/spreadsheets/d/" + SpreadsheetId +
                            "/edit#gid=1858644570");
    }

    [MenuItem("Tools/Game Database/Preview Localization/Korean")]
    public static void PreviewKorean() => GameLocalization.SetLanguage(GameLocalization.Korean);

    [MenuItem("Tools/Game Database/Preview Localization/English")]
    public static void PreviewEnglish() => GameLocalization.SetLanguage(GameLocalization.English);

    public static void ImportCsv(string csv)
    {
        List<List<string>> table = ParseCsv(csv);
        if (table.Count == 0) throw new InvalidDataException("Localization_Master CSV가 비어 있습니다.");
        Dictionary<string, int> columns = Columns(table[0]);
        string[] required = { "loc_key", "category", "source_ref", "ko", "en", "status",
            "review_note", "enabled", "data_version" };
        foreach (string column in required)
            if (!columns.ContainsKey(column))
                throw new InvalidDataException("필수 열 누락: " + column);

        LocalizationDatabaseFile file = new LocalizationDatabaseFile();
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, string> englishByKorean =
            new Dictionary<string, string>(StringComparer.Ordinal);
        int missingEnglish = 0;
        for (int row = 1; row < table.Count; row++)
        {
            string key = Cell(table[row], columns, "loc_key").Trim();
            string korean = Decode(Cell(table[row], columns, "ko"));
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(korean)) continue;
            if (string.IsNullOrEmpty(key)) throw new InvalidDataException("loc_key 누락: " + (row + 1) + "행");
            if (string.IsNullOrEmpty(korean)) throw new InvalidDataException("ko 누락: " + key);
            if (!keys.Add(key)) throw new InvalidDataException("loc_key 중복: " + key);
            bool enabled = Bool(Cell(table[row], columns, "enabled"), true);
            string english = Decode(Cell(table[row], columns, "en"));
            if (enabled && string.IsNullOrWhiteSpace(english)) missingEnglish++;
            if (enabled && !string.IsNullOrWhiteSpace(english))
            {
                if (englishByKorean.TryGetValue(korean, out string previousEnglish) &&
                    !string.Equals(previousEnglish, english, StringComparison.Ordinal))
                    Debug.LogWarning("[Localization Sync] 같은 한국어 원문의 영어가 충돌함: " + korean +
                                     " (" + previousEnglish + " / " + english + ")");
                else englishByKorean[korean] = english;
            }
            file.entries.Add(new LocalizationEntry
            {
                key = key,
                category = Cell(table[row], columns, "category"),
                sourceRef = Cell(table[row], columns, "source_ref"),
                ko = korean,
                en = english,
                status = Cell(table[row], columns, "status"),
                reviewNote = Decode(Cell(table[row], columns, "review_note")),
                enabled = enabled,
                dataVersion = Int(Cell(table[row], columns, "data_version"), 1)
            });
        }
        file.entries.Sort((left, right) => string.CompareOrdinal(left.key, right.key));
        string directory = Path.GetDirectoryName(OutputPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(OutputPath, JsonUtility.ToJson(file, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
        GameLocalization.Reload();
        Debug.Log("[Localization Sync] " + file.entries.Count + "개 문구 적용" +
                  (missingEnglish > 0 ? " · 영어 미입력 " + missingEnglish + "개" : " · 영어 누락 없음"));
    }

    private static void Poll()
    {
        if (request == null || !request.isDone) return;
        EditorApplication.update -= Poll;
        UnityWebRequest completed = request;
        request = null;
        if (completed.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[Localization Sync] 다운로드 실패: " + completed.error);
            completed.Dispose();
            return;
        }
        string csv = Encoding.UTF8.GetString(completed.downloadHandler.data);
        completed.Dispose();
        try { ImportCsv(csv); }
        catch (Exception exception) { Debug.LogError("[Localization Sync] 적용 실패: " + exception.Message); }
    }

    private static Dictionary<string, int> Columns(List<string> header)
    {
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++) result[header[i].Trim()] = i;
        return result;
    }

    private static string Cell(List<string> row, Dictionary<string, int> columns, string name)
    {
        int index = columns[name];
        return index >= 0 && index < row.Count ? row[index] : "";
    }

    private static bool Bool(string value, bool fallback)
    {
        if (bool.TryParse(value, out bool result)) return result;
        if (int.TryParse(value, out int number)) return number != 0;
        return fallback;
    }

    private static int Int(string value, int fallback) =>
        int.TryParse(value, out int result) ? result : fallback;

    private static string Decode(string value) =>
        (value ?? "").Replace("\\n", "\n");

    private static List<List<string>> ParseCsv(string csv)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char character = csv[i];
            if (quoted)
            {
                if (character == '"' && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    cell.Append('"'); i++;
                }
                else if (character == '"') quoted = false;
                else cell.Append(character);
            }
            else if (character == '"') quoted = true;
            else if (character == ',') { row.Add(cell.ToString()); cell.Length = 0; }
            else if (character == '\n')
            {
                row.Add(cell.ToString().TrimEnd('\r')); cell.Length = 0;
                rows.Add(row); row = new List<string>();
            }
            else cell.Append(character);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString().TrimEnd('\r'));
            rows.Add(row);
        }
        return rows;
    }
}
#endif
