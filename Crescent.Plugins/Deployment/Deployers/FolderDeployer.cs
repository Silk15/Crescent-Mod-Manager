using Crescent.Utilities;

namespace Crescent.Plugins
{
    public class FolderDeployer : Deployer
    {
        public List<string> ManifestFiles { get; set; } = new()
        {
            "manifest.json",
            "content.json",
            "fabric.mod.json",
            "quilt.mod.json",
            "pack.mcmeta"
        };

        public FolderDeployer(PluginData pluginData) : base(pluginData) { }

        public override async Task Deploy()
        {
            if (string.IsNullOrWhiteSpace(plugin.DeploymentPath) || string.IsNullOrWhiteSpace(plugin.StagingPath))
            {
                Notify(plugin.Name, "Deployment or staging path is not configured!", "Warning");
                return;
            }

            plugin.SetDeploymentStatus(DeploymentState.Deploying, $"Preparing {plugin.Name} mods...");
            PluginData.ShowProgressToast("Preparing Mods", $"Staging {plugin.Name} mods...", string.Empty, 0, true);

            SetupDirectories(out string archiveFolder, out string manualFolder);
            linker.hardLinkFallbackUsed = false;

            int deployedCount = 0;
            int skippedCount = 0;

            if (PluginData.OnProgressWindowOpen != null) await PluginData.OnProgressWindowOpen.Invoke();
            int totalMods = plugin.installedMods.Count;
            await Task.Run(async () =>
            {
                for (int index = 0; index < plugin.installedMods.Count; index++)
                {
                    ModData mod = plugin.installedMods[index];
                    if (!plugin.SelectedProfile.IsModEnabled(mod) || string.IsNullOrEmpty(mod.modName) || mod.isDeployed)
                    {
                        skippedCount++;
                        continue;
                    }

                    plugin.SetDeploymentStatus(DeploymentState.Deploying, $"Deploying {mod.DisplayName}...");
                    if (PluginData.OnProgressWindowUpdate != null)
                        await PluginData.OnProgressWindowUpdate.Invoke(index + 1, totalMods, mod.DisplayName, plugin.DeploymentPath);

                    PluginData.UpdateProgressToast(index + 1, totalMods, mod.DisplayName, plugin.DeploymentPath);
                    SetModState(mod, manualFolder);
                    
                    string stagingSubFolder = mod.isManual ? manualFolder : plugin.StagingPath;
                    string cleanName = ModData.StripArchiveExtension(mod.modName);
                    string modStagingDirectory = GetStagingFolder(mod, stagingSubFolder, cleanName);
                    try
                    {
                        ExtractMod(mod, stagingSubFolder, archiveFolder, modStagingDirectory);
                        ArchiveMod(mod, stagingSubFolder, archiveFolder);
                        NormalizeFolder(modStagingDirectory);

                        if (Directory.Exists(modStagingDirectory))
                        {
                            mod.filePath = modStagingDirectory;
                            DeployFolder(mod, modStagingDirectory, cleanName);
                            deployedCount++;
                        }
                    }
                    catch (Exception exception)
                    {
                        Logger.Log(exception.ToString());
                        Notify("Deployment Error", $"Failed to deploy {mod.DisplayName}", "Error");
                    }
                }
            });

            if (PluginData.OnProgressWindowClose != null)
                await PluginData.OnProgressWindowClose.Invoke();

            bool hasEnabledUndeployedMods = plugin.installedMods.Any(mod => plugin.SelectedProfile.IsModEnabled(mod) && !mod.isDeployed);
            DeploymentState state = hasEnabledUndeployedMods ? DeploymentState.PartiallyDeployed : DeploymentState.Deployed;
            string statusMessage = $"Deployed {deployedCount} mod{deployedCount.WithPlural()}";
            string detailMessage = skippedCount > 0 ? $"Skipped {skippedCount} already deployed or disabled mod{skippedCount.WithPlural()}" : string.Empty;

            plugin.SetDeploymentStatus(state, statusMessage);
            PluginData.CompleteProgressToast("Deployment Complete", statusMessage, "Success", 4500, detailMessage);
            await plugin.SaveAsync();
        }

