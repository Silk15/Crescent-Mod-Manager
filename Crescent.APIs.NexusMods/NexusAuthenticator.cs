using Crescent.APIs;
using Crescent.Json.Serialization;
using Crescent.Utilities;
using Newtonsoft.Json.Linq;

namespace Crescent.Nexus.Authentication
{
    public static class NexusAuthenticator
    {
        public static readonly string ApiPath = Path.Combine(Runtime.DataPath, "nexus.api");
        public static byte[]? UserAvatarBytes { get; private set; }
        public static JObject? UserProfile { get; private set; }
        public static ApiKey? ApiKey { get; private set; }

        [InitialiseOnLoad]
        public static void Initialise()
        {
            ApiStartupProcess.onStartupTasksGathered += () => new IStartupTask[]
            {
                new LoadApiKeyTask(),
                new SignInTask()
            };
        }
        
        public class LoadApiKeyTask : IStartupTask
        {
            public string GetName() => GetType().FullName;

            public async Task ExecuteAsync(Action<string> updateTask)
            {
                updateTask?.Invoke("Loading Nexus API Key...");
                string settingsPath = Path.Combine(ApiPath);
                if (File.Exists(settingsPath)) ApiKey = await FileManager.ReadJson<ApiKey>(settingsPath);
                else
                {
                    ApiKey = new ApiKey();
                    await FileManager.WriteJson(ApiPath, ApiKey);
                }
            }
        }

        public class SignInTask : IStartupTask
        {
            public string GetName() => GetType().FullName;
            
            public async Task ExecuteAsync(Action<string>? updateTask)
            {
                updateTask?.Invoke("Signing In to Nexus Mods API...");
                if (ApiKey == null || string.IsNullOrEmpty(ApiKey.key)) return;
                try
                {
                    HttpClient client = new();
                    HttpRequestMessage request = new(HttpMethod.Get, "https://api.nexusmods.com/v1/users/validate.json");
                    request.Headers.Add("apikey", ApiKey.key);
                    request.Headers.Add("User-Agent", "CrescentModManager/1.0");
                    HttpResponseMessage response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonString = await response.Content.ReadAsStringAsync();
                        UserProfile = JObject.Parse(jsonString);
                        string profileUrl = UserProfile["profile_url"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(profileUrl)) UserAvatarBytes = await client.GetByteArrayAsync(profileUrl);
                        
                    }
                    else
                    {
                        ApiKey.key = string.Empty;
                        UserProfile = null;
                        UserAvatarBytes = null;
                        await FileManager.WriteJson(ApiPath, ApiKey);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            }
        }
    }
}