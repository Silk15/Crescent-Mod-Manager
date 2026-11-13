using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Crescent.APIs.NexusMods
{
    public static class NexusUtils
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static HttpRequestMessage CreateApiRequest(HttpMethod method, string requestUri)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(method, requestUri);
            requestMessage.Headers.Add("apikey", NexusAuthenticator.ApiKey?.key);
            requestMessage.Headers.Add("User-Agent", "CrescentModManager/1.0");
            return requestMessage;
        }

        public static async Task<JObject?> GetModFilesJsonAsync(string gameDomain, string nexusId)
        {
            if (string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return null;

            string requestUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{nexusId}/files.json";
            HttpRequestMessage requestMessage = CreateApiRequest(HttpMethod.Get, requestUrl);
            HttpResponseMessage responseMessage = await HttpClient.SendAsync(requestMessage);

            if (!responseMessage.IsSuccessStatusCode) return null;
            string jsonString = await responseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(jsonString);
        }

        public static async Task<string> GetDownloadUrlAsync(string gameDomain, string nexusId, string fileId)
        {
            if (string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return string.Empty;

            string downloadLinkUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{nexusId}/files/{fileId}/download_link.json";
            HttpRequestMessage requestMessage = CreateApiRequest(HttpMethod.Get, downloadLinkUrl);
            HttpResponseMessage responseMessage = await HttpClient.SendAsync(requestMessage);

            if (!responseMessage.IsSuccessStatusCode) return string.Empty;
            string jsonString = await responseMessage.Content.ReadAsStringAsync();
            JArray jsonArray = JArray.Parse(jsonString);
            return jsonArray[0]?["URI"]?.ToString() ?? string.Empty;
        }

        public static async Task<JObject?> GetModDetailsJsonAsync(string gameDomain, string nexusId)
        {
            if (string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return null;

            string requestUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{nexusId}.json";
            HttpRequestMessage requestMessage = CreateApiRequest(HttpMethod.Get, requestUrl);
            HttpResponseMessage responseMessage = await HttpClient.SendAsync(requestMessage);

            if (!responseMessage.IsSuccessStatusCode) return null;
            string jsonString = await responseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(jsonString);
        }

        public static async Task<List<string>> GetRequirementIdsAsync(string gameDomain, string nexusId)
        {
            List<string> requirementIds = new List<string>();
            if (string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(gameDomain)) return requirementIds;

            JObject? modDetails = await GetModDetailsJsonAsync(gameDomain, nexusId);
            if (modDetails == null) return requirementIds;

            JArray? requirementsArray = modDetails["requirements"] as JArray;
            if (requirementsArray != null)
            {
                for (int i = 0; i < requirementsArray.Count; i++)
                {
                    JToken? reqToken = requirementsArray[i];
                    string reqId = reqToken?["mod_id"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(reqId) && !requirementIds.Contains(reqId))
                    {
                        requirementIds.Add(reqId);
                    }
                }
            }

            return requirementIds;
        }

        public static async Task<List<JObject>> GetBrowseModsAsync(string gameDomain, string category, string period)
        {
            if (string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) throw new InvalidOperationException("A Nexus Mods API key is required to browse mods.");

            List<string> endpoints = category switch
            {
                "New" => new List<string> { $"games/{gameDomain}/mods/latest_added.json" },
                "Updated" => new List<string> { $"games/{gameDomain}/mods/updated.json?period={period}" },
                "Monthly Trending" => new List<string> { $"games/{gameDomain}/mods/updated.json?period=1m" },
                "Popular" => new List<string> { $"games/{gameDomain}/mods/updated.json?period={period}" },
                "Trending" => new List<string> { $"games/{gameDomain}/mods/trending.json" },
                _ => new List<string>
                {
                    $"games/{gameDomain}/mods/latest_added.json",
                    $"games/{gameDomain}/mods/latest_updated.json",
                    $"games/{gameDomain}/mods/trending.json"
                }
            };

            Dictionary<string, JObject> uniqueMods = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < endpoints.Count; i++)
            {
                string endpoint = endpoints[i];
                using HttpRequestMessage request = CreateApiRequest(HttpMethod.Get, $"https://api.nexusmods.com/v1/{endpoint}");
                using HttpResponseMessage response = await HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Nexus Mods returned {(int)response.StatusCode} ({response.ReasonPhrase}).");

                JToken responseData = JToken.Parse(await response.Content.ReadAsStringAsync());
                JArray items = responseData as JArray ?? (responseData as JObject)?["mods"] as JArray ?? new JArray();
                for (int j = 0; j < items.Count; j++)
                {
                    JObject mod = items[j] as JObject ?? new JObject();
                    string modId = mod["mod_id"]?.ToString() ?? mod["id"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(modId)) uniqueMods[modId] = mod;
                }
            }

            List<JObject> mods = new List<JObject>();
            foreach (KeyValuePair<string, JObject> entry in uniqueMods)
            {
                JObject mod = entry.Value;
                if (string.IsNullOrEmpty(mod["name"]?.ToString()) || string.IsNullOrEmpty(mod["picture_url"]?.ToString()))
                {
                    JObject? details = await GetModDetailsJsonAsync(gameDomain, entry.Key);
                    if (details != null)
                    {
                        details["mod_id"] = entry.Key;
                        details["domain_name"] = gameDomain;
                        if (mod["latest_file_update"] != null) details["latest_file_update"] = mod["latest_file_update"];
                        mod = details;
                    }
                }

                mod["mod_id"] ??= entry.Key;
                mod["domain_name"] ??= gameDomain;
                mods.Add(mod);
            }

            if (category == "Popular") mods = mods.OrderByDescending(mod => ReadLong(mod["mod_downloads"] ?? mod["total_downloads"])).ToList();
            else if (category == "Monthly Trending") mods = mods.OrderByDescending(mod => ReadLong(mod["endorsement_count"] ?? mod["endorsements"])).ToList();
            else if (category == "Updated") mods = mods.OrderByDescending(mod => ReadLong(mod["updated_timestamp"] ?? mod["latest_file_update"])).ToList();

            return mods;
        }

        private static long ReadLong(JToken? value)
        {
            if (long.TryParse(value?.ToString(), out long number)) return number;
            return DateTime.TryParse(value?.ToString(), out DateTime date) ? date.Ticks : 0;
        }

        public static async Task<string?> DownloadFileAsync(string downloadUrl, string destinationDirectory, string fallbackModName, IProgress<double>? progress = null)
        {
            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(destinationDirectory)) return null;
            try
            {
                using HttpClient downloadClient = new HttpClient();
                using HttpResponseMessage response = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode) return null;
                string fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? string.Empty;
                if (string.IsNullOrEmpty(fileName))
                {
                    string baseName = Path.GetFileNameWithoutExtension(fallbackModName);
                    if (string.IsNullOrEmpty(baseName)) baseName = "mod_download";
                    fileName = $"{baseName}.zip";
                }

                string mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!string.IsNullOrEmpty(mediaType))
                {
                    string extension = mediaType.ToLower() switch
                    {
                        "application/x-rar-compressed" or "application/vnd.rar" => ".rar",
                        "application/x-7z-compressed" => ".7z",
                        "application/zip" or "application/x-zip-compressed" => ".zip",
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(extension)) fileName = Path.ChangeExtension(fileName, extension);
                }

                if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);
                string destinationPath = Path.Combine(destinationDirectory, fileName);

                await using Stream contentStream = await response.Content.ReadAsStreamAsync();
                await using FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                long totalLength = response.Content.Headers.ContentLength ?? 0;
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    if (totalLength > 0) progress?.Report((double)totalRead / totalLength * 100);
                }
                progress?.Report(100);
                return destinationPath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                return null;
            }
        }

        public static string TryCleanVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return string.Empty;
            List<string> cleanParts = new List<string>();

            string[] parts = version.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length >= 5 && int.TryParse(part, out _)) break;
                cleanParts.Add(part);
                if (cleanParts.Count >= 4) break;
            }

            return string.Join(".", cleanParts);
        }

        public static string TrimNexusFormatting(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string cleanText = Regex.Replace(input, "<.*?>", " ");
            cleanText = cleanText.Replace("&quot;", "\"").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&#39;", "'");
            return cleanText.Trim();
        }
    }
}