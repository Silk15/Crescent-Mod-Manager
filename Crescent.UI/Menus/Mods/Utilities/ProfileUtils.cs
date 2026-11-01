using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Crescent.Json.Serialization;
using Crescent.Plugins;
using Newtonsoft.Json;

namespace Crescent.UI
{
    public class ProfileUtils
    {
        public static async Task ImportProfileAsync(GameData gameData, SidebarOverlay sidebarOverlay)
        {
            ModsTab modsTab = sidebarOverlay.GetTab<ModsTab>()!;
            try
            {
                Window? parentWindow = TopLevel.GetTopLevel(modsTab) as Window;
                if (parentWindow == null) return;

                IStorageProvider storage = parentWindow.StorageProvider;
                IReadOnlyList<IStorageFile>? files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import Profile",
                    AllowMultiple = false
                });
                
                if (files.Count > 0)
                {
                    if (!files.FirstOrDefault()!.Name.EndsWith(".crescent"))
                    {
                        ConfirmationDialog errorDialog = new("Invalid Format!", $"This file is not a \".crescent\" profile, nothing will be imported.", false);
                        await errorDialog.ShowDialog(parentWindow);
                        return;
                    }
                    
                    string path = files[0].Path.LocalPath;
                    ExportedProfile exportedProfile = await FileManager.ReadJson<ExportedProfile>(path);
                    if (!string.Equals(exportedProfile.gameName, gameData.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        ConfirmationDialog errorDialog = new("Invalid Game", $"This profile belongs to \"{exportedProfile.gameName}\" but the current game is \"{gameData.Name}\"", false);
                        await errorDialog.ShowDialog(parentWindow);
                        return;
                    }
                    
                    ProfileData? targetProfile = gameData.profiles.Find(p => string.Equals(p.name, exportedProfile.profileName, StringComparison.OrdinalIgnoreCase));
                    if (targetProfile == null)
                    {
                        targetProfile = new ProfileData { name = exportedProfile.profileName };
                        gameData.profiles.Add(targetProfile);
                    }

                    gameData.selectedProfileName = targetProfile.name;
                    await gameData.SaveAsync();

                    ProfileImportDialog importDialog = new(gameData, exportedProfile, targetProfile);
                    await importDialog.ShowDialog(parentWindow);

                    if (importDialog.ImportSuccess)
                    {
                        sidebarOverlay.RenderProfiles();
                        modsTab.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
    }
}