using System;
using System.Text;
using System.Windows.Forms;
using RFTool.Advanced;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // NILAI LIVE (REAL TIME) DI ADDRESS MANAGER
    // Kolom "Value" = nilai yang sedang ada di memori game.
    // Kolom "Target" = nilai tujuan untuk WRITE / FREEZE.
    // =========================================================

    private System.Windows.Forms.Timer? addressLiveTimer;

    private void EnsureAddressLiveTimer()
    {
        if (addressLiveTimer != null)
            return;

        addressLiveTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };

        addressLiveTimer.Tick += (_, _) => UpdateAddressLiveValues();
        addressLiveTimer.Start();
    }

    private void UpdateAddressLiveValues()
    {
        if (addressGrid == null ||
            addressGrid.IsDisposed ||
            !addressGrid.Visible ||
            addressGrid.Columns["Live"] == null)
        {
            return;
        }

        int count = Math.Min(addressGrid.Rows.Count, addressManager.Entries.Count);

        if (count == 0)
            return;

        // Hanya baris yang terlihat di layar yang dibaca
        int first = Math.Max(0, addressGrid.FirstDisplayedScrollingRowIndex);
        int last = Math.Min(count - 1, first + addressGrid.DisplayedRowCount(true));

        TargetProcess? target = AdvSvc.Target.Get();

        for (int i = first; i <= last; i++)
        {
            AddressEntry entry = addressManager.Entries[i];

            string text = entry.IsScript
                ? (AdvSvc.IsAaActive(entry.ScriptKey) ? "ENABLED" : "disabled")
                : target == null
                    ? "??"
                    : ReadLiveValue(target, entry);

            DataGridViewCell cell = addressGrid.Rows[i].Cells["Live"];

            if (!Equals(cell.Value, text))
                cell.Value = text;
        }
    }

    private static string ReadLiveValue(TargetProcess target, AddressEntry entry)
    {
        try
        {
            if (!EntryIO.TryResolve(target, entry, out ulong address, out _))
                return "??";

            ScanValueType type = ValueCodec.TypeFromName(entry.ValueType);

            if (type == ScanValueType.String)
                return ReadLiveString(target, address, entry);

            byte[]? data = target.ReadBytes(address, EntryIO.ValueSize(entry));

            if (data == null)
                return "??";

            return ValueCodec.Format(data, type, false, entry.Signed, entry.Hex);
        }
        catch
        {
            return "??";
        }
    }

    // Panjang string tidak diketahui (Value hanya teks target), jadi baca blok
    // tetap lalu potong di karakter NUL. Cocok untuk Nickname.
    private static string ReadLiveString(TargetProcess target, ulong address, AddressEntry entry)
    {
        int want = Math.Clamp(Math.Max(entry.Value?.Length ?? 0, 64), 16, 256);

        byte[]? data = null;

        // Kalau blok besar melewati batas halaman memori, coba lebih kecil
        for (int size = want; size >= 4 && data == null; size /= 2)
            data = target.ReadBytes(address, size);

        if (data == null)
            return "??";

        // Deteksi UTF-16 (huruf, 00, huruf, 00) otomatis
        bool utf16 = data.Length >= 4 &&
                     data[0] != 0 && data[1] == 0 &&
                     data[2] != 0 && data[3] == 0;

        string text = utf16
            ? Encoding.Unicode.GetString(data)
            : Encoding.Latin1.GetString(data);

        int nul = text.IndexOf('\0');

        return nul >= 0 ? text.Substring(0, nul) : text;
    }
}
