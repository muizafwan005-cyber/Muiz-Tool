using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace RFTool.Advanced;

internal enum ScanMode
{
    Exact,
    Unknown,
    Increased,
    Decreased,
    Unchanged,
    Changed
}

internal sealed class ScanSpec
{
    public ScanValueType Type;
    public byte[]? Value;
    public bool Unicode;
    public ulong Start;
    public ulong Stop = ulong.MaxValue;
    public bool WritableOnly = true;
    public bool IncludeExecutable;
    public int Alignment = 1;
}

internal sealed class ScanLimitException : Exception
{
    public ScanLimitException(string message) : base(message)
    {
    }
}

/// <summary>
/// Mesin pencarian nilai: First Scan (Exact / Unknown Initial Value) dan
/// Next Scan (Exact / Increased / Decreased / Unchanged / Changed).
/// </summary>
internal sealed class MemoryScanner
{
    public const int MaxResults = 8_000_000;
    private const long MaxSnapshotBytes = 768L * 1024 * 1024;
    private const int ChunkSize = 4 * 1024 * 1024;

    private sealed class ResultSet
    {
        public ulong[] Addr = new ulong[1024];
        public byte[] Prev;
        public int Count;
        public readonly int Stride;

        public ResultSet(int stride)
        {
            Stride = stride;
            Prev = new byte[1024 * stride];
        }

        public void Add(ulong address, ReadOnlySpan<byte> value)
        {
            if (Count >= MaxResults)
            {
                throw new ScanLimitException(
                    "Hasil terlalu banyak (lebih dari " + MaxResults.ToString("N0") +
                    "). Persempit pencarian: pakai Exact Value, rentang alamat, atau opsi Writable.");
            }

            if (Count == Addr.Length)
            {
                int n = Addr.Length * 2;
                Array.Resize(ref Addr, n);
                Array.Resize(ref Prev, n * Stride);
            }

            Addr[Count] = address;
            value.CopyTo(new Span<byte>(Prev, Count * Stride, Stride));
            Count++;
        }

        public ReadOnlySpan<byte> ValueAt(int index)
        {
            return new ReadOnlySpan<byte>(Prev, index * Stride, Stride);
        }
    }

    private sealed class SnapChunk
    {
        public ulong Base;
        public byte[] Data = Array.Empty<byte>();
    }

    private sealed class State
    {
        public ResultSet? Results;
        public List<SnapChunk>? Snapshot;
        public ScanValueType Type;
        public int Size;
        public bool Unicode;
        public int Align;
        public int Pid;
    }

    private State? current;
    private State? undo;
    private bool hasUndo;

    public bool HasScan => current != null;
    public bool IsSnapshot => current?.Snapshot != null;
    public int ResultCount => current?.Results?.Count ?? 0;
    public ScanValueType Type => current?.Type ?? ScanValueType.Int32;
    public bool Unicode => current?.Unicode ?? false;
    public int ValueSize => current?.Size ?? 0;
    public bool CanUndo => hasUndo;

    public long SnapshotBytes
    {
        get
        {
            long total = 0;

            if (current?.Snapshot != null)
            {
                foreach (SnapChunk c in current.Snapshot)
                    total += c.Data.Length;
            }

            return total;
        }
    }

    public void Reset()
    {
        current = null;
        undo = null;
        hasUndo = false;
    }

    public void Undo()
    {
        if (!hasUndo)
            return;

        current = undo;
        undo = null;
        hasUndo = false;
    }

    public ulong AddressAt(int index)
    {
        return current!.Results!.Addr[index];
    }

