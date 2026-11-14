using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.APIs.Discord;
using Crescent.Utilities;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class PlayTab : Grid, ISidebarTab
    {
        public TextBlock? consoleOutput;
        public ScrollViewer? consoleScrollViewer;
        public TextBlock? statusBadge;
        public TextBlock? playtimeBlock;
        public CrescentButton? launchButton;
        public CrescentButton? closeButton;
        public CrescentButton? forceKillButton;
        public PluginData? pluginData;

        public SidebarOverlay? sidebarOverlay;
        public Stopwatch stopwatch = new();

        private List<string> consoleHistory = new();
        private FileSystemWatcher? logWatcher;
        private DispatcherTimer? sessionTimer;
        private Process? activeProcess;
        private string currentSearchFilter = string.Empty;

        public int Order => 0;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Play",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            try
            {
                Children.Clear();
                RowDefinitions.Clear();
                this.pluginData = pluginData;
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

                Grid headerRow = new();
                headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                TextBlock titleBlock = new()
                {
                    Text = "Console",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };

                SetColumn(titleBlock, 0);
                statusBadge = new()
                {
                    Text = IsGameRunning() ? "● Running" : "○ Idle",
                    Foreground = IsGameRunning() ? Colors.Green : Brushes.Gray,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(14, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                SetColumn(statusBadge, 1);
                playtimeBlock = new()
                {
                    Text = GetPlaytime(0),
                    Foreground = Brushes.LightGray,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                SetColumn(playtimeBlock, 3);
                headerRow.Children.Add(titleBlock);
                headerRow.Children.Add(statusBadge);
                headerRow.Children.Add(playtimeBlock);

                mainPanel.Children.Add(headerRow);
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

                RefreshConsole();
                UpdateProcessButtons();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        public void OnClick()
        {
            if (pluginData == null) return;
            if (string.IsNullOrEmpty(pluginData.ExePath) || !File.Exists(pluginData.ExePath))
            {
                ToastManager.Show("Executable Not Set", $"Set the executable path for {pluginData.Name} in Settings first!", ToastType.Warning);
                return;
            }

            if (!IsGameRunning())
                TryOpenGame();
        }

        public Grid GetButtonsGrid()
        {
            Grid controlsGrid = new()
            {
                ColumnSpacing = 8
            };
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition(280, GridUnitType.Pixel));
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBox searchBox = GetConsoleSearchBox();
            controlsGrid.Children.Add(searchBox);
            SetColumn(searchBox, 0);

            StackPanel actionButtons = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            launchButton = new(Colors.Green)
            {
                Content = $"Launch {pluginData!.Name}",
                Padding = new Thickness(12, 0),
                Height = 35
            };

            launchButton.onClick += () => TryOpenGame();
            closeButton = GetCloseButton();

            forceKillButton = new(Colors.Red)
            {
                Content = "Force Stop",
                Width = 100,
                Height = 35
            };

            forceKillButton.onClick += () => TryKillGame();
            CrescentButton clearButton = new(ButtonStyle.Translucent)
            {
                Content = "Clear",
                Width = 75,
                Height = 35
            };

            clearButton.onClick += () =>
            {
                consoleHistory.Clear();
                RefreshConsole();
            };

            CrescentButton copyButton = new(ButtonStyle.Translucent)
            {
                Content = "Copy",
                Width = 75,
                Height = 35
            };

            copyButton.onClick += async () =>
            {
                TopLevel? topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null && consoleOutput != null)
                {
                    await topLevel.Clipboard.SetTextAsync(consoleOutput.Text ?? string.Empty);
                    ToastManager.Show("Console Copied", "Copied console output to clipboard", ToastType.Info, 2500);
                }
            };

            actionButtons.Children.Add(launchButton);
            actionButtons.Children.Add(closeButton);
            actionButtons.Children.Add(forceKillButton);
            actionButtons.Children.Add(clearButton);
            actionButtons.Children.Add(copyButton);

            SetColumn(actionButtons, 2);
            controlsGrid.Children.Add(actionButtons);
            return controlsGrid;
        }

        #region Console

        public Border GetConsoleBorder()
        {
            consoleOutput = new()
            {
                Foreground = Brushes.LightGray,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            consoleScrollViewer = new()
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
                Text = currentSearchFilter,
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center
            };

            searchBox.TextChanged += (_, _) =>
            {
                currentSearchFilter = searchBox.Text?.Trim() ?? string.Empty;
                RefreshConsole();
            };

            return searchBox;
        }

        public void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string timestampPrefix = DateTime.Now.ToString("HH:mm:ss");
            string[] lines = message.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Dispatcher.UIThread.Post(() =>
            {
                for (int index = 0; index < lines.Length; index++)
                {
                    string formattedLine = $"{timestampPrefix} : {lines[index]}";
                    consoleHistory.Add(formattedLine);
                }

                RefreshConsole();
            });
        }

        private void RefreshConsole()
        {
            if (consoleOutput == null) return;
            if (string.IsNullOrEmpty(currentSearchFilter)) consoleOutput.Text = string.Join("\n", consoleHistory);
            else
            {
                List<string> matchingLines = consoleHistory.Where(line => line.Contains(currentSearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                consoleOutput.Text = string.Join("\n", matchingLines);
            }

            if (Settings.Instance.autoScrollConsole) consoleScrollViewer?.ScrollToEnd();
        }

        #endregion

        #region Game Management

        public CrescentButton GetCloseButton()
        {
            CrescentButton button = new(ButtonStyle.Translucent)
            {
                Content = $"Close {pluginData!.Name}",
                Padding = new Thickness(12, 0),
                Height = 35
            };

            button.onClick += () => TryExitGame();
            return button;
        }

        private bool IsGameRunning()
        {
            if (activeProcess == null) return false;
            try
            {
                return !activeProcess.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateProcessButtons()
        {
            bool running = IsGameRunning();
            if (launchButton != null) launchButton.IsEnabled = !running;
            if (closeButton != null) closeButton.IsEnabled = running;
            if (forceKillButton != null) forceKillButton.IsEnabled = running;
            if (statusBadge != null)
            {
                statusBadge.Text = running ? "● Running" : "○ Idle";
                statusBadge.Foreground = running ? Colors.Green : Brushes.Gray;
            }
        }

        private string GetPlaytime(long activeSessionSeconds)
        {
            long totalSeconds = (pluginData?.playTime ?? 0) + activeSessionSeconds;
            TimeSpan totalSpan = TimeSpan.FromSeconds(totalSeconds);
            TimeSpan sessionSpan = TimeSpan.FromSeconds(activeSessionSeconds);
            return $"Session: {sessionSpan:hh\\:mm\\:ss} • Total: {(int)totalSpan.TotalHours}h {totalSpan.Minutes}m";
        }

        private void StartSessionTimer()
        {
            sessionTimer?.Stop();
            sessionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            sessionTimer.Tick += (_, _) =>
            {
                if (playtimeBlock != null)
                    playtimeBlock.Text = GetPlaytime(stopwatch.ElapsedMilliseconds / 1000);
                UpdateProcessButtons();
            };
            sessionTimer.Start();
        }

        public void TryOpenGame()
        {
            if (IsGameRunning() || pluginData == null) return;
            if (string.IsNullOrEmpty(pluginData.ExePath) || !File.Exists(pluginData.ExePath))
            {
                ToastManager.Show("Executable Not Set", $"Set the executable path for {pluginData.Name} in Settings first!", ToastType.Warning);
                return;
            }

            consoleHistory.Clear();
            RefreshConsole();

            StringBuilder gameBuilder = new();
            List<ModData> enabledMods = pluginData.installedMods.Where(mod => pluginData.SelectedProfile.IsModInstalled(mod) && pluginData.SelectedProfile.IsModEnabled(mod)).OrderBy(mod => mod.loadOrder).ToList();

            gameBuilder.AppendLine($"Opening {pluginData.Name} [Profile: {pluginData.SelectedProfile.name} | Enabled Mods: {enabledMods.Count}]");
            for (int index = 0; index < enabledMods.Count; index++)
            {
                ModData modData = enabledMods[index];
                string load = ("(" + modData.loadOrder + ")").PadRight(4);
                gameBuilder.AppendLine($"{load}{modData.DisplayName.Cleaned().WithoutSpaces()}");
            }

            Log(gameBuilder.ToString());
            try
            {
                ProcessStartInfo startInfo = new(pluginData.ExePath)
                {
                    WorkingDirectory = Path.GetDirectoryName(pluginData.ExePath) ?? string.Empty,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };

                activeProcess = Process.Start(startInfo);
                if (activeProcess == null) return;

                activeProcess.EnableRaisingEvents = true;
                activeProcess.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        Log(args.Data);
                };

                activeProcess.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        Log($"[ERROR] {args.Data}");
                };

                activeProcess.BeginOutputReadLine();
                activeProcess.BeginErrorReadLine();
                activeProcess.Exited += OnGameExit;

                DiscordManager.SetPlaying(pluginData);
                Log($"{activeProcess.ProcessName} started!");
                ToastManager.Show("Game Launched", $"Started {pluginData.Name}!", ToastType.Success, 3000);

                stopwatch = Stopwatch.StartNew();
                StartSessionTimer();
                UpdateProcessButtons();

                Runtime.onClosed -= OnRuntimeClosed;
                Runtime.onClosed += OnRuntimeClosed;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                Log($"Failed to start process: \n{ex.Message}!");
            }
        }

        public void TryExitGame()
        {
            if (!IsGameRunning() || activeProcess == null) return;
            try
            {
                Log($"Requesting {activeProcess.ProcessName} to close...");
                if (!activeProcess.CloseMainWindow()) activeProcess.Kill();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        public void TryKillGame()
        {
            if (!IsGameRunning() || activeProcess == null) return;
            try
            {
                string processName = activeProcess.ProcessName;
                activeProcess.Kill(true);
                Log($"Force terminated {processName}!");
            }
            catch (Exception ex)
            {
                Logger.Error($"Murder failed :(\n{ex}");
            }
        }

        private void OnGameExit(object? sender, EventArgs evt)
        {
            try
            {
                stopwatch.Stop();
                sessionTimer?.Stop();

                long elapsedSeconds = stopwatch.ElapsedTicks / Stopwatch.Frequency;
                if (pluginData != null)
                {
                    DiscordManager.SetModding(pluginData);
                    pluginData.playTime += elapsedSeconds;
                    _ = pluginData.SaveAsync();
                }

                string exitedName = activeProcess?.ProcessName ?? pluginData?.Name ?? "Game";
                Log($"{exitedName} exited after {TimeSpan.FromSeconds(elapsedSeconds):hh\\:mm\\:ss}.");

                activeProcess?.Dispose();
                activeProcess = null;
                Runtime.onClosed -= OnRuntimeClosed;

                Dispatcher.UIThread.Post(() =>
                {
                    if (playtimeBlock != null) playtimeBlock.Text = GetPlaytime(0);
                    UpdateProcessButtons();
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private void OnRuntimeClosed()
        {
            if (pluginData == null || !stopwatch.IsRunning) return;
            stopwatch.Stop();
            pluginData.playTime += stopwatch.ElapsedTicks / Stopwatch.Frequency;
            Task.Run(async () => await pluginData.SaveAsync());
        }

        #endregion
    }
}