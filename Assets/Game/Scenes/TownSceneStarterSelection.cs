using System;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;
using GUI = LocalizedGUI;

namespace BombBrick.Game.Scenes
{
    public sealed partial class TownSceneController
    {
        private string hoveredGrowthTrait;
        private ChestRewardPresentation starterReveal;
        private int starterGuideStep;
        private bool StarterGuideActive => profile != null && profile.starterSelectionPending &&
            profile.firstExpeditionGuideStep == GuideIntroduceCollection && starterGuideStep < 3;
        private bool StarterSelectionButtonsEnabled => !StarterGuideActive;

        private void AdvanceStarterGuide()
        {
            if (StarterGuideActive) starterGuideStep++;
        }
        private readonly List<OwnedMonsterSave> starterFixedMonsters = new List<OwnedMonsterSave>();

        private void BeginStarterReveal()
        {
            starterFixedMonsters.Clear();
            starterFixedMonsters.AddRange(profile.monsters.Where(m =>
                m.instanceId != PlayerProfileStore.MemorialChundongiInstanceId &&
                !profile.starterRerollInstanceIds.Contains(m.instanceId)).Take(6));
            var candidates = profile.starterRerollInstanceIds.Select(FindOwned).Where(m => m != null).ToList();
            starterReveal = new ChestRewardPresentation(candidates, false, false, RegionId, null);
        }

        private void DrawSpeciesProgression(Rect rect, MonsterDefinition definition, OwnedMonsterSave owned = null)
        {
            if (definition == null) return;
            ExpeditionMonsterStateSave current = owned == null ? null :
                activeRun?.monsters.FirstOrDefault(m => m.instanceId == owned.instanceId);
            for (int tier = 2; tier <= 3; tier++)
            {
                string saved = tier == 2 ? current?.rank2TraitId : current?.rank3TraitId;
                MonsterTraitDefinition trait = string.IsNullOrEmpty(saved)
                    ? ExpeditionTraitProgression.OwnedTrait(definition, owned, tier) : MonsterTraitCatalog.Get(saved);
                if (trait == null) continue;
                Rect row = new Rect(rect.x, rect.y + (tier - 2) * rect.height / 2f,
                    rect.width, rect.height / 2f - 2f);
                Color accent = tier == 2 ? BombBrickUiTheme.Cyan : BombBrickUiTheme.Violet;
                BombBrickUiTheme.Fill(new Rect(row.x, row.y + 3f, 3f, row.height - 6f), accent);
                GUI.Label(new Rect(row.x + 10f, row.y, row.width - 10f, row.height),
                    tier + "★  " + GameLocalization.Text(trait.displayName), BombBrickUiTheme.BodyStyle(12));
                if (row.Contains(Event.current.mousePosition)) hoveredGrowthTrait = trait.id;
            }
        }

        private void DrawGrowthTraitHover()
        {
            if (string.IsNullOrEmpty(hoveredGrowthTrait)) return;
            TraitMasterRow trait = MonsterAbilityTooltipContent.Trait(hoveredGrowthTrait);
            if (trait != null)
                BombBrickUiTheme.DrawHoverTooltip(Event.current.mousePosition,
                    "성장 특성", trait.displayName,
                    MonsterAbilityTooltipContent.TraitSummary(trait), BombBrickUiTheme.Violet);
        }

