using Crescent.Utilities;
using Crescent.Plugins;

namespace Crescent.UI
{
    public static class ModPathUtils
    {
        public static readonly string[] ArchiveExtensions = new[]
        {
            ".zip",
            ".rar",
            ".7z"
        };

        public static string StripArchiveExtension(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName)) return string.Empty;
            for (int index = 0; index < ArchiveExtensions.Length; index++)
            {
                string extension = ArchiveExtensions[index];
                if (pathOrName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return pathOrName.Substring(0, pathOrName.Length - extension.Length);
            }

            return pathOrName;
        }

        public static bool HasArchiveExtension(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName)) return false;
            for (int index = 0; index < ArchiveExtensions.Length; index++)
                if (pathOrName.EndsWith(ArchiveExtensions[index], StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public static void CleanupStagingDirectory(PluginData plugin)
        {
            if (string.IsNullOrEmpty(plugin.StagingPath) || !Directory.Exists(plugin.StagingPath)) return;

            string archiveFolder = Path.Combine(plugin.StagingPath, PluginData.Archive);
            string manualFolder = Path.Combine(plugin.StagingPath, PluginData.Manual);
            string[] targetFolders = new[] { plugin.StagingPath, manualFolder };

            foreach (string folder in targetFolders)
            {
                if (!Directory.Exists(folder)) continue;

                string[] directories = Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly);
                for (int index = 0; index < directories.Length; index++)
                {
                    string dirPath = directories[index];
                    string dirName = Path.GetFileName(dirPath);
                    if (ModsTab.PrivilegedFolders.AnyEquals(dirName)) continue;

                    string dotTruncatedName = Path.GetFileNameWithoutExtension(dirName);
                    if (!string.IsNullOrEmpty(dotTruncatedName) && !string.Equals(dotTruncatedName, dirName, StringComparison.OrdinalIgnoreCase))
                    {
                        string truncatedSiblingPath = Path.Combine(folder, dotTruncatedName);
                        if (Directory.Exists(truncatedSiblingPath))
                        {
                            TryDeleteDirectoryRecursive(truncatedSiblingPath);
                            plugin.installedMods.RemoveAll(mod => string.Equals(StripArchiveExtension(mod.modName), dotTruncatedName, StringComparison.OrdinalIgnoreCase));
                            foreach (ProfileData profile in plugin.profiles)
                                profile.mods.RemoveAll(profileMod => string.Equals(StripArchiveExtension(profileMod.modName), dotTruncatedName, StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }

                string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                for (int index = 0; index < files.Length; index++)
                {
                    string filePath = files[index];
                    string extension = Path.GetExtension(filePath).ToLower();
                    if (!extension.IsArchive()) continue;

                    string cleanFolderName = StripArchiveExtension(Path.GetFileName(filePath));
                    string expectedDirPath = Path.Combine(folder, cleanFolderName);
                    if (Directory.Exists(expectedDirPath))
                    {
                        try
                        {
                            if (!Directory.Exists(archiveFolder)) Directory.CreateDirectory(archiveFolder);
                            string archiveDestination = Path.Combine(archiveFolder, Path.GetFileName(filePath));
                            if (File.Exists(archiveDestination)) File.Delete(archiveDestination);
                            File.Move(filePath, archiveDestination);
                        }
                        catch (Exception exception)
                        {
                            Logger.Error(exception.ToString());
                        }
                    }
                }
            }
        }

        private static void TryDeleteDirectoryRecursive(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                string[] allFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                for (int index = 0; index < allFiles.Length; index++)
                    File.SetAttributes(allFiles[index], FileAttributes.Normal);

                Directory.Delete(path, true);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        public static List<string> GetModPaths(PluginData plugin)
        {
            List<string> modPaths = new();
            if (string.IsNullOrEmpty(plugin.StagingPath) || !Directory.Exists(plugin.StagingPath)) return modPaths;

            CleanupStagingDirectory(plugin);

            HashSet<string> addedCleanNames = new(StringComparer.OrdinalIgnoreCase);

            bool HasExtractedCounterpart(string archivePath, string baseDirectory)
            {
                string expectedDirName = StripArchiveExtension(Path.GetFileName(archivePath));
                string expectedDirPath = Path.Combine(baseDirectory, expectedDirName);
                return Directory.Exists(expectedDirPath);
            }

            string manualFolder = Path.Combine(plugin.StagingPath, PluginData.Manual);
            if (Directory.Exists(manualFolder))
            {
                string[] directories = Directory.GetDirectories(manualFolder, "*", SearchOption.TopDirectoryOnly);
                foreach (string directory in directories)
                {
                    string cleanName = StripArchiveExtension(Path.GetFileName(directory));
                    if (addedCleanNames.Add(cleanName)) modPaths.Add(directory);
                }

                string[] manualFiles = Directory.GetFiles(manualFolder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in manualFiles)
                {
                    string extension = Path.GetExtension(file).ToLower();
                    if (extension.IsArchive())
                    {
                        if (HasExtractedCounterpart(file, manualFolder)) continue;
                        string cleanName = StripArchiveExtension(Path.GetFileName(file));
                        if (addedCleanNames.Add(cleanName)) modPaths.Add(file);
                    }
                }
            }

            string[] rootDirectories = Directory.GetDirectories(plugin.StagingPath, "*", SearchOption.TopDirectoryOnly);
            foreach (string directory in rootDirectories)
            {
                string directoryName = Path.GetFileName(directory);
                if (ModsTab.PrivilegedFolders.AnyEquals(directoryName)) continue;
                string cleanName = StripArchiveExtension(directoryName);
                if (addedCleanNames.Add(cleanName)) modPaths.Add(directory);
            }

            string[] rootFiles = Directory.GetFiles(plugin.StagingPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in rootFiles)
            {
                string extension = Path.GetExtension(file).ToLower();
                if (extension.IsArchive())
                {
                    if (HasExtractedCounterpart(file, plugin.StagingPath)) continue;
                    string cleanName = StripArchiveExtension(Path.GetFileName(file));
                    if (addedCleanNames.Add(cleanName)) modPaths.Add(file);
                }
            }

            return modPaths;
        }
    }
}