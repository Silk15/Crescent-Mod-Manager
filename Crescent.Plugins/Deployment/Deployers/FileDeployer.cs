using Crescent.IO;
using Crescent.Utilities;

namespace Crescent.Plugins
{
    public class FileDeployer : Deployer
    {
        public List<string> RootFolders { get; set; } = new();
        public Dictionary<string, string> ExtensionRoutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public FileDeployer(PluginData pluginData) : base(pluginData) { }

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

            List<DeployedContent> deploymentQueue = new();
            HashSet<ModData> queuedMods = new();
            HashSet<string> knownModFiles = GetKnownOwnedFiles();
            int skippedCount = 0;

            plugin.SetLoadOrder();
            List<ModData> modsInLoadOrder = plugin.installedMods.OrderBy(mod => mod.loadOrder).ToList();

            await Task.Run(() =>
            {
                for (int index = 0; index < modsInLoadOrder.Count; index++)
                {
                    ModData mod = modsInLoadOrder[index];
                    if (!plugin.SelectedProfile.IsModEnabled(mod) || string.IsNullOrEmpty(mod.modName))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (mod.isDeployed && AreOwnedFilesPresent(mod))
                    {
                        skippedCount++;
                        continue;
                    }

                    SetModState(mod, manualFolder);

                    string stagingSubFolder = mod.isManual ? manualFolder : plugin.StagingPath;
                    string cleanName = ModData.StripArchiveExtension(mod.modName);
                    string modStagingDirectory = GetStagingFolder(mod, stagingSubFolder, cleanName);

                    try
                    {
                        ExtractMod(mod, stagingSubFolder, archiveFolder, modStagingDirectory);
                        ArchiveMod(mod, stagingSubFolder, archiveFolder);
                        NormalizeFolder(modStagingDirectory);

                        if (Directory.Exists(modStagingDirectory)) mod.filePath = modStagingDirectory;
                        QueueFiles(mod, modStagingDirectory, deploymentQueue, queuedMods);
                    }
                    catch (Exception exception)
                    {
                        Logger.Log(exception.ToString());
                        Notify("Extraction Failed", $"Could not stage {mod.DisplayName}", "Error");
                    }
                }
            });

            if (deploymentQueue.Count == 0)
            {
                if (PluginData.OnProgressWindowClose != null)
                    await PluginData.OnProgressWindowClose.Invoke();

                FinishEmptyDeployment();
                return;
            }

            Dictionary<string, List<ModData>> conflicts = plugin.GetConflicts();
            if (conflicts.Count > 0 && PluginData.OnConflictResolve != null)
            {
                bool canProceed = await PluginData.OnConflictResolve.Invoke(conflicts);
                if (!canProceed)
                {
                    if (PluginData.OnProgressWindowClose != null)
                        await PluginData.OnProgressWindowClose.Invoke();

                    plugin.SetDeploymentStatus(DeploymentState.NotDeployed, "Deployment cancelled due to unresolved file conflicts");
                    PluginData.CompleteProgressToast("Deployment Cancelled", $"Unresolved file conflicts in {plugin.Name}", "Warning", 4000);
                    return;
                }
            }

            if (PluginData.OnProgressWindowOpen != null)
                await PluginData.OnProgressWindowOpen.Invoke();

            int processedFiles = 0;
            int failedFiles = 0;
            int backedUpVanillaFiles = 0;
            int totalFiles = deploymentQueue.Count;
            int updateInterval = Math.Max(1, totalFiles / 40);