        private void DrawStarterSelection(Rect viewport)
        {
            hoveredGrowthTrait = null;
            BombBrickUiTheme.DrawPanel(viewport, true);
            float x = viewport.x + 24f, width = viewport.width - 48f;
            GUI.Label(new Rect(x, viewport.y + 22f, width, 40f), "첫 탐험의 동료", BombBrickUiTheme.TitleStyle(28));
            GUI.Label(new Rect(x, viewport.y + 68f, width, 26f),
                "다시 뽑기마다 동료 8마리의 종류·능력치·성격·특성을 모두 새로 정합니다.",
                BombBrickUiTheme.BodyStyle(16));
            GUI.Label(new Rect(x, viewport.y + 106f, width, 24f), "기본 동료 · 종류 무작위 · 평균 B−~B+ · 이로치 없음", BombBrickUiTheme.TitleStyle(17));
            if (starterReveal == null) BeginStarterReveal();
            if (Event.current.type == EventType.Repaint && GameFlowController.CanPresentScene && !GameFrontend.BlocksGameplay)
                starterReveal.BeginPresentation();
            var fixedMonsters = starterFixedMonsters;
            float gap = 12f, smallWidth = (width - gap * 5f) / 6f;
            for (int i = 0; i < fixedMonsters.Count; i++)
            {
                Rect card = new Rect(x + i * (smallWidth + gap), viewport.y + 142f, smallWidth, 284f);
                OwnedMonsterSave monster = fixedMonsters[i];
                BombBrickUiTheme.DrawCard(card, BombBrickUiTheme.Cyan, false, false);
                MonsterDefinition definition = database.Get(monster.monsterId);
                DrawMonsterPortrait(new Rect(card.x + 12f, card.y + 12f, 60f, 60f), monster);
                GUI.Label(new Rect(card.x + 82f, card.y + 16f, card.width - 94f, 28f), MonsterName(monster), BombBrickUiTheme.TitleStyle(16));
                GUI.Label(new Rect(card.x + 82f, card.y + 46f, card.width - 94f, 24f),
                    MonsterInstanceGenerator.PersonalityDisplayName(monster.personalityId), BombBrickUiTheme.CaptionStyle(12));
                GUI.Label(new Rect(card.x + 82f, card.y + 73f, card.width - 94f, 23f),
                    GameLocalization.Text("평균 ") + MonsterStatQuality.OverallGrade(monster), BombBrickUiTheme.CaptionStyle(12));
                Rect stats = new Rect(card.x + 8f, card.y + 100f, card.width - 16f, 82f);
                MonsterAcquisitionReveal.DrawStats(stats, definition, monster, MonsterAcquisitionReveal.ReadyAt, 4, 13);
                if (definition != null) DrawTooltipStat(stats, 2, 1, "사거리", definition.attackRange.ToString());
                DrawSpeciesProgression(new Rect(card.x + 12f, card.y + 194f, card.width - 24f, 76f), definition, monster);
            }
            GUI.Label(new Rect(x, viewport.y + 440f, width, 26f), "추가 동료 · 능력치·이로치 무작위", BombBrickUiTheme.TitleStyle(19));
            float top = viewport.y + 482f;
            float height = viewport.height - 638f;
            for (int slot = 0; slot < 2; slot++)
            {
                OwnedMonsterSave monster = slot < profile.starterRerollInstanceIds.Count ? FindOwned(profile.starterRerollInstanceIds[slot]) : null;
                if (monster == null) continue;
                Rect card = new Rect(x + slot * (width + gap) / 2f, top, (width - gap) / 2f, height);
                Matrix4x4 previous = GUI.matrix;
                try { DrawStarterCandidate(starterReveal.AnimateCard(card, slot), monster, slot); }
                finally { GUI.matrix = previous; }
            }
            GUI.Label(new Rect(x, viewport.yMax - 140f, width, 26f),
                "8마리 모두 종류까지 다시 뽑습니다. 기본 6마리는 평균 B−~B+·이로치 없음, 추가 2마리는 기존 추첨 규칙을 따릅니다.", BombBrickUiTheme.BodyStyle(14));
            GUI.Label(new Rect(x, viewport.yMax - 108f, width, 26f),
                string.IsNullOrEmpty(feedback)
                    ? "확정한 특성은 계속 유지됩니다. 동료를 확정하면 다시 뽑기는 종료됩니다." : feedback,
                BombBrickUiTheme.CaptionStyle(14));
            Rect rerollButton = new Rect(x, viewport.yMax - 70f, 432f, 46f);
            Rect confirmButton = new Rect(viewport.xMax - 460f, viewport.yMax - 70f, 432f, 46f);
            if (starterReveal.Elapsed < starterReveal.ReadyAt && StarterSelectionButtonsEnabled &&
                BombBrickUiTheme.Button(new Rect(viewport.xMax - 220f, viewport.y + 100f, 196f, 30f),
                    GameLocalization.TextByKey("reward.acquisition.skip", "결과 바로 보기"), true, false, 14))
                starterReveal.CompleteReveal();
            if (BombBrickUiTheme.Button(rerollButton, "전체 다시 뽑기", StarterSelectionButtonsEnabled, false, 18))
            {
                try { profileStore.RerollStarters(profile, database); BeginStarterReveal(); feedback = ""; }
                catch (Exception exception)
                {
                    feedback = "저장하지 못했습니다. 다시 시도해 주세요.";
                    Debug.LogException(exception);
                }
            }
            if (BombBrickUiTheme.Button(confirmButton,
                profile.firstExpeditionGuideStep >= GuideComplete
                    ? "동료 확정 · 탐험 준비" : "동료 확정 · 튜토리얼 시작",
                StarterSelectionButtonsEnabled && starterReveal.Elapsed >= starterReveal.ReadyAt, true, 18))
            {
                try
                {
                    profileStore.ConfirmStarters(profile);
                    starterReveal = null;
                    ReloadTownData();
                    feedback = "";
                    guideStepChangedAt = Time.unscaledTime;
                    screenRevealStartedAt = Time.unscaledTime;
                }
                catch (Exception exception)
                {
                    feedback = "저장하지 못했습니다. 다시 시도해 주세요.";
                    Debug.LogException(exception);
                }
            }
            DrawGrowthTraitHover();
            DrawStarterSelectionGuide(viewport,
                Rect.MinMaxRect(x, viewport.y + 142f, x + width, viewport.yMax - 156f),
                rerollButton, confirmButton);
        }

