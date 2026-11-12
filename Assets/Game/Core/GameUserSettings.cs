using System;
using UnityEngine;

namespace BombBrick.Game.Core
{
    [Serializable]
    public sealed class GameUserSettings
    {
        public const string PreferenceKey = "BombBrick.UserSettings.v1";
        public int width = 1600;
        public int height = 900;
        public bool fullscreen = true;
        public bool vSync;
        public int frameRate = 60;
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float effectsVolume = 1f;
        public bool showFps;
        public int cameraShake = 2;
        public bool reduceFlashes;
        public bool pauseWhenUnfocused = true;
        public bool muteWhenUnfocused = true;
        public string language = GameLocalization.Korean;

        public float CameraShakeScale => cameraShake == 0 ? 0f : cameraShake == 1 ? 0.35f : 1f;

        public static GameUserSettings Load()
        {
            GameUserSettings settings = null;
            try
            {
                if (PlayerPrefs.HasKey(PreferenceKey))
                    settings = JsonUtility.FromJson<GameUserSettings>(PlayerPrefs.GetString(PreferenceKey));
                else
                {
                    string legacy = ReadLegacyWindowsSettings();
                    if (!string.IsNullOrEmpty(legacy))
                    {
                        settings = JsonUtility.FromJson<GameUserSettings>(legacy);
                        if (settings != null)
                        {
                            PlayerPrefs.SetString(PreferenceKey, legacy);
                            PlayerPrefs.Save();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Settings] 기본 설정으로 복구: " + exception.Message);
            }
            settings = settings ?? new GameUserSettings();
            settings.Normalize();
            return settings;
        }

        public GameUserSettings Copy() => (GameUserSettings)MemberwiseClone();

        private static string ReadLegacyWindowsSettings()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            // Unity stores Windows strings as UTF-8 REG_BINARY, with a hashed value name.
            string path = Application.isEditor
                ? @"Software\Unity\UnityEditor\DefaultCompany\BecomeZ_BombBrick"
                : @"Software\DefaultCompany\BecomeZ_BombBrick";
            foreach (string name in new[] { PreferenceKey + "_h1416618149", PreferenceKey })
            {
                // The suffix is the recorded Unity 2022 key for BombBrick.UserSettings.v1.
                IntPtr currentUser = new IntPtr(unchecked((int)0x80000001));
                uint size = 0;
                uint type;
                int result = RegGetValue(currentUser, path, name, 0x0a, out type, null, ref size);
                if (result == 2) continue;
                if (result != 0) throw new System.ComponentModel.Win32Exception(result);
                if (size == 0 || size > 1048576) throw new System.IO.InvalidDataException("Invalid legacy settings size.");
                byte[] bytes = new byte[size];
                result = RegGetValue(currentUser, path, name, 0x0a, out type, bytes, ref size);
                if (result != 0) throw new System.ComponentModel.Win32Exception(result);
                return (type == 1 ? System.Text.Encoding.Unicode : System.Text.Encoding.UTF8)
                    .GetString(bytes, 0, (int)size).TrimEnd('\0');
            }
#endif
            return null;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Read-only Windows API; avoid adding a managed Registry package to the player.
        [System.Runtime.InteropServices.DllImport("advapi32.dll", EntryPoint = "RegGetValueW",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true)]
        private static extern int RegGetValue(IntPtr key, string path, string name, uint flags,
            out uint type, [System.Runtime.InteropServices.Out] byte[] data, ref uint size);
#endif

        public void Normalize()
        {
            width = Mathf.Clamp(width, 960, 7680);
            height = Mathf.Clamp(height, 540, 4320);
            if (frameRate != 30 && frameRate != 60 && frameRate != 120) frameRate = 60;
            masterVolume = Volume(masterVolume);
            musicVolume = Volume(musicVolume);
            effectsVolume = Volume(effectsVolume);
            if (cameraShake < 0 || cameraShake > 2) cameraShake = 2;
            language = GameLocalization.NormalizeLanguage(language);
        }

        private static float Volume(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0.5f : Mathf.Clamp01(value);

        public void Save()
        {
            Normalize();
            PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Apply(bool changeDisplay)
        {
            Normalize();
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = frameRate;
            AudioListener.volume = masterVolume;
            GameFrontend.ApplyBackgroundSettings(this);
            GameLocalization.SetLanguage(language);
            CombatFlashPresentation.Apply(reduceFlashes);
            foreach (CombatCameraDirector director in UnityEngine.Object.FindObjectsOfType<CombatCameraDirector>())
                director.SetShakeStrength(CameraShakeScale);
            if (changeDisplay && !Application.isEditor)
                Screen.SetResolution(width, height,
                    fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
