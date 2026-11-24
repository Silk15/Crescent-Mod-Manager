using Avalonia.Threading;
using Crescent.Plugins;
using Crescent.UI.Controls.Toasts;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public static partial class ModsIO
    {
        public static async Task MoveLegacyFoldersAsync(PluginData? pluginData)
        {
            if (pluginData == null || string.IsNullOrEmpty(pluginData.StagingPath)) return;
            Dictionary<string, string> oldFolders = new()
            {
                { "Profiles", ".profiles" },
                { "Archives", ".archive" },
                { "Archive", ".archive" },
                { ".backup", ".backups" },
                { "Manual", ".manual" }
            };

            foreach (KeyValuePair<string, string> folder in oldFolders)
            {
                string oldPath = Path.Combine(pluginData.StagingPath, folder.Key);
                string newPath = Path.Combine(pluginData.StagingPath, folder.Value);
                if (Directory.Exists(oldPath))
                {
                    try
                    {
                        await MoveDirectoryAsync(oldPath, newPath, folder.Key);
                    }
                    catch (Exception exception)
                    {
                        Logger.Error($"Failed to migrate {folder.Key} to {folder.Value}!\n{exception}");
                    }
                }
            }
        }

        private static async Task MoveDirectoryAsync(string sourceDirectory, string destinationDirectory, string folderName)
        {
            await Task.Run(async () =>
            {
                DirectoryInfo directory = new(sourceDirectory);
                if (!directory.Exists) return;

                FileInfo[] allFiles = directory.GetFiles("*", SearchOption.AllDirectories);
                int totalFiles = allFiles.Length;

                Directory.CreateDirectory(destinationDirectory);
                if (totalFiles == 0)
                {
                    Directory.Delete(sourceDirectory, true);
                    return;
                }

                int currentFile = 0;
                int lastAnimatedStep = -1;

                Dispatcher.UIThread.Post(() => ToastManager.ShowProgress("Migrating Folders", $"Moving {folderName}", "Starting..."));
                foreach (FileInfo file in allFiles)
                {
                    string relativePath = Path.GetRelativePath(sourceDirectory, file.FullName);
                    string targetFilePath = Path.Combine(destinationDirectory, relativePath);
                    string targetDirPath = Path.GetDirectoryName(targetFilePath) ?? destinationDirectory;

                    if (!Directory.Exists(targetDirPath))
                        Directory.CreateDirectory(targetDirPath);

                    try
                    {
                        if (file.IsReadOnly)
                            file.IsReadOnly = false;

                        if (File.Exists(targetFilePath))
                        {
                            FileAttributes targetAttrs = File.GetAttributes(targetFilePath);
                            if ((targetAttrs & FileAttributes.ReadOnly) != 0)
                                File.SetAttributes(targetFilePath, targetAttrs & ~FileAttributes.ReadOnly);
                        }

                        File.Move(file.FullName, targetFilePath, overwrite: true);
                    }
                    catch (Exception fileException)
                    {
                        Logger.Error($"Failed to move {file.FullName}: {fileException.Message}");
                    }

                    currentFile++;
                    double percentage = (double)currentFile / totalFiles * 100;
                    int currentStep = (int)(percentage / 4);
                    if (currentFile == totalFiles || currentStep > lastAnimatedStep)
                    {
                        lastAnimatedStep = currentStep;
                        string fileName = file.Name;
                        double reportedPercentage = percentage;

                        Dispatcher.UIThread.Post(() => ToastManager.ShowProgress("Migrating Folders", $"Moving {folderName}", fileName, reportedPercentage));
                        await Task.Delay(15);
                    }
                }

                foreach (DirectoryInfo subDirectory in directory.GetDirectories("*", SearchOption.AllDirectories))
                    if ((subDirectory.Attributes & FileAttributes.ReadOnly) != 0)
                        subDirectory.Attributes &= ~FileAttributes.ReadOnly;

                if ((directory.Attributes & FileAttributes.ReadOnly) != 0)
                    directory.Attributes &= ~FileAttributes.ReadOnly;

                Directory.Delete(sourceDirectory, true);
                Dispatcher.UIThread.Post(() => ToastManager.CompleteProgress("Migration Complete", $"Successfully migrated {folderName}", ToastType.Success, 4000));
            });
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
                    string directoryPath = directories[index];
                    string directoryName = Path.GetFileName(directoryPath);
                    if (directoryName.StartsWith('.') || IsPrivilegedFolder(directoryName)) continue;

                    string dotTruncatedName = Path.GetFileNameWithoutExtension(directoryName);
                    if (!string.IsNullOrEmpty(dotTruncatedName) && !IsPrivilegedFolder(dotTruncatedName) && !string.Equals(dotTruncatedName, directoryName, StringComparison.OrdinalIgnoreCase))
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
                    string directoryName = Path.GetFileName(directory);
                    if (directoryName.StartsWith('.') || IsPrivilegedFolder(directoryName)) continue;

                    string cleanName = StripArchiveExtension(directoryName);
                    if (addedCleanNames.Add(cleanName)) modPaths.Add(directory);
                }

                string[] manualFiles = Directory.GetFiles(manualFolder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in manualFiles)
                {
                    if (Path.GetExtension(file).ToLower().IsArchive() && !HasExtractedCounterpart(file, manualFolder))
                    {
                        string cleanName = StripArchiveExtension(Path.GetFileName(file));
                        if (addedCleanNames.Add(cleanName)) modPaths.Add(file);
                    }
                }
            }

            string[] rootDirectories = Directory.GetDirectories(plugin.StagingPath, "*", SearchOption.TopDirectoryOnly);
            foreach (string directory in rootDirectories)
            {
                string directoryName = Path.GetFileName(directory);
                if (directoryName.StartsWith('.') || IsPrivilegedFolder(directoryName)) continue;

                string cleanName = StripArchiveExtension(directoryName);
                if (addedCleanNames.Add(cleanName)) modPaths.Add(directory);
            }

            string[] rootFiles = Directory.GetFiles(plugin.StagingPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in rootFiles)
            {
                if (Path.GetExtension(file).ToLower().IsArchive() && !HasExtractedCounterpart(file, plugin.StagingPath))
                {
                    string cleanName = StripArchiveExtension(Path.GetFileName(file));
                    if (addedCleanNames.Add(cleanName)) modPaths.Add(file);
                }
            }

            string archiveFolder = Path.Combine(plugin.StagingPath, PluginData.Archive);
            if (Directory.Exists(archiveFolder))
            {
                string[] archiveFiles = Directory.GetFiles(archiveFolder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in archiveFiles)
                {
                    if (Path.GetExtension(file).ToLower().IsArchive() && !HasExtractedCounterpart(file, plugin.StagingPath))
                    {
                        string cleanName = StripArchiveExtension(Path.GetFileName(file));
                        if (addedCleanNames.Add(cleanName)) modPaths.Add(file);
                    }
                }
            }

            return modPaths;
        }

        public static bool SyncMods(PluginData? pluginData, CancellationToken token)
        {
            if (pluginData == null) return false;

            List<string> modPaths = GetModPaths(pluginData);
            HashSet<string> archiveFiles = GetArchiveFileNames(pluginData);
            HashSet<string> validModNames = new(StringComparer.OrdinalIgnoreCase);
            string manualDirectory = Path.Combine(pluginData.StagingPath, PluginData.Manual);

            for (int index = 0; index < modPaths.Count; index++)
            {
                string fileName = Path.GetFileName(modPaths[index]);
                validModNames.Add(fileName);
                validModNames.Add(StripArchiveExtension(fileName));
            }

            int removedCount = pluginData.installedMods.RemoveAll(mod =>
            {
                if (validModNames.Contains(mod.modName)) return false;
                string unzippedName = StripArchiveExtension(mod.modName);
                return !validModNames.Contains(unzippedName);
            });

            bool listChanged = removedCount > 0;

            for (int index = 0; index < modPaths.Count; index++)
            {
                if (token.IsCancellationRequested) return false;

                string filePath = modPaths[index];
                string diskName = Path.GetFileName(filePath);
                string strippedName = StripArchiveExtension(diskName);
                bool isManualPath = filePath.StartsWith(manualDirectory, StringComparison.OrdinalIgnoreCase);

                ModData? modData = pluginData.installedMods.Find(mod => string.Equals(mod.modName, diskName, StringComparison.OrdinalIgnoreCase) || string.Equals(StripArchiveExtension(mod.modName), strippedName, StringComparison.OrdinalIgnoreCase));

                string safeModName = GetModId(archiveFiles, diskName);

                if (modData == null)
                {
                    modData = new(pluginData, safeModName, filePath)
                    {
                        modName = safeModName,
                        isManual = isManualPath
                    };

                    pluginData.installedMods.Add(modData);
                    pluginData.OnModLoaded(modData);
                    listChanged = true;
                }
                else
                {
                    string previousModName = modData.modName;
                    bool pathChanged = !string.Equals(modData.filePath, filePath, StringComparison.OrdinalIgnoreCase);
                    modData.filePath = filePath;
                    modData.isManual = isManualPath;

                    if (!HasArchiveExtension(modData.modName))
                    {
                        modData.modName = safeModName;
                        if (!string.Equals(previousModName, safeModName, StringComparison.OrdinalIgnoreCase))
                        {
                            pluginData.ReplaceModId(previousModName, safeModName, modData);
                            listChanged = true;
                        }
                    }

                    if (pathChanged)
                    {
                        pluginData.OnModLoaded(modData);
                        listChanged = true;
                    }
                }

                SyncModDeploymentState(pluginData, modData);
            }

            return listChanged;
        }
    }
}