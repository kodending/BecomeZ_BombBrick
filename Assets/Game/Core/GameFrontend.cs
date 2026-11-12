using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GUI = LocalizedGUI;

namespace BombBrick.Game.Core
{
    [DefaultExecutionOrder(-900)]
    public sealed class GameFrontend : MonoBehaviour
    {
        private static GameFrontend instance;
        private static int sceneNameFrame=-1,sceneNameHandle;
        private static string sceneName;
        private GameUserSettings settings;
        private GameUserSettings draft;
        private GameUserSettings previousDisplay;
        private GameAudio audioPlayer;
        private Texture2D backdrop;
        private Texture2D skyOccluder;
        private Texture2D treeOccluder;
        private bool ownsSkyOccluder, ownsTreeOccluder;
        private bool ownsMenuChundongiAtlas;
        private bool initialized;
        private bool menuOpen;
        private bool settingsOpen;
        private bool helpOpen;
        private int helpPage;
        private string supportMessage = "";
        private bool creditsOpen;
        private bool chundongiMemorialOpen;
        private bool chundongiAlreadyOwned;
        private int creditsPage;
        private Vector2 creditsScroll;
        private string creditsNotice = "";
        private GUIStyle creditsTextStyle;
        private string[] creditsLines;
        private float[] creditsLineHeights;
        private float creditsContentHeight;
        private Texture2D creditsChundongiIdle;
        private Texture2D creditsChundongiSpecial;
        private float creditsChundongiCycleStartedAt;
        private float creditsChundongiSpecialAt = float.PositiveInfinity;
        private bool newGameOpen;
        private bool newGameTutorial = true;
        private string newGameMessage = "";
        private bool hasSavedGame;
        private float nextSaveCheck;
        private bool confirmQuit;
        private string profileLoadMessage = "";
        private string profileLoadDirectory = "";
        private bool profileLoadFailed;
        private readonly Queue<System.Action> pendingSaves = new Queue<System.Action>();
        private Task pendingSaveTask;
        private Action pendingSaveCompleted;
        private string saveDirectory = "";
        private float saveTimeScale;
        private int settingsTab;
        private int blockThroughFrame = -1;
        private float previousTimeScale = 1f;
        private bool applicationFocused = true;
        private bool applicationPaused;
        private bool backgroundPaused;
        private float backgroundTimeScale = 1f;
        private bool waitForFocusInputRelease;
        private int focusInputBlockThroughFrame = -1;
        private float displayConfirmUntil;
        private float shownAt;
        private float fpsTimer;
        private int fpsFrames;
        private string fpsLabel = "";
        private string settingsMessage = "";
        private readonly Dictionary<int, GUIStyle> labels = new Dictionary<int, GUIStyle>();
        private readonly List<MenuLeaf> menuLeaves = new List<MenuLeaf>();
        private readonly System.Random menuAmbientRandom = new System.Random(7319);
        private float nextMenuLeafBurst;
        private Texture2D menuChundongiAtlas;
        private readonly System.Random menuPeekRandom = new System.Random();
        private float menuPeekStartedAt = float.PositiveInfinity;
        private float menuPeekHold;
        private float nextMenuPeekAt = float.PositiveInfinity;
        private const float MenuPeekEnter = 1.08f;
        private const float MenuPeekExit = 0.60f;
        private float mainExitStartedAt = -1f;
        private float bootLogoStartedAt;
        private bool bootLogoPresentationStarted;
        private float BootLogoElapsed => bootLogoPresentationStarted ? Mathf.Max(0f, Time.unscaledTime - bootLogoStartedAt) : 0f;
        private bool bootLogoFinished;
        private bool startupResourcesStarted;
        private bool startupResourcesReady;
        private bool bootLogoCuePlayed;
        private const float MainExitDuration = 0.82f;
        public const float BootLogoSettleAt = 1.65f;
        public const float BootLogoDuration = 3.55f;
        public const float BootLoadingMinimumDuration = .3f;
        private const float BootLogoScrambleAt = 0.28f;
        private const float BootLogoAngAt = 1.88f;
        private const float BootLogoFadeAt = 3.00f;
        private const float BootLogoGlyphStep = 0.11f;
        private const string BootLogoGlyphs = "7R3BK9Z2F5NQ";

        private bool ReturningToMain => GameFlowController.Instance.IsTransitioning &&
            GameFlowController.Instance.TransitionTarget == GameSceneCatalog.Boot;

