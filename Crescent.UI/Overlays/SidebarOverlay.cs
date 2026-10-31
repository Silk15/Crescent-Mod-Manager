using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class SidebarOverlay : StackPanel
    {
        public readonly List<Type> tabTypes = new()
        {
            typeof(PlayTab),
            typeof(LogsTab),
            typeof(ModsTab),
            typeof(SettingsTab)
        };
        
        public static ProgressBar? deploymentProgressBar;
        public static CrescentButton? deployButton;
        public static CrescentButton? withdrawButton;
        
        public StackPanel contentPanel;
        public TextBlock? deploymentText;
        public Action? renderProfiles;
        public GameData? currentGame;

        public List<ISidebarTab> sidebarTabs = new();
        public ISidebarTab? currentTab;
        
        public event Action<GameData>? onGameSelected;
        public event Action<GameData, ISidebarTab>? onTabOpened;

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
        
        public void ShowGameSidebar(GameData game)
        {
            currentGame = game;
            onGameSelected?.Invoke(game);
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
                    GetBanner(game)
                }
            });
            
            sidebarLayoutPanel.Children.Add(GetModManagementPanel());
            Border statusBorder = new()
            {
                Background = Colors.Button,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8)
            };
            
            statusBorder.Child = GetStatusPanel();
            sidebarLayoutPanel.Children.Add(statusBorder);
            UpdateDeploymentStatus(game);

            TextBlock gameTitle = new()
            {
                Text = game.Name,
                FontWeight = FontWeight.Bold,
                
                VerticalAlignment = VerticalAlignment.Center
            };

            sidebarLayoutPanel.Children.Add(gameTitle);
            StackPanel verticalNavPanel = GetTabsPanel(game);
            sidebarLayoutPanel.Children.Add(verticalNavPanel);

            StackPanel profileSectionPanel = GetProfileSectionPanel(game);
            sidebarLayoutPanel.Children.Add(profileSectionPanel);
            contentPanel.Children.Add(sidebarLayoutPanel);
        }
        
        public Border GetBanner(GameData game)
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
            string bannerPath = Path.Combine(Runtime.PluginPath, game.AssemblyName, "Assets", game.Banner);
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
        
        public StackPanel GetTabsPanel(GameData gameData)
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
                
                tab.OnLoaded(this, gameData);
                sidebarTabs.Add(tab);
            }

            foreach (ISidebarTab tab in sidebarTabs.OrderBy(s => s.Order))
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
            for (int i = 0; i < sidebarTabs.Count; i++) 
                if (sidebarTabs[i] is T) return (T)sidebarTabs[i];
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

        public StackPanel GetStatusPanel()
        {
            StackPanel statusPanel = new()
            {
                Spacing = 6
            };

            deploymentText = new()
            {
                TextAlignment = TextAlignment.Center,
                Text = "",
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };

            deploymentProgressBar = new()
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 6,
                IsVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            statusPanel.Children.Add(deploymentText);
            statusPanel.Children.Add(deploymentProgressBar);
            return statusPanel;
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
                if (currentGame == null) return;
                
                bool isDeploying = true;
                Task animationTask = Task.Run(async () =>
                {
                    int count = 0;
                    while (isDeploying)
                    {
                        count = count % 3 + 1;
                        string dots = new('.', count);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => deployButton.Content = $"Deploying{dots}");
                        await Task.Delay(250);
                    }
                });

                try
                {
                    await currentGame.Deploy();
                }
                finally
                {
                    isDeploying = false;
                    await animationTask;

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        deployButton.Content = "Deploy";
                        UpdateDeploymentStatus(currentGame);
                        if (deploymentProgressBar != null) deploymentProgressBar.IsVisible = false;
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
                Width = 105,
            };

            withdrawButton.onClick += async () =>
            {
                if (currentGame == null) return;
                
                bool isWithdrawing = true;
                Task animationTask = Task.Run(async () =>
                {
                    int count = 0;
                    while (isWithdrawing)
                    {
                        count = count % 3 + 1;
                        string dots = new('.', count);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => withdrawButton.Content = $"Withdrawing{dots}");
                        await Task.Delay(250);
                    }
                });

                try
                {
                    await Task.Run(() => currentGame.Withdraw());
                }
                finally
                {
                    isWithdrawing = false;
                    await animationTask;

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        withdrawButton.Content = "Withdraw";
                        UpdateDeploymentStatus(currentGame);
                        if (deploymentProgressBar != null) deploymentProgressBar.IsVisible = false;
                    });
                }
            };
            
            return withdrawButton;
        }
        
        #endregion

        #region Profiles
        
        public StackPanel GetProfileSectionPanel(GameData game)
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

            renderProfiles = () => RenderProfiles(game, profileListStack);
    
            Grid headerGrid = GetProfileHeader(game, profileScrollViewer);
    
            profileSection.Children.Add(headerGrid);
            profileSection.Children.Add(profileScrollViewer);
            renderProfiles?.Invoke();

            return profileSection;
        }
        
        private void RenderProfiles(GameData game, StackPanel profileListStack)
        {
            profileListStack.Children.Clear();
            ModsTab? modsTab = GetTab<ModsTab>();
            List<ProfileData> profiles = game.Profiles;

            for (int index = 0; index < profiles.Count; index++)
            {
                ProfileData profile = profiles[index];
                bool isSelected = string.Equals(profile.name, game.selectedProfileName, StringComparison.OrdinalIgnoreCase);

                Grid profileGrid = new();
                profileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                profileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                CrescentButton profileButton = new(isSelected ? Colors.Green : Colors.Button)
                {
                    Content = profile.name,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(10, 8)
                };

                profileButton.onClick += async () => await SetProfileAsync(game, profile, modsTab);

                CrescentButton menuButton = new(ButtonStyle.Translucent)
                {
                    Content = "⋮",
                    Width = 35,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                ContextMenu contextMenu = ProfileContextMenu.Get(game, profile, this);
                menuButton.ContextMenu = contextMenu;
                menuButton.onClick += () => contextMenu.Open(menuButton);

                profileGrid.Children.Add(profileButton);
                Grid.SetColumn(profileButton, 0);

                profileGrid.Children.Add(menuButton);
                Grid.SetColumn(menuButton, 1);

                profileListStack.Children.Add(profileGrid);
            }
        }
        
        public Grid GetProfileHeader(GameData game, ScrollViewer profileScrollViewer)
        {
            Grid headerGrid = new();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
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
                Content = "+",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 30,
                Width = 30 * 1.2,
                Background = Colors.Button,
                Margin = new Thickness(0, 0, 4, 0)
            };
            
            Grid.SetColumn(addProfileButton, 1);
            Button toggleButton = new()
            {
                Content = "▼",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 30,
                Width = 30,
                Background = Colors.Button
            };
            
            Grid.SetColumn(toggleButton, 2);
            addProfileButton.Click += async (_, _) => await AddProfileAsync(game);

            bool isCollapsed = false;
            toggleButton.Click += (_, _) =>
            {
                isCollapsed = !isCollapsed;
                profileScrollViewer.IsVisible = !isCollapsed;
                toggleButton.Content = isCollapsed ? "▲" : "▼";
            };

            headerGrid.Children.Add(profileTitle);
            headerGrid.Children.Add(addProfileButton);
            headerGrid.Children.Add(toggleButton);
            return headerGrid;
        }
        
        #region Profile Interaction
        
        public async Task SetProfileAsync(GameData game, ProfileData profile, ModsTab? modsTab)
        {
            if (game.selectedProfileName == profile.name) return;
    
            DeploymentState previousState = game.deploymentState;
            if (previousState == DeploymentState.Deployed) game.Withdraw();

            game.selectedProfileName = profile.name;
            await game.SaveAsync();
            renderProfiles?.Invoke();

            if (previousState == DeploymentState.Deployed) await game.Deploy();
            if (modsTab != null) modsTab.Refresh();
        }
        
        public async Task AddProfileAsync(GameData game)
        {
            Window? parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null) return;

            ConfirmationDialog confirmationDialog = new("New Profile", "Enter profile name", true, string.Empty);
            await confirmationDialog.ShowDialog(parentWindow);

            string newName = confirmationDialog.InputText?.Trim() ?? string.Empty;
            bool profileExists = game.Profiles.Any(profileItem => string.Equals(profileItem.name, newName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(newName) && !profileExists)
            {
                ProfileData newProfile = new()
                {
                    name = newName
                };

                game.Profiles.Add(newProfile);
                game.selectedProfileName = newProfile.name;
                await game.SaveAsync();
        
                renderProfiles?.Invoke();
                ModsTab? modsTab = GetTab<ModsTab>();
                if (modsTab != null) modsTab.Refresh();
            }
        }
        
        #endregion
        
        #endregion

        public void UpdateDeploymentStatus(GameData game)
        {
            if (deploymentText == null) return;
            deploymentText.Text = game.DeploymentStatus;
            deploymentText.Foreground = game.DeploymentState switch
            {
                DeploymentState.Deployed => Colors.Green,
                DeploymentState.Deploying => Brushes.Yellow,
                DeploymentState.Withdrawing => Brushes.Yellow,
                DeploymentState.PartiallyDeployed => Brushes.Orange,
                DeploymentState.Failed => Colors.Red,
                DeploymentState.NotDeployed => Brushes.White,
                _ => Brushes.LightGray
            };
        }

        public void UpdateProgress(int current, int total)
        {
            if (deploymentProgressBar == null) return;
            deploymentProgressBar.IsVisible = true;
            if (total > 0) deploymentProgressBar.Value = (double)current / total * 100;
        }
    }
}