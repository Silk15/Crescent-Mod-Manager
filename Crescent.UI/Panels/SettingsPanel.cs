using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.APIs.Discord;
using Crescent.APIs.NexusMods;
using Crescent.Json.Serialization;
using Crescent.Versioning;
using Version = Crescent.Versioning.Version;

namespace Crescent.UI
{
    public class SettingsPanel : StackPanel
    {
        public MainWindow mainWindow;

        public SettingsPanel(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            Margin = new Thickness(30, 15, 30, 30);
            Spacing = 22;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Setup();
        }

        public void Setup()
        {
            Children.Clear();
            Settings settings = Settings.Instance;

            Children.Add(SettingsUtils.GetHeader("Settings"));
            Children.Add(GetUpdateSettings());
            Children.Add(GetNexusSettings());
            Children.Add(GetModManagerSettings(settings));
            Children.Add(GetConsoleSettings(settings));
            Children.Add(GetDiscordSettings(settings));
        }

        #region Updates & Versioning

        public Border GetUpdateSettings()
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Updates & Versioning", out Border cardBorder);

            Grid versionRow = new();
            versionRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            versionRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Version? pendingUpdate = GithubUpdater.availableManagerUpdate;
            bool hasUpdate = pendingUpdate != null && Version.IsNewerVersion(pendingUpdate.version, Runtime.Version);

            StackPanel versionInfoStack = new()
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            versionInfoStack.Children.Add(new TextBlock
            {
                Text = $"Crescent Mod Manager ({Runtime.Version})",
                FontWeight = FontWeight.SemiBold,
                FontSize = 13
            });

            versionInfoStack.Children.Add(new TextBlock
            {
                Text = hasUpdate ? $"Update {pendingUpdate!.version} is available" : "This is the latest version",
                FontSize = 12,
                Foreground = hasUpdate ? Colors.Green : Brushes.Gray
            });
            Grid.SetColumn(versionInfoStack, 0);

            CrescentButton actionButton = hasUpdate ? new(Colors.Green) : new(ButtonStyle.Translucent)
            {
                Content = hasUpdate ? $"Update to {pendingUpdate!.version}" : "Check for Updates",
                Width = 150,
                Height = 35
            };

            actionButton.onClick += async () =>
            {
                if (hasUpdate && pendingUpdate != null)
                {
                    UpdateDialog dialog = new(pendingUpdate, false);
                    await dialog.ShowDialog(mainWindow);
                    return;
                }

                actionButton.IsEnabled = false;
                actionButton.Content = "Checking...";

                Version? foundUpdate = await GithubUpdater.CheckManagerUpdateAsync();
                Setup();

                if (foundUpdate != null)
                {
                    ToastManager.Show("Update Available", $"Crescent {foundUpdate.version} is ready to install", ToastType.Info, 4000);
                    UpdateDialog dialog = new(foundUpdate, false);
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    ToastManager.Show("Up to Date", $"This is the latest version", ToastType.Success, 3000);
                }
            };

            Grid.SetColumn(actionButton, 1);
            versionRow.Children.Add(versionInfoStack);
            versionRow.Children.Add(actionButton);
            sectionStack.Children.Add(versionRow);

            return cardBorder;
        }

        #endregion

        #region Nexus Settings

        public Border GetNexusSettings()
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Nexus Mods & NXM Protocol", out Border cardBorder);

            sectionStack.Children.Add(GetNexusStatusBlock());
            sectionStack.Children.Add(GetApiGrid());
            sectionStack.Children.Add(GetNxmRow());

