using Silk.Utilities;

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
                    lastLaunchedVersion = Version.Clean(Runtime.Version)
                };
                loaded.SaveAsync().FireAndForget();
                return loaded;
            }

            string currentClean = Version.Clean(Runtime.Version);
            string recordedClean = Version.Clean(loaded.lastLaunchedVersion);

            if (!string.IsNullOrEmpty(recordedClean) && Version.IsNewerVersion(currentClean, recordedClean))
            {
                loaded.previousVersion = loaded.lastLaunchedVersion;
                loaded.lastLaunchedVersion = currentClean;
                loaded.showPostUpdatePopup = true;
                loaded.SaveAsync().FireAndForget();
            }

            return loaded;
        }

        public async Task StagePendingUpdateAsync(Version targetRelease)
        {
            previousVersion = Version.Clean(Runtime.Version);
            lastLaunchedVersion = Version.Clean(targetRelease.version);
            lastReleaseNotes = targetRelease.releaseNotes;
            showPostUpdatePopup = true;
            await SaveAsync();
        }

        public async Task AcknowledgePopupAsync()
        {
            showPostUpdatePopup = false;
            lastLaunchedVersion = Version.Clean(Runtime.Version);
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