        private struct MenuLeaf
        {
            public float born;
            public float lifetime;
            public float x;
            public float y;
            public float speedX;
            public float speedY;
            public float wobble;
            public float phase;
            public float size;
            public Color color;
        }
        private static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
            new Vector2Int(1920, 1200), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440)
        };

        private static string CurrentSceneName
        {
            get
            {
                Scene scene=SceneManager.GetActiveScene();
                int frame=Time.frameCount;
                // Scene.name marshals a new string; still detect scene switches within the same frame.
                if(sceneNameFrame!=frame||sceneNameHandle!=scene.handle)
                {
                    sceneNameFrame=frame;sceneNameHandle=scene.handle;
                    sceneName=scene.name;
                }
                return sceneName;
            }
        }

        public static bool IsProduction
        {
            get
            {
                if (CombatEffectTestConfiguration.Active) return false;
                string scene = CurrentSceneName;
                return scene == GameSceneCatalog.Boot || scene == GameSceneCatalog.Town ||
                    scene == GameSceneCatalog.Result || scene == GameSceneCatalog.Battle ||
                    scene == GameSceneCatalog.ExpeditionBattle && GameFlowController.HasActiveExpeditionRequest;
            }
        }

        public static bool HasProfileLoadNotice => instance != null &&
            !string.IsNullOrEmpty(instance.profileLoadMessage);
        public static bool HasPendingSave => instance != null && instance.pendingSaves.Count > 0;

        public static bool BlocksGameplay => HasPendingSave || HasProfileLoadNotice || instance != null && IsProduction &&
            (!GameFlowController.CanPresentScene || instance.backgroundPaused || instance.menuOpen || instance.settingsOpen || instance.helpOpen || instance.creditsOpen || instance.chundongiMemorialOpen || instance.newGameOpen || instance.confirmQuit ||
             Time.frameCount <= instance.blockThroughFrame || GameFlowController.Instance.IsTransitioning);

        public static bool TrySaveProgress(System.Action save, string directory)
        {
            GameFrontend frontend = GameFlowController.Instance.GetComponent<GameFrontend>();
            bool alreadyPending = HasPendingSave;
            frontend.pendingSaves.Enqueue(save);
            frontend.saveDirectory = directory;
            if (alreadyPending) return false;
            CombatCameraDirector.PrepareForGameplayPause();
            frontend.saveTimeScale = frontend.GameplayTimeScale;
            return frontend.RetryPendingSave();
        }

        public bool RetryPendingSave()
        {
            if (pendingSaves.Count == 0) return true;
            try
            {
                while (pendingSaves.Count > 0)
                {
                    if (pendingSaveTask != null)
                    {
                        if (!pendingSaveTask.IsCompleted) return false;
                        pendingSaveTask.GetAwaiter().GetResult();
                        pendingSaveTask = null;
                    }
                    else
                    {
                        pendingSaves.Peek()();
                        if (pendingSaveTask != null) return false;
                    }
                    var completed = pendingSaveCompleted;
                    pendingSaveCompleted = null;
                    completed?.Invoke();
                    pendingSaves.Dequeue();
                }
            }
            catch (System.Exception exception) when (exception is System.IO.IOException ||
                exception is System.UnauthorizedAccessException || exception is System.Security.SecurityException ||
                exception is System.NotSupportedException)
            {
                pendingSaveTask = null;
                pendingSaveCompleted = null;
                GameplayTimeScale = 0f;
                if (audioPlayer != null) audioPlayer.SetPaused(true);
                Debug.LogWarning("[Save Retry] " + exception.Message);
                return false;
            }
            GameplayTimeScale = saveTimeScale;
            if (audioPlayer != null) audioPlayer.SetPaused(menuOpen || backgroundPaused);
            blockThroughFrame = Time.frameCount;
            return true;
        }

        public static bool TrySaveProgressAcrossFrames(Func<Action> prepareFiles, string directory, Action completed)
        {
            var frontend = GameFlowController.Instance.GetComponent<GameFrontend>();
            Action writeFiles = null;
            return TrySaveProgress(() =>
            {
                // Keep one immutable snapshot across file-access retries.
                if (writeFiles == null) writeFiles = prepareFiles();
                if (writeFiles == null) return;
                frontend.pendingSaveCompleted = completed;
                frontend.pendingSaveTask = Task.Run(writeFiles);
            }, directory);
        }

        public static bool SaveBeforeLeaving()
        {
            if (HasPendingSave) return false;
            MonsterAutoBattlePrototype battle = FindObjectOfType<MonsterAutoBattlePrototype>();
            return battle == null || battle.SaveBeforeLeaving();
        }

        private bool WantsToQuit() => SaveBeforeLeaving();

        public static bool TryLoadExpedition(ExpeditionRunStore store, PlayerProfileSave profile,
            MonsterDatabase database, out ExpeditionRunSave run)
        {
            run = null;
            try
            {
                run = store.LoadActive(profile, database);
                return true;
            }
            catch (System.Exception exception) when (exception is System.IO.IOException ||
                exception is System.UnauthorizedAccessException || exception is System.Security.SecurityException ||
                exception is System.NotSupportedException)
            {
                ReportExpeditionLoadFailure(store, exception);
                return false;
            }
        }

        private static void ReportExpeditionLoadFailure(ExpeditionRunStore store, Exception exception)
        {
            GameFrontend frontend = GameFlowController.Instance.GetComponent<GameFrontend>();
            frontend.profileLoadFailed = true;
            frontend.profileLoadDirectory = store.DirectoryPath;
            frontend.profileLoadMessage = "탐험 기록을 불러오지 못했습니다.\n파일 접근 상태와 게임 버전을 확인한 뒤 다시 시도해 주세요.\n기존 탐험 기록을 새 탐험으로 덮어쓰지 않았습니다.";
            Debug.LogWarning("[Expedition Load] " + exception.Message);
        }

        public static IEnumerator LoadExpeditionAcrossFrames(ExpeditionRunStore store, PlayerProfileSave profile,
            MonsterDatabase database, Action<ExpeditionRunSave> completed)
        {
            ExpeditionRunSave run = null;
            bool failed = false;
            yield return LoadAcrossFrames(store.LoadActiveAcrossFrames(profile, database, value => run = value),
                error => { failed = true; ReportExpeditionLoadFailure(store, error); });
            if (!failed) completed(run);
        }

        public static bool TryLoadPlayerProfile(PlayerProfileStore store, MonsterDatabase database,
            out PlayerProfileSave profile)
        {
            profile = null;
            GameFrontend frontend = GameFlowController.Instance.GetComponent<GameFrontend>();
            frontend.profileLoadMessage = "";
            try
            {
                profile = store.LoadOrCreate(database);
                ReportProfileLoaded(store);
                return true;
            }
            catch (System.Exception exception) when (exception is System.IO.IOException ||
                exception is System.IO.InvalidDataException || exception is System.UnauthorizedAccessException ||
                exception is System.Security.SecurityException || exception is System.NotSupportedException)
            {
                ReportProfileLoadFailure(store, exception);
                return false;
            }
        }

        private static void ReportProfileLoaded(PlayerProfileStore store)
        {
            var frontend = GameFlowController.Instance.GetComponent<GameFrontend>();
            frontend.profileLoadFailed = false;
            frontend.profileLoadMessage = "";
            if (!store.RecoveredFromBackup) return;
            frontend.profileLoadDirectory = store.DirectoryPath;
            frontend.profileLoadMessage = "정상 백업에서 진행 기록을 복구했습니다.\n손상된 원본이 있으면 저장 폴더에 별도로 보관됩니다.\n백업 이후의 진행은 포함되지 않을 수 있습니다.";
        }

        private static void ReportProfileLoadFailure(PlayerProfileStore store, Exception exception)
        {
            var frontend = GameFlowController.Instance.GetComponent<GameFrontend>();
            frontend.profileLoadFailed = true;
            frontend.profileLoadDirectory = store.DirectoryPath;
            frontend.profileLoadMessage = exception is System.NotSupportedException
                ? "더 최신 버전에서 만든 저장 파일입니다.\n게임을 업데이트한 뒤 다시 시도해 주세요.\n기존 기록을 새 게임으로 덮어쓰지 않았습니다."
                : "저장 파일을 불러오지 못했습니다.\n기존 기록을 새 게임으로 덮어쓰지 않았습니다.\n파일 접근 상태를 확인하거나 저장 폴더를 보관한 뒤 도움을 요청해 주세요.";
            Debug.LogWarning("[Profile Load] " + exception.Message);
        }

        public static IEnumerator LoadPlayerProfileAcrossFrames(PlayerProfileStore store,
            MonsterDatabase database, Action<PlayerProfileSave> completed)
        {
            PlayerProfileSave profile = null;
            var loading = store.LoadOrCreateAcrossFrames(database, value => profile = value);
            bool failed = false;
            yield return LoadAcrossFrames(loading, error => { failed = true; ReportProfileLoadFailure(store, error); });
            if (failed) yield break;
            ReportProfileLoaded(store);
            completed(profile);
        }

        private static IEnumerator LoadAcrossFrames(IEnumerator loading, Action<Exception> failed)
        {
            try
            {
                while (true)
                {
                    bool moved;
                    try { moved = loading.MoveNext(); }
                    catch (Exception exception) when (exception is System.IO.IOException ||
                        exception is System.IO.InvalidDataException || exception is System.UnauthorizedAccessException ||
                        exception is System.Security.SecurityException || exception is System.NotSupportedException)
                    {
                        failed(exception);
                        yield break;
                    }
                    if (!moved) break;
                    yield return loading.Current;
                }
            }
            finally { (loading as IDisposable)?.Dispose(); }
        }

        public static bool BlocksBackgroundInput => instance != null && IsProduction &&
            (instance.InBackground || instance.waitForFocusInputRelease || Time.frameCount <= instance.focusInputBlockThroughFrame);

        public static bool BlocksGuiInput => (BlocksGameplay || BlocksBackgroundInput) &&
            Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout;

        private bool InBackground => !applicationFocused || applicationPaused;
        private float GameplayTimeScale
        {
            get => backgroundPaused ? backgroundTimeScale : Time.timeScale;
            set { if (backgroundPaused) backgroundTimeScale = value; else Time.timeScale = value; }
        }

        public static void ApplyBackgroundSettings(GameUserSettings current)
        {
            if (instance == null || !instance.initialized) return;
            instance.settings = current;
            instance.RefreshBackgroundState();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (applicationFocused == focused) return;
            applicationFocused = focused;
            BackgroundStateChanged();
        }

        private void OnApplicationPause(bool paused)
        {
            if (applicationPaused == paused) return;
            applicationPaused = paused;
            BackgroundStateChanged();
        }

        private void BackgroundStateChanged()
        {
            if (!initialized || !IsProduction) return;
            PrepareForInputInterruption();
            // The click/key used to return to the window must not place a unit or start a battle.
            waitForFocusInputRelease = true;
            focusInputBlockThroughFrame = Time.frameCount;
            GUIUtility.hotControl = 0;
            RefreshBackgroundState();
        }

        private void RefreshBackgroundState()
        {
            bool pause = initialized && IsProduction && InBackground && settings.pauseWhenUnfocused;
            if (pause && !backgroundPaused)
            {
                CombatCameraDirector.PrepareForGameplayPause();
                backgroundTimeScale = Time.timeScale;
            }
            else if (!pause && backgroundPaused) Time.timeScale = backgroundTimeScale;
            backgroundPaused = pause;
            if (backgroundPaused) Time.timeScale = 0f;
            if (audioPlayer != null) audioPlayer.SetPaused(menuOpen ||
                HasPendingSave && pendingSaveTask == null || backgroundPaused);
            AudioListener.volume = IsProduction && InBackground && settings.muteWhenUnfocused ? 0f : settings.masterVolume;
        }

        private void PrepareForInputInterruption()
        {
            CombatCameraDirector.PrepareForGameplayPause();
            MonsterAutoBattlePrototype battle = FindObjectOfType<MonsterAutoBattlePrototype>();
            if (battle != null) battle.PrepareForSystemMenu();
            BombBrick.Game.Scenes.TownSceneController town = FindObjectOfType<BombBrick.Game.Scenes.TownSceneController>();
            if (town != null) town.PrepareForSystemMenu();
        }

        private static float HudScale => BombBrickUiLayout.Scale;
        public static Rect MenuButtonRect => new Rect(
            Screen.width * 0.5f - 70f * HudScale, 10f * HudScale,
            140f * HudScale, 56f * HudScale);
        public static bool PointerOverMenu(Vector2 pointer) => IsProduction &&
            (BlocksGameplay || MenuButtonRect.Contains(pointer));

        public static Rect FitPanel(Rect safe, Vector2 size)
        {
            float scale = Mathf.Min(safe.width / size.x, safe.height / size.y);
            size *= Mathf.Max(0.01f, scale);
            return new Rect(safe.center - size * 0.5f, size);
        }

        public static Rect MainScreenRect(Rect viewport, Vector2 screenSize)
        {
            float scale = viewport.width / 960f;
            return new Rect(-viewport.position / scale, screenSize / scale);
        }

        public static float SkyTravelX(Rect screen, float phase, float leftExtent, float rightExtent)
        {
            // Wrap only when the entire cloud/flock is beyond the physical screen.
            return Mathf.Lerp(screen.xMin - rightExtent - 8f,
                screen.xMax + leftExtent + 8f, Mathf.Repeat(phase, 1f));
        }

        private void Awake()
        {
            instance = this;
            applicationFocused = Application.isFocused;
            SceneManager.sceneLoaded += SceneLoaded;
            Application.wantsToQuit += WantsToQuit;
        }

        private void Start() => SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClosePanels();
            shownAt = Time.unscaledTime;
            if (!IsProduction) return;
            if (!initialized)
            {
                initialized = true;
                settings = GameUserSettings.Load();
                settings.Apply(true);
                backdrop = Resources.Load<Texture2D>("Environment/MainMenu/MainMenuExpeditionBase") ??
                    Resources.Load<Texture2D>("Environment/PrimordialMeadow/BattleBackground");
                if (backdrop != null)
                {
                    backdrop.filterMode = FilterMode.Point;
                    backdrop.wrapMode = TextureWrapMode.Clamp;
                    skyOccluder = Resources.Load<Texture2D>("Environment/MainMenu/MainMenuSkyOccluder");
                    treeOccluder = Resources.Load<Texture2D>("Environment/MainMenu/MainMenuTreeOccluder");
                    ownsSkyOccluder = skyOccluder == null;
                    ownsTreeOccluder = treeOccluder == null;
                    if (ownsSkyOccluder) skyOccluder = CreateMenuSkyOccluder(backdrop);
                    if (ownsTreeOccluder) treeOccluder = CreateMenuSkyOccluder(backdrop, true);
                }
                audioPlayer = gameObject.AddComponent<GameAudio>();
            }
            if (scene.name == GameSceneCatalog.Boot)
            {
                GameplayTimeScale = 1f;
                previousTimeScale = 1f;
                menuLeaves.Clear();
                bootLogoStartedAt = Time.unscaledTime;
                bootLogoPresentationStarted = false;
                bootLogoFinished = false;
                bootLogoCuePlayed = false;
                nextMenuLeafBurst = float.PositiveInfinity;
                menuPeekStartedAt = nextMenuPeekAt = float.PositiveInfinity;
                mainExitStartedAt = -1f;
                RefreshContinueAvailability();
            }
            // The battle prototype camera has no listener; keep production audio audible.
            if (FindObjectOfType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            audioPlayer.Apply(settings);
            audioPlayer.SetScene(scene.name);
            if (scene.name == GameSceneCatalog.Boot && ReturningToMain) CompleteBoot(true);
            RefreshBackgroundState();
            if (scene.name == GameSceneCatalog.Boot && !startupResourcesStarted)
            {
                startupResourcesStarted = true;
                StartCoroutine(WarmupStartupResources());
            }
        }

        private IEnumerator WarmupStartupResources()
        {
            yield return GameResourceCache.WarmupStartup();
            while (BootLogoElapsed < BootLogoDuration) yield return null;
            // Render the loading screen before native sprite/audio preparation can stall a frame.
            yield return null;
            yield return null;
            yield return GameResourceCache.WarmupGameplay();
            startupResourcesReady = true;
        }

        private void CompleteBoot(bool immediately = false)
        {
            bootLogoFinished = true;
            shownAt = Time.unscaledTime - (immediately ? 10f : 0f);
            nextMenuLeafBurst = Time.unscaledTime + 1.8f;
            if (menuChundongiAtlas == null)
            {
                menuChundongiAtlas = GameResourceCache.GetTexture("Environment/MainMenu/ChundongiPeekAtlas_Runtime");
                ownsMenuChundongiAtlas = menuChundongiAtlas == null;
                if (ownsMenuChundongiAtlas)
                    menuChundongiAtlas = CreateMenuPixelAtlas(Resources.Load<Texture2D>(
                        "Environment/MainMenu/ChundongiPeekAtlas_Pixel"));
            }
            nextMenuPeekAt = Time.unscaledTime + 10f + (float)menuPeekRandom.NextDouble() * 4f;
            audioPlayer.StartMainMenuMusic();
        }

        private void Update()
        {
            if (pendingSaveTask != null && pendingSaveTask.IsCompleted) RetryPendingSave();
            if (initialized)
            {
                RefreshBackgroundState();
                if (!InBackground && waitForFocusInputRelease && !Input.anyKey &&
                    !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
                {
                    waitForFocusInputRelease = false;
                    focusInputBlockThroughFrame = Time.frameCount;
                }
            }
            if (HasProfileLoadNotice) return;
            if (!initialized || !IsProduction) return;
            if (displayConfirmUntil > 0f && Time.realtimeSinceStartup >= displayConfirmUntil) RevertDisplay();
            if (backgroundPaused) return;
            if (settings.showFps)
            {
                fpsFrames++;
                fpsTimer += Time.unscaledDeltaTime;
                if (fpsTimer >= 0.5f)
                {
                    fpsLabel = Mathf.RoundToInt(fpsFrames / fpsTimer) + " FPS";
                    fpsFrames = 0;
                    fpsTimer = 0f;
                }
            }
            if (HasPendingSave || !GameFlowController.CanPresentScene) return;
            if (CurrentSceneName == GameSceneCatalog.Boot)
            {
                if (!bootLogoPresentationStarted)
                {
                    bootLogoStartedAt = Time.unscaledTime;
                    bootLogoPresentationStarted = true;
                }
                if (Time.unscaledTime >= nextSaveCheck) RefreshContinueAvailability();
                float logoElapsed = BootLogoElapsed;
                if (!bootLogoFinished)
                {
                    if (!bootLogoCuePlayed && logoElapsed >= BootLogoSettleAt)
                    {
                        bootLogoCuePlayed = true;
                        GameAudio.Play(GameAudio.Cue.Logo);
                    }
                    if (logoElapsed < BootLogoDuration) return;
                    if (!BootReadyForMain(
                            logoElapsed,
                            GameResourceCache.StartupWarmupComplete && startupResourcesReady)) return;
                    CompleteBoot();
                }
                UpdateMenuLeaves();
                UpdateMenuChundongi();
                if (mainExitStartedAt >= 0f && Time.unscaledTime - mainExitStartedAt >= MainExitDuration)
                {
                    mainExitStartedAt = -1f;
                    GameFlowController.Instance.EnterTown();
                    return;
                }
            }
            if (!BlocksBackgroundInput && Input.GetKeyDown(KeyCode.Escape))
                HandleEscape();
        }

        private void HandleEscape()
        {
            if (displayConfirmUntil > 0f) RevertDisplay();
            else if (chundongiMemorialOpen) CloseChundongiMemorial();
            else if (newGameOpen) CloseNewGame();
            else if (confirmQuit) { confirmQuit = false; }
            else if (creditsOpen) CloseCredits();
            else if (helpOpen) CloseHelp();
            else if (settingsOpen) CloseSettings();
            else if (menuOpen) ClosePanels();
            else if (CurrentSceneName != GameSceneCatalog.Boot)
            {
                var town = FindObjectOfType<BombBrick.Game.Scenes.TownSceneController>();
                if (town != null &&
                    (town.TryCloseMonsterExchange() || town.TryClosePreparationModal()))
                {
                    blockThroughFrame = Time.frameCount;
                    return;
                }
                MonsterAutoBattlePrototype battle = FindObjectOfType<MonsterAutoBattlePrototype>();
                if (battle != null && battle.TryClosePreparationShop())
                    blockThroughFrame = Time.frameCount;
                else OpenMenu();
            }
        }

        public void OpenMenu()
        {
            if (HasPendingSave || !initialized || GameFlowController.Instance.IsTransitioning) return;
            if (!menuOpen)
            {
                PrepareForInputInterruption();
                previousTimeScale = GameplayTimeScale;
                GameplayTimeScale = 0f;
                audioPlayer.SetPaused(true);
            }
            menuOpen = true;
            shownAt = Time.unscaledTime;
        }

        public void OpenSettings()
        {
            if (CurrentSceneName != GameSceneCatalog.Boot && !menuOpen) OpenMenu();
            draft = settings.Copy();
            settingsOpen = true;
            settingsMessage = "";
        }

        public void OpenHelp()
        {
            if (!initialized || !IsProduction || HasPendingSave || HasProfileLoadNotice ||
                GameFlowController.Instance.IsTransitioning || settingsOpen || creditsOpen || confirmQuit)
                return;
            bool main = CurrentSceneName == GameSceneCatalog.Boot;
            if (main && (!bootLogoFinished || mainExitStartedAt >= 0f)) return;
            if (!main) OpenMenu();
            helpOpen = true;
            blockThroughFrame = Time.frameCount;
        }

        private void CloseHelp()
        {
            helpOpen = false;
            blockThroughFrame = Time.frameCount;
        }

        public void OpenSupport()
        {
            OpenHelp();
            if (!helpOpen) return;
            helpPage = GameHelp.Pages.Count;
            supportMessage = "";
        }

        public void OpenCredits()
        {
            if (!initialized || !IsProduction || HasPendingSave || HasProfileLoadNotice ||
                GameFlowController.Instance.IsTransitioning || settingsOpen || helpOpen || confirmQuit)
                return;
            bool main = CurrentSceneName == GameSceneCatalog.Boot;
            if (main && (!bootLogoFinished || mainExitStartedAt >= 0f)) return;
            if (!main) OpenMenu();
            creditsOpen = true;
            SelectCreditsPage(0);
            blockThroughFrame = Time.frameCount;
        }

        private void CloseCredits()
        {
            creditsOpen = false;
            blockThroughFrame = Time.frameCount;
        }

        private void SelectCreditsPage(int page)
        {
            creditsPage = page;
            creditsScroll = Vector2.zero;
            creditsNotice = page < 2 ? "" : GameCredits.ReadNotice(page - 2);
            creditsLines = GameLocalization.Text(creditsNotice).Replace("\r", "").Split('\n');
            creditsLineHeights = null;
            if (page < 2)
            {
                if (creditsChundongiIdle == null)
                    creditsChundongiIdle = Resources.Load<Texture2D>("UI/Chundongi/South_Idle");
                if (creditsChundongiSpecial == null)
                    creditsChundongiSpecial = Resources.Load<Texture2D>("UI/Chundongi/Special");
                ResetCreditsChundongi(Time.unscaledTime);
            }
        }

        private void ResetCreditsChundongi(float now)
        {
            creditsChundongiCycleStartedAt = now;
            creditsChundongiSpecialAt = now + 6f + (float)menuPeekRandom.NextDouble() * 6f;
        }

        private int CreditsChundongiFrame(float now, out bool special)
        {
            if (now >= creditsChundongiSpecialAt + 1f) ResetCreditsChundongi(now);
            special = now >= creditsChundongiSpecialAt;
            float elapsed = Mathf.Max(0f, now - (special ? creditsChundongiSpecialAt : creditsChundongiCycleStartedAt));
            int frame = Mathf.FloorToInt(elapsed * 8f);
            return special ? Mathf.Min(7, frame) : frame % 8;
        }

        private void DrawCreditsChundongi(Rect rect)
        {
            int frame = CreditsChundongiFrame(Time.unscaledTime, out bool special);
            Texture2D sheet = special ? creditsChundongiSpecial : creditsChundongiIdle;
            if (sheet != null)
                GUI.DrawTextureWithTexCoords(rect, sheet, new Rect(frame / 8f, 0f, 1f / 8f, 1f), true);
        }

        private void CloseSettings()
        {
            if (displayConfirmUntil > 0f) RevertDisplay();
            settingsOpen = false;
            blockThroughFrame = Time.frameCount;
        }

        public void ClosePanels()
        {
            if (HasPendingSave) return;
            if (displayConfirmUntil > 0f) RevertDisplay();
            if (menuOpen) GameplayTimeScale = previousTimeScale;
            if (audioPlayer != null) audioPlayer.SetPaused(backgroundPaused);
            menuOpen = settingsOpen = helpOpen = creditsOpen = chundongiMemorialOpen = newGameOpen = confirmQuit = false;
            blockThroughFrame = Time.frameCount;
        }

        private void OnGUI()
        {
            using (new BombBrickUiLayout.GuiScope(true)) DrawFrontendGui();
            if (Event.current.type == EventType.Repaint && initialized && IsProduction &&
                !InBackground && !HasPendingSave && !HasProfileLoadNotice &&
                !menuOpen && !settingsOpen && !helpOpen && !creditsOpen && !confirmQuit)
                GameFlowController.Instance.NotifySceneRendered();
        }

        private void DrawFrontendGui()
        {
            if (BlocksBackgroundInput && Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            if (HasPendingSave)
            {
                if (pendingSaveTask == null)
                {
                    DrawSaveFailure();
                    return;
                }
                if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            }
            if (HasProfileLoadNotice)
            {
                DrawProfileLoadNotice();
                return;
            }
            if (!GameFlowController.CanPresentScene && Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            if (!initialized || !IsProduction) return;
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -30000;
            bool returningToMain = ReturningToMain;
            bool main = returningToMain || CurrentSceneName == GameSceneCatalog.Boot;
            bool startupLoading = main && !returningToMain && !bootLogoFinished &&
                BootLogoElapsed >= BootLogoDuration;
            bool loading = !returningToMain && GameFlowController.Instance.IsTransitioning || startupLoading;
            bool logo = main && !returningToMain && !bootLogoFinished && !startupLoading;
            Rect menu = MenuButtonRect;
            menu = new Rect(menu.position / HudScale, menu.size / HudScale);
            if (!main && !loading && !menuOpen && !settingsOpen && !helpOpen && !creditsOpen)
                if (BombBrickUiTheme.Button(menu, "메뉴", true, false, 20)) OpenMenu();
            if (settings.showFps && !loading && !logo)
                GUI.Label(new Rect(14f, BombBrickUiLayout.Height - 26f, 100f, 22f), fpsLabel,
                    BombBrickUiTheme.CaptionStyle(11));
            if (!main && !loading && !menuOpen && !settingsOpen && !helpOpen && !creditsOpen && !confirmQuit)
            {
                DrawTransitionError();
                GUI.depth = oldDepth;
                return;
            }

            Rect safe = BombBrickUiLayout.CurrentSafeArea();
            Rect viewport = FitPanel(safe, new Vector2(960f, 540f));
            float scale = viewport.width / 960f;
            Vector2 origin = viewport.position;
            if (logo) BombBrickUiTheme.Fill(
                new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height), Color.white);
            else if (main || loading) BombBrickUiTheme.Fill(new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height), Color.black);
            else BombBrickUiTheme.Fill(new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height), new Color(0f, 0f, 0f, 0.72f));
            GUI.matrix = oldMatrix * Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
            if (loading) DrawLoading(startupLoading);
            else
            {
                if (logo) DrawBootLogo();
                else if (main) DrawMain(MainScreenRect(viewport, new Vector2(BombBrickUiLayout.Width, BombBrickUiLayout.Height)));
                if (menuOpen && !settingsOpen && !helpOpen && !creditsOpen) DrawPause();
                if (settingsOpen)
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && !newGameOpen;
                    DrawSettings();
                    GUI.enabled = enabled;
                }
                if (helpOpen) DrawHelp();
                if (creditsOpen) DrawCredits();
                if (newGameOpen) DrawNewGame();
                if (chundongiMemorialOpen) DrawChundongiMemorial();
                if (confirmQuit) DrawConfirmation();
            }
            if (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)
                Event.current.Use();
            GUI.matrix = oldMatrix;
            if (!loading) DrawTransitionError();
            GUI.depth = oldDepth;
        }

        private void DrawSaveFailure()
        {
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -31000;
            BombBrickUiTheme.Fill(new Rect(0, 0, BombBrickUiLayout.Width, BombBrickUiLayout.Height),
                new Color(0.025f, 0.035f, 0.05f, 0.96f));
            Rect viewport = FitPanel(BombBrickUiLayout.CurrentSafeArea(), new Vector2(960, 540));
            GUI.matrix = oldMatrix * Matrix4x4.TRS(viewport.position, Quaternion.identity,
                Vector3.one * (viewport.width / 960f));
            BombBrickUiTheme.DrawPanel(new Rect(112, 92, 736, 356), true);
            Label(new Rect(140, 112, 680, 46), "진행 기록을 저장하지 못했습니다",
                23, BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            GUI.Label(new Rect(144, 176, 672, 112),
                "현재 진행은 메모리에 보관하고 있습니다.\n저장 공간과 파일 접근 상태를 확인한 뒤 다시 시도해 주세요.\n저장에 성공하면 멈춘 지점에서 계속합니다. 게임을 강제로 닫으면 최근 진행을 잃을 수 있습니다.",
                BombBrickUiTheme.BodyStyle(15, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(144, 288, 672, 70), saveDirectory,
                BombBrickUiTheme.BodyStyle(10, TextAnchor.MiddleCenter));
            if (BombBrickUiTheme.Button(new Rect(222, 374, 246, 44), "저장 재시도", true, true, 14))
                RetryPendingSave();
            if (BombBrickUiTheme.Button(new Rect(492, 374, 246, 44), "저장 폴더 열기",
                System.IO.Directory.Exists(saveDirectory), false, 14))
                Application.OpenURL(new System.Uri(saveDirectory).AbsoluteUri);
            if (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)
                Event.current.Use();
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        private void DrawProfileLoadNotice()
        {
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -31000;
            BombBrickUiTheme.Fill(new Rect(0, 0, BombBrickUiLayout.Width, BombBrickUiLayout.Height),
                new Color(0.025f, 0.035f, 0.05f, 0.98f));
            Rect viewport = FitPanel(BombBrickUiLayout.CurrentSafeArea(), new Vector2(960, 540));
            GUI.matrix = oldMatrix * Matrix4x4.TRS(viewport.position, Quaternion.identity,
                Vector3.one * (viewport.width / 960f));
            BombBrickUiTheme.DrawPanel(new Rect(112, 92, 736, 356), true);
            Label(new Rect(140, 112, 680, 46), profileLoadFailed
                ? "저장 기록을 확인해 주세요" : "백업에서 복구했습니다",
                23, BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            GUI.Label(new Rect(144, 176, 672, 112), profileLoadMessage,
                BombBrickUiTheme.BodyStyle(15, TextAnchor.MiddleCenter));
            // File paths are diagnostic data, not localization keys.
            UnityEngine.GUI.Label(new Rect(144, 296, 672, 48), profileLoadDirectory,
                BombBrickUiTheme.BodyStyle(12, TextAnchor.MiddleCenter));
            bool ready = !GameFlowController.Instance.IsTransitioning;
            if (BombBrickUiTheme.Button(new Rect(144, 374, 204, 44),
                    profileLoadFailed ? "다시 불러오기" : "계속하기", ready, true, 14))
            {
                bool retry = profileLoadFailed;
                profileLoadMessage = "";
                blockThroughFrame = Time.frameCount;
                if (retry) GameFlowController.Instance.ReloadCurrentScene();
            }
            if (BombBrickUiTheme.Button(new Rect(378, 374, 204, 44), "저장 폴더 열기",
                    System.IO.Directory.Exists(profileLoadDirectory), false, 14))
                Application.OpenURL(new System.Uri(profileLoadDirectory).AbsoluteUri);
            if (BombBrickUiTheme.Button(new Rect(612, 374, 204, 44), "메인 화면", ready, false, 14))
            {
                profileLoadMessage = "";
                blockThroughFrame = Time.frameCount;
                GameFlowController.Instance.ReturnToMain();
            }
            if (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)
                Event.current.Use();
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        public static char BootLogoGlyph(float elapsed)
        {
            if (elapsed >= BootLogoSettleAt) return 'P';
            int step = Mathf.Max(0, Mathf.FloorToInt(
                (elapsed - BootLogoScrambleAt) / BootLogoGlyphStep));
            return BootLogoGlyphs[step % BootLogoGlyphs.Length];
        }

        public static bool BootReadyForMain(float elapsed, bool warmupComplete) =>
            elapsed >= BootLogoDuration + BootLoadingMinimumDuration && warmupComplete;

        private void DrawBootLogo()
        {
            float elapsed = BootLogoElapsed;
            BombBrickUiTheme.Fill(new Rect(0f, 0f, 960f, 540f), Color.white);
            if (elapsed >= BootLogoScrambleAt)
            {
                float fadeOut = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(BootLogoFadeAt, BootLogoDuration, elapsed));
                bool settled = elapsed >= BootLogoSettleAt;
                Color ink = new Color(0.82f, 0.045f, 0.07f, 1f - fadeOut);
                float scrambleProgress = Mathf.Clamp01(Mathf.InverseLerp(
                    BootLogoScrambleAt, BootLogoSettleAt, elapsed));
                float bounceCycle = scrambleProgress * 3f;
                float bouncePhase = Mathf.Repeat(bounceCycle, 1f);
                float bounceArc = settled ? 0f : Mathf.Sin(bouncePhase * Mathf.PI);
                float bounceHeight = bounceArc * Mathf.Lerp(92f, 54f, scrambleProgress);
                float impact = settled ? 0f : 1f - Mathf.SmoothStep(0f, 0.15f,
                    Mathf.Min(bouncePhase, 1f - bouncePhase));
                float settleAge = Mathf.Clamp01((elapsed - BootLogoSettleAt) / 0.48f);
                float settleArc = settled
                    ? Mathf.Sin(settleAge * Mathf.PI) * (1f - settleAge) * 30f : 0f;
                float settleImpact = settled ? (1f - Mathf.SmoothStep(0f, 0.13f, settleAge)) : 0f;
                float scaleX = 1f + impact * 0.18f + settleImpact * 0.22f - bounceArc * 0.04f;
                float scaleY = 1f - impact * 0.16f - settleImpact * 0.18f + bounceArc * 0.08f;
                Vector2 pivot = new Vector2(426f, 300f);
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(scaleX, scaleY), pivot);
                Rect glyphRect = new Rect(350f, 174f - bounceHeight - settleArc, 152f, 192f);
                if (!settled)
                {
                    float glyphPhase = Mathf.Repeat(
                        (elapsed - BootLogoScrambleAt) / BootLogoGlyphStep, 1f);
                    float glyphBlend = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.34f, 1f, glyphPhase));
                    Color outgoing = ink;
                    outgoing.a *= 1f - glyphBlend;
                    Color incoming = ink;
                    incoming.a *= glyphBlend;
                    char nextGlyph = BootLogoGlyph(Mathf.Min(
                        BootLogoSettleAt, elapsed + BootLogoGlyphStep));
                    Label(new Rect(glyphRect.x, glyphRect.y - glyphBlend * 8f,
                        glyphRect.width, glyphRect.height), BootLogoGlyph(elapsed).ToString(),
                        120, outgoing, TextAnchor.MiddleCenter);
                    Label(new Rect(glyphRect.x, glyphRect.y + (1f - glyphBlend) * 8f,
                        glyphRect.width, glyphRect.height), nextGlyph.ToString(),
                        120, incoming, TextAnchor.MiddleCenter);
                }
                else
                    Label(glyphRect, "P", 120, ink, TextAnchor.MiddleCenter);
                GUI.matrix = matrix;

                float ang = BombBrickUiTheme.EaseOutCubic(
                    (elapsed - BootLogoAngAt) / 0.58f);
                if (ang > 0f)
                {
                    Color angInk = new Color(0.035f, 0.045f, 0.06f,
                        (1f - fadeOut) * ang);
                    Label(new Rect(Mathf.Lerp(424f, 466f, ang), 242f, 190f, 88f),
                        "ang", 52, angInk, TextAnchor.MiddleLeft);
                    Label(new Rect(354f, 334f, 252f, 28f), "PangStudio", 18,
                        angInk, TextAnchor.MiddleCenter);
                }
                if (fadeOut > 0f) BombBrickUiTheme.Fill(
                    new Rect(0f, 0f, 960f, 540f), new Color(0f, 0f, 0f, fadeOut));
            }
        }

        private void DrawTransitionError()
        {
            string error = GameFlowController.Instance.LastTransitionError;
            if (string.IsNullOrEmpty(error)) return;
            float width = Mathf.Min(550f, BombBrickUiLayout.Width - 24f);
            Rect rect = new Rect((BombBrickUiLayout.Width - width) * 0.5f, BombBrickUiLayout.Height - 58f, width, 42f);
            BombBrickUiTheme.Fill(rect, BombBrickUiTheme.Surface);
            GUI.Label(rect, error, BombBrickUiTheme.BodyStyle(13, TextAnchor.MiddleCenter));
        }

        private void DrawMain(Rect screen)
        {
            float elapsed = ReturningToMain ? 10f : Time.unscaledTime - shownAt;
            float backgroundAlpha = MainElementAlpha(elapsed, 0f, 1.8f, 0.58f, 0.24f);
            if (backdrop != null) GUI.DrawTexture(screen, backdrop, ScaleMode.ScaleAndCrop);
            DrawMenuSky(screen);
            if (skyOccluder != null) GUI.DrawTexture(screen, skyOccluder, ScaleMode.ScaleAndCrop);
            DrawBirdFlock(screen, Time.unscaledTime);
            if (treeOccluder != null) GUI.DrawTexture(screen, treeOccluder, ScaleMode.ScaleAndCrop);
            DrawMenuChundongi(screen);
            DrawMenuLeaves();
            if (backgroundAlpha < 1f)
                BombBrickUiTheme.Fill(screen, new Color(0f, 0f, 0f, 1f - backgroundAlpha));

            float shadeDark = MainElementAlpha(elapsed, 3.80f, 0.62f, 0.38f, 0.20f);
            float shadeMid = MainElementAlpha(elapsed, 4.35f, 0.56f, 0.34f, 0.20f);
            float shadeLight = MainElementAlpha(elapsed, 4.85f, 0.50f, 0.30f, 0.20f);
            DrawAnimatedShade(new Rect(screen.xMin, screen.yMin, 340f - screen.xMin, screen.height), new Color(0.004f, 0.012f, 0.009f, 0.53f), shadeDark);
            DrawAnimatedShade(new Rect(340, screen.yMin, 74, screen.height), new Color(0.004f, 0.012f, 0.009f, 0.32f), shadeMid);
            DrawAnimatedShade(new Rect(414, screen.yMin, 62, screen.height), new Color(0.004f, 0.012f, 0.009f, 0.14f), shadeLight);

            float titleAlpha = MainElementAlpha(elapsed, 5.55f, 0.70f, 0.28f, 0.22f);
            float subtitleAlpha = MainElementAlpha(elapsed, 6.35f, 0.60f, 0.22f, 0.22f);
            float startAlpha = MainElementAlpha(elapsed, 7.10f, 0.55f, 0.15f, 0.22f);
            float settingsAlpha = MainElementAlpha(elapsed, 7.75f, 0.50f, 0.10f, 0.22f);
            float quitAlpha = MainElementAlpha(elapsed, 8.32f, 0.50f, 0.05f, 0.22f);
            float footerAlpha = MainElementAlpha(elapsed, 8.90f, 0.50f, 0f, 0.22f);

            AnimatedLabel(new Rect(50, 60, 326, 32), "BecomeZ", 24,
                BombBrickUiTheme.Lime, titleAlpha, 18f);
            AnimatedLabel(new Rect(48, 95, 330, 64), "와일드 크루", 42,
                BombBrickUiTheme.TextPrimary, titleAlpha, 14f);
            AnimatedLabel(new Rect(50, 168, 330, 26), "작은 몬스터들과, 가장 높은 곳으로.", 13,
                BombBrickUiTheme.TextSecondary, subtitleAlpha, 12f);

            bool available = MainMenuAvailable;
            if (AnimatedButton(new Rect(50, 238, 324, 48), "새 게임 시작", available, true, 18, startAlpha))
                OpenNewGame();
            if (hasSavedGame && AnimatedButton(new Rect(50, 298, 324, 42), "이어하기", available, false, 18, startAlpha))
                mainExitStartedAt = Time.unscaledTime;
            float menuY = hasSavedGame ? 352f : 298f;
            if (AnimatedButton(new Rect(50, menuY, 324, 40), "설정", available, false, 15, settingsAlpha))
                OpenSettings();
            if (AnimatedButton(new Rect(50, menuY + 52, 157, 40), "게임 도움말", available, false, 13, settingsAlpha))
                OpenHelp();
            if (AnimatedButton(new Rect(217, menuY + 52, 157, 40), "크레딧", available, false, 13, settingsAlpha))
                OpenCredits();
            if (AnimatedButton(new Rect(50, menuY + 104, 324, 38), "게임 종료", available, false, 15, quitAlpha))
                confirmQuit = true;

            AnimatedLabel(new Rect(50, 504, 350, 22), "v" + Application.version + "  ·  PangStudio / Pangsoo", 10,
                BombBrickUiTheme.TextSecondary, footerAlpha, 8f);
        }

        private static void DrawAnimatedShade(Rect rect, Color color, float alpha)
        {
            if (alpha <= 0f) return;
            rect.width *= alpha;
            color.a *= alpha;
            BombBrickUiTheme.Fill(rect, color);
        }

        private float MainElementAlpha(float elapsed, float enterAt, float enterDuration,
            float exitAt, float exitDuration)
        {
            float alpha = BombBrickUiTheme.EaseOutCubic((elapsed - enterAt) / enterDuration);
            if (mainExitStartedAt < 0f) return alpha;
            float exitElapsed = Time.unscaledTime - mainExitStartedAt;
            return alpha * (1f - BombBrickUiTheme.EaseOutCubic((exitElapsed - exitAt) / exitDuration));
        }

        private void AnimatedLabel(Rect rect, string text, int size, Color color,
            float alpha, float slide)
        {
            if (alpha <= 0f) return;
            rect.x -= (1f - alpha) * slide;
            color.a *= alpha;
            Label(rect, text, size, color);
        }

        private static bool AnimatedButton(Rect rect, string text, bool enabled,
            bool primary, int size, float alpha)
        {
            if (alpha <= 0f) return false;
            rect.x -= (1f - alpha) * 14f;
            return BombBrickUiTheme.Button(rect, text, enabled && alpha >= 0.98f,
                primary, size, alpha);
        }

        public static Texture2D CreateMenuSkyOccluder(Texture2D source, bool treesOnly = false)
        {
            // The menu is flattened art. Key its sky gradient once; retain the exact
            // original tree/terrain pixels above the moving distant sky effects.
            // ponytail: keyed to this backdrop; use an authored sky mask if the art changes.
            RenderTexture previous = RenderTexture.active;
            RenderTexture copy = RenderTexture.GetTemporary(source.width, source.height,
                0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D layer = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                { name = "Main menu sky occlusion", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            try
            {
                Graphics.Blit(source, copy);
                RenderTexture.active = copy;
                layer.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(copy);
            }
            Color32[] pixels = layer.GetPixels32();
            int skyColumn = Mathf.RoundToInt(source.width * 0.49f);
            int lastClearSkyRow = Mathf.RoundToInt(source.height * 0.68f);
            for (int y = Mathf.RoundToInt(source.height * 0.64f); y < source.height; y++)
            {
                Color32 sky = pixels[Mathf.Max(y, lastClearSkyRow) * source.width + skyColumn];
                for (int x = 0; x < source.width; x++)
                {
                    int index = y * source.width + x;
                    Color32 pixel = pixels[index];
                    int difference = Mathf.Max(Mathf.Abs(pixel.r - sky.r),
                        Mathf.Max(Mathf.Abs(pixel.g - sky.g), Mathf.Abs(pixel.b - sky.b)));
                    if (difference <= 20) pixels[index].a = 0;
                }
            }
            if (treesOnly)
            {
                // Keep the near tree silhouettes, including warm bark highlights. The
                // blue/grey mountains and the view through the arch belong behind birds.
                // ponytail: these bounds/palette belong to MainMenuExpeditionBase; author
                // a foreground mask if that artwork or the tree positions change.
                layer.name = "Main menu foreground trees";
                for (int y = 0; y < source.height; y++)
                {
                    float top = 1f - (y + 0.5f) / source.height;
                    float leftEdge = top < 0.44f ? Mathf.Max(0.32f, 0.49f - top * 0.6f)
                        : Mathf.Lerp(0.32f, 0.53f, (top - 0.44f) / 0.2f);
                    for (int x = 0; x < source.width; x++)
                    {
                        int index = y * source.width + x;
                        Color32 pixel = pixels[index];
                        bool nearTree = x < source.width * leftEdge || x > source.width * 0.65f;
                        bool treeColor = pixel.b < 110;
                        if (top < 0.64f && (!nearTree || !treeColor)) pixels[index].a = 0;
                    }
                }
            }
            layer.SetPixels32(pixels);
            layer.Apply(false, true);
            return layer;
        }

        public static Texture2D CreateMenuPixelAtlas(Texture2D source)
        {
            if (source == null) return null;
            // Importer resizing blends colors even with Point filtering. Sample the
            // full-size source once on a real 32px-per-frame grid instead.
            source.filterMode = FilterMode.Point;
            RenderTexture previous = RenderTexture.active;
            RenderTexture copy = RenderTexture.GetTemporary(128, 128, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false)
                { name = "Chundongi 32px frames", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            try
            {
                Graphics.Blit(source, copy);
                RenderTexture.active = copy;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0, false);
                pixels.Apply(false, true);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(copy);
            }
            return pixels;
        }

        public static float MenuPeekAmount(float age, float hold)
        {
            if (age < 0f || age >= MenuPeekEnter + hold + MenuPeekExit) return 0f;
            return age < MenuPeekEnter
                ? Mathf.SmoothStep(0f, 1f, age / MenuPeekEnter)
                : 1f - Mathf.SmoothStep(0f, 1f, (age - MenuPeekEnter - hold) / MenuPeekExit);
        }

        public static int MenuPeekFrame(float age, float hold)
        {
            if (age < 0f || age >= MenuPeekEnter + hold + MenuPeekExit) return -1;
            if (age < MenuPeekEnter) return Mathf.Min(8, (int)(age / 0.12f));
            float watching = age - MenuPeekEnter;
            if (watching >= hold) return Mathf.Min(15, 11 + (int)((watching - hold) / 0.12f));
            float blink = watching - hold * 0.45f;
            return blink < 0f || blink >= 0.36f ? 8 : blink < 0.16f ? 9 : 10;
        }

        public static Rect MainArtworkRect(Rect screen, Vector2 artworkSize)
        {
            float scale = Mathf.Max(screen.width / artworkSize.x, screen.height / artworkSize.y);
            return new Rect(screen.center - artworkSize * scale * 0.5f, artworkSize * scale);
        }

        private void UpdateMenuChundongi()
        {
            float now = Time.unscaledTime;
            if (now < nextMenuPeekAt || mainExitStartedAt >= 0f) return;
            menuPeekStartedAt = now;
            menuPeekHold = 1.6f + (float)menuPeekRandom.NextDouble() * 1.2f;
            nextMenuPeekAt = now + MenuPeekEnter + menuPeekHold + MenuPeekExit +
                7f + (float)menuPeekRandom.NextDouble() * 10f;
        }

        private bool MainMenuAvailable => bootLogoFinished && !HasPendingSave && !HasProfileLoadNotice &&
            !GameFlowController.Instance.IsTransitioning && !newGameOpen && !chundongiMemorialOpen &&
            !settingsOpen && !helpOpen && !creditsOpen && !confirmQuit && mainExitStartedAt < 0f;

        private void OpenChundongiMemorial()
        {
            if (!MainMenuAvailable || CurrentSceneName != GameSceneCatalog.Boot) return;
            var store = new PlayerProfileStore();
            TrySaveProgress(() =>
            {
                chundongiAlreadyOwned = !store.UnlockMemorialChundongi(
                    Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase"));
                chundongiMemorialOpen = true;
                RefreshContinueAvailability();
            }, store.DirectoryPath);
        }

        private void CloseChundongiMemorial()
        {
            chundongiMemorialOpen = false;
            blockThroughFrame = Time.frameCount;
        }

        private void DrawChundongiMemorial()
        {
            BombBrickUiTheme.Fill(new Rect(0, 0, 960, 540), new Color(0, 0, 0, .76f));
            BombBrickUiTheme.DrawPanel(new Rect(140, 24, 680, 492), true);
            Label(new Rect(164, 40, 632, 38), GameCredits.ChundongiMemorialTitle,
                23, BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            if (menuChundongiAtlas != null)
                GUI.DrawTextureWithTexCoords(new Rect(190, 94, 88, 88), menuChundongiAtlas,
                    new Rect(0, .25f, .25f, .25f), true);
            Label(new Rect(296, 100, 488, 32), chundongiAlreadyOwned
                    ? "춘동이는 이미 당신과 함께하고 있습니다." : "춘동이가 동료가 되었습니다",
                16, BombBrickUiTheme.Cyan);
            Label(new Rect(296, 140, 488, 26), "평균 S급 · 춘동이", 14, BombBrickUiTheme.Lime);
            GUI.Label(new Rect(180, 190, 600, 220), GameCredits.ChundongiMemorialBody,
                BombBrickUiTheme.BodyStyle(15, TextAnchor.UpperCenter));
            Label(new Rect(166, 421, 628, 24), "이제 새 게임을 시작해도 춘동이가 늘 함께합니다.",
                13, BombBrickUiTheme.TextSecondary, TextAnchor.MiddleCenter);
            if (BombBrickUiTheme.Button(new Rect(306, 466, 348, 32), "함께해 줘서 고마워", true, true, 14))
                CloseChundongiMemorial();
        }

        private void DrawMenuChundongi(Rect screen)
        {
            float age = Time.unscaledTime - menuPeekStartedAt;
            float peek = MenuPeekAmount(age, menuPeekHold);
            int frame = MenuPeekFrame(age, menuPeekHold);
            if (peek <= 0f || frame < 0 || backdrop == null || menuChundongiAtlas == null) return;

            Rect artwork = MainArtworkRect(screen, new Vector2(backdrop.width, backdrop.height));
            float scale = artwork.width / backdrop.width;
            // The atlas animates the bend, paws and head tilt. Small root movement only
            // tucks the first/last poses fully behind the tree; never rotate the sprite.
            Rect character = new Rect(Mathf.Round(1347f + 30f * (1f - peek) * (1f - peek)),
                464f, 80f, 80f);
            Rect uv = new Rect(frame % 4 * 0.25f, (3 - frame / 4) * 0.25f, 0.25f, 0.25f);
            Vector2 pointer = Event.current.mousePosition;
            Rect characterOnScreen = new Rect(artwork.position + character.position * scale, character.size * scale);
            bool hit = false;
            // The background is flattened: clip along the inner right root's pixel edge.
            // ponytail: this contour belongs to MainMenuExpeditionBase; retrace if it changes.
            for (int y = 416; y < 544; y += 4)
            {
                float edge = y < 496 ? 1407f : y < 508 ? 1403f : y < 516 ? 1399f :
                    y < 524 ? 1395f : y < 532 ? 1391f : 1387f;
                Rect opening = new Rect(artwork.x + 1300f * scale, artwork.y + y * scale,
                    (edge - 1300f) * scale, 4f * scale);
                hit |= opening.Contains(pointer) && characterOnScreen.Contains(pointer);
                GUI.BeginGroup(opening);
                GUI.DrawTextureWithTexCoords(new Rect((character.x - 1300f) * scale,
                    (character.y - y) * scale, character.width * scale, character.height * scale),
                    menuChundongiAtlas, uv, true);
                GUI.EndGroup();
            }
            if (hit && peek >= .2f && MainMenuAvailable &&
                Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                OpenChundongiMemorial();
            }
        }

        private void DrawMenuSky(Rect screen)
        {
            float time = Time.unscaledTime;
            float coverScale = Mathf.Max(screen.width / 960f, screen.height / 540f);
            DrawMenuSun(screen.center + new Vector2(144f, -131f) * coverScale, time);

            for (int i = 0; i < 4; i++)
            {
                float scale = i % 2 == 0 ? 1f : 0.72f;
                float width = Mathf.Max(2f, Mathf.Round(5f * scale)) * 11f;
                // A fixed phase period also keeps resize from wrapping visible objects.
                float x = Mathf.Floor(SkyTravelX(screen, i * 0.25f - time / 187.5f, 0f, width));
                float y = screen.yMin + screen.height * (32f + (i % 3) * 34f) / 540f;
                DrawPixelCloud(new Vector2(x, Mathf.Floor(y)), scale,
                    new Color(1f, 0.91f, 0.65f, i % 2 == 0 ? 0.3f : 0.2f));
            }
        }

        private static void DrawMenuSun(Vector2 center, float time)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 1.35f);
            Color halo = new Color(1f, 0.83f, 0.34f, 0.035f + pulse * 0.045f);
            BombBrickUiTheme.Fill(new Rect(center.x - 34f, center.y - 18f, 68f, 36f), halo);
            BombBrickUiTheme.Fill(new Rect(center.x - 18f, center.y - 34f, 36f, 68f), halo);
            Color edge = new Color(1f, 0.91f, 0.58f, 0.42f + pulse * 0.18f);
            BombBrickUiTheme.Fill(new Rect(center.x - 13f, center.y - 9f, 26f, 18f), edge);
            BombBrickUiTheme.Fill(new Rect(center.x - 9f, center.y - 13f, 18f, 26f), edge);
            Color core = new Color(1f, 0.97f, 0.79f, 0.86f + pulse * 0.14f);
            BombBrickUiTheme.Fill(new Rect(center.x - 9f, center.y - 9f, 18f, 18f), core);
        }

        private static void DrawPixelCloud(Vector2 position, float scale, Color color)
        {
            float unit = Mathf.Max(2f, Mathf.Round(5f * scale));
            BombBrickUiTheme.Fill(new Rect(position.x + unit * 2f, position.y, unit * 5f, unit * 2f), color);
            BombBrickUiTheme.Fill(new Rect(position.x + unit, position.y + unit, unit * 8f, unit * 2f), color);
            BombBrickUiTheme.Fill(new Rect(position.x, position.y + unit * 2f, unit * 11f, unit * 2f), color);
            BombBrickUiTheme.Fill(new Rect(position.x + unit * 3f, position.y - unit, unit * 2f, unit), color);
        }

        private static void DrawBirdFlock(Rect screen, float time)
        {
            float travel = SkyTravelX(screen, time / 43f, 36f, 11f);
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Floor(travel - i * 12f);
                float y = Mathf.Floor(screen.yMin + screen.height * 122f / 540f +
                    i * 5f + Mathf.Sin(time * 1.7f + i) * 4f);
                DrawPixelBird(new Vector2(x, y), ((int)(time * 5f) + i) % 2 == 0);
            }
        }

        private static void DrawPixelBird(Vector2 position, bool wingsUp)
        {
            Color color = new Color(0.16f, 0.25f, 0.26f, 0.72f);
            BombBrickUiTheme.Fill(new Rect(position.x + 4f, position.y + 2f, 3f, 2f), color);
            if (wingsUp)
            {
                BombBrickUiTheme.Fill(new Rect(position.x, position.y, 4f, 2f), color);
                BombBrickUiTheme.Fill(new Rect(position.x + 7f, position.y, 4f, 2f), color);
            }
            else
            {
                BombBrickUiTheme.Fill(new Rect(position.x + 1f, position.y + 4f, 3f, 2f), color);
                BombBrickUiTheme.Fill(new Rect(position.x + 7f, position.y + 4f, 3f, 2f), color);
            }
        }

        private void UpdateMenuLeaves()
        {
            float now = Time.unscaledTime;
            for (int i = menuLeaves.Count - 1; i >= 0; i--)
                if (now - menuLeaves[i].born >= menuLeaves[i].lifetime) menuLeaves.RemoveAt(i);

            if (now < nextMenuLeafBurst) return;
            int count = 3 + menuAmbientRandom.Next(4);
            for (int i = 0; i < count; i++)
            {
                float hue = (float)menuAmbientRandom.NextDouble();
                menuLeaves.Add(new MenuLeaf
                {
                    born = now + i * 0.12f,
                    lifetime = 5.5f + (float)menuAmbientRandom.NextDouble() * 2.5f,
                    x = 160f + (float)menuAmbientRandom.NextDouble() * 700f,
                    y = -12f - (float)menuAmbientRandom.NextDouble() * 72f,
                    speedX = -7f + (float)menuAmbientRandom.NextDouble() * 17f,
                    speedY = 38f + (float)menuAmbientRandom.NextDouble() * 22f,
                    wobble = 8f + (float)menuAmbientRandom.NextDouble() * 15f,
                    phase = (float)menuAmbientRandom.NextDouble() * Mathf.PI * 2f,
                    size = 3f + (float)menuAmbientRandom.NextDouble() * 2f,
                    color = Color.Lerp(new Color(0.25f, 0.46f, 0.13f, 0.8f),
                        new Color(0.69f, 0.72f, 0.2f, 0.85f), hue)
                });
            }
            nextMenuLeafBurst = now + 3.2f + (float)menuAmbientRandom.NextDouble() * 3.6f;
        }

        private void DrawMenuLeaves()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < menuLeaves.Count; i++)
            {
                MenuLeaf leaf = menuLeaves[i];
                float age = now - leaf.born;
                if (age < 0f || age >= leaf.lifetime) continue;
                float fade = Mathf.Clamp01(Mathf.Min(age * 2f, (leaf.lifetime - age) * 1.5f));
                float x = leaf.x + leaf.speedX * age + Mathf.Sin(age * 3.4f + leaf.phase) * leaf.wobble;
                float y = leaf.y + leaf.speedY * age;
                Color color = leaf.color;
                color.a *= fade;
                float size = leaf.size;
                bool leanRight = Mathf.Sin(age * 5f + leaf.phase) >= 0f;
                BombBrickUiTheme.Fill(new Rect(Mathf.Round(x), Mathf.Round(y), size, size * 2f), color);
                BombBrickUiTheme.Fill(new Rect(Mathf.Round(x + (leanRight ? size : -size)), Mathf.Round(y + size), size, size), color);
            }
        }

        private void DrawPause()
        {
            Rect panel = new Rect(310, 62, 340, 426);
            BombBrickUiTheme.DrawPanel(panel, true);
            Label(new Rect(334, 82, 292, 40), "잠깐 쉬어가기", 23, BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            if (BombBrickUiTheme.Button(new Rect(338, 132, 284, 46), "계속하기  [ESC]", !confirmQuit, true, 16)) ClosePanels();
            if (BombBrickUiTheme.Button(new Rect(338, 188, 284, 42), "설정", !confirmQuit, false, 15)) OpenSettings();
            if (BombBrickUiTheme.Button(new Rect(338, 242, 90, 42), "게임 도움말", !confirmQuit, false, 12)) OpenHelp();
            if (BombBrickUiTheme.Button(new Rect(435, 242, 90, 42), "크레딧", !confirmQuit, false, 12)) OpenCredits();
            if (BombBrickUiTheme.Button(new Rect(532, 242, 90, 42), "문의", !confirmQuit, false, 12)) OpenSupport();
            if (BombBrickUiTheme.Button(new Rect(338, 296, 284, 42), "메인 화면", !confirmQuit, false, 15))
                GameFlowController.Instance.ReturnToMain();
            if (BombBrickUiTheme.Button(new Rect(338, 350, 284, 42), "게임 종료", !confirmQuit, false, 15)) confirmQuit = true;
            Label(new Rect(330, 434, 300, 24), "전투는 멈춰 있습니다.", 11, BombBrickUiTheme.TextSecondary, TextAnchor.MiddleCenter);
        }

        private void DrawHelp()
        {
            if (!menuOpen) BombBrickUiTheme.Fill(new Rect(0, 0, 960, 540), new Color(0, 0, 0, 0.72f));
            BombBrickUiTheme.DrawPanel(new Rect(68, 28, 824, 484), true);
            Label(new Rect(92, 45, 710, 34), "게임 도움말", 23, BombBrickUiTheme.TextPrimary);
            Label(new Rect(92, 83, 750, 24), "진행 기록을 바꾸지 않고 기본 조작과 탐험 흐름을 다시 확인하세요.", 12, BombBrickUiTheme.TextSecondary);
            if (BombBrickUiTheme.Button(new Rect(844, 46, 28, 28), "X")) CloseHelp();
            for (int i = 0; i < GameHelp.Pages.Count; i++)
                if (BombBrickUiTheme.Button(new Rect(92, 122 + i * 39, 188, 33),
                    GameHelp.Pages[i].Title, true, helpPage == i, 13)) helpPage = i;
            if (BombBrickUiTheme.Button(new Rect(92, 122 + GameHelp.Pages.Count * 39, 188, 33),
                GameSupport.Title, true, helpPage == GameHelp.Pages.Count, 13)) helpPage = GameHelp.Pages.Count;

            if (helpPage == GameHelp.Pages.Count) DrawSupport();
            else
            {
                GameHelp.Page page = GameHelp.Pages[helpPage];
                Label(new Rect(306, 118, 550, 38), page.Title, 22, BombBrickUiTheme.Cyan);
                for (int i = 0; i < page.Steps.Count; i++)
                {
                    float y = 171 + i * 91;
                    BombBrickUiTheme.Fill(new Rect(306, y, 550, 83), BombBrickUiTheme.Surface);
                    BombBrickUiTheme.Badge(new Rect(318, y + 12, 28, 26), (i + 1).ToString(), BombBrickUiTheme.Cyan);
                    Label(new Rect(360, y + 9, 480, 65), page.Steps[i], 14, BombBrickUiTheme.TextPrimary);
                }
            }
            Label(new Rect(306, 459, 210, 30), (helpPage + 1) + " / " + (GameHelp.Pages.Count + 1) + "  ·  ESC", 12, BombBrickUiTheme.TextSecondary);
            if (BombBrickUiTheme.Button(new Rect(584, 458, 124, 32), "이전", helpPage > 0, false, 13)) helpPage--;
            if (BombBrickUiTheme.Button(new Rect(724, 458, 132, 32), "다음", helpPage < GameHelp.Pages.Count, false, 13)) helpPage++;
        }

        private void DrawSupport()
        {
            Label(new Rect(306, 118, 550, 38), GameSupport.Title, 22, BombBrickUiTheme.Cyan);
            // Product names and versions must not be matched against dialogue translation patterns.
            UnityEngine.GUI.Label(new Rect(306, 164, 550, 28), Application.productName +
                "  ·  v" + Application.version, BombBrickUiTheme.TitleStyle(16));
            bool hasContact = !string.IsNullOrEmpty(GameSupport.ContactEmail);
            Label(new Rect(306, 197, 550, 28), hasContact ? GameSupport.ContactEmail : "문의 주소 준비 중", 14, BombBrickUiTheme.TextSecondary);
            if (BombBrickUiTheme.Button(new Rect(306, 233, 264, 34), "이메일로 문의", hasContact, true, 14))
                SupportAction(() => Application.OpenURL(GameSupport.EmailUrl), "");
            if (BombBrickUiTheme.Button(new Rect(584, 233, 272, 34), "문의 주소 복사", hasContact, false, 14))
                SupportAction(() => GUIUtility.systemCopyBuffer = GameSupport.ContactEmail, "문의 주소를 복사했습니다.");
            Label(new Rect(306, 278, 550, 42), "발생 순서와 화면, 필요하면 로그 파일을 함께 보내주세요.\n진단 정보에는 게임 버전과 실행 환경이 포함됩니다.", 13, BombBrickUiTheme.TextSecondary);
            if (BombBrickUiTheme.Button(new Rect(306, 330, 264, 34), "진단 정보 복사", true, false, 14))
                SupportAction(() => GUIUtility.systemCopyBuffer = GameSupport.DiagnosticInformation(), "진단 정보를 복사했습니다.");
            if (BombBrickUiTheme.Button(new Rect(584, 330, 272, 34), "로그 폴더 열기", true, false, 14))
                SupportAction(() => { if (!GameSupport.TryOpenLogDirectory()) throw new System.IO.DirectoryNotFoundException(); }, "");
            string logPath = Application.consoleLogPath;
            Label(new Rect(306, 371, 550, 48), string.IsNullOrWhiteSpace(logPath)
                ? "이 실행에서는 로그 파일을 사용할 수 없습니다."
                : GameLocalization.Text("로그 위치") + "\n" + logPath, 11, BombBrickUiTheme.TextSecondary);
            Label(new Rect(306, 422, 550, 26), supportMessage, 12, BombBrickUiTheme.Lime);
        }

        private void SupportAction(System.Action action, string success)
        {
            try { action(); supportMessage = success; }
            catch (System.Exception)
            {
                supportMessage = "작업을 완료하지 못했습니다. 문의 주소와 로그 위치를 직접 확인해 주세요.";
            }
        }

        private void DrawCredits()
        {
            if (!menuOpen) BombBrickUiTheme.Fill(new Rect(0, 0, 960, 540), new Color(0, 0, 0, 0.72f));
            BombBrickUiTheme.DrawPanel(new Rect(68, 28, 824, 484), true);
            if (creditsPage == 0)
            {
                Label(new Rect(306, 124, 550, 38), "BecomeZ: " + GameLocalization.Text("와일드 크루"), 24, BombBrickUiTheme.Cyan);
                Label(new Rect(306, 180, 550, 28), "개발 스튜디오", 13, BombBrickUiTheme.TextSecondary);
                Label(new Rect(306, 212, 550, 38), "PangStudio", 26, BombBrickUiTheme.TextPrimary);
                Label(new Rect(306, 268, 550, 28), "기획 · 개발", 13, BombBrickUiTheme.TextSecondary);
                Label(new Rect(306, 298, 550, 35), "Pangsoo", 23, BombBrickUiTheme.TextPrimary);
                Label(new Rect(306, 346, 550, 28), "마스코트", 13, BombBrickUiTheme.TextSecondary);
                Label(new Rect(306, 375, 550, 35), "춘동이", 23, BombBrickUiTheme.TextPrimary);
                DrawCreditsChundongi(new Rect(716, 308, 128, 128));
                Label(new Rect(306, 434, 550, 32), "함께 모험해 주셔서 감사합니다.", 14, BombBrickUiTheme.TextSecondary);
            }
            else if (creditsPage == 1)
            {
                Label(new Rect(306, 124, 550, 38), GameCredits.ChundongiMemorialTitle,
                    22, BombBrickUiTheme.Cyan);
                DrawCreditsChundongi(new Rect(716, 110, 128, 128));
                GUI.Label(new Rect(306, 238, 550, 228), GameCredits.ChundongiMemorialBody,
                    BombBrickUiTheme.BodyStyle(15, TextAnchor.UpperLeft));
            }
            else
            {
                // Keep legal originals readable at a fixed size, independently of the pixel menu.
                if (creditsTextStyle == null)
                {
                    creditsTextStyle = new GUIStyle(GUI.skin.label)
                    {
                        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                        fontSize = 14, wordWrap = true, richText = false,
                        alignment = TextAnchor.UpperLeft
                    };
                    creditsTextStyle.normal.textColor = BombBrickUiTheme.TextPrimary;
                }
                GUIStyle style = creditsTextStyle;
                if (creditsLineHeights == null)
                {
                    creditsLineHeights = new float[creditsLines.Length];
                    creditsContentHeight = 12;
                    for (int i = 0; i < creditsLines.Length; i++)
                    {
                        creditsLineHeights[i] = Mathf.Max(style.lineHeight,
                            style.CalcHeight(new GUIContent(creditsLines[i]), 510));
                        creditsContentHeight += creditsLineHeights[i];
                    }
                }
                creditsScroll = GUI.BeginScrollView(new Rect(306, 122, 550, 337), creditsScroll,
                    new Rect(0, 0, 520, Mathf.Max(330, creditsContentHeight)), false, true);
                // Only visible lines generate geometry; package notices can exceed 100,000 characters.
                float y = 4;
                for (int i = 0; i < creditsLines.Length; i++)
                {
                    float height = creditsLineHeights[i];
                    if (y + height >= creditsScroll.y && y <= creditsScroll.y + 337)
                        UnityEngine.GUI.Label(new Rect(4, y, 510, height), creditsLines[i], style);
                    y += height;
                }
                GUI.EndScrollView();
            }
            Label(new Rect(92, 45, 710, 34), "크레딧", 23, BombBrickUiTheme.TextPrimary);
            Label(new Rect(92, 83, 750, 24), "함께 만든 사람들과 사용 자산", 12, BombBrickUiTheme.TextSecondary);
            if (BombBrickUiTheme.Button(new Rect(844, 46, 28, 28), "X")) CloseCredits();
            for (int i = 0; i <= GameCredits.NoticeTitles.Count + 1; i++)
                if (BombBrickUiTheme.Button(new Rect(92, 122 + i * 43, 188, 35),
                    i == 0 ? "제작진" : i == 1 ? GameCredits.ChundongiMemorialTitle : GameCredits.NoticeTitles[i - 2],
                    true, creditsPage == i, 13)) SelectCreditsPage(i);
            Label(new Rect(306, 475, 550, 22), "v" + Application.version + "  ·  ESC", 11, BombBrickUiTheme.TextSecondary);
        }

        private void DrawSettings()
        {
            if (!menuOpen) BombBrickUiTheme.Fill(new Rect(0, 0, 960, 540), new Color(0, 0, 0, 0.68f));
            Rect panel = new Rect(176, 42, 608, 456);
            BombBrickUiTheme.DrawPanel(panel, true);
            Label(new Rect(202, 60, 440, 36), "설정", 24, BombBrickUiTheme.TextPrimary);
            if (BombBrickUiTheme.Button(new Rect(728, 58, 32, 32), "X")) CloseSettings();
            if (BombBrickUiTheme.Button(new Rect(202, 108, 102, 36), "화면", true, settingsTab == 0, 13)) settingsTab = 0;
            if (BombBrickUiTheme.Button(new Rect(314, 108, 102, 36), "소리", true, settingsTab == 1, 13)) settingsTab = 1;
            if (BombBrickUiTheme.Button(new Rect(426, 108, 102, 36), "조작", true, settingsTab == 2, 13)) settingsTab = 2;
            if (BombBrickUiTheme.Button(new Rect(538, 108, 102, 36), "언어", true, settingsTab == 3, 13)) settingsTab = 3;
            if (BombBrickUiTheme.Button(new Rect(650, 108, 106, 36), "데이터", true, settingsTab == 4, 13)) settingsTab = 4;
            bool controlsEnabled = GUI.enabled;
            GUI.enabled = controlsEnabled && displayConfirmUntil <= 0f;
            if (settingsTab == 0)
            {
                int resolutionIndex = 0;
                for (int i = 0; i < Resolutions.Length; i++)
                    if (Resolutions[i].x == draft.width && Resolutions[i].y == draft.height) resolutionIndex = i;
                int changed = Choice(162, "해상도", draft.width + " × " + draft.height, resolutionIndex, Resolutions.Length);
                if (changed != resolutionIndex) { draft.width = Resolutions[changed].x; draft.height = Resolutions[changed].y; }
                draft.fullscreen = Choice(200, "화면 모드", draft.fullscreen ? "전체 화면" : "창 모드", draft.fullscreen ? 1 : 0, 2) == 1;
                draft.vSync = Choice(238, "수직 동기화", draft.vSync ? "켜짐" : "꺼짐", draft.vSync ? 1 : 0, 2) == 1;
                int fpsIndex = draft.frameRate == 30 ? 0 : draft.frameRate == 120 ? 2 : 1;
                int next = Choice(276, "최대 프레임", draft.vSync ? "모니터에 맞춤" : draft.frameRate + " FPS", fpsIndex, 3, !draft.vSync);
                draft.frameRate = next == 0 ? 30 : next == 2 ? 120 : 60;
                draft.showFps = Choice(314, "FPS 표시", draft.showFps ? "켜짐" : "꺼짐", draft.showFps ? 1 : 0, 2) == 1;
                int shake = Choice(352, "카메라 흔들림", settings.cameraShake == 0 ? "끄기" :
                    settings.cameraShake == 1 ? "약하게 (35%)" : "기본 (100%)", settings.cameraShake, 3);
                if (shake != settings.cameraShake)
                {
                    settings.cameraShake = draft.cameraShake = shake;
                    settings.Apply(false);
                    settings.Save();
                    settingsMessage = "카메라 흔들림을 저장했습니다.";
                }
                bool reduced = Choice(390, "강한 섬광 감소", settings.reduceFlashes ? "켜짐" : "꺼짐",
                    settings.reduceFlashes ? 1 : 0, 2) == 1;
                if (reduced != settings.reduceFlashes)
                {
                    settings.reduceFlashes = draft.reduceFlashes = reduced;
                    settings.Apply(false);
                    settings.Save();
                    settingsMessage = "섬광 감소 설정을 저장했습니다.";
                }
                if (BombBrickUiTheme.Button(new Rect(584, 440, 172, 34), "화면 설정 적용", true, true, 13)) ApplyDisplay();
                Label(new Rect(204, 440, 366, 34), settingsMessage.Length > 0 ? settingsMessage : "기본 60 FPS · 화면 변경 후 확인", 11, BombBrickUiTheme.TextSecondary);
            }
            else if (settingsTab == 1)
            {
                float master = VolumeRow(184, "전체 음량", settings.masterVolume);
                float music = VolumeRow(252, "배경 음악", settings.musicVolume);
                float effects = VolumeRow(320, "효과음", settings.effectsVolume);
                if (master != settings.masterVolume || music != settings.musicVolume || effects != settings.effectsVolume)
                {
                    settings.masterVolume = master; settings.musicVolume = music; settings.effectsVolume = effects;
                    settings.Apply(false); audioPlayer.Apply(settings); settings.Save();
                }
                bool mute = Choice(382, "백그라운드 음소거", settings.muteWhenUnfocused ? "켜짐" : "꺼짐",
                    settings.muteWhenUnfocused ? 1 : 0, 2) == 1;
                if (mute != settings.muteWhenUnfocused)
                {
                    settings.muteWhenUnfocused = draft.muteWhenUnfocused = mute;
                    settings.Apply(false); settings.Save();
                }
                if (BombBrickUiTheme.Button(new Rect(584, 438, 172, 34), "효과음 듣기")) GameAudio.Play(GameAudio.Cue.Reward);
                Label(new Rect(204, 442, 366, 26), "소리는 즉시 적용되고 저장됩니다.", 11, BombBrickUiTheme.TextSecondary);
            }
            else if (settingsTab == 2)
            {
                Instruction(164, "마우스 드래그", "몬스터 배치 · 아이템 장착 / 조합");
                Instruction(208, "SPACE", "전투 시작 · 전투 일시정지");
                Instruction(252, "마우스 오른쪽", "선택 / 드래그 취소");
                Instruction(296, "ESC", "메뉴 열기 · 닫기");
                bool pause = Choice(346, "창 전환 시 자동 정지", settings.pauseWhenUnfocused ? "켜짐" : "꺼짐",
                    settings.pauseWhenUnfocused ? 1 : 0, 2) == 1;
                if (pause != settings.pauseWhenUnfocused)
                {
                    settings.pauseWhenUnfocused = draft.pauseWhenUnfocused = pause;
                    settings.Apply(false); settings.Save();
                }
                Label(new Rect(204, 387, 552, 30), "복귀 시 재개 · 수동 일시정지는 유지", 11, BombBrickUiTheme.TextSecondary);
                Label(new Rect(204, 430, 552, 44), "게임 진행은 자동 저장됩니다.\n전투 중 종료하면 해당 전투 시작 시점에서 이어집니다.", 12, BombBrickUiTheme.TextSecondary);
            }
            else if (settingsTab == 3)
            {
                int languageIndex = draft.language == GameLocalization.English ? 1 : 0;
                int changed = Choice(196, "표시 언어", languageIndex == 0 ? "한국어" : "English",
                    languageIndex, 2);
                if (changed != languageIndex)
                {
                    draft.language = changed == 1 ? GameLocalization.English : GameLocalization.Korean;
                    settings.language = draft.language;
                    GameLocalization.SetLanguage(settings.language);
                    settings.Save();
                    settingsMessage = GameLocalization.Text("언어 설정을 저장했습니다.");
                }
                Label(new Rect(204, 420, 552, 30),
                    string.IsNullOrEmpty(settingsMessage)
                        ? GameLocalization.TextByKey("settings.language_sync",
                            "언어 변경은 즉시 적용되고 저장됩니다.")
                        : settingsMessage,
                    11, BombBrickUiTheme.TextSecondary, TextAnchor.MiddleCenter);
            }
            else DrawDataSettings();
            GUI.enabled = controlsEnabled;
            if (displayConfirmUntil > 0f)
            {
                BombBrickUiTheme.Fill(new Rect(176, 42, 608, 456), new Color(0, 0, 0, 0.85f));
                BombBrickUiTheme.DrawPanel(new Rect(228, 146, 504, 222), true);
                Label(new Rect(236, 168, 488, 40), "이 화면 설정을 유지할까요?", 22, BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
                Label(new Rect(236, 226, 488, 32), Mathf.Max(0, Mathf.CeilToInt(displayConfirmUntil - Time.realtimeSinceStartup)) + "초 후 이전 설정으로 돌아갑니다.", 14, BombBrickUiTheme.TextSecondary, TextAnchor.MiddleCenter);
                if (BombBrickUiTheme.Button(new Rect(252, 302, 208, 44), "되돌리기", true, false, 15)) RevertDisplay();
                if (BombBrickUiTheme.Button(new Rect(500, 302, 208, 44), "유지", true, true, 15)) ConfirmDisplay();
            }
        }

        private void DrawDataSettings()
        {
            Label(new Rect(204, 170, 552, 34), "새 게임과 저장 기록", 20, BombBrickUiTheme.TextPrimary);
            Label(new Rect(204, 216, 552, 112),
                "몬스터 수집·편성, 증표·그물, 영구 연구, 지역·난이도 해금, 진행 중인 탐험과 결과 기록을 초기화합니다.\n튜토리얼 진행 여부는 새 게임에서 선택할 수 있습니다.",
                14, BombBrickUiTheme.TextPrimary);
            Label(new Rect(204, 334, 552, 46), "화면·음량·언어 설정은 유지합니다.", 13, BombBrickUiTheme.Lime);
            Label(new Rect(204, 385, 552, 42), "새 게임은 메인 화면에서만 시작할 수 있습니다.",
                12, BombBrickUiTheme.TextSecondary);
            DrawResetBackupButton(new Rect(480, 440, 276, 34));
        }

        private void RefreshContinueAvailability()
        {
            nextSaveCheck = Time.unscaledTime + 1f;
            try
            {
                GameDataPaths.EnsureMigrated();
                hasSavedGame = new PlayerProfileStore().HasSavedProgress;
            }
            catch (System.Exception exception) when (exception is System.IO.IOException ||
                exception is System.UnauthorizedAccessException || exception is System.Security.SecurityException ||
                exception is System.NotSupportedException)
            {
                hasSavedGame = true; // Let Continue show the existing load/recovery notice for inaccessible saves.
            }
        }

        private void OpenNewGame()
        {
            if (CurrentSceneName != GameSceneCatalog.Boot || HasPendingSave ||
                HasProfileLoadNotice || GameFlowController.Instance.IsTransitioning || displayConfirmUntil > 0f)
                return;
            newGameOpen = true;
            newGameTutorial = true;
            newGameMessage = "";
            blockThroughFrame = Time.frameCount;
        }

        private void CloseNewGame()
        {
            newGameOpen = false;
            blockThroughFrame = Time.frameCount;
        }

        private void ConfirmNewGame()
        {
            if (!newGameOpen) return;
            try
            {
                GameFlowController.Instance.ResetProgressForNewGame(newGameTutorial);
            }
            catch (System.Exception exception) when (exception is System.IO.IOException ||
                exception is System.UnauthorizedAccessException || exception is System.Security.SecurityException ||
                exception is System.NotSupportedException || exception is System.InvalidOperationException)
            {
                newGameMessage = "새 게임을 준비하지 못했습니다. 저장 공간과 파일 접근 상태를 확인하세요.\n기존 기록 또는 복구 백업은 보관됩니다.";
                Debug.LogWarning("[New Game] " + exception.Message);
                return;
            }
            CloseNewGame();
            RefreshContinueAvailability();
            GameFlowController.Instance.EnterTown();
        }

        private void DrawResetBackupButton(Rect rect)
        {
            string directory = new PlayerProgressReset(new PlayerProfileStore().DirectoryPath).BackupDirectory;
            if (BombBrickUiTheme.Button(rect, "복구 백업 폴더 열기", System.IO.Directory.Exists(directory), false, 13))
                Application.OpenURL(new System.Uri(directory).AbsoluteUri);
        }

        private void DrawNewGame()
        {
            BombBrickUiTheme.Fill(new Rect(0, 0, 960, 540), new Color(0, 0, 0, 0.8f));
            BombBrickUiTheme.DrawPanel(new Rect(208, 128, 544, 302), true);
            Label(new Rect(236, 148, 488, 36), "새 게임 시작", 23, BombBrickUiTheme.TextPrimary);
            var toggleStyle = new GUIStyle(BombBrickUiTheme.ClearButtonStyle(17))
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(32, 4, 0, 0),
                normal = { textColor = BombBrickUiTheme.TextPrimary },
                onNormal = { textColor = BombBrickUiTheme.Lime }
            };
            newGameTutorial = UnityEngine.GUI.Toggle(new Rect(236, 210, 488, 34),
                newGameTutorial, GameLocalization.Text("튜토리얼 진행"), toggleStyle);
            BombBrickUiTheme.Fill(new Rect(236, 217, 20, 20),
                newGameTutorial ? BombBrickUiTheme.Lime : BombBrickUiTheme.TextSecondary);
            BombBrickUiTheme.Fill(new Rect(238, 219, 16, 16), BombBrickUiTheme.SurfaceRaised);
            if (newGameTutorial)
            {
                BombBrickUiTheme.Fill(new Rect(241, 226, 3, 4), BombBrickUiTheme.Lime);
                BombBrickUiTheme.Fill(new Rect(244, 229, 3, 3), BombBrickUiTheme.Lime);
                BombBrickUiTheme.Fill(new Rect(247, 223, 3, 6), BombBrickUiTheme.Lime);
            }
            Label(new Rect(236, 252, 488, 30), "해제하면 모든 튜토리얼을 건너뜁니다.",
                13, BombBrickUiTheme.TextSecondary);
            if (hasSavedGame)
                Label(new Rect(236, 292, 488, 40), "새 게임을 시작하면 현재 진행 기록이 초기화됩니다.",
                    12, BombBrickUiTheme.Orange);
            Label(new Rect(236, 334, 488, 38), newGameMessage, 11, BombBrickUiTheme.Orange);
            if (BombBrickUiTheme.Button(new Rect(236, 380, 200, 34), "취소", true, false, 14)) CloseNewGame();
            if (BombBrickUiTheme.Button(new Rect(452, 380, 272, 34), "새 게임 시작", true, true, 14)) ConfirmNewGame();
        }
        private int Choice(float y, string label, string value, int index, int count, bool enabled = true)
        {
            Label(new Rect(204, y, 204, 32), label, 14, BombBrickUiTheme.TextSecondary);
            if (BombBrickUiTheme.Button(new Rect(444, y, 32, 32), "<", enabled)) index = (index + count - 1) % count;
            Label(new Rect(480, y, 234, 32), value, 14, BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            if (BombBrickUiTheme.Button(new Rect(724, y, 32, 32), ">", enabled)) index = (index + 1) % count;
            return index;
        }

        private float VolumeRow(float y, string label, float value)
        {
            Label(new Rect(204, y, 196, 30), label, 15, BombBrickUiTheme.TextSecondary);
            if (BombBrickUiTheme.Button(new Rect(408, y, 32, 32), "-")) value -= 0.1f;
            Rect bar = new Rect(452, y + 4, 238, 24);
            for (int i = 0; i < 10; i++)
            {
                Rect cell = new Rect(bar.x + i * 24, bar.y, 20, 24);
                BombBrickUiTheme.Fill(cell, i < Mathf.RoundToInt(value * 10) ? BombBrickUiTheme.Lime : BombBrickUiTheme.SurfaceHover);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) value = (i + 1) / 10f;
            }
            if (BombBrickUiTheme.Button(new Rect(708, y, 32, 32), "+")) value += 0.1f;
            Label(new Rect(452, y + 34, 238, 20), Mathf.RoundToInt(Mathf.Clamp01(value) * 100) + "%", 11, BombBrickUiTheme.TextSecondary, TextAnchor.MiddleCenter);
            return Mathf.Clamp01(value);
        }

        private void Instruction(float y, string key, string meaning)
        {
            BombBrickUiTheme.Badge(new Rect(204, y, 166, 34), key, BombBrickUiTheme.Cyan);
            Label(new Rect(394, y, 362, 34), meaning, 13, BombBrickUiTheme.TextPrimary);
        }

        private void ApplyDisplay()
        {
            previousDisplay = settings.Copy();
            settings.width = draft.width; settings.height = draft.height; settings.fullscreen = draft.fullscreen;
            settings.vSync = draft.vSync; settings.frameRate = draft.frameRate; settings.showFps = draft.showFps;
            settings.Apply(true);
            displayConfirmUntil = Time.realtimeSinceStartup + 12f;
        }

        private void ConfirmDisplay()
        {
            displayConfirmUntil = 0f;
            previousDisplay = null;
            settings.Save();
            settingsMessage = "저장했습니다.";
        }

        private void RevertDisplay()
        {
            if (previousDisplay != null)
            {
                settings = previousDisplay;
                settings.Apply(true);
                audioPlayer.Apply(settings);
                draft = settings.Copy();
            }
            previousDisplay = null;
            displayConfirmUntil = 0f;
            settingsMessage = "이전 설정으로 돌아왔습니다.";
        }

        private void DrawConfirmation()
        {
            BombBrickUiTheme.Fill(new Rect(0, 0, 960, 540), new Color(0, 0, 0, 0.86f));
            BombBrickUiTheme.DrawPanel(new Rect(248, 142, 464, 248), true);
            Label(new Rect(272, 164, 416, 42), "게임을 종료할까요?", 21, BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            Label(new Rect(280, 224, 400, 54), "탐험 기록은 유지됩니다.\n전투 중이라면 전투 시작 시점부터 이어집니다.", 13, BombBrickUiTheme.TextSecondary, TextAnchor.MiddleCenter);
            if (BombBrickUiTheme.Button(new Rect(280, 310, 184, 44), "취소", true, false, 15)) confirmQuit = false;
            if (BombBrickUiTheme.Button(new Rect(496, 310, 184, 44), "확인", true, true, 15))
            {
                if (!SaveBeforeLeaving()) return;
                ClosePanels();
                settings.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void DrawLoading(bool startup = false)
        {
            GameFlowController flow = GameFlowController.Instance;
            float loadingProgress = startup
                ? GameResourceCache.StartupProgress
                : flow.LoadingProgress;
            Label(new Rect(80, 110, 800, 52),
                GameLocalization.Text("다음 여정을 준비하는 중"), 28,
                BombBrickUiTheme.TextPrimary, TextAnchor.MiddleCenter);
            string destination = startup ? GameLocalization.TextByKey("frontend.loading_compact", "로딩중...") :
                flow.TransitionTarget == GameSceneCatalog.ExpeditionBattle
                ? HunterRegionCatalog.Get(flow.Run.RegionId).displayName
                : flow.TransitionTarget == GameSceneCatalog.Town ? "탐험 준비"
                : flow.TransitionTarget == GameSceneCatalog.Result ? "탐험 결과" : "메인 화면";
            Label(new Rect(80, 184, 800, 34), GameLocalization.Text(destination),
                16, BombBrickUiTheme.Lime, TextAnchor.MiddleCenter);
            int filled = Mathf.FloorToInt(loadingProgress * 20f);
            for (int i = 0; i < 20; i++)
                BombBrickUiTheme.Fill(new Rect(240 + i * 24, 284, 18, 18), i < filled ? BombBrickUiTheme.Lime : BombBrickUiTheme.SurfaceHover);
            string progress = startup
                ? GameLocalization.Text("몬스터 기록과 공통 리소스를 준비하고 있습니다.  ") +
                  Mathf.RoundToInt(loadingProgress * 100f) + "%"
                : GameResourceCache.WarmupProgress < 1f
                ? GameLocalization.Text("몬스터 기록과 공통 리소스를 준비하고 있습니다.  ") +
                  Mathf.RoundToInt(GameResourceCache.WarmupProgress * 100) + "%"
                : flow.LoadingProgress >= 0.9f
                    ? GameLocalization.Text("전장과 몬스터를 준비하고 있습니다.")
                    : GameLocalization.Text("불러오는 중  ") +
                      Mathf.RoundToInt(flow.LoadingProgress * 100) + "%";
            Label(new Rect(80, 328, 800, 28), progress, 12, BombBrickUiTheme.TextSecondary, TextAnchor.MiddleCenter);
        }

        private void Label(Rect rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            int key = size * 16 + (int)anchor;
            if (!labels.TryGetValue(key, out GUIStyle style))
            {
                style = new GUIStyle(BombBrickUiTheme.TitleStyle(size, anchor)) { fontSize = size, wordWrap = true };
                labels[key] = style;
            }
            BombBrickUiTheme.Label(rect, text, style, color);
        }

        private void OnDestroy()
        {
            Application.wantsToQuit -= WantsToQuit;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (ownsSkyOccluder && skyOccluder != null) Destroy(skyOccluder);
            if (ownsTreeOccluder && treeOccluder != null) Destroy(treeOccluder);
            if (ownsMenuChundongiAtlas && menuChundongiAtlas != null) Destroy(menuChundongiAtlas);
            if (backgroundPaused) Time.timeScale = backgroundTimeScale;
            if (settings != null) AudioListener.volume = settings.masterVolume;
            if (menuOpen) Time.timeScale = previousTimeScale;
            else if (pendingSaves.Count > 0) Time.timeScale = saveTimeScale;
            if (instance == this) instance = null;
        }
    }
}
