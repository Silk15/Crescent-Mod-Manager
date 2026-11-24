using System.Diagnostics;
using Silk.Utilities;
using Newtonsoft.Json;

namespace Crescent.Plugins
{
    [Serializable]
    public abstract class PluginData
    {
        public const string Default = "Default";
        public const string Archive = ".archive";
        public const string Manual = ".manual";

        public static List<PluginData> InstalledPlugins { get; set; } = new();

        [JsonProperty(Order = 8)]
        public List<ModData> installedMods = new();

        [JsonProperty(Order = 6)] public List<ProfileData> profiles = new();
        [JsonProperty(Order = 7)] public string selectedProfileName = "Default";

        [JsonProperty(Order = 5)] public DeploymentState deploymentState = DeploymentState.NotDeployed;
        [JsonProperty(Order = 4)] public string deploymentStatus = "Not deployed";
        [JsonProperty(Order = 9)] public long playTime;
        [JsonProperty(Order = 4)] public bool isEnabled;
        
        public Theme? theme;

        [NonSerialized]
        public string version;

        [NonSerialized]
        public string folderName;

        public ModGridType modGridType;

        public event Action<PluginData> onDeploymentStatusChanged;

        [JsonProperty(Order = 0)] public abstract string DeploymentPath { get; set; }
        [JsonProperty(Order = 1)] public abstract string StagingPath { get; set; }
        [JsonProperty(Order = 2)] public abstract string ExePath { get; set; }

        [JsonIgnore] public DeploymentState DeploymentState => deploymentState == DeploymentState.NotDeployed && installedMods.Any(modData => modData.isDeployed) ? installedMods.Any(modData => SelectedProfile.IsModEnabled(modData) && !modData.isDeployed) ? DeploymentState.PartiallyDeployed : DeploymentState.Deployed : deploymentState;
        [JsonIgnore] public string DeploymentStatus => deploymentStatus == "Not deployed" && installedMods.Any(modData => modData.isDeployed) ? "Deployed mods restored from configuration" : deploymentStatus;

        [JsonIgnore] public abstract string Name { get; }
        [JsonIgnore] public virtual int ModCount => SelectedProfile?.mods.Count ?? 0;
        
        [JsonIgnore] public virtual Deployer Deployer { get; set; }
        [JsonIgnore] public abstract API API { get; }

        [JsonIgnore] public ProfileData SelectedProfile
        {
            get
            {
                ProfileData? foundProfile = profiles.Find(profileData => string.Equals(profileData.name, selectedProfileName, StringComparison.OrdinalIgnoreCase));
                if (foundProfile == null)
                {
                    for (int index = 0; index < profiles.Count; index++)
                    {
                        ProfileData profileData = profiles[index];
                        if (profileData.name == "Default") foundProfile = profileData;
                    }

                    if (foundProfile == null) foundProfile = new() { name = "Default" };
                }
                return foundProfile;
            }
            set => selectedProfileName = value.name;
        }

        [JsonIgnore] public List<ProfileData> Profiles
        {
            get
            {
                if (profiles.Count == 0 || !profiles.Any(profileData => string.Equals(profileData.name, "Default", StringComparison.OrdinalIgnoreCase)))
                {
                    ProfileData defaultProfile = new() { name = "Default" };
                    profiles.Insert(0, defaultProfile);
                }
                return profiles;
            }
        }

        protected PluginData()
        {
            Deployer = new FileDeployer(this);
        }

        #region Window Functions

        public static Func<int, int, string, string, Task>? OnProgressWindowUpdate { get; set; }
        public static Func<Task>? OnProgressWindowOpen { get; set; }
        public static Func<Task>? OnProgressWindowClose { get; set; }

        public static Func<Dictionary<string, List<ModData>>, Task<bool>>? OnConflictResolve { get; set; }
        public static Func<string, string, Task<string>>? OnOverlapConfirm { get; set; }

        public static Action<string, string, string, int, string>? OnToastShow { get; set; }
        public static Action<string, string, string, double, bool>? OnToastProgress { get; set; }
        public static Action<double, double, string, string>? OnToastProgressUpdate { get; set; }
        public static Action<string, string, string, int, string>? OnToastProgressComplete { get; set; }
        public static Action? OnToastProgressClose { get; set; }

        public static void ShowToast(string title, string message, string toastType = "Info", int duration = 4000, string detail = "")
        {
            OnToastShow?.Invoke(title, message, toastType, duration, detail);
        }

        public static void ShowProgressToast(string title, string message, string detail = "", double percentage = 0, bool isIndeterminate = false)
        {
            OnToastProgress?.Invoke(title, message, detail, percentage, isIndeterminate);
        }

        public static void UpdateProgressToast(double current, double total, string modName, string targetFile = "")
        {
            OnToastProgressUpdate?.Invoke(current, total, modName, targetFile);
        }

        public static void CompleteProgressToast(string title, string message, string toastType = "Success", int duration = 4500, string detail = "")
        {
            OnToastProgressComplete?.Invoke(title, message, toastType, duration, detail);
        }

        public static void CloseProgressToast()
        {
            OnToastProgressClose?.Invoke();
        }

        #endregion

        #region Utilities

        public virtual async Task SaveAsync()
        {
            string directoryPath = Path.Combine(Runtime.DataPath, "Games", Name);
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
            string filePath = Path.Combine(directoryPath, $"{Name.ToLower().Replace(" ", "")}.sav");
            await FileManager.WriteJsonAsync(filePath, (dynamic)this);
        }

        public virtual Process? Launch() => Process.Start(new ProcessStartInfo(ExePath)
        {
            UseShellExecute = true
        });

