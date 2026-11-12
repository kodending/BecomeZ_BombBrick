using UnityEngine;
using GUI = LocalizedGUI;

namespace BombBrick.Game.Core
{
    // Presentation only: acquisition has already rolled and saved these values.
    public static class MonsterAcquisitionReveal
    {
        public const int StatCount = 6;
        public const float PortraitAt = .12f, StatsAt = .42f, FirstStatInterval = .36f, CountDuration = .10f;
        public const float StampAt = 1.97f, StampDuration = .30f, ReadyAt = 2.77f;
        private static readonly float[] StatOffsets = { 0f, .36f, .64f, .84f, .98f, 1.09f };
        private static readonly string[] Labels = { "체력", "공격력", "공격속도", "방어력", "상태저항", "이동속도" };

        public static float StatAt(int stat) => StatsAt + StatOffsets[Mathf.Clamp(stat, 0, StatCount - 1)];
        public static float StatProgress(float age, int stat)
            => Mathf.Clamp01((age - StatAt(stat)) / CountDuration);
        public static bool GradeVisible(float age) => age >= StampAt;
        public static float StatImpact(float age, int stat)
        {
            stat = Mathf.Clamp(stat, 0, StatCount - 1);
            float elapsed = age - StatAt(stat);
            float duration = stat < StatCount - 1 ? Mathf.Min(.28f, StatAt(stat + 1) - StatAt(stat)) : CountDuration;
            return CombatFlashPresentation.Reduced || elapsed < 0f || elapsed >= duration ? 0f :
                Mathf.Pow(1f - elapsed / duration, 2f);
        }
        public static int QualityTier(OwnedMonsterSave owned) => owned == null ? 2 :
            Tier(MonsterStatQuality.OverallGrade(owned));
        public static int StatQualityTier(OwnedMonsterSave owned, int stat) => owned == null ? 2 :
            Tier(MonsterStatQuality.StatGrade(owned, (MonsterIndividualStat)Mathf.Clamp(stat, 0, StatCount - 1)));
        private static int Tier(string grade) => Mathf.Clamp("DCBAS".IndexOf(grade[0]), 0, 4);
        public static Color GradeColor(OwnedMonsterSave owned) => GradeColor(QualityTier(owned));
        public static Color GradeColor(int quality)
        {
            switch (Mathf.Clamp(quality, 0, 4))
            {
                case 0: return new Color(.66f, .70f, .74f);
                case 1: return new Color(.45f, .83f, .59f);
                case 2: return new Color(.32f, .83f, 1f);
                case 3: return new Color(.80f, .48f, 1f);
                default: return new Color(1f, .79f, .25f);
            }
        }
        public static float GradeStrength(int quality, bool shiny = false) =>
            .55f + Mathf.Clamp(quality, 0, 4) * .25f + (shiny ? .15f : 0f);
        public static float GradeImpact(float age, int quality, bool shiny = false)
        {
            float elapsed = age - StampAt;
            float duration = .22f + Mathf.Clamp(quality, 0, 4) * .065f + (shiny ? .08f : 0f);
            return CombatFlashPresentation.Reduced || elapsed < 0f || elapsed >= duration ? 0f :
                GradeStrength(quality, shiny) * Mathf.Pow(1f - elapsed / duration, 2f);
        }
        public static float StampScale(float age, int quality = 2, bool shiny = false)
        {
            if (!GradeVisible(age)) return 0f;
            if (CombatFlashPresentation.Reduced) return 1f;
            quality = Mathf.Clamp(quality, 0, 4);
            float t = Mathf.Clamp01((age - StampAt) / StampDuration);
            float compression = .04f + quality * .01f;
            if (t < .30f) return Mathf.Lerp(1.10f + quality * .10f + (shiny ? .04f : 0f), 1f - compression, t / .30f);
            return 1f - compression * Mathf.Cos((t - .30f) / .70f * Mathf.PI * 3f) *
                Mathf.Pow(1f - (t - .30f) / .70f, 2f);
        }
        public static float Value(MonsterDefinition definition, OwnedMonsterSave owned, int index)
        {
            if (definition == null || owned == null) return 0f;
            switch ((MonsterIndividualStat)index)
            {
                case MonsterIndividualStat.Health: return definition.maxHp * owned.hpMultiplier;
                case MonsterIndividualStat.AttackDamage: return definition.attackDamage * owned.damageMultiplier;
                case MonsterIndividualStat.AttackSpeed: return owned.attackSpeedMultiplier / Mathf.Max(.1f, definition.attackInterval);
                case MonsterIndividualStat.Defense: return definition.defense * owned.defenseMultiplier;
                case MonsterIndividualStat.StatusResistance: return definition.statusResistance * owned.statusResistanceMultiplier;
                default: return definition.moveSpeedTilesPerSecond * owned.moveSpeedMultiplier;
            }
        }
        public static float DisplayValue(MonsterDefinition definition, OwnedMonsterSave owned, float age, int index)
            => Value(definition, owned, index) * BombBrickUiTheme.EaseOutCubic(StatProgress(age, index));

