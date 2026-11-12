#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public readonly struct MonsterBaseStatSpec
{
    public readonly string monsterId;
    public readonly string serialNumber;
    public readonly float maxHp;
    public readonly float attackDamage;
    public readonly float attacksPerSecond;
    public readonly float defense;
    public readonly float statusResistance;
    public readonly float moveSpeed;
    public readonly int attackRange;

    public MonsterBaseStatSpec(
        string monsterId,
        string serialNumber,
        float maxHp,
        float attackDamage,
        float attacksPerSecond,
        float defense,
        float statusResistance,
        float moveSpeed,
        int attackRange)
    {
        this.monsterId=monsterId;
        this.serialNumber=serialNumber;
        this.maxHp=maxHp;
        this.attackDamage=attackDamage;
        this.attacksPerSecond=attacksPerSecond;
        this.defense=defense;
        this.statusResistance=statusResistance;
        this.moveSpeed=moveSpeed;
        this.attackRange=attackRange;
    }

    public float AttackInterval=>1f/attacksPerSecond;
}

public static class MonsterBaseStatMasterSetup
{
    private const string MonsterDatabasePath=
        "Assets/Resources/MonsterData/MonsterDatabase.asset";
    private const string GameMasterDatabasePath=
        "Assets/Resources/CombatData/GameMasterDatabase.asset";
    private const int DataVersion=4;

    public static readonly MonsterBaseStatSpec[] Specs=
    {
        new MonsterBaseStatSpec("raptor_reference_palette_v2","001",108,23,1.42f,11,12,3.05f,1),
        new MonsterBaseStatSpec("velok","002",92,29,1.30f,7,8,3.15f,1),
        new MonsterBaseStatSpec("raputo","003",100,21,1.55f,9,15,3.25f,1),
        new MonsterBaseStatSpec("wilo","004",132,18,1.02f,23,28,2.40f,1)
    };

    private readonly struct GenerationConfigSpec
    {
        public readonly string key;
        public readonly int value;
        public readonly string description;

        public GenerationConfigSpec(string key,int value,string description)
        {
            this.key=key;
            this.value=value;
            this.description=description;
        }
    }

    private static readonly GenerationConfigSpec[] GenerationSpecs=
    {
        new GenerationConfigSpec("hp_percent_min",85,"HP individual multiplier minimum (%)"),
        new GenerationConfigSpec("hp_percent_max",115,"HP individual multiplier maximum (%)"),
        new GenerationConfigSpec("damage_percent_min",85,"Attack individual multiplier minimum (%)"),
        new GenerationConfigSpec("damage_percent_max",115,"Attack individual multiplier maximum (%)"),
        new GenerationConfigSpec("attack_speed_percent_min",90,"Attack speed individual multiplier minimum (%)"),
        new GenerationConfigSpec("attack_speed_percent_max",110,"Attack speed individual multiplier maximum (%)"),
        new GenerationConfigSpec("defense_percent_min",85,"Defense individual multiplier minimum (%)"),
        new GenerationConfigSpec("defense_percent_max",115,"Defense individual multiplier maximum (%)"),
        new GenerationConfigSpec("status_resistance_percent_min",85,"Status resistance individual multiplier minimum (%)"),
        new GenerationConfigSpec("status_resistance_percent_max",115,"Status resistance individual multiplier maximum (%)"),
        new GenerationConfigSpec("move_speed_percent_min",94,"Move speed individual multiplier minimum (%)"),
        new GenerationConfigSpec("move_speed_percent_max",106,"Move speed individual multiplier maximum (%)")
    };

    [MenuItem("BombBrick/Data/Apply Monster Base Stats and Quality Config")]
    public static void Apply()
    {
        MonsterDatabase monsters=
            AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterDatabasePath);
        if(monsters==null)
            throw new InvalidOperationException(
                "Monster database not found: "+MonsterDatabasePath);

