using System;
using System.Collections.Generic;

namespace RFTool.Advanced;

/// <summary>Log pemeriksaan bersama untuk semua modul Advanced.</summary>
internal static class DebugLog
{
    private static readonly List<string> lines = new List<string>();
    private static readonly object gate = new object();

    /// <summary>Dipanggil dari thread mana pun. UI wajib marshal ke UI thread.</summary>
    public static event Action<string>? LineAdded;

    public static void Info(string message) => Add("INFO ", message);

    public static void Warn(string message) => Add("WARN ", message);

    public static void Error(string message) => Add("ERROR", message);

    private static void Add(string level, string message)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message;

        lock (gate)
        {
            lines.Add(line);

            if (lines.Count > 3000)
                lines.RemoveRange(0, 1000);
        }

        LineAdded?.Invoke(line);
    }

    public static string[] Snapshot()
    {
        lock (gate)
        {
            return lines.ToArray();
        }
    }

    public static void Clear()
    {
        lock (gate)
        {
            lines.Clear();
        }
    }
}
