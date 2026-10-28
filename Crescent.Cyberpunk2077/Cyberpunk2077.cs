using Avalonia.Controls;
using Avalonia.Threading;
using Crescent.Configs;
using Crescent.UI;
using Crescent.Utils;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Common;
using Logger = Crescent.Logging.Logger;

namespace Crescent.Cyberpunk2077
{
    public class Cyberpunk2077 : ConfiguredGameData
    {
        public static readonly List<string> CoreMods = new()
        {
            "redscript",
            "redfilemanager",
            "reddata",
            "red4ext",
            "audioware",
            "archivexl",
            "tweakxl",
            "engine",
            "cyberenginetweaks",
            "codeware",
            "cet",
            "cmd",
            "equipment-ex",
            "input_loader",
            "mod_settings",
            "nativesettings",
            "virtualatelier"
        };
        
        public override string Name => "Cyberpunk 2077";
        public override string NexusGameDomain => "cyberpunk2077";
        public override string Theme => "#FBF30A";
        
        [JsonIgnore]
        public override int ModCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < installedMods.Count; i++)
                {
                    ModData mod = installedMods[i];
                    if (SelectedProfile.IsModInstalled(mod))
                    {
                        if (mod.filePath.IsArchive()) continue;
                        string[] split = mod.filePath.Split(Path.PathSeparator);
                        string folder = split[split.Length - 1];

                        if (folder.Contains(ModsList.Archive)) continue;
                        count++;
                    }
                }
                
