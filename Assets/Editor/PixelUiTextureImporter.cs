using UnityEditor;
using UnityEngine;

public sealed class PixelUiTextureImporter : AssetPostprocessor
{
    private const string UiRoot = "Assets/Resources/UI/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(UiRoot) || !assetPath.EndsWith(".png"))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        bool combatStatusIcon = assetPath.StartsWith(UiRoot + "CombatStatusIcons/");
        bool chestAnimation = assetPath == UiRoot + "HunterShop/chest_opening_sheet.png";
        bool chundongiAnimation = assetPath.StartsWith(UiRoot + "Chundongi/");
        importer.filterMode = combatStatusIcon ? FilterMode.Bilinear : FilterMode.Point;
        if (combatStatusIcon || chestAnimation || chundongiAnimation) importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = chundongiAnimation ? 512 : chestAnimation ? 256 : 64;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
    }
}
