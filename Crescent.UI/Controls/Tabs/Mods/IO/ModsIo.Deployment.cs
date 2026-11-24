using System.IO.Compression;
using Crescent.Plugins;
using Crescent.UI.Controls.Toasts;
using Crescent.Utilities;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public static partial class ModsIO
    {
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
            ToastManager.Show("Install Mode Changed", $"{modData.DisplayName.Cleaned()} is now {(modData.isManual ? "Manual" : "Managed")}", ToastType.Info, 3000, plugin.Name);
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

            string cleanModName = directoryExists ? fileName : StripArchiveExtension(fileName);
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

            string cleanModName = directoryExists ? fileName : StripArchiveExtension(fileName);
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
    }
}