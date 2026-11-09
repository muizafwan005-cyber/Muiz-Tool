using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace RFTool.Advanced;

internal readonly record struct AddrRange(ulong Lo, ulong Hi);

/// <summary>Satu region memori (hasil VirtualQueryEx).</summary>
internal readonly struct MemRegion
{
    public ulong Base { get; }
    public ulong Size { get; }
    public uint State { get; }
    public uint Protect { get; }
    public uint Type { get; }

    public MemRegion(ulong baseAddress, ulong size, uint state, uint protect, uint type)
    {
        Base = baseAddress;
        Size = size;
        State = state;
        Protect = protect;
        Type = type;
    }

    public ulong End => Base + Size;

    public bool IsCommitted => State == NativeApi.MEM_COMMIT;

    public bool IsReadable
    {
        get
        {
            if (!IsCommitted) return false;
            if ((Protect & NativeApi.PAGE_GUARD) != 0) return false;

            uint p = Protect & 0xFF;
            return p != 0 && p != NativeApi.PAGE_NOACCESS;
        }
    }

    public bool IsWritable =>
        IsCommitted &&
        (Protect & (NativeApi.PAGE_READWRITE | NativeApi.PAGE_WRITECOPY |
                    NativeApi.PAGE_EXECUTE_READWRITE | NativeApi.PAGE_EXECUTE_WRITECOPY)) != 0;

    public bool IsExecutable =>
        IsCommitted &&
        (Protect & (NativeApi.PAGE_EXECUTE | NativeApi.PAGE_EXECUTE_READ |
                    NativeApi.PAGE_EXECUTE_READWRITE | NativeApi.PAGE_EXECUTE_WRITECOPY)) != 0;

    public string StateText
    {
        get
        {
            if (State == NativeApi.MEM_COMMIT) return "Commit";
            if (State == NativeApi.MEM_RESERVE) return "Reserve";
            if (State == NativeApi.MEM_FREE) return "Free";
            return "-";
        }
    }

    public string ProtectText
    {
        get
        {
            string text;

            switch (Protect & 0xFF)
            {
                case NativeApi.PAGE_NOACCESS: text = "No Access"; break;
                case NativeApi.PAGE_READONLY: text = "Read"; break;
                case NativeApi.PAGE_READWRITE: text = "Read/Write"; break;
                case NativeApi.PAGE_WRITECOPY: text = "Write Copy"; break;
                case NativeApi.PAGE_EXECUTE: text = "Execute"; break;
                case NativeApi.PAGE_EXECUTE_READ: text = "Execute/Read"; break;
                case NativeApi.PAGE_EXECUTE_READWRITE: text = "Execute/Read/Write"; break;
                case NativeApi.PAGE_EXECUTE_WRITECOPY: text = "Execute/Write Copy"; break;
                default: text = "-"; break;
            }

            if ((Protect & NativeApi.PAGE_GUARD) != 0)
                text += " +Guard";

            return text;
        }
    }

    public string TypeText
    {
        get
        {
            if (Type == NativeApi.MEM_IMAGE) return "Image";
            if (Type == NativeApi.MEM_MAPPED) return "Mapped";
            if (Type == NativeApi.MEM_PRIVATE) return "Private";
            return "-";
        }
    }
}

internal sealed class ModuleInfo
{
    public string Name { get; }
    public ulong Base { get; }
    public ulong Size { get; }
    public string Path { get; }

    public ModuleInfo(string name, ulong baseAddress, ulong size, string path)
    {
        Name = name;
        Base = baseAddress;
        Size = size;
        Path = path;
    }

    public ulong End => Base + Size;
}

/// <summary>
/// Handle ke process target + operasi baca/tulis/query memori.
/// Mandiri: tidak bergantung pada MemoryAccess di folder Core.
/// </summary>
internal sealed class TargetProcess : IDisposable
{
    private IntPtr handle;
    private readonly object moduleLock = new object();
    private List<ModuleInfo>? modules;
    private DateTime modulesLoadedAt = DateTime.MinValue;

    public int Id { get; }
    public string Name { get; }
    public bool Is32Bit { get; }
    public int PointerSize => Is32Bit ? 4 : 8;
    public ulong MaxAddress { get; }