        private void DrawStarterSelectionGuide(Rect viewport, Rect cards, Rect rerollButton, Rect confirmButton)
        {
            if (!StarterGuideActive) return;
            Rect target;
            string message;
            if (starterGuideStep == 0)
            {
                target = cards;
                message = "탐험을 함께할 동료 8마리를 먼저 뽑습니다.\n" +
                    "기본 6마리는 평균 B−~B+·이로치 없음, 추가 2마리는 능력치·이로치가 무작위입니다.";
            }
            else if (starterGuideStep == 1)
            {
                target = rerollButton;
                message = "전체 다시 뽑기를 누르면 8마리 모두 다시 뽑습니다.\n" +
                    "종류·능력치·성격·성장 특성이 바뀌니 원하는 조합을 찾아보세요.";
            }
            else
            {
                target = confirmButton;
                message = "원하는 동료를 골랐다면 동료 확정을 누르세요.\n" +
                    "확정한 성장 특성은 유지되며, 확정 후에는 다시 뽑을 수 없습니다.";
            }
            DrawGuideShade(new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height), target, .68f);
            DrawGuideFrame(target, BombBrickUiTheme.Cyan);
            float width = Mathf.Min(1060f, viewport.width - 32f);
            Rect hint = new Rect(Mathf.Clamp(target.center.x - width * .5f,
                viewport.x + 8f, viewport.xMax - width - 8f),
                starterGuideStep == 0 ? viewport.y + 56f : target.y - 94f, width, 80f);
            DrawHunterPixelCard(hint, BombBrickUiTheme.Cyan, false);
            GUI.Label(new Rect(hint.x + 16f, hint.y + 12f, 52f, 56f),
                (starterGuideStep + 1) + " / 3", BombBrickUiTheme.CaptionStyle(14));
            GUI.Label(new Rect(hint.x + 76f, hint.y + 12f,
                hint.width - 212f, 56f),
                message, BombBrickUiTheme.BodyStyle(14, TextAnchor.MiddleLeft));
            if (BombBrickUiTheme.Button(
                new Rect(hint.xMax - 112f, hint.y + 18f, 92f, 44f),
                starterGuideStep == 2 ? "닫기" : "다음", true, true, 15))
                AdvanceStarterGuide();
        }

        private void DrawStarterCandidate(Rect card, OwnedMonsterSave monster, int slot)
        {
            MonsterDefinition definition = database.Get(monster.monsterId);
            float age = starterReveal.SinceOpen(slot);
            Color cardAccent = MonsterAcquisitionReveal.GradeVisible(age) ? MonsterAcquisitionReveal.GradeColor(monster) :
                monster.isShiny ? BombBrickUiTheme.Lime : BombBrickUiTheme.Violet;
            BombBrickUiTheme.DrawCard(card, cardAccent, false, false);
            starterReveal.DrawCandidateReveal(card, slot);
            if (age < .12f) return;
            float lift = Mathf.Sin(Mathf.Clamp01((age - .12f) / .42f) * Mathf.PI) * 42f;
            int quality = MonsterAcquisitionReveal.QualityTier(monster);
            float stamp = MonsterAcquisitionReveal.GradeImpact(age, quality, monster.isShiny);
            Color accent = MonsterAcquisitionReveal.GradeVisible(age)
                ? MonsterAcquisitionReveal.GradeColor(quality) : BombBrickUiTheme.Violet;
            if (stamp > 0f)
            {
                BombBrickUiTheme.Fill(new Rect(card.x + 4f, card.y + 2f, card.width - 6f, card.height - 4f),
                    new Color(accent.r, accent.g, accent.b, stamp * .18f));
                BombBrickUiTheme.Frame(card, new Color(accent.r, accent.g, accent.b, stamp), 3f);
            }
            float portraitSize = 108f * (1f + stamp * .14f);
            DrawMonsterPortrait(new Rect(card.x + 78f - portraitSize * .5f,
                card.y + 78f - lift - portraitSize * .5f, portraitSize, portraitSize), monster);
            if (age < .3f) return;
            GUI.Label(new Rect(card.x + 150f, card.y + 22f, card.width - 610f, 36f), MonsterName(monster), BombBrickUiTheme.TitleStyle(25));
            GUI.Label(new Rect(card.x + 150f, card.y + 66f, card.width - 610f, 28f),
                MonsterInstanceGenerator.PersonalityDisplayName(monster.personalityId), BombBrickUiTheme.BodyStyle(18));
            MonsterAcquisitionReveal.DrawGradeStamp(new Rect(card.xMax - 390f, card.y + 42f, 280f, 54f),
                monster, age, 27);
            Rect stats = new Rect(card.x + 24f, card.y + 150f, card.width - 48f, 108f);
            DrawStarterStats(stats, monster, age);
            if (age >= MonsterAcquisitionReveal.StampAt)
                DrawSpeciesProgression(new Rect(card.x + 24f, card.y + 278f, card.width - 48f, 76f), definition, monster);
        }

        private void DrawStarterStats(Rect stats, OwnedMonsterSave monster, float age)
        {
            MonsterDefinition definition = database.Get(monster.monsterId);
            if (definition == null) return;
            MonsterAcquisitionReveal.DrawStats(stats, definition, monster, age, 4, 20);
            if (age >= MonsterAcquisitionReveal.StampAt)
                DrawTooltipStat(stats, 2, 1, "사거리", definition.attackRange.ToString());
        }
    }
}
