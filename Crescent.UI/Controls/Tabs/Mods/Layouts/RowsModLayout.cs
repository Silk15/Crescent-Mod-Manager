using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;
using Crescent.UI.Controls.Tabs.Elements;

namespace Crescent.UI.Controls.Tabs.Layouts
{
    public class RowsModLayout : IModLayout
    {
        public ModGridType GridType => ModGridType.Rows;
        public double EstimatedHeight => 52;
        public Thickness Margin => new(0);
        public bool CanShowThumbnail(bool hasImage) => hasImage;

        public Panel CreateContainer() => new StackPanel { Spacing = 6 };

        public Panel BuildMod(RenderedMod mod)
        {
            ModLayouts.DetachControls(mod);

            mod.Padding = new Thickness(12, 7);
            mod.Margin = new Thickness(0);
            mod.Width = double.NaN;
            mod.Height = double.NaN;
            mod.VerticalAlignment = VerticalAlignment.Top;

            mod.modToggle.Margin = new Thickness(0, 0, 10, 0);

            mod.thumbnailBorder.UseAspectRatio = false;
            mod.thumbnailBorder.IsVisible = mod.thumbnailImage.Source != null;
            mod.thumbnailBorder.Width = 64;
            mod.thumbnailBorder.Height = 36;
            mod.thumbnailBorder.Margin = new Thickness(0, 0, 12, 0);

            mod.nameBlock.FontSize = 14;
            mod.nameBlock.TextWrapping = TextWrapping.NoWrap;
            mod.nameBlock.MaxLines = 1;
            mod.nameBlock.VerticalAlignment = VerticalAlignment.Center;
            mod.nameBlock.Margin = new Thickness(0, 0, 10, 0);

            bool hasUpdateControl = mod.updateControl.IsVisible;
            mod.updateControl.Width = hasUpdateControl ? 85 : 0;
            mod.updateControl.Margin = hasUpdateControl ? new Thickness(0, 0, 8, 0) : new Thickness(0);
            mod.updateControl.HorizontalAlignment = HorizontalAlignment.Center;

            mod.versionBlock.FontSize = 13;
            mod.versionBlock.HorizontalAlignment = HorizontalAlignment.Center;
            mod.versionBlock.TextAlignment = TextAlignment.Center;
            mod.versionBlock.Margin = new Thickness(6, 0, 6, 0);

            mod.manualButton.Width = 105;
            mod.manualButton.HorizontalAlignment = HorizontalAlignment.Center;
            mod.manualButton.Margin = new Thickness(8, 0, 0, 0);

            mod.foldoutButton.Margin = new Thickness(6, 0, 0, 0);
            mod.nexusButton.Margin = new Thickness(2, 0, 0, 0);

            mod.menuButton.Width = 32;
            mod.menuButton.Margin = new Thickness(6, 0, 0, 0);

            mod.descriptionBlock.Margin = new Thickness(0);
            mod.updatedDateBlock.HorizontalAlignment = HorizontalAlignment.Left;
            mod.releaseDateBlock.HorizontalAlignment = HorizontalAlignment.Left;

            StackPanel containerStack = new() { Spacing = 0 };

            Grid rowGrid = new();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(80, GridUnitType.Pixel)));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid.SetColumn(mod.modToggle, 0);
            Grid.SetColumn(mod.thumbnailBorder, 1);
            Grid.SetColumn(mod.nameBlock, 2);
            Grid.SetColumn(mod.updateControl, 3);
            Grid.SetColumn(mod.versionBlock, 4);
            Grid.SetColumn(mod.manualButton, 5);
            Grid.SetColumn(mod.foldoutButton, 6);
            Grid.SetColumn(mod.nexusButton, 7);
            Grid.SetColumn(mod.menuButton, 8);

            rowGrid.Children.Add(mod.modToggle);
            rowGrid.Children.Add(mod.thumbnailBorder);
            rowGrid.Children.Add(mod.nameBlock);
            rowGrid.Children.Add(mod.updateControl);
            rowGrid.Children.Add(mod.versionBlock);
            rowGrid.Children.Add(mod.manualButton);
            rowGrid.Children.Add(mod.foldoutButton);
            rowGrid.Children.Add(mod.nexusButton);
            rowGrid.Children.Add(mod.menuButton);

            StackPanel detailsStack = new()
            {
                Spacing = 5,
                IsVisible = mod.isExpanded
            };

            detailsStack.Children.Add(ModLayouts.CreateLine(new Thickness(0, 6, 0, 2)));
            detailsStack.Children.Add(mod.descriptionBlock);

            StackPanel datesPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Margin = new Thickness(0, 1, 0, 2)
            };
            datesPanel.Children.Add(mod.updatedDateBlock);
            datesPanel.Children.Add(mod.releaseDateBlock);
            detailsStack.Children.Add(datesPanel);

            mod.foldoutContainer = detailsStack;

            containerStack.Children.Add(rowGrid);
            containerStack.Children.Add(detailsStack);

            return containerStack;
        }

        public void ReplaceUpdateControl(RenderedMod mod, Control oldControl, Control newControl)
        {
            if (oldControl.Parent is not Grid rowGrid) return;
            int column = Grid.GetColumn(oldControl);
            rowGrid.Children.Remove(oldControl);

            bool hasUpdateControl = newControl.IsVisible;
            newControl.Width = hasUpdateControl ? 85 : 0;
            newControl.Margin = hasUpdateControl ? new Thickness(0, 0, 8, 0) : new Thickness(0);

            Grid.SetRow(newControl, 0);
            Grid.SetColumn(newControl, column);
            rowGrid.Children.Add(newControl);
        }
    }
}