    public int LastError { get; private set; }
    public long ReadFailures { get; private set; }
    public long WriteFailures { get; private set; }

    private TargetProcess(IntPtr handle, int id, string name)
    {
        this.handle = handle;
        Id = id;
        Name = name;

        bool wow = false;

        try
        {
            NativeApi.IsWow64Process(handle, out wow);
        }
        catch
        {
        }

        Is32Bit = wow || !Environment.Is64BitOperatingSystem;
        MaxAddress = Is32Bit ? 0xFFFFFFFFUL : 0x00007FFFFFFFFFFFUL;
    }

    public static TargetProcess Open(Process process)
    {
        uint access =
            NativeApi.PROCESS_VM_OPERATION |
            NativeApi.PROCESS_VM_READ |
            NativeApi.PROCESS_VM_WRITE |
            NativeApi.PROCESS_QUERY_INFORMATION;

        IntPtr h = NativeApi.OpenProcess(access, false, process.Id);

        if (h == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();

            throw new Win32Exception(
                err,
                "OpenProcess gagal untuk PID " + process.Id +
                " (coba jalankan tool sebagai Administrator).");
        }

        return new TargetProcess(h, process.Id, process.ProcessName);
    }

    public static string ErrorText(int code)
    {
        try
        {
            return new Win32Exception(code).Message;
        }
        catch
        {
            return "Error " + code;
        }
    }

    // ---------------------------------------------------------
    // BACA / TULIS
    // ---------------------------------------------------------

    /// <summary>
    /// Membaca sampai 'count' byte ke awal 'buffer'. bytesRead berisi jumlah byte
    /// yang berhasil dibaca (bisa parsial). Return true hanya jika semuanya terbaca.
    /// </summary>
    public bool TryRead(ulong address, byte[] buffer, int count, out int bytesRead)
    {
        bytesRead = 0;

        if (count <= 0)
            return true;

        if (count > buffer.Length)
            count = buffer.Length;

        if (address > (ulong)UIntPtr.MaxValue)
        {
            ReadFailures++;
            LastError = 299;
            return false;
        }

        bool ok = NativeApi.ReadProcessMemory(
            handle,
            new UIntPtr(address),
            buffer,
            new UIntPtr((uint)count),
            out UIntPtr read);

        bytesRead = (int)(ulong)read;

        if (!ok)
        {
            ReadFailures++;
            LastError = Marshal.GetLastWin32Error();
            return false;
        }

        return bytesRead == count;
    }

    public byte[]? ReadBytes(ulong address, int count)
    {
        if (count <= 0)
            return Array.Empty<byte>();

        byte[] buffer = new byte[count];

        if (!TryRead(address, buffer, count, out int got) || got < count)
            return null;

        return buffer;
    }

    public bool TryReadPointer(ulong address, out ulong value)
    {
        value = 0;

        byte[] buffer = new byte[8];

        if (!TryRead(address, buffer, PointerSize, out int got) || got < PointerSize)
            return false;

        value = PointerSize == 4
            ? BitConverter.ToUInt32(buffer, 0)
            : BitConverter.ToUInt64(buffer, 0);

        return true;
    }

    public bool Write(ulong address, byte[] data)
    {
        if (data.Length == 0)
            return true;

        if (address > (ulong)UIntPtr.MaxValue)
            return false;

        var addr = new UIntPtr(address);
        var len = new UIntPtr((uint)data.Length);

        if (NativeApi.WriteProcessMemory(handle, addr, data, len, out UIntPtr written) &&
            (ulong)written == (ulong)data.Length)
        {
            return true;
        }

        int err = Marshal.GetLastWin32Error();

        // Halaman mungkin read-only: ubah proteksi sebentar lalu kembalikan.
        if (NativeApi.VirtualProtectEx(handle, addr, len, NativeApi.PAGE_EXECUTE_READWRITE, out uint oldProtect))
        {
            bool ok =
                NativeApi.WriteProcessMemory(handle, addr, data, len, out written) &&
                (ulong)written == (ulong)data.Length;

            if (!ok)
                err = Marshal.GetLastWin32Error();

            NativeApi.VirtualProtectEx(handle, addr, len, oldProtect, out _);

            if (ok)
                return true;
        }

        LastError = err;
        WriteFailures++;
        return false;
    }

