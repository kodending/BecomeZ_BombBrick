using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExpeditionItemMasterSetup
{
    private const string AssetPath="Assets/Resources/CombatData/GameMasterDatabase.asset";
    private const string Pool="default_equipment";
    private const int Version=3;

    private sealed class Recipe
    {
        public string id,name,a,b,effect;
        public float p1,p2,p3;
        public Recipe(string id,string name,string a,string b,string effect="None",
            float p1=0f,float p2=0f,float p3=0f)
        {this.id=id;this.name=name;this.a=a;this.b=b;this.effect=effect;this.p1=p1;this.p2=p2;this.p3=p3;}
    }

    [MenuItem("BombBrick/Data/Apply Expedition Item Master")]
    public static void Apply()
    {
        GameMasterDatabase db=AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(AssetPath);
        if(db==null)throw new InvalidOperationException("GameMasterDatabase not found: "+AssetPath);
        List<ItemMasterRow> rows=Build();
        Validate(rows);
        db.items=rows;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        GameMasterData.ClearCache();
        Debug.Log("[BombBrick] Item master applied: 8 components + 36 completed items.");
    }

    public static List<ItemMasterRow> Build()
    {
        List<ItemMasterRow> rows=new List<ItemMasterRow>
        {
            Base("base_fang","야수의 송곳니",attack:.08f),
            Base("base_feather","신속의 깃털",attackSpeed:.08f),
            Base("base_core","마력핵",skillPower:.10f),
            Base("base_sand","시간의 모래",cooldown:.08f),
            Base("base_scale","수호의 비늘",defense:10f),
            Base("base_talisman","정령의 부적",resistance:10f),
            Base("base_heart","생명의 심장",health:.15f),
            Base("base_crystal","예리한 수정",critical:.08f)
        };
        Recipe[] recipes={
            new Recipe("complete_tyrant_fang","폭군의 송곳니","base_fang","base_fang","AllDamageAmplification",.08f),
            new Recipe("complete_storm_feather","폭풍의 깃털","base_feather","base_feather","BasicAttackSpeedRamp",.02f),
            new Recipe("complete_ancient_core","고대의 마력핵","base_core","base_core","SkillPowerRamp",.05f),
            new Recipe("complete_eternal_hourglass","영겁의 모래시계","base_sand","base_sand","FirstSkillCooldownReduction",.30f),
            new Recipe("complete_immortal_carapace","불멸의 갑각","base_scale","base_scale","BasicAttackDamageReduction",.08f),
            new Recipe("complete_spirit_king_talisman","정령왕의 부적","base_talisman","base_talisman","PeriodicSelfHeal",.02f,2f),
            new Recipe("complete_giant_heart","거인의 심장","base_heart","base_heart"),
            new Recipe("complete_slaughter_crystal","살육의 수정","base_crystal","base_crystal"),
            new Recipe("complete_berserker_claw","광전사의 발톱","base_fang","base_feather","BasicAttackDamageRamp",.015f),
            new Recipe("complete_magic_blade_core","마검의 핵","base_fang","base_core","Omnivamp",.10f),
            new Recipe("complete_hunter_gear","사냥꾼의 태엽","base_fang","base_sand","PostSkillBasicDamage",.60f),
            new Recipe("complete_vengeance_armor","복수의 갑주","base_fang","base_scale","CounterGuaranteedCrit",4f),
            new Recipe("complete_blood_talisman","피의 부적","base_fang","base_talisman","BloodTalisman",.08f,.40f,.25f),
            new Recipe("complete_predator_heart","포식자의 심장","base_fang","base_heart","KillHeal",.10f),
            new Recipe("complete_executioner_eye","처형자의 눈","base_fang","base_crystal"),
            new Recipe("complete_magic_accelerator","마력 가속기","base_feather","base_core","PostSkillAttackSpeed",.25f,4f),
            new Recipe("complete_swift_hourglass","쾌속의 모래시계","base_feather","base_sand","BasicCooldownReduction",.15f),
            new Recipe("complete_battle_carapace","전투의 갑각","base_feather","base_scale","CombatCarapaceRamp",.01f,1f),
            new Recipe("complete_purifying_feather","정화의 깃털","base_feather","base_talisman","StatusRecoveryAttackSpeed",.35f,5f),
            new Recipe("complete_vampiric_feather","흡혈의 깃털","base_feather","base_heart","EveryNthBasicHeal",5f,.05f),
            new Recipe("complete_eye_of_storm","폭풍의 눈","base_feather","base_crystal","CriticalDefenseShred",.20f,5f),
            new Recipe("complete_archmage_clock","대마도사의 시계","base_core","base_sand","TimedSkillPowerRamp",.08f,4f),
            new Recipe("complete_crown_carapace","왕관의 갑각","base_core","base_scale","CrownCarapace",.15f,8f,.20f),
            new Recipe("complete_resonance_talisman","공명의 부적","base_core","base_talisman","NearbyStatusResistanceReduction",2f,15f),
            new Recipe("complete_plague_core","역병의 마력핵","base_core","base_heart","SkillPlague",.015f,4f,.30f),
            new Recipe("complete_chaos_crystal_core","혼돈의 수정핵","base_core","base_crystal","SkillCritical"),
            new Recipe("complete_guardian_hourglass","수호자의 모래시계","base_sand","base_scale","GuardianHourglass",.50f,.20f,.30f),
            new Recipe("complete_cycle_talisman","순환의 부적","base_sand","base_talisman","PostSkillDefenseResistance",10f,10f,5f),
            new Recipe("complete_redemption_heart","구원의 심장","base_sand","base_heart","PeriodicAllyHeal",.06f,6f,2f),
            new Recipe("complete_fate_crystal_clock","운명의 수정시계","base_sand","base_crystal","RandomBattleMode",.15f,.12f),
            new Recipe("complete_ironwall_charm","철벽의 수호부","base_scale","base_talisman","TargetingEnemyDefenseResistance",4f,4f),
            new Recipe("complete_sun_carapace","태양의 갑각","base_scale","base_heart","SunCarapace",.015f,4f,.30f),
            new Recipe("complete_unyielding_mirror","불굴의 거울","base_scale","base_crystal","HighHealthDamageReduction",.08f,.15f,.50f),
            new Recipe("complete_twilight_shroud","황혼의 장막","base_talisman","base_heart","TwilightShroud",.15f,10f,10f),
            new Recipe("complete_liberation_crystal","해방의 수정","base_talisman","base_crystal","OpeningStatusImmunity",6f,.04f,2f),
            new Recipe("complete_blood_crystal","피의 수정","base_heart","base_crystal","CritHeal",.02f)
        };
        Dictionary<string,ItemMasterRow> byId=rows.ToDictionary(row=>row.id);
        foreach(Recipe recipe in recipes)rows.Add(Completed(recipe,byId));
        TuneSameComponentItems(rows);
        return rows;
    }

    private static ItemMasterRow Base(string id,string name,float attack=0f,float attackSpeed=0f,
        float health=0f,float critical=0f,float skillPower=0f,float defense=0f,
        float resistance=0f,float cooldown=0f)
    {
        return new ItemMasterRow{id=id,displayName=name,itemKind="Base",itemPoolId=Pool,
            attackDamageBonus=attack,attackSpeedBonus=attackSpeed,maxHealthBonus=health,
            criticalChance=critical,skillPowerBonus=skillPower,defenseBonus=defense,
            statusResistanceBonus=resistance,skillCooldownReductionBonus=cooldown,
            specialEffect="None",enabled=true,dataVersion=Version};
    }

    private static ItemMasterRow Completed(Recipe recipe,Dictionary<string,ItemMasterRow> byId)
    {
        ItemMasterRow a=byId[recipe.a],b=byId[recipe.b];
        return new ItemMasterRow{id=recipe.id,displayName=recipe.name,itemKind="Completed",
            componentAId=recipe.a,componentBId=recipe.b,itemPoolId=Pool,
            attackDamageBonus=a.attackDamageBonus+b.attackDamageBonus,
            attackSpeedBonus=a.attackSpeedBonus+b.attackSpeedBonus,
            maxHealthBonus=a.maxHealthBonus+b.maxHealthBonus,
            criticalChance=a.criticalChance+b.criticalChance,
            criticalDamageBonus=a.criticalDamageBonus+b.criticalDamageBonus,
            skillPowerBonus=a.skillPowerBonus+b.skillPowerBonus,
            defenseBonus=a.defenseBonus+b.defenseBonus,
            statusResistanceBonus=a.statusResistanceBonus+b.statusResistanceBonus,
            skillCooldownReductionBonus=a.skillCooldownReductionBonus+b.skillCooldownReductionBonus,
            specialEffect=recipe.effect,effectParam1=recipe.p1,effectParam2=recipe.p2,
            effectParam3=recipe.p3,enabled=true,dataVersion=Version};
    }

    private static void TuneSameComponentItems(List<ItemMasterRow> rows)
    {
        ItemMasterRow tyrant=rows.Find(row=>row.id=="complete_tyrant_fang");tyrant.attackDamageBonus=.20f;
        ItemMasterRow storm=rows.Find(row=>row.id=="complete_storm_feather");storm.attackSpeedBonus=.16f;
        ItemMasterRow core=rows.Find(row=>row.id=="complete_ancient_core");core.skillPowerBonus=.20f;
        ItemMasterRow sand=rows.Find(row=>row.id=="complete_eternal_hourglass");sand.skillCooldownReductionBonus=.20f;
        ItemMasterRow shell=rows.Find(row=>row.id=="complete_immortal_carapace");shell.defenseBonus=20f;
        ItemMasterRow charm=rows.Find(row=>row.id=="complete_spirit_king_talisman");charm.statusResistanceBonus=25f;
        ItemMasterRow heart=rows.Find(row=>row.id=="complete_giant_heart");heart.maxHealthBonus=.40f;
        ItemMasterRow crystal=rows.Find(row=>row.id=="complete_slaughter_crystal");
        crystal.criticalChance=.20f;crystal.criticalDamageBonus=.30f;
        ItemMasterRow eye=rows.Find(row=>row.id=="complete_executioner_eye");eye.criticalDamageBonus=.35f;
    }

    private static void Validate(List<ItemMasterRow> rows)
    {
        if(rows.Count!=44||rows.Count(row=>row.IsBase)!=8||rows.Count(row=>row.IsCompleted)!=36)
            throw new InvalidOperationException("Expected 8 base and 36 completed items.");
        if(rows.GroupBy(row=>row.id).Any(group=>group.Count()!=1))
            throw new InvalidOperationException("Duplicate item id.");
        string[] baseIds=rows.Where(row=>row.IsBase).Select(row=>row.id).ToArray();
        for(int a=0;a<baseIds.Length;a++)for(int b=a;b<baseIds.Length;b++)
            if(!rows.Any(row=>row.IsCompleted&&
                ((row.componentAId==baseIds[a]&&row.componentBId==baseIds[b])||
                 (row.componentAId==baseIds[b]&&row.componentBId==baseIds[a]))))
                throw new InvalidOperationException("Missing recipe: "+baseIds[a]+" + "+baseIds[b]);
    }
}
