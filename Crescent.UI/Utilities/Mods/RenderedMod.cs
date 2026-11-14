using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public class RenderedMod : Border
    {
        public PluginData pluginData;
        public ModData modData;
        public ModsTab modsTab;
        public string cleanName;

        public Grid rowGrid;
        public ToggleSwitch modToggle;
        public TextBlock nameBlock;
        public Control updateControl;
        public TextBlock versionBlock;
        public Button manualButton;
        public Button menuButton;

        public string Version
        {
            get => modData.version;
            set
            {
                modData.version = value;
                versionBlock.Text = string.IsNullOrEmpty(value) ? "-" : $"v{value}";
            }
        }

        public bool IsChecked
        {
            get => modToggle.IsChecked ?? false;
            set => modToggle.IsChecked = value;
        }

        public bool IsManual
        {
            get => modData.isManual;
            set
            {
                modData.isManual = value;
                if (manualButton is CrescentButton crescentButton) crescentButton.customColor = modData.isManual ? Colors.Red : Colors.Green;
                manualButton.Content = modData.isManual ? PluginData.Manual : PluginData.Managed;
            }
        }

        public RenderedMod(PluginData pluginData, ModData modData, ModsTab modsTab)
        {
            this.pluginData = pluginData;
            this.modData = modData;
            this.modsTab = modsTab;
            cleanName = modData.DisplayName.Cleaned();

            Background = Colors.Overlay;
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(12, 8);

            rowGrid = new();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55, GridUnitType.Pixel)));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(95, GridUnitType.Pixel)));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(90, GridUnitType.Pixel)));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            modToggle = GetModToggle();
            Grid.SetColumn(modToggle, 0);

            nameBlock = GetNameTextBlock();
            Grid.SetColumn(nameBlock, 1);

            updateControl = GetUpdateControl();
            Grid.SetColumn(updateControl, 2);

            versionBlock = GetVersionTextBlock();
            Grid.SetColumn(versionBlock, 3);

            manualButton = GetManualButton();
            Grid.SetColumn(manualButton, 4);

            menuButton = GetMenuButton();
            Grid.SetColumn(menuButton, 5);

            rowGrid.Children.Add(modToggle);
            rowGrid.Children.Add(nameBlock);
            rowGrid.Children.Add(updateControl);
            rowGrid.Children.Add(versionBlock);
            rowGrid.Children.Add(manualButton);
            rowGrid.Children.Add(menuButton);

            Child = rowGrid;
        }

        public void UpdateControl()
        {
            if (rowGrid.Children.Contains(updateControl)) rowGrid.Children.Remove(updateControl);
            updateControl = GetUpdateControl();
            Grid.SetColumn(updateControl, 2);
            rowGrid.Children.Add(updateControl);
        }

        public void RefreshDeployedState()
        {
            bool isEnabled = pluginData.SelectedProfile.IsModEnabled(modData);
            if (modToggle.IsChecked != isEnabled) modToggle.IsChecked = isEnabled;
            nameBlock.Foreground = isEnabled ? Brushes.White : Brushes.Gray;
            IsManual = modData.isManual;
        }

        public void RefreshMod()
        {
            cleanName = modData.DisplayName.Cleaned();
            Version = modData.version;
            nameBlock.Text = cleanName;
            RefreshDeployedState();
            UpdateControl();
        }

        #region Controls & Updating

        public ToggleSwitch GetModToggle()
        {
            ToggleSwitch toggleSwitch = new()
            {
                IsChecked = pluginData.SelectedProfile.IsModEnabled(modData),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
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
                        await Task.Run(() => ModInteractionUtils.SyncModDeploymentState(pluginData, modData));
                        modsTab.RefreshDeployedStates();
                        ToastManager.CompleteProgress("Mod Deployed", $"Deployed {cleanName}", ToastType.Success, 3000, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
                    }
                    else
                    {
                        await Task.Run(() => ModInteractionUtils.WithdrawModFiles(pluginData, modData));
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
            return new()
            {
                Text = cleanName,
                Foreground = isEnabled ? Brushes.White : Brushes.Gray,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 10, 0)
            };
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
            return new Control();
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
                Margin = new Thickness(5, 0, 5, 0)
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

                    await ModInteractionUtils.UpdateAsync(pluginData, modData);
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
            CrescentButton manualButton = new(modData.isManual ? Colors.Red : Colors.Green)
            {
                Content = modData.isManual ? PluginData.Manual : PluginData.Managed,
                Width = 115,
                Margin = new Thickness(10, 0, 0, 0)
            };

            manualButton.onClick += async () =>
            {
                try
                {
                    if (!manualButton.IsEnabled) return;
                    manualButton.IsEnabled = false;

                    await ModInteractionUtils.ToggleManualInstallAsync(pluginData, modData);
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
                }
            };

            return manualButton;
        }

        public Button GetMenuButton()
        {
            CrescentButton menuButton = new(ButtonStyle.Translucent)
            {
                Content = "⋮",
                Width = 35,
                Margin = new Thickness(8, 0, 0, 0)
            };

            menuButton.onClick += () =>
            {
                ContextMenu contextMenu = ModContextMenu.Get(pluginData, modData, modsTab);
                menuButton.ContextMenu = contextMenu;
                contextMenu.Open(menuButton);
            };

            return menuButton;
        }

        #endregion
    }
}