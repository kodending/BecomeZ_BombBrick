using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BombBrick.Game.Core
{
    public static class GameSupport
    {
        public const string ContactEmail = "codending@gmail.com";
        public const string Title = "문의 · 오류 신고";

        public static string EmailUrl => "mailto:" + ContactEmail + "?subject=" +
            Uri.EscapeDataString("BecomeZ: Wild Crew v" + Application.version);

        public static string DiagnosticInformation()
        {
            // An allowlist: no account IDs, device IDs, save contents, logs, or user paths.
            return string.Join("\n", new[]
            {
                "BecomeZ: Wild Crew",
                "Version: " + Application.version,
                "Build: " + (Application.isEditor ? "Editor" : Application.buildGUID),
                "Unity: " + Application.unityVersion,
                "OS: " + SystemInfo.operatingSystem,
                "GPU: " + SystemInfo.graphicsDeviceName,
                "Display: " + Screen.width + "x" + Screen.height + " / " + Screen.fullScreenMode,
                "Language: " + GameLocalization.CurrentLanguage,
                "Scene: " + SceneManager.GetActiveScene().name
            });
        }

        public static string LogDirectory(string logPath)
        {
            if (string.IsNullOrWhiteSpace(logPath)) return "";
            try { return Path.GetDirectoryName(Path.GetFullPath(logPath)) ?? ""; }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException ||
                e is IOException || e is System.Security.SecurityException) { return ""; }
        }

        public static bool TryOpenLogDirectory()
        {
            string directory = LogDirectory(Application.consoleLogPath);
            if (!Directory.Exists(directory)) return false;
            Application.OpenURL(new Uri(directory + Path.DirectorySeparatorChar).AbsoluteUri);
            return true;
        }
    }
}
