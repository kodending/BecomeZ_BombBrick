using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public sealed class LocalizationDatabaseFile
{
    public int dataVersion = 1;
    public List<LocalizationEntry> entries = new List<LocalizationEntry>();
}

[Serializable]
public sealed class LocalizationEntry
{
    public string key = "";
    public string category = "";
    public string sourceRef = "";
    public string ko = "";
    public string en = "";
    public string status = "";
    public string reviewNote = "";
    public bool enabled = true;
    public int dataVersion = 1;
}

/// <summary>
/// Lightweight runtime localization backed by Localization_Master.
/// Korean remains the safe fallback, so a missing English cell never breaks UI.
/// </summary>
public static class GameLocalization
{
    public const string Korean = "ko";
    public const string English = "en";
    public const string ResourcePath = "Localization/LocalizationDatabase";

    private static readonly Dictionary<string, string> EnglishByKorean =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, LocalizationEntry> ByKey =
        new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Cache =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly List<PatternEntry> Patterns = new List<PatternEntry>();
    private static bool loaded;
    private static bool preloadRunning;
    private static int loadGeneration;
    private static string currentLanguage = Korean;

    private sealed class PatternEntry
    {
        public Regex regex;
        public string english;
        public List<int> indexes;
    }

    public static event Action LanguageChanged;
    public static string CurrentLanguage => currentLanguage;
    public static bool IsEnglish => currentLanguage == English;

    public static string NormalizeLanguage(string language)
    {
        return string.Equals(language, English, StringComparison.OrdinalIgnoreCase)
            ? English
            : Korean;
    }

    public static void SetLanguage(string language)
    {
        language = NormalizeLanguage(language);
        if (currentLanguage == language) return;
        currentLanguage = language;
        Cache.Clear();
        LanguageChanged?.Invoke();
    }

    public static string Text(string source)
    {
        if (string.IsNullOrEmpty(source) || !IsEnglish) return source ?? "";
        EnsureLoaded();
        if (Cache.TryGetValue(source, out string cached)) return cached;
        if (EnglishByKorean.TryGetValue(source, out string exact))
            return Cache[source] = exact;

        for (int i = 0; i < Patterns.Count; i++)
        {
            PatternEntry pattern = Patterns[i];
            Match match = pattern.regex.Match(source);
            if (!match.Success) continue;
            string translated = pattern.english;
            bool unresolvedCapture = false;
            for (int valueIndex = 0; valueIndex < pattern.indexes.Count; valueIndex++)
            {
                int placeholder = pattern.indexes[valueIndex];
                string value = match.Groups["p" + placeholder].Value;
                if (!string.Equals(value, source, StringComparison.Ordinal)) value = Text(value);
                if (ContainsKorean(value))
                {
                    unresolvedCapture = true;
                    break;
                }
                translated = translated.Replace("{" + placeholder + "}", value);
            }
            if (unresolvedCapture) continue;
            return Cache[source] = translated;
        }

        return Cache[source] = source;
    }

    public static string TextByKey(string key, string koreanFallback)
    {
        if (!IsEnglish) return koreanFallback ?? "";
        EnsureLoaded();
        return !string.IsNullOrEmpty(key) && ByKey.TryGetValue(key, out LocalizationEntry entry) &&
               !string.IsNullOrWhiteSpace(entry.en)
            ? entry.en
            : Text(koreanFallback);
    }

    public static string Format(string key, string koreanFallback, params object[] args)
    {
        return string.Format(TextByKey(key, koreanFallback), args);
    }

    public static void Reload()
    {
        loadGeneration++;
        preloadRunning = false;
        loaded = false;
        EnglishByKorean.Clear();
        ByKey.Clear();
        Patterns.Clear();
        Cache.Clear();
        EnsureLoaded();
    }

    public static void Preload() => EnsureLoaded();

    private static void EnsureLoaded()
    {
        if (loaded || preloadRunning) return;
        loaded = true;
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogWarning("[Localization] Resources/" + ResourcePath + ".json is missing.");
            return;
        }

        LocalizationDatabaseFile file;
        try
        {
            file = JsonUtility.FromJson<LocalizationDatabaseFile>(asset.text);
        }
        catch (Exception exception)
        {
            Debug.LogError("[Localization] Failed to parse database: " + exception.Message);
            return;
        }

