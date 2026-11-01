using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

        public GameData? selectedGame;
        public GameData? subscribedGame;

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
                Background = GetBackgroundGradient();
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

        public Grid GetMainGrid()
        {
            mainGrid = new();
            mainGrid.RowDefinitions.Add(new RowDefinition(50, GridUnitType.Pixel));
            mainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

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

        #region Content

        public Grid GetContentGrid()
        {
            contentGrid = new();
            contentGrid.Children.Add(GetLibraryPanel());
            return contentGrid;
        }

        public ScrollViewer GetLibraryPanel()
        {
            libraryPanel = new();
            libraryPanel.onGameClicked += OnGameClicked;
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

        public void ShowLibrary()
        {
            if (libraryScrollViewer == null) return;
            libraryScrollViewer.Content = libraryPanel;
            Background = GetBackgroundGradient();
            if (sidebarOverlay == null)
            {
                Logger.Warn("No sidebar to update :(");
                return;
            }
            
            sidebarOverlay.IsVisible = false;
        }

        public void TryUpdateDeploymentStatus(GameData game)
        {
            if (selectedGame != game || sidebarOverlay == null) return;
            sidebarOverlay.UpdateDeploymentStatus(game);
        }

        public IBrush GetBackgroundGradient(GameData? configuredGame = null)
        {
            if (configuredGame != null)
            {
                Color baseColor = Color.Parse(configuredGame.Theme);
                return new RadialGradientBrush
                {
                    Center = new RelativePoint(0.5, 0.00, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.5, 0.0, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(1, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.8, RelativeUnit.Relative),
                    GradientStops = new()
                    {
                        new GradientStop(Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B), 0.0),
                        new GradientStop(Colors.MainPanelEnd.Color, 1.0)
                    }
                };
            }

            return Colors.MainGradient;
        }

        private void OnGameClicked(GameData game)
        {
            selectedGame = game;
            if (sidebarOverlay == null)
            {
                Logger.Warn("No sidebar to update :(");
                return;
            }
            
            sidebarOverlay.IsVisible = true;
            sidebarOverlay.ShowGameSidebar(game);
            sidebarOverlay.OpenTab<ModsTab>();
        }

        private void OnGameSelected(GameData game)
        {
            selectedGame = game;
            if (subscribedGame != null) subscribedGame.onDeploymentStatusChanged -= OnDeploymentStatusChanged;
            subscribedGame = game;
            subscribedGame.onDeploymentStatusChanged += OnDeploymentStatusChanged;
            TryUpdateDeploymentStatus(game);
        }

        private void OnTabOpened(GameData game, ISidebarTab activeTab)
        {
            if (libraryScrollViewer == null) return;
            selectedGame = game;
            libraryScrollViewer.Content = activeTab as Control;
            Background = GetBackgroundGradient(game);
        }

        private void OnDeploymentStatusChanged(GameData game) => Avalonia.Threading.Dispatcher.UIThread.Post(() => TryUpdateDeploymentStatus(game));
    }
}