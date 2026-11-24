using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Crescent.Plugins;
using Crescent.UI.ContextMenus;
using Crescent.UI.Controls.Tabs;
using Crescent.UI.Controls.Toasts;
using Crescent.UI.Dialogs;
using Crescent.UI.Dialogs.Responses;
using Newtonsoft.Json;
using Silk.Utilities;

namespace Crescent.UI.Controls.Sidebar
{
    public class ProfilePanel : Border
    {
        private Action? renderProfiles;
        public SidebarOverlay sidebar;
        public PluginData pluginData;

        public ProfilePanel(PluginData pluginData, SidebarOverlay sidebar)
        {
            this.pluginData = pluginData;
            this.sidebar = sidebar;

            Background = Colors.Button;
            BorderBrush = Colors.Separator;
            BorderThickness = new(1);
            CornerRadius = new(4);
            Padding = new(16);
            ClipToBounds = true;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            StackPanel contentStack = new()
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            Grid headerGrid = GetProfileHeader(pluginData);

            StackPanel gridHost = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new(4)
            };

            ScrollViewer profileScrollViewer = new()
            {
                Content = gridHost,
                MaxHeight = 200,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            renderProfiles = () => RenderProfiles(pluginData, gridHost);

            contentStack.Children.Add(headerGrid);
            contentStack.Children.Add(profileScrollViewer);
            Child = contentStack;

            renderProfiles.Invoke();
        }

        public void RenderProfiles() => renderProfiles?.Invoke();

