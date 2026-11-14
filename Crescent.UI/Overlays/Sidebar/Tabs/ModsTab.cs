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

        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;
        public List<RenderedMod> renderedMods = new();
        public Dictionary<ModData, RenderedMod> renderedModCache = new();
        public TextBlock? titleBlock;
        public StackPanel? listPanel;

        public string selectedSortOption = "A - Z";
        public string selectedFilterOption = "All";
        public bool isSetup;
        public CancellationTokenSource? searchCancellationToken;
        public Action? renderMods;
        public int Order => 2;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Mods",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            if (this.pluginData != pluginData)
            {
                renderedModCache.Clear();
                isSetup = false;
            }

            this.pluginData = pluginData;
            this.sidebarOverlay = sidebarOverlay;
            Setup();
        }

        public void OnClick()
        {
            if (!isSetup) Setup();
            else RenderMods();
        }

        public void Refresh()
        {
            renderedModCache.Clear();
            isSetup = false;
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
            if (isSetup) return;
            isSetup = true;

            Children.Clear();
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
                Text = $"{pluginData!.SelectedProfile.name} ({pluginData.ModCount} Mod{pluginData.ModCount.WithPlural()})",
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
            if (string.IsNullOrEmpty(pluginData.StagingPath)) return;
            if (!Directory.Exists(pluginData.StagingPath))
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

            listPanel = new()
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

            List<string> filters = pluginData!.GetModFilters();
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

        public static HashSet<string> GetArchiveFileNames(PluginData pluginData)
        {
            HashSet<string> archiveFiles = new(StringComparer.OrdinalIgnoreCase);
            string archiveDirectory = Path.Combine(pluginData.StagingPath, PluginData.Archive);
            if (!Directory.Exists(archiveDirectory)) return archiveFiles;

            string[] files = Directory.GetFiles(archiveDirectory);
            for (int index = 0; index < files.Length; index++)
                archiveFiles.Add(Path.GetFileName(files[index]));

            return archiveFiles;
        }

        public static string GetModId(HashSet<string> archiveFiles, string diskName)
        {
            if (ModPathUtils.HasArchiveExtension(diskName)) return diskName;

            for (int index = 0; index < ModPathUtils.ArchiveExtensions.Length; index++)
            {
                string candidateName = diskName + ModPathUtils.ArchiveExtensions[index];
                if (archiveFiles.Contains(candidateName)) return candidateName;
            }

            return diskName.Contains('.') ? diskName + ".zip" : diskName;
        }

        public RenderedMod GetOrCreateRenderedMod(ModData modData)
        {
            if (renderedModCache.TryGetValue(modData, out RenderedMod? existing))
            {
                existing.RefreshDeployedState();
                return existing;
            }

            RenderedMod created = new(pluginData!, modData, this);
            renderedModCache[modData] = created;
            return created;
        }

        public List<RenderedMod> Sort(List<RenderedMod> items, string sortMode)
        {
            return sortMode switch
            {
                "Z - A" => items.OrderByDescending(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Managed First" => items.OrderBy(item => item.modData.isManual).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Manual First" => items.OrderByDescending(item => item.modData.isManual).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Enabled First" => items.OrderByDescending(item => pluginData!.SelectedProfile.IsModEnabled(item.modData)).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Deployed First" => items.OrderByDescending(item => item.modData.isDeployed).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Update Available" => items.OrderByDescending(item => item.modData.UpdateAvailable).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => items.OrderBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        public void SetupModListRenderer(TextBox searchBox, ComboBox filterComboBox, ComboBox sortComboBox, StackPanel targetListPanel)
        {
            renderMods = () =>
            {
                searchCancellationToken?.Cancel();
                searchCancellationToken = new();
                CancellationToken token = searchCancellationToken.Token;

                string filterText = searchBox.Text != null ? searchBox.Text.ToLower() : string.Empty;
                selectedFilterOption = filterComboBox.SelectedItem as string ?? "All";
                selectedSortOption = sortComboBox.SelectedItem as string ?? "A - Z";

                RenderMods(targetListPanel, filterText);

                _ = Task.Run(async () =>
                {
                    bool listChanged = SyncMods(token);
                    if (token.IsCancellationRequested) return;

                    if (listChanged)
                        await Dispatcher.UIThread.InvokeAsync(() => RenderMods(targetListPanel, filterText));

                    await CheckUpdatesAsync(token);
                }, token);
            };

            filterComboBox.SelectionChanged += (_, _) =>
            {
                selectedFilterOption = filterComboBox.SelectedItem as string ?? "All";
                string filterText = searchBox.Text != null ? searchBox.Text.ToLower() : string.Empty;
                RenderMods(targetListPanel, filterText);
            };

            sortComboBox.SelectionChanged += (_, _) =>
            {
                selectedSortOption = sortComboBox.SelectedItem as string ?? "A - Z";
                string filterText = searchBox.Text != null ? searchBox.Text.ToLower() : string.Empty;
                RenderMods(targetListPanel, filterText);
            };

            searchBox.TextChanged += async (_, _) =>
            {
                searchCancellationToken?.Cancel();
                searchCancellationToken = new();
                CancellationToken token = searchCancellationToken.Token;
                try
                {
                    await Task.Delay(120, token);
                    if (!token.IsCancellationRequested)
                    {
                        string filterText = searchBox.Text != null ? searchBox.Text.ToLower() : string.Empty;
                        RenderMods(targetListPanel, filterText);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            };

            renderMods?.Invoke();
        }

        public void RenderMods(StackPanel targetListPanel, string filterText)
        {
            if (pluginData == null) return;

            renderedMods.Clear();
            targetListPanel.Children.Clear();

            bool isDefaultProfile = string.Equals(pluginData.selectedProfileName, PluginData.Default, StringComparison.OrdinalIgnoreCase);
            List<RenderedMod> matchingMods = new();

            if (isDefaultProfile)
            {
                for (int index = 0; index < pluginData.installedMods.Count; index++)
                {
                    ModData modData = pluginData.installedMods[index];
                    if (!pluginData.MatchesFilter(modData, selectedFilterOption)) continue;

                    RenderedMod renderedMod = GetOrCreateRenderedMod(modData);
                    if (!string.IsNullOrEmpty(filterText) && !renderedMod.cleanName.Contains(filterText, StringComparison.OrdinalIgnoreCase) && !modData.modName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    matchingMods.Add(renderedMod);
                }
            }
            else
            {
                ProfileData profile = pluginData.SelectedProfile;
                for (int index = 0; index < profile.mods.Count; index++)
                {
                    ProfileModData profileMod = profile.mods[index];
                    string strippedProfileMod = ModPathUtils.StripArchiveExtension(profileMod.modName);
                    ModData? modData = pluginData.installedMods.Find(mod => string.Equals(mod.modName, profileMod.modName, StringComparison.OrdinalIgnoreCase) || string.Equals(ModPathUtils.StripArchiveExtension(mod.modName), strippedProfileMod, StringComparison.OrdinalIgnoreCase));
                    if (modData == null) continue;
                    if (!pluginData.MatchesFilter(modData, selectedFilterOption)) continue;

                    RenderedMod renderedMod = GetOrCreateRenderedMod(modData);
                    if (!string.IsNullOrEmpty(filterText) && !renderedMod.cleanName.Contains(filterText, StringComparison.OrdinalIgnoreCase) && !modData.modName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    matchingMods.Add(renderedMod);
                }
            }

            List<RenderedMod> sortedRenderedMods = Sort(matchingMods, selectedSortOption);

            if (titleBlock != null)
                titleBlock.Text = $"{pluginData.SelectedProfile.name} ({pluginData.ModCount} Mod{pluginData.ModCount.WithPlural()})";

            for (int index = 0; index < sortedRenderedMods.Count; index++)
            {
                RenderedMod renderedMod = sortedRenderedMods[index];
                renderedMods.Add(renderedMod);
                targetListPanel.Children.Add(renderedMod);
            }
        }

        public bool SyncMods(CancellationToken token)
        {
            if (pluginData == null) return false;

            List<string> modPaths = ModPathUtils.GetModPaths(pluginData);
            HashSet<string> archiveFiles = GetArchiveFileNames(pluginData);
            HashSet<string> validModNames = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < modPaths.Count; index++)
            {
                string fileName = Path.GetFileName(modPaths[index]);
                validModNames.Add(fileName);
                validModNames.Add(ModPathUtils.StripArchiveExtension(fileName));
            }

            int removedCount = pluginData.installedMods.RemoveAll(mod =>
            {
                if (validModNames.Contains(mod.modName)) return false;
                string unzippedName = ModPathUtils.StripArchiveExtension(mod.modName);
                return !validModNames.Contains(unzippedName);
            });

            bool listChanged = removedCount > 0;

            for (int index = 0; index < modPaths.Count; index++)
            {
                if (token.IsCancellationRequested) return false;

                string filePath = modPaths[index];
                string diskName = Path.GetFileName(filePath);
                string strippedName = ModPathUtils.StripArchiveExtension(diskName);

                ModData? modData = pluginData.installedMods.Find(mod => string.Equals(mod.modName, diskName, StringComparison.OrdinalIgnoreCase) || string.Equals(ModPathUtils.StripArchiveExtension(mod.modName), strippedName, StringComparison.OrdinalIgnoreCase));
                string safeModName = GetModId(archiveFiles, diskName);

                if (modData == null)
                {
                    modData = new(pluginData, safeModName, filePath) { modName = safeModName };
                    pluginData.installedMods.Add(modData);
                    pluginData.OnModLoaded(modData);
                    listChanged = true;
                }
                else
                {
                    string previousModName = modData.modName;
                    modData.filePath = filePath;
                    if (!ModPathUtils.HasArchiveExtension(modData.modName))
                    {
                        modData.modName = safeModName;
                        if (!string.Equals(previousModName, safeModName, StringComparison.OrdinalIgnoreCase))
                            pluginData.ReplaceModId(previousModName, safeModName, modData);
                    }

                    pluginData.OnModLoaded(modData);
                }

                ModInteractionUtils.SyncModDeploymentState(pluginData, modData);
            }

            _ = pluginData.SaveAsync();
            return listChanged;
        }

        public async Task CheckUpdatesAsync(CancellationToken token)
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
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (renderedModCache.TryGetValue(mod, out RenderedMod? renderedMod))
                            renderedMod.RefreshMod();
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }

                await Task.Delay(300, token);
            }

            await pluginData.SaveAsync();
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
                if (string.IsNullOrEmpty(pluginData!.StagingPath)) return;
                if (!Directory.Exists(pluginData.StagingPath)) return;
                Process.Start(new ProcessStartInfo(pluginData.StagingPath)
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
                if (string.IsNullOrEmpty(pluginData!.DeploymentPath)) return;
                if (!Directory.Exists(pluginData.DeploymentPath)) return;
                Process.Start(new ProcessStartInfo(pluginData.DeploymentPath)
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
                    int totalMods = pluginData!.installedMods.Count;
                    int checkedCount = 0;

                    foreach (ModData modData in pluginData.installedMods)
                    {
                        checkedCount++;
                        ToastManager.ShowProgress("Checking Updates", $"{modData.DisplayName} ({checkedCount}/{totalMods})", $"Current: v{modData.version}", (double)checkedCount / Math.Max(1, totalMods) * 100);

                        modData.HasCheckedForUpdate = false;
                        await modData.CheckForUpdateAsync(pluginData.API.Name);
                        if (renderedModCache.TryGetValue(modData, out RenderedMod? renderedMod))
                            renderedMod.RefreshMod();

                        await Task.Delay(350);
                    }

                    await pluginData.SaveAsync();
                    RenderMods();
                    int updatesFound = pluginData.installedMods.Count(mod => mod.UpdateAvailable);
                    ToastManager.CompleteProgress("Update Check Complete", updatesFound > 0 ? $"{updatesFound} mod update{(updatesFound == 1 ? "" : "s")} available" : "All installed mods are up to date", updatesFound > 0 ? ToastType.Warning : ToastType.Success, 4500, $"Checked {totalMods} mod{totalMods.WithPlural()}");
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                    ToastManager.CompleteProgress("Update Check Failed", "An error occurred while checking for updates :(", ToastType.Error);
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

                ModDownloadDialog downloadDialog = new(pluginData!, url);
                await downloadDialog.ShowDialog(window);

                if (downloadDialog.DownloadSuccess) Refresh();
            };

            return downloadLinkButton;
        }

        #endregion
    }
}