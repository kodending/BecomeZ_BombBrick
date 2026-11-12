using UnityEditor;
using UnityEngine;

public sealed class CombatEffectTextureImporter:AssetPostprocessor
{
    private const string EffectRoot="Assets/Resources/CombatEffects/";

    private void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith(EffectRoot,System.StringComparison.Ordinal))return;
        Configure((TextureImporter)assetImporter);
    }

    [MenuItem("BombBrick/Effects/Reimport Pixel Combat Effects")]
    private static void ReimportAll()
    {
        string[] guids=AssetDatabase.FindAssets("t:Texture2D",new[]{EffectRoot});
        for(int i=0;i<guids.Length;i++)
        {
            string path=AssetDatabase.GUIDToAssetPath(guids[i]);
            if(AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                Configure(importer);
                importer.SaveAndReimport();
            }
        }
    }

    private static void Configure(TextureImporter importer)
    {
        importer.textureType=TextureImporterType.Sprite;
        bool royalSequence=importer.assetPath.EndsWith("/Griffin/RoyalWingbeatSequence.png",System.StringComparison.Ordinal);
        importer.spriteImportMode=royalSequence?SpriteImportMode.Multiple:SpriteImportMode.Single;
        importer.spritePixelsPerUnit=royalSequence?64f:32f;
        if(royalSequence)importer.maxTextureSize=2048;
        importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.npotScale=TextureImporterNPOTScale.None;
    }
}
