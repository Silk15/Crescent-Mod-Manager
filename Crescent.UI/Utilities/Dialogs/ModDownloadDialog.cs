using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;
using System.Text.RegularExpressions;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Crescent.APIs.NexusMods;
using Newtonsoft.Json.Linq;

namespace Crescent.UI
{
    public class ModDownloadDialog : Dialog
    {
        private TextBlock? statusBlock;
        private ProgressBar? progressBar;
        private StackPanel? detailsPanel;
        private CrescentButton? closeButton;

        public bool DownloadSuccess { get; private set; }

        public ModDownloadDialog(PluginData pluginData, string modPageUrl)
        {
            Title = "Download Mod";
            SizeToContent = SizeToContent.Height;
            Width = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Colors.MainGradient;
            CanResize = false;
            Icon = new WindowIcon(AssetLoader.Open(new Uri(MainWindow.IconUri)));

            StackPanel mainPanel = GetMainLayout();
            Content = mainPanel;

            StartDownload(pluginData, modPageUrl);
        }

        public StackPanel GetMainLayout()
        {
            StackPanel mainPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };

            statusBlock = new TextBlock
            {
                Text = "Initialising download...",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            progressBar = new ProgressBar
            {
                Height = 30,
                IsIndeterminate = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = Brushes.CornflowerBlue
            };

            detailsPanel = new StackPanel
            {
                Spacing = 8
            };

            ScrollViewer detailsScrollViewer = new ScrollViewer
            {
                Height = 220,
                Content = detailsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Border border = new Border
            {
                Background = Colors.MainPanelEnd,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };

            border.Child = detailsScrollViewer;

            Grid grid = new Grid();

            closeButton = GetCloseButton();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            grid.Children.Add(progressBar);
            Grid.SetColumn(progressBar, 0);

            grid.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 1);

            mainPanel.Children.Add(statusBlock);
            mainPanel.Children.Add(border);
            mainPanel.Children.Add(grid);
            return mainPanel;
        }

        public CrescentButton GetCloseButton()
        {
            CrescentButton button = new CrescentButton(ButtonStyle.Translucent)
            {
                Content = "Close",
                Width = 90,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(10, 0, 0, 0)
            };

            button.onClick += () => Close();
            return button;
        }

