namespace Crescent.Plugins
{
    public abstract class Theme
    {
        public string backgroundName;
        public string posterName;
        
        public event Action? onThemeAssetsUpdated;
        
        public void NotifyAssetsUpdated() => onThemeAssetsUpdated?.Invoke();
        
        public abstract string GetBackgroundName();
        public abstract string GetPosterName();
    }
}