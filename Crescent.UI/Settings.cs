using Crescent.Json.Serialization;

namespace Crescent.UI
{
    [Serializable]
    public class Settings
    {
        public static readonly string SettingsFilePath = Path.Combine(Runtime.DataPath, "settings.json");

        private static Settings? instance;
        
        public bool enableDiscordPresence;
        public bool autoDeployOnDownload = true;
        public bool autoResolveConflicts;
        public bool checkUpdatesOnLaunch = true;
        public bool skipUpdatePopup;
        public bool autoRegisterNxm = true;
        public bool autoScrollConsole = true;

        public static Settings Instance
        {
            get
            {
                if (instance == null) instance = LoadSync();
                return instance;
            }
        }

        public static Settings LoadSync()
        {
            Settings? loaded = FileManager.ReadJson<Settings>(SettingsFilePath);
            if (loaded != null) instance = loaded;
            else instance = new();
            return instance;
        }

        public async Task SaveAsync()
        {
            try
            {
                string? directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
                await FileManager.WriteJsonAsync(SettingsFilePath, this);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }
    }
}