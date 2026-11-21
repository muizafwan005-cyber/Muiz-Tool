using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RFTool.AdvancedSystem;

namespace RFTool.Advanced;

/// <summary>
/// Layanan bersama Tab 5: target process, daftar alamat (AddressManager milik proyek)
/// dan mesin Freeze.
/// </summary>
internal sealed class AdvancedServices : IDisposable
{
    public TargetProvider Target { get; }
    public AddressManager Manager { get; }
    public FreezeService Freeze { get; }

    /// <summary>Mesin Lua Advance bersama (state persisten antar skrip).</summary>
    public LuaAdvance Lua { get; }

    /// <param name="getProcess">Mengembalikan process yang sedang di-ATTACH (atau null).</param>
    public AdvancedServices(Func<Process?> getProcess, AddressManager manager)
    {
        Target = new TargetProvider(getProcess);
        Manager = manager;
        Freeze = new FreezeService(Target, manager);

        // Aksi UI dari thread Lua dikembalikan ke UI thread (AdvancedServices dibuat di UI thread).
        SynchronizationContext? context = SynchronizationContext.Current;

        Lua = new LuaAdvance(
            Target,
            manager,
            action =>
            {
                if (context != null)
                    context.Post(_ => action(), null);
                else
                    action();
            });
    }

    // ---------- Sesi Auto Assemble bersama (Script Manager + Address Manager) ----------

    private readonly Dictionary<string, AaSession> aaSessions =
        new Dictionary<string, AaSession>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> aaActive =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>(kunci script, aktif?) - dipanggil di UI thread setiap script di-enable/disable.</summary>
    public event Action<string, bool>? AaStateChanged;

    /// <summary>Pesan log Auto Assemble dari Address Manager (bisa dari thread lain).</summary>
    public event Action<string>? AaLog;

    public AaSession GetAaSession(string key)
    {
        if (!aaSessions.TryGetValue(key, out AaSession? session))
        {
            session = new AaSession();
            aaSessions[key] = session;
        }

        return session;
    }

    public void RemoveAaSession(string key)
    {
        aaSessions.Remove(key);
        aaActive.Remove(key);
    }

    public bool IsAaActive(string key)
    {
        return aaActive.Contains(key);
    }

    public void SetAaActive(string key, bool active)
    {
        if (active)
            aaActive.Add(key);
        else
            aaActive.Remove(key);

        AaStateChanged?.Invoke(key, active);
    }

    public void RaiseAaLog(string line)
    {
        AaLog?.Invoke(line);
    }

    /// <summary>Dipanggil dari DetachCurrent(): semua Freeze dimatikan, handle ditutup.</summary>
    public void OnDetach()
    {
        // Memori hasil alloc ikut hilang bersama process, jadi status script dibersihkan.
        aaSessions.Clear();
        aaActive.Clear();

        foreach (AddressEntry e in Manager.Entries)
            e.Freeze = false;

        Freeze.Refresh();
        Target.Invalidate();
        SymbolTable.Clear();
        Lua.Reset();
    }

    public void Dispose()
    {
        Freeze.Dispose();
        Target.Dispose();
    }
}

/// <summary>Resolve alamat entry + baca/tulis satu kali.</summary>
internal static class EntryIO
{
    public static bool TryResolve(TargetProcess t, AddressEntry e, out ulong address, out string error)
    {
        return AddressExpression.TryEvaluate(t, e.Address, null, out address, out error);
    }

    public static int ValueSize(AddressEntry e)
    {
        ScanValueType type = ValueCodec.TypeFromName(e.ValueType);

        if (type != ScanValueType.String && type != ScanValueType.ByteArray)
            return ValueCodec.Size(type);

        return ValueCodec.TryParse(e.Value, type, e.Hex, false, out byte[] b, out _) ? b.Length : 16;
    }

    public static bool TryWrite(TargetProvider provider, AddressEntry e, out string message)
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            message = "Belum attach ke process.";
            return false;
        }

        ScanValueType type = ValueCodec.TypeFromName(e.ValueType);

        if (!ValueCodec.TryParse(e.Value, type, e.Hex, false, out byte[] data, out string parseError))
        {
            message = parseError;
            return false;
        }

        if (!TryResolve(t, e, out ulong address, out string addrError))
        {
            message = addrError;
            return false;
        }

        if (!t.Write(address, data))
        {
            message = "Write gagal: " + TargetProcess.ErrorText(t.LastError);
            return false;
        }

        message = "OK -> 0x" + t.FormatAddress(address);
        return true;
    }

    public static bool TryRead(TargetProvider provider, AddressEntry e, out string text, out string message)
    {
        text = "";
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            message = "Belum attach ke process.";
            return false;
        }

        if (!TryResolve(t, e, out ulong address, out string addrError))
        {
            message = addrError;
            return false;
        }

        byte[]? data = t.ReadBytes(address, ValueSize(e));

        if (data == null)
        {
            message = "Read gagal di 0x" + t.FormatAddress(address) + ": " + TargetProcess.ErrorText(t.LastError);
            return false;
        }

        text = ValueCodec.Format(data, ValueCodec.TypeFromName(e.ValueType), false, e.Signed, e.Hex);
        message = "0x" + t.FormatAddress(address);
        return true;
    }
}

