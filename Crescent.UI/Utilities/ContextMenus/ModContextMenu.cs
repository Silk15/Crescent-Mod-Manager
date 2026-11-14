using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public static class ModContextMenu
    {
        public static ContextMenu Get(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            ContextMenu contextMenu = new()
            {
                Background = Colors.MainGradient,
                CornerRadius = new(8),
                ClipToBounds = true
            };

            contextMenu.Items.Add(GetNexusOpenButton(modData));
            contextMenu.Items.Add(GetOpenFilesButton(modData));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetUpdateButton(pluginData, modData, modsTab));
            contextMenu.Items.Add(GetCheckButton(pluginData, modData, modsTab));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetProfilesButton(pluginData, modData, modsTab));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetRenameButton(pluginData, modData, modsTab));
            contextMenu.Items.Add(GetDeleteButton(pluginData, modData, modsTab));
            return contextMenu;
        }

        #region Buttons

        public static MenuItem GetNexusOpenButton(ModData modData)
        {
            MenuItem nexusMenuItem = new()
            {
                Header = "Open Nexus",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 5, 10, 0)
            };
            nexusMenuItem.Click += (_, _) => modData.url.nexusUrl.TryOpen();
            return nexusMenuItem;
        }

        public static MenuItem GetOpenFilesButton(ModData modData)
        {
            MenuItem openFolderMenuItem = new()
            {
                Header = "Open Folder",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            openFolderMenuItem.Click += (_, _) => modData.filePath.TryOpen();
            return openFolderMenuItem;
        }

        public static MenuItem GetProfilesButton(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            MenuItem profileMenu = new()
            {
                Header = "Profiles",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            List<ProfileData> allProfiles = pluginData.Profiles;
            for (int index = 0; index < allProfiles.Count; index++)
            {
                ProfileData profile = allProfiles[index];
                bool isEnabledInProfile = profile.IsModEnabled(modData);
                MenuItem profileItem = new()
                {
                    Header = $"{profile.name} {(isEnabledInProfile ? " (Enabled)" : " (Disabled)")}",
                    Foreground = isEnabledInProfile ? Brushes.LightGreen : Brushes.IndianRed,
                    CornerRadius = new CornerRadius(8)
                };

                profileItem.Click += async (_, _) =>
                {
                    bool newState = !profile.IsModEnabled(modData);
                    profile.SetModEnabled(modData, newState);
                    await pluginData.SaveAsync();
                    modsTab.RenderMods();
                };

                profileMenu.Items.Add(profileItem);
                profileItem.Margin = new(10, index == 0 ? 5 : 0, 10, index == allProfiles.Count - 1 ? 5 : 0);
            }

            return profileMenu;
        }

        public static MenuItem GetUpdateButton(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            string cleanLatestVersion = (modData.LatestVersion ?? string.Empty).Trim().TrimStart('v', 'V');
            string versionSuffix = modData.UpdateAvailable && !string.IsNullOrEmpty(cleanLatestVersion)
                ? $" (v{cleanLatestVersion})"
                : string.Empty;

            MenuItem updateButton = new()
            {
                Header = $"Update{versionSuffix}",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            updateButton.Click += async (_, _) =>
            {
                if (!modData.UpdateAvailable) return;
                try
                {
                    Window? window = TopLevel.GetTopLevel(modsTab) as Window;
                    if (window != null)
                    {
                        ModUpdateDialog updateDialog = new(modData);
                        await updateDialog.ShowDialog(window);
                        if (!updateDialog.UpdateConfirmed) return;
                    }

                    await ModInteractionUtils.UpdateAsync(pluginData, modData);
                    modsTab.Refresh();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
            };

            return updateButton;
        }

        public static MenuItem GetCheckButton(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            MenuItem checkButton = new()
            {
                Header = "Check",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            RenderedMod? renderedMod = modsTab.renderedMods.Find(item => item.modData == modData);
            checkButton.Click += async (_, _) =>
            {
                await modData.CheckForUpdateAsync(pluginData.API.Name);
                if (renderedMod != null) renderedMod.RefreshMod();
            };

            return checkButton;
        }

        public static MenuItem? GetRenameButton(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            MenuItem renameButton = new()
            {
                Header = "Rename",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            renameButton.Click += async (_, _) =>
            {
                Window? parentWindow = TopLevel.GetTopLevel(modsTab) as Window;
                if (parentWindow != null)
                {
                    string oldName = modData.DisplayName;
                    ConfirmationDialog renameDialog = new("Rename Mod", "Enter new name for mod:", true, oldName);
                    await renameDialog.ShowDialog(parentWindow);
                    string newName = renameDialog.InputText;

                    if (!string.IsNullOrEmpty(newName) && newName != oldName)
                    {
                        modData.displayName = newName;
                        await pluginData.SaveAsync();
                        modsTab.RenderMods();
                    }
                }
            };

            return renameButton;
        }

        public static MenuItem GetDeleteButton(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            MenuItem deleteButton = new()
            {
                Header = "Delete",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 5)
            };

            deleteButton.Click += async (_, _) =>
            {
                string deletedDisplayName = modData.DisplayName.Cleaned();
                ModInteractionUtils.WithdrawModFiles(pluginData, modData);
                modData.DeleteFiles(pluginData.StagingPath);
                pluginData.RemoveMod(modData);

                await pluginData.SaveAsync();
                modsTab.Refresh();
                ToastManager.Show("Mod Deleted", $"Removed \"{deletedDisplayName}\" and all staging files", ToastType.Info, 3500, pluginData.Name);
            };

            return deleteButton;
        }

        #endregion
    }
}