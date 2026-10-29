using System.Text;

namespace Crescent
{
    public class LogInterceptor : TextWriter
    {
        public Action<string> onMessageCaptured;
        public TextWriter originalOutput;

        public LogInterceptor(TextWriter originalOutput, Action<string> onMessageCaptured)
        {
            this.originalOutput = originalOutput;
            this.onMessageCaptured = onMessageCaptured;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            originalOutput.WriteLine(value);
            if (value != null) onMessageCaptured(value);
        }
    }
}