using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Crescent.UI.Controls.Sidebar;
using Crescent.UI.Controls.Tabs.Elements;
using Crescent.UI.Queues;
using Crescent.UI.Tabs.Elements;
using Newtonsoft.Json.Linq;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public class BrowseTab : StackPanel, ISidebarTab
    {
        public static readonly List<string> Categories = new()
        {
            "New",
            "Updated",
            "Trending",
            "Popular",
            "Surprise"
        };

        public static readonly List<string> TimePeriods = new()
        {
            "All Time",
            "24 Hours",
            "7 Days",
            "14 Days",
            "30 Days",
            "1 Year"
        };

        public static readonly Dictionary<string, List<string>> NexusTagGroups = new()
        {
            ["Generative AI Usage"] = NexusUtils.GenerativeAiTags,
            ["Content & Realism"] = new()
            {
                "Saved games",
                "Humour, Joke or Just for Fun",
                "Not Safe For Work",
                "Unrealistic",
                "Related to Movies/TV/Books/Other Games",
                "Translation",
                "Real World Issues",
                "Horror"
            },
            ["Resources"] = new()
            {
                "Modder's Resource",
                "Non-Playable Resource",
                "Tutorials for Modders",
                "Tutorials for Players",
                "Utilities for Modders",
                "Utilities for Players"
            },
            ["Audio"] = new()
            {
                "Music",
                "Sound FX",
                "Voice Acting"
            },
            ["Visuals & Gameplay"] = new()
            {
                "Animation - New",
                "Animation - Modified",
                "Models/Meshes",
                "Textures",
                "Video",
                "User Interface",
                "Lighting",
                "Races - New",
                "Camera",
                "Visual Effects/Particles",
                "NPC Behaviours (AI)",
                "Menu Replacer"
            },
            ["Landscape & Environment"] = new()
            {
                "Terrain",
                "Sky",
                "Water",
                "Weather",
                "Foliage (Plants)"
            }
        };

        public HashSet<string> excludedTags = new(NexusUtils.GenerativeAiTags, StringComparer.OrdinalIgnoreCase);

        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;
        public ScrollViewer? parentScrollViewer;

        public string selectedCategory = "New";
        public string selectedPeriod = "All Time";
        public string searchQuery = string.Empty;

        public CrescentTextBox? searchBox;
        public CrescentComboBox? categoryComboBox;
        public CrescentComboBox? periodComboBox;

        public List<BrowseModCard> trendingCards = new();
        public List<BrowseModCard> browseCards = new();

        public UniformGrid? trendingTopHeroGrid;
        public UniformGrid? trendingBottomGrid;
        public ModGrid? browseGrid;
        public TextBlock? statusBlock;
        public TextBlock? queueBadgeBlock;
        public Border? trendingSectionHost;
        public CrescentButton? loadMoreButton;

        public int currentOffset;
        public bool hasMoreMods = true;
        public bool isLoadingMore;
        public bool isSetup;
        public bool isLoading;
        public bool isVirtualisationQueued;

        private CancellationTokenSource? loadCancellationToken;
        private CancellationTokenSource? searchDebounceToken;

        public int Order => 1;

        public BrowseTab()
        {
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
            EffectiveViewportChanged += (_, _) => QueueVirtualisation();

            ModDownloadQueue.onQueueChanged += () => Dispatcher.UIThread.Post(UpdateQueueBadge);
            ModDownloadQueue.onModDownloadFinished += (plugin, _) =>
            {
                if (plugin != pluginData) return;
                Dispatcher.UIThread.Post(() =>
                {
                    RefreshCardInstallStates();
                    sidebarOverlay?.GetTab<ModsTab>()?.Refresh();
                });
            };
        }

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Browse",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8)
        };

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

        private void OnParentScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            UpdateVirtualisation();

            if (parentScrollViewer == null || isLoading || isLoadingMore || !hasMoreMods) return;

            double maxOffset = parentScrollViewer.Extent.Height - parentScrollViewer.Viewport.Height;
            if (maxOffset > 0 && parentScrollViewer.Offset.Y >= maxOffset - 350)
                LoadMoreModsAsync().FireAndForget();
        }

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
            if (pluginData == null || browseGrid == null) return;
            AttachScrollViewer();

            for (int index = 0; index < trendingCards.Count; index++)
                trendingCards[index].Realise();

            if (parentScrollViewer == null)
            {
                for (int index = 0; index < browseCards.Count; index++)
                {
                    if (index < 12) browseCards[index].Realise();
                    else browseCards[index].Virtualise();
                }

                return;
            }

            Point? gridOrigin = browseGrid.TranslatePoint(new Point(0, 0), parentScrollViewer);
            if (gridOrigin == null) return;

            double listTopInViewport = gridOrigin.Value.Y;
            double viewportHeight = parentScrollViewer.Bounds.Height;
            if (viewportHeight <= 0) viewportHeight = 900;

            double realizeTop = -450;
            double realizeBottom = viewportHeight + 450;
            double virtualizeTop = -800;
            double virtualizeBottom = viewportHeight + 800;

            for (int index = 0; index < browseCards.Count; index++)
            {
                BrowseModCard card = browseCards[index];
                Rect bounds = card.Bounds;

                if (bounds.Height <= 0 && index > 0 && bounds.Y <= 0)
                {
                    if (index < 12) card.Realise();
                    continue;
                }

                double cardTop = listTopInViewport + bounds.Y;
                double cardHeight = bounds.Height > 0 ? bounds.Height : card.EstimatedHeight;
                double cardBottom = cardTop + cardHeight;

                if (cardBottom >= realizeTop && cardTop <= realizeBottom) card.Realise();
                else if (cardBottom < virtualizeTop || cardTop > virtualizeBottom) card.Virtualise();
            }
        }

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            if (this.pluginData != pluginData)
            {
                isSetup = false;
                trendingCards.Clear();
                browseCards.Clear();
            }

            this.sidebarOverlay = sidebarOverlay;
            this.pluginData = pluginData;
            Setup();
        }

        public void OnClick()
        {
            if (!isSetup) Setup();
            else RefreshCardInstallStates();
        }

        public void Setup()
        {
            if (isSetup || pluginData == null) return;
            isSetup = true;

            Children.Clear();
            Margin = new Thickness(30, 15, 30, 30);
            Spacing = 15;
            Background = Brushes.Transparent;

            StackPanel mainPanel = new() { Spacing = 10 };
            mainPanel.Children.Add(BuildHeaderRow());
            mainPanel.Children.Add(BuildToolbar());
            mainPanel.Children.Add(new Separator
            {
                Height = 1,
                Margin = new Thickness(0),
                Background = Colors.Separator,
                VerticalAlignment = VerticalAlignment.Bottom
            });

            Children.Add(mainPanel);

            trendingSectionHost = BuildTrendingSection();
            Children.Add(trendingSectionHost);

            statusBlock = new TextBlock
            {
                Text = "Loading mods...",
                FontSize = 13,
                Foreground = Brushes.Gray,
                IsVisible = false
            };
            Children.Add(statusBlock);

            browseGrid = new ModGrid
            {
                MinColumnWidth = 265,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };
            Children.Add(browseGrid);

            loadMoreButton = new CrescentButton(ButtonStyle.Translucent)
            {
                Content = "Load More",
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(24, 0),
                Height = 36,
                Margin = new Thickness(0, 10, 0, 0),
                IsVisible = false
            };
            loadMoreButton.onClick += () => LoadMoreModsAsync().FireAndForget();
            Children.Add(loadMoreButton);

            LoadInitialDataAsync().FireAndForget();
        }

        private Grid BuildHeaderRow()
        {
            Grid headerRow = new();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock titleBlock = new()
            {
                Text = $"Browse {pluginData!.Name}",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            headerRow.Children.Add(titleBlock);

            if (!NexusUtils.IsPremiumUser)
            {
                TextBlock accountBadge = new()
                {
                    Text = "Free Account",
                    Foreground = Brushes.Gray,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 12,
                    Margin = new Thickness(14, 0, 0, 2),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                ToolTip.SetTip(accountBadge, "Nexus restricts direct downloads to Premium users. Clicking Download will open the Nexus file page where you can click Mod Manager Download. Make sure you have Crescent set as your NXM handler (see Settings)");
                Grid.SetColumn(accountBadge, 1);
                headerRow.Children.Add(accountBadge);
            }

            queueBadgeBlock = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = Colors.Accent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsVisible = ModDownloadQueue.QueuedCount > 0
            };

            UpdateQueueBadge();
            Grid.SetColumn(queueBadgeBlock, 3);
            headerRow.Children.Add(queueBadgeBlock);
            return headerRow;
        }

        private Grid BuildToolbar()
        {
            Grid toolbarGrid = new() { ColumnSpacing = 12 };
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid filterGrid = new()
            {
                ColumnSpacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition(2.2, GridUnitType.Star) { MinWidth = 110, MaxWidth = 260 });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition(1.1, GridUnitType.Star) { MinWidth = 90, MaxWidth = 140 });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition(1.3, GridUnitType.Star) { MinWidth = 95, MaxWidth = 160 });

            searchBox = new CrescentTextBox
            {
                PlaceholderText = "Search Nexus mods...",
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            searchBox.TextChanged += async (_, _) =>
            {
                searchDebounceToken?.Cancel();
                searchDebounceToken = new CancellationTokenSource();
                CancellationToken token = searchDebounceToken.Token;
                try
                {
                    await Task.Delay(220, token);
                    if (token.IsCancellationRequested) return;
                    searchQuery = searchBox.Text?.Trim() ?? string.Empty;
                    await LoadBrowseListAsync();
                }
                catch (OperationCanceledException)
                {
                }
            };

            Grid.SetColumn(searchBox, 0);
            filterGrid.Children.Add(searchBox);
            categoryComboBox = new CrescentComboBox
            {
                ItemsSource = Categories,
                SelectedIndex = Categories.IndexOf(selectedCategory),
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            categoryComboBox.SelectionChanged += (_, _) =>
            {
                selectedCategory = categoryComboBox.SelectedItem as string ?? "New";
                LoadBrowseListAsync().FireAndForget();
            };

            Grid.SetColumn(categoryComboBox, 1);
            filterGrid.Children.Add(categoryComboBox);
            periodComboBox = new CrescentComboBox
            {
                ItemsSource = TimePeriods,
                SelectedIndex = TimePeriods.IndexOf(selectedPeriod),
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            periodComboBox.SelectionChanged += (_, _) =>
            {
                selectedPeriod = periodComboBox.SelectedItem as string ?? "All Time";
                LoadBrowseListAsync().FireAndForget();
            };

            Grid.SetColumn(periodComboBox, 2);
            filterGrid.Children.Add(periodComboBox);

            Grid.SetColumn(filterGrid, 0);
            toolbarGrid.Children.Add(filterGrid);

            StackPanel actionPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            actionPanel.Children.Add(BuildTagFilterButton());
            actionPanel.Children.Add(new Separator
            {
                Width = 1,
                Height = 35,
                Background = Colors.Separator,
                Padding = new Thickness(3, 0, 3, 0)
            });

            CrescentButton refreshButton = new(ButtonStyle.Translucent)
            {
                Content = "Refresh",
                Padding = new Thickness(12, 0),
                Height = 35
            };
            refreshButton.onClick += () => LoadInitialDataAsync().FireAndForget();
            actionPanel.Children.Add(refreshButton);

            Grid.SetColumn(actionPanel, 1);
            toolbarGrid.Children.Add(actionPanel);

            return toolbarGrid;
        }

        private Border BuildTrendingSection()
        {
            Border containerBorder = new()
            {
                Background = Colors.Overlay,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14, 12)
            };

            StackPanel sectionStack = new() { Spacing = 10 };

            Grid headerRow = new();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock sectionTitle = new()
            {
                Text = "Trending Mods",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(sectionTitle, 0);

            CrescentButton viewAllButton = new(ButtonStyle.Transparent)
            {
                Content = "View all",
                Height = 28,
                Padding = new Thickness(8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            viewAllButton.onClick += () =>
            {
                if (categoryComboBox != null)
                    categoryComboBox.SelectedIndex = Categories.IndexOf("Trending");
            };
            Grid.SetColumn(viewAllButton, 1);

            headerRow.Children.Add(sectionTitle);
            headerRow.Children.Add(viewAllButton);

            trendingTopHeroGrid = new UniformGrid
            {
                Columns = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };

            trendingBottomGrid = new UniformGrid
            {
                Columns = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };

            sectionStack.Children.Add(headerRow);
            sectionStack.Children.Add(trendingTopHeroGrid);
            sectionStack.Children.Add(trendingBottomGrid);

            containerBorder.Child = sectionStack;
            return containerBorder;
        }

        private CrescentButton BuildTagFilterButton()
        {
            CrescentButton tagButton = new(ButtonStyle.Translucent)
            {
                Content = GetTagFilterButtonLabel(),
                Height = 35,
                Padding = new Thickness(12, 0)
            };

            StackPanel flyoutStack = new()
            {
                Spacing = 6,
                MinWidth = 250,
                Margin = new Thickness(6)
            };

            foreach (KeyValuePair<string, List<string>> group in NexusTagGroups)
            {
                if (flyoutStack.Children.Count > 0)
                    flyoutStack.Children.Add(ModLayouts.CreateLine(new Thickness(0, 4, 0, 2)));

                flyoutStack.Children.Add(new TextBlock
                {
                    Text = group.Key.ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 2, 0, 2)
                });

                for (int index = 0; index < group.Value.Count; index++)
                {
                    string tag = group.Value[index];
                    CheckBox tagBox = new()
                    {
                        Content = $"Exclude {tag}",
                        IsChecked = excludedTags.Contains(tag),
                        FontSize = 12
                    };

                    tagBox.IsCheckedChanged += (_, _) =>
                    {
                        if (tagBox.IsChecked ?? false) excludedTags.Add(tag);
                        else excludedTags.Remove(tag);

                        tagButton.Content = GetTagFilterButtonLabel();
                        LoadInitialDataAsync().FireAndForget();
                    };

                    flyoutStack.Children.Add(tagBox);
                }
            }

            ScrollViewer flyoutScroll = new()
            {
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = flyoutStack
            };

            tagButton.Flyout = new Flyout
            {
                Placement = PlacementMode.BottomEdgeAlignedRight,
                Content = flyoutScroll
            };

            return tagButton;
        }

        private string GetTagFilterButtonLabel()
        {
            if (excludedTags.Count == 0) return "Content Preferences";
            bool onlyAiExcluded = excludedTags.Count == NexusUtils.GenerativeAiTags.Count && NexusUtils.GenerativeAiTags.All(excludedTags.Contains);
            return onlyAiExcluded ? "Content: No AI" : $"Content ({excludedTags.Count} Excluded)";
        }

        private void UpdateQueueBadge()
        {
            if (queueBadgeBlock == null) return;
            int count = ModDownloadQueue.QueuedCount;
            queueBadgeBlock.IsVisible = count > 0;
            queueBadgeBlock.Text = count > 0 ? $"Downloading ({count} in queue)" : string.Empty;
        }

        public void RefreshCardInstallStates()
        {
            for (int index = 0; index < trendingCards.Count; index++)
                trendingCards[index].RefreshInstallState();

            for (int index = 0; index < browseCards.Count; index++)
                browseCards[index].RefreshInstallState();
        }

        private async Task LoadInitialDataAsync()
        {
            await LoadTrendingSectionAsync();
            await LoadBrowseListAsync();
        }

        private async Task LoadTrendingSectionAsync()
        {
            if (pluginData == null || trendingTopHeroGrid == null || trendingBottomGrid == null) return;

            try
            {
                List<JObject> trendingMods = await NexusUtils.GetTrendingAsync(pluginData.API.Name, excludedTags);
                trendingTopHeroGrid.Children.Clear();
                trendingBottomGrid.Children.Clear();
                trendingCards.Clear();

                if (trendingSectionHost != null)
                    trendingSectionHost.IsVisible = trendingMods.Count > 0 && string.IsNullOrEmpty(searchQuery);

                for (int index = 0; index < trendingMods.Count; index++)
                {
                    BrowseCardStyle style = index < 2 ? BrowseCardStyle.TrendingHero : BrowseCardStyle.TrendingCompact;
                    BrowseModCard card = new(pluginData, trendingMods[index], this, style);
                    card.Realise();
                    trendingCards.Add(card);

                    if (index < 2) trendingTopHeroGrid.Children.Add(card);
                    else trendingBottomGrid.Children.Add(card);
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }

        public async Task LoadBrowseListAsync()
        {
            if (pluginData == null || browseGrid == null || statusBlock == null) return;

            if (trendingSectionHost != null)
                trendingSectionHost.IsVisible = trendingCards.Count > 0 && string.IsNullOrEmpty(searchQuery) && selectedCategory != "Trending";

            loadCancellationToken?.Cancel();
            loadCancellationToken = new CancellationTokenSource();
            CancellationToken token = loadCancellationToken.Token;

            currentOffset = 0;
            hasMoreMods = true;
            isLoading = true;
            statusBlock.IsVisible = true;
            statusBlock.Text = $"Loading {selectedCategory.ToLower()} mods...";
            if (loadMoreButton != null) loadMoreButton.IsVisible = false;

            try
            {
                const int pageSize = 24;
                List<JObject> mods = await NexusUtils.GetBrowseModsAsync(pluginData.API.Name, selectedCategory, selectedPeriod, searchQuery, excludedTags, null, pageSize, currentOffset);
                if (token.IsCancellationRequested) return;

                browseGrid.Children.Clear();
                browseCards.Clear();

                if (mods.Count == 0)
                {
                    hasMoreMods = false;
                    statusBlock.IsVisible = true;
                    statusBlock.Text = "No mods matched your current filters.";
                    return;
                }

                statusBlock.IsVisible = false;
                currentOffset += mods.Count;
                hasMoreMods = mods.Count >= pageSize;

                for (int index = 0; index < mods.Count; index++)
                {
                    BrowseModCard card = new(pluginData, mods[index], this, BrowseCardStyle.Standard);
                    if (index < 12) card.Realise();
                    else card.Virtualise();

                    browseCards.Add(card);
                    browseGrid.Children.Add(card);
                }

                if (loadMoreButton != null)
                {
                    loadMoreButton.Content = "Load More";
                    loadMoreButton.IsEnabled = true;
                    loadMoreButton.IsVisible = hasMoreMods;
                }

                QueueVirtualisation();
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                statusBlock.IsVisible = true;
                statusBlock.Text = "Failed to load mods from Nexus Mods.";
            }
            finally
            {
                isLoading = false;
            }
        }

        public async Task LoadMoreModsAsync()
        {
            if (pluginData == null || browseGrid == null || isLoading || isLoadingMore || !hasMoreMods) return;

            CancellationToken token = loadCancellationToken?.Token ?? CancellationToken.None;
            isLoadingMore = true;

            if (loadMoreButton != null)
            {
                loadMoreButton.Content = "Loading...";
                loadMoreButton.IsEnabled = false;
            }

            try
            {
                const int pageSize = 24;
                List<JObject> mods = await NexusUtils.GetBrowseModsAsync(pluginData.API.Name, selectedCategory, selectedPeriod, searchQuery, excludedTags, null, pageSize, currentOffset);
                if (token.IsCancellationRequested) return;

                currentOffset += mods.Count;
                hasMoreMods = mods.Count >= pageSize;

                for (int index = 0; index < mods.Count; index++)
                {
                    BrowseModCard card = new(pluginData, mods[index], this, BrowseCardStyle.Standard);
                    card.Virtualise();

                    browseCards.Add(card);
                    browseGrid.Children.Add(card);
                }

                QueueVirtualisation();
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
            finally
            {
                isLoadingMore = false;
                if (loadMoreButton != null)
                {
                    loadMoreButton.Content = "Load More";
                    loadMoreButton.IsEnabled = true;
                    loadMoreButton.IsVisible = hasMoreMods;
                }
            }
        }
    }
}