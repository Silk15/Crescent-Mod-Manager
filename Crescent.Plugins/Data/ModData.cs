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

        public string filePath;
        public string modName;
        public string displayName;
        public string version;
        
        public bool isDeployed;
        public bool isManual;
        public List<string> ownedFiles = new();

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
                clean = clean.Replace(".7z", string.Empty);
                clean = clean.Replace(".zip", string.Empty);
                clean = clean.Replace(".rar", string.Empty);
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

        public ModData(GameData gameData, string modName, string filePath)
        {
            this.filePath = filePath;
            this.modName = modName;
            
            isManual = gameData.IsCoreMod(this) ? true : filePath.Contains("Manual", StringComparison.OrdinalIgnoreCase);
            url = GetUrl(gameData.BaseUrl);
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

        public void OpenInExplorer()
        {
            
        }

        #region File Management
        
        public void UpdateFiles(JArray filesArray)
        {
            JToken targetFile = null;
            foreach (JToken fileToken in filesArray)
            {
                int categoryId = fileToken["category_id"]?.Value<int>() ?? 0;
                if (categoryId == 1)
                {
                    targetFile = fileToken;
                    break;
                }
            }

            if (targetFile == null) targetFile = filesArray[0];

            update.latestVersion = targetFile["version"]?.ToString() ?? string.Empty;
            update.latestFileId = targetFile["file_id"]?.ToString() ?? string.Empty;
            update.latestFileDescription = targetFile["description"]?.ToString() ?? string.Empty;
            update.latestChangelog = targetFile["changelog_html"]?.ToString() ?? targetFile["changelog"]?.ToString() ?? string.Empty;
            update.latestDownloads = targetFile["downloads"]?.Value<int>() ?? targetFile["download_count"]?.Value<int>() ?? targetFile["unique_downloads"]?.Value<int>() ?? 0;
            update.latestPublishDate = targetFile["uploaded_time"]?.ToString() ?? string.Empty;
            update.hasValidNexusData = true;

            string cleanLocalVersion = version.Trim().ToLower().TrimStart('v');
            string cleanLatestVersion = update.latestVersion.Trim().ToLower().TrimStart('v');
            update.updateAvailable = !string.IsNullOrEmpty(cleanLocalVersion) && !string.IsNullOrEmpty(cleanLatestVersion) && cleanLocalVersion != cleanLatestVersion;
        }

        public void DeleteFiles(string stagingPath)
        {
            string modName = Path.GetFileName(filePath);
            string[] possibleExtensions = new[]
            {
                ".zip",
                ".rar",
                ".7z"
            };

            Logger.Log($"Deleting {modName}...");
            if (Directory.Exists(filePath))
            {
                Logger.Log($"  Deleted manual mod: {modName}");
                Directory.Delete(filePath, true);
                if (isManual)
                {
                    string manualPath = Path.Combine(stagingPath, GameData.Manual);
                    for (int extension = 0; extension < possibleExtensions.Length; extension++)
                    {
                        string path = Path.Combine(manualPath, filePath + possibleExtensions[extension]);
                        if (File.Exists(path))
                        {
                            string manualArchiveName = Path.GetFileName(path);
                            Logger.Log($"  Deleted manual archive: {manualArchiveName}");
                            File.Delete(path);
                        }
                    }
                }
            }

            string archivePath = Path.Combine(stagingPath, GameData.Archive);
            if (Directory.Exists(archivePath))
            {
                for (int extension = 0; extension < possibleExtensions.Length; extension++)
                {
                    string path = Path.Combine(archivePath, modName + possibleExtensions[extension]);
                    if (File.Exists(path))
                    {
                        string manualArchiveName = Path.GetFileName(path);
                        Logger.Log($"  Deleted archive: {manualArchiveName}");
                        File.Delete(path);
                    }
                }
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
                JObject jsonObject = await NexusUtils.GetModFilesJsonAsync(gameDomain, url.nexusId);
                if (jsonObject == null)
                {
                    update.hasValidNexusData = false;
                    return;
                }

                JArray filesArray = jsonObject["files"] as JArray;
                if (filesArray == null || filesArray.Count == 0)
                {
                    update.hasValidNexusData = false;
                    return;
                }

                UpdateFiles(filesArray);
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