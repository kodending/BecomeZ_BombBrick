using System.Collections.Generic;
using UnityEngine;
using GUI = LocalizedGUI;

namespace BombBrick.Game.Core
{
    // One presentation for purchased boxes and the persisted clear-reward choice.
    public sealed class ChestRewardPresentation
    {
        public const float FirstOpenAt = 0.94f;
        public const float RevealSpacing = .18f;
        public const float RevealImpactAt = .18f;
        public static float RevealKick(float sinceOpen) => sinceOpen < RevealImpactAt ? 0f :
            Mathf.Pow(1f - Mathf.Clamp01((sinceOpen - RevealImpactAt) / .46f), 3f);
        private static float ImpactStrength(int quality, bool shiny) => shiny ? 3f : quality == 4 ? 2.3f : quality == 3 ? 1.65f : 1f;
        private static Color GradeColor(int quality) => MonsterAcquisitionReveal.GradeColor(quality);
        public float ReadyAt => candidates.Count == 0 ? 0f : RevealAt(candidates.Count - 1) + RevealHold(candidates[candidates.Count - 1]);
        public float Elapsed => Mathf.Max(0f, Time.unscaledTime - startedAt);
        public float SinceOpen(int index) => Elapsed - RevealAt(index);
        public static int QualityTier(OwnedMonsterSave monster) => MonsterAcquisitionReveal.QualityTier(monster);
        private static float RevealHold(OwnedMonsterSave monster) => MonsterAcquisitionReveal.ReadyAt +
            (monster.isShiny ? .4f : QualityTier(monster) >= 3 ? .25f : 0f);
        public float RevealAt(int index)
        {
            float time = FirstOpenAt;
            for (int i = 0; i < index; i++)
                time += RevealHold(candidates[i]) + RevealSpacing;
            return time;
        }
        public static int OpeningFrame(float sinceOpen) => Mathf.Clamp(Mathf.FloorToInt(sinceOpen / 0.038f) + 2, 0, 7);

        private readonly List<OwnedMonsterSave> candidates;
        private readonly bool[] discoveries;
        private readonly bool family;
        private readonly bool chooseOne;
        private readonly string regionId;
        private readonly Texture2D sheet;
        private readonly float shakeScale;
        private float startedAt = float.PositiveInfinity;
        private int presentedFrame = -1;
        private float confirmedAt = -1f;
        private int selected;
        private int soundMask;
        private readonly int[] statSoundMasks;
        private string hoveredTrait;
        public string Error { get; set; }
        public bool Finished => confirmedAt >= 0f && Time.unscaledTime - confirmedAt >= 0.55f;

