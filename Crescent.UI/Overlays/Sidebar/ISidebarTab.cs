using Crescent.Plugins;

namespace Crescent.UI
{
    public interface ISidebarTab
    {
        public const int TopBarHeight = 100;
        public Tab Tab { get; }
        public int Order { get; }
        
        public CrescentButton GetButton();
        public void OnLoaded(SidebarOverlay sidebarOverlay, GameData gameData);
        public void OnClick();
    }
}