using System.Reflection;
using Crescent.Plugins;
using Crescent.Utilities;
using Crescent.Json.Serialization;

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
            Logger.Log($"Loading plugin from {jsonPath}...");
            if (File.Exists(jsonPath))
            {
                try
                {
                    manifest = await FileManager.ReadJson<PluginManifest>(jsonPath);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            }

            await assembly.InitialiseOnLoadTypesAsync();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAssignableTo(typeof(PluginData)) && !type.IsAbstract)
                {
                    PluginData? pluginData = Activator.CreateInstance(type) as PluginData;
                    if (pluginData == null) throw new NullReferenceException($"PluginData of type \"{type?.Name}\" could not be instantiated!");

                    updateTask.Invoke($"Loading {pluginData.Name}...");

                    if (!Directory.Exists(Runtime.GamesPath)) Directory.CreateDirectory(Runtime.GamesPath);
                    string dataName = pluginData.Name.ToLower().Replace(" ", "");
                    string filePath = Path.Combine(Runtime.GamesPath, pluginData.Name, dataName + ".sav");
                    try
                    {
                        if (File.Exists(filePath)) pluginData = await FileManager.ReadJson(type, filePath) as PluginData;
                        else await FileManager.WriteJson(filePath, (dynamic)pluginData);

                        if (pluginData != null)
                        {
                            if (manifest != null && !string.IsNullOrEmpty(manifest.version)) pluginData.version = manifest.version;
                            updateTask.Invoke($"Loaded {pluginData.Name}!");
                            
                            if (manifest != null && !string.IsNullOrEmpty(manifest.version))
                            {
                                pluginData.version = manifest.version;
                                pluginData.folderName = directoryPath.Split("\\").Last();
                            }
                            
                            PluginData.InstalledPlugins.Add(pluginData);
                            pluginData.Load();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.ToString());
                    }

                    await Task.Delay(250);
                }
            }
        }
    }
}