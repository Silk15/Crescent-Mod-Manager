using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Crescent.Plugins;
using Crescent.UI.Controls.Sidebar;

namespace Crescent.UI.Controls.Tabs
{
    public class LogsTab : StackPanel, ISidebarTab
    {
        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;
        
        public int Order => 3;
        
        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Logs",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            this.pluginData = pluginData;
            this.sidebarOverlay = sidebarOverlay;
        }
        
        public void OnClick()
        {
            
        }
    }
}