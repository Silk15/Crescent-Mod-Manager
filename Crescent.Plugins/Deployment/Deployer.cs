using Crescent.IO;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Crescent.Plugins
{
    public abstract class Deployer
    {
        public PluginData plugin;
        public FileLinker linker = new();
        public BackupManager backups = new();

        public Deployer(PluginData pluginData) => plugin = pluginData;

        public abstract Task Deploy();
        public abstract void Withdraw();
        public abstract void WithdrawMod(ModData mod);

        public virtual void SetupDirectories(out string archiveFolder, out string manualFolder)
        {
            if (!Directory.Exists(plugin.DeploymentPath)) Directory.CreateDirectory(plugin.DeploymentPath);

            archiveFolder = Path.Combine(plugin.StagingPath, PluginData.Archive);
            if (!Directory.Exists(archiveFolder)) Directory.CreateDirectory(archiveFolder);

            manualFolder = Path.Combine(plugin.StagingPath, PluginData.Manual);
            if (!Directory.Exists(manualFolder)) Directory.CreateDirectory(manualFolder);

            backups.Load(plugin.StagingPath);
        }

        public virtual string GetStagingFolder(ModData mod, string stagingSubFolder, string cleanName)
        {
            if (!string.IsNullOrEmpty(mod.filePath) && Directory.Exists(mod.filePath)) return mod.filePath;

            string exactCandidate = Path.Combine(stagingSubFolder, mod.modName);
            if (Directory.Exists(exactCandidate)) return exactCandidate;
            return Path.Combine(stagingSubFolder, cleanName);
        }

        public virtual void SetModState(ModData mod, string manualFolder)
        {
            bool isCoreMod = plugin.IsCoreMod(mod);
            string cleanName = ModData.StripArchiveExtension(mod.modName);
            bool isManualFolder = File.Exists(Path.Combine(manualFolder, mod.modName)) || Directory.Exists(Path.Combine(manualFolder, mod.modName)) || Directory.Exists(Path.Combine(manualFolder, cleanName));

            if (isCoreMod && !isManualFolder)
            {
                string rootModPath = Path.Combine(plugin.StagingPath, mod.modName);
                string manualModPath = Path.Combine(manualFolder, mod.modName);

                if (File.Exists(rootModPath)) File.Move(rootModPath, manualModPath, true);
                else if (Directory.Exists(rootModPath) && !Directory.Exists(manualModPath)) Directory.Move(rootModPath, manualModPath);

                string rootStagingDirectory = Path.Combine(plugin.StagingPath, cleanName);
                string manualStagingDirectory = Path.Combine(manualFolder, cleanName);

                if (Directory.Exists(rootStagingDirectory) && !Directory.Exists(manualStagingDirectory)) Directory.Move(rootStagingDirectory, manualStagingDirectory);
                isManualFolder = true;
            }

            mod.isManual = isCoreMod || isManualFolder;
        }

        public virtual void ExtractMod(ModData mod, string stagingSubFolder, string archiveFolder, string modStagingDirectory)
        {
            string? unarchivedPath = GetDirectArchivePath(mod, stagingSubFolder);
            bool hasFreshArchive = !string.IsNullOrEmpty(unarchivedPath) && File.Exists(unarchivedPath);

            if (hasFreshArchive && Directory.Exists(modStagingDirectory)) Directory.Delete(modStagingDirectory, true);
            else if (Directory.Exists(modStagingDirectory) && Directory.EnumerateFileSystemEntries(modStagingDirectory).Any()) return;

            string? sourceArchivePath = hasFreshArchive ? unarchivedPath : GetArchivePath(mod, stagingSubFolder, archiveFolder);
            if (string.IsNullOrEmpty(sourceArchivePath) || !File.Exists(sourceArchivePath))
                return;

            Directory.CreateDirectory(modStagingDirectory);
            FileInfo fileInfo = new(sourceArchivePath);
            using IArchive archive = ArchiveFactory.OpenArchive(fileInfo);

            foreach (IArchiveEntry entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;
                ExtractionOptions options = new()
                {
                    ExtractFullPath = true,
                    Overwrite = true
                };
                entry.WriteToDirectory(modStagingDirectory, options);
            }
        }

        public virtual string? GetDirectArchivePath(ModData mod, string stagingSubFolder)
        {
            if (!Directory.Exists(stagingSubFolder)) return null;

            string directPath = Path.Combine(stagingSubFolder, mod.modName);
            if (File.Exists(directPath)) return directPath;

            string cleanName = ModData.StripArchiveExtension(mod.modName);
            for (int extensionIndex = 0; extensionIndex < ModData.ArchiveExtensions.Length; extensionIndex++)
            {
                string candidatePath = Path.Combine(stagingSubFolder, cleanName + ModData.ArchiveExtensions[extensionIndex]);
                if (File.Exists(candidatePath)) return candidatePath;
            }

            return null;
        }

        public virtual string? GetArchivePath(ModData mod, string stagingSubFolder, string archiveFolder)
        {
            if (!string.IsNullOrEmpty(mod.filePath) && File.Exists(mod.filePath)) 
                return mod.filePath;

            string cleanName = ModData.StripArchiveExtension(mod.modName);
            string[] searchFolders = new[] { stagingSubFolder, archiveFolder };

            for (int folderIndex = 0; folderIndex < searchFolders.Length; folderIndex++)
            {
                string folder = searchFolders[folderIndex];
                if (!Directory.Exists(folder)) continue;

                string directPath = Path.Combine(folder, mod.modName);
                if (File.Exists(directPath)) return directPath;

                for (int extensionIndex = 0; extensionIndex < ModData.ArchiveExtensions.Length; extensionIndex++)
                {
                    string candidatePath = Path.Combine(folder, cleanName + ModData.ArchiveExtensions[extensionIndex]);
                    if (File.Exists(candidatePath)) return candidatePath;
                }
            }

            return null;
        }

        public virtual void ArchiveMod(ModData mod, string stagingSubFolder, string archiveFolder)
        {
            if (mod.isManual) return;

            string? modPath = GetDirectArchivePath(mod, stagingSubFolder);
            if (string.IsNullOrEmpty(modPath) || !File.Exists(modPath)) return;

            string archivedZipPath = Path.Combine(archiveFolder, Path.GetFileName(modPath));
            if (File.Exists(archivedZipPath)) File.Delete(archivedZipPath);

            File.Move(modPath, archivedZipPath);
        }

        public virtual void CleanEmptyFolders(string? directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return;

            string fullDeploymentPath = Path.GetFullPath(plugin.DeploymentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? currentDirectory = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            while (!string.IsNullOrEmpty(currentDirectory) && currentDirectory.Length > fullDeploymentPath.Length && currentDirectory.StartsWith(fullDeploymentPath, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(currentDirectory) && !Directory.EnumerateFileSystemEntries(currentDirectory).Any())
                {
                    Directory.Delete(currentDirectory, false);
                    currentDirectory = Path.GetDirectoryName(currentDirectory);
                }
                else break;
            }
        }

        public virtual void Notify(string title, string message, string toastType = "Info", int duration = 4000, string detail = "") => PluginData.ShowToast(title, message, toastType, duration, detail);
    }
}