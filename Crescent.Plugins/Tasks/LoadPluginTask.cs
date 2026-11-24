using System.Reflection;
using Silk.Utilities;

namespace Crescent.Plugins
{
    public class LoadPluginTask : IStartupTask
    {
        public string directoryPath;

        public LoadPluginTask(string directoryPath) => this.directoryPath = directoryPath;

        public string GetName() => GetType().FullName + $"+{Path.GetFileName(directoryPath)}";

        public async Task ExecuteAsync(Action<string> updateTask)
        {
            string folderName = Path.GetFileName(directoryPath);
            updateTask.Invoke($"Loading {folderName}...");

            PluginManifest? manifest = null;
            string jsonPath = Path.Combine(directoryPath, "plugin.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    manifest = await FileManager.ReadJsonAsync<PluginManifest>(jsonPath);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
            }

            string pluginName = manifest != null && !string.IsNullOrEmpty(manifest.name) ? manifest.name : folderName;
            string version = manifest != null && !string.IsNullOrEmpty(manifest.version) ? manifest.version : "0.0.0";
            bool loadedAny = false;

            try
            {
                string[] assemblyPaths = Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly);
                for (int index = 0; index < assemblyPaths.Length; index++)
                {
                    Assembly assembly = PluginStartupProcess.LoadAssembly(assemblyPaths[index]);
                    await assembly.InitialiseOnLoadTypesAsync();

                    foreach (Type type in assembly.GetTypes())
                    {
                        if (type.IsAssignableTo(typeof(PluginData)) && !type.IsAbstract && type != typeof(BrokenPluginData))
                        {
                            PluginData? pluginData = Activator.CreateInstance(type) as PluginData;
                            if (pluginData == null) throw new NullReferenceException($"PluginData of type \"{type.Name}\" could not be instantiated!");

                            updateTask.Invoke($"Loading {pluginData.Name}...");

                            if (!Directory.Exists(Runtime.GamesPath)) Directory.CreateDirectory(Runtime.GamesPath);
                            string dataName = pluginData.Name.ToLower().Replace(" ", "");
                            string folderPath = Path.Combine(Runtime.GamesPath, pluginData.Name);
                            string filePath = Path.Combine(folderPath, dataName + ".sav");
                            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                            if (File.Exists(filePath)) pluginData = await FileManager.ReadJsonAsync(type, filePath) as PluginData;
                            else await FileManager.WriteJsonAsync(filePath, (dynamic)pluginData);
                            if (pluginData != null)
                            {
                                pluginData.version = version;
                                pluginData.folderName = folderName;

                                PluginData.InstalledPlugins.RemoveAll(existing => string.Equals(existing.Name, pluginData.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(existing.Name, pluginName, StringComparison.OrdinalIgnoreCase) || string.Equals(existing.folderName, folderName, StringComparison.OrdinalIgnoreCase));
                                PluginData.InstalledPlugins.Add(pluginData);
                                pluginData.Load();
                                loadedAny = true;
                                updateTask.Invoke($"Loaded {pluginData.Name}!");
                            }

                            await Task.Delay(250);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }

            if (!loadedAny)
            {
                BrokenPluginData brokenPlugin = new(pluginName, version, folderName);
                PluginData.InstalledPlugins.RemoveAll(existing => string.Equals(existing.Name, pluginName, StringComparison.OrdinalIgnoreCase) || string.Equals(existing.folderName, folderName, StringComparison.OrdinalIgnoreCase));
                PluginData.InstalledPlugins.Add(brokenPlugin);
                PluginData.ShowToast("Plugin Failed", $"Failed to load {pluginName} ({version})", "Error", 4000, "Library");
            }
        }
    }
}