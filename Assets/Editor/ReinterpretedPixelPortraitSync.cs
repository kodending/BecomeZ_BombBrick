#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ReinterpretedPixelPortraitSync
{
    private const int PortraitSize=512;
    private const int ExpectedPortraitCount=36;
    private const string SourceFolder=
        "Assets/Art/UI/MonsterPortraits/ReinterpretedPixel";
    private const string OutputFolder="Assets/Resources/MonsterPortraits";
    private const string DatabasePath=
        "Assets/Resources/MonsterData/MonsterDatabase.asset";

    [MenuItem("BombBrick/Art/Sync Reinterpreted Pixel Portraits")]
    public static void SyncAll()
    {
        MonsterDatabase database=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            DatabasePath);
        if(database==null)
            throw new InvalidOperationException(
                "Monster database not found: "+DatabasePath);

        List<MonsterDefinition> definitions=database.monsters
            .Where(definition=>definition!=null&&definition.enabled)
            .OrderBy(definition=>definition.serialNumber,StringComparer.Ordinal)
            .ToList();
        ValidateDefinitions(definitions);
        Directory.CreateDirectory(Path.GetFullPath(OutputFolder));

        foreach(MonsterDefinition definition in definitions)
            SyncPortrait(definition);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateLoadedPortraits(definitions);
        Debug.Log("[Monster Portraits] Synced "+definitions.Count+
                  " ReinterpretedPixel portraits to Resources.");
    }

    private static void ValidateDefinitions(
        IReadOnlyList<MonsterDefinition> definitions)
    {
        if(definitions.Count!=ExpectedPortraitCount)
            throw new InvalidOperationException(
                "Expected "+ExpectedPortraitCount+
                " enabled monster definitions, found "+definitions.Count+".");

        HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        for(int index=0;index<definitions.Count;index++)
        {
            MonsterDefinition definition=definitions[index];
            string expectedSerial=(index+1).ToString("000");
            if(!string.Equals(
                   definition.serialNumber,expectedSerial,
                   StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Portrait serial mismatch at "+expectedSerial+
                    ": "+definition.serialNumber+" / "+definition.monsterId);
            if(string.IsNullOrWhiteSpace(definition.monsterId)||
               !ids.Add(definition.monsterId))
                throw new InvalidOperationException(
                    "Missing or duplicate monster id at "+expectedSerial+": "+
                    definition.monsterId);

            string sourcePath=SourcePath(definition);
            if(!File.Exists(Path.GetFullPath(sourcePath)))
                throw new FileNotFoundException(
                    "Curated portrait not found.",sourcePath);
        }
    }

    private static void SyncPortrait(MonsterDefinition definition)
    {
        string sourcePath=SourcePath(definition);
        string destinationPath=OutputFolder+"/"+definition.monsterId+".png";
        File.Copy(
            Path.GetFullPath(sourcePath),
            Path.GetFullPath(destinationPath),
            true);
        AssetDatabase.ImportAsset(
            destinationPath,
            ImportAssetOptions.ForceSynchronousImport|
            ImportAssetOptions.ForceUpdate);
        ConfigureSprite(destinationPath);

        definition.portraitResourcePath=
            "MonsterPortraits/"+definition.monsterId;
        EditorUtility.SetDirty(definition);
    }

    private static string SourcePath(MonsterDefinition definition)
    {
        return SourceFolder+"/"+definition.serialNumber+"_"+
               definition.monsterId+".png";
    }

    private static void ConfigureSprite(string assetPath)
    {
        TextureImporter importer=
            AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if(importer==null)
            throw new InvalidOperationException(
                "Texture importer not found: "+assetPath);

        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100f;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=PortraitSize;
        importer.SaveAndReimport();
    }

    private static void ValidateLoadedPortraits(
        IEnumerable<MonsterDefinition> definitions)
    {
        List<string> failures=new List<string>();
        foreach(MonsterDefinition definition in definitions)
        {
            Sprite portrait=Resources.Load<Sprite>(
                definition.portraitResourcePath);
            if(portrait==null)
            {
                failures.Add(definition.serialNumber+" "+
                             definition.monsterId+": failed to load");
                continue;
            }
            if(portrait.texture.width!=PortraitSize||
               portrait.texture.height!=PortraitSize)
                failures.Add(definition.serialNumber+" "+
                             definition.monsterId+": "+
                             portrait.texture.width+"x"+
                             portrait.texture.height);
        }

        if(failures.Count>0)
            throw new InvalidOperationException(
                "Portrait validation failed:\n"+
                string.Join("\n",failures));
    }
}
#endif
