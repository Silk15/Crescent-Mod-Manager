using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Crescent.Plugins;
using Crescent.Utilities;
using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public class Settings : StackPanel
    {
        public Settings(GameData game)
        {
            Margin = new Thickness(30, 25, 30, 30);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Top;
            Spacing = 15;

            TextBlock titleBlock = new()
            {
                Text = string.Concat(game.Name, " - Settings"),
                FontSize = 28,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };
            
            Children.Add(titleBlock);
            TextBlock infoBlock = new()
            {
                Text = "Configure folder locations for game executables, mod deployment folders and mod storage. Symlinks are used by default to cut down on file pollution and allow mods to be easily withdrawn.",
                FontSize = 13,
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap
            };
            
            Children.Add(infoBlock);
            Children.Add(new Separator
            {
                Height = 2,
                Background = Colors.Separator
            });

            Children.Add(CreatePathSetting(game, "Game Executable Path:", game.ExePath, nameof(game.ExePath), async path =>
            {
                game.ExePath = path;
                await game.SaveAsync();
            }, true));

            Children.Add(CreatePathSetting(game, "Mod Staging Folder:", game.StagingPath, nameof(game.StagingPath), async path =>
            {
                game.StagingPath = path;
                await game.SaveAsync();
            }, false));

            Children.Add(CreatePathSetting(game, "Target Folder:", game.DeploymentPath, nameof(game.DeploymentPath), async path =>
            {
                game.DeploymentPath = path;
                await game.SaveAsync();
            }, false));
        }

        private StackPanel CreatePathSetting(GameData game, string labelText, string currentPath, string propertyName, Func<string, Task> onPathChanged, bool isFile)
        {
            StackPanel panel = new()
            {
                Spacing = 5
            };

            panel.Children.Add(new TextBlock
            {
                Text = labelText,
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold
            });

            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBox textBox = new()
            {
                Text = currentPath,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Grid.SetColumn(textBox, 0);
            Button browseButton = new()
            {
                Content = "Browse...",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 90,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(browseButton, 1);
            browseButton.Click += async (sender, routedEventArgs) =>
            {
                Window? window = TopLevel.GetTopLevel(this) as Window;
                if (window == null) return;

                IStorageProvider storageProvider = window.StorageProvider;
                if (isFile)
                {
                    IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select Game Executable",
                        AllowMultiple = false
                    });

                    if (files.Count > 0)
                    {
                        string path = files[0].Path.LocalPath;
                        textBox.Text = path;
                        await onPathChanged(path);
                    }
                }
                else
                {
                    IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select Target Folder",
                        AllowMultiple = false
                    });

                    if (folders.Count > 0)
                    {
                        string path = folders[0].Path.LocalPath;
                        textBox.Text = path;
                        await onPathChanged(path);
                    }
                }
            };

            grid.Children.Add(textBox); 
            grid.Children.Add(browseButton);
            panel.Children.Add(grid);

            PropertyInfo? property = game.GetType().GetProperty(propertyName);
            HintAttribute? hintAttribute = property != null ? Attribute.GetCustomAttribute(property, typeof(HintAttribute)) as HintAttribute : null;
            if (hintAttribute != null)
            {
                Color hintColor = Colors.Separator.Color;
                try
                {
                    hintColor = Color.Parse(hintAttribute.TextHex);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.ToString());
                }

                panel.Children.Add(new TextBlock
                {
                    Text = hintAttribute.Hint,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(hintColor),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            return panel;
        }
    }
}