using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BombBrick.Game.Core;
using UnityEditor;
using UnityEngine;

public static class FrameDropAssetPreparation
{
    [MenuItem("Tools/BecomeZ/Prepare background combat audio loading")]
    public static void PrepareBackgroundAudioLoading()
    {
        var prepared=JsonUtility.FromJson<GameAudio.PreparedCombatSounds>(File.ReadAllText(
            "Assets/Resources/"+GameAudio.PreparedCombatSoundsPath+".json"));
        var effects=Resources.Load<PreparedEffectSoundLevels>("Audio/PreparedEffectSoundLevels");
        var paths=prepared.clips.Select(entry=>AssetDatabase.GetAssetPath(Resources.Load<AudioClip>(entry.path)))
            .Concat(effects.clips.Select(entry=>AssetDatabase.GetAssetPath(entry.clip)))
            .Where(path=>!string.IsNullOrEmpty(path)).Distinct().ToArray();
        foreach(string path in paths)
        {
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(importer==null||importer.loadInBackground)continue;
            importer.loadInBackground=true;
            importer.SaveAndReimport();
        }
    }

    [MenuItem("Tools/BecomeZ/Rebuild prepared menu and combat audio")]
    public static async void Rebuild()
    {
        var backdrop = Resources.Load<Texture2D>("Environment/MainMenu/MainMenuExpeditionBase");
        WriteMask(GameFrontend.CreateMenuSkyOccluder(backdrop), "MainMenuSkyOccluder");
        WriteMask(GameFrontend.CreateMenuSkyOccluder(backdrop, true), "MainMenuTreeOccluder");
        WriteMask(GameFrontend.CreateMenuPixelAtlas(Resources.Load<Texture2D>(
            "Environment/MainMenu/ChundongiPeekAtlas_Pixel")), "ChundongiPeekAtlas_Runtime");
        var effectPaths=AssetDatabase.FindAssets("t:Texture2D", new[] {"Assets/Resources/CombatEffects"})
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path=>path.Substring("Assets/Resources/".Length).Replace(Path.GetExtension(path),""))
            .Distinct().OrderBy(path=>path);
        File.WriteAllLines("Assets/Resources/CombatEffects/PreparedFramePaths.txt",effectPaths);

        var host = new GameObject("Combat audio preparation");
        host.SetActive(false);
        var audio = host.AddComponent<GameAudio>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var measure = typeof(GameAudio).GetMethod("MeasureCombatClip", flags);
        try
        {
            var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Concat(new[] { "Assets/Resources/Sounds/Bear Fighting Spirit Growl.wav" })
                .Where(path => AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null)
                .OrderBy(path => path).ToArray();
            var preparedClips=new List<GameAudio.PreparedCombatSound>();
            foreach(string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                await LoadAudioDataForMeasurement(clip);
                var level = measure.Invoke(audio, new object[] { clip });
                preparedClips.Add(new GameAudio.PreparedCombatSound
                {
                    path = path.Substring("Assets/Resources/".Length).Replace(Path.GetExtension(path), ""),
                    samples = clip.samples, channels = clip.channels, frequency = clip.frequency,
                    peak = (float)level.GetType().GetField("peak").GetValue(level),
                    rms = (float)level.GetType().GetField("rms").GetValue(level)
                });
            }
            var prepared=new GameAudio.PreparedCombatSounds{clips=preparedClips.ToArray()};
            File.WriteAllText("Assets/Resources/" + GameAudio.PreparedCombatSoundsPath + ".json",
                JsonUtility.ToJson(prepared, true));
            var effectAudioPaths=AssetDatabase.GetDependencies(
                AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources"})
                    .Select(AssetDatabase.GUIDToAssetPath).ToArray(),true)
                .Where(path=>!path.StartsWith("Assets/Resources/")&&
                    AssetDatabase.GetMainAssetTypeAtPath(path)==typeof(AudioClip))
                .Distinct().OrderBy(path=>path).ToArray();
            const string extraPath="Assets/Resources/Audio/PreparedEffectSoundLevels.asset";
            var extra=AssetDatabase.LoadAssetAtPath<PreparedEffectSoundLevels>(extraPath);
            if(extra==null)
            {extra=ScriptableObject.CreateInstance<PreparedEffectSoundLevels>();AssetDatabase.CreateAsset(extra,extraPath);}
            var extraClips=new List<PreparedEffectSoundLevels.Entry>();
            foreach(string path in effectAudioPaths.Concat(paths.Where(path=>path.StartsWith("Assets/Resources/Audio/Rewards/"))))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                await LoadAudioDataForMeasurement(clip);
                var level=measure.Invoke(audio,new object[]{clip});
                extraClips.Add(new PreparedEffectSoundLevels.Entry
                {
                    clip=clip,samples=clip.samples,channels=clip.channels,frequency=clip.frequency,
                    peak=(float)level.GetType().GetField("peak").GetValue(level),
                    rms=(float)level.GetType().GetField("rms").GetValue(level)
                });
            }
            extra.clips=extraClips.ToArray();
            EditorUtility.SetDirty(extra);
            AssetDatabase.SaveAssets();
            PrepareBackgroundAudioLoading();
        }
        finally { Object.DestroyImmediate(host); }
        AssetDatabase.Refresh();
    }

    private static async Task LoadAudioDataForMeasurement(AudioClip clip)
    {
        if(clip==null)throw new System.InvalidOperationException("Cannot prepare a missing audio clip.");
        clip.LoadAudioData();
        while(clip.loadState==AudioDataLoadState.Loading)await Task.Yield();
        if(clip.loadState!=AudioDataLoadState.Loaded)
            throw new System.InvalidOperationException("Audio data failed to load: "+clip.name);
    }

    private static void WriteMask(Texture2D mask, string name)
    {
        var previous = RenderTexture.active;
        var copy = RenderTexture.GetTemporary(mask.width, mask.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var readable = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false);
        string path = "Assets/Resources/Environment/MainMenu/" + name + ".png";
        try
        {
            Graphics.Blit(mask, copy);
            RenderTexture.active = copy;
            readable.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0, false);
            readable.Apply(false, false);
            File.WriteAllBytes(path, readable.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            Object.DestroyImmediate(readable);
            Object.DestroyImmediate(mask);
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }
}
