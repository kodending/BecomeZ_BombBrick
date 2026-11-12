using BombBrick.Game.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GUI = LocalizedGUI;

namespace BombBrick.Game.Scenes
{
    public sealed class ResultSceneController : ProductionSceneController
    {
        private MonsterDatabase database;
        private PlayerProfileSave profile;
        private PlayerProfileStore profileStore;
        private ChestRewardPresentation chestReward;
        private string chestRewardRunId;
        private float screenRevealStartedAt;
        private float displayedPlayerExperience;
        private int playerExperienceStart;
        private int playerExperienceTarget;
        private int playerExperienceEarned;
        private int playerLevelStart;
        private float playerExperienceAnimationStartsAt;
        private float playerExperiencePauseUntil;
        private float playerLevelPulseStartedAt = -10f;
        private bool playerExperienceReady;
        private bool scenePresentationStarted;

        private void Start()
        {
            screenRevealStartedAt = Time.unscaledTime;
            database = GameResourceCache.Database;
            profileStore = new PlayerProfileStore();
            if (database != null && !GameFrontend.TryLoadPlayerProfile(
                    profileStore, database, out profile))
                return;
            InitializePlayerExperienceAnimation();
        }

        private void Update()
        {
            if (!playerExperienceReady || !GameFlowController.CanPresentScene || GameFrontend.BlocksGameplay) return;
            if (!scenePresentationStarted)
            {
                scenePresentationStarted = true;
                screenRevealStartedAt = Time.unscaledTime;
                playerExperienceAnimationStartsAt = Time.unscaledTime + 0.8f;
            }
            chestReward?.Update();
            if (chestReward != null && chestReward.Finished) chestReward = null;
            if (!playerExperienceReady ||
                displayedPlayerExperience >= playerExperienceTarget ||
                Time.unscaledTime < playerExperienceAnimationStartsAt ||
                Time.unscaledTime < playerExperiencePauseUntil)
                return;

            int current = Mathf.FloorToInt(displayedPlayerExperience);
            int level = PermanentResearchSystem.LevelForExperience(current);
            int nextThreshold = level >= PermanentResearchSystem.MaximumLevel
                ? playerExperienceTarget
                : PermanentResearchSystem.ExperienceForLevel(level + 1);
            int stepTarget = Mathf.Min(playerExperienceTarget, nextThreshold);
            float total = Mathf.Max(1f,
                playerExperienceTarget - playerExperienceStart);
            float levelUps = Mathf.Max(0,
                GameFlowController.Instance.Run.PlayerLevelAfter - playerLevelStart);
            float duration = Mathf.Clamp(2.3f + levelUps * 0.45f, 2.3f, 5.8f);
            float speed = Mathf.Max(24f, total / duration);
            displayedPlayerExperience = Mathf.MoveTowards(
                displayedPlayerExperience, stepTarget,
                speed * Time.unscaledDeltaTime);

            int advancedLevel = PermanentResearchSystem.LevelForExperience(
                Mathf.FloorToInt(displayedPlayerExperience));
            if (advancedLevel > level)
            {
                playerLevelPulseStartedAt = Time.unscaledTime;
                if (stepTarget < playerExperienceTarget)
                    playerExperiencePauseUntil = Time.unscaledTime + 0.42f;
            }
        }

        private void InitializePlayerExperienceAnimation()
        {
            GameRunSession run = GameFlowController.Instance.Run;
            int savedExperience = profile == null
                ? 0
                : profile.permanentResearchExperience;
            playerExperienceStart = run.HasPlayerExperienceSettlement
                ? run.PlayerExperienceBefore
                : savedExperience;
            playerExperienceTarget = run.HasPlayerExperienceSettlement
                ? run.PlayerExperienceAfter
                : savedExperience;
            playerExperienceEarned = run.HasPlayerExperienceSettlement
                ? run.PlayerExperienceEarned
                : 0;
            playerLevelStart = PermanentResearchSystem.LevelForExperience(
                playerExperienceStart);
            displayedPlayerExperience = playerExperienceStart;
            playerExperienceAnimationStartsAt = Time.unscaledTime + 0.8f;
            playerExperienceReady = true;
        }

        private void OnGUI()
        {
            using (new BombBrickUiLayout.GuiScope(true)) DrawSceneGui();
        }

