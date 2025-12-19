using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace XYZ.modules
{
    /// <summary>
    /// Sistema avançado de detecção de ambientes de análise, VMs, sandboxes e debuggers
    /// </summary>
    public static class AdvancedAntiAnalysis
    {
        private static int? cachedSuspicionScore = null;
        private static readonly object cacheLock = new object();
        private static DateTime lastCheck = DateTime.MinValue;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(10);

        // Threshold de suspeita (0-100)
        private const int SUSPICION_THRESHOLD = 40;

        /// <summary>
        /// Verifica se o ambiente é suspeito de ser análise/sandbox
        /// </summary>
        public static bool IsRunningInAnalysisEnvironment()
        {
            lock (cacheLock)
            {
                // Usa cache se ainda válido
                if (cachedSuspicionScore.HasValue && 
                    (DateTime.UtcNow - lastCheck) < CACHE_DURATION)
                {
                    return cachedSuspicionScore.Value > SUSPICION_THRESHOLD;
                }

                // Calcula score de suspeita
                int score = CalculateSuspicionScore();
                cachedSuspicionScore = score;
                lastCheck = DateTime.UtcNow;

                SecureLogger.LogInfo("AntiAnalysis", string.Format("Suspicion score: {0}/100", score));

                return score > SUSPICION_THRESHOLD;
            }
        }

        /// <summary>
        /// Calcula score de suspeita baseado em múltiplos indicadores
        /// </summary>
        private static int CalculateSuspicionScore()
        {
            int score = 0;

            try
            {
                // 1. Verificações de Hardware (30 pontos máximo)
                score += CheckHardware();

                // 2. Verificações de VM (30 pontos máximo)
                score += CheckVirtualizationArtifacts();

                // 3. Verificações de Debugger (20 pontos máximo)
                score += CheckDebuggers();

                // 4. Verificações de Timing (10 pontos máximo)
                score += CheckTimingAnomalies();

                // 5. Verificações de Comportamento do Usuário (10 pontos máximo)
                score += CheckUserBehavior();

                SecureLogger.LogDebug("AntiAnalysis", string.Format("Final suspicion score: {0}", score), new Dictionary<string, object>
                {
                    { "Hardware", CheckHardware() },
                    { "VM", CheckVirtualizationArtifacts() },
                    { "Debugger", CheckDebuggers() },
                    { "Timing", CheckTimingAnomalies() },
                    { "UserBehavior", CheckUserBehavior() }
                });
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AntiAnalysis", ex);
            }

            return Math.Min(score, 100); // Máximo 100
        }

        #region Hardware Checks

        /// <summary>
        /// Verifica características de hardware suspeitas
        /// </summary>
        private static int CheckHardware()
        {
            int score = 0;

            try
            {
                // CPU cores muito baixo
                int cpuCount = Environment.ProcessorCount;
                if (cpuCount < 2)
                    score += 10;
                else if (cpuCount == 2)
                    score += 5;

                // RAM muito baixa
                long ramMB = GetTotalRAM();
                if (ramMB > 0 && ramMB < 2048)
                    score += 10;
                else if (ramMB > 0 && ramMB < 4096)
                    score += 5;

                // Disco muito pequeno
                long diskGB = GetTotalDiskSpace();
                if (diskGB > 0 && diskGB < 60)
                    score += 10;
                else if (diskGB > 0 && diskGB < 100)
                    score += 5;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AntiAnalysis.Hardware", ex);
            }

            return score;
        }

        /// <summary>
        /// Obtém RAM total em MB
        /// </summary>
        private static long GetTotalRAM()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        long bytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                        return bytes / (1024 * 1024); // MB
                    }
                }
            }
            catch
            {
                return -1;
            }
            return -1;
        }

        /// <summary>
        /// Obtém espaço total de disco em GB
        /// </summary>
        private static long GetTotalDiskSpace()
        {
            try
            {
                DriveInfo systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                return systemDrive.TotalSize / (1024 * 1024 * 1024); // GB
            }
            catch
            {
                return -1;
            }
        }

        #endregion

        #region Virtualization Checks

        /// <summary>
        /// Verifica artefatos de virtualização
        /// </summary>
        private static int CheckVirtualizationArtifacts()
        {
            int score = 0;

            try
            {
                // VMWare
                if (CheckVMWare()) score += 20;

                // VirtualBox
                if (CheckVirtualBox()) score += 20;

                // Hyper-V
                if (CheckHyperV()) score += 20;

                // QEMU/KVM
                if (CheckQEMU()) score += 20;

                // Parallels
                if (CheckParallels()) score += 20;

                // Generic VM checks
                if (CheckGenericVM()) score += 10;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AntiAnalysis.VM", ex);
            }

            return Math.Min(score, 30); // Máximo 30 pontos
        }

        private static bool CheckVMWare()
        {
            try
            {
                // Verifica processos VMWare
                string[] vmwareProcesses = { "vmtoolsd", "vmwaretray", "vmwareuser" };
                Process[] processes = Process.GetProcesses();
                
                foreach (var proc in processes)
                {
                    try
                    {
                        string name = proc.ProcessName.ToLower();
                        if (vmwareProcesses.Any(vm => name.Contains(vm)))
                        {
                            SecureLogger.LogWarning("AntiAnalysis", "VMWare process detected");
                            return true;
                        }
                    }
                    catch { }
                }

                // Verifica drivers VMWare
                string[] vmwareDrivers = {
                    @"C:\Windows\System32\drivers\vmmouse.sys",
                    @"C:\Windows\System32\drivers\vmhgfs.sys",
                    @"C:\Windows\System32\drivers\vmxnet.sys"
                };

                foreach (var driver in vmwareDrivers)
                {
                    if (File.Exists(driver))
                    {
                        SecureLogger.LogWarning("AntiAnalysis", string.Format("VMWare driver found: {0}", driver));
                        return true;
                    }
                }

                // Verifica registry
                // Nota: Requer permissões adequadas
            }
            catch { }

            return false;
        }

        private static bool CheckVirtualBox()
        {
            try
            {
                // Processos VirtualBox
                string[] vboxProcesses = { "vboxservice", "vboxtray" };
                Process[] processes = Process.GetProcesses();
                
                foreach (var proc in processes)
                {
                    try
                    {
                        string name = proc.ProcessName.ToLower();
                        if (vboxProcesses.Any(vb => name.Contains(vb)))
                        {
                            SecureLogger.LogWarning("AntiAnalysis", "VirtualBox process detected");
                            return true;
                        }
                    }
                    catch { }
                }

                // Drivers VirtualBox
                string[] vboxDrivers = {
                    @"C:\Windows\System32\drivers\VBoxMouse.sys",
                    @"C:\Windows\System32\drivers\VBoxGuest.sys",
                    @"C:\Windows\System32\drivers\VBoxSF.sys"
                };

                foreach (var driver in vboxDrivers)
                {
                    if (File.Exists(driver))
                    {
                        SecureLogger.LogWarning("AntiAnalysis", string.Format("VirtualBox driver found: {0}", driver));
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool CheckHyperV()
        {
            try
            {
                // Verifica WMI para Hyper-V
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        object manufacturerObj = obj["Manufacturer"];
                        object modelObj = obj["Model"];
                        string manufacturer = manufacturerObj != null ? manufacturerObj.ToString().ToLower() : "";
                        string model = modelObj != null ? modelObj.ToString().ToLower() : "";

                        if (manufacturer.Contains("microsoft") && model.Contains("virtual"))
                        {
                            SecureLogger.LogWarning("AntiAnalysis", "Hyper-V detected via WMI");
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool CheckQEMU()
        {
            try
            {
                // Verifica BIOS Info para QEMU
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        object versionObj = obj["Version"];
                        string version = versionObj != null ? versionObj.ToString().ToLower() : "";
                        if (version.Contains("qemu") || version.Contains("bochs"))
                        {
                            SecureLogger.LogWarning("AntiAnalysis", "QEMU/Bochs detected");
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool CheckParallels()
        {
            try
            {
                string[] parallelsDrivers = {
                    @"C:\Windows\System32\drivers\prleth.sys",
                    @"C:\Windows\System32\drivers\prlfs.sys",
                    @"C:\Windows\System32\drivers\prlmouse.sys"
                };

                foreach (var driver in parallelsDrivers)
                {
                    if (File.Exists(driver))
                    {
                        SecureLogger.LogWarning("AntiAnalysis", string.Format("Parallels driver found: {0}", driver));
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool CheckGenericVM()
        {
            try
            {
                // Verifica MAC address suspeitos
                // VMs geralmente têm prefixos específicos
                // 00:05:69 (VMware), 08:00:27 (VirtualBox), etc.
                
                // Por simplicidade, não implementado aqui
                // mas seria feito via NetworkInterface.GetAllNetworkInterfaces()
            }
            catch { }

            return false;
        }

        #endregion

        #region Debugger Checks

        /// <summary>
        /// Verifica presença de debuggers
        /// </summary>
        private static int CheckDebuggers()
        {
            int score = 0;

            try
            {
                // Debugger presente
                if (IsDebuggerPresent())
                {
                    score += 30;
                    SecureLogger.LogWarning("AntiAnalysis", "Debugger detected (IsDebuggerPresent)");
                }

                // Remote debugger
                bool isRemoteDebugger = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isRemoteDebugger);
                if (isRemoteDebugger)
                {
                    score += 30;
                    SecureLogger.LogWarning("AntiAnalysis", "Remote debugger detected");
                }

                // Processos de debugging conhecidos
                string[] debuggerProcesses = {
                    "ollydbg", "x32dbg", "x64dbg", "ida", "ida64", "windbg",
                    "immunity", "pestudio", "procmon", "procexp", "tcpview"
                };

                Process[] processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        string name = proc.ProcessName.ToLower();
                        if (debuggerProcesses.Any(dbg => name.Contains(dbg)))
                        {
                            score += 20;
                            SecureLogger.LogWarning("AntiAnalysis", string.Format("Debugger process detected: {0}", name));
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AntiAnalysis.Debugger", ex);
            }

            return Math.Min(score, 20); // Máximo 20 pontos
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        #endregion

        #region Timing Checks

        /// <summary>
        /// Detecta anomalias de timing (comum em sandboxes)
        /// </summary>
        private static int CheckTimingAnomalies()
        {
            int score = 0;

            try
            {
                // Sleep acceleration test
                Stopwatch sw = Stopwatch.StartNew();
                Thread.Sleep(500);
                sw.Stop();

                // Se o sleep foi muito mais rápido, pode ser sandbox
                if (sw.ElapsedMilliseconds < 450)
                {
                    score += 10;
                    SecureLogger.LogWarning("AntiAnalysis", string.Format("Sleep acceleration detected: {0}ms", sw.ElapsedMilliseconds));
                }

                // RDTSC check (mais avançado, não implementado aqui)
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AntiAnalysis.Timing", ex);
            }

            return score;
        }

        #endregion

        #region User Behavior Checks

        /// <summary>
        /// Verifica comportamento genuíno de usuário
        /// </summary>
        private static int CheckUserBehavior()
        {
            int score = 0;

            try
            {
                // Verifica uptime do sistema
                TimeSpan uptime = GetSystemUptime();
                if (uptime.TotalMinutes < 10)
                {
                    score += 5;
                    SecureLogger.LogWarning("AntiAnalysis", string.Format("Low system uptime: {0} minutes", uptime.TotalMinutes));
                }

                // Verifica número de processos em execução
                int processCount = Process.GetProcesses().Length;
                if (processCount < 30)
                {
                    score += 5;
                    SecureLogger.LogWarning("AntiAnalysis", string.Format("Low process count: {0}", processCount));
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AntiAnalysis.UserBehavior", ex);
            }

            return score;
        }

        /// <summary>
        /// Obtém uptime do sistema
        /// </summary>
        private static TimeSpan GetSystemUptime()
        {
            try
            {
                using (var uptime = new PerformanceCounter("System", "System Up Time"))
                {
                    uptime.NextValue();
                    return TimeSpan.FromSeconds(uptime.NextValue());
                }
            }
            catch
            {
                return TimeSpan.FromHours(1); // Default safe value
            }
        }

        #endregion

        /// <summary>
        /// Limpa cache forçando nova verificação
        /// </summary>
        public static void ClearCache()
        {
            lock (cacheLock)
            {
                cachedSuspicionScore = null;
                lastCheck = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Obtém score de suspeita atual sem cache
        /// </summary>
        public static int GetCurrentSuspicionScore()
        {
            return CalculateSuspicionScore();
        }
    }
}
