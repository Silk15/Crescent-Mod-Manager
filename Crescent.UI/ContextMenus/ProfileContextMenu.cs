using Avalonia.Controls;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public static class ProfileContextMenu
    {
        public static ContextMenu Get(Mods? mods, Sidebar sidebar, GameData game, ProfileData profile)
        {
            ContextMenu contextMenu = new();
            contextMenu.Background = Colors.MainPanelEnd;
            contextMenu.CornerRadius = new(8);
            contextMenu.ClipToBounds = true;
            MenuItem renameButton = new()
            {
                Header = "Rename"
            };

            renameButton.Click += async (sender, args) =>
            {
                try
                {
                    string beforeName = game.selectedProfileName;
                    Window? parentWindow = TopLevel.GetTopLevel(mods) as Window;
                    if (parentWindow != null)
                    {
                        InputDialog renameDialog = new("Rename Profile", "Enter new name for profile:", true, profile.name);
                        await renameDialog.ShowDialog(parentWindow);
                        string newName = renameDialog.InputText;
                        if (!string.IsNullOrEmpty(newName) && newName != profile.name) profile.name = newName;
                    }

                    game.selectedProfileName = beforeName == "Default" ? "Default" : profile.name;
                    await game.SaveAsync();
                    sidebar.renderProfiles?.Invoke();
                    if (mods != null) mods.RefreshPanel();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };

            MenuItem deleteButton = new()
            {
                Header = "Delete"
            };

            deleteButton.Click += async (sender, args) =>
            {
                try
                {
                    game.selectedProfileName = "Default";
                    game.profiles.Remove(profile);
                    
                    await game.SaveAsync();
                    sidebar.renderProfiles?.Invoke();
                    if (mods != null) mods.RefreshPanel();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            };

            contextMenu.Items.Add(renameButton);
            contextMenu.Items.Add(deleteButton);
            return contextMenu;
        }
    }
}