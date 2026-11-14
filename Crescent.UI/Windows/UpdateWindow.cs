using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Versioning;
using Version = Crescent.Versioning.Version;

namespace Crescent.UI
{
    public class UpdateDialog : Window
    {
        public Version version;
        public bool isPostUpdate;

        public UpdateDialog(Version version, bool isPostUpdate)
        {
            this.version = version;
            this.isPostUpdate = isPostUpdate;

            Title = isPostUpdate ? $"Updated to Crescent {version.version}" : $"Update Available ({version.version})";
            Width = 500;
            Height = 360;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowDecorations = WindowDecorations.None;
            Background = Colors.MainPanelStart;

            Content = BuildLayout();
        }

        public Control BuildLayout()
        {
            Grid rootGrid = new()
            {
                Margin = new Thickness(24)
            };

            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            rootGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            TextBlock headerBlock = new()
            {
                Text = isPostUpdate ? $"Welcome to Crescent {version.version}" : $"Crescent {version.version} is Available",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            };
            Grid.SetRow(headerBlock, 0);

            string subtitleText = isPostUpdate ? $"Successfully updated from {VersionTracker.Instance.previousVersion} to {version.version}" : $"You are currently running {Runtime.Version}";

            TextBlock subtitleBlock = new()
            {
                Text = subtitleText,
                FontSize = 13,
                Foreground = Colors.Green,
                Margin = new Thickness(0, 4, 0, 14)
            };
            Grid.SetRow(subtitleBlock, 1);

            Border notesBorder = new()
            {
                Background = Colors.MainPanelEnd,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 18),
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(version.releaseNotes) ? "No changelog provided for this release" : version.releaseNotes,
                        FontSize = 13,
                        Foreground = Brushes.LightGray,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
            Grid.SetRow(notesBorder, 2);

            StackPanel buttonRow = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            if (isPostUpdate)
            {
                CrescentButton closeButton = new(Colors.Green)
                {
                    Content = "Awesome",
                    Width = 120,
                    Height = 36
                };

                closeButton.onClick += async () =>
                {
                    await VersionTracker.Instance.AcknowledgePopupAsync();
                    Close();
                };

                buttonRow.Children.Add(closeButton);
            }
            else
            {
                CrescentButton laterButton = new(ButtonStyle.Translucent)
                {
                    Content = "Later",
                    Width = 100,
                    Height = 36
                };
                laterButton.onClick += () => Close();

                CrescentButton updateButton = new(Colors.Green)
                {
                    Content = "Update & Restart",
                    Width = 150,
                    Height = 36
                };

                updateButton.onClick += async () =>
                {
                    updateButton.IsEnabled = false;
                    laterButton.IsEnabled = false;
                    Close();
                    await RunManagerUpdateWithToastsAsync(version);
                };

                buttonRow.Children.Add(laterButton);
                buttonRow.Children.Add(updateButton);
            }

            Grid.SetRow(buttonRow, 3);
            rootGrid.Children.Add(headerBlock);
            rootGrid.Children.Add(subtitleBlock);
            rootGrid.Children.Add(notesBorder);
            rootGrid.Children.Add(buttonRow);

            return rootGrid;
        }

        public static async Task RunManagerUpdateWithToastsAsync(Version release)
        {
            ToastManager.ShowProgress("Updating Crescent", $"Downloading {release.version}...", release.assetName, 0, false);
            bool success = await GithubUpdater.UpdateAsync(release, (downloadedBytes, totalBytes) =>
            {
                double percentage = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100.0 : 0;
                double downloadedMegabytes = downloadedBytes / 1048576.0;
                double totalMegabytes = totalBytes / 1048576.0;
                string progressStatus = totalBytes > 0 ? $"Downloading {release.version} ({downloadedMegabytes:F1} MB / {totalMegabytes:F1} MB)" : $"Downloading {release.version} ({downloadedMegabytes:F1} MB)";

                ToastManager.ShowProgress("Updating Crescent", progressStatus, release.assetName, percentage, totalBytes <= 0);
            }, stageMessage => { ToastManager.ShowProgress("Updating Crescent", stageMessage, release.version, 100, true); });

            if (!success) ToastManager.CompleteProgress("Update Failed", $"Could not install Crescent {release.version} :(", ToastType.Error, 4000);
        }
    }
}