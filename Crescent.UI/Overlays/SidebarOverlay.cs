using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public class SidebarOverlay : StackPanel
    {
        public readonly List<Type> tabTypes = new()
        {
            typeof(PlayTab),
            typeof(LogsTab),
            typeof(ModsTab),
            typeof(LoadOrderTab),
            typeof(SettingsTab)
        };

        public static ProgressBar? deploymentProgressBar;
        private static CrescentButton? deployButton;
        private static CrescentButton? withdrawButton;

        public OverlaySeparator overlaySeparator;
        public StackPanel contentPanel;
        public TextBlock? deploymentText;
        private Action? renderProfiles;
        public PluginData? currentGame;

        public List<ISidebarTab> sidebarTabs = new();
        public ISidebarTab? currentTab;

        public new bool IsVisible
        {
            get => base.IsVisible;
            set
            {
                base.IsVisible = value;
                overlaySeparator.IsVisible = value;
            }
        }

        public event Action<PluginData>? onGameSelected;
        public event Action<PluginData, ISidebarTab>? onTabOpened;

        public SidebarOverlay()
        {
            Background = Colors.Overlay;
            Margin = new Thickness(0);
            contentPanel = new()
            {
                Margin = new Thickness(0)
            };

            Children.Add(contentPanel);
        }

        public void RenderProfiles() => renderProfiles?.Invoke();

        public void ShowGameSidebar(PluginData plugin)
        {
            currentGame = plugin;
            onGameSelected?.Invoke(plugin);
            contentPanel.Children.Clear();

            StackPanel sidebarLayoutPanel = new()
            {
                Margin = new Thickness(10),
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            sidebarLayoutPanel.Children.Add(new Grid
            {
                Children =
                {
                    GetBanner(plugin)
                }
            });

            sidebarLayoutPanel.Children.Add(GetModManagementPanel());

            TextBlock gameTitle = new()
            {
                Text = plugin.Name,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            sidebarLayoutPanel.Children.Add(gameTitle);
            StackPanel verticalNavPanel = GetTabsPanel(plugin);
            sidebarLayoutPanel.Children.Add(verticalNavPanel);

            StackPanel profileSectionPanel = GetProfileSectionPanel(plugin);
            sidebarLayoutPanel.Children.Add(profileSectionPanel);
            contentPanel.Children.Add(sidebarLayoutPanel);
        }

        public Border GetBanner(PluginData plugin)
        {
            Border bannerBorder = new()
            {
                Width = 230,
                Height = 107,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            Grid bannerGrid = new();
            string bannerPath = Path.Combine(Runtime.PluginPath, plugin.folderName, "Assets", plugin.Theme.BannerName);
            if (File.Exists(bannerPath))
            {
                Image image = new()
                {
                    Source = new Bitmap(bannerPath),
                    Stretch = Stretch.UniformToFill
                };
                bannerGrid.Children.Add(image);
            }

            bannerBorder.Child = bannerGrid;
            return bannerBorder;
        }

        #region Tabs

        public StackPanel GetTabsPanel(PluginData pluginData)
        {
            sidebarTabs.Clear();
            StackPanel verticalNavPanel = new()
            {
                Spacing = 6
            };

            foreach (Type type in tabTypes)
            {
                ISidebarTab? tab = Activator.CreateInstance(type) as ISidebarTab;
                if (tab == null) continue;

                tab.OnLoaded(this, pluginData);
                sidebarTabs.Add(tab);
            }

            foreach (ISidebarTab tab in sidebarTabs.OrderBy(sidebarTab => sidebarTab.Order))
            {
                CrescentButton crescentButton = tab.GetButton();
                crescentButton.onClick += () => OpenTab(tab);
                verticalNavPanel.Children.Add(crescentButton);
            }

            return verticalNavPanel;
        }

        public void OpenTab(ISidebarTab? targetTab)
        {
            if (currentGame == null || targetTab == null) return;
            currentTab = targetTab;
            targetTab.OnClick();

            onTabOpened?.Invoke(currentGame, targetTab);
        }

        public void OpenTab<T>() where T : ISidebarTab
        {
            ISidebarTab? tab = sidebarTabs.OfType<T>().FirstOrDefault();
            if (tab != null) OpenTab(tab);
        }

        public T? GetTab<T>() where T : ISidebarTab
        {
            for (int index = 0; index < sidebarTabs.Count; index++)
                if (sidebarTabs[index] is T matchedTab)
                    return matchedTab;
            return default;
        }

        #endregion

        #region Deployment & Withdrawl

        public StackPanel GetModManagementPanel()
        {
            StackPanel modManagementPanel = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10
            };

            modManagementPanel.Children.Add(GetDeployButton());
            modManagementPanel.Children.Add(GetWithdrawButton());
            return modManagementPanel;
        }

        public CrescentButton GetDeployButton()
        {
            deployButton = new(Colors.Green)
            {
                Content = "Deploy",
                Width = 105
            };

            deployButton.onClick += async () =>
            {
                if (currentGame == null || !deployButton.IsEnabled) return;
                SetActionButtonsEnabled(false);
                deployButton.Content = "Deploying...";

                ModPathUtils.CleanupStagingDirectory(currentGame);
                ToastManager.ShowProgress($"Deploying {currentGame.Name}", $"Profile: {currentGame.SelectedProfile.name}", "Preparing staging files...", 0, true);

                try
                {
                    await currentGame.Deploy();
                    ModsTab? modsTab = GetTab<ModsTab>();
                    modsTab?.RenderMods();
                }
                finally
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        deployButton.Content = "Deploy";
                        SetActionButtonsEnabled(true);
                        PresentDeploymentToast(currentGame);
                    });
                }
            };

            return deployButton;
        }

        public CrescentButton GetWithdrawButton()
        {
            withdrawButton = new(Colors.Red)
            {
                Content = "Withdraw",
                Width = 105
            };

            withdrawButton.onClick += async () =>
            {
                if (currentGame == null || !withdrawButton.IsEnabled) return;
                SetActionButtonsEnabled(false);
                withdrawButton.Content = "Withdrawing...";

                int deployedBeforeCount = currentGame.installedMods.Count(mod => mod.isDeployed);
                ToastManager.ShowProgress($"Withdrawing {currentGame.Name}", $"Removing {deployedBeforeCount} deployed mod{(deployedBeforeCount == 1 ? "" : "s")}...", $"Profile: {currentGame.SelectedProfile.name}", 0, true);

                try
                {
                    await Task.Run(() => currentGame.Withdraw());
                    ModsTab? modsTab = GetTab<ModsTab>();
                    modsTab?.RenderMods();
                }
                finally
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        withdrawButton.Content = "Withdraw";
                        SetActionButtonsEnabled(true);
                        ToastManager.CompleteProgress("Mods Withdrawn", currentGame.DeploymentStatus.Replace("\n", " "), ToastType.Info, 4000, $"{currentGame.Name} • ({currentGame.SelectedProfile.name})");
                    });
                }
            };

            return withdrawButton;
        }

        public static void PresentDeploymentToast(PluginData plugin)
        {
            int deployedCount = plugin.installedMods.Count(mod => mod.isDeployed && plugin.SelectedProfile.IsModEnabled(mod));
            int enabledCount = plugin.installedMods.Count(mod => plugin.SelectedProfile.IsModEnabled(mod));
            int totalFiles = plugin.installedMods.Where(mod => mod.isDeployed && plugin.SelectedProfile.IsModEnabled(mod)).Sum(mod => mod.ownedFiles.Count);

            ToastType toastType = plugin.DeploymentState switch
            {
                DeploymentState.Deployed => ToastType.Success,
                DeploymentState.PartiallyDeployed => ToastType.Warning,
                DeploymentState.Failed => ToastType.Error,
                _ => ToastType.Info
            };

            string title = plugin.DeploymentState switch
            {
                DeploymentState.Deployed => "Deployment Complete",
                DeploymentState.PartiallyDeployed => "Partially Deployed",
                DeploymentState.Failed => "Deployment Failed",
                _ => "Deployment Status"
            };

            string primaryLine = deployedCount > 0 ? $"Deployed {deployedCount}/{enabledCount} enabled mod{enabledCount.WithPlural()} ({totalFiles} file{totalFiles.WithPlural()})" : plugin.DeploymentStatus.Replace("\n", " ");
            ToastManager.CompleteProgress(title, primaryLine, toastType, 4500, $"{plugin.Name} • ({plugin.SelectedProfile.name})");
        }

        private static void SetActionButtonsEnabled(bool isEnabled)
        {
            if (deployButton != null) deployButton.IsEnabled = isEnabled;
            if (withdrawButton != null) withdrawButton.IsEnabled = isEnabled;
        }

        #endregion

        #region Profiles

        public StackPanel GetProfileSectionPanel(PluginData plugin)
        {
            StackPanel profileSection = new()
            {
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            StackPanel profileListStack = new()
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            ScrollViewer profileScrollViewer = new()
            {
                Content = profileListStack,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            renderProfiles = () => RenderProfiles(plugin, profileListStack);

            Grid headerGrid = GetProfileHeader(plugin, profileScrollViewer);

            profileSection.Children.Add(headerGrid);
            profileSection.Children.Add(profileScrollViewer);
            renderProfiles?.Invoke();

            return profileSection;
        }

        private void RenderProfiles(PluginData plugin, StackPanel profileListStack)
        {
            profileListStack.Children.Clear();
            ModsTab? modsTab = GetTab<ModsTab>();
            List<ProfileData> profiles = plugin.Profiles;

            for (int index = 0; index < profiles.Count; index++)
            {
                ProfileData profile = profiles[index];
                bool isSelected = string.Equals(profile.name, plugin.selectedProfileName, StringComparison.OrdinalIgnoreCase);

                Grid profileGrid = new();
                profileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                profileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                CrescentButton profileButton = new(isSelected ? Colors.Green : Colors.Button)
                {
                    Content = profile.name,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(10, 8)
                };

                profileButton.onClick += async () => await SetProfileAsync(plugin, profile, modsTab);

                CrescentButton menuButton = new(ButtonStyle.Translucent)
                {
                    Content = "⋮",
                    Width = 35,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                ContextMenu contextMenu = ProfileContextMenu.Get(plugin, profile, this);
                menuButton.ContextMenu = contextMenu;
                menuButton.onClick += () => contextMenu.Open(menuButton);

                profileGrid.Children.Add(profileButton);
                Grid.SetColumn(profileButton, 0);

                profileGrid.Children.Add(menuButton);
                Grid.SetColumn(menuButton, 1);

                profileListStack.Children.Add(profileGrid);
            }
        }

        public Grid GetProfileHeader(PluginData plugin, ScrollViewer profileScrollViewer)
        {
            Grid headerGrid = new();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock profileTitle = new()
            {
                Text = "Profiles",
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(profileTitle, 0);
            Button addProfileButton = new()
            {
                Content = "New",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 35,
                Width = 50,
                Background = Colors.Button,
                Margin = new Thickness(0, 0, 4, 0)
            };

            Grid.SetColumn(addProfileButton, 1);
            Button importProfileButton = new()
            {
                Content = "Import",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 35,
                Width = 60,
                Background = Colors.Button,
                Margin = new Thickness(0, 0, 4, 0)
            };

            Grid.SetColumn(importProfileButton, 2);
            Button toggleButton = new()
            {
                Content = "▼",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 35,
                Width = 30,
                Background = Colors.Button
            };

            Grid.SetColumn(toggleButton, 3);
            addProfileButton.Click += async (_, _) => await AddProfileAsync(plugin);
            importProfileButton.Click += async (_, _) => await ProfileUtils.ImportProfileAsync(plugin, this);

            bool isCollapsed = false;
            toggleButton.Click += (_, _) =>
            {
                isCollapsed = !isCollapsed;
                profileScrollViewer.IsVisible = !isCollapsed;
                toggleButton.Content = isCollapsed ? "▲" : "▼";
            };

            headerGrid.Children.Add(profileTitle);
            headerGrid.Children.Add(addProfileButton);
            headerGrid.Children.Add(importProfileButton);
            headerGrid.Children.Add(toggleButton);
            return headerGrid;
        }

        #region Profile Interaction

        public async Task SetProfileAsync(PluginData plugin, ProfileData profile, ModsTab? modsTab)
        {
            if (plugin.selectedProfileName == profile.name) return;

            DeploymentState previousState = plugin.deploymentState;
            if (previousState == DeploymentState.Deployed) plugin.Withdraw();

            plugin.selectedProfileName = profile.name;
            await plugin.SaveAsync();
            renderProfiles?.Invoke();

            if (previousState == DeploymentState.Deployed) await plugin.Deploy();
            if (modsTab != null) modsTab.Refresh();
            ToastManager.Show("Profile Switched", $"Active profile set to {profile.name}", ToastType.Info, 3000, plugin.Name);
        }

        public async Task AddProfileAsync(PluginData plugin)
        {
            Window? parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null) return;

            ConfirmationDialog confirmationDialog = new("New Profile", "Enter profile name", true, string.Empty);
            await confirmationDialog.ShowDialog(parentWindow);

            string newName = confirmationDialog.InputText?.Trim() ?? string.Empty;
            bool profileExists = plugin.Profiles.Any(profileItem => string.Equals(profileItem.name, newName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(newName) && !profileExists)
            {
                ProfileData newProfile = new()
                {
                    name = newName
                };

                plugin.Profiles.Add(newProfile);
                plugin.selectedProfileName = newProfile.name;
                await plugin.SaveAsync();

                renderProfiles?.Invoke();
                ModsTab? modsTab = GetTab<ModsTab>();
                if (modsTab != null) modsTab.Refresh();
                ToastManager.Show("Profile Created", $"Created and switched to {newName}", ToastType.Success, 3000, plugin.Name);
            }
        }

        #endregion

        #endregion

        public void UpdateDeploymentStatus(PluginData plugin)
        {
            if (plugin.DeploymentState != DeploymentState.Deploying && plugin.DeploymentState != DeploymentState.Withdrawing)
                ToastManager.OnProgressDialogClosed();
        }

        public void UpdateProgress(int current, int total) => ToastManager.UpdateProgress(current, total, currentGame?.Name ?? "Mods");
    }
}