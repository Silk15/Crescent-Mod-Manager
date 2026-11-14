using Crescent.Json.Serialization;

namespace Crescent.Versioning
{
    [Serializable]
    public class VersionTracker
    {
        public static readonly string TrackerFilePath = Path.Combine(Runtime.DataPath, "version.json");
        public static VersionTracker? instance;

        public string lastLaunchedVersion = string.Empty;
        public string previousVersion = string.Empty;
        public string lastReleaseNotes = string.Empty;
        public bool showPostUpdatePopup;

        public static VersionTracker Instance
        {
            get
            {
                if (instance == null) instance = Load();
                return instance;
            }
        }

        public static VersionTracker Load()
        {
            VersionTracker? loaded = FileManager.ReadJson<VersionTracker>(TrackerFilePath);
            if (loaded == null)
            {
                loaded = new()
                {
                    lastLaunchedVersion = VersionInfo.Clean(Runtime.Version)
                };
                _ = loaded.SaveAsync();
                return loaded;
            }

            string currentClean = VersionInfo.Clean(Runtime.Version);
            string recordedClean = VersionInfo.Clean(loaded.lastLaunchedVersion);

            if (!string.IsNullOrEmpty(recordedClean) && VersionInfo.IsNewerVersion(currentClean, recordedClean))
            {
                loaded.previousVersion = loaded.lastLaunchedVersion;
                loaded.lastLaunchedVersion = currentClean;
                loaded.showPostUpdatePopup = true;
                _ = loaded.SaveAsync();
            }

            return loaded;
        }

        public async Task StagePendingUpdateAsync(VersionInfo targetRelease)
        {
            previousVersion = VersionInfo.Clean(Runtime.Version);
            lastLaunchedVersion = VersionInfo.Clean(targetRelease.version);
            lastReleaseNotes = targetRelease.releaseNotes;
            showPostUpdatePopup = true;
            await SaveAsync();
        }

        public async Task AcknowledgePopupAsync()
        {
            showPostUpdatePopup = false;
            lastLaunchedVersion = VersionInfo.Clean(Runtime.Version);
            await SaveAsync();
        }

        public async Task SaveAsync()
        {
            try
            {
                string? directory = Path.GetDirectoryName(TrackerFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
                await FileManager.WriteJsonAsync(TrackerFilePath, this);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }
    }
}