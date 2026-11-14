using System.IO.Compression;
using System.Text.Json;
using Newtonsoft.Json;

namespace Crescent.Plugins
{
    public class GithubPluginInfo
    {
        public static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static List<GithubPluginInfo>? cachedPlugins;

        public string? name;
        public byte[]? pluginBytes;
        public string? posterPath;
        public string? version;

        public static async Task<List<GithubPluginInfo>> GetAvailablePluginsAsync(bool forceRefresh = false)
        {
            if (cachedPlugins != null && !forceRefresh) return cachedPlugins;

            List<GithubPluginInfo> plugins = new();
            if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
                HttpClient.DefaultRequestHeaders.Add("User-Agent", "Crescent");

            string json = await HttpClient.GetStringAsync("https://api.github.com/repos/Silk15/Crescent-Mod-Manager/contents?ref=plugins");
            using JsonDocument document = JsonDocument.Parse(json);

            List<Task<GithubPluginInfo?>> downloadTasks = new();
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                string? fileName = element.GetProperty("name").GetString();
                if (fileName == null || !fileName.EndsWith(".plugin", StringComparison.OrdinalIgnoreCase)) continue;

                string? downloadUrl = element.GetProperty("download_url").GetString();
                if (string.IsNullOrEmpty(downloadUrl)) continue;

                downloadTasks.Add(GetPluginInfoFromUrlAsync(fileName, downloadUrl));
            }

            GithubPluginInfo?[] results = await Task.WhenAll(downloadTasks);
            for (int index = 0; index < results.Length; index++)
                if (results[index] != null)
                    plugins.Add(results[index]!);

            cachedPlugins = plugins;
            return plugins;
        }

        public static async Task<GithubPluginInfo?> GetPluginInfoFromUrlAsync(string fileName, string downloadUrl)
        {
            try
            {
                byte[] bytes = await HttpClient.GetByteArrayAsync(downloadUrl);
                string version = "1.0.0";
                string pluginName = fileName.Replace(".plugin", "");
                string? posterSubPath = null;

                using MemoryStream stream = new(bytes);
                using ZipArchive archive = new(stream, ZipArchiveMode.Read);
                ZipArchiveEntry? manifestEntry = archive.Entries.FirstOrDefault(entry => entry.Name.Equals("plugin.json", StringComparison.OrdinalIgnoreCase));

                if (manifestEntry != null)
                {
                    await using Stream entryStream = manifestEntry.Open();
                    using StreamReader reader = new(entryStream);
                    string manifestJson = await reader.ReadToEndAsync();
                    PluginManifest? manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestJson);

                    if (manifest != null)
                    {
                        if (!string.IsNullOrEmpty(manifest.name)) pluginName = manifest.name;
                        if (!string.IsNullOrEmpty(manifest.version)) version = manifest.version;
                        posterSubPath = manifest.poster;
                    }
                }

                return new()
                {
                    name = pluginName,
                    version = version,
                    posterPath = posterSubPath,
                    pluginBytes = bytes
                };
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return null;
            }
        }
    }
}