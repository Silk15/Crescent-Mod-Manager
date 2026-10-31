using JetBrains.Annotations;

namespace Crescent.Plugins
{
    [PublicAPI]
    public class DeployedContent
    {
        public bool isManual;
        public string modName;
        public string sourceFile;
        public string targetFile;

        [NonSerialized]
        public string targetDirectory;

        public DeployedContent(string modName, string sourceFile, string targetFile, string targetDirectory, bool isManual)
        {
            this.modName = modName;
            this.sourceFile = sourceFile;
            this.targetFile = targetFile;
            this.targetDirectory = targetDirectory;
            this.isManual = isManual;
        }
    }
}