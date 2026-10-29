using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Crescent.Plugins;
using Crescent.Utilities;
using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public static class ModsList
    {
        public static readonly List<string> PrivilegedFolders = new()
        {
            "Profiles",
            "Archive",
            "Manual"
        };
        
        public static void SetupModListRenderer(GameData game, TextBox searchBox, ComboBox filterComboBox, StackPanel listPanel, Mods mods)
        {
            Action? renderMods = null;
            renderMods = () =>
            {
                listPanel.Children.Clear();
                List<string> modPaths = GetModPaths(game);

                HashSet<string> validModNames = new(StringComparer.OrdinalIgnoreCase);
                foreach (string filePath in modPaths) validModNames.Add(Path.GetFileName(filePath));

                game.installedMods.RemoveAll(mod =>
                {
                    if (validModNames.Contains(mod.modName)) return false;

                    string unzippedName = Path.GetFileNameWithoutExtension(mod.modName);
                    if (validModNames.Contains(unzippedName))
                    {
                        mod.modName = unzippedName;
                        return false;
                    }

                    return true;
                });

                string filterText = searchBox.Text != null ? searchBox.Text.ToLower() : "";
                string selectedFilter = filterComboBox.SelectedItem as string ?? "All";
                bool isDefaultProfile = string.Equals(game.selectedProfileName, GameData.Default, StringComparison.OrdinalIgnoreCase);

                if (isDefaultProfile)
                {
                    foreach (string filePath in modPaths)
                    {
                        string modName = Path.GetFileName(filePath);
                        string name = Directory.Exists(filePath) ? modName : Path.GetFileNameWithoutExtension(filePath);
                        ModData? modData = game.installedMods.Find(mod => mod.modName == modName);
                        if (modData == null)
                        {
                            modData = new(game, modName, filePath)
                            {
                                modName = modName
                            };
                            game.installedMods.Add(modData);
                        }

                        game.OnModLoaded(modData);

                        if (!game.MatchesFilter(modData, selectedFilter)) continue;
                        if (!string.IsNullOrEmpty(filterText) && !modData.DisplayName.ToLower().Contains(filterText) && !modName.ToLower().Contains(filterText)) continue;

                        Grid modRowGrid = GetModRowGrid(game, modData, filePath, modName, name, renderMods, mods);
                        listPanel.Children.Add(modRowGrid);
                    }
                }
                else
                {
                    ProfileData profile = game.SelectedProfile;
                    for (int i = 0; i < profile.mods.Count; i++)
                    {
                        ProfileModData profileMod = profile.mods[i];
                        ModData? modData = game.installedMods.Find(mod => string.Equals(mod.modName, profileMod.modName, StringComparison.OrdinalIgnoreCase));
                        if (modData == null) continue;

                        string filePath = modData.filePath;
                        if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath))) continue;

                        string modName = modData.modName;
                        string name = Directory.Exists(filePath) ? modName : Path.GetFileNameWithoutExtension(filePath);

                        if (!game.MatchesFilter(modData, selectedFilter)) continue;
                        if (!string.IsNullOrEmpty(filterText) && !modData.DisplayName.ToLower().Contains(filterText) && !modName.ToLower().Contains(filterText)) continue;

                        Grid modRowGrid = GetModRowGrid(game, modData, filePath, modName, name, renderMods, mods);
                        listPanel.Children.Add(modRowGrid);
                    }
                }

                _ = game.SaveAsync();
            };

            searchBox.TextChanged += (sender, e) => renderMods?.Invoke();
            filterComboBox.SelectionChanged += (sender, e) => renderMods?.Invoke();

            renderMods?.Invoke();
        }

        public static List<string> GetModPaths(GameData game)
        {
            List<string> modPaths = new();
            bool HasExtractedCounterpart(string archivePath, string baseDirectory)
            {
                string expectedDirName = Path.GetFileNameWithoutExtension(archivePath);
                string expectedDirPath = Path.Combine(baseDirectory, expectedDirName);
                return Directory.Exists(expectedDirPath);
            }

            string[] rootFiles = Directory.GetFiles(game.StagingPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in rootFiles)
            {
                string extension = Path.GetExtension(file).ToLower();
                if (extension.IsArchive())
                {
                    if (HasExtractedCounterpart(file, game.StagingPath)) continue;
                    modPaths.Add(file);
                }
            }

            string[] rootDirectories = Directory.GetDirectories(game.StagingPath, "*", SearchOption.TopDirectoryOnly);
            foreach (string directory in rootDirectories)
            {
                string directoryName = Path.GetFileName(directory);
                if (!PrivilegedFolders.AnyEquals(directoryName)) modPaths.Add(directory);
            }

            string manualFolder = Path.Combine(game.StagingPath, GameData.Manual);
            if (Directory.Exists(manualFolder))
            {
                string[] manualFiles = Directory.GetFiles(manualFolder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in manualFiles)
                {
                    string extension = Path.GetExtension(file).ToLower();
                    if (extension.IsArchive())
                    {
                        if (HasExtractedCounterpart(file, manualFolder)) continue;
                        modPaths.Add(file);
                    }
                }

                string[] directories = Directory.GetDirectories(manualFolder, "*", SearchOption.TopDirectoryOnly);
                foreach (string directory in directories)
                {
                    modPaths.Add(directory);
                }
            }

            return modPaths;
        }

        public static Grid GetModRowGrid(GameData game, ModData modData, string filePath, string fileName, string name, Action? renderMods, Mods mods)
        {
            Grid modRowGrid = new();
            modRowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            modRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            modRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            CheckBox modCheckBox = GetModCheckBox(game, modData);
            Grid.SetColumn(modCheckBox, 0);

            Button manualButton = GetManualButton(game, modData, filePath, renderMods);
            Grid.SetColumn(manualButton, 1);

            Button menuButton = GetMenuButton(game, modData, filePath, fileName, name, renderMods, mods);
            Grid.SetColumn(menuButton, 2);

            modRowGrid.Children.Add(modCheckBox);
            modRowGrid.Children.Add(manualButton);
            modRowGrid.Children.Add(menuButton);

            return modRowGrid;
        }

        public static CheckBox GetModCheckBox(GameData game, ModData modData)
        {
            CheckBox modCheckBox = new()
            {
                Content = modData.DisplayName,
                IsChecked = game.SelectedProfile.IsModEnabled(modData),
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            modCheckBox.IsCheckedChanged += async (sender, routedEventArgs) =>
            {
                bool isEnabled = modCheckBox.IsChecked ?? true;
                game.SelectedProfile.SetModEnabled(modData, isEnabled);
                if (isEnabled) game.TryDeploy();
                else game.WithdrawMod(modData);
                await game.SaveAsync();
            };

            return modCheckBox;
        }

        public static Button GetManualButton(GameData game, ModData modData, string filePath, Action? renderMods)
        {
            CrescentButton manualButton = new(modData.isManual ? Colors.Red : Colors.Green)
            {
                Content = modData.isManual ? GameData.Manual : GameData.Managed,
                Width = 115,
                Height = 35,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            manualButton.Click += async (sender, routedEventArgs) =>
            {
                await ToggleManualInstallAsync(game, modData, filePath);
                manualButton.customColor = modData.isManual ? Colors.Red : Colors.Green;
                renderMods?.Invoke();
            };

            return manualButton;
        }

        public static Button GetMenuButton(GameData game, ModData modData, string filePath, string fileName, string name, Action? renderMods, Mods mods)
        {
            CrescentButton menuButton = new(ButtonStyle.Translucent)
            {
                Content = "⋮",
                Width = 35,
                Height = 35,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            ContextMenu contextMenu = ModContextMenu.Get(game, modData, filePath, fileName, name, renderMods, mods);
            menuButton.ContextMenu = contextMenu;
            menuButton.Click += (sender, e) => contextMenu.Open(menuButton);

            return menuButton;
        }

        public static async Task ToggleManualInstallAsync(GameData game, ModData modData, string currentFilePath)
        {
            if (string.IsNullOrEmpty(game.StagingPath) || string.IsNullOrEmpty(game.DeploymentPath)) return;

            string manualFolder = Path.Combine(game.StagingPath, GameData.Manual);
            if (!Directory.Exists(manualFolder)) Directory.CreateDirectory(manualFolder);

            string fileName = Path.GetFileName(currentFilePath);
            bool directoryExists = Directory.Exists(currentFilePath);
            if (!modData.isManual)
            {
                modData.isManual = true;
                string rootPath = Path.Combine(game.StagingPath, fileName);
                string manualPath = Path.Combine(manualFolder, fileName);

                if (directoryExists)
                {
                    if (Directory.Exists(rootPath) && !Directory.Exists(manualPath)) 
                        Directory.Move(rootPath, manualPath);
                }
                else
                {
                    if (File.Exists(rootPath) && !File.Exists(manualPath)) 
                        File.Move(rootPath, manualPath);
                }

                string modName = directoryExists ? fileName : Path.GetFileNameWithoutExtension(fileName);
                string manualDirectory = Path.Combine(manualFolder, modName);
                string extractedDirectory = Path.Combine(game.StagingPath, modName);

                if (!directoryExists && Directory.Exists(extractedDirectory))
                {
                    if (Directory.Exists(manualDirectory)) Directory.Delete(manualDirectory, true);
                    Directory.Move(extractedDirectory, manualDirectory);
                }
                else if (!directoryExists && !Directory.Exists(manualDirectory))
                {
                    Directory.CreateDirectory(manualDirectory);
                    if (File.Exists(manualPath)) ZipFile.ExtractToDirectory(manualPath, manualDirectory, true);
                }

                string sourceDirectory = directoryExists ? manualPath : manualDirectory;
                if (Directory.Exists(sourceDirectory))
                {
                    string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                    foreach (string sourceFile in sourceFiles)
                    {
                        string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                        string targetFile = Path.Combine(game.DeploymentPath, relativePath);
                        string? targetDirectory = Path.GetDirectoryName(targetFile);

                        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory)) 
                            Directory.CreateDirectory(targetDirectory);

                        if (File.Exists(targetFile) || Directory.Exists(targetFile)) File.Delete(targetFile);
                        File.Copy(sourceFile, targetFile, true);
                    }
                }
            }
            else
            {
                modData.isManual = false;
                string manualPath = Path.Combine(manualFolder, fileName);
                string rootPath = Path.Combine(game.StagingPath, fileName);

                string modName = directoryExists ? fileName : Path.GetFileNameWithoutExtension(fileName);
                string manualDirectory = Path.Combine(manualFolder, modName);

                string sourceDirectory = directoryExists ? manualPath : manualDirectory;
                if (Directory.Exists(sourceDirectory))
                {
                    string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
                    foreach (string sourceFile in sourceFiles)
                    {
                        string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                        string targetFile = Path.Combine(game.DeploymentPath, relativePath);
                        if (File.Exists(targetFile)) File.Delete(targetFile);
                    }

                    if (!directoryExists) Directory.Delete(manualDirectory, true);
                }

                if (directoryExists)
                {
                    if (Directory.Exists(manualPath) && !Directory.Exists(rootPath)) 
                        Directory.Move(manualPath, rootPath);
                }
                else
                {
                    if (File.Exists(manualPath))
                    {
                        if (File.Exists(rootPath)) File.Delete(rootPath);
                        File.Move(manualPath, rootPath);
                    }
                }
            }

            await game.SaveAsync();
        }
    }
}