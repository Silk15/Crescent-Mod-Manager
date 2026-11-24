using Avalonia;
using Avalonia.Controls;
using Crescent.Plugins;
using Crescent.UI.Controls.Tabs.Elements;

namespace Crescent.UI.Controls.Tabs
{
    public interface IModLayout
    {
        ModGridType GridType { get; }
        double EstimatedHeight { get; }
        Thickness Margin { get; }

        bool CanShowThumbnail(bool hasImage);

        Panel BuildMod(RenderedMod mod);
        Panel CreateContainer();

        void ReplaceUpdateControl(RenderedMod mod, Control oldControl, Control newControl);
    }
}