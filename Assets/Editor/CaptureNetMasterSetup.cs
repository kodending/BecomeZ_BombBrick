#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CaptureNetMasterSetup
{
    private const string DatabasePath=
        "Assets/Resources/CombatData/GameMasterDatabase.asset";

    private sealed class CaptureChanceSpec
    {
        public string key;
        public float chance;
        public string description;
    }

    private static readonly CaptureChanceSpec[] Specs=
    {
        new CaptureChanceSpec
        {
            key="capture_net_common_chance",
            chance=0.25f,
            description="포획 그물 성공 확률"
        },
        new CaptureChanceSpec
        {
            key="capture_net_rare_chance",
            chance=0.75f,
            description="강화 포획 그물 성공 확률"
        }
    };

    [MenuItem("BombBrick/Data/Apply Capture Net Master")]
    public static void Apply()
    {
        GameMasterDatabase database=
            AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(DatabasePath);
        if(database==null)
            throw new InvalidOperationException(
                "Game master database not found: "+DatabasePath);
        if(database.captureConfigs==null)
            database.captureConfigs=new List<MasterConfigRow>();

        foreach(CaptureChanceSpec spec in Specs)
        {
            MasterConfigRow row=database.captureConfigs.FirstOrDefault(
                value=>value!=null&&
                       string.Equals(
                           value.key,spec.key,StringComparison.Ordinal));
            if(row==null)
            {
                row=new MasterConfigRow{key=spec.key};
                database.captureConfigs.Add(row);
            }
            row.valueType="Float";
            row.value=spec.chance.ToString(
                "0.##",CultureInfo.InvariantCulture);
            row.description=spec.description;
            row.enabled=true;
            row.dataVersion=2;
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        GameMasterData.ClearCache();
        Validate();
        Debug.Log(
            "[Capture Net] Applied Standard 25% and Rare 75%.");
    }

    public static void Validate()
    {
        foreach(CaptureChanceSpec spec in Specs)
        {
            CaptureNetDefinition definition=CaptureNetCatalog.All.First(
                value=>value.chanceConfigKey==spec.key);
            if(Mathf.Abs(definition.SuccessChance-spec.chance)>0.0001f)
                throw new InvalidOperationException(
                    spec.key+" does not match "+spec.chance+".");
        }
    }
}
#endif