            await Task.Run(async () =>
            {
                for (int index = 0; index < deploymentQueue.Count; index++)
                {
                    DeployedContent item = deploymentQueue[index];
                    processedFiles++;

                    bool shouldUpdate = processedFiles == 1 || processedFiles == totalFiles || processedFiles % updateInterval == 0;
                    if (shouldUpdate)
                    {
                        if (PluginData.OnProgressWindowUpdate != null)
                            await PluginData.OnProgressWindowUpdate.Invoke(processedFiles, totalFiles, item.modName, item.targetFile);

                        PluginData.UpdateProgressToast(processedFiles, totalFiles, item.modName, item.targetFile);
                    }

                    string relativePath = Path.GetRelativePath(plugin.DeploymentPath, item.targetFile);
                    if (backups.BackupFile(plugin.StagingPath, plugin.DeploymentPath, relativePath, knownModFiles.Contains(relativePath)))
                        backedUpVanillaFiles++;

                    LinkType result = linker.LinkFile(item.sourceFile, item.targetFile, item.isManual);
                    if (result == LinkType.Failed)
                        failedFiles++;
                }
            });

            if (failedFiles == 0)
            {
                foreach (ModData mod in queuedMods)
                    mod.isDeployed = true;
            }

            if (PluginData.OnProgressWindowClose != null)
                await PluginData.OnProgressWindowClose.Invoke();

            FinishDeployment(processedFiles, failedFiles, queuedMods.Count, backedUpVanillaFiles);
        }

        public override void Withdraw()
        {
            if (string.IsNullOrWhiteSpace(plugin.DeploymentPath) || string.IsNullOrWhiteSpace(plugin.StagingPath))
                return;

            plugin.SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing {plugin.Name} mods...");
            backups.Load(plugin.StagingPath);

            int withdrawnCount = 0;
            for (int index = 0; index < plugin.installedMods.Count; index++)
            {
                ModData mod = plugin.installedMods[index];
                if (!mod.isDeployed && mod.ownedFiles.Count == 0) continue;
                if (WithdrawModInternal(mod, false))
                    withdrawnCount++;
            }

            int restoredVanillaCount = backups.RestoreAll(plugin.StagingPath, plugin.DeploymentPath);
            string summary = $"Withdrew {withdrawnCount} mod{withdrawnCount.WithPlural()} from game directory";
            string detail = restoredVanillaCount > 0 ? $"Restored {restoredVanillaCount} original game file{restoredVanillaCount.WithPlural()}" : string.Empty;

            plugin.SetDeploymentStatus(DeploymentState.NotDeployed, summary);
            Notify("Mods Withdrawn", summary, "Success", 4000, detail);
            _ = plugin.SaveAsync();
        }

