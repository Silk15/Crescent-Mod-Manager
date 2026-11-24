using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Crescent.Plugins;
using Crescent.UI.Controls.Toasts;
using Crescent.UI.Dialogs;
using Crescent.UI.Dialogs.Responses;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public class ModsToolbar : Grid
    {
        public static readonly List<string> SortOptions = new()
        {
            "A - Z",
            "Z - A",
            "Managed First",
            "Manual First",
            "Enabled First",
            "Deployed First",
            "Update Available"
        };

        public ModsTab modsTab;

        public CrescentTextBox SearchBox { get; private set; } = null!;
        public CrescentComboBox FilterComboBox { get; private set; } = null!;
        public CrescentComboBox SortComboBox { get; private set; } = null!;

        private CancellationTokenSource? searchCancellationToken;

        public string SearchText => SearchBox.Text != null ? SearchBox.Text.ToLower() : string.Empty;

        public ModsToolbar(ModsTab modsTab)
        {
            this.modsTab = modsTab;
            ColumnSpacing = 12;

            ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid filterControlGrid = GetFilterGrid();
            Children.Add(filterControlGrid);
            SetColumn(filterControlGrid, 0);

            StackPanel actionButtonPanel = GetActionButtons();
            Children.Add(actionButtonPanel);
            SetColumn(actionButtonPanel, 1);

            StackPanel modDisplayPanel = GetModDisplayType();
            Children.Add(modDisplayPanel);
            SetColumn(modDisplayPanel, 2);
            BindEvents();
        }

        private void BindEvents()
        {
            FilterComboBox.SelectionChanged += (_, _) =>
            {
                modsTab.selectedFilterOption = FilterComboBox.SelectedItem as string ?? "All";
                modsTab.OnCriteriaChanged();
            };

            SortComboBox.SelectionChanged += (_, _) =>
            {
                modsTab.selectedSortOption = SortComboBox.SelectedItem as string ?? "A - Z";
                modsTab.OnCriteriaChanged();
            };

            SearchBox.TextChanged += async (_, _) =>
            {
                searchCancellationToken?.Cancel();
                searchCancellationToken = new();
                CancellationToken token = searchCancellationToken.Token;
                try
                {
                    await Task.Delay(120, token);
                    if (!token.IsCancellationRequested)
                        modsTab.OnCriteriaChanged();
                }
                catch (OperationCanceledException)
                {
                }
            };
        }

        private Grid GetFilterGrid()
        {
            Grid searchGrid = new()
            {
                ColumnSpacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(2.2, GridUnitType.Star) { MinWidth = 110, MaxWidth = 260 });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(1.1, GridUnitType.Star) { MinWidth = 80, MaxWidth = 140 });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition(1.3, GridUnitType.Star) { MinWidth = 95, MaxWidth = 160 });

            SearchBox = new()
            {
                PlaceholderText = "Search installed mods...",
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            SetColumn(SearchBox, 0);
            searchGrid.Children.Add(SearchBox);

            FilterComboBox = new()
            {
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            List<string> filters = modsTab.pluginData!.GetModFilters();
            FilterComboBox.ItemsSource = filters;
            int filterIndex = filters.IndexOf(modsTab.selectedFilterOption);
            FilterComboBox.SelectedIndex = filterIndex >= 0 ? filterIndex : 0;
            SetColumn(FilterComboBox, 1);
            searchGrid.Children.Add(FilterComboBox);

            SortComboBox = new()
            {
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            SortComboBox.ItemsSource = SortOptions;
            int sortIndex = SortOptions.IndexOf(modsTab.selectedSortOption);
            SortComboBox.SelectedIndex = sortIndex >= 0 ? sortIndex : 0;
            SetColumn(SortComboBox, 2);
            searchGrid.Children.Add(SortComboBox);

            return searchGrid;
        }

        private StackPanel GetActionButtons()
        {
            StackPanel actionButtonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            actionButtonPanel.Children.Add(GetStagingButton());
            actionButtonPanel.Children.Add(GetTargetButton());
            actionButtonPanel.Children.Add(new Separator
            {
                Width = 1,
                Height = 35,
                Background = Colors.Separator,
                Padding = new(3, 0, 3, 0)
            });

            actionButtonPanel.Children.Add(GetCheckButton());
            actionButtonPanel.Children.Add(GetDownloadButton());
            return actionButtonPanel;
        }

        private StackPanel GetModDisplayType()
        {
            StackPanel actionButtonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            actionButtonPanel.Children.Add(new Separator
            {
                Width = 1,
                Height = 35,
                Background = Colors.Separator,
                Padding = new(3, 0, 3, 0)
            });

            actionButtonPanel.Children.Add(GetDisplayButton());
            return actionButtonPanel;
        }

        private CrescentButton GetStagingButton()
        {
            CrescentButton openStagingButton = new(ButtonStyle.Translucent)
            {
                Content = "Staging Folder",
                Padding = new(12, 0),
                Height = 35
            };

            openStagingButton.onClick += () => modsTab.pluginData?.StagingPath.TryOpen();
            return openStagingButton;
        }

        private CrescentButton GetTargetButton()
        {
            CrescentButton openDeploymentButton = new(ButtonStyle.Translucent)
            {
                Content = "Target Folder",
                Padding = new(12, 0),
                Height = 35
            };

            openDeploymentButton.onClick += () => modsTab.pluginData?.DeploymentPath.TryOpen();
            return openDeploymentButton;
        }

        private CrescentButton GetCheckButton()
        {
            CrescentButton checkUpdatesButton = new(ButtonStyle.Translucent)
            {
                Content = "Check Updates",
                Padding = new(12, 0),
                Height = 35
            };

            checkUpdatesButton.onClick += async () =>
            {
                try
                {
                    checkUpdatesButton.IsEnabled = false;
                    await ModsIO.CheckAllUpdatesManualAsync(modsTab.pluginData, modsTab.RefreshMod, modsTab.RenderMods);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                    ToastManager.CompleteProgress("Update Check Failed", "An error occurred while checking for updates :(", ToastType.Error);
                }
                finally
                {
                    checkUpdatesButton.IsEnabled = true;
                    checkUpdatesButton.Content = "Check Updates";
                }
            };

            return checkUpdatesButton;
        }

        private CrescentButton GetDownloadButton()
        {
            CrescentButton downloadLinkButton = new(ButtonStyle.Translucent)
            {
                Content = "Download Mod",
                Padding = new(12, 0),
                Height = 35
            };

            downloadLinkButton.onClick += async () =>
            {
                Window? window = TopLevel.GetTopLevel(modsTab) as Window;
                if (window == null) return;

                ConfirmationDialog confirmationDialog = new("Download Mod", "Enter the Url");
                await confirmationDialog.ShowDialog(window);

                string url = confirmationDialog.InputText;
                if (string.IsNullOrEmpty(url)) return;

                ModDownloadDialog downloadDialog = new(modsTab.pluginData!, url);
                await downloadDialog.ShowDialog(window);

                if (downloadDialog.DownloadSuccess)
                    modsTab.Refresh();
            };

            return downloadLinkButton;
        }

        private CrescentButton GetDisplayButton()
        {
            CrescentButton displayButton = new(ButtonStyle.Translucent)
            {
                Width = 35,
                Height = 35,
                Padding = new(7)
            };

            SetButtonContent(displayButton, modsTab.pluginData!.modGridType);
            ModGridType[] gridTypes = Enum.GetValues<ModGridType>();
            UniformGrid layoutGrid = new()
            {
                Rows = 1,
                Columns = gridTypes.Length
            };

            Flyout flyout = new()
            {
                Placement = PlacementMode.BottomEdgeAlignedRight,
                Content = layoutGrid
            };

            for (int index = 0; index < gridTypes.Length; index++)
            {
                ModGridType gridType = gridTypes[index];
                CrescentButton optionButton = new(ButtonStyle.Translucent)
                {
                    Width = 35,
                    Height = 35,
                    Padding = new(7),
                    Margin = new(3)
                };

                SetButtonContent(optionButton, gridType);
                optionButton.onClick += async () =>
                {
                    flyout.Hide();
                    if (modsTab.pluginData!.modGridType == gridType) return;

                    modsTab.pluginData.modGridType = gridType;
                    SetButtonContent(displayButton, gridType);
                    await modsTab.pluginData.SaveAsync();
                    modsTab.RenderMods();
                };

                layoutGrid.Children.Add(optionButton);
            }

            displayButton.Flyout = flyout;
            return displayButton;

            void SetButtonContent(CrescentButton targetButton, ModGridType type)
            {
                Image image = new()
                {
                    Source = new Bitmap(AssetLoader.Open(new Uri($"avares://Crescent.UI/Assets/GridTypes/{type}.png", UriKind.Absolute)))
                };

                Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                targetButton.Content = image;
            }
        }
    }
}