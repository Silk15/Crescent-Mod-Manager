using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace Crescent.UI
{
    public class ToastItem : Border
    {
        public const double FixedToastWidth = 360;

        public TextBlock titleBlock;
        public TextBlock messageBlock;
        public TextBlock detailBlock;
        public ProgressBar progressBar;
        public Border accentBar;
        public Button dismissButton;

        public ToastItem(string title, string message, string detail, ToastType toastType)
        {
            Width = FixedToastWidth;
            MinWidth = FixedToastWidth;
            MaxWidth = FixedToastWidth;
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
            
            BorderBrush = Colors.Separator;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            ClipToBounds = true;
            Margin = new Thickness(0, 8, 0, 0);
            HorizontalAlignment = HorizontalAlignment.Right;

            Grid rootGrid = new();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            accentBar = new()
            {
                Background = GetAccentBrush(toastType),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(accentBar, 0);
            rootGrid.Children.Add(accentBar);

            StackPanel contentStack = new()
            {
                Margin = new Thickness(14, 12, 14, 12),
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            Grid headerGrid = new();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            titleBlock = new()
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            headerGrid.Children.Add(titleBlock);

            dismissButton = new()
            {
                Content = "✕",
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brushes.Gray,
                FontSize = 11,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dismissButton, 1);
            headerGrid.Children.Add(dismissButton);

            messageBlock = new()
            {
                Text = message,
                FontSize = 12,
                Foreground = Brushes.LightGray,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            detailBlock = new()
            {
                Text = detail,
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsVisible = !string.IsNullOrEmpty(detail)
            };

            progressBar = new()
            {
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = Brushes.CornflowerBlue,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsVisible = toastType == ToastType.Progress
            };

            contentStack.Children.Add(headerGrid);
            contentStack.Children.Add(messageBlock);
            contentStack.Children.Add(detailBlock);
            contentStack.Children.Add(progressBar);

            Grid.SetColumn(contentStack, 1);
            rootGrid.Children.Add(contentStack);
            Child = rootGrid;
        }

        public void SetAccent(ToastType toastType) => accentBar.Background = GetAccentBrush(toastType);

        private static IBrush GetAccentBrush(ToastType toastType) => toastType switch
        {
            ToastType.Success => Colors.Green,
            ToastType.Warning => Brushes.Orange,
            ToastType.Error => Colors.Red,
            ToastType.Progress => Brushes.CornflowerBlue,
            _ => Brushes.CornflowerBlue
        };
    }
}