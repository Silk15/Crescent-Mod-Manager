using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Crescent.APIs.Discord;
using Crescent.Plugins;
using Crescent.UI.Controls;
using Crescent.UI.Controls.Panels;
using Crescent.UI.Controls.Sidebar;
using Crescent.UI.Controls.Tabs;
using Crescent.UI.Controls.Toasts;
using Crescent.UI.Controls.Toolbar;
using Crescent.UI.Dialogs;
using Crescent.UI.Nxm;
using Silk.Utilities;

namespace Crescent.UI.Windows
{
    public class MainWindow : Window
    {
        public const string IconUri = "avares://Crescent.UI/Assets/crescent.ico";
        public const string TitleText = "Crescent Mod Manager";

        public ScrollViewer? pageScrollViewer;
        public Grid? contentGrid;
        public Grid? mainGrid;
        public Grid? backgroundGrid;
        public Grid? gameTabBar;
        public Border? gameTabContainer;

        public PluginData? selectedGame;
        public PluginData? subscribedGame;

        public Bitmap? currentBackgroundBitmap;
        public string? currentBackgroundPath;

        public SidebarOverlay? sidebarOverlay;
        public OverlaySeparator? overlaySeparator;
        public ToolbarOverlay? toolbarOverlay;
        public LibraryPanel? libraryPanel;
        public SettingsPanel? settingsPanel;
        public ToastOverlay? toastOverlay;

        public DeploymentState lastObservedState = DeploymentState.NotDeployed;

        public MainWindow()
        {
            Vector2 size = Settings.Instance.saveWindowSize ? Settings.Instance.windowSize : new(1500, 900);
            Screen? screen = Screens.Primary;
            try
            {
                Title = TitleText;
                Width = size.X;
                Height = size.Y;

                WindowState = Settings.Instance.maximised ? WindowState.Maximized : WindowState.Normal;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                WindowDecorations = WindowDecorations.Full;
                Background = Colors.MainPanelEnd;
                Foreground = Brushes.White;
                Content = GetMainGrid();
                Icon = new WindowIcon(AssetLoader.Open(new Uri(IconUri)));
                CornerRadius = new(8);
                ClipToBounds = true;

                SetPluginCallbacks();
                SetNxmHandling();
            }
            catch (Exception exception)
            {
                Background = Colors.MainPanelEnd;
                Content = new TextBlock
                {
                    Text = $"{exception.Message}!\n{exception.StackTrace}",
                    TextAlignment = TextAlignment.Center,
                    FontSize = 15,
                    Foreground = Brushes.Crimson,
                    VerticalAlignment = VerticalAlignment.Center
                };

                HorizontalAlignment = HorizontalAlignment.Center;
                VerticalAlignment = VerticalAlignment.Center;
                Logger.Error(exception.ToString());
            }
        }
        
        public void SetPageContent(Control content)
        {
            if (pageScrollViewer == null) return;
            pageScrollViewer.Content = content;
        }

        public void SetPluginCallbacks()
        {
            PluginData.OnProgressWindowOpen = () => Task.CompletedTask;
            PluginData.OnProgressWindowUpdate = (current, total, modName, targetFile) =>
            {
                ToastManager.UpdateProgress(current, total, modName, targetFile);
                return Task.CompletedTask;
            };
            PluginData.OnProgressWindowClose = () =>
            {
                ToastManager.OnProgressDialogClosed();
                return Task.CompletedTask;
            };

            PluginData.OnConflictResolve = async conflicts =>
            {
                if (Settings.Instance.autoResolveConflicts) return true;

                bool resolved = false;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    ConflictReportDialog conflictDialog = new(selectedGame, conflicts);
                    await conflictDialog.ShowDialog(this);
                    resolved = conflictDialog.conflictsResolved;
                });

