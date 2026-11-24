using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.APIs.NexusMods;
using Crescent.Plugins;
using Crescent.UI.Controls;
using Crescent.UI.Queues;
using Silk.Utilities;

namespace Crescent.UI.Dialogs
{
    public class ModDownloadDialog : Dialog
    {
        public PluginData pluginData;
        public string gameDomain;
        public string nexusId;
        public string targetModName;
        public List<RequirementNode> requirements;
        public bool DownloadSuccess { get; private set; }

        public ModDownloadDialog(PluginData pluginData, string modPageUrl, string modName = "")
        {
            this.pluginData = pluginData;
            this.gameDomain = pluginData.API.Name;
            this.nexusId = string.Empty;
            this.targetModName = modName;
            this.requirements = new();

            Width = 1;
            Height = 1;
            WindowDecorations = WindowDecorations.None;
            ShowInTaskbar = false;
            Opacity = 0;

            Opened += async (_, _) =>
            {
                Window? owner = Owner as Window;
                Close();
                try
                {
                    DownloadSuccess = await ModDownloadQueue.RequestDownloadAsync(pluginData, modPageUrl, modName, owner);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
            };
        }

        public ModDownloadDialog(PluginData pluginData, string gameDomain, string nexusId, string targetModName, List<RequirementNode> requirements)
        {
            this.pluginData = pluginData;
            this.gameDomain = gameDomain;
            this.nexusId = nexusId;
            this.targetModName = targetModName;
            this.requirements = requirements;

            Width = 520;
            SizeToContent = SizeToContent.Height;
            Title = "Optional Requirements";

            BuildRequirementsUI();
        }

        private void BuildRequirementsUI()
        {
            StackPanel contentHost = new()
            {
                Margin = new Thickness(22),
                Spacing = 14
            };

            bool isMainInstalled = ModDownloadQueue.IsModInstalled(pluginData, nexusId, targetModName);
            bool isPremium = NexusUtils.IsPremiumUser;

            StackPanel headerStack = new() { Spacing = 4 };
            headerStack.Children.Add(new TextBlock
            {
                Text = targetModName,
                FontSize = 17,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });

            headerStack.Children.Add(new TextBlock
            {
                Text = isMainInstalled ? "Select optional dependencies to install" : $"Select optional dependencies • Profile: {pluginData.SelectedProfile.name}",
                FontSize = 12,
                Foreground = isMainInstalled ? Colors.Accent : Brushes.Gray
            });

            contentHost.Children.Add(headerStack);

            if (!isPremium)
            {
                Border nonPremiumBanner = new()
                {
                    Background = Colors.Overlay,
                    BorderBrush = Colors.Red,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12, 8)
                };

                StackPanel bannerStack = new() { Spacing = 3 };
                bannerStack.Children.Add(new TextBlock
                {
                    Text = "Free Account • Browser NXM Download",
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Colors.Red
                });

                bannerStack.Children.Add(new TextBlock
                {
                    Text = "Clicking Download will open each selected mod page in your browser so you can click 'Mod Manager Download'.",
                    FontSize = 11,
                    Foreground = Brushes.LightGray,
                    TextWrapping = TextWrapping.Wrap
                });

                nonPremiumBanner.Child = bannerStack;
                contentHost.Children.Add(nonPremiumBanner);
            }

            List<RequirementNode> requiredNodes = requirements.Where(req => !req.IsOptional && !req.IsExternal).ToList();
            List<RequirementNode> optionalNodes = requirements.Where(req => req.IsOptional || req.IsExternal).ToList();

            StackPanel reqListStack = new() { Spacing = 8 };

            if (optionalNodes.Count > 0)
            {
                reqListStack.Children.Add(new TextBlock
                {
                    Text = $"Optional Mods ({optionalNodes.Count})",
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White
                });

                for (int index = 0; index < optionalNodes.Count; index++)
                    reqListStack.Children.Add(BuildRequirementRow(optionalNodes[index]));
            }

            if (requiredNodes.Count > 0)
            {
                reqListStack.Children.Add(new TextBlock
                {
                    Text = $"Required Mods ({requiredNodes.Count})",
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.LightGray,
                    Margin = new Thickness(0, 6, 0, 0)
                });

                for (int index = 0; index < requiredNodes.Count; index++)
                    reqListStack.Children.Add(BuildRequirementRow(requiredNodes[index]));
            }

            ScrollViewer reqScroll = new()
            {
                MaxHeight = 280,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = reqListStack
            };

            contentHost.Children.Add(reqScroll);

            Grid actionRow = new()
            {
                ColumnSpacing = 10,
                Margin = new Thickness(0, 6, 0, 0)
            };
            actionRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            actionRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            actionRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            CrescentButton cancelButton = new(ButtonStyle.Translucent)
            {
                Content = "Cancel",
                Width = 100,
                Height = 35
            };
            cancelButton.onClick += Close;
            Grid.SetColumn(cancelButton, 1);

            CrescentButton downloadButton = new(Colors.Accent)
            {
                Content = isPremium ? "Download" : "Open Pages",
                Padding = new Thickness(16, 0),
                Height = 35
            };

            downloadButton.onClick += () =>
            {
                List<RequirementNode> selectedReqs = requirements.Where(req => req.IsSelected && !req.IsInstalled).ToList();
                for (int index = 0; index < selectedReqs.Count; index++)
                {
                    RequirementNode req = selectedReqs[index];
                    if (req.IsExternal && !string.IsNullOrEmpty(req.Url))
                    {
                        Process.Start(new ProcessStartInfo(req.Url) { UseShellExecute = true });
                        continue;
                    }

                    string reqUrl = $"https://www.nexusmods.com/{gameDomain}/mods/{req.NexusId}";
                    ModDownloadQueue.Enqueue(pluginData, reqUrl, req.ModName, req.NexusId, gameDomain, false);
                }

                string mainUrl = $"https://www.nexusmods.com/{gameDomain}/mods/{nexusId}";
                ModDownloadQueue.Enqueue(pluginData, mainUrl, targetModName, nexusId, gameDomain, true);

                DownloadSuccess = true;
                Close();
            };
            Grid.SetColumn(downloadButton, 2);

            actionRow.Children.Add(cancelButton);
            actionRow.Children.Add(downloadButton);
            contentHost.Children.Add(actionRow);

            Content = new Border
            {
                Background = Colors.MainGradient,
                BorderBrush = Colors.Separator,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = contentHost
            };
        }

