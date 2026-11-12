#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MonsterPortraitGenerator
{
    private const int PortraitSize=512;
    private const string BackgroundPath=
        "Assets/Art/UI/MonsterPortraits/portrait_background.png";
    private const string OutputFolder="Assets/Resources/MonsterPortraits";
    private const string DatabasePath=
        "Assets/Resources/MonsterData/MonsterDatabase.asset";

    [MenuItem("BombBrick/Art/Generate All Monster Portraits")]
    public static void GenerateAll()
    {
        MonsterDatabase database=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            DatabasePath);
        Texture2D background=AssetDatabase.LoadAssetAtPath<Texture2D>(
            BackgroundPath);
        if(database==null)throw new InvalidOperationException(
            "Monster database not found: "+DatabasePath);
        if(background==null)throw new InvalidOperationException(
            "Portrait background not found: "+BackgroundPath);

        Directory.CreateDirectory(OutputFolder);
        List<string> failures=new List<string>();
        int generated=0;
        foreach(MonsterDefinition definition in database.monsters.Where(
            value=>value!=null&&value.enabled))
        {
            try
            {
                Generate(definition,background);
                generated++;
            }
            catch(Exception exception)
            {
                failures.Add(definition.monsterId+": "+exception.Message);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if(failures.Count>0)
            throw new InvalidOperationException("Portrait failures:\n"+
                string.Join("\n",failures));
        Debug.Log("[Monster Portraits] Generated "+generated+
                  " portraits and linked all monster definitions.");
    }

    public static void GenerateMonster(string monsterId)
    {
        MonsterDatabase database=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            DatabasePath);
        Texture2D background=AssetDatabase.LoadAssetAtPath<Texture2D>(
            BackgroundPath);
        if(database==null)throw new InvalidOperationException(
            "Monster database not found: "+DatabasePath);
        if(background==null)throw new InvalidOperationException(
            "Portrait background not found: "+BackgroundPath);
        MonsterDefinition definition=database.Get(monsterId);
        if(definition==null)throw new InvalidOperationException(
            "Monster definition not found: "+monsterId);

        Directory.CreateDirectory(OutputFolder);
        Generate(definition,background);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void Generate(
        MonsterDefinition definition,Texture2D background)
    {
        GameObject prefab=Resources.Load<GameObject>(definition.resourcePath);
        if(prefab==null)throw new InvalidOperationException(
            "Prefab not found: "+definition.resourcePath);

        Texture render=RenderPrefab(prefab);
        Texture2D composite=Composite(background,render);
        UnityEngine.Object.DestroyImmediate(render);
        string fileName=definition.monsterId+".png";
        string assetPath=OutputFolder+"/"+fileName;
        File.WriteAllBytes(Path.GetFullPath(assetPath),composite.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(composite);

        AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport);
        ConfigureSprite(assetPath);
        definition.portraitResourcePath="MonsterPortraits/"+definition.monsterId;
        EditorUtility.SetDirty(definition);
    }

    private static Texture RenderPrefab(GameObject prefab)
    {
        PreviewRenderUtility preview=new PreviewRenderUtility();
        try
        {
            preview.camera.clearFlags=CameraClearFlags.SolidColor;
            preview.camera.backgroundColor=new Color(0f,0f,0f,0f);
            preview.camera.orthographic=true;
            preview.camera.nearClipPlane=.01f;
            preview.camera.farClipPlane=100f;
            preview.camera.allowHDR=false;
            preview.camera.allowMSAA=true;

            GameObject instance=preview.InstantiatePrefabInScene(prefab);
            instance.transform.position=Vector3.zero;
            Renderer[] renderers=instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer=>renderer.enabled).ToArray();
            if(renderers.Length==0)throw new InvalidOperationException(
                "No enabled renderers.");
            Bounds bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);

            Vector3 center=bounds.center+Vector3.up*bounds.size.y*.04f;
            Vector3 viewDirection=new Vector3(.58f,.22f,1f).normalized;
            float projectedWidth=Mathf.Max(bounds.size.x,bounds.size.z*.82f);
            preview.camera.orthographicSize=Mathf.Max(
                bounds.size.y*.58f,projectedWidth*.64f)*1.14f;
            preview.camera.transform.position=center+viewDirection*12f;
            preview.camera.transform.LookAt(center);

            preview.lights[0].intensity=1.25f;
            preview.lights[0].color=new Color(1f,.91f,.8f);
            preview.lights[0].transform.rotation=Quaternion.Euler(32f,35f,0f);
            preview.lights[1].intensity=.9f;
            preview.lights[1].color=new Color(.32f,.72f,1f);
            preview.lights[1].transform.rotation=Quaternion.Euler(330f,210f,0f);
            preview.ambientColor=new Color(.32f,.36f,.44f);

            preview.BeginPreview(
                new Rect(0f,0f,PortraitSize,PortraitSize),GUIStyle.none);
            preview.Render(true);
            return CopyTexture(preview.EndPreview());
        }
        finally
        {
            preview.Cleanup();
        }
    }

    private static Texture2D CopyTexture(Texture source)
    {
        RenderTexture target=RenderTexture.GetTemporary(
            PortraitSize,PortraitSize,0,RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previous=RenderTexture.active;
        try
        {
            Graphics.Blit(source,target);
            RenderTexture.active=target;
            Texture2D result=new Texture2D(
                PortraitSize,PortraitSize,TextureFormat.RGBA32,false);
            result.ReadPixels(
                new Rect(0f,0f,PortraitSize,PortraitSize),0,0,false);
            result.Apply(false,false);
            return result;
        }
        finally
        {
            RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static Texture2D Composite(Texture background,Texture foreground)
    {
        RenderTexture target=RenderTexture.GetTemporary(
            PortraitSize,PortraitSize,0,RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previous=RenderTexture.active;
        try
        {
            Graphics.Blit(background,target);
            RenderTexture.active=target;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f,PortraitSize,PortraitSize,0f);
            Graphics.DrawTexture(
                new Rect(0f,0f,PortraitSize,PortraitSize),foreground);
            GL.PopMatrix();
            Texture2D result=new Texture2D(
                PortraitSize,PortraitSize,TextureFormat.RGBA32,false);
            result.ReadPixels(
                new Rect(0f,0f,PortraitSize,PortraitSize),0,0,false);
            result.Apply(false,false);
            return result;
        }
        finally
        {
            RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void ConfigureSprite(string assetPath)
    {
        TextureImporter importer=AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if(importer==null)return;
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100f;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Bilinear;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize=PortraitSize;
        importer.SaveAndReimport();
    }
}
#endif