        foreach(MonsterBaseStatSpec spec in Specs)
        {
            MonsterDefinition definition=monsters.Get(spec.monsterId);
            if(definition==null)
                throw new InvalidOperationException(
                    "Monster definition not found: "+spec.monsterId);

            definition.serialNumber=spec.serialNumber;
            definition.maxHp=spec.maxHp;
            definition.attackDamage=spec.attackDamage;
            definition.attackInterval=spec.AttackInterval;
            definition.defense=spec.defense;
            definition.statusResistance=spec.statusResistance;
            definition.moveSpeedTilesPerSecond=spec.moveSpeed;
            definition.attackRange=spec.attackRange;
            definition.dataVersion=Mathf.Max(definition.dataVersion,DataVersion);
            EditorUtility.SetDirty(definition);
        }

        ApplyGenerationConfig();
        EditorUtility.SetDirty(monsters);
        AssetDatabase.SaveAssets();
        GameMasterData.ClearCache();
        Validate();
        Debug.Log(
            "[Monster Stats] Applied 001-036 base stats and individual quality ranges.");
    }

    public static void Validate()
    {
        MonsterDatabase monsters=
            AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterDatabasePath);
        if(monsters==null)
            throw new InvalidOperationException(
                "Monster database not found: "+MonsterDatabasePath);
        if(Specs.Length!=36)
            throw new InvalidOperationException(
                "Expected 36 monster stat specs, found "+Specs.Length+".");

        foreach(MonsterBaseStatSpec spec in Specs)
        {
            MonsterDefinition definition=monsters.Get(spec.monsterId);
            if(definition==null)
                throw new InvalidOperationException(
                    "Monster definition not found: "+spec.monsterId);
            RequireEqual(definition.maxHp,spec.maxHp,spec.monsterId,"maxHp");
            RequireEqual(
                definition.attackDamage,spec.attackDamage,spec.monsterId,
                "attackDamage");
            RequireEqual(
                definition.attackInterval,spec.AttackInterval,spec.monsterId,
                "attackInterval");
            RequireEqual(
                definition.defense,spec.defense,spec.monsterId,"defense");
            RequireEqual(
                definition.statusResistance,spec.statusResistance,
                spec.monsterId,"statusResistance");
            RequireEqual(
                definition.moveSpeedTilesPerSecond,spec.moveSpeed,
                spec.monsterId,"moveSpeedTilesPerSecond");
            if(definition.attackRange!=spec.attackRange)
                throw new InvalidOperationException(
                    spec.monsterId+" attackRange does not match.");
        }

        foreach(GenerationConfigSpec spec in GenerationSpecs)
        {
            int actual=GameMasterData.GenerationInt(spec.key,int.MinValue);
            if(actual!=spec.value)
                throw new InvalidOperationException(
                    spec.key+" expected "+spec.value+", found "+actual+".");
        }
    }

    private static void ApplyGenerationConfig()
    {
        GameMasterDatabase database=
            AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(
                GameMasterDatabasePath);
        if(database==null)
            throw new InvalidOperationException(
                "Game master database not found: "+GameMasterDatabasePath);
        if(database.monsterGenerationConfigs==null)
            database.monsterGenerationConfigs=new List<MasterConfigRow>();

        foreach(GenerationConfigSpec spec in GenerationSpecs)
        {
            MasterConfigRow row=database.monsterGenerationConfigs.FirstOrDefault(
                item=>item!=null&&string.Equals(
                    item.key,spec.key,StringComparison.Ordinal));
            if(row==null)
            {
                row=new MasterConfigRow{key=spec.key};
                database.monsterGenerationConfigs.Add(row);
            }

            row.valueType="Int";
            row.value=spec.value.ToString(CultureInfo.InvariantCulture);
            row.description=spec.description;
            row.enabled=true;
            row.dataVersion=DataVersion;
        }

        EditorUtility.SetDirty(database);
    }

    private static void RequireEqual(
        float actual,float expected,string monsterId,string field)
    {
        if(Mathf.Abs(actual-expected)>0.0001f)
            throw new InvalidOperationException(
                monsterId+" "+field+" expected "+
                expected.ToString(CultureInfo.InvariantCulture)+", found "+
                actual.ToString(CultureInfo.InvariantCulture)+".");
    }
}
#endif
