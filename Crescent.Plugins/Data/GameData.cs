using System.Diagnostics;
using System.Text;
using Crescent.Utilities;
using Crescent.Json.Serialization;
using Newtonsoft.Json;

namespace Crescent.Plugins
{
    [Serializable]
    public abstract class GameData
    {
        public const string Default = "Default";
        public const string Managed = "Managed";
        public const string Archive = "Archive";
        public const string Manual = "Manual";
        
        public static List<GameData> InstalledGames { get; set; } = new();
        
        [JsonProperty(Order = 8)] 
        public List<ModData> installedMods = new();
        
        [JsonProperty(Order = 6)] public List<ProfileData> profiles = new();
        [JsonProperty(Order = 7)] public string selectedProfileName = "Default";
        
        [JsonProperty(Order = 5)] public DeploymentState deploymentState = DeploymentState.NotDeployed;
        [JsonProperty(Order = 4)] public string deploymentStatus = "Not deployed";
        [JsonProperty(Order = 9)] public long playTime;
        
        public event Action<GameData> onDeploymentStatusChanged;

        [JsonProperty(Order = 0)] public abstract string DeploymentPath { get; set; }
        [JsonProperty(Order = 1)] public abstract string StagingPath { get; set; }
        [JsonProperty(Order = 2)] public abstract string ExePath { get; set; }

        [JsonIgnore] public DeploymentState DeploymentState => deploymentState == DeploymentState.NotDeployed && installedMods.Any(modData => modData.isDeployed) ? installedMods.Any(modData => SelectedProfile.IsModEnabled(modData) && !modData.isDeployed) ? DeploymentState.PartiallyDeployed : DeploymentState.Deployed : deploymentState;
        [JsonIgnore] public string DeploymentStatus => deploymentStatus == "Not deployed" && installedMods.Any(modData => modData.isDeployed) ? "Deployed mods restored from configuration" : deploymentStatus;

        #region Game Data
        
        [JsonIgnore] public abstract string Name { get; }
        [JsonIgnore] public abstract string GameLogo { get; }
        [JsonIgnore] public abstract string Banner { get; }
        [JsonIgnore] public abstract string Theme { get; }

        [JsonIgnore] public virtual int ModCount => SelectedProfile?.mods.Count ?? 0;
        [JsonIgnore] public string AssemblyName => GetType().Assembly.GetName().Name!;
        [JsonIgnore] public abstract Type LogFileType { get; }
        
        [JsonIgnore] public abstract string NexusName { get; }
        [JsonIgnore] public virtual string BaseUrl => $"https://www.nexusmods.com/{NexusName}/mods/";
        
        [JsonIgnore] public ProfileData SelectedProfile
        {
            get
            {
                ProfileData foundProfile = profiles.Find(profileData => string.Equals(profileData.name, selectedProfileName, StringComparison.OrdinalIgnoreCase));
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
        
        #endregion

        #region Window Functions
        
        public static Func<int, int, string, string, Task>? OnProgressWindowUpdate { get; set; }
        public static Func<Task>? OnProgressWindowOpen { get; set; }
        public static Func<Task>? OnProgressWindowClose { get; set; }
        
        public static Func<Dictionary<string, List<ModData>>, Task<bool>>? OnConflictResolve { get; set; }
        public static Func<string, string, Task<string>>? OnOverlapConfirm { get; set; }

        #endregion

        #region Utilities
        
        public virtual async Task SaveAsync()
        {
            string directoryPath = Path.Combine(Runtime.DataPath, "Games", Name);
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
            string filePath = Path.Combine(directoryPath, $"{Name.ToLower().Replace(" ", "")}.sav");
            await FileManager.WriteJson(filePath, (dynamic)this);
        }

        public virtual Process? Launch() => Process.Start(new ProcessStartInfo(ExePath) 
        {
            UseShellExecute = true
        });
        
        public Dictionary<string, List<ModData>> GetConflicts()
        {
            Dictionary<string, List<ModData>> files = new();
            foreach (ModData modData in installedMods)
            {
                if (!SelectedProfile.IsModEnabled(modData)) continue;
                foreach (string fileItem in modData.ownedFiles)
                {
                    if (!files.ContainsKey(fileItem)) files[fileItem] = new();
                    if (!files[fileItem].Any(existingMod => string.Equals(existingMod.modName, modData.modName, StringComparison.OrdinalIgnoreCase))) files[fileItem].Add(modData);
                }
            }

            Dictionary<string, List<ModData>> conflicts = new();
            foreach (KeyValuePair<string, List<ModData>> filePair in files)
            {
                if (filePair.Value.Count > 1) 
                    conflicts[filePair.Key] = filePair.Value;
            }
            
            return conflicts;
        }

        public void ReplaceModId(string oldModName, string newModName)
        {
            foreach (ProfileData profileData in profiles)
            {
                ProfileModData? existingProfileMod = profileData.mods.Find(profileModData => string.Equals(profileModData.modName, oldModName, StringComparison.OrdinalIgnoreCase));
                if (existingProfileMod != null)
                {
                    bool wasEnabled = existingProfileMod.isEnabled;
                    profileData.mods.Remove(existingProfileMod);

                    ProfileModData? newModEntry = profileData.mods.Find(profileModData => string.Equals(profileModData.modName, newModName, StringComparison.OrdinalIgnoreCase));
                    if (newModEntry != null) newModEntry.isEnabled = wasEnabled;
                    else profileData.mods.Add(new ProfileModData { modName = newModName, isEnabled = wasEnabled });
                }
            }

            installedMods.RemoveAll(modData => string.Equals(modData.modName, oldModName, StringComparison.OrdinalIgnoreCase));
        }
        
        #endregion

        #region Events

        public virtual void OnModLoaded(ModData modData)
        {
            ProfileData defaultProfile = Profiles.Find(profileData => string.Equals(profileData.name, "Default", StringComparison.OrdinalIgnoreCase));
            if (defaultProfile != null && !defaultProfile.IsModInstalled(modData)) defaultProfile.SetModEnabled(modData, true);
        }

        public virtual void OnModUnloaded(ModData modData) { }
        
        public virtual void Load() { }
        public virtual void Unload() { }
        
        #endregion

        #region Deployment & Withdrawl
        
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

        public abstract Task Deploy();
        public abstract void Withdraw();
        public abstract void WithdrawMod(ModData modData);

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
        
        public override string ToString()
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("Crescent Mod Manager [Game Export]");
            stringBuilder.AppendLine($"{Name} [profile: {SelectedProfile.name}] [mods: {ModCount}] [state: {DeploymentState}]");
            foreach (ModData modData in installedMods)
            {
                stringBuilder.AppendLine($"- [{modData.DisplayName.WithoutNumbers().Replace(" ", "")}] [v{modData.version}]");
                stringBuilder.AppendLine("  - File Data");
                stringBuilder.AppendLine($"    Update: [{modData.UpdateAvailable}]");
                stringBuilder.AppendLine($"    Latest: [v{modData.LatestVersion}]");
                stringBuilder.AppendLine("  - State");
                stringBuilder.AppendLine($"    Deployed: [{modData.isDeployed}]");
                stringBuilder.AppendLine($"    Symlinked: [{!modData.isManual}]");
                stringBuilder.AppendLine("  - Profiles");
                foreach (ProfileData profileData in Profiles)
                {
                    if (profileData.IsModInstalled(modData)) 
                        stringBuilder.AppendLine($"    [in: {profileData.name}] ({(profileData.IsModEnabled(modData) ? "enabled" : "disabled")})");
                }
            }
    
            return stringBuilder.ToString();
        }
    }
}