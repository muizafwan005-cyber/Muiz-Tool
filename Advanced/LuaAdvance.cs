using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MoonSharp.Interpreter;
using RFTool.AdvancedSystem;

namespace RFTool.Advanced;

/// <summary>
/// Lua Advance: menjalankan skrip Lua dengan fungsi bergaya Cheat Engine
/// (readInteger, writeInteger, getAddress, AOBScan, autoAssemble, dst).
/// Catatan: angka Lua bertipe double sehingga nilai di atas 2^53 kehilangan presisi.
/// </summary>
internal sealed class LuaAdvance
{
    private readonly TargetProvider provider;
    private readonly AddressManager manager;
    private readonly Action<string> output;
    private readonly Action<Action> postUi;
    private readonly AaSession aa;
    private readonly CancellationToken token;
    private Script script = null!;

    public LuaAdvance(
        TargetProvider provider,
        AddressManager manager,
        Action<string> output,
        Action<Action> postUi,
        AaSession aaSession,
        CancellationToken token)
    {
        this.provider = provider;
        this.manager = manager;
        this.output = output;
        this.postUi = postUi;
        aa = aaSession;
        this.token = token;
    }

    /// <summary>Menjalankan kode Lua. Blocking; panggil dari thread latar.</summary>
    public void Run(string code)
    {
        script = new Script(CoreModules.Preset_SoftSandbox);
        script.Options.DebugPrint = s => output(s);

        RegisterApi();

        try
        {
            DynValue function = script.LoadString(code, null, "skrip");
            DynValue coroutine = script.CreateCoroutine(function);

            // Memberi kesempatan menghentikan skrip yang berputar tanpa henti.
            coroutine.Coroutine.AutoYieldCounter = 2000;

            while (coroutine.Coroutine.State != CoroutineState.Dead)
            {
                token.ThrowIfCancellationRequested();
                coroutine.Coroutine.Resume();
            }
        }
        catch (InterpreterException ex)
        {
            output("[Lua Advance] Error: " + (ex.DecoratedMessage ?? ex.Message));
        }
        catch (OperationCanceledException)
        {
            output("[Lua Advance] Dihentikan.");
        }
        catch (Exception ex)
        {
            output("[Lua Advance] Error: " + ex.Message);
        }
    }

    // ---------------------------------------------------------
    // HELPER
    // ---------------------------------------------------------

    private TargetProcess T()
    {
        return provider.Require();
    }

    private static DynValue Num(double value)
    {
        return DynValue.NewNumber(value);
    }

    private static long ToLong(DynValue v)
    {
        double? n = v.CastToNumber();

        if (n == null)
            throw new ScriptRuntimeException("Nilai harus berupa angka.");

        double d = n.Value;

        if (d >= 9.2233720368547758E18)
            return unchecked((long)(ulong)d);

        return (long)d;
    }

    private static ulong ToULong(DynValue v)
    {
        return unchecked((ulong)ToLong(v));
    }

    private ulong Addr(DynValue v)
    {
        if (v.Type == DataType.Number)
        {
            double d = v.Number;
            return d < 0 ? unchecked((ulong)(long)d) : (ulong)d;
        }

        if (v.Type == DataType.String)
        {
            if (!AddressExpression.TryEvaluate(T(), v.String, aa.Symbols, out ulong address, out string error))
                throw new ScriptRuntimeException("Alamat tidak valid '" + v.String + "': " + error);

            return address;
        }

        throw new ScriptRuntimeException("Alamat harus angka atau teks, contoh \"RF_Online.bin+C28B40\".");
    }

    private byte[]? Read(ulong address, int count)
    {
        return T().ReadBytes(address, count);
    }

    private bool Write(ulong address, byte[] data)
    {
        return T().Write(address, data);
    }

    private static bool Flag(CallbackArguments a, int index, bool defaultValue)
    {
        return a.Count > index && !a[index].IsNil() ? a[index].CastToBool() : defaultValue;
    }

