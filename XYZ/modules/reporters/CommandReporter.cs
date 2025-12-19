using System;
using System.Collections.Generic;

namespace XYZ.modules.reporters
{
    public class CommandReporter
    {
        private const string REPORT_TYPE = "command_execution_log";

        public static void ReportReceived(string commandId, string type, string rawData = "")
        {
            var details = new Dictionary<string, object>
            {
                { "limit_id", commandId },
                { "command_type", type },
                { "status", "received" },
                { "raw_data_snippet", rawData.Length > 50 ? rawData.Substring(0, 50) + "..." : rawData }
            };
            TelemetryAggregator.QueueReport(REPORT_TYPE, details);
        }

        public static void ReportExecutionResult(string commandId, string type, bool success, string resultOrError)
        {
            var details = new Dictionary<string, object>
            {
                { "limit_id", commandId },
                { "command_type", type },
                { "status", success ? "completed" : "failed" },
                { "output", resultOrError }
            };
            TelemetryAggregator.QueueReport(REPORT_TYPE, details);
        }
    }
}
