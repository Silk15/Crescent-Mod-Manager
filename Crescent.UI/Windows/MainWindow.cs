using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
        public ToolbarOverlay? toolbarOverlay;
        public LibraryPanel? libraryPanel;

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

        public void ShowLibrary()
        {
            if (libraryScrollViewer == null || libraryPanel == null) return;
            libraryPanel.onPluginClicked -= OnPluginClicked;
            libraryPanel = new();
            libraryScrollViewer.Content = libraryPanel;
            libraryPanel.onPluginClicked += OnPluginClicked;
            UpdateBackground();
            if (sidebarOverlay == null)
            {
                Logger.Warn("No sidebar to update :(");
                return;
            }

            sidebarOverlay.IsVisible = false;
        }

        public void TryUpdateDeploymentStatus(PluginData plugin)
        {
            if (selectedGame != plugin || sidebarOverlay == null) return;
            sidebarOverlay.UpdateDeploymentStatus(plugin);
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
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition(280, GridUnitType.Pixel));

            bodyGrid.Children.Add(GetContentGrid());
            Grid.SetColumn(contentGrid!, 0);

            bodyGrid.Children.Add(GetSidebarOverlay());
            Grid.SetColumn(sidebarOverlay!, 1);

            OverlaySeparator overlaySeparator = new(OverlaySeparator.Mode.Vertical);
            sidebarOverlay!.overlaySeparator = overlaySeparator;
            bodyGrid.Children.Add(overlaySeparator);
            Grid.SetColumn(overlaySeparator, 1);

            mainGrid.Children.Add(bodyGrid);
            Grid.SetRow(bodyGrid, 1);

            sidebarOverlay.IsVisible = false;
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
                Content = libraryPanel,
            };

            return libraryScrollViewer;
        }

        public SidebarOverlay GetSidebarOverlay()
        {
            sidebarOverlay = new();
            sidebarOverlay.onGameSelected += OnGameSelected;
            sidebarOverlay.onTabOpened += OnTabOpened;
            return sidebarOverlay;
        }

        #endregion


        #region Events

        private void OnPluginClicked(PluginData plugin)
        {
            selectedGame = plugin;
            if (sidebarOverlay == null)
            {
                Logger.Warn("No sidebar to update :(");
                return;
            }

            sidebarOverlay.IsVisible = true;
            sidebarOverlay.ShowGameSidebar(plugin);
            sidebarOverlay.OpenTab<ModsTab>();
        }

        private void OnGameSelected(PluginData plugin)
        {
            selectedGame = plugin;
            if (subscribedGame != null) subscribedGame.onDeploymentStatusChanged -= OnDeploymentStatusChanged;
            subscribedGame = plugin;
            subscribedGame.onDeploymentStatusChanged += OnDeploymentStatusChanged;
            TryUpdateDeploymentStatus(plugin);
        }

        private void OnTabOpened(PluginData plugin, ISidebarTab activeTab)
        {
            if (libraryScrollViewer == null) return;
            selectedGame = plugin;
            libraryScrollViewer.Content = activeTab as Control;
            UpdateBackground(plugin);
        }

        private void OnDeploymentStatusChanged(PluginData plugin) => Avalonia.Threading.Dispatcher.UIThread.Post(() => TryUpdateDeploymentStatus(plugin));

        #endregion
    }
}