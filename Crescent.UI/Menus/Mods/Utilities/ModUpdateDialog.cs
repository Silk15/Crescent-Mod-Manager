using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.APIs.NexusMods;
using Crescent.Utilities;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class ModUpdateDialog : Window
    {
        public bool UpdateConfirmed { get; private set; }

        public ModUpdateDialog(ModData modData)
        {
            Title = modData.DisplayName.WithoutNumbers();
            Width = 900;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Colors.MainPanelStart.Color, 0),
                    new GradientStop(Colors.MainPanelEnd.Color, 1)
                }
            };

            Foreground = Brushes.White;
            CanResize = false;

            StackPanel mainPanel = new()
            {
                Margin = new Thickness(20),
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock titleBlock = new()
            {
                Text = $"Update Available for {modData.DisplayName.WithoutNumbers()}",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
            };
            mainPanel.Children.Add(titleBlock);

            Grid diffGrid = GetDiffGrid(modData);
            mainPanel.Children.Add(diffGrid);

            StackPanel buttonPanel = GetButtonPanel(modData);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        #region UI

        public Grid GetDiffGrid(ModData modData)
        {
            Grid diffGrid = new();
            diffGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            diffGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            diffGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            Border currentBorder = GetCurrentVersionBorder(modData);
            Grid.SetColumn(currentBorder, 0);
            Grid.SetRow(currentBorder, 0);

            Border newBorder = GetNewVersionBorder(modData);
            Grid.SetColumn(newBorder, 1);
            Grid.SetRow(newBorder, 0);

            diffGrid.Children.Add(currentBorder);
            diffGrid.Children.Add(newBorder);
            return diffGrid;
        }

        public Border GetCurrentVersionBorder(ModData modData)
        {
            Border border = new()
            {
                Background = Colors.MainPanelEnd,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 10, 0)
            };

            StackPanel panel = new()
            {
                Spacing = 10
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Current Version",
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Foreground = Brushes.LightGray
            });

            string versionText = string.IsNullOrEmpty(modData.version) ? "0.0.0" : modData.version;
            panel.Children.Add(new TextBlock
            {
                Text = $"v{versionText}",
                FontSize = 14
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Installed locally in staging folder.",
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            });

            border.Child = panel;
            return border;
        }

        public Border GetNewVersionBorder(ModData modData)
        {
            Border border = new()
            {
                Background = Colors.MainPanelEnd,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 0, 0)
            };
            
            StackPanel panel = new()
            {
                Spacing = 10
            };

            panel.Children.Add(new TextBlock
            {
                Text = "New Version (Latest)",
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Foreground = Colors.Green
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"v{modData.LatestVersion}",
                FontWeight = FontWeight.Bold,
                FontSize = 14
            });

            string cleanedDescription = NexusUtils.TrimNexusFormatting(modData.LatestFileDescription);
            string cleanedChangelog = NexusUtils.TrimNexusFormatting(modData.LatestChangelog);

            ScrollViewer descriptionScrollViewer = new()
            {
                Height = 120,
                Content = new TextBlock
                {
                    Text = $"Description:\n{(string.IsNullOrEmpty(cleanedDescription) ? "No description provided." : cleanedDescription)}",
                    TextWrapping = TextWrapping.Wrap
                },
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            
            ScrollViewer changelogScrollViewer = new()
            {
                Height = 120,
                Content = new TextBlock
                {
                    Text = $"Changelog:\n {(string.IsNullOrEmpty(cleanedChangelog) ? "No changelog provided." : cleanedChangelog)}",
                    TextWrapping = TextWrapping.Wrap
                },
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            panel.Children.Add(new TextBlock
            {
                Text = $"Downloads: {modData.LatestDownloads} | Published: {modData.LatestPublishDate}",
                Foreground = Brushes.Gray,
                FontSize = 12
            });
            
            panel.Children.Add(descriptionScrollViewer);
            panel.Children.Add(changelogScrollViewer);
            border.Child = panel;
            return border;
        }

        public StackPanel GetButtonPanel(ModData modData)
        {
            StackPanel buttonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            buttonPanel.Children.Add(GetUpdateButton());
            buttonPanel.Children.Add(GetViewOnNexusButton(modData));
            buttonPanel.Children.Add(GetCancelButton());
            return buttonPanel;
        }

        #endregion

        #region Buttons

        public CrescentButton GetUpdateButton()
        {
            CrescentButton button = new(Colors.Green)
            {
                Content = "Update Now",
                Width = 110
            };
            
            button.onClick += () =>
            {
                UpdateConfirmed = true;
                Close();
            };
            
            return button;
        }

        public CrescentButton GetViewOnNexusButton(ModData modData)
        {
            CrescentButton button = new(ButtonStyle.Translucent)
            {
                Content = "View On Nexus",
                Width = 110
            };
            
            button.onClick += () => Process.Start(new ProcessStartInfo($"{modData.url.nexusUrl}?tab=files")
            {
                UseShellExecute = true
            });
            
            return button;
        }

        public CrescentButton GetCancelButton()
        {
            CrescentButton button = new(ButtonStyle.Translucent)
            {
                Content = "Cancel",
                Width = 90
            };
            
            button.onClick += () =>
            {
                UpdateConfirmed = false;
                Close();
            };
            
            return button;
        }

        #endregion
    }
}