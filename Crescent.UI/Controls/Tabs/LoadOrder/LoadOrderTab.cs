using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;
using Crescent.UI.Controls.Sidebar;
using Crescent.UI.Controls.Tabs.Elements;
using Crescent.UI.Controls.Toasts;
using Crescent.UI.Dialogs;
using Silk.Utilities;

namespace Crescent.UI.Controls.Tabs
{
    public class LoadOrderTab : StackPanel, ISidebarTab
    {
        public PluginData? pluginData;
        public SidebarOverlay? sidebarOverlay;
        public StackPanel? orderListPanel;
        public string currentSearchFilter = string.Empty;

        public int Order => 1;

        public CrescentButton GetButton() => new(ButtonStyle.Translucent)
        {
            Content = "Load Order",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new(10, 8)
        };

        public void OnLoaded(SidebarOverlay sidebarOverlay, PluginData pluginData)
        {
            this.pluginData = pluginData;
            this.sidebarOverlay = sidebarOverlay;
            Setup();
        }

        public void OnClick() => Refresh();

        public void Refresh()
        {
            Children.Clear();
            Setup();
        }

        public void Setup()
        {
            if (pluginData == null) return;
            pluginData.SetLoadOrder();

            Margin = new(30, 15, 30, 30);
            Spacing = 15;
            Background = Brushes.Transparent;

            Children.Add(GetHeaderPanel());
            orderListPanel = new()
            {
                Spacing = 8
            };

            RenderOrder();
            Children.Add(orderListPanel);
        }

        #region Header & Buttons

        public StackPanel GetHeaderPanel()
        {
            StackPanel headerPanel = new()
            {
                Spacing = 10
            };

            TextBlock titleBlock = new()
            {
                Text = $"Load Order ({pluginData!.SelectedProfile.name})",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(titleBlock);
            headerPanel.Children.Add(GetControlsGrid());
            headerPanel.Children.Add(new Separator
            {
                Height = 1,
                Background = Colors.Separator
            });

            return headerPanel;
        }

        public Grid GetControlsGrid()
        {
            Grid controlsRow = new()
            {
                ColumnSpacing = 12
            };

            controlsRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star)
            {
                MinWidth = 140,
                MaxWidth = 280
            });
            controlsRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            controlsRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            CrescentTextBox searchBox = GetSearchBox();
            Grid.SetColumn(searchBox, 0);

            StackPanel actionsStack = GetActionsPanel();
            Grid.SetColumn(actionsStack, 2);

            controlsRow.Children.Add(searchBox);
            controlsRow.Children.Add(actionsStack);
            return controlsRow;
        }

        public CrescentTextBox GetSearchBox()
        {
            CrescentTextBox searchBox = new()
            {
                PlaceholderText = "Search load order...",
                Text = currentSearchFilter,
                Height = 35,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            searchBox.TextChanged += (_, _) =>
            {
                currentSearchFilter = searchBox.Text?.Trim() ?? string.Empty;
                RenderOrder();
            };

            return searchBox;
        }

        public StackPanel GetActionsPanel()
        {
            StackPanel actionsStack = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            actionsStack.Children.Add(GetCoreFirstButton());
            actionsStack.Children.Add(GetConflictsButton());
            return actionsStack;
        }

        public CrescentButton GetCoreFirstButton()
        {
            CrescentButton coreFirstButton = new(ButtonStyle.Translucent)
            {
                Content = "Core Mods First",
                Padding = new(14, 0),
                Height = 35
            };

            coreFirstButton.onClick += async () =>
            {
                if (pluginData == null) return;
                SetCoreModsFirst();
                await pluginData.SaveAsync();
                RenderOrder();
                ToastManager.Show("Load Order Updated", "Core frameworks moved to the start of the load order", ToastType.Info, 3000, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
            };

            return coreFirstButton;
        }

        public CrescentButton GetConflictsButton()
        {
            CrescentButton conflictsButton = new(Colors.Accent)
            {
                Content = "Resolve File Conflicts",
                Padding = new(14, 0),
                Height = 35
            };

            conflictsButton.onClick += async () =>
            {
                if (pluginData == null) return;
                Dictionary<string, List<ModData>> conflicts = pluginData.GetConflicts();
                if (conflicts.Count == 0)
                {
                    ToastManager.Show("No Conflicts Found", "No enabled mods have overlapping files", ToastType.Success, 3000, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
                    return;
                }

                Window? window = TopLevel.GetTopLevel(this) as Window;
                if (window == null) return;

                ConflictReportDialog dialog = new(pluginData, conflicts);
                await dialog.ShowDialog(window);
                if (dialog.conflictsResolved)
                {
                    await pluginData.SaveAsync();
                    RenderOrder();
                    ToastManager.Show("Priorities Saved", "Conflict winners applied to load order", ToastType.Success, 3000, $"{pluginData.Name} • ({pluginData.SelectedProfile.name})");
                }
            };

            return conflictsButton;
        }

        #endregion

        #region Ordering & Rendering

        public void SetCoreModsFirst()
        {
            if (pluginData == null) return;
            List<ModData> ordered = pluginData.installedMods.OrderByDescending(mod => pluginData.IsCoreMod(mod)).ThenBy(mod => mod.loadOrder).ToList();

            for (int index = 0; index < ordered.Count; index++)
                ordered[index].loadOrder = index;
        }

        public Dictionary<ModData, int> GetConflictCounts(Dictionary<string, List<ModData>> conflicts)
        {
            Dictionary<ModData, int> conflictCounts = new();
            foreach (KeyValuePair<string, List<ModData>> conflictEntry in conflicts)
                for (int index = 0; index < conflictEntry.Value.Count; index++)
                {
                    ModData conflictingMod = conflictEntry.Value[index];
                    conflictCounts.TryGetValue(conflictingMod, out int currentCount);
                    conflictCounts[conflictingMod] = currentCount + 1;
                }

            return conflictCounts;
        }

        public void RenderOrder()
        {
            if (pluginData == null || orderListPanel == null) return;
            orderListPanel.Children.Clear();

            Dictionary<string, List<ModData>> conflicts = pluginData.GetConflicts();
            Dictionary<ModData, int> conflictCounts = GetConflictCounts(conflicts);
            List<ModData> enabledMods = pluginData.installedMods.Where(mod => pluginData.SelectedProfile.IsModEnabled(mod)).OrderBy(mod => mod.loadOrder).ToList();

            for (int index = 0; index < enabledMods.Count; index++)
            {
                ModData mod = enabledMods[index];
                if (!string.IsNullOrEmpty(currentSearchFilter) && !mod.DisplayName.Contains(currentSearchFilter, StringComparison.OrdinalIgnoreCase) && !mod.modName.Contains(currentSearchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                conflictCounts.TryGetValue(mod, out int modConflicts);
                LoadOrderMod loadOrderMod = new(pluginData, mod, this, enabledMods, index, modConflicts);
                orderListPanel.Children.Add(loadOrderMod);
            }
        }

        public async Task MoveModAsync(List<ModData> enabledMods, int fromIndex, int toIndex)
        {
            if (pluginData == null || toIndex < 0 || toIndex >= enabledMods.Count || fromIndex == toIndex) return;

            ModData movedMod = enabledMods[fromIndex];
            enabledMods.RemoveAt(fromIndex);
            enabledMods.Insert(toIndex, movedMod);

            for (int index = 0; index < enabledMods.Count; index++) enabledMods[index].loadOrder = index;
            await pluginData.SaveAsync();
            RenderOrder();
        }

        #endregion
    }
}