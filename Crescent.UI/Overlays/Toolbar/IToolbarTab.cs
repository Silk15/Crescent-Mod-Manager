namespace Crescent.UI
{
    public interface IToolbarTab
    {
        public CrescentButton GetButton();
        public void OnLoaded(ToolbarOverlay toolbarOverlay);
        public void OnClick();
    }
}