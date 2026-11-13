using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Utilities;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class ModsTab : StackPanel, ISidebarTab
    {
        public static readonly List<string> PrivilegedFolders = new()
        {
            "Profiles",
            "Archive",
            "Manual"
        };

        public static readonly List<string> SortOptions = new()
        {
            "A - Z",
            "Z - A",
            "Managed First",
            "Manual First",
            "Enabled First",
            "Deployed First",
            "Update Available"
        };

        public PluginData? gameData;
        public SidebarOverlay? sidebarOverlay;
        public List<RenderedMod> renderedMods = new();
        public TextBlock? titleBlock;

        private string selectedSortOption = "A - Z";
        private string selectedFilterOption = "All";
        private CancellationTokenSource? searchCancellationToken;
        private Action? renderMods;
        public int Order => 2;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Mods",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            this.gameData = pluginData;
            this.sidebarOverlay = sidebarOverlay;
            Setup();
        }

        public void OnClick() => Refresh();

        public void Refresh()
        {
            Children.Clear();
            Setup();
        }

        public void RenderMods() => renderMods?.Invoke();

        public void RefreshDeployedStates()
        {
            for (int index = 0; index < renderedMods.Count; index++)
                renderedMods[index].RefreshDeployedState();
        }

        public void Setup()
        {
            renderedMods.Clear();
            Margin = new Thickness(30, 15, 30, 30);
            Spacing = 15;
            Background = Brushes.Transparent;
            StackPanel mainPanel = new()
            {
                Height = ISidebarTab.TopBarHeight,
                Spacing = 10
            };

            titleBlock = new()
            {
                Text = $"{gameData!.SelectedProfile.name} ({gameData.ModCount} Mod{gameData.ModCount.WithPlural()})",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            mainPanel.Children.Add(titleBlock);
            Grid interactionsGrid = new()
            {
                ColumnSpacing = 10
            };

            interactionsGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            interactionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid filterControlGrid = GetFilterGrid(out TextBox searchBox, out ComboBox filterComboBox, out ComboBox sortComboBox);
            interactionsGrid.Children.Add(filterControlGrid);
            Grid.SetColumn(filterControlGrid, 0);

            StackPanel actionButtonPanel = GetModsActionButtons();
            interactionsGrid.Children.Add(actionButtonPanel);
            Grid.SetColumn(actionButtonPanel, 1);

            mainPanel.Children.Add(interactionsGrid);
            mainPanel.Children.Add(new Separator
            {
                Height = 1,
                Background = Colors.Separator,
                VerticalAlignment = VerticalAlignment.Bottom
            });

            Children.Add(mainPanel);
            if (string.IsNullOrEmpty(gameData.StagingPath)) return;
            if (!Directory.Exists(gameData.StagingPath))
            {
                TextBlock warningText = new()
                {
                    Text = "Staging directory is not configured or does not exist. Please set it in the settings tab",
                    Foreground = Brushes.IndianRed,
                    FontWeight = FontWeight.Bold
                };

                Children.Add(warningText);
                return;
            }

            StackPanel listPanel = new()
            {
                Spacing = 8
            };

            SetupModListRenderer(searchBox, filterComboBox, sortComboBox, listPanel);
            Children.Add(listPanel);
        }

        #region Filtering, Sorting & List Rendering

        public Grid GetFilterGrid(out TextBox searchBox, out ComboBox filterComboBox, out ComboBox sortComboBox)
        {
            Grid searchGrid = new();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(260, GridUnitType.Pixel));
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(135, GridUnitType.Pixel));
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(155, GridUnitType.Pixel));
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            searchBox = new()
            {
                PlaceholderText = "Search installed mods...",
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Grid.SetColumn(searchBox, 0);
            searchGrid.Children.Add(searchBox);

            filterComboBox = new()
            {
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 10, 0)
            };

            List<string> filters = gameData!.GetModFilters();
            filterComboBox.ItemsSource = filters;
            int filterIndex = filters.IndexOf(selectedFilterOption);
            filterComboBox.SelectedIndex = filterIndex >= 0 ? filterIndex : 0;
            Grid.SetColumn(filterComboBox, 1);
            searchGrid.Children.Add(filterComboBox);

            sortComboBox = new()
            {
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            sortComboBox.ItemsSource = SortOptions;
            int sortIndex = SortOptions.IndexOf(selectedSortOption);
            sortComboBox.SelectedIndex = sortIndex >= 0 ? sortIndex : 0;
            Grid.SetColumn(sortComboBox, 2);
            searchGrid.Children.Add(sortComboBox);

            return searchGrid;
        }

        private static string EnsureSafeModIdentifier(PluginData pluginData, string diskName)
        {
            if (ModPathUtils.HasArchiveExtension(diskName)) return diskName;

            string archiveDirectory = Path.Combine(pluginData.StagingPath, PluginData.Archive);
            if (Directory.Exists(archiveDirectory))
            {
                for (int index = 0; index < ModPathUtils.ArchiveExtensions.Length; index++)
                {
                    string candidateName = diskName + ModPathUtils.ArchiveExtensions[index];
                    if (File.Exists(Path.Combine(archiveDirectory, candidateName)))
                        return candidateName;
                }
            }

            return diskName.Contains('.') ? diskName + ".zip" : diskName;
        }

        private List<RenderedMod> ApplySortOrder(List<RenderedMod> items, string sortMode)
        {
            return sortMode switch
            {
                "Z - A" => items
                    .OrderByDescending(item => item.modData.DisplayName.Cleaned(), StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                "Managed First" => items
                    .OrderBy(item => item.modData.isManual)
                    .ThenBy(item => item.modData.DisplayName.Cleaned(), StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                "Manual First" => items
                    .OrderByDescending(item => item.modData.isManual)
                    .ThenBy(item => item.modData.DisplayName.Cleaned(), StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                "Enabled First" => items
                    .OrderByDescending(item => gameData!.SelectedProfile.IsModEnabled(item.modData))
                    .ThenBy(item => item.modData.DisplayName.Cleaned(), StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                "Deployed First" => items
                    .OrderByDescending(item => item.modData.isDeployed)
                    .ThenBy(item => item.modData.DisplayName.Cleaned(), StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                "Update Available" => items
                    .OrderByDescending(item => item.modData.UpdateAvailable)
                    .ThenBy(item => item.modData.DisplayName.Cleaned(), StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                _ => items
                    .OrderBy(item => item.modData.DisplayName.Cleaned(), StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        public void SetupModListRenderer(TextBox searchBox, ComboBox filterComboBox, ComboBox sortComboBox, StackPanel listPanel)
        {
            renderMods = () =>
            {
                searchCancellationToken?.Cancel();
                searchCancellationToken = new();
                CancellationToken token = searchCancellationToken.Token;

                renderedMods.Clear();
                listPanel.Children.Clear();
                List<string> modPaths = ModPathUtils.GetModPaths(gameData!);

                HashSet<string> validModNames = new(StringComparer.OrdinalIgnoreCase);
                foreach (string filePath in modPaths)
                {
                    string fileName = Path.GetFileName(filePath);
                    validModNames.Add(fileName);
                    validModNames.Add(ModPathUtils.StripArchiveExtension(fileName));
                }

                gameData!.installedMods.RemoveAll(mod =>
                {
                    if (validModNames.Contains(mod.modName)) return false;
                    string unzippedName = ModPathUtils.StripArchiveExtension(mod.modName);
                    if (validModNames.Contains(unzippedName)) return false;

                    return true;
                });

                string filterText = searchBox.Text != null ? searchBox.Text.ToLower() : string.Empty;
                selectedFilterOption = filterComboBox.SelectedItem as string ?? "All";
                selectedSortOption = sortComboBox.SelectedItem as string ?? "A - Z";
                bool isDefaultProfile = string.Equals(gameData.selectedProfileName, PluginData.Default, StringComparison.OrdinalIgnoreCase);

                List<ModData> modsToCheck = new();
                List<RenderedMod> tempRenderedMods = new();

                if (isDefaultProfile)
                {
                    foreach (string filePath in modPaths)
                    {
                        if (token.IsCancellationRequested) return;

                        string diskName = Path.GetFileName(filePath);
                        string strippedName = ModPathUtils.StripArchiveExtension(diskName);
                        ModData? modData = gameData.installedMods.Find(mod =>
                            string.Equals(mod.modName, diskName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ModPathUtils.StripArchiveExtension(mod.modName), strippedName, StringComparison.OrdinalIgnoreCase));

                        string safeModName = EnsureSafeModIdentifier(gameData, diskName);

                        if (modData == null)
                        {
                            modData = new(gameData, safeModName, filePath) { modName = safeModName };
                            gameData.installedMods.Add(modData);
                        }
                        else
                        {
                            string previousModName = modData.modName;
                            modData.filePath = filePath;
                            if (!ModPathUtils.HasArchiveExtension(modData.modName))
                            {
                                modData.modName = safeModName;
                                if (!string.Equals(previousModName, safeModName, StringComparison.OrdinalIgnoreCase))
                                    gameData.ReplaceModId(previousModName, safeModName, modData);
                            }
                        }

                        ModInteractionUtils.SyncModDeploymentState(gameData, modData);
                        if (!modData.HasCheckedForUpdate) modsToCheck.Add(modData);

                        gameData.OnModLoaded(modData);

                        if (!gameData.MatchesFilter(modData, selectedFilterOption)) continue;
                        if (!string.IsNullOrEmpty(filterText) && !modData.DisplayName.Cleaned().ToLower().Contains(filterText) && !diskName.ToLower().Contains(filterText) && !strippedName.ToLower().Contains(filterText)) continue;

                        RenderedMod renderedMod = new(gameData, modData, this);
                        tempRenderedMods.Add(renderedMod);
                    }
                }
                else
                {
                    ProfileData profile = gameData.SelectedProfile;
                    for (int index = 0; index < profile.mods.Count; index++)
                    {
                        if (token.IsCancellationRequested) return;

                        ProfileModData profileMod = profile.mods[index];
                        string strippedProfileMod = ModPathUtils.StripArchiveExtension(profileMod.modName);
                        ModData? modData = gameData.installedMods.Find(mod =>
                            string.Equals(mod.modName, profileMod.modName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ModPathUtils.StripArchiveExtension(mod.modName), strippedProfileMod, StringComparison.OrdinalIgnoreCase));

                        if (modData == null) continue;

                        string? matchedDiskPath = modPaths.Find(path =>
                            string.Equals(Path.GetFileName(path), modData.modName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ModPathUtils.StripArchiveExtension(Path.GetFileName(path)), strippedProfileMod, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(matchedDiskPath))
                        {
                            modData.filePath = matchedDiskPath;
                            string safeModName = EnsureSafeModIdentifier(gameData, Path.GetFileName(matchedDiskPath));
                            if (!ModPathUtils.HasArchiveExtension(modData.modName))
                                modData.modName = safeModName;
                            profileMod.modName = modData.modName;
                        }

                        string filePath = modData.filePath;
                        if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath))) continue;

                        ModInteractionUtils.SyncModDeploymentState(gameData, modData);

                        string modName = modData.modName;
                        string strippedName = ModPathUtils.StripArchiveExtension(modName);

                        if (!modData.HasCheckedForUpdate) modsToCheck.Add(modData);

                        if (!gameData.MatchesFilter(modData, selectedFilterOption)) continue;
                        if (!string.IsNullOrEmpty(filterText) && !modData.DisplayName.Cleaned().ToLower().Contains(filterText) && !modName.ToLower().Contains(filterText) && !strippedName.ToLower().Contains(filterText)) continue;

                        gameData.OnModLoaded(modData);
                        RenderedMod renderedMod = new(gameData, modData, this);
                        tempRenderedMods.Add(renderedMod);
                    }
                }

                List<RenderedMod> sortedRenderedMods = ApplySortOrder(tempRenderedMods, selectedSortOption);

                _ = gameData.SaveAsync();

                if (titleBlock != null)
                    titleBlock.Text = $"{gameData.SelectedProfile.name} ({gameData.ModCount} Mod{gameData.ModCount.WithPlural()})";

                foreach (RenderedMod renderedMod in sortedRenderedMods)
                {
                    renderedMods.Add(renderedMod);
                    listPanel.Children.Add(renderedMod);
                }

                if (modsToCheck.Count > 0)
                    _ = Task.Run(async () =>
                    {
                        for (int index = 0; index < modsToCheck.Count; index++)
                        {
                            if (token.IsCancellationRequested) return;
                            ModData mod = modsToCheck[index];
                            try
                            {
                                await mod.CheckForUpdateAsync(gameData.API.Name);
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    RenderedMod? renderedMod = renderedMods.Find(item => item.modData == mod);
                                    renderedMod?.RefreshMod();
                                });
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex.ToString());
                            }

                            await Task.Delay(300, token);
                        }

                        await gameData.SaveAsync();
                    });
            };

            filterComboBox.SelectionChanged += (_, _) => renderMods?.Invoke();
            sortComboBox.SelectionChanged += (_, _) => renderMods?.Invoke();
            searchBox.TextChanged += async (_, _) =>
            {
                searchCancellationToken?.Cancel();
                searchCancellationToken = new();
                CancellationToken token = searchCancellationToken.Token;
                try
                {
                    await Task.Delay(200, token);
                    if (!token.IsCancellationRequested) renderMods?.Invoke();
                }
                catch (OperationCanceledException) { }
            };

            renderMods?.Invoke();
        }

        #endregion

        #region Action Buttons

        public StackPanel GetModsActionButtons()
        {
            StackPanel actionButtonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            actionButtonPanel.Children.Add(GetStagingButton());
            actionButtonPanel.Children.Add(GetTargetButton());
            actionButtonPanel.Children.Add(new Separator
            {
                Width = 1,
                Height = 35,
                Background = Colors.Separator,
                Padding = new(5, 0, 5, 0)
            });

            actionButtonPanel.Children.Add(GetCheckButton());
            actionButtonPanel.Children.Add(GetDownloadButton());
            return actionButtonPanel;
        }

        public CrescentButton GetStagingButton()
        {
            CrescentButton openStagingButton = new(ButtonStyle.Translucent)
            {
                Content = "Staging Folder",
                Width = 135
            };

            openStagingButton.onClick += () =>
            {
                if (string.IsNullOrEmpty(gameData!.StagingPath)) return;
                if (!Directory.Exists(gameData.StagingPath)) return;
                Process.Start(new ProcessStartInfo(gameData.StagingPath)
                {
                    UseShellExecute = true
                });
            };

            return openStagingButton;
        }

        public CrescentButton GetTargetButton()
        {
            CrescentButton openDeploymentButton = new(ButtonStyle.Translucent)
            {
                Content = "Target Folder",
                Width = 120
            };

            openDeploymentButton.onClick += () =>
            {
                if (string.IsNullOrEmpty(gameData!.DeploymentPath)) return;
                if (!Directory.Exists(gameData.DeploymentPath)) return;
                Process.Start(new ProcessStartInfo(gameData.DeploymentPath)
                {
                    UseShellExecute = true
                });
            };

            return openDeploymentButton;
        }

        public CrescentButton GetCheckButton()
        {
            CrescentButton checkUpdatesButton = new(ButtonStyle.Translucent)
            {
                Content = "Check Updates",
                Width = 120
            };

            checkUpdatesButton.onClick += async () =>
            {
                try
                {
                    checkUpdatesButton.IsEnabled = false;
                    int totalMods = gameData!.installedMods.Count;
                    int checkedCount = 0;

                    foreach (ModData modData in gameData.installedMods)
                    {
                        checkedCount++;
                        ToastManager.ShowProgress("Checking Updates", $"{modData.DisplayName} ({checkedCount}/{totalMods})", $"Current: v{modData.version}", (double)checkedCount / Math.Max(1, totalMods) * 100);

                        modData.HasCheckedForUpdate = false;
                        await modData.CheckForUpdateAsync(gameData.API.Name);
                        await Task.Delay(350);
                    }

                    await gameData.SaveAsync();
                    Refresh();
                    int updatesFound = gameData.installedMods.Count(mod => mod.UpdateAvailable);
                    ToastManager.CompleteProgress(
                        "Update Check Complete",
                        updatesFound > 0 ? $"{updatesFound} mod update{(updatesFound == 1 ? "" : "s")} available" : "All installed mods are up to date",
                        updatesFound > 0 ? ToastType.Warning : ToastType.Success,
                        4500,
                        $"Checked {totalMods} mod{(totalMods == 1 ? "" : "s")}");
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                    ToastManager.CompleteProgress("Update Check Failed", "An error occurred while checking for updates.", ToastType.Error);
                }
                finally
                {
                    checkUpdatesButton.IsEnabled = true;
                    checkUpdatesButton.Content = "Check Updates";
                }
            };

            return checkUpdatesButton;
        }

        public CrescentButton GetDownloadButton()
        {
            CrescentButton downloadLinkButton = new(ButtonStyle.Translucent)
            {
                Content = "Download Mod",
                Width = 130
            };

            downloadLinkButton.onClick += async () =>
            {
                Window? window = TopLevel.GetTopLevel(this) as Window;
                if (window == null) return;

                ConfirmationDialog confirmationDialog = new("Download Mod", "Enter the Url");
                await confirmationDialog.ShowDialog(window);

                string url = confirmationDialog.InputText;
                if (string.IsNullOrEmpty(url)) return;

                ModDownloadDialog downloadDialog = new(gameData!, url);
                await downloadDialog.ShowDialog(window);

                if (downloadDialog.DownloadSuccess) Refresh();
            };

            return downloadLinkButton;
        }

        #endregion
    }
}