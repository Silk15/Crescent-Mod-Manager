using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Crescent.Utilities;
using JetBrains.Annotations;

namespace Crescent
{
    public static class Runtime
    {
        public const string Version = "v1.3.4";
        public static readonly string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "Crescent");

        public static readonly string PluginPath = Environment.ProcessPath.Replace("Managed\\Crescent.exe", "Plugins");
        public static readonly string ManagedPath = Environment.ProcessPath.Replace("Crescent.exe", "");

        public static readonly string GamesPath = Path.Combine(DataPath, "Games");
        public static readonly string LogPath = Path.Combine(DataPath, "Logs");

        public static readonly Dictionary<string, string> AssemblyPath = new(StringComparer.OrdinalIgnoreCase);

        [PublicAPI]
        public static readonly List<IStartupProcess> StartupProcesses = new();

        public static event Action<string[]>? onCrescentLoaded;
        public static event Action onClosed;
        
        public static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0].StartsWith("nxm://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using System.IO.Pipes.NamedPipeClientStream client = new(".", "Crescent_NXM_Pipe", System.IO.Pipes.PipeDirection.Out);
                    client.Connect(400);
                    byte[] payload = Encoding.UTF8.GetBytes(args[0]);
                    client.Write(payload, 0, payload.Length);
                    client.Flush();
                    return;
                }
                catch { }
            }

            Logger.Log($"Exe Path = \"{Environment.ProcessPath}\"", false);
            Logger.Log($"Data Path = \"{DataPath}\"", false);
            await LoadAssembliesAsync();
            try
            {
                onCrescentLoaded?.Invoke(args);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
            }

            Logger.Log("Crescent runtime started!");
            AppDomain.CurrentDomain.ProcessExit += (_, _) => onClosed?.Invoke();
            await Task.Delay(-1);
        }

        public static void Exit(int exitCode = 0) => Environment.Exit(exitCode);

        #region Assembly Loading

        public static void GetAssemblyPath()
        {
            int nativeBinaries = 0;
            foreach (string file in Directory.GetFiles(ManagedPath, "*.dll"))
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.IsNativeBinary())
                    {
                        nativeBinaries++;
                        continue;
                    }

                    AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);
                    if (assemblyName.Name != null) AssemblyPath[assemblyName.Name] = file;
                }
                catch (BadImageFormatException) { }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to read assembly name for {file}!\n{ex}");
                }
            }

            Logger.Log($"- Environment\n" +
                       $"     Managed: {AssemblyPath.Count}\n" +
                       $"     Native: {nativeBinaries}", false);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                AssemblyName requestedAssembly = new(args.Name);
                if (requestedAssembly.Name != null && AssemblyPath.TryGetValue(requestedAssembly.Name, out string? path)) return Assembly.LoadFrom(path);
                return null;
            };
        }

        public static async Task LoadAssembliesAsync()
        {
            Logger.Log($"Crescent Mod Manager ({Version})", false);
            try
            {
                LoadNativeDependencies();
                GetAssemblyPath();
                string[] files = Directory.GetFiles(ManagedPath, "Crescent.*.dll");
                Logger.Log($"- Core ({files.Length})", false);
                foreach (string file in files)
                {
                    try
                    {
                        Logger.Log($"     {Path.GetFileName(file)}", false);
                        Assembly assembly = Assembly.LoadFrom(file);
                        await assembly.InitialiseOnLoadTypesAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to load {file}!\n{ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load assemblies!\n{ex}");
            }
        }

        #endregion

        #region Native Loading

        public static void LoadNativeDependencies()
        {
            Logger.Log("- Native Binaries", false);
            string nativeDirectory = GetNativeDirectory();
            if (Directory.Exists(nativeDirectory))
                foreach (string file in Directory.GetFiles(nativeDirectory))
                {
                    if (file.EndsWith(".pdb")) continue;
                    try
                    {
                        NativeLibrary.Load(file);
                        Logger.Log($"     {file.Split("\\").Last()}", false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to load {file}!\n{ex}");
                    }
                }
        }

        public static string GetNativeDirectory()
        {
            string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "osx" : "unknown";
            string architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => "x64"
            };

            string rid = $"{os}-{architecture}";
            return Path.Combine(ManagedPath, "runtimes", rid, "native");
        }

        #endregion
        
        #region Loading

        public static async Task LoadCrescentAsync(Action<string> onProcessUpdated, Action<string> onTaskUpdated)
        {
            StringBuilder processBuilder = new();
            processBuilder.AppendLine("- Startup Processes");
            foreach (IStartupProcess startupProcess in StartupProcesses)
            {
                try
                {
                    processBuilder.AppendLine($"   - {startupProcess.GetType().Name}");
                    IEnumerable<IStartupTask> startupTasks = startupProcess.GetTasks();
                    int total = startupTasks.Count();
                    int completed = 0;

                    onProcessUpdated?.Invoke(startupProcess.GetTitle(completed, total));
                    foreach (IStartupTask startupTask in startupTasks)
                    {
                        processBuilder.AppendLine($"        {startupTask.GetName()}");
                        try
                        {
                            completed++;
                            await startupTask.ExecuteAsync(onTaskUpdated);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.ToString());
                        }

                        onProcessUpdated?.Invoke(startupProcess.GetTitle(completed, total));
                    }

                    onProcessUpdated?.Invoke(startupProcess.GetTitle(completed, total));
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to run startup process {startupProcess.GetType().FullName}!\n{ex}");
                }
            }
            
            Logger.Log(processBuilder.ToString(), false);
        }

        #endregion
    }
}