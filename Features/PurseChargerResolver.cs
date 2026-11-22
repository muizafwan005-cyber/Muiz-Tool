using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using RFTool.Core;
using RFTool.PrivateAdvance.Config;

namespace RFTool.Features;

// =============================================================
// PURSE CHARGER RESOLVER
//
// Struktur Purse Charger dikenali dari:
//   Base + 0x00 : FF 55 55 55 00 00 00 00   (pola AOB)
//   Base + 0x2C : B7 84 41 00               (marker)
//   Base + 0x30 : 55                        (marker)
//   Target      : Base + 0x08
//
// Pola AOB itu lemah (banyak salinan lama di heap), jadi:
//   1. Kalau base pointer sudah dipelajari -> pakai itu (tanpa scan).
//   2. Kalau tidak -> AOB, lalu cari base pointer statis di module
//      RF_Online.bin yang menunjuk ke struktur tersebut.
//      Salinan lama (stale) tidak punya pointer statis, jadi bisa
//      dibedakan dari struktur yang benar-benar hidup.
// =============================================================
internal sealed class PurseChargerResolver
{
    public const string Pattern = "FF 55 55 55 00 00 00 00";

    private static readonly byte[] HeaderBytes =
        { 0xFF, 0x55, 0x55, 0x55, 0x00, 0x00, 0x00, 0x00 };

    private static readonly byte[] MarkerBytes =
        { 0xB7, 0x84, 0x41, 0x00 };

    private const long MaxPointerDelta = 0x2000;
    private const long MaxPlayerDelta = 0x100000;
    private const string ModuleName = "RF_Online.bin";

    public sealed record StaticPointer(long ModuleOffset, long Value, long Delta);

