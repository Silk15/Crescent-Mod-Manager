using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public class ConflictReportDialog : Dialog
    {
        public PluginData? pluginData;
        public Dictionary<string, List<ModData>> conflicts;
        public Dictionary<string, ModData> selectedWinners = new(StringComparer.OrdinalIgnoreCase);
        public bool conflictsResolved;

        public ConflictReportDialog(Dictionary<string, List<ModData>> conflicts) : this(null, conflicts)
        {
        }

        public ConflictReportDialog(PluginData? pluginData, Dictionary<string, List<ModData>> conflicts)
        {
            this.pluginData = pluginData;
            this.conflicts = conflicts;
            Title = "Mod File Conflicts & Priority";
            Width = 680;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Colors.MainGradient;
            CanResize = false;
            Icon = new WindowIcon(AssetLoader.Open(new Uri(MainWindow.IconUri)));

            StackPanel mainPanel = new()
            {
                Margin = new Thickness(20),
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock titleBlock = new()
            {
                Text = "File Conflict & Priority Resolution",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center
            };

            TextBlock infoBlock = new()
            {
                Text = "Select which mod should win each conflicting file. Winning mods are automatically given higher load order priority so their files overwrite earlier ones.",
                FontSize = 13,
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap
            };

            mainPanel.Children.Add(titleBlock);
            mainPanel.Children.Add(infoBlock);
            mainPanel.Children.Add(GetConflictsScroll());
            mainPanel.Children.Add(GetButtonPanel());
            Content = mainPanel;
        }

        public ScrollViewer GetConflictsScroll()
        {
            StackPanel conflictListStack = new()
            {
                Spacing = 8
            };

            ScrollViewer conflictScrollViewer = new()
            {
                Content = conflictListStack,
                Height = 320,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            foreach (KeyValuePair<string, List<ModData>> pair in conflicts)
            {
                string fileKey = pair.Key;
                List<ModData> contendingMods = pair.Value.OrderBy(mod => mod.loadOrder).ToList();
                ModData currentWinner = contendingMods.Last();
                selectedWinners[fileKey] = currentWinner;

                Border conflictBorder = new()
                {
                    Background = Colors.Button,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 10)
                };

                Grid itemGrid = new()
                {
                    ColumnSpacing = 12
                };
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(220, GridUnitType.Pixel));

                StackPanel fileStack = new()
                {
                    Spacing = 4,
                    VerticalAlignment = VerticalAlignment.Center
                };

                fileStack.Children.Add(new TextBlock
                {
                    Text = fileKey,
                    FontWeight = FontWeight.Bold,
                    FontSize = 13,
                    Foreground = Brushes.Orange,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                string contendersText = string.Join(" vs ", contendingMods.Select(mod => mod.DisplayName.Cleaned()));
                fileStack.Children.Add(new TextBlock
                {
                    Text = contendersText,
                    FontSize = 11,
                    Foreground = Brushes.LightGray,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                ComboBox winnerSelector = new()
                {
                    ItemsSource = contendingMods.Select(mod => mod.DisplayName.Cleaned()).ToList(),
                    SelectedIndex = contendingMods.Count - 1,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 34
                };

                winnerSelector.SelectionChanged += (_, _) =>
                {
                    int chosenIndex = winnerSelector.SelectedIndex;
                    if (chosenIndex >= 0 && chosenIndex < contendingMods.Count)
                        selectedWinners[fileKey] = contendingMods[chosenIndex];
                };

                Grid.SetColumn(fileStack, 0);
                Grid.SetColumn(winnerSelector, 1);

                itemGrid.Children.Add(fileStack);
                itemGrid.Children.Add(winnerSelector);

                conflictBorder.Child = itemGrid;
                conflictListStack.Children.Add(conflictBorder);
            }

            return conflictScrollViewer;
        }

        private void ApplySelectedPriorities()
        {
            foreach (KeyValuePair<string, ModData> winnerPair in selectedWinners)
            {
                if (!conflicts.TryGetValue(winnerPair.Key, out List<ModData>? contenders)) continue;
                ModData winner = winnerPair.Value;
                int maxContenderOrder = contenders.Max(mod => mod.loadOrder);
                if (winner.loadOrder <= maxContenderOrder)
                    winner.loadOrder = maxContenderOrder + 1;
            }

            pluginData?.SetLoadOrder();
        }

        public StackPanel GetButtonPanel()
        {
            StackPanel buttonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            CrescentButton confirmButton = new(Colors.Green)
            {
                Content = "Apply Priorities & Proceed",
                Width = 190
            };

            confirmButton.onClick += () =>
            {
                ApplySelectedPriorities();
                conflictsResolved = true;
                Close();
            };

            CrescentButton cancelButton = new(ButtonStyle.Translucent)
            {
                Content = "Cancel",
                Width = 100
            };

            cancelButton.onClick += () =>
            {
                conflictsResolved = false;
                Close();
            };

            buttonPanel.Children.Add(confirmButton);
            buttonPanel.Children.Add(cancelButton);
            return buttonPanel;
        }
    }
}