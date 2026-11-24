using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;
using Crescent.Utilities;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs.Elements
{
    public class LoadOrderMod : Border
    {
        public PluginData pluginData;
        public ModData modData;
        public LoadOrderTab loadOrderTab;
        public List<ModData> enabledMods;
        public int position;
        public int conflictCount;

        public LoadOrderMod(PluginData pluginData, ModData modData, LoadOrderTab loadOrderTab, List<ModData> enabledMods, int position, int conflictCount)
        {
            this.pluginData = pluginData;
            this.modData = modData;
            this.loadOrderTab = loadOrderTab;
            this.enabledMods = enabledMods;
            this.position = position;
            this.conflictCount = conflictCount;

            Background = Colors.Overlay;
            CornerRadius = new CornerRadius(4);
            Padding = new Thickness(12, 8);

            Grid rowGrid = new();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55, GridUnitType.Pixel)));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock indexBlock = GetIndexBlock();
            Grid.SetColumn(indexBlock, 0);

            TextBlock nameBlock = GetNameBlock();
            Grid.SetColumn(nameBlock, 1);

            TextBlock conflictBlock = GetConflictBlock();
            Grid.SetColumn(conflictBlock, 2);

            StackPanel movePanel = GetMovePanel();
            Grid.SetColumn(movePanel, 3);

            rowGrid.Children.Add(indexBlock);
            rowGrid.Children.Add(nameBlock);
            rowGrid.Children.Add(conflictBlock);
            rowGrid.Children.Add(movePanel);

            Child = rowGrid;
        }

        public TextBlock GetIndexBlock()
        {
            return new()
            {
                Text = $"#{position + 1}",
                Foreground = Brushes.Gray,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public TextBlock GetNameBlock()
        {
            return new()
            {
                Text = modData.DisplayName.Cleaned(),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        public TextBlock GetConflictBlock()
        {
            return new()
            {
                Text = conflictCount > 0 ? $"{conflictCount} Conflict{conflictCount.WithPlural()}" : "Clean",
                Foreground = conflictCount > 0 ? Brushes.Orange : Colors.Accent,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 16, 0)
            };
        }

        public StackPanel GetMovePanel()
        {
            StackPanel moveButtons = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            int lastIndex = enabledMods.Count - 1;
            moveButtons.Children.Add(GetMoveButton("⏫", position > 0, 0));
            moveButtons.Children.Add(GetMoveButton("▲", position > 0, position - 1));
            moveButtons.Children.Add(GetMoveButton("▼", position < lastIndex, position + 1));
            moveButtons.Children.Add(GetMoveButton("⏬", position < lastIndex, lastIndex));

            return moveButtons;
        }

        public CrescentButton GetMoveButton(string icon, bool isEnabled, int targetIndex)
        {
            CrescentButton button = new(ButtonStyle.Translucent)
            {
                Content = icon,
                Width = 34,
                IsEnabled = isEnabled
            };

            button.onClick += async () => await loadOrderTab.MoveModAsync(enabledMods, position, targetIndex);
            return button;
        }
    }
}