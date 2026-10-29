namespace Crescent
{
    public interface IStartupTask
    {
        public string GetName();
        
        public Task ExecuteAsync(Action<string> updateTask);
    }
}