namespace Crescent
{
    public interface IStartupProcess
    {
        public string GetTitle(int completedTasks, int totalTasks);

        public IEnumerable<IStartupTask> GetTasks();
    }
}