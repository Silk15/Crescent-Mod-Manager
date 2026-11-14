namespace Crescent.APIs.Discord
{
    public class ConnectTask : IStartupTask
    {
        public string GetName() => GetType().FullName!;

        public Task ExecuteAsync(Action<string>? statusCallback)
        {
            statusCallback?.Invoke("Initialising Discord Rich Presence...");
            if (DiscordManager.Connect()) DiscordManager.SetIdle();

            return Task.CompletedTask;
        }
    }
}