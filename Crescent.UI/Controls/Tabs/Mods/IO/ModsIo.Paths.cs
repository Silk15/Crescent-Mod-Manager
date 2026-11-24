using Crescent.Plugins;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public static partial class ModsIO
    {
        public static readonly string[] ArchiveExtensions = new[]
        {
            ".zip",
            ".rar",
            ".7z"
        };

        public static readonly List<string> PrivilegedFolders = new()
        {
            ".profiles",
            ".archive",
            ".manual",
            ".backups",
            ".themes",
            ".cache"
        };

        public static readonly List<string> LegacyPrivilegedFolders = new()
        {
            "Profiles",
            "Archive",
            "Archives",
            "Manual",
            ".backup"
        };

        public static bool IsPrivilegedFolder(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName)) return false;
            return PrivilegedFolders.AnyEquals(directoryName) || LegacyPrivilegedFolders.AnyEquals(directoryName);
        }

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
            {
                if (pathOrName.EndsWith(ArchiveExtensions[index], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static HashSet<string> GetArchiveFileNames(PluginData pluginData)
        {
            HashSet<string> archiveFiles = new(StringComparer.OrdinalIgnoreCase);
            string archiveDirectory = Path.Combine(pluginData.StagingPath, PluginData.Archive);
            if (!Directory.Exists(archiveDirectory)) return archiveFiles;

            string[] files = Directory.GetFiles(archiveDirectory);
            for (int index = 0; index < files.Length; index++) archiveFiles.Add(Path.GetFileName(files[index]));
            return archiveFiles;
        }

        public static string GetModId(HashSet<string> archiveFiles, string diskName)
        {
            if (HasArchiveExtension(diskName)) return diskName;

            for (int index = 0; index < ArchiveExtensions.Length; index++)
            {
                string candidateName = diskName + ArchiveExtensions[index];
                if (archiveFiles.Contains(candidateName)) return candidateName;
            }

            return diskName.Contains('.') ? diskName + ".zip" : diskName;
        }

        private static string? ResolveExtractedModDirectory(PluginData plugin, ModData modData)
        {
            if (!string.IsNullOrEmpty(modData.filePath) && Directory.Exists(modData.filePath))
                return modData.filePath;

            if (string.IsNullOrEmpty(plugin.StagingPath)) return null;

            string baseFolder = modData.isManual ? Path.Combine(plugin.StagingPath, PluginData.Manual) : plugin.StagingPath;
            string cleanName = StripArchiveExtension(modData.modName);
            string candidate = Path.Combine(baseFolder, cleanName);
            if (Directory.Exists(candidate)) return candidate;

            string fallbackCandidate = Path.Combine(plugin.StagingPath, cleanName);
            if (Directory.Exists(fallbackCandidate)) return fallbackCandidate;

            string manualCandidate = Path.Combine(plugin.StagingPath, PluginData.Manual, cleanName);
            if (Directory.Exists(manualCandidate)) return manualCandidate;

            return null;
        }

        private static void DeleteFileOrSymlink(string path)
        {
            try
            {
                FileInfo info = new(path);
                if (info.Exists || info.LinkTarget != null || Path.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        private static void TryDeleteDirectoryRecursive(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                string[] allFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                for (int index = 0; index < allFiles.Length; index++) File.SetAttributes(allFiles[index], FileAttributes.Normal);
                Directory.Delete(path, true);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }
    }
}