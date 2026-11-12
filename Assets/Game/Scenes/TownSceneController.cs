using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;
using GUI = LocalizedGUI;

namespace BombBrick.Game.Scenes
{
    public sealed partial class TownSceneController : ProductionSceneController
    {
        private const string RegionId = HunterRegionCatalog.FirstRegionId;
        private const int ExpeditionWaveCount = 12;
        private MonsterDatabase database;
        private PlayerProfileStore profileStore;
        private PlayerProfileSave profile;
        private ExpeditionRunStore expeditionStore;
        private ExpeditionRunSave activeRun;
        private readonly List<string> encyclopediaFamilies =
            new List<string>();
        private readonly Dictionary<string, List<MonsterDefinition>>
            encyclopediaMonstersByFamily =
                new Dictionary<string, List<MonsterDefinition>>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, int> encyclopediaFoundByFamily =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> encyclopediaHabitatByMonster =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private HashSet<string> encyclopediaEncountered =
            new HashSet<string>(StringComparer.Ordinal);
        private int encyclopediaDiscoveredCount;
        private int encyclopediaTotalCount;
        private readonly List<MonsterCardHit> cardHits =
            new List<MonsterCardHit>();
        private Vector2 collectionScroll;
        private PlayerProfileSave collectionProfile;
        private long collectionSavedAt = -1;
        private List<OwnedMonsterSave> collectionMonsters;
        private string feedback = "";
        private float abandonConfirmationUntil;
        private const float AbandonConfirmationDuration = 3f;
        private const string AbandonConfirmationHint = "3초 안에 다시 누르면 진행 중인 탐험을 포기합니다.";
        private Rect collectionDropRect;
        private string pressedMonsterInstanceId = "";
        private bool pressedFromRoster;
        private int pressedRosterIndex = -1;
        private Vector2 pressedPointerPosition;
        private bool draggingMonster;
        private string tooltipMonsterInstanceId = "";
        private Rect tooltipAnchor;
        private float? hoveredRegionAffinity;
        private Rect currentTooltipRect;
        private int selectedRegionIndex;
        private int previousRegionIndex = -1;
        private int regionSlideDirection = 1;
        private float regionSlideStartedAt = -10f;
        private const float RegionSlideDuration = 0.31f;
        private bool progressUnlockNoticeOpen;
        private float progressUnlockNoticeStartedAt;
        private string progressUnlockSourceRegionId = "";
        private string progressUnlockRegionId = "";
        private ExpeditionDifficulty progressUnlockDifficulty =
            ExpeditionDifficulty.Normal;
        private ExpeditionDifficulty selectedDifficulty =
            ExpeditionDifficulty.Normal;
        private bool hunterShopOpen;
        private bool hunterShopClosing;
        private float hunterShopTransitionStartedAt = -10f;
        private const float HunterShopOpenDuration = 0.28f;
        private const float HunterShopCloseDuration = 0.21f;
        private int hunterShopTab;
        private readonly HashSet<string> hunterShopNewUnlockKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private float hunterShopUnlockStartedAt = -1f;
        private string hunterShopUnlockNotice = "";
        private bool encyclopediaOpen;
        private bool researchOpen;
        private float researchResetConfirmationUntil;
        private int encyclopediaFamilyIndex;
        private Vector2 encyclopediaFamilyScroll;
        private OwnedMonsterSave hunterBoxResult;
        private ChestRewardPresentation hunterBoxReveal;
        private string pendingRewardRunId;
        private Texture2D hunterRegionBoxTexture;
        private Texture2D hunterFamilyBoxTexture;
        private Texture2D hunterTokenTexture;
        private Texture2D hunterCommonNetTexture;
        private Texture2D hunterRareNetTexture;
        private Texture2D townBackgroundTexture;
        private readonly Dictionary<string, Texture2D> regionPreviewTextures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly List<TownLeaf> townLeaves = new List<TownLeaf>();
        private readonly System.Random townAmbientRandom =
            new System.Random(2309);
        private readonly Rect[] rosterSlotRects =
            new Rect[PlayerProfileStore.ExpeditionRosterSize];
        private readonly float[] rosterSlotImpactStartedAt =
            new float[PlayerProfileStore.ExpeditionRosterSize];
        private float nextTownLeafBurst;
        private bool hunterBoxContentsOpen;
        private bool hunterBoxContentsFamily;
        private string hunterBoxContentsTitle = "";
        private readonly List<string> hunterBoxContentsMonsterIds =
            new List<string>();
        private Vector2 hunterBoxContentsScroll;
        private float screenRevealStartedAt;
        private bool scenePresentationStarted;
        private const float MonsterDragThreshold = 12f;
        private const int GuideIntroduceCollection = 0;
        private const int GuidePlaceRoster = 1;
        private const int GuideExplainMap = 2;
        private const int GuideExplainDifficulty = 3;
        private const int GuideStartExpedition = 4;
        private const int GuideAwaitResult = 5;
        private const int GuideShowTokens = 6;
        private const int GuideOpenShop = 7;
        private const int GuideComplete = 8;
        private Rect guideRosterRect;
        private Rect guideCollectionRect;
        private Rect guideMapRect;
        private Rect guideDifficultyRect;
        private Rect guideStartRect;
        private Rect guideTokenRect;
        private Rect guideShopRect;
        private float guideStepChangedAt;

        private struct MonsterCardHit
        {
            public Rect rect;
            public string instanceId;
            public bool fromRoster;
            public int rosterIndex;
        }

        private struct TownLeaf
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
            public bool reactive;
        }

        public bool IsSceneReady { get; private set; }

        private IEnumerator Start()
        {
            screenRevealStartedAt = Time.unscaledTime;
            nextTownLeafBurst = Time.unscaledTime + 1.5f;
            for (int index = 0; index < rosterSlotImpactStartedAt.Length; index++)
                rosterSlotImpactStartedAt[index] = -10f;
            LoadTownVisuals();
            yield return null;
#if UNITY_EDITOR
            yield return GameResourceCache.WarmupEditorMethods(typeof(TownSceneController),
                typeof(PlayerProfileStore), typeof(ExpeditionRunStore), typeof(ExpeditionRunFactory),
                typeof(ExpeditionTraitProgression),typeof(MonsterTraitCatalog),
                typeof(MonsterInstanceGenerator),typeof(CombatHudIconArt),
                typeof(BombBrickUiTheme),typeof(LocalizedGUI));
#endif
            yield return ReloadTownDataRoutine(true);
            if(profile!=null&&database!=null)
                foreach(var owned in profile.monsters)
                {
                    var definition=database.Get(owned.monsterId);
                    if(definition==null)continue;
                    ExpeditionTraitProgression.OwnedTrait(definition,owned,2);
                    ExpeditionTraitProgression.OwnedTrait(definition,owned,3);
                    yield return null;
                }
            guideStepChangedAt = Time.unscaledTime;
            IsSceneReady = true;
        }

        private void Update()
        {
            if (!IsSceneReady || !GameFlowController.CanPresentScene || GameFrontend.BlocksGameplay) return;
            if (!scenePresentationStarted)
            {
                scenePresentationStarted = true;
                screenRevealStartedAt = guideStepChangedAt = Time.unscaledTime;
                nextTownLeafBurst = Time.unscaledTime + 1.5f;
                if (progressUnlockNoticeOpen) progressUnlockNoticeStartedAt = Time.unscaledTime;
            }
            UpdateTownLeaves();
            if (profile != null && profile.starterSelectionPending)
            {
                if (starterReveal == null) BeginStarterReveal();
                starterReveal?.Update();
            }
            hunterBoxReveal?.Update();
            if (hunterBoxReveal != null && hunterBoxReveal.Finished)
            {
                hunterBoxReveal = null;
                hunterBoxResult = null;
                pendingRewardRunId = null;
                BuildEncyclopediaCache();
                BeginPendingRegionReward();
            }
        }

        private void LoadTownVisuals()
        {
            townBackgroundTexture = Resources.Load<Texture2D>(
                "Environment/Town/TownExpeditionHub");
            PreparePixelTexture(townBackgroundTexture);
            regionPreviewTextures.Clear();
            foreach (HunterRegionDefinition region in HunterRegionCatalog.All)
            {
                Texture2D texture = Resources.Load<Texture2D>(
                    "Environment/RegionIllustrations/" + region.id);
                if (texture == null)
                    continue;
                PreparePixelTexture(texture);
                regionPreviewTextures[region.id] = texture;
            }
        }

        private static void PreparePixelTexture(Texture2D texture)
        {
            if (texture == null)
                return;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
        }

        private void ReloadTownData()
        {
            var loading = ReloadTownDataRoutine(false);
            try { while (loading.MoveNext()) {} }
            finally { (loading as IDisposable)?.Dispose(); }
        }

        private IEnumerator ReloadTownDataRoutine(bool acrossFrames)
        {
            database = GameResourceCache.Database;
            if (database == null)
            {
                feedback = "몬스터 데이터베이스를 불러오지 못했습니다.";
                yield break;
            }

            profileStore = new PlayerProfileStore();
            if (acrossFrames)
            {
                profile = null;
                yield return GameFrontend.LoadPlayerProfileAcrossFrames(profileStore, database, value => profile = value);
                if (profile == null) yield break;
                yield return null;
            }
            else if (!GameFrontend.TryLoadPlayerProfile(profileStore, database, out profile))
                yield break;
            expeditionStore = new ExpeditionRunStore(
                profile.profileId, profileStore.DirectoryPath);
            if (acrossFrames)
            {
                bool loaded = false;
                yield return GameFrontend.LoadExpeditionAcrossFrames(expeditionStore, profile, database,
                    value => { activeRun = value; loaded = true; });
                if (!loaded) yield break;
            }
            else if (!GameFrontend.TryLoadExpedition(expeditionStore, profile, database, out activeRun))
                yield break;
            if (acrossFrames) yield return null;
            IReadOnlyList<HunterRegionDefinition> regions =
                HunterRegionCatalog.All;
            selectedRegionIndex = activeRun == null
                ? 0
                : Mathf.Max(
                    0,
                    regions.ToList().FindIndex(region =>
                        region.id == activeRun.regionId));
            previousRegionIndex = -1;
            if (activeRun != null)
                selectedDifficulty = activeRun.difficulty;
            PrepareProgressUnlockNotice(regions);
            ReconcileFirstExpeditionGuide();
            BuildEncyclopediaCache();
            BeginPendingRegionReward();
        }

        private void BuildEncyclopediaCache()
        {
            encyclopediaFamilies.Clear();
            encyclopediaMonstersByFamily.Clear();
            encyclopediaFoundByFamily.Clear();
            encyclopediaHabitatByMonster.Clear();
            encyclopediaEncountered = new HashSet<string>(
                profile == null || profile.encounteredMonsterIds == null
                    ? Enumerable.Empty<string>()
                    : profile.encounteredMonsterIds,
                StringComparer.Ordinal);
            encyclopediaDiscoveredCount = 0;
            encyclopediaTotalCount = 0;
            if (database == null || profile == null)
                return;

            List<MonsterDefinition> enabled = database.monsters
                .Where(definition => definition != null && definition.enabled)
                .ToList();
            foreach (IGrouping<string, MonsterDefinition> family in enabled
                         .GroupBy(definition => definition.familyId)
                         .OrderBy(group => group.Min(definition =>
                             definition.serialNumber)))
            {
                List<MonsterDefinition> monsters = family
                    .OrderBy(definition => definition.serialNumber)
                    .ToList();
                encyclopediaFamilies.Add(family.Key);
                encyclopediaMonstersByFamily[family.Key] = monsters;
                encyclopediaFoundByFamily[family.Key] = monsters.Count(
                    definition => encyclopediaEncountered.Contains(
                        definition.monsterId));
            }

            foreach (HunterRegionDefinition region in HunterRegionCatalog.All)
                foreach (string monsterId in region.MonsterIds())
                    if (!encyclopediaHabitatByMonster.ContainsKey(monsterId))
                        encyclopediaHabitatByMonster[monsterId] =
                            region.displayName;

            encyclopediaTotalCount = enabled.Count;
            encyclopediaDiscoveredCount = enabled.Count(definition =>
                encyclopediaEncountered.Contains(definition.monsterId));
            encyclopediaFamilyIndex = Mathf.Clamp(
                encyclopediaFamilyIndex,
                0,
                Mathf.Max(0, encyclopediaFamilies.Count - 1));
        }

        public void PrepareForSystemMenu()
        {
            pressedMonsterInstanceId = "";
            pressedFromRoster = false;
            pressedRosterIndex = -1;
            draggingMonster = false;
        }

        public bool TryClosePreparationModal()
        {
            if (researchOpen)
            {
                researchOpen = false;
                researchResetConfirmationUntil = 0f;
                return true;
            }
            if (!encyclopediaOpen) return false;
            encyclopediaOpen = false;
            return true;
        }

        private void OnGUI()
        {
            using (new BombBrickUiLayout.GuiScope(true)) DrawSceneGui();
        }

        private void DrawSceneGui()
        {
            if(Event.current.type==EventType.Layout||!IsSceneReady)return;
            if (GameFrontend.BlocksGuiInput) return;
            cardHits.Clear();
            hoveredGrowthTrait = null;
            hoveredRegionAffinity = null;
            DrawTownBackground();
            DrawTownLeaves(false);
            if (profile == null || database == null)
            {
                DrawLoadFailure();
                return;
            }

            Rect viewport = GameFrontend.FitPanel(
                BombBrickUiLayout.CurrentSafeArea(),
                new Vector2(BombBrickUiLayout.ReferenceWidth, BombBrickUiLayout.ReferenceHeight));
            const float margin = 24f;
            viewport = new Rect(viewport.x + margin, viewport.y + margin,
                viewport.width - margin * 2f, viewport.height - margin * 2f);
            if (profile.starterSelectionPending)
            {
                DrawStarterSelection(viewport);
                return;
            }
            float reveal = BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(
                scenePresentationStarted ? (Time.unscaledTime - screenRevealStartedAt) / 0.28f : 0f));
            viewport.y += (1f - reveal) * 10f;
            DrawHeader(new Rect(viewport.x, viewport.y, viewport.width, 54f));
            if (!string.IsNullOrEmpty(exchangeMonsterId))
            {
                DrawMonsterExchange();
                return;
            }
            if (encyclopediaOpen || researchOpen || hunterShopOpen ||
                hunterBoxResult != null)
            {
                if (encyclopediaOpen)
                    DrawEncyclopediaModal();
                if (researchOpen)
                    DrawResearchModal();
                if (hunterShopOpen)
                    DrawHunterShopModal();
                if (hunterBoxResult != null)
                    DrawHunterBoxReveal();
                DrawTownLeaves(true);
                DrawGrowthTraitHover();
                return;
            }

            float upperY = viewport.y + 62f;
            float upperHeight = Mathf.Clamp(viewport.height * 0.48f, 360f, 480f);
            float regionWidth = Mathf.Clamp(viewport.width * 0.29f, 330f, 440f);
            Rect rosterPanel = new Rect(
                viewport.x,
                upperY,
                viewport.width - regionWidth - 8f,
                upperHeight);
            Rect regionPanel = new Rect(
                rosterPanel.xMax + 8f,
                upperY,
                regionWidth,
                upperHeight);
            Rect collectionPanel = new Rect(
                viewport.x,
                rosterPanel.yMax + 8f,
                viewport.width,
                Mathf.Max(180f, viewport.yMax - rosterPanel.yMax - 8f));
            guideRosterRect = rosterPanel;
            guideCollectionRect = collectionPanel;

