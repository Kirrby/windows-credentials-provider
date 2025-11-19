namespace WindowsCredentialProviderTest
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;

    public static class Log
    {
        public static void LogText(string text)
        {
            var config = CredentialProviderConfig.LoadConfig();

            // 如果禁用日志，则直接返回
            if (!config.EnableLogging)
                return;

            try
            {
                // Create a timestamp for the log entry
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] {text}" + Environment.NewLine;

                // Write to log file with proper encoding
                using (var fileStream = new FileStream(config.LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    writer.Write(logEntry);
                }
            }
            catch (Exception ex)
            {
                // If file logging fails, try alternative methods
                try
                {
                    Console.WriteLine($"LOG ERROR: {ex.Message}");
                }
                catch
                {
                    // Last resort: try Windows debugging output
                    Debug.WriteLine($"LOG ERROR: {ex.Message}");
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogMethodCall()
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            var methodBase = sf.GetMethod();
            LogText(methodBase.DeclaringType?.Name + "::" + methodBase.Name);
        }
    }
}
