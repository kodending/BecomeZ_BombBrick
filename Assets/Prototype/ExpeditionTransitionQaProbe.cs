#if UNITY_EDITOR || BOMB_BRICK_TRANSITION_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using BombBrick.Game.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

// This opt-in probe and its automatic navigation are absent from normal release players.
public sealed class ExpeditionTransitionQaProbe : MonoBehaviour
{
    private const string SessionKey = "BombBrick.ExpeditionTransitionQA";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool optedIn;
    private static string outputRoot, expectedUserRoot;
    private readonly List<string> errors = new List<string>();
    private float startedAt;
    private bool finished;
    private string stage = "Boot";
    private int examinedPixels, nonBlackPixels;

    [Serializable]
    private sealed class Result
    {
        public bool success, transitionComplete, battleEnabled, sceneReady;
        public bool environmentBackdropReady, cameraIsCurrentScene, deathShaderAvailable, bearShaderAvailable;
        public string stage, scene, cameraScene, outputDirectory;
        public int unitCount, activeSpriteRendererCount, examinedPixels, nonBlackPixels;
        public float elapsed;
        public string[] exceptionMessages;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConfigureIsolation()
    {
        optedIn = false;
#if UNITY_EDITOR
        if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
        string qa = Path.Combine(Path.GetDirectoryName(Application.dataPath),
            "CodexVerification/BattleSceneBlackQA-20261102");
        string localLow = Path.Combine(qa, "EditorLocalLow");
        var field = typeof(GameDataPaths).GetField("windowsLocalLow", BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException("QA cannot isolate the editor save path.");
        field.SetValue(null, localLow);
        expectedUserRoot = Path.Combine(localLow, "PangStudio/BecomeZWildCrew/UserData");
        outputRoot = Path.Combine(qa, "Editor-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
        UnityEditor.SessionState.SetString(SessionKey + ".Output", outputRoot);
#else
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-qa-expedition-smoke") < 0 || Array.IndexOf(args, "-qa-profile") < 0) return;
        expectedUserRoot = GameDataPaths.QaProfileRoot(args, Path.GetDirectoryName(Application.dataPath));
        if (expectedUserRoot == null) return;
        outputRoot = Path.Combine(Path.GetDirectoryName(expectedUserRoot),
            "TransitionQA-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
#endif
        if (!string.Equals(Path.GetFullPath(GameDataPaths.Root), Path.GetFullPath(expectedUserRoot),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("QA refused a non-isolated save path.");
        Directory.CreateDirectory(expectedUserRoot);
        Directory.CreateDirectory(outputRoot);
        optedIn = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (!optedIn) return;
        var host = new GameObject("[Isolated Expedition Transition QA]");
        DontDestroyOnLoad(host);
        host.AddComponent<ExpeditionTransitionQaProbe>();
    }

    private void Awake()
    {
        startedAt = Time.realtimeSinceStartup;
        Application.logMessageReceived += RecordError;
    }

    private IEnumerator Start()
    {
        var routine = Navigate();
        while (!finished)
        {
            bool next;
            try { next = routine.MoveNext(); }
            catch (Exception exception) { errors.Add(exception.ToString()); Finish(false); yield break; }
            if (!next) yield break;
            yield return routine.Current;
        }
    }

    private void Update()
    {
        if (!finished && Time.realtimeSinceStartup - startedAt > 90f)
        { errors.Add("Timed out after 90s at " + stage); Finish(false); }
    }

    private IEnumerator Navigate()
    {
        GameFrontend frontend;
        while (SceneManager.GetActiveScene().name != GameSceneCatalog.Boot ||
               (frontend = FindObjectOfType<GameFrontend>()) == null ||
               !Flag(frontend, "startupResourcesReady") || !Flag(frontend, "bootLogoFinished") ||
               GameFlowController.Instance.IsTransitioning) yield return null;
        PrepareIsolatedProfile();
        stage = "Town";
        GameFlowController.Instance.EnterTown();
        TownSceneController town;
        while (SceneManager.GetActiveScene().name != GameSceneCatalog.Town ||
               (town = FindObjectOfType<TownSceneController>()) == null || !town.IsSceneReady ||
               GameFlowController.Instance.IsTransitioning || GameFrontend.HasPendingSave) yield return null;
        stage = "Expedition";
        GameFlowController.Instance.StartExpedition("prototype_region", false, ExpeditionDifficulty.Beginner);
        MonsterAutoBattlePrototype battle;
        while (SceneManager.GetActiveScene().name != GameSceneCatalog.ExpeditionBattle ||
               (battle = FindObjectOfType<MonsterAutoBattlePrototype>()) == null ||
               GameFlowController.Instance.IsTransitioning) yield return null;
        while (battle.enabled && (!battle.IsSceneReady || !GameFlowController.CanPresentScene ||
               Flag(battle, "mapIntroductionActive") || Flag(battle, "waveArrivalActive") ||
               Flag(battle, "waveIntroActive"))) yield return null;
        stage = "RenderedBattle";
        yield return new WaitForEndOfFrame();
        if (finished) yield break;
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            if (screenshot == null) throw new InvalidOperationException("Game framebuffer capture returned null.");
            Color32[] pixels = screenshot.GetPixels32();
            for (int y = screenshot.height * 12 / 100; y < screenshot.height * 85 / 100; y += 8)
            for (int x = screenshot.width / 10; x < screenshot.width * 9 / 10; x += 8)
            {
                examinedPixels++;
                Color32 pixel = pixels[y * screenshot.width + x];
                if (pixel.r + pixel.g + pixel.b > 30) nonBlackPixels++;
            }
            File.WriteAllBytes(Path.Combine(outputRoot, "BattleFrame.png"), screenshot.EncodeToPNG());
        }
        finally { if (screenshot != null) Destroy(screenshot); }
        Finish(true);
    }

    private static void PrepareIsolatedProfile()
    {
        if (!string.Equals(Path.GetFullPath(GameDataPaths.Root), Path.GetFullPath(expectedUserRoot),
                StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("QA save isolation changed.");
        var store = new PlayerProfileStore();
        var database = Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var profile = store.LoadOrCreate(database);
        var starters = profile.monsters.Where(m => m != null &&
            m.instanceId != PlayerProfileStore.MemorialChundongiInstanceId && database.Get(m.monsterId)?.enabled == true)
            .GroupBy(m => m.monsterId).Select(group => group.First()).Take(PlayerProfileStore.ExpeditionRosterSize).ToArray();
        if (starters.Length != PlayerProfileStore.ExpeditionRosterSize)
            throw new InvalidOperationException("QA setup requires 8 enabled, distinct owned starter monsters; found " + starters.Length);
        // Confirm only the generated QA starters; no real player profile is copied or reset.
        if (profile.starterSelectionPending) store.ConfirmStarters(profile);
        // Town's drag/drop flow uses this same public API to fill the eight exploration slots.
        store.SetExpeditionRoster(profile, database, starters.Select(m => m.instanceId).ToArray());
        if (profile.expeditionPreset.RosterCount != PlayerProfileStore.ExpeditionRosterSize)
            throw new InvalidOperationException("QA setup failed to fill all 8 exploration slots.");
        profile.firstExpeditionGuideInitialized = true;
        profile.firstExpeditionGuideVersion = 2;
        profile.firstExpeditionGuideStep = 8;
        profile.firstExpeditionCaptureGuideStep = 8;
        store.Save(profile, false);
        Debug.Log("[Expedition Transition QA] Setup ready: 8 enabled owned monsters / 8 exploration slots.");
    }

    private void RecordError(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 64)
            errors.Add(message + "\n" + stack);
    }

    private void Finish(bool captured)
    {
        if (finished) return;
        finished = true;
        Application.logMessageReceived -= RecordError;
        var battle = FindObjectOfType<MonsterAutoBattlePrototype>();
        var camera = Read(battle, "mainCamera") as Camera;
        var result = new Result
        {
            stage = stage, scene = SceneManager.GetActiveScene().name, outputDirectory = outputRoot,
            transitionComplete = !GameFlowController.Instance.IsTransitioning,
            battleEnabled = battle != null && battle.isActiveAndEnabled, sceneReady = battle != null && battle.IsSceneReady,
            cameraScene = camera == null ? "" : camera.gameObject.scene.name,
            cameraIsCurrentScene = camera != null && camera.enabled && camera.gameObject.scene == SceneManager.GetActiveScene(),
            environmentBackdropReady = Flag(battle, "environmentBackdropReady"),
            deathShaderAvailable = Shader.Find("Particles/Standard Unlit") != null,
            bearShaderAvailable = Shader.Find("BecomeZ/Effects/Bear Roar Spirit Fade") != null,
            unitCount = (Read(battle, "units") as ICollection)?.Count ?? 0,
            activeSpriteRendererCount = FindObjectsOfType<SpriteRenderer>().Count(r => r.enabled && !r.forceRenderingOff &&
                r.gameObject.scene == SceneManager.GetActiveScene()),
            examinedPixels = examinedPixels, nonBlackPixels = nonBlackPixels,
            elapsed = Time.realtimeSinceStartup - startedAt, exceptionMessages = errors.ToArray()
        };
        result.success = captured && result.transitionComplete && result.battleEnabled && result.sceneReady &&
            result.cameraIsCurrentScene && result.environmentBackdropReady && result.unitCount > 0 &&
            result.activeSpriteRendererCount > 0 && examinedPixels > 0 && nonBlackPixels > examinedPixels * .08f && errors.Count == 0;
        File.WriteAllText(Path.Combine(outputRoot, "Result.json"), JsonUtility.ToJson(result, true));
        Debug.Log("[Expedition Transition QA] " + (result.success ? "PASS " : "FAIL ") + outputRoot);
#if !UNITY_EDITOR
        // An incomplete battle must not veto exit for this isolated QA process.
        var frontend = FindObjectOfType<GameFrontend>();
        var quit = typeof(GameFrontend).GetMethod("WantsToQuit", Hidden);
        if (frontend != null && quit != null)
            Application.wantsToQuit -= (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), frontend, quit);
        Application.Quit(result.success ? 0 : 1);
#endif
    }

    private static object Read(object target, string name) => target == null ? null : target.GetType().GetField(name, Hidden)?.GetValue(target);
    private static bool Flag(object target, string name) => Read(target, name) is bool value && value;
}
#endif
