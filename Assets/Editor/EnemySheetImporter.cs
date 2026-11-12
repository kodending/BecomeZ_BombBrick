using UnityEditor;
using UnityEngine;

public sealed class EnemySheetImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Enemies/"))
            return;
        if (assetPath.StartsWith(
                "Assets/Resources/Enemies/SpriteTestVelociraptor000/Frames/"))
            return;
        if (assetPath.StartsWith(
                "Assets/Resources/Enemies/PixelRaptors/") &&
            assetPath.Contains("/Frames/"))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
    }
}
