using System;
using System.IO;
using UnityEngine;

namespace BombBrick.Game.Core
{
    public static class GameDataPaths
    {
        public static string Root
        {
            get
            {
                string qaRoot = QaProfileRoot(Environment.GetCommandLineArgs(),
                    Path.GetDirectoryName(Application.dataPath));
                if (qaRoot != null) return qaRoot;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                // Keep saves independent of punctuation sanitization and future display-title edits.
                return Path.Combine(WindowsLocalLow, "PangStudio", "BecomeZWildCrew", "UserData");
#else
                return Path.Combine(Application.persistentDataPath, "UserData");
#endif
            }
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static string windowsLocalLow;
        private static string WindowsLocalLow => windowsLocalLow ??
            (windowsLocalLow = Directory.GetParent(Application.persistentDataPath).Parent.FullName);
#endif

        public static void EnsureMigrated()
        {
            string destination = Root;
            if (DirectoryPresent(destination)) return;
            string legacy = null;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (QaProfileRoot(Environment.GetCommandLineArgs(),
                    Path.GetDirectoryName(Application.dataPath)) == null)
                legacy = Path.Combine(WindowsLocalLow, "DefaultCompany", "BecomeZ_BombBrick");
#endif
            MigrateProfiles(legacy, destination);
        }

        // Explicit release-build QA sessions never import or overwrite the player's profile.
        public static string QaProfileRoot(string[] arguments, string buildDirectory)
        {
            int index = Array.IndexOf(arguments, "-qa-profile");
            if (index < 0) return null;
            if (index + 1 >= arguments.Length)
                throw new ArgumentException("-qa-profile requires a profile name.");
            string name = arguments[index + 1];
            if (name.Length == 0 || name.Length > 48 ||
                Array.LastIndexOf(arguments, "-qa-profile") != index)
                throw new ArgumentException("Invalid QA profile name.");
            foreach (char c in name)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' ||
                      c >= '0' && c <= '9' || c == '-' || c == '_'))
                    throw new ArgumentException("QA profile names use letters, digits, - or _ only.");
            return Path.Combine(Path.GetFullPath(buildDirectory), "QAData", name, "UserData");
        }

        // Commit the whole profile family together, including interrupted-reset recovery files.
        // A failed copy must never look like an empty, successfully migrated save directory.
        public static void MigrateProfiles(string legacyRoot, string destination)
        {
            if (DirectoryPresent(destination)) return;
            bool hasLegacy = legacyRoot != null && DirectoryPresent(legacyRoot);
            string staging = destination + ".migrating-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            try
            {
                if (hasLegacy)
                    foreach (string source in Directory.GetFileSystemEntries(legacyRoot, "Profiles*"))
                        CopyEntry(source, Path.Combine(staging, Path.GetFileName(source)));
                Directory.Move(staging, destination);
            }
            finally
            {
                // This directory belongs only to this attempt; the legacy source is never changed.
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }

        private static bool DirectoryPresent(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) == 0)
                    throw new IOException("Save directory is occupied by a file: " + path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Save migration cannot follow directory links: " + path);
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        private static void CopyEntry(string source, string destination)
        {
            FileAttributes attributes = File.GetAttributes(source);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Save migration cannot follow links: " + source);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                File.Copy(source, destination, false);
                return;
            }
            Directory.CreateDirectory(destination);
            foreach (string child in Directory.GetFileSystemEntries(source))
                CopyEntry(child, Path.Combine(destination, Path.GetFileName(child)));
        }
    }
}
