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

public sealed class MonsterDatabaseSyncWindow : EditorWindow
{
    private const string SpreadsheetId = "YOUR_SPREADSHEET_ID";
    private const string SheetName = "Monster_Master";
    private const string SheetUrl = "https://docs.google.com/spreadsheets/d/" + SpreadsheetId + "/edit#gid=1002012538";
    private static string CsvUrl => GameMasterDatabaseSync.CsvUrlForSheet(SheetName);
    private const string DatabasePath = "Assets/Resources/MonsterData/MonsterDatabase.asset";
    private const string DefinitionFolder = "Assets/Resources/MonsterData";
    private const string SnapshotPath = "Assets/MonsterData/Generated/MonsterMasterSnapshot.json";
    private const string BackupFolder = "Library/MonsterDataSync/Backups";

    private static readonly string[] ExpectedHeaders =
    {
        "monster_id", "display_name", "family_id", "default_grade", "resource_path",
        "animation_set_id", "visual_scale", "model_height", "ground_span_tile_ratio",
        "collision_radius", "tactic", "max_hp", "attack_damage", "attack_interval",
        "move_speed_tiles_per_second", "attack_range", "basic_attack_id", "skill_id",
        "defense", "status_resistance",
        "enabled", "data_version", "portrait_sprite_path", "serial_number", "intrinsic_trait_id"
    };

    private UnityWebRequest request;
    private MonsterMasterSnapshot snapshot;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<DiffEntry> diffs = new List<DiffEntry>();
    private Vector2 scroll;
    private bool downloading;
    private string status = "동기화를 시작하면 Google Sheet를 읽고 변경사항을 미리 보여줍니다.";

    [MenuItem("Tools/Monster Database/Sync & Validate")]
    public static void OpenAndSync()
    {
        MonsterDatabaseSyncWindow window = GetWindow<MonsterDatabaseSyncWindow>("Monster DB Sync");
        window.minSize = new Vector2(720f, 440f);
        window.StartDownload();
    }

    [MenuItem("Tools/Monster Database/Open Monster_Master Sheet")]
    public static void OpenSheet()
    {
        Application.OpenURL(SheetUrl);
    }

