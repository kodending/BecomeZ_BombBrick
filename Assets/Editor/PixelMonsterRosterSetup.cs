using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PixelMonsterRosterSetup
{
    public const int FirstSerial = 5;
    public const int LastSerial = 80;
    public const string SourceRoot =
        "Assets/Art/Sprites/Characters/Velociraptor";
    public const string GeneratedFrameRoot =
        "Assets/Generated/PixelMonsterSkillFrames";
    public const string GeneratedShinyFrameRoot =
        "Assets/Generated/PixelMonsterShinyFrames";
    public const string ShinyPrefabRoot =
        "Assets/Resources/Enemies/Shiny";

    private static readonly string[] Motions =
    {
        "Idle", "Run", "Attack", "SkillAttack",
        "Death", "Capture", "Struggle"
    };

    [MenuItem(
        "Tools/Monster Production/Sync DB + Build 001-080 + Create Showcase")]
    public static void SyncBuildAndCreateShowcase()
    {
        string csvUrl = GameMasterDatabaseSync.CsvUrlForSheet("Monster_Master");
        string csv;
        using (WebClient client = new WebClient())
        {
            client.Encoding = Encoding.UTF8;
            csv = client.DownloadString(csvUrl);
        }
        if (!MonsterDatabaseSyncWindow.ImportCsvAndApply(
                csv,
                out string message))
            throw new InvalidOperationException(message);
        PixelRaptorFamilySetup.BuildAll();
        BuildAll();
        MonsterAnimationShowcaseSceneSetup.CreateScene();
    }

    [MenuItem("Tools/Monster Production/Build Pixel Monsters 005-080")]
    public static void BuildAll()
    {
        try
        {
            ValidateAll();
            BuildShiny();
            Debug.Log("[Pixel Monsters] Independent frames are current.");
            return;
        }
        catch (InvalidOperationException)
        {
        }

        PixelSpriteIdleScaleNormalizer.ClearCaches();
        MonsterDatabase database = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            "Assets/Resources/MonsterData/MonsterDatabase.asset");
        List<MonsterDefinition> definitions = database == null
            ? new List<MonsterDefinition>()
            : database.monsters
                .Where(definition =>
                    definition != null &&
                    int.TryParse(definition.serialNumber, out int serial) &&
                    serial >= FirstSerial &&
                    serial <= LastSerial)
                .OrderBy(definition => definition.serialNumber)
                .ToList();
        if (definitions.Count != LastSerial - FirstSerial + 1)
            throw new InvalidOperationException(
                "Monster_Master를 먼저 동기화하세요. 005-080 활성 DB가 필요합니다.");

        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                PixelRaptorFamilySetup.ControllerPath);
        if (controller == null)
            throw new InvalidOperationException(
                "001-004 Pixel Raptor Animator Controller가 없습니다.");

        foreach (MonsterDefinition definition in definitions)
            GenerateIndependentFrames(definition);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (MonsterDefinition definition in definitions)
            Build(definition, controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateAll();
        BuildShiny();
        Debug.Log("[Pixel Monsters] 005-080 build complete.");
    }

    [MenuItem("Tools/Monster Production/Build Shiny Monsters")]
    public static void BuildShiny()
    {
        PixelSpriteIdleScaleNormalizer.ClearCaches();
        MonsterDatabase database=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            "Assets/Resources/MonsterData/MonsterDatabase.asset");
        if(database==null)
            throw new InvalidOperationException("Monster database is missing.");

        EnsureFolder(GeneratedShinyFrameRoot);
        EnsureFolder(ShinyPrefabRoot);
        List<MonsterDefinition> shinyDefinitions=new List<MonsterDefinition>();
        foreach(MonsterDefinition definition in database.monsters
                    .Where(value=>value!=null&&value.enabled)
                    .OrderBy(value=>value.serialNumber))
        {
            string sourceFolder=FindSourceFolder(definition.serialNumber);
            string shinyFolder=sourceFolder+"/Shiny";
            if(string.IsNullOrEmpty(sourceFolder)||
               !Directory.Exists(Path.GetFullPath(shinyFolder)))
            {
                if(!string.IsNullOrEmpty(definition.shinyResourcePath))
                {
                    definition.shinyResourcePath="";
                    EditorUtility.SetDirty(definition);
                }
                continue;
            }

            foreach(string motion in Motions)
                if(string.IsNullOrEmpty(FindMotionPath(shinyFolder,motion)))
                    throw new FileNotFoundException(
                        definition.serialNumber+" Shiny "+motion+" sheet is missing.");
            GenerateShinyFrames(definition,shinyFolder);
            shinyDefinitions.Add(definition);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(MonsterDefinition definition in shinyDefinitions)
            BuildShinyPrefab(
                definition,
                FindSourceFolder(definition.serialNumber)+"/Shiny");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(MonsterDefinition definition in shinyDefinitions)
        {
            GameObject prefab=Resources.Load<GameObject>(
                definition.shinyResourcePath);
            SpriteMonsterAnimationDriver driver=prefab==null
                ?null
                :prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            if(driver==null||driver.IdleFrameCount==0||driver.RunFrameCount==0||
               driver.AttackFrameCount==0||driver.SkillAttackFrameCount==0||
               driver.DeathFrameCount==0||driver.CaptureFrameCount==0||
               driver.StruggleFrameCount==0)
                throw new InvalidOperationException(
                    definition.serialNumber+" Shiny prefab is incomplete.");
        }
        PixelSpecialAnimationClipSetup.BuildAll();
        Debug.Log("[Shiny Monsters] "+shinyDefinitions.Count+" variants built.");
    }

    [MenuItem("Tools/Monster Production/Validate Pixel Monsters 005-080")]
    public static void ValidateAll()
    {
        MonsterDatabase database = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            "Assets/Resources/MonsterData/MonsterDatabase.asset");
        List<MonsterDefinition> definitions = database == null
            ? new List<MonsterDefinition>()
            : database.monsters.Where(definition =>
                definition != null &&
                int.TryParse(definition.serialNumber, out int serial) &&
                serial >= FirstSerial &&
                serial <= LastSerial).ToList();
        if (definitions.Count != LastSerial - FirstSerial + 1)
            throw new InvalidOperationException(
                "005-080 MonsterDefinition count is invalid.");

        foreach (MonsterDefinition definition in definitions)
        {
            GameObject prefab = Resources.Load<GameObject>(
                definition.resourcePath);
            SpriteMonsterAnimationDriver driver = prefab == null
                ? null
                : prefab.GetComponentInChildren<
                    SpriteMonsterAnimationDriver>(true);
            Transform visual = prefab == null
                ? null
                : prefab.transform.Find("Visual");
            if (driver == null ||
                Vector3.Distance(
                    prefab.transform.localScale,
                    Vector3.one *
                    PixelSpriteIdleScaleNormalizer.BaseMonsterScale) >
                    0.001f ||
                visual == null ||
                visual.localPosition.sqrMagnitude > 0.000001f ||
                driver.IdleFrameCount == 0 ||
                driver.RunFrameCount == 0 ||
                driver.AttackFrameCount == 0 ||
                driver.SkillAttackFrameCount == 0 ||
                driver.DeathFrameCount == 0 ||
                driver.CaptureFrameCount == 0 ||
                driver.StruggleFrameCount == 0)
                throw new InvalidOperationException(
                    definition.serialNumber +
                    " pixel prefab is incomplete.");

            SerializedObject serializedDriver = new SerializedObject(driver);
            string sourceFolder = FindSourceFolder(
                definition.serialNumber);
            foreach (string motion in Motions)
            {
                string propertyName = char.ToLowerInvariant(motion[0]) +
                                      motion.Substring(1) + "Frames";
                SerializedProperty frames =
                    serializedDriver.FindProperty(propertyName);
                float expectedPixelsPerUnit =
                    PixelSpriteIdleScaleNormalizer.CalculatePixelsPerUnit(
                        sourceFolder, motion);
                string sheetPath = FindMotionPath(
                    sourceFolder, motion);
                Vector2 expectedPivot =
                    PixelSpriteIdleScaleNormalizer.CalculateFramePivot(
                        sheetPath, motion);
                for (int index = 0; index < frames.arraySize; index++)
                {
                    Sprite frame = frames.GetArrayElementAtIndex(index)
                        .objectReferenceValue as Sprite;
                    Vector2 normalizedPivot = frame == null
                        ? Vector2.zero
                        : new Vector2(
                            frame.pivot.x / frame.rect.width,
                            frame.pivot.y / frame.rect.height);
                    if (frame == null ||
                        AssetDatabase.GetAssetPath(frame) !=
                        IndependentFramePath(
                            definition.serialNumber, motion, index) ||
                        Mathf.Abs(
                            frame.pixelsPerUnit -
                            expectedPixelsPerUnit) > 0.001f ||
                        Vector2.Distance(
                            normalizedPivot, expectedPivot) > 0.001f)
                        throw new InvalidOperationException(
                            definition.serialNumber + " " + motion +
                            " independent frame is stale.");
                }
            }
        }
    }

    public static string FindSourceFolder(string serial)
    {
        string fullRoot = Path.GetFullPath(SourceRoot);
        string folder = Directory.GetDirectories(
                fullRoot,
                serial + ".*",
                SearchOption.TopDirectoryOnly)
            .SingleOrDefault();
        return string.IsNullOrEmpty(folder)
            ? string.Empty
            : ToAssetPath(folder);
    }

    public static string FindMotionPath(
        string folder,
        string motion)
    {
        string path = PixelSpriteIdleScaleNormalizer.FindMotionSheetPath(
            folder,
            motion);
        return string.IsNullOrEmpty(path) && motion == "Struggle"
            ? PixelSpriteIdleScaleNormalizer.FindMotionSheetPath(
                folder,
                "Drag")
            : path;
    }

    private static void Build(
        MonsterDefinition definition,
        RuntimeAnimatorController controller)
    {
        string folder = FindSourceFolder(definition.serialNumber);
        if (string.IsNullOrEmpty(folder))
            throw new DirectoryNotFoundException(
                definition.serialNumber + " source folder is missing.");

        Dictionary<string, Sprite[]> frames =
            new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
        foreach (string motion in Motions)
        {
            string sheetPath = FindMotionPath(folder, motion);
            if (string.IsNullOrEmpty(sheetPath))
                throw new FileNotFoundException(
                    definition.serialNumber + " " + motion +
                    " sheet is missing.");
            string sourceMotion = sheetPath.IndexOf(
                    "_Drag",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? "Drag"
                : motion;
            float pixelsPerUnit =
                PixelSpriteIdleScaleNormalizer.CalculatePixelsPerUnit(
                    folder,
                    motion);
            PixelSpriteIdleScaleNormalizer.ConfigureSheetImporter(
                sheetPath,
                sourceMotion,
                pixelsPerUnit,
                "Pixel" + definition.serialNumber + "_" + motion);
            frames[motion] = LoadIndependentFrames(
                definition.serialNumber,
                motion,
                sheetPath,
                pixelsPerUnit);
            if (frames[motion].Length == 0)
                throw new InvalidOperationException(
                    sheetPath + " imported no Sprite frames.");
        }

        string prefabPath =
            "Assets/Resources/" + definition.resourcePath + ".prefab";
        EnsureFolder(
            Path.GetDirectoryName(prefabPath)
                .Replace('\\', '/'));
        GameObject root = new GameObject(
            Path.GetFileNameWithoutExtension(prefabPath));
        try
        {
            root.transform.localScale =
                Vector3.one *
                PixelSpriteIdleScaleNormalizer.BaseMonsterScale;
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frames["Idle"][0];
            renderer.sortingOrder = 10;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            visual.AddComponent<SpriteMonsterBillboard>();
            SpriteMonsterAnimationDriver driver =
                visual.AddComponent<SpriteMonsterAnimationDriver>();
            driver.Configure(
                frames["Idle"],
                frames["Run"],
                frames["Attack"],
                frames["SkillAttack"],
                frames["Death"],
                frames["Capture"],
                frames["Struggle"]);
            driver.ConfigureHitFrames(
                Mathf.Min(3, frames["Attack"].Length - 1),
                Mathf.Min(3, frames["SkillAttack"].Length - 1));

            SpriteMonsterAnimationEventRelay relay =
                root.AddComponent<SpriteMonsterAnimationEventRelay>();
            relay.Bind(driver);
            if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                throw new InvalidOperationException(
                    "Could not save " + prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void GenerateIndependentFrames(
        MonsterDefinition definition)
    {
        string folder = FindSourceFolder(definition.serialNumber);
        string outputFolder =
            GeneratedFrameRoot + "/" + definition.serialNumber;
        EnsureFolder(outputFolder);
        foreach (string motion in Motions)
        {
            string sheetPath = FindMotionPath(folder, motion);
            Texture2D source = new Texture2D(
                2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(
                        source,
                        File.ReadAllBytes(Path.GetFullPath(sheetPath)),
                        false) ||
                    source.height !=
                        PixelSpriteIdleScaleNormalizer.FrameHeight ||
                    source.width % source.height != 0)
                    throw new InvalidOperationException(
                        definition.serialNumber + " " + motion +
                        " sheet is not a row of 256px frames.");

                for (int index = 0;
                     index < source.width / source.height;
                     index++)
                {
                    Texture2D frame = new Texture2D(
                        source.height,
                        source.height,
                        TextureFormat.RGBA32,
                        false);
                    try
                    {
                        frame.SetPixels(source.GetPixels(
                            index * source.height,
                            0,
                            source.height,
                            source.height));
                        frame.Apply(false, false);
                        File.WriteAllBytes(
                            Path.GetFullPath(IndependentFramePath(
                                definition.serialNumber,
                                motion,
                                index)),
                            frame.EncodeToPNG());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(frame);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }

    private static void GenerateShinyFrames(
        MonsterDefinition definition,string sourceFolder)
    {
        string outputFolder=GeneratedShinyFrameRoot+"/"+definition.serialNumber;
        EnsureFolder(outputFolder);
        foreach(string motion in Motions)
        {
            string sheetPath=FindMotionPath(sourceFolder,motion);
            Texture2D source=new Texture2D(
                2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!ImageConversion.LoadImage(
                       source,File.ReadAllBytes(Path.GetFullPath(sheetPath)),false)||
                   source.height!=PixelSpriteIdleScaleNormalizer.FrameHeight||
                   source.width%source.height!=0)
                    throw new InvalidOperationException(
                        definition.serialNumber+" Shiny "+motion+
                        " sheet is not a row of 256px frames.");

                for(int index=0;index<source.width/source.height;index++)
                {
                    Texture2D frame=new Texture2D(
                        source.height,source.height,TextureFormat.RGBA32,false);
                    try
                    {
                        frame.SetPixels(source.GetPixels(
                            index*source.height,0,source.height,source.height));
                        frame.Apply(false,false);
                        string output=Path.GetFullPath(ShinyFramePath(
                            definition.serialNumber,motion,index));
                        byte[] png=frame.EncodeToPNG();
                        if(!File.Exists(output)||
                           !File.ReadAllBytes(output).SequenceEqual(png))
                            File.WriteAllBytes(output,png);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(frame);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }

    private static void BuildShinyPrefab(
        MonsterDefinition definition,string sourceFolder)
    {
        Dictionary<string,Sprite[]> frames=
            new Dictionary<string,Sprite[]>(StringComparer.Ordinal);
        foreach(string motion in Motions)
        {
            string sheetPath=FindMotionPath(sourceFolder,motion);
            int count=SourceFrameCount(sheetPath);
            float pixelsPerUnit=
                PixelSpriteIdleScaleNormalizer.CalculatePixelsPerUnit(
                    sourceFolder,motion);
            Sprite[] motionFrames=new Sprite[count];
            for(int index=0;index<count;index++)
            {
                string path=ShinyFramePath(
                    definition.serialNumber,motion,index);
                ConfigureIndependentFrame(
                    path,sheetPath,motion,pixelsPerUnit);
                motionFrames[index]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            frames[motion]=motionFrames;
        }

        GameObject basePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/"+definition.resourcePath+".prefab");
        if(basePrefab==null)
            throw new InvalidOperationException(
                definition.serialNumber+" base prefab is missing.");
        GameObject root=UnityEngine.Object.Instantiate(basePrefab);
        try
        {
            root.name=definition.monsterId+"_Shiny";
            SpriteMonsterAnimationDriver driver=
                root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            if(driver==null)
                throw new InvalidOperationException(
                    definition.serialNumber+" Sprite driver is missing.");
            driver.Configure(
                frames["Idle"],frames["Run"],frames["Attack"],
                frames["SkillAttack"],frames["Death"],frames["Capture"],
                frames["Struggle"]);
            driver.GetComponent<SpriteRenderer>().sprite=frames["Idle"][0];

            string prefabPath=ShinyPrefabRoot+"/"+definition.monsterId+".prefab";
            if(PrefabUtility.SaveAsPrefabAsset(root,prefabPath)==null)
                throw new InvalidOperationException(
                    definition.serialNumber+" Shiny prefab save failed.");
            definition.shinyResourcePath=
                "Enemies/Shiny/"+definition.monsterId;
            EditorUtility.SetDirty(definition);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string ShinyFramePath(
        string serial,string motion,int index)
    {
        return GeneratedShinyFrameRoot+"/"+serial+"/Shiny"+serial+"_"+
               motion+"_"+index.ToString("00")+".png";
    }

    private static int SourceFrameCount(string sheetPath)
    {
        Texture2D source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(
                   source,File.ReadAllBytes(Path.GetFullPath(sheetPath)),false)||
               source.height<=0||source.width%source.height!=0)
                throw new InvalidOperationException(
                    sheetPath+" is not a row of square frames.");
            return source.width/source.height;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static Sprite[] LoadIndependentFrames(
        string serial,
        string motion,
        string sheetPath,
        float pixelsPerUnit)
    {
        Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
        int frameCount = sheet.width / sheet.height;
        Sprite[] frames = new Sprite[frameCount];
        for (int index = 0; index < frameCount; index++)
        {
            string path = IndependentFramePath(serial, motion, index);
            ConfigureIndependentFrame(
                path, sheetPath, motion, pixelsPerUnit);
            frames[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (frames[index] == null)
                throw new InvalidOperationException(
                    serial + " " + motion + " frame " + index +
                    " failed to import.");
        }
        return frames;
    }

    private static void ConfigureIndependentFrame(
        string path,
        string sheetPath,
        string motion,
        float pixelsPerUnit)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException(
                "Generated frame importer is missing: " + path);

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        Vector2 pivot=PixelSpriteIdleScaleNormalizer.CalculateFramePivot(
            sheetPath,motion);
        if(importer.textureType==TextureImporterType.Sprite&&
           importer.spriteImportMode==SpriteImportMode.Single&&
           Mathf.Abs(importer.spritePixelsPerUnit-pixelsPerUnit)<0.001f&&
           importer.npotScale==TextureImporterNPOTScale.None&&
           importer.maxTextureSize==PixelSpriteIdleScaleNormalizer.FrameWidth&&
           !importer.mipmapEnabled&&importer.alphaIsTransparency&&
           importer.filterMode==FilterMode.Point&&
           importer.textureCompression==TextureImporterCompression.Uncompressed&&
           importer.wrapMode==TextureWrapMode.Clamp&&!importer.isReadable&&
           settings.spriteMeshType==SpriteMeshType.FullRect&&
           settings.spriteAlignment==(int)SpriteAlignment.Custom&&
           Vector2.Distance(settings.spritePivot,pivot)<0.001f)
            return;
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spritePixelsPerUnit = pixelsPerUnit;
        settings.npotScale = TextureImporterNPOTScale.None;
        settings.mipmapEnabled = false;
        settings.alphaIsTransparency = true;
        settings.filterMode = FilterMode.Point;
        settings.wrapMode = TextureWrapMode.Clamp;
        settings.readable = false;
        importer.SetTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = PixelSpriteIdleScaleNormalizer.FrameWidth;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    public static string IndependentFramePath(
        string serial,
        string motion,
        int index)
    {
        return GeneratedFrameRoot + "/" + serial + "/Pixel" +
               serial + "_" + motion + "_" +
               index.ToString("00") + ".png";
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Split('/').Skip(1))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }

    private static string ToAssetPath(string path)
    {
        string full = Path.GetFullPath(path).Replace('\\', '/');
        string root = Path.GetFullPath(".")
            .Replace('\\', '/')
            .TrimEnd('/') + "/";
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? full.Substring(root.Length)
            : string.Empty;
    }
}