        public override void WithdrawMod(ModData mod)
        {
            if (string.IsNullOrWhiteSpace(plugin.DeploymentPath) || string.IsNullOrWhiteSpace(plugin.StagingPath))
                return;

            if (!mod.isDeployed && !mod.isManual && mod.ownedFiles.Count == 0)
                return;

            plugin.SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing {mod.DisplayName}...");
            backups.Load(plugin.StagingPath);

            try
            {
                WithdrawModInternal(mod, true);
                DeploymentState newState = plugin.installedMods.Any(installedMod => installedMod.isDeployed) ? DeploymentState.PartiallyDeployed : DeploymentState.NotDeployed;

                plugin.SetDeploymentStatus(newState, $"Withdrew {mod.DisplayName}");
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

            if (RootFolders.Count > 0)
            {
                bool hasRootFolderAtTop = Directory.GetDirectories(modStagingDirectory).Select(directory => Path.GetFileName(directory)).Any(folderName => RootFolders.Contains(folderName, StringComparer.OrdinalIgnoreCase));
                if (!hasRootFolderAtTop)
                {
                    string[] allDirectories = Directory.GetDirectories(modStagingDirectory, "*", SearchOption.AllDirectories);
                    string? nestedRootParent = null;

                    for (int index = 0; index < allDirectories.Length; index++)
                    {
                        string folderName = Path.GetFileName(allDirectories[index]);
                        if (RootFolders.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                        {
                            nestedRootParent = Path.GetDirectoryName(allDirectories[index]);
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(nestedRootParent) && !string.Equals(Path.GetFullPath(nestedRootParent), Path.GetFullPath(modStagingDirectory), StringComparison.OrdinalIgnoreCase)) 
                        UnwrapFolder(nestedRootParent, modStagingDirectory);
                }
            }

            if (ExtensionRoutes.Count > 0)
            {
                string[] topFiles = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.TopDirectoryOnly);
                for (int index = 0; index < topFiles.Length; index++)
                {
                    string topFile = topFiles[index];
                    string extension = Path.GetExtension(topFile);

                    if (ExtensionRoutes.TryGetValue(extension, out string? targetSubFolder) && !string.IsNullOrEmpty(targetSubFolder))
                    {
                        string destinationFolder = Path.Combine(modStagingDirectory, targetSubFolder);
                        if (!Directory.Exists(destinationFolder)) Directory.CreateDirectory(destinationFolder);

                        string destinationFile = Path.Combine(destinationFolder, Path.GetFileName(topFile));
                        File.Move(topFile, destinationFile, true);
                    }
                }
            }
        }

        public virtual void UnwrapFolder(string sourceRoot, string targetRoot)
        {
            string tempFolder = targetRoot + "_unwrap";
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);

            Directory.Move(sourceRoot, tempFolder);
            if (Directory.Exists(targetRoot)) Directory.Delete(targetRoot, true);

            Directory.Move(tempFolder, targetRoot);
        }

        public virtual void QueueFiles(ModData mod, string modStagingDirectory, List<DeployedContent> deploymentQueue, HashSet<ModData> queuedMods)
        {
            if (!Directory.Exists(modStagingDirectory)) return;

            string[] files = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
            mod.ownedFiles.Clear();

            for (int index = 0; index < files.Length; index++)
            {
                string sourceFile = files[index];
                string relativePath = Path.GetRelativePath(modStagingDirectory, sourceFile);
                string targetFile = Path.Combine(plugin.DeploymentPath, relativePath);
                string targetDirectory = Path.GetDirectoryName(targetFile) ?? string.Empty;

                mod.ownedFiles.Add(relativePath);
                deploymentQueue.Add(new(mod.modName, sourceFile, targetFile, targetDirectory, mod.isManual));
                queuedMods.Add(mod);
            }
        }

        public virtual bool WithdrawModInternal(ModData mod, bool restoreWinners)
        {
            try
            {
                HashSet<string> pathsToRemove = new(mod.ownedFiles, StringComparer.OrdinalIgnoreCase);
                string stagingSubFolder = mod.isManual ? Path.Combine(plugin.StagingPath, PluginData.Manual) : plugin.StagingPath;
                string cleanName = ModData.StripArchiveExtension(mod.modName);
                string modStagingDirectory = GetStagingFolder(mod, stagingSubFolder, cleanName);

                if (Directory.Exists(modStagingDirectory))
                {
                    string[] stagingFiles = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
                    for (int index = 0; index < stagingFiles.Length; index++)
                    {
                        string relativePath = Path.GetRelativePath(modStagingDirectory, stagingFiles[index]);
                        pathsToRemove.Add(relativePath);
                    }
                }

                mod.isDeployed = false;
                foreach (string relativePath in pathsToRemove)
                {
                    string targetFile = Path.Combine(plugin.DeploymentPath, relativePath);
                    if (restoreWinners)
                    {
                        ModData? fallbackWinner = GetWinner(relativePath, mod);
                        if (fallbackWinner != null)
                        {
                            string winnerSourceFile = GetModSourceFile(fallbackWinner, relativePath);
                            if (File.Exists(winnerSourceFile))
                            {
                                linker.LinkFile(winnerSourceFile, targetFile, fallbackWinner.isManual);
                                continue;
                            }
                        }
                    }

                    linker.DeleteFile(targetFile);

                    if (restoreWinners) backups.RestoreFile(plugin.StagingPath, plugin.DeploymentPath, relativePath);
                    CleanEmptyFolders(Path.GetDirectoryName(targetFile));
                }

                return true;
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
                return false;
            }
        }

        public virtual ModData? GetWinner(string relativePath, ModData excludedMod) => plugin.installedMods.Where(candidate => candidate != excludedMod && candidate.isDeployed && plugin.SelectedProfile.IsModEnabled(candidate) && candidate.ownedFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase)).OrderByDescending(candidate => candidate.loadOrder).FirstOrDefault();