    // ---------------------------------------------------------
    // ALOKASI MEMORI (dipakai Auto Assemble: alloc / dealloc)
    // ---------------------------------------------------------

    /// <summary>
    /// Mengalokasikan memori RWX di process target. Pada target 64-bit mencoba mencari
    /// area bebas dalam jangkauan +-2GB dari 'near' agar jmp rel32 bisa menjangkaunya.
    /// Mengembalikan 0 jika gagal.
    /// </summary>
    public ulong Allocate(int size, ulong near)
    {
        size = Math.Max(size, 0x1000);
        uint type = NativeApi.MEM_COMMIT | NativeApi.MEM_RESERVE;

        if (!Is32Bit && near != 0)
        {
            ulong low = near > 0x7FFF0000UL ? near - 0x7FFF0000UL : 0x10000UL;
            ulong high = Math.Min(MaxAddress, near + 0x7FFF0000UL);
            ulong best = 0;
            ulong bestDistance = ulong.MaxValue;
            ulong address = low;

            while (address < high)
            {
                if (!QueryRegion(address, out MemRegion r))
                    break;

                if (r.State == NativeApi.MEM_FREE)
                {
                    ulong start = (r.Base + 0xFFFFUL) & ~0xFFFFUL;
                    ulong candidate = near & ~0xFFFFUL;

                    if (candidate < start)
                        candidate = start;

                    if (candidate + (ulong)size > r.End)
                    {
                        ulong tail = r.End >= (ulong)size ? (r.End - (ulong)size) & ~0xFFFFUL : 0;
                        candidate = tail;
                    }

                    if (candidate >= start && candidate + (ulong)size <= r.End)
                    {
                        ulong distance = candidate > near ? candidate - near : near - candidate;

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = candidate;
                        }
                    }
                }

                ulong next = r.Base + r.Size;

                if (next <= address)
                    break;

                address = next;
            }

            if (best != 0)
            {
                UIntPtr p = NativeApi.VirtualAllocEx(
                    handle, new UIntPtr(best), new UIntPtr((uint)size), type, NativeApi.PAGE_EXECUTE_READWRITE);

                if (p != UIntPtr.Zero)
                    return (ulong)p;
            }
        }

        UIntPtr any = NativeApi.VirtualAllocEx(
            handle, UIntPtr.Zero, new UIntPtr((uint)size), type, NativeApi.PAGE_EXECUTE_READWRITE);

        if (any == UIntPtr.Zero)
            LastError = Marshal.GetLastWin32Error();

