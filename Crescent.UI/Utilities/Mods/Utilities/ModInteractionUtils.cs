using System.IO.Compression;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public static class ModInteractionUtils
    {
        #region Deployment State & Withdrawal Helpers

        public static void SyncModDeploymentState(PluginData plugin, ModData modData)
        {
            if (string.IsNullOrEmpty(plugin.DeploymentPath) || !Directory.Exists(plugin.DeploymentPath))
            {
                modData.isDeployed = false;
                return;
            }

            if (modData.ownedFiles.Count > 0)
            {
                for (int index = 0; index < modData.ownedFiles.Count; index++)
                {
                    string deployedFile = Path.Combine(plugin.DeploymentPath, modData.ownedFiles[index]);
                    if (!File.Exists(deployedFile) && !Path.Exists(deployedFile))
                    {
                        modData.isDeployed = false;
                        return;
                    }
                }

                modData.isDeployed = true;
                return;
            }

            string? stagingFolder = ResolveExtractedModDirectory(plugin, modData);
            if (string.IsNullOrEmpty(stagingFolder) || !Directory.Exists(stagingFolder))
            {
                modData.isDeployed = false;
                return;
            }

            string[] stagingFiles = Directory.GetFiles(stagingFolder, "*.*", SearchOption.AllDirectories);
            if (stagingFiles.Length == 0)
            {
                modData.isDeployed = false;
                return;
            }

            List<string> discoveredRelativeFiles = new(stagingFiles.Length);
            for (int index = 0; index < stagingFiles.Length; index++)
            {
                string relativePath = Path.GetRelativePath(stagingFolder, stagingFiles[index]);
                string deployedFile = Path.Combine(plugin.DeploymentPath, relativePath);
                if (!File.Exists(deployedFile) && !Path.Exists(deployedFile))
                {
                    modData.isDeployed = false;
                    return;
                }

                discoveredRelativeFiles.Add(relativePath);
            }

            modData.ownedFiles = discoveredRelativeFiles;
            modData.isDeployed = true;
        }

        public static void WithdrawModFiles(PluginData plugin, ModData modData)
        {
            try
            {
                plugin.WithdrawMod(modData);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }

            if (string.IsNullOrEmpty(plugin.DeploymentPath) || !Directory.Exists(plugin.DeploymentPath))
            {
                modData.isDeployed = false;
                return;
            }

            for (int index = 0; index < modData.ownedFiles.Count; index++)
            {
                string targetFile = Path.Combine(plugin.DeploymentPath, modData.ownedFiles[index]);
                DeleteFileOrSymlink(targetFile);
            }

            string? stagingDirectory = ResolveExtractedModDirectory(plugin, modData);
            if (!string.IsNullOrEmpty(stagingDirectory) && Directory.Exists(stagingDirectory))
            {
                string[] files = Directory.GetFiles(stagingDirectory, "*.*", SearchOption.AllDirectories);
                for (int index = 0; index < files.Length; index++)
                {
                    string relativePath = Path.GetRelativePath(stagingDirectory, files[index]);
                    string targetFile = Path.Combine(plugin.DeploymentPath, relativePath);
                    DeleteFileOrSymlink(targetFile);
                }
            }

            modData.isDeployed = false;
        }

        private static string? ResolveExtractedModDirectory(PluginData plugin, ModData modData)
        {
            if (!string.IsNullOrEmpty(modData.filePath) && Directory.Exists(modData.filePath))
                return modData.filePath;

            if (string.IsNullOrEmpty(plugin.StagingPath)) return null;

            string baseFolder = modData.isManual ? Path.Combine(plugin.StagingPath, PluginData.Manual) : plugin.StagingPath;
            string cleanName = ModPathUtils.StripArchiveExtension(modData.modName);
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
                if (info.Exists || info.LinkTarget != null || Path.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        #endregion

        #region Update Operations

        public static async Task UpdateAsync(PluginData plugin, ModData modData)
        {
            if (string.IsNullOrEmpty(plugin.StagingPath) || string.IsNullOrEmpty(modData.LatestFileId) || string.IsNullOrEmpty(modData.url.nexusId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return;
            string tempDownloadFolder = Path.Combine(plugin.StagingPath, ".temp_update_" + Guid.NewGuid().ToString("N"));
            try
            {
                string cleanDisplayName = modData.DisplayName.Cleaned();
                ToastManager.ShowProgress("Updating Mod", $"Downloading {cleanDisplayName} v{modData.LatestVersion}...", string.Empty, 0, true);

                string downloadUrl = await NexusUtils.GetDownloadUrlAsync(plugin.API.Name, modData.url.nexusId, modData.LatestFileId);
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    ToastManager.CompleteProgress("Update Failed", $"Could not retrieve download link for {cleanDisplayName} :(", ToastType.Error);
                    return;
                }

                string fallbackName = $"{cleanDisplayName}.zip";
                Progress<double> downloadProgress = new(percent => { ToastManager.ShowProgress("Updating Mod", $"Downloading {cleanDisplayName} ({percent:0}%)...", $"v{modData.version} → v{modData.LatestVersion}", percent, false); });

                string? tempFilePath = await NexusUtils.DownloadFileAsync(downloadUrl, tempDownloadFolder, fallbackName, downloadProgress);
                if (string.IsNullOrEmpty(tempFilePath) || !File.Exists(tempFilePath))
                {
                    ToastManager.CompleteProgress("Update Failed", $"Download failed for {cleanDisplayName} :(", ToastType.Error);
                    return;
                }

                bool wasDeployed = modData.isDeployed;
                bool wasManual = modData.isManual;
                string oldModName = modData.modName;

                WithdrawModFiles(plugin, modData);
                modData.DeleteFiles(plugin.StagingPath);

                string targetBaseDirectory = wasManual ? Path.Combine(plugin.StagingPath, PluginData.Manual) : plugin.StagingPath;
                if (!Directory.Exists(targetBaseDirectory)) Directory.CreateDirectory(targetBaseDirectory);

                string fileName = Path.GetFileName(tempFilePath);
                string finalDestinationPath = Path.Combine(targetBaseDirectory, fileName);
                if (File.Exists(finalDestinationPath)) File.Delete(finalDestinationPath);
                File.Move(tempFilePath, finalDestinationPath);

                modData.filePath = finalDestinationPath;
                modData.modName = fileName;
                modData.displayName = string.Empty;
                modData.version = NexusUtils.TryCleanVersion(modData.LatestVersion);
                if (string.IsNullOrEmpty(modData.version)) modData.version = modData.LatestVersion;
                modData.UpdateAvailable = false;
                modData.HasCheckedForUpdate = true;

                plugin.ReplaceModId(oldModName, fileName, modData);
                await plugin.SaveAsync();

                ToastManager.CompleteProgress("Mod Updated", $"{modData.DisplayName.Cleaned()} updated to v{modData.version}!", ToastType.Success, 4000, plugin.Name);

                if (wasDeployed) await plugin.Deploy();
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                ToastManager.CompleteProgress("Update Error", $"Failed to update {modData.DisplayName.Cleaned()} :(", ToastType.Error);
            }
            finally
            {
                if (Directory.Exists(tempDownloadFolder))
                {
                    try
                    {
                        Directory.Delete(tempDownloadFolder, true);
                    }
                    catch (Exception cleanupException)
                    {
                        Logger.Error(cleanupException.ToString());
                    }
                }
            }
        }

        #endregion

        #region Manual Installation Operations

        public static async Task ToggleManualInstallAsync(PluginData plugin, ModData modData)
        {
            if (string.IsNullOrEmpty(plugin.StagingPath) || string.IsNullOrEmpty(plugin.DeploymentPath)) return;

            string manualFolder = Path.Combine(plugin.StagingPath, PluginData.Manual);
            if (!Directory.Exists(manualFolder)) Directory.CreateDirectory(manualFolder);

            string fileName = Path.GetFileName(modData.filePath);
            bool directoryExists = Directory.Exists(modData.filePath);

            if (!modData.isManual) EnableManualInstall(plugin, modData, manualFolder, fileName, directoryExists);
            else DisableManualInstall(plugin, modData, manualFolder, fileName, directoryExists);

            await plugin.SaveAsync();
            ToastManager.Show("Install Mode Changed", $"{modData.DisplayName.Cleaned()} is now {(modData.isManual ? PluginData.Manual : PluginData.Managed)}", ToastType.Info, 3000, plugin.Name);
        }

        private static void EnableManualInstall(PluginData plugin, ModData modData, string manualFolder, string fileName, bool directoryExists)
        {
            if (modData.isDeployed) WithdrawModFiles(plugin, modData);

            modData.isManual = true;
            string rootPath = Path.Combine(plugin.StagingPath, fileName);
            string manualPath = Path.Combine(manualFolder, fileName);

            if (directoryExists)
            {
                if (Directory.Exists(rootPath) && !Directory.Exists(manualPath))
                    Directory.Move(rootPath, manualPath);
            }
            else
            {
                if (File.Exists(rootPath) && !File.Exists(manualPath))
                    File.Move(rootPath, manualPath);
            }

            string cleanModName = directoryExists ? fileName : ModPathUtils.StripArchiveExtension(fileName);
            string manualDirectory = Path.Combine(manualFolder, cleanModName);
            string extractedDirectory = Path.Combine(plugin.StagingPath, cleanModName);

            if (!directoryExists && Directory.Exists(extractedDirectory))
            {
                if (Directory.Exists(manualDirectory)) Directory.Delete(manualDirectory, true);
                Directory.Move(extractedDirectory, manualDirectory);
            }
            else if (!directoryExists && !Directory.Exists(manualDirectory))
            {
                Directory.CreateDirectory(manualDirectory);
                if (File.Exists(manualPath)) ZipFile.ExtractToDirectory(manualPath, manualDirectory, true);
            }

            string sourceDirectory = directoryExists ? manualPath : manualDirectory;
            if (Directory.Exists(sourceDirectory))
            {
                modData.filePath = sourceDirectory;
                modData.ownedFiles.Clear();
                string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string sourceFile in sourceFiles)
                {
                    string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                    string targetFile = Path.Combine(plugin.DeploymentPath, relativePath);
                    string? targetDirectory = Path.GetDirectoryName(targetFile);

                    if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                        Directory.CreateDirectory(targetDirectory);

                    DeleteFileOrSymlink(targetFile);
                    File.Copy(sourceFile, targetFile, true);
                    modData.ownedFiles.Add(relativePath);
                }

                modData.isDeployed = true;
            }
        }

        private static void DisableManualInstall(PluginData plugin, ModData modData, string manualFolder, string fileName, bool directoryExists)
        {
            WithdrawModFiles(plugin, modData);
            modData.isManual = false;

            string manualPath = Path.Combine(manualFolder, fileName);
            string rootPath = Path.Combine(plugin.StagingPath, fileName);

            string cleanModName = directoryExists ? fileName : ModPathUtils.StripArchiveExtension(fileName);
            string manualDirectory = Path.Combine(manualFolder, cleanModName);

            if (directoryExists)
            {
                if (Directory.Exists(manualPath) && !Directory.Exists(rootPath))
                    Directory.Move(manualPath, rootPath);

                modData.filePath = rootPath;
            }
            else
            {
                if (File.Exists(manualPath))
                {
                    if (File.Exists(rootPath)) File.Delete(rootPath);
                    File.Move(manualPath, rootPath);
                    modData.filePath = rootPath;
                }
                else if (Directory.Exists(manualDirectory))
                {
                    string targetRootDir = Path.Combine(plugin.StagingPath, cleanModName);
                    if (!Directory.Exists(targetRootDir)) Directory.Move(manualDirectory, targetRootDir);
                    modData.filePath = targetRootDir;
                }
            }
        }

        #endregion
    }
}