        if (file == null || file.entries == null) return;
        for (int i = 0; i < file.entries.Count; i++)
        {
            AddEntry(file.entries[i]);
        }
        Patterns.Sort((left, right) => right.regex.ToString().Length.CompareTo(left.regex.ToString().Length));
    }

    public static IEnumerator PreloadAcrossFrames()
    {
        if (loaded) yield break;
        if (preloadRunning)
        { while (preloadRunning) yield return null; yield break; }
        int generation = loadGeneration;
        preloadRunning = true;
        EnglishByKorean.Clear(); ByKey.Clear(); Patterns.Clear(); Cache.Clear();
        try
        {
            var request = Resources.LoadAsync<TextAsset>(ResourcePath);
            while (!request.isDone) yield return null;
            if (generation != loadGeneration) yield break;
            var asset = request.asset as TextAsset;
            if (asset == null)
            {
                loaded = true;
                Debug.LogWarning("[Localization] Resources/" + ResourcePath + ".json is missing.");
                yield break;
            }
            string json = asset.text;
            // JsonUtility's string overload supports background threads; Unity objects stay on the main thread.
            var parse = Task.Run(() => JsonUtility.FromJson<LocalizationDatabaseFile>(json));
            while (!parse.IsCompleted) yield return null;
            if (generation != loadGeneration) yield break;
            if (parse.IsFaulted)
            {
                loaded = true;
                Debug.LogError("[Localization] Failed to parse database: " + parse.Exception.GetBaseException().Message);
                yield break;
            }
            var file = parse.Result;
            if (file?.entries != null)
                for (int i = 0; i < file.entries.Count; i++)
                {
                    if (generation != loadGeneration) yield break;
                    AddEntry(file.entries[i]);
                    if (i % 32 == 31) yield return null;
                }
            Patterns.Sort((left, right) => right.regex.ToString().Length.CompareTo(left.regex.ToString().Length));
            Cache.Clear();
            loaded = true;
        }
        finally
        {
            if (generation == loadGeneration)
            {
                preloadRunning = false;
                if (!loaded)
                { EnglishByKorean.Clear(); ByKey.Clear(); Patterns.Clear(); Cache.Clear(); }
            }
        }
    }

    private static void AddEntry(LocalizationEntry entry)
    {
        if (entry == null || !entry.enabled || string.IsNullOrWhiteSpace(entry.ko) ||
            string.IsNullOrWhiteSpace(entry.en)) return;
        if (!string.IsNullOrWhiteSpace(entry.key)) ByKey[entry.key] = entry;
        if (HasPlaceholder(entry.ko))
        {
            PatternEntry pattern = BuildPattern(entry);
            if (pattern != null) Patterns.Add(pattern);
        }
        else EnglishByKorean[entry.ko] = entry.en;
    }


    private static bool HasPlaceholder(string value) =>
        Regex.IsMatch(value, @"\{\d+\}", RegexOptions.CultureInvariant);

    private static bool ContainsKorean(string value) =>
        !string.IsNullOrEmpty(value) &&
        Regex.IsMatch(value, "[가-힣ㄱ-ㅎㅏ-ㅣ]", RegexOptions.CultureInvariant);

    private static PatternEntry BuildPattern(LocalizationEntry entry)
    {
        MatchCollection placeholders = Regex.Matches(
            entry.ko, @"\{(\d+)\}", RegexOptions.CultureInvariant);
        if (placeholders.Count == 0) return null;
        StringBuilder expression = new StringBuilder("^");
        List<int> indexes = new List<int>();
        int cursor = 0;
        HashSet<int> seen = new HashSet<int>();
        foreach (Match placeholder in placeholders)
        {
            expression.Append(Regex.Escape(entry.ko.Substring(cursor, placeholder.Index - cursor)));
            int index = int.Parse(placeholder.Groups[1].Value);
            if (seen.Add(index))
            {
                indexes.Add(index);
                expression.Append("(?<p").Append(index).Append(">.+?)");
            }
            else expression.Append("\\k<p").Append(index).Append(">");
            cursor = placeholder.Index + placeholder.Length;
        }
        expression.Append(Regex.Escape(entry.ko.Substring(cursor))).Append("$");
        return new PatternEntry
        {
            regex = new Regex(expression.ToString(), RegexOptions.CultureInvariant),
            english = entry.en,
            indexes = indexes
        };
    }
}