    private void Sleep(long milliseconds)
    {
        long end = Environment.TickCount64 + milliseconds;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            long left = end - Environment.TickCount64;

            if (left <= 0)
                break;

            Thread.Sleep((int)Math.Min(left, 20));
        }
    }

    private void Reg(string name, Func<CallbackArguments, DynValue> function)
    {
        script.Globals[name] = DynValue.NewCallback((context, args) => function(args));
    }

    // ---------------------------------------------------------
    // DAFTAR FUNGSI
    // ---------------------------------------------------------

    private void RegisterApi()
    {
        // ----- umum -----
        Reg("sleep", a => { Sleep(ToLong(a[0])); return DynValue.Nil; });
        Reg("getTickCount", a => Num(Environment.TickCount64));
        Reg("showMessage", a => { output("[showMessage] " + a[0].CastToString()); return DynValue.Nil; });

        Reg("getOpenedProcessID", a =>
        {
            TargetProcess? t = provider.Get();
            return t == null ? DynValue.Nil : Num(t.Id);
        });

        Reg("getProcessName", a =>
        {
            TargetProcess? t = provider.Get();
            return t == null ? DynValue.Nil : DynValue.NewString(t.Name);
        });

        Reg("targetIs64Bit", a => DynValue.NewBoolean(!T().Is32Bit));

        // ----- operasi bit gaya CE -----
        Reg("bAnd", a => Num(ToULong(a[0]) & ToULong(a[1])));
        Reg("bOr", a => Num(ToULong(a[0]) | ToULong(a[1])));
        Reg("bXor", a => Num(ToULong(a[0]) ^ ToULong(a[1])));
        Reg("bShl", a => Num(ToULong(a[0]) << (int)ToLong(a[1])));
        Reg("bShr", a => Num(ToULong(a[0]) >> (int)ToLong(a[1])));
        Reg("bNot", a => Num(~ToULong(a[0])));

        // ----- baca memori -----
        Reg("readByte", a =>
        {
            byte[]? d = Read(Addr(a[0]), 1);
            return d == null ? DynValue.Nil : Num(d[0]);
        });

        Reg("readSmallInteger", a =>
        {
            byte[]? d = Read(Addr(a[0]), 2);

            if (d == null)
                return DynValue.Nil;

            return Flag(a, 1, false) ? Num(BitConverter.ToInt16(d, 0)) : Num(BitConverter.ToUInt16(d, 0));
        });

        Reg("readInteger", a =>
        {
            byte[]? d = Read(Addr(a[0]), 4);

            if (d == null)
                return DynValue.Nil;

            return Flag(a, 1, true) ? Num(BitConverter.ToInt32(d, 0)) : Num(BitConverter.ToUInt32(d, 0));
        });

        Reg("readQword", a =>
        {
            byte[]? d = Read(Addr(a[0]), 8);
            return d == null ? DynValue.Nil : Num(BitConverter.ToInt64(d, 0));
        });

        Reg("readPointer", a =>
        {
            TargetProcess t = T();

            if (!t.TryReadPointer(Addr(a[0]), out ulong value))
                return DynValue.Nil;

            return Num(value);
        });

        Reg("readFloat", a =>
        {
            byte[]? d = Read(Addr(a[0]), 4);
            return d == null ? DynValue.Nil : Num(BitConverter.ToSingle(d, 0));
        });

        Reg("readDouble", a =>
        {
            byte[]? d = Read(Addr(a[0]), 8);
            return d == null ? DynValue.Nil : Num(BitConverter.ToDouble(d, 0));
        });

        Reg("readString", a =>
        {
            int max = a.Count > 1 ? (int)ToLong(a[1]) : 256;
            max = Math.Max(1, Math.Min(max, 4096));
            bool wide = Flag(a, 2, false);

            byte[]? d = Read(Addr(a[0]), wide ? max * 2 : max);

            if (d == null)
                return DynValue.Nil;

            string text = wide ? Encoding.Unicode.GetString(d) : Encoding.Latin1.GetString(d);
            int nul = text.IndexOf('\0');
            return DynValue.NewString(nul >= 0 ? text.Substring(0, nul) : text);
        });

        Reg("readBytes", a =>
        {
            int count = (int)ToLong(a[1]);
            count = Math.Max(1, Math.Min(count, 4096));
            byte[]? d = Read(Addr(a[0]), count);

            if (d == null)
                return DynValue.Nil;

            if (Flag(a, 2, false))
            {
                var table = new Table(script);

                for (int i = 0; i < d.Length; i++)
                    table.Set(DynValue.NewNumber(i + 1), DynValue.NewNumber(d[i]));

                return DynValue.NewTable(table);
            }

            var values = new DynValue[d.Length];

            for (int i = 0; i < d.Length; i++)
                values[i] = DynValue.NewNumber(d[i]);

            return DynValue.NewTuple(values);
        });

        // ----- tulis memori -----
        Reg("writeByte", a => DynValue.NewBoolean(Write(Addr(a[0]), new[] { unchecked((byte)ToLong(a[1])) })));
        Reg("writeSmallInteger", a => DynValue.NewBoolean(Write(Addr(a[0]), BitConverter.GetBytes(unchecked((short)ToLong(a[1]))))));
        Reg("writeInteger", a => DynValue.NewBoolean(Write(Addr(a[0]), BitConverter.GetBytes(unchecked((int)ToLong(a[1]))))));
        Reg("writeQword", a => DynValue.NewBoolean(Write(Addr(a[0]), BitConverter.GetBytes(ToLong(a[1])))));

        Reg("writeFloat", a =>
        {
            double? n = a[1].CastToNumber();

            if (n == null)
                throw new ScriptRuntimeException("Nilai harus berupa angka.");

            return DynValue.NewBoolean(Write(Addr(a[0]), BitConverter.GetBytes((float)n.Value)));
        });

        Reg("writeDouble", a =>
        {
            double? n = a[1].CastToNumber();

            if (n == null)
                throw new ScriptRuntimeException("Nilai harus berupa angka.");

            return DynValue.NewBoolean(Write(Addr(a[0]), BitConverter.GetBytes(n.Value)));
        });

        Reg("writeString", a =>
        {
            string text = a[1].CastToString() ?? "";
            byte[] bytes = Flag(a, 2, false) ? Encoding.Unicode.GetBytes(text) : Encoding.Latin1.GetBytes(text);
            return DynValue.NewBoolean(Write(Addr(a[0]), bytes));
        });

        Reg("writeBytes", a =>
        {
            ulong address = Addr(a[0]);
            var bytes = new List<byte>();

            if (a.Count >= 2 && a[1].Type == DataType.Table)
            {
                Table table = a[1].Table;

                for (int i = 1; i <= table.Length; i++)
                    bytes.Add(unchecked((byte)ToLong(table.Get(i))));
            }
            else
            {
                for (int i = 1; i < a.Count; i++)
                    bytes.Add(unchecked((byte)ToLong(a[i])));
            }

            return DynValue.NewBoolean(Write(address, bytes.ToArray()));
        });

        // ----- alamat dan simbol -----
        Reg("getAddress", a =>
        {
            if (a[0].Type == DataType.Number)
                return a[0];

            string text = a[0].CastToString() ?? "";

            if (!AddressExpression.TryEvaluate(T(), text, aa.Symbols, out ulong value, out string error))
                throw new ScriptRuntimeException("getAddress('" + text + "') gagal: " + error);

            return Num(value);
        });

        Reg("getAddressSafe", a =>
        {
            if (a[0].Type == DataType.Number)
                return a[0];

            string text = a[0].CastToString() ?? "";

            return AddressExpression.TryEvaluate(T(), text, aa.Symbols, out ulong value, out _)
                ? Num(value)
                : DynValue.Nil;
        });

        Reg("registerSymbol", a =>
        {
            string name = a[0].CastToString() ?? "";
            SymbolTable.Set(name, Addr(a[1]));
            return DynValue.Nil;
        });

        Reg("unregisterSymbol", a =>
        {
            SymbolTable.Remove(a[0].CastToString() ?? "");
            return DynValue.Nil;
        });

        // ----- pencarian pola -----
        Reg("AOBScan", a => ScanToTable(a[0].CastToString() ?? "", null));
        Reg("AOBScanModule", a => ScanToTable(a[1].CastToString() ?? "", a[0].CastToString()));

        // ----- Auto Assemble -----
        Reg("autoAssemble", a =>
        {
            string text = a[0].CastToString() ?? "";
            bool ok = new AutoAssembler(provider, output, token).Run(text, true, aa);
            return DynValue.NewBoolean(ok);
        });

        // ----- Address Manager -----
        Reg("muizAddAddress", a =>
        {
            string description = a[0].CastToString() ?? "No description";

            string address = a[1].Type == DataType.Number
                ? ((ulong)a[1].Number).ToString("X")
                : (a[1].CastToString() ?? "");

            string type = a.Count > 2 ? (a[2].CastToString() ?? "4 Bytes") : "4 Bytes";
            string value = a.Count > 3 ? (a[3].CastToString() ?? "0") : "0";
            string group = a.Count > 4 ? (a[4].CastToString() ?? "Lua") : "Lua";

            postUi(() => manager.Add(new AddressEntry
            {
                Description = description,
                Address = address,
                ValueType = type,
                Signed = ValueCodec.DefaultSigned(ValueCodec.TypeFromName(type)),
                Value = value,
                Group = group
            }));

            return DynValue.NewBoolean(true);
        });
    }

    private DynValue ScanToTable(string patternText, string? module)
    {
        if (!AobPattern.TryParse(patternText, out AobPattern? pattern, out string error) || pattern == null)
            throw new ScriptRuntimeException("Pattern tidak valid: " + error);

        TargetProcess t = T();
        Func<MemRegion, bool>? filter = null;

        if (module != null)
        {
            ulong lo = 0;
            ulong hi = 0;

            foreach (ModuleInfo m in t.GetModules())
            {
                if (string.Equals(m.Name, module, StringComparison.OrdinalIgnoreCase))
                {
                    lo = m.Base;
                    hi = m.End;
                }
            }

            if (hi == 0)
                throw new ScriptRuntimeException("Module '" + module + "' tidak ditemukan.");

            filter = r => r.Base < hi && r.End > lo;
        }

        var found = new List<ulong>();

        PatternSearcher.Search(
            t, pattern, 0, ulong.MaxValue, filter,
            address =>
            {
                found.Add(address);
                return found.Count < 5000;
            },
            null, token);

        if (found.Count == 0)
            return DynValue.Nil;

        // Mirip StringList milik Cheat Engine: [0..n-1] berisi alamat hex, ada Count dan destroy().
        var table = new Table(script);

        for (int i = 0; i < found.Count; i++)
            table.Set(DynValue.NewNumber(i), DynValue.NewString(t.FormatAddress(found[i])));

        table.Set(DynValue.NewString("Count"), DynValue.NewNumber(found.Count));
        table.Set(DynValue.NewString("destroy"), DynValue.NewCallback((context, args) => DynValue.Nil));

        return DynValue.NewTable(table);
    }
}
