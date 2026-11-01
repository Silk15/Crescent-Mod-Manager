using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Utilities;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class PlayTab : Grid, ISidebarTab
    {
        public TextBlock? consoleOutput;
        public GameData? gameData;
        public SidebarOverlay? sidebarOverlay;
        public Stopwatch stopwatch;

        private Process? activeProcess;

        public Tab Tab => Tab.Play;
        public int Order => 0;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Play",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, GameData gameData)
        {
            try
            {
                Children.Clear();
                this.gameData = gameData;
                this.sidebarOverlay = sidebarOverlay;

                Margin = new Thickness(30, 15, 30, 30);
                Background = Brushes.Transparent;

                RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star)); 
                StackPanel mainPanel = new()
                {
                    Spacing = 10,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                TextBlock titleBlock = new()
                {
                    Text = "Console",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    
                    VerticalAlignment = VerticalAlignment.Center,
                };

                mainPanel.Children.Add(titleBlock);
                mainPanel.Children.Add(GetButtonsGrid());
                mainPanel.Children.Add(new Separator
                {
                    Height = 1,
                    Background = Colors.Separator,
                    VerticalAlignment = VerticalAlignment.Bottom
                });

                SetRow(mainPanel, 0);
                Children.Add(mainPanel);

                Border consoleBorder = GetConsoleBorder();
                SetRow(consoleBorder, 1);
                Children.Add(consoleBorder);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
        
        public void OnClick()
        {
            if (string.IsNullOrEmpty(gameData!.ExePath) || !File.Exists(gameData.ExePath)) return;
            TryOpenGame();
        }

        public Grid GetButtonsGrid()
        {
            Grid searchGrid = new();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(320, GridUnitType.Pixel));
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Auto));

            TextBox searchBox = GetConsoleSearchBox();
            searchGrid.Children.Add(searchBox);
            SetColumn(searchBox, 0);

            CrescentButton closeButton = GetCloseButton();
            searchGrid.Children.Add(closeButton);
            SetColumn(closeButton, 1);
            return searchGrid;
        }

        #region Console

        public Border GetConsoleBorder()
        {
            consoleOutput = new TextBlock
            {
                Foreground = Brushes.LightGray,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                TextWrapping = TextWrapping.Wrap
            };

            ScrollViewer consoleScrollViewer = new()
            {
                Content = consoleOutput,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            Border consoleBorder = new()
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(Colors.MainPanelStart.Color, 0),
                        new GradientStop(Colors.MainPanelEnd.Color, 1)
                    }
                },
                Child = consoleScrollViewer,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            
            return consoleBorder;
        }

        public TextBox GetConsoleSearchBox()
        {
            TextBox searchBox = new()
            {
                PlaceholderText = "Search console...",
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            
            return searchBox;
        }
        
        public void Log(string message)
        {
            if (consoleOutput == null) return;
            message = DateTime.Now.ToString("HH:mm:ss") + " : " + message;
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => consoleOutput.Text += message + "\n");
        }
        
        #endregion

        #region Game Management

        public CrescentButton GetCloseButton()
        {
            CrescentButton closeButton = new(ButtonStyle.Translucent)
            {
                Content = $"Close {gameData!.Name}",
                Padding = new(10, 0, 10, 0)
            };

            closeButton.onClick += () => TryExitGame();
            return closeButton;
        }

        public void TryOpenGame()
        {
            if (activeProcess != null)
            {
                try
                {
                    if (!activeProcess.HasExited) return;
                }
                catch
                {
                    return;
                }
            }
            
            if (consoleOutput != null) consoleOutput.Text = string.Empty;
            StringBuilder gameBuilder = new();
            if (gameData == null) return;

            gameBuilder.AppendLine($"Opening {gameData.Name}... [Profile = {gameData.SelectedProfile.name}]");
            foreach (ModData modData in gameData.installedMods)
            {
                if (gameData.SelectedProfile.IsModInstalled(modData) && gameData.SelectedProfile.IsModEnabled(modData)) 
                    gameBuilder.AppendLine($"• {modData.DisplayName.WithoutNumbers().Replace(" ", "")}");
            }
            Log(gameBuilder.ToString());
            try
            {
                activeProcess = Process.Start(new ProcessStartInfo(gameData.ExePath));
                if (activeProcess == null) return;
                
                Log($"{activeProcess.ProcessName} started!");

                activeProcess.EnableRaisingEvents = true;
                activeProcess.Exited += OnGameExit;
                stopwatch = Stopwatch.StartNew();
                Runtime.onClosed -= OnRuntimeClosed;
                Runtime.onClosed += OnRuntimeClosed;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        public void TryExitGame()
        {
            if (activeProcess == null) return;
            try
            {
                Log($"{activeProcess.ProcessName} exited!");
                activeProcess.Close();
                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private void OnGameExit(object? sender, EventArgs evt)
        {
            if (activeProcess == null) return;
            try
            {
                Log($"{activeProcess.ProcessName} exited!");
                activeProcess?.Dispose();
                activeProcess = null;
                
                gameData!.playTime += stopwatch.ElapsedTicks / Stopwatch.Frequency;
                Task.Run(async () => await gameData.SaveAsync());
                Runtime.onClosed -= OnRuntimeClosed;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
        
        private void OnRuntimeClosed()
        {
            gameData!.playTime += stopwatch.ElapsedTicks / Stopwatch.Frequency;
            Task.Run(async () => await gameData.SaveAsync());
        }
        
        #endregion
    }
}