        public Grid GetProfileHeader(PluginData plugin)
        {
            Grid headerGrid = new()
            {
                ColumnSpacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock profileTitle = new()
            {
                Text = "Profiles",
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(profileTitle, 0);

            CrescentButton addProfileButton = new(ButtonStyle.Transparent)
            {
                Content = "+",
                Width = 28,
                Height = 28,
                Padding = new(0),
                FontSize = 15
            };
            Grid.SetColumn(addProfileButton, 1);

            CrescentButton moreOptionsButton = new(ButtonStyle.Transparent)
            {
                Content = "⋮",
                Width = 28,
                Height = 28,
                Padding = new(0),
                FontSize = 14
            };
            Grid.SetColumn(moreOptionsButton, 2);

            addProfileButton.onClick += async () => await AddProfileAsync(plugin);
            moreOptionsButton.onClick += () =>
            {
                ContextMenu dropdownMenu = BuildHeaderDropdownMenu(plugin);
                dropdownMenu.Open(moreOptionsButton);
            };

            headerGrid.Children.Add(profileTitle);
            headerGrid.Children.Add(addProfileButton);
            headerGrid.Children.Add(moreOptionsButton);
            return headerGrid;
        }

        private ContextMenu BuildHeaderDropdownMenu(PluginData plugin)
        {
            ContextMenu menu = new()
            {
                Background = Colors.MainGradient,
                CornerRadius = new(8),
                ClipToBounds = true
            };

            MenuItem importItem = new()
            {
                Header = "Import Profile",
                CornerRadius = new CornerRadius(16),
                Margin = new(10, 5, 10, 0)
            };
            importItem.Click += async (_, _) => await ImportProfileAsync(plugin, sidebar);

            MenuItem exportSubMenu = new()
            {
                Header = "Export Profile",
                CornerRadius = new CornerRadius(16),
                Margin = new(10, 0, 10, 5)
            };

            List<ProfileData> allProfiles = plugin.Profiles;
            for (int index = 0; index < allProfiles.Count; index++)
            {
                ProfileData profile = allProfiles[index];
                bool isSelected = string.Equals(profile.name, plugin.selectedProfileName, StringComparison.OrdinalIgnoreCase);

                MenuItem profileExportItem = new()
                {
                    Header = isSelected ? $"{profile.name} (Active)" : profile.name,
                    Foreground = isSelected ? Colors.Accent : Brushes.White,
                    CornerRadius = new CornerRadius(16),
                    Margin = new(10, index == 0 ? 5 : 0, 10, index == allProfiles.Count - 1 ? 5 : 0)
                };

                profileExportItem.Click += async (_, _) => await ExportProfileAsync(plugin, profile, sidebar);
                exportSubMenu.Items.Add(profileExportItem);
            }

            menu.Items.Add(importItem);
            menu.Items.Add(exportSubMenu);
            return menu;
        }

        private void RenderProfiles(PluginData plugin, StackPanel gridHost)
        {
            gridHost.Children.Clear();
            List<ProfileData> profiles = plugin.Profiles;

            int columns = profiles.Count <= 1 ? 1 : 2;
            int rows = (int)Math.Ceiling(profiles.Count / (double)columns);

            Grid cartridgeGrid = new()
            {
                ColumnSpacing = 8,
                RowSpacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            for (int c = 0; c < columns; c++)
                cartridgeGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            for (int r = 0; r < rows; r++)
                cartridgeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (int index = 0; index < profiles.Count; index++)
            {
                ProfileData profile = profiles[index];
                bool isActive = string.Equals(profile.name, plugin.selectedProfileName, StringComparison.OrdinalIgnoreCase);

                ProfileButton profileButton = new(profile.name, isActive)
                {
                    ContextMenu = ProfileContextMenu.Get(plugin, profile, sidebar)
                };

                profileButton.onClick += async () => await SetProfileAsync(plugin, profile, sidebar.GetTab<ModsTab>());

                Grid.SetRow(profileButton, index / columns);
                Grid.SetColumn(profileButton, index % columns);
                cartridgeGrid.Children.Add(profileButton);
            }

            gridHost.Children.Add(cartridgeGrid);
        }

        public async Task SetProfileAsync(PluginData plugin, ProfileData profile, ModsTab? modsTab)
        {
            if (plugin.selectedProfileName == profile.name) return;

            DeploymentState previousState = plugin.deploymentState;
            if (previousState == DeploymentState.Deployed) plugin.Withdraw();

            plugin.selectedProfileName = profile.name;
            await plugin.SaveAsync();
            renderProfiles?.Invoke();

            if (previousState == DeploymentState.Deployed) await plugin.Deploy();
            modsTab ??= sidebar.GetTab<ModsTab>();
            modsTab?.Refresh();
            ToastManager.Show("Profile Switched", $"Active profile set to {profile.name}", ToastType.Info, 3000, plugin.Name);
        }

        public async Task AddProfileAsync(PluginData plugin)
        {
            Window? parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null) return;

            ConfirmationDialog confirmationDialog = new("New Profile", "Enter profile name", true, string.Empty);
            await confirmationDialog.ShowDialog(parentWindow);

            string newName = confirmationDialog.InputText?.Trim() ?? string.Empty;
            bool profileExists = plugin.Profiles.Any(profileItem => string.Equals(profileItem.name, newName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(newName) && !profileExists)
            {
                ProfileData newProfile = new()
                {
                    name = newName
                };

                plugin.Profiles.Add(newProfile);
                plugin.selectedProfileName = newProfile.name;
                await plugin.SaveAsync();

                renderProfiles?.Invoke();
                ModsTab? modsTab = sidebar.GetTab<ModsTab>();
                modsTab?.Refresh();
                ToastManager.Show("Profile Created", $"Created and switched to {newName}", ToastType.Success, 3000, plugin.Name);
            }
        }

        public static async Task ImportProfileAsync(PluginData pluginData, SidebarOverlay sidebarOverlay)
        {
            try
            {
                Window? parentWindow = TopLevel.GetTopLevel(sidebarOverlay) as Window;
                if (parentWindow == null) return;

                IStorageProvider storage = parentWindow.StorageProvider;
                IReadOnlyList<IStorageFile>? files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import Profile",
                    AllowMultiple = false
                });

                if (files.Count > 0)
                {
                    if (!files[0].Name.EndsWith(".crescent", StringComparison.OrdinalIgnoreCase))
                    {
                        ToastManager.Show("Invalid Profile Format", "The selected file is not a .crescent profile", ToastType.Error, 4000);
                        return;
                    }

                    string path = files[0].Path.LocalPath;
                    ExportedProfile? exportedProfile = await FileManager.ReadJsonAsync<ExportedProfile>(path);
                    if (exportedProfile == null || !string.Equals(exportedProfile.gameName, pluginData.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        ToastManager.Show("Game Mismatch", $"This profile belongs to \"{exportedProfile?.gameName}\" not \"{pluginData.Name}\"", ToastType.Error, 4500);
                        return;
                    }

                    ProfileData? targetProfile = pluginData.profiles.Find(profile => string.Equals(profile.name, exportedProfile.profileName, StringComparison.OrdinalIgnoreCase));
                    if (targetProfile == null)
                    {
                        targetProfile = new() { name = exportedProfile.profileName };
                        pluginData.profiles.Add(targetProfile);
                    }

                    pluginData.selectedProfileName = targetProfile.name;
                    await pluginData.SaveAsync();

                    ProfileImportDialog importDialog = new(pluginData, exportedProfile, targetProfile);
                    await importDialog.ShowDialog(parentWindow);

                    if (importDialog.ImportSuccess)
                    {
                        sidebarOverlay.profilePanel?.RenderProfiles();
                        sidebarOverlay.GetTab<ModsTab>()?.Refresh();
                        ToastManager.Show("Profile Imported", $"Imported profile \"{targetProfile.name}\"", ToastType.Success, 3500);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                ToastManager.Show("Import Failed", "An error occurred while importing the profile :(", ToastType.Error);
            }
        }

        public static async Task ExportProfileAsync(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            try
            {
                Window? parentWindow = TopLevel.GetTopLevel(sidebarOverlay) as Window;
                if (parentWindow == null) return;

                IStorageProvider storage = parentWindow.StorageProvider;
                IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = $"Export Profile ({profileData.name})",
                    SuggestedFileName = $"{profileData.name}.crescent",
                    DefaultExtension = "crescent",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Crescent Profiles")
                        {
                            Patterns = new[] { "*.crescent", "*.json" }
                        }
                    }
                });

                if (file != null)
                {
                    string path = file.Path.LocalPath;
                    ExportedProfile exportedProfile = new(pluginData, profileData);
                    string json = JsonConvert.SerializeObject(exportedProfile, Formatting.Indented);
                    await File.WriteAllTextAsync(path, json);
                    ToastManager.Show("Profile Exported", $"Saved \"{profileData.name}\" to {file.Name}", ToastType.Success, 3500, pluginData.Name);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                ToastManager.Show("Export Failed", "An error occurred while exporting the profile :(", ToastType.Error);
            }
        }
    }
}