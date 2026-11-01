using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Crescent.UI
{
    public class LoadingWindow : Window
    {
        public TextBlock processBlock;
        public TextBlock taskBlock;

        public LoadingWindow()
        {
            Title = MainWindow.TitleText;
            Width = 560;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            CanResize = false;
            WindowDecorations = WindowDecorations.BorderOnly;
            Background = Brushes.Transparent;
            Icon = new WindowIcon(AssetLoader.Open(new Uri(MainWindow.IconUri)));

            Border imageCoverBorder = new()
            {
                Height = 170,
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                ClipToBounds = true
            };

            Uri assetUri = new("avares://Crescent.UI/Assets/crescent_cover.png");
            Image gamePosterImage = new()
            {
                Source = new Bitmap(AssetLoader.Open(assetUri)),
                Stretch = Stretch.UniformToFill
            };

            imageCoverBorder.Child = gamePosterImage;
            
            processBlock = new()
            {
                Text = "Initialising...",
                Foreground = Brushes.LightGray,
                TextAlignment = TextAlignment.Center,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(10, 5, 10, 15),
            };

            taskBlock = new()
            {
                Text = "Initialising...",
                Foreground = Brushes.LightGray,
                TextAlignment = TextAlignment.Center,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(10, 5, 10, 15),
            };

            StackPanel panel = new()
            {
                Spacing = 10
            };

            panel.Children.Add(imageCoverBorder);
            panel.Children.Add(processBlock);
            panel.Children.Add(taskBlock);

            Content = new Border
            {
                Background = Colors.MainPanelEnd,
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Child = panel
            };
        }
        
        public void UpdateProcess(string message) => Avalonia.Threading.Dispatcher.UIThread.Post(() => processBlock.Text = message);

        public void UpdateStatus(string message) => Avalonia.Threading.Dispatcher.UIThread.Post(() => taskBlock.Text = message);
    }
}