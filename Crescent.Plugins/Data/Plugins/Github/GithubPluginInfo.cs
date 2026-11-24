using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Silk.Utilities;

namespace Crescent.Plugins
{
    public class GithubPluginInfo
    {
        public static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static List<GithubPluginInfo>? cachedPlugins;

        public string? name;
        public string? downloadUrl;
        public byte[]? pluginBytes;
        public string? posterPath;
        public string? version;

        public static async Task<List<GithubPluginInfo>> GetAvailablePluginsAsync(bool forceRefresh = false)
        {
            if (cachedPlugins != null && !forceRefresh) return cachedPlugins;

            List<GithubPluginInfo> plugins = new();
            if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent")) HttpClient.DefaultRequestHeaders.Add("User-Agent", "Crescent");

            string json = await HttpClient.GetStringAsync("https://api.github.com/repos/Silk15/Crescent-Mod-Manager/contents?ref=plugins");
            JArray contents = JArray.Parse(json);

            List<Task<GithubPluginInfo?>> downloadTasks = new();
            for (int index = 0; index < contents.Count; index++)
            {
                JToken element = contents[index];
                string? fileName = element["name"]?.ToString();
                if (fileName == null || !fileName.EndsWith(".plugin", StringComparison.OrdinalIgnoreCase)) continue;

                string? url = element["download_url"]?.ToString();
                if (string.IsNullOrEmpty(url)) continue;

                downloadTasks.Add(GetPluginInfoFromUrlAsync(fileName, url));
            }

            GithubPluginInfo?[] results = await Task.WhenAll(downloadTasks);
            for (int index = 0; index < results.Length; index++)
            {
                if (results[index] != null) 
                    plugins.Add(results[index]!);
            }

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

                using (MemoryStream stream = new(bytes))
                using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
                {
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
                }

                string normalizedName = NormalizeName(pluginName);
                bool isInstalled = PluginData.InstalledPlugins.Any(plugin => NormalizeName(plugin.Name) == normalizedName);

                return new()
                {
                    name = pluginName,
                    version = version,
                    posterPath = posterSubPath,
                    downloadUrl = downloadUrl,
                    pluginBytes = isInstalled ? null : bytes
                };
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return null;
            }
        }

        public static async Task<byte[]?> DownloadPluginBytesAsync(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
                    HttpClient.DefaultRequestHeaders.Add("User-Agent", "Crescent");

                return await HttpClient.GetByteArrayAsync(url);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return null;
            }
        }

        public static string NormalizeName(string name) => name.Replace(" ", "").ToLowerInvariant();
    }
}