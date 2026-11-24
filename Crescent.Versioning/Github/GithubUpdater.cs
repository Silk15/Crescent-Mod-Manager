using System.Diagnostics;
using System.IO.Compression;
using Silk.Utilities;

namespace Crescent.Versioning
{
    public class GithubUpdater
    {
        public static Version? availableManagerUpdate;
        
        public static string StagingRoot => Path.Combine(Runtime.DataPath, "Updates");
        public static string InstallRoot => Path.GetFullPath(Path.Combine(Runtime.ManagedPath, ".."));

        public static async Task<Version?> CheckManagerUpdateAsync()
        {
            Version? latestRelease = await GithubClient.GetLatestReleaseAsync();
            if (latestRelease != null && Version.IsNewerVersion(latestRelease.version, Runtime.Version))
            {
                availableManagerUpdate = latestRelease;
                return latestRelease;
            }

            availableManagerUpdate = null;
            return null;
        }

        public static string ExtractToTemp(string archiveFilePath, out string tempRootDirectory)
        {
            tempRootDirectory = Path.Combine(StagingRoot, Guid.NewGuid().ToString("N"));
            string extractDirectory = Path.Combine(tempRootDirectory, "extracted");
            Directory.CreateDirectory(extractDirectory);

            ZipFile.ExtractToDirectory(archiveFilePath, extractDirectory, true);

            string[] subDirectories = Directory.GetDirectories(extractDirectory);
            string[] subFiles = Directory.GetFiles(extractDirectory);

            if (subDirectories.Length == 1 && subFiles.Length == 0) return subDirectories[0];
            return extractDirectory;
        }

        public static void CopyContents(string sourceDirectory, string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            string[] allFiles = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
            for (int index = 0; index < allFiles.Length; index++)
            {
                string sourceFile = allFiles[index];
                string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                string destinationFile = Path.Combine(targetDirectory, relativePath);
                string? destinationFolder = Path.GetDirectoryName(destinationFile);

                if (!string.IsNullOrEmpty(destinationFolder) && !Directory.Exists(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);

                File.Copy(sourceFile, destinationFile, true);
            }
        }

        public static async Task<bool> InstallBufferAsync(byte[] archiveBytes, string packageName, string targetDirectory)
        {
            string tempRootDirectory = string.Empty;
            try
            {
                if (!Directory.Exists(StagingRoot))
                    Directory.CreateDirectory(StagingRoot);

                string tempArchivePath = Path.Combine(StagingRoot, $"{packageName}_{Guid.NewGuid():N}.zip");
                await File.WriteAllBytesAsync(tempArchivePath, archiveBytes);

                string unwrappedSource = ExtractToTemp(tempArchivePath, out tempRootDirectory);
                File.Delete(tempArchivePath);

                CopyContents(unwrappedSource, targetDirectory);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempRootDirectory) && Directory.Exists(tempRootDirectory))
                    Directory.Delete(tempRootDirectory, true);
            }
        }

        public static async Task<bool> UpdateAsync(Version release, Action<long, long>? onDownloadProgress = null, Action<string>? onStageStatus = null)
        {
            try
            {
                if (Directory.Exists(StagingRoot))
                    Directory.Delete(StagingRoot, true);

                Directory.CreateDirectory(StagingRoot);
                string archivePath = Path.Combine(StagingRoot, release.assetName);

                bool downloaded = await GithubClient.DownloadFileAsync(release.downloadUrl, archivePath, onDownloadProgress);
                if (!downloaded || !File.Exists(archivePath))
                    return false;

                onStageStatus?.Invoke("Extracting update package...");
                string unwrappedSource = ExtractToTemp(archivePath, out _);
                File.Delete(archivePath);

                await VersionTracker.Instance.StagePendingUpdateAsync(release);
                onStageStatus?.Invoke($"Restarting into Crescent {release.version}");
                await Task.Delay(600);

                Restart(unwrappedSource, InstallRoot);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
                return false;
            }
        }

        public static void Restart(string sourceDirectory, string destinationDirectory)
        {
            string executablePath = Environment.ProcessPath!;
            int processId = Environment.ProcessId;

            string script =
                $"Wait-Process -Id {processId} -ErrorAction SilentlyContinue; " +
                $"Start-Sleep -Milliseconds 400; " +
                $"Copy-Item -Path '{sourceDirectory}\\*' -Destination '{destinationDirectory}' -Recurse -Force; " +
                $"Remove-Item -Path '{StagingRoot}' -Recurse -Force -ErrorAction SilentlyContinue; " +
                $"Start-Process -FilePath '{executablePath}'";
            
            ProcessStartInfo startInfo = new("powershell.exe")
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
            Runtime.Exit(0);
        }
    }
}