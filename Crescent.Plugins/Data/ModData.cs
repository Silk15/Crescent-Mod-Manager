using System.Text.RegularExpressions;
using Crescent.APIs.NexusMods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Crescent.Plugins
{
    [Serializable]
    public class ModData
    {
        public static readonly Regex ModIdRegex = new(@"\b\d{3,6}\b");
        public static readonly Regex VersionRegex = new(@"\b\d{3,6}[-_]v?(\d+(?:[.-]\d+)*[a-zA-Z]*)");
        public static readonly Regex ModNameRegex = new(@"[-_]\d{4,}(-\d+)*");
        public static readonly Regex ModCleanRegex = new(@"\d{4}-\d{2}-\d{2}T.*$");

        public static readonly string[] ArchiveExtensions = new[]
        {
            ".zip",
            ".rar",
            ".7z"
        };

        public string filePath;
        public string modName;
        public string displayName;
        public string version;

        public bool isDeployed;
        public bool isManual;
        public int loadOrder = -1;
        public List<string> ownedFiles = new();
        public List<string> requirementIds = new();

        public Update update = new();
        public Url url;

        [JsonIgnore] public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayName)) return displayName;
                string clean = ModNameRegex.Replace(modName, string.Empty);
                clean = ModCleanRegex.Replace(clean, string.Empty);

                clean = clean.Replace('_', ' ').Replace('-', ' ');
                clean = clean.Replace(".7z", string.Empty, StringComparison.OrdinalIgnoreCase);
                clean = clean.Replace(".zip", string.Empty, StringComparison.OrdinalIgnoreCase);
                clean = clean.Replace(".rar", string.Empty, StringComparison.OrdinalIgnoreCase);
                clean = clean.Trim();
                if (string.IsNullOrWhiteSpace(clean)) displayName = modName;

                return displayName = clean;
            }
        }
        [JsonIgnore] public string LatestVersion
        {
            get => update.latestVersion;
            set => update.latestVersion = value;
        }
        [JsonIgnore] public string LatestFileId
        {
            get => update.latestFileId;
            set => update.latestFileId = value;
        }
        [JsonIgnore] public string LatestFileDescription
        {
            get => update.latestFileDescription;
            set => update.latestFileDescription = value;
        }
        [JsonIgnore] public string LatestChangelog
        {
            get => update.latestChangelog;
            set => update.latestChangelog = value;
        }
        [JsonIgnore] public int LatestDownloads
        {
            get => update.latestDownloads;
            set => update.latestDownloads = value;
        }
        [JsonIgnore] public string LatestPublishDate
        {
            get => update.latestPublishDate;
            set => update.latestPublishDate = value;
        }
        [JsonIgnore] public bool HasCheckedForUpdate
        {
            get => update.hasCheckedForUpdate;
            set => update.hasCheckedForUpdate = value;
        }
        [JsonIgnore] public bool UpdateAvailable
        {
            get => update.updateAvailable;
            set => update.updateAvailable = value;
        }
        [JsonIgnore] public bool HasValidNexusData
        {
            get => update.hasValidNexusData;
            set => update.hasValidNexusData = value;
        }

        public ModData() { }

        public ModData(PluginData pluginData, string modName, string filePath)
        {
            this.filePath = filePath;
            this.modName = modName;

            isManual = pluginData.IsCoreMod(this) ? true : filePath.Contains("Manual", StringComparison.OrdinalIgnoreCase);
            url = GetUrl(pluginData.API.BaseUrl);
        }

        public static string StripArchiveExtension(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            for (int index = 0; index < ArchiveExtensions.Length; index++)
            {
                string extension = ArchiveExtensions[index];
                if (value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return value.Substring(0, value.Length - extension.Length);
            }

            return value;
        }

        public Url GetUrl(string baseUrl)
        {
            Match idMatch = ModIdRegex.Match(this.modName);
            if (idMatch.Success)
            {
                string idValue = idMatch.Value;
                url = new(baseUrl, idValue);

                int idIndex = idMatch.Index;
                string afterId = this.modName.Substring(idIndex + idValue.Length);
                Match versionAfterIdMatch = Regex.Match(afterId, @"^[\s\-_]+v?(\d+(?:[.-]\d+)*[a-zA-Z]*)");
                if (versionAfterIdMatch.Success) version = NexusUtils.TryCleanVersion(versionAfterIdMatch.Groups[1].Value.Replace('-', '.'));
                else
                {
                    Match versionMatch = VersionRegex.Match(this.modName);
                    if (versionMatch.Success) version = NexusUtils.TryCleanVersion(versionMatch.Groups[1].Value.Replace('-', '.'));
                }
            }
            else url = new(baseUrl, string.Empty);
            return url;
        }

        public async Task LoadRequirementsAsync(string gameDomain)
        {
            if (string.IsNullOrEmpty(url.nexusId)) return;
            requirementIds = await NexusUtils.GetRequirementIdsAsync(gameDomain, url.nexusId);
        }

        #region File Management

        public void UpdateFiles(JArray filesArray)
        {
            IEnumerable<JToken> mainFiles = filesArray.Where(fileToken => (fileToken["category_id"]?.Value<int>() ?? 0) == 1);
            JToken? targetFile = mainFiles.OrderByDescending(GetFileSortValue).FirstOrDefault();

            if (targetFile == null) targetFile = filesArray.OrderByDescending(GetFileSortValue).FirstOrDefault() ?? filesArray[0];

            update.latestVersion = NexusUtils.TryCleanVersion(targetFile["version"]?.ToString() ?? string.Empty);
            if (string.IsNullOrEmpty(update.latestVersion)) update.latestVersion = targetFile["version"]?.ToString() ?? string.Empty;

            update.latestFileId = targetFile["file_id"]?.ToString() ?? string.Empty;
            update.latestFileDescription = targetFile["description"]?.ToString() ?? string.Empty;
            update.latestChangelog = targetFile["changelog_html"]?.ToString() ?? targetFile["changelog"]?.ToString() ?? string.Empty;
            update.latestDownloads = targetFile["downloads"]?.Value<int>() ?? targetFile["download_count"]?.Value<int>() ?? targetFile["unique_downloads"]?.Value<int>() ?? 0;
            update.latestPublishDate = targetFile["uploaded_time"]?.ToString() ?? string.Empty;
            update.hasValidNexusData = true;

            string cleanLocalVersion = (version ?? string.Empty).Trim().ToLower().TrimStart('v');
            string cleanLatestVersion = update.latestVersion.Trim().ToLower().TrimStart('v');
            update.updateAvailable = !string.IsNullOrEmpty(cleanLocalVersion) && !string.IsNullOrEmpty(cleanLatestVersion) && cleanLocalVersion != cleanLatestVersion;
        }

        private static long GetFileSortValue(JToken fileToken)
        {
            JToken? timeToken = fileToken["uploaded_timestamp"] ?? fileToken["uploaded_time"];
            if (timeToken != null && timeToken.Type != JTokenType.Null)
            {
                if (timeToken.Type == JTokenType.Date) return timeToken.Value<DateTime>().Ticks;
                if (long.TryParse(timeToken.ToString(), out long timestampValue)) return timestampValue;
                if (DateTime.TryParse(timeToken.ToString(), out DateTime parsedDate)) return parsedDate.Ticks;
            }

            JToken? idToken = fileToken["file_id"];
            if (idToken != null && idToken.Type != JTokenType.Null && long.TryParse(idToken.ToString(), out long idValue))
                return idValue;

            return 0;
        }

        public void DeleteFiles(string stagingPath)
        {
            if (string.IsNullOrEmpty(stagingPath)) return;

            HashSet<string> candidateNames = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(modName))
            {
                candidateNames.Add(modName);
                candidateNames.Add(StripArchiveExtension(modName));
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                string fileNameFromPath = Path.GetFileName(filePath);
                if (!string.IsNullOrEmpty(fileNameFromPath))
                {
                    candidateNames.Add(fileNameFromPath);
                    candidateNames.Add(StripArchiveExtension(fileNameFromPath));
                }
            }

            string manualDirectory = Path.Combine(stagingPath, PluginData.Manual);
            string archiveDirectory = Path.Combine(stagingPath, PluginData.Archive);
            string[] searchDirectories = new[]
            {
                stagingPath,
                manualDirectory,
                archiveDirectory
            };

            Logger.Log($"Deleting {modName}...");

            if (!string.IsNullOrEmpty(filePath))
            {
                TryDeleteFile(filePath);
                TryDeleteDirectory(filePath);
            }

            foreach (string baseDirectory in searchDirectories)
            {
                if (!Directory.Exists(baseDirectory)) continue;

                foreach (string candidate in candidateNames)
                {
                    if (string.IsNullOrEmpty(candidate)) continue;

                    string targetPath = Path.Combine(baseDirectory, candidate);
                    TryDeleteDirectory(targetPath);
                    TryDeleteFile(targetPath);

                    string strippedCandidate = StripArchiveExtension(candidate);
                    for (int index = 0; index < ArchiveExtensions.Length; index++)
                    {
                        string archiveCandidatePath = Path.Combine(baseDirectory, strippedCandidate + ArchiveExtensions[index]);
                        TryDeleteFile(archiveCandidatePath);
                    }
                }
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                Logger.Log($"  Deleted file: {Path.GetFileName(path)}");
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                string[] allFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                for (int index = 0; index < allFiles.Length; index++)
                    File.SetAttributes(allFiles[index], FileAttributes.Normal);

                Directory.Delete(path, true);
                Logger.Log($"  Deleted directory: {Path.GetFileName(path)}");
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        public async Task CheckForUpdateAsync(string gameDomain)
        {
            update.hasCheckedForUpdate = true;
            if (string.IsNullOrEmpty(url.nexusId) || string.IsNullOrEmpty(NexusAuthenticator.ApiKey?.key))
            {
                update.hasValidNexusData = false;
                return;
            }

            try
            {
                JObject? jsonObject = await NexusUtils.GetModFilesJsonAsync(gameDomain, url.nexusId);
                if (jsonObject == null)
                {
                    update.hasValidNexusData = false;
                    return;
                }

                JArray? filesArray = jsonObject["files"] as JArray;
                if (filesArray == null || filesArray.Count == 0)
                {
                    update.hasValidNexusData = false;
                    return;
                }

                UpdateFiles(filesArray);
                await LoadRequirementsAsync(gameDomain);
            }
            catch (Exception exception)
            {
                update.hasValidNexusData = false;
                Logger.Error(exception.ToString());
            }
        }

        #endregion

        [Serializable] public class Url
        {
            public string nexusUrl;
            public string nexusId;

            public Url(string baseUrl, string id)
            {
                nexusUrl = baseUrl + id;
                nexusId = id;
            }
        }

        [Serializable] public class Update
        {
            public string latestVersion = string.Empty;
            public string latestFileId = string.Empty;
            public string latestFileDescription = string.Empty;
            public string latestChangelog = string.Empty;
            public int latestDownloads;
            public string latestPublishDate = string.Empty;
            public bool updateAvailable;
            public bool hasCheckedForUpdate;
            public bool hasValidNexusData;
        }
    }
}