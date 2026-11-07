namespace Crescent.Plugins
{
    [Serializable]
    public class ExportedProfile
    {
        public string profileName;
        public string gameName;

        public List<ExportedMod> mods = new();

        public ExportedProfile() { }

        public ExportedProfile(PluginData pluginData, ProfileData profileData)
        {
            profileName = profileData.name;
            gameName = pluginData.Name;

            foreach (ModData modData in pluginData.installedMods)
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