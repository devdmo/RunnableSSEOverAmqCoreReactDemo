using System;
using System.Diagnostics;

namespace MyProject.Services
{
    /// <summary>
    /// LoggerHelper provides static methods to log messages at various levels.
    /// It includes detailed logs and timestamps, similar to the original LoggerWrapper.
    /// </summary>
    public static class LoggerHelper
    {
        private static readonly TraceSource Trace = new TraceSource("LoggerHelper");
        public static bool EnableDebugLogs { get; set; } = true;

        static LoggerHelper()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Switch = new SourceSwitch("LoggerSwitch", "All");
        }

        public static void Debug(string message)
        {
            if (EnableDebugLogs)
                Log(TraceEventType.Verbose, message);
        }

        public static void Info(string message)
        {
            if (EnableDebugLogs)
                Log(TraceEventType.Information, message);
        }

        public static void Warn(string message)
        {
            Log(TraceEventType.Warning, message);
        }

        public static void Error(string message)
        {
            Log(TraceEventType.Error, message);
        }

        public static void Fatal(string message)
        {
            Log(TraceEventType.Critical, message);
        }

        public static void Error(string message, Exception ex)
        {
            Log(TraceEventType.Error, $"{message} | Exception: {ex}");
        }

        public static void Fatal(string message, Exception ex)
        {
            Log(TraceEventType.Critical, $"{message} | Exception: {ex}");
        }

        /// <summary>
        /// Writes a log message with the specified log level and timestamp.
        /// </summary>
        private static void Log(TraceEventType level, string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}";
            Trace.TraceEvent(level, 0, logMessage);
            Trace.Flush();
        }
    }
}