        public override void Withdraw()
        {
            if (string.IsNullOrWhiteSpace(plugin.DeploymentPath) || string.IsNullOrWhiteSpace(plugin.StagingPath))
                return;

            if (!Directory.Exists(plugin.DeploymentPath))
                return;

            plugin.SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing {plugin.Name} mods...");
            int withdrawnCount = 0;

            for (int index = 0; index < plugin.installedMods.Count; index++)
            {
                ModData mod = plugin.installedMods[index];
                if (!mod.isDeployed && mod.ownedFiles.Count == 0) continue;
                if (RemoveModFolder(mod)) withdrawnCount++;
            }

            string summary = $"Withdrew {withdrawnCount} mod{withdrawnCount.WithPlural()}";
            plugin.SetDeploymentStatus(DeploymentState.NotDeployed, summary);
            Notify("Mods Withdrawn", $"{summary} from {plugin.Name}", "Success");
            _ = plugin.SaveAsync();
        }

        public override void WithdrawMod(ModData mod)
        {
            if (string.IsNullOrWhiteSpace(plugin.DeploymentPath) || string.IsNullOrWhiteSpace(plugin.StagingPath) || !mod.isDeployed)
                return;

            if (!Directory.Exists(plugin.DeploymentPath))
                return;

            plugin.SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing {mod.DisplayName}...");
            try
            {
                RemoveModFolder(mod);
                DeploymentState state = plugin.installedMods.Any(installedMod => installedMod.isDeployed) ? DeploymentState.PartiallyDeployed : DeploymentState.NotDeployed;

                plugin.SetDeploymentStatus(state, $"Withdrew {mod.DisplayName}");
                Notify("Mod Withdrawn", $"Removed {mod.DisplayName} from {plugin.Name}", "Success");
                _ = plugin.SaveAsync();
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
                Notify("Withdrawal Failed", $"Could not withdraw {mod.DisplayName}", "Error");
            }
        }

        public virtual void NormalizeFolder(string modStagingDirectory)
        {
            if (!Directory.Exists(modStagingDirectory)) return;

            bool hasManifestAtTop = ManifestFiles.Any(manifest => File.Exists(Path.Combine(modStagingDirectory, manifest)));
            if (hasManifestAtTop) return;

            string[] subDirectories = Directory.GetDirectories(modStagingDirectory, "*", SearchOption.TopDirectoryOnly);
            string[] topFiles = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.TopDirectoryOnly);

            if (subDirectories.Length == 1 && topFiles.Length == 0)
            {
                string innerFolder = subDirectories[0];
                bool innerHasManifest = ManifestFiles.Any(manifest => File.Exists(Path.Combine(innerFolder, manifest)));

                if (innerHasManifest || Directory.GetDirectories(innerFolder).Length > 0)
                {
                    string tempFolder = modStagingDirectory + "_unwrap";
                    if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);

                    Directory.Move(innerFolder, tempFolder);
                    Directory.Delete(modStagingDirectory, true);
                    Directory.Move(tempFolder, modStagingDirectory);
                }
            }
        }

        public virtual void DeployFolder(ModData mod, string modStagingDirectory, string modFolderName)
        {
            string targetModDirectory = Path.Combine(plugin.DeploymentPath, modFolderName);
            if (Directory.Exists(targetModDirectory)) Directory.Delete(targetModDirectory, true);
            Directory.CreateDirectory(targetModDirectory);
            mod.ownedFiles.Clear();

            string[] files = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string sourceFile = files[index];
                string relativeToMod = Path.GetRelativePath(modStagingDirectory, sourceFile);
                string targetFile = Path.Combine(targetModDirectory, relativeToMod);
                string relativeToDeployment = Path.GetRelativePath(plugin.DeploymentPath, targetFile);

                linker.LinkFile(sourceFile, targetFile, mod.isManual);
                mod.ownedFiles.Add(relativeToDeployment);
            }

            mod.isDeployed = true;
        }

        public virtual bool RemoveModFolder(ModData mod)
        {
            try
            {
                string cleanName = ModData.StripArchiveExtension(mod.modName);
                string targetDirectory = Path.Combine(plugin.DeploymentPath, cleanName);

                if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
                for (int index = 0; index < mod.ownedFiles.Count; index++)
                {
                    string deployedFile = Path.Combine(plugin.DeploymentPath, mod.ownedFiles[index]);
                    linker.DeleteFile(deployedFile);
                    CleanEmptyFolders(Path.GetDirectoryName(deployedFile));
                }

                mod.ownedFiles.Clear();
                mod.isDeployed = false;
                return true;
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
                return false;
            }
        }
    }
}