        public static void DrawStats(Rect area, MonsterDefinition definition, OwnedMonsterSave owned, float age,
            int columns = 3, int fontSize = 17)
        {
            if (definition == null || owned == null || age < StatsAt) return;
            int rows = Mathf.CeilToInt(StatCount / (float)columns);
            float height = area.height / rows;
            for (int i = 0; i < StatCount; i++)
            {
                float progress = StatProgress(age, i);
                Rect cell = new Rect(area.x + i % columns * area.width / columns,
                    area.y + i / columns * height, area.width / columns, height - 2f);
                float pulse = StatImpact(age, i);
                string statGrade = MonsterStatQuality.StatGrade(owned, (MonsterIndividualStat)i);
                int quality = Tier(statGrade);
                Color accent = GradeColor(quality);
                float impact = pulse * GradeStrength(quality);
                if (impact > 0f)
                {
                    BombBrickUiTheme.Fill(new Rect(cell.x + 4f, cell.y, cell.width - 8f, cell.height),
                        new Color(accent.r, accent.g, accent.b, impact * .22f));
                    BombBrickUiTheme.Frame(new Rect(cell.x + 4f, cell.y, cell.width - 8f, cell.height),
                        new Color(accent.r, accent.g, accent.b, Mathf.Min(1f, impact)), quality >= 3 ? 3f : 2f);
                    BombBrickUiTheme.Fill(new Rect(cell.x + 6f, cell.yMax - 3f,
                        (cell.width - 12f) * progress, 2f), new Color(accent.r, accent.g, accent.b, pulse));
                    for (int particle = 0; particle < 2 + quality * 2; particle++)
                    {
                        float angle = particle * 2.39996f + i * .7f;
                        Vector2 point = new Vector2(cell.center.x + Mathf.Cos(angle) * cell.width * (.10f + (1f - pulse) * .28f),
                            cell.y + height * .68f + Mathf.Sin(angle) * height * (.08f + (1f - pulse) * .12f));
                        float size = 2f + quality * .6f;
                        point.x = Mathf.Clamp(point.x, cell.x + 5f, cell.xMax - size - 5f);
                        point.y = Mathf.Clamp(point.y, cell.y + height * .38f, cell.yMax - size - 2f);
                        BombBrickUiTheme.Fill(new Rect(Mathf.Round(point.x), Mathf.Round(point.y), size, size),
                            new Color(accent.r, accent.g, accent.b, pulse));
                    }
                }
                GUI.Label(new Rect(cell.x, cell.y, cell.width, height * .38f), Labels[i],
                    BombBrickUiTheme.CaptionStyle(Mathf.Max(9, fontSize - 6), TextAnchor.MiddleCenter));
                if (age < StatAt(i))
                {
                    GUI.Label(new Rect(cell.x, cell.y + height * .4f, cell.width, height * .56f), "—",
                        BombBrickUiTheme.CaptionStyle(fontSize, TextAnchor.MiddleCenter));
                    continue;
                }
                Color previous = GUI.color;
                GUI.color = Color.Lerp(previous, new Color(accent.r, accent.g, accent.b, previous.a), Mathf.Min(1f, impact * .8f));
                string format = i == (int)MonsterIndividualStat.AttackSpeed || i == (int)MonsterIndividualStat.MoveSpeed ? "0.00" : "0";
                string grade = progress >= 1f ? " " + statGrade : "";
                float shake = quality >= 3 ? Mathf.Sin((age - StatAt(i)) * 110f) * impact : 0f;
                Rect valueRect = new Rect(cell.x + shake, cell.y + height * .4f + Mathf.Min(1.5f, impact * 2f), cell.width, height * .56f);
                Matrix4x4 previousMatrix = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(1f + Mathf.Min(.30f, impact * .22f),
                    1f + Mathf.Min(.25f, impact * .30f)), valueRect.center);
                GUI.Label(valueRect,
                    DisplayValue(definition, owned, age, i).ToString(format) + grade,
                    BombBrickUiTheme.TitleStyle(fontSize, TextAnchor.MiddleCenter));
                GUI.matrix = previousMatrix;
                GUI.color = previous;
            }
        }

