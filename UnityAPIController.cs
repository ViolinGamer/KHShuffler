using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterGameShuffler;

/// <summary>
/// Unity API integration for Melody of Memory pause/resume control
/// Uses discovered memory addresses for actual Unity gameplay control
/// </summary>
public static class UnityAPIController
{
    #region Windows API Imports

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll")]
    private static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll")]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

    [DllImport("psapi.dll")]
    private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

    #endregion

    #region Constants

    private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

    // Unity gameplay pause address discovered by user
    private const uint UNITY_GAMEPLAY_PAUSE_OFFSET = 0x017A68C8; // "UnityPlayer.dll"+017A68C8
    private const uint UNITY_GAMEPLAY_PAUSE_POINTER = 0xF4;       // Pointer offset

    #endregion

    #region Structures

    [StructLayout(LayoutKind.Sequential)]
    private struct MODULEINFO
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    #endregion

    /// <summary>
    /// Checks if Unity API control is available for the given process
    /// Currently disabled - using thread suspension until music pause address is found
    /// </summary>
    public static bool IsUnityAPIAvailable(Process process)
    {
        // Temporarily disabled - reverting to thread suspension until music pause address is discovered
        Debug.WriteLine($"[UNITY-API] Unity API temporarily disabled - using thread suspension approach");
        return false;
    }

    /// <summary>
    /// Pauses Melody of Memory using thread suspension (temporary approach)
    /// Will switch back to memory address manipulation once music pause address is found
    /// </summary>
    public static bool PauseMelodyOfMemory(Process process)
    {
        // Temporarily using simple approach until music pause address is discovered
        Debug.WriteLine($"[UNITY-API] Using thread suspension approach (temporary)");
        return false; // Let the main program handle thread suspension
    }

    /// <summary>
    /// Resumes Melody of Memory using thread resumption (temporary approach)
    /// Will switch back to memory address manipulation once music pause address is found
    /// </summary>
    public static bool ResumeMelodyOfMemory(Process process)
    {
        // Temporarily using simple approach until music pause address is discovered
        Debug.WriteLine($"[UNITY-API] Using thread resumption approach (temporary)");
        return false; // Let the main program handle thread resumption
    }

    #region Utility Methods

    /// <summary>
    /// Finds the UnityPlayer.dll module in the target process
    /// </summary>
    private static IntPtr FindUnityPlayerModule(IntPtr processHandle)
    {
        try
        {
            const int MAX_MODULES = 1024;
            IntPtr[] modules = new IntPtr[MAX_MODULES];

            if (!EnumProcessModules(processHandle, modules, (uint)(IntPtr.Size * MAX_MODULES), out uint bytesNeeded))
            {
                return IntPtr.Zero;
            }

            int moduleCount = (int)(bytesNeeded / IntPtr.Size);

            for (int i = 0; i < moduleCount; i++)
            {
                var moduleNameBuilder = new StringBuilder(256);
                if (GetModuleBaseName(processHandle, modules[i], moduleNameBuilder, 256) > 0)
                {
                    string moduleName = moduleNameBuilder.ToString().ToLowerInvariant();
                    if (moduleName.Contains("unityplayer"))
                    {
                        Debug.WriteLine($"[UNITY-API] Found UnityPlayer module: {moduleName}");
                        return modules[i];
                    }
                }
            }

            return IntPtr.Zero;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UNITY-API] Error finding UnityPlayer module: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    #endregion
}