using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RFTool.Core;

public sealed class AobScanner
{
    private readonly MemoryAccess memory;

    private const uint MEM_COMMIT = 0x1000;

    private const uint PAGE_NOACCESS = 0x01;
    private const uint PAGE_READONLY = 0x02;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_WRITECOPY = 0x08;
    private const uint PAGE_EXECUTE = 0x10;
    private const uint PAGE_EXECUTE_READ = 0x20;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    private const uint PAGE_GUARD = 0x100;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern UIntPtr VirtualQueryEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer,
        UIntPtr dwLength);

    public AobScanner(
        MemoryAccess memory)
    {
        this.memory =
            memory ??
            throw new ArgumentNullException(
                nameof(memory));
    }

    // =========================================================
    // SCAN NORMAL
    //
    // Setara dengan AOBScan(pattern)
    //
    // Mencari seluruh committed readable memory process.
    // =========================================================

    public List<IntPtr> ScanProcess(
        string pattern)
    {
        return ScanProcess(
            pattern,
            writableOnly: false,
            excludeCopyOnWrite: false);
    }

    // =========================================================
    // SCAN WRITABLE
    //
    // Dipakai Combine.
    //
    // Setara:
    //
    // AOBScan(pattern, "+W-C")
    // =========================================================

    public List<IntPtr> ScanWritable(
        string pattern)
    {
        return ScanProcess(
            pattern,
            writableOnly: true,
            excludeCopyOnWrite: true);
    }

    // =========================================================
    // COMPATIBILITY
    //
    // Method lama tetap tersedia supaya bagian lain tidak
    // langsung rusak.
    //
    // Sekarang ScanModule sebenarnya scan process.
    // =========================================================

    public List<IntPtr> ScanModule(
        string pattern)
    {
        return ScanProcess(pattern);
    }

    // =========================================================
    // CORE SCANNER
    // =========================================================

    private List<IntPtr> ScanProcess(
        string pattern,
        bool writableOnly,
        bool excludeCopyOnWrite)
    {
        var wanted =
            Parse(
                pattern,
                out var mask);

        var results =
            new List<IntPtr>();

        if (wanted.Length == 0)
            return results;

        IntPtr address = IntPtr.Zero;

        int mbiSize =
            Marshal.SizeOf<MEMORY_BASIC_INFORMATION>();

        while (true)
        {
            MEMORY_BASIC_INFORMATION mbi;

            UIntPtr queried;

            try
            {
                queried =
                    VirtualQueryEx(
                        memory.Handle,
                        address,
                        out mbi,
                        (UIntPtr)mbiSize);
            }
            catch
            {
                break;
            }

            if (queried == UIntPtr.Zero)
                break;

            long regionBase =
                mbi.BaseAddress.ToInt64();

            long regionSize =
                unchecked(
                    (long)mbi.RegionSize.ToUInt64());

            if (regionSize <= 0)
                break;

            if (
                mbi.State == MEM_COMMIT &&
                IsReadable(mbi.Protect) &&
                (!writableOnly ||
                 IsWritable(mbi.Protect)) &&
                (!excludeCopyOnWrite ||
                 !IsCopyOnWrite(mbi.Protect)))
            {
                ScanRegion(
                    regionBase,
                    regionSize,
                    wanted,
                    mask,
                    results);
            }

            long next;

            try
            {
                next =
                    checked(
                        regionBase +
                        regionSize);
            }
            catch
            {
                break;
            }

            if (next <= regionBase)
                break;

            address =
                new IntPtr(next);

            // Untuk proses 32-bit, ini sudah cukup.
            // Untuk menghindari infinite loop jika address
            // melewati batas address space.
            if (address == IntPtr.Zero)
                break;
        }

        return results;
    }

    // =========================================================
    // SCAN ONE MEMORY REGION
    // =========================================================

    private void ScanRegion(
        long regionBase,
        long regionSize,
        byte[] wanted,
        bool[] mask,
        List<IntPtr> results)
    {
        // Jangan mencoba membaca region absurd besar sekaligus.
        const long MaxRegionRead =
            16L * 1024L * 1024L;

        long position = 0;

        while (position < regionSize)
        {
            long remaining =
                regionSize -
                position;

            int chunkSize =
                (int)Math.Min(
                    remaining,
                    MaxRegionRead);

            if (chunkSize < wanted.Length)
                break;

            IntPtr chunkAddress =
                new IntPtr(
                    regionBase +
                    position);

            byte[] data;

            try
            {
                data =
                    memory.ReadBytes(
                        chunkAddress,
                        chunkSize);
            }
            catch
            {
                position += chunkSize;
                continue;
            }

            int limit =
                data.Length -
                wanted.Length;

            for (
                int i = 0;
                i <= limit;
                i++)
            {
                bool match = true;

                for (
                    int j = 0;
                    j < wanted.Length;
                    j++)
                {
                    if (
                        mask[j] &&
                        data[i + j] != wanted[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    results.Add(
                        new IntPtr(
                            regionBase +
                            position +
                            i));
                }
            }

            // Overlap agar pattern yang berada tepat
            // di batas antar-chunk tetap dapat ditemukan.
            position +=
                chunkSize -
                wanted.Length +
                1;
        }
    }

    // =========================================================
    // MEMORY PROTECTION
    // =========================================================

    private static bool IsReadable(
        uint protect)
    {
        if (
            (protect &
             PAGE_GUARD) != 0)
        {
            return false;
        }

        uint baseProtect =
            protect &
            0xFF;

        return
            baseProtect ==
                PAGE_READONLY ||

            baseProtect ==
                PAGE_READWRITE ||

            baseProtect ==
                PAGE_WRITECOPY ||

            baseProtect ==
                PAGE_EXECUTE ||

            baseProtect ==
                PAGE_EXECUTE_READ ||

            baseProtect ==
                PAGE_EXECUTE_READWRITE ||

            baseProtect ==
                PAGE_EXECUTE_WRITECOPY;
    }

    private static bool IsWritable(
        uint protect)
    {
        if (
            (protect &
             PAGE_GUARD) != 0)
        {
            return false;
        }

        uint baseProtect =
            protect &
            0xFF;

        return
            baseProtect ==
                PAGE_READWRITE ||

            baseProtect ==
                PAGE_WRITECOPY ||

            baseProtect ==
                PAGE_EXECUTE_READWRITE ||

            baseProtect ==
                PAGE_EXECUTE_WRITECOPY;
    }

    private static bool IsCopyOnWrite(
        uint protect)
    {
        uint baseProtect =
            protect &
            0xFF;

        return
            baseProtect ==
                PAGE_WRITECOPY ||

            baseProtect ==
                PAGE_EXECUTE_WRITECOPY;
    }

    // =========================================================
    // PATTERN PARSER
    // =========================================================

    private static byte[] Parse(
        string pattern,
        out bool[] mask)
    {
        var parts =
            pattern.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var bytes =
            new byte[parts.Length];

        mask =
            new bool[parts.Length];

        for (
            int i = 0;
            i < parts.Length;
            i++)
        {
            if (
                parts[i] is "?" or "??")
            {
                mask[i] = false;
                continue;
            }

            bytes[i] =
                Convert.ToByte(
                    parts[i],
                    16);

            mask[i] = true;
        }

        return bytes;
    }
}
