using Crescent.Utilities;

namespace Crescent.APIs;

public class ApiStartupProcess : IStartupProcess
{
    public static event OnStartupTasksGathered onStartupTasksGathered;
    
    [InitialiseOnLoad]
    public static void Initialise() => Runtime.StartupProcesses.Add(new ApiStartupProcess());

    public string GetTitle(int completedTasks, int totalTasks)  => $"Loading API Request{totalTasks.AsPlural()}... ({completedTasks}/{totalTasks})";

    public IEnumerable<IStartupTask> GetTasks()
    {
        List<IStartupTask> allTasks = new();
        foreach (Delegate task in onStartupTasksGathered.GetInvocationList())
        {
            if (task is not OnStartupTasksGathered startupTaskDelegate) continue;
            try
            {
                IEnumerable<IStartupTask> tasks = startupTaskDelegate.Invoke();
                allTasks.AddRange(tasks);
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