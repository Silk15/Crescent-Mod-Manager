using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;

using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public class MainWindow : Window
    {
        public const string TitleText = "Crescent Mod Manager";
        public GameData? selectedGame;
        public GameData? subscribedGame;
        
        public ScrollViewer libraryScrollViewer;
        public Grid contentGrid;
        public Grid mainGrid;
        
        public Library library;
        public Sidebar sidebar;
        public Toolbar topToolbar;

        public MainWindow()
        {
            try
            {
                Title = TitleText;
                Width = 1400;
                Height = 900;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                WindowDecorations = WindowDecorations.Full;
                Background = GetBackgroundGradient();
                Foreground = Brushes.White;
                Content = Setup();
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

        public Grid Setup()
        {
            mainGrid = new();
            mainGrid.RowDefinitions.Add(new RowDefinition(50, GridUnitType.Pixel));
            mainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            topToolbar = new(this);
            mainGrid.Children.Add(topToolbar);
            Grid.SetRow(topToolbar, 0);

            Grid bodyGrid = new();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition(280, GridUnitType.Pixel));

            SetupContent();
            bodyGrid.Children.Add(contentGrid);
            Grid.SetColumn(contentGrid, 0);

            SetupSidebarPanel();
            bodyGrid.Children.Add(sidebar);
            Grid.SetColumn(sidebar, 1);

            Avalonia.Controls.Shapes.Rectangle leftBorder = new()
            {
                Width = 1,
                Fill = Colors.Separator,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            bodyGrid.Children.Add(leftBorder);
            Grid.SetColumn(leftBorder, 1);

            mainGrid.Children.Add(bodyGrid);
            Grid.SetRow(bodyGrid, 1);

            return mainGrid;
        }

        public void SetupContent()
        {
            contentGrid = new();
            SetupLibraryPanel();
            contentGrid.Children.Add(libraryScrollViewer);
        }

        public void SetupLibraryPanel()
        {
            libraryScrollViewer = new();
            library = new();
            libraryScrollViewer.Content = library;
            libraryScrollViewer.Margin = new Thickness(0);
            library.onGameClicked += OnGameClicked;
        }

        public void SetupSidebarPanel()
        {
            sidebar = new();
            sidebar.onGameSettingsClicked += OnGameSettingsClicked;
            sidebar.onModsTabClicked += OnModsTabClicked;
            sidebar.onGameSelected += OnGameSelected;
        }

        public void ShowLibrary()
        {
            libraryScrollViewer.Content = library;
            sidebar.ShowDefaultLibrarySidebar();
            Background = GetBackgroundGradient();
        }

        public void OnGameClicked(GameData game)
        {
            selectedGame = game;
            sidebar.ShowGameSidebar(game);
            OnModsTabClicked(game);
        }

        public void OnGameSelected(GameData game)
        {
            selectedGame = game;
            if (subscribedGame != null) subscribedGame.onDeploymentStatusChanged -= OnDeploymentStatusChanged;
            subscribedGame = game;
            subscribedGame.onDeploymentStatusChanged += OnDeploymentStatusChanged;
            CheckAndUpdateDeploymentStatus(game);
        }

        public void OnDeploymentStatusChanged(GameData game) => Avalonia.Threading.Dispatcher.UIThread.Post(() => CheckAndUpdateDeploymentStatus(game));
        
        public void CheckAndUpdateDeploymentStatus(GameData game)
        {
            if (selectedGame != game) return;
            sidebar.UpdateDeploymentStatus(game);
        }

        public void OnGameSettingsClicked(GameData game)
        {
            selectedGame = game;
            libraryScrollViewer.Content = new Settings(game);
        }

        public void OnModsTabClicked(GameData game)
        {
            selectedGame = game;
            sidebar.ShowModsList(game);
            if (sidebar.modsMainPanel != null)
            {
                libraryScrollViewer.Content = sidebar.modsMainPanel;
                Background = GetBackgroundGradient(game);
            }
        }

        public IBrush GetBackgroundGradient(GameData? configuredGame = null)
        {
            Color startColor = Colors.MainPanelStart.Color; 
            if (configuredGame != null)
            {
                Color baseColor = Color.Parse(configuredGame.Theme);
                startColor = Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B);
                
                return new RadialGradientBrush
                {
                    Center = new RelativePoint(0.5, 0.00, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.5, 0.0, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(1, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.8, RelativeUnit.Relative),
                    GradientStops = new()
                    {
                        new GradientStop(startColor, 0.0),
                        new GradientStop(Colors.MainPanelEnd.Color, 1.0)
                    }
                };
            }

            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(startColor, 0),
                    new GradientStop(Colors.MainPanelEnd.Color, 1)
                }
            };
        }
    }
}