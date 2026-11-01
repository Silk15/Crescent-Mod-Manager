using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace Crescent.UI
{
    public abstract class ResponseDialog : Dialog
    {
        public string InputText { get; set; } = string.Empty;

        public ResponseDialog(string windowTitle)
        {
            Title = windowTitle;
            Width = 600;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Colors.MainGradient;
            CanResize = false;
            Icon = new WindowIcon(AssetLoader.Open(new Uri(MainWindow.IconUri)));
        }

        public static StackPanel GetBasePanel(string promptText) => new()
        {
            Margin = new Thickness(20),
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = promptText,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        public static StackPanel GetButtonPanel() => new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
    }
}