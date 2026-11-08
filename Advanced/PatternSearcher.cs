using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace RFTool.Advanced;

/// <summary>Pola byte (AOB) dengan wildcard ?? atau ?.</summary>
internal sealed class AobPattern
{
    public byte[] Bytes { get; }
    public bool[] Mask { get; }
    public int Anchor { get; }

    public int Length => Bytes.Length;

    private AobPattern(byte[] bytes, bool[] mask, int anchor)
    {
        Bytes = bytes;
        Mask = mask;
        Anchor = anchor;
    }

    public static bool TryParse(string? text, out AobPattern? pattern, out string error)
    {
        pattern = null;
        error = "";

        var bytes = new List<byte>();
        var mask = new List<bool>();

        string[] tokens = (text ?? "").Split(
            new[] { ' ', '\t', '\r', '\n', ',' },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            if (token == "?" || token == "??" || token == "*")
            {
                bytes.Add(0);
                mask.Add(false);
                continue;
            }

            if (token.Length % 2 != 0)
            {
                error = "Token '" + token + "' tidak valid (pakai 2 digit hex atau ??).";
                return false;
            }

            for (int k = 0; k < token.Length; k += 2)
            {
                string pair = token.Substring(k, 2);

                if (pair == "??")
                {
                    bytes.Add(0);
                    mask.Add(false);
                }
                else if (byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    bytes.Add(b);
                    mask.Add(true);
                }
                else
                {
                    error = "Byte '" + pair + "' bukan hex yang valid.";
                    return false;
                }
            }
        }

        if (bytes.Count == 0)
        {
            error = "Pattern masih kosong.";
            return false;
        }

        int anchor = mask.IndexOf(true);

        if (anchor < 0)
        {
            error = "Pattern tidak boleh seluruhnya wildcard.";
            return false;
        }

        pattern = new AobPattern(bytes.ToArray(), mask.ToArray(), anchor);
        return true;
    }

    public override string ToString()
    {
        var parts = new string[Bytes.Length];

        for (int k = 0; k < parts.Length; k++)
            parts[k] = Mask[k] ? Bytes[k].ToString("X2", CultureInfo.InvariantCulture) : "??";

        return string.Join(" ", parts);
    }
}

internal static class PatternSearcher
{
    private const int ChunkSize = 4 * 1024 * 1024;

    /// <summary>
    /// Mencari pola di semua region yang bisa dibaca dalam [start, stop).
    /// onMatch dipanggil untuk setiap hasil; return false untuk berhenti.
    /// </summary>
    public static void Search(
        TargetProcess target,
        AobPattern pattern,
        ulong start,
        ulong stop,
        Func<MemRegion, bool>? regionFilter,
        Func<ulong, bool> onMatch,
        Action<int>? progress,
        CancellationToken ct)
    {
        int len = pattern.Length;
        var ranges = new List<AddrRange>();
        long total = 0;

        foreach (MemRegion r in target.EnumerateRegions(start, stop))
        {
            if (!r.IsReadable)
                continue;

            if (regionFilter != null && !regionFilter(r))
                continue;

            ulong lo = Math.Max(r.Base, start);
            ulong hi = Math.Min(r.End, stop);

            if (hi <= lo)
                continue;

            ranges.Add(new AddrRange(lo, hi));
            total += (long)(hi - lo);
        }

        byte[] buffer = new byte[ChunkSize];
        long done = 0;
        int lastPercent = -1;

        foreach (AddrRange range in ranges)
        {
            ulong pos = range.Lo;

            while (pos + (ulong)len <= range.Hi)
            {
                ct.ThrowIfCancellationRequested();

                int want = (int)Math.Min((ulong)buffer.Length, range.Hi - pos);
                target.TryRead(pos, buffer, want, out int got);

                if (got < len)
                {
                    ulong skip = 4096UL - (pos % 4096UL);
                    pos += skip;
                    done += (long)skip;
                    continue;
                }

                if (!ScanBuffer(buffer, got, pos, pattern, onMatch))
                    return;

                int step = got - len + 1;
                pos += (ulong)step;
                done += step;

                if (progress != null && total > 0)
                {
                    int percent = (int)Math.Min(100L, done * 100L / total);

                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress(percent);
                    }
                }
            }
        }

        progress?.Invoke(100);
    }

    private static bool ScanBuffer(
        byte[] buffer,
        int length,
        ulong basePosition,
        AobPattern pattern,
        Func<ulong, bool> onMatch)
    {
        int len = pattern.Length;
        int last = length - len;

        if (last < 0)
            return true;

        var span = new ReadOnlySpan<byte>(buffer, 0, length);
        byte anchorByte = pattern.Bytes[pattern.Anchor];
        int idx = 0;

        while (idx <= last)
        {
            int f = span.Slice(idx + pattern.Anchor, last - idx + 1).IndexOf(anchorByte);

            if (f < 0)
                break;

            int at = idx + f;
            bool match = true;

            for (int j = 0; j < len; j++)
            {
                if (pattern.Mask[j] && span[at + j] != pattern.Bytes[j])
                {
                    match = false;
                    break;
                }
            }

            if (match && !onMatch(basePosition + (ulong)at))
                return false;

            idx = at + 1;
        }

        return true;
    }
}