    private sealed class Settings
    {
        public long ModuleOffset { get; set; }
        public long Offset { get; set; }
    }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MuizTool",
        "purse-charger-pointer.json");

    private readonly MemoryAccess memory;
    private readonly Func<long> playerOffset;

    public PurseChargerResolver(MemoryAccess memory, Func<long> playerOffset)
    {
        this.memory = memory;
        this.playerOffset = playerOffset;
    }

    // =========================================================
    // VALIDASI STRUKTUR
    // =========================================================

    public bool TryValidate(long candidate, out IntPtr target)
    {
        target = IntPtr.Zero;

        try
        {
            byte[] header = memory.ReadBytes(new IntPtr(candidate), 8);

            if (!header.SequenceEqual(HeaderBytes))
                return false;

            byte[] marker = memory.ReadBytes(new IntPtr(candidate + 0x2C), 4);

            if (!marker.SequenceEqual(MarkerBytes))
                return false;

            if (memory.ReadByte(new IntPtr(candidate + 0x30)) != 0x55)
                return false;

            IntPtr result = new IntPtr(candidate + 0x08);

            // Pastikan target dapat dibaca.
            memory.ReadBytes(result, 1);

            target = result;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string DescribeRaw(long candidate)
    {
        string marker;
        string marker30;

        try
        {
            marker = BitConverter.ToString(memory.ReadBytes(new IntPtr(candidate + 0x2C), 4)).Replace('-', ' ');
        }
        catch
        {
            marker = "??";
        }

        try
        {
            marker30 = memory.ReadByte(new IntPtr(candidate + 0x30)).ToString("X2");
        }
        catch
        {
            marker30 = "??";
        }

        return $"0x{candidate:X8}: +2C = {marker} | +30 = {marker30}";
    }

    // =========================================================
    // BASE POINTER YANG SUDAH DIPELAJARI
    // =========================================================

    public bool TryResolveLearned(out IntPtr target, out string info)
    {
        target = IntPtr.Zero;
        info = "";

        Settings? settings = LoadSettings();

        if (settings == null)
            return false;

        try
        {
            IntPtr pointer = memory.ReadPointer32(
                new IntPtr(memory.ModuleBase.ToInt64() + settings.ModuleOffset));

            if (pointer == IntPtr.Zero)
                return false;

            long resolved = pointer.ToInt64() + settings.Offset;

            if (!TryValidate(resolved - 0x08, out target))
                return false;

            info = $"via base pointer [{ModuleName}+{settings.ModuleOffset:X}]+{settings.Offset:X}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Settings? LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return null;

            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
        }
        catch
        {
            return null;
        }
    }

    private static void SaveSettings(StaticPointer pointer)
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsPath);

            if (string.IsNullOrWhiteSpace(directory))
                return;

            Directory.CreateDirectory(directory);

            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(
                    new Settings
                    {
                        ModuleOffset = pointer.ModuleOffset,
                        Offset = pointer.Delta
                    },
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Gagal menyimpan tidak boleh menggagalkan Initialize.
        }
    }

    // =========================================================
    // CARI BASE POINTER STATIS DI MODULE RF_Online.bin
    // Pola: [RF_Online.bin+X] = objek, target = objek + Delta
    // =========================================================

    public Dictionary<long, List<StaticPointer>> FindStaticPointers(IReadOnlyList<long> targets)
    {
        var result = new Dictionary<long, List<StaticPointer>>();

        foreach (long t in targets)
            result[t] = new List<StaticPointer>();

        if (result.Count == 0 || !TryGetModuleRange(out long start, out long size))
            return result;

        long[] list = result.Keys.ToArray();
        long minValue = list.Min() - MaxPointerDelta;
        long maxValue = list.Max();
        long moduleEnd = start + size;

        const int chunk = 0x10000;

        for (long offset = 0; offset < size; offset += chunk)
        {
            int length = (int)Math.Min(chunk, size - offset);
            length &= ~3;

            if (length <= 0)
                break;

            byte[] data;

            try
            {
                data = memory.ReadBytes(new IntPtr(start + offset), length);
            }
            catch
            {
                continue; // halaman tidak bisa dibaca
            }

            for (int i = 0; i + 4 <= length; i += 4)
            {
                uint raw = BitConverter.ToUInt32(data, i);

                if (raw < 0x10000)
                    continue;

                long value = raw;

                if (value < minValue || value > maxValue)
                    continue;

                if (value >= start && value < moduleEnd)
                    continue;

                foreach (long t in list)
                {
                    long delta = t - value;

                    if (delta < 0 || delta > MaxPointerDelta)
                        continue;

                    List<StaticPointer> found = result[t];

                    if (found.Count < 50)
                        found.Add(new StaticPointer(offset + i, value, delta));
                }
            }
        }

        foreach (List<StaticPointer> found in result.Values)
            found.Sort((a, b) => a.Delta.CompareTo(b.Delta));

        return result;
    }

    private bool TryGetModuleRange(out long start, out long size)
    {
        start = 0;
        size = 0;

        try
        {
            foreach (ProcessModule module in memory.Process.Modules)
            {
                if (module.ModuleName.Equals(ModuleName, StringComparison.OrdinalIgnoreCase))
                {
                    start = module.BaseAddress.ToInt64();
                    size = module.ModuleMemorySize;
                    return size > 0;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    // =========================================================
    // LAPORAN HUBUNGAN DENGAN BASE POINTER LAIN
    // =========================================================

    public string DescribeRelations(long target)
    {
        var parts = new List<string>();

        // Tab 1: [RF_Online.bin+C28B40] + offset
        try
        {
            long moduleBase = memory.ModuleBase.ToInt64();

            long player = memory.ReadPointer32(
                new IntPtr(moduleBase + playerOffset())).ToInt64();

            if (player != 0)
            {
                long delta = target - player;

                parts.Add(
                    Math.Abs(delta) <= MaxPlayerDelta
                        ? $"Tab 1: di dalam struktur Player (Player{Signed(delta)})"
                        : $"Tab 1: BUKAN bagian struktur Player (selisih {Signed(delta)})");
            }
        }
        catch
        {
            parts.Add("Tab 1: player pointer tidak terbaca");
        }

        // Tab 7: base pointer Private Advance
        try
        {
            BasePointerSettings settings = BasePointerStore.Load();

            if (settings.ModuleName.Equals(ModuleName, StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(settings.Offset, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long offset))
            {
                long baseObject = memory.ReadPointer32(
                    new IntPtr(memory.ModuleBase.ToInt64() + offset)).ToInt64();

                if (baseObject != 0)
                {
                    long delta = target - baseObject;

                    parts.Add(
                        Math.Abs(delta) <= MaxPlayerDelta
                            ? $"Tab 7: di dalam objek base [{ModuleName}+{settings.Offset}] (objek{Signed(delta)})"
                            : $"Tab 7: bukan bagian objek base [{ModuleName}+{settings.Offset}]");
                }
            }
        }
        catch
        {
        }

        return string.Join(" | ", parts);
    }

    private static string Signed(long value)
    {
        return value >= 0 ? $"+0x{value:X}" : $"-0x{-value:X}";
    }

    private static string DescribePointer(StaticPointer pointer)
    {
        return $"[{ModuleName}+{pointer.ModuleOffset:X}]+{pointer.Delta:X}";
    }

    // =========================================================
    // PELAJARI BASE POINTER (dipakai otomatis pada Initialize berikutnya)
    // =========================================================

    public string LearnFrom(IntPtr target, List<StaticPointer> pointers)
    {
        var parts = new List<string>();

        string relations = DescribeRelations(target.ToInt64());

        if (relations.Length > 0)
            parts.Add(relations);

        if (pointers.Count == 1)
        {
            SaveSettings(pointers[0]);
            parts.Add("base pointer dipelajari " + DescribePointer(pointers[0]));
        }
        else if (pointers.Count == 0)
        {
            parts.Add("tidak ada base pointer level-1 (<= 0x2000)");
        }
        else
        {
            parts.Add(
                pointers.Count + " kandidat base pointer: " +
                string.Join(", ", pointers.Take(4).Select(DescribePointer)));
        }

        return string.Join(" | ", parts);
    }

    // =========================================================
    // PESAN DIAGNOSA (saat kandidat valid bukan tepat 1)
    // =========================================================

    public string BuildDiagnostics(
        int hitCount,
        List<IntPtr> valid,
        List<string> rawSamples,
        Dictionary<long, List<StaticPointer>>? pointers)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $"PURSE CHARGER AOB: kandidat valid = {valid.Count} (hasil scan mentah = {hitCount})");

        if (valid.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Tidak ada kandidat yang lolos marker +0x2C / +0x30. Contoh hasil scan:");

            foreach (string sample in rawSamples)
                sb.AppendLine("  " + sample);

            sb.AppendLine();
            sb.AppendLine("Kemungkinan marker berubah setelah update game / karakter belum masuk dunia.");
        }
        else
        {
            sb.AppendLine();

            foreach (IntPtr target in valid.Take(8))
            {
                long address = target.ToInt64();
                sb.AppendLine($"Kandidat 0x{address:X8}");

                string relations = DescribeRelations(address);

                if (relations.Length > 0)
                    sb.AppendLine("  " + relations);

                if (pointers != null && pointers.TryGetValue(address, out List<StaticPointer>? found))
                {
                    sb.AppendLine(
                        found.Count == 0
                            ? "  tidak ada base pointer statis (kemungkinan salinan lama)"
                            : "  base pointer: " + string.Join(", ", found.Take(4).Select(DescribePointer)));
                }
            }

            sb.AppendLine();
            sb.AppendLine(
                "Lebih dari satu kandidat punya base pointer. " +
                "Coba lagi saat karakter sudah di dunia dan tidak sedang loading.");
        }

        return sb.ToString().TrimEnd();
    }
}
