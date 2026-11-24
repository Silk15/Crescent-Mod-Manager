using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Crescent.UI.Controls.Toasts;
using Crescent.UI.Dialogs;
using Crescent.UI.Nxm;
using Newtonsoft.Json.Linq;
using Silk.Utilities;

namespace Crescent.UI.Queues
{
    public static class ModDownloadQueue
    {
        private static readonly ConcurrentQueue<QueuedDownloadItem> Queue = new();
        private static readonly HashSet<string> ActiveOrQueuedIds = new(StringComparer.OrdinalIgnoreCase);
        private static int isProcessing;

        public static int QueuedCount => Queue.Count;
        public static event Action? onQueueChanged;
        public static event Action<PluginData, string>? onModDownloadFinished;

        public static bool IsModQueued(string nexusId)
        {
            if (string.IsNullOrEmpty(nexusId)) return false;
            lock (ActiveOrQueuedIds) return ActiveOrQueuedIds.Contains(nexusId);
        }

        public static bool IsModInstalled(PluginData pluginData, string checkNexusId, string checkName = "")
        {
            for (int index = 0; index < pluginData.installedMods.Count; index++)
            {
                ModData installed = pluginData.installedMods[index];
                if (!string.IsNullOrEmpty(checkNexusId) && string.Equals(installed.url.nexusId, checkNexusId, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrEmpty(checkName) && (string.Equals(installed.nexusName, checkName, StringComparison.OrdinalIgnoreCase) || string.Equals(installed.DisplayName, checkName, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        public static async Task<bool> RequestDownloadAsync(PluginData pluginData, string modPageUrl, string modName = "", Window? ownerWindow = null)
        {
            if (string.IsNullOrWhiteSpace(modPageUrl)) return false;

            try
            {
                if (!NexusUtils.TryParseNexusUrl(modPageUrl, pluginData.API.Name, out string gameDomain, out string nexusId) || modPageUrl.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase))
                {
                    Enqueue(pluginData, modPageUrl, !string.IsNullOrEmpty(modName) ? modName : "Mod Download", string.Empty, pluginData.API.Name);
                    return true;
                }

                if (IsModQueued(nexusId)) return false;

                string initialName = !string.IsNullOrEmpty(modName) ? modName : $"Mod #{nexusId}";
                ShowProgressToast("Checking Requirements", $"Checking requirements for {initialName}...", pluginData.Name, 0, true);

                List<RequirementNode> allRequirements = new();
                string resolvedModName = initialName;

                await Task.Run(async () =>
                {
                    if (string.IsNullOrEmpty(modName))
                        resolvedModName = await NexusUtils.GetModNameAsync(gameDomain, nexusId) ?? initialName;

                    ShowProgressToast("Checking Requirements", $"Querying dependencies for {resolvedModName}...", pluginData.Name, 25, true);
                    await CollectRequirementsRecursiveAsync(pluginData, gameDomain, nexusId, allRequirements, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
                });

                List<RequirementNode> uninstalledRequired = allRequirements.Where(req => !req.IsOptional && !req.IsInstalled && !req.IsExternal).ToList();
                List<RequirementNode> uninstalledOptional = allRequirements.Where(req => (req.IsOptional || req.IsExternal) && !req.IsInstalled).ToList();

                if (uninstalledOptional.Count > 0 && ownerWindow != null)
                {
                    CloseProgressToast();
                    bool confirmed = false;

                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        ModDownloadDialog dialog = new(pluginData, gameDomain, nexusId, resolvedModName, allRequirements);
                        await dialog.ShowDialog(ownerWindow);
                        confirmed = dialog.DownloadSuccess;
                    });

                    return confirmed;
                }

                for (int index = 0; index < uninstalledRequired.Count; index++)
                {
                    RequirementNode req = uninstalledRequired[index];
                    string reqUrl = $"https://www.nexusmods.com/{gameDomain}/mods/{req.NexusId}";
                    Enqueue(pluginData, reqUrl, req.ModName, req.NexusId, gameDomain, false);
                }

                string mainUrl = $"https://www.nexusmods.com/{gameDomain}/mods/{nexusId}";
                Enqueue(pluginData, mainUrl, resolvedModName, nexusId, gameDomain, uninstalledRequired.Count == 0);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                CompleteProgressToast("Download Error", "Failed to check mod requirements :(", ToastType.Error, 3500, pluginData.Name);
                return false;
            }
        }

        private static async Task CollectRequirementsRecursiveAsync(PluginData pluginData, string gameDomain, string currentNexusId, List<RequirementNode> collected, HashSet<string> visited, int depth)
        {
            if (depth > 3 || !visited.Add(currentNexusId)) return;

            List<RequirementNode> directReqs = await NexusUtils.GetModRequirementsAsync(gameDomain, currentNexusId);
            for (int index = 0; index < directReqs.Count; index++)
            {
                RequirementNode node = directReqs[index];
                if (string.Equals(node.NexusId, currentNexusId, StringComparison.OrdinalIgnoreCase)) continue;
                if (collected.Any(existing => !string.IsNullOrEmpty(node.NexusId) && string.Equals(existing.NexusId, node.NexusId, StringComparison.OrdinalIgnoreCase))) continue;

                node.Depth = depth;
                node.IsInstalled = !node.IsExternal && IsModInstalled(pluginData, node.NexusId, node.ModName);
                node.IsSelected = !node.IsInstalled && !node.IsOptional;
                collected.Add(node);

                if (!node.IsOptional && !node.IsExternal && !node.IsInstalled && !string.IsNullOrEmpty(node.NexusId))
                {
                    ShowProgressToast("Checking Requirements", $"Checking sub-requirements for {node.ModName}...", pluginData.Name, 60, true);
                    await CollectRequirementsRecursiveAsync(pluginData, gameDomain, node.NexusId, collected, visited, depth + 1);
                }
            }
        }

        public static void Enqueue(PluginData pluginData, string urlOrNxm, string modName, string nexusId = "", string gameDomain = "", bool showQueuedToast = true)
        {
            if (!string.IsNullOrEmpty(nexusId))
            {
                lock (ActiveOrQueuedIds)
                {
                    if (!ActiveOrQueuedIds.Add(nexusId)) return;
                }
            }

            Queue.Enqueue(new QueuedDownloadItem
            {
                PluginData = pluginData,
                GameDomain = !string.IsNullOrEmpty(gameDomain) ? gameDomain : pluginData.API.Name,
                UrlOrNxm = urlOrNxm,
                ModName = modName,
                NexusId = nexusId
            });

            NotifyQueueChanged();
            if (showQueuedToast) ShowToast("Queued Download", $"Added {modName} to download queue", ToastType.Info, 2500, pluginData.Name);
            Task.Run(ProcessQueueAsync).FireAndForget();
        }

        private static async Task ProcessQueueAsync()
        {
            if (Interlocked.CompareExchange(ref isProcessing, 1, 0) != 0) return;

            PluginData? lastPluginData = null;
            int completedInBatch = 0;

            try
            {
                while (Queue.TryDequeue(out QueuedDownloadItem? item))
                {
                    lastPluginData = item.PluginData;
                    NotifyQueueChanged();

                    try
                    {
                        if (!NexusUtils.IsPremiumUser && !item.UrlOrNxm.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase))
                        {
                            CloseProgressToast();
                            $"{item.UrlOrNxm}?tab=files".TryOpen();
                            ShowToast("Opened in Browser", $"Click 'Mod Manager Download' on Nexus for {item.ModName}", ToastType.Info, 4500, item.PluginData.Name);
                        }
                        else if (item.UrlOrNxm.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(item.NexusId))
                        {
                            ShowProgressToast("Downloading Mod", $"Downloading {item.ModName}...", $"{Queue.Count} remaining in queue", 0, true);
                            bool success = await NxmProtocolHandler.DownloadUrlOrNxmAsync(item.UrlOrNxm, item.PluginData);
                            if (success) completedInBatch++;
                        }
                        else
                        {
                            bool success = await DownloadSingleNexusModAsync(item);
                            if (success) completedInBatch++;
                        }
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception.ToString());
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(item.NexusId))
                        {
                            lock (ActiveOrQueuedIds) ActiveOrQueuedIds.Remove(item.NexusId);
                        }

                        NotifyQueueChanged();
                        PluginData finishedPlugin = item.PluginData;
                        string finishedId = item.NexusId;
                        Dispatcher.UIThread.Post(() => onModDownloadFinished?.Invoke(finishedPlugin, finishedId));
                    }
                }

                if (lastPluginData != null && completedInBatch > 0)
                {
                    lastPluginData.SetLoadOrder();
                    await lastPluginData.SaveAsync();

                    if (Settings.Instance.autoDeployOnDownload)
                    {
                        ShowProgressToast("Deploying Mods", $"Deploying {lastPluginData.Name}...", lastPluginData.SelectedProfile.name, 0, true);
                        await lastPluginData.Deploy();
                    }

                    CompleteProgressToast("Download Complete", $"Installed {completedInBatch} mod{(completedInBatch == 1 ? "" : "s")}", ToastType.Success, 3500, lastPluginData.Name);
                }
            }
            finally
            {
                Interlocked.Exchange(ref isProcessing, 0);
                NotifyQueueChanged();
            }
        }

        private static async Task<bool> DownloadSingleNexusModAsync(QueuedDownloadItem item)
        {
            if (string.IsNullOrEmpty(item.PluginData.StagingPath))
            {
                CompleteProgressToast("Staging Path Missing", $"Set the staging folder for {item.PluginData.Name} in Settings first!", ToastType.Error, 4000, item.PluginData.Name);
                return false;
            }

            string queueDetail = Queue.Count > 0 ? $"{Queue.Count} remaining in queue" : item.PluginData.Name;
            ShowProgressToast("Preparing Download", $"Fetching files for {item.ModName}...", queueDetail, 0, true);

            JObject? modFiles = await NexusUtils.GetModFilesJsonAsync(item.GameDomain, item.NexusId);
            JArray? filesArray = modFiles?["files"] as JArray;
            JToken? targetFile = NxmProtocolHandler.GetLatestVersion(filesArray);

            string fileId = targetFile?["file_id"]?.ToString() ?? string.Empty;
            string fallbackFileName = targetFile?["file_name"]?.ToString() ?? $"{item.ModName}-{item.NexusId}.zip";

            if (string.IsNullOrEmpty(fileId))
            {
                CompleteProgressToast("Download Failed", $"No main file found for {item.ModName}", ToastType.Error, 3500, item.PluginData.Name);
                return false;
            }

            string downloadUrl = await NexusUtils.GetDownloadUrlAsync(item.GameDomain, item.NexusId, fileId);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                CompleteProgressToast("Download Failed", $"Could not retrieve download link for {item.ModName}", ToastType.Error, 3500, item.PluginData.Name);
                return false;
            }

            Progress<double> progress = new(percent =>
            {
                ShowProgressToast("Downloading Mod", $"{item.ModName} ({percent:0}%)", queueDetail, percent, false);
            });

            string? savedPath = await NexusUtils.DownloadFileAsync(downloadUrl, item.PluginData.StagingPath, fallbackFileName, progress);
            if (string.IsNullOrEmpty(savedPath) || !File.Exists(savedPath))
            {
                CompleteProgressToast("Download Failed", $"Failed to save {item.ModName}", ToastType.Error, 3500, item.PluginData.Name);
                return false;
            }

            string savedFileName = Path.GetFileName(savedPath);
            ModData newModData = new(item.PluginData, savedFileName, savedPath)
            {
                nexusName = item.ModName
            };

            item.PluginData.installedMods.RemoveAll(existing => string.Equals(existing.modName, savedFileName, StringComparison.OrdinalIgnoreCase));
            item.PluginData.installedMods.Add(newModData);
            item.PluginData.OnModLoaded(newModData);
            return true;
        }

        private static void NotifyQueueChanged() => Dispatcher.UIThread.Post(() => onQueueChanged?.Invoke());

        private static void ShowToast(string title, string message, ToastType type, int duration, string detail)
        {
            Dispatcher.UIThread.Post(() => ToastManager.Show(title, message, type, duration, detail));
        }

        private static void ShowProgressToast(string title, string message, string detail, double percent, bool indeterminate)
        {
            Dispatcher.UIThread.Post(() => ToastManager.ShowProgress(title, message, detail, percent, indeterminate));
        }

        private static void CompleteProgressToast(string title, string message, ToastType type, int duration, string detail)
        {
            Dispatcher.UIThread.Post(() => ToastManager.CompleteProgress(title, message, type, duration, detail));
        }

        private static void CloseProgressToast()
        {
            Dispatcher.UIThread.Post(ToastManager.OnProgressDialogClosed);
        }
    }
}