        return (ulong)any;
    }

    public bool Free(ulong address)
    {
        if (address > (ulong)UIntPtr.MaxValue)
            return false;

        return NativeApi.VirtualFreeEx(handle, new UIntPtr(address), UIntPtr.Zero, NativeApi.MEM_RELEASE);
    }

    // ---------------------------------------------------------
    // REGION
    // ---------------------------------------------------------

    public bool QueryRegion(ulong address, out MemRegion region)
    {
        region = default;

        if (address > (ulong)UIntPtr.MaxValue)
            return false;

        UIntPtr result = NativeApi.VirtualQueryEx(
            handle,
            new UIntPtr(address),
            out NativeApi.MEMORY_BASIC_INFORMATION mbi,
            new UIntPtr((uint)Marshal.SizeOf<NativeApi.MEMORY_BASIC_INFORMATION>()));

        if (result == UIntPtr.Zero)
            return false;

        region = new MemRegion(
            (ulong)mbi.BaseAddress,
            (ulong)mbi.RegionSize,
            mbi.State,
            mbi.Protect,
            mbi.Type);

        return true;
    }

    public IEnumerable<MemRegion> EnumerateRegions(ulong start, ulong stop)
    {
        ulong address = start;
        ulong max = Math.Min(stop, MaxAddress);

        while (address < max)
        {
            if (!QueryRegion(address, out MemRegion region))
                yield break;

            yield return region;

            ulong next = region.Base + region.Size;

            if (next <= address)
                yield break;

            address = next;
        }
    }

    // ---------------------------------------------------------
    // MODULE
    // ---------------------------------------------------------

    public IReadOnlyList<ModuleInfo> GetModules(bool refresh = false)
    {
        lock (moduleLock)
        {
            if (modules != null && !refresh)
                return modules;

            var list = new List<ModuleInfo>();

            try
            {
                using Process p = Process.GetProcessById(Id);

                foreach (ProcessModule m in p.Modules)
                {
                    try
                    {
                        string file = "";

                        try
                        {
                            file = m.FileName ?? "";
                        }
                        catch
                        {
                        }

                        list.Add(new ModuleInfo(
                            m.ModuleName ?? "",
                            unchecked((ulong)m.BaseAddress.ToInt64()),
                            (ulong)Math.Max(0, m.ModuleMemorySize),
                            file));
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Warn("Gagal membaca daftar module: " + ex.Message);
            }

            modules = list;
            modulesLoadedAt = DateTime.UtcNow;

            return modules;
        }
    }

    public ModuleInfo? FindModule(ulong address)
    {
        foreach (ModuleInfo m in GetModules())
        {
            if (address >= m.Base && address < m.End)
                return m;
        }

        return null;
    }

    public ulong GetModuleBase(string name)
    {
        ulong b = FindBase(GetModules(), name);

        if (b == 0 && (DateTime.UtcNow - modulesLoadedAt).TotalSeconds > 2)
            b = FindBase(GetModules(true), name);

        return b;
    }

    private static ulong FindBase(IReadOnlyList<ModuleInfo> list, string name)
    {
        foreach (ModuleInfo m in list)
        {
            if (string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                return m.Base;
        }

        foreach (ModuleInfo m in list)
        {
            if (string.Equals(
                    Path.GetFileNameWithoutExtension(m.Name),
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return m.Base;
            }
        }

        return 0;
    }

    // ---------------------------------------------------------
    // FORMAT ALAMAT
    // ---------------------------------------------------------

    public string FormatAddress(ulong address)
    {
        return address.ToString(Is32Bit ? "X8" : "X16", CultureInfo.InvariantCulture);
    }

    /// <summary>"module.exe+1A2B" jika alamat berada di module, selain itu hex biasa.</summary>
    public string Describe(ulong address)
    {
        ModuleInfo? m = FindModule(address);

        if (m != null)
            return m.Name + "+" + (address - m.Base).ToString("X", CultureInfo.InvariantCulture);

        return FormatAddress(address);
    }

    public void Dispose()
    {
        IntPtr h = handle;
        handle = IntPtr.Zero;

        if (h != IntPtr.Zero)
            NativeApi.CloseHandle(h);
    }
}

/// <summary>
/// Menyediakan TargetProcess untuk semua modul Advanced berdasarkan process
/// yang sedang di-ATTACH di aplikasi utama. Aman dipakai dari banyak thread.
/// </summary>
internal sealed class TargetProvider : IDisposable
{
    private readonly Func<Process?> getProcess;
    private readonly object gate = new object();
    private TargetProcess? current;
    private int currentPid = -1;

    public TargetProvider(Func<Process?> getProcess)
    {
        this.getProcess = getProcess;
    }

    public TargetProcess? Get()
    {
        lock (gate)
        {
            Process? p = getProcess();
            int pid = -1;

            try
            {
                if (p != null && !p.HasExited)
                    pid = p.Id;
            }
            catch
            {
                pid = -1;
            }

            if (pid != currentPid)
            {
                current?.Dispose();
                current = null;
                currentPid = pid;

                if (pid > 0 && p != null)
                {
                    try
                    {
                        current = TargetProcess.Open(p);
                        DebugLog.Info(
                            "Target dibuka: " + current.Name + " (PID " + pid + ", " +
                            (current.Is32Bit ? "32-bit" : "64-bit") + ")");
                    }
                    catch (Exception ex)
                    {
                        current = null;
                        DebugLog.Error("Gagal membuka target: " + ex.Message);
                    }
                }
            }

            return current;
        }
    }

    public TargetProcess Require()
    {
        return Get() ?? throw new InvalidOperationException(
            "Belum attach ke process. Pilih process di bagian atas lalu klik ATTACH.");
    }

    public void Invalidate()
    {
        lock (gate)
        {
            current?.Dispose();
            current = null;
            currentPid = -1;
        }
    }

    public void Dispose()
    {
        Invalidate();
    }
}
