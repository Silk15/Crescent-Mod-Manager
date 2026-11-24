using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Silk.Utilities;

namespace Crescent.APIs.NexusMods
{
    public static class NexusUtils
    {
        private const string GraphQlUrl = "https://api.nexusmods.com/v2/graphql";

        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly SemaphoreSlim ApiSemaphore = new(3, 3);
        private static readonly SemaphoreSlim ImageSemaphore = new(4, 4);
        private static readonly ConcurrentDictionary<string, JObject> ModDetailsCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Task<JObject?>> InFlightDetailsRequests = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Task<string?>> InFlightThumbnailRequests = new(StringComparer.OrdinalIgnoreCase);
        public static readonly ConcurrentDictionary<string, string> GameStagingPaths = new(StringComparer.OrdinalIgnoreCase);

        public static readonly List<string> GenerativeAiTags = new()
        {
            "AI-Generated Content",
            "AI Assisted",
            "AI Media"
        };

        private static DateTimeOffset rateLimitResetTime = DateTimeOffset.MinValue;
        private static int hourlyRemaining = int.MaxValue;
        private static int dailyRemaining = int.MaxValue;

        public static bool IsRateLimited => DateTimeOffset.UtcNow < rateLimitResetTime || (hourlyRemaining <= 0 && dailyRemaining <= 0);
        public static bool IsPremiumUser => NexusAuthenticator.UserProfile?["is_premium"]?.ToObject<bool>() ?? false;

        public static HttpRequestMessage CreateApiRequest(HttpMethod method, string requestUri)
        {
            HttpRequestMessage requestMessage = new(method, requestUri);
            if (!string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) requestMessage.Headers.Add("apikey", NexusAuthenticator.ApiKey.key);
            requestMessage.Headers.Add("User-Agent", "CrescentModManager/1.4.0");
            requestMessage.Headers.Add("Accept", "application/json");
            return requestMessage;
        }

        private static async Task<HttpResponseMessage?> SendApiRequestAsync(HttpMethod method, string requestUri, HttpContent? content = null)
        {
            if (IsRateLimited) return null;
            await ApiSemaphore.WaitAsync();
            try
            {
                if (IsRateLimited) return null;
                using HttpRequestMessage requestMessage = CreateApiRequest(method, requestUri);
                if (content != null) requestMessage.Content = content;

                HttpResponseMessage responseMessage = await HttpClient.SendAsync(requestMessage);
                UpdateRateLimits(responseMessage);
                return responseMessage;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                return null;
            }
            finally
            {
                ApiSemaphore.Release();
            }
        }

        public static async Task<JObject?> QueryGraphQlAsync(string query, object variables)
        {
            string payload = JsonConvert.SerializeObject(new { query, variables });
            using StringContent content = new(payload, Encoding.UTF8, "application/json");
            using HttpResponseMessage? response = await SendApiRequestAsync(HttpMethod.Post, GraphQlUrl, content);
            if (response == null || !response.IsSuccessStatusCode) return null;

            JObject parsed = JObject.Parse(await response.Content.ReadAsStringAsync());
            return parsed["data"] is JObject ? parsed : null;
        }

        private static void UpdateRateLimits(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("X-RL-Hourly-Remaining", out IEnumerable<string>? hourlyValues) && int.TryParse(hourlyValues.FirstOrDefault(), out int parsedHourly))
                hourlyRemaining = parsedHourly;

            if (response.Headers.TryGetValues("X-RL-Daily-Remaining", out IEnumerable<string>? dailyValues) && int.TryParse(dailyValues.FirstOrDefault(), out int parsedDaily))
                dailyRemaining = parsedDaily;

            if (response.StatusCode == HttpStatusCode.TooManyRequests || (hourlyRemaining <= 0 && dailyRemaining <= 0))
            {
                DateTimeOffset resetTime = DateTimeOffset.UtcNow.AddMinutes(5);
                if (response.Headers.TryGetValues("X-RL-Hourly-Reset", out IEnumerable<string>? resetValues) && DateTimeOffset.TryParse(resetValues.FirstOrDefault(), out DateTimeOffset parsedReset))
                    resetTime = parsedReset;

                rateLimitResetTime = resetTime;
            }
        }

