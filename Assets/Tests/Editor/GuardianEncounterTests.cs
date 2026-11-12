using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GuardianEncounterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    private readonly Type battleType = typeof(MonsterAutoBattlePrototype);
    private GameObject host;
    private MonsterAutoBattlePrototype battle;
    private MonsterDatabase database;
    private ExpeditionRunSave run;
    private Type unitType;
    private bool previousEffectTest;
    private UnityEngine.Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        previousEffectTest = CombatEffectTestConfiguration.Active;
        CombatEffectTestConfiguration.Active = false;
        randomState = UnityEngine.Random.state;
        host = new GameObject("Guardian encounter rules test");
        host.SetActive(false);
        battle = host.AddComponent<MonsterAutoBattlePrototype>();
        unitType = battleType.GetNestedType("Unit", BindingFlags.NonPublic);
        database = Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database, Is.Not.Null);
        run = new ExpeditionRunSave
        {
            runId = "guardian_encounter_fixture",
            regionId = HunterRegionCatalog.FirstRegionId,
            difficulty = ExpeditionDifficulty.Beginner
        };
        battleType.GetField("expeditionRun", Private).SetValue(battle, run);
        battleType.GetField("playerProfile", Private).SetValue(battle, new PlayerProfileSave());
        battleType.GetField("monsterDatabase", Private).SetValue(battle, database);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        CombatEffectTestConfiguration.Active = previousEffectTest;
        UnityEngine.Random.state = randomState;
    }

    [TestCase("prototype_region")]
    [TestCase("poison_fog_swamp")]
    [TestCase("crimson_jungle")]
    [TestCase("frozen_highlands")]
    [TestCase("sky_sanctuary")]
    public void GuardianProgressionIsTranscendedAtBothEncountersWithOnlyFinalEquipment(string region)
    {
        run.regionId = region;
        List<MonsterDefinition> previewRoster = Roster(8);
        List<MonsterDefinition> finalRoster = Roster(12);
        object preview = Configure(8, 0, previewRoster[0], previewRoster);
        object final = Configure(12, 0, finalRoster[0], finalRoster);

        foreach (object unit in new[] { preview, final })
        {
            var progression = (ExpeditionMonsterStateSave)Get(unit, "enemyProgression");
            Assert.That(Get(unit, "expeditionRank"), Is.EqualTo(ExpeditionMonsterRank.Rank3));
            Assert.That(progression.rank, Is.EqualTo(ExpeditionMonsterRank.Rank3));
            Assert.That(progression.transcended, Is.True);
            Assert.That(Get(unit, "augmentTranscended"), Is.True);
            Assert.That(Get(unit, "hp"), Is.EqualTo(Get(unit, "maxHp")));
        }
        Assert.That(Get(preview, "guardianPreview"), Is.True);
        Assert.That(Get(preview, "regionGuardian"), Is.False);
        Assert.That(Items(preview), Is.Empty);
        Assert.That(Get(final, "guardianPreview"), Is.False);
        Assert.That(Get(final, "regionGuardian"), Is.True);
        Assert.That(Items(final), Has.Count.EqualTo(2));
    }

    [TestCase("prototype_region", null)]
    [TestCase("poison_fog_swamp", null)]
    [TestCase("crimson_jungle", null)]
    [TestCase("frozen_highlands", null)]
    [TestCase("sky_sanctuary", null)]
    [TestCase("prototype_region", "lilas")]
    [TestCase("prototype_region", "woongi")]
    [TestCase("prototype_region", "holydeer")]
    [TestCase("prototype_region", "geumgangsu")]
    public void PreviewAddsOnlyDoubleHealthAndVisualSizeAfterEquipmentAndTraitsAreMatched(string region, string healthScalingSpecies)
    {
        run.regionId = region;
        List<MonsterDefinition> roster = Roster(8);
        MonsterDefinition definition = healthScalingSpecies == null ? roster[0] : database.Get(healthScalingSpecies);
        if (healthScalingSpecies != null)
            Assert.That(GameMasterData.MonsterGrowth(definition.monsterId).skillScalingStat, Is.EqualTo("MaxHealth"));
        // Both encounters deliberately use the same saved individual; spawn-ID selection has separate coverage.
        OwnedMonsterSave owned = PlayerProfileStore.CreateMonster(definition, "matched_guardian_" + definition.monsterId, false);
        object preview = Configure(8, 0, definition, roster, owned);
        object final = Configure(12, 0, definition, Roster(12), owned);
        RemoveEquipmentAndRankTraits(preview);
        RemoveEquipmentAndRankTraits(final);
        run.wave = 8; Refresh(preview);
        run.wave = 12; Refresh(final);

        Assert.That(Number(preview, "maxHp"), Is.EqualTo(Number(final, "maxHp") * 2f).Within(.01f));
        foreach (string stat in new[] { "damage", "attackInterval", "defense", "statusResistance", "skillCooldownDuration", "speedTilesPerSecond", "collisionRadius" })
            Assert.That(Number(preview, stat), Is.EqualTo(Number(final, stat)).Within(.0001f), stat);
        Assert.That(Get(preview, "footprint"), Is.EqualTo(Get(final, "footprint")));
        Assert.That(Number(preview, "collisionRadius"), Is.GreaterThan(0f));
        Assert.That(Vector3.Distance(Model(preview).transform.localScale,
            Model(final).transform.localScale * 1.6f), Is.LessThan(.0001f));
        Assert.That((float)Call("OffensiveMaxHealth", preview), Is.EqualTo((float)Call("OffensiveMaxHealth", final)).Within(.01f));
        Assert.That((float)Call("SkillPower", preview), Is.EqualTo((float)Call("SkillPower", final)).Within(.001f));

        float hp = Number(preview, "maxHp"), attack = Number(preview, "damage");
        Vector3 scale = Model(preview).transform.localScale;
        run.wave = 8; Refresh(preview); Refresh(preview);
        Assert.That(Number(preview, "maxHp"), Is.EqualTo(hp).Within(.001f), "Refresh must not stack the preview health bonus.");
        Assert.That(Number(preview, "damage"), Is.EqualTo(attack).Within(.001f));
        Assert.That(Model(preview).transform.localScale, Is.EqualTo(scale));
    }

    [Test]
    public void EnemyRefreshReceivesTheSameTranscendenceBonusesAsAnOtherwiseUnmodifiedPlayer()
    {
        MonsterDefinition definition = database.Get("lilas");
        object unit = Configure(12, 0, definition, Roster(12));
        RemoveEquipmentAndRankTraits(unit);
        var progression = (ExpeditionMonsterStateSave)Get(unit, "enemyProgression");
        progression.transcended = false;
        Refresh(unit);
        string[] stats = { "maxHp", "damage", "attackInterval", "defense", "statusResistance", "skillCooldownDuration" };
        float[] before = stats.Select(stat => Number(unit, stat)).ToArray();
        Vector3 scale = Model(unit).transform.localScale;
        Assert.That(Get(unit, "augmentTranscended"), Is.False);

        var playerRun = new ExpeditionRunSave();
        playerRun.monsters.Add(new ExpeditionMonsterStateSave { instanceId = "transcended_player", monsterId = definition.monsterId,
            rank = ExpeditionMonsterRank.Rank3, transcended = true });
        ExpeditionAugmentCombatModifiers expected = ExpeditionAugmentRuntime.CombatModifiers(playerRun, "transcended_player", 12);
        Assert.That(expected.maxHealthBonus, Is.GreaterThan(0f));
        Assert.That(expected.attackDamageBonus, Is.GreaterThan(0f));
        progression.transcended = true;
        Refresh(unit);
        float[] multipliers = { 1f + expected.maxHealthBonus, 1f + expected.attackDamageBonus,
            1f / (1f + expected.attackSpeedBonus), expected.defenseMultiplier,
            expected.statusResistanceMultiplier, expected.skillCooldownMultiplier };
        for (int i = 0; i < stats.Length; i++)
            Assert.That(Number(unit, stats[i]), Is.EqualTo(before[i] * multipliers[i]).Within(.001f), stats[i]);
        Assert.That(Number(unit, "augmentSkillPowerMultiplier"), Is.EqualTo(expected.skillPowerMultiplier));
        Assert.That(Number(unit, "augmentSkillAreaMultiplier"), Is.EqualTo(expected.skillAreaMultiplier));
        Assert.That(Number(unit, "augmentHarmfulDurationMultiplier"), Is.EqualTo(expected.harmfulDurationMultiplier));
        Assert.That(Get(unit, "augmentTranscended"), Is.True);
        Assert.That(Vector3.Distance(Model(unit).transform.localScale, scale * expected.modelScaleMultiplier), Is.LessThan(.0001f));
    }

    [TestCase(7, 0)]
    [TestCase(9, 0)]
    [TestCase(11, 0)]
    [TestCase(12, 1)]
    [TestCase(12, 3)]
    public void OrdinaryEnemiesKeepExistingRanksEquipmentAndNonTranscendedStats(int wave, int index)
    {
        List<MonsterDefinition> roster = Roster(wave);
        object unit = Configure(wave, index, roster[index], roster);
        var expected = ExpeditionDropSystem.BuildEnemyProfile(wave, index, new System.Random(MonsterInstanceGenerator.StableSeed(
            run.runId + "_wave_" + wave + "_enemy_profile_" + index)), HunterRegionCatalog.Get(run.regionId).order,
            GameMasterData.WaveScaling(run.regionId, wave).enemyCount);
        Assert.That(Get(unit, "regionGuardian"), Is.False);
        Assert.That(Get(unit, "guardianPreview"), Is.False);
        Assert.That(Get(unit, "augmentTranscended"), Is.False);
        Assert.That(((ExpeditionMonsterStateSave)Get(unit, "enemyProgression")).transcended, Is.False);
        Assert.That(Get(unit, "expeditionRank"), Is.EqualTo(expected.rank));
        Assert.That(Items(unit).Select(item => item.id), Is.EqualTo(expected.equippedItems.Select(item => item.id)));
        Assert.That((float)Call("OffensiveMaxHealth", unit), Is.EqualTo(Number(unit, "maxHp")));
    }

    [TestCase(8, "Giant Guardian ")]
    [TestCase(12, "Guardian ")]
    public void GuardianNamesTranslateThePrefixAndSpeciesWithoutLosingKoreanOnLanguageSwitch(int wave, string prefix)
    {
        string language = GameLocalization.CurrentLanguage;
        try
        {
            MonsterDefinition definition = database.Get("lilas");
            object unit = Configure(wave, 0, definition, Roster(wave));
            string name = (string)Get(unit, "displayName");
            GameLocalization.SetLanguage(GameLocalization.English);
            Assert.That(GameLocalization.Text(name), Is.EqualTo(prefix +
                GameLocalization.Text(definition.displayName)));
            GameLocalization.SetLanguage(GameLocalization.Korean);
            Assert.That(GameLocalization.Text(name), Is.EqualTo(name));
        }
        finally { GameLocalization.SetLanguage(language); }
    }

    [Test]
    public void ActualCompositionsUseTheApprovedCountCurveAcrossAllFiveMaps()
    {
        int[] counts = { 2, 2, 3, 3, 4, 4, 5, 1, 6, 6, 7, 8 };
        Assert.That(HunterRegionCatalog.All, Has.Count.EqualTo(5));
        foreach (HunterRegionDefinition region in HunterRegionCatalog.All)
            for (int wave = 1; wave <= counts.Length; wave++)
            {
                WaveScalingMasterRow scaling = GameMasterData.WaveScaling(region.id, wave);
                Assert.That(scaling, Is.Not.Null, region.id + " Wave " + wave);
                Assert.That(scaling.enemyCount, Is.EqualTo(counts[wave - 1]), region.id + " Wave " + wave);
                Assert.That(HunterRegionCatalog.BuildExpeditionWaveMonsterIds(region.id, wave, scaling.enemyCount, run.runId, database),
                    Has.Count.EqualTo(counts[wave - 1]), region.id + " Wave " + wave);
            }
    }

    private List<MonsterDefinition> Roster(int wave) => HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
        run.regionId, wave, GameMasterData.WaveScaling(run.regionId, wave).enemyCount, run.runId, database).Select(database.Get).ToList();
    private object Configure(int wave, int index, MonsterDefinition definition, List<MonsterDefinition> roster, OwnedMonsterSave owned = null)
    {
        run.wave = wave;
        object unit = Activator.CreateInstance(unitType, true);
        owned = owned ?? PlayerProfileStore.CreateMonster(definition, "guardian_fixture_" + definition.monsterId, false);
        var model = new GameObject("Guardian model"); model.transform.SetParent(host.transform, false);
        var ring = new GameObject("Guardian ring"); ring.transform.SetParent(host.transform, false);
        Set(unit, "model", model); Set(unit, "ring", ring); Set(unit, "definition", definition); Set(unit, "ownedInstance", owned);
        Set(unit, "team", Enum.ToObject(unitType.GetField("team").FieldType, 1));
        Set(unit, "alive", true); Set(unit, "maxHp", 100f); Set(unit, "hp", 100f); Set(unit, "stableId", owned.instanceId);
        Set(unit, "baseModelScale", Vector3.one); Set(unit, "baseVisualHeightOffset", MonsterAutoBattlePrototype.PixelMonsterGroundOffset);
        Set(unit, "footprint", Vector2Int.one);
        Call("ConfigureEnemyProgression", unit, index, roster);
        return unit;
    }
    private void RemoveEquipmentAndRankTraits(object unit)
    {
        Items(unit).Clear();
        var progression = (ExpeditionMonsterStateSave)Get(unit, "enemyProgression");
        progression.rank2TraitId = ""; progression.rank3TraitId = "";
    }
    private void Refresh(object unit) => Call("RefreshUnitFromOwnedData", unit, Get(unit, "definition"), Get(unit, "ownedInstance"));
    private List<ItemMasterRow> Items(object unit) => (List<ItemMasterRow>)Get(unit, "equippedEnemyItems");
    private GameObject Model(object unit) => (GameObject)Get(unit, "model");
    private float Number(object unit, string field) => (float)Get(unit, field);
    private object Get(object unit, string field) => unitType.GetField(field).GetValue(unit);
    private void Set(object unit, string field, object value) => unitType.GetField(field).SetValue(unit, value);
    private object Call(string method, params object[] args) => battleType.GetMethod(method, Private).Invoke(battle, args);
}
