using Avalonia;
using Avalonia.Controls;
using Crescent.Plugins;
using Crescent.UI.Controls.Sidebar;
using Crescent.UI.Controls.Tabs;
using Crescent.UI.Dialogs.Responses;
using Silk.Utilities;

namespace Crescent.UI.ContextMenus
{
    public static class ProfileContextMenu
    {
        public static ContextMenu Get(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ContextMenu contextMenu = new()
            {
                Background = Colors.MainGradient,
                CornerRadius = new(8),
                ClipToBounds = true
            };

            contextMenu.Items.Add(GetAddButton(pluginData, profileData, sidebarOverlay));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetImportButton(pluginData, sidebarOverlay));
            contextMenu.Items.Add(GetExportButton(pluginData, profileData, sidebarOverlay));

            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetRenameButton(pluginData, profileData, sidebarOverlay));
            contextMenu.Items.Add(GetDeleteButton(pluginData, profileData, sidebarOverlay));
            return contextMenu;
        }

        #region Buttons

        public static MenuItem GetAddButton(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            MenuItem addButton = new()
            {
                Header = "Enable All Installed Mods",
                CornerRadius = new CornerRadius(4),
                Margin = new(10, 5, 10, 0)
            };

            addButton.Click += async (_, _) =>
            {
                try
                {
                    foreach (ModData modData in pluginData.installedMods)
                        profileData.SetModEnabled(modData, true);

                    await pluginData.SaveAsync();
                    sidebarOverlay.profilePanel?.RenderProfiles();
                    sidebarOverlay.GetTab<ModsTab>()?.Refresh();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };

            return addButton;
        }

        public static MenuItem GetImportButton(PluginData pluginData, SidebarOverlay sidebarOverlay)
        {
            MenuItem importButton = new()
            {
                Header = "Import Profile",
                CornerRadius = new CornerRadius(4),
                Margin = new(10, 0, 10, 0)
            };

            importButton.Click += async (_, _) => { await ProfilePanel.ImportProfileAsync(pluginData, sidebarOverlay); };

            return importButton;
        }

        public static MenuItem GetExportButton(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            MenuItem exportButton = new()
            {
                Header = "Export Profile",
                CornerRadius = new CornerRadius(4),
                Margin = new(10, 0, 10, 0)
            };

            exportButton.Click += async (_, _) => { await ProfilePanel.ExportProfileAsync(pluginData, profileData, sidebarOverlay); };

            return exportButton;
        }

        public static MenuItem GetRenameButton(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            MenuItem renameButton = new()
            {
                Header = "Rename",
                CornerRadius = new CornerRadius(4),
                Margin = new(10, 0, 10, 0)
            };

            renameButton.Click += async (_, _) =>
            {
                try
                {
                    bool wasSelected = string.Equals(pluginData.selectedProfileName, profileData.name, StringComparison.OrdinalIgnoreCase);
                    Window? parentWindow = TopLevel.GetTopLevel(sidebarOverlay) as Window;
                    if (parentWindow != null)
                    {
                        ConfirmationDialog confirmationDialog = new("Rename Profile", "Enter a new name", true, profileData.name);
                        await confirmationDialog.ShowDialog(parentWindow);
                        string? newName = confirmationDialog.InputText?.Trim();

                        if (!string.IsNullOrEmpty(newName) && !string.Equals(newName, profileData.name, StringComparison.OrdinalIgnoreCase))
                        {
                            profileData.name = newName;
                            if (wasSelected)
                                pluginData.selectedProfileName = newName;

                            await pluginData.SaveAsync();
                            sidebarOverlay.profilePanel?.RenderProfiles();
                            sidebarOverlay.GetTab<ModsTab>()?.Refresh();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };

            return renameButton;
        }

        public static MenuItem GetDeleteButton(PluginData plugin, ProfileData profile, SidebarOverlay sidebarOverlay)
        {
            MenuItem deleteButton = new()
            {
                Header = "Delete",
                CornerRadius = new CornerRadius(4),
                Margin = new(10, 0, 10, 5),
                IsEnabled = plugin.Profiles.Count > 1 && !string.Equals(profile.name, "Default", StringComparison.OrdinalIgnoreCase)
            };

            deleteButton.Click += async (_, _) =>
            {
                try
                {
                    if (plugin.Profiles.Count <= 1) return;

                    plugin.profiles.Remove(profile);
                    if (string.Equals(plugin.selectedProfileName, profile.name, StringComparison.OrdinalIgnoreCase))
                        plugin.selectedProfileName = plugin.Profiles.FirstOrDefault()?.name ?? "Default";

                    await plugin.SaveAsync();
                    sidebarOverlay.profilePanel?.RenderProfiles();
                    sidebarOverlay.GetTab<ModsTab>()?.Refresh();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };

            return deleteButton;
        }

        #endregion
    }
}