        private void DrawSceneGui()
        {
            if (GameFrontend.BlocksGuiInput) return;
            if (chestReward != null)
            {
                int choice = chestReward.Draw(database);
                if (choice >= 0)
                {
                    if (HunterShopSystem.TryClaimRegionBoxReward(profile, profileStore, database,
                            chestRewardRunId, choice, out _, out string error))
                        chestReward.Confirm();
                    else chestReward.Error = error;
                }
                return;
            }
            BombBrickUiTheme.DrawModalBackdrop();
            GameRunSession run = GameFlowController.Instance.Run;
            bool cleared = run.Result == ChallengeResult.Cleared;
            RegionBoxRewardSave reward = cleared ? profile?.pendingRegionBoxRewards?.LastOrDefault(
                item => item != null && item.regionId == run.RegionId && item.candidates != null && item.candidates.Count > 0) : null;
            Color accent = cleared
                ? BombBrickUiTheme.Lime
                : BombBrickUiTheme.Red;

            bool hasCaptures =
                GameFlowController.Instance.Run.CapturedMonsterInstanceIds.Count > 0;
            Rect panel = Centered(
                Mathf.Min(700f, BombBrickUiLayout.Width - 32f),
                Mathf.Min(hasCaptures ? 620f : 550f, BombBrickUiLayout.Height - 32f));
            float reveal = BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(
                scenePresentationStarted ? (Time.unscaledTime - screenRevealStartedAt) / 0.32f : 0f));
            panel.y += (1f - reveal) * 14f;
            BombBrickUiTheme.DrawPanel(panel, true);
            BombBrickUiTheme.Fill(
                new Rect(panel.x, panel.y, panel.width, 5f), accent);
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 28f, panel.width - 64f, 18f),
                HunterRegionCatalog.Get(run.RegionId).displayName,
                BombBrickUiTheme.CaptionStyle(
                    11, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 52f, panel.width - 64f, 48f),
                cleared ? "탐험 완료" : "탐험 실패",
                BombBrickUiTheme.DisplayStyle(34));
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 103f, panel.width - 64f, 28f),
                cleared ? "12 WAVE 돌파" : "탐험 종료",
                BombBrickUiTheme.BodyStyle(
                    14, TextAnchor.MiddleCenter));

            Rect stats = new Rect(
                panel.x + 42f,
                panel.y + 140f,
                panel.width - 84f,
                92f);
            BombBrickUiTheme.Fill(stats, BombBrickUiTheme.Surface);
            BombBrickUiTheme.Frame(stats, BombBrickUiTheme.Border, 1f);
            DrawStat(
                stats, 0, GameLocalization.TextByKey(
                    "result.waves_cleared", "돌파"),
                run.CompletedWave + " / " + run.TotalWaves);
            DrawStat(
                stats, 1, "남은 목숨",
                run.RemainingLives.ToString());
            DrawStat(
                stats, 2, "최종 골드",
                run.Gold.ToString());
            DrawStat(
                stats, 3, "진행 시간",
                FormatTime(run.ElapsedSeconds));
            DrawStat(
                stats, 4, "사냥의 증표",
                "+" + run.HunterTokensEarned);

            DrawPlayerExperience(new Rect(
                panel.x + 42f,
                panel.y + 246f,
                panel.width - 84f,
                96f));

            float noteY = panel.y + 358f;
            if (hasCaptures)
            {
                DrawCapturedMonsters(
                    new Rect(
                        panel.x + 42f,
                        panel.y + 354f,
                        panel.width - 84f,
                        126f));
                noteY = panel.y + 492f;
            }
            Rect note = new Rect(
                panel.x + 42f,
                noteY,
                panel.width - 84f,
                hasCaptures ? 54f : 70f);
            BombBrickUiTheme.Fill(
                note, new Color(0.025f, 0.036f, 0.056f, 0.96f));
            BombBrickUiTheme.Frame(
                note,
                new Color(accent.r, accent.g, accent.b, 0.42f),
                1f);
            bool hasRareNetReward=run.RareCaptureNetsEarned>0;
            if(hasRareNetReward)
            {
                Texture2D rareNetIcon=GameResourceCache.GetTexture(
                    "UI/CaptureNetIcons/capture_net_rare");
                if(rareNetIcon!=null)
                    GUI.DrawTexture(
                        new Rect(note.x+14f,note.center.y-27f,54f,54f),
                        rareNetIcon,ScaleMode.ScaleToFit,true);
            }
            GUI.Label(
                new Rect(
                    note.x+(hasRareNetReward?76f:20f),
                    note.y+7f,
                    note.width-(hasRareNetReward?96f:40f),
                    note.height-14f),
                hasRareNetReward
                    ? "강화 포획 그물 +"+run.RareCaptureNetsEarned+
                      "\n포획은 영구 보존 · 탐험 강화는 초기화"
                    : "포획은 영구 보존 · 탐험 강화는 초기화",
                BombBrickUiTheme.BodyStyle(
                    13, TextAnchor.MiddleCenter));

            if (BombBrickUiTheme.Button(
                new Rect(
                    panel.center.x - 150f,
                    panel.yMax - 60f,
                    300f,
                    42f),
                reward == null ? "마을로 돌아가기" : GameLocalization.TextByKey("reward.chest.open_reward", "지역 상자 개봉"),
                !GameFlowController.Instance.IsTransitioning,
                true))
            {
                if (reward == null) GameFlowController.Instance.ReturnToTown();
                else
                {
                    chestRewardRunId = reward.runId;
                    chestReward = new ChestRewardPresentation(reward.candidates, true, false, reward.regionId,
                        new HashSet<string>(profile.monsters.Select(monster => monster.monsterId)));
                }
            }
        }

        private void DrawPlayerExperience(Rect panel)
        {
            int experience = Mathf.FloorToInt(displayedPlayerExperience);
            int level = PermanentResearchSystem.LevelForExperience(experience);
            int levelStart = PermanentResearchSystem.ExperienceForLevel(level);
            int needed = level >= PermanentResearchSystem.MaximumLevel
                ? 0
                : PermanentResearchSystem.ExperienceForLevel(level + 1) - levelStart;
            int into = Mathf.Max(0, experience - levelStart);
            float ratio = level >= PermanentResearchSystem.MaximumLevel
                ? 1f
                : needed <= 0 ? 0f : Mathf.Clamp01((float)into / needed);
            float pulse = Mathf.Clamp01(
                1f - (Time.unscaledTime - playerLevelPulseStartedAt) / 0.55f);
            Color progressColor = Color.Lerp(
                BombBrickUiTheme.Lime, Color.white, pulse * 0.45f);

            BombBrickUiTheme.Fill(panel,
                new Color(0.025f, 0.036f, 0.020f, 0.98f));
            BombBrickUiTheme.Frame(panel,
                new Color(progressColor.r, progressColor.g,
                    progressColor.b, 0.68f), 1f);
            GUI.Label(
                new Rect(panel.x + 16f, panel.y + 8f,
                    panel.width * 0.55f, 20f),
                GameLocalization.TextByKey(
                    "result.player_experience", "플레이어 경험치"),
                BombBrickUiTheme.TitleStyle(14));
            GUI.Label(
                new Rect(panel.x + panel.width * 0.55f, panel.y + 8f,
                    panel.width * 0.45f - 16f, 20f),
                GameLocalization.Format(
                    "result.experience_gained", "+{0} EXP", playerExperienceEarned),
                BombBrickUiTheme.TitleStyle(14, TextAnchor.MiddleRight));

            Rect levelBadge = new Rect(
                panel.x + 16f, panel.y + 37f, 86f, 32f);
            BombBrickUiTheme.Badge(
                levelBadge,
                GameLocalization.Format(
                    "result.player_level", "LV. {0}", level),
                progressColor);
            Rect bar = new Rect(
                levelBadge.xMax + 14f, panel.y + 43f,
                panel.width - 132f, 17f);
            BombBrickUiTheme.Fill(bar, BombBrickUiTheme.Surface);
            BombBrickUiTheme.Fill(
                new Rect(bar.x, bar.y, bar.width * ratio, bar.height),
                progressColor);
            BombBrickUiTheme.Frame(bar, BombBrickUiTheme.Border, 1f);

            string progressLabel = level >= PermanentResearchSystem.MaximumLevel
                ? GameLocalization.TextByKey(
                    "result.maximum_level", "최대 레벨")
                : GameLocalization.Format(
                    "result.experience_progress", "EXP {0} / {1}", into, needed);
            GUI.Label(
                new Rect(bar.x, panel.y + 65f, bar.width, 18f),
                progressLabel,
                BombBrickUiTheme.CaptionStyle(10, TextAnchor.MiddleCenter));

            int visibleLevelGain = Mathf.Max(0, level - playerLevelStart);
            if (visibleLevelGain > 0)
                GUI.Label(
                    new Rect(panel.x + 16f, panel.y + 70f, 100f, 16f),
                    GameLocalization.Format(
                        "result.research_points_gained", "연구 포인트 +{0}",
                        visibleLevelGain),
                    BombBrickUiTheme.CaptionStyle(
                        10, TextAnchor.MiddleCenter));
        }

        private void DrawCapturedMonsters(Rect panel)
        {
            if (database == null || profile == null)
                return;
            IReadOnlyList<string> capturedIds =
                GameFlowController.Instance.Run.CapturedMonsterInstanceIds;
            List<OwnedMonsterSave> captured = capturedIds
                .Select(id => profile.monsters.Find(monster =>
                    monster != null && monster.instanceId == id))
                .Where(monster => monster != null)
                .ToList();

            BombBrickUiTheme.Fill(panel, BombBrickUiTheme.Surface);
            BombBrickUiTheme.Frame(panel, BombBrickUiTheme.Cyan, 1f);
            GUI.Label(
                new Rect(panel.x + 16f, panel.y + 10f, panel.width - 32f, 22f),
                "포획 성공  " + captured.Count,
                BombBrickUiTheme.TitleStyle(14));
            if (captured.Count == 0)
                return;

            int visible = Mathf.Min(6, captured.Count);
            float gap = 8f;
            float cardWidth = (panel.width - 32f - gap * (visible - 1)) /
                              visible;
            for (int index = 0; index < visible; index++)
            {
                OwnedMonsterSave owned = captured[index];
                MonsterDefinition definition = database.Get(owned.monsterId);
                Rect card = new Rect(
                    panel.x + 16f + index * (cardWidth + gap),
                    panel.y + 39f,
                    cardWidth,
                    panel.height - 51f);
                BombBrickUiTheme.DrawCard(
                    card, BombBrickUiTheme.Cyan, false, false);
                Rect portrait = new Rect(
                    card.center.x - 35f, card.y + 7f, 70f, 70f);
                Sprite sprite = ResolveCapturedMonsterPortrait(
                    definition, owned);
                if (sprite != null)
                {
                    Rect source = sprite.textureRect;
                    Rect uv = new Rect(
                        source.x / sprite.texture.width,
                        source.y / sprite.texture.height,
                        source.width / sprite.texture.width,
                        source.height / sprite.texture.height);
                    GUI.DrawTextureWithTexCoords(
                        portrait, sprite.texture, uv, true);
                }
                GUI.Label(
                    new Rect(card.x + 4f, card.yMax - 25f,
                        card.width - 8f, 18f),
                    definition == null ? owned.monsterId :
                        definition.displayName,
                    BombBrickUiTheme.CaptionStyle(
                        10, TextAnchor.MiddleCenter));
            }
            if (captured.Count > visible)
                BombBrickUiTheme.Badge(
                    new Rect(panel.xMax - 52f, panel.y + 8f, 40f, 22f),
                    "+" + (captured.Count - visible),
                    BombBrickUiTheme.Cyan);
        }

        private Sprite ResolveCapturedMonsterPortrait(
            MonsterDefinition definition, OwnedMonsterSave owned)
        {
            if (definition == null)
                return null;
            string resourcePath = owned != null && owned.isShiny &&
                                  !string.IsNullOrWhiteSpace(
                                      definition.shinyResourcePath)
                ? definition.shinyResourcePath
                : definition.resourcePath;
            return GameResourceCache.GetMonsterIdleSprite(
                definition, Time.unscaledTime, resourcePath);
        }

        private static void DrawStat(
            Rect panel,
            int index,
            string label,
            string value)
        {
            float width = panel.width / 5f;
            Rect cell = new Rect(
                panel.x + index * width,
                panel.y + 18f,
                width,
                panel.height - 36f);
            if (index > 0)
                BombBrickUiTheme.Fill(
                    new Rect(cell.x, cell.y + 8f, 1f, cell.height - 16f),
                    BombBrickUiTheme.Border);
            GUI.Label(
                new Rect(cell.x + 6f, cell.y + 3f, cell.width - 12f, 20f),
                label,
                BombBrickUiTheme.CaptionStyle(
                    10, TextAnchor.MiddleCenter));
            GUI.Label(
                new Rect(cell.x + 6f, cell.y + 29f, cell.width - 12f, 34f),
                value,
                BombBrickUiTheme.TitleStyle(
                    18, TextAnchor.MiddleCenter));
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int minutes = total / 60;
            int remainingSeconds = total % 60;
            return minutes.ToString("00") + ":" +
                remainingSeconds.ToString("00");
        }

        private static string RegionLabel(string regionId)
        {
            return string.IsNullOrWhiteSpace(regionId) ||
                   regionId == "prototype_region"
                ? "프로토타입 지역"
                : regionId;
        }
    }
}
