namespace Crescent.Plugins
{
    [Serializable]
    public class ProfileData
    {
        public string name = "Default";
        public string? iconName;
        public List<ProfileModData> mods = new();

        public bool IsModInstalled(ModData modData)
        {
            for (int i = 0; i < mods.Count; i++)
                if (string.Equals(mods[i].modName, modData.modName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public bool IsModEnabled(ModData modData)
        {
            for (int i = 0; i < mods.Count; i++)
                if (string.Equals(mods[i].modName, modData.modName, StringComparison.OrdinalIgnoreCase)) return mods[i].isEnabled;
            return false;
        }

        public void SetModEnabled(ModData modData, bool isEnabled)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                if (string.Equals(mods[i].modName, modData.modName, StringComparison.OrdinalIgnoreCase))
                {
                    mods[i].isEnabled = isEnabled;
                    return;
                }
            }
            
            mods.Add(new ProfileModData
            {
                modName = modData.modName, 
                isEnabled = isEnabled
            });
        }
    }
}