using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace Crescent.UI
{
    public static class NxmProtocolHandler
    {
        public const string PipeName = "Crescent_NXM_Pipe";
        private static readonly Regex NxmRegex = new(@"^nxm://([^/]+)/mods/(\d+)/files/(\d+)(?:\?(.*))?$", RegexOptions.IgnoreCase);

        private static CancellationTokenSource? pipeServerCancellation;
        public static event Action<PluginData>? onModDownloaded;

        public static bool TryForwardToRunningInstance(string[] args)
        {
            string? nxmArg = args.FirstOrDefault(arg => arg.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(nxmArg)) return false;

            try
            {
                using NamedPipeClientStream client = new(".", PipeName, PipeDirection.Out);
                client.Connect(400);
                byte[] payload = Encoding.UTF8.GetBytes(nxmArg);
                client.Write(payload, 0, payload.Length);
                client.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void StartPipeServer()
        {
            if (pipeServerCancellation != null) return;
            pipeServerCancellation = new();
            CancellationToken token = pipeServerCancellation.Token;
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await using NamedPipeServerStream server = new(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        await server.WaitForConnectionAsync(token);

                        using StreamReader reader = new(server, Encoding.UTF8);
                        string message = await reader.ReadToEndAsync(token);
                        if (!string.IsNullOrWhiteSpace(message)) _ = DownloadUrlOrNxmAsync(message.Trim());
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception.ToString());
                        await Task.Delay(500, token);
                    }
                }
            }, token);
        }

        public static bool RegisterProtocol(bool showToast = true)
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return false;

                using RegistryKey nxmKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\nxm");
                nxmKey.SetValue(string.Empty, "URL:NXM Protocol");
                nxmKey.SetValue("URL Protocol", string.Empty);

                using RegistryKey defaultIconKey = nxmKey.CreateSubKey("DefaultIcon");
                defaultIconKey.SetValue(string.Empty, $"\"{exePath}\",0");

                using RegistryKey commandKey = nxmKey.CreateSubKey(@"shell\open\command");
                commandKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");

                if (showToast) ToastManager.Show("NXM Protocol Registered", "Crescent is now your active mod manager", ToastType.Success, 3500);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                if (showToast) ToastManager.Show("Registration Failed", "Could not register Crescent :(", ToastType.Error);
                return false;
            }
        }

        public static bool IsProtocolRegistered()
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                using RegistryKey? commandKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\nxm\shell\open\command");
                string? currentCommand = commandKey?.GetValue(string.Empty)?.ToString();
                string? exePath = Environment.ProcessPath;
                return !string.IsNullOrEmpty(currentCommand) && !string.IsNullOrEmpty(exePath) && currentCommand.Contains(exePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static NxmLinkData? ParseNxmUri(string uri)
        {
            Match match = NxmRegex.Match(uri.Trim());
            if (!match.Success) return null;

            NxmLinkData data = new()
            {
                gameDomain = match.Groups[1].Value,
                modId = match.Groups[2].Value,
                fileId = match.Groups[3].Value
            };

            string queryString = match.Groups[4].Value;
            if (!string.IsNullOrEmpty(queryString))
            {
                string[] pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
                for (int index = 0; index < pairs.Length; index++)
                {
                    string[] kvp = pairs[index].Split('=', 2);
                    if (kvp.Length != 2) continue;
                    string key = Uri.UnescapeDataString(kvp[0]).ToLowerInvariant();
                    string val = Uri.UnescapeDataString(kvp[1]);
                    if (key == "key") data.key = val;
                    else if (key == "expires") data.expires = val;
                    else if (key == "user_id") data.userId = val;
                }
            }

            return data;
        }

        public static async Task<bool> DownloadUrlOrNxmAsync(string inputUrl, PluginData? explicitPlugin = null)
        {
            if (string.IsNullOrWhiteSpace(inputUrl)) return false;

            if (inputUrl.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase))
            {
                NxmLinkData? nxmData = ParseNxmUri(inputUrl);
                if (nxmData == null)
                {
                    ToastManager.Show("Invalid NXM Link", "Could not parse the provided nxm:// URI", ToastType.Error);
                    return false;
                }

                PluginData? targetPlugin = explicitPlugin ?? GetPluginByDomain(nxmData.gameDomain);
                if (targetPlugin == null)
                {
                    ToastManager.Show("Game Not Found", $"No installed plugin matches domain \"{nxmData.gameDomain}\"", ToastType.Error);
                    return false;
                }

                return await DownloadModWithRequirementsAsync(targetPlugin, nxmData.gameDomain, nxmData.modId, nxmData.fileId, nxmData.key, nxmData.expires);
            }

            Match webMatch = Regex.Match(inputUrl, @"nexusmods\.com/([^/]+)/mods/(\d+)", RegexOptions.IgnoreCase);
            if (!webMatch.Success)
            {
                ToastManager.Show("Invalid URL", "Enter a valid Nexus Mods Url", ToastType.Error);
                return false;
            }

            string gameDomain = webMatch.Groups[1].Value;
            string nexusId = webMatch.Groups[2].Value;
            Match fileParamMatch = Regex.Match(inputUrl, @"[?&]file_id=(\d+)", RegexOptions.IgnoreCase);
            string specificFileId = fileParamMatch.Success ? fileParamMatch.Groups[1].Value : string.Empty;

            PluginData? resolvedPlugin = explicitPlugin ?? GetPluginByDomain(gameDomain);
            if (resolvedPlugin == null)
            {
                ToastManager.Show("Game Not Found", $"No installed plugin matches \"{gameDomain}\"", ToastType.Error);
                return false;
            }

            return await DownloadModWithRequirementsAsync(resolvedPlugin, gameDomain, nexusId, specificFileId, string.Empty, string.Empty);
        }

        private static PluginData? GetPluginByDomain(string gameDomain)
        {
            for (int index = 0; index < PluginData.InstalledPlugins.Count; index++)
            {
                PluginData plugin = PluginData.InstalledPlugins[index];
                if (string.Equals(plugin.API.Name, gameDomain, StringComparison.OrdinalIgnoreCase) || string.Equals(plugin.Name.Replace(" ", ""), gameDomain, StringComparison.OrdinalIgnoreCase))
                    return plugin;
            }

            return PluginData.InstalledPlugins.FirstOrDefault();
        }

        public static async Task<bool> DownloadModWithRequirementsAsync(PluginData pluginData, string gameDomain, string rootNexusId, string rootFileId, string nxmKey, string nxmExpires)
        {
            if (string.IsNullOrEmpty(pluginData.StagingPath))
            {
                ToastManager.Show("Staging Path Missing", $"Set the staging folder for {pluginData.Name} in Settings first!", ToastType.Error);
                return false;
            }
            try
            {
                ToastManager.ShowProgress("Preparing Download", $"Fetching metadata for mod #{rootNexusId}...", pluginData.Name, 0, true);

                JObject? rootDetails = await NexusUtils.GetModDetailsJsonAsync(gameDomain, rootNexusId);
                string rootModName = rootDetails?["name"]?.ToString() ?? $"Mod {rootNexusId}";

                List<RequirementNode> requirementTree = await GetRequirementsAsync(gameDomain, rootNexusId, pluginData, 0, null, true);
                List<RequirementNode> flatQueue = FlattenTree(requirementTree);

                int downloadedCount = 0;
                for (int queueIndex = 0; queueIndex < flatQueue.Count; queueIndex++)
                {
                    RequirementNode currentNode = flatQueue[queueIndex];
                    bool isRootNode = string.Equals(currentNode.NexusId, rootNexusId, StringComparison.OrdinalIgnoreCase);

                    string fileIdToDownload = isRootNode && !string.IsNullOrEmpty(rootFileId) ? rootFileId : string.Empty;
                    string fallbackFileName = $"{currentNode.ModName}-{currentNode.NexusId}.zip";

                    if (string.IsNullOrEmpty(fileIdToDownload))
                    {
                        JObject? modFiles = await NexusUtils.GetModFilesJsonAsync(gameDomain, currentNode.NexusId);
                        JArray? filesArray = modFiles?["files"] as JArray;
                        JToken? targetFile = GetLatestVersion(filesArray);
                        if (targetFile != null)
                        {
                            fileIdToDownload = targetFile["file_id"]?.ToString() ?? string.Empty;
                            fallbackFileName = targetFile["file_name"]?.ToString() ?? fallbackFileName;
                        }
                    }

                    if (string.IsNullOrEmpty(fileIdToDownload)) continue;

                    string useKey = isRootNode ? nxmKey : string.Empty;
                    string useExpires = isRootNode ? nxmExpires : string.Empty;

                    string downloadUrl = await NexusUtils.GetDownloadUrlAsync(gameDomain, currentNode.NexusId, fileIdToDownload, useKey, useExpires);
                    if (string.IsNullOrEmpty(downloadUrl)) continue;

                    int itemNumber = queueIndex + 1;
                    int totalItems = flatQueue.Count;
                    Progress<double> itemProgress = new(percent => { ToastManager.ShowProgress($"Downloading ({itemNumber}/{totalItems})", $"{currentNode.ModName} ({percent:0}%)", pluginData.Name, percent, false); });

                    string? savedPath = await NexusUtils.DownloadFileAsync(downloadUrl, pluginData.StagingPath, fallbackFileName, itemProgress);
                    if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
                    {
                        string savedFileName = Path.GetFileName(savedPath);
                        ModData newModData = new(pluginData, savedFileName, savedPath);
                        pluginData.installedMods.RemoveAll(existing => string.Equals(existing.modName, savedFileName, StringComparison.OrdinalIgnoreCase));
                        pluginData.installedMods.Add(newModData);
                        pluginData.OnModLoaded(newModData);
                        downloadedCount++;
                    }
                }

                pluginData.SetLoadOrder();
                await pluginData.SaveAsync();

                if (downloadedCount == 0)
                {
                    ToastManager.CompleteProgress("Download Failed", $"Could not download files for {rootModName}! (Free accounts require clicking 'Mod Manager Download' on Nexus)", ToastType.Error, 5000, pluginData.Name);
                    return false;
                }

                ToastManager.CompleteProgress("Download Complete", $"Downloaded {rootModName}" + (downloadedCount > 1 ? $" (+{downloadedCount - 1} requirement{(downloadedCount - 1 == 1 ? "" : "s")})" : string.Empty), ToastType.Success, 4000, pluginData.Name);

                if (Settings.Instance.autoDeployOnDownload)
                    await pluginData.Deploy();

                Dispatcher.UIThread.Post(() => onModDownloaded?.Invoke(pluginData));
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                ToastManager.CompleteProgress("Download Error", "An error occurred while downloading the mod :(", ToastType.Error, 4000, pluginData.Name);
                return false;
            }
        }

        private static async Task<List<RequirementNode>> GetRequirementsAsync(string gameDomain, string nexusId, PluginData pluginData, int depth, HashSet<string>? visited, bool isRoot)
        {
            visited ??= new(StringComparer.OrdinalIgnoreCase);
            List<RequirementNode> nodes = new();
            if (visited.Contains(nexusId)) return nodes;
            visited.Add(nexusId);

            if (!isRoot && pluginData.installedMods.Any(mod => string.Equals(mod.url.nexusId, nexusId, StringComparison.OrdinalIgnoreCase)))
                return nodes;

            JObject? modDetails = await NexusUtils.GetModDetailsJsonAsync(gameDomain, nexusId);
            string modName = modDetails?["name"]?.ToString() ?? $"Mod {nexusId}";

            RequirementNode currentNode = new()
            {
                NexusId = nexusId,
                ModName = modName,
                Depth = depth
            };

            List<string> reqIds = await NexusUtils.GetRequirementIdsAsync(gameDomain, nexusId);
            for (int index = 0; index < reqIds.Count; index++)
            {
                List<RequirementNode> subNodes = await GetRequirementsAsync(gameDomain, reqIds[index], pluginData, depth + 1, visited, false);
                for (int subIndex = 0; subIndex < subNodes.Count; subIndex++)
                    currentNode.Children.Add(subNodes[subIndex]);
            }

            nodes.Add(currentNode);
            return nodes;
        }

        private static List<RequirementNode> FlattenTree(List<RequirementNode> roots)
        {
            List<RequirementNode> result = new();
            for (int index = 0; index < roots.Count; index++) FlattenRecursive(roots[index], result);
            return result;

            static void FlattenRecursive(RequirementNode node, List<RequirementNode> list)
            {
                for (int index = 0; index < node.Children.Count; index++)
                    FlattenRecursive(node.Children[index], list);

                if (!list.Any(existing => string.Equals(existing.NexusId, node.NexusId, StringComparison.OrdinalIgnoreCase)))
                    list.Add(node);
            }
        }

        public static JToken? GetLatestVersion(JArray? filesArray)
        {
            if (filesArray == null || filesArray.Count == 0) return null;

            IEnumerable<JToken> category1Files = filesArray.Where(file => file["category_id"]?.Value<int>() == 1);
            JToken? latestCategory1 = category1Files.OrderByDescending(GetSortValue).FirstOrDefault();
            return latestCategory1 ?? filesArray.OrderByDescending(GetSortValue).FirstOrDefault() ?? filesArray[0];

            static long GetSortValue(JToken file)
            {
                JToken? timeToken = file["uploaded_timestamp"] ?? file["uploaded_time"];
                if (timeToken != null && timeToken.Type != JTokenType.Null)
                {
                    if (timeToken.Type == JTokenType.Date) return timeToken.Value<DateTime>().Ticks;
                    if (long.TryParse(timeToken.ToString(), out long lVal)) return lVal;
                }

                JToken? idToken = file["file_id"];
                if (idToken != null && idToken.Type != JTokenType.Null && long.TryParse(idToken.ToString(), out long idValue)) return idValue;

                return 0;
            }
        }
    }
}