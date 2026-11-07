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
            StackPanel mainPanel = new()
            {
                Margin = new Thickness(20),
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };

            statusBlock = new()
            {
                Text = "Initialising download...",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
            };

            progressBar = new()
            {
                Height = 30,
                IsIndeterminate = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new(0, 0, 10, 0),
                Foreground = Brushes.CornflowerBlue
            };

            detailsPanel = new()
            {
                Spacing = 8
            };

            ScrollViewer detailsScrollViewer = new()
            {
                Height = 220,
                Content = detailsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Border border = new()
            {
                Background = Colors.MainPanelEnd,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };

            border.Child = detailsScrollViewer;

            Grid grid = new();

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
            CrescentButton button = new(ButtonStyle.Translucent)
            {
                Content = "Close",
                Width = 90,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new(10, 0, 0, 0)
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
                    if (!match.Success)
                    {
                        await SetStatusAsync("Invalid Nexus Mods Url :(", false);
                        return;
                    }

                    string gameDomain = match.Groups[1].Value;
                    string nexusId = match.Groups[2].Value;

                    JObject? modDetails = await NexusUtils.GetModDetailsJsonAsync(gameDomain, nexusId);
                    string modName = modDetails?["name"]?.ToString() ?? nexusId;

                    await SetStatusAsync($"Fetching mod data for: {modName}", true);
                    JObject? modFiles = await NexusUtils.GetModFilesJsonAsync(gameDomain, nexusId);
                    if (modFiles == null)
                    {
                        await SetStatusAsync("Failed to retrieve mod files.", false);
                        return;
                    }

                    JArray? filesArray = modFiles["files"] as JArray;
                    if (filesArray == null || filesArray.Count == 0)
                    {
                        await SetStatusAsync("No files found for this mod.", false);
                        return;
                    }

                    JToken? targetFile = ResolveTargetFile(filesArray);
                    if (targetFile == null)
                    {
                        await SetStatusAsync("No valid files found.", false);
                        return;
                    }

                    string fileId = targetFile["file_id"]?.ToString() ?? string.Empty;
                    string fileName = targetFile["file_name"]?.ToString() ?? $"mod_{nexusId}.zip";
                    List<string> downloadQueueIds = await CollectRequirementIdsAsync(gameDomain, nexusId, pluginData);

                    for (int queueIndex = 0; queueIndex < downloadQueueIds.Count; queueIndex++)
                    {
                        string currentNexusId = downloadQueueIds[queueIndex];
                        string currentModName = currentNexusId;
                        if (string.Equals(currentNexusId, nexusId, StringComparison.OrdinalIgnoreCase)) currentModName = modName;
                        else
                        {
                            JObject? reqDetails = await NexusUtils.GetModDetailsJsonAsync(gameDomain, currentNexusId);
                            currentModName = reqDetails?["name"]?.ToString() ?? currentNexusId;
                        }

                        await AddDetailEntryAsync(modName, currentModName, queueIndex + 1, downloadQueueIds.Count);

                        string currentFileId = fileId;
                        string currentFileName = fileName;

                        if (currentNexusId != nexusId)
                        {
                            JObject? requiredFiles = await NexusUtils.GetModFilesJsonAsync(gameDomain, currentNexusId);
                            if (requiredFiles != null)
                            {
                                JArray? reqFilesArray = requiredFiles["files"] as JArray;
                                JToken? reqTargetFile = ResolveTargetFile(reqFilesArray);
                                if (reqTargetFile != null)
                                {
                                    currentFileId = reqTargetFile["file_id"]?.ToString() ?? string.Empty;
                                    currentFileName = reqTargetFile["file_name"]?.ToString() ?? $"mod_{currentNexusId}.zip";
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(currentFileId))
                        {
                            string downloadUrl = await NexusUtils.GetDownloadUrlAsync(gameDomain, currentNexusId, currentFileId);
                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                string? savedPath = await NexusUtils.DownloadFileAsync(downloadUrl, pluginData.StagingPath, currentFileName);
                                if (!string.IsNullOrEmpty(savedPath))
                                {
                                    ModData newModData = new(pluginData, Path.GetFileName(savedPath), savedPath);
                                    newModData.requirementIds = new List<string>(downloadQueueIds);
                                    pluginData.installedMods.Add(newModData);
                                    pluginData.OnModLoaded(newModData);
                                }
                            }
                        }

                        await Task.Delay(300);
                    }

                    await pluginData.SaveAsync();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        statusBlock!.Text = "All downloads completed successfully!";
                        progressBar!.IsIndeterminate = false;
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

        private JToken? ResolveTargetFile(JArray? filesArray)
        {
            if (filesArray == null || filesArray.Count == 0) return null;

            IEnumerable<JToken> category1Files = filesArray.Where(f => f["category_id"]?.Value<int>() == 1);
            JToken? latestCategory1 = category1Files.OrderByDescending(f => f["uploaded_time"]?.Value<long>() ?? f["file_id"]?.Value<long>() ?? 0).FirstOrDefault();

            if (latestCategory1 != null) return latestCategory1;
            return filesArray.OrderByDescending(f => f["uploaded_time"]?.Value<long>() ?? f["file_id"]?.Value<long>() ?? 0).FirstOrDefault() ?? filesArray[0];
        }

        private async Task<List<string>> CollectRequirementIdsAsync(string gameDomain, string nexusId, PluginData pluginData)
        {
            List<string> downloadQueueIds = new List<string> { nexusId };
            List<string> requirementIds = await NexusUtils.GetRequirementIdsAsync(gameDomain, nexusId);

            for (int i = 0; i < requirementIds.Count; i++)
            {
                string reqId = requirementIds[i];
                bool isInstalled = false;
                for (int installedIndex = 0; installedIndex < pluginData.installedMods.Count; installedIndex++)
                {
                    ModData installedMod = pluginData.installedMods[installedIndex];
                    if (string.Equals(installedMod.url.nexusId, reqId, StringComparison.OrdinalIgnoreCase))
                    {
                        isInstalled = true;
                        break;
                    }
                }

                if (!isInstalled && !downloadQueueIds.Contains(reqId)) downloadQueueIds.Add(reqId);
            }

            return downloadQueueIds;
        }

        private async Task SetStatusAsync(string message, bool isIndeterminate)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (statusBlock != null) statusBlock.Text = message;
                if (progressBar != null) progressBar.IsIndeterminate = isIndeterminate;
            });
        }

        private async Task AddDetailEntryAsync(string modName, string currentModName, int current, int total)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (statusBlock != null) statusBlock.Text = $"Downloading {modName} ({current}/{total})";
                if (detailsPanel != null)
                {
                    detailsPanel.Children.Add(new TextBlock
                    {
                        Text = $"• {currentModName}",
                        Foreground = Brushes.LightGray,
                        FontSize = 12
                    });
                }
            });
        }
    }
}