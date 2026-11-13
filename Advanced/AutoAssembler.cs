using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Reloaded.Assembler;

namespace RFTool.Advanced;

internal sealed class AaException : Exception
{
    public AaException(string message) : base(message)
    {
    }
}

internal sealed class AllocInfo
{
    public ulong Address;
    public int Size;
}

/// <summary>Status satu skrip Auto Assemble yang sedang aktif (simbol dan alokasi).</summary>
internal sealed class AaSession
{
    public readonly Dictionary<string, ulong> Symbols =
        new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

    public readonly Dictionary<string, AllocInfo> Allocs =
        new Dictionary<string, AllocInfo>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Auto Assemble bergaya Cheat Engine: [ENABLE] / [DISABLE], aobscan, alloc, label,
/// registersymbol, dan instruksi assembly (dirakit oleh FASM). Angka default HEX.
/// </summary>
internal sealed class AutoAssembler
{
    private sealed class Block
    {
        public string StartExpr = "";
        public int LineNo;
        public ulong Address;
        public readonly List<string> Lines = new List<string>();
        public readonly List<string> Labels = new List<string>();
    }

    private static readonly HashSet<string> Directives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "alloc", "dealloc", "label", "registersymbol", "unregistersymbol",
        "aobscan", "aobscanmodule", "define", "assert", "fullaccess"
    };

    private static readonly Regex DirectiveRegex =
        new Regex(@"^(?<n>[A-Za-z_]\w*)\s*\((?<a>.*)\)\s*$", RegexOptions.Compiled);

    private static readonly Regex HexToken =
        new Regex(@"(?<![\w.])([0-9A-Fa-f]+)(?![\w.])", RegexOptions.Compiled);

    private static readonly Regex RegisterToken = new Regex(
        @"\b(r?[a-d]x|e[a-d]x|r?[sd]i|e[sd]i|r?[sb]p|e[sb]p|[a-d][lh]|[sd]il|[sb]pl|rip|eip|r\d{1,2}[dwb]?|[xy]mm\d{1,2})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly TargetProvider provider;
    private readonly Action<string> log;
    private readonly CancellationToken ct;
    private readonly LuaAdvance? lua;

    /// <param name="lua">Mesin Lua untuk blok {$lua}. Null = blok {$lua} tidak didukung.</param>
    public AutoAssembler(TargetProvider provider, Action<string> log, CancellationToken ct, LuaAdvance? lua)
    {
        this.provider = provider;
        this.log = log;
        this.ct = ct;
        this.lua = lua;
    }

    /// <summary>Menjalankan bagian [ENABLE] (enable=true) atau [DISABLE]. Return true jika sukses.</summary>
    public bool Run(string script, bool enable, AaSession session)
    {
        try
        {
            Execute(script, enable, session);
            return true;
        }
        catch (AaException ex)
        {
            log("[AA] Error: " + ex.Message);
            return false;
        }
        catch (OperationCanceledException)
        {
            log("[AA] Dibatalkan.");
            return false;
        }
        catch (Exception ex)
        {
            log("[AA] Error: " + ex.Message);
            return false;
        }
    }

    // ---------------------------------------------------------
    // PARSING
    // ---------------------------------------------------------

    private static List<string> ExtractSection(string script, bool enable)
    {
        string[] lines = script.Replace("\r", "").Split('\n');
        int en = -1;
        int dis = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].Trim();

            if (t.Equals("[ENABLE]", StringComparison.OrdinalIgnoreCase))
                en = i;
            else if (t.Equals("[DISABLE]", StringComparison.OrdinalIgnoreCase))
                dis = i;
        }

        if (en < 0 && dis < 0)
            return enable ? lines.ToList() : new List<string>();

        int start;
        int end;

        if (enable)
        {
            if (en < 0)
                return new List<string>();

            start = en + 1;
            end = dis > en ? dis : lines.Length;
        }
        else
        {
            if (dis < 0)
                return new List<string>();

            start = dis + 1;
            end = en > dis ? en : lines.Length;
        }

