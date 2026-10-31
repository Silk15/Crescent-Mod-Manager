using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Crescent.UI
{
    public abstract class ResponseDialog : Dialog
    {
        public string InputText { get; set; } = string.Empty;

        public ResponseDialog(string windowTitle, double height)
        {
            Title = windowTitle;
            Width = 420;
            Height = height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromArgb(255, 37, 37, 38));
            Foreground = Brushes.White;
            CanResize = false;
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