        private static string? GetCacheDirectory(string gameDomain)
        {
            if (!GameStagingPaths.TryGetValue(gameDomain, out string? stagingPath) || string.IsNullOrEmpty(stagingPath) || !Directory.Exists(stagingPath))
                return null;

            string directory = Path.Combine(stagingPath, ".cache");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            return directory;
        }

        private static void CacheModDetails(string gameDomain, string nexusId, JObject details)
        {
            if (string.IsNullOrEmpty(gameDomain) || string.IsNullOrEmpty(nexusId)) return;
            ModDetailsCache[$"{gameDomain}:{nexusId}"] = details;

            string? cacheDir = GetCacheDirectory(gameDomain);
            if (string.IsNullOrEmpty(cacheDir)) return;

            try
            {
                string cachePath = Path.Combine(cacheDir, $"{nexusId}.json");
                File.WriteAllText(cachePath, details.ToString());
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        public static bool TryParseNexusUrl(string input, string fallbackDomain, out string gameDomain, out string nexusId)
        {
            gameDomain = fallbackDomain;
            nexusId = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string trimmed = input.Trim();
            if (int.TryParse(trimmed, out _))
            {
                nexusId = trimmed;
                return true;
            }

            Match nxmMatch = Regex.Match(trimmed, @"^nxm://([^/]+)/mods/(\d+)", RegexOptions.IgnoreCase);
            if (nxmMatch.Success)
            {
                gameDomain = nxmMatch.Groups[1].Value;
                nexusId = nxmMatch.Groups[2].Value;
                return true;
            }

            Match webMatch = Regex.Match(trimmed, @"nexusmods\.com/([^/]+)/mods/(\d+)", RegexOptions.IgnoreCase);
            if (webMatch.Success)
            {
                gameDomain = webMatch.Groups[1].Value;
                nexusId = webMatch.Groups[2].Value;
                return true;
            }

            return false;
        }

        public static async Task<JObject?> GetModFilesJsonAsync(string gameDomain, string nexusId)
        {
            if (string.IsNullOrEmpty(gameDomain) || string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return null;

            string requestUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{nexusId}/files.json";
            using HttpResponseMessage? responseMessage = await SendApiRequestAsync(HttpMethod.Get, requestUrl);

            if (responseMessage == null || !responseMessage.IsSuccessStatusCode) return null;
            return JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        }

        public static async Task<string?> GetModNameAsync(string gameDomain, string modId)
        {
            JObject? details = await GetModDetailsJsonAsync(gameDomain, modId);
            return details?["name"]?.ToString();
        }

        public static async Task<string> GetDownloadUrlAsync(string gameDomain, string nexusId, string fileId, string? nxmKey = null, string? nxmExpires = null)
        {
            if (string.IsNullOrEmpty(gameDomain) || string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return string.Empty;

            string querySuffix = !string.IsNullOrEmpty(nxmKey) && !string.IsNullOrEmpty(nxmExpires) ? $"?key={Uri.EscapeDataString(nxmKey)}&expires={Uri.EscapeDataString(nxmExpires)}" : string.Empty;
            string downloadLinkUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{nexusId}/files/{fileId}/download_link.json{querySuffix}";
            using HttpResponseMessage? responseMessage = await SendApiRequestAsync(HttpMethod.Get, downloadLinkUrl);

            if (responseMessage == null || !responseMessage.IsSuccessStatusCode) return string.Empty;
            JArray jsonArray = JArray.Parse(await responseMessage.Content.ReadAsStringAsync());
            return (jsonArray.FirstOrDefault() as JObject)?["URI"]?.ToString() ?? string.Empty;
        }

        public static async Task<JObject?> GetModDetailsJsonAsync(string gameDomain, string nexusId)
        {
            if (string.IsNullOrEmpty(gameDomain) || string.IsNullOrEmpty(nexusId)) return null;

            string cacheKey = $"{gameDomain}:{nexusId}";
            if (ModDetailsCache.TryGetValue(cacheKey, out JObject? cachedDetails))
                return cachedDetails;

            string? cacheDir = GetCacheDirectory(gameDomain);
            string? cachePath = !string.IsNullOrEmpty(cacheDir) ? Path.Combine(cacheDir, $"{nexusId}.json") : null;

            if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                try
                {
                    DateTime lastWrite = File.GetLastWriteTimeUtc(cachePath);
                    if (DateTime.UtcNow - lastWrite < TimeSpan.FromHours(12) || IsRateLimited || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key))
                    {
                        JObject parsedCache = JObject.Parse(await File.ReadAllTextAsync(cachePath));
                        ModDetailsCache[cacheKey] = parsedCache;
                        return parsedCache;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }

            if (string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key)) return null;
            Task<JObject?> requestTask = InFlightDetailsRequests.GetOrAdd(cacheKey, _ => FetchModDetailsInternalAsync(gameDomain, nexusId, cacheKey, cachePath));
            try
            {
                return await requestTask;
            }
            finally
            {
                InFlightDetailsRequests.TryRemove(cacheKey, out _);
            }
        }

        private static async Task<JObject?> FetchModDetailsInternalAsync(string gameDomain, string nexusId, string cacheKey, string? cachePath)
        {
            string requestUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{nexusId}.json";
            using HttpResponseMessage? responseMessage = await SendApiRequestAsync(HttpMethod.Get, requestUrl);
            if (responseMessage != null && responseMessage.IsSuccessStatusCode)
            {
                JObject details = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
                CacheModDetails(gameDomain, nexusId, details);
                return details;
            }

            if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                try
                {
                    JObject fallbackDetails = JObject.Parse(await File.ReadAllTextAsync(cachePath));
                    ModDetailsCache[cacheKey] = fallbackDetails;
                    return fallbackDetails;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }

            return null;
        }

        public static async Task<string?> GetCachedThumbnailPathAsync(string gameDomain, string nexusId, string? pictureUrl)
        {
            if (string.IsNullOrEmpty(gameDomain) || string.IsNullOrEmpty(nexusId) || string.IsNullOrEmpty(pictureUrl)) return null;

            string? cacheDir = GetCacheDirectory(gameDomain);
            if (string.IsNullOrEmpty(cacheDir)) return null;

            string cachePath = Path.Combine(cacheDir, $"{nexusId}.img");
            if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
                return cachePath;

            string cacheKey = $"{gameDomain}:{nexusId}";
            Task<string?> downloadTask = InFlightThumbnailRequests.GetOrAdd(cacheKey, _ => DownloadThumbnailInternalAsync(pictureUrl, cachePath));
            try
            {
                return await downloadTask;
            }
            finally
            {
                InFlightThumbnailRequests.TryRemove(cacheKey, out _);
            }
        }

        private static async Task<string?> DownloadThumbnailInternalAsync(string pictureUrl, string cachePath)
        {
            await ImageSemaphore.WaitAsync();
            try
            {
                if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
                    return cachePath;

                using HttpResponseMessage response = await HttpClient.GetAsync(pictureUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return null;

                byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                if (imageBytes.Length == 0) return null;

                string tempPath = $"{cachePath}.tmp";
                await File.WriteAllBytesAsync(tempPath, imageBytes);
                File.Move(tempPath, cachePath, true);
                return cachePath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                return null;
            }
            finally
            {
                ImageSemaphore.Release();
            }
        }

        public static string FormatNexusDate(JToken? timestampToken, JToken? timeStringToken)
        {
            if (long.TryParse(timestampToken?.ToString(), out long unixSeconds) && unixSeconds > 0)
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString("dd MMM yyyy");

            if (DateTimeOffset.TryParse(timeStringToken?.ToString(), out DateTimeOffset parsedDate))
                return parsedDate.LocalDateTime.ToString("dd MMM yyyy");

            return "-";
        }

        public static string FormatRelativeTime(JToken? timestampToken, JToken? timeStringToken)
        {
            DateTimeOffset targetTime = DateTimeOffset.MinValue;
            if (long.TryParse(timestampToken?.ToString(), out long unixSeconds) && unixSeconds > 0)
                targetTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

            else if (DateTimeOffset.TryParse(timeStringToken?.ToString(), out DateTimeOffset parsedDate))
                targetTime = parsedDate;

            if (targetTime == DateTimeOffset.MinValue) return "-";

            TimeSpan diff = DateTimeOffset.UtcNow - targetTime;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d ago";
            if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)}mo ago";
            return $"{(int)(diff.TotalDays / 365)}y ago";
        }

        public static string FormatCompactNumber(long number)
        {
            if (number >= 1_000_000) return $"{number / 1_000_000D:0.#}M";
            if (number >= 1_000) return $"{number / 1_000D:0.#}k";
            return number.ToString();
        }

        public static string FormatFileSize(long sizeInKbOrBytes)
        {
            if (sizeInKbOrBytes <= 0) return "-";
            double kilobytes = sizeInKbOrBytes > 10_000_000 ? sizeInKbOrBytes / 1024D : sizeInKbOrBytes;
            if (kilobytes >= 1024 * 1024) return $"{kilobytes / (1024D * 1024D):0.0}GB";
            if (kilobytes >= 1024) return $"{kilobytes / 1024D:0.0}MB";
            return $"{kilobytes:0.0}KB";
        }

        public static async Task<List<string>> GetRequirementIdsAsync(string gameDomain, string nexusId)
        {
            List<RequirementNode> nodes = await GetModRequirementsAsync(gameDomain, nexusId);
            return nodes.Where(node => !node.IsOptional && !node.IsExternal && !string.IsNullOrEmpty(node.NexusId)).Select(node => node.NexusId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static async Task<List<RequirementNode>> GetModRequirementsAsync(string gameDomain, string nexusId)
        {
            List<RequirementNode> requirements = new();
            if (string.IsNullOrEmpty(gameDomain) || !int.TryParse(nexusId, out int parsedModId)) return requirements;

            const string query = @"
                query ModRequirementsQuery($ids: [CompositeDomainWithIdInput!]!) {
                    legacyModsByDomain(ids: $ids) {
                        nodes {
                            modId
                            name
                            modRequirements {
                                nexusRequirements {
                                    nodes {
                                        id
                                        modId
                                        modName
                                        notes
                                        url
                                        externalRequirement
                                    }
                                }
                            }
                        }
                    }
                }";

            object variables = new
            {
                ids = new[]
                {
                    new
                    {
                        gameDomain,
                        modId = parsedModId
                    }
                }
            };

            JObject? response = await QueryGraphQlAsync(query, variables);
            JArray? reqNodes = response?.SelectToken("data.legacyModsByDomain.nodes[0].modRequirements.nexusRequirements.nodes") as JArray;
            if (reqNodes == null) return requirements;

            HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reqNodes.Count; i++)
            {
                if (reqNodes[i] is not JObject reqObj) continue;

                bool isExternal = reqObj["externalRequirement"]?.ToObject<bool>() ?? false;
                string reqModId = reqObj["modId"]?.ToString() ?? string.Empty;
                string reqUrl = reqObj["url"]?.ToString() ?? string.Empty;

                if (isExternal && TryParseNexusUrl(reqUrl, gameDomain, out _, out string parsedId))
                {
                    reqModId = parsedId;
                    isExternal = false;
                }

                if (!isExternal && (string.IsNullOrEmpty(reqModId) || string.Equals(reqModId, nexusId, StringComparison.OrdinalIgnoreCase))) continue;
                string uniqueKey = !string.IsNullOrEmpty(reqModId) ? reqModId : reqUrl;
                if (string.IsNullOrEmpty(uniqueKey) || !seenIds.Add(uniqueKey)) continue;

                string notes = TrimNexusFormatting(reqObj["notes"]?.ToString() ?? string.Empty);
                bool isOptional = isExternal || IsNoteOptional(notes);

                requirements.Add(new RequirementNode
                {
                    NexusId = reqModId,
                    ModName = reqObj["modName"]?.ToString() ?? $"Mod #{reqModId}",
                    Notes = notes,
                    Url = reqUrl,
                    IsExternal = isExternal,
                    IsOptional = isOptional,
                    IsSelected = !isOptional
                });
            }

            return requirements;
        }

        private static bool IsNoteOptional(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return false;
            return notes.Contains("optional", StringComparison.OrdinalIgnoreCase) || notes.Contains("not required", StringComparison.OrdinalIgnoreCase) || notes.Contains("recommended", StringComparison.OrdinalIgnoreCase) || notes.Contains("only needed if", StringComparison.OrdinalIgnoreCase) || notes.Contains("only if", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<List<JObject>> GetTrendingAsync(string gameDomain, IEnumerable<string>? excludedTags = null) => await GetBrowseModsAsync(gameDomain, "Trending", "7 Days", string.Empty, excludedTags, null, 6);

        public static async Task<List<JObject>> GetBrowseModsAsync(string gameDomain, string category, string period, string searchQuery, IEnumerable<string>? excludedTags = null, IEnumerable<string>? includedTags = null, int count = 24, int offset = 0)
        {
            const string query = @"
                query ModsListing($count: Int, $offset: Int, $filter: ModsFilter, $postFilter: ModsFilter, $sort: [ModsSort!]) {
                    mods(count: $count, offset: $offset, filter: $filter, postFilter: $postFilter, sort: $sort, viewUserBlockedContent: false) {
                        nodesCount
                        nodes {
                            modId
                            name
                            summary
                            thumbnailUrl
                            pictureUrl
                            version
                            downloads
                            endorsements
                            fileSize
                            createdAt
                            updatedAt
                            game {
                                domainName
                                name
                            }
                            modCategory {
                                categoryId
                                name
                            }
                            uploader {
                                name
                                avatar
                            }
                        }
                    }
                }";

            Dictionary<string, object> filterObj = new()
            {
                ["gameDomainName"] = new[]
                {
                    new
                    {
                        op = "EQUALS",
                        value = gameDomain
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(searchQuery))
                filterObj["name"] = new[]
                {
                    new
                    {
                        op = "WILDCARD",
                        value = searchQuery.Trim()
                    }
                };

            if (category == "Updated")
                filterObj["hasUpdated"] = new[]
                {
                    new
                    {
                        op = "EQUALS",
                        value = true
                    }
                };

            string effectivePeriod = category == "Trending" && period == "All Time" ? "7 Days" : period;
            long? minCreatedTimestamp = GetPeriodTimestamp(effectivePeriod);
            if (minCreatedTimestamp.HasValue && category is "Popular" or "Trending" or "New")
            {
                filterObj["createdAt"] = new[]
                {
                    new
                    {
                        op = "GTE",
                        value = minCreatedTimestamp.Value.ToString()
                    }
                };
            }

            List<object> tagFilters = new();
            List<string>? includeList = includedTags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (includeList != null)
                for (int i = 0; i < includeList.Count; i++)
                    tagFilters.Add(new
                    {
                        op = "EQUALS",
                        value = includeList[i]
                    });

            List<string>? excludeList = excludedTags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            List<object> excludeTagFilters = new();
            if (excludeList != null)
                for (int i = 0; i < excludeList.Count; i++)
                {
                    object rule = new
                    {
                        op = "NOT_EQUALS",
                        value = excludeList[i]
                    };
                    tagFilters.Add(rule);
                    excludeTagFilters.Add(rule);
                }

            if (tagFilters.Count > 0)
                filterObj["tag"] = tagFilters.ToArray();

            Dictionary<string, object> postFilterObj = new();
            if (excludeTagFilters.Count > 0)
                postFilterObj["tag"] = excludeTagFilters.ToArray();

            object variables = new
            {
                count,
                offset = category == "Surprise" ? Random.Shared.Next(0, 150) : offset,
                filter = filterObj,
                postFilter = postFilterObj,
                sort = GetSortForCategory(category)
            };

            JObject? response = await QueryGraphQlAsync(query, variables);
            JArray? nodes = response?.SelectToken("data.mods.nodes") as JArray;
            if (nodes == null) return new List<JObject>();

            List<JObject> mods = new(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is JObject node)
                    mods.Add(GetModJObject(node, gameDomain));
            }

            return category == "Surprise" ? mods.OrderBy(_ => Random.Shared.Next()).ToList() : mods;
        }

        private static object[] GetSortForCategory(string category)
        {
            return category switch
            {
                "Updated" => new object[]
                {
                    new
                    {
                        updatedAt = new
                        {
                            direction = "DESC"
                        }
                    }
                },
                "Trending" => new object[]
                {
                    new
                    {
                        endorsements = new
                        {
                            direction = "DESC"
                        }
                    }
                },
                "Popular" => new object[]
                {
                    new
                    {
                        downloads = new
                        {
                            direction = "DESC"
                        }
                    }
                },
                _ => new object[]
                {
                    new
                    {
                        createdAt = new
                        {
                            direction = "DESC"
                        }
                    }
                }
            };
        }

        private static long? GetPeriodTimestamp(string period)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return period switch
            {
                "24 Hours" => now.AddDays(-1).ToUnixTimeSeconds(),
                "7 Days" => now.AddDays(-7).ToUnixTimeSeconds(),
                "14 Days" => now.AddDays(-14).ToUnixTimeSeconds(),
                "30 Days" => now.AddDays(-30).ToUnixTimeSeconds(),
                "1 Year" => now.AddYears(-1).ToUnixTimeSeconds(),
                _ => null
            };
        }

        private static JObject GetModJObject(JObject node, string gameDomain)
        {
            JObject? gameObj = node["game"] as JObject;
            JObject? uploaderObj = node["uploader"] as JObject;
            JObject? categoryObj = node["modCategory"] as JObject;

            return new JObject
            {
                ["mod_id"] = node["modId"]?.ToString() ?? string.Empty,
                ["domain_name"] = gameObj?["domainName"]?.ToString() ?? gameDomain,
                ["game_name"] = gameObj?["name"]?.ToString() ?? string.Empty,
                ["name"] = node["name"]?.ToString() ?? string.Empty,
                ["summary"] = node["summary"]?.ToString() ?? string.Empty,
                ["picture_url"] = node["pictureUrl"]?.ToString() ?? node["thumbnailUrl"]?.ToString() ?? string.Empty,
                ["version"] = node["version"]?.ToString() ?? string.Empty,
                ["author"] = uploaderObj?["name"]?.ToString() ?? "Unknown",
                ["author_avatar"] = uploaderObj?["avatar"]?.ToString() ?? string.Empty,
                ["category_name"] = categoryObj?["name"]?.ToString() ?? "Mod",
                ["endorsement_count"] = node["endorsements"]?.ToObject<long>() ?? 0,
                ["mod_downloads"] = node["downloads"]?.ToObject<long>() ?? 0,
                ["file_size"] = node["fileSize"]?.ToObject<long>() ?? 0,
                ["created_time"] = node["createdAt"]?.ToString() ?? string.Empty,
                ["updated_time"] = node["updatedAt"]?.ToString() ?? string.Empty
            };
        }

        public static async Task<string?> DownloadFileAsync(string downloadUrl, string destinationDirectory, string fallbackModName, IProgress<double>? progress = null)
        {
            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(destinationDirectory)) return null;
            try
            {
                using HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
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
                await using FileStream fileStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                long totalLength = response.Content.Headers.ContentLength ?? 0;
                int lastReportedPercent = -1;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    if (totalLength > 0 && progress != null)
                    {
                        int currentPercent = (int)((double)totalRead / totalLength * 100);
                        if (currentPercent > lastReportedPercent)
                        {
                            lastReportedPercent = currentPercent;
                            progress.Report(currentPercent);
                        }
                    }
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
            List<string> cleanParts = new();

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

        public static string TrimNexusFormatting(string input) => WebUtility.HtmlDecode(input).Trim();
    }
}