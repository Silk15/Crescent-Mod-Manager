using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;
using Crescent.UI.Controls.Tabs.Elements;

namespace Crescent.UI.Controls.Tabs.Layouts
{
    public class GridModLayout : IModLayout
    {
        public ModGridType GridType => ModGridType.Grid;
        public double EstimatedHeight => 250;
        public Thickness Margin => new(4);
        public bool CanShowThumbnail(bool hasImage) => true;

        public Panel CreateContainer() => new ModGrid
        {
            MinColumnWidth = 265,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };

        public Panel BuildMod(RenderedMod mod)
        {
            ModLayouts.DetachControls(mod);

            mod.Padding = new Thickness(10);
            mod.Margin = new Thickness(4);
            mod.Width = double.NaN;
            mod.Height = double.NaN;
            mod.VerticalAlignment = VerticalAlignment.Top;

            mod.thumbnailBorder.UseAspectRatio = true;
            mod.thumbnailBorder.IsVisible = true;
            mod.thumbnailBorder.Width = double.NaN;
            mod.thumbnailBorder.Height = double.NaN;
            mod.thumbnailBorder.Margin = new Thickness(0, 0, 0, 8);
            mod.thumbnailBorder.HorizontalAlignment = HorizontalAlignment.Stretch;

            mod.nameBlock.FontSize = 14;
            mod.nameBlock.TextWrapping = TextWrapping.NoWrap;
            mod.nameBlock.MaxLines = 1;
            mod.nameBlock.VerticalAlignment = VerticalAlignment.Center;
            mod.nameBlock.Margin = new Thickness(0);

            mod.versionBlock.FontSize = 12;
            mod.versionBlock.HorizontalAlignment = HorizontalAlignment.Left;
            mod.versionBlock.VerticalAlignment = VerticalAlignment.Center;
            mod.versionBlock.TextAlignment = TextAlignment.Left;
            mod.versionBlock.Margin = new Thickness(0, 0, 6, 0);

            mod.foldoutButton.Margin = new Thickness(0, 0, 2, 0);
            mod.nexusButton.Margin = new Thickness(0, 0, 6, 0);
            mod.modToggle.Margin = new Thickness(0, 0, -6, 0);

            mod.manualButton.Width = double.NaN;
            mod.manualButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            mod.manualButton.Margin = new Thickness(0);

            bool hasUpdateControl = mod.updateControl.IsVisible;
            mod.updateControl.Width = hasUpdateControl ? 85 : 0;
            mod.updateControl.Margin = hasUpdateControl ? new Thickness(6, 0, 0, 0) : new Thickness(0);
            mod.updateControl.HorizontalAlignment = HorizontalAlignment.Center;

            mod.menuButton.Width = 32;
            mod.menuButton.Margin = new Thickness(6, 0, 0, 0);

            mod.descriptionBlock.Margin = new Thickness(0);
            mod.updatedDateBlock.HorizontalAlignment = HorizontalAlignment.Left;
            mod.releaseDateBlock.HorizontalAlignment = HorizontalAlignment.Right;

            StackPanel cardStack = new() { Spacing = 0 };
            cardStack.Children.Add(mod.thumbnailBorder);
            cardStack.Children.Add(mod.nameBlock);
            cardStack.Children.Add(ModLayouts.CreateLine(new Thickness(0, 6, 0, 4)));

            Grid metaRow = new() { Margin = new Thickness(0, 0, 0, 6) };
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid.SetColumn(mod.versionBlock, 0);
            Grid.SetColumn(mod.foldoutButton, 1);
            Grid.SetColumn(mod.nexusButton, 2);
            Grid.SetColumn(mod.modToggle, 3);

            metaRow.Children.Add(mod.versionBlock);
            metaRow.Children.Add(mod.foldoutButton);
            metaRow.Children.Add(mod.nexusButton);
            metaRow.Children.Add(mod.modToggle);
            cardStack.Children.Add(metaRow);

            StackPanel detailsStack = new()
            {
                Spacing = 5,
                Margin = new Thickness(0, 0, 0, 8),
                IsVisible = mod.isExpanded
            };

            detailsStack.Children.Add(mod.descriptionBlock);

            Grid datesGrid = new() { Margin = new Thickness(0, 2, 0, 0) };
            datesGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            datesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid.SetColumn(mod.updatedDateBlock, 0);
            Grid.SetColumn(mod.releaseDateBlock, 1);
            datesGrid.Children.Add(mod.updatedDateBlock);
            datesGrid.Children.Add(mod.releaseDateBlock);
            detailsStack.Children.Add(datesGrid);
            detailsStack.Children.Add(ModLayouts.CreateLine(new Thickness(0, 2, 0, 0)));

            mod.foldoutContainer = detailsStack;
            cardStack.Children.Add(detailsStack);

            Grid actionGrid = new();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid.SetColumn(mod.manualButton, 0);
            Grid.SetColumn(mod.updateControl, 1);
            Grid.SetColumn(mod.menuButton, 2);

            actionGrid.Children.Add(mod.manualButton);
            actionGrid.Children.Add(mod.updateControl);
            actionGrid.Children.Add(mod.menuButton);
            cardStack.Children.Add(actionGrid);

            return cardStack;
        }

        public void ReplaceUpdateControl(RenderedMod mod, Control oldControl, Control newControl)
        {
            if (oldControl.Parent is not Grid actionGrid) return;
            int column = Grid.GetColumn(oldControl);
            actionGrid.Children.Remove(oldControl);

            bool hasUpdateControl = newControl.IsVisible;
            newControl.Width = hasUpdateControl ? 85 : 0;
            newControl.Margin = hasUpdateControl ? new Thickness(6, 0, 0, 0) : new Thickness(0);
            newControl.HorizontalAlignment = HorizontalAlignment.Center;

            Grid.SetColumn(newControl, column);
            actionGrid.Children.Add(newControl);
        }
    }
}