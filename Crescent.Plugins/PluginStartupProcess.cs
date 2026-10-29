using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Crescent.Utilities;

namespace Crescent.Plugins;

public class PluginStartupProcess : IStartupProcess
{
    [InitialiseOnLoad]
    public static void Initialise() => Runtime.StartupProcesses.Add(new PluginStartupProcess());
    
    public string GetTitle(int completedTasks, int totalTasks)  => $"Loading Game Plugin{totalTasks.AsPlural()}... ({completedTasks}/{totalTasks})";

    public IEnumerable<IStartupTask> GetTasks()
    {
        List<IStartupTask> allTasks = new();
        Logger.Log("- Configured Games", false);
        
        string[] directories = Directory.GetDirectories(Runtime.PluginPath);
        for (int i = 0; i < directories.Length; i++)
        {
            string directory = directories[i];
            try
            {
                string split = directory.Split("\\").Last();
                string assemblyPath = directory + $"\\{split}.dll";

                Logger.Log($"   - {split}", false);
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                allTasks.Add(new LoadPluginTask(assembly));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        return allTasks;
    }

    public delegate IEnumerable<IStartupTask> OnStartupTasksGathered();
}