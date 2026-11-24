using Crescent.Plugins;
using Crescent.UI.Controls.Sidebar;

namespace Crescent.UI.Controls.Tabs
{
    public interface ISidebarTab
    {
        public int Order { get; }
        
        public CrescentButton GetButton();
        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData);
        public void OnClick();
    }
}