            bool previousEnabled = GUI.enabled;
            if (progressUnlockNoticeOpen)
                GUI.enabled = false;
            DrawRosterPanel(rosterPanel);
            DrawRegionPanel(regionPanel);
            DrawCollectionPanel(collectionPanel);
            GUI.enabled = previousEnabled;
            if (!progressUnlockNoticeOpen)
                ProcessMonsterPointerInput();
            DrawTownLeaves(true);
            if (progressUnlockNoticeOpen)
            {
                DrawProgressUnlockNotice();
                return;
            }
            DrawMonsterTooltip();
            DrawGrowthTraitHover();
            DrawFirstExpeditionGuide(viewport);
            DrawDraggedMonsterGhost();
            DrawRegionAffinityHover();
        }

        private void DrawTownBackground()
        {
            Rect screen = new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height);
            if (townBackgroundTexture != null)
                GUI.DrawTexture(
                    screen, townBackgroundTexture, ScaleMode.ScaleAndCrop, true);
            else
                BombBrickUiTheme.Fill(screen, BombBrickUiTheme.Backdrop);
            BombBrickUiTheme.Fill(
                screen, new Color(0.004f, 0.012f, 0.009f, 0.46f));
            DrawTownSkyLayer();
            DrawTownFoliageLayer();
        }

        private static void DrawTownSkyLayer()
        {
            float time = Time.unscaledTime;
            Rect sky = new Rect(
                BombBrickUiLayout.Width * 0.18f, 0f,
                BombBrickUiLayout.Width * 0.64f, BombBrickUiLayout.Height * 0.31f);
            GUI.BeginGroup(sky);
            for (int index = 0; index < 4; index++)
            {
                float travel = Mathf.Repeat(
                    index * sky.width * 0.31f - time * (7f + index),
                    sky.width + 180f) - 110f;
                float y = 18f + index % 3 * sky.height * 0.18f;
                DrawTownPixelCloud(
                    new Vector2(Mathf.Floor(travel), Mathf.Floor(y)),
                    index % 2 == 0 ? 1f : 0.72f);
            }
            DrawTownBirdFlock(sky.width, sky.height, time);
            GUI.EndGroup();
        }

        private static void DrawTownPixelCloud(Vector2 position, float scale)
        {
            float unit = Mathf.Max(2f, Mathf.Round(5f * scale));
            Color color = new Color(0.91f, 0.95f, 0.78f, 0.18f);
            BombBrickUiTheme.Fill(new Rect(
                position.x + unit * 2f, position.y, unit * 5f, unit * 2f), color);
            BombBrickUiTheme.Fill(new Rect(
                position.x + unit, position.y + unit, unit * 8f, unit * 2f), color);
            BombBrickUiTheme.Fill(new Rect(
                position.x, position.y + unit * 2f, unit * 11f, unit * 2f), color);
            BombBrickUiTheme.Fill(new Rect(
                position.x + unit * 3f, position.y - unit, unit * 2f, unit), color);
        }

        private static void DrawTownBirdFlock(
            float width, float height, float time)
        {
            float travel = Mathf.Repeat(time * 23f, width + 150f) - 90f;
            for (int index = 0; index < 5; index++)
            {
                float x = Mathf.Floor(travel - index * 13f);
                float y = Mathf.Floor(
                    height * 0.61f + index * 5f +
                    Mathf.Sin(time * 1.8f + index) * 4f);
                bool wingsUp = ((int)(time * 5f) + index) % 2 == 0;
                Color color = new Color(0.10f, 0.18f, 0.16f, 0.68f);
                BombBrickUiTheme.Fill(
                    new Rect(x + 4f, y + 2f, 3f, 2f), color);
                BombBrickUiTheme.Fill(
                    wingsUp
                        ? new Rect(x, y, 4f, 2f)
                        : new Rect(x + 1f, y + 4f, 3f, 2f),
                    color);
                BombBrickUiTheme.Fill(
                    wingsUp
                        ? new Rect(x + 7f, y, 4f, 2f)
                        : new Rect(x + 7f, y + 4f, 3f, 2f),
                    color);
            }
        }

        private static void DrawTownFoliageLayer()
        {
            float time = Time.unscaledTime;
            DrawTownFoliageCluster(
                new Vector2(BombBrickUiLayout.Width * 0.035f, BombBrickUiLayout.Height * 0.16f),
                1.1f, time, 0f);
            DrawTownFoliageCluster(
                new Vector2(BombBrickUiLayout.Width * 0.92f, BombBrickUiLayout.Height * 0.12f),
                1.35f, time, 1.7f);
        }

        private static void DrawTownFoliageCluster(
            Vector2 center, float scale, float time, float phase)
        {
            float sway = Mathf.Round(Mathf.Sin(time * 0.72f + phase) * 3f);
            float unit = Mathf.Max(4f, Mathf.Round(7f * scale));
            Color shade = new Color(0.06f, 0.20f, 0.08f, 0.26f);
            Color light = new Color(0.36f, 0.55f, 0.13f, 0.22f);
            BombBrickUiTheme.Fill(new Rect(
                center.x - unit * 4f + sway, center.y,
                unit * 8f, unit * 3f), shade);
            BombBrickUiTheme.Fill(new Rect(
                center.x - unit * 2f + sway, center.y - unit * 2f,
                unit * 5f, unit * 3f), shade);
            BombBrickUiTheme.Fill(new Rect(
                center.x - unit * 3f + sway * 1.4f, center.y - unit,
                unit * 3f, unit), light);
            BombBrickUiTheme.Fill(new Rect(
                center.x + unit + sway * 1.4f, center.y + unit,
                unit * 2f, unit), light);
        }

        private void UpdateTownLeaves()
        {
            float now = Time.unscaledTime;
            for (int index = townLeaves.Count - 1; index >= 0; index--)
                if (now - townLeaves[index].born >= townLeaves[index].lifetime)
                    townLeaves.RemoveAt(index);

            if (now < nextTownLeafBurst)
                return;
            bool fromRight = townAmbientRandom.Next(2) == 0;
            SpawnTownLeafBurst(
                new Rect(
                    BombBrickUiLayout.Width * (fromRight ? 0.84f : 0.03f),
                    BombBrickUiLayout.Height * 0.08f,
                    BombBrickUiLayout.Width * 0.12f,
                    BombBrickUiLayout.Height * 0.10f),
                3 + townAmbientRandom.Next(4), false);
            nextTownLeafBurst = now + 3.2f +
                (float)townAmbientRandom.NextDouble() * 3.4f;
        }

        private void TriggerTownInteractionLeaves(Rect area, int count)
        {
            Rect spread = new Rect(
                area.x - area.width * 0.12f,
                area.y - area.height * 0.20f,
                area.width * 1.24f,
                area.height * 1.40f);
            SpawnTownLeafBurst(spread, count, true);
        }

        private void SpawnTownLeafBurst(
            Rect area, int count, bool reactive)
        {
            float now = Time.unscaledTime;
            for (int index = 0; index < count; index++)
            {
                float colorMix = (float)townAmbientRandom.NextDouble();
                townLeaves.Add(new TownLeaf
                {
                    born = now + index * (reactive ? 0.025f : 0.10f),
                    lifetime = (reactive ? 1.8f : 4.8f) +
                        (float)townAmbientRandom.NextDouble() * 1.7f,
                    x = area.x +
                        (float)townAmbientRandom.NextDouble() * area.width,
                    y = area.y +
                        (float)townAmbientRandom.NextDouble() * area.height,
                    speedX = reactive
                        ? -80f + (float)townAmbientRandom.NextDouble() * 160f
                        : -12f + (float)townAmbientRandom.NextDouble() * 24f,
                    speedY = reactive
                        ? -72f + (float)townAmbientRandom.NextDouble() * 64f
                        : 28f + (float)townAmbientRandom.NextDouble() * 22f,
                    wobble = 6f +
                        (float)townAmbientRandom.NextDouble() * 13f,
                    phase = (float)townAmbientRandom.NextDouble() *
                        Mathf.PI * 2f,
                    size = 3f +
                        (float)townAmbientRandom.NextDouble() * 2f,
                    color = Color.Lerp(
                        new Color(0.22f, 0.48f, 0.10f, 0.88f),
                        new Color(0.78f, 0.68f, 0.16f, 0.92f),
                        colorMix),
                    reactive = reactive
                });
            }
        }

        private void DrawTownLeaves(bool reactive)
        {
            float now = Time.unscaledTime;
            foreach (TownLeaf leaf in townLeaves)
            {
                if (leaf.reactive != reactive)
                    continue;
                float age = now - leaf.born;
                if (age < 0f || age >= leaf.lifetime)
                    continue;
                float fade = Mathf.Clamp01(Mathf.Min(
                    age * 3f, (leaf.lifetime - age) * 1.7f));
                float x = leaf.x + leaf.speedX * age +
                    Mathf.Sin(age * 4f + leaf.phase) * leaf.wobble;
                float y = leaf.y + leaf.speedY * age + age * age * 12f;
                Color color = leaf.color;
                color.a *= fade;
                bool right = Mathf.Sin(age * 6f + leaf.phase) >= 0f;
                BombBrickUiTheme.Fill(new Rect(
                    Mathf.Round(x), Mathf.Round(y),
                    leaf.size, leaf.size * 2f), color);
                BombBrickUiTheme.Fill(new Rect(
                    Mathf.Round(x + (right ? leaf.size : -leaf.size)),
                    Mathf.Round(y + leaf.size),
                    leaf.size, leaf.size), color);
            }
        }

        private static void DrawTownPanel(Rect rect, bool raised)
        {
            BombBrickUiTheme.Fill(
                new Rect(rect.x + 4f, rect.y + 4f, rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.48f));
            BombBrickUiTheme.Fill(
                rect,
                raised
                    ? new Color(0.006f, 0.012f, 0.008f, 0.84f)
                    : new Color(0.006f, 0.012f, 0.008f, 0.72f));
            BombBrickUiTheme.Frame(rect, BombBrickUiTheme.Border, 1f);
        }

        private void DrawRegionPreview(
            Rect rect,
            HunterRegionDefinition region,
            bool unlocked,
            Color accent)
        {
            Rect local = new Rect(0f, 0f, rect.width, rect.height);
            float progress = RegionSlideProgress();
            bool sliding = activeRun == null && previousRegionIndex >= 0 &&
                progress < 1f;
            GUI.BeginGroup(rect);
            if (sliding)
            {
                IReadOnlyList<HunterRegionDefinition> regions =
                    HunterRegionCatalog.All;
                HunterRegionDefinition previous = regions[Mathf.Clamp(
                    previousRegionIndex, 0, regions.Count - 1)];
                DrawRegionTexture(
                    new Rect(-regionSlideDirection * progress * rect.width,
                        0f, rect.width, rect.height),
                    previous);
                DrawRegionTexture(
                    new Rect(regionSlideDirection * (1f - progress) * rect.width,
                        0f, rect.width, rect.height),
                    region);
            }
            else
            {
                DrawRegionTexture(local, region);
            }
            BombBrickUiTheme.Fill(
                local, new Color(0f, 0f, 0f, unlocked ? 0.08f : 0.58f));
            GUI.EndGroup();
            BombBrickUiTheme.Frame(rect, accent, 2f);
        }

        private void DrawRegionTexture(
            Rect rect, HunterRegionDefinition region)
        {
            if (region != null && regionPreviewTextures.TryGetValue(
                    region.id, out Texture2D texture) && texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop, true);
            else
                BombBrickUiTheme.Fill(rect, BombBrickUiTheme.Surface);
        }

        private float RegionSlideProgress()
        {
            return previousRegionIndex < 0
                ? 1f
                : BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(
                    (Time.unscaledTime - regionSlideStartedAt) /
                    RegionSlideDuration));
        }

        private void BeginRegionSlide(int fromIndex, int direction)
        {
            GameAudio.Play(GameAudio.Cue.SlideClose);
            previousRegionIndex = fromIndex;
            regionSlideDirection = direction;
            regionSlideStartedAt = Time.unscaledTime;
        }

        private void PrepareProgressUnlockNotice(
            IReadOnlyList<HunterRegionDefinition> regions)
        {
            GameRunSession run = GameFlowController.Instance.Run;
            if (run == null || !run.HasExpeditionProgressUnlockNotice)
                return;

            progressUnlockNoticeOpen = true;
            progressUnlockNoticeStartedAt = Time.unscaledTime;
            progressUnlockSourceRegionId = run.ProgressUnlockSourceRegionId;
            progressUnlockRegionId = run.ProgressUnlockRegionId;
            progressUnlockDifficulty = run.ProgressUnlockDifficulty;

            int sourceIndex = regions.ToList().FindIndex(region =>
                region.id == progressUnlockSourceRegionId);
            int unlockedIndex = regions.ToList().FindIndex(region =>
                region.id == progressUnlockRegionId);
            if (unlockedIndex >= 0)
            {
                int fromIndex = sourceIndex >= 0 ? sourceIndex : selectedRegionIndex;
                selectedRegionIndex = unlockedIndex;
                selectedDifficulty = ExpeditionDifficulty.Beginner;
                if (fromIndex != unlockedIndex)
                    BeginRegionSlide(fromIndex,
                        unlockedIndex > fromIndex ? 1 : -1);
            }
            else if (sourceIndex >= 0)
            {
                selectedRegionIndex = sourceIndex;
                selectedDifficulty = progressUnlockDifficulty;
            }
        }

        private void DrawProgressUnlockNotice()
        {
            BombBrickUiTheme.DrawModalBackdrop();
            bool hasNewRegion = !string.IsNullOrWhiteSpace(
                progressUnlockRegionId);
            HunterRegionDefinition source = HunterRegionCatalog.Get(
                progressUnlockSourceRegionId);
            HunterRegionDefinition unlocked = hasNewRegion
                ? HunterRegionCatalog.Get(progressUnlockRegionId)
                : null;
            string sourceName = GameLocalization.Text(source.displayName);
            string difficultyName = GameLocalization.Text(
                ExpeditionDifficultyRules.DisplayName(
                    progressUnlockDifficulty));
            string unlockedName = unlocked == null ? "" :
                GameLocalization.Text(unlocked.displayName);
            string title = hasNewRegion
                ? GameLocalization.TextByKey(
                    "town.progress_unlock.region_title",
                    "새로운 탐험지 해금!")
                : GameLocalization.TextByKey(
                    "town.progress_unlock.difficulty_title",
                    "새 난이도 해금!");
            string message = hasNewRegion
                ? GameLocalization.Format(
                    "town.progress_unlock.region_and_difficulty",
                    "새 탐험지 · {0}\n{1} · {2} 난이도 선택 가능",
                    unlockedName, sourceName, difficultyName)
                : GameLocalization.Format(
                    "town.progress_unlock.difficulty_only",
                    "{0} · {1} 난이도 선택 가능",
                    sourceName, difficultyName);

            Rect panel = new Rect(
                BombBrickUiLayout.Width * 0.5f - 280f,
                BombBrickUiLayout.Height * 0.5f - 142f,
                560f,
                284f);
            float reveal = BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(
                scenePresentationStarted ? (Time.unscaledTime - progressUnlockNoticeStartedAt) / 0.32f : 0f));
            panel.y += (1f - reveal) * 18f;
            BombBrickUiTheme.DrawPanel(panel, true);
            BombBrickUiTheme.Fill(
                new Rect(panel.x, panel.y, panel.width, 5f),
                BombBrickUiTheme.Lime);
            GUI.Label(
                new Rect(panel.x + 24f, panel.y + 34f,
                    panel.width - 48f, 42f),
                title,
                BombBrickUiTheme.DisplayStyle(27));
            GUI.Label(
                new Rect(panel.x + 38f, panel.y + 92f,
                    panel.width - 76f, 76f),
                message,
                BombBrickUiTheme.BodyStyle(
                    15, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(panel.x + 38f, panel.y + 170f,
                    panel.width - 76f, 22f),
                GameLocalization.TextByKey(
                    "town.progress_unlock.hint",
                    "맵 선택에서 난이도를 자유롭게 변경할 수 있습니다."),
                BombBrickUiTheme.CaptionStyle(
                    11, TextAnchor.MiddleCenter));
            if (BombBrickUiTheme.Button(
                new Rect(panel.center.x - 145f,
                    panel.yMax - 64f, 290f, 42f),
                hasNewRegion
                    ? GameLocalization.TextByKey(
                        "town.progress_unlock.view_region",
                        "새 탐험지 확인")
                    : GameLocalization.TextByKey(
                        "common.confirm", "확인"),
                true,
                true))
            {
                progressUnlockNoticeOpen = false;
                GameFlowController.Instance.Run
                    .AcknowledgeExpeditionProgressUnlock();
            }
        }

        private void DrawHeader(Rect rect)
        {
            GUI.Label(
                new Rect(rect.x, rect.y + 4f, 220f, 40f),
                "탐험 준비",
                BombBrickUiTheme.TitleStyle(26));
            DrawPlayerProgressSummary(new Rect(
                rect.x + 230f,
                rect.y + 5f,
                Mathf.Max(0f, rect.width * 0.5f - 318f),
                38f));
            guideTokenRect = new Rect(
                rect.xMax - 610f, rect.y + 9f, 120f, 28f);
            BombBrickUiTheme.Badge(
                guideTokenRect,
                "증표  " + profile.hunterTokens,
                BombBrickUiTheme.Orange);
            bool guideComplete =
                profile.firstExpeditionGuideStep >= GuideComplete;
            Rect researchButton = new Rect(
                rect.xMax - 480f, rect.y + 6f, 132f, 36f);
            if (BombBrickUiTheme.Button(
                researchButton,
                GameLocalization.TextByKey("research.title", "연구"),
                !encyclopediaOpen && !researchOpen && !hunterShopOpen &&
                hunterBoxResult == null && guideComplete))
            {
                researchOpen = true;
                tooltipMonsterInstanceId = "";
                draggingMonster = false;
            }
            Rect encyclopediaButton = new Rect(
                rect.xMax - 338f, rect.y + 6f, 132f, 36f);
            if (BombBrickUiTheme.Button(
                encyclopediaButton,
                "몬스터 도감",
                !encyclopediaOpen && !researchOpen && !hunterShopOpen &&
                hunterBoxResult == null && guideComplete))
            {
                encyclopediaOpen = true;
                tooltipMonsterInstanceId = "";
                draggingMonster = false;
            }
            guideShopRect = new Rect(
                rect.xMax - 196f, rect.y + 6f, 196f, 36f);
            bool guideAllowsShop =
                profile.firstExpeditionGuideStep >= GuideOpenShop;
            if (BombBrickUiTheme.Button(
                guideShopRect,
                "증표 상점",
                activeRun == null && !encyclopediaOpen && !researchOpen &&
                !hunterShopOpen &&
                hunterBoxResult == null && guideAllowsShop,
                true))
            {
                OpenHunterShop();
            }
            BombBrickUiTheme.Fill(
                new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                new Color(
                    BombBrickUiTheme.Cyan.r,
                    BombBrickUiTheme.Cyan.g,
                    BombBrickUiTheme.Cyan.b,
                    0.45f));
        }

        private void DrawPlayerProgressSummary(Rect rect)
        {
            if (profile == null || rect.width < 250f)
                return;
            int level = PermanentResearchSystem.Level(profile);
            int into = PermanentResearchSystem.ExperienceIntoLevel(profile);
            int needed = PermanentResearchSystem.ExperienceToNextLevel(profile);
            int points = PermanentResearchSystem.AvailablePoints(profile);
            bool maximum = level >= PermanentResearchSystem.MaximumLevel;

            BombBrickUiTheme.Fill(rect,
                new Color(0.012f, 0.020f, 0.014f, 0.92f));
            BombBrickUiTheme.Frame(rect,
                new Color(BombBrickUiTheme.Lime.r,
                    BombBrickUiTheme.Lime.g,
                    BombBrickUiTheme.Lime.b, 0.48f), 1f);

            float levelWidth = Mathf.Clamp(rect.width * 0.18f, 68f, 96f);
            float pointsWidth = Mathf.Clamp(rect.width * 0.25f, 110f, 148f);
            Rect levelRect = new Rect(
                rect.x + 8f, rect.y + 5f, levelWidth - 8f, rect.height - 10f);
            GUI.Label(levelRect,
                GameLocalization.Format(
                    "town.player_level", "LV. {0}", level),
                BombBrickUiTheme.TitleStyle(15, TextAnchor.MiddleCenter));

            Rect pointsRect = new Rect(
                rect.xMax - pointsWidth, rect.y + 5f,
                pointsWidth - 8f, rect.height - 10f);
            GUI.Label(pointsRect,
                GameLocalization.Format(
                    "town.research_points_short", "연구 포인트 {0}", points),
                BombBrickUiTheme.CaptionStyle(11, TextAnchor.MiddleCenter));

            Rect experienceRect = new Rect(
                rect.x + levelWidth + 2f, rect.y + 5f,
                rect.width - levelWidth - pointsWidth - 6f,
                rect.height - 10f);
            string experience = maximum
                ? GameLocalization.TextByKey(
                    "town.player_max_level", "최대 레벨")
                : GameLocalization.Format(
                    "town.player_experience", "EXP {0} / {1}", into, needed);
            GUI.Label(
                new Rect(experienceRect.x, experienceRect.y,
                    experienceRect.width, 15f),
                experience,
                BombBrickUiTheme.CaptionStyle(10, TextAnchor.MiddleCenter));
            Rect bar = new Rect(
                experienceRect.x + 4f,
                experienceRect.yMax - 9f,
                experienceRect.width - 8f,
                7f);
            BombBrickUiTheme.Fill(bar, BombBrickUiTheme.Surface);
            float ratio = maximum ? 1f : needed <= 0
                ? 0f
                : Mathf.Clamp01((float)into / needed);
            BombBrickUiTheme.Fill(
                new Rect(bar.x, bar.y, bar.width * ratio, bar.height),
                BombBrickUiTheme.Lime);
            BombBrickUiTheme.Frame(bar, BombBrickUiTheme.Border, 1f);
        }

        private void DrawRosterPanel(Rect panel)
        {
            DrawTownPanel(panel, true);
            int rosterCount =
                profile.expeditionPreset.RosterCount;
            GUI.Label(
                new Rect(panel.x + 22f, panel.y + 15f, panel.width - 44f, 24f),
                "탐험덱  " + rosterCount + " / " +
                PlayerProfileStore.ExpeditionRosterSize,
                BombBrickUiTheme.TitleStyle(18));
            GUI.Label(
                new Rect(panel.x + 22f, panel.y + 40f, panel.width - 44f, 20f),
                activeRun == null
                    ? "드래그하여 편성"
                    : "탐험 진행 중 · 편성 잠금",
                BombBrickUiTheme.CaptionStyle(11));

            List<string> roster = profile.expeditionPreset.rosterInstanceIds;
            float gap = 10f;
            float cardsTop = panel.y + 72f;
            float cardWidth = (panel.width - 44f - gap * 3f) / 4f;
            float cardHeight = (panel.height - 94f - gap) * 0.5f;
            for (int index = 0; index < PlayerProfileStore.ExpeditionRosterSize; index++)
            {
                int row = index / 4;
                int column = index % 4;
                Rect card = new Rect(
                    panel.x + 22f + column * (cardWidth + gap),
                    cardsTop + row * (cardHeight + gap),
                    cardWidth,
                    cardHeight);
                rosterSlotRects[index] = card;
                OwnedMonsterSave monster = index < roster.Count
                    ? FindOwned(roster[index])
                    : null;
                DrawRosterCard(card, monster, index);
            }
        }

        private void DrawRosterCard(
            Rect card,
            OwnedMonsterSave owned,
            int rosterIndex)
        {
            card = AnimateRosterSlotImpact(card, rosterIndex);
            bool hovered = card.Contains(Event.current.mousePosition);
            bool dragged = draggingMonster && owned != null &&
                pressedMonsterInstanceId == owned.instanceId;
            BombBrickUiTheme.DrawCard(
                card,
                dragged ? BombBrickUiTheme.Lime : BombBrickUiTheme.Cyan,
                dragged,
                hovered);

            Rect portrait = new Rect(
                card.x + 10f,
                card.y + 13f,
                Mathf.Min(72f, card.height - 26f),
                Mathf.Min(72f, card.height - 26f));
            DrawMonsterPortrait(portrait, owned);
            if (owned != null)
            {
                DrawMonsterRegionAffinity(portrait, owned);
                MonsterDefinition definition = database.Get(owned.monsterId);
                GUI.Label(
                    new Rect(portrait.xMax + 10f, card.y + 15f,
                        card.width - portrait.width - 30f, 18f),
                    "#" + (definition == null ? "---" : definition.serialNumber),
                    BombBrickUiTheme.CaptionStyle(10));
                GUI.Label(
                    new Rect(portrait.xMax + 10f, card.y + 34f,
                        card.width - portrait.width - 30f, 26f),
                    MonsterName(owned),
                    BombBrickUiTheme.TitleStyle(15));
                GUI.Label(
                    new Rect(portrait.xMax + 10f, card.y + 61f,
                        card.width - portrait.width - 30f, 18f),
                    MonsterInstanceGenerator.PersonalityDisplayName(
                        owned.personalityId),
                    BombBrickUiTheme.CaptionStyle(10));
                DrawSpeciesProgression(new Rect(card.x + 12f, card.y + 96f,
                    card.width - 24f, 48f), definition, owned);
            }
            else
            {
                GUI.Label(
                    card,
                    "빈 슬롯",
                    BombBrickUiTheme.CaptionStyle(
                        12, TextAnchor.MiddleCenter));
            }

            BombBrickUiTheme.Badge(
                new Rect(card.xMax - 34f, card.y + 10f, 24f, 20f),
                (rosterIndex + 1).ToString(),
                dragged ? BombBrickUiTheme.Lime : BombBrickUiTheme.Cyan);
            cardHits.Add(new MonsterCardHit
            {
                rect = card,
                instanceId = owned == null ? "" : owned.instanceId,
                fromRoster = true,
                rosterIndex = rosterIndex
            });
        }

        private Rect AnimateRosterSlotImpact(Rect card, int rosterIndex)
        {
            float age = Time.unscaledTime -
                rosterSlotImpactStartedAt[rosterIndex];
            if (age < 0f || age >= 0.52f)
                return card;

            if (age < 0.11f)
            {
                float drop = BombBrickUiTheme.EaseOutCubic(age / 0.11f);
                card.y += Mathf.Lerp(-13f, 3f, drop);
                return card;
            }

            float fade = 1f - (age - 0.11f) / 0.41f;
            float strength = 5f * fade;
            card.x += Mathf.Round(Mathf.Sin(age * 72f) * strength);
            card.y += Mathf.Round(Mathf.Sin(age * 43f + 1.1f) * strength * 0.42f);
            return card;
        }

        private void DrawRegionPanel(Rect panel)
        {
            DrawTownPanel(panel, true);
            bool guideAllowsMapInput =
                profile.firstExpeditionGuideStep >= GuideStartExpedition;
            IReadOnlyList<HunterRegionDefinition> regions =
                HunterRegionCatalog.All;
            selectedRegionIndex = Mathf.Clamp(
                selectedRegionIndex, 0, regions.Count - 1);
            HunterRegionDefinition selectedRegion = activeRun == null
                ? regions[selectedRegionIndex]
                : HunterRegionCatalog.Get(activeRun.regionId);
            bool regionUnlocked =
                profile.unlockedRegionIds.Contains(selectedRegion.id);
            Color accent = activeRun == null
                ? BombBrickUiTheme.Orange
                : BombBrickUiTheme.Lime;
            BombBrickUiTheme.Fill(
                new Rect(panel.x, panel.y, panel.width, 4f), accent);
            float previewHeight = activeRun == null
                ? Mathf.Clamp(panel.height - 270f, 96f, 170f)
                : Mathf.Clamp(panel.height * 0.30f, 108f, 145f);
            Rect preview = new Rect(
                panel.x + 14f, panel.y + 14f,
                panel.width - 28f, previewHeight);
            DrawRegionPreview(preview, selectedRegion, regionUnlocked, accent);
            if (activeRun == null)
            {
                Rect navigation = new Rect(
                    panel.x + 24f, panel.yMax - 128f,
                    panel.width - 48f, 56f);
                selectedRegion = DrawRegionNavigation(
                    navigation, regions, guideAllowsMapInput);
                regionUnlocked =
                    profile.unlockedRegionIds.Contains(selectedRegion.id);
                ExpeditionDifficulty highestUnlocked =
                    ExpeditionDifficultyProgress.HighestUnlocked(
                        profile, selectedRegion.id);
                if (selectedDifficulty > highestUnlocked)
                    selectedDifficulty = highestUnlocked;
            }
            else
            {
                BombBrickUiTheme.Fill(
                    new Rect(preview.x, preview.y, preview.width, 54f),
                    new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(
                    new Rect(preview.x + 18f, preview.y + 4f,
                        preview.width - 36f, 27f),
                    selectedRegion.displayName,
                    BombBrickUiTheme.TitleStyle(
                        18, TextAnchor.MiddleCenter));
                GUI.Label(
                    new Rect(preview.x + 18f, preview.y + 30f,
                        preview.width - 36f, 18f),
                    GameLocalization.Text(
                        ExpeditionDifficultyRules.DisplayName(activeRun.difficulty)) +
                    " · WAVE " + activeRun.wave,
                    BombBrickUiTheme.CaptionStyle(
                        9, TextAnchor.MiddleCenter));
            }
            guideMapRect = preview;

            Rect summary = new Rect(
                panel.x + 24f,
                preview.yMax + 8f,
                panel.width - 48f,
                86f);
            guideDifficultyRect = summary;
            BombBrickUiTheme.Fill(summary, BombBrickUiTheme.Surface);
            BombBrickUiTheme.Frame(summary, BombBrickUiTheme.Border, 1f);
            if (activeRun == null)
            {
                DrawDifficultySelector(
                    summary, selectedRegion.id, guideAllowsMapInput);
            }
            else
            {
                DrawRunStat(summary, 0, "현재", "WAVE " + activeRun.wave);
                DrawRunStat(
                    summary, 1, "목숨",
                    activeRun.lives + " / " + activeRun.maximumLives);
                DrawRunStat(
                    summary, 2, "골드",
                    activeRun.expeditionCurrency.ToString());
            }

            bool transitioning = GameFlowController.Instance.IsTransitioning;
            if (activeRun == null)
            {
                int rosterCount =
                    profile.expeditionPreset.RosterCount;
                bool rosterReady =
                    rosterCount == PlayerProfileStore.ExpeditionRosterSize &&
                    regionUnlocked;
                bool guideAllowsStart =
                    profile.firstExpeditionGuideStep == GuideStartExpedition ||
                    profile.firstExpeditionGuideStep >= GuideComplete;
                guideStartRect = new Rect(
                    panel.x + 24f, panel.yMax - 62f,
                    panel.width - 48f, 42f);
                if (BombBrickUiTheme.Button(
                    guideStartRect,
                    !regionUnlocked
                        ? "잠긴 지역"
                        : rosterReady && guideAllowsStart
                        ? "새 탐험 시작"
                        : rosterReady
                        ? "튜토리얼 안내 확인"
                        : "탐험덱 편성 필요  " + rosterCount + " / " +
                          PlayerProfileStore.ExpeditionRosterSize,
                    !transitioning && rosterReady && guideAllowsStart,
                    true))
                {
                    if (!HasCompletedFirstBattleTutorial(profile))
                        SetFirstExpeditionGuideStep(GuideAwaitResult);
                    GameFlowController.Instance.StartExpedition(
                        selectedRegion.id, false, selectedDifficulty);
                }
            }
            else
            {
                if (BombBrickUiTheme.Button(
                    new Rect(panel.x + 24f, panel.yMax - 108f,
                        panel.width - 48f, 42f),
                    "탐험 이어하기",
                    !transitioning,
                    true))
                {
                    GameFlowController.Instance.StartExpedition(
                        activeRun.regionId, true, activeRun.difficulty);
                }

                float now = Time.unscaledTime;
                bool confirming = now < abandonConfirmationUntil;
                if (!confirming && abandonConfirmationUntil > 0f)
                {
                    abandonConfirmationUntil = 0f;
                    if (feedback == AbandonConfirmationHint) feedback = "";
                }
                Rect abandonButton = BombBrickUiTheme.PixelRect(new Rect(
                    panel.x + 24f, panel.yMax - 58f, panel.width - 48f, 34f));
                bool abandonPressed = BombBrickUiTheme.Button(
                    abandonButton, confirming ? "" : "탐험 포기", !transitioning);
                if (confirming)
                {
                    float progress = 1f - (abandonConfirmationUntil - now) /
                        AbandonConfirmationDuration;
                    if (progress > 0f)
                        BombBrickUiTheme.Fill(new Rect(abandonButton.x + 4f, abandonButton.y + 4f,
                            (abandonButton.width - 8f) * progress, abandonButton.height - 8f),
                            Color.Lerp(BombBrickUiTheme.Surface, BombBrickUiTheme.Red, 0.38f));
                    GUI.Label(abandonButton, "다시 눌러 탐험 포기",
                        BombBrickUiTheme.TitleStyle(13, TextAnchor.MiddleCenter));
                }
                if (abandonPressed)
                {
                    if (!confirming)
                    {
                        abandonConfirmationUntil = now + AbandonConfirmationDuration;
                        feedback = AbandonConfirmationHint;
                    }
                    else
                    {
                        expeditionStore.Abandon(activeRun);
                        activeRun = null;
                        ReconcileFirstExpeditionGuide();
                        abandonConfirmationUntil = 0f;
                        feedback = "탐험을 포기했습니다. 영구 보유 몬스터는 유지됩니다.";
                    }
                }
            }

            GUI.Label(
                new Rect(panel.x + 24f, summary.yMax + 4f,
                    panel.width - 48f, 20f),
                feedback,
                BombBrickUiTheme.CaptionStyle(
                    10, TextAnchor.MiddleCenter));
        }

        private HunterRegionDefinition DrawRegionNavigation(
            Rect rect,
            IReadOnlyList<HunterRegionDefinition> regions,
            bool allowInput)
        {
            DrawTownPanel(rect, false);
            Rect left = new Rect(rect.x + 6f, rect.y + 5f, 54f, 46f);
            Rect right = new Rect(rect.xMax - 60f, rect.y + 5f, 54f, 46f);
            bool slideComplete = RegionSlideProgress() >= 0.999f;
            int fromIndex = selectedRegionIndex;
            if (BombBrickUiTheme.Button(
                left, "‹", allowInput && slideComplete &&
                selectedRegionIndex > 0, false, 26))
            {
                selectedRegionIndex--;
                BeginRegionSlide(fromIndex, -1);
            }
            else if (BombBrickUiTheme.Button(
                right, "›", allowInput && slideComplete &&
                selectedRegionIndex < regions.Count - 1, false, 26))
            {
                selectedRegionIndex++;
                BeginRegionSlide(fromIndex, 1);
            }

            HunterRegionDefinition selected = regions[selectedRegionIndex];
            Rect names = new Rect(
                left.xMax + 6f, rect.y + 4f,
                right.x - left.xMax - 12f, rect.height - 8f);
            DrawRegionNavigationNames(names, regions, selected);
            return selected;
        }

        private void DrawRegionNavigationNames(
            Rect rect,
            IReadOnlyList<HunterRegionDefinition> regions,
            HunterRegionDefinition selected)
        {
            float progress = RegionSlideProgress();
            bool sliding = previousRegionIndex >= 0 && progress < 1f;
            GUI.BeginGroup(rect);
            Rect local = new Rect(0f, 0f, rect.width, rect.height);
            if (sliding)
            {
                HunterRegionDefinition previous = regions[Mathf.Clamp(
                    previousRegionIndex, 0, regions.Count - 1)];
                DrawRegionNavigationText(
                    new Rect(-regionSlideDirection * progress * rect.width,
                        0f, rect.width, rect.height),
                    previous, 1f - progress);
                DrawRegionNavigationText(
                    new Rect(regionSlideDirection * (1f - progress) * rect.width,
                        0f, rect.width, rect.height),
                    selected, progress);
            }
            else
            {
                DrawRegionNavigationText(local, selected, 1f);
            }
            GUI.EndGroup();
        }

        private void DrawRegionNavigationText(
            Rect rect, HunterRegionDefinition region, float opacity)
        {
            Color previous = GUI.color;
            GUI.color = new Color(
                previous.r, previous.g, previous.b, previous.a * opacity);
            GUI.Label(
                new Rect(rect.x, rect.y, rect.width, 27f),
                region.displayName,
                BombBrickUiTheme.TitleStyle(
                    17, TextAnchor.MiddleCenter));
            bool unlocked = profile.unlockedRegionIds.Contains(region.id);
            GUI.Label(
                new Rect(rect.x, rect.y + 27f, rect.width, 18f),
                unlocked
                    ? ExpeditionWaveCount + " WAVE  ·  지역 상자 " +
                      region.regionBoxCost
                    : "잠긴 지역",
                BombBrickUiTheme.CaptionStyle(
                    9, TextAnchor.MiddleCenter));
            GUI.color = previous;
        }

        private void DrawDifficultySelector(
            Rect rect, string regionId, bool allowInput)
        {
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 15f),
                "도전 난이도",
                BombBrickUiTheme.CaptionStyle(9, TextAnchor.MiddleCenter));
            const float gap = 4f;
            float width = (rect.width - 20f - gap * 3f) / 4f;
            for (int index = 0; index < 4; index++)
            {
                ExpeditionDifficulty difficulty =
                    (ExpeditionDifficulty)index;
                Rect button = new Rect(
                    rect.x + 10f + index * (width + gap),
                    rect.y + 23f,
                    width,
                    26f);
                bool selected = selectedDifficulty == difficulty;
                bool unlocked = ExpeditionDifficultyProgress.IsUnlocked(
                    profile, regionId, difficulty);
                if (BombBrickUiTheme.Button(
                    button,
                    ExpeditionDifficultyRules.DisplayName(difficulty),
                    unlocked && allowInput,
                    selected))
                {
                    selectedDifficulty = difficulty;
                }
                if (selected)
                    BombBrickUiTheme.Frame(
                        button,
                        difficulty == ExpeditionDifficulty.Hell
                            ? BombBrickUiTheme.Red
                            : BombBrickUiTheme.Cyan,
                        2f);
            }
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 55f, rect.width - 16f, 20f),
                "적 HP ×" +
                ExpeditionDifficultyRules.HealthMultiplier(selectedDifficulty)
                    .ToString("0.00") +
                "  ·  포획 "+ExpeditionDifficultyRules.CaptureQualityRolls(selectedDifficulty)+"회",
                BombBrickUiTheme.CaptionStyle(10, TextAnchor.MiddleCenter));
        }

        private void DrawRunStat(Rect rect, int index, string label, string value)
        {
            float width = rect.width / 3f;
            Rect cell = new Rect(
                rect.x + index * width, rect.y + 11f, width, rect.height - 22f);
            if (index > 0)
                BombBrickUiTheme.Fill(
                    new Rect(cell.x, cell.y + 5f, 1f, cell.height - 10f),
                    BombBrickUiTheme.Border);
            GUI.Label(
                new Rect(cell.x + 4f, cell.y, cell.width - 8f, 18f),
                label,
                BombBrickUiTheme.CaptionStyle(
                    10, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(cell.x + 4f, cell.y + 20f, cell.width - 8f, 26f),
                value,
                BombBrickUiTheme.TitleStyle(
                    15, TextAnchor.MiddleCenter));
        }

        private void DrawCollectionPanel(Rect panel)
        {
            List<OwnedMonsterSave> monsters = CollectionMonsters();
            DrawTownPanel(panel, false);
            GUI.Label(
                new Rect(panel.x + 22f, panel.y + 14f, panel.width * 0.45f, 24f),
                "대기 몬스터  " + monsters.Count,
                BombBrickUiTheme.TitleStyle(17));
            GUI.Label(
                new Rect(panel.x + panel.width * 0.45f, panel.y + 14f,
                    panel.width * 0.55f - 22f, 24f),
                activeRun == null
                    ? "클릭: 정보 · 드래그: 편성"
                    : "탐험 진행 중 · 편성 잠금",
                BombBrickUiTheme.CaptionStyle(
                    11, TextAnchor.MiddleRight));

            Rect scrollRect = new Rect(
                panel.x + 16f,
                panel.y + 48f,
                panel.width - 32f,
                panel.height - 62f);
            collectionDropRect = scrollRect;
            float gap = 10f;
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((scrollRect.width - 18f + gap) / 280f), 1, 6);
            float cardWidth =
                (scrollRect.width - 18f - gap * (columns - 1)) / columns;
            const float cardHeight = 176f;
            int rows = Mathf.CeilToInt(monsters.Count / (float)columns);
            Rect content = new Rect(
                0f, 0f, scrollRect.width - 18f,
                Mathf.Max(scrollRect.height, rows * (cardHeight + gap)));
            collectionScroll = GUI.BeginScrollView(
                scrollRect, collectionScroll, content);
            Vector2Int visible = VisibleCollectionRange(collectionScroll.y, scrollRect.height,
                monsters.Count, columns, cardHeight + gap);
            for (int index = visible.x; index < visible.y; index++)
            {
                int row = index / columns;
                int column = index % columns;
                Rect card = new Rect(
                    column * (cardWidth + gap),
                    row * (cardHeight + gap),
                    cardWidth,
                    cardHeight);
                Rect screenCard = new Rect(
                    scrollRect.x + card.x - collectionScroll.x,
                    scrollRect.y + card.y - collectionScroll.y,
                    card.width,
                    card.height);
                DrawOwnedCard(card, screenCard, monsters[index]);
            }
            GUI.EndScrollView();
        }

        private List<OwnedMonsterSave> CollectionMonsters()
        {
            // Every purchase, exchange and formation change saves before updating this screen.
            if (collectionProfile == profile && collectionSavedAt == profile.lastSavedUtcTicks &&
                collectionMonsters != null) return collectionMonsters;
            collectionProfile = profile;
            collectionSavedAt = profile.lastSavedUtcTicks;
            var roster = profile.expeditionPreset.rosterInstanceIds;
            collectionMonsters = profile.monsters.Where(item => item != null &&
                    !roster.Contains(item.instanceId) && database.Get(item.monsterId) != null &&
                    database.Get(item.monsterId).enabled)
                .OrderBy(item => database.Get(item.monsterId).serialNumber)
                .ThenBy(item => item.acquiredUtcTicks).ToList();
            return collectionMonsters;
        }

        public static Vector2Int VisibleCollectionRange(float scrollY, float height, int count,
            int columns, float rowHeight)
        {
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollY / rowHeight));
            int endRow = Mathf.Max(firstRow, Mathf.CeilToInt((scrollY + height) / rowHeight));
            return new Vector2Int(Mathf.Min(count, firstRow * columns), Mathf.Min(count, endRow * columns));
        }

        private void DrawMonsterRegionAffinity(Rect portrait, OwnedMonsterSave owned, bool allowTooltip = true)
        {
            var regions = HunterRegionCatalog.All;
            string map = activeRun == null
                ? regions[Mathf.Clamp(selectedRegionIndex, 0, regions.Count - 1)].id
                : activeRun.regionId;
            float multiplier = HunterRegionCatalog.RegionDamageMultiplier(map, owned.monsterId);
            CombatHudIconArt.DrawRegionAffinity(new Rect(
                portrait.xMax - 18f, portrait.y + 2f, 16f, 16f), multiplier);
            Rect label = new Rect(portrait.xMax - 38f, portrait.yMax - 16f, 36f, 14f);
            BombBrickUiTheme.Fill(label, BombBrickUiTheme.Surface);
            Color previous = GUI.color;
            GUI.color = multiplier > 1f ? BombBrickUiTheme.Lime :
                multiplier < 1f ? BombBrickUiTheme.Red : BombBrickUiTheme.TextSecondary;
            GUI.Label(label, "×" + multiplier.ToString("0.0"),
                BombBrickUiTheme.CaptionStyle(10, TextAnchor.MiddleRight));
            GUI.color = previous;
            if (allowTooltip && label.Contains(Event.current.mousePosition))
                hoveredRegionAffinity = multiplier;
        }

        private static string RegionAffinityTooltipDescription(float multiplier)
        {
            int percent = Mathf.RoundToInt(Mathf.Abs(multiplier - 1f) * 100f);
            return multiplier > 1f
                ? GameLocalization.Format("tooltip.region_affinity.strong", "현재 지역에서 주는 피해가 {0}% 증가합니다.", percent)
                : multiplier < 1f
                    ? GameLocalization.Format("tooltip.region_affinity.weak", "현재 지역에서 주는 피해가 {0}% 감소합니다.", percent)
                    : GameLocalization.TextByKey("tooltip.region_affinity.neutral", "현재 지역에서 주는 피해는 변하지 않습니다.");
        }

        private void DrawRegionAffinityHover()
        {
            if (!hoveredRegionAffinity.HasValue || draggingMonster ||
                currentTooltipRect.Contains(Event.current.mousePosition)) return;
            float multiplier = hoveredRegionAffinity.Value;
            Color accent = multiplier > 1f ? BombBrickUiTheme.Lime :
                multiplier < 1f ? BombBrickUiTheme.Red : BombBrickUiTheme.TextSecondary;
            BombBrickUiTheme.DrawHoverTooltip(Event.current.mousePosition,
                GameLocalization.TextByKey("tooltip.region_affinity.title", "지역 상성"),
                "×" + multiplier.ToString("0.0"), RegionAffinityTooltipDescription(multiplier), accent);
        }

        private void DrawOwnedCard(
            Rect card,
            Rect screenCard,
            OwnedMonsterSave owned)
        {
            bool hovered = card.Contains(Event.current.mousePosition) &&
                collectionDropRect.Contains(screenCard.position + Event.current.mousePosition - card.position);
            BombBrickUiTheme.DrawCard(
                card, BombBrickUiTheme.Violet, false, hovered);

            Rect portrait = new Rect(
                card.x + 12f, card.y + 12f, 72f, 72f);
            DrawMonsterPortrait(portrait, owned);
            DrawMonsterRegionAffinity(portrait, owned, hovered);
            GUI.Label(
                new Rect(portrait.xMax + 12f, card.y + 15f,
                    card.width - 108f - (owned.isNew ? 46f : 0f), 26f),
                MonsterName(owned),
                BombBrickUiTheme.TitleStyle(15));
            GUI.Label(
                new Rect(portrait.xMax + 12f, card.y + 48f, card.width - 108f, 24f),
                GameLocalization.Text("평균 ") + MonsterStatQuality.OverallGrade(owned),
                BombBrickUiTheme.BodyStyle(14));
            string previousTrait = hoveredGrowthTrait;
            DrawSpeciesProgression(new Rect(card.x + 12f, card.y + 100f,
                card.width - 24f, 64f), database.Get(owned.monsterId), owned);
            if (!hovered) hoveredGrowthTrait = previousTrait;
            if (owned.isNew)
            {
                BombBrickUiTheme.Badge(
                    new Rect(card.xMax - 48f, card.y + 7f, 41f, 20f),
                    "NEW",
                    BombBrickUiTheme.Orange);
            }

            if (screenCard.Overlaps(collectionDropRect))
            {
                cardHits.Add(new MonsterCardHit
                {
                    rect = Intersection(screenCard, collectionDropRect),
                    instanceId = owned.instanceId,
                    fromRoster = false,
                    rosterIndex = -1
                });
            }
        }

        private void ProcessMonsterPointerInput()
        {
            if (encyclopediaOpen || researchOpen || hunterShopOpen ||
                hunterBoxResult != null)
                return;
            int guideStep = profile.firstExpeditionGuideStep;
            if (guideStep < GuideComplete && guideStep != GuidePlaceRoster)
                return;
            Event current = Event.current;
            if (current.type == EventType.KeyDown &&
                current.keyCode == KeyCode.Escape)
            {
                tooltipMonsterInstanceId = "";
                current.Use();
                return;
            }

            Vector2 pointer = current.mousePosition;
            if (currentTooltipRect.Contains(pointer)) return;
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                MonsterCardHit? hit = CardAt(pointer);
                if (hit.HasValue &&
                    !string.IsNullOrWhiteSpace(hit.Value.instanceId))
                {
                    pressedMonsterInstanceId = hit.Value.instanceId;
                    pressedFromRoster = hit.Value.fromRoster;
                    pressedRosterIndex = hit.Value.rosterIndex;
                    pressedPointerPosition = pointer;
                    draggingMonster = false;
                    current.Use();
                }
                else if (!currentTooltipRect.Contains(pointer))
                {
                    tooltipMonsterInstanceId = "";
                }
                return;
            }

            if (current.type == EventType.MouseDrag &&
                !string.IsNullOrWhiteSpace(pressedMonsterInstanceId))
            {
                if (!draggingMonster &&
                    Vector2.Distance(pointer, pressedPointerPosition) >=
                    MonsterDragThreshold)
                {
                    if (activeRun == null)
                    {
                        draggingMonster = true;
                        tooltipMonsterInstanceId = "";
                    }
                    else
                    {
                        feedback =
                            "진행 중인 탐험을 마친 뒤 탐험덱을 변경할 수 있습니다.";
                    }
                }
                current.Use();
                GUI.changed = true;
                return;
            }

            if (current.type != EventType.MouseUp ||
                current.button != 0 ||
                string.IsNullOrWhiteSpace(pressedMonsterInstanceId))
                return;

            if (draggingMonster)
            {
                DropDraggedMonster(pointer);
            }
            else
            {
                tooltipMonsterInstanceId = pressedMonsterInstanceId;
                MarkMonsterSeen(pressedMonsterInstanceId);
                MonsterCardHit? anchorHit = CardAt(pointer);
                if (!anchorHit.HasValue ||
                    anchorHit.Value.instanceId != pressedMonsterInstanceId)
                {
                    anchorHit = cardHits
                        .FirstOrDefault(hit =>
                            hit.instanceId == pressedMonsterInstanceId);
                }
                if (anchorHit.HasValue)
                    tooltipAnchor = anchorHit.Value.rect;
            }

            pressedMonsterInstanceId = "";
            pressedFromRoster = false;
            pressedRosterIndex = -1;
            draggingMonster = false;
            current.Use();
            GUI.changed = true;
        }

        private void MarkMonsterSeen(string instanceId)
        {
            OwnedMonsterSave owned = FindOwned(instanceId);
            if (owned == null || !owned.isNew)
                return;
            owned.isNew = false;
            profileStore.Save(profile);
        }

        private MonsterCardHit? CardAt(Vector2 pointer)
        {
            for (int index = cardHits.Count - 1; index >= 0; index--)
            {
                if (cardHits[index].rect.Contains(pointer))
                    return cardHits[index];
            }
            return null;
        }

        private void DropDraggedMonster(Vector2 pointer)
        {
            if (activeRun != null)
            {
                feedback =
                    "진행 중인 탐험을 마친 뒤 탐험덱을 변경할 수 있습니다.";
                return;
            }

            MonsterCardHit? target = cardHits
                .Where(hit => hit.fromRoster && hit.rect.Contains(pointer))
                .Cast<MonsterCardHit?>()
                .FirstOrDefault();
            if (target.HasValue)
            {
                PlaceMonsterInRoster(
                    pressedMonsterInstanceId,
                    target.Value.rosterIndex);
                return;
            }

            if (pressedFromRoster && collectionDropRect.Contains(pointer))
            {
                RemoveMonsterFromRoster(pressedMonsterInstanceId);
                return;
            }

            feedback = "편성 변경이 취소되었습니다.";
        }

        private void PlaceMonsterInRoster(
            string instanceId,
            int targetIndex)
        {
            OwnedMonsterSave owned = FindOwned(instanceId);
            if (owned == null || targetIndex < 0 ||
                targetIndex >= PlayerProfileStore.ExpeditionRosterSize)
                return;

            List<string> roster = new List<string>(
                profile.expeditionPreset.rosterInstanceIds);
            int existingIndex = roster.IndexOf(instanceId);
            if (existingIndex == targetIndex)
            {
                feedback = MonsterName(owned) + "은(는) 이미 이 슬롯에 있습니다.";
                return;
            }

            while (roster.Count <= targetIndex)
                roster.Add(string.Empty);
            if (existingIndex >= 0)
                roster[existingIndex] = roster[targetIndex];
            roster[targetIndex] = instanceId;

            if (roster.Select(FindOwned)
                .Where(monster => monster != null)
                .GroupBy(monster => monster.monsterId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                feedback = GameLocalization.TextByKey(
                    "town.roster.unique_monster_type",
                    "같은 종류의 몬스터는 탐험대에 한 마리만 편성할 수 있습니다.");
                return;
            }

            if (SaveRoster(
                roster,
                MonsterName(owned) + "을(를) 탐험덱에 편성했습니다."))
            {
                GameAudio.Play(GameAudio.Cue.Place);
                StartRosterSlotImpact(targetIndex, 18);
                if (existingIndex >= 0 && existingIndex != targetIndex)
                    StartRosterSlotImpact(existingIndex, 10);
            }
        }

        private void RemoveMonsterFromRoster(string instanceId)
        {
            OwnedMonsterSave owned = FindOwned(instanceId);
            List<string> roster = new List<string>(
                profile.expeditionPreset.rosterInstanceIds);
            int index = roster.IndexOf(instanceId);
            if (index < 0)
                return;
            roster[index] = string.Empty;
            SaveRoster(
                roster,
                MonsterName(owned) + "을(를) 탐험덱에서 제외했습니다.");
        }

        private bool SaveRoster(List<string> roster, string successMessage)
        {
            try
            {
                profileStore.SetExpeditionRoster(profile, database, roster);
                ReconcileFirstExpeditionGuide();
                feedback = successMessage;
                return true;
            }
            catch (Exception exception)
            {
                feedback = "편성 저장 실패: " + exception.Message;
                return false;
            }
        }

        private void StartRosterSlotImpact(int slotIndex, int leafCount)
        {
            if (slotIndex < 0 || slotIndex >= rosterSlotRects.Length)
                return;
            rosterSlotImpactStartedAt[slotIndex] = Time.unscaledTime;
            TriggerTownInteractionLeaves(rosterSlotRects[slotIndex], leafCount);
        }

        private void DrawResearchModal()
        {
            BombBrickUiTheme.DrawModalBackdrop();
            float width = Mathf.Min(1240f, BombBrickUiLayout.Width - 40f);
            float height = Mathf.Min(740f, BombBrickUiLayout.Height - 40f);
            Rect panel = new Rect(
                (BombBrickUiLayout.Width - width) * 0.5f,
                (BombBrickUiLayout.Height - height) * 0.5f,
                width,
                height);
            BombBrickUiTheme.DrawPanel(panel, true);
            BombBrickUiTheme.Fill(
                new Rect(panel.x, panel.y, panel.width, 5f),
                BombBrickUiTheme.Lime);

            GUI.Label(
                new Rect(panel.x + 26f, panel.y + 18f, 420f, 34f),
                GameLocalization.TextByKey("research.title", "연구"),
                BombBrickUiTheme.TitleStyle(27));
            GUI.Label(
                new Rect(panel.x + 27f, panel.y + 51f, 520f, 20f),
                GameLocalization.TextByKey(
                    "research.subtitle",
                    "탐험 경험으로 계정 전체에 적용되는 능력을 연구합니다."),
                BombBrickUiTheme.CaptionStyle(11));
            if (BombBrickUiTheme.Button(
                new Rect(panel.xMax - 67f, panel.y + 18f, 42f, 38f),
                "×", true))
            {
                researchOpen = false;
                researchResetConfirmationUntil = 0f;
                return;
            }

            int level = PermanentResearchSystem.Level(profile);
            int spent = PermanentResearchSystem.SpentPoints(profile);
            int available = PermanentResearchSystem.AvailablePoints(profile);
            int into = PermanentResearchSystem.ExperienceIntoLevel(profile);
            int needed = PermanentResearchSystem.ExperienceToNextLevel(profile);
            Rect summary = new Rect(
                panel.x + 26f, panel.y + 82f, panel.width - 52f, 70f);
            BombBrickUiTheme.DrawCard(
                summary, BombBrickUiTheme.Lime, false, false);
            GUI.Label(
                new Rect(summary.x + 16f, summary.y + 8f, 170f, 24f),
                GameLocalization.Format(
                    "research.level", "연구 레벨 {0}", level),
                BombBrickUiTheme.TitleStyle(17));
            GUI.Label(
                new Rect(summary.x + 196f, summary.y + 8f, 230f, 24f),
                GameLocalization.Format(
                    "research.points", "사용 가능 {0} · 사용 {1}",
                    available, spent),
                BombBrickUiTheme.TitleStyle(14));
            string experienceLabel = level >= PermanentResearchSystem.MaximumLevel
                ? GameLocalization.TextByKey("research.max_level", "최대 레벨")
                : GameLocalization.Format(
                    "research.experience", "연구 경험치 {0} / {1}", into, needed);
            GUI.Label(
                new Rect(summary.xMax - 250f, summary.y + 8f, 232f, 22f),
                experienceLabel,
                BombBrickUiTheme.CaptionStyle(
                    11, TextAnchor.MiddleRight));
            Rect progress = new Rect(
                summary.x + 16f, summary.y + 42f, summary.width - 32f, 13f);
            BombBrickUiTheme.Fill(progress, BombBrickUiTheme.Surface);
            float progressRatio = level >= PermanentResearchSystem.MaximumLevel
                ? 1f
                : needed <= 0 ? 0f : Mathf.Clamp01((float)into / needed);
            BombBrickUiTheme.Fill(
                new Rect(progress.x, progress.y,
                    progress.width * progressRatio, progress.height),
                BombBrickUiTheme.Lime);
            BombBrickUiTheme.Frame(progress, BombBrickUiTheme.Border, 1f);

            bool editable = activeRun == null;
            string[] branches = { "survival", "combat", "support" };
            Color[] accents =
            {
                BombBrickUiTheme.Cyan,
                BombBrickUiTheme.Red,
                BombBrickUiTheme.Orange
            };
            float gap = 10f;
            float columnWidth = (panel.width - 52f - gap * 2f) / 3f;
            float top = summary.yMax + 12f;
            float columnHeight = panel.yMax - top - 72f;
            for (int branchIndex = 0; branchIndex < branches.Length; branchIndex++)
            {
                string branch = branches[branchIndex];
                Color accent = accents[branchIndex];
                Rect column = new Rect(
                    panel.x + 26f + branchIndex * (columnWidth + gap),
                    top, columnWidth, columnHeight);
                BombBrickUiTheme.DrawPanel(column, true);
                BombBrickUiTheme.Fill(
                    new Rect(column.x, column.y, column.width, 4f), accent);
                GUI.Label(
                    new Rect(column.x + 14f, column.y + 10f,
                        column.width - 28f, 28f),
                    PermanentResearchSystem.BranchName(branch),
                    BombBrickUiTheme.TitleStyle(
                        17, TextAnchor.MiddleCenter));

                List<PermanentResearchMasterRow> definitions =
                    PermanentResearchSystem.All.Where(value => value != null &&
                        string.Equals(value.branch, branch,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.displayOrder).ToList();
                float rowTop = column.y + 47f;
                float rowHeight = (column.height - 58f) /
                    Mathf.Max(1, definitions.Count);
                for (int index = 0; index < definitions.Count; index++)
                    DrawResearchRow(
                        new Rect(column.x + 10f,
                            rowTop + index * rowHeight,
                            column.width - 20f, rowHeight - 7f),
                        definitions[index], accent, editable, available);
            }

            Rect reset = new Rect(
                panel.xMax - 252f, panel.yMax - 53f, 226f, 34f);
            bool confirming = Time.unscaledTime <
                researchResetConfirmationUntil;
            string resetLabel = confirming
                ? GameLocalization.TextByKey(
                    "research.reset_confirm", "한 번 더 눌러 전체 초기화")
                : GameLocalization.TextByKey(
                    "research.reset", "전체 연구 초기화");
            if (BombBrickUiTheme.Button(
                reset, resetLabel,
                editable && spent > 0, confirming))
            {
                if (!confirming)
                    researchResetConfirmationUntil = Time.unscaledTime + 3f;
                else
                {
                    PermanentResearchSystem.ResetAllocations(profile);
                    profileStore.Save(profile);
                    researchResetConfirmationUntil = 0f;
                    feedback = GameLocalization.TextByKey(
                        "research.reset_done", "연구 포인트를 모두 반환했습니다.");
                }
            }
            GUI.Label(
                new Rect(panel.x + 26f, panel.yMax - 51f,
                    panel.width - 296f, 32f),
                editable
                    ? GameLocalization.TextByKey(
                        "research.edit_hint",
                        "+ / - 버튼으로 언제든 무료 분배 · 핵심 연구는 앞 연구를 완료해야 합니다.")
                    : GameLocalization.TextByKey(
                        "research.locked_during_expedition",
                        "탐험 진행 중에는 연구 분배와 초기화가 잠깁니다."),
                BombBrickUiTheme.CaptionStyle(
                    11, TextAnchor.MiddleLeft));
        }

        private void DrawResearchRow(
            Rect rect, PermanentResearchMasterRow definition,
            Color accent, bool editable, int available)
        {
            int rank = PermanentResearchSystem.Rank(profile, definition.id);
            bool capstoneReady =
                PermanentResearchSystem.IsCapstoneAvailable(profile, definition);
            BombBrickUiTheme.DrawCard(
                rect, definition.capstone ? Color.Lerp(
                    accent, BombBrickUiTheme.Violet, 0.45f) : accent,
                rank >= definition.maxRank, false);
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 7f, rect.width - 112f, 21f),
                PermanentResearchSystem.DisplayName(definition),
                BombBrickUiTheme.TitleStyle(13));
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 28f,
                    rect.width - 112f, rect.height - 34f),
                definition.capstone && !capstoneReady
                    ? GameLocalization.TextByKey(
                        "research.capstone_locked", "앞 연구를 모두 완료하면 해금")
                    : PermanentResearchSystem.Description(definition),
                BombBrickUiTheme.CaptionStyle(9));
            BombBrickUiTheme.Badge(
                new Rect(rect.xMax - 94f, rect.y + 7f, 84f, 20f),
                rank + " / " + definition.maxRank,
                definition.capstone ? BombBrickUiTheme.Violet : accent);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && editable && rank > 0;
            if (BombBrickUiTheme.Button(
                new Rect(rect.xMax - 94f, rect.yMax - 31f, 38f, 25f),
                "−", true, false) &&
                PermanentResearchSystem.TryChangeRank(
                    profile, definition.id, -1))
                profileStore.Save(profile);
            GUI.enabled = previousEnabled && editable && available > 0 &&
                rank < definition.maxRank && capstoneReady;
            if (BombBrickUiTheme.Button(
                new Rect(rect.xMax - 48f, rect.yMax - 31f, 38f, 25f),
                "+", true, false) &&
                PermanentResearchSystem.TryChangeRank(
                    profile, definition.id, 1))
                profileStore.Save(profile);
            GUI.enabled = previousEnabled;
        }

        private void DrawEncyclopediaModal()
        {
            Event current = Event.current;
            BombBrickUiTheme.DrawModalBackdrop();
            float width = Mathf.Min(1120f, BombBrickUiLayout.Width - 52f);
            float height = Mathf.Min(680f, BombBrickUiLayout.Height - 52f);
            Rect panel = new Rect(
                (BombBrickUiLayout.Width - width) * 0.5f,
                (BombBrickUiLayout.Height - height) * 0.5f,
                width,
                height);
            DrawHunterPixelPanel(panel, BombBrickUiTheme.Cyan);

            List<string> families = encyclopediaFamilies;
            encyclopediaFamilyIndex = Mathf.Clamp(
                encyclopediaFamilyIndex, 0, Mathf.Max(0, families.Count - 1));
            int discovered = encyclopediaDiscoveredCount;
            int total = encyclopediaTotalCount;

            GUI.Label(
                new Rect(panel.x + 30f, panel.y + 20f,
                    panel.width - 250f, 18f),
                "계열별 서식 기록",
                BombBrickUiTheme.CaptionStyle(10));
            GUI.Label(
                new Rect(panel.x + 30f, panel.y + 39f,
                    panel.width - 250f, 38f),
                "몬스터 도감",
                BombBrickUiTheme.TitleStyle(27));
            BombBrickUiTheme.Badge(
                new Rect(panel.xMax - 256f, panel.y + 29f, 164f, 30f),
                "발견  " + discovered + " / " + total,
                BombBrickUiTheme.Cyan);
            if (BombBrickUiTheme.Button(
                new Rect(panel.xMax - 74f, panel.y + 24f, 44f, 38f),
                "×", true))
            {
                encyclopediaOpen = false;
                return;
            }

            Rect familyPanel = new Rect(
                panel.x + 28f, panel.y + 91f, 220f, panel.height - 121f);
            Rect detailPanel = new Rect(
                familyPanel.xMax + 12f, familyPanel.y,
                panel.xMax - familyPanel.xMax - 40f, familyPanel.height);
            DrawHunterPixelPanel(familyPanel, BombBrickUiTheme.Violet);
            DrawHunterPixelPanel(detailPanel, BombBrickUiTheme.Cyan);

            GUI.Label(
                new Rect(familyPanel.x + 14f, familyPanel.y + 12f,
                    familyPanel.width - 28f, 24f),
                "계열",
                BombBrickUiTheme.TitleStyle(
                    16, TextAnchor.MiddleCenter));
            Rect familyScrollRect = new Rect(
                familyPanel.x + 10f, familyPanel.y + 43f,
                familyPanel.width - 20f, familyPanel.height - 55f);
            const float familyRowStep = 38f;
            const float familyRowHeight = 32f;
            Rect familyContent = new Rect(
                0f, 0f, familyScrollRect.width - 16f,
                Mathf.Max(familyScrollRect.height,
                    families.Count * familyRowStep));
            if (current.type == EventType.KeyDown && families.Count > 1)
            {
                bool previousFamily = current.keyCode == KeyCode.LeftArrow ||
                    current.keyCode == KeyCode.UpArrow;
                bool nextFamily = current.keyCode == KeyCode.RightArrow ||
                    current.keyCode == KeyCode.DownArrow;
                if (previousFamily || nextFamily)
                {
                    int step = previousFamily ? -1 : 1;
                    encyclopediaFamilyIndex = (encyclopediaFamilyIndex + step + families.Count) % families.Count;
                    float rowTop = encyclopediaFamilyIndex * familyRowStep;
                    encyclopediaFamilyScroll.y = Mathf.Clamp(encyclopediaFamilyScroll.y,
                        Mathf.Max(0f, rowTop + familyRowHeight - familyScrollRect.height), rowTop);
                    GameAudio.Play(GameAudio.Cue.Click);
                    current.Use();
                }
            }
            encyclopediaFamilyScroll = GUI.BeginScrollView(
                familyScrollRect, encyclopediaFamilyScroll, familyContent);
            for (int index = 0; index < families.Count; index++)
            {
                string familyId = families[index];
                int familyTotal =
                    encyclopediaMonstersByFamily[familyId].Count;
                int familyFound = encyclopediaFoundByFamily[familyId];
                if (BombBrickUiTheme.Button(
                    new Rect(0f, index * familyRowStep,
                        familyContent.width, familyRowHeight),
                    FamilyDisplayName(familyId) + "  " +
                    familyFound + "/" + familyTotal,
                    true,
                    encyclopediaFamilyIndex == index))
                    encyclopediaFamilyIndex = index;
            }
            GUI.EndScrollView();

            if (families.Count == 0)
                return;
            string selectedFamily = families[encyclopediaFamilyIndex];
            List<MonsterDefinition> monsters =
                encyclopediaMonstersByFamily[selectedFamily];
            int found = encyclopediaFoundByFamily[selectedFamily];
            bool familyUnlocked = monsters.Count > 0 && found == monsters.Count;

            GUI.Label(
                new Rect(detailPanel.x + 18f, detailPanel.y + 12f,
                    detailPanel.width - 196f, 28f),
                FamilyDisplayName(selectedFamily) + " 계열",
                BombBrickUiTheme.TitleStyle(20));
            BombBrickUiTheme.Badge(
                new Rect(detailPanel.xMax - 174f, detailPanel.y + 11f,
                    156f, 26f),
                familyUnlocked
                    ? "계열 상자 해금"
                    : "조우  " + found + " / " + monsters.Count,
                familyUnlocked
                    ? BombBrickUiTheme.Lime
                    : BombBrickUiTheme.Violet);

            const float gap = 10f;
            float cardWidth =
                (detailPanel.width - 36f - gap * 3f) / 4f;
            float cardHeight = detailPanel.height - 68f;
            for (int index = 0; index < monsters.Count; index++)
            {
                MonsterDefinition definition = monsters[index];
                bool encountered = encyclopediaEncountered.Contains(
                    definition.monsterId);
                encyclopediaHabitatByMonster.TryGetValue(
                    definition.monsterId, out string habitat);
                Rect card = new Rect(
                    detailPanel.x + 18f + index * (cardWidth + gap),
                    detailPanel.y + 50f,
                    cardWidth,
                    cardHeight);
                Color accent = encountered
                    ? BombBrickUiTheme.Cyan
                    : BombBrickUiTheme.Border;
                DrawHunterPixelCard(
                    card, accent,
                    card.Contains(current.mousePosition));
                BombBrickUiTheme.Badge(
                    new Rect(card.x + 10f, card.y + 10f,
                        card.width - 20f, 22f),
                    encountered
                        ? "#" + definition.serialNumber
                        : "미발견",
                    accent);
                Rect portrait = new Rect(
                    card.x + 12f, card.y + 44f,
                    card.width - 24f,
                    Mathf.Min(150f, card.width - 24f));
                DrawEncyclopediaPortrait(
                    portrait, definition, !encountered);
                GUI.Label(
                    new Rect(card.x + 8f, portrait.yMax + 12f,
                        card.width - 16f, 28f),
                    encountered ? definition.displayName : "???",
                    BombBrickUiTheme.TitleStyle(
                        17, TextAnchor.MiddleCenter));
                Rect habitatRect = new Rect(
                    card.x + 10f, card.yMax - 72f,
                    card.width - 20f, 56f);
                if (encountered)
                    DrawSpeciesProgression(new Rect(card.x + 12f, portrait.yMax + 62f,
                        card.width - 24f, 84f), definition);
                BombBrickUiTheme.Fill(
                    habitatRect,
                    new Color(0.055f, 0.070f, 0.045f, 1f));
                BombBrickUiTheme.Frame(
                    habitatRect, accent, 2f);
                DrawHabitatGlyph(
                    new Rect(habitatRect.x + 8f,
                        habitatRect.y + 13f, 30f, 30f),
                    encountered
                        ? BombBrickUiTheme.Lime
                        : BombBrickUiTheme.TextSecondary);
                GUI.Label(
                    new Rect(habitatRect.x + 44f, habitatRect.y + 4f,
                        habitatRect.width - 50f, 18f),
                    "서식지",
                    BombBrickUiTheme.CaptionStyle(9));
                GUI.Label(
                    new Rect(habitatRect.x + 44f, habitatRect.y + 21f,
                        habitatRect.width - 50f, 27f),
                    string.IsNullOrWhiteSpace(habitat) ? "미상" : habitat,
                    BombBrickUiTheme.TitleStyle(11));
            }
        }

        private void OpenHunterShop()
        {
            if (activeRun != null)
                return;
            if (profile.firstExpeditionGuideStep == GuideOpenShop)
                SetFirstExpeditionGuideStep(GuideComplete);
            bool stockCreated =
                HunterShopSystem.EnsureFamilyStock(profile, database);
            if (stockCreated)
                profileStore.Save(profile);
            DetectHunterShopUnlocks();
            hunterShopOpen = true;
            hunterShopClosing = false;
            hunterShopTransitionStartedAt = Time.unscaledTime;
            GameAudio.Play(GameAudio.Cue.SlideOpen);
            hunterShopTab = 0;
            hunterBoxContentsOpen = false;
            hunterBoxContentsMonsterIds.Clear();
            tooltipMonsterInstanceId = "";
            draggingMonster = false;
            pressedMonsterInstanceId = "";
            feedback = "";
        }

        private void CloseHunterShop()
        {
            if (!hunterShopOpen || hunterShopClosing)
                return;
            hunterShopClosing = true;
            hunterShopTransitionStartedAt = Time.unscaledTime;
            GameAudio.Play(GameAudio.Cue.SlideClose);
        }

        private void ReconcileFirstExpeditionGuide()
        {
            if (profile == null || profileStore == null || activeRun != null)
                return;
            int step = profile.firstExpeditionGuideStep;
            if (step < GuideShowTokens &&
                profile.settledExpeditionRunIds != null &&
                profile.settledExpeditionRunIds.Count > 0)
            {
                SetFirstExpeditionGuideStep(GuideShowTokens);
                return;
            }
            if (step == GuidePlaceRoster &&
                profile.expeditionPreset.RosterCount ==
                PlayerProfileStore.ExpeditionRosterSize)
            {
                SetFirstExpeditionGuideStep(GuideExplainMap);
            }
            else if (step == GuideAwaitResult)
            {
                SetFirstExpeditionGuideStep(GuideStartExpedition);
            }
        }

        private static bool HasCompletedFirstBattleTutorial(
            PlayerProfileSave value)
        {
            return value != null &&
                   (value.firstExpeditionGuideStep >= GuideComplete ||
                    value.firstExpeditionCaptureGuideStep >= 8 ||
                    value.settledExpeditionRunIds != null &&
                    value.settledExpeditionRunIds.Count > 0);
        }

        private void SetFirstExpeditionGuideStep(int step)
        {
            bool resetInterruptedItemGuide =
                step == GuideAwaitResult &&
                profile.firstExpeditionCaptureGuideStep > 4 &&
                profile.firstExpeditionCaptureGuideStep < 8;
            if (profile.firstExpeditionGuideStep == step &&
                !resetInterruptedItemGuide)
                return;
            if (resetInterruptedItemGuide)
                profile.firstExpeditionCaptureGuideStep = 4;
            // A settled expedition ends battle guidance; reward/shop guidance still follows.
            if (step >= GuideShowTokens)
                profile.firstExpeditionCaptureGuideStep = 8;
            profile.firstExpeditionGuideStep = step;
            guideStepChangedAt = Time.unscaledTime;
            profileStore.Save(profile);
        }

        private void DrawFirstExpeditionGuide(Rect viewport)
        {
            if (activeRun != null ||
                profile.firstExpeditionGuideStep == GuideAwaitResult ||
                profile.firstExpeditionGuideStep >= GuideComplete)
                return;

            int step = profile.firstExpeditionGuideStep;
            string message;
            Rect target;
            bool clickToContinue = false;
            if (step == GuideIntroduceCollection)
            {
                message = "이곳은 보유한 몬스터 목록입니다.";
                target = guideCollectionRect;
                clickToContinue = true;
            }
            else if (step == GuidePlaceRoster)
            {
                int rosterCount =
                    profile.expeditionPreset.RosterCount;
                message = "몬스터를 위 탐험덱으로 드래그해 8칸을 채우세요.  " +
                          rosterCount + " / " +
                          PlayerProfileStore.ExpeditionRosterSize;
                target = guideRosterRect;
            }
            else if (step == GuideExplainMap)
            {
                message = "지역마다 등장하는 몬스터와 적 조합이 달라집니다.";
                target = guideMapRect;
                clickToContinue = true;
            }
            else if (step == GuideExplainDifficulty)
            {
                message = "난이도를 클리어하면 같은 지역의 다음 단계가 열립니다.";
                target = guideDifficultyRect;
                clickToContinue = true;
            }
            else if (step == GuideStartExpedition)
            {
                message = "준비가 끝났습니다. 새 탐험 시작을 누르세요.";
                target = guideStartRect;
            }
            else if (step == GuideShowTokens)
            {
                message = "탐험에서 모은 사냥의 증표는 여기에 표시됩니다.";
                target = guideTokenRect;
                clickToContinue = true;
            }
            else if (step == GuideOpenShop)
            {
                message = "증표 상점을 열어 상자와 포획 그물을 확인하세요.";
                target = guideShopRect;
            }
            else
            {
                return;
            }

            float reveal = BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(
                scenePresentationStarted ? (Time.unscaledTime - guideStepChangedAt) / 0.22f : 0f));
            float blink = Mathf.PingPong(Time.unscaledTime * 2.2f, 1f);
            Color accent = Color.Lerp(
                BombBrickUiTheme.Cyan, Color.white, 0.2f + blink * 0.45f);
            if (step == GuidePlaceRoster)
            {
                float split = (guideRosterRect.yMax + guideCollectionRect.yMin) * .5f;
                DrawGuideShade(new Rect(0f, 0f, BombBrickUiLayout.Width, split),
                    guideRosterRect, .74f * reveal);
                DrawGuideShade(new Rect(0f, split, BombBrickUiLayout.Width, BombBrickUiLayout.Height - split),
                    guideCollectionRect, .74f * reveal);
                DrawGuideFrame(guideRosterRect, accent);
                DrawGuideFrame(guideCollectionRect, accent);
            }
            else
            {
                target = new Rect(
                    target.x - 6f, target.y - 6f,
                    target.width + 12f, target.height + 12f);
                DrawGuideShade(
                    new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height),
                    target,
                    0.74f * reveal);
                DrawGuideFrame(target, accent);
            }

            float width = Mathf.Min(650f, viewport.width - 24f);
            float hintY = target.yMax + 14f;
            if (step == GuidePlaceRoster || hintY + 58f > viewport.yMax)
                hintY = target.y - 70f;
            Rect hint = new Rect(
                Mathf.Clamp(
                    target.center.x - width * 0.5f,
                    viewport.x + 8f,
                    viewport.xMax - width - 8f),
                Mathf.Clamp(
                    hintY + (1f - reveal) * 10f,
                    Mathf.Max(viewport.y + 4f, GameFrontend.MenuButtonRect.yMax / BombBrickUiLayout.Scale + 12f),
                    viewport.yMax - 56f),
                width,
                52f);
            DrawHunterPixelCard(hint, accent, false);
            GUI.Label(
                new Rect(hint.x + 24f, hint.y + 8f,
                    hint.width - 80f, hint.height - 16f),
                message,
                BombBrickUiTheme.TitleStyle(15, TextAnchor.MiddleLeft));
            if (clickToContinue)
                GUI.Label(
                    new Rect(hint.xMax - 54f, hint.y + 8f,
                        34f, hint.height - 16f),
                    "›",
                    BombBrickUiTheme.TitleStyle(
                        22, TextAnchor.MiddleCenter));

            Event current = Event.current;
            if (clickToContinue &&
                current.type == EventType.MouseUp && current.button == 0)
            {
                SetFirstExpeditionGuideStep(step + 1);
                current.Use();
            }
        }

        private static void DrawGuideShade(
            Rect screen, Rect target, float alpha)
        {
            Rect hole = Rect.MinMaxRect(
                Mathf.Clamp(target.xMin, screen.xMin, screen.xMax),
                Mathf.Clamp(target.yMin, screen.yMin, screen.yMax),
                Mathf.Clamp(target.xMax, screen.xMin, screen.xMax),
                Mathf.Clamp(target.yMax, screen.yMin, screen.yMax));
            Color shade = new Color(0f, 0f, 0f, alpha);
            BombBrickUiTheme.Fill(
                new Rect(screen.x, screen.y, screen.width,
                    Mathf.Max(0f, hole.yMin - screen.y)), shade);
            BombBrickUiTheme.Fill(
                new Rect(screen.x, hole.yMin,
                    Mathf.Max(0f, hole.xMin - screen.x), hole.height), shade);
            BombBrickUiTheme.Fill(
                new Rect(hole.xMax, hole.yMin,
                    Mathf.Max(0f, screen.xMax - hole.xMax), hole.height), shade);
            BombBrickUiTheme.Fill(
                new Rect(screen.x, hole.yMax, screen.width,
                    Mathf.Max(0f, screen.yMax - hole.yMax)), shade);
        }

        private static void DrawGuideFrame(Rect rect, Color accent)
        {
            Rect pixel = BombBrickUiTheme.PixelRect(rect);
            BombBrickUiTheme.Frame(
                new Rect(pixel.x - 3f, pixel.y - 3f,
                    pixel.width + 6f, pixel.height + 6f),
                new Color(accent.r, accent.g, accent.b, 0.92f), 3f);
        }

        private void DetectHunterShopUnlocks()
        {
            hunterShopNewUnlockKeys.Clear();
            List<string> unlockedNames = new List<string>();
            foreach (HunterRegionDefinition region in HunterRegionCatalog.All)
            {
                if (!HunterShopSystem.IsRegionBoxUnlocked(
                        profile, region.id) ||
                    profile.notifiedHunterRegionBoxIds.Contains(region.id))
                    continue;
                profile.notifiedHunterRegionBoxIds.Add(region.id);
                hunterShopNewUnlockKeys.Add("region:" + region.id);
                unlockedNames.Add(region.displayName);
            }
            foreach (string familyId in HunterShopSystem.EligibleFamilies(
                         profile, database))
            {
                if (profile.notifiedHunterFamilyBoxIds.Contains(familyId))
                    continue;
                profile.notifiedHunterFamilyBoxIds.Add(familyId);
                hunterShopNewUnlockKeys.Add("family:" + familyId);
                unlockedNames.Add(FamilyDisplayName(familyId) + " 계열");
            }
            if (unlockedNames.Count == 0)
            {
                hunterShopUnlockStartedAt = -1f;
                hunterShopUnlockNotice = "";
                return;
            }
            profileStore.Save(profile);
            hunterShopUnlockStartedAt = Time.unscaledTime;
            hunterShopUnlockNotice = unlockedNames.Count == 1
                ? unlockedNames[0] + " 상자 해금!"
                : unlockedNames.Count + "개 상자 신규 해금!";
        }

        private void DrawHunterShopModal()
        {
            float duration = hunterShopClosing
                ? HunterShopCloseDuration
                : HunterShopOpenDuration;
            float progress = Mathf.Clamp01(
                (Time.unscaledTime - hunterShopTransitionStartedAt) / duration);
            if (hunterShopClosing && progress >= 1f)
            {
                hunterShopClosing = false;
                hunterShopOpen = false;
                return;
            }
            float visibility = hunterShopClosing
                ? 1f - BombBrickUiTheme.EaseOutCubic(progress)
                : BombBrickUiTheme.EaseOutCubic(progress);
            Color backdrop = BombBrickUiTheme.Backdrop;
            backdrop.a *= visibility;
            BombBrickUiTheme.Fill(
                new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height), backdrop);
            bool previousEnabled = GUI.enabled;
            if (hunterBoxContentsOpen || hunterShopClosing || progress < 1f)
                GUI.enabled = false;
            float width = Mathf.Min(1080f, BombBrickUiLayout.Width - 52f);
            float height = Mathf.Min(650f, BombBrickUiLayout.Height - 52f);
            Rect panel = new Rect(
                (BombBrickUiLayout.Width - width) * 0.5f,
                (BombBrickUiLayout.Height - height) * 0.5f,
                width,
                height);
            panel.x = Mathf.Lerp(BombBrickUiLayout.Width + 24f, panel.x, visibility);
            Color shopAccent = hunterShopTab == 0
                ? BombBrickUiTheme.Orange
                : hunterShopTab == 1
                    ? BombBrickUiTheme.Violet
                    : BombBrickUiTheme.Cyan;
            DrawHunterPixelPanel(panel, shopAccent);
            for (int block = 0; block < 9; block++)
            {
                BombBrickUiTheme.Fill(
                    new Rect(panel.x + 22f + block * 18f,
                        panel.y + 12f, 10f, 6f),
                    block % 2 == 0
                        ? shopAccent
                        : new Color(shopAccent.r, shopAccent.g,
                            shopAccent.b, 0.42f));
            }

            GUI.Label(
                new Rect(panel.x + 34f, panel.y + 24f,
                    panel.width - 250f, 18f),
                "사냥의 증표",
                BombBrickUiTheme.CaptionStyle(10));
            GUI.Label(
                new Rect(panel.x + 34f, panel.y + 43f,
                    panel.width - 260f, 38f),
                "증표 상점",
                BombBrickUiTheme.TitleStyle(26));
            Rect balance = new Rect(
                panel.xMax - 264f, panel.y + 27f, 168f, 36f);
            DrawHunterPixelCard(balance, BombBrickUiTheme.Orange, false);
            DrawHunterShopTexture(
                new Rect(balance.x + 8f, balance.y + 5f, 26f, 26f),
                HunterTokenTexture, false);
            GUI.Label(
                new Rect(balance.x + 39f, balance.y + 2f,
                    balance.width - 46f, balance.height - 4f),
                "증표  " + profile.hunterTokens,
                BombBrickUiTheme.TitleStyle(
                    15, TextAnchor.MiddleCenter));
            if (BombBrickUiTheme.Button(
                new Rect(panel.xMax - 76f, panel.y + 24f, 46f, 38f),
                "×", true))
            {
                CloseHunterShop();
                return;
            }

            float tabWidth = (panel.width - 88f) / 3f;
            Rect regionTab = new Rect(
                panel.x + 34f, panel.y + 93f,
                tabWidth, 42f);
            Rect familyTab = new Rect(
                regionTab.xMax + 10f, regionTab.y,
                regionTab.width, regionTab.height);
            Rect captureTab = new Rect(
                familyTab.xMax + 10f, regionTab.y,
                regionTab.width, regionTab.height);
            if (BombBrickUiTheme.Button(
                regionTab, "지역 상자", true, hunterShopTab == 0))
                hunterShopTab = 0;
            if (BombBrickUiTheme.Button(
                familyTab, "계열 상자", true, hunterShopTab == 1))
                hunterShopTab = 1;
            if (BombBrickUiTheme.Button(
                captureTab, "포획 도구", true, hunterShopTab == 2))
                hunterShopTab = 2;

            Rect content = new Rect(
                panel.x + 34f,
                panel.y + 151f,
                panel.width - 68f,
                panel.height - 221f);
            DrawHunterPixelPanel(content, shopAccent);

            if (hunterShopTab == 0)
                DrawHunterRegionOffers(content);
            else if (hunterShopTab == 1)
                DrawHunterFamilyOffers(content);
            else
                DrawHunterCaptureNetOffer(content);

            DrawHunterUnlockNotice(content);

            GUI.Label(
                new Rect(panel.x + 34f, panel.yMax - 53f,
                    panel.width - 68f, 28f),
                string.IsNullOrWhiteSpace(feedback)
                    ? hunterShopTab == 2
                        ? "25% 그물은 구매 · 75% 그물은 탐험 전리품 전용"
                        : "상자 안의 몬스터·성격·능력치는 개봉 전까지 공개되지 않습니다."
                    : feedback,
                BombBrickUiTheme.CaptionStyle(
                    11, TextAnchor.MiddleCenter));

            GUI.enabled = previousEnabled;
            if (hunterBoxContentsOpen)
                DrawHunterBoxContentsModal();
        }

        private void DrawHunterUnlockNotice(Rect content)
        {
            float elapsed = Time.unscaledTime - hunterShopUnlockStartedAt;
            if (hunterShopUnlockStartedAt < 0f || elapsed >= 1.8f ||
                string.IsNullOrWhiteSpace(hunterShopUnlockNotice))
                return;
            float rise = BombBrickUiTheme.EaseOutBack(
                Mathf.Clamp01(elapsed / 0.35f));
            Rect notice = new Rect(
                content.center.x - 190f,
                content.y + 18f - (1f - rise) * 26f,
                380f,
                58f);
            Color accent = Mathf.FloorToInt(elapsed * 8f) % 2 == 0
                ? Color.white
                : BombBrickUiTheme.Lime;
            DrawHunterPixelCard(notice, accent, false);
            GUI.Label(
                notice,
                hunterShopUnlockNotice,
                BombBrickUiTheme.TitleStyle(
                    20, TextAnchor.MiddleCenter));
        }

        private bool IsHunterShopUnlockAnimating(string key)
        {
            return Time.unscaledTime - hunterShopUnlockStartedAt < 1.8f &&
                hunterShopNewUnlockKeys.Contains(key);
        }

        private void DrawHunterRegionOffers(Rect content)
        {
            IReadOnlyList<HunterRegionDefinition> regions =
                HunterRegionCatalog.All;
            float gap = 14f;
            float cardWidth =
                (content.width - 36f - gap * (regions.Count - 1)) /
                regions.Count;
            float cardHeight = content.height - 34f;
            for (int index = 0; index < regions.Count; index++)
            {
                HunterRegionDefinition region = regions[index];
                bool unlocked =
                    HunterShopSystem.IsRegionBoxUnlocked(
                        profile, region.id);
                bool unlocking = IsHunterShopUnlockAnimating(
                    "region:" + region.id);
                Color accent = unlocking
                    ? Color.white
                    : unlocked
                    ? BombBrickUiTheme.Orange
                    : BombBrickUiTheme.Border;
                Rect card = new Rect(
                    content.x + 18f + index * (cardWidth + gap),
                    content.y + 17f,
                    cardWidth,
                    cardHeight);
                DrawHunterPixelCard(
                    card, accent,
                    unlocked && card.Contains(Event.current.mousePosition));
                BombBrickUiTheme.Badge(
                    new Rect(card.x + 12f, card.y + 12f,
                        card.width - 24f, 22f),
                    unlocked ? "초보 완료" : "초보 미완료",
                    accent);
                Rect boxRect = new Rect(
                    card.center.x - 54f,
                    card.y + 54f,
                    108f,
                    104f);
                if (GUI.Button(
                    new Rect(boxRect.x, boxRect.y,
                        boxRect.width, boxRect.height + 30f),
                    GUIContent.none, GUIStyle.none))
                    OpenHunterBoxContents(
                        region.displayName + " 지역 상자",
                        HunterShopSystem.RegionBoxContents(
                            database, region.id),
                        false);
                DrawHunterShopTexture(
                    boxRect, HunterRegionBoxTexture, !unlocked);
                BombBrickUiTheme.Badge(
                    new Rect(boxRect.center.x - 38f, boxRect.yMax + 2f,
                        76f, 26f),
                    "구성 보기", accent);
                GUI.Label(
                    new Rect(card.x + 10f, card.y + 190f,
                        card.width - 20f, 30f),
                    region.displayName,
                    BombBrickUiTheme.TitleStyle(
                        16, TextAnchor.MiddleCenter));
                GUI.Label(
                    new Rect(card.x + 12f, card.y + 227f,
                        card.width - 24f, 42f),
                    unlocked
                        ? "지역 출몰 몬스터\n무작위 1개체"
                        : "초보 난이도\n클리어 필요",
                    BombBrickUiTheme.CaptionStyle(
                        10, TextAnchor.MiddleCenter));
                if (BombBrickUiTheme.Button(
                    new Rect(card.x + 12f, card.yMax - 51f,
                        card.width - 24f, 38f),
                    unlocked
                        ? "증표 " + region.regionBoxCost
                        : "잠김",
                    unlocked &&
                    profile.hunterTokens >= region.regionBoxCost,
                    true))
                {
                    PurchaseRegionBox(region);
                }
            }
        }

        private void DrawHunterFamilyOffers(Rect content)
        {
            if (profile.hunterFamilyStockIds.Count == 0)
            {
                GUI.Label(
                    new Rect(content.x + 50f, content.y + 80f,
                        content.width - 100f, 54f),
                    "아직 진열 가능한 계열 상자가 없습니다.",
                    BombBrickUiTheme.TitleStyle(
                        19, TextAnchor.MiddleCenter));
                GUI.Label(
                    new Rect(content.x + 70f, content.y + 145f,
                        content.width - 140f, 62f),
                    "해당 계열의 일반 개체를 모두 조우하면\n" +
                    "다음 상점 갱신부터 계열 상자가 진열됩니다.",
                    BombBrickUiTheme.BodyStyle(
                        13, TextAnchor.MiddleCenter));
                return;
            }

            int offerCount = profile.hunterFamilyStockIds.Count;
            float gap = 20f;
            float cardWidth = Mathf.Min(
                286f,
                (content.width - 40f - gap * (offerCount - 1)) /
                offerCount);
            float totalWidth =
                cardWidth * offerCount + gap * (offerCount - 1);
            float startX = content.center.x - totalWidth * 0.5f;
            int cost = HunterShopSystem.CurrentFamilyBoxCost(profile);
            for (int index = 0; index < offerCount; index++)
            {
                string familyId = profile.hunterFamilyStockIds[index];
                bool unlocking = IsHunterShopUnlockAnimating(
                    "family:" + familyId);
                Color accent = unlocking
                    ? Color.white
                    : BombBrickUiTheme.Violet;
                Rect card = new Rect(
                    startX + index * (cardWidth + gap),
                    content.y + 18f,
                    cardWidth,
                    content.height - 36f);
                DrawHunterPixelCard(
                    card, accent,
                    card.Contains(Event.current.mousePosition));
                BombBrickUiTheme.Badge(
                    new Rect(card.x + 16f, card.y + 14f,
                        card.width - 32f, 23f),
                    "4 / 4 조우",
                    accent);
                Rect boxRect = new Rect(
                    card.center.x - 60f,
                    card.y + 55f,
                    120f,
                    112f);
                if (GUI.Button(
                    new Rect(boxRect.x, boxRect.y,
                        boxRect.width, boxRect.height + 30f),
                    GUIContent.none, GUIStyle.none))
                    OpenHunterBoxContents(
                        HunterShopSystem.FamilyDisplayName(familyId) +
                        " 계열 상자",
                        HunterShopSystem.FamilyBoxContents(
                            database, familyId),
                        true);
                DrawHunterShopTexture(
                    boxRect, HunterFamilyBoxTexture, false);
                BombBrickUiTheme.Badge(
                    new Rect(boxRect.center.x - 38f, boxRect.yMax + 2f,
                        76f, 26f),
                    "구성 보기", accent);
                GUI.Label(
                    new Rect(card.x + 14f, card.y + 199f,
                        card.width - 28f, 34f),
                    HunterShopSystem.FamilyDisplayName(familyId) +
                    " 계열 상자",
                    BombBrickUiTheme.TitleStyle(
                        18, TextAnchor.MiddleCenter));
                GUI.Label(
                    new Rect(card.x + 18f, card.y + 240f,
                        card.width - 36f, 40f),
                    "일반 개체 전부 조우\n계열 내 무작위 1개체",
                    BombBrickUiTheme.CaptionStyle(
                        10, TextAnchor.MiddleCenter));
                if (BombBrickUiTheme.Button(
                    new Rect(card.x + 18f, card.yMax - 55f,
                        card.width - 36f, 40f),
                    "증표 " + cost,
                    profile.hunterTokens >= cost,
                    true))
                {
                    PurchaseFamilyBox(familyId);
                }
            }
        }

        private void OpenHunterBoxContents(
            string title, IEnumerable<MonsterDefinition> definitions,
            bool familyBox)
        {
            hunterBoxContentsTitle = title ?? "상자 구성";
            hunterBoxContentsFamily = familyBox;
            hunterBoxContentsMonsterIds.Clear();
            hunterBoxContentsMonsterIds.AddRange(
                (definitions ?? Enumerable.Empty<MonsterDefinition>())
                    .Where(definition => definition != null &&
                        definition.enabled)
                    .Select(definition => definition.monsterId)
                    .Distinct(StringComparer.Ordinal));
            hunterBoxContentsScroll = Vector2.zero;
            hunterBoxContentsOpen = true;
        }

        private void DrawHunterBoxContentsModal()
        {
            BombBrickUiTheme.DrawModalBackdrop();
            float width = Mathf.Min(900f, BombBrickUiLayout.Width - 96f);
            float height = Mathf.Min(570f, BombBrickUiLayout.Height - 96f);
            Rect panel = new Rect(
                (BombBrickUiLayout.Width - width) * 0.5f,
                (BombBrickUiLayout.Height - height) * 0.5f,
                width,
                height);
            Color accent = hunterBoxContentsFamily
                ? BombBrickUiTheme.Violet
                : BombBrickUiTheme.Orange;
            int encounteredCount = hunterBoxContentsMonsterIds.Count(
                monsterId => encyclopediaEncountered.Contains(monsterId));
            DrawHunterPixelPanel(panel, accent);
            GUI.Label(
                new Rect(panel.x + 28f, panel.y + 22f,
                    panel.width - 190f, 34f),
                hunterBoxContentsTitle,
                BombBrickUiTheme.TitleStyle(23));
            BombBrickUiTheme.Badge(
                new Rect(panel.xMax - 208f, panel.y + 25f, 120f, 27f),
                "발견 " + encounteredCount + " / " +
                hunterBoxContentsMonsterIds.Count,
                accent);
            if (BombBrickUiTheme.Button(
                new Rect(panel.xMax - 70f, panel.y + 20f, 42f, 36f),
                "×", true))
            {
                hunterBoxContentsOpen = false;
                hunterBoxContentsMonsterIds.Clear();
                return;
            }

            GUI.Label(
                new Rect(panel.x + 28f, panel.y + 57f,
                    panel.width - 56f, 20f),
                "발견하지 못한 개체는 실루엣으로 표시됩니다.",
                BombBrickUiTheme.CaptionStyle(10));
            Rect viewport = new Rect(
                panel.x + 24f, panel.y + 86f,
                panel.width - 48f, panel.height - 112f);
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((viewport.width - 16f) / 156f), 3, 5);
            const float gap = 12f;
            float cardWidth =
                (viewport.width - 18f - gap * (columns - 1)) / columns;
            const float cardHeight = 166f;
            int rows = Mathf.CeilToInt(
                hunterBoxContentsMonsterIds.Count /
                Mathf.Max(1f, columns));
            Rect scrollContent = new Rect(
                0f, 0f, viewport.width - 18f,
                Mathf.Max(viewport.height,
                    rows * (cardHeight + gap) - gap));
            hunterBoxContentsScroll = GUI.BeginScrollView(
                viewport, hunterBoxContentsScroll, scrollContent);
            for (int index = 0;
                 index < hunterBoxContentsMonsterIds.Count;
                 index++)
            {
                MonsterDefinition definition =
                    database.Get(hunterBoxContentsMonsterIds[index]);
                if (definition == null)
                    continue;
                bool encountered = encyclopediaEncountered.Contains(
                    definition.monsterId);
                Color cardAccent = encountered
                    ? accent
                    : BombBrickUiTheme.Border;
                int row = index / columns;
                int column = index % columns;
                Rect card = new Rect(
                    column * (cardWidth + gap),
                    row * (cardHeight + gap),
                    cardWidth,
                    cardHeight);
                DrawHunterPixelCard(card, cardAccent, false);
                BombBrickUiTheme.Badge(
                    new Rect(card.x + 10f, card.y + 9f,
                        card.width - 20f, 20f),
                    encountered
                        ? "#" + definition.serialNumber
                        : "미발견",
                    cardAccent);
                Rect portrait = new Rect(
                    card.x + 12f, card.y + 38f,
                    card.width - 24f, 91f);
                DrawEncyclopediaPortrait(
                    portrait, definition, !encountered);
                GUI.Label(
                    new Rect(card.x + 8f, card.y + 134f,
                        card.width - 16f, 23f),
                    encountered ? definition.displayName : "???",
                    BombBrickUiTheme.TitleStyle(
                        13, TextAnchor.MiddleCenter));
            }
            GUI.EndScrollView();
        }

        private void DrawHunterCaptureNetOffer(Rect content)
        {
            const float gap = 22f;
            float cardWidth = Mathf.Min(340f, (content.width - 70f) * 0.5f);
            float totalWidth = cardWidth * 2f + gap;
            Rect commonCard = new Rect(
                content.center.x - totalWidth * 0.5f,
                content.y + 18f,
                cardWidth,
                content.height - 36f);
            Rect rareCard = new Rect(
                commonCard.xMax + gap,
                commonCard.y,
                cardWidth,
                commonCard.height);
            DrawHunterPixelCard(
                commonCard, BombBrickUiTheme.Cyan,
                commonCard.Contains(Event.current.mousePosition));
            DrawHunterPixelCard(
                rareCard, BombBrickUiTheme.Violet, false);

            int commonChance = Mathf.RoundToInt(
                CaptureNetCatalog.Get(
                    CaptureNetCatalog.StandardId).SuccessChance * 100f);
            int rareChance = Mathf.RoundToInt(
                CaptureNetCatalog.Get(
                    CaptureNetCatalog.RareId).SuccessChance * 100f);
            BombBrickUiTheme.Badge(
                new Rect(commonCard.x + 18f, commonCard.y + 14f,
                    commonCard.width - 36f, 23f),
                "포획 " + commonChance + "%  ·  보유 " +
                CaptureNetInventory.Count(
                    profile, CaptureNetCatalog.StandardId),
                BombBrickUiTheme.Cyan);
            BombBrickUiTheme.Badge(
                new Rect(rareCard.x + 18f, rareCard.y + 14f,
                    rareCard.width - 36f, 23f),
                "포획 " + rareChance + "%  ·  보유 " +
                CaptureNetInventory.Count(
                    profile, CaptureNetCatalog.RareId),
                BombBrickUiTheme.Violet);

            DrawHunterShopTexture(
                new Rect(commonCard.center.x - 58f,
                    commonCard.y + 50f, 116f, 116f),
                HunterCommonNetTexture, false);
            DrawHunterShopTexture(
                new Rect(rareCard.center.x - 58f,
                    rareCard.y + 50f, 116f, 116f),
                HunterRareNetTexture, false);
            GUI.Label(
                new Rect(commonCard.x + 16f, commonCard.y + 177f,
                    commonCard.width - 32f, 30f),
                "포획 그물",
                BombBrickUiTheme.TitleStyle(
                    19, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(rareCard.x + 16f, rareCard.y + 177f,
                    rareCard.width - 32f, 30f),
                "강화 포획 그물",
                BombBrickUiTheme.TitleStyle(
                    19, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(commonCard.x + 24f, commonCard.y + 215f,
                    commonCard.width - 48f, 38f),
                "증표 상점에서 구매",
                BombBrickUiTheme.CaptionStyle(
                    10, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(rareCard.x + 24f, rareCard.y + 215f,
                    rareCard.width - 48f, 38f),
                "탐험 전리품에서만 획득",
                BombBrickUiTheme.CaptionStyle(
                    10, TextAnchor.MiddleCenter));
            int cost = CaptureNetCatalog.TokenCost;
            if (BombBrickUiTheme.Button(
                new Rect(commonCard.x + 18f, commonCard.yMax - 55f,
                    commonCard.width - 36f, 40f),
                "증표 " + cost,
                profile.hunterTokens >= cost,
                true))
            {
                if (HunterShopSystem.TryPurchaseCaptureNet(
                    profile, profileStore, out string error))
                    feedback = "포획 그물 1개를 구매했습니다.";
                else
                    feedback = error;
            }
            BombBrickUiTheme.Badge(
                new Rect(rareCard.x + 18f, rareCard.yMax - 55f,
                    rareCard.width - 36f, 40f),
                "전리품 전용", BombBrickUiTheme.Violet);
        }

        private void PurchaseRegionBox(HunterRegionDefinition region)
        {
            var known = profile.monsters.Select(monster => monster.monsterId).ToArray();
            if (HunterShopSystem.TryPurchaseRegionBox(
                profile, profileStore, database, region.id,
                out OwnedMonsterSave acquired,
                out string error))
            {
                BeginHunterBoxReveal(acquired, false, region.id, known);
                feedback = "";
            }
            else
            {
                feedback = error;
            }
        }

        private void PurchaseFamilyBox(string familyId)
        {
            var known = profile.monsters.Select(monster => monster.monsterId).ToArray();
            if (HunterShopSystem.TryPurchaseFamilyBox(
                profile, profileStore, database, familyId,
                out OwnedMonsterSave acquired,
                out string error))
            {
                BeginHunterBoxReveal(acquired, true, null, known);
                feedback = "";
            }
            else
            {
                feedback = error;
            }
        }

        private void BeginHunterBoxReveal(OwnedMonsterSave acquired, bool familyBox,
            string region, ICollection<string> known)
        {
            hunterBoxResult = acquired;
            pendingRewardRunId = null;
            hunterBoxReveal = new ChestRewardPresentation(
                new List<OwnedMonsterSave> { acquired }, false, familyBox, region, known);
            tooltipMonsterInstanceId = "";
            BuildEncyclopediaCache();
        }

        private void BeginPendingRegionReward()
        {
            if (profile == null || profile.starterSelectionPending || hunterBoxReveal != null) return;
            RegionBoxRewardSave reward = profile.pendingRegionBoxRewards?.FirstOrDefault(
                item => item != null && item.candidates != null && item.candidates.Count > 0);
            if (reward == null) return;
            pendingRewardRunId = reward.runId;
            hunterBoxResult = reward.candidates[0];
            hunterBoxReveal = new ChestRewardPresentation(reward.candidates, true, false, reward.regionId,
                new HashSet<string>(profile.monsters.Select(monster => monster.monsterId)));
        }

        private void DrawHunterBoxReveal()
        {
            if (hunterBoxReveal == null) return;
            int choice = hunterBoxReveal.Draw(database);
            if (choice < 0) return;
            if (!string.IsNullOrEmpty(pendingRewardRunId) &&
                !HunterShopSystem.TryClaimRegionBoxReward(profile, profileStore, database,
                    pendingRewardRunId, choice, out _, out string error))
            {
                hunterBoxReveal.Error = error;
                return;
            }
            hunterBoxReveal.Confirm();
        }
        private Texture2D HunterRegionBoxTexture
        {
            get
            {
                if (hunterRegionBoxTexture == null)
                    hunterRegionBoxTexture = GameResourceCache.GetTexture(
                        "UI/HunterShop/region_box_pixel");
                PrepareHunterTexture(hunterRegionBoxTexture);
                return hunterRegionBoxTexture;
            }
        }

        private Texture2D HunterFamilyBoxTexture
        {
            get
            {
                if (hunterFamilyBoxTexture == null)
                    hunterFamilyBoxTexture = GameResourceCache.GetTexture(
                        "UI/HunterShop/family_box_pixel");
                PrepareHunterTexture(hunterFamilyBoxTexture);
                return hunterFamilyBoxTexture;
            }
        }

        private Texture2D HunterTokenTexture
        {
            get
            {
                if (hunterTokenTexture == null)
                    hunterTokenTexture = GameResourceCache.GetTexture(
                        "UI/HunterShop/hunter_token_pixel");
                PrepareHunterTexture(hunterTokenTexture);
                return hunterTokenTexture;
            }
        }

        private Texture2D HunterCommonNetTexture
        {
            get
            {
                if (hunterCommonNetTexture == null)
                    hunterCommonNetTexture = GameResourceCache.GetTexture(
                        "UI/CaptureNetIcons/capture_net_common");
                PrepareHunterTexture(hunterCommonNetTexture);
                return hunterCommonNetTexture;
            }
        }

        private Texture2D HunterRareNetTexture
        {
            get
            {
                if (hunterRareNetTexture == null)
                    hunterRareNetTexture = GameResourceCache.GetTexture(
                        "UI/CaptureNetIcons/capture_net_rare");
                PrepareHunterTexture(hunterRareNetTexture);
                return hunterRareNetTexture;
            }
        }

        private static void PrepareHunterTexture(Texture2D texture)
        {
            if (texture == null)
                return;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
        }

        private static void DrawHunterShopTexture(
            Rect rect, Texture2D texture, bool locked)
        {
            if (texture == null)
                return;
            Color previous = GUI.color;
            GUI.color = locked
                ? new Color(0.25f, 0.28f, 0.30f, 0.58f)
                : Color.white;
            GUI.DrawTexture(
                BombBrickUiTheme.PixelRect(rect), texture,
                ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        private static void DrawHunterPixelPanel(Rect rect, Color accent)
        {
            rect = BombBrickUiTheme.PixelRect(rect);
            BombBrickUiTheme.Fill(
                new Rect(rect.x + 6f, rect.y + 6f,
                    rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.66f));
            BombBrickUiTheme.Fill(
                rect, new Color(0.035f, 0.025f, 0.020f, 0.99f));
            BombBrickUiTheme.Frame(
                rect, new Color(0.015f, 0.010f, 0.008f, 1f), 4f);
            BombBrickUiTheme.Frame(
                new Rect(rect.x + 4f, rect.y + 4f,
                    rect.width - 8f, rect.height - 8f),
                new Color(accent.r, accent.g, accent.b, 0.72f), 2f);
            BombBrickUiTheme.Fill(
                new Rect(rect.x + 10f, rect.y + 10f, 8f, 8f), accent);
            BombBrickUiTheme.Fill(
                new Rect(rect.xMax - 18f, rect.yMax - 18f, 8f, 8f), accent);
        }

        private static void DrawHunterPixelCard(
            Rect rect, Color accent, bool hovered)
        {
            rect = BombBrickUiTheme.PixelRect(rect);
            Color edge = hovered
                ? Color.Lerp(accent, Color.white, 0.22f)
                : new Color(accent.r, accent.g, accent.b, 0.72f);
            BombBrickUiTheme.Fill(
                new Rect(rect.x + 4f, rect.y + 4f,
                    rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.55f));
            BombBrickUiTheme.Fill(
                rect, hovered
                    ? new Color(0.075f, 0.060f, 0.046f, 1f)
                    : new Color(0.050f, 0.038f, 0.030f, 1f));
            BombBrickUiTheme.Frame(
                rect, new Color(0.012f, 0.008f, 0.006f, 1f), 4f);
            BombBrickUiTheme.Frame(
                new Rect(rect.x + 4f, rect.y + 4f,
                    rect.width - 8f, rect.height - 8f), edge, 2f);
            BombBrickUiTheme.Fill(
                new Rect(rect.x + 10f, rect.y + 10f, 8f, 8f), edge);
            BombBrickUiTheme.Fill(
                new Rect(rect.xMax - 18f, rect.y + 10f, 8f, 8f), edge);
        }

        private void DrawMonsterTooltip()
        {
            currentTooltipRect = Rect.zero;
            if (string.IsNullOrWhiteSpace(tooltipMonsterInstanceId))
                return;
            OwnedMonsterSave owned = FindOwned(tooltipMonsterInstanceId);
            if (owned == null)
            {
                tooltipMonsterInstanceId = "";
                return;
            }
            MonsterDefinition definition = database.Get(owned.monsterId);
            if (definition == null)
                return;

            const float width = 410f;
            const float height = 466f;
            float x = tooltipAnchor.xMax + 12f;
            if (x + width > BombBrickUiLayout.Width - 18f)
                x = tooltipAnchor.x - width - 12f;
            float y = Mathf.Clamp(
                tooltipAnchor.y,
                18f,
                BombBrickUiLayout.Height - height - 18f);
            currentTooltipRect = new Rect(
                Mathf.Clamp(x, 18f, BombBrickUiLayout.Width - width - 18f),
                y,
                width,
                height);

            Rect panel = currentTooltipRect;
            BombBrickUiTheme.DrawPanel(panel, true);
            BombBrickUiTheme.Fill(
                new Rect(panel.x, panel.y, panel.width, 4f),
                BombBrickUiTheme.Cyan);
            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 15f, panel.width - 40f, 16f),
                "몬스터 정보",
                BombBrickUiTheme.CaptionStyle(9));
            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 32f, panel.width - 40f, 32f),
                "#" + definition.serialNumber + "  " + MonsterName(owned),
                BombBrickUiTheme.TitleStyle(22));

            Rect portrait = new Rect(
                panel.x + 20f, panel.y + 73f, 104f, 104f);
            DrawMonsterPortrait(portrait, owned);
            GUI.Label(
                new Rect(panel.x + 140f, panel.y + 75f,
                    panel.width - 270f, 20f),
                FamilyDisplayName(definition.familyId) + " 계열",
                BombBrickUiTheme.CaptionStyle(11));
            BombBrickUiTheme.Badge(
                new Rect(panel.xMax - 112f, panel.y + 73f, 92f, 20f),
                "평균 " + MonsterStatQuality.OverallGrade(owned),
                BombBrickUiTheme.Violet);
            Rect personalityRect = new Rect(
                panel.x + 136f, panel.y + 98f, panel.width - 152f, 28f);
            bool personalityHovered =
                personalityRect.Contains(Event.current.mousePosition);
            if (personalityHovered)
                BombBrickUiTheme.Fill(
                    personalityRect, BombBrickUiTheme.SurfaceHover);
            GUI.Label(
                new Rect(personalityRect.x + 4f, personalityRect.y + 3f,
                    personalityRect.width - 8f, 22f),
                "성격  ·  " +
                MonsterInstanceGenerator.PersonalityDisplayName(
                    owned.personalityId),
                BombBrickUiTheme.TitleStyle(14));
            MonsterTraitDefinition intrinsic =
                MonsterTraitCatalog.Get(owned.intrinsicTraitId);
            Rect traitRect = new Rect(
                panel.x + 136f, panel.y + 128f, panel.width - 152f, 34f);
            bool traitHovered = traitRect.Contains(Event.current.mousePosition);
            if (traitHovered)
                BombBrickUiTheme.Fill(traitRect, BombBrickUiTheme.SurfaceHover);
            GUI.Label(
                new Rect(traitRect.x + 4f, traitRect.y + 3f,
                    traitRect.width - 8f, 28f),
                "고유 특성  ·  " +
                (intrinsic == null
                    ? owned.intrinsicTraitId
                    : intrinsic.displayName),
                BombBrickUiTheme.BodyStyle(12));
            SkillMasterRow skill = GameMasterData.Skill(definition.skillId);
            Rect skillRect = new Rect(
                panel.x + 136f, panel.y + 165f, panel.width - 152f, 28f);
            bool skillHovered = skillRect.Contains(Event.current.mousePosition);
            if (skillHovered)
                BombBrickUiTheme.Fill(skillRect, BombBrickUiTheme.SurfaceHover);
            GUI.Label(
                new Rect(skillRect.x + 4f, skillRect.y + 2f,
                    skillRect.width - 8f, 24f),
                "고유 스킬  ·  " +
                (skill == null ? definition.skillId : skill.displayName),
                BombBrickUiTheme.BodyStyle(12));

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 203f, panel.width - 40f, 20f),
                "영구 능력치",
                BombBrickUiTheme.TitleStyle(13));
            Rect stats = new Rect(
                panel.x + 20f, panel.y + 229f, panel.width - 40f, 82f);
            BombBrickUiTheme.Fill(stats, BombBrickUiTheme.Surface);
            BombBrickUiTheme.Frame(stats, BombBrickUiTheme.Border, 1f);
            DrawTooltipStat(
                stats, 0, 0, "체력",
                Mathf.RoundToInt(
                    definition.maxHp * owned.hpMultiplier) + "  " +
                MonsterStatQuality.StatGrade(
                    owned,MonsterIndividualStat.Health));
            DrawTooltipStat(
                stats, 1, 0, "공격",
                Mathf.RoundToInt(
                    definition.attackDamage *
                    owned.damageMultiplier) + "  " +
                MonsterStatQuality.StatGrade(
                    owned,MonsterIndividualStat.AttackDamage));
            DrawTooltipStat(
                stats, 2, 0, "공속",
                ((1f / Mathf.Max(0.1f, definition.attackInterval)) *
                 owned.attackSpeedMultiplier).ToString("0.00") + "  " +
                MonsterStatQuality.StatGrade(
                    owned,MonsterIndividualStat.AttackSpeed));
            DrawTooltipStat(
                stats, 3, 0, "방어",
                (definition.defense * owned.defenseMultiplier).ToString("0") +
                "  " + MonsterStatQuality.StatGrade(
                    owned,MonsterIndividualStat.Defense));
            DrawTooltipStat(
                stats, 0, 1, "저항",
                (definition.statusResistance *
                 owned.statusResistanceMultiplier).ToString("0") + "  " +
                MonsterStatQuality.StatGrade(
                    owned,MonsterIndividualStat.StatusResistance));
            DrawTooltipStat(
                stats, 1, 1, "사거리",
                definition.attackRange.ToString());
            DrawTooltipStat(
                stats, 2, 1, "이동",
                (definition.moveSpeedTilesPerSecond *
                 owned.moveSpeedMultiplier).ToString("0.00") + "  " +
                MonsterStatQuality.StatGrade(
                    owned,MonsterIndividualStat.MoveSpeed));
            DrawTooltipStat(
                stats, 3, 1, "잠금",
                owned.locked ? "ON" : "OFF");

            DrawSpeciesProgression(new Rect(panel.x + 20f, panel.y + 328f,
                panel.width - 40f, 72f), definition, owned);
            DrawMonsterExchangeButton(new Rect(panel.x + 20f, panel.yMax - 63f,
                panel.width - 40f, 28f), owned);
            GUI.Label(
                new Rect(panel.x + 20f, panel.yMax - 31f,
                    panel.width - 40f, 18f),
                "빈 곳을 클릭하거나 ESC를 누르면 닫힙니다.",
                BombBrickUiTheme.CaptionStyle(
                    10, TextAnchor.MiddleCenter));

            if (personalityHovered)
            {
                PersonalityMasterRow personality =
                    MonsterAbilityTooltipContent.Personality(
                        owned.personalityId);
                BombBrickUiTheme.DrawHoverTooltip(
                    Event.current.mousePosition, "전투 AI 성격",
                    MonsterInstanceGenerator.PersonalityDisplayName(
                        owned.personalityId),
                    MonsterAbilityTooltipContent.PersonalitySummary(
                        personality),
                    BombBrickUiTheme.Orange);
            }
            else if (traitHovered)
            {
                TraitMasterRow master =
                    MonsterAbilityTooltipContent.Trait(owned.intrinsicTraitId);
                BombBrickUiTheme.DrawHoverTooltip(
                    Event.current.mousePosition,
                    MonsterAbilityTooltipContent.TraitCategory(master),
                    intrinsic == null ? owned.intrinsicTraitId : intrinsic.displayName,
                    MonsterAbilityTooltipContent.TraitSummary(master),
                    BombBrickUiTheme.Cyan);
            }
            else if (skillHovered)
            {
                BombBrickUiTheme.DrawHoverTooltip(
                    Event.current.mousePosition, "고유 스킬",
                    skill == null ? definition.skillId : skill.displayName,
                    MonsterAbilityTooltipContent.SkillSummary(skill),
                    BombBrickUiTheme.Violet);
            }
        }

        private static void DrawTooltipStat(
            Rect panel,
            int column,
            int row,
            string label,
            string value)
        {
            float width = panel.width / 4f;
            float height = panel.height / 2f;
            Rect cell = new Rect(
                panel.x + column * width,
                panel.y + row * height,
                width,
                height);
            GUI.Label(
                new Rect(cell.x + 4f, cell.y + 2f, cell.width - 8f, 15f),
                label,
                BombBrickUiTheme.CaptionStyle(
                    9, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(cell.x + 4f, cell.y + 17f, cell.width - 8f, 20f),
                value,
                BombBrickUiTheme.TitleStyle(
                    12, TextAnchor.MiddleCenter));
        }

        private void DrawDraggedMonsterGhost()
        {
            if (!draggingMonster ||
                string.IsNullOrWhiteSpace(pressedMonsterInstanceId))
                return;
            OwnedMonsterSave owned = FindOwned(pressedMonsterInstanceId);
            if (owned == null)
                return;
            Vector2 pointer = Event.current.mousePosition;
            Rect ghost = new Rect(
                pointer.x - 48f,
                pointer.y - 48f,
                96f,
                112f);
            BombBrickUiTheme.DrawCard(
                ghost, BombBrickUiTheme.Lime, true, false);
            DrawMonsterPortrait(
                new Rect(ghost.x + 9f, ghost.y + 9f, 78f, 78f),
                owned);
            GUI.Label(
                new Rect(ghost.x + 5f, ghost.yMax - 23f, ghost.width - 10f, 18f),
                MonsterName(owned),
                BombBrickUiTheme.TitleStyle(
                    11, TextAnchor.MiddleCenter));
        }

        private static Rect Intersection(Rect first, Rect second)
        {
            float xMin = Mathf.Max(first.xMin, second.xMin);
            float yMin = Mathf.Max(first.yMin, second.yMin);
            float xMax = Mathf.Min(first.xMax, second.xMax);
            float yMax = Mathf.Min(first.yMax, second.yMax);
            return xMax <= xMin || yMax <= yMin
                ? Rect.zero
                : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static string FamilyDisplayName(string familyId)
        {
            return HunterShopSystem.FamilyDisplayName(familyId);
        }

        private void DrawMonsterPortrait(Rect rect, OwnedMonsterSave owned)
        {
            BombBrickUiTheme.Fill(rect, new Color(0.015f, 0.022f, 0.036f, 1f));
            if (owned == null)
                return;
            MonsterDefinition definition = database.Get(owned.monsterId);
            if (definition == null)
                return;
            Sprite portrait = GameResourceCache.GetMonsterIdleSprite(definition, Time.unscaledTime,
                owned.isShiny && !string.IsNullOrEmpty(definition.shinyResourcePath)
                    ? definition.shinyResourcePath : definition.resourcePath);
            if (portrait != null)
            {
                Rect textureRect = portrait.textureRect;
                Rect uv = new Rect(
                    textureRect.x / portrait.texture.width,
                    textureRect.y / portrait.texture.height,
                    textureRect.width / portrait.texture.width,
                    textureRect.height / portrait.texture.height);
                GUI.DrawTextureWithTexCoords(
                    rect, portrait.texture, uv, true);
            }
            BombBrickUiTheme.Frame(rect, BombBrickUiTheme.Border, 1f);
        }

        private void DrawEncyclopediaPortrait(
            Rect rect, MonsterDefinition definition, bool silhouette)
        {
            BombBrickUiTheme.Fill(
                rect,
                silhouette
                    ? new Color(0.14f, 0.16f, 0.12f, 1f)
                    : new Color(0.015f, 0.022f, 0.036f, 1f));
            Sprite portrait = ResolveMonsterPortrait(definition);
            if (portrait != null)
            {
                Rect textureRect = portrait.textureRect;
                Rect uv = new Rect(
                    textureRect.x / portrait.texture.width,
                    textureRect.y / portrait.texture.height,
                    textureRect.width / portrait.texture.width,
                    textureRect.height / portrait.texture.height);
                Color previous = GUI.color;
                GUI.color = silhouette ? Color.black : Color.white;
                GUI.DrawTextureWithTexCoords(
                    rect, portrait.texture, uv, true);
                GUI.color = previous;
            }
            BombBrickUiTheme.Frame(
                rect,
                silhouette
                    ? BombBrickUiTheme.Border
                    : BombBrickUiTheme.Cyan,
                2f);
        }

        private static void DrawHabitatGlyph(Rect rect, Color color)
        {
            float block = Mathf.Floor(Mathf.Min(
                rect.width, rect.height) / 4f);
            BombBrickUiTheme.Fill(
                new Rect(rect.x, rect.y + block, block, block * 2f), color);
            BombBrickUiTheme.Fill(
                new Rect(rect.x + block, rect.y,
                    block * 2f, block * 3f), color);
            BombBrickUiTheme.Fill(
                new Rect(rect.x + block * 3f, rect.y + block,
                    block, block * 2f), color);
            BombBrickUiTheme.Fill(
                new Rect(rect.x + block, rect.y + block,
                    block, block),
                new Color(0.04f, 0.06f, 0.04f, 1f));
        }

        private Sprite ResolveMonsterPortrait(MonsterDefinition definition)
        {
            return GameResourceCache.GetMonsterIdleSprite(
                definition, Time.unscaledTime);
        }

        private OwnedMonsterSave FindOwned(string instanceId)
        {
            return profile.monsters.Find(
                item => item != null && item.instanceId == instanceId);
        }

        private string MonsterName(OwnedMonsterSave owned)
        {
            if (owned == null)
                return "빈 슬롯";
            if (!string.IsNullOrWhiteSpace(owned.customName))
                return owned.customName;
            MonsterDefinition definition = database.Get(owned.monsterId);
            return definition == null ? owned.monsterId : definition.displayName;
        }

        private void DrawLoadFailure()
        {
            Rect panel = Centered(560f, 220f);
            BombBrickUiTheme.DrawPanel(panel, true);
            GUI.Label(
                new Rect(panel.x + 24f, panel.y + 34f, panel.width - 48f, 44f),
                "마을 데이터를 불러오지 못했습니다.",
                BombBrickUiTheme.TitleStyle(
                    22, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(panel.x + 24f, panel.y + 86f, panel.width - 48f, 46f),
                feedback,
                BombBrickUiTheme.BodyStyle(
                    13, TextAnchor.MiddleCenter));
            if (BombBrickUiTheme.Button(
                new Rect(panel.center.x - 100f, panel.yMax - 58f, 200f, 38f),
                "다시 불러오기",
                true,
                true))
                ReloadTownData();
        }
    }
}
