using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Crescent.UI.ContextMenus;
using Crescent.UI.Controls.Toasts;
using Crescent.UI.Dialogs;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs.Elements
{
    public partial class RenderedMod
    {
        public Image GetThumbnailImage()
        {
            Image image = new()
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.MediumQuality);
            return image;
        }

        public ThumbnailBorder GetThumbnailBorder()
        {
            return new()
            {
                Background = Colors.Background,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = thumbnailImage
            };
        }

        public ToggleSwitch GetModToggle()
        {
            ToggleSwitch toggleSwitch = new()
            {
                IsChecked = pluginData.SelectedProfile.IsModEnabled(modData),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 0,
                Padding = new Thickness(0),
                OffContent = string.Empty,
                OnContent = string.Empty
            };

            toggleSwitch.IsCheckedChanged += async (_, _) =>
            {
                try
                {
                    bool isEnabled = toggleSwitch.IsChecked ?? false;
                    if (pluginData.SelectedProfile.IsModEnabled(modData) == isEnabled) return;

                    toggleSwitch.IsEnabled = false;
                    pluginData.SelectedProfile.SetModEnabled(modData, isEnabled);
                    nameBlock.Foreground = isEnabled ? Brushes.White : Brushes.Gray;

                    if (isEnabled)
                    {
                        ToastManager.ShowProgress("Deploying Mod", $"Deploying {cleanName}...", pluginData.Name, 0, true);
                        await pluginData.Deploy();
                        await Task.Run(() => ModsIO.SyncModDeploymentState(pluginData, modData));
                        modsTab.RefreshDeployedStates();
                        ToastManager.CompleteProgress("Mod Deployed", $"Deployed {cleanName}", ToastType.Success, 3000, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
                    }
                    else
                    {
                        await Task.Run(() => ModsIO.WithdrawModFiles(pluginData, modData));
                        RefreshDeployedState();
                        ToastManager.Show("Mod Withdrawn", $"Withdrew {cleanName}", ToastType.Info, 3000, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
                    }

                    await pluginData.SaveAsync();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
                finally
                {
                    toggleSwitch.IsEnabled = true;
                }
            };

            return toggleSwitch;
        }

        public TextBlock GetNameTextBlock()
        {
            bool isEnabled = pluginData.SelectedProfile.IsModEnabled(modData);
            TextBlock block = new()
            {
                Text = cleanName,
                Foreground = isEnabled ? Brushes.White : Brushes.Gray,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 8, 0)
            };

            ToolTip.SetTip(block, cleanName);
            return block;
        }

        public TextBlock GetDescriptionTextBlock()
        {
            return new()
            {
                Text = descriptionText,
                Foreground = Brushes.LightGray,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        public TextBlock GetReleaseDateTextBlock()
        {
            TextBlock block = new()
            {
                Foreground = Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            SetDateBlockContent(block, releaseDateText, "publish.png");
            return block;
        }

        public TextBlock GetUpdatedDateTextBlock()
        {
            TextBlock block = new()
            {
                Foreground = Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            SetDateBlockContent(block, updatedDateText, "update.png");
            return block;
        }

        public void SetReleaseDateValue(string rawValue) => SetDateBlockContent(releaseDateBlock, rawValue, "publish.png");

        public void SetUpdatedDateValue(string rawValue) => SetDateBlockContent(updatedDateBlock, rawValue, "update.png");

        private static void SetDateBlockContent(TextBlock block, string rawValue, string assetFileName)
        {
            string cleanValue = StripDatePrefix(rawValue);
            block.Text = null;
            block.Inlines?.Clear();
            block.Inlines ??= new InlineCollection();

            Uri iconUri = new($"avares://Crescent.UI/Assets/{assetFileName}", UriKind.Absolute);
            if (AssetLoader.Exists(iconUri))
            {
                Image iconImage = new()
                {
                    Source = new Bitmap(AssetLoader.Open(iconUri)),
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                RenderOptions.SetBitmapInterpolationMode(iconImage, BitmapInterpolationMode.HighQuality);
                block.Inlines.Add(new InlineUIContainer
                {
                    Child = iconImage,
                    BaselineAlignment = BaselineAlignment.Center
                });
            }

            block.Inlines.Add(new Run(cleanValue));
        }

        private static string StripDatePrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            int colonIndex = value.IndexOf(':');
            string cleaned = colonIndex >= 0 && colonIndex + 1 < value.Length ? value[(colonIndex + 1)..].Trim() : value.Trim();
            return string.IsNullOrEmpty(cleaned) ? "-" : cleaned;
        }

        public Control GetUpdateControl()
        {
            if (string.IsNullOrEmpty(modData.url.nexusId))
                return GetDisconnectedBlock();

            if (!modData.HasCheckedForUpdate)
                return GetCheckButton();

            if (!modData.HasValidNexusData)
                return GetInvalidBlock();

            if (modData.UpdateAvailable) return GetUpdateButton();
            return new Control { IsVisible = false, Width = 0, Height = 0 };
        }

        public TextBlock GetVersionTextBlock()
        {
            return new()
            {
                Text = string.IsNullOrEmpty(modData.version) ? "-" : $"v{modData.version}",
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 4, 0)
            };
        }

        public TextBlock GetDisconnectedBlock()
        {
            return new()
            {
                Text = "Disconnected",
                Width = 85,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = Colors.Red
            };
        }

        public TextBlock GetInvalidBlock()
        {
            return new()
            {
                Text = "Invalid Data",
                Width = 85,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = Colors.Red
            };
        }

        public CrescentButton GetCheckButton()
        {
            CrescentButton checkButton = new(ButtonStyle.Translucent)
            {
                Content = "Check",
                Width = 85,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            checkButton.onClick += async () =>
            {
                try
                {
                    checkButton.IsEnabled = false;
                    await modData.CheckForUpdateAsync(pluginData.API.Name);
                    RefreshMod();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
                finally
                {
                    checkButton.IsEnabled = true;
                }
            };

            return checkButton;
        }

        public CrescentButton GetUpdateButton()
        {
            string cleanLatestVersion = (modData.LatestVersion ?? string.Empty).Trim().TrimStart('v', 'V');
            CrescentButton updateButton = new(ButtonStyle.Translucent)
            {
                Content = "Update",
                Width = 85,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            if (!string.IsNullOrEmpty(cleanLatestVersion)) ToolTip.SetTip(updateButton, $"Update to v{cleanLatestVersion}");
            updateButton.onClick += async () =>
            {
                try
                {
                    if (!Settings.Instance.skipUpdatePopup)
                    {
                        Window? window = TopLevel.GetTopLevel(modsTab) as Window;
                        if (window != null)
                        {
                            ModUpdateDialog updateDialog = new(modData);
                            await updateDialog.ShowDialog(window);
                            if (!updateDialog.UpdateConfirmed) return;
                        }
                    }

                    updateButton.IsEnabled = false;
                    updateButton.Content = "...";

                    await ModsIO.UpdateModAsync(pluginData, modData);
                    Dispatcher.UIThread.Post(() => modsTab.Refresh());
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
                finally
                {
                    updateButton.IsEnabled = true;
                    updateButton.Content = "Update";
                }
            };

            return updateButton;
        }

        public Button GetManualButton()
        {
            CrescentButton manualButton = new(ButtonStyle.Translucent)
            {
                BorderBrush = modData.isManual ? Colors.Red : Colors.Accent,
                Foreground = modData.isManual ? Colors.Red : Colors.Accent,
                Content = modData.isManual ? "Manual" : "Managed",
                Width = 115,
                Margin = new Thickness(10, 0, 0, 0)
            };

            manualButton.onClick += async () =>
            {
                try
                {
                    if (!manualButton.IsEnabled) return;
                    manualButton.IsEnabled = false;

                    await ModsIO.ToggleManualInstallAsync(pluginData, modData);
                    RefreshDeployedState();
                    Dispatcher.UIThread.Post(() => modsTab.RenderMods());
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
                finally
                {
                    manualButton.IsEnabled = true;
                    manualButton.Foreground = modData.isManual ? Colors.Red : Colors.Accent;
                    manualButton.BorderBrush = modData.isManual ? Colors.Red : Colors.Accent;
                }
            };

            return manualButton;
        }

        public Button GetNexusButton()
        {
            CrescentButton linkButton = new(ButtonStyle.Transparent)
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsVisible = !string.IsNullOrEmpty(modData.url.nexusId)
            };

            Uri iconUri = new("avares://Crescent.UI/Assets/link.png", UriKind.Absolute);
            if (AssetLoader.Exists(iconUri))
            {
                Image iconImage = new()
                {
                    Source = new Bitmap(AssetLoader.Open(iconUri))
                };

                RenderOptions.SetBitmapInterpolationMode(iconImage, BitmapInterpolationMode.HighQuality);
                linkButton.Content = iconImage;
            }

            ToolTip.SetTip(linkButton, "Open on Nexus Mods");
            linkButton.onClick += () =>
            {
                if (string.IsNullOrEmpty(modData.url.nexusId) || string.IsNullOrEmpty(pluginData.API.Name)) return;
                $"https://www.nexusmods.com/{pluginData.API.Name}/mods/{modData.url.nexusId}".TryOpen();
            };

            return linkButton;
        }

        public Button GetFoldoutButton()
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
                modsTab.QueueVirtualisation();
            };

            return expandButton;
        }

        public Button GetMenuButton()
        {
            CrescentButton menuButton = new(ButtonStyle.Translucent)
            {
                Content = "⋮",
                Width = 32,
                Margin = new Thickness(6, 0, 0, 0)
            };

            menuButton.onClick += () =>
            {
                ContextMenu contextMenu = ModContextMenu.Get(pluginData, modData, modsTab);
                menuButton.ContextMenu = contextMenu;
                contextMenu.Open(menuButton);
            };

            return menuButton;
        }
    }
}