    /// <summary>Menghapus baris hasil tertentu dari daftar (bisa di-Undo).</summary>
    public int RemoveIndices(IEnumerable<int> indices)
    {
        State? s = current;

        if (s?.Results == null)
            return 0;

        var drop = new HashSet<int>(indices);

        if (drop.Count == 0)
            return 0;

        ResultSet src = s.Results;
        var rs = new ResultSet(src.Stride);

        for (int i = 0; i < src.Count; i++)
        {
            if (!drop.Contains(i))
                rs.Add(src.Addr[i], src.ValueAt(i));
        }

        int removed = src.Count - rs.Count;

        Commit(new State
        {
            Results = rs,
            Type = s.Type,
            Size = s.Size,
            Unicode = s.Unicode,
            Align = s.Align,
            Pid = s.Pid
        });

        return removed;
    }

    public byte[] PreviousAt(int index)
    {
        return current!.Results!.ValueAt(index).ToArray();
    }

    private void Commit(State next)
    {
        undo = current;
        hasUndo = true;
        current = next;
    }

    // ---------------------------------------------------------
    // PILIH REGION
    // ---------------------------------------------------------

    private static List<AddrRange> SelectRanges(TargetProcess t, ScanSpec spec, out long total)
    {
        var list = new List<AddrRange>();
        total = 0;

        foreach (MemRegion r in t.EnumerateRegions(spec.Start, spec.Stop))
        {
            if (!r.IsReadable)
                continue;

            if (spec.WritableOnly && !r.IsWritable)
                continue;

            if (!spec.IncludeExecutable && r.IsExecutable)
                continue;

            ulong lo = Math.Max(r.Base, spec.Start);
            ulong hi = Math.Min(r.End, spec.Stop);

            if (hi <= lo)
                continue;

            list.Add(new AddrRange(lo, hi));
            total += (long)(hi - lo);
        }

        return list;
    }

    // ---------------------------------------------------------
    // FIRST SCAN
    // ---------------------------------------------------------

