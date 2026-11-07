using System.IO.Compression;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;

namespace Crescent.UI
{
    public static class ModInteractionUtils
    {
        #region Update Operations

        public static async Task UpdateAsync(PluginData plugin, ModData modData)
        {
            if (string.IsNullOrEmpty(plugin.StagingPath) || string.IsNullOrEmpty(modData.LatestFileId) || string.IsNullOrEmpty(modData.url.nexusId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return;
            try
            {
                bool deployed = modData.isDeployed;
                if (deployed) plugin.WithdrawMod(modData);

                string downloadUrl = await NexusUtils.GetDownloadUrlAsync(plugin.API.Name, modData.url.nexusId, modData.LatestFileId);
                if (string.IsNullOrEmpty(downloadUrl)) return;

                string fallbackName = $"{modData.DisplayName}.zip";
                string? destinationPath = await NexusUtils.DownloadFileAsync(downloadUrl, plugin.StagingPath, fallbackName);

                if (string.IsNullOrEmpty(destinationPath)) return;

                string fileName = Path.GetFileName(destinationPath);
                modData.DeleteFiles(plugin.StagingPath);

                modData.filePath = destinationPath;
                modData.modName = fileName;
                modData.version = modData.LatestVersion;
                modData.UpdateAvailable = false;
                await plugin.SaveAsync();
                if (deployed) await plugin.Deploy();
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
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
        }

        private static void EnableManualInstall(PluginData plugin, ModData modData, string manualFolder, string fileName, bool directoryExists)
        {
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

            string modName = directoryExists ? fileName : Path.GetFileNameWithoutExtension(fileName);
            string manualDirectory = Path.Combine(manualFolder, modName);
            string extractedDirectory = Path.Combine(plugin.StagingPath, modName);

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
                string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string sourceFile in sourceFiles)
                {
                    string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                    string targetFile = Path.Combine(plugin.DeploymentPath, relativePath);
                    string? targetDirectory = Path.GetDirectoryName(targetFile);

                    if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                        Directory.CreateDirectory(targetDirectory);

                    if (File.Exists(targetFile) || Directory.Exists(targetFile)) File.Delete(targetFile);
                    File.Copy(sourceFile, targetFile, true);
                }
            }
        }

        private static void DisableManualInstall(PluginData plugin, ModData modData, string manualFolder, string fileName, bool directoryExists)
        {
            modData.isManual = false;
            string manualPath = Path.Combine(manualFolder, fileName);
            string rootPath = Path.Combine(plugin.StagingPath, fileName);

            string modName = directoryExists ? fileName : Path.GetFileNameWithoutExtension(fileName);
            string manualDirectory = Path.Combine(manualFolder, modName);

            string sourceDirectory = directoryExists ? manualPath : manualDirectory;
            if (Directory.Exists(sourceDirectory))
            {
                string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string sourceFile in sourceFiles)
                {
                    string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                    string targetFile = Path.Combine(plugin.DeploymentPath, relativePath);
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                }

                if (!directoryExists) Directory.Delete(manualDirectory, true);
            }

            if (directoryExists)
            {
                if (Directory.Exists(manualPath) && !Directory.Exists(rootPath))
                    Directory.Move(manualPath, rootPath);
            }
            else
            {
                if (File.Exists(manualPath))
                {
                    if (File.Exists(rootPath)) File.Delete(rootPath);
                    File.Move(manualPath, rootPath);
                }
            }
        }

        #endregion
    }
}