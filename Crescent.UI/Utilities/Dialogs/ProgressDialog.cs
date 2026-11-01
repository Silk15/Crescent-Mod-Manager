using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace Crescent.UI
{
    public class ProgressDialog : Dialog
    {
        public TextBlock directoryTextBlock;
        public TextBlock statusTextBlock;
        public ProgressBar progressBar;

        public ProgressDialog()
        {
            Title = "Deploy Mods";
            SizeToContent = SizeToContent.WidthAndHeight;
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
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock titleTextBlock = new()
            {
                Text = "Deploying...",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            
            progressBar = new()
            {
                Height = 30,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Foreground = Brushes.CornflowerBlue
            };
            
            statusTextBlock = new()
            {
                Text = "Initialising...",
                FontSize = 14,
                Foreground = Brushes.LightGray,
                TextAlignment = TextAlignment.Center
            };
            
            directoryTextBlock = new()
            {
                Text = "",
                FontSize = 12,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            
            mainPanel.Children.Add(titleTextBlock);
            mainPanel.Children.Add(progressBar);
            mainPanel.Children.Add(statusTextBlock);
            mainPanel.Children.Add(directoryTextBlock);
            Content = mainPanel;
        }

        public void UpdateProgress(double current, double total, string modName, string targetDirectory)
        {
            double percentage = total > 0 ? current / total * 100 : 0;
            progressBar.Value = percentage;
            
            statusTextBlock.Text = $"Deploying: {modName} ({current}/{total})";
            directoryTextBlock.Text = $"Target: {targetDirectory}";
        }
    }
}