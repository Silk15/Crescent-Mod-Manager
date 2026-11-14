using Crescent.Plugins;
using Crescent.Utilities;
using DiscordRPC;

namespace Crescent.APIs.Discord
{
    public class DiscordManager
    {
        public const string ApplicationId = "1570954799145549946";

        public static RichPresence? currentPresence;
        public static DiscordRpcClient? client;
        public static bool isEnabled;

        [InitialiseOnLoad]
        public static void Initialise()
        {
            ApiStartupProcess.onStartupTasksGathered += () => new List<IStartupTask>
            {
                new ConnectTask()
            };
        }

        public static void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            if (!isEnabled)
            {
                Disconnect();
                return;
            }

            if (Connect())
            {
                if (currentPresence != null) client?.SetPresence(currentPresence);
                else SetIdle();
            }
        }

        public static bool Connect()
        {
            if (!isEnabled) return false;
            if (client is
                {
                    IsInitialized: true, 
                    IsDisposed: false
                }) 
                return true;

            try
            {
                Disconnect();
                client = new(ApplicationId);
                return client.Initialize();
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return false;
            }
        }

        public static void Disconnect()
        {
            try
            {
                if (client != null)
                {
                    if (client.IsInitialized && !client.IsDisposed)
                        client.ClearPresence();

                    if (!client.IsDisposed)
                        client.Dispose();
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
            finally
            {
                client = null;
            }
        }

        public static void SetIdle()
        {
            RichPresence presence = new()
            {
                Details = "",
                State = $"",
                Timestamps = Timestamps.Now,
                Assets = new()
                {
                    SmallImageKey = "cover_image",
                },
                Buttons = new []
                {
                    new Button
                    {
                        Label = "Get",
                        Url = "https://github.com/Silk15/Crescent-Mod-Manager"
                    }
                }
            };

            SetPresence(presence);
        }

        public static void SetModding(PluginData pluginData)
        {
            int enabledCount = pluginData.installedMods.Count(mod => pluginData.SelectedProfile.IsModInstalled(mod) && pluginData.SelectedProfile.IsModEnabled(mod));
            RichPresence presence = new()
            {
                Details = $"Modding {pluginData.Name}",
                State = $"{pluginData.SelectedProfile.name} • {enabledCount} mod{enabledCount.WithPlural()}",
                Assets = new()
                {
                    LargeImageKey = "cover_image",
                    SmallImageKey = pluginData.Name.ToLower().Replace(" ", "")
                }
            };

            SetPresence(presence);
        }

        public static void SetPlaying(PluginData pluginData)
        {
            int enabledCount = pluginData.installedMods.Count(mod => pluginData.SelectedProfile.IsModInstalled(mod) && pluginData.SelectedProfile.IsModEnabled(mod));
            RichPresence presence = new()
            {
                Details = $"Playing {pluginData.Name}",
                State = $"{pluginData.SelectedProfile.name} • {enabledCount} mod{enabledCount.WithPlural()}",
                Timestamps = Timestamps.Now,
                Assets = new()
                {
                    LargeImageKey = "cover_image",
                    SmallImageKey = pluginData.Name.ToLower().Replace(" ", "")
                }
            };

            SetPresence(presence);
        }

        public static void SetPresence(RichPresence presence)
        {
            currentPresence = presence;
            if (!isEnabled) return;
            try
            {
                if (client is not { IsInitialized: true, IsDisposed: false } && !Connect()) return;
                client?.SetPresence(presence);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        public static void ClearPresence()
        {
            try
            {
                if (client is { IsInitialized: true, IsDisposed: false })
                    client.ClearPresence();
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }
    }
}