        public virtual string GetModSourceFile(ModData mod, string relativePath)
        {
            string stagingSubFolder = mod.isManual ? Path.Combine(plugin.StagingPath, PluginData.Manual) : plugin.StagingPath;
            string cleanName = ModData.StripArchiveExtension(mod.modName);
            string modStagingDirectory = GetStagingFolder(mod, stagingSubFolder, cleanName);
            return Path.Combine(modStagingDirectory, relativePath);
        }

        public virtual bool AreOwnedFilesPresent(ModData mod)
        {
            if (mod.ownedFiles.Count == 0) return false;
            for (int index = 0; index < mod.ownedFiles.Count; index++)
            {
                string targetFile = Path.Combine(plugin.DeploymentPath, mod.ownedFiles[index]);
                if (!File.Exists(targetFile)) return false;
            }

            return true;
        }

        public virtual HashSet<string> GetKnownOwnedFiles()
        {
            HashSet<string> knownFiles = new(StringComparer.OrdinalIgnoreCase);
            for (int modIndex = 0; modIndex < plugin.installedMods.Count; modIndex++)
            {
                ModData mod = plugin.installedMods[modIndex];
                for (int fileIndex = 0; fileIndex < mod.ownedFiles.Count; fileIndex++)
                    knownFiles.Add(mod.ownedFiles[fileIndex]);
            }

            return knownFiles;
        }

        public virtual void FinishEmptyDeployment()
        {
            bool hasEnabledUndeployedMods = plugin.installedMods.Any(mod => plugin.SelectedProfile.IsModEnabled(mod) && !mod.isDeployed);
            DeploymentState state = hasEnabledUndeployedMods ? DeploymentState.PartiallyDeployed : (plugin.installedMods.Any(mod => mod.isDeployed) ? DeploymentState.Deployed : DeploymentState.NotDeployed);

            string message = hasEnabledUndeployedMods ? "No deployable files found in staging!" : "All enabled mods are already deployed";
            plugin.SetDeploymentStatus(state, message);
            PluginData.CompleteProgressToast(plugin.Name, message, hasEnabledUndeployedMods ? "Warning" : "Info", 4000);
            _ = plugin.SaveAsync();
        }

        public virtual void FinishDeployment(int processedFiles, int failedFiles, int totalDeployedModCount, int backedUpVanillaFiles)
        {
            bool hasEnabledUndeployedMods = plugin.installedMods.Any(mod => plugin.SelectedProfile.IsModEnabled(mod) && !mod.isDeployed);
            DeploymentState state = (hasEnabledUndeployedMods || failedFiles > 0) ? DeploymentState.PartiallyDeployed : DeploymentState.Deployed;
            string statusText = $"Deployed {totalDeployedModCount} mod{totalDeployedModCount.WithPlural()} ({processedFiles} file{processedFiles.WithPlural()})";

            List<string> details = new();
            if (failedFiles > 0) details.Add($"{failedFiles} failed");
            if (backedUpVanillaFiles > 0) details.Add($"Backed up {backedUpVanillaFiles} vanilla file{backedUpVanillaFiles.WithPlural()}");
            if (linker.hardLinkFallbackUsed) details.Add("Used hardlink/copy fallback");
            string detailText = string.Join(" • ", details);

            plugin.SetDeploymentStatus(state, statusText);
            PluginData.CompleteProgressToast(failedFiles > 0 ? "Partial Deployment" : "Deployment Complete", statusText, failedFiles > 0 ? "Warning" : "Success", 4500, detailText);
            _ = plugin.SaveAsync();
        }
    }
}