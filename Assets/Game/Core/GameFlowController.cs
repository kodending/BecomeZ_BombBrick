using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BombBrick.Game.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameFlowController : MonoBehaviour
    {
        private static GameFlowController instance;
        private bool isTransitioning;
        private int scenePresentedFrame = -1;
        private Scene previousScene;
        private GameObject[] previousSceneRoots;
        private AudioListener transitionListener;

        public static GameFlowController Instance
        {
            get
            {
                EnsureInstance();
                return instance;
            }
        }

        public static bool HasActiveExpeditionRequest =>
            instance != null && instance.Run.ExpeditionRequested;

        public static string RequestedRegionId =>
            HasActiveExpeditionRequest
                ? instance.Run.RegionId
                : "prototype_region";

        public static ExpeditionDifficulty RequestedExpeditionDifficulty =>
            HasActiveExpeditionRequest
                ? instance.Run.SelectedExpeditionDifficulty
                : ExpeditionDifficulty.Normal;

        public GameRunSession Run { get; private set; } = new GameRunSession();
        public bool IsTransitioning => isTransitioning;
        public static bool CanPresentScene => instance == null || !instance.isTransitioning &&
            instance.scenePresentedFrame >= 0 && Time.frameCount > instance.scenePresentedFrame;
        public static float PresentationDeltaTime => !CanPresentScene || instance != null &&
            Time.frameCount == instance.scenePresentedFrame + 1 ? 0f : Time.unscaledDeltaTime;
        public string TransitionTarget { get; private set; } = "";
        public string LastTransitionError { get; private set; } = "";
        public float LoadingProgress { get; private set; }
        public event Action<string> SceneTransitionStarted;
        public event Action<string> SceneTransitionCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (instance != null)
                return;

            GameFlowController existing = FindObjectOfType<GameFlowController>();
            if (existing != null)
            {
                instance = existing;
                return;
            }

            GameObject root = new GameObject("[Game Flow]");
            instance = root.AddComponent<GameFlowController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += ActivateLoadedScene;
            if (GetComponent<GameFrontend>() == null) gameObject.AddComponent<GameFrontend>();
        }

        private void ActivateLoadedScene(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || isTransitioning && scene.name == TransitionTarget)
                scenePresentedFrame = -1;
            if (!isTransitioning || mode != LoadSceneMode.Additive || scene.name != TransitionTarget)
                return;
            // Awake can create unparented objects in the old active scene before sceneLoaded.
            foreach (var root in previousScene.GetRootGameObjects())
                if (Array.IndexOf(previousSceneRoots, root) < 0)
                    SceneManager.MoveGameObjectToScene(root, scene);
            SceneManager.SetActiveScene(scene);
            if (transitionListener != null)
            {
                transitionListener.enabled = false;
                Destroy(transitionListener);
                transitionListener = null;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= ActivateLoadedScene;
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!GameFrontend.BlocksGameplay &&
                SceneManager.GetActiveScene().name == GameSceneCatalog.ExpeditionBattle)
                Run.Tick(PresentationDeltaTime);
        }

        public void NotifySceneRendered()
        {
            // A loaded scene can still be covered by the loading overlay until its first visible repaint.
            if (!isTransitioning && scenePresentedFrame < 0) scenePresentedFrame = Time.frameCount;
        }

        public void EnterTown()
        {
            if (!GameFrontend.SaveBeforeLeaving()) return;
            LoadScene(GameSceneCatalog.Town);
        }

        public string ResetProgressForNewGame(bool playTutorial = true)
        {
            if (SceneManager.GetActiveScene().name != GameSceneCatalog.Boot || isTransitioning ||
                GameFrontend.HasPendingSave || GameFrontend.HasProfileLoadNotice)
                throw new InvalidOperationException("New games can only be created from the main menu.");
            GameDataPaths.EnsureMigrated();
            string archive = new PlayerProgressReset(new PlayerProfileStore().DirectoryPath)
                .StartNewGame(Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase"), playTutorial);
            Run = new GameRunSession();
            return archive;
        }

        public void StartChallenge(int difficulty)
        {
            if (isTransitioning || !GameFrontend.SaveBeforeLeaving())
                return;

            Run.Begin(difficulty);
            LoadScene(GameSceneCatalog.Battle);
        }

        public void FinishChallenge(ChallengeResult result)
        {
            if (isTransitioning || !GameFrontend.SaveBeforeLeaving())
                return;

            Run.Finish(result);
            LoadScene(GameSceneCatalog.Result);
        }

        public void StartExpedition(
            string regionId,
            bool resume,
            ExpeditionDifficulty difficulty = ExpeditionDifficulty.Normal)
        {
            if (isTransitioning || !GameFrontend.SaveBeforeLeaving())
                return;

            Run.BeginExpedition(regionId, resume, difficulty);
            LoadScene(GameSceneCatalog.ExpeditionBattle);
        }

        public void FinishExpedition(
            bool victory,
            int completedWave,
            int totalWaves,
            int remainingLives,
            int gold)
        {
            if (isTransitioning || !GameFrontend.SaveBeforeLeaving())
                return;

            Run.FinishExpedition(
                victory ? ChallengeResult.Cleared : ChallengeResult.Defeated,
                completedWave,
                totalWaves,
                remainingLives,
                gold);
            LoadScene(GameSceneCatalog.Result);
        }

        public void ReturnToTown()
        {
            if (isTransitioning || !GameFrontend.SaveBeforeLeaving()) return;
            Run.LeaveExpeditionFlow();
            LoadScene(GameSceneCatalog.Town);
        }

        public void ReturnToMain()
        {
            if (isTransitioning || !GameFrontend.SaveBeforeLeaving()) return;
            Run.LeaveExpeditionFlow();
            LoadScene(GameSceneCatalog.Boot);
        }

        public void ReloadCurrentScene()
        {
            if (!GameFrontend.SaveBeforeLeaving()) return;
            LoadScene(SceneManager.GetActiveScene().name, true);
        }

        private void LoadScene(string sceneName, bool reload = false)
        {
            if (GameFrontend.HasPendingSave || isTransitioning || !reload && SceneManager.GetActiveScene().name == sceneName)
                return;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                LastTransitionError = "화면을 불러오지 못했습니다. 다시 시도해 주세요.";
                Debug.LogError($"[Game Flow] Scene '{sceneName}' is not available in build settings.");
                return;
            }
            GetComponent<GameFrontend>()?.ClosePanels();
            isTransitioning = true;
            TransitionTarget = sceneName;
            LastTransitionError = "";
            LoadingProgress = 0f;
            SceneTransitionStarted?.Invoke(sceneName);
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            var priority=Application.backgroundLoadingPriority;
            bool ownsLoadingPriority=false;
            previousScene = SceneManager.GetActiveScene();
            GameObject[] previousRoots = null;
            bool activated = false;
            try
            {
                bool returningToMain = sceneName == GameSceneCatalog.Boot;
                const float resourceShare = 0.32f;
                if (!returningToMain)
                {
                    // Give the loading overlay a rendered frame before starting scene I/O.
                    yield return null;
                    yield return null;
                    // Finish the menu's background preparation before taking ownership of the loading priority.
                    yield return GameResourceCache.WarmupGameplay();
                }
                priority=Application.backgroundLoadingPriority;
                Application.backgroundLoadingPriority=ThreadPriority.Low;
                ownsLoadingPriority=true;
                if (!returningToMain)
                    yield return GameResourceCache.WarmupCommon(
                        progress => LoadingProgress = progress * resourceShare);
                AsyncOperation operation = null;
                // Single loading forces UnloadUnusedAssets and a full GC in Unity 2022.
                try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
                catch (Exception exception) { Debug.LogException(exception); }
                if (operation == null)
                {
                    LastTransitionError = "화면을 불러오지 못했습니다. 다시 시도해 주세요.";
                    isTransitioning = false;
                    yield break;
                }
                operation.allowSceneActivation = false;
                float startedAt = Time.unscaledTime;
                while (operation.progress < 0.9f || !returningToMain && Time.unscaledTime - startedAt < 0.3f)
                {
                    LoadingProgress = resourceShare +
                        operation.progress / 0.9f * (0.9f - resourceShare);
                    yield return null;
                }
                LoadingProgress = 0.9f;
                previousSceneRoots = previousScene.GetRootGameObjects();
                previousRoots = Array.FindAll(previousSceneRoots, root => root.activeSelf);
                transitionListener = gameObject.AddComponent<AudioListener>();
                foreach (var root in previousRoots) root.SetActive(false);
                operation.allowSceneActivation = true;
                while (!operation.isDone) yield return null;
                Scene destination = SceneManager.GetActiveScene();
                var battle=FindSceneController<MonsterAutoBattlePrototype>(destination);
                while(battle!=null&&battle.isActiveAndEnabled&&!battle.IsSceneReady)
                    yield return null;
                var town=FindSceneController<BombBrick.Game.Scenes.TownSceneController>(destination);
                while(town!=null&&town.isActiveAndEnabled&&!town.IsSceneReady)
                    yield return null;
                if (!ConfirmSceneInitialization(destination, previousRoots))
                {
                    if (destination.IsValid() && destination.isLoaded && destination != previousScene)
                    {
                        var discard = SceneManager.UnloadSceneAsync(destination);
                        while (discard != null && !discard.isDone) yield return null;
                    }
                    yield break;
                }
                // Keep the previous scene recoverable until destination setup has succeeded.
                activated = true;
                var unload = SceneManager.UnloadSceneAsync(previousScene);
                while (unload != null && !unload.isDone) yield return null;
                // Keep the overlay over scene Awake/Start and their resource setup.
                yield return null;
                LoadingProgress = 1f;
                if (!returningToMain) yield return new WaitForSecondsRealtime(0.12f);
                isTransitioning = false;
                SceneTransitionCompleted?.Invoke(sceneName);
            }
            finally
            {
                if (!activated) RestorePreviousScene(previousRoots);
                if (transitionListener != null) Destroy(transitionListener);
                transitionListener = null;
                previousSceneRoots = null;
                isTransitioning = false;
                if(ownsLoadingPriority)Application.backgroundLoadingPriority=priority;
            }
        }

        private static T FindSceneController<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                T controller = root.GetComponentInChildren<T>(true);
                if (controller != null) return controller;
            }
            return null;
        }

        private bool ConfirmSceneInitialization(Scene destination, GameObject[] previousRoots)
        {
            var battle = FindSceneController<MonsterAutoBattlePrototype>(destination);
            var town = FindSceneController<BombBrick.Game.Scenes.TownSceneController>(destination);
            bool valid = destination.IsValid() && destination.isLoaded && destination != previousScene;
            bool missingController = valid && (destination.name == GameSceneCatalog.ExpeditionBattle && battle == null ||
                destination.name == GameSceneCatalog.Town && town == null);
            if (valid && !missingController &&
                (battle == null || battle.IsSceneReady) && (town == null || town.IsSceneReady)) return true;
            LastTransitionError = "화면을 불러오지 못했습니다. 다시 시도해 주세요.";
            RestorePreviousScene(previousRoots);
            return false;
        }

        private void RestorePreviousScene(GameObject[] previousRoots)
        {
            scenePresentedFrame = -1;
            if (!previousScene.IsValid() || !previousScene.isLoaded) return;
            SceneManager.SetActiveScene(previousScene);
            if (previousRoots != null)
                foreach (var root in previousRoots) if (root != null) root.SetActive(true);
        }
    }
}
