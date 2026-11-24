using Newtonsoft.Json;
using Silk.Utilities;

namespace Crescent.IO
{
    public class BackupManager
    {
        public HashSet<string> backedUpFiles = new(StringComparer.OrdinalIgnoreCase);

        public string BackupFolderName => ".backups";

        public string ManifestFileName => "backups.json";

        public string GetBackupRoot(string stagingPath) => Path.Combine(stagingPath, BackupFolderName);

        public string GetManifestPath(string stagingPath) => Path.Combine(GetBackupRoot(stagingPath), ManifestFileName);

        public void Load(string stagingPath)
        {
            string manifestPath = GetManifestPath(stagingPath);
            if (!File.Exists(manifestPath)) return;
            try
            {
                string json = File.ReadAllText(manifestPath);
                HashSet<string>? loaded = JsonConvert.DeserializeObject<HashSet<string>>(json);
                if (loaded != null) backedUpFiles = new(loaded, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
            }
        }

        public void Save(string stagingPath)
        {
            try
            {
                string backupRoot = GetBackupRoot(stagingPath);
                if (!Directory.Exists(backupRoot)) Directory.CreateDirectory(backupRoot);

                string json = JsonConvert.SerializeObject(backedUpFiles, Formatting.Indented);
                File.WriteAllText(GetManifestPath(stagingPath), json);
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
            }
        }

        public bool BackupFile(string stagingPath, string deploymentPath, string relativePath, bool isKnownModFile)
        {
            if (isKnownModFile || backedUpFiles.Contains(relativePath)) return false;
            string targetFile = Path.Combine(deploymentPath, relativePath);
            FileInfo fileInfo = new(targetFile);

            if (!fileInfo.Exists || fileInfo.LinkTarget != null) return false;
            try
            {
                string backupFile = Path.Combine(GetBackupRoot(stagingPath), relativePath);
                string? backupDirectory = Path.GetDirectoryName(backupFile);

                if (!string.IsNullOrEmpty(backupDirectory) && !Directory.Exists(backupDirectory))
                    Directory.CreateDirectory(backupDirectory);

                File.Copy(targetFile, backupFile, true);
                backedUpFiles.Add(relativePath);
                Save(stagingPath);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
                return false;
            }
        }

        public bool RestoreFile(string stagingPath, string deploymentPath, string relativePath)
        {
            if (!backedUpFiles.Contains(relativePath)) return false;
            string backupFile = Path.Combine(GetBackupRoot(stagingPath), relativePath);
            if (!File.Exists(backupFile))
            {
                backedUpFiles.Remove(relativePath);
                Save(stagingPath);
                return false;
            }
            try
            {
                string targetFile = Path.Combine(deploymentPath, relativePath);
                string? targetDirectory = Path.GetDirectoryName(targetFile);

                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                if (File.Exists(targetFile) || Path.Exists(targetFile))
                    File.Delete(targetFile);

                File.Move(backupFile, targetFile);
                backedUpFiles.Remove(relativePath);
                Save(stagingPath);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
                return false;
            }
        }

        public int RestoreAll(string stagingPath, string deploymentPath)
        {
            Load(stagingPath);
            List<string> snapshot = backedUpFiles.ToList();
            int restoredCount = 0;

            for (int index = 0; index < snapshot.Count; index++)
            {
                if (RestoreFile(stagingPath, deploymentPath, snapshot[index]))
                    restoredCount++;
            }

            return restoredCount;
        }
    }
}