namespace Crescent.Plugins
{
    public class BrokenPluginData : PluginData
    {
        public string pluginName;

        public override string Name => pluginName;
        public override string DeploymentPath { get; set; } = "";
        public override string StagingPath { get; set; } = "";
        public override string ExePath { get; set; } = "";
        public override API API => null!;

        public BrokenPluginData(string pluginName, string version, string folderName)
        {
            this.pluginName = pluginName;
            this.version = version;
            this.folderName = folderName;
        }

        public override Task SaveAsync() => Task.CompletedTask;
    }
}