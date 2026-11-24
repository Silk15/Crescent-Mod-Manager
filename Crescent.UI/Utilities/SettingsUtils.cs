using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Crescent.UI.Controls;
using Crescent.Utilities;
using Silk.Utilities;

namespace Crescent.UI.Utilities
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
                CornerRadius = new CornerRadius(4),
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

        public static Grid GetSliderRow(string title, string description, float min, float max, float initialValue, Func<float, Task> onValueChanged)
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

            StackPanel sliderStack = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock valueBlock = new()
            {
                Text = initialValue.ToString("0.##"),
                FontSize = 12,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 36,
                TextAlignment = TextAlignment.Right
            };

            Slider slider = new()
            {
                Minimum = min,
                Maximum = max,
                Value = initialValue,
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center
            };

            slider.PropertyChanged += async (_, changeArgs) =>
            {
                if (changeArgs.Property == Slider.ValueProperty)
                {
                    float currentValue = (float)slider.Value;
                    valueBlock.Text = currentValue.ToString("0.##");
                    await onValueChanged(currentValue);
                }
            };

            sliderStack.Children.Add(valueBlock);
            sliderStack.Children.Add(slider);

            Grid.SetColumn(textStack, 0);
            Grid.SetColumn(sliderStack, 1);

            rowGrid.Children.Add(textStack);
            rowGrid.Children.Add(sliderStack);
            return rowGrid;
        }

        public static Grid GetIntSliderRow(string title, string description, int min, int max, int initialValue, Func<int, Task> onValueChanged)
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

            StackPanel sliderStack = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock valueBlock = new()
            {
                Text = initialValue.ToString(),
                FontSize = 12,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 36,
                TextAlignment = TextAlignment.Right
            };

            Slider slider = new()
            {
                Minimum = min,
                Maximum = max,
                Value = initialValue,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center
            };

            slider.PropertyChanged += async (_, changeArgs) =>
            {
                if (changeArgs.Property == Slider.ValueProperty)
                {
                    int currentValue = (int)Math.Round(slider.Value);
                    valueBlock.Text = currentValue.ToString();
                    await onValueChanged(currentValue);
                }
            };

            sliderStack.Children.Add(valueBlock);
            sliderStack.Children.Add(slider);

            Grid.SetColumn(textStack, 0);
            Grid.SetColumn(sliderStack, 1);

            rowGrid.Children.Add(textStack);
            rowGrid.Children.Add(sliderStack);
            return rowGrid;
        }

        public static Grid GetEnumRow<T>(string title, string description, T selectedValue, Func<T, Task> onValueChanged) where T : struct, Enum
        {
            List<T> enumValues = Enum.GetValues<T>().ToList();
            return GetSelectionRow(title, description, enumValues, selectedValue, onValueChanged);
        }

        public static Grid GetCopyTextRow(Control owner, string title, string description, string valueText)
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

            StackPanel rightStack = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            CrescentTextBox valueBox = new()
            {
                Text = valueText,
                IsReadOnly = true,
                Width = 180,
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center
            };

            CrescentButton copyButton = new(ButtonStyle.Translucent)
            {
                Content = "Copy",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 90,
                Height = 36
            };

            copyButton.onClick += async () =>
            {
                TopLevel? topLevel = TopLevel.GetTopLevel(owner);
                if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync(valueText);
            };

            rightStack.Children.Add(valueBox);
            rightStack.Children.Add(copyButton);

            Grid.SetColumn(textStack, 0);
            Grid.SetColumn(rightStack, 1);

            rowGrid.Children.Add(textStack);
            rowGrid.Children.Add(rightStack);
            return rowGrid;
        }
        
        public static Grid GetSelectionRow<T>(string title, string description, List<T> items, T selectedItem, Func<T, Task> onSelectionChanged)
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

            CrescentComboBox comboBox = new()
            {
                ItemsSource = items,
                SelectedItem = selectedItem,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 140
            };

            comboBox.SelectionChanged += async (_, _) =>
            {
                if (comboBox.SelectedItem is T selected)
                    await onSelectionChanged(selected);
            };

            Grid.SetColumn(textStack, 0);
            Grid.SetColumn(comboBox, 1);

            rowGrid.Children.Add(textStack);
            rowGrid.Children.Add(comboBox);
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

            CrescentTextBox CrescentTextBox = GetCrescentTextBox(currentPath, onPathChanged);
            Grid.SetColumn(CrescentTextBox, 0);

            CrescentButton browseButton = GetBrowseButton(owner, isFile, CrescentTextBox, onPathChanged);
            Grid.SetColumn(browseButton, 1);

            grid.Children.Add(CrescentTextBox);
            grid.Children.Add(browseButton);
            panel.Children.Add(grid);

            TextBlock? hintBlock = GetHint(targetObject, propertyName);
            if (hintBlock != null) panel.Children.Add(hintBlock);
            return panel;
        }

        public static CrescentTextBox GetCrescentTextBox(string currentPath, Func<string, Task> onPathChanged)
        {
            CrescentTextBox CrescentTextBox = new()
            {
                Text = currentPath,
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center
            };

            CrescentTextBox.LostFocus += async (_, _) => await onPathChanged(CrescentTextBox.Text?.Trim() ?? string.Empty);
            return CrescentTextBox;
        }

        public static CrescentButton GetBrowseButton(Control owner, bool isFile, CrescentTextBox CrescentTextBox, Func<string, Task> onPathChanged)
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
                        CrescentTextBox.Text = path;
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
                        CrescentTextBox.Text = path;
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