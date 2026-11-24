using System.Reflection;
using System.Runtime.Loader;
using Silk.Utilities;

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
            allTasks.Add(new LoadPluginTask(directory));
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
        LoadPluginTask loadTask = new(directory);
        await loadTask.ExecuteAsync(_ => { });
    }

    public delegate IEnumerable<IStartupTask> OnStartupTasksGathered();
}