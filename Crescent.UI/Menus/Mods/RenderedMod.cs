using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crescent.Plugins;
using Crescent.Utilities;

namespace Crescent.UI
{
    public class RenderedMod : Grid
    {
        public GameData gameData;
        public ModData modData;
        public ModsTab modsTab;
    
        public TextBlock versionBlock;
        public CheckBox modCheckBox;
        public Control updateControl;
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
            get => modCheckBox.IsChecked ?? false;
            set => modCheckBox.IsChecked = value;
        }

        public bool IsManual
        {
            get => modData.isManual;
            set
            {
                modData.isManual = value;
                if (manualButton is CrescentButton crescentButton) crescentButton.customColor = modData.isManual ? Colors.Red : Colors.Green;
                manualButton.Content = modData.isManual ? GameData.Manual : GameData.Managed;
            }
        }

        public RenderedMod(GameData gameData, ModData modData, ModsTab modsTab)
        {
            this.gameData = gameData;
            this.modData = modData;
            this.modsTab = modsTab;

            ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ColumnDefinitions.Add(new ColumnDefinition(new GridLength(90, GridUnitType.Pixel)));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            modCheckBox = GetModCheckBox();
            SetColumn(modCheckBox, 0);
            
            updateControl = GetUpdateControl();
            SetColumn(updateControl, 1);

            versionBlock = GetVersionTextBlock();
            SetColumn(versionBlock, 2);

            manualButton = GetManualButton();
            SetColumn(manualButton, 3);

            menuButton = GetMenuButton();
            SetColumn(menuButton, 4);

            Children.Add(modCheckBox);
            Children.Add(versionBlock);
            Children.Add(updateControl);
            Children.Add(manualButton);
            Children.Add(menuButton);
        }
        
        public void UpdateControl()
        {
            if (Children.Contains(updateControl)) Children.Remove(updateControl);
            updateControl = GetUpdateControl();
            SetColumn(updateControl, 1);
            Children.Add(updateControl);
        }

        public void RefreshMod()
        {
            Version = modData.version;
            UpdateControl();
        }

        #region Buttons & Updating

        public CheckBox GetModCheckBox()
        {
            CheckBox checkBox = new()
            {
                Content = modData.DisplayName.WithoutNumbers(),
                IsChecked = gameData.SelectedProfile.IsModEnabled(modData),
                VerticalAlignment = VerticalAlignment.Center
            };

            checkBox.IsCheckedChanged += async (_, _) =>
            {
                bool isEnabled = checkBox.IsChecked ?? true;
                gameData.SelectedProfile.SetModEnabled(modData, isEnabled);
                
                if (isEnabled) gameData.TryDeploy();
                else gameData.WithdrawMod(modData);
                await gameData.SaveAsync();
            };

            return checkBox;
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
            TextBlock versionBlock = new()
            {
                Text = string.IsNullOrEmpty(modData.version) ? "-" : $"v{modData.version}",
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                Margin = new Thickness(5, 0, 5, 0)
            };
            
            return versionBlock;
        }

        public TextBlock GetDisconnectedBlock()
        {
            TextBlock disconnected = new()
            {
                Text = "Disconnected",
                Width = 85,
                Height = 35,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Colors.Red,
                Margin = new Thickness(10, 0, 0, 0)
            };
            return disconnected;
        }

        public TextBlock GetInvalidBlock()
        {
            TextBlock invalidData = new()
            {
                Text = "Invalid Data",
                Width = 85,
                Height = 35,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Colors.Red,
                Margin = new Thickness(10, 0, 0, 0)
            };
                
            return invalidData;
        }

        public CrescentButton GetCheckButton()
        {
            CrescentButton checkButton = new(ButtonStyle.Translucent)
            {
                Content = "Check",
                Width = 85,
                Margin = new Thickness(10, 0, 0, 0)
            };

            checkButton.onClick += async () =>
            {
                await modData.CheckForUpdateAsync(gameData.NexusName);
                RefreshMod();
            };
                
            return checkButton;
        }

        public CrescentButton GetUpdateButton()
        {
            CrescentButton updateButton = new(ButtonStyle.Translucent)
            {
                Content = "Update",
                Width = 85,
                Margin = new Thickness(10, 0, 0, 0)
            };

            updateButton.onClick += async () =>
            {
                try
                {
                    Window? window = TopLevel.GetTopLevel(modsTab) as Window;
                    if (window != null)
                    {
                        ModUpdateDialog updateDialog = new(modData);
                        await updateDialog.ShowDialog(window);
                        if (!updateDialog.UpdateConfirmed) return;
                    }

                    bool isUpdating = true;
                    Task animationTask = Task.Run(async () =>
                    {
                        int count = 0;
                        while (isUpdating)
                        {
                            count = (count % 3) + 1;
                            string dots = new('.', count);

                            await Dispatcher.UIThread.InvokeAsync(() => updateButton.Content = "Checking" + dots);
                            await Task.Delay(250);
                        }
                    });

                    try
                    {
                        await ModInteractionUtils.UpdateAsync(gameData, modData);
                    }
                    finally
                    {
                        isUpdating = false;
                        await animationTask;
                        updateButton.Content = "Updated";
                    }

                    RefreshMod();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
            };

            return updateButton;
        }

        public Button GetManualButton()
        {
            CrescentButton manualButton = new(modData.isManual ? Colors.Red : Colors.Green)
            {
                Content = modData.isManual ? GameData.Manual : GameData.Managed,
                Width = 115,
                Margin = new Thickness(10, 0, 0, 0)
            };

            manualButton.onClick += async () =>
            {
                await ModInteractionUtils.ToggleManualInstallAsync(gameData, modData);
                manualButton.customColor = modData.isManual ? Colors.Red : Colors.Green;
                modsTab.RenderMods();
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

            ContextMenu contextMenu = ModContextMenu.Get(gameData, modData, modsTab);
            menuButton.ContextMenu = contextMenu;
            menuButton.onClick += () => contextMenu.Open(menuButton);

            return menuButton;
        }

        #endregion
    }
}