        public static void DrawGradeStamp(Rect rect, OwnedMonsterSave owned, float age, int fontSize)
        {
            if (!GradeVisible(age))
            {
                GUI.Label(rect, GameLocalization.TextByKey("reward.acquisition.calculating", "능력치 확인 중…"),
                    BombBrickUiTheme.CaptionStyle(Mathf.Max(10, fontSize - 5), TextAnchor.MiddleCenter));
                return;
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            try
            {
                int quality = QualityTier(owned);
                bool shiny = owned != null && owned.isShiny;
                Color accent = GradeColor(quality);
                float t = Mathf.Clamp01((age - StampAt) / StampDuration);
                float impact = GradeImpact(age, quality, shiny);
                float fade = impact / GradeStrength(quality, shiny);
                int count = 4 + quality * 4 + (shiny ? 6 : 0);
                for (int i = 0; i < count && impact > 0f; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    float travel = (1f - fade) * (12f + quality * 5f + i % 3 * 8f);
                    Vector2 point = rect.center + new Vector2(side * (rect.width * .5f + 4f + travel),
                        (i / 2f / count - .25f) * rect.height * 2f);
                    Color spark = i % 3 == 0 ? Color.Lerp(accent, Color.white, .7f) : accent;
                    BombBrickUiTheme.Fill(new Rect(Mathf.Round(point.x), Mathf.Round(point.y),
                        3f + quality + i % 3, 2f + quality * .4f), new Color(spark.r, spark.g, spark.b, fade));
                }
                if (quality >= 3 && impact > 0f)
                {
                    float spread = 4f + (1f - fade) * 18f;
                    BombBrickUiTheme.Frame(new Rect(rect.x - spread, rect.y - spread * .3f,
                        rect.width + spread * 2f, rect.height + spread * .6f),
                        new Color(accent.r, accent.g, accent.b, fade * .7f), quality == 4 ? 3f : 2f);
                }
                // Compact cards leave less room between the name, grade and stats.
                bool compact = fontSize < 25;
                float scale = Mathf.Lerp(1f, StampScale(age, quality, shiny), compact ? .48f : 1f);
                rect.x += compact ? 0f : Mathf.Sin((age - StampAt) * 91f) * impact * 2f;
                GUIUtility.ScaleAroundPivot(Vector2.one * scale, rect.center);
                GUIUtility.RotateAroundPivot(CombatFlashPresentation.Reduced || compact ? 0f :
                    Mathf.Lerp(-2f, -1f, BombBrickUiTheme.EaseOutCubic(t)), rect.center);
                BombBrickUiTheme.Fill(rect, Color.Lerp(new Color(.035f, .055f, .045f, .96f),
                    new Color(accent.r * .35f, accent.g * .35f, accent.b * .35f, .96f), impact));
                BombBrickUiTheme.Frame(rect, Color.Lerp(accent, Color.white, impact * .65f), compact ? 2f : 2f + quality * .75f);
                if (quality == 4)
                    BombBrickUiTheme.Frame(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f),
                        new Color(accent.r, accent.g, accent.b, .45f + fade * .35f), 1f);
                Color ink = Color.Lerp(accent, Color.white, impact * .75f);
                GUI.color = new Color(ink.r, ink.g, ink.b, accent.a * previousColor.a);
                GUI.Label(rect, GameLocalization.TextByKey("reward.acquisition.grade", "최종 등급") + "  " +
                    MonsterStatQuality.OverallGrade(owned), BombBrickUiTheme.TitleStyle(fontSize, TextAnchor.MiddleCenter));
            }
            finally { GUI.matrix = previousMatrix; GUI.color = previousColor; }
        }
    }
}
