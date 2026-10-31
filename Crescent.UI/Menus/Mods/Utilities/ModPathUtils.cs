using Crescent.Utilities;
using Crescent.Plugins;

namespace Crescent.UI
{
    public static class ModPathUtils
    {
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
                if (!ModsTab.PrivilegedFolders.AnyEquals(directoryName)) modPaths.Add(directory);
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
                foreach (string directory in directories) modPaths.Add(directory);
            }

            return modPaths;
        }
    }
}