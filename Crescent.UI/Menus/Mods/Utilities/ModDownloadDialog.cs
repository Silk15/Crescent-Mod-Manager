using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;
using System.Text.RegularExpressions;
using Avalonia.Controls.Primitives;
using Crescent.APIs.NexusMods;
using Newtonsoft.Json.Linq;

namespace Crescent.UI
{
    public class ModDownloadDialog : Window
    {
        private TextBlock? statusBlock;
        private ProgressBar? progressBar;
        private StackPanel? detailsPanel;
        private CrescentButton? closeButton;
        
        public bool DownloadSuccess { get; private set; }

        public ModDownloadDialog(GameData gameData, string modPageUrl)
        {
            Title = "Download Mod & Dependencies";
            Width = 500;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromArgb(255, 37, 37, 38));
            Foreground = Brushes.White;
            CanResize = false;

            StackPanel mainPanel = GetMainLayout();
            Content = mainPanel;

            StartDownload(gameData, modPageUrl);
        }

        #region UI

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
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            mainPanel.Children.Add(statusBlock);

            progressBar = new()
            {
                Height = 20,
                IsIndeterminate = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            mainPanel.Children.Add(progressBar);

            detailsPanel = new()
            {
                Spacing = 8
            };

            ScrollViewer detailsScrollViewer = new()
            {
                Height = 120,
                Content = detailsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            mainPanel.Children.Add(detailsScrollViewer);

            closeButton = GetCloseButton();
            mainPanel.Children.Add(closeButton);

            return mainPanel;
        }

        public CrescentButton GetCloseButton()
        {
            CrescentButton button = new(ButtonStyle.Translucent)
            {
                Content = "Close",
                Width = 90,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsEnabled = false
            };

            button.onClick += () => Close();
            return button;
        }

        #endregion

        #region Downloads

        private void StartDownload(GameData gameData, string modPageUrl)
        {
            Task.Run(async () =>
            {
                try
                {
                    Match match = Regex.Match(modPageUrl, @"nexusmods\.com/([^/]+)/mods/(\d+)", RegexOptions.IgnoreCase);
                    if (!match.Success)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock.Text = "Invalid Nexus Mods URL.";
                            progressBar.IsIndeterminate = false;
                            closeButton.IsEnabled = true;
                        });
                        return;
                    }

                    string gameDomain = match.Groups[1].Value;
                    string nexusId = match.Groups[2].Value;

                    await Dispatcher.UIThread.InvokeAsync(() => statusBlock.Text = $"Fetching mod data for ID: {nexusId}");
                    JObject? modDetails = await NexusUtils.GetModDetailsJsonAsync(gameDomain, nexusId);
                    JObject? modFiles = await NexusUtils.GetModFilesJsonAsync(gameDomain, nexusId);
                    if (modFiles == null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock.Text = "Failed to retrieve mod files.";
                            progressBar.IsIndeterminate = false;
                            closeButton.IsEnabled = true;
                        });
                        return;
                    }

                    JArray? filesArray = modFiles["files"] as JArray;
                    if (filesArray == null || filesArray.Count == 0)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock.Text = "No files found for this mod.";
                            progressBar.IsIndeterminate = false;
                            closeButton.IsEnabled = true;
                        });
                        return;
                    }

                    JToken? targetFile = null;
                    for (int fileIndex = 0; fileIndex < filesArray.Count; fileIndex++)
                    {
                        JToken fileToken = filesArray[fileIndex];
                        int categoryId = fileToken["category_id"]?.Value<int>() ?? 0;
                        if (categoryId == 1)
                        {
                            targetFile = fileToken;
                            break;
                        }
                    }

                    if (targetFile == null) targetFile = filesArray[0];
                    string fileId = targetFile["file_id"]?.ToString() ?? string.Empty;
                    string fileName = targetFile["file_name"]?.ToString() ?? $"mod_{nexusId}.zip";
                    List<string> downloadQueueIds = new()
                    {
                        nexusId
                    };
                    
                    if (modDetails != null)
                    {
                        JArray? requirementIds = modDetails["required_mods"] as JArray;
                        if (requirementIds != null)
                        {
                            for (int reqIndex = 0; reqIndex < requirementIds.Count; reqIndex++)
                            {
                                string reqId = requirementIds[reqIndex]?.ToString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(reqId))
                                {
                                    bool isInstalled = false;
                                    for (int installedIndex = 0; installedIndex < gameData.installedMods.Count; installedIndex++)
                                    {
                                        ModData installedMod = gameData.installedMods[installedIndex];
                                        if (string.Equals(installedMod.url.nexusId, reqId, StringComparison.OrdinalIgnoreCase))
                                        {
                                            isInstalled = true;
                                            break;
                                        }
                                    }

                                    if (!isInstalled) downloadQueueIds.Add(reqId);
                                }
                            }
                        }
                    }

                    for (int queueIndex = 0; queueIndex < downloadQueueIds.Count; queueIndex++)
                    {
                        string currentNexusId = downloadQueueIds[queueIndex];
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock.Text = $"Downloading mod ({queueIndex + 1}/{downloadQueueIds.Count}) Mod ID: {currentNexusId}";
                            detailsPanel.Children.Add(new TextBlock
                            {
                                Text = $"Queued download: Mod ID {currentNexusId}",
                                Foreground = Brushes.LightGray,
                                FontSize = 12
                            });
                        });

                        string currentFileId = fileId;
                        string currentFileName = fileName;

                        if (currentNexusId != nexusId)
                        {
                            JObject? requiredFiles = await NexusUtils.GetModFilesJsonAsync(gameDomain, currentNexusId);
                            if (requiredFiles != null)
                            {
                                JArray? reqFilesArray = requiredFiles["files"] as JArray;
                                if (reqFilesArray != null && reqFilesArray.Count > 0)
                                {
                                    JToken reqTargetFile = reqFilesArray[0];
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
                                string? savedPath = await NexusUtils.DownloadFileAsync(downloadUrl, gameData.StagingPath, currentFileName);
                                if (!string.IsNullOrEmpty(savedPath))
                                {
                                    ModData newModData = new(gameData, Path.GetFileName(savedPath), savedPath);
                                    gameData.installedMods.Add(newModData);
                                    gameData.OnModLoaded(newModData);
                                }
                            }
                        }

                        await Task.Delay(300);
                    }

                    await gameData.SaveAsync();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        statusBlock.Text = "All downloads completed successfully!";
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = 100;
                        closeButton.IsEnabled = true;
                        DownloadSuccess = true;
                    });
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        statusBlock.Text = "An error occurred during download.";
                        progressBar.IsIndeterminate = false;
                        closeButton.IsEnabled = true;
                    });
                }
            });
        }

        #endregion
    }
}