using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Crescent.APIs.NexusMods;
using Crescent.UI.Windows;
using Silk.Utilities;

namespace Crescent.UI.Controls.Toolbar
{
    public class ToolbarOverlay : Grid
    {
        public MainWindow mainWindow;
        public CrescentButton libraryButton;
        public CrescentButton settingsButton;
        public Border avatarBorder;

        public ToolbarOverlay(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            Background = Colors.Overlay;
            Height = 50;

            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            StackPanel navigationStack = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            libraryButton = new(ButtonStyle.Translucent)
            {
                Content = "Library",
                Padding = new Thickness(14, 6)
            };
            libraryButton.onClick += () => mainWindow.ShowLibrary();

            settingsButton = new(ButtonStyle.Translucent)
            {
                Content = "Settings",
                Padding = new Thickness(14, 6)
            };
            settingsButton.onClick += () => mainWindow.ShowSettings();

            navigationStack.Children.Add(libraryButton);
            navigationStack.Children.Add(settingsButton);

            SetColumn(navigationStack, 0);
            Children.Add(navigationStack);

            StackPanel accountStack = GetAccountPanel();
            SetColumn(accountStack, 2);
            Children.Add(accountStack);

            OverlaySeparator bottomSeparator = new(OverlaySeparator.Mode.Horizontal)
            {
                VerticalAlignment = VerticalAlignment.Bottom
            };
            SetColumnSpan(bottomSeparator, 3);
            Children.Add(bottomSeparator);
        }

        public StackPanel GetAccountPanel()
        {
            StackPanel accountPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 25,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            avatarBorder = new()
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Background = Colors.Button,
                VerticalAlignment = VerticalAlignment.Center
            };

            RefreshAvatar();
            accountPanel.Children.Add(new DiscordButton());
            accountPanel.Children.Add(new NotificationsButton());
            accountPanel.Children.Add(avatarBorder);
            return accountPanel;
        }

        public void RefreshAvatar()
        {
            if (NexusAuthenticator.UserAvatarBytes != null && NexusAuthenticator.UserAvatarBytes.Length > 0)
            {
                try
                {
                    using MemoryStream memoryStream = new(NexusAuthenticator.UserAvatarBytes);
                    Image image = new()
                    {
                        Source = new Bitmap(memoryStream),
                        Stretch = Stretch.UniformToFill,
                    };
                    
                    RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                    avatarBorder.Child = image;
                    return;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
            }

            avatarBorder.Child = new TextBlock
            {
                Text = "N",
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }
}