        public ChestRewardPresentation(List<OwnedMonsterSave> monsters, bool choice, bool familyBox,
            string region, ICollection<string> discovered)
        {
            candidates = new List<OwnedMonsterSave>(monsters);
            chooseOne = choice;
            family = familyBox;
            regionId = region;
            discoveries = new bool[candidates.Count];
            statSoundMasks = new int[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                discoveries[i] = discovered != null && !discovered.Contains(candidates[i].monsterId);
            selected = candidates.Count == 1 ? 0 : -1;
            GameUserSettings settings = GameUserSettings.Load();
            shakeScale = settings.reduceFlashes ? 0f : settings.CameraShakeScale;
            sheet = GameResourceCache.GetTexture("UI/HunterShop/chest_opening_sheet");
        }

        public void Update()
        {
            if (confirmedAt >= 0f) return;
            if (float.IsPositiveInfinity(startedAt))
            {
                if (presentedFrame < 0 || Time.frameCount <= presentedFrame) return;
                startedAt = Time.unscaledTime;
            }
            float time = Elapsed;
            PlayOnce(0, time >= 0.2f, GameAudio.ChestCue.Arrival);
            PlayOnce(1, time >= 0.3f, GameAudio.ChestCue.Tension);
            PlayOnce(2, time >= 0.86f, GameAudio.ChestCue.Hold);
            bool statDue = false;
            int statIndex = 0, statQuality = 2;
            for (int i = 0; i < candidates.Count; i++)
            {
                float at = RevealAt(i);
                PlayOnce(3 + i * 2, time >= at, GameAudio.ChestCue.Open, candidates[i].isShiny, i);
                PlayOnce(4 + i * 2, time >= at + MonsterAcquisitionReveal.StampAt, GameAudio.ChestCue.Reveal, candidates[i].isShiny, i);
                for (int stat = 0; stat < MonsterAcquisitionReveal.StatCount; stat++)
                    if (time >= at + MonsterAcquisitionReveal.StatAt(stat) &&
                        (statSoundMasks[i] & (1 << stat)) == 0)
                    {
                        statSoundMasks[i] |= 1 << stat;
                        statDue = true; statIndex = stat;
                        statQuality = MonsterAcquisitionReveal.StatQualityTier(candidates[i], stat);
                    }
            }
            if (statDue)
                GameAudio.PlayChest(GameAudio.ChestCue.Stat, false, statIndex, statQuality);
        }

        public void BeginPresentation()
        {
            // First-time texture uploads belong to the painted frame, not the reveal's timer.
            if (float.IsPositiveInfinity(startedAt) && presentedFrame < 0) presentedFrame = Time.frameCount;
        }

        public void CompleteReveal()
        {
            if (Elapsed >= ReadyAt || confirmedAt >= 0f) return;
            startedAt = Time.unscaledTime - ReadyAt - .001f;
            GameAudio.PlayChest(GameAudio.ChestCue.Hold);
            int last = candidates.Count - 1;
            if (last >= 0)
                PlayOnce(4 + last * 2, true, GameAudio.ChestCue.Reveal, candidates[last].isShiny, last);
            soundMask = -1;
            for (int i = 0; i < statSoundMasks.Length; i++) statSoundMasks[i] = (1 << MonsterAcquisitionReveal.StatCount) - 1;
        }

        private void PlayOnce(int bit, bool due, GameAudio.ChestCue cue, bool shiny = false, int index = 0)
        {
            if (!due || (soundMask & (1 << bit)) != 0) return;
            soundMask |= 1 << bit;
            GameAudio.PlayChest(cue, shiny, index, index < candidates.Count ? QualityTier(candidates[index]) : 2);
        }

        public void Confirm()
        {
            if (confirmedAt >= 0f) return;
            confirmedAt = Time.unscaledTime;
            Error = null;
            GameAudio.PlayChest(GameAudio.ChestCue.Confirm, selected >= 0 && candidates[selected].isShiny);
        }

        public int Draw(MonsterDatabase database)
        {
            if (candidates.Count == 0 || database == null) return -1;
            if (Event.current.type == EventType.Repaint && GameFlowController.CanPresentScene && !GameFrontend.BlocksGameplay)
                BeginPresentation();
            float elapsed = Elapsed;
            bool ready = elapsed >= ReadyAt;
            bool confirmed = confirmedAt >= 0f;
            hoveredTrait = null;
            BombBrickUiTheme.DrawModalBackdrop();
            float width = Mathf.Min(candidates.Count == 1 ? 660f : 1560f, BombBrickUiLayout.Width - 64f);
            const float height = 800f;
            Rect panel = new Rect((BombBrickUiLayout.Width - width) * 0.5f,
                (BombBrickUiLayout.Height - height) * 0.5f, width, height);
            BombBrickUiTheme.DrawPanel(panel, true);
            Color accent = family ? BombBrickUiTheme.Violet : BombBrickUiTheme.Orange;
            BombBrickUiTheme.Fill(new Rect(panel.x, panel.y, panel.width, 4f), accent);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, 22f),
                chooseOne ? Text("clear_reward", "탐험 완료 보상") : Text("eyebrow", "새로운 동료"),
                BombBrickUiTheme.CaptionStyle(13, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(panel.x + 24f, panel.y + 48f, panel.width - 48f, 40f),
                confirmed ? Text("joined", "동료가 합류했습니다!") :
                ready ? (chooseOne ? Text("choose", "함께할 동료를 선택하세요") : Text("discovered", "새로운 동료를 만났습니다!")) : Text("opening", "상자 안에서 무언가 움직입니다…"),
                BombBrickUiTheme.TitleStyle(26, TextAnchor.MiddleCenter));

            float gap = 16f;
            float cardWidth = (panel.width - 48f - (candidates.Count - 1) * gap) / candidates.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                Rect card = new Rect(panel.x + 24f + i * (cardWidth + gap), panel.y + 110f, cardWidth, 574f);
                Matrix4x4 previousMatrix = GUI.matrix;
                Color previousColor = GUI.color;
                try
                {
                    card = AnimateCard(card, i);
                    DrawCard(card, database, i, elapsed, ready, confirmed);
                }
                finally { GUI.matrix = previousMatrix; GUI.color = previousColor; }
            }
            for (int i = 0; i < candidates.Count; i++)
                DrawScreenReveal(panel, new Vector2(panel.x + 24f + i * (cardWidth + gap) + cardWidth * .5f,
                    panel.y + 228f), i);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 696f, panel.width - 48f, 26f),
                !string.IsNullOrEmpty(Error) ? Error : confirmed ? Text("saved", "마을 보유 목록에 저장되었습니다.") :
                chooseOne ? Text("one_only", "개봉한 결과를 비교하고, 한 마리만 데려갑니다.") : Text("already_owned", "새 동료의 능력치와 고정 특성을 확인하세요."),
                BombBrickUiTheme.BodyStyle(14, TextAnchor.MiddleCenter));
            if (ready && !confirmed && BombBrickUiTheme.Button(new Rect(panel.center.x - 180f, panel.yMax - 64f, 360f, 42f),
                    chooseOne ? Text("confirm", "이 동료 데려가기") : Text("close", "확인"), selected >= 0, true, 18))
                return selected;
            if (!ready && !confirmed && BombBrickUiTheme.Button(new Rect(panel.center.x - 180f, panel.yMax - 64f, 360f, 42f),
                    GameLocalization.TextByKey("reward.acquisition.skip", "결과 바로 보기"), true, false, 18))
                CompleteReveal();
            if (!string.IsNullOrEmpty(hoveredTrait))
            {
                TraitMasterRow trait = MonsterAbilityTooltipContent.Trait(hoveredTrait);
                if (trait != null) BombBrickUiTheme.DrawHoverTooltip(Event.current.mousePosition,
                    "성장 특성", trait.displayName, MonsterAbilityTooltipContent.TraitSummary(trait), BombBrickUiTheme.Violet);
            }
            return -1;
        }

        private void DrawCard(Rect card, MonsterDatabase database, int index, float elapsed, bool ready, bool confirmed)
        {
            OwnedMonsterSave owned = candidates[index];
            MonsterDefinition definition = database.Get(owned.monsterId);
            float sinceOpen = elapsed - RevealAt(index);
            int quality = QualityTier(owned);
            bool waiting = index > 0 && sinceOpen < 0f;
            Color accent = waiting ? (family ? BombBrickUiTheme.Violet : BombBrickUiTheme.Orange) : GradeColor(quality);
            Color previous = GUI.color;
            if (confirmed && selected != index) GUI.color = new Color(1f, 1f, 1f, 0.25f);
            if (confirmed && selected == index)
                card.y -= Mathf.Sin(Mathf.Clamp01((Time.unscaledTime - confirmedAt) / 0.55f) * Mathf.PI) * 10f;
            BombBrickUiTheme.DrawCard(card, accent, selected == index && ready, false);
            Rect stage = new Rect(card.x + 18f, card.y + 10f, card.width - 36f, 216f);
            Color ground = RegionColor();
            BombBrickUiTheme.Fill(new Rect(stage.center.x - 64f, stage.yMax - 22f, 128f, 5f), new Color(ground.r, ground.g, ground.b, 0.26f));
            float size = 190f;
            if (waiting)
            {
                DrawChest(new Rect(stage.center.x - size * .5f, stage.y + 16f, size, size), 0);
                GUI.color = previous;
                return;
            }
            DrawQualityReveal(card, sinceOpen, accent, quality, owned.isShiny);
            float localElapsed = sinceOpen + FirstOpenAt;
            float arrive = BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(localElapsed / 0.22f));
            float shake = localElapsed > 0.3f && localElapsed < 0.86f ? Mathf.Round(Mathf.Sin(localElapsed * 57f + index) * 2f) : 0f;
            Rect chest = new Rect(stage.center.x - size * 0.5f + shake, stage.y + 16f - (1f - arrive) * 56f, size, size);
            if (sinceOpen < 0.4f)
            {
                Color chestColor = GUI.color;
                GUI.color = new Color(chestColor.r, chestColor.g, chestColor.b,
                    chestColor.a * (1f - Mathf.Clamp01((sinceOpen - 0.18f) / 0.22f)));
                int frame = sinceOpen >= 0f ? OpeningFrame(sinceOpen) : localElapsed > 0.7f ? 1 : 0;
                DrawChest(chest, frame);
                GUI.color = chestColor;
            }
            if (sinceOpen < 0f && localElapsed > 0.52f)
            {
                float glow = Mathf.Clamp01((localElapsed - 0.52f) / 0.34f);
                Color hint = owned.isShiny ? BombBrickUiTheme.Cyan : accent;
                BombBrickUiTheme.Fill(new Rect(chest.center.x - 34f, chest.center.y - 4f, 68f, 3f),
                    CombatFlashPresentation.Flash(new Color(hint.r, hint.g, hint.b, glow * 0.6f)));
            }
            DrawBurst(stage, sinceOpen, accent, index);
            if (sinceOpen >= 0.12f && definition != null)
            {
                float jump = Mathf.Clamp01((sinceOpen - 0.12f) / 0.42f);
                float lift = Mathf.Sin(jump * Mathf.PI) * 42f;
                Rect portrait = new Rect(stage.center.x - 78f, stage.y + 32f - lift, 156f, 156f);
                Sprite sprite = GameResourceCache.GetMonsterIdleSprite(definition, Time.unscaledTime,
                    owned.isShiny && !string.IsNullOrEmpty(definition.shinyResourcePath) ? definition.shinyResourcePath : definition.resourcePath);
                if (sprite != null)
                {
                    Color old = GUI.color;
                    if (sinceOpen < 0.2f) GUI.color = new Color(0.12f, 0.15f, 0.12f, old.a);
                    Rect src = sprite.textureRect;
                    // Keep authored proportions even for non-square monster frames.
                    float ratio = src.width / Mathf.Max(1f, src.height);
                    if (ratio > 1f) { portrait.height /= ratio; portrait.y += (156f - portrait.height) * 0.5f; }
                    else { portrait.width *= ratio; portrait.x += (156f - portrait.width) * 0.5f; }
                    GUI.DrawTextureWithTexCoords(portrait, sprite.texture,
                        new Rect(src.x / sprite.texture.width, src.y / sprite.texture.height, src.width / sprite.texture.width, src.height / sprite.texture.height), true);
                    GUI.color = old;
                }
            }
            if (sinceOpen >= 0.3f)
            {
                float opacity = Mathf.Clamp01((sinceOpen - 0.3f) / 0.18f);
                Color old = GUI.color;
                GUI.color = new Color(old.r, old.g, old.b, old.a * opacity);
                if (owned.isShiny) BombBrickUiTheme.Badge(new Rect(card.x + 12f, card.y + 12f, 80f, 24f), Text("shiny", "이로치"), BombBrickUiTheme.Lime);
                if (discoveries[index]) BombBrickUiTheme.Badge(new Rect(card.xMax - 66f, card.y + 12f, 54f, 24f), "NEW", BombBrickUiTheme.Orange);
                GUI.Label(new Rect(card.x + 14f, card.y + 222f, card.width - 28f, 32f),
                    definition == null ? owned.monsterId : definition.displayName, BombBrickUiTheme.TitleStyle(23, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(card.x + 14f, card.y + 258f, card.width - 28f, 26f),
                    MonsterInstanceGenerator.PersonalityDisplayName(owned.personalityId), BombBrickUiTheme.BodyStyle(15, TextAnchor.MiddleCenter));
                MonsterAcquisitionReveal.DrawGradeStamp(new Rect(card.center.x - Mathf.Min(140f, (card.width - 36f) * .5f),
                    card.y + 289f, Mathf.Min(280f, card.width - 36f), 30f), owned, sinceOpen, quality >= 3 ? 20 : 17);
                MonsterAcquisitionReveal.DrawStats(new Rect(card.x + 14f, card.y + 328f, card.width - 28f, 100f),
                    definition, owned, sinceOpen);
                for (int tier = 2; tier <= 3; tier++)
                {
                    MonsterTraitDefinition trait = ExpeditionTraitProgression.OwnedTrait(definition, owned, tier);
                    if (trait == null) continue;
                    Rect row = new Rect(card.x + 16f, card.y + 447f + (tier - 2) * 42f, card.width - 32f, 36f);
                    BombBrickUiTheme.Fill(new Rect(row.x, row.y + 5f, 3f, 25f), tier == 2 ? BombBrickUiTheme.Cyan : BombBrickUiTheme.Violet);
                    GUI.Label(new Rect(row.x + 10f, row.y, row.width - 10f, row.height), tier + "★  " + GameLocalization.Text(trait.displayName), BombBrickUiTheme.BodyStyle(15));
                    if (row.Contains(Event.current.mousePosition)) hoveredTrait = trait.id;
                }
                GUI.Label(new Rect(card.x + 14f, card.yMax - 38f, card.width - 28f, 25f),
                    selected == index ? Text("selected", "선택한 동료") : ready ? Text("select_hint", "클릭하여 선택") : "",
                    BombBrickUiTheme.CaptionStyle(13, TextAnchor.MiddleCenter));
                GUI.color = old;
            }
            if (ready && !confirmed && Event.current.type == EventType.MouseDown && Event.current.button == 0 && card.Contains(Event.current.mousePosition))
            {
                selected = index;
                GameAudio.Play(GameAudio.Cue.Click);
                Event.current.Use();
            }
            GUI.color = previous;
        }

        // The starter cards use the same impact, grade glow and screen burst as chest rewards.
        public Rect AnimateCard(Rect card, int index)
        {
            float age = SinceOpen(index);
            if (age < 0f) return card;
            float force = RevealKick(age) * ImpactStrength(QualityTier(candidates[index]), candidates[index].isShiny);
            float stampKick = MonsterAcquisitionReveal.GradeImpact(age, QualityTier(candidates[index]), candidates[index].isShiny);
            card.position += new Vector2(Mathf.Sin(age * 117f) * force * 4f,
                -force * 3f + Mathf.Cos(age * 83f) * force * .8f + stampKick * 3f) * shakeScale;
            GUIUtility.ScaleAroundPivot(Vector2.one * (1f + force * .014f *
                (CombatFlashPresentation.Reduced ? 0f : 1f)), card.center);
            return card;
        }

        public void DrawCandidateReveal(Rect card, int index)
        {
            if (SinceOpen(index) < 0f) return;
            int quality = QualityTier(candidates[index]);
            DrawQualityReveal(card, SinceOpen(index), GradeColor(quality), quality, candidates[index].isShiny);
            DrawBurst(card, SinceOpen(index), GradeColor(quality), index);
        }

        public void DrawScreenReveal(Rect panel, Vector2 origin, int index)
        {
            Rect screen = new Rect(0f, 0f, BombBrickUiLayout.Width, BombBrickUiLayout.Height);
            float age = SinceOpen(index) - RevealImpactAt;
            int quality = QualityTier(candidates[index]);
            bool shiny = candidates[index].isShiny;
            float duration = shiny ? 1.65f : quality >= 3 ? 1.2f : .65f;
            if (age < 0f || age >= duration) return;
            float fade = Mathf.Pow(1f - age / duration, 1.5f);
            Color accent = GradeColor(quality);
            float flash = Mathf.Clamp01(1f - age / .24f);
            BombBrickUiTheme.Fill(screen, CombatFlashPresentation.Flash(new Color(accent.r, accent.g, accent.b,
                flash * (shiny ? .14f : quality >= 3 ? .1f : .035f))));
            float spread = BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(age / .65f));
            int count = shiny ? 52 : quality == 4 ? 40 : quality == 3 ? 30 : 16;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f + index;
                Vector2 direction = new Vector2(Mathf.Cos(angle) * screen.width * .82f,
                    Mathf.Sin(angle) * screen.height * 1.05f);
                float speed = .58f + i % 5 * .11f;
                Color tint = shiny ? Color.Lerp(BombBrickUiTheme.Cyan, new Color(1f,.65f,.86f), i % 3 / 2f) : accent;
                for (int tail = 2; tail >= 0; tail--)
                {
                    Vector2 point = origin + direction * Mathf.Max(0f, spread * speed - tail * .025f);
                    float size = (i % 4 == 0 ? 8f : 4f) - tail;
                    tint.a = fade * (tail == 0 ? .85f : .28f) * (panel.Contains(point) ? .45f : 1f);
                    BombBrickUiTheme.Fill(new Rect(Mathf.Round(point.x / 2f) * 2f,
                        Mathf.Round(point.y / 2f) * 2f, size, size), CombatFlashPresentation.Flash(tint));
                    if (tail == 0 && i % 4 == 0)
                    {
                        BombBrickUiTheme.Fill(new Rect(point.x - size, point.y + size * .25f, size * 3f, size * .5f), CombatFlashPresentation.Flash(tint));
                        BombBrickUiTheme.Fill(new Rect(point.x + size * .25f, point.y - size, size * .5f, size * 3f), CombatFlashPresentation.Flash(tint));
                    }
                }
            }
            if (quality < 3 && !shiny) return;
            // A soft pixel-stepped edge glow lets the impact reach the whole viewport.
            for (int edge = 0; edge < 12; edge++)
            {
                Color tint = accent;
                tint.a = fade * Mathf.Clamp01(age / .12f) * (1f - edge / 12f) * .2f;
                float inset = edge * 8f;
                tint = CombatFlashPresentation.Flash(tint);
                BombBrickUiTheme.Fill(new Rect(inset, inset, screen.width - inset * 2f, 8f), tint);
                BombBrickUiTheme.Fill(new Rect(inset, screen.height - inset - 8f, screen.width - inset * 2f, 8f), tint);
                BombBrickUiTheme.Fill(new Rect(inset, inset + 8f, 8f, screen.height - inset * 2f - 16f), tint);
                BombBrickUiTheme.Fill(new Rect(screen.width - inset - 8f, inset + 8f, 8f, screen.height - inset * 2f - 16f), tint);
            }
        }

        private static void DrawQualityReveal(Rect stage, float time, Color accent, int quality, bool shiny)
        {
            if (time < RevealImpactAt) return;
            float kick = RevealKick(time);
            Color wash = accent;
            wash.a = kick * (shiny ? .34f : quality >= 3 ? .25f : .12f);
            if (kick > 0f) BombBrickUiTheme.Fill(stage, CombatFlashPresentation.Flash(wash));
            Color rim = Color.Lerp(accent, Color.white, .4f);
            rim.a = kick * .9f;
            if (kick > 0f) BombBrickUiTheme.Frame(stage, CombatFlashPresentation.Flash(rim), quality >= 3 ? 4f : 2f);
            if (quality < 3 && !shiny) return;
            float age = time - RevealImpactAt;
            float duration = shiny ? 1.8f : quality == 4 ? 1.5f : 1.1f;
            float pulse = Mathf.Clamp01(age / .09f) * Mathf.Clamp01(1f - age / duration);
            float spread = BombBrickUiTheme.EaseOutCubic(Mathf.Clamp01(age / .4f));
            int beams = quality == 4 || shiny ? 7 : 5;
            for (int i = 0; pulse > 0f && i < beams; i++)
            {
                float x = stage.center.x + (i - (beams - 1) * .5f) * stage.width / (beams + 1) * spread;
                float height = stage.height * (i % 2 == 0 ? .94f : .76f) * spread;
                for (int j = 0; j < 10; j++)
                {
                    Color light = Color.Lerp(accent, Color.white, .3f);
                    light.a = pulse * (1f - j / 10f) * .22f;
                    BombBrickUiTheme.Fill(new Rect(Mathf.Round(x / 2f) * 2f - 4f,
                        stage.yMax - 32f - height * (j + 1) / 10f, 8f, height / 10f + 1f),
                        CombatFlashPresentation.Flash(light));
                }
            }
            if (quality == 4) DrawBurst(stage, time - .28f, accent, 7);
            int stars = shiny ? 12 : quality == 4 ? 7 : 4;
            for (int i = 0; i < stars; i++)
            {
                float cycle = Mathf.Repeat(age * (shiny ? .8f : .5f) + i * .237f, 1f);
                float glow = Mathf.Sin(cycle * Mathf.PI);
                float angle = i * 2.39996f + (shiny ? age * .45f : 0f);
                float radius = .34f + i % 3 * .055f;
                Vector2 point = stage.center + new Vector2(Mathf.Cos(angle) * stage.width * radius,
                    Mathf.Sin(angle) * stage.height * radius - cycle * 14f);
                Color tint = shiny ? Color.Lerp(BombBrickUiTheme.Cyan, new Color(1f,.68f,.89f), i % 3 / 2f) : accent;
                tint.a = glow * Mathf.Clamp01(age / .15f) * (shiny ? .85f : .5f);
                float size = Mathf.Round((shiny ? 9f : 6f) * glow / 2f) * 2f;
                float x = Mathf.Round(point.x / 2f) * 2f, y = Mathf.Round(point.y / 2f) * 2f;
                BombBrickUiTheme.Fill(new Rect(x - 1f, y - size / 2f, 2f, size), tint);
                BombBrickUiTheme.Fill(new Rect(x - size / 2f, y - 1f, size, 2f), tint);
            }
        }

        private void DrawChest(Rect rect, int frame)
        {
            if (sheet == null) return;
            int cell = frame + (family ? 8 : 0);
            GUI.DrawTextureWithTexCoords(rect, sheet, new Rect((cell % 4) * 0.25f, 1f - (cell / 4 + 1) * 0.25f, 0.25f, 0.25f), true);
        }

        private static void DrawBurst(Rect stage, float time, Color tint, int seed)
        {
            if (time < 0f || time > 0.62f) return;
            float p = time / 0.62f;
            for (int i = 0; i < 18; i++)
            {
                float angle = (i * 2.39996f + seed) % (Mathf.PI * 2f);
                float travel = (35f + (i % 5) * 11f) * (1f - Mathf.Pow(1f - p, 3f));
                Vector2 point = new Vector2(stage.center.x + Mathf.Cos(angle) * travel,
                    stage.center.y + 10f + Mathf.Sin(angle) * travel * 0.65f - p * 25f + p * p * 43f);
                float size = i % 4 == 0 ? 5f : 3f;
                Color color = i % 3 == 0 ? Color.Lerp(tint, Color.white, 0.5f) : tint;
                color.a = (1f - p) * 0.8f;
                BombBrickUiTheme.Fill(new Rect(Mathf.Round(point.x / 2f) * 2f, Mathf.Round(point.y / 2f) * 2f, size, size), CombatFlashPresentation.Flash(color));
            }
        }

        private Color RegionColor()
        {
            switch (regionId)
            {
                case "poison_fog_swamp": return new Color(0.74f, 0.52f, 0.86f);
                case "crimson_jungle": return new Color(1f, 0.4f, 0.16f);
                case "frozen_highlands": return new Color(0.56f, 0.87f, 0.96f);
                case "sky_sanctuary": return new Color(0.92f, 0.81f, 0.42f);
                default: return new Color(0.62f, 0.77f, 0.25f);
            }
        }

        private static string Text(string suffix, string fallback) => GameLocalization.TextByKey("reward.chest." + suffix, fallback);
    }
}
