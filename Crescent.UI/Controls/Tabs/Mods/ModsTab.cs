using System.Runtime;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Crescent.UI.Controls.Sidebar;
using Crescent.UI.Controls.Tabs.Elements;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public class ModsTab : StackPanel, ISidebarTab
    {
        public static List<string> SortOptions => ModsToolbar.SortOptions;

        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;
        public ModsToolbar? toolbar;

        public List<RenderedMod> renderedMods = new();
        public Dictionary<ModData, RenderedMod> renderedModCache = new();
        public TextBlock? titleBlock;
        public Border? listHost;
        public Panel? listPanel;
        public ScrollViewer? parentScrollViewer;
        public ModGridType? currentGridType;

        public string selectedSortOption = "A - Z";
        public string selectedFilterOption = "All";
        public bool isSetup;
        public bool isSyncing;
        public bool isMigrating;
        public bool hasSyncedThisSession;
        public bool isVirtualisationQueued;
        public CancellationTokenSource? syncCancellationToken;

        public int Order => 0;

        public ModsTab()
        {
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
            EffectiveViewportChanged += (_, _) => QueueVirtualisation();
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            AttachScrollViewer();
            QueueVirtualisation();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (parentScrollViewer != null)
            {
                parentScrollViewer.ScrollChanged -= OnParentScrollChanged;
                parentScrollViewer.SizeChanged -= OnParentSizeChanged;
                parentScrollViewer = null;
            }
        }

        private void AttachScrollViewer()
        {
            ScrollViewer? found = this.FindAncestorOfType<ScrollViewer>();
            if (ReferenceEquals(parentScrollViewer, found)) return;

            if (parentScrollViewer != null)
            {
                parentScrollViewer.ScrollChanged -= OnParentScrollChanged;
                parentScrollViewer.SizeChanged -= OnParentSizeChanged;
            }

            parentScrollViewer = found;
            if (parentScrollViewer != null)
            {
                parentScrollViewer.ScrollChanged += OnParentScrollChanged;
                parentScrollViewer.SizeChanged += OnParentSizeChanged;
            }
        }

        private void OnParentScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateVirtualisation();

        private void OnParentSizeChanged(object? sender, SizeChangedEventArgs e) => QueueVirtualisation();

        public void QueueVirtualisation()
        {
            if (isVirtualisationQueued) return;
            isVirtualisationQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                isVirtualisationQueued = false;
                UpdateVirtualisation();
            }, DispatcherPriority.Loaded);
        }

        public void UpdateVirtualisation()
        {
            if (pluginData == null || listPanel == null || renderedMods.Count == 0) return;
            AttachScrollViewer();

            if (parentScrollViewer == null)
            {
                int initialCount = Math.Min(renderedMods.Count, 24);
                for (int index = 0; index < renderedMods.Count; index++)
                {
                    if (index < initialCount) renderedMods[index].Realise(pluginData.modGridType);
                    else renderedMods[index].Virtualise(pluginData.modGridType);
                }

                return;
            }

            Point? listOrigin = listPanel.TranslatePoint(new Point(0, 0), parentScrollViewer);
            if (listOrigin == null) return;

            double listTopInViewport = listOrigin.Value.Y;
            double viewportHeight = parentScrollViewer.Bounds.Height;
            if (viewportHeight <= 0) viewportHeight = 900;

            double realiseTop = -450;
            double realiseBottom = viewportHeight + 450;
            double virtualizeTop = -800;
            double virtualizeBottom = viewportHeight + 800;

            for (int index = 0; index < renderedMods.Count; index++)
            {
                RenderedMod mod = renderedMods[index];
                Rect bounds = mod.Bounds;

                if (bounds.Height <= 0 && index > 0 && bounds.Y <= 0)
                {
                    if (index < 24) mod.Realise(pluginData.modGridType);
                    continue;
                }

                double modTop = listTopInViewport + bounds.Y;
                double modHeight = bounds.Height > 0 ? bounds.Height : mod.GetEstimatedHeight(pluginData.modGridType);
                double modBottom = modTop + modHeight;

                if (modBottom >= realiseTop && modTop <= realiseBottom)
                    mod.Realise(pluginData.modGridType);
                
                else if (modBottom < virtualizeTop || modTop > virtualizeBottom)
                    mod.Virtualise(pluginData.modGridType);
            }
        }

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Mods",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new(10, 8)
        };

        public async void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            NexusUtils.GameStagingPaths[pluginData.API.Name] = pluginData.StagingPath;
            if (this.pluginData != pluginData)
            {
                renderedModCache.Clear();
                renderedMods.Clear();
                isSetup = false;
                hasSyncedThisSession = false;
            }

            this.pluginData = pluginData;
            this.sidebarOverlay = sidebarOverlay;

            if (isMigrating) return;

            isMigrating = true;
            try
            {
                await ModsIO.MoveLegacyFoldersAsync(pluginData);
            }
            finally
            {
                isMigrating = false;
            }

            isSetup = false;
            hasSyncedThisSession = false;
            Setup();
        }

        public void OnClick()
        {
            if (isMigrating) return;
            if (!isSetup) Setup();
            else RenderMods();
        }

        public void Refresh()
        {
            syncCancellationToken?.Cancel();
            renderedModCache.Clear();
            renderedMods.Clear();
            isSetup = false;
            isSyncing = false;
            hasSyncedThisSession = false;
            Children.Clear();
            Setup();
        }

        public void RefreshDeployedStates()
        {
            for (int index = 0; index < renderedMods.Count; index++)
                renderedMods[index].RefreshDeployedState();
        }

        public void RefreshMod(ModData modData)
        {
            if (renderedModCache.TryGetValue(modData, out RenderedMod? renderedMod))
                renderedMod.RefreshMod();
        }

        public void Setup()
        {
            if (isSetup) return;
            isSetup = true;

            Children.Clear();
            renderedMods.Clear();
            Margin = new(30, 15, 30, 30);
            Spacing = 15;
            Background = Brushes.Transparent;

            StackPanel mainPanel = new() { Spacing = 10 };

            titleBlock = new()
            {
                Text = $"{pluginData!.SelectedProfile.name} ({pluginData.ModCount} Mod{pluginData.ModCount.WithPlural()})",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            mainPanel.Children.Add(titleBlock);

            toolbar = new ModsToolbar(this);
            mainPanel.Children.Add(toolbar);
            mainPanel.Children.Add(new Separator
            {
                Height = 1,
                Margin = new Thickness(0),
                Background = Colors.Separator,
                VerticalAlignment = VerticalAlignment.Bottom
            });

            Children.Add(mainPanel);
            if (string.IsNullOrEmpty(pluginData.StagingPath)) return;
            if (!Directory.Exists(pluginData.StagingPath))
            {
                Children.Add(new TextBlock
                {
                    Text = "Staging directory is not configured or does not exist. Please set it in the settings tab",
                    Foreground = Brushes.IndianRed,
                    FontWeight = FontWeight.Bold
                });
                return;
            }

            currentGridType = pluginData.modGridType;
            listPanel = ModLayouts.Get(pluginData.modGridType).CreateContainer();
            listHost = new() { Child = listPanel };
            Children.Add(listHost);

            RenderMods();
        }

        public void OnCriteriaChanged()
        {
            if (listPanel == null || toolbar == null) return;
            RenderModsList(toolbar.SearchText);
        }

        public void RenderMods()
        {
            if (listPanel == null || toolbar == null) return;

            selectedFilterOption = toolbar.FilterComboBox.SelectedItem as string ?? "All";
            selectedSortOption = toolbar.SortComboBox.SelectedItem as string ?? "A - Z";

            if (!isSyncing) RenderModsList(toolbar.SearchText);
            if (!hasSyncedThisSession) StartSessionSync();
        }

        private void StartSessionSync()
        {
            hasSyncedThisSession = true;
            isSyncing = true;
            syncCancellationToken?.Cancel();
            syncCancellationToken = new();
            CancellationToken token = syncCancellationToken.Token;
            Task.Run(async () =>
            {
                try
                {
                    bool listChanged = ModsIO.SyncMods(pluginData, token);
                    if (token.IsCancellationRequested) return;
                    isSyncing = false;

                    await Dispatcher.UIThread.InvokeAsync(OnCriteriaChanged);
                    if (listChanged && pluginData != null) await pluginData.SaveAsync();
                    await ModsIO.CheckUpdatesOnLaunchAsync(pluginData, RefreshMod, token);
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                }
                finally
                {
                    isSyncing = false;
                }
            }, token).FireAndForget();
        }

        public void RenderModsList(string filterText)
        {
            if (pluginData == null || listHost == null || isSyncing) return;

            listPanel?.Children.Clear();
            if (currentGridType != pluginData.modGridType || listPanel == null)
            {
                currentGridType = pluginData.modGridType;
                listPanel = ModLayouts.Get(pluginData.modGridType).CreateContainer();
                listHost.Child = listPanel;
            }

            renderedMods.Clear();

            bool isDefaultProfile = string.Equals(pluginData.selectedProfileName, PluginData.Default, StringComparison.OrdinalIgnoreCase);
            List<RenderedMod> matchingMods = new();
            HashSet<RenderedMod> seenControls = new();

            if (isDefaultProfile)
            {
                for (int index = 0; index < pluginData.installedMods.Count; index++)
                {
                    ModData modData = pluginData.installedMods[index];
                    TryAddMatchingMod(modData, filterText, seenControls, matchingMods);
                }
            }
            else
            {
                ProfileData profile = pluginData.SelectedProfile;
                for (int index = 0; index < profile.mods.Count; index++)
                {
                    ProfileModData profileMod = profile.mods[index];
                    string strippedProfileMod = ModsIO.StripArchiveExtension(profileMod.modName);
                    ModData? modData = pluginData.installedMods.Find(mod => string.Equals(mod.modName, profileMod.modName, StringComparison.OrdinalIgnoreCase) || string.Equals(ModsIO.StripArchiveExtension(mod.modName), strippedProfileMod, StringComparison.OrdinalIgnoreCase));

                    if (modData != null) TryAddMatchingMod(modData, filterText, seenControls, matchingMods);
                }
            }

            List<RenderedMod> sortedRenderedMods = Sort(matchingMods, selectedSortOption);
            if (titleBlock != null) titleBlock.Text = $"{pluginData.SelectedProfile.name} ({pluginData.ModCount} Mod{pluginData.ModCount.WithPlural()})";
            for (int index = 0; index < sortedRenderedMods.Count; index++)
            {
                RenderedMod renderedMod = sortedRenderedMods[index];
                renderedMod.SetLayout(pluginData.modGridType, index < 20);
                renderedMods.Add(renderedMod);
                listPanel.Children.Add(renderedMod);
            }

            QueueVirtualisation();
        }

        private void TryAddMatchingMod(ModData modData, string filterText, HashSet<RenderedMod> seenControls, List<RenderedMod> matchingMods)
        {
            if (!pluginData!.MatchesFilter(modData, selectedFilterOption)) return;

            RenderedMod renderedMod = GetOrCreateRenderedMod(modData);
            if (!string.IsNullOrEmpty(filterText) && !renderedMod.cleanName.Contains(filterText, StringComparison.OrdinalIgnoreCase) && !modData.modName.Contains(filterText, StringComparison.OrdinalIgnoreCase)) return;
            if (seenControls.Add(renderedMod)) matchingMods.Add(renderedMod);
        }

        public RenderedMod GetOrCreateRenderedMod(ModData modData)
        {
            if (renderedModCache.TryGetValue(modData, out RenderedMod? existing))
            {
                existing.RefreshDeployedState();
                return existing;
            }

            RenderedMod created = new(pluginData!, modData, this);
            renderedModCache[modData] = created;
            return created;
        }

        public List<RenderedMod> Sort(List<RenderedMod> items, string sortMode)
        {
            return sortMode switch
            {
                "Z - A" => items.OrderByDescending(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Managed First" => items.OrderBy(item => item.modData.isManual).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Manual First" => items.OrderByDescending(item => item.modData.isManual).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Enabled First" => items.OrderByDescending(item => pluginData!.SelectedProfile.IsModEnabled(item.modData)).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Deployed First" => items.OrderByDescending(item => item.modData.isDeployed).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                "Update Available" => items.OrderByDescending(item => item.modData.UpdateAvailable).ThenBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => items.OrderBy(item => item.cleanName, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }
    }
}