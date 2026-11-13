using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class LogsTab : StackPanel, ISidebarTab
    {
        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;
        
        public int Order => 1;
        
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