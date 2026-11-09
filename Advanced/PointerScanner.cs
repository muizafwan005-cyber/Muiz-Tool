using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace RFTool.Advanced;

/// <summary>
/// Satu jalur pointer: alamat static (module+offset) lalu rantai offset menuju target.
/// Ekspresi: [[base]+o1]+o2 ... (dibaca dari dalam ke luar).
/// </summary>
internal sealed class PointerPath
{
    public string BaseText { get; }
    public ulong BaseAddress { get; }
    public int[] Offsets { get; }

    public PointerPath(string baseText, ulong baseAddress, int[] offsets)
    {
        BaseText = baseText;
        BaseAddress = baseAddress;
        Offsets = offsets;
    }

    public int Depth => Offsets.Length;

    public string Expression
    {
        get
        {
            string s = BaseText;

            foreach (int offset in Offsets)
                s = offset == 0 ? "[" + s + "]" : "[" + s + "]+" + offset.ToString("X");

            return s;
        }
    }

    public string OffsetsText
    {
        get { return string.Join(", ", Offsets.Select(o => "+" + o.ToString("X"))); }
    }
}

internal sealed class PointerScanOptions
{
    public int MaxDepth = 3;
    public int MaxOffset = 0x400;
    public int MaxResults = 500;
    public int MaxNodesPerLevel = 150000;
}

internal sealed class PointerScanResult
{
    public List<PointerPath> Paths = new List<PointerPath>();
    public bool Truncated;
    public string Note = "";
}

/// <summary>
/// Pointer scan bertingkat. Tiap level memindai seluruh memori mencari nilai yang
/// menunjuk (dengan selisih paling besar MaxOffset) ke alamat-alamat level sebelumnya.
/// Jalur berhenti saat alamat penyimpan pointer berada di dalam module (alamat static).
/// </summary>
internal static class PointerScanner
{
    private const int ChunkSize = 4 * 1024 * 1024;

    private struct Node
    {
        public ulong Address;
        public int Parent;
        public int Offset;
    }

    private struct Hit
    {
        public ulong Location;
        public int ParentNode;
        public int Offset;
    }

