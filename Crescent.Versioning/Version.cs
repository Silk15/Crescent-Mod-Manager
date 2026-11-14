namespace Crescent.Versioning
{
    [Serializable]
    public class VersionInfo
    {
        public string name = string.Empty;
        public string version = string.Empty;
        public string assetName = string.Empty;
        public string downloadUrl = string.Empty;
        public string releaseNotes = string.Empty;
        public long sizeBytes;

        public static string Clean(string? versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString)) return "0.0.0";
            string trimmed = versionString.Trim().TrimStart('v', 'V');
            int dashIndex = trimmed.IndexOf('-');
            if (dashIndex > 0) trimmed = trimmed.Substring(0, dashIndex);
            return trimmed;
        }

        public static bool IsNewerVersion(string? remoteVersionString, string? localVersionString)
        {
            if (string.IsNullOrWhiteSpace(remoteVersionString)) return false;
            if (string.IsNullOrWhiteSpace(localVersionString)) return true;

            string cleanRemote = Clean(remoteVersionString);
            string cleanLocal = Clean(localVersionString);

            if (Version.TryParse(cleanRemote, out Version? remoteVersion) && Version.TryParse(cleanLocal, out Version? localVersion)) return remoteVersion > localVersion;
            return !string.Equals(cleanRemote, cleanLocal, StringComparison.OrdinalIgnoreCase);
        }
    }
}