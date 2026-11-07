using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;
using Crescent.APIs.NexusMods;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Crescent.Utilities;

namespace Crescent.UI
{
    public class ProfileImportDialog : Dialog
    {
        private TextBlock? statusBlock;
        private ProgressBar? progressBar;
        private StackPanel? detailsPanel;
        private CrescentButton? closeButton;

        public bool ImportSuccess { get; private set; }

        public ProfileImportDialog(PluginData pluginData, ExportedProfile exportedProfile, ProfileData targetProfile)
        {
            Title = $"Importing Profile";
            SizeToContent = SizeToContent.Height;
            Width = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Colors.MainGradient;
            CanResize = false;
            Icon = new WindowIcon(AssetLoader.Open(new Uri(MainWindow.IconUri)));

            StackPanel mainPanel = new()
            {
                Margin = new Thickness(20),
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };

            statusBlock = new()
            {
                Text = "Checking missing mods...",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
            };

            progressBar = new()
            {
                Foreground = Brushes.CornflowerBlue,
                Height = 30,
                IsIndeterminate = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new(0, 0, 10, 0)
            };
            
            detailsPanel = new()
            {
                Spacing = 6
            };

            ScrollViewer detailsScrollViewer = new()
            {
                Height = 220,
                Content = detailsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            closeButton = new CrescentButton(ButtonStyle.Translucent)
            {
                Content = "Close",
                Width = 90,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(10, 0, 0, 0)
            };
            closeButton.onClick += () => Close();
            
            Border border = new()
            {
                Background = Colors.MainPanelEnd,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };

            border.Child = detailsScrollViewer;
            
            mainPanel.Children.Add(statusBlock);
            mainPanel.Children.Add(border);
            Grid grid = new();
            
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            
            grid.Children.Add(progressBar);
            Grid.SetColumn(progressBar, 0);
            
            grid.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 1);
            
            mainPanel.Children.Add(grid);
            
            Content = mainPanel;
            StartImportQueue(pluginData, exportedProfile, targetProfile);
        }

        private void StartImportQueue(PluginData pluginData, ExportedProfile exportedProfile, ProfileData profile)
        {
            Task.Run(async () =>
            {
                try
                {
                    List<ExportedProfile.ExportedMod> missingMods = new();
                    foreach (ExportedProfile.ExportedMod exportedMod in exportedProfile.mods)
                    {
                        bool found = false;
                        foreach (ModData installedMod in pluginData.installedMods)
                        {
                            if (!string.IsNullOrEmpty(installedMod.url.nexusUrl) && string.Equals(installedMod.url.nexusUrl, exportedMod.url, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                profile.SetModEnabled(installedMod, exportedMod.isEnabled);
                                break;
                            }

                            if (string.Equals(installedMod.DisplayName, exportedMod.displayName, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                profile.SetModEnabled(installedMod, exportedMod.isEnabled);
                                break;
                            }
                        }

                        if (!found) missingMods.Add(exportedMod);
                    }

                    await pluginData.SaveAsync();

                    if (missingMods.Count == 0)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock!.Text = "Profile imported successfully! All mods already installed.";
                            progressBar!.IsIndeterminate = false;
                            progressBar.Value = 100;
                            ImportSuccess = true;
                        });
                        return;
                    }

                    if (string.IsNullOrEmpty(pluginData.StagingPath) || !Directory.Exists(pluginData.StagingPath))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock!.Text = "Staging path is not configured or does not exist. Cannot download missing mods.";
                            progressBar!.IsIndeterminate = false;
                        });
                        return;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        progressBar!.IsIndeterminate = false;
                        progressBar.Maximum = missingMods.Count;
                        progressBar.Value = 0;
                    });
                    
                    for (int i = 0; i < missingMods.Count; i++)
                    {
                        ExportedProfile.ExportedMod mod = missingMods[i];
                        int queueIndex = i + 1;
                        
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            statusBlock!.Text = $"Downloading {exportedProfile.profileName} ({queueIndex}/{missingMods.Count})";
                            detailsPanel!.Children.Add(new TextBlock
                            {
                                Text = $"• {mod.displayName}",
                                Foreground = Brushes.LightGray,
                                FontSize = 12
                            });
                        });

                        Match match = Regex.Match(mod.url, @"nexusmods\.com/([^/]+)/mods/(\d+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string gameDomain = match.Groups[1].Value;
                            string nexusId = match.Groups[2].Value;

                            JObject? modFiles = await NexusUtils.GetModFilesJsonAsync(gameDomain, nexusId);
                            if (modFiles != null)
                            {
                                JArray? filesArray = modFiles["files"] as JArray;
                                if (filesArray != null && filesArray.Count > 0)
                                {
                                    JToken? targetFile = null;
                                    foreach (JToken fileToken in filesArray)
                                    {
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

                                    if (!string.IsNullOrEmpty(fileId))
                                    {
                                        string downloadUrl = await NexusUtils.GetDownloadUrlAsync(gameDomain, nexusId, fileId);
                                        if (!string.IsNullOrEmpty(downloadUrl))
                                        {
                                            string? savedPath = await NexusUtils.DownloadFileAsync(downloadUrl, pluginData.StagingPath, fileName);
                                            if (!string.IsNullOrEmpty(savedPath))
                                            {
                                                ModData newModData = new(pluginData, Path.GetFileName(savedPath), savedPath);
                                                pluginData.installedMods.Add(newModData);
                                                pluginData.OnModLoaded(newModData);
                                                profile.SetModEnabled(newModData, mod.isEnabled);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        await pluginData.SaveAsync();
                        await Dispatcher.UIThread.InvokeAsync(() => { progressBar!.Value = queueIndex; });
                        await Task.Delay(300);
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        statusBlock!.Text = "Profile imported successfully!";
                        progressBar!.Value = progressBar.Maximum;
                        ImportSuccess = true;
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        statusBlock!.Text = "An error occurred importing this profile!";
                        progressBar!.IsIndeterminate = false;
                    });
                }
            });
        }
    }
}