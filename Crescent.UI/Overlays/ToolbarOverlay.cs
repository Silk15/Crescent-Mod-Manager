using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Crescent.APIs.NexusMods;
using Crescent.Json.Serialization;

namespace Crescent.UI
{
    public class ToolbarOverlay : Grid
    {
        public MainWindow mainWindow;

        public ToolbarOverlay(MainWindow window)
        {
            mainWindow = window;
            Background = Colors.Overlay;

            ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            StackPanel toolbarButtons = GetToolbarPanel();
            Children.Add(toolbarButtons);
            SetColumn(toolbarButtons, 0);
            SetRow(toolbarButtons, 0);

            StackPanel userPanel = GetUserPanel();
            Children.Add(userPanel);
            SetColumn(userPanel, 1);
            SetRow(userPanel, 0);

            OverlaySeparator bottomBorder = new(OverlaySeparator.Mode.Horizontal);
            Children.Add(bottomBorder);
            SetColumnSpan(bottomBorder, 2);
            SetRow(bottomBorder, 0);
        }

        public StackPanel GetToolbarPanel()
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
                Width = 80
            };
            
            libraryButton.onClick += () => mainWindow.ShowLibrary();
            buttons.Children.Add(libraryButton);
            return buttons;
        }

        #region User Panel
        
        public StackPanel GetUserPanel()
        {
            StackPanel userPanel = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8,
                Margin = new Thickness(0, 0, 10, 0)
            };

            bool isLoggedOut = string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key);
            if (isLoggedOut) userPanel.Children.Add(GetLoginButton(userPanel));
            else userPanel.Children.Add(GetAvatarBorder());
            return userPanel;
        }

        public Border GetAvatarBorder()
        {
            Border avatarBorder = new()
            {
                CornerRadius = new CornerRadius(30),
                ClipToBounds = true
            };

            if (NexusAuthenticator.UserAvatarBytes != null)
            {
                MemoryStream memoryStream = new(NexusAuthenticator.UserAvatarBytes);
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
            }
            else
            {
                Logger.Warn("User's avatar byte array was null, sorry I got hungry :(");
                avatarBorder.Child = new TextBlock
                {
                    Text = "Null"
                };
            }
            
            return avatarBorder;
        }

        public CrescentButton GetLoginButton(StackPanel userPanel)
        {
            CrescentButton loginButton = new(ButtonStyle.Translucent)
            {
                Content = "Login",
                Width = 80
            };

            loginButton.onClick += async () =>
            {
                string currentApiKey = NexusAuthenticator.ApiKey != null ? NexusAuthenticator.ApiKey.key ?? string.Empty : string.Empty;
                ConfirmationDialog confirmationDialog = new("Nexus Login", "Enter your Nexus Mods API Key", true, currentApiKey);
                await confirmationDialog.ShowDialog(mainWindow);
                string enteredKey = confirmationDialog.InputText;

                if (!string.IsNullOrEmpty(enteredKey))
                {
                    NexusAuthenticator.ApiKey!.key = enteredKey;
                    await FileManager.WriteJson(NexusAuthenticator.ApiPath, NexusAuthenticator.ApiKey);
                    await new NexusAuthenticator.SignInTask().ExecuteAsync(null);

                    userPanel.Children.Clear();
                    bool isLoggedOut = string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key);
                    if (isLoggedOut) userPanel.Children.Add(GetLoginButton(userPanel));
                    else userPanel.Children.Add(GetAvatarBorder());
                }
            };

            return loginButton;
        }
        
        #endregion
    }
}