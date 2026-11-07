using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Crescent.APIs.NexusMods
{
    public static class NexusUtils
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        #region Version & Requests

        public static HttpRequestMessage CreateApiRequest(HttpMethod method, string requestUri)
        {
            HttpRequestMessage requestMessage = new(method, requestUri);
            requestMessage.Headers.Add("apikey", NexusAuthenticator.ApiKey?.key);
            requestMessage.Headers.Add("User-Agent", "CrescentModManager/1.0");
            return requestMessage;
        }

        #endregion

        #region API Calls

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
            List<string> requirementIds = new();
            if (string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(gameDomain)) return requirementIds;

            JObject? modDetails = await GetModDetailsJsonAsync(gameDomain, nexusId);
            if (modDetails == null) return requirementIds;

            string descriptionHtml = modDetails["description"]?.ToString() ?? string.Empty;
            MatchCollection linkMatches = Regex.Matches(descriptionHtml, @"nexusmods\.com/[^/]+/mods/(\d+)", RegexOptions.IgnoreCase);

            for (int i = 0; i < linkMatches.Count; i++)
            {
                Match linkMatch = linkMatches[i];
                if (linkMatch.Groups.Count > 1)
                {
                    string reqId = linkMatch.Groups[1].Value;
                    if (!string.Equals(reqId, nexusId, StringComparison.OrdinalIgnoreCase) && !requirementIds.Contains(reqId)) requirementIds.Add(reqId);
                }
            }

            return requirementIds;
        }

        #endregion

        #region File Management

        public static async Task<string?> DownloadFileAsync(string downloadUrl, string destinationDirectory, string fallbackModName)
        {
            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(destinationDirectory)) return null;
            try
            {
                HttpClient downloadClient = new();
                HttpResponseMessage response = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

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

                    if (!string.IsNullOrEmpty(extension))
                        fileName = Path.ChangeExtension(fileName, extension);
                }

                if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);
                string destinationPath = Path.Combine(destinationDirectory, fileName);

                Stream contentStream = await response.Content.ReadAsStreamAsync();
                FileStream fileStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                await contentStream.CopyToAsync(fileStream);

                fileStream.Close();
                contentStream.Close();
                downloadClient.Dispose();
                return destinationPath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                return null;
            }
        }

        #endregion

        #region Formatting

        public static string TryCleanVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return string.Empty;
            List<string> cleanParts = new();

            foreach (string part in version.Split('.'))
            {
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

        #endregion
    }
}