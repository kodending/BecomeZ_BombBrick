using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PixelSpriteIdleScaleNormalizer
{
    public const string SourceRoot =
        "Assets/Art/Sprites/Characters/Velociraptor";
    public static string ReferenceFolder =>
        Directory.GetDirectories(Path.GetFullPath(SourceRoot), "001.*")
            .Select(ToAssetPath)
            .FirstOrDefault() ?? string.Empty;
    public const int FrameWidth = 256;
    public const int FrameHeight = 256;
    public const int GroundLineY = 80;
    public const float BasePixelsPerUnit = 96f;
    public const float BaseMonsterScale = 1f;

    private const byte AlphaThreshold = 8;
    private static readonly string[] MotionNames =
    {
        "Idle",
        "Run",
        "Attack",
        "SkillAttack",
        "Death",
        "Capture",
        "Struggle"
    };

    private static readonly Dictionary<string, Vector2Int> OpaqueSizeCache =
        new Dictionary<string, Vector2Int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int[]> OpaqueBottomCache =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> ReferenceBottomCache =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public static void ClearCaches()
    {
        OpaqueSizeCache.Clear();
        OpaqueBottomCache.Clear();
        ReferenceBottomCache.Clear();
    }

    [MenuItem(
        "Tools/Monster Production/Upgrade 128px Sprite Canvases To 256px")]
    public static void Upgrade128CanvasesTo256()
    {
        string[] paths = Directory.GetFiles(
                Path.GetFullPath(SourceRoot),
                "*.png",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        int converted = 0;
        foreach (string path in paths)
        {
            Texture2D source = new Texture2D(
                2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(
                        source, File.ReadAllBytes(path), false) ||
                    source.height != 128)
                    continue;
                if (source.width % 128 != 0)
                    throw new InvalidOperationException(
                        path + " is not a row of 128px frames.");

                int frameCount = source.width / 128;
                int targetWidth = frameCount * FrameWidth;
                Color32[] sourcePixels = source.GetPixels32();
                Color32[] targetPixels =
                    new Color32[targetWidth * FrameHeight];
                for (int frame = 0; frame < frameCount; frame++)
                for (int y = 0; y < 128; y++)
                {
                    int sourceIndex = y * source.width + frame * 128;
                    int targetIndex =
                        (y + 64) * targetWidth + frame * FrameWidth + 64;
                    Array.Copy(
                        sourcePixels, sourceIndex,
                        targetPixels, targetIndex,
                        128);
                }

                Texture2D target = new Texture2D(
                    targetWidth,
                    FrameHeight,
                    TextureFormat.RGBA32,
                    false);
                try
                {
                    target.SetPixels32(targetPixels);
                    target.Apply(false, false);
                    File.WriteAllBytes(path, target.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
                converted++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        ClearCaches();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            "[Pixel Sprite Canvas] " + converted +
            " PNG files upgraded from 128px to 256px frames.");
    }

    [MenuItem(
        "Tools/Monster Production/Bake All Pixel Sprite Ground Lines")]
    public static void BakeAllGroundLines()
    {
        ClearCaches();
        Dictionary<string, int> shiftsBySheet =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
        string[] folders = Directory.GetDirectories(
                Path.GetFullPath(SourceRoot))
            .Select(ToAssetPath)
            .Where(IsCompletedRosterFolder)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string folder in folders)
        {
            string idlePath = FindMotionSheetPath(folder, "Idle");
            if (string.IsNullOrEmpty(idlePath))
                throw new InvalidOperationException(
                    folder + " has no Idle sheet for ground alignment.");
            int[] idleBottoms = GetOpaqueFrameBottoms(idlePath)
                .OrderBy(value => value)
                .ToArray();
            int shiftY = GroundLineY -
                idleBottoms[idleBottoms.Length / 2];

            foreach (string motionName in MotionNames)
            {
                string sheetPath = FindMotionSheetPath(folder, motionName);
                if (!string.IsNullOrEmpty(sheetPath))
                    shiftsBySheet[sheetPath] = shiftY;
            }
        }

        foreach (KeyValuePair<string, int> pair in shiftsBySheet)
            ShiftSheetVertically(pair.Key, pair.Value, false);
        foreach (KeyValuePair<string, int> pair in shiftsBySheet)
            ShiftSheetVertically(pair.Key, pair.Value, true);

        ClearCaches();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);
        NormalizeAll();
        Debug.Log(
            "[Pixel Sprite Ground] " + shiftsBySheet.Count +
            " sheets aligned to Y=" + GroundLineY + ".");
    }

    private static void ShiftSheetVertically(
        string sheetPath,
        int shiftY,
        bool write)
    {
        Texture2D source = LoadTexture(sheetPath);
        try
        {
            int frameCount = source.width / FrameWidth;
            if (source.height != FrameHeight ||
                source.width % FrameWidth != 0)
                throw new InvalidOperationException(
                    sheetPath + " is not a row of 256px frames.");
            if (shiftY == 0)
                return;
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] targetPixels =
                write ? new Color32[sourcePixels.Length] : null;
            for (int frame = 0; frame < frameCount; frame++)
            {
                for (int y = 0; y < FrameHeight; y++)
                {
                    int targetY = y + shiftY;
                    int sourceIndex =
                        y * source.width + frame * FrameWidth;
                    if (targetY < 0 || targetY >= FrameHeight)
                    {
                        for (int x = 0; x < FrameWidth; x++)
                            if (sourcePixels[sourceIndex + x].a >
                                AlphaThreshold)
                                throw new InvalidOperationException(
                                    sheetPath + " frame " + frame +
                                    " would clip at the common ground line.");
                        continue;
                    }
                    if (write)
                        Array.Copy(
                            sourcePixels, sourceIndex,
                            targetPixels,
                            targetY * source.width +
                            frame * FrameWidth,
                            FrameWidth);
                }
            }
            if (!write)
                return;

            Texture2D target = new Texture2D(
                source.width,
                FrameHeight,
                TextureFormat.RGBA32,
                false);
            try
            {
                target.SetPixels32(targetPixels);
                target.Apply(false, false);
                File.WriteAllBytes(
                    Path.GetFullPath(sheetPath),
                    target.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [MenuItem(
        "Tools/Monster Production/Normalize All Pixel Sprites To Idle Size")]
    public static void NormalizeAll()
    {
        ClearCaches();
        string[] folders = Directory.GetDirectories(
                Path.GetFullPath(SourceRoot))
            .Select(ToAssetPath)
            .Where(IsCompletedRosterFolder)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int normalizedSheetCount = 0;
        int fallbackFolderCount = 0;
        List<string> skippedFolders = new List<string>();
        StringBuilder report = new StringBuilder();

        foreach (string folder in folders)
        {
            string baselinePath = FindBaselineSheetPath(folder);
            if (string.IsNullOrEmpty(baselinePath))
            {
                skippedFolders.Add(Path.GetFileName(folder));
                continue;
            }

            bool usesBasicFallback =
                !baselinePath.EndsWith(
                    "_Idle.png",
                    StringComparison.OrdinalIgnoreCase);
            if (usesBasicFallback)
                fallbackFolderCount++;

            foreach (string motionName in MotionNames)
            {
                string sheetPath =
                    FindMotionSheetPath(folder, motionName);
                if (string.IsNullOrEmpty(sheetPath))
                    continue;

                float pixelsPerUnit = CalculatePixelsPerUnit(
                    folder,
                    motionName,
                    BasePixelsPerUnit);
                ConfigureSheetImporter(
                    sheetPath,
                    motionName,
                    pixelsPerUnit,
                    null);
                normalizedSheetCount++;

                report.AppendLine(
                    Path.GetFileName(folder) + " " + motionName +
                    " PPU=" + pixelsPerUnit.ToString("0.###"));
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);
        ValidateAll();

        Debug.Log(
            "[Pixel Sprite Idle Normalizer] " + normalizedSheetCount +
            " sheets normalized. 001 Velos remains the visual reference. " +
            fallbackFolderCount +
            " folder(s) used Basic_128 until Idle is supplied." +
            (skippedFolders.Count > 0
                ? " No usable sprite baseline: " +
                  string.Join(", ", skippedFolders)
                : string.Empty) +
            "\n" + report);
    }

    [MenuItem(
        "Tools/Monster Production/Validate Pixel Sprite Idle Sizes")]
    public static void ValidateAll()
    {
        ClearCaches();
        string[] folders = Directory.GetDirectories(
                Path.GetFullPath(SourceRoot))
            .Select(ToAssetPath)
            .Where(IsCompletedRosterFolder)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int validatedSheetCount = 0;
        foreach (string folder in folders)
        {
            if (string.IsNullOrEmpty(
                    FindBaselineSheetPath(folder)))
                continue;

            foreach (string motionName in MotionNames)
            {
                string sheetPath =
                    FindMotionSheetPath(folder, motionName);
                if (string.IsNullOrEmpty(sheetPath))
                    continue;

                TextureImporter importer =
                    AssetImporter.GetAtPath(sheetPath)
                        as TextureImporter;
                float expectedPixelsPerUnit =
                    CalculatePixelsPerUnit(
                        folder,
                        motionName,
                        BasePixelsPerUnit);
                if (importer == null ||
                    importer.textureType !=
                        TextureImporterType.Sprite ||
                    importer.spriteImportMode !=
                        SpriteImportMode.Multiple ||
                    Mathf.Abs(
                        importer.spritePixelsPerUnit -
                        expectedPixelsPerUnit) > 0.01f ||
                    importer.mipmapEnabled ||
                    importer.filterMode != FilterMode.Point ||
                    importer.textureCompression !=
                        TextureImporterCompression.Uncompressed)
                    throw new InvalidOperationException(
                        "Sprite import normalization is invalid: " +
                        sheetPath);

                Texture2D texture = LoadTexture(sheetPath);
                int expectedFrameCount;
                int frameSize;
                try
                {
                    frameSize = texture.height;
                    if (frameSize <= 0 ||
                        texture.width % frameSize != 0)
                        throw new InvalidOperationException(
                            sheetPath +
                            " must contain square frames in one row.");
                    expectedFrameCount =
                        texture.width / frameSize;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                Sprite[] importedSprites =
                    AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                        .OfType<Sprite>()
                        .OrderBy(sprite => sprite.name,
                            StringComparer.Ordinal)
                        .ToArray();
                if (importedSprites.Length != expectedFrameCount)
                    throw new InvalidOperationException(
                        sheetPath + " imported " +
                        importedSprites.Length + " frames; expected " +
                        expectedFrameCount + ".");

                Vector2[] expectedPivots = CalculateFramePivots(
                    sheetPath,
                    motionName,
                    expectedPixelsPerUnit,
                    frameSize);
                if (motionName == "Idle")
                {
                    int[] bottoms = GetOpaqueFrameBottoms(sheetPath)
                        .OrderBy(value => value)
                        .ToArray();
                    if (bottoms[bottoms.Length / 2] != GroundLineY)
                        throw new InvalidOperationException(
                            sheetPath + " artwork is not physically " +
                            "aligned to Y=" + GroundLineY + ".");
                }
                for (int index = 0;
                     index < importedSprites.Length;
                     index++)
                {
                    Sprite sprite = importedSprites[index];
                    Vector2 normalizedPivot = new Vector2(
                        sprite.pivot.x / sprite.rect.width,
                        sprite.pivot.y / sprite.rect.height);
                    if (Vector2.Distance(
                            normalizedPivot,
                            expectedPivots[index]) > 0.001f)
                        throw new InvalidOperationException(
                            sheetPath + " frame " + index +
                            " pivot is not grounded to the reference.");
                }
                validatedSheetCount++;
            }
        }

        Debug.Log(
            "[Pixel Sprite Idle Normalizer] " +
            validatedSheetCount +
            " normalized sheets validated.");
    }

    public static float CalculatePixelsPerUnit(
        string subjectFolder,
        string motionName,
        float basePixelsPerUnit = BasePixelsPerUnit)
    {
        string subjectBaseline =
            FindBaselineSheetPath(subjectFolder);
        string subjectMotion =
            FindMotionSheetPath(subjectFolder, motionName);
        string referenceBaseline =
            FindBaselineSheetPath(ReferenceFolder);
        string referenceMotion =
            FindMotionSheetPath(ReferenceFolder, motionName);

        if (string.IsNullOrEmpty(subjectBaseline) ||
            string.IsNullOrEmpty(subjectMotion) ||
            string.IsNullOrEmpty(referenceBaseline) ||
            string.IsNullOrEmpty(referenceMotion))
            return basePixelsPerUnit;

        bool useFootprint = UsesFootprintNormalization(
            subjectFolder,
            motionName);
        int subjectBaselineHeight = GetOpaqueMeasure(
            subjectBaseline,
            useFootprint);
        int subjectMotionHeight = GetOpaqueMeasure(
            subjectMotion,
            useFootprint);
        int referenceBaselineHeight = GetOpaqueMeasure(
            referenceBaseline,
            useFootprint);
        int referenceMotionHeight = GetOpaqueMeasure(
            referenceMotion,
            useFootprint);

        if (subjectBaselineHeight <= 0 ||
            subjectMotionHeight <= 0 ||
            referenceBaselineHeight <= 0 ||
            referenceMotionHeight <= 0)
            return basePixelsPerUnit;

        float subjectRatio =
            subjectMotionHeight / (float)subjectBaselineHeight;
        float referenceRatio =
            referenceMotionHeight / (float)referenceBaselineHeight;
        float result =
            basePixelsPerUnit * subjectRatio /
            Mathf.Max(0.01f, referenceRatio);
        result *= GetPixelsPerUnitCorrection(
            subjectFolder,
            motionName);
        return Mathf.Clamp(result, 48f, 192f);
    }

    public static string FindMotionSheetPath(
        string folder,
        string motionName)
    {
        if (string.IsNullOrEmpty(folder) ||
            string.IsNullOrEmpty(motionName))
            return string.Empty;

        string fullFolder = Path.GetFullPath(folder);
        if (!Directory.Exists(fullFolder))
            return string.Empty;

        string marker = "_" + motionName;
        string fullPath = Directory.GetFiles(
                fullFolder,
                "*.png",
                SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                int markerIndex = name.LastIndexOf(
                    marker,
                    StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                    return false;
                int suffixIndex = markerIndex + marker.Length;
                return suffixIndex == name.Length ||
                       name[suffixIndex] == '_';
            })
            .OrderByDescending(ExtractVersion)
            .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(fullPath) &&
            motionName == "Struggle")
            return FindMotionSheetPath(folder, "Drag");
        return string.IsNullOrEmpty(fullPath)
            ? string.Empty
            : ToAssetPath(fullPath);
    }

    private static string FindBaselineSheetPath(string folder)
    {
        string idlePath = FindMotionSheetPath(folder, "Idle");
        if (!string.IsNullOrEmpty(idlePath))
            return idlePath;

        string fullFolder = Path.GetFullPath(folder);
        if (!Directory.Exists(fullFolder))
            return string.Empty;

        string fullPath = Directory.GetFiles(
                fullFolder,
                "*Basic_128*.png",
                SearchOption.TopDirectoryOnly)
            .OrderByDescending(
                path => path,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return string.IsNullOrEmpty(fullPath)
            ? string.Empty
            : ToAssetPath(fullPath);
    }

    public static void ConfigureSheetImporter(
        string sheetPath,
        string motionName,
        float pixelsPerUnit,
        string framePrefix)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(sheetPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(
                sheetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            importer =
                AssetImporter.GetAtPath(sheetPath)
                    as TextureImporter;
        }
        if (importer == null)
            throw new InvalidOperationException(
                "Sprite sheet importer is missing: " + sheetPath);

        Texture2D texture = LoadTexture(sheetPath);
        int frameCount;
        int frameSize;
        int maxTextureSize;
        try
        {
            frameSize = texture.height;
            if (frameSize <= 0 ||
                texture.width % frameSize != 0)
                throw new InvalidOperationException(
                    sheetPath +
                    " must contain square frames in one row.");
            frameCount = texture.width / frameSize;
            maxTextureSize = Mathf.Clamp(
                Mathf.NextPowerOfTwo(
                    Mathf.Max(texture.width, texture.height)),
                32,
                8192);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        TextureImporterSettings settings =
            new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = maxTextureSize;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = false;

        string serial = ExtractSerial(sheetPath);
        string resolvedFramePrefix =
            string.IsNullOrEmpty(framePrefix)
                ? FramePrefix(serial, motionName)
                : framePrefix;
        Vector2[] framePivots = CalculateFramePivots(
            sheetPath,
            motionName,
            pixelsPerUnit,
            frameSize);
        SpriteRect[] spriteRects =
            Enumerable.Range(0, frameCount)
            .Select(index =>
            {
                string frameName =
                    resolvedFramePrefix +
                    "_" + index.ToString("00");
                GUID spriteId = new GUID(
                    Hash128.Compute(
                        sheetPath + "|" + frameName)
                    .ToString());
                return new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(
                    index * frameSize,
                    0f,
                    frameSize,
                    frameSize),
                    alignment = SpriteAlignment.Custom,
                    pivot = framePivots[index],
                    border = Vector4.zero,
                    spriteID = spriteId
                };
            })
            .ToArray();

        SpriteDataProviderFactories factories =
            new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider =
            factories.GetSpriteEditorDataProviderFromObject(
                importer);
        if (dataProvider == null)
            throw new InvalidOperationException(
                "Sprite data provider is missing: " +
                sheetPath);
        dataProvider.InitSpriteEditorDataProvider();
        dataProvider.SetSpriteRects(spriteRects);
        ISpriteNameFileIdDataProvider nameFileIdProvider =
            dataProvider.GetDataProvider<
                ISpriteNameFileIdDataProvider>();
        if (nameFileIdProvider == null)
            throw new InvalidOperationException(
                "Sprite name/file ID provider is missing: " +
                sheetPath);
        nameFileIdProvider.SetNameFileIdPairs(
            spriteRects.Select(rect =>
                new SpriteNameFileIdPair(
                    rect.name,
                    rect.spriteID)));
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static string FramePrefix(
        string serial,
        string motionName)
    {
        int serialNumber;
        string prefix =
            int.TryParse(serial, out serialNumber) &&
            serialNumber >= 1 &&
            serialNumber <= 4
                ? "Raptor"
                : "Pixel";
        return prefix + serial + "_" + motionName;
    }

    private static int GetMaximumOpaqueFrameHeight(string sheetPath)
    {
        return GetMaximumOpaqueFrameSize(sheetPath).y;
    }

    private static int GetOpaqueMeasure(
        string sheetPath,
        bool useFootprint)
    {
        Vector2Int size = GetMaximumOpaqueFrameSize(sheetPath);
        return useFootprint
            ? Mathf.Max(size.x, size.y)
            : size.y;
    }

    private static Vector2Int GetMaximumOpaqueFrameSize(string sheetPath)
    {
        Vector2Int cachedSize;
        if (OpaqueSizeCache.TryGetValue(
                sheetPath,
                out cachedSize))
            return cachedSize;

        Texture2D texture = LoadTexture(sheetPath);
        int maximumWidth = 0;
        int maximumHeight = 0;
        try
        {
            int frameSize = texture.height;
            if (frameSize <= 0 ||
                texture.width % frameSize != 0)
                throw new InvalidOperationException(
                    sheetPath +
                    " must contain square frames in one row.");

            Color32[] pixels = texture.GetPixels32();
            int frameCount = texture.width / frameSize;
            for (int frame = 0; frame < frameCount; frame++)
            {
                int minimumX = frameSize;
                int maximumX = -1;
                int minimumY = frameSize;
                int maximumY = -1;
                int frameStartX = frame * frameSize;
                for (int y = 0; y < frameSize; y++)
                for (int x = 0; x < frameSize; x++)
                {
                    int pixelIndex =
                        y * texture.width + frameStartX + x;
                    if (pixels[pixelIndex].a <= AlphaThreshold)
                        continue;
                    minimumX = Mathf.Min(minimumX, x);
                    maximumX = Mathf.Max(maximumX, x);
                    minimumY = Mathf.Min(minimumY, y);
                    maximumY = Mathf.Max(maximumY, y);
                }

                if (maximumY >= minimumY)
                {
                    maximumWidth = Mathf.Max(
                        maximumWidth,
                        maximumX - minimumX + 1);
                    maximumHeight = Mathf.Max(
                        maximumHeight,
                        maximumY - minimumY + 1);
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        Vector2Int result =
            new Vector2Int(maximumWidth, maximumHeight);
        OpaqueSizeCache[sheetPath] = result;
        return result;
    }

    private static bool UsesFootprintNormalization(
        string folder,
        string motionName)
    {
        int serial = FolderSerial(folder);
        return motionName == "SkillAttack" &&
               (serial == 13 ||
                serial == 16 ||
                serial >= 17 && serial <= 20 ||
                serial >= 25 && serial <= 28);
    }

    private static float GetPixelsPerUnitCorrection(
        string folder,
        string motionName)
    {
        int serial = FolderSerial(folder);
        bool struggle =
            motionName == "Struggle" ||
            motionName == "Drag";
        if (motionName == "Run")
        {
            if (serial == 9)
                return 0.87f;
            if (serial == 11)
                return 0.96f;
            if (serial == 12)
                return 0.94f;
        }
        if (serial >= 13 && serial <= 16 && struggle)
            return 0.96f;
        if (serial >= 17 &&
            serial <= 18 &&
            motionName == "SkillAttack")
            return 0.893f;
        if (serial >= 19 &&
            serial <= 20 &&
            motionName == "SkillAttack")
            return 0.827f;
        if (serial >= 21 && serial <= 24)
        {
            if (motionName == "Run")
                return 1.053f;
            if (motionName == "SkillAttack")
                return 0.9f;
        }
        if (serial >= 25 &&
            serial <= 28)
        {
            // Dami's raised-arm frame is taller; its neutral attack body still matches Idle.
            if (serial == 26 && motionName == "Attack")
                return 0.843f;
            if (motionName == "Run")
                return 1.099f;
            if (motionName == "SkillAttack")
                return 0.9f;
        }
        if (serial == 36 &&
            motionName == "SkillAttack")
            return 0.955f;
        if (serial >= 33 &&
            serial <= 36 &&
            motionName != "Idle")
            return 1.031f;
        if (serial == 37 && motionName == "Run")
            return 1.075f;
        if (serial >= 37 &&
            serial <= 38 &&
            motionName == "SkillAttack")
            return 0.95f;
        if (serial >= 37 &&
            serial <= 40 &&
            struggle)
            return 1.132f;
        if (serial >= 37 &&
            serial <= 40 &&
            motionName == "Death")
            return 1.075f;
        if (serial >= 41 && serial <= 44)
        {
            if (motionName == "Run")
                return serial == 41 ? 1.215f : 1.133f;
            if (motionName == "SkillAttack")
                return serial == 42 ? 0.8f : 0.84f;
            if (serial == 43 && motionName == "Attack")
                return 0.9f;
        }
        if (serial >= 45 && serial <= 48)
        {
            if (motionName == "SkillAttack")
                return 0.95f;
            if ((serial == 46 || serial == 47) && struggle)
                return 1.087f;
        }
        if (serial >= 49 && serial <= 52)
        {
            if (motionName == "Run")
                return serial <= 50
                    ? 0.92f
                    : serial == 51 ? 0.96f : 1.04f;
            if (motionName == "Attack")
            {
                if (serial == 51)
                    return 0.93f;
                if (serial == 52)
                    return 1.087f;
            }
            if (motionName == "SkillAttack")
            {
                if (serial == 49 || serial == 52)
                    return serial == 52 ? 0.9f : 0.93f;
                if (serial == 51)
                    return 1.053f;
            }
            if (struggle)
                return serial == 49 ? 0.93f : 1.087f;
            if (serial >= 51 && motionName == "Death")
                return 1.087f;
        }
        if (serial >= 53 && serial <= 56)
        {
            if (motionName == "Run")
                return serial <= 55 ? 1.08f : 1.053f;
            if (motionName == "Attack")
                return 1.04f;
            if (motionName == "Death")
                return 1.053f;
            if ((serial == 54 || serial == 55) &&
                motionName == "SkillAttack")
                return 0.95f;
            if (serial <= 55 && struggle)
            {
                if (serial == 54)
                    return 1.18f;
                return serial == 55 ? 1.1f : 1.087f;
            }
        }
        if (serial >= 57 && serial <= 60)
        {
            if (motionName == "Attack")
                return serial == 57 ? 0.64f : 0.74f;
            if (motionName == "Death")
                return 1.053f;
        }
        if (serial >= 61 && serial <= 68)
        {
            if (serial == 65 && motionName == "Run")
                return 1.05f;
            if (serial == 62 && motionName == "Run")
                return 1.04f;
            if ((serial == 63 || serial == 64) &&
                motionName == "Run")
                return 0.86f;
            if (serial <= 64 && motionName == "SkillAttack")
                return serial == 62 ? 0.64f : 0.8f;
            if (serial <= 64 && struggle)
                return 1.2f;
            if (serial >= 65 && motionName == "SkillAttack")
                return serial == 68 ? 0.72f : 0.84f;
        }
        if (serial >= 69 && serial <= 72)
        {
            if (motionName == "Run")
            {
                if (serial == 69)
                    return 0.95f;
                if (serial == 70)
                    return 0.9f;
                if (serial == 71)
                    return 0.81f;
                if (serial == 72)
                    return 1.04f;
            }
            if (serial == 72 && motionName == "Attack")
                return 0.84f;
            if (motionName == "SkillAttack")
                return serial <= 70 ? 0.684f :
                    serial == 71 ? 0.72f : 0.8f;
            if (struggle)
            {
                if (serial == 69 || serial == 71)
                    return 0.9f;
                if (serial == 70)
                    return 1.1f;
                if (serial == 72)
                    return 1.05f;
            }
            if (motionName == "Death")
                return 1.1f;
        }
        if (serial >= 73 && serial <= 80)
        {
            if (motionName == "Run")
            {
                if (serial == 73)
                    return 1.16f;
                if (serial == 74)
                    return 0.95f;
                if (serial == 75)
                    return 0.855f;
            }
            if (motionName == "Attack")
            {
                if (serial == 73)
                    return 0.95f;
                if (serial == 74)
                    return 0.72f;
                if (serial == 75)
                    return 0.64f;
                if (serial == 76)
                    return 0.81f;
                if (serial == 80)
                    return 1.1f;
            }
            if (motionName == "SkillAttack")
            {
                if (serial == 74)
                    return 0.81f;
                if (serial == 75)
                    return 0.855f;
                if (serial == 76)
                    return 0.95f;
                if (serial == 77 || serial == 80)
                    return 0.9f;
                if (serial == 78)
                    return 0.76f;
                if (serial == 79)
                    return 0.95f;
            }
            if (struggle)
            {
                if (serial == 73)
                    return 1.2f;
                if (serial >= 77 && serial <= 80)
                    return 1.1f;
                if (serial == 75)
                    return 0.9f;
            }
            if ((serial == 77 || serial == 78) &&
                motionName == "Death")
                return 0.95f;
        }
        return 1f;
    }

    private static int FolderSerial(string folder)
    {
        string name = Path.GetFileName(folder);
        if(string.Equals(name,"Shiny",StringComparison.OrdinalIgnoreCase))
            name=Path.GetFileName(Path.GetDirectoryName(folder));
        int serial;
        return !string.IsNullOrEmpty(name) &&
               name.Length >= 3 &&
               int.TryParse(name.Substring(0, 3), out serial)
            ? serial
            : 0;
    }

    private static Vector2[] CalculateFramePivots(
        string sheetPath,
        string motionName,
        float pixelsPerUnit,
        int frameSize)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            sheetPath);
        if (texture == null || texture.width % frameSize != 0)
            throw new InvalidOperationException(
                sheetPath + " must contain square frames in one row.");
        int frameCount = texture.width / frameSize;
        Vector2 pivot = CalculateFramePivot(
            sheetPath, motionName, frameSize);
        return Enumerable.Range(0, frameCount)
            .Select(_ => pivot)
            .ToArray();
    }

    public static Vector2 CalculateFramePivot(
        string sheetPath,
        string motionName,
        int frameSize = FrameHeight)
    {
        return new Vector2(
            0.5f,
            (GroundLineY +
             GetVerticalOffsetPixels(sheetPath, motionName)) /
            Mathf.Max(1f, frameSize));
    }

    private static float GetVerticalOffsetPixels(
        string sheetPath,
        string motionName)
    {
        int serial;
        int.TryParse(ExtractSerial(sheetPath), out serial);
        if (serial >= 13 && serial <= 16 &&
            (motionName == "Attack" || motionName == "SkillAttack"))
            return 3f;
        if (serial >= 69 && serial <= 72 &&
            motionName == "Attack")
            return 2f;
        if (serial >= 21 && serial <= 40)
        {
            if (motionName == "Attack")
                return serial >= 37 ? 2.5f : 1f;
            if (motionName == "SkillAttack")
                return serial >= 33 && serial <= 35 ? 2f : 1f;
        }
        if (serial >= 69 &&
            serial <= 80 &&
            motionName == "SkillAttack")
            return serial == 78 || serial == 79 ? 2f :
                serial == 80 ? 0f : 1f;
        if (serial >= 33 &&
            serial <= 36 &&
            motionName == "Run")
            return 1f;
        if (serial >= 27 &&
            serial <= 28 &&
            motionName == "Run")
            return 1f;
        if (serial >= 41 &&
            serial <= 52 &&
            motionName == "SkillAttack")
            return 1f;
        if (serial >= 53 &&
            serial <= 56 &&
            (motionName == "Run" ||
             motionName == "Attack" ||
             motionName == "SkillAttack" ||
             motionName == "Death"))
            return motionName == "SkillAttack" ? 2f : 1f;
        if (serial >= 57 &&
            serial <= 60 &&
            (motionName == "Run" ||
             motionName == "SkillAttack" ||
             motionName == "Death"))
            return motionName == "SkillAttack" ? 2f : 1f;
        if (serial == 57 && motionName == "Attack")
            return 3f;
        return 0f;
    }

    private static float GetReferenceBottomWorld(string motionName)
    {
        string referenceMotion =
            motionName == "Drag" ? "Struggle" : motionName;
        float cached;
        if (ReferenceBottomCache.TryGetValue(
                referenceMotion,
                out cached))
            return cached;

        string referencePath =
            FindMotionSheetPath(ReferenceFolder, referenceMotion);
        if (string.IsNullOrEmpty(referencePath))
            return 0f;

        int[] bottoms = GetOpaqueFrameBottoms(referencePath)
            .OrderBy(value => value)
            .ToArray();
        if (bottoms.Length == 0)
            return 0f;

        float referencePixelsPerUnit = CalculatePixelsPerUnit(
            ReferenceFolder,
            referenceMotion,
            BasePixelsPerUnit);
        float result =
            bottoms[bottoms.Length / 2] /
            Mathf.Max(1f, referencePixelsPerUnit);
        ReferenceBottomCache[referenceMotion] = result;
        return result;
    }

    private static int[] GetOpaqueFrameBottoms(string sheetPath)
    {
        int[] cached;
        if (OpaqueBottomCache.TryGetValue(sheetPath, out cached))
            return cached;

        Texture2D texture = LoadTexture(sheetPath);
        int[] bottoms;
        try
        {
            int frameSize = texture.height;
            if (frameSize <= 0 ||
                texture.width % frameSize != 0)
                throw new InvalidOperationException(
                    sheetPath +
                    " must contain square frames in one row.");

            Color32[] pixels = texture.GetPixels32();
            int frameCount = texture.width / frameSize;
            bool useClawAnchor =
                ExtractSerial(sheetPath) == "080" &&
                Path.GetFileNameWithoutExtension(sheetPath)
                    .IndexOf(
                        "_SkillAttack",
                        StringComparison.OrdinalIgnoreCase) >= 0;
            bottoms = new int[frameCount];
            for (int frame = 0; frame < frameCount; frame++)
            {
                int minimumOpaqueY = frameSize;
                int minimumClawY = frameSize;
                int frameStartX = frame * frameSize;
                for (int y = 0; y < frameSize; y++)
                for (int x = 0; x < frameSize; x++)
                {
                    int pixelIndex =
                        y * texture.width + frameStartX + x;
                    Color32 pixel = pixels[pixelIndex];
                    if (pixel.a <= AlphaThreshold)
                        continue;
                    minimumOpaqueY =
                        Mathf.Min(minimumOpaqueY, y);
                    if (useClawAnchor &&
                        pixel.r < 80 &&
                        pixel.g > 100 &&
                        pixel.b > 100)
                        minimumClawY =
                            Mathf.Min(minimumClawY, y);
                }
                int minimumY =
                    useClawAnchor &&
                    minimumClawY < frameSize
                        ? minimumClawY
                        : minimumOpaqueY;
                bottoms[frame] =
                    minimumY == frameSize ? 0 : minimumY;
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        OpaqueBottomCache[sheetPath] = bottoms;
        return bottoms;
    }

    private static Texture2D LoadTexture(string assetPath)
    {
        Texture2D texture =
            new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(
                texture,
                File.ReadAllBytes(Path.GetFullPath(assetPath)),
                false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException(
                "Could not decode sprite sheet: " + assetPath);
        }
        return texture;
    }

    private static string ExtractSerial(string assetPath)
    {
        string folderName = Path.GetFileName(
            Path.GetDirectoryName(assetPath));
        if (!string.IsNullOrEmpty(folderName) &&
            folderName.Length >= 3 &&
            char.IsDigit(folderName[0]) &&
            char.IsDigit(folderName[1]) &&
            char.IsDigit(folderName[2]))
            return folderName.Substring(0, 3);
        return "000";
    }

    private static bool IsCompletedRosterFolder(string folder)
    {
        string name = Path.GetFileName(folder);
        int serial;
        return !string.IsNullOrEmpty(name) &&
               name.Length >= 3 &&
               int.TryParse(name.Substring(0, 3), out serial) &&
               serial >= 1 &&
               serial <= PixelMonsterRosterSetup.LastSerial;
    }

    private static int ExtractVersion(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        int marker = name.LastIndexOf(
            "_v",
            StringComparison.OrdinalIgnoreCase);
        int version;
        return marker >= 0 &&
               int.TryParse(name.Substring(marker + 2), out version)
            ? version
            : 0;
    }

    private static string ToAssetPath(string path)
    {
        string normalizedPath =
            Path.GetFullPath(path).Replace('\\', '/');
        string projectRoot =
            Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');
        string prefix = projectRoot + "/";
        if (!normalizedPath.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return normalizedPath.Substring(prefix.Length);
    }
}
