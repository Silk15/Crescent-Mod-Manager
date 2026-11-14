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
    public static class SettingsUtils
    {
        public static StackPanel GetHeader(string title)
        {
            StackPanel headerStack = new()
            {
                Spacing = 8
            };

            headerStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            });

            headerStack.Children.Add(new Separator
            {
                Height = 1,
                Background = Colors.Separator,
                Margin = new Thickness(0, 6, 0, 0)
            });

            return headerStack;
        }

        public static StackPanel GetSectionContainer(string title, out Border border)
        {
            StackPanel stack = new()
            {
                Spacing = 14
            };

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            });

            border = new()
            {
                Background = Colors.Overlay,
                BorderBrush = Colors.Separator,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20)
            };

            border.Child = stack;
            return stack;
        }

        public static Grid GetToggleRow(string title, string description, bool initialValue, Func<bool, Task> onToggled)
        {
            Grid rowGrid = new();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            StackPanel textStack = new()
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            });

            textStack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            });

            ToggleSwitch toggle = new()
            {
                IsChecked = initialValue,
                OffContent = string.Empty,
                OnContent = string.Empty,
                VerticalAlignment = VerticalAlignment.Center
            };

            toggle.IsCheckedChanged += async (_, _) => await onToggled(toggle.IsChecked ?? false);

            Grid.SetColumn(textStack, 0);
            Grid.SetColumn(toggle, 1);

            rowGrid.Children.Add(textStack);
            rowGrid.Children.Add(toggle);
            return rowGrid;
        }

        public static StackPanel GetPathRow(Control owner, object targetObject, string labelText, string currentPath, string propertyName, Func<string, Task> onPathChanged, bool isFile)
        {
            StackPanel panel = new()
            {
                Spacing = 6
            };

            panel.Children.Add(new TextBlock
            {
                Text = labelText,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            });

            Grid grid = new()
            {
                ColumnSpacing = 10
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBox textBox = GetTextBox(currentPath, onPathChanged);
            Grid.SetColumn(textBox, 0);

            CrescentButton browseButton = GetBrowseButton(owner, isFile, textBox, onPathChanged);
            Grid.SetColumn(browseButton, 1);

            grid.Children.Add(textBox);
            grid.Children.Add(browseButton);
            panel.Children.Add(grid);

            TextBlock? hintBlock = GetHint(targetObject, propertyName);
            if (hintBlock != null) panel.Children.Add(hintBlock);
            return panel;
        }

        public static TextBox GetTextBox(string currentPath, Func<string, Task> onPathChanged)
        {
            TextBox textBox = new()
            {
                Text = currentPath,
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center
            };

            textBox.LostFocus += async (_, _) => await onPathChanged(textBox.Text?.Trim() ?? string.Empty);
            return textBox;
        }

        public static CrescentButton GetBrowseButton(Control owner, bool isFile, TextBox textBox, Func<string, Task> onPathChanged)
        {
            CrescentButton browseButton = new(ButtonStyle.Translucent)
            {
                Content = "Browse...",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 110,
                Height = 36
            };

            browseButton.onClick += async () =>
            {
                Window? window = TopLevel.GetTopLevel(owner) as Window;
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

        public static TextBlock? GetHint(object targetObject, string propertyName)
        {
            PropertyInfo? property = targetObject.GetType().GetProperty(propertyName);
            HintAttribute? hintAttribute = property != null ? Attribute.GetCustomAttribute(property, typeof(HintAttribute)) as HintAttribute : null;
            if (hintAttribute == null) return null;

            Color hintColor = Colors.Separator.Color;
            try
            {
                hintColor = Color.Parse(hintAttribute.TextHex);
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
            }

            return new()
            {
                Text = hintAttribute.Hint,
                FontSize = 12,
                Foreground = new SolidColorBrush(hintColor),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };
        }
    }
}