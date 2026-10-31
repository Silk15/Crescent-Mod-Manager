using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public class SettingsTab : StackPanel, ISidebarTab
    {
        public GameData? gameData;
        public SidebarOverlay? sidebarOverlay;
        
        public Tab Tab => Tab.Settings;
        public int Order => 3;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Settings",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, GameData gameData)
        {
            this.gameData = gameData;
            this.sidebarOverlay = sidebarOverlay;
            Refresh();
        }

        public void OnClick() => Refresh();

        public void Refresh()
        {
            Children.Clear();
            Margin = new Thickness(30, 15, 30, 30);
            Spacing = 15;
            Background = Brushes.Transparent;
            
            StackPanel mainPanel = new()
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = ISidebarTab.TopBarHeight,
                Spacing = 10
            };
            
            TextBlock titleBlock = new()
            {
                Text = "Settings",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                
                VerticalAlignment = VerticalAlignment.Center,
            };

            mainPanel.Children.Add(titleBlock);
            mainPanel.Children.Add(new Separator
            {
                Height = 1,
                Background = Colors.Separator,
                VerticalAlignment = VerticalAlignment.Bottom
            });

            Children.Add(mainPanel);
            Children.Add(CreatePathSetting("Game Executable Path:", gameData!.ExePath, nameof(gameData.ExePath), async path =>
            {
                gameData.ExePath = path;
                await gameData.SaveAsync();
            }, true));

            Children.Add(CreatePathSetting("Mod Staging Folder:", gameData.StagingPath, nameof(gameData.StagingPath), async path =>
            {
                gameData.StagingPath = path;
                await gameData.SaveAsync();
            }, false));

            Children.Add(CreatePathSetting("Target Folder:", gameData.DeploymentPath, nameof(gameData.DeploymentPath), async path =>
            {
                gameData.DeploymentPath = path;
                await gameData.SaveAsync();
            }, false));
        }

        #region Paths

        private StackPanel CreatePathSetting(string labelText, string currentPath, string propertyName, Func<string, Task> onPathChanged, bool isFile)
        {
            StackPanel panel = new()
            {
                Spacing = 5
            };

            panel.Children.Add(new TextBlock
            {
                Text = labelText,
                FontWeight = FontWeight.SemiBold
            });

            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBox textBox = GetTextBox(currentPath);
            Grid.SetColumn(textBox, 0);
            
            CrescentButton browseButton = GetBrowseButton(isFile, textBox, onPathChanged);
            Grid.SetColumn(browseButton, 1);
            
            grid.Children.Add(textBox);
            grid.Children.Add(browseButton);
            panel.Children.Add(grid);

            TextBlock? hintBlock = GetHint(propertyName);
            if (hintBlock != null) panel.Children.Add(hintBlock);
            return panel;
        }

        public TextBox GetTextBox(string currentPath)
        {
            return new()
            {
                Text = currentPath,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
        }

        public CrescentButton GetBrowseButton(bool isFile, TextBox textBox, Func<string, Task> onPathChanged)
        {
            CrescentButton browseButton = new(ButtonStyle.Translucent)
            {
                Content = "Browse...",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 90
            };

            Grid.SetColumn(browseButton, 1);
            browseButton.onClick += async () =>
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
            
            return browseButton;
        }

        public TextBlock? GetHint(string propertyName)
        {
            PropertyInfo? property = gameData.GetType().GetProperty(propertyName);
            HintAttribute? hintAttribute = property != null ? Attribute.GetCustomAttribute(property, typeof(HintAttribute)) as HintAttribute : null;
            if (hintAttribute != null)
            {
                Color hintColor = Colors.Separator.Color;
                try
                {
                    hintColor = Color.Parse(hintAttribute.TextHex);
                }
                catch (Exception exception)
                {
                    Logger.Log(exception.ToString());
                }

                return new TextBlock
                {
                    Text = hintAttribute.Hint,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(hintColor),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                };
            }
            
            return null;
        }
        
        #endregion
    }
}