namespace Crescent.Plugins
{
    [Serializable]
    public class ExportedProfile
    {
        public string profileName;
        public string gameName;

        public List<ExportedMod> mods = new();

        public ExportedProfile() { }

        public ExportedProfile(GameData gameData, ProfileData profileData)
        {
            profileName = profileData.name;
            gameName = gameData.Name;

            foreach (ModData modData in gameData.installedMods)
            {
                if (profileData.IsModInstalled(modData))
                {
                    mods.Add(new()
                    {
                        displayName = modData.DisplayName,
                        isEnabled = profileData.IsModEnabled(modData),
                        url = modData.url.nexusUrl
                    });
                }
            }
        }
        
        [Serializable]
        public class ExportedMod
        {
            public string displayName;
            public string url;
            public bool isEnabled;
        }
    }
}