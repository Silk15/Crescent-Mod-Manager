using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Crescent.UI
{
    public class ProgressDialog : Dialog
    {
        public TextBlock directoryTextBlock;
        public TextBlock statusTextBlock;
        public ProgressBar progressBar;

        public ProgressDialog()
        {
            Title = "Deploying Mods";
            Width = 500;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            CanResize = false;
            Background = new SolidColorBrush(Color.FromArgb(255, 32, 32, 32));
            Foreground = Brushes.White;

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
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            };
            
            progressBar = new()
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            
            statusTextBlock = new()
            {
                Text = "Initialising...",
                FontSize = 13,
                Foreground = Brushes.LightGray
            };
            
            directoryTextBlock = new()
            {
                Text = "",
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
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