        return lines.Skip(start).Take(end - start).ToList();
    }

    private static string StripComment(string line)
    {
        int i = line.IndexOf("//", StringComparison.Ordinal);
        return i >= 0 ? line.Substring(0, i) : line;
    }

    private static List<string> SplitArgs(string text)
    {
        return text.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
    }

    private static bool IsIdentifier(string text)
    {
        return Regex.IsMatch(text, @"^[A-Za-z_][A-Za-z0-9_]*$");
    }

    private static bool TryHex(string text, out ulong value)
    {
        text = text.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text.Substring(2);

        return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsBlockStart(string head, AaSession session, HashSet<string> declared)
    {
        if (declared.Contains(head))
            return false;

        if (Regex.IsMatch(head, @"[+\-\[\]\.]"))
            return true;

        if (session.Symbols.ContainsKey(head))
            return true;

        if (SymbolTable.TryGet(head, out _))
            return true;

        return Regex.IsMatch(head, @"^(0x)?[0-9A-Fa-f]{5,}$");
    }

    private void Execute(string script, bool enable, AaSession session)
    {
        TargetProcess t = provider.Require();
        List<string> lines = ExtractSection(script, enable);

        if (lines.Count == 0)
        {
            log("[AA] Bagian " + (enable ? "[ENABLE]" : "[DISABLE]") + " kosong.");
            return;
        }

        var blocks = new List<Block>();
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var register = new List<string>();
        var unregister = new List<string>();
        Block? current = null;

        // {$lua} tanpa {$asm} berlaku sampai akhir bagian: tutup otomatis.
        CloseOpenLuaBlock(lines);

        bool inLua = false;
        int luaStart = 0;
        var luaCode = new List<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string marker = lines[i].Trim();

            if (inLua)
            {
                if (marker.Equals("{$asm}", StringComparison.OrdinalIgnoreCase))
                {
                    inLua = false;
                    RunLuaBlock(luaCode, luaStart, lines, i, session);
                    luaCode.Clear();
                }
                else
                {
                    luaCode.Add(lines[i]);
                }

                continue;
            }

            if (marker.Equals("{$lua}", StringComparison.OrdinalIgnoreCase))
            {
                inLua = true;
                luaStart = i + 1;
                continue;
            }

            if (marker.Equals("{$asm}", StringComparison.OrdinalIgnoreCase))
                continue;

            string line = StripComment(lines[i]).Trim();

            if (line.Length == 0)
                continue;

            foreach (KeyValuePair<string, string> d in defines)
            {
                string replacement = d.Value;
                line = Regex.Replace(line, @"\b" + Regex.Escape(d.Key) + @"\b", m => replacement, RegexOptions.IgnoreCase);
            }

            Match dm = DirectiveRegex.Match(line);

            if (dm.Success && Directives.Contains(dm.Groups["n"].Value))
            {
                RunDirective(
                    t, session,
                    dm.Groups["n"].Value.ToLowerInvariant(),
                    SplitArgs(dm.Groups["a"].Value),
                    declared, defines, register, unregister);

                continue;
            }

            if (line.EndsWith(":", StringComparison.Ordinal))
            {
                string head = line.Substring(0, line.Length - 1).Trim();

                if (head.Length > 0 && IsBlockStart(head, session, declared))
                {
                    current = new Block { StartExpr = head, LineNo = i + 1 };
                    blocks.Add(current);
                    continue;
                }

                if (IsIdentifier(head))
                {
                    if (current == null)
                        throw new AaException("Label '" + head + "' berada di luar blok alamat.");

                    current.Lines.Add(head + ":");
                    current.Labels.Add(head);
                    declared.Add(head);
                    continue;
                }
            }

            if (current == null)
                throw new AaException("Baris " + (i + 1) + ": instruksi berada di luar blok alamat -> " + line);

            current.Lines.Add(line);
        }

        AssembleBlocks(t, session, blocks);

        foreach (string name in register)
        {
            if (session.Symbols.TryGetValue(name, out ulong address))
            {
                SymbolTable.Set(name, address);
                log("[AA] registersymbol " + name + " = 0x" + address.ToString("X"));
            }
            else
            {
                log("[AA] Peringatan: registersymbol(" + name + ") tidak menemukan simbol itu.");
            }
        }

        foreach (string name in unregister)
        {
            SymbolTable.Remove(name);
            log("[AA] unregistersymbol " + name);
        }

        log("[AA] " + (enable ? "ENABLE" : "DISABLE") + " selesai.");
    }

    // ---------------------------------------------------------
    // BLOK {$lua} ... {$asm}
    // ---------------------------------------------------------

    private static void CloseOpenLuaBlock(List<string> lines)
    {
        bool open = false;

        foreach (string raw in lines)
        {
            string marker = raw.Trim();

            if (marker.Equals("{$lua}", StringComparison.OrdinalIgnoreCase))
                open = true;
            else if (marker.Equals("{$asm}", StringComparison.OrdinalIgnoreCase))
                open = false;
        }

        if (open)
            lines.Add("{$asm}");
    }

    private void RunLuaBlock(List<string> code, int startLine, List<string> lines, int insertAfter, AaSession session)
    {
        if (lua == null)
        {
            throw new AaException(
                "Blok {$lua} (baris " + startLine + ") tidak didukung di konteks ini " +
                "(mis. di dalam autoAssemble() yang dipanggil dari Lua).");
        }

        LuaResult result = lua.Execute(string.Join("\n", code), log, ct, session);

        if (result.Cancelled)
            throw new OperationCanceledException();

        if (!result.Ok)
            throw new AaException("Blok {$lua} baris " + startLine + ": " + result.Error);

        // String yang dikembalikan Lua disisipkan sebagai kode assembly di posisi ini.
        if (!string.IsNullOrWhiteSpace(result.Text))
        {
            string[] extra = result.Text.Replace("\r", "").Split('\n');
            lines.InsertRange(insertAfter + 1, extra);
            log("[AA] {$lua} menghasilkan " + extra.Length + " baris assembly.");
        }
    }

    // ---------------------------------------------------------
    // DIREKTIF
    // ---------------------------------------------------------

    private static void RequireArgs(string name, List<string> args, int min)
    {
        if (args.Count < min)
            throw new AaException(name + "(...) butuh minimal " + min + " argumen.");
    }

    private void RunDirective(
        TargetProcess t,
        AaSession s,
        string name,
        List<string> a,
        HashSet<string> declared,
        Dictionary<string, string> defines,
        List<string> register,
        List<string> unregister)
    {
        switch (name)
        {
            case "alloc":
            {
                RequireArgs(name, a, 2);
                string symbol = a[0];

                if (!TryHex(a[1], out ulong size) || size == 0 || size > 0x10000000)
                    throw new AaException("alloc(" + symbol + "): ukuran '" + a[1] + "' tidak valid (hex, maks 10000000).");

                ulong near = 0;

                if (a.Count >= 3)
                {
                    if (!AddressExpression.TryEvaluate(t, a[2], s.Symbols, out near, out string nearError))
                        throw new AaException("alloc(" + symbol + "): alamat dekat tidak valid: " + nearError);
                }
                else
                {
                    IReadOnlyList<ModuleInfo> mods = t.GetModules();

                    if (mods.Count > 0)
                        near = mods[0].Base;
                }

                if (s.Allocs.TryGetValue(symbol, out AllocInfo? old))
                    t.Free(old.Address);

                ulong address = t.Allocate((int)size, near);

                if (address == 0)
                    throw new AaException("alloc(" + symbol + ") gagal: " + TargetProcess.ErrorText(t.LastError));

                s.Allocs[symbol] = new AllocInfo { Address = address, Size = (int)size };
                s.Symbols[symbol] = address;
                log("[AA] alloc " + symbol + " = 0x" + t.FormatAddress(address) + " (" + size.ToString("X") + " byte)");
                break;
            }

            case "dealloc":
            {
                RequireArgs(name, a, 1);

                if (s.Allocs.TryGetValue(a[0], out AllocInfo? info))
                {
                    t.Free(info.Address);
                    s.Allocs.Remove(a[0]);
                    s.Symbols.Remove(a[0]);
                    SymbolTable.Remove(a[0]);
                    log("[AA] dealloc " + a[0]);
                }
                else
                {
                    log("[AA] Peringatan: dealloc(" + a[0] + ") tidak ada alokasinya.");
                }

                break;
            }

            case "label":
                RequireArgs(name, a, 1);
                foreach (string l in a)
                    declared.Add(l);
                break;

            case "registersymbol":
                RequireArgs(name, a, 1);
                register.AddRange(a);
                break;

            case "unregistersymbol":
                RequireArgs(name, a, 1);
                unregister.AddRange(a);
                break;

            case "define":
                RequireArgs(name, a, 2);
                defines[a[0]] = a[1];
                break;

            case "aobscan":
            {
                RequireArgs(name, a, 2);
                string pattern = string.Join(" ", a.Skip(1));
                ulong found = ScanAob(t, pattern, null, a[0], out int count);
                s.Symbols[a[0]] = found;
                log("[AA] aobscan " + a[0] + " = " + t.Describe(found) + (count > 1 ? " (PERINGATAN: pola tidak unik)" : ""));
                break;
            }

            case "aobscanmodule":
            {
                RequireArgs(name, a, 3);
                string pattern = string.Join(" ", a.Skip(2));
                ulong found = ScanAob(t, pattern, a[1], a[0], out int count);
                s.Symbols[a[0]] = found;
                log("[AA] aobscanmodule " + a[0] + " = " + t.Describe(found) + (count > 1 ? " (PERINGATAN: pola tidak unik)" : ""));
                break;
            }

            case "assert":
            {
                RequireArgs(name, a, 2);

                if (!AddressExpression.TryEvaluate(t, a[0], s.Symbols, out ulong address, out string error))
                    throw new AaException("assert: alamat tidak valid: " + error);

                if (!AobPattern.TryParse(string.Join(" ", a.Skip(1)), out AobPattern? p, out string perr) || p == null)
                    throw new AaException("assert: pattern tidak valid: " + perr);

                byte[]? actual = t.ReadBytes(address, p.Length);
                bool ok = actual != null;

                for (int i = 0; ok && i < p.Length; i++)
                {
                    if (p.Mask[i] && actual![i] != p.Bytes[i])
                        ok = false;
                }

                if (!ok)
                    throw new AaException("assert gagal di " + t.Describe(address) + ": byte tidak sesuai (versi game berbeda?).");

                log("[AA] assert OK di " + t.Describe(address));
                break;
            }

            case "fullaccess":
                log("[AA] fullaccess diabaikan (tidak diperlukan, penulisan memakai VirtualProtectEx otomatis).");
                break;
        }
    }

    private ulong ScanAob(TargetProcess t, string patternText, string? module, string symbol, out int count)
    {
        if (!AobPattern.TryParse(patternText, out AobPattern? pattern, out string error) || pattern == null)
            throw new AaException("Pattern '" + symbol + "' tidak valid: " + error);

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
                throw new AaException("Module '" + module + "' tidak ditemukan.");

            filter = r => r.Base < hi && r.End > lo;
        }

        ulong first = 0;
        int found = 0;

        PatternSearcher.Search(
            t, pattern, 0, ulong.MaxValue, filter,
            address =>
            {
                if (found == 0)
                    first = address;

                found++;
                return found < 2;
            },
            null, ct);

        count = found;

        if (found == 0)
            throw new AaException("AOB '" + symbol + "' tidak ditemukan (versi game berbeda?).");

        return first;
    }

    // ---------------------------------------------------------
    // MERAKIT BLOK
    // ---------------------------------------------------------

    private void AssembleBlocks(TargetProcess t, AaSession s, List<Block> blocks)
    {
        if (blocks.Count == 0)
            return;

        foreach (Block b in blocks)
        {
            if (!AddressExpression.TryEvaluate(t, b.StartExpr, s.Symbols, out ulong address, out string error))
                throw new AaException("Baris " + b.LineNo + ": alamat blok '" + b.StartExpr + "' tidak valid: " + error);

            b.Address = address;
        }

        var labelAddr = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

        foreach (Block b in blocks)
        {
            foreach (string label in b.Labels)
                labelAddr[label] = b.Address;
        }

        Assembler fasm;

        try
        {
            fasm = new Assembler();
        }
        catch (Exception ex)
        {
            throw new AaException("Assembler (FASM) gagal dimuat: " + ex.Message);
        }

        using (fasm)
        {
            // Hitung alamat tiap label (berulang sampai stabil, karena ukuran jmp bisa berubah).
            for (int pass = 0; pass < 8; pass++)
            {
                ct.ThrowIfCancellationRequested();
                bool changed = false;

                foreach (Block b in blocks)
                {
                    foreach (string label in b.Labels)
                    {
                        int index = b.Lines.FindIndex(
                            l => string.Equals(l, label + ":", StringComparison.OrdinalIgnoreCase));

                        byte[] prefix = AssembleLines(fasm, t, s, b, index, labelAddr);
                        ulong addr = b.Address + (ulong)prefix.Length;

                        if (!labelAddr.TryGetValue(label, out ulong previous) || previous != addr)
                        {
                            labelAddr[label] = addr;
                            changed = true;
                        }
                    }
                }

                if (!changed && pass > 0)
                    break;
            }

            foreach (Block b in blocks)
            {
                byte[] bytes = AssembleLines(fasm, t, s, b, b.Lines.Count, labelAddr);

                foreach (AllocInfo info in s.Allocs.Values)
                {
                    ulong end = info.Address + (ulong)info.Size;

                    if (b.Address >= info.Address && b.Address < end && b.Address + (ulong)bytes.Length > end)
                        throw new AaException("Blok '" + b.StartExpr + "' (" + bytes.Length + " byte) melebihi ukuran alloc.");
                }

                if (bytes.Length > 0)
                {
                    if (!t.Write(b.Address, bytes))
                        throw new AaException("Gagal menulis ke " + t.Describe(b.Address) + ": " + TargetProcess.ErrorText(t.LastError));

                    log("[AA] Menulis " + bytes.Length + " byte ke " + t.Describe(b.Address));
                }
            }
        }

        foreach (KeyValuePair<string, ulong> kv in labelAddr)
            s.Symbols[kv.Key] = kv.Value;
    }

    private byte[] AssembleLines(
        Assembler fasm,
        TargetProcess t,
        AaSession s,
        Block b,
        int count,
        Dictionary<string, ulong> labelAddr)
    {
        if (count <= 0)
            return Array.Empty<byte>();

        var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < count; i++)
        {
            string l = b.Lines[i];

            if (l.EndsWith(":", StringComparison.Ordinal))
                defined.Add(l.Substring(0, l.Length - 1));
        }

        var names = new HashSet<string>(defined, StringComparer.OrdinalIgnoreCase);
        var source = new List<string> { t.Is32Bit ? "use32" : "use64", "org 0x" + b.Address.ToString("X") };

        foreach (KeyValuePair<string, ulong> kv in s.Symbols)
            AddEquate(source, names, kv.Key, kv.Value, defined);

        foreach (KeyValuePair<string, ulong> kv in labelAddr)
            AddEquate(source, names, kv.Key, kv.Value, defined);

        foreach (KeyValuePair<string, ulong> kv in SymbolTable.Snapshot())
            AddEquate(source, names, kv.Key, kv.Value, defined);

        int headerLines = source.Count;

        for (int i = 0; i < count; i++)
            source.Add(ConvertLine(b.Lines[i], t, names));

        try
        {
            return fasm.Assemble(source.ToArray());
        }
        catch (Exception ex)
        {
            string culprit = FindFailingLine(fasm, source, headerLines, b);
            throw new AaException("Blok '" + b.StartExpr + "': " + ex.Message + culprit);
        }
    }

    private static void AddEquate(List<string> source, HashSet<string> names, string name, ulong value, HashSet<string> defined)
    {
        if (!IsIdentifier(name) || defined.Contains(name) || !names.Add(name))
            return;

        source.Add(name + " = 0x" + value.ToString("X"));
    }

    private static string FindFailingLine(Assembler fasm, List<string> source, int headerLines, Block b)
    {
        for (int i = headerLines; i < source.Count; i++)
        {
            try
            {
                fasm.Assemble(source.Take(i + 1).ToArray());
            }
            catch
            {
                int original = i - headerLines;
                string text = original >= 0 && original < b.Lines.Count ? b.Lines[original] : "?";
                return "  [baris bermasalah: \"" + text + "\"]";
            }
        }

        return "";
    }

    // ---------------------------------------------------------
    // KONVERSI SINTAKS AA -> FASM
    // ---------------------------------------------------------

    private static string ConvertLine(string line, TargetProcess t, HashSet<string> names)
    {
        string trimmed = line.Trim();

        if (trimmed.EndsWith(":", StringComparison.Ordinal))
            return trimmed;

        int space = trimmed.IndexOfAny(new[] { ' ', '\t' });
        string op = space < 0 ? trimmed : trimmed.Substring(0, space);
        string rest = space < 0 ? "" : trimmed.Substring(space + 1).Trim();
        string lower = op.ToLowerInvariant();

        if (lower == "db" || lower == "dw" || lower == "dd" || lower == "dq")
            return ConvertData(lower, rest, names);

        foreach (ModuleInfo m in t.GetModules())
        {
            if (rest.IndexOf(m.Name, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string hex = "0x" + m.Base.ToString("X");
                rest = Regex.Replace(rest, Regex.Escape(m.Name), x => hex, RegexOptions.IgnoreCase);
            }
        }

        rest = Regex.Replace(rest, @"\bptr\b", "", RegexOptions.IgnoreCase);
        rest = HexToken.Replace(rest, m => names.Contains(m.Value) ? m.Value : "0x" + m.Value);

        if (!t.Is32Bit)
            rest = AddRelative(rest);

        return (op + " " + rest).Trim();
    }

    private static string ConvertData(string op, string rest, HashSet<string> names)
    {
        var parts = new List<string>();

        foreach (Match m in Regex.Matches(rest, "\"[^\"]*\"|[^\\s,]+"))
        {
            string token = m.Value;

            if (token.StartsWith("\"", StringComparison.Ordinal))
            {
                parts.Add(token);
                continue;
            }

            if (token.StartsWith("(float)", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(7);
            else if (token.StartsWith("(double)", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(8);
            else if (!names.Contains(token) && Regex.IsMatch(token, "^[0-9A-Fa-f]+$"))
                token = "0x" + token;

            parts.Add(token);
        }

        return op + " " + string.Join(", ", parts);
    }

    /// <summary>Di mode 64-bit, [alamat-konstan] dibuat RIP-relatif: [rel alamat].</summary>
    private static string AddRelative(string text)
    {
        return Regex.Replace(text, @"\[([^\]]+)\]", m =>
        {
            string inner = m.Groups[1].Value;

            if (RegisterToken.IsMatch(inner) ||
                inner.TrimStart().StartsWith("rel ", StringComparison.OrdinalIgnoreCase) ||
                inner.TrimStart().StartsWith("abs ", StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }

            return "[rel " + inner.Trim() + "]";
        });
    }
}
