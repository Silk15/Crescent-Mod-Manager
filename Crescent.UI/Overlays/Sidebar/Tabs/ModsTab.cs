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
        
        public GameData? gameData;
        public SidebarOverlay? sidebarOverlay;
        public List<RenderedMod> renderedMods = new();
        
        private CancellationTokenSource? searchCancellationToken;
        private Action? renderMods;

        public Tab Tab => Tab.Mods;
        public int Order => 2;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Mods",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, GameData gameData)
        {
            this.gameData = gameData;
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

            TextBlock titleBlock = new()
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
            
            interactionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            interactionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid filterControlGrid = GetFilterGrid(out TextBox searchBox, out ComboBox filterComboBox);
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

            SetupModListRenderer(searchBox, filterComboBox, listPanel);
            Children.Add(listPanel);
        }

        #region Filtering & List Rendering

        public Grid GetFilterGrid(out TextBox searchBox, out ComboBox filterComboBox)
        {
            Grid searchGrid = new();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(320, GridUnitType.Pixel));
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(150, GridUnitType.Pixel));
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
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            List<string> filters = gameData!.GetModFilters();
            filterComboBox.ItemsSource = filters;
            filterComboBox.SelectedIndex = 0;
            Grid.SetColumn(filterComboBox, 1);
            searchGrid.Children.Add(filterComboBox);

            return searchGrid;
        }

        public void SetupModListRenderer(TextBox searchBox, ComboBox filterComboBox, StackPanel listPanel)
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
                foreach (string filePath in modPaths) validModNames.Add(Path.GetFileName(filePath));

                gameData!.installedMods.RemoveAll(mod =>
                {
                    if (validModNames.Contains(mod.modName)) return false;
                    string unzippedName = Path.GetFileNameWithoutExtension(mod.modName);
                    if (validModNames.Contains(unzippedName))
                    {
                        mod.modName = unzippedName;
                        return false;
                    }

                    return true;
                });

                string filterText = searchBox.Text != null ? searchBox.Text.ToLower() : "";
                string selectedFilter = filterComboBox.SelectedItem as string ?? "All";
                bool isDefaultProfile = string.Equals(gameData.selectedProfileName, GameData.Default, StringComparison.OrdinalIgnoreCase);

                List<ModData> modsToCheck = new();
                List<RenderedMod> tempRenderedMods = new();

                if (isDefaultProfile)
                {
                    foreach (string filePath in modPaths)
                    {
                        if (token.IsCancellationRequested) return;

                        string modName = Path.GetFileName(filePath);
                        string name = Directory.Exists(filePath) ? modName : Path.GetFileNameWithoutExtension(filePath);
                        ModData? modData = gameData.installedMods.Find(mod => mod.modName == modName);

                        if (modData == null)
                        {
                            modData = new(gameData, modName, filePath) { modName = modName };
                            gameData.installedMods.Add(modData);
                        }

                        if (!modData.HasCheckedForUpdate) modsToCheck.Add(modData);

                        if (!gameData.MatchesFilter(modData, selectedFilter)) continue;
                        if (!string.IsNullOrEmpty(filterText) && !modData.DisplayName.WithoutNumbers().ToLower().Contains(filterText) && !modName.ToLower().Contains(filterText) && !name.ToLower().Contains(filterText)) continue;

                        gameData.OnModLoaded(modData);
                        RenderedMod renderedMod = new(gameData, modData, this);
                        tempRenderedMods.Add(renderedMod);
                    }
                }
                else
                {
                    ProfileData profile = gameData.SelectedProfile;
                    for (int i = 0; i < profile.mods.Count; i++)
                    {
                        if (token.IsCancellationRequested) return;

                        ProfileModData profileMod = profile.mods[i];
                        ModData? modData = gameData.installedMods.Find(mod => string.Equals(mod.modName, profileMod.modName, StringComparison.OrdinalIgnoreCase));
                        if (modData == null) continue;

                        string filePath = modData.filePath;
                        if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath))) continue;

                        string modName = modData.modName;
                        string name = Directory.Exists(filePath) ? modName : Path.GetFileNameWithoutExtension(filePath);

                        if (!modData.HasCheckedForUpdate) modsToCheck.Add(modData);

                        if (!gameData.MatchesFilter(modData, selectedFilter)) continue;
                        if (!string.IsNullOrEmpty(filterText) && !modData.DisplayName.WithoutNumbers().ToLower().Contains(filterText) && !modName.ToLower().Contains(filterText) && !name.ToLower().Contains(filterText)) continue;

                        gameData.OnModLoaded(modData);
                        RenderedMod renderedMod = new(gameData, modData, this);
                        tempRenderedMods.Add(renderedMod);
                    }

                    _ = gameData.SaveAsync();
                }

                foreach (RenderedMod renderedMod in tempRenderedMods)
                {
                    renderedMods.Add(renderedMod);
                    listPanel.Children.Add(renderedMod);
                }

                if (modsToCheck.Count > 0)
                    _ = Task.Run(async () =>
                    {
                        for (int i = 0; i < modsToCheck.Count; i++)
                        {
                            if (token.IsCancellationRequested) return;
                            ModData mod = modsToCheck[i];
                            try
                            {
                                await mod.CheckForUpdateAsync(gameData.NexusName);
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
                Width = 135,
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
                Width = 120,
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
                    checkUpdatesButton.Content = "Checking...";
                    int count = 0;
                    
                    foreach (ModData modData in gameData!.installedMods)
                    {
                        count = count % 3 + 1;
                        checkUpdatesButton.Content = "Checking" + new string('.', count);

                        modData.HasCheckedForUpdate = false;
                        await modData.CheckForUpdateAsync(gameData.NexusName);
                        await Task.Delay(350);
                    }

                    await gameData.SaveAsync();
                    Refresh();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
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
                Width = 130,
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