        private void StartDownload(PluginData pluginData, string modPageUrl)
        {
            Task.Run(async () =>
            {
                try
                {
                    Match match = Regex.Match(modPageUrl, @"nexusmods\.com/([^/]+)/mods/(\d+)", RegexOptions.IgnoreCase);
                    if (!match.Success) return;

                    string gameDomain = match.Groups[1].Value;
                    string nexusId = match.Groups[2].Value;

                    JObject? modDetails = null;
                    Task<JObject?> detailsTask = NexusUtils.GetModDetailsJsonAsync(gameDomain, nexusId);
                    Task delayDetailsTask = Task.Delay(5000);
                    Task completedDetailsTask = await Task.WhenAny(detailsTask, delayDetailsTask);
                    if (completedDetailsTask == detailsTask) modDetails = await detailsTask;

                    string modName = modDetails?["name"]?.ToString() ?? nexusId;

                    await SetStatusAsync($"Fetching requirements for: {modName}", true);

                    List<RequirementNode> requirementTree = await BuildRequirementTreeAsync(gameDomain, nexusId, pluginData, 0, null, true);
                    List<RequirementNode> flatQueue = FlattenTree(requirementTree);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        statusBlock.Text = $"Ready to download ({flatQueue.Count} items)";
                        if (detailsPanel != null)
                        {
                            detailsPanel.Children.Clear();
                            for (int i = 0; i < flatQueue.Count; i++)
                            {
                                RequirementNode node = flatQueue[i];
                                detailsPanel.Children.Add(new TextBlock
                                {
                                    Text = new string(' ', node.Depth * 4) + $"• {node.ModName}",
                                    Foreground = Brushes.LightGray,
                                    FontSize = 12
                                });
                            }
                        }
                    });

                    await Task.Delay(1500);

                    for (int queueIndex = 0; queueIndex < flatQueue.Count; queueIndex++)
                    {
                        RequirementNode currentNode = flatQueue[queueIndex];
                        await SetStatusAsync($"Downloading {modName} ({queueIndex + 1}/{flatQueue.Count})", false);

                        JObject? modFiles = null;
                        Task<JObject?> filesTask = NexusUtils.GetModFilesJsonAsync(gameDomain, currentNode.NexusId);
                        Task delayFilesTask = Task.Delay(5000);
                        Task completedFilesTask = await Task.WhenAny(filesTask, delayFilesTask);
                        if (completedFilesTask == filesTask) modFiles = await filesTask;

                        if (modFiles != null)
                        {
                            JArray? filesArray = modFiles["files"] as JArray;
                            JToken? targetFile = GetTargetFile(filesArray);
                            if (targetFile != null)
                            {
                                string fileId = targetFile["file_id"]?.ToString() ?? string.Empty;
                                string fileName = targetFile["file_name"]?.ToString() ?? $"mod_{currentNode.NexusId}.zip";

                                if (!string.IsNullOrEmpty(fileId))
                                {
                                    string? downloadUrl = null;
                                    Task<string> urlTask = NexusUtils.GetDownloadUrlAsync(gameDomain, currentNode.NexusId, fileId);
                                    Task delayUrlTask = Task.Delay(5000);
                                    Task completedUrlTask = await Task.WhenAny(urlTask, delayUrlTask);
                                    if (completedUrlTask == urlTask) downloadUrl = await urlTask;

                                    if (!string.IsNullOrEmpty(downloadUrl))
                                    {
                                        string? savedPath = null;
                                        Task<string?> downloadTask = NexusUtils.DownloadFileAsync(downloadUrl, pluginData.StagingPath, fileName);
                                        Task delayDownloadTask = Task.Delay(15000);
                                        Task completedDownloadTask = await Task.WhenAny(downloadTask, delayDownloadTask);
                                        if (completedDownloadTask == downloadTask) savedPath = await downloadTask;

                                        if (!string.IsNullOrEmpty(savedPath))
                                        {
                                            ModData newModData = new ModData(pluginData, Path.GetFileName(savedPath), savedPath);
                                            pluginData.installedMods.Add(newModData);
                                            pluginData.OnModLoaded(newModData);
                                        }
                                    }
                                }
                            }
                        }

                        await Task.Delay(300);
                    }

                    await pluginData.SaveAsync();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        statusBlock.Text = "All downloads completed successfully!";
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = 100;
                        DownloadSuccess = true;
                    });
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                    await SetStatusAsync("An error occurred during download.", false);
                }
            });
        }

        private async Task<List<RequirementNode>> BuildRequirementTreeAsync(string gameDomain, string nexusId, PluginData pluginData, int depth, HashSet<string>? visited, bool isRoot)
        {
            if (visited == null) visited = new HashSet<string>();

            List<RequirementNode> nodes = new List<RequirementNode>();
            if (visited.Contains(nexusId)) return nodes;
            visited.Add(nexusId);

            if (!isRoot)
            {
                bool isInstalled = false;
                for (int i = 0; i < pluginData.installedMods.Count; i++)
                {
                    if (string.Equals(pluginData.installedMods[i].url.nexusId, nexusId, StringComparison.OrdinalIgnoreCase))
                    {
                        isInstalled = true;
                        break;
                    }
                }
                if (isInstalled) return nodes;
            }

            JObject? modDetails = null;
            Task<JObject?> detailsTask = NexusUtils.GetModDetailsJsonAsync(gameDomain, nexusId);
            Task delayDetailsTask = Task.Delay(5000);
            Task completedDetailsTask = await Task.WhenAny(detailsTask, delayDetailsTask);
            if (completedDetailsTask == detailsTask) modDetails = await detailsTask;

            string modName = modDetails?["name"]?.ToString() ?? nexusId;

            RequirementNode currentNode = new RequirementNode
            {
                NexusId = nexusId,
                ModName = modName,
                Depth = depth
            };

            List<string>? reqIds = null;
            Task<List<string>> reqTask = NexusUtils.GetRequirementIdsAsync(gameDomain, nexusId);
            Task delayReqTask = Task.Delay(5000);
            Task completedReqTask = await Task.WhenAny(reqTask, delayReqTask);
            if (completedReqTask == reqTask) reqIds = await reqTask;

            if (reqIds != null)
            {
                for (int i = 0; i < reqIds.Count; i++)
                {
                    List<RequirementNode> subNodes = await BuildRequirementTreeAsync(gameDomain, reqIds[i], pluginData, depth + 1, visited, false);
                    for (int j = 0; j < subNodes.Count; j++) currentNode.Children.Add(subNodes[j]);
                }
            }

            nodes.Add(currentNode);
            return nodes;
        }

        private List<RequirementNode> FlattenTree(List<RequirementNode> roots)
        {
            List<RequirementNode> result = new List<RequirementNode>();
            for (int i = 0; i < roots.Count; i++) FlattenRecursive(roots[i], result);
            return result;
        }

        private void FlattenRecursive(RequirementNode node, List<RequirementNode> list)
        {
            for (int i = 0; i < node.Children.Count; i++) FlattenRecursive(node.Children[i], list);
            if (!list.Any(n => string.Equals(n.NexusId, node.NexusId, StringComparison.OrdinalIgnoreCase))) list.Add(node);
        }

        private JToken? GetTargetFile(JArray? filesArray)
        {
            if (filesArray == null || filesArray.Count == 0) return null;

            IEnumerable<JToken> category1Files = filesArray.Where(f => f["category_id"]?.Value<int>() == 1);
            JToken? latestCategory1 = category1Files.OrderByDescending(GetSortValue).FirstOrDefault();

            if (latestCategory1 != null) return latestCategory1;
            return filesArray.OrderByDescending(GetSortValue).FirstOrDefault() ?? filesArray[0];
            
            long GetSortValue(JToken file)
            {
                JToken? timeToken = file["uploaded_time"];
                if (timeToken != null && timeToken.Type != JTokenType.Null)
                {
                    if (timeToken.Type == JTokenType.Date) return timeToken.Value<DateTime>().Ticks;
                    if (long.TryParse(timeToken.ToString(), out long lVal)) return lVal;
                }

                JToken? idToken = file["file_id"];
                if (idToken != null && idToken.Type != JTokenType.Null)
                {
                    if (long.TryParse(idToken.ToString(), out long idValue)) return idValue;
                }

                return 0;
            }
        }

        private async Task SetStatusAsync(string message, bool isIndeterminate)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (statusBlock != null) statusBlock.Text = message;
                if (progressBar != null) progressBar.IsIndeterminate = isIndeterminate;
            });
        }
    }

    public class RequirementNode
    {
        public string NexusId { get; set; } = string.Empty;
        public string ModName { get; set; } = string.Empty;
        public int Depth { get; set; }
        public List<RequirementNode> Children { get; set; } = new List<RequirementNode>();
    }
}