using Avalonia.Threading;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Crescent.UI.Controls.Toasts;
using Crescent.Utilities;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public static partial class ModsIO
    {
        public static async Task CheckUpdatesOnLaunchAsync(PluginData? pluginData, Action<ModData>? onModUpdated, CancellationToken token)
        {
            if (pluginData == null || !Settings.Instance.checkUpdatesOnLaunch) return;

            List<ModData> modsToCheck = pluginData.installedMods.Where(mod => !mod.HasCheckedForUpdate).ToList();
            if (modsToCheck.Count == 0) return;

            for (int index = 0; index < modsToCheck.Count; index++)
            {
                if (token.IsCancellationRequested) return;
                ModData mod = modsToCheck[index];
                try
                {
                    await mod.CheckForUpdateAsync(pluginData.API.Name);
                    await Dispatcher.UIThread.InvokeAsync(() => onModUpdated?.Invoke(mod));
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }

                await Task.Delay(300, token);
            }

            await pluginData.SaveAsync();
        }

        public static async Task CheckAllUpdatesManualAsync(PluginData? pluginData, Action<ModData>? onModUpdated, Action? onCompleted)
        {
            if (pluginData == null) return;

            int totalMods = pluginData.installedMods.Count;
            int checkedCount = 0;

            foreach (ModData modData in pluginData.installedMods)
            {
                checkedCount++;
                ToastManager.ShowProgress("Checking Updates", $"{modData.DisplayName} ({checkedCount}/{totalMods})", $"Current: v{modData.version}", (double)checkedCount / Math.Max(1, totalMods) * 100);

                modData.HasCheckedForUpdate = false;
                await modData.CheckForUpdateAsync(pluginData.API.Name);
                onModUpdated?.Invoke(modData);

                await Task.Delay(350);
            }

            await pluginData.SaveAsync();
            onCompleted?.Invoke();

            int updatesFound = pluginData.installedMods.Count(mod => mod.UpdateAvailable);
            ToastManager.CompleteProgress("Update Check Complete", updatesFound > 0 ? $"{updatesFound} mod update{(updatesFound == 1 ? "" : "s")} available" : "All installed mods are up to date", updatesFound > 0 ? ToastType.Warning : ToastType.Success, 4500, $"Checked {totalMods} mod{totalMods.WithPlural()}");
        }

        public static async Task UpdateModAsync(PluginData plugin, ModData modData)
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
    }
}