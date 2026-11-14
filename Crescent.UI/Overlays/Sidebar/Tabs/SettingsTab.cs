using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class SettingsTab : StackPanel, ISidebarTab
    {
        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;

        public int Order => 4;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Settings",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            this.pluginData = pluginData;
            this.sidebarOverlay = sidebarOverlay;
            Setup();
        }

        public void OnClick() => Refresh();

        public void Refresh()
        {
            Children.Clear();
            Setup();
        }

        public void Setup()
        {
            if (pluginData == null) return;

            Margin = new Thickness(30, 15, 30, 30);
            Spacing = 22;
            Background = Brushes.Transparent;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            Children.Add(SettingsUtils.GetHeader($"{pluginData.Name} Settings"));
            Children.Add(GetPathsSettings());
        }

        #region Paths

        public Border GetPathsSettings()
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Directories & Executable", out Border cardBorder);

            sectionStack.Children.Add(GetExePathRow());
            sectionStack.Children.Add(GetStagingPathRow());
            sectionStack.Children.Add(GetDeploymentPathRow());

            return cardBorder;
        }

        public StackPanel GetExePathRow()
        {
            return SettingsUtils.GetPathRow(this, pluginData!, "Game Executable Path", pluginData!.ExePath, nameof(pluginData.ExePath), async path =>
            {
                if (pluginData.ExePath == path) return;
                pluginData.ExePath = path;
                await pluginData.SaveAsync();
                ToastManager.Show("Path Saved", "Updated game executable path", ToastType.Success, 2500, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
            }, true);
        }

        public StackPanel GetStagingPathRow()
        {
            return SettingsUtils.GetPathRow(this, pluginData!, "Mod Staging Folder", pluginData!.StagingPath, nameof(pluginData.StagingPath), async path =>
            {
                if (pluginData.StagingPath == path) return;
                pluginData.StagingPath = path;
                await pluginData.SaveAsync();
                ToastManager.Show("Path Saved", "Updated mod staging folder", ToastType.Success, 2500, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
            }, false);
        }

        public StackPanel GetDeploymentPathRow()
        {
            return SettingsUtils.GetPathRow(this, pluginData!, "Target Folder", pluginData!.DeploymentPath, nameof(pluginData.DeploymentPath), async path =>
            {
                if (pluginData.DeploymentPath == path) return;
                pluginData.DeploymentPath = path;
                await pluginData.SaveAsync();
                ToastManager.Show("Path Saved", "Updated target deployment folder", ToastType.Success, 2500, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
            }, false);
        }

        #endregion
    }
}