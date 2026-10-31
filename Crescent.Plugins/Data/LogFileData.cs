namespace Crescent.Plugins
{
    [Serializable]
    public abstract class LogFileData
    {
        public abstract IEnumerable<LogFile> GetFiles();

        public abstract class LogFile
        {
            public abstract string Name { get; }
            
            public FileStream? Stream { get; set; }
            
            public abstract Task<FileStream> CreateStreamAsync();
            
            public virtual Task CloseStreamAsync()
            {
                Stream?.Close();
                Stream?.Dispose();
                return Task.CompletedTask;
            }
        }
    }
}