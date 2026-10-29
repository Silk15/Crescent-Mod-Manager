using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Crescent.Plugins;

using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public class Sidebar : StackPanel
    {
        public GameData? currentGame;
        public StackPanel contentContainer;
        public Mods? modsMainPanel;
        public TextBlock? deploymentStatusBlock;
        public Action? renderProfiles = null;

        public event Action<GameData> onGameSettingsClicked;
        public event Action<GameData> onModsTabClicked;
        public event Action<GameData> onGameSelected;
        
        public Sidebar()
        {
            Background = Colors.Overlay;
            Margin = new Thickness(0);
            contentContainer = new()
            {
                Margin = new Thickness(0)
            };

            Children.Add(contentContainer);
            ShowDefaultLibrarySidebar();
        }

        public void ShowDefaultLibrarySidebar()
        {
            contentContainer.Children.Clear();
            StackPanel gamesPanel = GetGamesPanel();
            contentContainer.Children.Add(gamesPanel);
        }

        public StackPanel GetGamesPanel()
        {
            StackPanel gamesPanel = new()
            {
                Margin = new Thickness(10, 25, 10, 0)
            };

            foreach (GameData configuredGame in GameData.InstalledGames)
            {
                CrescentButton gameButton = GetGameButton(configuredGame);
                gamesPanel.Children.Add(gameButton);
            }

            return gamesPanel;
        }

        public CrescentButton GetGameButton(GameData game)
        {
            CrescentButton gameButton = new(ButtonStyle.Translucent)
            {
                Content = game.Name,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Foreground = Brushes.White,
                Padding = new Thickness(10, 8),
                Margin = new Thickness(0, 0, 0, 5)
            };

            gameButton.Click += (sender, routedEventArgs) =>
            {
                onGameSelected?.Invoke(game);
                ShowGameSidebar(game);
                onModsTabClicked?.Invoke(game);
            };

            return gameButton;
        }

        public void ShowGameSidebar(GameData game)
        {
            currentGame = game;
            onGameSelected?.Invoke(game);
            contentContainer.Children.Clear();

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
                    GetBannerBorder(game)
                },
            });

            StackPanel deployWithdrawPanel = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10
            };

            CrescentButton deployButton = new(ButtonStyle.Transparent)
            {
                Content = "Deploy",
                Width = 105,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Colors.Green,
                FontWeight = FontWeight.Bold
            };

            deployButton.Click += async (sender, routedEventArgs) => await game.Deploy();
            CrescentButton withdrawButton = new(ButtonStyle.Transparent)
            {
                Content = "Withdraw",
                Width = 105,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Colors.Red,
                FontWeight = FontWeight.Bold
            };

            withdrawButton.Click += (sender, routedEventArgs) => game.Withdraw();

            deployWithdrawPanel.Children.Add(deployButton);
            deployWithdrawPanel.Children.Add(withdrawButton);
            sidebarLayoutPanel.Children.Add(deployWithdrawPanel);

            Border statusBorder = new()
            {
                Background = Colors.Button,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(0, 8)
            };

            deploymentStatusBlock = new()
            {
                TextAlignment = TextAlignment.Center,
                Text = "",
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(10, 0),
                FontSize = 12
            };

            UpdateDeploymentStatus(game);
            statusBorder.Child = deploymentStatusBlock;
            sidebarLayoutPanel.Children.Add(statusBorder);

            TextBlock gameTitle = new()
            {
                Text = game.Name,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            sidebarLayoutPanel.Children.Add(gameTitle);
            StackPanel verticalNavPanel = GetVerticalNavPanel(game);
            sidebarLayoutPanel.Children.Add(verticalNavPanel);

            StackPanel profileSectionPanel = GetProfileSectionPanel(game);
            sidebarLayoutPanel.Children.Add(profileSectionPanel);

            contentContainer.Children.Add(sidebarLayoutPanel);
        }

        public StackPanel GetProfileSectionPanel(GameData game)
        {
            StackPanel profileSection = new()
            {
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            Grid headerGrid = new();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock profileTitle = new()
            {
                Text = "Profiles",
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(profileTitle, 0);
            Button addProfileButton = new()
            {
                Content = "+",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 30,
                Width = 25,
                Foreground = Brushes.White,
                Background = Colors.Button,
                Margin = new Thickness(0, 0, 4, 0)
            };

            addProfileButton.Width *= 1.2;
            Grid.SetColumn(addProfileButton, 1);
            Button toggleButton = new()
            {
                Content = "▼",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 30,
                Width = 30,
                Foreground = Brushes.White,
                Background = Colors.Button
            };

            Grid.SetColumn(toggleButton, 2);
            headerGrid.Children.Add(profileTitle);
            headerGrid.Children.Add(addProfileButton);
            headerGrid.Children.Add(toggleButton);
            profileSection.Children.Add(headerGrid);

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
            
            renderProfiles = () =>
            {
                profileListStack.Children.Clear();
                List<ProfileData> profiles = game.Profiles;
                for (int i = 0; i < profiles.Count; i++)
                {
                    Grid profileGrid = new();
                    profileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    profileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    
                    ProfileData profile = profiles[i];
                    bool isSelected = string.Equals(profile.name, game.selectedProfileName, StringComparison.OrdinalIgnoreCase);
                    CrescentButton profileButton = new(isSelected ? Colors.Green : Colors.Button)
                    {
                        Content = profile.name,
                        Height = 35,
                        
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Padding = new Thickness(10, 8)
                    };
                    
                    profileButton.Click += async (sender, args) =>
                    {
                        DeploymentState previousState = game.deploymentState;
                        if (previousState == DeploymentState.Deployed) 
                            game.Withdraw();
                        
                        game.selectedProfileName = profile.name;
                        await game.SaveAsync();
                        renderProfiles?.Invoke();
                        
                        if (previousState == DeploymentState.Deployed) 
                            await game.Deploy();
                        
                        if (modsMainPanel != null)
                            modsMainPanel.RefreshPanel();
                        
                        profileButton.customColor = isSelected ? Colors.Green : Colors.Button;
                    };
                    
                    CrescentButton menuButton = new(ButtonStyle.Translucent)
                    {
                        Content = "⋮",
                        Width = 35,
                        Height = 35,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    ContextMenu contextMenu = ProfileContextMenu.Get(modsMainPanel, this, game, profile);
                    menuButton.ContextMenu = contextMenu;
                    menuButton.Click += (sender, e) => contextMenu.Open(menuButton);
                    
                    profileGrid.Children.Add(profileButton);
                    Grid.SetColumn(profileButton, 0);
                    
                    profileGrid.Children.Add(menuButton);
                    Grid.SetColumn(menuButton, 1);
                    

                    profileListStack.Children.Add(profileGrid);
                }
            };

            addProfileButton.Click += async (sender, args) =>
            {
                Window? parentWindow = TopLevel.GetTopLevel(this) as Window;
                if (parentWindow == null) return;
                InputDialog dialog = new("New Profile", "Enter profile name:", true, "");
                await dialog.ShowDialog(parentWindow);
                string newName = dialog.InputText?.Trim() ?? "";
                if (!string.IsNullOrEmpty(newName) && !game.Profiles.Any(p => string.Equals(p.name, newName, StringComparison.OrdinalIgnoreCase)))
                {
                    ProfileData newProfile = new()
                    {
                        name = newName
                    };
                    
                    game.Profiles.Add(newProfile);
                    game.selectedProfileName = newProfile.name;
                    await game.SaveAsync();
                    renderProfiles?.Invoke();
                    if (modsMainPanel != null) modsMainPanel.RefreshPanel();
                }
            };

            bool isCollapsed = false;
            toggleButton.Click += (sender, args) =>
            {
                isCollapsed = !isCollapsed;
                profileScrollViewer.IsVisible = !isCollapsed;
                toggleButton.Content = isCollapsed ? "▲" : "▼";
            };

            profileSection.Children.Add(profileScrollViewer);
            renderProfiles?.Invoke();

            return profileSection;
        }

        public Border GetBannerBorder(GameData game)
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
            bannerBorder.PointerReleased += (sender, pointerReleasedEventArgs) =>
            {
                if (pointerReleasedEventArgs.InitialPressMouseButton == MouseButton.Left && !string.IsNullOrEmpty(game.ExePath) && File.Exists(game.ExePath))
                    Process.Start(new ProcessStartInfo(game.ExePath)
                    {
                        UseShellExecute = true
                    });
            };

            return bannerBorder;
        }

        public StackPanel GetVerticalNavPanel(GameData game)
        {
            StackPanel verticalNavPanel = new()
            {
                Spacing = 6
            };

            CrescentButton playButton = new(ButtonStyle.Translucent)
            {
                Content = "Play",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };

            playButton.Click += (sender, args) => Process.Start(new ProcessStartInfo(game.ExePath)
            {
                UseShellExecute = true
            });

            CrescentButton modsTabButton = new(ButtonStyle.Translucent)
            {
                Content = "Mods",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };

            modsTabButton.Click += (sender, routedEventArgs) => onModsTabClicked?.Invoke(game);

            CrescentButton settingsTabButton = new(ButtonStyle.Translucent)
            {
                Content = "Settings",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };

            settingsTabButton.Click += (sender, routedEventArgs) => onGameSettingsClicked?.Invoke(game);
            verticalNavPanel.Children.Add(playButton);
            verticalNavPanel.Children.Add(modsTabButton);
            verticalNavPanel.Children.Add(settingsTabButton);

            return verticalNavPanel;
        }

        public void ShowModsList(GameData game) => modsMainPanel = new(game);

        public void UpdateDeploymentStatus(GameData game)
        {
            if (deploymentStatusBlock != null)
            {
                deploymentStatusBlock.Text = game.DeploymentStatus;
                deploymentStatusBlock.Foreground = game.DeploymentState switch
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
        }
    }
}