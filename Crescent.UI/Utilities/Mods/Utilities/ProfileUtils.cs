using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Crescent.Json.Serialization;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class ProfileUtils
    {
        public static async Task ImportProfileAsync(PluginData pluginData, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return;

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
                    if (!files[0].Name.EndsWith(".crescent", StringComparison.OrdinalIgnoreCase))
                    {
                        ToastManager.Show("Invalid Profile Format", "The selected file is not a .crescent profile", ToastType.Error, 4000);
                        return;
                    }

                    string path = files[0].Path.LocalPath;
                    ExportedProfile exportedProfile = await FileManager.ReadJsonAsync<ExportedProfile>(path);
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
                        sidebarOverlay.RenderProfiles();
                        modsTab.Refresh();
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
    }
}