                return resolved;
            };
        }

        public void SetNxmHandling()
        {
            if (Settings.Instance.autoRegisterNxm && !NxmProtocolHandler.IsProtocolRegistered()) NxmProtocolHandler.RegisterProtocol(false);
            NxmProtocolHandler.StartPipeServer();
            NxmProtocolHandler.onModDownloaded += plugin =>
            {
                if (selectedGame == plugin && sidebarOverlay != null)
                {
                    ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
                    modsTab?.Refresh();
                }
            };

            string[] commandLineArgs = Environment.GetCommandLineArgs();
            string? startupNxm = commandLineArgs.FirstOrDefault(argument => argument.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(startupNxm)) NxmProtocolHandler.DownloadUrlOrNxmAsync(startupNxm).FireAndForget();
        }

        public void SetSidebarVisible(bool isVisible)
        {
            if (sidebarOverlay != null) sidebarOverlay.IsVisible = isVisible;
            if (overlaySeparator != null) overlaySeparator.IsVisible = isVisible;
            if (gameTabBar != null) gameTabBar.IsVisible = isVisible;
        }

        public void ShowLibrary()
        {
            if (libraryPanel != null) libraryPanel.onPluginClicked -= OnPluginClicked;
            libraryPanel = new();
            libraryPanel.onPluginClicked += OnPluginClicked;
            SetPageContent(libraryPanel);
            UpdateBackground(null);
            SetSidebarVisible(false);
            DiscordManager.SetIdle();
        }

        public void ShowSettings()
        {
            settingsPanel = new(this);
            SetPageContent(settingsPanel);
            UpdateBackground(null);
            SetSidebarVisible(false);
            DiscordManager.SetIdle();
        }

        public void TryUpdateDeploymentStatus(PluginData plugin)
        {
            if (selectedGame != plugin || sidebarOverlay == null) return;
            sidebarOverlay.UpdateDeploymentStatus(plugin);

            DeploymentState currentState = plugin.deploymentState;
            if ((lastObservedState == DeploymentState.Deploying || lastObservedState == DeploymentState.Withdrawing) && currentState != DeploymentState.Deploying && currentState != DeploymentState.Withdrawing)
            {
                if (lastObservedState == DeploymentState.Deploying) SidebarOverlay.ShoDeploymentToast(plugin);
                else ToastManager.CompleteProgress("Mods Withdrawn", plugin.DeploymentStatus.Replace("\n", " "), ToastType.Info, 4000, $"{plugin.Name} • ({plugin.SelectedProfile.name})");
            }

            lastObservedState = currentState;
        }

        public void ClearBackgroundBitmap()
        {
            currentBackgroundBitmap?.Dispose();
            currentBackgroundBitmap = null;
            currentBackgroundPath = null;
        }

        public void UpdateBackground(PluginData? game = null)
        {
            if (backgroundGrid == null) return;

            string? targetBannerPath = game != null ? Path.Combine(Runtime.PluginPath, game.folderName, "Assets", game.theme.GetBackgroundName()) : null;
            if (targetBannerPath == currentBackgroundPath && backgroundGrid.Children.Count > 0) return;

            backgroundGrid.Children.Clear();
            ClearBackgroundBitmap();
            try
            {
                if (game != null && !string.IsNullOrEmpty(targetBannerPath) && File.Exists(targetBannerPath))
                {
                    currentBackgroundBitmap = new Bitmap(targetBannerPath);
                    currentBackgroundPath = targetBannerPath;

                    backgroundGrid.Children.Add(new Image
                    {
                        Source = currentBackgroundBitmap,
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.45,
                        Effect = new BlurEffect
                        {
                            Radius = 60
                        }
                    });

                    backgroundGrid.Children.Add(new Border
                    {
                        Background = new RadialGradientBrush
                        {
                            Center = new(0.0, 0.0, RelativeUnit.Relative),
                            GradientOrigin = new(0.0, 0.0, RelativeUnit.Relative),
                            RadiusX = new(1.5, RelativeUnit.Relative),
                            RadiusY = new(1.5, RelativeUnit.Relative),
                            GradientStops = new()
                            {
                                new GradientStop(Color.FromArgb(20, 0, 0, 0), 0.0),
                                new GradientStop(Color.FromArgb(120, Colors.MainPanelEnd.Color.R, Colors.MainPanelEnd.Color.G, Colors.MainPanelEnd.Color.B), 1)
                            }
                        }
                    });
                }
                else
                {
                    backgroundGrid.Children.Add(new Border
                    {
                        Background = Colors.MainGradient
                    });
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                ClearBackgroundBitmap();
                backgroundGrid.Children.Add(new Border
                {
                    Background = Colors.MainPanelEnd
                });
            }
        }

        #region Content

        public Grid GetMainGrid()
        {
            mainGrid = new();
            mainGrid.RowDefinitions.Add(new RowDefinition(50, GridUnitType.Pixel));
            mainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            backgroundGrid = new();
            UpdateBackground(null);
            mainGrid.Children.Add(backgroundGrid);
            Grid.SetRowSpan(backgroundGrid, 3);

            toolbarOverlay = new(this);
            mainGrid.Children.Add(toolbarOverlay);
            Grid.SetRow(toolbarOverlay, 0);

            Grid bodyGrid = new();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            bodyGrid.Children.Add(GetContentGrid());
            Grid.SetColumn(contentGrid!, 0);

            bodyGrid.Children.Add(GetSidebarOverlay());
            Grid.SetColumn(sidebarOverlay!, 1);

            overlaySeparator = new(OverlaySeparator.Mode.Vertical);
            sidebarOverlay!.overlaySeparator = overlaySeparator;
            bodyGrid.Children.Add(overlaySeparator);
            Grid.SetColumn(overlaySeparator, 1);

            mainGrid.Children.Add(bodyGrid);
            Grid.SetRow(bodyGrid, 1);

            toastOverlay = ToastManager.Initialize();
            mainGrid.Children.Add(toastOverlay);
            Grid.SetRowSpan(toastOverlay, 3);

            SetSidebarVisible(false);
            return mainGrid;
        }

        public Grid GetContentGrid()
        {
            contentGrid = new();
            contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            contentGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            gameTabContainer = new()
            {
                Background = Colors.Overlay,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            OverlaySeparator tabBottomSeparator = new(OverlaySeparator.Mode.Horizontal)
            {
                VerticalAlignment = VerticalAlignment.Bottom
            };

            gameTabBar = new()
            {
                IsVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            gameTabBar.RowDefinitions.Add(new RowDefinition(42, GridUnitType.Pixel));
            gameTabBar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetRow(gameTabContainer, 0);
            Grid.SetRow(tabBottomSeparator, 1);

            gameTabBar.Children.Add(gameTabContainer);
            gameTabBar.Children.Add(tabBottomSeparator);

            Grid.SetRow(gameTabBar, 0);
            contentGrid.Children.Add(gameTabBar);

            ScrollViewer scrollViewer = GetPageScrollViewer();
            Grid.SetRow(scrollViewer, 1);
            contentGrid.Children.Add(scrollViewer);

            return contentGrid;
        }

        public ScrollViewer GetPageScrollViewer()
        {
            libraryPanel = new();
            libraryPanel.onPluginClicked += OnPluginClicked;
            pageScrollViewer = new()
            {
                Margin = new(0),
                Padding = new(0, 0, 0, 28),
                Content = libraryPanel
            };

            return pageScrollViewer;
        }

        public SidebarOverlay GetSidebarOverlay()
        {
            sidebarOverlay = new()
            {
                Width = 300
            };
            sidebarOverlay.onGameSelected += OnGameSelected;
            sidebarOverlay.onTabOpened += OnTabOpened;
            return sidebarOverlay;
        }

        #endregion

        #region Events

        public void OnPluginClicked(PluginData plugin)
        {
            selectedGame = plugin;
            if (sidebarOverlay == null)
            {
                Logger.Warn("No sidebar to update :(");
                return;
            }

            SetSidebarVisible(true);
            sidebarOverlay.ShowSidebar(plugin);
            if (gameTabContainer != null) gameTabContainer.Child = sidebarOverlay.GetTabsPanel(plugin);

            sidebarOverlay.OpenTab<ModsTab>();
            DiscordManager.SetModding(plugin);
        }

        public void OnGameSelected(PluginData plugin)
        {
            selectedGame = plugin;
            lastObservedState = plugin.deploymentState;
            if (subscribedGame != null) subscribedGame.onDeploymentStatusChanged -= OnDeploymentStatusChanged;
            subscribedGame = plugin;
            subscribedGame.onDeploymentStatusChanged += OnDeploymentStatusChanged;
        }

        public void OnTabOpened(PluginData plugin, ISidebarTab activeTab)
        {
            selectedGame = plugin;
            if (activeTab is Control tabControl) SetPageContent(tabControl);
            UpdateBackground(plugin);
        }

        public void OnDeploymentStatusChanged(PluginData plugin) => Dispatcher.UIThread.Post(() => TryUpdateDeploymentStatus(plugin));

        #endregion
    }
}