    public void FirstScan(
        TargetProcess t,
        ScanSpec spec,
        ScanMode mode,
        Action<int>? progress,
        CancellationToken ct)
    {
        int size = spec.Type == ScanValueType.String
            ? (spec.Value?.Length ?? 0)
            : ValueCodec.Size(spec.Type);

        if (size <= 0)
            throw new InvalidOperationException("Ukuran nilai tidak valid.");

        int align = Math.Max(1, spec.Alignment);
        List<AddrRange> ranges = SelectRanges(t, spec, out long total);

        long done = 0;
        int lastPercent = -1;

        void Tick(long amount)
        {
            done += amount;

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

        var next = new State
        {
            Type = spec.Type,
            Size = size,
            Unicode = spec.Unicode,
            Align = align,
            Pid = t.Id
        };

        if (mode == ScanMode.Exact)
        {
            if (spec.Value == null)
                throw new InvalidOperationException("Nilai pencarian belum diisi.");

            var rs = new ResultSet(size);
            byte[] buffer = new byte[ChunkSize];

            foreach (AddrRange r in ranges)
                ScanRangeExact(t, r, spec.Value, align, rs, buffer, Tick, ct);

            next.Results = rs;
        }
        else
        {
            if (spec.Type == ScanValueType.String)
                throw new InvalidOperationException("Unknown Initial Value tidak tersedia untuk String.");

            // Unknown Initial Value: simpan salinan memori, bandingkan di Next Scan.
            var chunks = new List<SnapChunk>();
            long bytes = 0;

            foreach (AddrRange r in ranges)
            {
                ulong pos = r.Lo;

                while (pos < r.Hi)
                {
                    ct.ThrowIfCancellationRequested();

                    int want = (int)Math.Min((ulong)ChunkSize, r.Hi - pos);
                    byte[] data = new byte[want];
                    t.TryRead(pos, data, want, out int got);

                    if (got >= size)
                    {
                        if (got < want)
                            Array.Resize(ref data, got);

                        chunks.Add(new SnapChunk { Base = pos, Data = data });
                        bytes += got;

                        if (bytes > MaxSnapshotBytes)
                        {
                            throw new ScanLimitException(
                                "Memori terlalu besar untuk Unknown Initial Value (batas " +
                                (MaxSnapshotBytes / (1024 * 1024)) +
                                " MB). Persempit rentang alamat atau pilih Writable saja.");
                        }
                    }

                    pos += (ulong)want;
                    Tick(want);
                }
            }

            next.Snapshot = chunks;
        }

        progress?.Invoke(100);
        Commit(next);
    }

    private static void ScanRangeExact(
        TargetProcess t,
        AddrRange range,
        byte[] pattern,
        int align,
        ResultSet rs,
        byte[] buffer,
        Action<long> tick,
        CancellationToken ct)
    {
        int size = pattern.Length;
        ulong pos = range.Lo;

        while (pos + (ulong)size <= range.Hi)
        {
            ct.ThrowIfCancellationRequested();

            int want = (int)Math.Min((ulong)buffer.Length, range.Hi - pos);
            t.TryRead(pos, buffer, want, out int got);

            if (got < size)
            {
                ulong skip = 4096UL - (pos % 4096UL);
                tick((long)skip);
                pos += skip;
                continue;
            }

            ScanBufferExact(buffer, got, pos, pattern, align, rs);

            int step = got - size + 1;
            tick(step);
            pos += (ulong)step;
        }
    }

    private static void ScanBufferExact(
        byte[] buffer,
        int length,
        ulong basePos,
        byte[] pattern,
        int align,
        ResultSet rs)
    {
        int size = pattern.Length;
        int last = length - size;

        if (last < 0)
            return;

        var span = new ReadOnlySpan<byte>(buffer, 0, length);
        int first = (int)(((ulong)align - (basePos % (ulong)align)) % (ulong)align);

        switch (size)
        {
            case 1:
            {
                byte v = pattern[0];

                for (int i = first; i <= last; i += align)
                {
                    if (span[i] == v)
                        rs.Add(basePos + (ulong)i, span.Slice(i, 1));
                }

                break;
            }

            case 2:
            {
                ushort v = BitConverter.ToUInt16(pattern, 0);

                for (int i = first; i <= last; i += align)
                {
                    if (MemoryMarshal.Read<ushort>(span.Slice(i)) == v)
                        rs.Add(basePos + (ulong)i, span.Slice(i, 2));
                }

                break;
            }

            case 4:
            {
                uint v = BitConverter.ToUInt32(pattern, 0);

                for (int i = first; i <= last; i += align)
                {
                    if (MemoryMarshal.Read<uint>(span.Slice(i)) == v)
                        rs.Add(basePos + (ulong)i, span.Slice(i, 4));
                }

                break;
            }

            case 8:
            {
                ulong v = BitConverter.ToUInt64(pattern, 0);

                for (int i = first; i <= last; i += align)
                {
                    if (MemoryMarshal.Read<ulong>(span.Slice(i)) == v)
                        rs.Add(basePos + (ulong)i, span.Slice(i, 8));
                }

                break;
            }

            default:
            {
                var pat = new ReadOnlySpan<byte>(pattern);

                if (align == 1)
                {
                    int idx = 0;

                    while (idx <= last)
                    {
                        int f = span.Slice(idx, length - idx).IndexOf(pat);

                        if (f < 0)
                            break;

                        int at = idx + f;

                        if (at > last)
                            break;

                        rs.Add(basePos + (ulong)at, span.Slice(at, size));
                        idx = at + 1;
                    }
                }
                else
                {
                    for (int i = first; i <= last; i += align)
                    {
                        if (span.Slice(i, size).SequenceEqual(pat))
                            rs.Add(basePos + (ulong)i, span.Slice(i, size));
                    }
                }

                break;
            }
        }
    }

    // ---------------------------------------------------------
    // NEXT SCAN
    // ---------------------------------------------------------

    private static bool Match(
        ScanMode mode,
        ScanValueType type,
        ReadOnlySpan<byte> cur,
        ReadOnlySpan<byte> old,
        byte[]? exact)
    {
        switch (mode)
        {
            case ScanMode.Exact:
                return exact != null && cur.SequenceEqual(new ReadOnlySpan<byte>(exact));

            case ScanMode.Unchanged:
                return cur.SequenceEqual(old);

            case ScanMode.Changed:
                return !cur.SequenceEqual(old);

            case ScanMode.Increased:
                return ValueCodec.Compare(cur, old, type) > 0;

            case ScanMode.Decreased:
                return ValueCodec.Compare(cur, old, type) < 0;

            default:
                return false;
        }
    }

    public void NextScan(
        TargetProcess t,
        ScanMode mode,
        byte[]? value,
        Action<int>? progress,
        CancellationToken ct)
    {
        State? state = current;

        if (state == null)
            throw new InvalidOperationException("Belum ada First Scan.");

        if (state.Pid != t.Id)
            throw new InvalidOperationException("Process berubah sejak First Scan. Klik New Scan.");

        if (mode == ScanMode.Unknown)
            throw new InvalidOperationException("Mode tidak valid untuk Next Scan.");

        if (mode == ScanMode.Exact && value == null)
            throw new InvalidOperationException("Nilai pencarian belum diisi.");

        int size = state.Size;
        var rs = new ResultSet(size);

        if (state.Snapshot != null)
        {
            List<SnapChunk> chunks = state.Snapshot;
            byte[] buffer = new byte[ChunkSize];
            int n = 0;

            foreach (SnapChunk c in chunks)
            {
                ct.ThrowIfCancellationRequested();

                int len = c.Data.Length;
                t.TryRead(c.Base, buffer, len, out int got);
                int readable = Math.Min(got, len);

                if (readable >= size)
                {
                    int first = (int)(((ulong)state.Align - (c.Base % (ulong)state.Align)) % (ulong)state.Align);

                    for (int i = first; i + size <= readable; i += state.Align)
                    {
                        var cur = new ReadOnlySpan<byte>(buffer, i, size);
                        var old = new ReadOnlySpan<byte>(c.Data, i, size);

                        if (Match(mode, state.Type, cur, old, value))
                            rs.Add(c.Base + (ulong)i, cur);
                    }
                }

                n++;

                if (progress != null)
                    progress((int)(n * 100L / chunks.Count));
            }
        }
        else
        {
            ResultSet src = state.Results!;
            byte[] window = new byte[65536];
            byte[] single = new byte[Math.Max(size, 8)];
            int i = 0;

            while (i < src.Count)
            {
                if ((i & 0x3FF) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Invoke((int)(i * 100L / Math.Max(1, src.Count)));
                }

                ulong address = src.Addr[i];

                // Kumpulkan hasil yang berdekatan agar dibaca dengan satu panggilan.
                int j = i;

                while (j + 1 < src.Count &&
                       src.Addr[j + 1] + (ulong)size <= address + (ulong)window.Length)
                {
                    j++;
                }

                int span = (int)(src.Addr[j] + (ulong)size - address);
                bool okWindow = t.TryRead(address, window, span, out int got) && got >= span;

                if (okWindow)
                {
                    for (int k = i; k <= j; k++)
                    {
                        int off = (int)(src.Addr[k] - address);
                        var cur = new ReadOnlySpan<byte>(window, off, size);

                        if (Match(mode, state.Type, cur, src.ValueAt(k), value))
                            rs.Add(src.Addr[k], cur);
                    }
                }
                else
                {
                    for (int k = i; k <= j; k++)
                    {
                        if (!t.TryRead(src.Addr[k], single, size, out int g) || g < size)
                            continue;

                        var cur = new ReadOnlySpan<byte>(single, 0, size);

                        if (Match(mode, state.Type, cur, src.ValueAt(k), value))
                            rs.Add(src.Addr[k], cur);
                    }
                }

                i = j + 1;
            }
        }

        progress?.Invoke(100);

        Commit(new State
        {
            Results = rs,
            Type = state.Type,
            Size = state.Size,
            Unicode = state.Unicode,
            Align = state.Align,
            Pid = state.Pid
        });
    }
}