        public Dictionary<string, List<ModData>> GetConflicts()
        {
            Dictionary<string, List<ModData>> files = new(StringComparer.OrdinalIgnoreCase);
            foreach (ModData modData in installedMods)
            {
                if (!SelectedProfile.IsModEnabled(modData)) continue;
                foreach (string fileItem in modData.ownedFiles)
                {
                    if (!files.ContainsKey(fileItem)) files[fileItem] = new();
                    if (!files[fileItem].Any(existingMod => string.Equals(existingMod.modName, modData.modName, StringComparison.OrdinalIgnoreCase)))
                        files[fileItem].Add(modData);
                }
            }

            Dictionary<string, List<ModData>> conflicts = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<ModData>> filePair in files)
                if (filePair.Value.Count > 1)
                    conflicts[filePair.Key] = filePair.Value;

            return conflicts;
        }

        public void ReplaceModId(string oldModName, string newModName, ModData? targetMod = null)
        {
            if (string.IsNullOrEmpty(oldModName) || string.IsNullOrEmpty(newModName)) return;
            string strippedOldName = ModData.StripArchiveExtension(oldModName);
            string strippedNewName = ModData.StripArchiveExtension(newModName);

            foreach (ProfileData profileData in profiles)
            {
                ProfileModData? existingProfileMod = profileData.mods.Find(profileModData =>
                    string.Equals(profileModData.modName, oldModName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ModData.StripArchiveExtension(profileModData.modName), strippedOldName, StringComparison.OrdinalIgnoreCase));

                if (existingProfileMod != null)
                {
                    bool wasEnabled = existingProfileMod.isEnabled;
                    profileData.mods.RemoveAll(profileModData =>
                        string.Equals(profileModData.modName, oldModName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ModData.StripArchiveExtension(profileModData.modName), strippedOldName, StringComparison.OrdinalIgnoreCase));

                    ProfileModData? newModEntry = profileData.mods.Find(profileModData =>
                        string.Equals(profileModData.modName, newModName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ModData.StripArchiveExtension(profileModData.modName), strippedNewName, StringComparison.OrdinalIgnoreCase));

                    if (newModEntry != null) newModEntry.isEnabled = wasEnabled;
                    else profileData.mods.Add(new() { modName = newModName, isEnabled = wasEnabled });
                }
            }

            installedMods.RemoveAll(modData =>
                modData != targetMod &&
                !string.Equals(modData.modName, newModName, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(modData.modName, oldModName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(ModData.StripArchiveExtension(modData.modName), strippedOldName, StringComparison.OrdinalIgnoreCase)));
        }

        public void RemoveMod(ModData modData)
        {
            string strippedName = ModData.StripArchiveExtension(modData.modName);
            installedMods.Remove(modData);
            installedMods.RemoveAll(existing =>
                string.Equals(existing.modName, modData.modName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ModData.StripArchiveExtension(existing.modName), strippedName, StringComparison.OrdinalIgnoreCase));

            foreach (ProfileData profileData in profiles)
            {
                profileData.mods.RemoveAll(profileMod =>
                    string.Equals(profileMod.modName, modData.modName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ModData.StripArchiveExtension(profileMod.modName), strippedName, StringComparison.OrdinalIgnoreCase));
            }
        }

        #endregion

        #region Events

        public virtual void OnModLoaded(ModData modData)
        {
            ProfileData? defaultProfile = Profiles.Find(profileData => string.Equals(profileData.name, "Default", StringComparison.OrdinalIgnoreCase));
            if (defaultProfile != null && !defaultProfile.IsModInstalled(modData)) defaultProfile.SetModEnabled(modData, true);
        }

        public virtual void OnModUnloaded(ModData modData) { }

        public virtual void Load() { }
        public virtual void Unload() { }

        #endregion

        #region Deployment & Withdrawl

        public void SetLoadOrder()
        {
            List<ModData> ordered = installedMods.OrderBy(mod => mod.loadOrder < 0 ? int.MaxValue : mod.loadOrder).ThenByDescending(IsCoreMod).ToList();
            for (int index = 0; index < ordered.Count; index++)
                ordered[index].loadOrder = index;
        }

        public void SetDeploymentStatus(DeploymentState state, string statusMessage)
        {
            deploymentState = state;
            deploymentStatus = statusMessage;
            onDeploymentStatusChanged?.Invoke(this);
        }

        public virtual void TryDeploy()
        {
            Task.Run(async () =>
            {
                try
                {
                    await Deploy();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
            });
        }

        public virtual async Task Deploy() => await Deployer.Deploy();

        public virtual void Withdraw() => Deployer.Withdraw();

        public virtual void WithdrawMod(ModData modData) => Deployer.WithdrawMod(modData);

        #endregion

        #region Filtering

        public virtual List<string> GetModFilters() => new()
        {
            "All",
            "Managed",
            "Manual",
            "Enabled",
            "Disabled",
            "Deployed",
            "Has Update"
        };

        public virtual bool MatchesFilter(ModData modData, string filterName)
        {
            switch (filterName)
            {
                case "All":
                    return true;
                case "Managed":
                    return !modData.isManual;
                case "Manual":
                    return modData.isManual;
                case "Enabled":
                    return SelectedProfile.IsModEnabled(modData);
                case "Disabled":
                    return !SelectedProfile.IsModEnabled(modData);
                case "Deployed":
                    return modData.isDeployed;
                case "Has Update":
                    return modData.UpdateAvailable;
                default:
                    return true;
            }
        }

        public virtual List<string> GetCoreModMatches() => new();

        public virtual bool IsCoreMod(ModData modData) => false;

        #endregion
    }
}