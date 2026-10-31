namespace Crescent.Plugins
{
    [Serializable]
    public class ProfileData
    {
        public string name = "Default";
        public List<ProfileModData> mods = new();

        public bool IsModInstalled(ModData modData)
        {
            for (int index = 0; index < mods.Count; index++)
                if (string.Equals(mods[index].modName, modData.modName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public bool IsModEnabled(ModData modData)
        {
            for (int index = 0; index < mods.Count; index++)
                if (string.Equals(mods[index].modName, modData.modName, StringComparison.OrdinalIgnoreCase)) return mods[index].isEnabled;
            return false;
        }

        public void SetModEnabled(ModData modData, bool isEnabled)
        {
            for (int index = 0; index < mods.Count; index++)
            {
                if (string.Equals(mods[index].modName, modData.modName, StringComparison.OrdinalIgnoreCase))
                {
                    mods[index].isEnabled = isEnabled;
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