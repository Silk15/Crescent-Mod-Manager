using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Crescent.Plugins;
using Crescent.Versioning;

namespace Crescent.UI
{
    public class LibraryPanel : StackPanel
    {
        public WrapPanel activeGrid;
        public Separator separator;
        public WrapPanel totalGrid;
        public Dictionary<string, PluginCard> activeCards = new(StringComparer.OrdinalIgnoreCase);

        public event Action<PluginData>? onPluginClicked;

        public LibraryPanel()
        {
            Orientation = Orientation.Vertical;
            Margin = new Thickness(30, 25, 30, 30);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Top;

            activeGrid = new()
            {
                Orientation = Orientation.Horizontal
            };

            separator = new()
            {
                Background = Colors.Separator,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 1,
                Margin = new Thickness(0, 10, 0, 10)
            };

            totalGrid = new()
            {
                Orientation = Orientation.Horizontal
            };

            RenderInstalledPlugins();
            _ = LoadRemotePluginsAsync();
        }

        public void RenderInstalledPlugins()
        {
            activeGrid.Children.Clear();
            activeCards.Clear();

            HashSet<string> processedNames = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < PluginData.InstalledPlugins.Count; index++)
            {
                PluginData pluginData = PluginData.InstalledPlugins[index];
                if (!processedNames.Add(pluginData.Name)) continue;

                PluginCard card = new(pluginData, false);
                card.onClick += _ => onPluginClicked?.Invoke(pluginData);
                activeGrid.Children.Add(card);
                activeCards[NormalizeName(pluginData.Name)] = card;
            }

            if (activeGrid.Children.Count > 0 && !Children.Contains(activeGrid))
            {
                Children.Add(activeGrid);
                Children.Add(separator);
            }

            if (!Children.Contains(totalGrid))
                Children.Add(totalGrid);
        }

        public async Task LoadRemotePluginsAsync()
        {
            List<GithubPluginInfo> remotePlugins = new();
            try
            {
                remotePlugins = await GithubPluginInfo.GetAvailablePluginsAsync();
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to fetch plugins:\n{exception}");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                totalGrid.Children.Clear();

                for (int index = 0; index < remotePlugins.Count; index++)
                {
                    GithubPluginInfo remote = remotePlugins[index];
                    if (string.IsNullOrEmpty(remote.name)) continue;

                    string normalizedRemote = NormalizeName(remote.name);
                    PluginData? matchedLocal = PluginData.InstalledPlugins.FirstOrDefault(plugin => NormalizeName(plugin.Name) == normalizedRemote);

                    if (matchedLocal != null)
                    {
                        bool hasUpdate = VersionInfo.IsNewerVersion(remote.version, matchedLocal.version);
                        if (hasUpdate && activeCards.TryGetValue(normalizedRemote, out PluginCard? existingCard))
                        {
                            int cardIndex = activeGrid.Children.IndexOf(existingCard);
                            if (cardIndex >= 0)
                            {
                                PluginCard updatedCard = new(matchedLocal, true);
                                updatedCard.onClick += _ => onPluginClicked?.Invoke(matchedLocal);
                                updatedCard.onActionClick += async _ => await InstallPluginAsync(remote, true);
                                activeGrid.Children[cardIndex] = updatedCard;
                                activeCards[normalizedRemote] = updatedCard;
                            }
                        }
                    }
                    else
                    {
                        PluginCard card = new(remote);
                        card.onActionClick += async _ => await InstallPluginAsync(remote, false);
                        totalGrid.Children.Add(card);
                    }
                }
            });
        }

        public static string NormalizeName(string name) => name.Replace(" ", "").ToLowerInvariant();

        public async Task InstallPluginAsync(GithubPluginInfo remotePlugin, bool isUpdate)
        {
            if (remotePlugin.name == null || remotePlugin.pluginBytes == null) return;

            string actionLabel = isUpdate ? "Updating Plugin" : "Installing Plugin";
            ToastManager.ShowProgress(actionLabel, $"{remotePlugin.name} ({remotePlugin.version})", "Extracting plugin files", 50, true);

            string targetDirectory = Path.Combine(Runtime.PluginPath, remotePlugin.name);
            bool extracted = await GithubUpdater.InstallBufferAsync(remotePlugin.pluginBytes, remotePlugin.name, targetDirectory);

            if (!extracted)
            {
                ToastManager.CompleteProgress("Plugin Failed", $"Could not install {remotePlugin.name}", ToastType.Error, 4000);
                return;
            }

            await PluginStartupProcess.LoadPluginAsync(targetDirectory);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RenderInstalledPlugins();
            });

            await LoadRemotePluginsAsync();

            string completionTitle = isUpdate ? "Plugin Updated" : "Plugin Installed";
            string completionVerb = isUpdate ? "Updated" : "Installed";
            ToastManager.CompleteProgress(completionTitle, $"{completionVerb} {remotePlugin.name} ({remotePlugin.version})", ToastType.Success, 3500, "Library");
        }
    }
}