/// <summary>
/// Mesin Freeze: setiap entry yang Freeze-nya dicentang ditulis ulang terus-menerus
/// dengan nilai Value miliknya sendiri. Alamat pointer di-resolve ulang tiap siklus.
/// </summary>
internal sealed class FreezeService : IDisposable
{
    private sealed class Item
    {
        public AddressEntry Entry = null!;
        public string Expression = "";
        public byte[] Data = Array.Empty<byte>();
        public FreezeMode Mode;
        public ScanValueType Type;
    }

    private readonly TargetProvider provider;
    private readonly AddressManager manager;
    private volatile Item[] items = Array.Empty<Item>();
    private Thread? thread;
    private volatile bool running;

    public int IntervalMs { get; set; } = 25;

    public FreezeService(TargetProvider provider, AddressManager manager)
    {
        this.provider = provider;
        this.manager = manager;
    }

    /// <summary>Panggil di UI thread setiap Freeze/Value/Address berubah, atau entry dihapus.</summary>
    public void Refresh()
    {
        var list = new List<Item>();

        foreach (AddressEntry e in manager.Entries)
        {
            // Entri script diatur oleh Auto Assemble, bukan oleh mesin Freeze nilai.
            if (e.IsScript)
                continue;

            if (!e.Freeze)
            {
                e.Status = "";
                continue;
            }

            ScanValueType type = ValueCodec.TypeFromName(e.ValueType);

            if (!ValueCodec.TryParse(e.Value, type, e.Hex, false, out byte[] data, out string err))
            {
                SetStatus(e, "Value tidak valid: " + err);
                continue;
            }

            list.Add(new Item { Entry = e, Expression = e.Address, Data = data, Mode = e.FreezeMode, Type = type });
        }

        items = list.ToArray();

        if (list.Count > 0)
            EnsureThread();
    }

    private void EnsureThread()
    {
        if (running && thread != null && thread.IsAlive)
            return;

        running = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "MuizFreeze" };
        thread.Start();
    }

    private void Loop()
    {
        while (running)
        {
            Item[] snapshot = items;

            if (snapshot.Length == 0)
            {
                Thread.Sleep(80);
                continue;
            }

            TargetProcess? t = provider.Get();

            if (t == null)
            {
                foreach (Item it in snapshot)
                    SetStatus(it.Entry, "Menunggu attach...");

                Thread.Sleep(200);
                continue;
            }

            foreach (Item it in snapshot)
            {
                if (!AddressExpression.TryEvaluate(t, it.Expression, null, out ulong address, out string err))
                {
                    SetStatus(it.Entry, "Alamat gagal: " + err);
                    continue;
                }

                // "Allow increase/decrease": nilai hanya dikembalikan jika bergerak ke arah yang tidak diizinkan
                if (it.Mode != FreezeMode.Normal &&
                    it.Type != ScanValueType.String &&
                    it.Type != ScanValueType.ByteArray)
                {
                    byte[]? current = t.ReadBytes(address, it.Data.Length);

                    if (current != null)
                    {
                        int cmp = ValueCodec.Compare(current, it.Data, it.Type);

                        if ((it.Mode == FreezeMode.AllowIncrease && cmp >= 0) ||
                            (it.Mode == FreezeMode.AllowDecrease && cmp <= 0))
                        {
                            SetStatus(it.Entry,
                                "Freeze aktif (" +
                                (it.Mode == FreezeMode.AllowIncrease ? "boleh naik" : "boleh turun") +
                                ") -> 0x" + t.FormatAddress(address));
                            continue;
                        }
                    }
                }

                if (t.Write(address, it.Data))
                    SetStatus(it.Entry, "Freeze aktif -> 0x" + t.FormatAddress(address));
                else
                    SetStatus(it.Entry, "Write gagal: " + TargetProcess.ErrorText(t.LastError));
            }

            Thread.Sleep(Math.Max(5, IntervalMs));
        }
    }

    private static void SetStatus(AddressEntry e, string status)
    {
        if (e.Status == status)
            return;

        string old = e.Status;
        e.Status = status;

        bool wasOk = old.StartsWith("Freeze aktif", StringComparison.Ordinal);
        bool isOk = status.StartsWith("Freeze aktif", StringComparison.Ordinal);

        if (wasOk != isOk || old.Length == 0)
            DebugLog.Info("Freeze '" + e.Description + "': " + status);
    }

    public void Stop()
    {
        running = false;
        items = Array.Empty<Item>();
    }

    public void Dispose()
    {
        Stop();
    }
}
