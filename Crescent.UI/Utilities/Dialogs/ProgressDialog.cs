using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace Crescent.UI
{
    public class ProgressDialog : Dialog
    {
        public const double DialogFixedWidth = 380;

        public TextBlock directoryTextBlock;
        public TextBlock statusTextBlock;
        public ProgressBar progressBar;

        public ProgressDialog()
        {
            Title = "Deploy Mods";
            Width = DialogFixedWidth;
            MinWidth = DialogFixedWidth;
            MaxWidth = DialogFixedWidth;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            CanResize = false;
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
            Icon = new WindowIcon(AssetLoader.Open(new Uri(MainWindow.IconUri)));

            StackPanel mainPanel = new()
            {
                Margin = new Thickness(20),
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock titleTextBlock = new()
            {
                Text = "Deploying...",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };

            progressBar = new()
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Foreground = Brushes.CornflowerBlue
            };

            statusTextBlock = new()
            {
                Text = "Initialising...",
                FontSize = 13,
                Foreground = Brushes.LightGray,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            directoryTextBlock = new()
            {
                Text = string.Empty,
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            mainPanel.Children.Add(titleTextBlock);
            mainPanel.Children.Add(progressBar);
            mainPanel.Children.Add(statusTextBlock);
            mainPanel.Children.Add(directoryTextBlock);
            Content = mainPanel;

            Opened += (_, _) =>
            {
                if (ToastManager.Initialised) Hide();
            };

            Closed += (_, _) => ToastManager.OnProgressDialogClosed();
        }

        public void UpdateProgress(double current, double total, string modName, string targetDirectory)
        {
            if (ToastManager.Initialised)
            {
                if (IsVisible) Hide();
                ToastManager.UpdateProgress(current, total, modName, targetDirectory);
                return;
            }

            double percentage = total > 0 ? current / total * 100 : 0;
            progressBar.Value = percentage;
            statusTextBlock.Text = $"Deploying: {modName} ({current}/{total})";
            directoryTextBlock.Text = $"Target: {targetDirectory}";
        }
    }
}