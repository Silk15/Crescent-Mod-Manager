using System.Reflection;
using System.Runtime.Loader;
using Crescent.Utilities;

namespace Crescent.Plugins;

public class PluginStartupProcess : IStartupProcess
{
    [InitialiseOnLoad]
    public static void Initialise() => Runtime.StartupProcesses.Add(new PluginStartupProcess());

    public string GetTitle(int completedTasks, int totalTasks) => $"Loading Game Plugin{totalTasks.WithPlural()}... ({completedTasks}/{totalTasks})";

    public IEnumerable<IStartupTask> GetTasks()
    {
        List<IStartupTask> allTasks = new();
        Logger.Log("- Configured Games", false);

        if (!Directory.Exists(Runtime.PluginPath))
            Directory.CreateDirectory(Runtime.PluginPath);

        string[] directories = Directory.GetDirectories(Runtime.PluginPath);
        for (int index = 0; index < directories.Length; index++)
        {
            string directory = directories[index];
            string folderName = Path.GetFileName(directory);
            Logger.Log($"   - {folderName}", false);

            foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    Assembly assembly = LoadAssembly(assemblyPath);
                    allTasks.Add(new LoadPluginTask(assembly, directory));
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.ToString());
                }
            }
        }

        return allTasks;
    }

    public static Assembly LoadAssembly(string assemblyPath)
    {
        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        MemoryStream stream = new(assemblyBytes);
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }

    public static async Task LoadPluginAsync(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                Assembly assembly = LoadAssembly(assemblyPath);
                LoadPluginTask loadTask = new(assembly, directory);
                await loadTask.ExecuteAsync(_ => { });
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
        }
    }

    public delegate IEnumerable<IStartupTask> OnStartupTasksGathered();
}