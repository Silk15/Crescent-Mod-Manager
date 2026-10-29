using System.Diagnostics.CodeAnalysis;
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
        
        [NotNull] public static List<GameData>? InstalledGames { get; set; } = new();
        
        [JsonProperty(Order = 8)] 
        public List<ModData> installedMods = new();
        
        [JsonProperty(Order = 6)]
        public List<ProfileData> profiles = new();
        
        [JsonProperty(Order = 7)]
        public string selectedProfileName = "Default";
        
        [JsonProperty(Order = 5)]
        public DeploymentState deploymentState = DeploymentState.NotDeployed;
        
        [JsonProperty(Order = 4)]
        public string deploymentStatus = "Not deployed";
        
        public event Action<GameData>? onDeploymentStatusChanged;

        [JsonProperty(Order = 0)] public abstract string DeploymentPath { get; set; }
        
        [JsonProperty(Order = 1)] public abstract string StagingPath { get; set; }
        
        [JsonProperty(Order = 2)] public abstract string ExePath { get; set; }

        [JsonIgnore] public DeploymentState DeploymentState => deploymentState == DeploymentState.NotDeployed && installedMods.Any(mod => mod.isDeployed) ? installedMods.Any(mod => SelectedProfile.IsModEnabled(mod) && !mod.isDeployed) ? DeploymentState.PartiallyDeployed : DeploymentState.Deployed : deploymentState;
        [JsonIgnore] public string DeploymentStatus => deploymentStatus == "Not deployed" && installedMods.Any(mod => mod.isDeployed) ? "Deployed mods restored from configuration" : deploymentStatus;
        
        [JsonIgnore] public abstract string Name { get; }
        [JsonIgnore] public abstract string NexusGameDomain { get; }

        [JsonIgnore] public abstract string GameLogo { get; }
        [JsonIgnore] public abstract string Banner { get; }
        [JsonIgnore] public abstract string Theme { get; }

        [JsonIgnore] public virtual int ModCount => SelectedProfile?.mods.Count ?? 0;
        [JsonIgnore] public string AssemblyName => GetType().Assembly.GetName().Name!;
        [JsonIgnore] public virtual string BaseUrl => $"https://www.nexusmods.com/{NexusGameDomain}/mods/";
        
        [JsonIgnore] 
        public ProfileData SelectedProfile
        {
            get
            {
                ProfileData? found = profiles.Find(profile => string.Equals(profile.name, selectedProfileName, StringComparison.OrdinalIgnoreCase));
                if (found == null)
                {
                    for (int index = 0; index < profiles.Count; index++)
                    {
                        ProfileData profile = profiles[index];
                        if (profile.name == "Default") found = profile;
                    }

                    if (found == null) found = new() { name = "Default" };
                }
                return found;
            }
            set => selectedProfileName = value.name;
        }

        [JsonIgnore]
        public List<ProfileData> Profiles
        {
            get
            {
                if (profiles.Count == 0 || !profiles.Any(profile => string.Equals(profile.name, "Default", StringComparison.OrdinalIgnoreCase)))
                {
                    ProfileData defaultProfile = new() { name = "Default" };
                    profiles.Insert(0, defaultProfile);
                }
                return profiles;
            }
        }
        
        public static Func<string, string, Task<string>>? OnRequestOverlapConfirmation { get; set; }
        public static Func<Task>? OnRequestProgressShow { get; set; }
        public static Func<int, int, string, string, Task>? OnRequestProgressUpdate { get; set; }
        public static Func<Task>? OnRequestProgressClose { get; set; }

        public virtual void OnModLoaded(ModData mod)
        {
            ProfileData? defaultProfile = Profiles.Find(profile => string.Equals(profile.name, "Default", StringComparison.OrdinalIgnoreCase));
            if (defaultProfile != null && !defaultProfile.IsModInstalled(mod)) defaultProfile.SetModEnabled(mod, true);
        }

        public virtual void OnModUnloaded(ModData mod) { }
        
        public virtual void Load() { }
        public virtual void Unload() { }

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

        public virtual async Task SaveAsync()
        {
            string path = Path.Combine(Runtime.DataPath, "Games");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string filePath = Path.Combine(path, $"{Name.ToLower().Replace(" ", "")}.sav");
            await FileManager.WriteJson(filePath, (dynamic)this);
        }

        public void SetDeploymentStatus(DeploymentState state, string status)
        {
            deploymentState = state;
            deploymentStatus = status;
            onDeploymentStatusChanged?.Invoke(this);
        }

        public abstract Task Deploy();
        public abstract void Withdraw();
        public abstract void WithdrawMod(ModData mod);

        public virtual List<string> GetModFilters()
        {
            List<string> filters = new() { "All", "Managed", "Manual", "Enabled", "Disabled" };
            return filters;
        }

        public virtual bool MatchesFilter(ModData mod, string filter)
        {
            switch (filter)
            {
                case "All":
                    return true;
                case "Managed":
                    return !mod.isManual;
                case "Manual":
                    return mod.isManual;
                case "Enabled":
                    return SelectedProfile.IsModEnabled(mod);
                case "Disabled":
                    return !SelectedProfile.IsModEnabled(mod);
                default:
                    return true;
            }
        }

        public virtual List<string> GetCoreModMatches() => new();

        public virtual bool IsCoreMod(ModData mod) => false;
    }

    public class DeployedContent
    {
        public bool isManual;
        public string modName;
        public string sourceFile;
        public string targetFile;

        [NonSerialized]
        public string targetDirectory;

        public DeployedContent(string modName, string sourceFile, string targetFile, string targetDirectory, bool isManual)
        {
            this.modName = modName;
            this.sourceFile = sourceFile;
            this.targetFile = targetFile;
            this.targetDirectory = targetDirectory;
            this.isManual = isManual;
        }
    }

    public enum DeploymentState
    {
        NotDeployed,
        Deploying,
        Deployed,
        Withdrawing,
        PartiallyDeployed,
        Failed
    }
}