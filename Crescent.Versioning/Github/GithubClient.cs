using Newtonsoft.Json.Linq;

namespace Crescent.Versioning
{
    public class GithubClient
    {
        public const string Owner = "Silk15";
        public const string Repository = "Crescent-Mod-Manager";

        public static HttpClient GetHttpClient()
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Repository);
            client.Timeout = TimeSpan.FromMinutes(10);
            return client;
        }

        public static async Task<Version?> GetLatestReleaseAsync()
        {
            try
            {
                using HttpClient client = GetHttpClient();
                string endpoint = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";
                string json = await client.GetStringAsync(endpoint);
                JObject releaseObject = JObject.Parse(json);

                string? tagName = releaseObject["tag_name"]?.ToString();
                if (string.IsNullOrEmpty(tagName)) return null;

                string releaseTitle = releaseObject["name"]?.ToString() ?? $"Crescent {tagName}";
                string releaseNotes = releaseObject["body"]?.ToString() ?? "Bug fixes and performance improvements";

                JArray? assets = releaseObject["assets"] as JArray;
                if (assets == null || assets.Count == 0) return null;

                JToken? matchedAsset = assets.FirstOrDefault(asset => asset["name"]?.ToString()?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true) ?? assets[0];

                string assetName = matchedAsset["name"]?.ToString() ?? "Crescent.zip";
                string downloadUrl = matchedAsset["browser_download_url"]?.ToString() ?? string.Empty;
                long sizeBytes = matchedAsset["size"]?.ToObject<long>() ?? 0;

                if (string.IsNullOrEmpty(downloadUrl)) return null;
                return new()
                {
                    name = releaseTitle,
                    version = tagName,
                    assetName = assetName,
                    downloadUrl = downloadUrl,
                    releaseNotes = releaseNotes,
                    sizeBytes = sizeBytes
                };
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return null;
            }
        }

        public static async Task<bool> DownloadFileAsync(string downloadUrl, string destinationFilePath, Action<long, long>? onProgress = null)
        {
            try
            {
                string? parentDirectory = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrEmpty(parentDirectory) && !Directory.Exists(parentDirectory))
                    Directory.CreateDirectory(parentDirectory);

                using HttpClient client = GetHttpClient();
                using HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                long downloadedBytes = 0;
                byte[] buffer = new byte[81920];

                await using Stream contentStream = await response.Content.ReadAsStreamAsync();
                await using FileStream fileStream = new(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                int bytesRead;
                DateTime lastReportTime = DateTime.MinValue;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (onProgress != null && (DateTime.UtcNow - lastReportTime).TotalMilliseconds >= 80)
                    {
                        lastReportTime = DateTime.UtcNow;
                        onProgress.Invoke(downloadedBytes, totalBytes);
                    }
                }

                onProgress?.Invoke(downloadedBytes, totalBytes);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return false;
            }
        }
    }
}