using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Crescent.Plugins
{
    [Serializable]
    public class ModData
    {
        public static readonly Regex ModIdRegex = new(@"\b\d{3,6}\b");
        public static readonly Regex ModNameRegex = new(@"[-_]\d{4,}(-\d+)*");
        public static readonly Regex ModCleanRegex = new(@"\d{4}-\d{2}-\d{2}T.*$");

        public string filePath;
        public string modName;
        public string displayName;
    
        public bool isManual;
        public bool isDeployed;
        public Url url;
        public List<string> requirements = new();
        
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayName)) return displayName;
                string clean = ModNameRegex.Replace(modName, string.Empty);
                clean = ModCleanRegex.Replace(clean, string.Empty);

                clean = clean.Replace('_', ' ').Replace('-', ' ');
                clean = clean.Replace(".7z", "");
                clean = clean.Replace(".zip", "");
                clean = clean.Replace(".rar", "");
                clean = clean.Trim();
                if (string.IsNullOrWhiteSpace(clean)) displayName = modName;
                
                return displayName = clean;
            }
        }
        
        public ModData() { }

        public ModData(GameData gameData, string modName, string filePath)
        {
            this.filePath = filePath;
            this.modName = modName;
            
            isManual = gameData.IsCoreMod(this) ? true : filePath.Contains("Manual", StringComparison.OrdinalIgnoreCase);
            url = new(gameData.BaseUrl, ModIdRegex.Match(this.modName).Value);
        }

        [Serializable]
        public class Url
        {
            public string nexusUrl;
            public string nexusId;

            public Url(string baseUrl, string id)
            {
                nexusUrl = $"{baseUrl}{id}";
                nexusId = id;
            }
        }
    }
}