#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public static class HunterShopMasterSetup
{
    private const string DatabasePath=
        "Assets/Resources/CombatData/GameMasterDatabase.asset";

    private sealed class Spec
    {
        public string key;
        public string type;
        public string value;

        public Spec(string key,string type,string value)
        {
            this.key=key;
            this.type=type;
            this.value=value;
        }
    }

    private static readonly Spec[] Specs=
    {
        Region(1,"id","String","prototype_region"),
        Region(1,"display_name","String","태초의 초원"),
        Region(1,"box_cost","Int","10"),
        Region(1,"token_multiplier_percent","Int","100"),
        Region(1,"clear_bonus","Int","3"),
        Region(1,"monster_ids","String","velos|velok|raputo|wilo|geomchi|seola|nox|terakan|golgaebi|kingpa|rokrok|frosk|woongi|dami|ironkuma|berber"),
        Region(2,"id","String","poison_fog_swamp"),
        Region(2,"display_name","String","독안개 습지"),
        Region(2,"box_cost","Int","13"),
        Region(2,"token_multiplier_percent","Int","125"),
        Region(2,"clear_bonus","Int","4"),
        Region(2,"monster_ids","String","ager|kimbul|krogal|tarvil|aranea|silanti|ragon|icearachne|mantius|flotis|santius|nectis|versus|doksirum|lumirum|kingmore"),
        Region(3,"id","String","crimson_jungle"),
        Region(3,"display_name","String","붉은 밀림"),
        Region(3,"box_cost","Int","16"),
        Region(3,"token_multiplier_percent","Int","150"),
        Region(3,"clear_bonus","Int","5"),
        Region(3,"monster_ids","String","lilas|magril|bingareu|moska|nukgu|seollang|honga|wolyeong|chundongi|seolmyo|sundongi|hongryeon|chiwoo|toto|uno|maka"),
        Region(4,"id","String","frozen_highlands"),
        Region(4,"display_name","String","얼어붙은 고원"),
        Region(4,"box_cost","Int","20"),
        Region(4,"token_multiplier_percent","Int","180"),
        Region(4,"clear_bonus","Int","7"),
        Region(4,"monster_ids","String","tino|magno|cheolno|bonrex|kogason|seolgason|irontusk|gaiason|jangpung|noepung|atlagon|solarbeetle|holydeer|lunadeer|forestdeer|eclipsedeer"),
        Region(5,"id","String","sky_sanctuary"),
        Region(5,"display_name","String","천공 성역"),
        Region(5,"box_cost","Int","25"),
        Region(5,"token_multiplier_percent","Int","220"),
        Region(5,"clear_bonus","Int","9"),
        Region(5,"monster_ids","String","kongjwi|patjwi|seoljwi|byeoljwi|pone|griphonas|holophonas|entpone|vivre|igvire|frovire|temvire|haeta|yeomhae|geumgangsu|mukryeong"),
        new Spec("hunter_family_box_cost_0","Int","30"),
        new Spec("hunter_family_box_cost_1","Int","38"),
        new Spec("hunter_family_box_cost_2","Int","48"),
        new Spec("hunter_family_box_cost_3","Int","60"),
        new Spec("hunter_family_stock_count","Int","3"),
        new Spec("hunter_family_min_encountered_species","Int","4")
    };

    [MenuItem("BombBrick/Data/Apply Hunter Shop Master")]
    public static void Apply()
    {
        GameMasterDatabase database=
            AssetDatabase.LoadAssetAtPath<GameMasterDatabase>(DatabasePath);
        if(database==null)
            throw new InvalidOperationException(
                "Game master database not found: "+DatabasePath);
        if(database.expeditionConfigs==null)
            database.expeditionConfigs=new List<MasterConfigRow>();

        database.expeditionConfigs.RemoveAll(row=>
            row!=null&&row.key.StartsWith("hunter_region_",StringComparison.Ordinal));
        foreach(Spec spec in Specs)
        {
            MasterConfigRow row=database.expeditionConfigs.FirstOrDefault(
                value=>value!=null&&value.key==spec.key);
            if(row==null)
            {
                row=new MasterConfigRow{key=spec.key};
                database.expeditionConfigs.Add(row);
            }
            row.valueType=spec.type;
            row.value=spec.value;
            row.description="Hunter shop configuration";
            row.enabled=true;
            row.dataVersion=5;
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        ConfigureHunterShopPixelAssets();
        GameMasterData.ClearCache();
        Validate();
        UnityEngine.Debug.Log("[Hunter Shop] Applied five region catalogs.");
    }

    public static void Validate()
    {
        IReadOnlyList<HunterRegionDefinition> regions=HunterRegionCatalog.All;
        if(regions.Count!=5||regions.Any(region=>region.MonsterIds().Count!=16)||
           regions.SelectMany(region=>region.MonsterIds()).Distinct().Count()!=80)
            throw new InvalidOperationException(
                "Hunter regions must contain 80 unique monsters in five pools.");
        int firstFamilyCost=HunterShopSystem.CurrentFamilyBoxCost(
            new PlayerProfileSave());
        int mostExpensiveRegion=regions.Max(region=>region.regionBoxCost);
        if(firstFamilyCost!=30||firstFamilyCost<=mostExpensiveRegion)
            throw new InvalidOperationException(
                "Family boxes must start above every region box price.");
    }

    [MenuItem("BombBrick/UI/Configure Hunter Shop Pixel Assets")]
    public static void ConfigureHunterShopPixelAssets()
    {
        AssetDatabase.Refresh();
        ConfigureTexture(
            "Assets/Resources/UI/HunterShop/region_box_pixel.png");
        ConfigureTexture(
            "Assets/Resources/UI/HunterShop/family_box_pixel.png");
        ConfigureTexture(
            "Assets/Resources/UI/HunterShop/hunter_token_pixel.png");
    }

    private static void ConfigureTexture(string assetPath)
    {
        TextureImporter importer=
            AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if(importer==null)return;
        importer.textureType=TextureImporterType.Default;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.filterMode=UnityEngine.FilterMode.Point;
        importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=256;
        importer.SaveAndReimport();
    }

    private static Spec Region(int number,string suffix,string type,string value)
    {
        return new Spec("hunter_region_"+number+"_"+suffix,type,value);
    }
}
#endif
