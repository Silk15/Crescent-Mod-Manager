using System.IO.Compression;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;

namespace Crescent.UI
{
    public static class ModInteractionUtils
    {
        #region Update Operations

        public static async Task UpdateAsync(GameData game, ModData modData)
        {
            if (string.IsNullOrEmpty(game.StagingPath) || string.IsNullOrEmpty(modData.LatestFileId) || string.IsNullOrEmpty(modData.url.nexusId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return;
            try
            {
                bool deployed = modData.isDeployed;
                if (deployed) game.WithdrawMod(modData);

                string downloadUrl = await NexusUtils.GetDownloadUrlAsync(game.NexusName, modData.url.nexusId, modData.LatestFileId);
                if (string.IsNullOrEmpty(downloadUrl)) return;

                string fallbackName = $"{modData.DisplayName}.zip";
                string? destinationPath = await NexusUtils.DownloadFileAsync(downloadUrl, game.StagingPath, fallbackName);

                if (string.IsNullOrEmpty(destinationPath)) return;

                string fileName = Path.GetFileName(destinationPath);
                modData.DeleteFiles(game.StagingPath);

                modData.filePath = destinationPath;
                modData.modName = fileName;
                modData.version = modData.LatestVersion;
                modData.UpdateAvailable = false;
                await game.SaveAsync();
                if (deployed) await game.Deploy();
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        #endregion

        #region Manual Installation Operations

        public static async Task ToggleManualInstallAsync(GameData game, ModData modData)
        {
            if (string.IsNullOrEmpty(game.StagingPath) || string.IsNullOrEmpty(game.DeploymentPath)) return;

            string manualFolder = Path.Combine(game.StagingPath, GameData.Manual);
            if (!Directory.Exists(manualFolder)) Directory.CreateDirectory(manualFolder);

            string fileName = Path.GetFileName(modData.filePath);
            bool directoryExists = Directory.Exists(modData.filePath);

            if (!modData.isManual) EnableManualInstall(game, modData, manualFolder, fileName, directoryExists);
            else DisableManualInstall(game, modData, manualFolder, fileName, directoryExists);
            await game.SaveAsync();
        }

        private static void EnableManualInstall(GameData game, ModData modData, string manualFolder, string fileName, bool directoryExists)
        {
            modData.isManual = true;
            string rootPath = Path.Combine(game.StagingPath, fileName);
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
            string extractedDirectory = Path.Combine(game.StagingPath, modName);

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
                    string targetFile = Path.Combine(game.DeploymentPath, relativePath);
                    string? targetDirectory = Path.GetDirectoryName(targetFile);

                    if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                        Directory.CreateDirectory(targetDirectory);

                    if (File.Exists(targetFile) || Directory.Exists(targetFile)) File.Delete(targetFile);
                    File.Copy(sourceFile, targetFile, true);
                }
            }
        }

        private static void DisableManualInstall(GameData game, ModData modData, string manualFolder, string fileName, bool directoryExists)
        {
            modData.isManual = false;
            string manualPath = Path.Combine(manualFolder, fileName);
            string rootPath = Path.Combine(game.StagingPath, fileName);

            string modName = directoryExists ? fileName : Path.GetFileNameWithoutExtension(fileName);
            string manualDirectory = Path.Combine(manualFolder, modName);

            string sourceDirectory = directoryExists ? manualPath : manualDirectory;
            if (Directory.Exists(sourceDirectory))
            {
                string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string sourceFile in sourceFiles)
                {
                    string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                    string targetFile = Path.Combine(game.DeploymentPath, relativePath);
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