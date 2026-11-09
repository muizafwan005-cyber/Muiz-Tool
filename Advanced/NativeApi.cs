using System;
using System.Runtime.InteropServices;

namespace RFTool.Advanced;

/// <summary>Pemanggilan API Windows untuk membaca/menulis memori process lain.</summary>
internal static class NativeApi
{
    public const uint PROCESS_VM_OPERATION = 0x0008;
    public const uint PROCESS_VM_READ = 0x0010;
    public const uint PROCESS_VM_WRITE = 0x0020;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;

    public const uint MEM_COMMIT = 0x1000;
    public const uint MEM_RESERVE = 0x2000;
    public const uint MEM_FREE = 0x10000;
    public const uint MEM_PRIVATE = 0x20000;
    public const uint MEM_MAPPED = 0x40000;
    public const uint MEM_IMAGE = 0x1000000;
    public const uint MEM_RELEASE = 0x8000;

    public const uint PAGE_NOACCESS = 0x01;
    public const uint PAGE_READONLY = 0x02;
    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_WRITECOPY = 0x08;
    public const uint PAGE_EXECUTE = 0x10;
    public const uint PAGE_EXECUTE_READ = 0x20;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;
    public const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    public const uint PAGE_GUARD = 0x100;

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public UIntPtr BaseAddress;
        public UIntPtr AllocationBase;
        public uint AllocationProtect;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(
        IntPtr process, UIntPtr address, byte[] buffer, UIntPtr size, out UIntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(
        IntPtr process, UIntPtr address, byte[] buffer, UIntPtr size, out UIntPtr bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr VirtualQueryEx(
        IntPtr process, UIntPtr address, out MEMORY_BASIC_INFORMATION info, UIntPtr length);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualProtectEx(
        IntPtr process, UIntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr VirtualAllocEx(
        IntPtr process, UIntPtr address, UIntPtr size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFreeEx(
        IntPtr process, UIntPtr address, UIntPtr size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool IsWow64Process(IntPtr process, out bool wow64);
}
