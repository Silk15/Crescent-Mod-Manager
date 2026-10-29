using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Crescent.Plugins;
using Crescent.Utilities;
using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public class Mods : StackPanel
    {
        public GameData game;

        public Mods(GameData gameData)
        {
            game = gameData;
            Setup();
        }

        public void Setup()
        {
            Margin = new Thickness(30, 25, 30, 30);
            Spacing = 15;
            Background = Brushes.Transparent;
            DragDrop.SetAllowDrop(this, true);
            SetupModsDragDrop();

            Grid headerGrid = GetModsHeaderGrid();
            Children.Add(headerGrid);

            Grid filterControlGrid = GetFilterControlGrid(out TextBox searchBox, out ComboBox filterComboBox);
            Children.Add(filterControlGrid);

            Children.Add(new Separator
            {
                Height = 2,
                Background = Colors.Separator
            });

            if (string.IsNullOrEmpty(game.StagingPath) || !Directory.Exists(game.StagingPath))
            {
                TextBlock warningText = new()
                {
                    Text = "Staging directory is not configured or does not exist. Please set it under Settings.",
                    Foreground = Brushes.IndianRed,
                    FontWeight = FontWeight.Bold
                };
                Children.Add(warningText);
                return;
            }

            StackPanel listPanel = new()
            {
                Spacing = 8
            };

            ModsList.SetupModListRenderer(game, searchBox, filterComboBox, listPanel, this);
            Children.Add(listPanel);
        }

        public void SetupModsDragDrop()
        {
            AddHandler(DragDrop.DragEnterEvent, (sender, evt) =>
            {
                if (evt.DataTransfer.Contains(DataFormat.File)) evt.DragEffects = DragDropEffects.Move;
                else evt.DragEffects = DragDropEffects.None;
                evt.Handled = true;
            });

            AddHandler(DragDrop.DropEvent, (sender, evt) =>
            {
                if (evt.DataTransfer.Contains(DataFormat.File))
                {
                    IEnumerable<IStorageItem>? items = evt.DataTransfer.TryGetFiles();
                    if (items != null && !string.IsNullOrEmpty(game.StagingPath) && Directory.Exists(game.StagingPath))
                    {
                        foreach (IStorageItem item in items)
                        {
                            string sourcePath = item.Path.LocalPath;
                            string extension = Path.GetExtension(sourcePath).ToLower();
                            if (extension == ".zip" || extension == ".rar" || extension == ".7z")
                            {
                                string fileName = Path.GetFileName(sourcePath);
                                string destinationPath = Path.Combine(game.StagingPath, fileName);
                                if (!File.Exists(destinationPath)) File.Move(sourcePath, destinationPath);
                            }
                            else if (Directory.Exists(sourcePath))
                            {
                                string fileName = Path.GetFileName(sourcePath);
                                string destinationPath = Path.Combine(game.StagingPath, fileName);
                                if (!Directory.Exists(destinationPath)) Directory.Move(sourcePath, destinationPath);
                            }
                        }

                        RefreshPanel();
                    }
                }

                evt.Handled = true;
            });
        }

        public void RefreshPanel()
        {
            Children.Clear();
            Setup();
        }

        public Grid GetModsHeaderGrid()
        {
            Grid headerGrid = new();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock titleBlock = new()
            {
                Text = string.Concat(game.SelectedProfile.name, $" ({game.ModCount} Mod{(game.ModCount == 1 ? "" : "s")})"),
                FontSize = 28,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(titleBlock, 0);
            headerGrid.Children.Add(titleBlock);

            StackPanel actionButtonPanel = GetModsActionButtons();
            Grid.SetColumn(actionButtonPanel, 1);
            headerGrid.Children.Add(actionButtonPanel);

            return headerGrid;
        }

        public StackPanel GetModsActionButtons()
        {
            StackPanel actionButtonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            CrescentButton openStagingButton = new(ButtonStyle.Translucent)
            {
                Content = "Staging Folder",
                Width = 130,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };

            openStagingButton.Click += (sender, routedEventArgs) =>
            {
                if (!string.IsNullOrEmpty(game.StagingPath) && Directory.Exists(game.StagingPath))
                {
                    Process.Start(new ProcessStartInfo(game.StagingPath)
                    {
                        UseShellExecute = true
                    });
                }
            };

            CrescentButton openDeploymentButton = new(ButtonStyle.Translucent)
            {
                Content = "Mods Folder",
                Width = 120,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };

            openDeploymentButton.Click += (sender, routedEventArgs) =>
            {
                if (!string.IsNullOrEmpty(game.DeploymentPath) && Directory.Exists(game.DeploymentPath))
                {
                    Process.Start(new ProcessStartInfo(game.DeploymentPath)
                    {
                        UseShellExecute = true
                    });
                }
            };

            CrescentButton installModButton = new(ButtonStyle.Translucent)
            {
                Content = "Import Mod",
                Width = 120,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };

            installModButton.Click += async (sender, routedEventArgs) =>
            {
                Window? window = TopLevel.GetTopLevel(this) as Window;
                if (window == null) return;

                IStorageProvider storageProvider = window.StorageProvider;
                IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select mod file(s)",
                    AllowMultiple = true
                });

                foreach (IStorageItem file in files)
                {
                    if (!file.Name.IsArchive()) continue;
                    IStorageFolder? path = await storageProvider.TryGetFolderFromPathAsync(game.StagingPath);
                    if (path == null) continue;
                    await file.MoveAsync(path);
                }

                RefreshPanel();
            };

            actionButtonPanel.Children.Add(openStagingButton);
            actionButtonPanel.Children.Add(openDeploymentButton);
            actionButtonPanel.Children.Add(installModButton);

            return actionButtonPanel;
        }

        public Grid GetFilterControlGrid(out TextBox searchBox, out ComboBox filterComboBox)
        {
            Grid filterControlGrid = new();
            filterControlGrid.ColumnDefinitions.Add(new ColumnDefinition(320, GridUnitType.Pixel));
            filterControlGrid.ColumnDefinitions.Add(new ColumnDefinition(150, GridUnitType.Pixel));
            filterControlGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            searchBox = new()
            {
                PlaceholderText = "Search installed mods...",
                Height = 32,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Grid.SetColumn(searchBox, 0);
            filterControlGrid.Children.Add(searchBox);

            filterComboBox = new()
            {
                Height = 32,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            List<string> filters = game.GetModFilters();
            filterComboBox.ItemsSource = filters;
            filterComboBox.SelectedIndex = 0;
            Grid.SetColumn(filterComboBox, 1);
            filterControlGrid.Children.Add(filterComboBox);

            return filterControlGrid;
        }
    }
}