        private Border BuildRequirementRow(RequirementNode node)
        {
            Border rowBorder = new()
            {
                Background = Colors.Overlay,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 7)
            };

            Grid rowGrid = new() { ColumnSpacing = 10 };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            CheckBox selectBox = new()
            {
                IsChecked = node.IsSelected,
                IsEnabled = !node.IsInstalled,
                VerticalAlignment = VerticalAlignment.Center
            };

            selectBox.IsCheckedChanged += (_, _) => node.IsSelected = selectBox.IsChecked ?? false;
            Grid.SetColumn(selectBox, 0);

            StackPanel infoStack = new()
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            infoStack.Children.Add(new TextBlock
            {
                Text = node.ModName,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = node.IsInstalled ? Brushes.Gray : Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (!string.IsNullOrEmpty(node.Notes))
            {
                infoStack.Children.Add(new TextBlock
                {
                    Text = node.Notes,
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    MaxLines = 2
                });
            }

            Grid.SetColumn(infoStack, 1);

            string badgeText = node.IsInstalled ? "Installed" : node.IsExternal ? "External" : node.IsOptional ? "Optional" : "Required";
            IBrush badgeBrush = node.IsInstalled ? Colors.Accent : node.IsOptional ? Brushes.Gray : Colors.Red;

            TextBlock badgeBlock = new()
            {
                Text = badgeText,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = badgeBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(badgeBlock, 2);

            rowGrid.Children.Add(selectBox);
            rowGrid.Children.Add(infoStack);
            rowGrid.Children.Add(badgeBlock);

            rowBorder.Child = rowGrid;
            return rowBorder;
        }
    }
}