            return cardBorder;
        }

        public TextBlock GetNexusStatusBlock()
        {
            string connectedUser = NexusAuthenticator.UserProfile?["name"]?.ToString() ?? "Not Authenticated";
            bool isPremium = NexusAuthenticator.UserProfile?["is_premium"]?.ToObject<bool>() ?? false;
            string accountStatus = NexusAuthenticator.UserProfile != null ? $"Connected as {connectedUser} ({(isPremium ? "Premium" : "Free")})" : "No API key connected";

            return new()
            {
                Text = accountStatus,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = NexusAuthenticator.UserProfile != null ? Colors.Green : Brushes.Orange
            };
        }

        public Grid GetApiGrid()
        {
            Grid apiGrid = new()
            {
                ColumnSpacing = 10
            };

            apiGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            apiGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBox apiKeyBox = new()
            {
                Text = NexusAuthenticator.ApiKey?.key ?? string.Empty,
                PasswordChar = '•',
                PlaceholderText = "Enter your Nexus Mods API Key...",
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center
            };
            apiKeyBox.GotFocus += (_, _) => apiKeyBox.RevealPassword = true;
            apiKeyBox.LostFocus += (_, _) => apiKeyBox.RevealPassword = false;
            Grid.SetColumn(apiKeyBox, 0);

            CrescentButton saveApiKeyButton = GetSaveApiKeyButton(apiKeyBox);
            Grid.SetColumn(saveApiKeyButton, 1);

            apiGrid.Children.Add(apiKeyBox);
            apiGrid.Children.Add(saveApiKeyButton);
            return apiGrid;
        }

        public CrescentButton GetSaveApiKeyButton(TextBox apiKeyBox)
        {
            CrescentButton saveApiKeyButton = new(Colors.Green)
            {
                Content = "Validate & Save",
                Width = 140,
                Height = 36
            };

            saveApiKeyButton.onClick += async () =>
            {
                saveApiKeyButton.IsEnabled = false;
                saveApiKeyButton.Content = "Validating...";
                try
                {
                    if (NexusAuthenticator.ApiKey != null) NexusAuthenticator.ApiKey.key = apiKeyBox.Text?.Trim() ?? string.Empty;
                    await FileManager.WriteJsonAsync(NexusAuthenticator.ApiPath, NexusAuthenticator.ApiKey);
                    NexusAuthenticator.SignInTask signInTask = new();
                    await signInTask.ExecuteAsync(null);

                    mainWindow.toolbarOverlay?.RefreshAvatar();
                    Setup();

                    if (NexusAuthenticator.UserProfile != null) ToastManager.Show("Nexus Connected", $"Signed in as {NexusAuthenticator.UserProfile["name"]}!", ToastType.Success);
                    else ToastManager.Show("Authentication Failed", "The provided API Key was rejected :(", ToastType.Error);
                }
                finally
                {
                    saveApiKeyButton.IsEnabled = true;
                    saveApiKeyButton.Content = "Validate & Save";
                }
            };

            return saveApiKeyButton;
        }

        public Grid GetNxmRow()
        {
            Grid nxmRow = new()
            {
                Margin = new Thickness(0, 6, 0, 0)
            };

            nxmRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            nxmRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            bool isRegistered = NxmProtocolHandler.IsProtocolRegistered();
            StackPanel nxmInfo = new()
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            nxmInfo.Children.Add(new TextBlock
            {
                Text = "Mod Manager Downloads",
                FontWeight = FontWeight.SemiBold,
                FontSize = 13
            });

            nxmInfo.Children.Add(new TextBlock
            {
                Text = isRegistered ? "Registered in Windows" : "To enable Mod Manager Downloads on nexusmods.com, please register Crescent as the handler for NXM requests",
                FontSize = 12,
                Foreground = isRegistered ? Colors.Green : Brushes.Gray
            });
            Grid.SetColumn(nxmInfo, 0);

            CrescentButton registerNxmButton = new(ButtonStyle.Translucent)
            {
                Content = isRegistered ? "Re-register NXM" : "Register NXM",
                Width = 140,
                Height = 35
            };

            registerNxmButton.onClick += () =>
            {
                NxmProtocolHandler.RegisterProtocol();
                Setup();
            };

            Grid.SetColumn(registerNxmButton, 1);
            nxmRow.Children.Add(nxmInfo);
            nxmRow.Children.Add(registerNxmButton);
            return nxmRow;
        }

        #endregion

        #region Mod & Console Settings

        public Border GetModManagerSettings(Settings settings)
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Mod Management & Deployment", out Border cardBorder);

            sectionStack.Children.Add(SettingsUtils.GetToggleRow("Auto-deploy New Mods", "Automatically deploy newly downloaded mods", settings.autoDeployOnDownload, async value =>
            {
                settings.autoDeployOnDownload = value;
                await settings.SaveAsync();
            }));

            sectionStack.Children.Add(SettingsUtils.GetToggleRow("Auto-check Updates", "Automatically check Nexus Mods and GitHub for new versions", settings.checkUpdatesOnLaunch, async value =>
            {
                settings.checkUpdatesOnLaunch = value;
                await settings.SaveAsync();
            }));

            sectionStack.Children.Add(SettingsUtils.GetToggleRow("One-click Updates", "Update mods immediately instead of with a confirmation popup", settings.skipUpdatePopup, async value =>
            {
                settings.skipUpdatePopup = value;
                await settings.SaveAsync();
            }));

            sectionStack.Children.Add(SettingsUtils.GetToggleRow("Auto-resolution", "Deploy conflicting files automatically according to the Load Order tab without opening the conflict popup", settings.autoResolveConflicts, async value =>
            {
                settings.autoResolveConflicts = value;
                await settings.SaveAsync();
            }));

            return cardBorder;
        }

        public Border GetConsoleSettings(Settings settings)
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Console", out Border cardBorder);
            sectionStack.Children.Add(SettingsUtils.GetToggleRow("Auto-scroll console output", "Automatically scroll the Play console when new info is sent", settings.autoScrollConsole, async value =>
            {
                settings.autoScrollConsole = value;
                await settings.SaveAsync();
            }));

            return cardBorder;
        }
        
        public Border GetDiscordSettings(Settings settings)
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Rich Presence", out Border cardBorder);
            sectionStack.Children.Add(SettingsUtils.GetToggleRow("Discord Rich Presence", "Display your current game, active profile, and mod count on Discord", settings.enableDiscordPresence, async value =>
            {
                settings.enableDiscordPresence = value;
                DiscordManager.SetEnabled(value);
                await settings.SaveAsync();
            }));

            return cardBorder;
        }

        #endregion
    }
}