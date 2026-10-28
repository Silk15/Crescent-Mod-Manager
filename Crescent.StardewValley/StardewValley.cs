using Avalonia.Controls;
using Crescent.Configs;
using Crescent.UI;
using Crescent.Utils;
using SharpCompress.Archives;
using SharpCompress.Common;
using Logger = Crescent.Logging.Logger;

namespace Crescent.StardewValley
{
    public class StardewValley : ConfiguredGameData
    {
        public override string Name => "Stardew Valley";
        public override string NexusGameDomain => "stardewvalley";
        public override string Theme => "#067BE9";

        [Hint("Points to the path Stardew Valley mods will be deployed to. This should be:\n\"Stardew Valley\\Mods\"")] public override string DeploymentPath { get; set; }
        [Hint("Points to the path Stardew Valley mods are stored in, whether deployed or not. This is the location you should install mods to")] public override string StagingPath { get; set; }
        [Hint("Points to the Stardew Valley game executable")] public override string ExePath { get; set; }

        public override string GameLogo => "game_logo_stardew_valley.png";
        public override string Banner => "banner_stardew_valley.png";

        #region Deployment

        public override async Task Deploy(Window? parentWindow = null)
        {
            if (string.IsNullOrWhiteSpace(DeploymentPath) || string.IsNullOrWhiteSpace(StagingPath)) return;
            SetDeploymentStatus(Configs.DeploymentState.Deploying, "Preparing\nStardew Valley mods...");
            if (!Directory.Exists(DeploymentPath)) Directory.CreateDirectory(DeploymentPath);
            int deployedCount = 0;
            int skippedCount = 0;
            await Task.Run(async () =>
            {
                foreach (ModData mod in installedMods)
                {
                    if (!SelectedProfile.IsModEnabled(mod) || string.IsNullOrEmpty(mod.modName) || mod.isDeployed)
                    {
                        skippedCount++;
                        continue;
                    }

                    SetDeploymentStatus(Configs.DeploymentState.Deploying, $"Deploying\n{mod.DisplayName}...");

                    string manualSubFolder = Path.Combine(StagingPath, ModsList.Manual);
                    bool isManual = File.Exists(Path.Combine(manualSubFolder, mod.modName)) || Directory.Exists(Path.Combine(manualSubFolder, mod.modName));
                    mod.isManual = isManual;

                    string stagingSubFolder = mod.isManual ? manualSubFolder : StagingPath;
                    string modFolderName = Directory.Exists(Path.Combine(stagingSubFolder, mod.modName)) ? mod.modName : Path.GetFileNameWithoutExtension(mod.modName);

                    string modStagingDirectory = Path.Combine(stagingSubFolder, modFolderName);
                    string modZipPath = Path.Combine(stagingSubFolder, mod.modName);
                    try
                    {
                        if (!Directory.Exists(modStagingDirectory))
                        {
                            if (File.Exists(modZipPath))
                            {
                                Directory.CreateDirectory(modStagingDirectory);
                                using IArchive archive = ArchiveFactory.OpenArchive(new FileInfo(modZipPath));
                                foreach (IArchiveEntry entry in archive.Entries)
                                {
                                    if (!entry.IsDirectory)
                                    {
                                        ExtractionOptions options = new();
                                        options.ExtractFullPath = true;
                                        options.Overwrite = true;
                                        entry.WriteToDirectory(modStagingDirectory, options);
                                    }
                                }
                            }
                            else continue;
                        }

                        if (Directory.Exists(modStagingDirectory))
                        {
                            string targetModDirectory = Path.Combine(DeploymentPath, modFolderName);
                            if (Directory.Exists(targetModDirectory)) Directory.Delete(targetModDirectory, true);
                            Directory.CreateDirectory(targetModDirectory);

                            string[] files = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
                            foreach (string sourceFile in files)
                            {
                                string relativePath = Path.GetRelativePath(modStagingDirectory, sourceFile);
                                string targetFile = Path.Combine(targetModDirectory, relativePath);
                                string? targetDirectory = Path.GetDirectoryName(targetFile);

                                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);
                                if (File.Exists(targetFile)) File.Delete(targetFile);

                                if (mod.isManual) File.Copy(sourceFile, targetFile, true);
                                else File.CreateSymbolicLink(targetFile, sourceFile);
                            }

                            mod.isDeployed = true;
                            deployedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex.ToString());
                    }

                    await Task.Yield();
                }
            });

            bool hasEnabledUndeployedMods = installedMods.Any(mod => SelectedProfile.IsModEnabled(mod) && !mod.isDeployed);
            SetDeploymentStatus(hasEnabledUndeployedMods ? DeploymentState.PartiallyDeployed : DeploymentState.Deployed, $"Deployed {deployedCount} mod{(SelectedProfile.mods.Count == 1 ? "" : "s")}!\nSkipped {skippedCount} already deployed/disabled folder{(skippedCount == 1 ? "" : "s")}");
            await SaveAsync();
        }

        public override void Withdraw()
        {
            if (string.IsNullOrWhiteSpace(DeploymentPath) || string.IsNullOrWhiteSpace(StagingPath)) return;
            SetDeploymentStatus(DeploymentState.Withdrawing, "Withdrawing\nStardew Valley mods...");
            int withdrawnCount = 0;
            if (!Directory.Exists(DeploymentPath)) return;
            foreach (ModData mod in installedMods)
            {
                if (!mod.isDeployed) continue;
                SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing\n{mod.DisplayName}...");
                try
                {
                    string modFolderName = Directory.Exists(Path.Combine(StagingPath, mod.modName)) ? mod.modName : Path.GetFileNameWithoutExtension(mod.modName);
                    string targetDirectory = Path.Combine(DeploymentPath, modFolderName);
                    if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
                    mod.isDeployed = false;
                    withdrawnCount++;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.ToString());
                }
            }

            SetDeploymentStatus(DeploymentState.NotDeployed, $"Withdrew {withdrawnCount} mod{(SelectedProfile.mods.Count == 1 ? "" : "s")}!");
            _ = SaveAsync();
        }

        public override void WithdrawMod(ModData mod)
        {
            if (string.IsNullOrWhiteSpace(DeploymentPath) || string.IsNullOrWhiteSpace(StagingPath) || !mod.isDeployed) return;
            SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing\n{mod.DisplayName}...");
            if (!Directory.Exists(DeploymentPath)) return;
            try
            {
                string modFolderName = Directory.Exists(Path.Combine(StagingPath, mod.modName)) ? mod.modName : Path.GetFileNameWithoutExtension(mod.modName);
                string targetDirectory = Path.Combine(DeploymentPath, modFolderName);
                if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
                mod.isDeployed = false;
                SetDeploymentStatus(installedMods.Any(installedMod => installedMod.isDeployed) ? Configs.DeploymentState.PartiallyDeployed : Configs.DeploymentState.NotDeployed, $"Withdrew\n{mod.DisplayName}!");
                _ = SaveAsync();
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
            }
        }

        #endregion
    }
}