                return count;
            }
        }

        [Hint("Points to the path Cyberpunk mods will be deployed to. This should be the Cyberpunk 2077 root directory")] public override string DeploymentPath { get; set; }
        [Hint("Points to the path Cyberpunk mods are stored in, whether deployed or not. This is the location you should install mods to")] public override string StagingPath { get; set; }
        [Hint("Points to the Cyberpunk 2077 game executable")] public override string ExePath { get; set; }

        public override string GameLogo => "game_logo_cyberpunk2077.png";
        public override string Banner => "banner_cyberpunk2077.png";

        #region Deployment

        public override async Task Deploy(Window? parentWindow = null)
        {
            if (string.IsNullOrWhiteSpace(DeploymentPath) || string.IsNullOrWhiteSpace(StagingPath)) return;
            SetDeploymentStatus(DeploymentState.Deploying, "Preparing\nCyberpunk 2077 mods...");
            if (!Directory.Exists(DeploymentPath)) Directory.CreateDirectory(DeploymentPath);

            string archiveFolder = Path.Combine(StagingPath, ModsList.Archive);
            if (!Directory.Exists(archiveFolder)) Directory.CreateDirectory(archiveFolder);

            List<DeployedContent> deploymentQueue = new();
            HashSet<ModData> queuedMods = new();
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
                    
                    SetDeploymentStatus(DeploymentState.Deploying, $"Preparing\n{mod.DisplayName}...");

                    string manualSubFolder = Path.Combine(StagingPath, ModsList.Manual);
                    if (!Directory.Exists(manualSubFolder)) Directory.CreateDirectory(manualSubFolder);

                    bool isCoreMod = IsCoreMod(mod);
                    bool isManualFolder = File.Exists(Path.Combine(manualSubFolder, mod.modName)) || Directory.Exists(Path.Combine(manualSubFolder, mod.modName));

                    if (isCoreMod && !isManualFolder)
                    {
                        string rootModPath = Path.Combine(StagingPath, mod.modName);
                        string manualModPath = Path.Combine(manualSubFolder, mod.modName);
                        if (File.Exists(rootModPath)) File.Move(rootModPath, manualModPath, true);
                        else if (Directory.Exists(rootModPath)) Directory.Move(rootModPath, manualModPath);

                        string rootStagingDirectory = Path.Combine(StagingPath, Path.GetFileNameWithoutExtension(mod.modName));
                        string manualStagingDirectory = Path.Combine(manualSubFolder, Path.GetFileNameWithoutExtension(mod.modName));
                        if (Directory.Exists(rootStagingDirectory) && !Directory.Exists(manualStagingDirectory)) Directory.Move(rootStagingDirectory, manualStagingDirectory);

                        isManualFolder = true;
                    }

                    mod.isManual = isCoreMod || isManualFolder;
                    string stagingSubFolder = mod.isManual ? manualSubFolder : StagingPath;
                    string modPath = Path.Combine(stagingSubFolder, mod.modName);
                    string archivedZipPath = Path.Combine(archiveFolder, mod.modName);
                    string modStagingDirectory = Directory.Exists(modPath) ? modPath : Path.Combine(stagingSubFolder, Path.GetFileNameWithoutExtension(mod.modName));

                    try
                    {
                        if (!Directory.Exists(modStagingDirectory))
                        {
                            string sourceZipPath = File.Exists(modPath) ? modPath : (File.Exists(archivedZipPath) ? archivedZipPath : string.Empty);
                            if (!string.IsNullOrEmpty(sourceZipPath))
                            {
                                Directory.CreateDirectory(modStagingDirectory);
                                using IArchive archive = ArchiveFactory.OpenArchive(new FileInfo(sourceZipPath));
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

                        if (!mod.isManual && File.Exists(modPath))
                        {
                            if (File.Exists(archivedZipPath)) File.Delete(archivedZipPath);
                            File.Move(modPath, archivedZipPath);
                        }

                        if (Directory.Exists(modStagingDirectory))
                        {
                            string[] files = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
                            foreach (string sourceFile in files)
                            {
                                string relativePath = Path.GetRelativePath(modStagingDirectory, sourceFile);
                                string targetFile = Path.Combine(DeploymentPath, relativePath);
                                string? targetDirectory = Path.GetDirectoryName(targetFile);
                                deploymentQueue.Add(new DeployedContent(mod.modName, sourceFile, targetFile, targetDirectory, mod.isManual));
                                queuedMods.Add(mod);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Logger.Log(exception.ToString());
                    }

                    await Task.Yield();
                }
            });

            if (deploymentQueue.Count == 0)
            {
                bool hasEnabledUndeployedMods = installedMods.Any(mod => SelectedProfile.IsModEnabled(mod) && !mod.isDeployed);
                SetDeploymentStatus(hasEnabledUndeployedMods ? DeploymentState.PartiallyDeployed : installedMods.Any(mod => mod.isDeployed) ? DeploymentState.Deployed : DeploymentState.NotDeployed, hasEnabledUndeployedMods ? "No new files were deployable. Check the staging folders." : "All enabled mods are already deployed.");
                return;
            }

            ProgressWindow? progressWindow = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    progressWindow = new ProgressWindow();
                    progressWindow.Show();
                }
                catch (Exception exception)
                {
                    Logger.Log(exception.ToString());
                }
            });

            int processedFiles = 0;
            int totalFiles = deploymentQueue.Count;

            bool overwriteAll = false;
            foreach (DeployedContent item in deploymentQueue)
            {
                processedFiles++;
                SetDeploymentStatus(Configs.DeploymentState.Deploying, $"Deploying\n{item.modName} ({processedFiles}/{totalFiles})...");
                bool shouldUpdate = processedFiles == 1 || processedFiles == totalFiles || processedFiles % 20 == 0;
                if (shouldUpdate && progressWindow != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            progressWindow.UpdateProgress(processedFiles, totalFiles, item.modName, item.targetFile);
                        }
                        catch (Exception exception)
                        {
                            Logger.Log(exception.ToString());
                        }
                    }, DispatcherPriority.Background);
                }

                await Task.Run(async () =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(item.targetDirectory) && !Directory.Exists(item.targetDirectory)) Directory.CreateDirectory(item.targetDirectory);
                        if (File.Exists(item.targetFile) || Directory.Exists(item.targetFile))
                        {
                            if (parentWindow != null && !overwriteAll)
                            {
                                bool overwrite = false;
                                await Dispatcher.UIThread.InvokeAsync(async () =>
                                {
                                    try
                                    {
                                        InputDialog dialog = new("Overlap Detected", string.Concat("File overlap detected:\n", Path.GetRelativePath(DeploymentPath, item.targetFile), "\n\nChoose Overwrite or Skip:"), true, "yes");
                                        await dialog.ShowDialog(parentWindow);
                                        string input = dialog.InputText?.Trim().ToLower() ?? "";
                                        if (input == "all")
                                        {
                                            overwriteAll = true;
                                            overwrite = true;
                                        }
                                        else overwrite = string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase);
                                    }
                                    catch
                                    {
                                        overwrite = false;
                                    }
                                });

                                if (!overwrite) return;
                            }

                            if (File.Exists(item.targetFile)) File.Delete(item.targetFile);
                        }

                        if (item.isManual) File.Copy(item.sourceFile, item.targetFile, true);
                        else File.CreateSymbolicLink(item.targetFile, item.sourceFile);
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        Logger.Log(exception.ToString());
                    }
                    catch (Exception exception)
                    {
                        Logger.Log(exception.ToString());
                    }
                });

                if (processedFiles % 50 == 0) await Task.Yield();
            }

            foreach (ModData mod in queuedMods)
                mod.isDeployed = true;

            bool hasEnabledUndeployedModsAfterDeployment = installedMods.Any(mod => SelectedProfile.IsModEnabled(mod) && !mod.isDeployed);
            SetDeploymentStatus(hasEnabledUndeployedModsAfterDeployment ? DeploymentState.PartiallyDeployed : DeploymentState.Deployed, $"Deployed {processedFiles} file{(processedFiles == 1 ? "" : "s")} across {ModCount} mod{(SelectedProfile.mods.Count == 1 ? "" : "s")}!\nSkipped {skippedCount} already deployed/disabled file{(skippedCount == 1 ? "" : "s")}");

            await SaveAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    progressWindow?.Close();
                }
                catch (Exception exception)
                {
                    Logger.Log(exception.ToString());
                }
            });
        }

        public override void Withdraw()
        {
            if (string.IsNullOrWhiteSpace(DeploymentPath) || string.IsNullOrWhiteSpace(StagingPath)) return;
            SetDeploymentStatus(DeploymentState.Withdrawing, "Withdrawing\nCyberpunk 2077 mods...");
            int withdrawnCount = 0;
            foreach (ModData mod in installedMods)
            {
                if (!mod.isDeployed) continue;
                SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing\n{mod.DisplayName}...");
                try
                {
                    string stagingSubFolder = mod.isManual ? Path.Combine(StagingPath, ModsList.Manual) : StagingPath;
                    string modStagingDirectory = Path.Combine(stagingSubFolder, mod.modName);
                    if (!Directory.Exists(modStagingDirectory)) modStagingDirectory = Path.Combine(stagingSubFolder, Path.GetFileNameWithoutExtension(mod.modName));
                    if (!Directory.Exists(modStagingDirectory)) continue;

                    string[] files = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
                    foreach (string sourceFile in files)
                    {
                        string relativePath = Path.GetRelativePath(modStagingDirectory, sourceFile);
                        string targetFile = Path.Combine(DeploymentPath, relativePath);

                        if (File.Exists(targetFile)) File.Delete(targetFile);
                    }

                    mod.isDeployed = false;
                    withdrawnCount++;
                }
                catch (Exception exception)
                {
                    Logger.Log(exception.ToString());
                }
            }

            SetDeploymentStatus(DeploymentState.NotDeployed, $"Withdrew {withdrawnCount} mod{(SelectedProfile.mods.Count == 1 ? "" : "s")}!"); 
            _ = SaveAsync();
        }

        public override void WithdrawMod(ModData mod)
        {
            if (string.IsNullOrWhiteSpace(DeploymentPath) || string.IsNullOrWhiteSpace(StagingPath) || !mod.isDeployed) return;
            SetDeploymentStatus(DeploymentState.Withdrawing, $"Withdrawing\n{mod.DisplayName}...");
            try
            {
                string stagingSubFolder = mod.isManual ? Path.Combine(StagingPath, ModsList.Manual) : StagingPath;
                string modStagingDirectory = Path.Combine(stagingSubFolder, mod.modName);
                if (!Directory.Exists(modStagingDirectory)) modStagingDirectory = Path.Combine(stagingSubFolder, Path.GetFileNameWithoutExtension(mod.modName));
                if (!Directory.Exists(modStagingDirectory)) return;

                string[] files = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string sourceFile in files)
                {
                    string relativePath = Path.GetRelativePath(modStagingDirectory, sourceFile);
                    string targetFile = Path.Combine(DeploymentPath, relativePath);

                    if (File.Exists(targetFile)) File.Delete(targetFile);
                }

                mod.isDeployed = false;
                SetDeploymentStatus(installedMods.Any(installedMod => installedMod.isDeployed) ? DeploymentState.PartiallyDeployed : DeploymentState.NotDeployed, $"Withdrew {mod.DisplayName}!");
                _ = SaveAsync();
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
            }
        }

        #endregion

        #region Mod Filtering

        public override List<string> GetModFilters()
        {
            List<string> filters = base.GetModFilters();
            filters.Add("Core");
            return filters;
        }

        public override bool MatchesFilter(ModData mod, string filter)
        {
            if (filter == "Core") return IsCoreMod(mod);
            return base.MatchesFilter(mod, filter);
        }

        public override List<string> GetCoreModMatches() => CoreMods;

        public override bool IsCoreMod(ModData mod)
        {
            if (string.IsNullOrEmpty(mod.modName)) return false;
            string lower = mod.modName.ToLower().Replace(" ", "");

            foreach (string coreModMatch in GetCoreModMatches())
                if (lower.Contains(coreModMatch))
                    return true;

            return false;
        }

        #endregion
    }
}