    public static bool ImportCsvAndApply(string csv,out string message)
    {
        MonsterDatabaseSyncWindow window=CreateInstance<MonsterDatabaseSyncWindow>();
        try
        {
            window.ParseAndPreview(csv,true);
            if(window.errors.Count>0)
            {
                message=string.Join("\n",window.errors);
                return false;
            }
            window.ApplySnapshot();
            message=window.status;
            return window.errors.Count==0;
        }
        finally
        {
            DestroyImmediate(window);
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= PollDownload;
        if (request != null)
        {
            request.Dispose();
            request = null;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Monster_Master → Unity", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(6f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !downloading;
            if (GUILayout.Button("Google Sheet 다시 읽기", GUILayout.Height(28f)))
                StartDownload();
            if (GUILayout.Button("로컬 스냅샷 불러오기", GUILayout.Height(28f)))
                LoadLocalSnapshot();
            if (GUILayout.Button("시트 열기", GUILayout.Height(28f)))
                OpenSheet();
            GUI.enabled = true;
        }

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawMessages("차단 오류", errors, MessageType.Error);
        DrawMessages("경고", warnings, MessageType.Warning);

        if (snapshot != null)
        {
            EditorGUILayout.LabelField(
                "미리보기 (" + snapshot.rows.Count + "행 / " + diffs.Count + "개 비교)",
                EditorStyles.boldLabel);
            foreach (DiffEntry diff in diffs)
            {
                MessageType type = diff.kind == DiffKind.New || diff.kind == DiffKind.Update
                    ? MessageType.Info
                    : diff.kind == DiffKind.Disabled || diff.kind == DiffKind.Preserved
                        ? MessageType.Warning
                        : MessageType.None;
                EditorGUILayout.HelpBox(diff.label, type);
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6f);
        GUI.enabled = snapshot != null && errors.Count == 0 && !downloading;
        if (GUILayout.Button("검증된 변경사항 Unity DB에 적용", GUILayout.Height(34f)))
            ApplySnapshot();
        GUI.enabled = true;
    }

    private static void DrawMessages(string title, List<string> messages, MessageType type)
    {
        if (messages.Count == 0)
            return;

        EditorGUILayout.LabelField(title + " (" + messages.Count + ")", EditorStyles.boldLabel);
        foreach (string message in messages)
            EditorGUILayout.HelpBox(message, type);
        EditorGUILayout.Space(4f);
    }

    private void StartDownload()
    {
        if (downloading)
            return;

        ResetPreview();
        downloading = true;
        status = "Google Sheet의 " + SheetName + " 탭을 내려받는 중...";
        request = UnityWebRequest.Get(CsvUrl);
        request.timeout = 20;
        request.SendWebRequest();
        EditorApplication.update -= PollDownload;
        EditorApplication.update += PollDownload;
        Repaint();
    }

    private void PollDownload()
    {
        if (request == null || !request.isDone)
            return;

        EditorApplication.update -= PollDownload;
        downloading = false;

        if (request.result != UnityWebRequest.Result.Success)
        {
            status = "Google Sheet 다운로드 실패. 기존 DB는 변경되지 않았습니다.";
            errors.Add("다운로드 실패: " + request.error +
                       "\n공유 설정이 '링크가 있는 모든 사용자 - 보기 가능'인지 확인하세요.");
            request.Dispose();
            request = null;
            Repaint();
            return;
        }

        string csv = Encoding.UTF8.GetString(request.downloadHandler.data);
        request.Dispose();
        request = null;
        ParseAndPreview(csv, true);
    }

    private void LoadLocalSnapshot()
    {
        ResetPreview();
        if (!File.Exists(SnapshotPath))
        {
            errors.Add("로컬 스냅샷이 없습니다: " + SnapshotPath);
            status = "불러오기 실패";
            return;
        }

        try
        {
            snapshot = JsonUtility.FromJson<MonsterMasterSnapshot>(
                File.ReadAllText(SnapshotPath, Encoding.UTF8));
            if (snapshot == null || snapshot.rows == null)
                throw new InvalidDataException("스냅샷 JSON 구조가 올바르지 않습니다.");

            ValidateAndBuildDiff();
            status = errors.Count == 0
                ? "로컬 스냅샷 검증 완료"
                : "로컬 스냅샷에 차단 오류가 있습니다.";
        }
        catch (Exception exception)
        {
            snapshot = null;
            errors.Add("스냅샷 읽기 실패: " + exception.Message);
            status = "불러오기 실패";
        }
    }

    private void ParseAndPreview(string csv, bool saveValidSnapshot)
    {
        ResetPreview();
        List<List<string>> table = ParseCsv(csv);
        if (table.Count == 0)
        {
            errors.Add("시트가 비어 있습니다.");
            status = "검증 실패";
            return;
        }

        Dictionary<string, int> columns = BuildColumnMap(table[0]);
        foreach (string header in ExpectedHeaders)
        {
            if (!columns.ContainsKey(header))
                errors.Add("필수 열 누락: " + header);
        }

        if (errors.Count > 0)
        {
            status = "헤더 검증 실패. 기존 DB는 변경되지 않았습니다.";
            return;
        }

        snapshot = new MonsterMasterSnapshot
        {
            spreadsheetId = SpreadsheetId,
            sheetName = SheetName,
            downloadedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            rows = new List<MonsterMasterRow>()
        };

        for (int rowIndex = 1; rowIndex < table.Count; rowIndex++)
        {
            List<string> cells = table[rowIndex];
            if (string.IsNullOrWhiteSpace(Cell(cells, columns, "monster_id")))
                continue;

            MonsterMasterRow row = ParseRow(cells, columns, rowIndex + 1);
            if (row != null)
                snapshot.rows.Add(row);
        }

        ValidateAndBuildDiff();
        if (errors.Count == 0 && saveValidSnapshot)
        {
            SaveSnapshotWithBackup();
            status = "Google Sheet 검증 완료. 적용 전 변경사항을 확인하세요.";
        }
        else
        {
            status = "검증 실패. 기존 DB와 마지막 정상 스냅샷은 유지됩니다.";
        }
    }

    private MonsterMasterRow ParseRow(
        List<string> cells,
        Dictionary<string, int> columns,
        int sheetRow)
    {
        MonsterMasterRow row = new MonsterMasterRow();
        row.sheetRow = sheetRow;
        row.monsterId = Cell(cells, columns, "monster_id").Trim();
        row.serialNumber = Cell(cells, columns, "serial_number").Trim();
        if (int.TryParse(row.serialNumber, out int serialNumber))
            row.serialNumber = serialNumber.ToString("000");
        row.intrinsicTraitId = Cell(cells, columns, "intrinsic_trait_id").Trim();
        row.displayName = Cell(cells, columns, "display_name").Trim();
        row.familyId = Cell(cells, columns, "family_id").Trim();
        row.defaultGrade = Cell(cells, columns, "default_grade").Trim();
        row.resourcePath = Cell(cells, columns, "resource_path").Trim();
        row.portraitResourcePath = Cell(cells, columns, "portrait_sprite_path").Trim();
        row.animationSetId = Cell(cells, columns, "animation_set_id").Trim();
        row.tactic = Cell(cells, columns, "tactic").Trim();
        row.basicAttackId = Cell(cells, columns, "basic_attack_id").Trim();
        row.skillId = Cell(cells, columns, "skill_id").Trim();

        bool valid = true;
        valid &= TryFloat(cells, columns, "visual_scale", sheetRow, out row.visualScale);
        valid &= TryFloat(cells, columns, "model_height", sheetRow, out row.modelHeight);
        valid &= TryFloat(cells, columns, "ground_span_tile_ratio", sheetRow, out row.groundSpanTileRatio);
        valid &= TryFloat(cells, columns, "collision_radius", sheetRow, out row.collisionRadius);
        valid &= TryFloat(cells, columns, "max_hp", sheetRow, out row.maxHp);
        valid &= TryFloat(cells, columns, "attack_damage", sheetRow, out row.attackDamage);
        valid &= TryFloat(cells, columns, "attack_interval", sheetRow, out row.attackInterval);
        valid &= TryFloat(cells, columns, "move_speed_tiles_per_second", sheetRow, out row.moveSpeedTilesPerSecond);
        valid &= TryFloat(
            cells,
            columns,
            "attack_range",
            sheetRow,
            out float attackRange);
        row.attackRange = Mathf.Max(1, Mathf.RoundToInt(attackRange));
        valid &= TryOptionalFloat(cells, columns, "defense", sheetRow, out row.defense);
        valid &= TryOptionalFloat(cells, columns, "status_resistance", sheetRow, out row.statusResistance);
        valid &= TryBool(cells, columns, "enabled", sheetRow, out row.enabled);
        valid &= TryInt(cells, columns, "data_version", sheetRow, out row.dataVersion);
        return valid ? row : null;
    }

    private void ValidateAndBuildDiff()
    {
        diffs.Clear();
        if (snapshot == null || snapshot.rows == null)
        {
            errors.Add("몬스터 행 데이터가 없습니다.");
            return;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MonsterMasterRow row in snapshot.rows)
        {
            string prefix = "행 " + row.sheetRow + " (" + row.monsterId + "): ";
            if (!Regex.IsMatch(row.monsterId ?? string.Empty, "^[a-z0-9_]+$"))
                errors.Add(prefix + "monster_id는 영문 소문자, 숫자, 밑줄만 사용할 수 있습니다.");
            if (!ids.Add(row.monsterId ?? string.Empty))
                errors.Add(prefix + "중복 monster_id입니다.");
            if (row.enabled && !Regex.IsMatch(row.serialNumber ?? string.Empty, "^[0-9]{3}$"))
                errors.Add(prefix + "serial_number는 001 형식의 세 자리 숫자여야 합니다.");
            if (string.IsNullOrWhiteSpace(row.displayName))
                errors.Add(prefix + "display_name이 비어 있습니다.");
            if (string.IsNullOrWhiteSpace(row.familyId))
                errors.Add(prefix + "family_id가 비어 있습니다.");
            if (!Enum.TryParse(row.defaultGrade, true, out MonsterGrade _))
                errors.Add(prefix + "default_grade가 Common/Rare/Epic/Legendary 중 하나가 아닙니다.");
            if (row.enabled && string.IsNullOrWhiteSpace(row.resourcePath))
                errors.Add(prefix + "resource_path가 비어 있습니다.");
            if (row.visualScale <= 0f || row.modelHeight <= 0f ||
                row.groundSpanTileRatio <= 0f || row.collisionRadius <= 0f)
                errors.Add(prefix + "표현·충돌 수치는 모두 0보다 커야 합니다.");
            if (row.maxHp <= 0f || row.attackDamage < 0f || row.attackInterval <= 0f ||
                row.moveSpeedTilesPerSecond <= 0f || row.attackRange < 1 ||
                row.defense < 0f || row.statusResistance < 0f)
                errors.Add(prefix + "전투 수치 범위를 확인하세요.");
            if (row.dataVersion < 1)
                errors.Add(prefix + "data_version은 1 이상이어야 합니다.");

            if (row.enabled)
            {
                string prefabPath =
                    "Assets/Resources/" + row.resourcePath + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    warnings.Add(
                        prefix + "프리팹을 찾지 못했습니다: " + prefabPath);
            }
        }

        MonsterDatabase database = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(DatabasePath);
        if (database == null)
        {
            errors.Add("MonsterDatabase를 찾지 못했습니다: " + DatabasePath);
            return;
        }

        Dictionary<string, MonsterDefinition> existing =
            database.monsters
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.monsterId))
                .GroupBy(item => item.monsterId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        HashSet<string> sheetIds = new HashSet<string>(
            snapshot.rows.Select(item => item.monsterId),
            StringComparer.Ordinal);

        foreach (MonsterMasterRow row in snapshot.rows)
        {
            if (!row.enabled)
            {
                diffs.Add(new DiffEntry(DiffKind.Disabled, row.monsterId + " — DB에서 제외, 에셋은 보존"));
                continue;
            }

            if (!existing.TryGetValue(row.monsterId, out MonsterDefinition definition))
            {
                diffs.Add(new DiffEntry(DiffKind.New, row.monsterId + " — 신규 생성"));
                continue;
            }

            List<string> changed = FindChanges(definition, row);
            diffs.Add(changed.Count == 0
                ? new DiffEntry(DiffKind.Unchanged, row.monsterId + " — 변경 없음")
                : new DiffEntry(DiffKind.Update, row.monsterId + " — 변경: " + string.Join(", ", changed)));
        }

        foreach (KeyValuePair<string, MonsterDefinition> pair in existing)
        {
            if (!sheetIds.Contains(pair.Key))
            {
                warnings.Add(pair.Key + "는 시트에 없으므로 기존 DB 항목을 보존합니다.");
                diffs.Add(new DiffEntry(DiffKind.Preserved, pair.Key + " — 시트 누락, 자동 삭제하지 않음"));
            }
        }
    }

    private void ApplySnapshot()
    {
        ValidateAndBuildDiff();
        if (errors.Count > 0)
        {
            status = "차단 오류가 있어 적용하지 않았습니다.";
            return;
        }

        Undo.IncrementCurrentGroup();
        MonsterDatabase database = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(DatabasePath);
        MonsterDefinition legacyVelos = database.monsters.FirstOrDefault(
            item => item != null &&
                    item.monsterId == "raptor_reference_palette_v2");
        if (legacyVelos != null &&
            database.monsters.All(item =>
                item == null || item.monsterId != "velos"))
        {
            legacyVelos.monsterId = "velos";
            EditorUtility.SetDirty(legacyVelos);
        }
        Dictionary<string, MonsterDefinition> existing =
            database.monsters
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.monsterId))
                .GroupBy(item => item.monsterId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        HashSet<string> sheetIds = new HashSet<string>(
            snapshot.rows.Select(item => item.monsterId),
            StringComparer.Ordinal);
        List<MonsterDefinition> ordered = new List<MonsterDefinition>();
        int created = 0;
        int updated = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (MonsterMasterRow row in snapshot.rows)
            {
                if (!row.enabled)
                    continue;

                if (!existing.TryGetValue(row.monsterId, out MonsterDefinition definition))
                {
                    definition = CreateInstance<MonsterDefinition>();
                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        DefinitionFolder + "/Monster_" + row.monsterId + ".asset");
                    AssetDatabase.CreateAsset(definition, path);
                    created++;
                }
                else if (FindChanges(definition, row).Count > 0)
                {
                    Undo.RecordObject(definition, "Sync Monster Definition");
                    updated++;
                }

                CopyRowToDefinition(row, definition);
                EditorUtility.SetDirty(definition);
                ordered.Add(definition);
            }

