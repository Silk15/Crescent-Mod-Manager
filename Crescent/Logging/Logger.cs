using System.Diagnostics;
using System.Reflection;

namespace Crescent
{
    public static class Logger
    {
        public const string Latest = "crecent.log";
        public const string Prev = "crecent-prev.log";
        private static readonly TextWriter OriginalConsoleOut;
        private static readonly StreamWriter LogWriter;

        static Logger()
        {
            string logDirectory = Runtime.LogPath;
            if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory);

            string latestPath = Path.Combine(logDirectory, Latest);
            string prevPath = Path.Combine(logDirectory, Prev);

            if (File.Exists(latestPath)) File.Move(latestPath, prevPath, true);
            LogWriter = new StreamWriter(latestPath, true)
            {
                AutoFlush = true
            };

            OriginalConsoleOut = Console.Out;
            LogInterceptor interceptor = new(OriginalConsoleOut, text => LogWriter.Write(text));
            Console.SetOut(interceptor);
        }

        private static string CallerMemberName
        {
            get
            {
                StackTrace stackTrace = new();
                StackFrame[] stackFrames = stackTrace.GetFrames();

                StackFrame callingFrame = stackFrames[2];
                MethodBase? method = callingFrame.GetMethod();

                return $"{method?.DeclaringType?.FullName}.{method?.Name}";
            }
        }

        public static void Log(string message, bool extraInfo = true)
        {
            string logMessage = extraInfo ? $"{DateTime.Now} INFO {CallerMemberName} " + message : message;
            Write(logMessage);
        }

        public static void Warn(string message, bool extraInfo = true)
        {
            string logMessage = extraInfo ? $"{DateTime.Now} WARN {CallerMemberName} " + message : message;
            Write(logMessage);
        }

        public static void Error(string message, bool extraInfo = true)
        {
            string logMessage = extraInfo ? $"{DateTime.Now} FATAL {CallerMemberName} " + message : message;
            Write(logMessage);
        }

        private static void Write(string logMessage)
        {
            LogWriter.WriteLine(logMessage);
            OriginalConsoleOut.WriteLine(logMessage);
        }
    }
}