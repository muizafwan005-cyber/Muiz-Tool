using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RFTool.Core;

public sealed class MemoryAccess : IDisposable
{
    public Process Process { get; }

    public IntPtr Handle { get; private set; }

    public IntPtr ModuleBase { get; }

    private const uint PROCESS_VM_READ =
        0x0010;

    private const uint PROCESS_VM_WRITE =
        0x0020;

    private const uint PROCESS_VM_OPERATION =
        0x0008;

    private const uint PROCESS_QUERY_INFORMATION =
        0x0400;


    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint access,
        bool inherit,
        int processId);


    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern bool CloseHandle(
        IntPtr handle);


    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr handle,
        IntPtr address,
        byte[] buffer,
        int size,
        out IntPtr read);


    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr handle,
        IntPtr address,
        byte[] buffer,
        int size,
        out IntPtr written);


    public MemoryAccess(
        Process process)
    {
        Process =
            process ??
            throw new ArgumentNullException(
                nameof(process));


        ModuleBase =
            FindModuleBase(
                process,
                "RF_Online.bin");


        if (ModuleBase == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Module RF_Online.bin tidak ditemukan.");
        }


        Handle =
            OpenProcess(
                PROCESS_VM_READ |
                PROCESS_VM_WRITE |
                PROCESS_VM_OPERATION |
                PROCESS_QUERY_INFORMATION,
                false,
                process.Id);


        if (Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "OpenProcess gagal.");
        }
    }


    // =========================================================
    // FIND MODULE
    // =========================================================

    private static IntPtr FindModuleBase(
        Process process,
        string moduleName)
    {
        try
        {
            foreach (
                ProcessModule module
                in process.Modules)
            {
                if (
                    module.ModuleName.Equals(
                        moduleName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return module.BaseAddress;
                }
            }
        }
        catch
        {
        }

        return IntPtr.Zero;
    }


    // =========================================================
    // READ BYTES
    // =========================================================

    public byte[] ReadBytes(
        IntPtr address,
        int count)
    {
        if (count <= 0)
            return Array.Empty<byte>();


        var buffer =
            new byte[count];


        if (
            !ReadProcessMemory(
                Handle,
                address,
                buffer,
                count,
                out var read) ||
            read.ToInt64() != count)
        {
            throw new InvalidOperationException(
                $"ReadProcessMemory gagal @ " +
                $"0x{address.ToInt64():X}.");
        }


        return buffer;
    }


    // =========================================================
    // READ BYTE
    // =========================================================

    public byte ReadByte(
        IntPtr address)
    {
        return ReadBytes(
            address,
            1)[0];
    }


    // =========================================================
    // READ POINTER 32-BIT
    // =========================================================

    public IntPtr ReadPointer32(
        IntPtr address)
    {
        var bytes =
            ReadBytes(
                address,
                4);


        int value =
            BitConverter.ToInt32(
                bytes,
                0);


        return new IntPtr(
            value);
    }


    // =========================================================
    // CE POINTER:
    //
    // RF_Online.bin+C28B40
    // + offset
    //
    // Equivalent:
    //
    // [[ModuleBase + C28B40] + offset]
    // =========================================================

    public IntPtr ResolvePointer32(
        long baseOffset,
        long offset)
    {
        IntPtr pointerAddress =
            new IntPtr(
                ModuleBase.ToInt64() +
                baseOffset);


        IntPtr pointer =
            ReadPointer32(
                pointerAddress);


        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Pointer NULL @ " +
                $"RF_Online.bin+{baseOffset:X}.");
        }


        return new IntPtr(
            pointer.ToInt64() +
            offset);
    }


    // =========================================================
    // WRITE BYTES
    // =========================================================

    public void WriteBytes(
        IntPtr address,
        params byte[] bytes)
    {
        if (bytes == null ||
            bytes.Length == 0)
        {
            return;
        }


        if (
            !WriteProcessMemory(
                Handle,
                address,
                bytes,
                bytes.Length,
                out var written) ||
            written.ToInt64() != bytes.Length)
        {
            throw new InvalidOperationException(
                $"WriteProcessMemory gagal @ " +
                $"0x{address.ToInt64():X}.");
        }
    }


    // =========================================================
    // WRITE BYTE
    // =========================================================

    public void WriteByte(
        IntPtr address,
        byte value)
    {
        WriteBytes(
            address,
            value);
    }


    // =========================================================
    // WRITE FLOAT
    // =========================================================

    public void WriteFloat(
        IntPtr address,
        float value)
    {
        WriteBytes(
            address,
            BitConverter.GetBytes(
                value));
    }


    // =========================================================
    // MODULE ADDRESS
    // =========================================================

    public IntPtr ModuleAddress(
        long offset)
    {
        return new IntPtr(
            ModuleBase.ToInt64() +
            offset);
    }


    // =========================================================
    // DISPOSE
    // =========================================================

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            CloseHandle(
                Handle);

            Handle =
                IntPtr.Zero;
        }
    }
}
