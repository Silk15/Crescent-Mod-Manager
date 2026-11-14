using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Crescent.APIs.Discord;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class MainWindow : Window
    {
        public const string IconUri = "avares://Crescent.UI/Assets/crescent.ico";
        public const string TitleText = "Crescent Mod Manager";

        public ScrollViewer? libraryScrollViewer;
        public Grid? contentGrid;
        public Grid? mainGrid;
        public Grid? backgroundGrid;

        public PluginData? selectedGame;
        public PluginData? subscribedGame;

        public SidebarOverlay? sidebarOverlay;
        public OverlaySeparator? overlaySeparator;
        public ToolbarOverlay? toolbarOverlay;
        public LibraryPanel? libraryPanel;
        public SettingsPanel? settingsPanel;
        public ToastOverlay? toastOverlay;

        public DeploymentState lastObservedState = DeploymentState.NotDeployed;

        public MainWindow()
        {
            try
            {
                Title = TitleText;
                Width = 1500;
                Height = 900;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                WindowDecorations = WindowDecorations.Full;
                Background = Colors.MainPanelEnd;
                Foreground = Brushes.White;
                Content = GetMainGrid();
                Icon = new WindowIcon(AssetLoader.Open(new Uri(IconUri)));

                SetPluginCallbacks();
                SetNxmHandling();
            }
            catch (Exception ex)
            {
                Background = Colors.MainPanelEnd;
                Content = new TextBlock
                {
                    Text = $"{ex.Message}!\n{ex.StackTrace}",
                    TextAlignment = TextAlignment.Center,
                    FontSize = 15,
                    Foreground = Brushes.Crimson,
                    VerticalAlignment = VerticalAlignment.Center
                };

                HorizontalAlignment = HorizontalAlignment.Center;
                VerticalAlignment = VerticalAlignment.Center;
                Logger.Error(ex.ToString());
            }
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
            if (Settings.Instance.autoRegisterNxm && !NxmProtocolHandler.IsProtocolRegistered())
                NxmProtocolHandler.RegisterProtocol(false);

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
            string? startupNxm = commandLineArgs.FirstOrDefault(arg => arg.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(startupNxm)) _ = NxmProtocolHandler.DownloadUrlOrNxmAsync(startupNxm);
        }

        public void SetSidebarVisible(bool isVisible)
        {
            if (sidebarOverlay != null) sidebarOverlay.IsVisible = isVisible;
            if (overlaySeparator != null) overlaySeparator.IsVisible = isVisible;
        }

        public void ShowLibrary()
        {
            if (libraryScrollViewer == null) return;
            if (libraryPanel != null) libraryPanel.onPluginClicked -= OnPluginClicked;
            libraryPanel = new();
            libraryScrollViewer.Content = libraryPanel;
            libraryPanel.onPluginClicked += OnPluginClicked;
            UpdateBackground();
            SetSidebarVisible(false);
            DiscordManager.SetIdle();
        }

        public void ShowSettings()
        {
            if (libraryScrollViewer == null) return;
            settingsPanel = new(this);
            libraryScrollViewer.Content = settingsPanel;
            UpdateBackground();
            SetSidebarVisible(false);
            DiscordManager.SetIdle();
        }

        public void TryUpdateDeploymentStatus(PluginData plugin)
        {
            if (selectedGame != plugin || sidebarOverlay == null) return;
            sidebarOverlay.UpdateDeploymentStatus(plugin);

            DeploymentState currentState = plugin.deploymentState;
            if ((lastObservedState == DeploymentState.Deploying || lastObservedState == DeploymentState.Withdrawing) &&
                currentState != DeploymentState.Deploying && currentState != DeploymentState.Withdrawing)
            {
                if (lastObservedState == DeploymentState.Deploying)
                    SidebarOverlay.PresentDeploymentToast(plugin);
                else
                    ToastManager.CompleteProgress("Mods Withdrawn", plugin.DeploymentStatus.Replace("\n", " "), ToastType.Info, 4000, $"{plugin.Name} • ({plugin.SelectedProfile.name})");
            }

            lastObservedState = currentState;
        }

        public void UpdateBackground(PluginData? game = null)
        {
            if (backgroundGrid == null) return;
            backgroundGrid.Children.Clear();
            try
            {
                if (game != null)
                {
                    string bannerPath = Path.Combine(Runtime.PluginPath, game.folderName, "Assets", game.Theme.BackgroundName);
                    Bitmap? bitmap = null;

                    if (!string.IsNullOrEmpty(bannerPath) && File.Exists(bannerPath)) bitmap = new Bitmap(bannerPath);
                    if (bitmap != null)
                    {
                        backgroundGrid.Children.Add(new Image
                        {
                            Source = bitmap,
                            Stretch = Stretch.UniformToFill,
                            Opacity = 0.45,
                            Effect = new BlurEffect
                            {
                                Radius = 60
                            }
                        });
                    }
                    else backgroundGrid.Children.Add(new Border
                    {
                        Background = Colors.MainGradient
                    });

                    backgroundGrid.Children.Add(new Border
                    {
                        Background = new RadialGradientBrush
                        {
                            Center = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
                            GradientOrigin = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
                            RadiusX = new RelativeScalar(1.5, RelativeUnit.Relative),
                            RadiusY = new RelativeScalar(1.5, RelativeUnit.Relative),
                            GradientStops = new()
                            {
                                new GradientStop(Color.FromArgb(20, 0, 0, 0), 0.0),
                                new GradientStop(Color.FromArgb(120, Colors.MainPanelEnd.Color.R, Colors.MainPanelEnd.Color.G, Colors.MainPanelEnd.Color.B), 1),
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
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
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
            Grid.SetRowSpan(backgroundGrid, 2);

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
            Grid.SetRowSpan(toastOverlay, 2);

            SetSidebarVisible(false);
            return mainGrid;
        }

        public Grid GetContentGrid()
        {
            contentGrid = new();
            contentGrid.Children.Add(GetLibraryPanel());
            return contentGrid;
        }

        public ScrollViewer GetLibraryPanel()
        {
            libraryPanel = new();
            libraryPanel.onPluginClicked += OnPluginClicked;
            libraryScrollViewer = new()
            {
                Margin = new Thickness(0),
                Content = libraryPanel
            };

            return libraryScrollViewer;
        }

        public SidebarOverlay GetSidebarOverlay()
        {
            sidebarOverlay = new()
            {
                Width = 280
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
            sidebarOverlay.ShowGameSidebar(plugin);
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
            if (libraryScrollViewer == null) return;
            selectedGame = plugin;
            libraryScrollViewer.Content = activeTab as Control;
            UpdateBackground(plugin);
        }

        public void OnDeploymentStatusChanged(PluginData plugin) => Dispatcher.UIThread.Post(() => TryUpdateDeploymentStatus(plugin));

        #endregion
    }
}