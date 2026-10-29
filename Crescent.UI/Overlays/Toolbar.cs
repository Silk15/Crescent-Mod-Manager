using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Crescent.Json.Serialization;
using Crescent.Nexus.Authentication;
using Crescent.Utilities;
using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public class Toolbar : Grid
    {
        public MainWindow mainWindow;

        public Toolbar(MainWindow window)
        {
            mainWindow = window;
            Background = Colors.Overlay;

            ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            StackPanel leftButtons = CreateToolbarButtons();
            Children.Add(leftButtons);
            SetColumn(leftButtons, 0);
            SetRow(leftButtons, 0);

            StackPanel rightUserContainer = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Children.Add(rightUserContainer);
            SetColumn(rightUserContainer, 1);
            SetRow(rightUserContainer, 0);

            LoadUserPanel(rightUserContainer);
            Avalonia.Controls.Shapes.Rectangle bottomBorder = new()
            {
                Height = 1,
                Fill = Colors.Separator,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Children.Add(bottomBorder);
            SetColumnSpan(bottomBorder, 2);
            SetRow(bottomBorder, 0);
        }

        public StackPanel CreateToolbarButtons()
        {
            StackPanel buttons = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            CrescentButton libraryButton = new(ButtonStyle.Translucent)
            {
                Content = "Library",
                Width = 80,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = Brushes.White
            };
            libraryButton.Click += (sender, routedEventArgs) => mainWindow.ShowLibrary();
            buttons.Children.Add(libraryButton);

            return buttons;
        }

        public void LoadUserPanel(StackPanel parentRightButtons)
        {
            bool isLoggedOut = string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key) || NexusAuthenticator.ApiKey == null;
            byte[]? imageBytes = NexusAuthenticator.UserAvatarBytes;

            StackPanel userPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            Border avatarBorder = new()
            {
                CornerRadius = new CornerRadius(30),
                ClipToBounds = true
            };

            if (isLoggedOut)
            {
                CrescentButton loginButton = CreateLoginButton(parentRightButtons, userPanel);
                userPanel.Children.Add(loginButton);
            }
            else if (imageBytes != null)
            {
                MemoryStream memoryStream = new(imageBytes);
                Bitmap avatarBitmap = Bitmap.DecodeToHeight(memoryStream, 30);
                Image avatarImage = new()
                {
                    Source = avatarBitmap,
                    Width = 30,
                    Height = 30,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapInterpolationMode(avatarImage, BitmapInterpolationMode.HighQuality);
                avatarBorder.Child = avatarImage;
                userPanel.Children.Add(avatarBorder);
            }

            parentRightButtons.Children.Add(userPanel);
        }

        public CrescentButton CreateLoginButton(StackPanel parentRightButtons, StackPanel userPanel)
        {
            CrescentButton loginButton = new(ButtonStyle.Translucent)
            {
                Content = "Login",
                Width = 80,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };

            loginButton.Click += async (sender, routedEventArgs) =>
            {
                string currentApiKey = NexusAuthenticator.ApiKey != null ? NexusAuthenticator.ApiKey.key ?? string.Empty : string.Empty;
                InputDialog loginDialog = new("Nexus Login", "Enter your Nexus Mods API Key:", true, currentApiKey);
                await loginDialog.ShowDialog(mainWindow);
                string enteredKey = loginDialog.InputText;

                if (!string.IsNullOrEmpty(enteredKey))
                {
                    NexusAuthenticator.ApiKey!.key = enteredKey;
                    await FileManager.WriteJson(NexusAuthenticator.ApiPath, NexusAuthenticator.ApiKey);
                    await new NexusAuthenticator.SignInTask().ExecuteAsync(null);

                    parentRightButtons.Children.Remove(userPanel);
                    LoadUserPanel(parentRightButtons);
                }
            };

            return loginButton;
        }
    }
}