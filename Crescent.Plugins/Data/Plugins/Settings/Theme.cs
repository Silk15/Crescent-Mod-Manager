using Newtonsoft.Json;

namespace Crescent.Plugins
{
    public abstract class Theme
    {
        [JsonIgnore] public abstract string BackgroundName { get; }
        [JsonIgnore] public abstract string PosterName { get; }
        [JsonIgnore] public abstract string BannerName { get; }
        public abstract string Accent { get; }
    }
}