using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Crescent.Plugins;
using Crescent.UI.Controls.Sidebar;
using Crescent.UI.Controls.Toasts;
using Crescent.UI.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public class SettingsTab : StackPanel, ISidebarTab
    {
        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;

        public int Order => 4;

        public CrescentButton GetButton()
        {
            return new CrescentButton(ButtonStyle.Translucent)
            {
                Content = "Game Settings",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };
        }

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            this.pluginData = pluginData;
            this.sidebarOverlay = sidebarOverlay;
            Setup();
        }

        public void OnClick()
        {
            Refresh();
        }

        public void Refresh()
        {
            Children.Clear();
            Setup();
        }

        public void Setup()
        {
            if (pluginData == null) return;

            Margin = new Thickness(30, 15, 30, 30);
            Spacing = 22;
            Background = Brushes.Transparent;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            Children.Add(new TextBlock
            {
                Text = "Settings",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });

            Children.Add(GetPathsSettings());
            Children.Add(GetThemeSettings());
        }

        #region Paths

        public Border GetPathsSettings()
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Directories & Executable", out Border cardBorder);

            sectionStack.Children.Add(GetExePathRow());
            sectionStack.Children.Add(GetStagingPathRow());
            sectionStack.Children.Add(GetDeploymentPathRow());

            return cardBorder;
        }

        public StackPanel GetExePathRow()
        {
            return SettingsUtils.GetPathRow(this, pluginData!, "Game Executable Path", pluginData!.ExePath, nameof(pluginData.ExePath), async path =>
            {
                if (pluginData.ExePath == path) return;
                pluginData.ExePath = path;
                await pluginData.SaveAsync();
                ToastManager.Show("Path Saved", "Updated game executable path", ToastType.Success, 2500, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
            }, true);
        }

        public StackPanel GetStagingPathRow()
        {
            return SettingsUtils.GetPathRow(this, pluginData!, "Mod Staging Folder", pluginData!.StagingPath, nameof(pluginData.StagingPath), async path =>
            {
                if (pluginData.StagingPath == path) return;
                pluginData.StagingPath = path;
                await pluginData.SaveAsync();
                ToastManager.Show("Path Saved", "Updated mod staging folder", ToastType.Success, 2500, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
            }, false);
        }

        public StackPanel GetDeploymentPathRow()
        {
            return SettingsUtils.GetPathRow(this, pluginData!, "Target Folder", pluginData!.DeploymentPath, nameof(pluginData.DeploymentPath), async path =>
            {
                if (pluginData.DeploymentPath == path) return;
                pluginData.DeploymentPath = path;
                await pluginData.SaveAsync();
                ToastManager.Show("Path Saved", "Updated target deployment folder", ToastType.Success, 2500, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
            }, false);
        }

        #endregion

        #region Themes

        public Border GetThemeSettings()
        {
            StackPanel sectionStack = SettingsUtils.GetSectionContainer("Appearance & Artwork", out Border cardBorder);

            sectionStack.Children.Add(GetThemeAssetRow("Game Poster", t => t.GetPosterName(), (t, val) => t.posterName = val, 2, 3));

            sectionStack.Children.Add(GetThemeAssetRow("Background Artwork", t => t.GetBackgroundName(), (t, val) => t.backgroundName = val, 16, 9));

            return cardBorder;
        }

        public Grid GetThemeAssetRow(string title, Func<Theme, string> assetSelector, Action<Theme, string> assetSetter, double widthRatio, double heightRatio)
        {
            Grid rowGrid = new()
            {
                ColumnSpacing = 16,
                Margin = new Thickness(0, 6)
            };

            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Control previewSlot = GetThemePreviewSlot(assetSelector, widthRatio, heightRatio);
            Grid.SetColumn(previewSlot, 0);
            rowGrid.Children.Add(previewSlot);

            StackPanel infoStack = new()
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            infoStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            });

            infoStack.Children.Add(new TextBlock
            {
                Text = $"Recommended aspect ratio: {widthRatio}:{heightRatio}",
                FontSize = 12,
                Foreground = Brushes.Gray
            });

            Grid.SetColumn(infoStack, 1);
            rowGrid.Children.Add(infoStack);

            StackPanel buttonStack = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            CrescentButton browseButton = new(ButtonStyle.Translucent)
            {
                Content = "Browse",
                Height = 35,
                Padding = new Thickness(14, 0)
            };

            browseButton.onClick += async () => await SelectThemeAssetAsync(title, assetSetter);

            CrescentButton resetButton = new(ButtonStyle.Translucent)
            {
                Content = "Reset",
                Height = 35,
                Padding = new Thickness(14, 0)
            };

            resetButton.onClick += async () => await ResetThemeAssetAsync(title, assetSelector, assetSetter);

            buttonStack.Children.Add(browseButton);
            buttonStack.Children.Add(resetButton);

            Grid.SetColumn(buttonStack, 2);
            rowGrid.Children.Add(buttonStack);

            return rowGrid;
        }

        private async Task SelectThemeAssetAsync(string title, Action<Theme, string> assetSetter)
        {
            if (pluginData == null || string.IsNullOrEmpty(pluginData.StagingPath))
            {
                ToastManager.Show("Staging Path Required", "Set a Mod Staging Folder before applying custom artwork", ToastType.Warning);
                return;
            }

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Select {title}",
                AllowMultiple = false,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files.Count == 0) return;

            string sourcePath = files[0].Path.LocalPath;
            string fileName = Path.GetFileName(sourcePath);

            string themesDirectory = Path.Combine(pluginData.StagingPath, ".themes");
            if (!Directory.Exists(themesDirectory)) Directory.CreateDirectory(themesDirectory);

            string destinationPath = Path.Combine(themesDirectory, fileName);
            if (!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(sourcePath, destinationPath, true);

            assetSetter(pluginData.theme, fileName);
            await pluginData.SaveAsync();
            pluginData.theme.NotifyAssetsUpdated();
            ToastManager.Show("Artwork Updated", $"Updated {title.ToLower()}", ToastType.Success, 2500, pluginData.Name);
        }

        private async Task ResetThemeAssetAsync(string title, Func<Theme, string> assetSelector, Action<Theme, string> assetSetter)
        {
            if (pluginData == null || string.IsNullOrEmpty(pluginData.StagingPath)) return;

            string currentAssetName = assetSelector(pluginData.theme);
            string customPath = Path.Combine(pluginData.StagingPath, ".themes", currentAssetName);
            if (File.Exists(customPath)) File.Delete(customPath);

            assetSetter(pluginData.theme, string.Empty);
            await pluginData.SaveAsync();
            pluginData.theme.NotifyAssetsUpdated();
            ToastManager.Show("Artwork Reset", $"Restored default {title.ToLower()}", ToastType.Info, 2500, pluginData.Name);
        }

        private Control GetThemePreviewSlot(Func<Theme, string> assetSelector, double widthRatio, double heightRatio, double previewHeight = 72)
        {
            double previewWidth = Math.Round(previewHeight * (widthRatio / heightRatio));

            ThemeImage previewImage = new(pluginData!, assetSelector);
            RenderOptions.SetBitmapInterpolationMode(previewImage, BitmapInterpolationMode.HighQuality);

            Border previewBorder = new()
            {
                Width = previewWidth,
                Height = previewHeight,
                CornerRadius = new CornerRadius(4),
                BorderBrush = Colors.Hover,
                BorderThickness = new Thickness(1),
                Background = Colors.Background,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = previewImage
            };

            return new Border
            {
                Width = 140,
                Height = previewHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = previewBorder
            };
        }

        #endregion
    }
}