using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BombBrick.Game.Core
{
    public static class GameResourceCache
    {
        private const string MonsterDatabasePath =
            "MonsterData/MonsterDatabase";
        private const string GameMasterDatabasePath =
            "CombatData/GameMasterDatabase";

        private static readonly string[] MainTexturePaths = new[]
        {
            "Environment/MainMenu/MainMenuExpeditionBase",
            "Environment/MainMenu/MainMenuSkyOccluder",
            "Environment/MainMenu/MainMenuTreeOccluder",
            "Environment/MainMenu/ChundongiPeekAtlas_Runtime"
        };

        private static readonly string[] CommonTexturePaths = MainTexturePaths.Concat(new[]
        {
            "Environment/PrimordialMeadow/BattleBackground",
            "Environment/CrimsonJungle/BattleBackground",
            "Environment/PoisonFogSwamp/BattleBackground",
            "Environment/FrozenHighlands/BattleBackground",
            "Environment/SkySanctuary/BattleBackground",
            "Environment/Town/TownExpeditionHub",
            "Environment/RegionIllustrations/prototype_region",
            "Environment/RegionIllustrations/crimson_jungle",
            "Environment/RegionIllustrations/poison_fog_swamp",
            "Environment/RegionIllustrations/frozen_highlands",
            "Environment/RegionIllustrations/sky_sanctuary",
            "UI/HunterShop/region_box_pixel",
            "UI/HunterShop/family_box_pixel",
            "UI/HunterShop/hunter_token_pixel",
            "UI/CaptureNetIcons/capture_net_common",
            "UI/CaptureNetIcons/capture_net_rare",
            "UI/SystemIcons/life", "UI/SystemIcons/deployment", "UI/SystemIcons/gold",
            "UI/SystemIcons/augment", "UI/SystemIcons/shop", "UI/SystemIcons/reroll",
            "UI/SystemIcons/fusion", "UI/SystemIcons/twin_command"
        }).Concat(Enum.GetNames(typeof(CombatStatusKind)).Concat(Enum.GetNames(typeof(CombatEffectIconKind)))
            .Distinct(StringComparer.Ordinal).Select(name=>"UI/CombatStatusIcons/"+name)).ToArray();

        private static readonly HashSet<Sprite> PreparedPreviewFrames=new HashSet<Sprite>();
        private static readonly Dictionary<string, SpriteMonsterAnimationDriver>
            MonsterPreviewDrivers =
                new Dictionary<string, SpriteMonsterAnimationDriver>(
                    StringComparer.Ordinal);
        private static readonly Dictionary<string, Texture2D> Textures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> Portraits =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        private static MonsterDatabase database;
        private static GameMasterDatabase gameMasterDatabase;
        private static bool warmupRunning;
        private static bool warmupComplete;
        private static bool previewWarmupRunning;
        private static bool previewWarmupComplete;
        private static bool effectWarmupRunning, effectWarmupComplete;
        private static bool mainWarmupRunning, mainWarmupComplete, gameplayWarmupRunning, gameplayWarmupComplete;
        private static float mainWarmupProgress, gameplayWarmupProgress;
        private const int ResourceBatchSize = 8;

        public static float WarmupProgress { get; private set; }
        public static float MonsterPreviewProgress { get; private set; }
        public static float StartupProgress => StartupWarmupComplete ? 1f : Mathf.Clamp(
            mainWarmupProgress * .1f + gameplayWarmupProgress * .2f +
            WarmupProgress * .2f + MonsterPreviewProgress * .5f, 0f, .99f);
        public static bool StartupWarmupComplete => mainWarmupComplete && gameplayWarmupComplete;

        public static MonsterDatabase Database
        {
            get
            {
                if (database == null)
                    database = Resources.Load<MonsterDatabase>(
                        MonsterDatabasePath);
                return database;
            }
        }

        private static IEnumerator WarmupMetadata()
        {
            if (database == null)
            {
                var request = Resources.LoadAsync<MonsterDatabase>(MonsterDatabasePath);
                while (!request.isDone) yield return null;
                database = request.asset as MonsterDatabase;
            }
            if (gameMasterDatabase == null)
            {
                var request = Resources.LoadAsync<GameMasterDatabase>(GameMasterDatabasePath);
                while (!request.isDone) yield return null;
                gameMasterDatabase = request.asset as GameMasterDatabase;
            }
        }

        private static ResourceRequest[] RequestBatch<T>(IList<string> paths, int offset) where T : UnityEngine.Object
        {
            var requests = new ResourceRequest[Math.Min(ResourceBatchSize, paths.Count - offset)];
            for (int i = 0; i < requests.Length; i++)
                requests[i] = Resources.LoadAsync<T>(paths[offset + i]);
            return requests;
        }

        private static bool PreparationSliceExpired(long started) =>
            (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d /
                System.Diagnostics.Stopwatch.Frequency >= 2d;

        public static IEnumerator WarmupCommon(Action<float> report = null)
        {
            if (warmupComplete) { report?.Invoke(1f); yield break; }
            if (warmupRunning)
            {
                while (warmupRunning) { report?.Invoke(WarmupProgress); yield return null; }
                if (warmupComplete) { report?.Invoke(1f); yield break; }
            }
            warmupRunning = true;
            WarmupProgress = 0f;
            try
            {
                yield return WarmupMetadata();
                int total = 2 + CommonTexturePaths.Length;
                int loaded = 2;
                WarmupProgress = loaded / (float)total;
                long slice = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int offset = 0; offset < CommonTexturePaths.Length; offset += ResourceBatchSize)
                {
                    var requests = RequestBatch<Texture2D>(CommonTexturePaths, offset);
                    for (int i = 0; i < requests.Length; i++)
                    {
                        while (!requests[i].isDone)
                        { yield return null; slice = System.Diagnostics.Stopwatch.GetTimestamp(); }
                        Textures[CommonTexturePaths[offset + i]] = requests[i].asset as Texture2D;
                        WarmupProgress = ++loaded / (float)total;
                        report?.Invoke(WarmupProgress);
                        if (!PreparationSliceExpired(slice)) continue;
                        yield return null;
                        slice = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                WarmupProgress = 1f;
                warmupComplete = true;
                report?.Invoke(1f);
            }
            finally { warmupRunning = false; }
        }

        public static IEnumerator WarmupMonsterPreviews()
        {
            if (previewWarmupComplete) yield break;
            if (previewWarmupRunning)
            { while (previewWarmupRunning) yield return null; if (previewWarmupComplete) yield break; }
            previewWarmupRunning = true;
            MonsterPreviewProgress = 0f;
            try
            {
                yield return WarmupCommon();
                var paths = database == null ? new List<string>() : database.monsters
                    .Where(definition => definition != null && definition.enabled &&
                        !string.IsNullOrWhiteSpace(definition.resourcePath))
                    .SelectMany(definition => new[] { definition.resourcePath, definition.shinyResourcePath })
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToList();
                int total = Mathf.Max(1, paths.Count), loaded = 0;
                long slice = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int offset = 0; offset < paths.Count; offset += ResourceBatchSize)
                {
                    var requests = RequestBatch<GameObject>(paths, offset);
                    for (int i = 0; i < requests.Length; i++)
                    {
                        while (!requests[i].isDone)
                        { yield return null; slice = System.Diagnostics.Stopwatch.GetTimestamp(); }
                        string path = paths[offset + i];
                        CacheMonsterPrefab(path, requests[i].asset as GameObject);
                        var driver = MonsterPreviewDrivers[path];
                        if (driver != null)
                            foreach (Sprite frame in driver.IdleFrames)
                            {
                                if (frame == null || !PreparedPreviewFrames.Add(frame)) continue;
                                // Touch imported sprite data before its first animated portrait draw.
                                _ = frame.textureRect;
                                _ = frame.texture.width;
                                if (!PreparationSliceExpired(slice)) continue;
                                yield return null;
                                slice = System.Diagnostics.Stopwatch.GetTimestamp();
                            }
                        MonsterPreviewProgress = ++loaded / (float)total;
                        if (!PreparationSliceExpired(slice)) continue;
                        yield return null;
                        slice = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                MonsterPreviewProgress = 1f;
                previewWarmupComplete = true;
            }
            finally { previewWarmupRunning = false; }
        }

        public static IEnumerator WarmupStartup()
        {
            if (mainWarmupComplete) yield break;
            if (mainWarmupRunning)
            { while (mainWarmupRunning) yield return null; if (mainWarmupComplete) yield break; }
            mainWarmupRunning = true;
            var priority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            try
            {
                yield return GameLocalization.PreloadAcrossFrames();
                mainWarmupProgress = .2f;
                yield return WarmupMetadata();
                mainWarmupProgress = .4f;
                var requests = RequestBatch<Texture2D>(MainTexturePaths, 0);
                for (int i = 0; i < requests.Length; i++)
                {
                    while (!requests[i].isDone) yield return null;
                    Textures[MainTexturePaths[i]] = requests[i].asset as Texture2D;
                    mainWarmupProgress = .4f + .6f * (i + 1) / requests.Length;
                }
                mainWarmupProgress = 1f;
                mainWarmupComplete = true;
            }
            finally
            { mainWarmupRunning = false; Application.backgroundLoadingPriority = priority; }
        }

        public static IEnumerator WarmupGameplay()
        {
            while (gameplayWarmupRunning) yield return null;
            gameplayWarmupRunning = true;
            gameplayWarmupComplete = false;
            gameplayWarmupProgress = 0f;
            var priority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            try
            {
                yield return GameAudio.WarmupCombatSounds();
                gameplayWarmupProgress = .5f;
                yield return WarmupCombatEffects();
                gameplayWarmupProgress = 1f;
                yield return WarmupMonsterPreviews();
                gameplayWarmupComplete = true;
            }
            finally
            { gameplayWarmupRunning = false; Application.backgroundLoadingPriority = priority; }
        }

#if UNITY_EDITOR
        public static IEnumerator WarmupEditorMethods(params Type[] types)
        {
            // Constructors and iterator bodies also compile on first use; prepare them without executing gameplay.
            var pending = new Queue<Type>(types);
            long slice = System.Diagnostics.Stopwatch.GetTimestamp();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.DeclaredOnly |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            while (pending.Count > 0)
            {
                Type type = pending.Dequeue();
                if (type.ContainsGenericParameters) continue;
                foreach (var nested in type.GetNestedTypes(flags)) pending.Enqueue(nested);
                foreach (var method in type.GetMethods(flags).Cast<System.Reflection.MethodBase>()
                    .Concat(type.GetConstructors(flags)))
                {
                    if (method.ContainsGenericParameters || method.IsAbstract) continue;
                    method.MethodHandle.GetFunctionPointer();
                    if ((System.Diagnostics.Stopwatch.GetTimestamp() - slice) * 1000d /
                        System.Diagnostics.Stopwatch.Frequency < 2d) continue;
                    yield return null;
                    slice = System.Diagnostics.Stopwatch.GetTimestamp();
                }
            }
        }
#endif

        public static IEnumerator WarmupCombatEffects()
        {
            if (effectWarmupComplete) yield break;
            if (effectWarmupRunning)
            { while (effectWarmupRunning) yield return null; if (effectWarmupComplete) yield break; }
            var catalog = Resources.Load<TextAsset>("CombatEffects/PreparedFramePaths");
            if (catalog == null) yield break;
            // shortcut: rebuild the catalog after adding atlases; uncatalogued effects load on demand.
            effectWarmupRunning = true;
            try
            {
                var paths = catalog.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Trim()).ToArray();
                long slice = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int offset = 0; offset < paths.Length; offset += ResourceBatchSize)
                {
                    var requests = RequestBatch<Texture2D>(paths, offset);
                    for (int i = 0; i < requests.Length; i++)
                    {
                        while (!requests[i].isDone)
                        { yield return null; slice = System.Diagnostics.Stopwatch.GetTimestamp(); }
                        string path = paths[offset + i];
                        Textures[path] = requests[i].asset as Texture2D;
                        PixelCombatEffect.LoadFrames(path);
                        if (!PreparationSliceExpired(slice)) continue;
                        yield return null;
                        slice = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                yield return PixelHaetaeThunder.WarmupTemplate();
                effectWarmupComplete = true;
            }
            finally { effectWarmupRunning = false; }
        }

        public static IEnumerator WarmupTexture(string path)
        {
            if(Textures.ContainsKey(path))yield break;
            var request=Resources.LoadAsync<Texture2D>(path);
            while(!request.isDone)yield return null;
            Textures[path]=request.asset as Texture2D;
        }

        public static Texture2D GetTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            if (!Textures.TryGetValue(path, out Texture2D texture))
            {
                texture = Resources.Load<Texture2D>(path);
                Textures[path] = texture;
            }
            return texture;
        }

        public static Sprite GetMonsterIdleSprite(
            MonsterDefinition definition,
            float unscaledTime,
            string resourcePath = null)
        {
            if (definition == null)
                return null;
            string path = string.IsNullOrWhiteSpace(resourcePath)
                ? definition.resourcePath
                : resourcePath;
            SpriteMonsterAnimationDriver driver = GetPreviewDriver(path);
            Sprite idle = driver == null
                ? null
                : driver.GetIdleFrame(unscaledTime);
            if (idle != null)
                return idle;

            string portraitPath = definition.portraitResourcePath;
            if (string.IsNullOrWhiteSpace(portraitPath))
                return null;
            if (!Portraits.TryGetValue(portraitPath, out Sprite portrait))
            {
                portrait = Resources.Load<Sprite>(portraitPath);
                Portraits[portraitPath] = portrait;
            }
            return portrait;
        }

        private static SpriteMonsterAnimationDriver GetPreviewDriver(
            string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;
            if (MonsterPreviewDrivers.TryGetValue(
                    resourcePath,
                    out SpriteMonsterAnimationDriver driver))
                return driver;

            if (previewWarmupRunning)
                return null;

            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            CacheMonsterPrefab(resourcePath, prefab);
            return MonsterPreviewDrivers.TryGetValue(resourcePath, out driver)
                ? driver
                : null;
        }

        private static void CacheMonsterPrefab(
            string resourcePath, GameObject prefab)
        {
            MonsterPreviewDrivers[resourcePath] = prefab == null
                ? null
                : prefab.GetComponentInChildren<
                    SpriteMonsterAnimationDriver>(true);
        }
    }
}
