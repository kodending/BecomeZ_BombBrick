using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class MonsterSpriteAtlasOptimizer
{
    private const string SourceRoot="Assets/Generated/PixelMonsterSkillFrames";
    private const string AtlasRoot="Assets/Generated/PixelMonsterAtlases";
    private const int FirstMonster=5;
    private const int LastMonster=80;
    private const int MonstersPerAtlas=4;

    [MenuItem("Tools/BombBrick/Optimization/Rebuild Monster Sprite Atlases")]
    public static void Rebuild()
    {
        EnsureFolder(AtlasRoot);
        List<SpriteAtlas> atlases=new List<SpriteAtlas>();
        for(int first=FirstMonster;first<=LastMonster;first+=MonstersPerAtlas)
        {
            int last=Mathf.Min(first+MonstersPerAtlas-1,LastMonster);
            string atlasPath=$"{AtlasRoot}/Monsters_{first:000}_{last:000}.spriteatlas";
            SpriteAtlas atlas=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if(atlas==null)
            {
                atlas=new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas,atlasPath);
            }
            else
            {
                UnityEngine.Object[] oldPackables=atlas.GetPackables();
                if(oldPackables.Length>0)atlas.Remove(oldPackables);
            }

            List<UnityEngine.Object> packables=new List<UnityEngine.Object>();
            for(int monster=first;monster<=last;monster++)
            {
                string folderPath=$"{SourceRoot}/{monster:000}";
                DefaultAsset folder=AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
                if(folder!=null)packables.Add(folder);
            }
            atlas.Add(packables.ToArray());
            atlas.SetIncludeInBuild(true);
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                blockOffset=1,
                enableAlphaDilation=true,
                enableRotation=false,
                enableTightPacking=false,
                padding=4
            });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                anisoLevel=0,
                filterMode=FilterMode.Point,
                generateMipMaps=false,
                readable=false,
                sRGB=true
            });
            atlas.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name="Standalone",
                overridden=true,
                maxTextureSize=4096,
                format=TextureImporterFormat.BC7,
                compressionQuality=100,
                crunchedCompression=false,
                textureCompression=TextureImporterCompression.CompressedHQ
            });
            EditorUtility.SetDirty(atlas);
            atlases.Add(atlas);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Optimization] Configured {atlases.Count} monster sprite atlases. "+
                  "Unity will pack them on demand and during the player build.");
    }

    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');
        string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }
}
