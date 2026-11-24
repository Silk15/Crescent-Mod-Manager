using System.Runtime.InteropServices;
using Silk.Utilities;

namespace Crescent.IO
{
    public class FileLinker
    {
        public bool hardLinkFallbackUsed;

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

        public LinkType LinkFile(string sourceFile, string targetFile, bool forceCopy)
        {
            try
            {
                string? targetDirectory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);
                DeleteFile(targetFile);
                if (forceCopy)
                {
                    File.Copy(sourceFile, targetFile, true);
                    return LinkType.Copy;
                }

                try
                {
                    File.CreateSymbolicLink(targetFile, sourceFile);
                    return LinkType.SymbolicLink;
                }
                catch (Exception)
                {
                    hardLinkFallbackUsed = true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && AreSameVolume(sourceFile, targetFile))
                {
                    bool created = CreateHardLink(targetFile, sourceFile, IntPtr.Zero);
                    if (created && File.Exists(targetFile))
                        return LinkType.HardLink;
                }

                File.Copy(sourceFile, targetFile, true);
                return LinkType.Copy;
            }
            catch (Exception exception)
            {
                Logger.Log(exception.ToString());
                return LinkType.Failed;
            }
        }

        public void DeleteFile(string targetFile)
        {
            FileInfo fileInfo = new(targetFile);
            if (fileInfo.Exists || fileInfo.LinkTarget != null || Path.Exists(targetFile))
                File.Delete(targetFile);
        }

        public bool IsSymbolicLink(string filePath)
        {
            FileInfo fileInfo = new(filePath);
            return fileInfo.LinkTarget != null;
        }

        public bool AreSameVolume(string firstPath, string secondPath)
        {
            string? firstRoot = Path.GetPathRoot(Path.GetFullPath(firstPath));
            string? secondRoot = Path.GetPathRoot(Path.GetFullPath(secondPath));
            return string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}