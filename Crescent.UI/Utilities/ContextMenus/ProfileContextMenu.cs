using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Crescent.Plugins;
using Newtonsoft.Json;

namespace Crescent.UI
{
    public static class ProfileContextMenu
    {
        public static ContextMenu Get(GameData gameData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ContextMenu contextMenu = new();
            contextMenu.Background = Colors.MainGradient;
            contextMenu.CornerRadius = new(8);
            contextMenu.ClipToBounds = true;

            contextMenu.Items.Add(GetAddButton(gameData, profileData, sidebarOverlay));
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });
            
            contextMenu.Items.Add(GetImportButton(gameData, sidebarOverlay));
            contextMenu.Items.Add(GetExportButton(gameData, profileData, sidebarOverlay));
            
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });
            
            contextMenu.Items.Add(GetRenameButton(gameData, profileData, sidebarOverlay));
            contextMenu.Items.Add(GetDeleteButton(gameData, profileData, sidebarOverlay));
            return contextMenu;
        }

        #region Buttons

        public static MenuItem? GetAddButton(GameData gameData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return null;
            
            MenuItem addButton = new()
            {
                Header = "Add All Installed Mods",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 5, 10, 0)
            };

            addButton.Click += async (_, _) =>
            {
                try
                {
                    foreach (ModData modData in gameData.installedMods) 
                        profileData.SetModEnabled(modData, true);
                    
                    await gameData.SaveAsync();
                    modsTab.Refresh();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };
            
            return addButton;
        }

        public static MenuItem? GetImportButton(GameData gameData, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return null;
            
            MenuItem importButton = new()
            {
                Header = "Import Profile",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            importButton.Click += async (_, _) =>
            {
                await ProfileUtils.ImportProfileAsync(gameData, sidebarOverlay);
            };
            
            return importButton;
        }

        public static MenuItem? GetExportButton(GameData gameData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return null;
            
            MenuItem exportButton = new()
            {
                Header = "Export Profile",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            exportButton.Click += async (_, _) =>
            {
                try
                {
                    Window? parentWindow = TopLevel.GetTopLevel(modsTab) as Window;
                    if (parentWindow == null) return;

                    IStorageProvider storage = parentWindow.StorageProvider;
                    IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export Profile",
                        SuggestedFileName = $"{profileData.name}.crescent",
                        DefaultExtension = "crescent",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("Crescent Profiles")
                            {
                                Patterns = new []
                                {
                                    ".crescent",
                                    ".json"
                                }
                            }
                        }
                    });

                    if (file != null)
                    {
                        string path = file.Path.LocalPath;
                        ExportedProfile exportedProfile = new(gameData, profileData);
                        string json = JsonConvert.SerializeObject(exportedProfile, Formatting.Indented);
                        await File.WriteAllTextAsync(path, json);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };
            
            return exportButton;
        }
        
        public static MenuItem? GetRenameButton(GameData gameData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return null;
            
            MenuItem renameButton = new()
            {
                Header = "Rename",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 0)
            };

            renameButton.Click += async (_, _) =>
            {
                try
                {
                    string beforeName = gameData.selectedProfileName;
                    Window? parentWindow = TopLevel.GetTopLevel(modsTab) as Window;
                    if (parentWindow != null)
                    {
                        ConfirmationDialog confirmationDialog = new("Rename Profile", "Enter a new name", true, profileData.name);
                        await confirmationDialog.ShowDialog(parentWindow);
                        string newName = confirmationDialog.InputText;
                        
                        if (!string.IsNullOrEmpty(newName) && newName != profileData.name) 
                            profileData.name = newName;
                    }

                    gameData.selectedProfileName = beforeName == "Default" ? "Default" : profileData.name;
                    await gameData.SaveAsync();
                    sidebarOverlay.RenderProfiles();
                    modsTab.Refresh();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };
            
            return renameButton;
        }

        public static MenuItem? GetDeleteButton(GameData game, ProfileData profile, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return null;

            MenuItem deleteButton = new()
            {
                Header = "Delete",
                CornerRadius = new CornerRadius(8),
                Margin = new(10, 0, 10, 5)
            };

            deleteButton.Click += async (_, _) =>
            {
                try
                {
                    game.selectedProfileName = "Default";
                    game.profiles.Remove(profile);
                    
                    await game.SaveAsync();
                    sidebarOverlay.RenderProfiles();
                    modsTab.Refresh();
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