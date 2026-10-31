using System.Reflection;
using Crescent.Plugins;
using Crescent.Utilities;
using Crescent.Json.Serialization;

namespace Crescent.Plugins
{
    public class LoadPluginTask : IStartupTask
    {
        public Assembly assembly;

        public LoadPluginTask(Assembly assembly) => this.assembly = assembly;

        public string GetName() => GetType().FullName + $"+{assembly.GetName().Name}";

        public async Task ExecuteAsync(Action<string> updateTask)
        {
            updateTask.Invoke($"Loading {assembly.GetName().Name}...");

            await assembly.InitialiseOnLoadTypesAsync();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAssignableTo(typeof(GameData)) && !type.IsAbstract)
                {
                    GameData? gameData = Activator.CreateInstance(type) as GameData;
                    if (gameData == null) throw new NullReferenceException($"GameData of type \"{type?.Name}\" could not be instantiated! Ensure there is a default constructor available");
                    updateTask.Invoke($"Loading {gameData.Name}...");

                    if (!Directory.Exists(Runtime.GamesPath)) Directory.CreateDirectory(Runtime.GamesPath);
                    string dataName = gameData.Name.ToLower().Replace(" ", "");
                    string filePath = Path.Combine(Runtime.GamesPath, gameData.Name, dataName + ".sav");
                    try
                    {
                        if (File.Exists(filePath)) gameData = await FileManager.ReadJson(type, filePath) as GameData;
                        else await FileManager.WriteJson(filePath, (dynamic)gameData);

                        if (gameData != null)
                        {
                            updateTask.Invoke($"Loaded {gameData.Name}!");
                            GameData.InstalledGames.Add(gameData);
                            gameData.Load();
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