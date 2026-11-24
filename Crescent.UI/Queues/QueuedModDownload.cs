using Crescent.Plugins;

namespace Crescent.UI.Queues
{
    public class QueuedDownloadItem
    {
        public PluginData PluginData { get; set; } = null!;
        public string GameDomain { get; set; } = string.Empty;
        public string UrlOrNxm { get; set; } = string.Empty;
        public string ModName { get; set; } = string.Empty;
        public string NexusId { get; set; } = string.Empty;
    }
}