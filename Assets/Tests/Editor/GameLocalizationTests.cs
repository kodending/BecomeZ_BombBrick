using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameLocalizationTests
{
    [UnityTest]
    public IEnumerator BackgroundPreloadRetainsTranslationsAndClearsTemporaryFallbacks()
    {
        GameLocalization.SetLanguage(GameLocalization.English);
        typeof(GameLocalization).GetField("loaded", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, false);
        var preload = GameLocalization.PreloadAcrossFrames();
        Assert.That(preload.MoveNext(), Is.True);
        Assert.That(GameLocalization.Text("설정"), Is.EqualTo("설정"));
        yield return preload.Current;
        while (preload.MoveNext()) yield return preload.Current;
        Assert.That(GameLocalization.Text("설정"), Is.EqualTo("Settings"));
        Assert.That(GameLocalization.Text("탐험덱  8 / 8"), Is.EqualTo("Exploration Team  8 / 8"));
        Assert.That(GameLocalization.TextByKey("settings.language", "언어"), Is.EqualTo("Language"));
    }

    [Test]
    public void ReloadDuringBackgroundPreloadKeepsTheFreshDatabase()
    {
        typeof(GameLocalization).GetField("loaded", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, false);
        var preload = GameLocalization.PreloadAcrossFrames();
        Assert.That(preload.MoveNext(), Is.True);
        GameLocalization.Reload();
        ((System.IDisposable)preload).Dispose();
        GameLocalization.SetLanguage(GameLocalization.English);
        Assert.That(GameLocalization.Text("설정"), Is.EqualTo("Settings"));
    }

    private string previousLanguage;

    [SetUp]
    public void RememberLanguage()
    {
        previousLanguage = GameLocalization.CurrentLanguage;
        GameLocalization.Reload();
    }

    [TearDown]
    public void RestoreLanguage()
    {
        GameLocalization.SetLanguage(previousLanguage);
    }

    [Test]
    public void DatabaseHasCompleteEnglishAndStableKeys()
    {
        TextAsset asset = Resources.Load<TextAsset>(GameLocalization.ResourcePath);
        Assert.That(asset, Is.Not.Null);

        LocalizationDatabaseFile database =
            JsonUtility.FromJson<LocalizationDatabaseFile>(asset.text);
        Assert.That(database, Is.Not.Null);
        Assert.That(database.entries.Count, Is.GreaterThanOrEqualTo(900));

        var keys = new HashSet<string>();
        var englishByKorean = new Dictionary<string, string>();
        foreach (LocalizationEntry entry in database.entries)
        {
            Assert.That(keys.Add(entry.key), Is.True, "Duplicate key: " + entry.key);
            if (!entry.enabled) continue;
            Assert.That(entry.ko, Is.Not.Empty, entry.key + " has no Korean text.");
            Assert.That(entry.en, Is.Not.Empty, entry.key + " has no English text.");
            if (englishByKorean.TryGetValue(entry.ko, out string existing))
                Assert.That(entry.en, Is.EqualTo(existing), "Conflicting translation: " + entry.ko);
            else englishByKorean.Add(entry.ko, entry.en);
        }
    }

    [Test]
    public void LanguageSwitchUsesEnglishAndKoreanFallback()
    {
        GameLocalization.SetLanguage(GameLocalization.English);
        Assert.That(GameLocalization.Text("설정"), Is.EqualTo("Settings"));
        Assert.That(GameLocalization.TextByKey("settings.language", "언어"), Is.EqualTo("Language"));
        Assert.That(GameLocalization.Text("#025  웅이"), Is.EqualTo("#025  Woongi"));
        Assert.That(GameLocalization.Text("성격  ·  겁이 많은"),
            Is.EqualTo("Personality  ·  Timid"));
        Assert.That(GameLocalization.Text("포획 25%  ·  보유 0"),
            Is.EqualTo("Capture 25%  ·  Owned 0"));
        Assert.That(GameLocalization.Text("태초의 초원"), Is.EqualTo("Primordial Meadow"));
        Assert.That(GameLocalization.Text(
            ExpeditionDifficultyRules.DisplayName(ExpeditionDifficulty.Beginner)),
            Is.EqualTo("Beginner"));
        Assert.That(GameLocalization.Text("탐험덱  8 / 8"),
            Is.EqualTo("Exploration Team  8 / 8"));
        Assert.That(GameLocalization.Text("12 WAVE  ·  지역 상자 10"),
            Is.EqualTo("12 WAVES  ·  Crate 10"));
        Assert.That(GameLocalization.Text("적 HP ×0.65  ·  포획 1회"),
            Is.EqualTo("Enemy HP ×0.65  ·  Capture rolls: 1"));
        Assert.That(GameLocalization.Text("대기 몬스터  1"),
            Is.EqualTo("Reserve Monsters  1"));
        Assert.That(GameLocalization.TextByKey(
            "tooltip.item.completed_badge", "완성"), Is.EqualTo("Completed"));
        Assert.That(GameLocalization.TextByKey(
            "tooltip.item.component_badge", "재료"), Is.EqualTo("Component"));
        Assert.That(GameLocalization.TextByKey(
            "battle.enemy_intel_restricted", "제한 공개"),
            Is.EqualTo("Limited Intel"));
        Assert.That(GameLocalization.Format(
            "tooltip.item.recipe", "조합  ·  {0}", "Mana Core + Sharp Crystal"),
            Is.EqualTo("Recipe  ·  Mana Core + Sharp Crystal"));
        Assert.That(GameLocalization.Text("합성 준비 중"), Is.EqualTo("Hold to Combine"));
        Assert.That(GameLocalization.Text("놓아서 합성"), Is.EqualTo("Release to Combine"));
        Assert.That(GameLocalization.Text("마력핵 조합 완료"), Is.EqualTo("Core Combined"));
        Assert.That(GameLocalization.Format(
            "town.research_points_short", "연구 포인트 {0}", 3),
            Is.EqualTo("Upgrade Points 3"));
        Assert.That(GameLocalization.TextByKey(
            "result.player_experience", "플레이어 경험치"),
            Is.EqualTo("Player Experience"));
        Assert.That(GameLocalization.Format(
            "result.research_points_gained", "연구 포인트 +{0}", 2),
            Is.EqualTo("Upgrade Point +2"));
        Assert.That(GameLocalization.Format(
            "capture.target_selected", "{0} 포획 지정", "Lizamo"),
            Is.EqualTo("Lizamo Marked for Capture"));
        Assert.That(GameLocalization.Format(
            "growth.trait_activated", "{0} 활성화", "Persistent Spirit"),
            Is.EqualTo("Persistent Spirit Activated"));
        Assert.That(GameLocalization.TextByKey(
            "common.monster", "몬스터"), Is.EqualTo("Monster"));
        Assert.That(GameLocalization.Text("슬롯에 장착"),
            Is.EqualTo("Equip to Slot"));
        Assert.That(GameLocalization.Text("지역 전술"),
            Is.EqualTo("Regional Tactics"));
        Assert.That(GameLocalization.TextByKey(
            "town.progress_unlock.region_title", "새로운 탐험지 해금!"),
            Is.EqualTo("New Exploration Unlocked!"));
        Assert.That(GameLocalization.Format(
            "town.progress_unlock.region_and_difficulty",
            "새 탐험지 · {0}\n{1} · {2} 난이도 선택 가능",
            "Poison Fog Swamp", "Primordial Meadow", "Normal"),
            Is.EqualTo("New Exploration · Poison Fog Swamp\n" +
                "Primordial Meadow · Normal difficulty now available"));
        Assert.That(Regex.IsMatch(
            GameLocalization.Text("탐험덱  8 / 8"), "[가-힣]"), Is.False);
        Assert.That(GameLocalization.Text("번역되지 않은 안전 문구"),
            Is.EqualTo("번역되지 않은 안전 문구"));
        Assert.That(GameLocalization.Text("보유 아직 번역되지 않은 문구"),
            Is.EqualTo("보유 아직 번역되지 않은 문구"),
            "A generic placeholder must not create mixed Korean/English UI.");

        GameLocalization.SetLanguage(GameLocalization.Korean);
        Assert.That(GameLocalization.Text("설정"), Is.EqualTo("설정"));
    }

    [Test]
    public void ExplorationTerminologyAndTutorialCountersAreLocalized()
    {
        var database = JsonUtility.FromJson<LocalizationDatabaseFile>(
            Resources.Load<TextAsset>("Localization/LocalizationDatabase").text);
        foreach (var entry in database.entries)
        {
            Assert.That(entry.ko, Does.Not.Contain("\uC6D0\uC815"), entry.key);
            Assert.That(Regex.IsMatch(entry.en, @"\bexpeditions?\b",
                RegexOptions.IgnoreCase), Is.False, entry.key);
        }

        string language = GameLocalization.CurrentLanguage;
        try
        {
            GameLocalization.SetLanguage(GameLocalization.English);
            Assert.That(GameLocalization.Text(
                "몬스터를 위 탐험덱으로 드래그해 8칸을 채우세요.  4 / 8"),
                Is.EqualTo("Drag monsters into the Exploration Team above.  4 / 8"));
            Assert.That(GameLocalization.Text("탐험 정보가 없습니다."),
                Is.EqualTo("Exploration data is unavailable."));
            Assert.That(GameLocalization.Text("W 12 · 탐험 결과"),
                Is.EqualTo("W 12 · Exploration Results"));
            GameLocalization.SetLanguage(GameLocalization.Korean);
            Assert.That(GameLocalization.TextByKey("frontend.expedition_prep", "탐험 준비"),
                Is.EqualTo("탐험 준비"));
        }
        finally { GameLocalization.SetLanguage(language); }
    }

    [Test]
    public void EveryMonsterAbilityTooltipDescriptionIsEnglish()
    {
        GameLocalization.SetLanguage(GameLocalization.English);
        GameMasterDatabase database = GameMasterData.Database;
        Assert.That(database, Is.Not.Null);
        var untranslated = new List<string>();

        foreach (SkillMasterRow skill in database.skills.Where(row => row != null && row.enabled))
            CollectUntranslated(untranslated, MonsterAbilityTooltipContent.SkillSummary(skill),
                "Skill " + skill.id);
        foreach (TraitMasterRow trait in database.traits.Where(row => row != null && row.enabled))
            CollectUntranslated(untranslated, MonsterAbilityTooltipContent.TraitSummary(trait),
                "Trait " + trait.id);
        foreach (PersonalityMasterRow personality in database.personalities.Where(
                     row => row != null && row.enabled))
            CollectUntranslated(untranslated,
                MonsterAbilityTooltipContent.PersonalitySummary(personality),
                "Personality " + personality.id);

        Assert.That(untranslated, Is.Empty,
            "Untranslated monster tooltip descriptions:\n" + string.Join("\n", untranslated));

        SkillMasterRow bearSkill = database.skills.First(row => row.id == "bear_fighting_spirit");
        string bearSummary = MonsterAbilityTooltipContent.SkillSummary(bearSkill);
        Assert.That(bearSummary, Does.Contain("For 4s, gain 20% attack speed and 15% damage reduction"));
        Assert.That(bearSummary, Does.Contain("20% skill power once per basic action"));
        Assert.That(bearSummary, Does.Contain("Cooldown"));
    }

    [TestCase(GameLocalization.Korean,"스킬 위력×180% 물리 피해","MaxHP×20% 고정 피해")]
    [TestCase(GameLocalization.English,"180% skill power as physical damage","20% of target max HP as true damage")]
    public void FangLockTooltipDistinguishesPhysicalAndTrueDamage(
        string language,string physical,string trueDamage)
    {
        GameLocalization.SetLanguage(language);
        string summary=MonsterAbilityTooltipContent.SkillSummary(
            GameMasterData.Skill(SabertoothCombatRules.SkillId));
        Assert.That(summary,Does.Contain(physical));
        Assert.That(summary,Does.Contain(trueDamage));
    }

    [Test]
    public void EveryItemAndAugmentTooltipDescriptionIsEnglish()
    {
        GameLocalization.SetLanguage(GameLocalization.English);
        GameMasterDatabase database = GameMasterData.Database;
        Assert.That(database, Is.Not.Null);

        MethodInfo itemEffect = typeof(MonsterAutoBattlePrototype).GetMethod(
            "ItemSpecialEffectTooltip", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo itemStats = typeof(MonsterAutoBattlePrototype).GetMethod(
            "CompactItemStats", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo specialDescription = typeof(MonsterAutoBattlePrototype).GetMethod(
            "SpecialItemDescription", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(itemEffect, Is.Not.Null);
        Assert.That(itemStats, Is.Not.Null);
        Assert.That(specialDescription, Is.Not.Null);

        var untranslated = new List<string>();
        foreach (ItemMasterRow item in database.items.Where(row => row != null && row.enabled))
        {
            CollectUntranslated(untranslated, GameLocalization.Text(item.displayName),
                "Item name " + item.id);
            CollectUntranslated(untranslated, (string)itemEffect.Invoke(null, new object[] { item }),
                "Item effect " + item.id);
            CollectUntranslated(untranslated, (string)itemStats.Invoke(null, new object[] { item }),
                "Item stats " + item.id);
            if (!item.IsCompleted) continue;
            ExpeditionItemTransformState[] states =
            {
                ExpeditionItemTransformState.Radiant,
                ExpeditionItemTransformState.Overcharged,
                ExpeditionItemTransformState.CorruptedSuccess,
                ExpeditionItemTransformState.CorruptedFailure,
                ExpeditionItemTransformState.CorruptedGreatSuccess
            };
            foreach (ExpeditionItemTransformState state in states)
            {
                var instance = new ExpeditionItemInstanceSave
                    { itemId = item.id, transformState = state };
                CollectUntranslated(untranslated,
                    GameLocalization.Text(ExpeditionItemTransformSystem.DisplayName(instance)),
                    "Transformed item name " + item.id + "/" + state);
            }
        }
        foreach (AugmentMasterRow augment in database.augments.Where(
                     row => row != null && row.enabled))
        {
            CollectUntranslated(untranslated, GameLocalization.Text(augment.displayName),
                "Augment name " + augment.id);
            CollectUntranslated(untranslated, GameLocalization.Text(augment.effectSummary),
                "Augment " + augment.id);
        }

        string[] specialItemIds =
        {
            "special_combined_cast_core", "special_return_mark",
            "special_corruption_catalyst", "special_trait_recombobulator",
            "special_golden_dismantler", "unknown_special_item"
        };
        foreach (string itemId in specialItemIds)
            CollectUntranslated(untranslated,
                (string)specialDescription.Invoke(null, new object[] { itemId }),
                "Special item " + itemId);

        Assert.That(untranslated, Is.Empty,
            "Untranslated item/augment tooltip descriptions:\n" +
            string.Join("\n", untranslated));
    }

    [Test]
    public void ExpeditionHudGrowthAndShopStringsAreEnglish()
    {
        GameLocalization.SetLanguage(GameLocalization.English);
        var untranslated = new List<string>();
        string[] samples =
        {
            GameLocalization.Text("다음 교전"),
            GameLocalization.Format("battle.gold_interest", "골드 · 이자 +{0}", 2),
            GameLocalization.Format("battle.seconds", "{0}초", "4.2"),
            GameLocalization.Text("환경 효과 없음"),
            GameLocalization.Format("battle.owned_augments", "보유 증강  {0}", 1),
            GameLocalization.Text("탐험 상점"),
            GameLocalization.Format("shop.wave_title", "W {0} · 아이템 상점", 2),
            GameLocalization.Format("shop.buy_gold", "구매 · 골드 {0}", 4),
            GameLocalization.Format("growth.shop_wave", "성장 상점  ·  W {0}", 2),
            GameLocalization.Format("growth.selection_cost",
                "선택 {0}  ·  비용 {1} 골드", 2, 4),
            GameLocalization.Format("growth.preview_health", "체력 +{0}", 4),
            GameLocalization.Format("growth.preview_rank", "{0} 달성", "★★"),
            GameLocalization.Text("전투 데이터"),
            GameLocalization.Text("회복량"),
            GameLocalization.Text("제한 공개"),
            GameLocalization.Text("장착 아이템  ·  일부 공개"),
            GameLocalization.Text("합동 시전 코어"),
            GameLocalization.Text("초월 유전자"),
            GameLocalization.Format("battle.combo_bonus", "연계 {0} +{1}", 2, 30),
            GameLocalization.Format("augment.ui.wave_eyebrow",
                "W {0} · 탐험 각인", 1),
            GameLocalization.Text("탐험 법칙 각인"),
            GameLocalization.Text("선택지 새로고침  ·  무료"),
            GameLocalization.Text("탐험 규칙 각인 중"),
            GameLocalization.Text("대상 지정 중"),
            GameLocalization.Text("선택 불가"),
            GameLocalization.Text("스킬 규칙"),
            GameLocalization.Text("탐험 경제"),
            GameLocalization.Text("아이템 규칙"),
            GameLocalization.Text("배치 전술"),
            GameLocalization.Text("탐험대"),
            GameLocalization.Text("위험 거래"),
            GameLocalization.Text("생명 규칙"),
            GameLocalization.Text("소환 규칙"),
            GameLocalization.Text("사망 규칙"),
            GameLocalization.Text("전투 규칙"),
            GameLocalization.Text("특수 규칙"),
            GameLocalization.Text("즉시 적용"),
            GameLocalization.Text("특수 아이템 지급"),
            GameLocalization.Text("몬스터 지정"),
            GameLocalization.Text("아이템 지정"),
            GameLocalization.Format("augment.ui.monster_selected",
                "{0} 선택 · 정보 확인 후 확정하세요.", "Woongi"),
            GameLocalization.Format("augment.ui.apply_to_monster",
                "{0}에게 {1} 적용", "Woongi", "Protagonist"),
            GameLocalization.Text("찬란한 살육의 수정"),
            GameLocalization.Text("과충전된 살육의 수정"),
            GameLocalization.Text("타락한 살육의 수정"),
            GameLocalization.Text("불완전 타락 살육의 수정"),
            GameLocalization.Format("augment.ui.reserve_rank_family",
                "{0}  ·  {1} 계열", "★★", GameLocalization.Text("곰")),
            GameLocalization.Format("augment.ui.reserve_role_personality",
                "{0}  ·  {1}", GameLocalization.Text("균형 성장"),
                GameLocalization.Text("겁이 많은")),
            GameLocalization.Format("augment.ui.reserve_rank_trait",
                "{0}★ {1}", 2, GameLocalization.Text("끈질긴 추적")),
            GameLocalization.Text("W 8 · 전리품"),
            GameLocalization.Text("획득 전리품 확인"),
            GameLocalization.Text("보관함으로 이동 중"),
            GameLocalization.Text("전리품 정산 완료"),
            GameLocalization.TextByKey(
                "result.confirm","탐험 결과 확인"),
            GameLocalization.Text("이번 Wave에서 확보한 전리품이 없습니다.")
        };
        foreach (string sample in samples)
            CollectUntranslated(untranslated, sample, "Expedition UI");

        GameMasterDatabase database = GameMasterData.Database;
        foreach (MonsterGrowthMasterRow growth in database.monsterGrowths.Where(
                     row => row != null && !string.IsNullOrWhiteSpace(row.growthRole)))
            CollectUntranslated(untranslated, GameLocalization.Text(growth.growthRole),
                "Growth role " + growth.monsterId);
        foreach (WaveScalingMasterRow wave in database.waveScalings.Where(
                     row => row != null && row.enabled &&
                            !string.IsNullOrWhiteSpace(row.threatLabel)))
            CollectUntranslated(untranslated, GameLocalization.Text(wave.threatLabel),
                "Wave threat " + wave.regionId + "/" + wave.wave);

        ItemMasterRow sampleItem=database.items.First(row=>row!=null&&row.enabled);
        MethodInfo lootLabel=typeof(MonsterAutoBattlePrototype).GetMethod(
            "LootLabel",BindingFlags.NonPublic|BindingFlags.Static);
        Assert.That(lootLabel,Is.Not.Null);
        CollectUntranslated(untranslated,(string)lootLabel.Invoke(null,new object[]{
            new EnemyDropRoll{rewardType="Equipment",itemId=sampleItem.id,amount=1}}),
            "World item drop");
        CollectUntranslated(untranslated,(string)lootLabel.Invoke(null,new object[]{
            new EnemyDropRoll{rewardType="ExpeditionCurrency",amount=2}}),
            "World gold drop");
        var lootSummary=new PendingWaveLoot{expeditionCurrency=2};
        lootSummary.itemIds.Add(sampleItem.id);
        CollectUntranslated(untranslated,lootSummary.Summary(),"Loot summary");

        Assert.That(untranslated, Is.Empty,
            "Untranslated expedition interface strings:\n" +
            string.Join("\n", untranslated));
    }

    [Test]
    public void ExpeditionInteractionAndComposedMessagesAreEnglish()
    {
        GameLocalization.SetLanguage(GameLocalization.English);
        var untranslated = new List<string>();
        string[] samples =
        {
            GameLocalization.Text("탐험 가방으로 이동"),
            GameLocalization.Text("탐험 가방으로 이동했습니다."),
            GameLocalization.Text("다른 아군에게 직접 옮기세요."),
            GameLocalization.Text("다른 아군에게 아이템을 옮기세요."),
            GameLocalization.Text("다른 아군에게 옮기세요."),
            GameLocalization.Text("원하는 아군에게 놓아 장착하세요."),
            GameLocalization.Text("다른 아군에게 놓아 옮기세요."),
            GameLocalization.Text("완성 아이템에 Corruption Catalyst 사용"),
            GameLocalization.Text("Combined Cast Core 적용"),
            GameLocalization.Text("먼저 아이템을 합성하세요."),
            GameLocalization.Text("최대 배치 수 확장: 5개체"),
            GameLocalization.Text("전투 수입 · 기본 3 · 이자 1 · 연속 2"),
            GameLocalization.Text("WAVE 4 승리"),
            GameLocalization.Text("WAVE 4 패배"),
            GameLocalization.Text("12Wave 탐험 완료"),
            GameLocalization.Text("Wave 4 패배 - 목숨 2 / 3"),
            GameLocalization.Text("최후의 축복 · 골드 5"),
            GameLocalization.Text("최후의 축복 · 골드 5 · Ruby Blade"),
            GameLocalization.Text("포획 시도 중: Sundongi  ·  처치 시 25% 판정"),
            GameLocalization.Text("슬롯 1에 장착했습니다."),
            GameLocalization.Text("Woongi에게 Combined Cast Core 영구 적용"),
            GameLocalization.Text("타락 결과: 대성공"),
            GameLocalization.Text("Woongi 특성 변경: Persistent Spirit"),
            GameLocalization.Text("Woongi의 2번 슬롯에 장착했습니다."),
            GameLocalization.Text("Woongi에게 Ruby Blade을 장착했습니다."),
            GameLocalization.Text("Ruby Blade을 Woongi에게 옮겼습니다."),
            GameLocalization.Text("조합할 수 없는 아이템입니다."),
            GameLocalization.Text("합성 게이지가 가득 찬 뒤 놓아주세요."),
            GameLocalization.Text("몬스터 위에 드롭해야 합니다."),
            GameLocalization.Text("완성 아이템 위에 드롭해야 합니다."),
            GameLocalization.Text("완성 아이템을 원래 재료로 분해했습니다."),
            GameLocalization.Text("기본 아이템 두 개를 합성하세요."),
            GameLocalization.Text("기본 아이템만 조합 재료로 선택할 수 있습니다."),
            GameLocalization.Text("첫 재료 선택을 해제했습니다."),
            GameLocalization.Text("두 번째 재료 선택을 해제했습니다."),
            GameLocalization.Text("몬스터 탐험 정보를 찾을 수 없습니다."),
            GameLocalization.Text("교체 가능한 특성이 없습니다."),
            GameLocalization.Text("가능한 조합이 없습니다."),
            GameLocalization.Text("특수 도구"),
            GameLocalization.Text("사용 시 소모"),
            GameLocalization.Text("탐험 아이템 · 전투 준비 중 자유 이동/조합"),
            GameLocalization.Text("장착할 아군 몬스터를 클릭하세요."),
            GameLocalization.Text("슬롯 1  비어 있음"),
            GameLocalization.Text("재료: Ruby Blade + Moon Guard"),
            GameLocalization.Text("결과 미리보기: Ruby Blade · Attack +10"),
            GameLocalization.Text("장착 아이템  ·  전체 공개"),
            GameLocalization.Text("장착 아이템  ·  일부 공개"),
            GameLocalization.Text("선택 후 가능한 행동"),
            GameLocalization.Text("미지정"),
            GameLocalization.Text("착용 아이템  ·  3 SLOT")
        };
        foreach (string sample in samples)
            CollectUntranslated(untranslated, sample, "Expedition interaction");

        Assert.That(untranslated, Is.Empty,
            "Untranslated expedition interaction strings:\n" +
            string.Join("\n", untranslated));
    }

    private static void CollectUntranslated(List<string> result, string value, string context)
    {
        if (Regex.IsMatch(value ?? "", "[가-힣ㄱ-ㅎㅏ-ㅣ]"))
            result.Add(context + ": " + value.Replace("\n", " / "));
    }

}
