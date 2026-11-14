using Avalonia;
using Avalonia.Controls;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class ModDownloadDialog : Dialog
    {
        public bool DownloadSuccess { get; private set; }

        public ModDownloadDialog(PluginData pluginData, string modPageUrl)
        {
            Width = 1;
            Height = 1;
            WindowDecorations = WindowDecorations.None;
            ShowInTaskbar = false;
            Opacity = 0;

            Opened += async (_, _) =>
            {
                Hide();
                DownloadSuccess = await NxmProtocolHandler.DownloadUrlOrNxmAsync(modPageUrl, pluginData);
                Close();
            };
        }
    }

    public class RequirementNode
    {
        public string NexusId { get; set; } = string.Empty;
        public string ModName { get; set; } = string.Empty;
        public int Depth { get; set; }
        public List<RequirementNode> Children { get; set; } = new();
    }
}