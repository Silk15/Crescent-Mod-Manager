using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public static class ModContextMenu
    {
        public static ContextMenu Get(GameData gameData, ModData modData, ModsTab modsTab)
        {
            ContextMenu contextMenu = new();
            contextMenu.Background = Colors.MainGradient;
            contextMenu.CornerRadius = new(8);
            contextMenu.ClipToBounds = true;

            contextMenu.Items.Add(GetNexusOpenButton(modData));
            contextMenu.Items.Add(GetOpenFilesButton(modData));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetUpdateButton(gameData, modData, modsTab));
            contextMenu.Items.Add(GetCheckButton(gameData, modData, modsTab));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetProfilesButton(gameData, modData, modsTab));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(GetRenameButton(gameData, modData, modsTab));
            contextMenu.Items.Add(GetDeleteButton(gameData, modData, modsTab));
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

        public static MenuItem GetProfilesButton(GameData gameData, ModData modData, ModsTab modsTab)
        {
            MenuItem profileMenu = new()
            {
                Header = "Profiles",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            List<ProfileData> allProfiles = gameData.Profiles;
            for (int i = 0; i < allProfiles.Count; i++)
            {
                ProfileData profile = allProfiles[i];
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
                    await gameData.SaveAsync();
                    modsTab.RenderMods();
                };
                
                profileMenu.Items.Add(profileItem);
                profileItem.Margin = new(10, i == 0 ? 5 : 0, 10, i == allProfiles.Count - 1 ? 5 : 0);
            }
            
            return profileMenu;
        }

        public static MenuItem GetUpdateButton(GameData gameData, ModData modData, ModsTab modsTab)
        {
            MenuItem updateButton = new()
            {
                Header = "Update",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            RenderedMod? renderedMod = modsTab.renderedMods.Find(item => item.modData == modData);
            updateButton.Click += async (_, _) =>
            {
                if (modData.UpdateAvailable)
                {
                    try
                    {
                        Window? window = TopLevel.GetTopLevel(modsTab) as Window;
                        if (window != null)
                        {
                            ModUpdateDialog updateDialog = new(modData);
                            await updateDialog.ShowDialog(window);
                            if (!updateDialog.UpdateConfirmed) return;
                        }

                        await ModInteractionUtils.UpdateAsync(gameData, modData);
                        if (renderedMod != null) renderedMod.RefreshMod();
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception.ToString());
                    }
                }
            };
            
            return updateButton;
        }

        public static MenuItem GetCheckButton(GameData gameData, ModData modData, ModsTab modsTab)
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
                await modData.CheckForUpdateAsync(gameData.NexusName);
                if (renderedMod != null) renderedMod.RefreshMod();
            };
            
            return checkButton;
        }

        public static MenuItem? GetRenameButton(GameData gameData, ModData modData, ModsTab modsTab)
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
                    string oldName = modData.displayName;
                    ConfirmationDialog renameDialog = new("Rename Mod", "Enter new name for mod folder/archive:", true, modData.displayName);
                    await renameDialog.ShowDialog(parentWindow);
                    string newName = renameDialog.InputText;
                    
                    if (!string.IsNullOrEmpty(newName) && newName != oldName)
                    {
                        modData.displayName = newName;
                        await gameData.SaveAsync();
                    }
                }
            };
            
            return renameButton;
        }

        public static MenuItem GetDeleteButton(GameData gameData, ModData modData, ModsTab modsTab)
        {
            MenuItem deleteButton = new()
            {
                Header = "Delete",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 5)
            };
            
            deleteButton.Click += async (_, _) =>
            {
                gameData.WithdrawMod(modData);
                gameData.installedMods.Remove(modData);
                modData.DeleteFiles(gameData.StagingPath);
                
                await gameData.SaveAsync();
                modsTab.RenderMods();
            };
            
            return deleteButton;
        }
        
        #endregion
    }
}