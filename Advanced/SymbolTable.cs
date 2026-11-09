using System;
using System.Collections.Generic;

namespace RFTool.Advanced;

/// <summary>
/// Simbol terdaftar (registersymbol / registerSymbol). Bisa dipakai di kolom Address,
/// Memory Viewer, Code Analyzer, dan semua ekspresi alamat.
/// </summary>
internal static class SymbolTable
{
    private static readonly Dictionary<string, ulong> map =
        new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

    private static readonly object gate = new object();

    public static void Set(string name, ulong address)
    {
        lock (gate)
        {
            map[name] = address;
        }
    }

    public static bool TryGet(string name, out ulong address)
    {
        lock (gate)
        {
            return map.TryGetValue(name, out address);
        }
    }

    public static void Remove(string name)
    {
        lock (gate)
        {
            map.Remove(name);
        }
    }

    public static void Clear()
    {
        lock (gate)
        {
            map.Clear();
        }
    }

    public static KeyValuePair<string, ulong>[] Snapshot()
    {
        lock (gate)
        {
            var list = new KeyValuePair<string, ulong>[map.Count];
            int i = 0;

            foreach (KeyValuePair<string, ulong> kv in map)
                list[i++] = kv;

            return list;
        }
    }
}
