using Avalonia.Controls;
using Crescent.Plugins;

namespace Crescent.UI
{
    public static class ProfileContextMenu
    {
        public static ContextMenu Get(GameData gameData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ContextMenu contextMenu = new();
            contextMenu.Background = Colors.MainPanelEnd;
            contextMenu.CornerRadius = new(8);
            contextMenu.ClipToBounds = true;

            contextMenu.Items.Add(GetRenameButton(gameData, profileData, sidebarOverlay));
            contextMenu.Items.Add(GetDeleteButton(gameData, profileData, sidebarOverlay));
            return contextMenu;
        }

        #region Buttons
        
        public static CrescentMenuItem? GetRenameButton(GameData gameData, ProfileData profileData, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return null;
            
            CrescentMenuItem renameButton = new()
            {
                Header = "Rename"
            };

            renameButton.onClick += async () =>
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
                    sidebarOverlay.renderProfiles?.Invoke();
                    modsTab.Refresh();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };
            
            return renameButton;
        }

        public static CrescentMenuItem? GetDeleteButton(GameData game, ProfileData profile, SidebarOverlay sidebarOverlay)
        {
            ModsTab? modsTab = sidebarOverlay.GetTab<ModsTab>();
            if (modsTab == null) return null;

            CrescentMenuItem deleteButton = new()
            {
                Header = "Delete"
            };

            deleteButton.onClick += async () =>
            {
                try
                {
                    game.selectedProfileName = "Default";
                    game.profiles.Remove(profile);
                    
                    await game.SaveAsync();
                    sidebarOverlay.renderProfiles?.Invoke();
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