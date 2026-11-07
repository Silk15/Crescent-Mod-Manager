using System.IO.Compression;
using System.Text.Json;
using Crescent.Json.Serialization;

namespace Crescent.Plugins
{
    public class GithubPluginInfo
    {
        public string? name;
        public byte[]? pluginBytes;
        public string? posterPath;
        public string? version;

        public static async Task<List<GithubPluginInfo>> GetAvailablePluginsAsync()
        {
            List<GithubPluginInfo> plugins = new();
            HttpClient client = new();

            client.DefaultRequestHeaders.Add("User-Agent", "Crescent");
            string json = await client.GetStringAsync("https://api.github.com/repos/Silk15/Crescent-Mod-Manager/contents?ref=plugins");
            using JsonDocument doc = JsonDocument.Parse(json);
            foreach (JsonElement element in doc.RootElement.EnumerateArray())
            {
                string? fileName = element.GetProperty("name").GetString();
                if (fileName != null && fileName.EndsWith(".plugin"))
                {
                    string? downloadUrl = element.GetProperty("download_url").GetString();
                    byte[] bytes = await client.GetByteArrayAsync(downloadUrl);

                    string version = "1.0.0";
                    string pluginName = fileName.Replace(".plugin", "");
                    string? posterSubPath = null;

                    using (MemoryStream stream = new(bytes))
                    {
                        using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
                        {
                            ZipArchiveEntry? manifestEntry = archive.Entries.FirstOrDefault(e => e.Name.Equals("plugin.json", StringComparison.OrdinalIgnoreCase));
                            if (manifestEntry != null)
                            {
                                string temporaryManifestPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
                                manifestEntry.ExtractToFile(temporaryManifestPath, true);
                                PluginManifest? manifest = await FileManager.ReadJson<PluginManifest>(temporaryManifestPath);
                                if (File.Exists(temporaryManifestPath)) File.Delete(temporaryManifestPath);

                                if (!string.IsNullOrEmpty(manifest.name)) pluginName = manifest.name;
                                if (!string.IsNullOrEmpty(manifest.version)) version = manifest.version;
                                posterSubPath = manifest.poster;
                            }
                        }
                    }

                    plugins.Add(new GithubPluginInfo
                    {
                        name = pluginName,
                        version = version,
                        posterPath = posterSubPath,
                        pluginBytes = bytes
                    });
                }
            }

            return plugins;
        }
    }
}