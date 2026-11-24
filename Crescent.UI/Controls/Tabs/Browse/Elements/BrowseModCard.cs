using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Crescent.UI.Controls;
using Crescent.UI.Controls.Tabs;
using Crescent.UI.Controls.Tabs.Elements;
using Crescent.UI.Dialogs;
using Crescent.UI.Queues;
using Newtonsoft.Json.Linq;
using Silk.Utilities;

namespace Crescent.UI.Tabs.Elements
{
    public class BrowseModCard : Card
    {
        private static readonly ConcurrentDictionary<string, Bitmap> BrowseThumbnailCache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly LinearGradientBrush HeroOverlayGradient = new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(0, 16, 16, 16), 0.0),
                new GradientStop(Color.FromArgb(140, 16, 16, 16), 0.45),
                new GradientStop(Color.FromArgb(235, 16, 16, 16), 1.0)
            }
        };

        public PluginData pluginData;
        public JObject modJson;
        public BrowseTab browseTab;
        public BrowseCardStyle cardStyle;

        public string nexusId;
        public string gameDomain;
        public string modName;
        public string authorName;
        public string categoryName;
        public string versionText;
        public string summaryText;
        public string pictureUrl;
        public string relativeUpdateText;
        public string releaseDateText;
        public string endorsementsText;
        public string downloadsText;
        public string fileSizeText;

        public bool isExpanded;
        public bool isRealised;
        public bool controlsCreated;
        public bool isLoadingImage;
        private double lastMeasuredHeight;

        public double EstimatedHeight => cardStyle switch
        {
            BrowseCardStyle.TrendingHero => 240,
            BrowseCardStyle.TrendingCompact => 165,
            _ => 250
        };

        public Panel? cardPanel;
        public ThumbnailBorder thumbnailBorder = null!;
        public Image thumbnailImage = null!;
        public CrescentButton downloadButton = null!;
        public Control? foldoutContainer;

        public BrowseModCard(PluginData pluginData, JObject modJson, BrowseTab browseTab, BrowseCardStyle cardStyle = BrowseCardStyle.Standard)
        {
            this.pluginData = pluginData;
            this.modJson = modJson;
            this.browseTab = browseTab;
            this.cardStyle = cardStyle;

            nexusId = modJson["mod_id"]?.ToString() ?? modJson["id"]?.ToString() ?? string.Empty;
            gameDomain = modJson["domain_name"]?.ToString() ?? pluginData.API.Name;
            modName = NexusUtils.TrimNexusFormatting(modJson["name"]?.ToString() ?? $"Mod #{nexusId}");
            authorName = modJson["author"]?.ToString() ?? modJson["uploaded_by"]?.ToString() ?? "Unknown";
            categoryName = modJson["category_name"]?.ToString() ?? "Mod";

            string rawVersion = NexusUtils.TryCleanVersion(modJson["version"]?.ToString() ?? string.Empty);
            versionText = string.IsNullOrEmpty(rawVersion) ? "-" : $"v{rawVersion.TrimStart('v', 'V')}";

            string rawSummary = NexusUtils.TrimNexusFormatting(modJson["summary"]?.ToString() ?? string.Empty);
            summaryText = string.IsNullOrWhiteSpace(rawSummary) ? "No description available." : rawSummary;
            pictureUrl = modJson["picture_url"]?.ToString() ?? string.Empty;

            relativeUpdateText = NexusUtils.FormatRelativeTime(modJson["updated_timestamp"] ?? modJson["latest_file_update"], modJson["updated_time"]);
            releaseDateText = NexusUtils.FormatNexusDate(modJson["created_timestamp"], modJson["created_time"]);

            long endorsements = modJson["endorsement_count"]?.ToObject<long>() ?? modJson["endorsements"]?.ToObject<long>() ?? 0;
            long downloads = modJson["mod_downloads"]?.ToObject<long>() ?? modJson["total_downloads"]?.ToObject<long>() ?? 0;
            long fileSize = modJson["file_size"]?.ToObject<long>() ?? 0;

            endorsementsText = NexusUtils.FormatCompactNumber(endorsements);
            downloadsText = NexusUtils.FormatCompactNumber(downloads);
            fileSizeText = fileSize > 0 ? NexusUtils.FormatFileSize(fileSize) : string.Empty;

            Background = cardStyle == BrowseCardStyle.Standard ? Colors.Overlay : Brushes.Transparent;
            CornerRadius = new CornerRadius(4);
            Margin = new Thickness(4);
            Padding = cardStyle == BrowseCardStyle.Standard ? new Thickness(10) : new Thickness(2);
            VerticalAlignment = VerticalAlignment.Top;
        }

        public bool IsInstalled()
        {
            for (int index = 0; index < pluginData.installedMods.Count; index++)
            {
                ModData mod = pluginData.installedMods[index];
                if (!string.IsNullOrEmpty(nexusId) && string.Equals(mod.url.nexusId, nexusId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void Realise()
        {
            if (!controlsCreated)
            {
                controlsCreated = true;
                cardPanel = cardStyle switch
                {
                    BrowseCardStyle.TrendingHero => BuildTrendingHeroLayout(),
                    BrowseCardStyle.TrendingCompact => BuildTrendingCompactLayout(),
                    _ => BuildStandardCardLayout()
                };
            }

            if (!isRealised || Child != cardPanel)
            {
                Height = double.NaN;
                Child = cardPanel;
                isRealised = true;
            }

            RefreshInstallState();

            string cacheKey = $"{gameDomain}:{nexusId}";
            if (thumbnailImage.Source == null && BrowseThumbnailCache.TryGetValue(cacheKey, out Bitmap? cachedBitmap))
                thumbnailImage.Source = cachedBitmap;
            else if (thumbnailImage.Source == null && !isLoadingImage && !string.IsNullOrEmpty(pictureUrl))
                LoadThumbnailAsync().FireAndForget();
        }

        public void Virtualise()
        {
            if (isRealised && Bounds.Height > 0 && !isExpanded)
                lastMeasuredHeight = Bounds.Height;

            isRealised = false;
            Child = null;
            Height = lastMeasuredHeight > 0 ? lastMeasuredHeight : EstimatedHeight;
        }

        private void CreateThumbnailControls()
        {
            thumbnailImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            RenderOptions.SetBitmapInterpolationMode(thumbnailImage, BitmapInterpolationMode.MediumQuality);

            thumbnailBorder = new ThumbnailBorder
            {
                UseAspectRatio = true,
                Background = Colors.Background,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = thumbnailImage
            };
        }

        private Panel BuildTrendingHeroLayout()
        {
            CreateThumbnailControls();

            Grid rootGrid = new();
            rootGrid.Children.Add(thumbnailBorder);

            Border gradientOverlay = new()
            {
                Background = HeroOverlayGradient,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14, 28, 14, 12),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            Grid bottomGrid = new() { ColumnSpacing = 10 };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            StackPanel textStack = new() { Spacing = 3 };

            TextBlock titleBlock = new()
            {
                Text = modName,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };
            ToolTip.SetTip(titleBlock, modName);
            textStack.Children.Add(titleBlock);

            TextBlock subBlock = new()
            {
                Text = $"by {authorName}  •  {endorsementsText} Endorsements",
                FontSize = 12,
                Foreground = Brushes.LightGray,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(subBlock);

            TextBlock summaryBlock = new()
            {
                Text = summaryText,
                FontSize = 12,
                Foreground = Brushes.LightGray,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(summaryBlock);
            Grid.SetColumn(textStack, 0);

            StackPanel actionsStack = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            actionsStack.Children.Add(BuildNexusLinkButton());
            downloadButton = BuildDownloadButton(95);
            actionsStack.Children.Add(downloadButton);
            Grid.SetColumn(actionsStack, 1);

            bottomGrid.Children.Add(textStack);
            bottomGrid.Children.Add(actionsStack);
            gradientOverlay.Child = bottomGrid;

            rootGrid.Children.Add(gradientOverlay);
            return rootGrid;
        }

        private Panel BuildTrendingCompactLayout()
        {
            CreateThumbnailControls();
            thumbnailBorder.Margin = new Thickness(0, 0, 0, 6);

            StackPanel stack = new() { Spacing = 2 };
            stack.Children.Add(thumbnailBorder);

            TextBlock titleBlock = new()
            {
                Text = modName,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ToolTip.SetTip(titleBlock, modName);
            stack.Children.Add(titleBlock);

            Grid metaRow = new();
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock authorBlock = new()
            {
                Text = $"by {authorName}  •  {endorsementsText}",
                FontSize = 12,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(authorBlock, 0);

            CrescentButton linkButton = BuildNexusLinkButton();
            Grid.SetColumn(linkButton, 1);

            downloadButton = BuildDownloadButton(80);
            downloadButton.Height = 26;
            downloadButton.FontSize = 12;
            Grid.SetColumn(downloadButton, 2);

            metaRow.Children.Add(authorBlock);
            metaRow.Children.Add(linkButton);
            metaRow.Children.Add(downloadButton);
            stack.Children.Add(metaRow);

            return stack;
        }

        private Panel BuildStandardCardLayout()
        {
            CreateThumbnailControls();
            thumbnailBorder.Margin = new Thickness(0, 0, 0, 8);

            StackPanel cardStack = new() { Spacing = 0 };
            cardStack.Children.Add(thumbnailBorder);

            TextBlock nameBlock = new()
            {
                Text = modName,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.NoWrap,
                MaxLines = 1,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0)
            };
            ToolTip.SetTip(nameBlock, modName);
            cardStack.Children.Add(nameBlock);
            Grid datesGrid = new()
            {
                Margin = new Thickness(0, 2, 0, 0)
            };
            
            datesGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            datesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            StackPanel updatedPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            Uri updatedUri = new("avares://Crescent.UI/Assets/update.png", UriKind.Absolute);
            Image updatedImage = new()
            {
                Source = new Bitmap(AssetLoader.Open(updatedUri)),
                Width = 14,
                Height = 14,
            };

            RenderOptions.SetBitmapInterpolationMode(updatedImage, BitmapInterpolationMode.HighQuality);
            TextBlock updatedBlock = new()
            {
                Text = relativeUpdateText,
                Foreground = Brushes.Gray,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            
            updatedPanel.Children.Add(updatedImage);
            updatedPanel.Children.Add(updatedBlock);
            Grid.SetColumn(updatedPanel, 0);
            
            StackPanel releasedPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };
            
            Uri releasedUri = new("avares://Crescent.UI/Assets/publish.png", UriKind.Absolute);
            Image releasedImage = new()
            {
                Source = new Bitmap(AssetLoader.Open(releasedUri)),
                Width = 14,
                Height = 14,
            };

            RenderOptions.SetBitmapInterpolationMode(releasedImage, BitmapInterpolationMode.HighQuality);
            TextBlock releasedBlock = new()
            {
                Text = releaseDateText,
                Foreground = Brushes.Gray,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            releasedPanel.Children.Add(releasedImage);
            releasedPanel.Children.Add(releasedBlock);
            Grid.SetColumn(releasedPanel, 1);

            datesGrid.Children.Add(updatedPanel);
            datesGrid.Children.Add(releasedPanel);
            cardStack.Children.Add(datesGrid);

            cardStack.Children.Add(ModLayouts.CreateLine(new Thickness(0, 6, 0, 4)));

            Grid metaRow = new() { Margin = new Thickness(0, 0, 0, 6) };
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            metaRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            TextBlock subMetaBlock = new()
            {
                Text = $"{versionText}  •  {authorName}  •  {categoryName}",
                FontSize = 12,
                Foreground = Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(subMetaBlock, 0);

            CrescentButton foldoutButton = BuildFoldoutButton();
            foldoutButton.Margin = new Thickness(0, 0, 2, 0);
            Grid.SetColumn(foldoutButton, 1);

            CrescentButton nexusButton = BuildNexusLinkButton();
            Grid.SetColumn(nexusButton, 2);

            metaRow.Children.Add(subMetaBlock);
            metaRow.Children.Add(foldoutButton);
            metaRow.Children.Add(nexusButton);
            cardStack.Children.Add(metaRow);

            StackPanel detailsStack = new()
            {
                Spacing = 5,
                Margin = new Thickness(0, 0, 0, 8),
                IsVisible = isExpanded
            };

            detailsStack.Children.Add(new TextBlock
            {
                Text = summaryText,
                Foreground = Brushes.LightGray,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });

            TextBlock statsBlock = new()
            {
                Text = $"{endorsementsText} Endorsements    •    {downloadsText} Downloads" + (!string.IsNullOrEmpty(fileSizeText) ? $"    •    {fileSizeText}" : string.Empty),
                Foreground = Brushes.Gray,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center
            };
            
            detailsStack.Children.Add(statsBlock);
            detailsStack.Children.Add(ModLayouts.CreateLine(new Thickness(0, 2, 0, 0)));

            foldoutContainer = detailsStack;
            cardStack.Children.Add(detailsStack);

            Grid actionGrid = new();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            downloadButton = BuildDownloadButton(double.NaN);
            downloadButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(downloadButton, 0);

            actionGrid.Children.Add(downloadButton);
            cardStack.Children.Add(actionGrid);
            return cardStack;
        }

        private CrescentButton BuildDownloadButton(double width)
        {
            CrescentButton button = new(ButtonStyle.Translucent)
            {
                BorderBrush = Colors.Accent,
                Foreground = Colors.Accent,
                Content = "Download",
                Width = width
            };

            button.onClick += async () =>
            {
                Window? window = TopLevel.GetTopLevel(browseTab) as Window;
                if (window == null || string.IsNullOrEmpty(nexusId)) return;

                string modUrl = $"https://www.nexusmods.com/{gameDomain}/mods/{nexusId}";
                ModDownloadDialog dialog = new(pluginData, modUrl, modName);
                await dialog.ShowDialog(window);
                RefreshInstallState();
            };

            return button;
        }

        private CrescentButton BuildFoldoutButton()
        {
            CrescentButton expandButton = new(ButtonStyle.Transparent)
            {
                Content = isExpanded ? "▴" : "▾",
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            ToolTip.SetTip(expandButton, "Toggle description");
            expandButton.onClick += () =>
            {
                isExpanded = !isExpanded;
                expandButton.Content = isExpanded ? "▴" : "▾";
                if (foldoutContainer != null) foldoutContainer.IsVisible = isExpanded;
                browseTab.QueueVirtualisation();
            };

            return expandButton;
        }

        private CrescentButton BuildNexusLinkButton()
        {
            CrescentButton linkButton = new(ButtonStyle.Transparent)
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Uri iconUri = new("avares://Crescent.UI/Assets/link.png", UriKind.Absolute);
            Image iconImage = new()
            {
                Source = new Bitmap(AssetLoader.Open(iconUri))
            };

            RenderOptions.SetBitmapInterpolationMode(iconImage, BitmapInterpolationMode.HighQuality);
            linkButton.Content = iconImage;

            ToolTip.SetTip(linkButton, "Open on Nexus Mods");
            linkButton.onClick += () =>
            {
                if (string.IsNullOrEmpty(nexusId)) return;
                $"https://www.nexusmods.com/{gameDomain}/mods/{nexusId}".TryOpen();
            };

            return linkButton;
        }

        public void RefreshInstallState()
        {
            if (!controlsCreated) return;

            bool installed = IsInstalled();
            bool queued = ModDownloadQueue.IsModQueued(nexusId);

            if (queued)
            {
                downloadButton.Content = "Queued";
                downloadButton.IsEnabled = false;
                downloadButton.Foreground = Brushes.Gray;
                downloadButton.BorderBrush = Colors.Separator;
            }
            else if (installed)
            {
                downloadButton.Content = "Installed";
                downloadButton.IsEnabled = true;
                downloadButton.Foreground = Brushes.LightGray;
                downloadButton.BorderBrush = Colors.Separator;
            }
            else
            {
                downloadButton.Content = "Download";
                downloadButton.IsEnabled = true;
                downloadButton.Foreground = Colors.Accent;
                downloadButton.BorderBrush = Colors.Accent;
            }
        }

        private async Task LoadThumbnailAsync()
        {
            if (isLoadingImage || string.IsNullOrEmpty(pictureUrl) || string.IsNullOrEmpty(nexusId)) return;
            isLoadingImage = true;

            try
            {
                string cacheKey = $"{gameDomain}:{nexusId}";
                if (BrowseThumbnailCache.TryGetValue(cacheKey, out Bitmap? cachedBitmap))
                {
                    Dispatcher.UIThread.Post(() => thumbnailImage.Source = cachedBitmap);
                    return;
                }

                string? path = await NexusUtils.GetCachedThumbnailPathAsync(gameDomain, nexusId, pictureUrl);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                Bitmap bitmap = await Task.Run(() =>
                {
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Bitmap.DecodeToWidth(stream, 480, BitmapInterpolationMode.HighQuality);
                });

                BrowseThumbnailCache[cacheKey] = bitmap;
                Dispatcher.UIThread.Post(() => thumbnailImage.Source = bitmap);
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
            finally
            {
                isLoadingImage = false;
            }
        }
    }
}