    public static PointerScanResult Scan(
        TargetProcess t,
        ulong target,
        PointerScanOptions o,
        Action<string, int>? progress,
        CancellationToken ct)
    {
        var result = new PointerScanResult();
        var nodes = new List<Node> { new Node { Address = target, Parent = -1, Offset = 0 } };
        var frontier = new List<int> { 0 };
        var seen = new HashSet<string>();
        int ptr = t.PointerSize;

        var ranges = new List<AddrRange>();
        long total = 0;

        foreach (MemRegion r in t.EnumerateRegions(0x10000, t.MaxAddress))
        {
            if (!r.IsReadable)
                continue;

            if (r.IsExecutable && !r.IsWritable)
                continue;

            ranges.Add(new AddrRange(r.Base, r.End));
            total += (long)(r.End - r.Base);
        }

        if (total <= 0)
        {
            result.Note = "Tidak ada region memori yang bisa dibaca.";
            return result;
        }

        byte[] buffer = new byte[ChunkSize];

        for (int level = 1; level <= o.MaxDepth && frontier.Count > 0; level++)
        {
            ct.ThrowIfCancellationRequested();

            List<int> ordered = frontier.OrderBy(i => nodes[i].Address).ToList();
            var tAddr = new List<ulong>();
            var tNode = new List<int>();

            foreach (int i in ordered)
            {
                ulong a = nodes[i].Address;

                if (tAddr.Count == 0 || tAddr[tAddr.Count - 1] != a)
                {
                    tAddr.Add(a);
                    tNode.Add(i);
                }
            }

            ulong[] ta = tAddr.ToArray();
            int[] tn = tNode.ToArray();

            var hits = new List<Hit>();
            bool capped = false;
            long done = 0;
            int lastPercent = -1;

            foreach (AddrRange range in ranges)
            {
                ulong pos = range.Lo;

                while (pos + (ulong)ptr <= range.Hi)
                {
                    ct.ThrowIfCancellationRequested();

                    int want = (int)Math.Min((ulong)buffer.Length, range.Hi - pos);
                    t.TryRead(pos, buffer, want, out int got);

                    if (got < ptr)
                    {
                        ulong skip = 4096UL - (pos % 4096UL);
                        pos += skip;
                        done += (long)skip;
                        continue;
                    }

                    if (!ScanChunk(buffer, got, pos, ptr, ta, tn, o.MaxOffset, hits, o.MaxNodesPerLevel))
                    {
                        capped = true;
                        break;
                    }

                    int step = got - ptr + 1;
                    pos += (ulong)step;
                    done += step;

                    if (progress != null)
                    {
                        int within = (int)Math.Min(100L, done * 100L / total);
                        int percent = ((level - 1) * 100 + within) / o.MaxDepth;

                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress("Level " + level + "/" + o.MaxDepth + ": memindai memori... (" + hits.Count + " kandidat)", percent);
                        }
                    }
                }

                if (capped)
                    break;
            }

            if (capped)
            {
                result.Truncated = true;
                result.Note = "Level " + level + " dibatasi " + o.MaxNodesPerLevel.ToString("N0") +
                              " kandidat. Kecilkan Max offset atau Depth untuk hasil lengkap.";
            }

            var next = new List<int>();
            var nextSeen = new HashSet<ulong>();

            foreach (Hit h in hits)
            {
                int index = nodes.Count;
                nodes.Add(new Node { Address = h.Location, Parent = h.ParentNode, Offset = h.Offset });

                ModuleInfo? m = t.FindModule(h.Location);

                if (m != null)
                {
                    var offsets = new List<int>();
                    int cur = index;

                    while (cur != 0)
                    {
                        offsets.Add(nodes[cur].Offset);
                        cur = nodes[cur].Parent;
                    }

                    string baseText = m.Name + "+" + (h.Location - m.Base).ToString("X");
                    var path = new PointerPath(baseText, h.Location, offsets.ToArray());

                    if (seen.Add(path.Expression))
                    {
                        result.Paths.Add(path);

                        if (result.Paths.Count >= o.MaxResults)
                        {
                            result.Truncated = true;
                            result.Note = "Berhenti di " + o.MaxResults + " hasil (batas Max results).";
                            break;
                        }
                    }
                }
                else if (level < o.MaxDepth && nextSeen.Add(h.Location))
                {
                    next.Add(index);
                }
            }

            frontier = next;

            if (result.Paths.Count >= o.MaxResults)
                break;
        }

        result.Paths = result.Paths
            .OrderBy(p => p.Depth)
            .ThenBy(p => p.Offsets.Select(x => (long)x).Sum())
            .ToList();

        progress?.Invoke("Selesai.", 100);
        return result;
    }

    private static bool ScanChunk(
        byte[] buffer,
        int length,
        ulong basePos,
        int ptr,
        ulong[] ta,
        int[] tn,
        int maxOffset,
        List<Hit> hits,
        int cap)
    {
        int first = (int)(((ulong)ptr - (basePos % (ulong)ptr)) % (ulong)ptr);
        var span = new ReadOnlySpan<byte>(buffer, 0, length);

        ulong minT = ta[0];
        ulong maxT = ta[ta.Length - 1];
        ulong low = minT > (ulong)maxOffset ? minT - (ulong)maxOffset : 0;

        for (int i = first; i + ptr <= length; i += ptr)
        {
            ulong v = ptr == 4
                ? MemoryMarshal.Read<uint>(span.Slice(i))
                : MemoryMarshal.Read<ulong>(span.Slice(i));

            if (v < low || v > maxT)
                continue;

            int lo = 0;
            int hi = ta.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;

                if (ta[mid] < v)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            if (lo >= ta.Length)
                continue;

            ulong diff = ta[lo] - v;

            if (diff > (ulong)maxOffset)
                continue;

            if (hits.Count >= cap)
                return false;

            hits.Add(new Hit
            {
                Location = basePos + (ulong)i,
                ParentNode = tn[lo],
                Offset = (int)diff
            });
        }

        return true;
    }
}