            foreach (MonsterDefinition definition in database.monsters)
            {
                if (definition != null && !sheetIds.Contains(definition.monsterId))
                    ordered.Add(definition);
            }

            Undo.RecordObject(database, "Sync Monster Database");
            database.monsters = ordered;
            EditorUtility.SetDirty(database);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // Test-runner cleanup reverts its current Undo group, so close this import.
        Undo.IncrementCurrentGroup();
        status = "적용 완료: 신규 " + created + ", 갱신 " + updated +
                 ", 활성 DB " + ordered.Count;
        ValidateAndBuildDiff();
        Debug.Log("[Monster DB Sync] " + status);
    }

    private static void CopyRowToDefinition(MonsterMasterRow row, MonsterDefinition definition)
    {
        definition.monsterId = row.monsterId;
        definition.serialNumber = row.serialNumber;
        definition.intrinsicTraitId = row.intrinsicTraitId;
        definition.displayName = row.displayName;
        definition.familyId = row.familyId;
        Enum.TryParse(row.defaultGrade, true, out definition.defaultGrade);
        definition.enabled = row.enabled;
        definition.dataVersion = row.dataVersion;
        definition.resourcePath = row.resourcePath;
        definition.portraitResourcePath = row.portraitResourcePath;
        definition.animationSetId = row.animationSetId;
        definition.visualScale = row.visualScale;
        definition.modelHeight = row.modelHeight;
        definition.groundSpanTileRatio = row.groundSpanTileRatio;
        definition.collisionRadius = row.collisionRadius;
        definition.tactic = row.tactic;
        definition.maxHp = row.maxHp;
        definition.attackDamage = row.attackDamage;
        definition.attackInterval = row.attackInterval;
        definition.moveSpeedTilesPerSecond = row.moveSpeedTilesPerSecond;
        definition.attackRange = row.attackRange;
        definition.defense = row.defense;
        definition.statusResistance = row.statusResistance;
        definition.basicAttackId = row.basicAttackId;
        definition.skillId = row.skillId;
    }

    private static List<string> FindChanges(MonsterDefinition definition, MonsterMasterRow row)
    {
        List<string> changed = new List<string>();
        Compare(changed, "serial_number", definition.serialNumber, row.serialNumber);
        Compare(changed, "intrinsic_trait_id", definition.intrinsicTraitId, row.intrinsicTraitId);
        Compare(changed, "display_name", definition.displayName, row.displayName);
        Compare(changed, "family_id", definition.familyId, row.familyId);
        Compare(changed, "default_grade", definition.defaultGrade.ToString(), row.defaultGrade);
        Compare(changed, "resource_path", definition.resourcePath, row.resourcePath);
        Compare(changed, "portrait_sprite_path", definition.portraitResourcePath, row.portraitResourcePath);
        Compare(changed, "animation_set_id", definition.animationSetId, row.animationSetId);
        Compare(changed, "visual_scale", definition.visualScale, row.visualScale);
        Compare(changed, "model_height", definition.modelHeight, row.modelHeight);
        Compare(changed, "ground_span_tile_ratio", definition.groundSpanTileRatio, row.groundSpanTileRatio);
        Compare(changed, "collision_radius", definition.collisionRadius, row.collisionRadius);
        Compare(changed, "tactic", definition.tactic, row.tactic);
        Compare(changed, "max_hp", definition.maxHp, row.maxHp);
        Compare(changed, "attack_damage", definition.attackDamage, row.attackDamage);
        Compare(changed, "attack_interval", definition.attackInterval, row.attackInterval);
        Compare(changed, "move_speed", definition.moveSpeedTilesPerSecond, row.moveSpeedTilesPerSecond);
        if (definition.attackRange != row.attackRange) changed.Add("attack_range");
        Compare(changed, "defense", definition.defense, row.defense);
        Compare(changed, "status_resistance", definition.statusResistance, row.statusResistance);
        Compare(changed, "basic_attack_id", definition.basicAttackId, row.basicAttackId);
        Compare(changed, "skill_id", definition.skillId, row.skillId);
        if (definition.enabled != row.enabled) changed.Add("enabled");
        if (definition.dataVersion != row.dataVersion) changed.Add("data_version");
        return changed;
    }

    private static void Compare(List<string> changed, string label, string left, string right)
    {
        if (!string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal))
            changed.Add(label);
    }

    private static void Compare(List<string> changed, string label, float left, float right)
    {
        if (!Mathf.Approximately(left, right))
            changed.Add(label);
    }

    private void SaveSnapshotWithBackup()
    {
        string directory = Path.GetDirectoryName(SnapshotPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        Directory.CreateDirectory(BackupFolder);

        if (File.Exists(SnapshotPath))
        {
            string backupName = "MonsterMasterSnapshot_" +
                                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) +
                                ".json";
            File.Copy(SnapshotPath, Path.Combine(BackupFolder, backupName), true);
        }

        File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(SnapshotPath, ImportAssetOptions.ForceUpdate);
    }

    private void ResetPreview()
    {
        errors.Clear();
        warnings.Clear();
        diffs.Clear();
        snapshot = null;
    }

    private Dictionary<string, int> BuildColumnMap(List<string> header)
    {
        Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            string key = (header[i] ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map.Add(key, i);
        }
        return map;
    }

    private static string Cell(List<string> cells, Dictionary<string, int> columns, string key)
    {
        return columns.TryGetValue(key, out int index) && index >= 0 && index < cells.Count
            ? cells[index]
            : string.Empty;
    }

    private bool TryFloat(
        List<string> cells,
        Dictionary<string, int> columns,
        string key,
        int row,
        out float value)
    {
        string raw = Cell(cells, columns, key).Trim();
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        errors.Add("행 " + row + ": " + key + " 값이 숫자가 아닙니다: " + raw);
        return false;
    }

    private bool TryOptionalFloat(
        List<string> cells,
        Dictionary<string, int> columns,
        string key,
        int row,
        out float value)
    {
        string raw = Cell(cells, columns, key).Trim().Trim('"');
        if (string.IsNullOrEmpty(raw))
        {
            value = 0f;
            return true;
        }
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        errors.Add("행 " + row + ": " + key + " 값이 숫자가 아닙니다: " + raw);
        return false;
    }

    private bool TryInt(
        List<string> cells,
        Dictionary<string, int> columns,
        string key,
        int row,
        out int value)
    {
        string raw = Cell(cells, columns, key).Trim();
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        errors.Add("행 " + row + ": " + key + " 값이 정수가 아닙니다: " + raw);
        return false;
    }

    private bool TryBool(
        List<string> cells,
        Dictionary<string, int> columns,
        string key,
        int row,
        out bool value)
    {
        string raw = Cell(cells, columns, key).Trim();
        if (bool.TryParse(raw, out value))
            return true;
        if (raw == "1")
        {
            value = true;
            return true;
        }
        if (raw == "0")
        {
            value = false;
            return true;
        }
        errors.Add("행 " + row + ": " + key + " 값은 TRUE/FALSE여야 합니다: " + raw);
        value = false;
        return false;
    }

    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Length = 0;
            }
            else if ((character == '\n' || character == '\r') && !quoted)
            {
                if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(field.ToString());
                field.Length = 0;
                if (row.Any(value => !string.IsNullOrEmpty(value)))
                    rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(character);
            }
        }

        row.Add(field.ToString());
        if (row.Any(value => !string.IsNullOrEmpty(value)))
            rows.Add(row);
        return rows;
    }

    [Serializable]
    private sealed class MonsterMasterSnapshot
    {
        public string spreadsheetId;
        public string sheetName;
        public string downloadedUtc;
        public List<MonsterMasterRow> rows = new List<MonsterMasterRow>();
    }

    [Serializable]
    private sealed class MonsterMasterRow
    {
        public int sheetRow;
        public string monsterId;
        public string serialNumber;
        public string intrinsicTraitId;
        public string displayName;
        public string familyId;
        public string defaultGrade;
        public string resourcePath;
        public string portraitResourcePath;
        public string animationSetId;
        public float visualScale;
        public float modelHeight;
        public float groundSpanTileRatio;
        public float collisionRadius;
        public string tactic;
        public float maxHp;
        public float attackDamage;
        public float attackInterval;
        public float moveSpeedTilesPerSecond;
        public int attackRange;
        public float defense;
        public float statusResistance;
        public string basicAttackId;
        public string skillId;
        public bool enabled;
        public int dataVersion;
    }

    private enum DiffKind
    {
        New,
        Update,
        Unchanged,
        Disabled,
        Preserved
    }

    private sealed class DiffEntry
    {
        public readonly DiffKind kind;
        public readonly string label;

        public DiffEntry(DiffKind kind, string label)
        {
            this.kind = kind;
            this.label = label;
        }
    }
}
