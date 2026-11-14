using System.Reflection;
using Crescent.Json.Serialization;
using Crescent.Utilities;

namespace Crescent.Plugins
{
    public class LoadPluginTask : IStartupTask
    {
        public Assembly assembly;
        public string directoryPath;

        public LoadPluginTask(Assembly assembly, string directoryPath)
        {
            this.assembly = assembly;
            this.directoryPath = directoryPath;
        }

        public string GetName() => GetType().FullName + $"+{assembly.GetName().Name}";

        public async Task ExecuteAsync(Action<string> updateTask)
        {
            updateTask.Invoke($"Loading {assembly.GetName().Name}...");

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

            await assembly.InitialiseOnLoadTypesAsync();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAssignableTo(typeof(PluginData)) && !type.IsAbstract)
                {
                    PluginData? pluginData = Activator.CreateInstance(type) as PluginData;
                    if (pluginData == null) throw new NullReferenceException($"PluginData of type \"{type.Name}\" could not be instantiated!");

                    updateTask.Invoke($"Loading {pluginData.Name}...");

                    if (!Directory.Exists(Runtime.GamesPath)) Directory.CreateDirectory(Runtime.GamesPath);
                    string dataName = pluginData.Name.ToLower().Replace(" ", "");
                    string folderPath = Path.Combine(Runtime.GamesPath, pluginData.Name);
                    try
                    {
                        string filePath = Path.Combine(folderPath, dataName + ".sav");
                        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                        if (File.Exists(filePath)) pluginData = await FileManager.ReadJsonAsync(type, filePath) as PluginData;
                        else await FileManager.WriteJsonAsync(filePath, (dynamic)pluginData);

                        if (pluginData != null)
                        {
                            if (manifest != null && !string.IsNullOrEmpty(manifest.version))
                            {
                                pluginData.version = manifest.version;
                                pluginData.folderName = Path.GetFileName(directoryPath);
                            }

                            PluginData.InstalledPlugins.RemoveAll(existing => string.Equals(existing.Name, pluginData.Name, StringComparison.OrdinalIgnoreCase));
                            PluginData.InstalledPlugins.Add(pluginData);
                            pluginData.Load();
                            updateTask.Invoke($"Loaded {pluginData.Name}!");
                        }
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception.ToString());
                    }

                    await Task.Delay(250);
                }
            }
        }
    }
}