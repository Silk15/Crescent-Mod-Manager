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
using Crescent.Utilities;
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

        public ModDownloadDialog(GameData gameData, string modPageUrl)
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

            StartDownload(gameData, modPageUrl);
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
                            statusBlock.Text = "Invalid Nexus Mods Url :(";
                            progressBar.IsIndeterminate = false;
                        });
                        return;
                    }

                    string gameDomain = match.Groups[1].Value;
                    string nexusId = match.Groups[2].Value;

                    JObject? modDetails = await NexusUtils.GetModDetailsJsonAsync(gameDomain, nexusId);
                    string modName = modDetails?["name"]?.ToString() ?? nexusId;

                    await Dispatcher.UIThread.InvokeAsync(() => statusBlock.Text = $"Fetching mod data for: {modName}");
                    JObject? modFiles = await NexusUtils.GetModFilesJsonAsync(gameDomain, nexusId);
                    if (modFiles == null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock.Text = "Failed to retrieve mod files.";
                            progressBar.IsIndeterminate = false;
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
                        string currentModName = currentNexusId;
                        if (string.Equals(currentNexusId, nexusId, StringComparison.OrdinalIgnoreCase)) currentModName = modName;
                        else
                        {
                            JObject? reqDetails = await NexusUtils.GetModDetailsJsonAsync(gameDomain, currentNexusId);
                            currentModName = reqDetails?["name"]?.ToString() ?? currentNexusId;
                        }

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock.Text = $"Downloading {modName} ({queueIndex + 1}/{downloadQueueIds.Count})";
                            detailsPanel!.Children.Add(new TextBlock
                            {
                                Text = $"• {currentModName}",
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
                    });
                }
            });
        }
    }
}