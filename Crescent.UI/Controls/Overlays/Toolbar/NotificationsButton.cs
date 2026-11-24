using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.UI.Controls.Toasts;

namespace Crescent.UI.Controls.Toolbar
{
    public class NotificationsButton : CrescentButton
    {
        public NotificationsButton() : base(ButtonStyle.Transparent)
        {
            Stream stream = AssetLoader.Open(new Uri("avares://Crescent.UI/Assets/notifications.png"));
            Image image = new()
            {
                Source = new Bitmap(stream),
            };

            Width = 24;
            Height = 24;
            Content = image;
            Flyout = GetFlyout();
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            onClick += () => Flyout = GetFlyout();
        }

        public Flyout GetFlyout()
        {
            StackPanel toastStack = new()
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            ScrollViewer toastScrollViewer = new()
            {
                Content = toastStack,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            toastScrollViewer.Content = toastStack;
            foreach (ToastData toastData in ToastManager.toastHistory)
            {
                Border border = new()
                {
                    Background = Colors.MainGradient,
                    CornerRadius = new(8),
                    ClipToBounds = true,
                };

                StackPanel stackPanel = new()
                {
                    Spacing = 2
                };

                stackPanel.Children.Add(new TextBlock
                {
                    Text = toastData.name,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14
                });

                stackPanel.Children.Add(new TextBlock
                {
                    Text = toastData.text,
                    FontWeight = FontWeight.Regular,
                    FontSize = 12
                });

                border.Child = stackPanel;
                toastStack.Children.Add(border);
            }

            Flyout flyout = new();
            flyout.Content = toastScrollViewer;
            return flyout;
        }
    }
}