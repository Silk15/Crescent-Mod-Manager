using Newtonsoft.Json;

namespace Crescent.Plugins
{
    public abstract class API
    {
        [JsonIgnore] public abstract string Name { get; }
        [JsonIgnore] public virtual string BaseUrl => $"https://www.nexusmods.com/{Name}/mods/";
    }
}