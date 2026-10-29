using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Crescent.Plugins;

using Colors = Crescent.Utilities.Colors;

namespace Crescent.UI
{
    public static class ModContextMenu
    {
        public static ContextMenu Get(GameData game, ModData modData, string filePath, string fileName, string name, Action? renderMods, Mods mods)
        {
            ContextMenu contextMenu = new();
            contextMenu.Background = Colors.MainPanelEnd;
            contextMenu.CornerRadius = new(8);
            contextMenu.ClipToBounds = true;
            
            MenuItem nexusMenuItem = new()
            {
                Header = "Open Modpage"
            };
            nexusMenuItem.Click += (sender, e) =>
            {
                Logger.Log(modData.url.nexusUrl);
                Process.Start(new ProcessStartInfo(modData.url.nexusUrl)
                {
                    UseShellExecute = true
                });
            };

            MenuItem openFolderMenuItem = new()
            {
                Header = "Open Mod Folder"
            };
            openFolderMenuItem.Click += (sender, e) =>
            {
                string stagingSubFolder = modData.isManual ? Path.Combine(game.StagingPath, "Manual") : game.StagingPath;
                string modStagingDirectory = Path.Combine(stagingSubFolder, Directory.Exists(filePath) ? fileName : Path.GetFileNameWithoutExtension(fileName));
                if (Directory.Exists(modStagingDirectory))
                {
                    Process.Start(new ProcessStartInfo(modStagingDirectory)
                    {
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(game.StagingPath))
                {
                    Process.Start(new ProcessStartInfo(game.StagingPath)
                    {
                        UseShellExecute = true
                    });
                }
            };

            MenuItem viewDeploysMenuItem = new()
            {
                Header = "View Deployed Files"
            };
            viewDeploysMenuItem.Click += async (sender, e) =>
            {
                Window? parentWindow = TopLevel.GetTopLevel(mods) as Window;
                if (parentWindow != null)
                {
                    string stagingSubFolder = modData.isManual ? Path.Combine(game.StagingPath, "Manual") : game.StagingPath;
                    string modStagingDirectory = Path.Combine(stagingSubFolder, Directory.Exists(filePath) ? fileName : Path.GetFileNameWithoutExtension(fileName));
                    string deployedList = "No files found.";
                    if (Directory.Exists(modStagingDirectory))
                    {
                        string[] sourceFiles = Directory.GetFiles(modStagingDirectory, "*.*", SearchOption.AllDirectories);
                        List<string> relativePaths = new();
                        foreach (string sourceFile in sourceFiles)
                        {
                            relativePaths.Add(Path.GetRelativePath(modStagingDirectory, sourceFile));
                        }
                        deployedList = string.Join("\n", relativePaths);
                    }

                    InputDialog infoDialog = new("Deployed Files", string.Concat("Files deployed by ", modData.DisplayName, ":\n\n", deployedList), false, "");
                    await infoDialog.ShowDialog(parentWindow);
                }
            };

            MenuItem updateModMenuItem = new()
            {
                Header = "Update Mod"
            };

            updateModMenuItem.Click += async (sender, e) =>
            {
                Window? window = TopLevel.GetTopLevel(mods) as Window;
                if (window == null) return;

                IStorageProvider storageProvider = window.StorageProvider;
                IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Updated Mod",
                    AllowMultiple = false
                });

                if (files.Count == 0) return;
                IStorageItem file = files[0];
                if (!file.Name.EndsWith(".zip") && !file.Name.EndsWith(".rar") && !file.Name.EndsWith(".7z")) return;

                game.WithdrawMod(modData);
                game.installedMods.Remove(modData);
                if (Directory.Exists(filePath)) Directory.Delete(filePath, true);
                else
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }

                string manualPath = Path.Combine(game.StagingPath, "Manual", fileName);
                if (Directory.Exists(manualPath)) Directory.Delete(manualPath, true);
                else if (File.Exists(manualPath)) File.Delete(manualPath);

                IStorageFolder? path = await storageProvider.TryGetFolderFromPathAsync(game.StagingPath);
                if (path != null)
                {
                    await file.MoveAsync(path);
                    await game.SaveAsync();
                    renderMods?.Invoke();
                }
            };

            MenuItem profileMenu = new()
            {
                Header = "Profiles"
            };

            List<ProfileData> allProfiles = game.Profiles;
            for (int i = 0; i < allProfiles.Count; i++)
            {
                ProfileData profile = allProfiles[i];
                bool isEnabledInProfile = profile.IsModEnabled(modData);
                MenuItem profileItem = new()
                {
                    Header = string.Concat(profile.name, isEnabledInProfile ? " (Enabled)" : " (Disabled)"),
                    Foreground = isEnabledInProfile ? Brushes.LightGreen : Brushes.IndianRed
                };

                profileItem.Click += async (sender, e) =>
                {
                    bool newState = !profile.IsModEnabled(modData);
                    profile.SetModEnabled(modData, newState);
                    await game.SaveAsync();
                    renderMods?.Invoke();
                };
                profileMenu.Items.Add(profileItem);
            }

            MenuItem renameMenuItem = new()
            {
                Header = "Rename"
            };
            renameMenuItem.Click += async (sender, e) =>
            {
                Window? parentWindow = TopLevel.GetTopLevel(mods) as Window;
                if (parentWindow != null)
                {
                    InputDialog renameDialog = new("Rename Mod", "Enter new name for mod folder/archive:", true, name);
                    await renameDialog.ShowDialog(parentWindow);
                    string newName = renameDialog.InputText;
                    if (!string.IsNullOrEmpty(newName) && newName != name)
                    {
                        string parentDirectory = Path.GetDirectoryName(filePath) ?? "";
                        bool directoryExists = Directory.Exists(filePath);
                        string newFilePath = directoryExists ? Path.Combine(parentDirectory, newName) : Path.Combine(parentDirectory, string.Concat(newName, Path.GetExtension(filePath)));
                        if (!File.Exists(newFilePath) && !Directory.Exists(newFilePath))
                        {
                            if (directoryExists)
                            {
                                Directory.Move(filePath, newFilePath);
                                modData.displayName = newName;
                            }
                            else
                            {
                                File.Move(filePath, newFilePath);
                                modData.displayName = Path.GetFileName(newFilePath);
                            }

                            await game.SaveAsync();
                            renderMods?.Invoke();
                        }
                    }
                }
            };

            MenuItem removeMenuItem = new()
            {
                Header = "Remove"
            };
            removeMenuItem.Click += async (sender, e) =>
            {
                game.WithdrawMod(modData);
                game.installedMods.Remove(modData);
                if (Directory.Exists(filePath)) Directory.Delete(filePath, true);
                else
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }

                string manualPath = Path.Combine(game.StagingPath, "Manual", fileName);
                if (Directory.Exists(manualPath)) Directory.Delete(manualPath, true);
                else if (File.Exists(manualPath)) File.Delete(manualPath);

                await game.SaveAsync();
                renderMods?.Invoke();
            };

            contextMenu.Items.Add(nexusMenuItem);
            contextMenu.Items.Add(openFolderMenuItem);
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(viewDeploysMenuItem);
            contextMenu.Items.Add(updateModMenuItem);
            contextMenu.Items.Add(profileMenu);
            contextMenu.Items.Add(new Separator
            {
                Background = Colors.Separator,
                Height = 1
            });

            contextMenu.Items.Add(renameMenuItem);
            contextMenu.Items.Add(removeMenuItem);

            return contextMenu;
        }
    }
}