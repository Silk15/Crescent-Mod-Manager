using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Crescent.Json.Serialization;
using Crescent.Plugins;

namespace Crescent.UI
{
    public class LibraryPanel : StackPanel
    {
        private readonly WrapPanel activeGrid;
        private readonly Separator separator;
        private readonly WrapPanel totalGrid;

        public LibraryPanel()
        {
            Orientation = Orientation.Vertical;
            Margin = new Thickness(30, 25, 30, 30);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Top;

            activeGrid = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            separator = new Separator
            {
                Background = Colors.Separator,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 1,
                Margin = new Thickness(0, 10, 0, 10)
            };

            totalGrid = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            Task.Run(async () => await LoadLibraryAsync());
        }

        public event Action<PluginData>? onPluginClicked;

        private async Task LoadLibraryAsync()
        {
            List<GithubPluginInfo> remotePlugins = new();
            try
            {
                remotePlugins = await GithubPluginInfo.GetAvailablePluginsAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to fetch plugins:\n{ex}");
            }
            
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                List<string> installedNames = new();
                if (Directory.Exists(Runtime.PluginPath))
                    foreach (string directoryPath in Directory.GetDirectories(Runtime.PluginPath))
                    {
                        string jsonPath = Path.Combine(directoryPath, "plugin.json");
                        if (File.Exists(jsonPath))
                            try
                            {
                                PluginManifest manifest = await FileManager.ReadJson<PluginManifest>(jsonPath);
                                if (!string.IsNullOrEmpty(manifest.name)) installedNames.Add(manifest.name);
                            }
                            catch { }
                    }

                foreach (PluginData installedPlugin in PluginData.InstalledPlugins)
                {
                    if (!installedNames.Contains(installedPlugin.Name))
                        installedNames.Add(installedPlugin.Name);
                }

                HashSet<string> processedActivePlugins = new(StringComparer.OrdinalIgnoreCase);
                foreach (string pluginName in installedNames)
                {
                    PluginData? matchedData = null;
                    foreach (PluginData pluginData in PluginData.InstalledPlugins)
                    {
                        if (pluginData.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase) || pluginData.Name.Replace(" ", "").Equals(pluginName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                            matchedData = pluginData;
                    }

                    GithubPluginInfo? matchedRemote = null;
                    foreach (GithubPluginInfo remote in remotePlugins)
                    {
                        if (remote.name != null && (remote.name.Equals(pluginName, StringComparison.OrdinalIgnoreCase) || remote.name.Replace(" ", "").Equals(pluginName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)))
                            matchedRemote = remote;
                    }

                    bool hasUpdate = false;
                    if (matchedRemote != null && matchedData != null && !string.IsNullOrEmpty(matchedRemote.version) && !string.IsNullOrEmpty(matchedData.version))
                    {
                        Version? remoteVersion;
                        Version? localVersion;
                        if (Version.TryParse(matchedRemote.version, out remoteVersion) && Version.TryParse(matchedData.version, out localVersion))
                        {
                            if (remoteVersion > localVersion)
                                hasUpdate = true;
                        }
                    }

                    if (matchedData != null && processedActivePlugins.Add(matchedData.Name))
                    {
                        PluginCard card = new(matchedData, hasUpdate);
                        card.onClick += _ => onPluginClicked?.Invoke(matchedData);
                        card.onActionClick += async _ =>
                        {
                            if (matchedRemote != null) await DownloadAndInstallPluginAsync(matchedRemote);
                        };
                        activeGrid.Children.Add(card);
                    }
                }

                if (activeGrid.Children.Count > 0)
                {
                    Children.Add(activeGrid);
                    Children.Add(separator);
                }

                foreach (GithubPluginInfo remote in remotePlugins)
                {
                    bool isInstalled = false;
                    foreach (string name in installedNames)
                    {
                        if (remote.name != null && (remote.name.Equals(name, StringComparison.OrdinalIgnoreCase) || remote.name.Replace(" ", "").Equals(name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)))
                            isInstalled = true;
                    }

                    if (!isInstalled)
                    {
                        PluginCard card = new(remote);
                        card.onActionClick += async _ => await DownloadAndInstallPluginAsync(remote);
                        totalGrid.Children.Add(card);
                    }
                }

                Children.Add(totalGrid);
            });
        }

        private async Task DownloadAndInstallPluginAsync(GithubPluginInfo remotePlugin)
        {
            if (remotePlugin.name == null || remotePlugin.pluginBytes == null) return;
            string targetDirectory = Path.Combine(Runtime.PluginPath, remotePlugin.name);
            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            string temporaryExtractDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(temporaryExtractDirectory);

            string temporaryPluginPath = Path.Combine(temporaryExtractDirectory, remotePlugin.name + ".plugin");
            await File.WriteAllBytesAsync(temporaryPluginPath, remotePlugin.pluginBytes);

            string innerExtractDirectory = Path.Combine(temporaryExtractDirectory, "extracted");
            ZipFile.ExtractToDirectory(temporaryPluginPath, innerExtractDirectory, true);

            string sourceDirectory = innerExtractDirectory;
            string[] subDirectories = Directory.GetDirectories(innerExtractDirectory);
            string[] subFiles = Directory.GetFiles(innerExtractDirectory);
            if (subDirectories.Length == 1 && subFiles.Length == 0) sourceDirectory = subDirectories[0];

            foreach (string filePath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, filePath);
                string destinationFilePath = Path.Combine(targetDirectory, relativePath);
                string destinationDirectory = Path.GetDirectoryName(destinationFilePath)!;
                if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);
                File.Copy(filePath, destinationFilePath, true);
            }

            if (Directory.Exists(temporaryExtractDirectory)) Directory.Delete(temporaryExtractDirectory, true);
        }
    }
}