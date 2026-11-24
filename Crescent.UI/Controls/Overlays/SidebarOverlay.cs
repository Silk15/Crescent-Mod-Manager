using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;
using Crescent.UI.Controls.Tabs;
using Crescent.UI.Controls.Toasts;
using Silk.Utilities;

namespace Crescent.UI.Controls.Sidebar
{
    public class SidebarOverlay : Grid
    {
        public readonly List<Type> tabTypes = new()
        {
            typeof(ConsoleTab),
            typeof(LogsTab),
            typeof(ModsTab),
            typeof(LoadOrderTab),
            typeof(SettingsTab),
            typeof(BrowseTab)
        };

        public static ProgressBar? deploymentProgressBar;
        private static CrescentButton? deployButton;
        private static CrescentButton? withdrawButton;

        public OverlaySeparator overlaySeparator;
        public ProfilePanel profilePanel;
        public Grid contentPanel;
        public TextBlock? deploymentText;
        public PluginData? currentGame;

        public List<ISidebarTab> sidebarTabs = new();
        public Dictionary<ISidebarTab, SidebarTabButton> tabButtons = new();
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
            Margin = new(0);
            VerticalAlignment = VerticalAlignment.Stretch;

            contentPanel = new()
            {
                Margin = new(0),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            contentPanel.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            contentPanel.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Children.Add(contentPanel);
        }

        public void ShowSidebar(PluginData plugin)
        {
            currentGame = plugin;
            onGameSelected?.Invoke(plugin);
            contentPanel.Children.Clear();

            StackPanel sidebarLayoutPanel = new()
            {
                Margin = new(16),
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };

            sidebarLayoutPanel.Children.Add(GetCoverArt(plugin));
            sidebarLayoutPanel.Children.Add(GetModManagementPanel(plugin));
            sidebarLayoutPanel.Children.Add(profilePanel = new ProfilePanel(plugin, this));

            SetRow(sidebarLayoutPanel, 0);
            contentPanel.Children.Add(sidebarLayoutPanel);

            TextBlock footerText = new()
            {
                Text = $"{Runtime.Version}{(Runtime.DeveloperBuild ? " • Developer Build" : "")}",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new(0, 0, 0, 16),
                FontSize = 11,
                Opacity = 0.35,
                FontWeight = FontWeight.Medium,
                TextAlignment = TextAlignment.Center
            };

            SetRow(footerText, 1);
            contentPanel.Children.Add(footerText);
        }

        public Border GetCoverArt(PluginData plugin)
        {
            Border coverBorder = new()
            {
                Height = 400,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new(4, 4, 0, 0),
                BorderBrush = Colors.Separator,
                BorderThickness = new(1),
                ClipToBounds = true
            };

            Grid coverGrid = new();
            coverGrid.Children.Add(new ThemeImage(plugin, theme => theme.GetPosterName()));

            coverBorder.Child = coverGrid;
            return coverBorder;
        }

        #region Tabs

        public UniformGrid GetTabsPanel(PluginData pluginData)
        {
            sidebarTabs.Clear();
            tabButtons.Clear();

            foreach (Type type in tabTypes)
            {
                ISidebarTab? tab = Activator.CreateInstance(type) as ISidebarTab;
                if (tab == null) continue;

                tab.OnLoaded(this, pluginData);
                sidebarTabs.Add(tab);
            }

            List<ISidebarTab> orderedTabs = sidebarTabs.OrderBy(sidebarTab => sidebarTab.Order).ToList();
            UniformGrid tabBarGrid = new()
            {
                Rows = 1,
                Columns = orderedTabs.Count,
                Margin = new(4, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            for (int index = 0; index < orderedTabs.Count; index++)
            {
                ISidebarTab tab = orderedTabs[index];
                CrescentButton sourceButton = tab.GetButton();
                string tabTitle = sourceButton.Content?.ToString() ?? string.Empty;

                SidebarTabButton tabButton = new(tabTitle);
                tabButton.onClick += () => OpenTab(tab);

                tabButtons[tab] = tabButton;
                tabBarGrid.Children.Add(tabButton);
            }

            return tabBarGrid;
        }

        public void OpenTab(ISidebarTab? targetTab)
        {
            if (currentGame == null || targetTab == null) return;
            currentTab = targetTab;

            foreach (KeyValuePair<ISidebarTab, SidebarTabButton> entry in tabButtons)
                entry.Value.SetActive(entry.Key == targetTab);

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

        #region Deployment & Withdrawal

        public Border GetModManagementPanel(PluginData pluginData)
        {
            Border managementBorder = new()
            {
                CornerRadius = new(0, 0, 4, 4),
                ClipToBounds = true,
                Background = Colors.Button,
                BorderBrush = Colors.Separator,
                BorderThickness = new(1),
                Margin = new(0, -17, 0, 0)
            };

            Grid modManagementGrid = new()
            {
                ColumnSpacing = 10,
                RowSpacing = 10,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new(16, 16, 16, 16),
            };

            modManagementGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            modManagementGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            modManagementGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            modManagementGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            CrescentButton deployButtonControl = GetDeployButton();
            deployButtonControl.Width = double.NaN;
            deployButtonControl.Height = 36;
            deployButtonControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            SetRow(deployButtonControl, 0);
            SetColumn(deployButtonControl, 0);

            CrescentButton withdrawButtonControl = GetWithdrawButton();
            withdrawButtonControl.Width = double.NaN;
            withdrawButtonControl.Height = 36;
            withdrawButtonControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            SetRow(withdrawButtonControl, 0);
            SetColumn(withdrawButtonControl, 1);

            CrescentButton playButton = new(Colors.Hover)
            {
                Content = "Play",
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            playButton.onClick += () =>
            {
                if (string.IsNullOrEmpty(pluginData.ExePath) || !File.Exists(pluginData.ExePath))
                    ToastManager.Show("Executable Not Set", $"Set the executable path for {pluginData.Name} in Settings first!", ToastType.Warning);
                
                ConsoleTab? consoleTab = GetTab<ConsoleTab>();
                if (consoleTab == null) return;

                OpenTab(consoleTab);
                consoleTab.TryOpenGame();
            };

            SetRow(playButton, 1);
            SetColumn(playButton, 0);
            SetColumnSpan(playButton, 2);

            modManagementGrid.Children.Add(deployButtonControl);
            modManagementGrid.Children.Add(withdrawButtonControl);
            modManagementGrid.Children.Add(playButton);

            managementBorder.Child = modManagementGrid;
            return managementBorder;
        }

        public CrescentButton GetDeployButton()
        {
            deployButton = new(Colors.Accent)
            {
                Content = "Deploy",
                Width = 105
            };

            deployButton.onClick += async () =>
            {
                if (currentGame == null || !deployButton.IsEnabled) return;
                SetActionButtonsEnabled(false);
                deployButton.Content = "Deploying...";

                ModsIO.CleanupStagingDirectory(currentGame);
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
                        ShoDeploymentToast(currentGame);
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

        public static void ShoDeploymentToast(PluginData plugin)
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

        public void UpdateDeploymentStatus(PluginData plugin)
        {
            if (plugin.DeploymentState != DeploymentState.Deploying && plugin.DeploymentState != DeploymentState.Withdrawing)
                ToastManager.OnProgressDialogClosed();
        }

        public void UpdateProgress(int current, int total) => ToastManager.UpdateProgress(current, total, currentGame?.Name ?? "Mods");
    }
}