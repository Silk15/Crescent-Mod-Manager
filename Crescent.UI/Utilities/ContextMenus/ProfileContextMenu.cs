using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Crescent.Plugins;
using Newtonsoft.Json;

namespace Crescent.UI
{
    public static class ProfileContextMenu
    {
        public static ContextMenu Get(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ContextMenu contextMenu = new();
            contextMenu.Background = Colors.MainGradient;
            contextMenu.CornerRadius = new(8);
            contextMenu.ClipToBounds = true;

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

        public static MenuItem? GetAddButton(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
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
                    foreach (ModData modData in pluginData.installedMods) 
                        profileData.SetModEnabled(modData, true);
                    
                    await pluginData.SaveAsync();
                    modsTab.Refresh();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };
            
            return addButton;
        }

        public static MenuItem? GetImportButton(PluginData pluginData, SidebarOverlay sidebarOverlay)
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
                await ProfileUtils.ImportProfileAsync(pluginData, sidebarOverlay);
            };
            
            return importButton;
        }

        public static MenuItem? GetExportButton(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
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
                        ExportedProfile exportedProfile = new(pluginData, profileData);
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
        
        public static MenuItem? GetRenameButton(PluginData pluginData, ProfileData profileData, SidebarOverlay sidebarOverlay)
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
                    string beforeName = pluginData.selectedProfileName;
                    Window? parentWindow = TopLevel.GetTopLevel(modsTab) as Window;
                    if (parentWindow != null)
                    {
                        ConfirmationDialog confirmationDialog = new("Rename Profile", "Enter a new name", true, profileData.name);
                        await confirmationDialog.ShowDialog(parentWindow);
                        string newName = confirmationDialog.InputText;
                        
                        if (!string.IsNullOrEmpty(newName) && newName != profileData.name) 
                            profileData.name = newName;
                    }

                    pluginData.selectedProfileName = beforeName == "Default" ? "Default" : profileData.name;
                    await pluginData.SaveAsync();
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

        public static MenuItem? GetDeleteButton(PluginData plugin, ProfileData profile, SidebarOverlay sidebarOverlay)
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
                    plugin.selectedProfileName = "Default";
                    plugin.profiles.Remove(profile);
                    
                    await plugin.SaveAsync();
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