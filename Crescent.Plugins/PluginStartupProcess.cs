using System.Reflection;
using System.Runtime.Loader;
using Crescent.Utilities;

namespace Crescent.Plugins;

public class PluginStartupProcess : IStartupProcess
{
    [InitialiseOnLoad]
    public static void Initialise() => Runtime.StartupProcesses.Add(new PluginStartupProcess());
    
    public string GetTitle(int completedTasks, int totalTasks)  => $"Loading Game Plugin{totalTasks.WithPlural()}... ({completedTasks}/{totalTasks})";

    public IEnumerable<IStartupTask> GetTasks()
    {
        List<IStartupTask> allTasks = new();
        Logger.Log("- Configured Games", false);
        
        string[] directories = Directory.GetDirectories(Runtime.PluginPath);
        for (int i = 0; i < directories.Length; i++)
        {
            string directory = directories[i];
            string split = directory.Split("\\").Last();
            Logger.Log($"   - {split}", false);

            foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                Logger.Log($"Loading {assemblyPath}");
                try
                {
                    Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                    allTasks.Add(new LoadPluginTask(assembly, directory));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                }
            }
        }

        return allTasks;
    }

    public delegate IEnumerable<IStartupTask> OnStartupTasksGathered();
}