using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class GameMasterDatabaseSync
{
    private const string SpreadsheetId="YOUR_SPREADSHEET_ID";
    private const string AssetPath="Assets/Resources/CombatData/GameMasterDatabase.asset";
    private static readonly string[] SheetNames={
        "Drop_Table_Master","Skill_Master","Expedition_Config_Master",
        "Growth_Card_Rarity_Master","Wave_Scaling_Master","Trait_Master","Personality_Master",
        "Monster_Generation_Config_Master","Item_Master","Capture_Config_Master",
        "Augment_Master","Item_Transform_Master","Growth_Card_Master","Monster_Master",
        "Monster_Growth_Master","Growth_Grade_Master","Monster_Combo_Master","Localization_Master",
        "Permanent_Research_Master"
    };
    // gviz infers column types and drops minority values (including numeric text).
    // Native CSV export preserves the sheet's actual cell values.
    private static readonly Dictionary<string,int> SheetIds=new Dictionary<string,int>{
        {"Drop_Table_Master",1140629057},
        {"Skill_Master",1092113326},
        {"Expedition_Config_Master",1227743700},
        {"Growth_Card_Rarity_Master",1693634464},
        {"Wave_Scaling_Master",1460288109},
        {"Trait_Master",210322635},
        {"Personality_Master",747572074},
        {"Monster_Generation_Config_Master",1197012400},
        {"Item_Master",1589647808},
        {"Capture_Config_Master",268939839},
        {"Augment_Master",1319897266},
        {"Item_Transform_Master",821698609},
        {"Growth_Card_Master",631624024},
        {"Monster_Master",1002012538},
        {"Monster_Growth_Master",854924104},
        {"Growth_Grade_Master",1840500907},
        {"Monster_Combo_Master",733472495},
        {"Localization_Master",1858644570},
        {"Permanent_Research_Master",1888820260},
    };

    public static string CsvUrlForSheet(string sheet)
    {
        return "https://docs.google.com/spreadsheets/d/"+SpreadsheetId+
               "/export?format=csv&gid="+SheetIds[sheet].ToString(CultureInfo.InvariantCulture);
    }

    private static int sheetIndex;
    private static UnityWebRequest request;
    private static GameMasterDatabase working;

    [MenuItem("Tools/Game Database/Sync All Master Data from Google Sheet")]
    public static void SyncAll()
    {
        if(request!=null){Debug.LogWarning("[Master DB Sync] 이미 동기화 중입니다.");return;}
        sheetIndex=0;
        working=ScriptableObject.CreateInstance<GameMasterDatabase>();
        working.name="GameMasterDatabase";
        DownloadNext();
    }

    private static void DownloadNext()
    {
        if(sheetIndex>=SheetNames.Length)
        {
            SaveWorkingDatabase();
            Debug.Log("[Master DB Sync] 완료: "+SheetNames.Length+"개 Master 탭 적용");
            return;
        }
        string sheet=SheetNames[sheetIndex];
        string url=CsvUrlForSheet(sheet);
        request=UnityWebRequest.Get(url);
        request.timeout=25;
        request.SendWebRequest();
        EditorApplication.update-=Poll;
        EditorApplication.update+=Poll;
    }

    private static void Poll()
    {
        if(request==null||!request.isDone)return;
        EditorApplication.update-=Poll;
        string sheet=SheetNames[sheetIndex];
        if(request.result!=UnityWebRequest.Result.Success)
        {
            string error=request.error;
            request.Dispose();request=null;
            Cleanup();
            Debug.LogError("[Master DB Sync] "+sheet+" 다운로드 실패: "+error);
            return;
        }
        string csv=Encoding.UTF8.GetString(request.downloadHandler.data);
        request.Dispose();request=null;
        try
        {
            Import(sheet,csv);
            sheetIndex++;
            DownloadNext();
        }
        catch(Exception exception)
        {
            Cleanup();
            Debug.LogError("[Master DB Sync] "+sheet+" 적용 실패: "+exception.Message);
        }
    }

    private static void Import(string sheet,string csv)
    {
        if(sheet=="Growth_Card_Master"){CombatGrowthCardDatabaseSync.ImportCsv(csv);return;}
        if(sheet=="Localization_Master"){LocalizationDatabaseSync.ImportCsv(csv);return;}
        if(sheet=="Monster_Master")
        {
            if(!MonsterDatabaseSyncWindow.ImportCsvAndApply(csv,out string message))
                throw new InvalidDataException(message);
            return;
        }

        List<List<string>> table=ParseCsv(csv);
        if(table.Count==0)throw new InvalidDataException("빈 CSV");
        Dictionary<string,int> columns=Columns(table[0]);
        if(sheet=="Drop_Table_Master")ImportDrops(table,columns);
        else if(sheet=="Skill_Master")ImportSkills(table,columns);
        else if(sheet=="Expedition_Config_Master")working.expeditionConfigs=ImportConfigs(table,columns);
        else if(sheet=="Growth_Card_Rarity_Master")ImportRarities(table,columns);
        else if(sheet=="Wave_Scaling_Master")ImportWaveScalings(table,columns);
        else if(sheet=="Trait_Master")ImportTraits(table,columns);
        else if(sheet=="Personality_Master")ImportPersonalities(table,columns);
        else if(sheet=="Monster_Generation_Config_Master")working.monsterGenerationConfigs=ImportConfigs(table,columns);
        else if(sheet=="Item_Master")ImportItems(table,columns);
        else if(sheet=="Capture_Config_Master")working.captureConfigs=ImportConfigs(table,columns);
        else if(sheet=="Augment_Master")ImportAugments(table,columns);
        else if(sheet=="Item_Transform_Master")ImportItemTransforms(table,columns);
        else if(sheet=="Monster_Growth_Master")ImportMonsterGrowths(table,columns);
        else if(sheet=="Growth_Grade_Master")ImportGrowthGrades(table,columns);
        else if(sheet=="Monster_Combo_Master")ImportMonsterCombos(table,columns);
        else if(sheet=="Permanent_Research_Master")
            ImportPermanentResearch(table,columns);
    }

    private static void ImportDrops(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"drop_id","reward_type","min_wave","max_wave","drop_chance","min_amount",
            "max_amount","item_pool_id","commit_rule","enabled","data_version");
        for(int i=1;i<table.Count;i++)if(!Blank(table[i],c,"drop_id"))working.drops.Add(new DropMasterRow{
            id=Cell(table[i],c,"drop_id"),rewardType=Cell(table[i],c,"reward_type"),
            minWave=Int(table[i],c,"min_wave"),maxWave=Int(table[i],c,"max_wave"),
            chance=Float(table[i],c,"drop_chance"),minAmount=Int(table[i],c,"min_amount"),
            maxAmount=Int(table[i],c,"max_amount"),itemPoolId=Cell(table[i],c,"item_pool_id"),
            commitRule=Cell(table[i],c,"commit_rule"),enabled=Bool(table[i],c,"enabled"),
            dataVersion=Int(table[i],c,"data_version")});
    }

    private static void ImportSkills(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"skill_id","display_name","behavior","base_cooldown","damage_multiplier",
            "radius_tiles","duration_seconds","max_stacks","stack_bonus","enabled","data_version",
            "secondary_damage_multiplier","caster_max_hp_ratio","target_current_hp_ratio",
            "target_missing_hp_ratio","hp_scaling_cap_attack_multiplier","defense_ignore_ratio",
            "hp_scaling_true_damage","damage_per_stack_multiplier","status_effect","status_value",
            "status_duration_seconds","cast_range_tiles","damage_formula_note","implementation_status");
        for(int i=1;i<table.Count;i++)if(!Blank(table[i],c,"skill_id"))working.skills.Add(new SkillMasterRow{
            id=Cell(table[i],c,"skill_id"),displayName=Cell(table[i],c,"display_name"),
            behavior=Cell(table[i],c,"behavior"),baseCooldown=Float(table[i],c,"base_cooldown"),
            damageMultiplier=Float(table[i],c,"damage_multiplier"),radiusTiles=Float(table[i],c,"radius_tiles"),
            durationSeconds=Float(table[i],c,"duration_seconds"),maxStacks=Int(table[i],c,"max_stacks"),
            stackBonus=Float(table[i],c,"stack_bonus"),
            secondaryDamageMultiplier=OptionalFloat(table[i],c,"secondary_damage_multiplier"),
            casterMaxHpRatio=OptionalFloat(table[i],c,"caster_max_hp_ratio"),
            targetCurrentHpRatio=OptionalFloat(table[i],c,"target_current_hp_ratio"),
            targetMissingHpRatio=OptionalFloat(table[i],c,"target_missing_hp_ratio"),
            hpScalingCapAttackMultiplier=OptionalFloat(table[i],c,"hp_scaling_cap_attack_multiplier"),
            defenseIgnoreRatio=OptionalFloat(table[i],c,"defense_ignore_ratio"),
            hpScalingTrueDamage=OptionalBool(table[i],c,"hp_scaling_true_damage"),
            damagePerStackMultiplier=OptionalFloat(table[i],c,"damage_per_stack_multiplier"),
            statusEffect=Cell(table[i],c,"status_effect"),statusValue=OptionalFloat(table[i],c,"status_value"),
            statusDurationSeconds=OptionalFloat(table[i],c,"status_duration_seconds"),
            castRangeTiles=OptionalFloat(table[i],c,"cast_range_tiles"),
            damageFormulaNote=Cell(table[i],c,"damage_formula_note"),
            implementationStatus=Cell(table[i],c,"implementation_status"),
            enabled=Bool(table[i],c,"enabled"),
            dataVersion=Int(table[i],c,"data_version")});
    }

    private static List<MasterConfigRow> ImportConfigs(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"config_key","value_type","value","description","enabled","data_version");
        List<MasterConfigRow> result=new List<MasterConfigRow>();
        for(int i=1;i<table.Count;i++)if(!Blank(table[i],c,"config_key"))result.Add(new MasterConfigRow{
            key=Cell(table[i],c,"config_key"),valueType=Cell(table[i],c,"value_type"),
            value=Cell(table[i],c,"value"),description=Cell(table[i],c,"description"),
            enabled=Bool(table[i],c,"enabled"),dataVersion=Int(table[i],c,"data_version")});
        return result;
    }

    private static void ImportRarities(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"min_wave","max_wave","common_weight","rare_weight","epic_weight",
            "legendary_weight","enabled","data_version");
        for(int i=1;i<table.Count;i++)if(!Blank(table[i],c,"min_wave"))working.growthCardRarities.Add(
            new GrowthCardRarityMasterRow{minWave=Int(table[i],c,"min_wave"),maxWave=Int(table[i],c,"max_wave"),
                commonWeight=Float(table[i],c,"common_weight"),rareWeight=Float(table[i],c,"rare_weight"),
                epicWeight=Float(table[i],c,"epic_weight"),legendaryWeight=Float(table[i],c,"legendary_weight"),
                enabled=Bool(table[i],c,"enabled"),dataVersion=Int(table[i],c,"data_version")});
    }

    private static void ImportMonsterGrowths(
        List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"monster_id","display_name","family_id","growth_role",
            "hp_weight","attack_weight","attack_speed_weight","defense_weight",
            "status_resistance_weight","weight_total","rank2_required_total_growth",
            "rank3_required_total_growth","rank2_bonus_growth_count",
            "rank3_bonus_growth_count","move_speed_growth_enabled",
            "growth_grade_basis","fractional_rule","hp_points_per_stat",
            "attack_points_per_stat","attack_speed_points_per_stat",
            "defense_points_per_stat","status_resistance_points_per_stat",
            "implementation_status","balance_note");
        for(int i=1;i<table.Count;i++)
        {
            if(Blank(table[i],c,"monster_id"))continue;
            MonsterGrowthMasterRow row=new MonsterGrowthMasterRow
            {
                monsterId=Cell(table[i],c,"monster_id"),
                displayName=Cell(table[i],c,"display_name"),
                familyId=Cell(table[i],c,"family_id"),
                growthRole=Cell(table[i],c,"growth_role"),
                hpWeight=Int(table[i],c,"hp_weight"),
                attackWeight=Int(table[i],c,"attack_weight"),
                attackSpeedWeight=Int(table[i],c,"attack_speed_weight"),
                defenseWeight=Int(table[i],c,"defense_weight"),
                statusResistanceWeight=Int(table[i],c,"status_resistance_weight"),
                weightTotal=Int(table[i],c,"weight_total"),
                rank2RequiredTotalGrowth=Int(table[i],c,"rank2_required_total_growth"),
                rank3RequiredTotalGrowth=Int(table[i],c,"rank3_required_total_growth"),
                rank2BonusGrowthCount=Int(table[i],c,"rank2_bonus_growth_count"),
                rank3BonusGrowthCount=Int(table[i],c,"rank3_bonus_growth_count"),
                moveSpeedGrowthEnabled=Bool(table[i],c,"move_speed_growth_enabled"),
                growthGradeBasis=Cell(table[i],c,"growth_grade_basis"),
                fractionalRule=Cell(table[i],c,"fractional_rule"),
                hpPointsPerStat=Int(table[i],c,"hp_points_per_stat"),
                attackPointsPerStat=Int(table[i],c,"attack_points_per_stat"),
                attackSpeedPointsPerStat=Int(table[i],c,"attack_speed_points_per_stat"),
                defensePointsPerStat=Int(table[i],c,"defense_points_per_stat"),
                statusResistancePointsPerStat=Int(table[i],c,"status_resistance_points_per_stat"),
                implementationStatus=Cell(table[i],c,"implementation_status"),
                balanceNote=Cell(table[i],c,"balance_note"),
                skillScalingStat=Blank(table[i],c,"skill_scaling_stat")?"Attack":Cell(table[i],c,"skill_scaling_stat"),
                skillScalingRatio=Blank(table[i],c,"skill_scaling_ratio")?1f:Float(table[i],c,"skill_scaling_ratio"),
                scalingVersion=Blank(table[i],c,"scaling_version")?0:Int(table[i],c,"scaling_version"),
                fixedRank2TraitId=Cell(table[i],c,"fixed_rank2_trait_id"),
                fixedRank3TraitId=Cell(table[i],c,"fixed_rank3_trait_id"),
                fixedTraitVersion=Blank(table[i],c,"fixed_trait_version")?0:Int(table[i],c,"fixed_trait_version")
            };
            if((row.skillScalingStat!="Attack"&&row.skillScalingStat!="MaxHealth"&&row.skillScalingStat!="Defense")||
               float.IsNaN(row.skillScalingRatio)||float.IsInfinity(row.skillScalingRatio)||row.skillScalingRatio<=0f||
               (row.skillScalingStat=="Attack"&&row.skillScalingRatio!=1f))
                throw new InvalidDataException(row.monsterId+" invalid skill scaling.");
            int total=row.hpWeight+row.attackWeight+row.attackSpeedWeight+
                      row.defenseWeight+row.statusResistanceWeight;
            if(total!=row.weightTotal||row.weightTotal!=100)
                throw new InvalidDataException(row.monsterId+
                    " growth weights must total 100.");
            if(row.rank2RequiredTotalGrowth<=0||
               row.rank3RequiredTotalGrowth<=row.rank2RequiredTotalGrowth)
                throw new InvalidDataException(row.monsterId+
                    " growth rank thresholds are invalid.");
            working.monsterGrowths.Add(row);
        }
        if(working.monsterGrowths.GroupBy(row=>row.monsterId)
           .Any(group=>group.Count()>1))
            throw new InvalidDataException(
                "Monster_Growth_Master has duplicate monster_id rows.");
    }

    private static void ImportGrowthGrades(
        List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"growth_grade","total_growth_value","enabled",
            "calculation_basis","note");
        for(int i=1;i<table.Count;i++)
        {
            if(Blank(table[i],c,"growth_grade"))continue;
            GrowthGradeMasterRow row=new GrowthGradeMasterRow
            {
                growthGrade=Cell(table[i],c,"growth_grade"),
                totalGrowthValue=Int(table[i],c,"total_growth_value"),
                enabled=Bool(table[i],c,"enabled"),
                calculationBasis=Cell(table[i],c,"calculation_basis"),
                note=Cell(table[i],c,"note")
            };
            if(row.totalGrowthValue<=0)
                throw new InvalidDataException(row.growthGrade+
                    " growth total must be greater than zero.");
            working.growthGrades.Add(row);
        }
        if(working.growthGrades.GroupBy(row=>row.growthGrade,
               StringComparer.OrdinalIgnoreCase).Any(group=>group.Count()>1))
            throw new InvalidDataException(
                "Growth_Grade_Master has duplicate growth_grade rows.");
    }

    private static void ImportWaveScalings(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"wave","enemy_count","enemy_monster_ids","hp_multiplier",
            "attack_damage_multiplier","attack_speed_multiplier","move_speed_multiplier",
            "threat_label","enabled","data_version","region_id");
        for(int i=1;i<table.Count;i++)
        {
            if(Blank(table[i],c,"wave"))continue;
            WaveScalingMasterRow row=new WaveScalingMasterRow
            {
                regionId=Cell(table[i],c,"region_id"),
                wave=Int(table[i],c,"wave"),
                enemyCount=Int(table[i],c,"enemy_count"),
                enemyMonsterIds=Cell(table[i],c,"enemy_monster_ids"),
                hpMultiplier=Float(table[i],c,"hp_multiplier"),
                attackDamageMultiplier=Float(table[i],c,"attack_damage_multiplier"),
                attackSpeedMultiplier=Float(table[i],c,"attack_speed_multiplier"),
                moveSpeedMultiplier=Float(table[i],c,"move_speed_multiplier"),
                threatLabel=Cell(table[i],c,"threat_label"),
                enabled=Bool(table[i],c,"enabled"),
                dataVersion=Int(table[i],c,"data_version")
            };
            int parsedCount=row.MonsterIds().Count;
            if(string.IsNullOrWhiteSpace(row.regionId)||row.wave<1||
               row.enemyCount<1||parsedCount!=row.enemyCount)
                throw new InvalidDataException("Wave "+row.wave+" 적 편성 수가 enemy_count와 일치하지 않습니다.");
            if(row.hpMultiplier<=0f||row.attackDamageMultiplier<=0f||
               row.attackSpeedMultiplier<=0f||row.moveSpeedMultiplier<=0f)
                throw new InvalidDataException("Wave "+row.wave+" 배율은 모두 0보다 커야 합니다.");
            working.waveScalings.Add(row);
        }
        if(working.waveScalings.GroupBy(row=>row.regionId+"|"+row.wave)
           .Any(group=>group.Count()>1))
            throw new InvalidDataException(
                "Wave_Scaling_Master has duplicate region_id/wave rows.");
    }

    private static void ImportMonsterCombos(
        List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"monster_id","producer_tags","consumer_tags",
            "cross_unit_only","status","data_version");
        for(int i=1;i<table.Count;i++)
        {
            if(Blank(table[i],c,"monster_id"))continue;
            working.monsterCombos.Add(new MonsterComboMasterRow
            {
                monsterId=Cell(table[i],c,"monster_id"),
                producerTags=Cell(table[i],c,"producer_tags"),
                consumerTags=Cell(table[i],c,"consumer_tags"),
                crossUnitOnly=Bool(table[i],c,"cross_unit_only"),
                enabled=string.Equals(Cell(table[i],c,"status"),"확정",
                    StringComparison.Ordinal),
                dataVersion=Int(table[i],c,"data_version")
            });
        }
        if(working.monsterCombos.GroupBy(row=>row.monsterId,
               StringComparer.Ordinal).Any(group=>group.Count()>1))
            throw new InvalidDataException(
                "Monster_Combo_Master has duplicate monster_id rows.");
    }

    private static void ImportPermanentResearch(
        List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"research_id","branch","display_order","name_key",
            "description_key","max_rank","value_per_rank","value_kind",
            "capstone","enabled","data_version");
        for(int i=1;i<table.Count;i++)
        {
            if(Blank(table[i],c,"research_id"))continue;
            PermanentResearchMasterRow row=new PermanentResearchMasterRow
            {
                id=Cell(table[i],c,"research_id"),
                branch=Cell(table[i],c,"branch"),
                displayOrder=Int(table[i],c,"display_order"),
                nameKey=Cell(table[i],c,"name_key"),
                descriptionKey=Cell(table[i],c,"description_key"),
                maxRank=Int(table[i],c,"max_rank"),
                valuePerRank=Float(table[i],c,"value_per_rank"),
                valueKind=Cell(table[i],c,"value_kind"),
                capstone=Bool(table[i],c,"capstone"),
                enabled=Bool(table[i],c,"enabled"),
                dataVersion=Int(table[i],c,"data_version")
            };
            if(string.IsNullOrWhiteSpace(row.id)||row.maxRank<1)
                throw new InvalidDataException(
                    "Permanent_Research_Master contains an invalid row.");
            working.permanentResearch.Add(row);
        }
        if(working.permanentResearch.GroupBy(row=>row.id,
               StringComparer.Ordinal).Any(group=>group.Count()>1))
            throw new InvalidDataException(
                "Permanent_Research_Master has duplicate research_id rows.");
    }

    private static void ImportTraits(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"trait_id","display_name","required_tags","category","trigger_rule",
            "effect_summary","enabled","data_version");
        for(int i=1;i<table.Count;i++)if(!Blank(table[i],c,"trait_id"))working.traits.Add(new TraitMasterRow{
            id=Cell(table[i],c,"trait_id"),displayName=Cell(table[i],c,"display_name"),
            requiredTags=Cell(table[i],c,"required_tags"),category=Cell(table[i],c,"category"),
            triggerRule=Cell(table[i],c,"trigger_rule"),effectSummary=Cell(table[i],c,"effect_summary"),
            enabled=Bool(table[i],c,"enabled"),dataVersion=Int(table[i],c,"data_version")});
    }

    private static void ImportPersonalities(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"personality_id","display_name","resolution_mode","priority","behavior_summary","enabled","data_version");
        string[] axes={"courage","aggression","discipline","composure","cooperation","adaptability"};
        foreach(string axis in axes){Require(c,axis+"_weight","low_"+axis+"_weight");}
        for(int i=1;i<table.Count;i++)if(!Blank(table[i],c,"personality_id"))working.personalities.Add(
            new PersonalityMasterRow{id=Cell(table[i],c,"personality_id"),displayName=Cell(table[i],c,"display_name"),
                resolutionMode=Cell(table[i],c,"resolution_mode"),
                courageWeight=Float(table[i],c,"courage_weight"),aggressionWeight=Float(table[i],c,"aggression_weight"),
                disciplineWeight=Float(table[i],c,"discipline_weight"),composureWeight=Float(table[i],c,"composure_weight"),
                cooperationWeight=Float(table[i],c,"cooperation_weight"),adaptabilityWeight=Float(table[i],c,"adaptability_weight"),
                lowCourageWeight=Float(table[i],c,"low_courage_weight"),lowAggressionWeight=Float(table[i],c,"low_aggression_weight"),
                lowDisciplineWeight=Float(table[i],c,"low_discipline_weight"),lowComposureWeight=Float(table[i],c,"low_composure_weight"),
                lowCooperationWeight=Float(table[i],c,"low_cooperation_weight"),lowAdaptabilityWeight=Float(table[i],c,"low_adaptability_weight"),
                priority=Int(table[i],c,"priority"),behaviorSummary=Cell(table[i],c,"behavior_summary"),
                enabled=Bool(table[i],c,"enabled"),dataVersion=Int(table[i],c,"data_version")});
    }

    private static void ImportItems(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"item_id","display_name","item_kind","component_a_id","component_b_id","item_pool_id",
            "attack_damage_bonus","attack_speed_bonus","max_health_bonus","damage_reduction_bonus",
            "critical_chance","critical_damage_bonus","skill_power_bonus","special_effect",
            "defense_bonus","status_resistance_bonus","skill_cooldown_reduction_bonus",
            "effect_param_1","effect_param_2","effect_param_3","enabled","data_version");
        for(int i=1;i<table.Count;i++)if(!Blank(table[i],c,"item_id"))working.items.Add(new ItemMasterRow{
            id=Cell(table[i],c,"item_id"),displayName=Cell(table[i],c,"display_name"),
            itemKind=Cell(table[i],c,"item_kind"),componentAId=Cell(table[i],c,"component_a_id"),
            componentBId=Cell(table[i],c,"component_b_id"),itemPoolId=Cell(table[i],c,"item_pool_id"),
            attackDamageBonus=Float(table[i],c,"attack_damage_bonus"),
            attackSpeedBonus=Float(table[i],c,"attack_speed_bonus"),
            maxHealthBonus=Float(table[i],c,"max_health_bonus"),
            damageReductionBonus=Float(table[i],c,"damage_reduction_bonus"),
            criticalChance=Float(table[i],c,"critical_chance"),
            criticalDamageBonus=Float(table[i],c,"critical_damage_bonus"),
            skillPowerBonus=Float(table[i],c,"skill_power_bonus"),
            defenseBonus=Float(table[i],c,"defense_bonus"),
            statusResistanceBonus=Float(table[i],c,"status_resistance_bonus"),
            skillCooldownReductionBonus=Float(table[i],c,"skill_cooldown_reduction_bonus"),
            specialEffect=Cell(table[i],c,"special_effect"),
            effectParam1=Float(table[i],c,"effect_param_1"),
            effectParam2=Float(table[i],c,"effect_param_2"),
            effectParam3=Float(table[i],c,"effect_param_3"),
            enabled=Bool(table[i],c,"enabled"),
            dataVersion=Int(table[i],c,"data_version")});
    }

    private static void ImportAugments(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"augment_id","display_name","category","offer_waves","selection_mode",
            "target_rule","effect_summary","trigger_rule","numeric_params","duration_rule",
            "granted_item_id","conflict_ids","eligibility_rule","retry_rule","enabled","data_version");
        for(int i=1;i<table.Count;i++)
        {
            if(Blank(table[i],c,"augment_id"))continue;
            working.augments.Add(new AugmentMasterRow
            {
                id=Cell(table[i],c,"augment_id"),
                displayName=Cell(table[i],c,"display_name"),
                category=Cell(table[i],c,"category"),
                offerWaves=Cell(table[i],c,"offer_waves"),
                selectionMode=Cell(table[i],c,"selection_mode"),
                targetRule=Cell(table[i],c,"target_rule"),
                effectSummary=Cell(table[i],c,"effect_summary"),
                triggerRule=Cell(table[i],c,"trigger_rule"),
                numericParams=Cell(table[i],c,"numeric_params"),
                durationRule=Cell(table[i],c,"duration_rule"),
                grantedItemId=Cell(table[i],c,"granted_item_id"),
                conflictIds=Cell(table[i],c,"conflict_ids"),
                eligibilityRule=Cell(table[i],c,"eligibility_rule"),
                retryRule=Cell(table[i],c,"retry_rule"),
                enabled=Bool(table[i],c,"enabled"),
                dataVersion=Int(table[i],c,"data_version")
            });
        }
    }

    private static void ImportItemTransforms(List<List<string>> table,Dictionary<string,int> c)
    {
        Require(c,"item_id","display_name","overcharge_effect","overcharge_params",
            "corruption_great_name","corruption_great_effect_id","corruption_great_effect",
            "corruption_great_params","corruption_success_effect","corruption_no_change_effect",
            "corruption_failure_effect","corruption_failure_params","great_success_percent",
            "success_percent","no_change_percent","failure_percent","transform_lock_rule",
            "enabled","data_version");
        for(int i=1;i<table.Count;i++)
        {
            if(Blank(table[i],c,"item_id"))continue;
            ItemTransformMasterRow row=new ItemTransformMasterRow
            {
                itemId=Cell(table[i],c,"item_id"),
                displayName=Cell(table[i],c,"display_name"),
                overchargeEffect=Cell(table[i],c,"overcharge_effect"),
                overchargeParams=Cell(table[i],c,"overcharge_params"),
                corruptionGreatName=Cell(table[i],c,"corruption_great_name"),
                corruptionGreatEffectId=Cell(table[i],c,"corruption_great_effect_id"),
                corruptionGreatEffect=Cell(table[i],c,"corruption_great_effect"),
                corruptionGreatParams=Cell(table[i],c,"corruption_great_params"),
                corruptionSuccessEffect=Cell(table[i],c,"corruption_success_effect"),
                corruptionNoChangeEffect=Cell(table[i],c,"corruption_no_change_effect"),
                corruptionFailureEffect=Cell(table[i],c,"corruption_failure_effect"),
                corruptionFailureParams=Cell(table[i],c,"corruption_failure_params"),
                greatSuccessPercent=Int(table[i],c,"great_success_percent"),
                successPercent=Int(table[i],c,"success_percent"),
                noChangePercent=Int(table[i],c,"no_change_percent"),
                failurePercent=Int(table[i],c,"failure_percent"),
                transformLockRule=Cell(table[i],c,"transform_lock_rule"),
                enabled=Bool(table[i],c,"enabled"),
                dataVersion=Int(table[i],c,"data_version")
            };
            int total=row.greatSuccessPercent+row.successPercent+
                      row.noChangePercent+row.failurePercent;
            if(total!=100)
                throw new InvalidDataException(row.itemId+" transform probability total must be 100.");
            if(GameMasterData.Item(row.itemId)==null&&
               !working.items.Exists(item=>item!=null&&item.id==row.itemId&&item.enabled))
                throw new InvalidDataException(row.itemId+" transform references an unknown item.");
            working.itemTransforms.Add(row);
        }
    }

    private static void SaveWorkingDatabase()
    {
        ValidateFixedTraits(working);
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        GameMasterDatabase asset=AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(AssetPath);
        if(asset==null){asset=working;AssetDatabase.CreateAsset(asset,AssetPath);}
        else{EditorUtility.CopySerialized(working,asset);UnityEngine.Object.DestroyImmediate(working);}
        working=null;
        asset.name="GameMasterDatabase";
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        GameMasterData.ClearCache();
    }

    private static void ValidateFixedTraits(GameMasterDatabase data)
    {
        MonsterDatabase monsters=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        foreach(MonsterDefinition monster in monsters.monsters.Where(m=>m!=null&&m.enabled))
        {
            MonsterGrowthMasterRow growth=data.monsterGrowths.FirstOrDefault(r=>r.monsterId==monster.monsterId);
            if(growth==null||growth.fixedTraitVersion<1)
                throw new InvalidDataException(monster.monsterId+" is missing fixed growth traits.");
            foreach(int rank in new[]{2,3})
            {
                string id=rank==2?growth.fixedRank2TraitId:growth.fixedRank3TraitId;
                TraitMasterRow row=data.traits.FirstOrDefault(r=>r.id==id&&r.enabled);
                MonsterTraitDefinition trait=row==null?null:new MonsterTraitDefinition(
                    row.id,row.displayName,MonsterTraitCatalog.ParseTags(row.requiredTags),row.category);
                if(row==null||trait==null||!trait.IsCompatible(monster)||
                   (rank==2?row.category!="Rank2Enhancement":row.category=="Rank2Enhancement"||row.category=="Rank4Transcendence"))
                    throw new InvalidDataException(monster.monsterId+" has an invalid fixed rank-"+rank+" trait: "+id);
            }
        }
    }

    private static void Cleanup()
    {
        if(request!=null){request.Dispose();request=null;}
        if(working!=null)UnityEngine.Object.DestroyImmediate(working);
        working=null;
    }

    private static Dictionary<string,int> Columns(List<string> header)
    {
        Dictionary<string,int> result=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<header.Count;i++){string key=(header[i]??"").Trim();if(key.Length>0&&!result.ContainsKey(key))result.Add(key,i);}
        return result;
    }
    private static void Require(Dictionary<string,int> c,params string[] headers)
    {
        foreach(string header in headers)if(!c.ContainsKey(header))throw new InvalidDataException("필수 열 누락: "+header);
    }
    private static string Cell(List<string> row,Dictionary<string,int> c,string key)
    {return c.TryGetValue(key,out int i)&&i<row.Count?(row[i]??"").Trim():"";}
    private static bool Blank(List<string> row,Dictionary<string,int> c,string key){return string.IsNullOrWhiteSpace(Cell(row,c,key));}
    private static int Int(List<string> row,Dictionary<string,int> c,string key)
    {if(int.TryParse(Cell(row,c,key),NumberStyles.Integer,CultureInfo.InvariantCulture,out int v))return v;throw new InvalidDataException(key+" 정수 변환 실패");}
    private static float Float(List<string> row,Dictionary<string,int> c,string key)
    {if(float.TryParse(Cell(row,c,key),NumberStyles.Float,CultureInfo.InvariantCulture,out float v))return v;throw new InvalidDataException(key+" 숫자 변환 실패");}
    private static bool Bool(List<string> row,Dictionary<string,int> c,string key)
    {string raw=Cell(row,c,key);if(bool.TryParse(raw,out bool v))return v;if(raw=="1")return true;if(raw=="0")return false;throw new InvalidDataException(key+" bool 변환 실패");}

    private static float OptionalFloat(List<string> row,Dictionary<string,int> c,string key)
    {
        string raw=Cell(row,c,key);
        return float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)
            ?value:0f;
    }

    private static bool OptionalBool(List<string> row,Dictionary<string,int> c,string key)
    {
        string raw=Cell(row,c,key);
        if(bool.TryParse(raw,out bool value))return value;
        return raw=="1";
    }

    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows=new List<List<string>>();List<string> row=new List<string>();StringBuilder field=new StringBuilder();bool quoted=false;
        for(int i=0;i<text.Length;i++){char ch=text[i];if(ch=='"'){if(quoted&&i+1<text.Length&&text[i+1]=='"'){field.Append('"');i++;}else quoted=!quoted;}
            else if(ch==','&&!quoted){row.Add(field.ToString());field.Length=0;}
            else if((ch=='\r'||ch=='\n')&&!quoted){if(ch=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row.Add(field.ToString());field.Length=0;rows.Add(row);row=new List<string>();}
            else field.Append(ch);}
        if(field.Length>0||row.Count>0){row.Add(field.ToString());rows.Add(row);}return rows;
    }
}
