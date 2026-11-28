using System.Numerics;
using Silk.Utilities;

namespace Crescent.UI
{
    [Serializable]
    public class Settings
    {
        public static readonly string SettingsPath = Path.Combine(Runtime.DataPath, "settings.json");

        private static Settings? instance;
        
        public bool enableDiscordPresence;
        public bool autoDeployOnDownload = true;
        public bool autoResolveConflicts;
        public bool checkUpdatesOnLaunch = true;
        public bool skipUpdatePopup;
        public bool autoRegisterNxm = true;
        public bool autoScrollConsole = true;
        public bool minimiseToTray;
        public bool saveWindowSize;
        public bool maximised;

        public Vector2 windowSize = new(1500, 900);

        public static Settings Instance
        {
            get
            {
                if (instance == null) instance = Load();
                return instance;
            }
        }

        public static Settings Load()
        {
            Settings? loaded = FileManager.ReadJson<Settings>(SettingsPath);
            if (loaded != null) instance = loaded;
            else instance = new();
            return instance;
        }

        public async Task SaveAsync()
        {
            try
            {
                string? directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
                await FileManager.WriteJsonAsync(SettingsPath, this);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }
    }
}